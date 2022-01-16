using System.Diagnostics.CodeAnalysis;
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

namespace MonBand.Tests.Core.Snmp;

[SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
public class SnmpTrafficRateServiceTests
{
    [Fact]
    public async Task Service_calculates_rate()
    {
        var trafficReadings = new NetworkTraffic?[]
        {
            new NetworkTraffic(100, 50),
            new NetworkTraffic(200, 100),
            new NetworkTraffic(300, 150)
        };

        await RunRateCalculationTestAsync(trafficReadings, 100, 50).ConfigureAwait(true);
    }

    [Fact]
    public async Task Service_calculates_rate_with_32bit_counter_wraparound()
    {
        var trafficReadings = new NetworkTraffic?[]
        {
            new NetworkTraffic(uint.MaxValue - 100, uint.MaxValue - 50, false),
            new NetworkTraffic(uint.MaxValue, uint.MaxValue, false),
            new NetworkTraffic(100, 50, false),
            new NetworkTraffic(200, 100, false)
        };

        await RunRateCalculationTestAsync(trafficReadings, 100, 50).ConfigureAwait(true);
    }
    
    [Fact]
    public async Task Service_calculates_rate_with_64bit_counter_wraparound()
    {
        var trafficReadings = new NetworkTraffic?[]
        {
            new NetworkTraffic(ulong.MaxValue - 100, ulong.MaxValue - 50),
            new NetworkTraffic(ulong.MaxValue, ulong.MaxValue),
            new NetworkTraffic(100, 50),
            new NetworkTraffic(200, 100)
        };

        // double to long conversion and back causes a rounding error
        await RunRateCalculationTestAsync(trafficReadings, 100, 50).ConfigureAwait(true);
    }

    static async Task RunRateCalculationTestAsync(
        NetworkTraffic?[] inputTrafficReadings,
        ulong expectedInBytesRate,
        ulong expectedOutBytesRate)
    {
        const byte pollIntervalSeconds = 1;

        var timeProvider = new MockTimeProvider();

        var trafficQuery = A.Fake<ISnmpTrafficQuery>();
        A.CallTo(() => trafficQuery.GetTotalTrafficBytesAsync(A<CancellationToken>.Ignored))
            .ReturnsNextFromSequence(inputTrafficReadings);

        using var service = new SnmpTrafficRateService(
            trafficQuery,
            pollIntervalSeconds,
            timeProvider,
            new NullLoggerFactory(),
            (interval, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                timeProvider.Advance(interval);
                return Task.CompletedTask;
            });

        var batchingBlock = new BatchBlock<NetworkTraffic>(2);
        var bufferBlock = new BufferBlock<NetworkTraffic>();
        bufferBlock.LinkTo(batchingBlock);

        service.TrafficRateUpdated += (_, t) => bufferBlock.Post(t);
        service.Start();

        var trafficRates = await batchingBlock.ReceiveAsync().ConfigureAwait(true);

        foreach (var trafficRate in trafficRates)
        {
            trafficRate.InBytes.Should().BeCloseTo(expectedInBytesRate, 1);
            trafficRate.OutBytes.Should().BeCloseTo(expectedOutBytesRate, 1);
        }
    }
}
