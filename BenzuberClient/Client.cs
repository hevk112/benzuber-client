using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Benzuber.Api;
using Benzuber.Api.Models;
using Benzuber.Extenisions;
using Benzuber.Helpers;
using ProjectSummer.Repository;

namespace Benzuber
{
    public class Client
    {
        #region Polling timing configuration 
        private readonly TimeSpan _informationPollingInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _ordersToSetPollingInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _ordersToCancelPollingInterval = TimeSpan.FromSeconds(10);
        #endregion

        #region Readonly fields
        private readonly Configuration _configuration;
        private readonly Logger _logger;
        private readonly RestApi _api;
        #endregion

        private TransactionInformation _currentOrderToSet;
        private TransactionInformation _lastOrderToSet;
        private StationInformation _stationInformation;
        private IList<PriceInfo> _pricesInfo;

        public Func<TransactionInformation, long> OrderToSetReceivedFunc { get; set; }
        public Action<TransactionInformation> OrderToCancelReceivedFunc { get; set; }

        public Client(Configuration configuration)
        {
            _configuration = configuration;
            _api = new RestApi(_configuration, new SignHelper(_configuration));
            _logger = new Logger(GetType().Name, _configuration.LogLevel);
            SetThreadPoolConfiguration();
        }

        public void SetPumpState(PumpState pumpState)
        {
            _stationInformation.SetPumpState(pumpState);
        }

        public bool SetPrice(IList<PriceInfo> pricesInfo)
        {
            return TryDoRequest(() => _api.SetPrices(pricesInfo), nameof(SetPrice));
        }
        public bool SetFilled(string rrn, decimal fillingOverAmount)
        {
            if (fillingOverAmount < 0)
                throw new ArgumentOutOfRangeException(nameof(fillingOverAmount), "Значение не должно быть меньше, чем 0");

            if (_currentOrderToSet?.TransactionID == rrn)
                throw new ArgumentOutOfRangeException(nameof(rrn),
                    "Не допускается вызывать SetFilled, для транзакций, для которых в данный момент выполняется OrderToSetReceivedFunc");

            return TryDoRequest(() => _api.OrderIsFilled(rrn, fillingOverAmount), nameof(SetFilled));
        }

        private bool TryDoRequest(Func<Results> func, string description)
        {
            try
            {
                func().ThrowWhenNotOk(description);
                return true;
            }
            catch (Exception e)
            {
                _logger.Error($"{description} error: {e.Message}");
                return false;
            }
        }

        public Task StartPolling(
            StationInformation initialState,
            IEnumerable<PriceInfo> initialPricesInfo,
            CancellationToken cancellationToken)
        {
            _stationInformation = initialState;
            _pricesInfo = initialPricesInfo.ToList();
            var result = Polling(cancellationToken);
            _logger.Info("Starting polling...");

            return result;
        }

        private async Task Polling(CancellationToken cancellationToken)
        {
            
            var authRequiredEvent = new ManualResetEvent(true);
            var pollingReadyEvent = new ManualResetEvent(false);

            var authPolling = Task.Run(
                () => AuthPolling(cancellationToken, authRequiredEvent, pollingReadyEvent),
                cancellationToken);

            var informationPollingHelper = new PollingHelper(
                new Logger("StationInformationPolling", _configuration.LogLevel),
                StationInformation,
                pollingReadyEvent,
                authRequiredEvent);

            var ordersToSetPollingHelper = new PollingHelper(
                new Logger("OrdersToSetPolling", _configuration.LogLevel), 
                OrdersToSetPolling, 
                pollingReadyEvent,
                authRequiredEvent);

            var ordersToCancelPollingHelper = new PollingHelper(
                new Logger("OrdersToCancelPolling", _configuration.LogLevel),
                OrdersToCancelPolling,
                pollingReadyEvent,
                authRequiredEvent);
            
            await Task
                .WhenAll(
                    authPolling,
                    informationPollingHelper.Start(_informationPollingInterval, cancellationToken),
                    ordersToSetPollingHelper.Start(_ordersToSetPollingInterval, cancellationToken),
                    ordersToCancelPollingHelper.Start(_ordersToCancelPollingInterval, cancellationToken))
                .ConfigureAwait(false);
        }

        private static void SetThreadPoolConfiguration()
        {
            ThreadPool.GetMaxThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(workerThreads, completionPortThreads);
        }

        private async Task AuthPolling(
            CancellationToken cancellationToken,
            ManualResetEvent authRequiredEvent,
            ManualResetEvent pollingReadyEvent)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.Info("Try Auth on server");

                    var authorize = _api.Authorize();
                    if (authorize.Result == Results.NeedLoad_Sign && _api.SetSignKey() == Results.OK)
                        authorize = _api.Authorize();

                    authorize.Result.ThrowWhenNotOk("Authorize");

                    _api.SetPrices(_pricesInfo)
                        .ThrowWhenNotOk("SetPrices");

                    authRequiredEvent.Reset();
                    pollingReadyEvent.Set();
                }
                catch (Exception e)
                {
                    _logger.Error($"Auth Failed: {e.Message}");
                    _logger.Info("Going to sleep for 1 minute");
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
                }

                authRequiredEvent.WaitOne();
            }

            pollingReadyEvent.Set();
        }

        private void StationInformation()
        {
            _api.SetStationInformation(_stationInformation)
                .ThrowWhenNotOk("SetStationInformation");
        }

        private void OrdersToSetPolling()
        {
            var orders = _api.GetOrders();
            if (!orders.Any())
                return;

            foreach (var order in orders)
            {
                _api.AcceptOrder(order.TransactionID)
                    .ThrowWhenNotOk("AcceptOrder");

                var pumpRrn = OnOrderToSetReceived(order);

                if (pumpRrn.HasValue && pumpRrn.Value > 0)
                {
                    _api.OrderIsFilling(order.TransactionID, pumpRrn.Value);
                }
                else
                {
                    _api.OrderIsFilled(order.TransactionID, 0M)
                        .ThrowWhenNotOk("OrderIsFilled");
                }
            }
        }

        private void OrdersToCancelPolling()
        {
            var orders = _api.ToCancelOrders();
            if (!orders.Any())
                return;

            foreach (var order in orders)
            {
                if (_lastOrderToSet.DateTime >= order.DateTime)
                    OnOrderToCancelReceived(order);
            }
        }
        
        private long? OnOrderToSetReceived(TransactionInformation transactionInformation)
        {
            try
            {
                _logger.Info($"Send order set request to control system. TransactionID: {transactionInformation.TransactionID}");

                _currentOrderToSet = transactionInformation;

                return OrderToSetReceivedFunc?.Invoke(transactionInformation);
            }
            catch (Exception ex)
            {
                _logger.Error($"OnOrderToSetReceived(TransactionID:{transactionInformation.TransactionID}) Exception: {ex}");
            }
            finally
            {

                _lastOrderToSet = transactionInformation;
                _currentOrderToSet = null;
            }
            return null;
        }

        private void OnOrderToCancelReceived(TransactionInformation transactionInformation)
        {
            try
            {
                _logger.Info($"Send order cancel request to control system. TransactionID: {transactionInformation.TransactionID}");
                OrderToCancelReceivedFunc?.Invoke(transactionInformation);
            }
            catch (Exception ex)
            {
                _logger.Error($"OnOrderToCancelReceived TransactionID:({transactionInformation.TransactionID}) Exception: {ex}");
            }
        }
    }
}