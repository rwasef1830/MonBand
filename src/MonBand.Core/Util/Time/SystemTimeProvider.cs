﻿using System;

namespace MonBand.Core.Util.Time;

public class SystemTimeProvider : ITimeProvider
{
    public static readonly ITimeProvider Instance = new SystemTimeProvider();

    SystemTimeProvider() { }

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public IStopwatch CreateStopwatch() => new SystemStopwatch();
}