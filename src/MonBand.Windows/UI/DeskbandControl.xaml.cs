using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Core.Util;
using MonBand.Windows.Settings;
using MonBand.Windows.UI.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using DataPointSeries = OxyPlot.Series.DataPointSeries;
using LinearAxis = OxyPlot.Axes.LinearAxis;
using LineSeries = OxyPlot.Series.LineSeries;

namespace MonBand.Windows.UI
{
    public partial class DeskbandControl
    {
        readonly IDictionary<ITrafficRateService, (PlotModel Model, TextBlock UpTextBlock, TextBlock DownTextBlock)>
            _objectsByService;

        public DeskbandControl() : this(AppSettings.Load()) { }

        public DeskbandControl(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this._objectsByService = new Dictionary<ITrafficRateService, (PlotModel, TextBlock, TextBlock)>();

            this.InitializeComponent();
            this.InitializeMonitors(settings.SnmpPollers);
        }

        void InitializeMonitors(IList<SnmpPollerConfig> snmpPollers)
        {
            if (snmpPollers == null) throw new ArgumentNullException(nameof(snmpPollers));

            for (var i = 0; i < snmpPollers.Count; i++)
            {
                var snmpPoller = snmpPollers[i];

                var trafficRateService = new SnmpPollingTrafficRateService(
                    new SnmpNamedInterfaceTrafficQuery(
                        new DnsEndPoint(snmpPoller.Address, snmpPoller.Port),
                        snmpPoller.Community,
                        snmpPoller.InterfaceName),
                    3,
                    SystemTimeProvider.Instance,
                    new NullLoggerFactory());

                var container = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
                    RowDefinitions = { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
                    ToolTip = new TextBlock
                    {
                        Text = $"{snmpPoller.Address}:{snmpPoller.Port} - {snmpPoller.InterfaceName}"
                    }
                };

                var plotModel = this.CreatePlotModel();
                trafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;

                var plot = new PlotView { Model = plotModel };
                container.Children.Add(plot);

                var textBlockStackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var downTextBlock = new TextBlock
                {
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 255)),
                    Margin = new Thickness(0, 0, 5, 0),
                    Text = "D: -"
                };
                var upTextBlock = new TextBlock
                {
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)),
                    Text = "U: -"
                };
                textBlockStackPanel.Children.Add(downTextBlock);
                textBlockStackPanel.Children.Add(upTextBlock);

                var currentBandwidthLabel = new Label { Content = textBlockStackPanel, Padding = new Thickness(3) };
                container.Children.Add(currentBandwidthLabel);

                this.RootGrid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });
                Grid.SetColumn(container, i);
                this.RootGrid.Children.Add(container);

                this._objectsByService[trafficRateService] = (plotModel, upTextBlock, downTextBlock);

                trafficRateService.Start();
            }
        }

        PlotModel CreatePlotModel()
        {
            var plotModel = new PlotModel
            {
                PlotMargins = new OxyThickness(0),
                Padding = new OxyThickness(0),
                IsLegendVisible = false,
                Axes =
                {
                    new LinearAxis
                    {
                        TickStyle = TickStyle.Inside,
                        Position = AxisPosition.Left,
                        IsAxisVisible = false
                    },
                    new LinearAxis
                    {
                        TickStyle = TickStyle.None,
                        Position = AxisPosition.Bottom,
                        IsAxisVisible = false
                    }
                },
                Series =
                {
                    new LineSeries
                    {
                        Title = "Download",
                        Color = OxyColor.FromArgb(255, 0, 0, 255)
                    },
                    new LineSeries
                    {
                        Title = "Upload",
                        Color = OxyColor.FromArgb(255, 255, 0, 0)
                    }
                }
            };

            this.ResetLineSeries(plotModel);
            return plotModel;
        }

        void ResetLineSeries(PlotModel plotModel)
        {
            foreach (var series in plotModel.Series.OfType<LineSeries>())
            {
                BandwidthSeriesHelper.Reset(series);
            }

            this.Dispatcher.Invoke(() => plotModel.InvalidatePlot(true));
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            var objects = this._objectsByService[(ITrafficRateService)sender];

            BandwidthSeriesHelper.AddPoint(traffic.InBytes, (DataPointSeries)objects.Model.Series[0]);
            BandwidthSeriesHelper.AddPoint(traffic.OutBytes, (DataPointSeries)objects.Model.Series[1]);

            var inMegabits = BandwidthSeriesHelper.ConvertToMegabits(traffic.InBytes);
            var outMegabits = BandwidthSeriesHelper.ConvertToMegabits(traffic.OutBytes);

            this.Dispatcher.Invoke(() =>
            {
                objects.DownTextBlock.Text = $"D: {inMegabits:F1} Mbps";
                objects.UpTextBlock.Text = $"U: {outMegabits:F1} Mbps";
                objects.Model.InvalidatePlot(true);
            });
        }
    }
}
