using System;
using System.Collections.Generic;
using System.Threading;
using MonBand.Core.Util.Time;

namespace MonBand.Tests.TestDoubles;

class MockTimeProvider : ITimeProvider
{
    readonly IList<MockStopwatch> _createdMockStopwatches;
    long _utcTicks;

    public DateTimeOffset UtcNow => new(Volatile.Read(ref this._utcTicks), TimeSpan.Zero);

    public MockTimeProvider() : this(DateTimeOffset.Parse("2000-01-01 00:00:00 +00:00")) { }

    public MockTimeProvider(DateTimeOffset initial)
    {
        Volatile.Write(ref this._utcTicks, initial.UtcTicks);
        this._createdMockStopwatches = new List<MockStopwatch>();
    }

    public void Advance(TimeSpan period)
    {
        Interlocked.Add(ref this._utcTicks, period.Ticks);

        lock (this._createdMockStopwatches)
        {
            foreach (var mockStopwatch in this._createdMockStopwatches)
            {
                mockStopwatch.NotifyTimePassed(period);
            }
        }
    }

    public IStopwatch CreateStopwatch()
    {
        var stopwatch = new MockStopwatch();
            
        lock (this._createdMockStopwatches)
        {
            this._createdMockStopwatches.Add(stopwatch);
        }

        return stopwatch;
    }
}
