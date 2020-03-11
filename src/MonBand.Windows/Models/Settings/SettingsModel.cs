using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace MonBand.Windows.Models.Settings
{
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public class SettingsModel
    {
        public LogLevel LogLevel { get; set; }
        public IList<SnmpPollerConfig> SnmpPollers { get; set; }
        public IList<PerformanceCounterPollerConfig> PerformanceCounterPollers { get; set; }

        public SettingsModel()
        {
            this.LogLevel = LogLevel.Information;
            this.SnmpPollers = new List<SnmpPollerConfig>();
            this.PerformanceCounterPollers = new List<PerformanceCounterPollerConfig>();
        }
    }
}
