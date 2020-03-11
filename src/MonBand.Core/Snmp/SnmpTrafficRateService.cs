using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util;

namespace MonBand.Core.Snmp
{
    public class SnmpTrafficRateService : PollingTrafficRateServiceBase
    {
        readonly ISnmpTrafficQuery _trafficQuery;
        readonly ITimeProvider _timeProvider;
        readonly TimeSpan _updateInterval;

        DateTimeOffset _previousTime;
        NetworkTraffic _previousTraffic;

        public SnmpTrafficRateService(
            ISnmpTrafficQuery trafficQuery,
            byte pollIntervalSeconds,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory) : this(
            trafficQuery,
            pollIntervalSeconds,
            timeProvider,
            loggerFactory,
            Task.Delay) { }

        internal SnmpTrafficRateService(
            ISnmpTrafficQuery trafficQuery,
            byte pollIntervalSeconds,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory) : base(
            TimeSpan.FromMilliseconds(pollIntervalSeconds * (double)1000 / 4),
            loggerFactory,
            delayTaskFactory)
        {
            this._updateInterval = TimeSpan.FromSeconds(pollIntervalSeconds);
            this._trafficQuery = trafficQuery
                                 ?? throw new ArgumentNullException(nameof(trafficQuery));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        protected override async Task PollAsync(CancellationToken cancellationToken)
        {
            var startTime = this._timeProvider.UtcNow;

            var traffic = await this._trafficQuery
                .GetTotalTrafficBytesAsync(cancellationToken)
                .ConfigureAwait(false);
            var queryDuration = this._timeProvider.UtcNow - startTime;

            this.Log.LogTrace(
                "Traffic in bytes: Received: {0:n0}; Sent: {1:n0}; Query duration: {2}",
                traffic.InBytes,
                traffic.OutBytes,
                queryDuration);

            cancellationToken.ThrowIfCancellationRequested();

            if (this._previousTime == default)
            {
                this._previousTime = startTime;
                this._previousTraffic = traffic;
                return;
            }

            var timeDelta = startTime - this._previousTime;
            var secondsDelta = timeDelta.TotalSeconds;
            var receivedBytesDelta = traffic.InBytes - this._previousTraffic.InBytes;
            var sentBytesDelta = traffic.OutBytes - this._previousTraffic.OutBytes;

            if (receivedBytesDelta < 0)
            {
                // Counter wrap around occurred.
                receivedBytesDelta += uint.MaxValue;
            }

            if (sentBytesDelta < 0)
            {
                // Counter wrap around occurred.
                sentBytesDelta += uint.MaxValue;
            }

            if (timeDelta < this._updateInterval)
            {
                return;
            }

            var receivedBytesPerSecond = (long)(receivedBytesDelta / secondsDelta);
            var sentBytesPerSecond = (long)(sentBytesDelta / secondsDelta);
            var trafficRate = new NetworkTraffic(receivedBytesPerSecond, sentBytesPerSecond);

            this.Log.LogDebug(
                "Traffic rate in bytes/sec: Received: {0:n0}; Sent: {1:n0}; Query duration: {2}; Seconds since last update: {3:n}",
                trafficRate.InBytes,
                trafficRate.OutBytes,
                queryDuration,
                secondsDelta);

            this.OnTrafficRateUpdated(trafficRate);

            this._previousTime = startTime;
            this._previousTraffic = traffic;
        }
    }
}
