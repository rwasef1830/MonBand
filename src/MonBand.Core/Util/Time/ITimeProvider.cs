using System;
using JetBrains.Annotations;

namespace MonBand.Core.Util.Time;

[PublicAPI]
public interface ITimeProvider
{
    DateTimeOffset UtcNow { get; }
    IStopwatch CreateStopwatch();
}
