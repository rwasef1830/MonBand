using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util;

namespace MonBand.Core
{
    public abstract class PollingTrafficRateServiceBase : ITrafficRateService
    {
        readonly TimeSpan _pollInterval;
        readonly Func<TimeSpan, CancellationToken, Task> _delayTaskFactory;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly IStopwatch _pollStopwatch;
        readonly object _startStopLocker = new object();
        bool _disposed;
        Task _pollLoop;

        protected ITimeProvider TimeProvider { get; }

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

            this.TimeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.Log = loggerFactory?.CreateLogger(this.GetType().Name)
                       ?? throw new ArgumentNullException(nameof(loggerFactory));

            this._pollInterval = pollInterval;
            this._delayTaskFactory = delayTaskFactory ?? throw new ArgumentNullException(nameof(delayTaskFactory));
            this._cancellationTokenSource = new CancellationTokenSource();
            this._pollStopwatch = timeProvider.CreateStopwatch();
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
            var cyclesPassed = 1;

            while (!cancellationToken.IsCancellationRequested)
            {
                var delayInterval = this._pollInterval;
                try
                {
                    this._pollStopwatch.Start();
                    var timeSinceLastPoll = delayInterval * cyclesPassed;

                    await this.PollAsync(timeSinceLastPoll, cancellationToken).ConfigureAwait(false);
                    var timeTaken = this._pollStopwatch.Elapsed;
                    this._pollStopwatch.Reset();

                    delayInterval -= timeTaken;
                    if (delayInterval > TimeSpan.Zero)
                    {
                        cyclesPassed = 1;
                        continue;
                    }

                    while (delayInterval <= TimeSpan.Zero)
                    {
                        delayInterval += this._pollInterval;
                        cyclesPassed++;
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
                    await this._delayTaskFactory(delayInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        protected abstract Task PollAsync(TimeSpan timeSinceLastPoll, CancellationToken cancellationToken);

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
