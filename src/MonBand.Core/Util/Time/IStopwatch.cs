using System;
using JetBrains.Annotations;

namespace MonBand.Core.Util.Time;

[PublicAPI]
public interface IStopwatch
{
    bool IsRunning { get; }
    TimeSpan Elapsed { get; }
    void Start();
    void Stop();
    void Reset();
    void Restart();
}
