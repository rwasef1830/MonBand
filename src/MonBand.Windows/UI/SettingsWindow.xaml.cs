using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MonBand.Core.Util;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Settings;

namespace MonBand.Windows.UI
{
    partial class SettingsWindow
    {
        public ICommand SaveAndApplyConfiguration { get; }
        public ICommand Exit { get; }

        public SettingsWindow()
        {
            var settings = AppSettings.Load();

            this.SaveAndApplyConfiguration = new DelegateCommand(
                _ =>
                {
                    settings.SnmpPollers = this.SnmpMonitors.Pollers.ToList();
                    settings.PerformanceCounterPollers = this.PerformanceCounterMonitors.Pollers.ToList();
                    settings.Save();
                    CrossProcessSignal.Signal(App.ReloadEventName);
                    this.Close();
                });
            this.Exit = new DelegateCommand(_ => this.Close());

            this.InitializeComponent();

            this.SnmpMonitors.Pollers = new ObservableCollection<SnmpPollerConfig>(settings.SnmpPollers);
            this.PerformanceCounterMonitors.Pollers =
                new ObservableCollection<PerformanceCounterPollerConfig>(settings.PerformanceCounterPollers);
        }
    }
}
