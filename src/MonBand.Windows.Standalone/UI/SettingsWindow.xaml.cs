using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using MonBand.Core.Util.Threading;
using MonBand.Windows.Bootstrap;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Models.Settings;
using MonBand.Windows.Services;

namespace MonBand.Windows.Standalone.UI;

partial class SettingsWindow
{
    public ILoggerFactory LoggerFactory { get; }
    public SettingsModel Settings { get; }
    public IReadOnlyList<LogLevel> LogLevels { get; }
    public ICommand SaveAndApplyConfigurationCommand { get; }
    public ICommand ExitCommand { get; }

    public SettingsWindow(
        ILoggerFactory loggerFactory,
        LogLevelSignal logLevelSignal,
        IAppSettingsService appSettingsService,
        CrossProcessSignal processSignal)
    {
        this.LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        if (appSettingsService == null)
        {
            throw new ArgumentNullException(nameof(appSettingsService));
        }

        if (processSignal == null)
        {
            throw new ArgumentNullException(nameof(processSignal));
        }

        this.Settings = appSettingsService.LoadOrCreate<SettingsModel>();
        logLevelSignal.Update(this.Settings.LogLevel);

        this.LogLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>().ToList();

        this.SaveAndApplyConfigurationCommand = new DelegateCommand(
            _ =>
            {
                this.Settings.SnmpPollers = this.SnmpMonitors.Pollers.ToList();
                this.Settings.PerformanceCounterPollers = this.PerformanceCounterMonitors.Pollers.ToList();
                appSettingsService.Save(this.Settings);
                logLevelSignal.Update(this.Settings.LogLevel);
                processSignal.Signal();
                this.Close();
            });
        this.ExitCommand = new DelegateCommand(_ => this.Close());

        this.InitializeComponent();

        this.SnmpMonitors.Pollers = new ObservableCollection<SnmpPollerConfig>(this.Settings.SnmpPollers);
        this.PerformanceCounterMonitors.Pollers =
            new ObservableCollection<PerformanceCounterPollerConfig>(this.Settings.PerformanceCounterPollers);
    }
}
