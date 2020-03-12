using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Time;

namespace MonBand.Core.Snmp
{
    public class SnmpTrafficRateService : PollingTrafficRateServiceBase
    {
        readonly ISnmpTrafficQuery _trafficQuery;
        readonly IStopwatch _queryStopwatch;

        NetworkTraffic? _previousTraffic;

        public SnmpTrafficRateService(
            ISnmpTrafficQuery trafficQuery,
            byte pollIntervalSeconds,
            ILoggerFactory loggerFactory) : this(
            trafficQuery,
            pollIntervalSeconds,
            SystemTimeProvider.Instance,
            loggerFactory,
            Task.Delay) { }

        internal SnmpTrafficRateService(
            ISnmpTrafficQuery trafficQuery,
            byte pollIntervalSeconds,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory) : base(
            TimeSpan.FromSeconds(pollIntervalSeconds),
            timeProvider,
            loggerFactory,
            delayTaskFactory)
        {
            this._trafficQuery = trafficQuery
                                 ?? throw new ArgumentNullException(nameof(trafficQuery));
            this._queryStopwatch = timeProvider.CreateStopwatch();
        }

        protected override async Task PollAsync(TimeSpan timeSinceLastPoll, CancellationToken cancellationToken)
        {
            this._queryStopwatch.Start();
            var traffic = await this._trafficQuery
                .GetTotalTrafficBytesAsync(cancellationToken)
                .ConfigureAwait(false);
            var queryDuration = this._queryStopwatch.Elapsed;
            this._queryStopwatch.Reset();

            this.Log.LogTrace(
                "Traffic in bytes: Received: {0:n0}; Sent: {1:n0}; Query duration: {2}",
                traffic.InBytes,
                traffic.OutBytes,
                queryDuration);

            cancellationToken.ThrowIfCancellationRequested();

            if (!this._previousTraffic.HasValue)
            {
                this._previousTraffic = traffic;
                return;
            }

            var secondsDelta = timeSinceLastPoll.TotalSeconds;
            var receivedBytesDelta = traffic.InBytes - this._previousTraffic.Value.InBytes;
            var sentBytesDelta = traffic.OutBytes - this._previousTraffic.Value.OutBytes;

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
                "Traffic rate in bytes/sec: Received: {0:n0}; Sent: {1:n0}; Query duration: {2}; Seconds since last update: {3:n}",
                trafficRate.InBytes,
                trafficRate.OutBytes,
                queryDuration,
                secondsDelta);

            this.OnTrafficRateUpdated(trafficRate);

            this._previousTraffic = traffic;
        }
    }
}
