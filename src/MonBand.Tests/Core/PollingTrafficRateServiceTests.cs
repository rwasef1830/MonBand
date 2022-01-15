using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Util.Time;
using MonBand.Tests.TestDoubles;
using Xunit;

namespace MonBand.Tests.Core;

[UsedImplicitly]
[SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
public class PollingTrafficRateServiceTests
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class Loop_compensates_for_polling_duration_to_maintain_poll_interval
    {
        [Fact]
        public void In_case_polling_takes_less_than_poll_duration()
        {
            RunTest(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.3), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void In_case_polling_takes_more_than_poll_duration()
        {
            RunTest(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4));
        }

        static void RunTest(TimeSpan pollInterval, TimeSpan pollQueryDelay, TimeSpan expectedTimeSinceDelayedPoll)
        {
            var waitHandle = new ManualResetEventSlim(false);
            var timeProvider = new MockTimeProvider();
            int pollCount = 0;
            var pollErrors = new List<Exception>();

            Task PollAsync(TimeSpan timeSinceLastPoll, CancellationToken cancellationToken)
            {
                try
                {
                    pollCount++;

                    switch (pollCount)
                    {
                        case 1:
                            timeSinceLastPoll.Should().Be(pollInterval);
                            timeProvider.Advance(pollQueryDelay);
                            return Task.CompletedTask;

                        case 2:
                            timeSinceLastPoll.Should().Be(expectedTimeSinceDelayedPoll);
                            return Task.CompletedTask;

                        default:
                            waitHandle.Set();
                            throw new OperationCanceledException();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    pollErrors.Add(ex);
                }

                return Task.CompletedTask;
            }

            Task DoLoopDelay(TimeSpan interval, CancellationToken _)
            {
                timeProvider.Advance(interval);
                return Task.CompletedTask;
            }

            using (var service = new TestPollingTrafficRateService(
                       pollInterval,
                       timeProvider,
                       PollAsync,
                       DoLoopDelay))
            {
                service.Start();

                if (!waitHandle.Wait(Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(5)))
                {
                    throw new Exception("Wait handle was not called in a reasonable time.");
                }
            }

            pollErrors.Should().BeEmpty();
        }
    }

    class TestPollingTrafficRateService : PollingTrafficRateServiceBase
    {
        readonly Func<TimeSpan, CancellationToken, Task> _calculateRateAsyncMethod;

        public TestPollingTrafficRateService(
            TimeSpan pollInterval,
            ITimeProvider timeProvider,
            Func<TimeSpan, CancellationToken, Task> calculateRateAsyncMethod,
            Func<TimeSpan, CancellationToken, Task> delayTaskFactory) : base(
            pollInterval,
            timeProvider,
            new NullLoggerFactory(),
            delayTaskFactory)
        {
            this._calculateRateAsyncMethod = calculateRateAsyncMethod
                                             ?? throw new ArgumentNullException(nameof(calculateRateAsyncMethod));
        }

        protected override Task PollAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        protected override Task CalculateRateAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
        {
            return this._calculateRateAsyncMethod(pollInterval, cancellationToken);
        }
    }
}
