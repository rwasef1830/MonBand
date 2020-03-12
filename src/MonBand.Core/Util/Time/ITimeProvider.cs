using System;

namespace MonBand.Core.Util.Time
{
    public interface ITimeProvider
    {
        DateTimeOffset UtcNow { get; }
        IStopwatch CreateStopwatch();
    }
}
