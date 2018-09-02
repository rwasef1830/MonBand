using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Settings;
using MonBand.Windows.UI.Commands;

namespace MonBand.Windows.UI
{
    public partial class SettingsWindow
    {
        public ObservableCollection<SnmpPollerConfig> SnmpPollers { get; }
        public ICommand AddMonitor { get; }
        public ICommand FetchInterfaces { get; }
        public ICommand DeleteMonitor { get; }
        public ICommand SaveAndApplyConfiguration { get; }

        public SettingsWindow(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this.SnmpPollers = new ObservableCollection<SnmpPollerConfig>(settings.SnmpPollers);

            this.AddMonitor = new DelegateCommand(
                _ => this.SnmpPollers.Add(
                    new SnmpPollerConfig
                    {
                        Address = "127.0.0.1",
                        Port = 161,
                        Community = "public"
                    }));

            this.FetchInterfaces = new FetchInterfacesCommand(this);
            this.DeleteMonitor = new DelegateCommand(o => this.SnmpPollers.Remove((SnmpPollerConfig)o));

            this.SaveAndApplyConfiguration = new DelegateCommand(
                _ =>
                {
                    settings.SnmpPollers = this.SnmpPollers.ToList();
                    settings.Save();
                    this.Close();
                });

            this.InitializeComponent();
            this.ListBoxMonitors.SelectionChanged += this.ListBoxMonitorsSelectionChanged;
        }

        void ListBoxMonitorsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.GridMonitorForm.Visibility = e.AddedItems.Count == 0 ? Visibility.Hidden : Visibility.Visible;
        }
    }
}
