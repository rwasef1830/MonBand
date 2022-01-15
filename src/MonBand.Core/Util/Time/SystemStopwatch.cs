using System;
using System.Diagnostics;

namespace MonBand.Core.Util.Time;

public class SystemStopwatch : IStopwatch
{
    readonly Stopwatch _stopwatch;

    public bool IsRunning => this._stopwatch.IsRunning;

    public TimeSpan Elapsed => this._stopwatch.Elapsed;

    public SystemStopwatch()
    {
        this._stopwatch = new Stopwatch();
    }

    public void Start() => this._stopwatch.Start();

    public void Stop() => this._stopwatch.Stop();

    public void Reset() => this._stopwatch.Reset();

    public void Restart() => this._stopwatch.Restart();
}