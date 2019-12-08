using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Tests.TestDoubles;
using Xunit;

namespace MonBand.Tests.Core.Snmp
{
    public class SnmpTrafficRateServiceTests
    {
        [Fact]
        public async Task Service_calculates_rate()
        {
            var trafficReadings = new[]
            {
                new NetworkTraffic(100, 50),
                new NetworkTraffic(200, 100),
                new NetworkTraffic(300, 150)
            };

            await RunRateCalculationTestAsync(trafficReadings, 100, 50).ConfigureAwait(true);
        }

        [Fact]
        public async Task Service_calculates_rate_with_counter_wraparound()
        {
            var trafficReadings = new[]
            {
                new NetworkTraffic(uint.MaxValue - 100, uint.MaxValue - 50),
                new NetworkTraffic(uint.MaxValue, uint.MaxValue),
                new NetworkTraffic(100, 50),
                new NetworkTraffic(200, 100)
            };

            await RunRateCalculationTestAsync(trafficReadings, 100, 50).ConfigureAwait(true);
        }

        static async Task RunRateCalculationTestAsync(
            NetworkTraffic[] inputTrafficReadings,
            long expectedInBytesRate,
            long expectedOutBytesRate)
        {
            var trafficQuery = A.Fake<ISnmpTrafficQuery>();
            A.CallTo(() => trafficQuery.GetTotalTrafficBytesAsync(A<CancellationToken>.Ignored))
                .ReturnsNextFromSequence(inputTrafficReadings);
            var timeProvider = new ManualTimeProvider();

            int i = 0;
            using var service = new SnmpTrafficRateService(
                trafficQuery,
                1,
                timeProvider,
                new NullLoggerFactory(),
                (interval, cancellationToken) =>
                {
                    if (++i >= inputTrafficReadings.Length)
                    {
                        throw new OperationCanceledException();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    timeProvider.Advance(interval);
                    return Task.CompletedTask;
                });

            var batchingBlock = new BatchBlock<NetworkTraffic>(2);
            var bufferBlock = new BufferBlock<NetworkTraffic>();
            bufferBlock.LinkTo(batchingBlock);

            service.TrafficRateUpdated += (s, t) => bufferBlock.Post(t);
            service.Start();

            var trafficRates = await batchingBlock.ReceiveAsync().ConfigureAwait(true);

            foreach (var trafficRate in trafficRates)
            {
                trafficRate.InBytes.Should().Be(expectedInBytesRate);
                trafficRate.OutBytes.Should().Be(expectedOutBytesRate);
            }
        }
    }
}
