using System;
using Microsoft.Extensions.Logging;

namespace MonBand.Windows.Bootstrap;

public class LogLevelSignal
{
    public event EventHandler<LogLevel>? LogLevelChanged;

    public void Update(LogLevel newLogLevel)
    {
        this.LogLevelChanged?.Invoke(this, newLogLevel);
    }
}
