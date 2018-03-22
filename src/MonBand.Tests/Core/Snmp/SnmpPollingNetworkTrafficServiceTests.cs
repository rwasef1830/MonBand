using System;
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
    public class SnmpPollingNetworkTrafficServiceTests
    {
        [Fact]
        public async Task Service_calculates_rate_correctly()
        {
            var trafficReadings = new[]
            {
                new NetworkTraffic(100, 50),
                new NetworkTraffic(200, 100),
                new NetworkTraffic(300, 150)
            };

            var trafficQuery = A.Fake<ISnmpNetworkTrafficQuery>();
            A.CallTo(() => trafficQuery.GetTotalTrafficBytesAsync())
                .ReturnsNextFromSequence(trafficReadings);
            var timeProvider = new ManualTimeProvider();

            int i = 0;
            using (var service = new SnmpPollingNetworkTrafficService(
                trafficQuery,
                timeProvider,
                new NullLoggerFactory(),
                (interval, cancellationToken) =>
                {
                    if (++i >= trafficReadings.Length)
                    {
                        throw new OperationCanceledException();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    timeProvider.Advance(interval);
                    return Task.CompletedTask;
                }))
            {

                var batchingBlock = new BatchBlock<NetworkTraffic>(2);
                var bufferBlock = new BufferBlock<NetworkTraffic>();
                bufferBlock.LinkTo(batchingBlock);

                service.TrafficRateUpdated += (s, t) => bufferBlock.Post(t);
                service.Start();

                var trafficRates = await batchingBlock.ReceiveAsync().ConfigureAwait(true);
                foreach (var trafficRate in trafficRates)
                {
                    trafficRate.InBytes.Should().Be(100);
                    trafficRate.OutBytes.Should().Be(50);
                }
            }
        }
    }
}
