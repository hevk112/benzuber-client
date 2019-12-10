using System;
using System.CodeDom;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Benzuber;
using Benzuber.Api.Models;
using ProjectSummer.Repository;


namespace BenzuberClientTestApp
{
    class Program
    {
        private static long _counter = 1;

        //Создаем клиента, и передаем ему конфигурацию
        private static Client client;

        static async Task Main(string[] args)
        {
            //Итентификатор АЗС в системе Benzuber
            var stationId = 0;

            //Hardware ID - идентификатор аппаратной составляющей ПК, HEX строка 32 символа
            var hwid = "1415D518C428500F70F688CE04D8EA47";

            //Адрес сервера
            var server = "https://test-as-apiazs.benzuber.ru";

            client = new Client(new Configuration(stationId, hwid, server, Logger.LogLevels.Debug));

            InitializeCallback();

            //Создаем базовые значения для поллинга
            //Начальные цены на топливо 
            var pricesInfo = CreatePricesInfo();
            //Начальные статусы ТРК
            var stationInformation = new StationInformation(CreatePumpStates());

            //Создаем объект источника токен отмены
            var cts = new CancellationTokenSource();

            //Запускаем поллинг сервера
            var polling = client.StartPolling(stationInformation, pricesInfo, cts.Token);

            //Иммитируем работу АСУ АЗС
            var fakeStationControl = FakeControlSystem(cts.Token);
            Console.ReadLine();
            Console.WriteLine("Closing client...");

            //Перез давершением приложения: останавливаем поллинг, через токен отмены
            cts.Cancel();
            //Дожидаемся завершения поллинга
            await polling.ConfigureAwait(false);
            Console.WriteLine("Client closed");

            Console.ReadLine();
        }

        private static void InitializeCallback()
        {
            // Устанавливаем делегат на функцию установки заказа на ТРК
            // Вызов данной функции выполняется синхронно, т.е. пока не
            // завершится вызов функции по первому поступившему заказу,
            // не начнется следующий вызов.

            // Рекомендуется выполнять данную функцию максимально быстро.
            // Функция должна возвращать уникальный идентификатор транзакции
            // (положительное число типа long)

            // В случае, если транзакция не может быть установлена - необходимо
            // вернуть отрицательное число.

            // Если же функция вернула положительное число, но в ходе выполнения
            // Запуска ТРК произошла какая-либо ошибка - необходимо завершить
            // транзакцию запросом SetFilled()

            // ВНИМАНИЕ!!! Вызывать SetFilled() непосредственно в данной функции
            // запрещено, т.к. это приведёт исключению!!!

            client.OrderToSetReceivedFunc = OnOrderToSetReceived;

            // Устанавливает делегат процедуры отмены заказа.
            // Данная процедура должна завершаться максимально быстро.
            // После завершения данной функции АСУ АЗС должна найти запрашиваемую
            // транзакцию, и если по ней идёт отпуск топлива - завершить его и
            // завершить вызовом SetFilled.

            // Также возможна ситуация, при которой данный запрос приходит по
            // уже завершенной транзакции - в этом случае необходимо повторно
            // выполнить SetFilling на корректную сумму

            //В случае, если приходит запрос на сброс заказа, который не был получен
            //такой запрос необходимо проигнорировать.
            client.OrderToCancelReceivedFunc = OnOrderToCancelReceived;
        }

        private static PriceInfo[] CreatePricesInfo()
        {
            return new[]
            {
                new PriceInfo("АИ-92", 92, 30.92M, new[] {1, 2, 3, 4}),
                new PriceInfo("АИ-95", 95, 30.95M, new[] {1, 2, 3, 4}),
                new PriceInfo("ДТ", 50, 30.50M, new[] {6, 7}),
            };
        }

        private static List<PumpState> CreatePumpStates()
        {
            return Enumerable
                .Range(1, 7)
                .Select(pump => new PumpState(pump, false, 0))
                .ToList();
        }

        private static long OnOrderToSetReceived(TransactionInformation transactionInformation)
        {
            Console.WriteLine($"Начинаем отпуск топлива, по транзакции: {transactionInformation.TransactionID}");
            ordersToSet.Enqueue(transactionInformation);
            return _counter++;
        }

        private static void OnOrderToCancelReceived(TransactionInformation transactionInformation)
        {
            Console.WriteLine($"Запрос завершения транзакции: {transactionInformation.TransactionID}");
            var cts = transactionCts.TryGetValue(transactionInformation.TransactionID, out var result) ? result : null;
            cts?.Cancel();
        }

        private static Queue<TransactionInformation> ordersToSet = new Queue<TransactionInformation>();

        private static Queue<TransactionInformation> transactionInformations = new Queue<TransactionInformation>();

        private static IDictionary<string, CancellationTokenSource> transactionCts= new ConcurrentDictionary<string, CancellationTokenSource>();

        private static async Task FakeControlSystem(CancellationToken cancellationToken)
        {
            var pumps = Enumerable.Range(1, 7).ToDictionary(i => i, _ => null as Task<(string TransId, decimal Amount)>);

            while (!cancellationToken.IsCancellationRequested)
            {
                // Для примера делаем обработку завершения налива
                // Обратите внимание, что завершение налива делается в другом потоке,
                // т.е. по факту SetFilled() вызовется уже позже чем завершится
                // OrderToSetReceivedFunc
                while (ordersToSet.Any())
                {
                    //Забираем из очереди заказ
                    var transaction = ordersToSet.Dequeue();

                    //Если ТРК занята - уведомляем об этом сервис, закрытием заказа на 0 сумму.
                    if (pumps[transaction.Pump] != null)
                    {
                        client.SetFilled(transaction.RequestID, 0M);
                    }
                    //Иначе устанавливаем заказ на ТРК
                    else
                    {
                        var cts = new CancellationTokenSource();
                        transactionCts[transaction.TransactionID] = cts;
                        pumps[transaction.Pump] = Filling(transaction.TransactionID, transaction.Amount, cts.Token);
                    }
                }

                var fixedPumps = pumps.ToArray();
                foreach (var pump in fixedPumps)
                {
                    //Устанавливаем статус доступности ТРК. 
                    client.SetPumpState(new PumpState(pump.Key, pump.Value == null));

                    //Когда налив завершен - устанавливаем сумму фактически отпущенного топлива
                    if (pump.Value?.IsCompleted ?? false)
                    {
                        var (transId, amount) = pump.Value.Result;
                        if (client.SetFilled(transId, amount))
                            pumps[pump.Key] = null;

                        transactionCts.Remove(transId);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task<(string TransId, decimal Amount)> Filling(string transId, decimal maxAmount, CancellationToken cancellationToken)
        {
            var amount = 0M;
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

                if ((amount += 50M) >= maxAmount)
                {
                    amount = maxAmount;
                    break;
                }
            }
            return (transId, amount);
        }
    }
}