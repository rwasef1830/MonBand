using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Core.Util;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Models;
using MonBand.Windows.Settings;
using MonBand.Windows.UI.Commands;

namespace MonBand.Windows.UI
{
    partial class SettingsWindow
    {
        ITrafficRateService _trafficRateService;

        public ObservableCollection<SnmpPollerConfig> SnmpPollers { get; }
        public ICommand AddMonitor { get; }
        public ICommand FetchInterfaces { get; }
        public ICommand DeleteMonitor { get; }
        public ICommand SaveAndApplyConfiguration { get; }
        public BandwidthPlotModel PlotModel { get; }

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
                    CrossProcessSignal.Signal(App.ReloadEventName);
                    this.Close();
                });

            this.PlotModel = new BandwidthPlotModel(100);
            this.InitializeComponent();

            this.ListBoxMonitors.SelectionChanged += this.HandleListBoxMonitorsSelectionChanged;

            if (this.ListBoxMonitors.Items.Count > 0)
            {
                this.ListBoxMonitors.SelectedItem = this.ListBoxMonitors.Items[0];
            }
        }

        void HandleListBoxMonitorsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.GridMonitorForm.Visibility = e.AddedItems.Count == 0 ? Visibility.Hidden : Visibility.Visible;

            foreach (SnmpPollerConfig config in e.RemovedItems)
            {
                config.PropertyChanged -= this.HandleConfigOnPropertyChanged;
            }

            foreach (SnmpPollerConfig config in e.AddedItems)
            {
                config.PropertyChanged += this.HandleConfigOnPropertyChanged;
                this.UpdateTrafficRateService(config);
            }
        }

        void HandleConfigOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            this.UpdateTrafficRateService((SnmpPollerConfig)sender);
        }

        void UpdateTrafficRateService(SnmpPollerConfig config)
        {
            try
            {
                var remoteEndPoint = new DnsEndPoint(config.Address, config.Port);
                var newTrafficRateService = new SnmpPollingTrafficRateService(
                    new SnmpNamedInterfaceTrafficQuery(remoteEndPoint, config.Community, config.InterfaceName),
                    3,
                    SystemTimeProvider.Instance,
                    App.LoggerFactory);

                var oldTrafficRateService = Interlocked.Exchange(
                    ref this._trafficRateService,
                    newTrafficRateService);
                if (oldTrafficRateService != null)
                {
                    oldTrafficRateService.TrafficRateUpdated -= this.HandleTrafficRateUpdated;
                    oldTrafficRateService.Dispose();
                }

                newTrafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;
                newTrafficRateService.Start();
            }
            catch (SocketException)
            {
                // ignore
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    ex.Message,
                    "Error updating bandwidth chart",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error,
                    MessageBoxResult.OK);
            }
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            var megabits = traffic.AsMegabits();
            this.PlotModel.AddTraffic(megabits.InMegabits, megabits.OutMegabits);
            this.Dispatcher.Invoke(() => this.PlotModel.InvalidatePlot(true));
        }
    }
}
