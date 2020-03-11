using System;
using Microsoft.Extensions.Logging;

namespace MonBand.Windows.Bootstrap
{
    public class LogLevelSignal
    {
        public event EventHandler<LogLevel> LoggingLevelChanged;

        public void Update(LogLevel newLogLevel)
        {
            this.LoggingLevelChanged?.Invoke(this, newLogLevel);
        }
    }
}
