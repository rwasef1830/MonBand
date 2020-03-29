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
            var cycleCount = 1;
            var isFirstLoop = true;
            var delayInterval = TimeSpan.Zero;

            this._pollStopwatch.Start();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var representedTime = this._pollInterval * cycleCount;
                    await this.PollAsync(representedTime, cancellationToken).ConfigureAwait(false);
                    var loopExecutionTime = this._pollStopwatch.Elapsed;
                    this._pollStopwatch.Restart();

                    if (isFirstLoop)
                    {
                        isFirstLoop = false;
                    }
                    else
                    {
                        loopExecutionTime -= delayInterval;
                    }

                    var nextDelayInterval = this._pollInterval - loopExecutionTime;
                    if (nextDelayInterval > TimeSpan.Zero)
                    {
                        cycleCount = 1;
                    }
                    else
                    {
                        while (nextDelayInterval <= TimeSpan.Zero)
                        {
                            nextDelayInterval += this._pollInterval;
                            cycleCount++;
                        }
                    }

                    delayInterval = nextDelayInterval;
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
