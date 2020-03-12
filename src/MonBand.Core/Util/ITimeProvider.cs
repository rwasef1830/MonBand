using System;

namespace MonBand.Core.Util
{
    public interface ITimeProvider
    {
        DateTimeOffset UtcNow { get; }
        IStopwatch CreateStopwatch();
    }
}
