﻿using System;

namespace MonBand.Core.Util.Time
{
    public interface IStopwatch
    {
        bool IsRunning { get; }
        TimeSpan Elapsed { get; }
        void Start();
        void Stop();
        void Reset();
        void Restart();
    }
}
