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
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Core.Util;
using MonBand.Windows.Infrastructure.Input;
using MonBand.Windows.Settings;
using MonBand.Windows.UI.Commands;
using MonBand.Windows.UI.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MonBand.Windows.UI
{
    public partial class SettingsWindow
    {
        DataPointSeries _downloadBandwidthSeries;
        DataPointSeries _uploadBandwidthSeries;
        ITrafficRateService _trafficRateService;

        public ObservableCollection<SnmpPollerConfig> SnmpPollers { get; }
        public ICommand AddMonitor { get; }
        public ICommand FetchInterfaces { get; }
        public ICommand DeleteMonitor { get; }
        public ICommand SaveAndApplyConfiguration { get; }
        public PlotModel BandwidthPlotModel { get; private set; }

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

            this.InitializePlotModel();
            this.InitializeComponent();

            this.ListBoxMonitors.SelectionChanged += this.HandleListBoxMonitorsSelectionChanged;

            if (this.ListBoxMonitors.Items.Count > 0)
            {
                this.ListBoxMonitors.SelectedItem = this.ListBoxMonitors.Items[0];
            }
        }

        void InitializePlotModel()
        {
            this._downloadBandwidthSeries = new AreaSeries
            {
                Title = "Download",
                Color = OxyColor.FromArgb(255, 0, 0, 255)
            };

            this._uploadBandwidthSeries = new AreaSeries
            {
                Title = "Upload",
                Color = OxyColor.FromArgb(255, 255, 0, 0)
            };

            this.BandwidthPlotModel = new PlotModel
            {
                LegendMargin = 0,
                LegendFontSize = 10,
                LegendPlacement = LegendPlacement.Inside,
                LegendPosition = LegendPosition.LeftTop,
                LegendItemSpacing = 0,
                Axes =
                {
                    new LinearAxis
                    {
                        TickStyle = TickStyle.Inside,
                        Position = AxisPosition.Left,
                        Title = "Bandwidth",
                        Unit = "Mbps",
                        IntervalLength = 20,
                        TitleFontSize = 10,
                        AbsoluteMinimum = 0
                    },
                    new LinearAxis
                    {
                        TickStyle = TickStyle.None,
                        Position = AxisPosition.Bottom,
                        IsAxisVisible = false
                    }
                },
                Series = { this._downloadBandwidthSeries, this._uploadBandwidthSeries }
            };

            this.ResetLineSeries();
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
                this.ResetLineSeries();

                var remoteEndPoint = new DnsEndPoint(config.Address, config.Port);
                var newTrafficRateService = new SnmpPollingTrafficRateService(
                    new SnmpNamedInterfaceTrafficQuery(remoteEndPoint, config.Community, config.InterfaceName),
                    3,
                    SystemTimeProvider.Instance,
                    new NullLoggerFactory());

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

        void ResetLineSeries()
        {
            foreach (var series in new[] { this._uploadBandwidthSeries, this._downloadBandwidthSeries })
            {
                BandwidthSeriesHelper.Reset(series);
            }

            this.Dispatcher.Invoke(() => this.BandwidthPlotModel.InvalidatePlot(true));
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            BandwidthSeriesHelper.AddPoint(traffic.InBytes, this._downloadBandwidthSeries);
            BandwidthSeriesHelper.AddPoint(traffic.OutBytes, this._uploadBandwidthSeries);
            this.Dispatcher.Invoke(() => this.BandwidthPlotModel.InvalidatePlot(true));
        }
    }
}
