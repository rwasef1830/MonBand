using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MonBand.Core
{
    public abstract class PollingTrafficRateServiceBase : ITrafficRateService
    {
        readonly TimeSpan _pollInterval;
        readonly Func<TimeSpan, CancellationToken, Task> _delayTaskFactory;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly object _startStopLocker = new object();
        bool _disposed;
        Task _pollLoop;

        protected ILogger Log { get; }

        public event EventHandler<NetworkTraffic> TrafficRateUpdated;

        protected PollingTrafficRateServiceBase(
            TimeSpan pollInterval,
            ILoggerFactory loggerFactory,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory)
        {
            if (pollInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(pollInterval));
            }

            this._pollInterval = pollInterval;
            this.Log = loggerFactory?.CreateLogger(this.GetType().Name)
                        ?? throw new ArgumentNullException(nameof(loggerFactory));
            this._delayTaskFactory = delayTaskFactory ?? throw new ArgumentNullException(nameof(delayTaskFactory));
            this._cancellationTokenSource = new CancellationTokenSource();
        }

        public void Start()
        {
            lock (this._startStopLocker)
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException(this.GetType().Name);
                }

                this._pollLoop = Task.Run(this.DoPollLoop);
            }
        }

        async Task DoPollLoop()
        {
            var cancellationToken = this._cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await this.PollAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    this.Log.LogError(ex, "Unhandled exception in poll loop.");
                }
                finally
                {
                    await this._delayTaskFactory(this._pollInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected abstract Task PollAsync(CancellationToken cancellationToken);

        protected void OnTrafficRateUpdated(NetworkTraffic traffic)
        {
            this.TrafficRateUpdated?.Invoke(this, traffic);
        }

        public void Dispose()
        {
            lock (this._startStopLocker)
            {
                if (this._disposed)
                {
                    return;
                }

                this._cancellationTokenSource.Cancel();
                try
                {
                    this._pollLoop.GetAwaiter().GetResult();
                }
                catch
                {
                    // ignore
                }

                this._pollLoop.Dispose();
                this._cancellationTokenSource.Dispose();
                this._disposed = true;
            }
        }
    }
}
