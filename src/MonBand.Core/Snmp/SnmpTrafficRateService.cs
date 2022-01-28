using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Time;

namespace MonBand.Core.Snmp;

public class SnmpTrafficRateService : PollingTrafficRateServiceBase
{
    readonly ISnmpTrafficQuery _trafficQuery;
    readonly SnmpTrafficRateValueFilter _downloadRateFilter;
    readonly SnmpTrafficRateValueFilter _uploadRateFilter;

    NetworkTraffic? _currentTraffic;
    NetworkTraffic? _previousTraffic;

    public SnmpTrafficRateService(
        ISnmpTrafficQuery trafficQuery,
        decimal pollIntervalSeconds,
        ILoggerFactory loggerFactory) : this(
        trafficQuery,
        pollIntervalSeconds,
        SystemTimeProvider.Instance,
        loggerFactory,
        Task.Delay) { }

    internal SnmpTrafficRateService(
        ISnmpTrafficQuery trafficQuery,
        decimal pollIntervalSeconds,
        ITimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        Func<TimeSpan, CancellationToken, Task> delayTaskFactory) : base(
        TimeSpan.FromSeconds((double)pollIntervalSeconds),
        timeProvider,
        loggerFactory,
        delayTaskFactory)
    {
        this._trafficQuery = trafficQuery ?? throw new ArgumentNullException(nameof(trafficQuery));
        this._downloadRateFilter = new SnmpTrafficRateValueFilter(pollIntervalSeconds);
        this._uploadRateFilter = new SnmpTrafficRateValueFilter(pollIntervalSeconds);
    }

    protected override async Task PollAsync(CancellationToken cancellationToken)
    {
        var traffic = await this._trafficQuery
            .GetTotalTrafficBytesAsync(cancellationToken)
            .ConfigureAwait(false);

        if (traffic == null)
        {
            this.Log.LogError("Failed to get traffic for interface {InterfaceId}", this._trafficQuery.InterfaceId);
            return;
        }

        this.Log.LogTrace(
            "Traffic in bytes: Received: {InBytes:n0}; Sent: {OutBytes:n0}",
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
            var receivedBytesDelta = (long)(this._currentTraffic.Value.InBytes - this._previousTraffic.Value.InBytes);
            var sentBytesDelta = (long)(this._currentTraffic.Value.OutBytes - this._previousTraffic.Value.OutBytes);

            if (receivedBytesDelta < 0)
            {
                // Counter wrap around occurred.
                receivedBytesDelta += this._currentTraffic.Value.Is64BitCounter ? long.MaxValue : uint.MaxValue;
            }

            if (sentBytesDelta < 0)
            {
                // Counter wrap around occurred.
                sentBytesDelta += this._currentTraffic.Value.Is64BitCounter ? long.MaxValue : uint.MaxValue;
            }

            var receivedBytesPerSecond = (ulong)(receivedBytesDelta / secondsDelta);
            var sentBytesPerSecond = (ulong)(sentBytesDelta / secondsDelta);

            var filteredReceivedBytesPerSecond = (ulong)this._downloadRateFilter.FilterValue(receivedBytesPerSecond);
            var filteredSentBytesPerSecond = (ulong)this._uploadRateFilter.FilterValue(sentBytesPerSecond);

            if (filteredReceivedBytesPerSecond != receivedBytesPerSecond)
            {
                this.Log.LogWarning(
                    "Ignoring download rate spike: {ReceivedBytesPerSecond:n}",
                    receivedBytesPerSecond);
                receivedBytesPerSecond = filteredReceivedBytesPerSecond;
            }

            if (filteredSentBytesPerSecond != sentBytesPerSecond)
            {
                this.Log.LogWarning(
                    "Ignoring upload rate spike: {SentBytesPerSecond:n}",
                    sentBytesPerSecond);
                sentBytesPerSecond = filteredSentBytesPerSecond;
            }

            var trafficRate = new NetworkTraffic(receivedBytesPerSecond, sentBytesPerSecond);

            this.Log.LogDebug(
                "Traffic rate in bytes/sec: Received: {InBytes:n0}; Sent: {OutBytes:n0}; Seconds since last update: {SecondsDelta:n}",
                trafficRate.InBytes,
                trafficRate.OutBytes,
                secondsDelta);

            this.OnTrafficRateUpdated(trafficRate);
        }

        this._previousTraffic = this._currentTraffic;
        return Task.CompletedTask;
    }
}
