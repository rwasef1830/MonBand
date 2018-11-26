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
            pollIntervalSeconds,
            loggerFactory,
            delayTaskFactory)
        {
            this._trafficQuery = trafficQuery
                                 ?? throw new ArgumentNullException(nameof(trafficQuery));
            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        protected override async Task PollAsync(CancellationToken cancellationToken)
        {
            var traffic = await this._trafficQuery
                .GetTotalTrafficBytesAsync(cancellationToken)
                .ConfigureAwait(false);
            this.Log.LogTrace(
                "Traffic in bytes: Received: {0} - Sent: {1}",
                traffic.InBytes,
                traffic.OutBytes);

            var now = this._timeProvider.UtcNow;
            cancellationToken.ThrowIfCancellationRequested();

            if (this._previousTime != default)
            {
                var secondsDelta = (now - this._previousTime).TotalSeconds;
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

                var receivedBytesPerSecond = (long)(receivedBytesDelta / secondsDelta);
                var sentBytesPerSecond = (long)(sentBytesDelta / secondsDelta);
                var trafficRate = new NetworkTraffic(receivedBytesPerSecond, sentBytesPerSecond);

                this.Log.LogDebug(
                    "Traffic rate in bytes/sec: Received: {0} - Sent: {1}",
                    trafficRate.InBytes,
                    trafficRate.OutBytes);

                this.OnTrafficRateUpdated(trafficRate);
            }

            this._previousTime = now;
            this._previousTraffic = traffic;
        }
    }
}
