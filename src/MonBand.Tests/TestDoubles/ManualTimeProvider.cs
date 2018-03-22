using System;
using System.Threading;
using MonBand.Core.Util;

namespace MonBand.Tests.TestDoubles
{
    class ManualTimeProvider : ITimeProvider
    {
        long _utcTicks;

        public DateTimeOffset UtcNow => new DateTimeOffset(Volatile.Read(ref this._utcTicks), TimeSpan.Zero);

        public ManualTimeProvider() : this(DateTimeOffset.Parse("2000-01-01 00:00:00 +00:00")) { }

        public ManualTimeProvider(DateTimeOffset initial)
        {
            Volatile.Write(ref this._utcTicks, initial.UtcTicks);
        }

        public void Advance(TimeSpan period)
        {
            Interlocked.Add(ref this._utcTicks, period.Ticks);
        }
    }
}
