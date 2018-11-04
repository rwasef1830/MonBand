using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util;

namespace MonBand.Core.Snmp
{
    public class SnmpPollingNetworkTrafficService : INetworkTrafficService
    {
        readonly ISnmpTrafficQuery _trafficQuery;
        readonly ITimeProvider _timeProvider;
        readonly ILogger _log;
        readonly Func<TimeSpan, CancellationToken, Task> _delayTaskFactory;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly object _startStopLocker = new object();
        bool _disposed;
        Task _pollLoop;

        public event EventHandler<NetworkTraffic> TrafficRateUpdated;

        public SnmpPollingNetworkTrafficService(
            ISnmpTrafficQuery trafficQuery,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory) : this(
            trafficQuery,
            timeProvider,
            loggerFactory,
            Task.Delay) { }

        internal SnmpPollingNetworkTrafficService(
            ISnmpTrafficQuery trafficQuery,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory)
        {
            this._trafficQuery = trafficQuery
                                        ?? throw new ArgumentNullException(nameof(trafficQuery));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this._log = loggerFactory?.CreateLogger(this.GetType().Name)
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

            var previousTime = default(DateTimeOffset);
            var previousTraffic = new NetworkTraffic(0, 0);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var traffic = await this._trafficQuery
                        .GetTotalTrafficBytesAsync()
                        .ConfigureAwait(false);
                    this._log.LogTrace(
                        "Traffic in bytes: Received: {0} - Sent: {1}",
                        traffic.InBytes,
                        traffic.OutBytes);

                    var now = this._timeProvider.UtcNow;
                    cancellationToken.ThrowIfCancellationRequested();

                    if (previousTime != default)
                    {
                        var secondsDelta = (now - previousTime).TotalSeconds;
                        var receivedBytesDelta = traffic.InBytes - previousTraffic.InBytes;
                        var sentBytesDelta = traffic.OutBytes - previousTraffic.OutBytes;

                        var receivedBytesPerSecond = (long)(receivedBytesDelta / secondsDelta);
                        var sentBytesPerSecond = (long)(sentBytesDelta / secondsDelta);
                        var trafficRate = new NetworkTraffic(receivedBytesPerSecond, sentBytesPerSecond);

                        this._log.LogDebug(
                            "Traffic rate in bytes/sec: Received: {0} - Sent: {1}",
                            trafficRate.InBytes,
                            trafficRate.OutBytes);

                        this.TrafficRateUpdated?.Invoke(
                            this,
                            trafficRate);
                    }

                    previousTime = now;
                    previousTraffic = traffic;

                    await this._delayTaskFactory(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    this._log.LogError(ex, "Unhandled exception in poll loop.");
                }
            }
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
