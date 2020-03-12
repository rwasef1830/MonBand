﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Time;

namespace MonBand.Core.PerformanceCounters
{
    public class PerformanceCounterTrafficRateService : PollingTrafficRateServiceBase
    {
        readonly PerformanceCounter _bytesReceivedCounter;
        readonly PerformanceCounter _bytesSentCounter;

        public PerformanceCounterTrafficRateService(
            string interfaceName,
            ILoggerFactory loggerFactory) : this(
            interfaceName,
            SystemTimeProvider.Instance,
            loggerFactory,
            Task.Delay) { }

        internal PerformanceCounterTrafficRateService(
            string interfaceName,
            ITimeProvider timeProvider,
            ILoggerFactory loggerFactory,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory) : base(
            TimeSpan.FromSeconds(1),
            timeProvider,
            loggerFactory,
            delayTaskFactory)
        {
            if (string.IsNullOrWhiteSpace(interfaceName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(interfaceName));
            }

            this._bytesReceivedCounter = new PerformanceCounter(
                "Network Interface",
                "Bytes Received/sec",
                interfaceName);

            this._bytesSentCounter = new PerformanceCounter(
                "Network Interface",
                "Bytes Sent/sec",
                interfaceName);
        }

        protected override Task PollAsync(TimeSpan timeSinceLastPoll, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var inBytes = (long)this._bytesReceivedCounter.NextValue();
            var outBytes = (long)this._bytesSentCounter.NextValue();
            this.OnTrafficRateUpdated(new NetworkTraffic(inBytes, outBytes));
            return Task.CompletedTask;
        }
    }
}
