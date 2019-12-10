using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectSummer.Repository;

namespace Benzuber.Helpers
{
    internal class PollingHelper
    {
        private readonly Logger _log;
        private readonly Action _pollingFunc;
        private readonly ManualResetEvent _errorEvent;
        private readonly ManualResetEvent _readyToPolling;

        public PollingHelper(
            Logger log,
            Action pollingFunc,
            ManualResetEvent readyToPolling, 
            ManualResetEvent errorEvent)
        {
            _log = log;
            _pollingFunc = pollingFunc;
            _errorEvent = errorEvent;
            _readyToPolling = readyToPolling;
        }

        public async Task Start(TimeSpan interval, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                    _readyToPolling.WaitOne();
                    _pollingFunc();
                }
                catch (TaskCanceledException)
                {
                    _log.Info("Polling canceled");
                    break;
                }
                catch (Exception e)
                {
                    _log.Error(e.Message);
                    _readyToPolling.Reset();
                    _errorEvent.Set();
                }
            }
            _errorEvent.Set();
        }
    }
}
