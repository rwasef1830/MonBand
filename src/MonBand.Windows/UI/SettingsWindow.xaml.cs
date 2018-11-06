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
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MonBand.Windows.UI
{
    public partial class SettingsWindow
    {
        const int c_MaxPlotPoints = 100;

        DataPointSeries _downloadBandwidthSeries;
        DataPointSeries _uploadBandwidthSeries;
        ITrafficRateService _trafficRateService;
        CancellationTokenSource _dnsCancellationTokenSource;

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

            this._dnsCancellationTokenSource = new CancellationTokenSource();
            this.ListBoxMonitors.SelectionChanged += this.HandleListBoxMonitorsSelectionChanged;

            if (this.ListBoxMonitors.Items.Count > 0)
            {
                this.ListBoxMonitors.SelectedItem = this.ListBoxMonitors.Items[0];
            }
        }

        void InitializePlotModel()
        {
            this._downloadBandwidthSeries = new LineSeries
            {
                Title = "Download",
                Color = OxyColor.Parse("#0000FF")
            };

            this._uploadBandwidthSeries = new LineSeries
            {
                Title = "Upload",
                Color = OxyColor.Parse("#FF0000")
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
                        TitleFontSize = 10
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

            var oldDnsCancellationTokenSource = Interlocked.Exchange(
                ref this._dnsCancellationTokenSource,
                new CancellationTokenSource());
            oldDnsCancellationTokenSource.Cancel();

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

        async void UpdateTrafficRateService(SnmpPollerConfig config)
        {
            var cancellationTokenSource = Volatile.Read(ref this._dnsCancellationTokenSource);

            try
            {
                this.ResetLineSeries();

                var ipAddresses = await Dns.GetHostAddressesAsync(config.Address)
                    .WithCancellation(cancellationTokenSource.Token)
                    .ConfigureAwait(true);

                var remoteEndPoint = new IPEndPoint(ipAddresses.First(), config.Port);
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
                series.Points.Clear();
                for (int i = 0; i < c_MaxPlotPoints; i++)
                {
                    series.Points.Add(new DataPoint(i, 0));
                }
            }

            this.Dispatcher.Invoke(() => this.BandwidthPlotModel.InvalidatePlot(true));
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            AddPointToLineSeries((double)traffic.InBytes * 8 / (1024 * 1024), this._downloadBandwidthSeries);
            AddPointToLineSeries((double)traffic.OutBytes * 8 / (1024 * 1024), this._uploadBandwidthSeries);
            this.Dispatcher.Invoke(() => this.BandwidthPlotModel.InvalidatePlot(true));
        }

        static void AddPointToLineSeries(double value, DataPointSeries series)
        {
            double x = series.Points.Count > 0
                ? series.Points[series.Points.Count - 1].X + 1
                : 0;
            double y = value;

            if (series.Points.Count >= c_MaxPlotPoints)
            {
                series.Points.RemoveAt(0);
            }

            series.Points.Add(new DataPoint(x, y));
        }
    }
}
