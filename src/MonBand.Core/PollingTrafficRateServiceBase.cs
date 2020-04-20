using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Time;

namespace MonBand.Core
{
    public abstract class PollingTrafficRateServiceBase : ITrafficRateService
    {
        readonly TimeSpan _pollInterval;
        readonly ITimeProvider _timeProvider;
        readonly Func<TimeSpan, CancellationToken, Task> _delayTaskFactory;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly IStopwatch _pollStopwatch;
        readonly object _startStopLocker = new object();
        bool _disposed;
        Task _pollLoop;

        protected ILogger Log { get; }

        public event EventHandler<NetworkTraffic> TrafficRateUpdated;

        protected PollingTrafficRateServiceBase(
            TimeSpan pollInterval,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory)
        {
            if (pollInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(pollInterval));
            }

            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this._pollInterval = pollInterval;
            this._delayTaskFactory = delayTaskFactory ?? throw new ArgumentNullException(nameof(delayTaskFactory));
            this._cancellationTokenSource = new CancellationTokenSource();
            this._pollStopwatch = timeProvider.CreateStopwatch();

            this.Log = loggerFactory?.CreateLogger(this.GetType().Name)
                       ?? throw new ArgumentNullException(nameof(loggerFactory));
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
            var delayInterval = this._pollInterval;
            var pollCount = 1;

            this._pollStopwatch.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    this._pollStopwatch.Restart();
                    await this.PollAsync(cancellationToken).ConfigureAwait(false);
                    var pollElapsed = this._pollStopwatch.Elapsed;
                    this.Log.LogTrace("Poll time duration: {0}", pollElapsed);
                    await this.CalculateRateAsync(this._pollInterval * pollCount, cancellationToken)
                        .ConfigureAwait(false);

                    pollCount = 1;
                    var pollAndCalculateRateElapsed = this._pollStopwatch.Elapsed;
                    delayInterval -= pollAndCalculateRateElapsed;
                    while (delayInterval <= TimeSpan.Zero)
                    {
                        delayInterval += this._pollInterval;
                        pollCount++;
                    }
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
                    this._pollStopwatch.Restart();
                    this.Log.LogTrace("Sleeping for {0}", delayInterval);
                    await this._delayTaskFactory(delayInterval, cancellationToken).ConfigureAwait(false);
                    var delayError = this._pollStopwatch.Elapsed - delayInterval;
                    delayInterval = this._pollInterval - delayError;
                }
            }
        }

        protected abstract Task PollAsync(CancellationToken cancellationToken);
        protected abstract Task CalculateRateAsync(TimeSpan pollInterval, CancellationToken cancellationToken);

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
