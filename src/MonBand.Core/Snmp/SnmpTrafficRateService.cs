﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Time;

namespace MonBand.Core.Snmp
{
    public class SnmpTrafficRateService : PollingTrafficRateServiceBase
    {
        readonly ISnmpTrafficQuery _trafficQuery;
        readonly SnmpTrafficRateValueFilter _downloadRateFilter;
        readonly SnmpTrafficRateValueFilter _uploadRateFilter;

        NetworkTraffic? _currentTraffic;
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
            this._trafficQuery = trafficQuery ?? throw new ArgumentNullException(nameof(trafficQuery));
            this._downloadRateFilter = new SnmpTrafficRateValueFilter();
            this._uploadRateFilter = new SnmpTrafficRateValueFilter();
        }

        protected override async Task PollAsync(CancellationToken cancellationToken)
        {
            var traffic = await this._trafficQuery
                .GetTotalTrafficBytesAsync(cancellationToken)
                .ConfigureAwait(false);

            if (traffic == null)
            {
                this.Log.LogError("Failed to get traffic for interface {0}", this._trafficQuery.InterfaceId);
                return;
            }

            this.Log.LogTrace(
                "Traffic in bytes: Received: {0:n0}; Sent: {1:n0}",
                traffic.Value.InBytes,
                traffic.Value.OutBytes);

            cancellationToken.ThrowIfCancellationRequested();

            this._currentTraffic = traffic;
        }

        protected override Task CalculateRateAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
        {
            if (!this._currentTraffic.HasValue)
            {
                throw new InvalidOperationException("BUG: Current traffic should have a value.");
            }

            if (this._previousTraffic.HasValue)
            {
                var secondsDelta = pollInterval.TotalSeconds;
                var receivedBytesDelta = this._currentTraffic.Value.InBytes - this._previousTraffic.Value.InBytes;
                var sentBytesDelta = this._currentTraffic.Value.OutBytes - this._previousTraffic.Value.OutBytes;

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

                var filteredReceivedBytesPerSecond = (long)this._downloadRateFilter.FilterValue(receivedBytesPerSecond);
                var filteredSentBytesPerSecond = (long)this._uploadRateFilter.FilterValue(sentBytesPerSecond);

                if (filteredReceivedBytesPerSecond != receivedBytesPerSecond)
                {
                    this.Log.LogWarning(
                        "Ignoring download rate spike: {0:n}",
                        receivedBytesPerSecond);
                    receivedBytesPerSecond = filteredReceivedBytesPerSecond;
                }

                if (filteredSentBytesPerSecond != sentBytesPerSecond)
                {
                    this.Log.LogWarning(
                        "Ignoring upload rate spike: {0:n}",
                        sentBytesPerSecond);
                    sentBytesPerSecond = filteredSentBytesPerSecond;
                }

                var trafficRate = new NetworkTraffic(receivedBytesPerSecond, sentBytesPerSecond);

                this.Log.LogDebug(
                    "Traffic rate in bytes/sec: Received: {0:n0}; Sent: {1:n0}; Seconds since last update: {2:n}",
                    trafficRate.InBytes,
                    trafficRate.OutBytes,
                    secondsDelta);

                this.OnTrafficRateUpdated(trafficRate);
            }

            this._previousTraffic = this._currentTraffic;
            return Task.CompletedTask;
        }
    }
}
