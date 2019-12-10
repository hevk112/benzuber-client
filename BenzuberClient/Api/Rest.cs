using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Benzuber.Api.Models;
using Newtonsoft.Json;
using ProjectSummer.Repository;

namespace Benzuber.Api
{
    internal class RestApi
    {
        private readonly Logger _logger;
        private readonly SignHelper _signHelper;
        private string _sessionId;
        private readonly WebClient _webClient = new WebClient();
        private readonly Configuration _config;
        private string _selectedServer;
        
        public RestApi(Configuration config, SignHelper signHelper)
        {
            _config = config;
            _selectedServer = config.Server;
            _signHelper = signHelper;
            _logger = new Logger(GetType().Name, _config.LogLevel);
        }

        public AuthResult Authorize()
        {
            if (!_signHelper.LoadKey())
                SetSignKey();

            var result =
                WebClientRequest<AuthResult>(
                    path: $"{_config.Server}/api/Auth",
                    setHwid: true,
                    signData: _config.Hwid);

            if (result.Result != Results.OK)
                return result;

            _selectedServer = result.FS_Servers.First();
            _sessionId = result.SessionID;

            return result;
        }

        public Results SetSignKey()
        {
            var publicKey = _signHelper.CreateKey();
            return string.IsNullOrWhiteSpace(publicKey)
                ? Results.Error
                : WebClientRequest<Results>(
                    path: $"{_config.Server}/api/SetSignKey",
                    setHwid: true,
                    queryParams: $"&PublicKey={publicKey}");
        }


        public Results SetStationInformation(StationInformation information)
        {
            return WebClientRequest<Results>(
                path: "api/SetStationInformationEx",
                setSession: true,
                queryParams: $"&Infromation={information}",
                disableLog: false);
        }

        public bool PingServer()
        {
            return WebClientRequest<string>(
                    new Uri(
                        new Uri(_config.Server),
                        "api/ping").ToString())
                .Contains("MachineName:");
        }

        public TransactionInformation[] GetOrders()
        {
            return WebClientRequest<TransactionInformation[]>($"api/GetOrdersEx", setSession: true)
                   ?? new TransactionInformation[0];
        }

        public TransactionInformation[] ToCancelOrders()
        {
            return WebClientRequest<TransactionInformation[]>("api/ToCancelOrders", setSession: true)
                   ?? new TransactionInformation[0];
        }

        public Results AcceptOrder(string rrn)
        {
            return WebClientRequest<Results>(
                path: "api/AcceptOrder",
                setSession: true,
                signData: $"{_config.StationId}/{rrn}",
                queryParams: $"&RRN={rrn}");
        }

        public Results OrderIsFilling(string rrn, long pumpRrn)
        {
            try
            {
                return WebClientRequest<Results>(
                    $"api/OrderIsFilling",
                    setSession:true,
                    queryParams: $"&RRN={rrn}&PumpRRN={pumpRrn}",
                    signData:$"{_config.StationId}/{rrn}/{pumpRrn}");

            }
            catch (Exception ex)
            {
                _logger.Error($"{nameof(OrderIsFilling)} Error {ex}");
            }

            return Results.Error;
        }

        public Results OrderIsFilled(string rrn, decimal fillingOverAmount)
        {
            _logger.Info($"Filling over: {rrn}, Amount: {fillingOverAmount:0.00} rub.");
            var amount = fillingOverAmount.ToString("0.00", CultureInfo.InvariantCulture);
            return WebClientRequest<Results>(
                path: "api/OrderIsFilled",
                signData: $"{_config.StationId}/{rrn}/{amount}",
                setSession: true,
                queryParams: $"&RRN={rrn}&Amount={amount}");
        }


        public Results SetPrices(IList<PriceInfo> pricesInfo)
        {
            var data = JsonConvert.SerializeObject(pricesInfo);
            _logger.Info("Set prices: " + data);
            return WebClientRequest<Results>(
                path: "api/SetPrices",
                postData: data,
                signData: string.Join(";", pricesInfo),
                setSession: true);
        }


        private T WebClientRequest<T>(
            string path, 
            string postData = null, 
            string signData = null,
            bool setSession = false, 
            bool setHwid = false,
            string queryParams = "", 
            bool disableLog = false)
        {

            var requestId = Guid.NewGuid().ToString().Substring(0, 6);
            var logLevel = disableLog ? Logger.LogLevels.Debug : Logger.LogLevels.Info;

            _logger.Log($"[{requestId}] Request: '{path}', params: '{queryParams}'", logLevel);


            

            var isAbsolutePath = path.StartsWith(_selectedServer) || path.StartsWith(_config.Server);
            var sb = new StringBuilder(path);
            sb.Append($"?TID={_config.StationId}");

            if (setHwid)
                sb.Append($"&HWID={_config.Hwid}");

            if (setSession)
                sb.Append($"&Session={_sessionId}");

            sb.Append(queryParams);

            if (!string.IsNullOrWhiteSpace(signData))
                sb.Append($"&Signature={_signHelper.SignData(signData)}");

            var uri = new Uri(sb.ToString(), isAbsolutePath ? UriKind.Absolute : UriKind.Relative);


            _logger.Log($"[{requestId}] Request URI: '{uri}'", logLevel);

                var responce = WebClientRequest(uri, postData);

            _logger.Log($"[{requestId}] Responce: {responce}", logLevel);

            return JsonConvert.DeserializeObject<T>(responce);
        }

        private string WebClientRequest(Uri uri, string postData = null)
        {
            if (!uri.IsAbsoluteUri)
                uri = new Uri(new Uri(_selectedServer), uri);

            try
            {
                lock (_webClient)
                {
                    _webClient.Encoding = Encoding.UTF8;
                    _webClient.Headers[HttpRequestHeader.ContentType] = "application/json";
                    return postData == null
                        ? _webClient.DownloadString(uri)
                        : _webClient.UploadString(uri, postData);
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response?.StatusCode == HttpStatusCode.NotFound)
                    return "";

                _logger.Info(ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }

            return "";
        }


        public Results ResultsParser(string resultData)
        {
            return int.TryParse(resultData, out var result) ? (Results) result : Results.Error;
        }
    }
}