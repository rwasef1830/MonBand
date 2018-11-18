using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Core.Snmp;
using MonBand.Core.Util;
using MonBand.Windows.Models;
using MonBand.Windows.Settings;
using OxyPlot;
using OxyPlot.Wpf;

namespace MonBand.Windows.UI
{
    public partial class DeskbandControl
    {
        readonly IDictionary<
            ITrafficRateService,
            (BandwidthPlotModel Model,
            TextBlock DownTextBlock,
            TextBlock UpTextBlock,
            TextBlock MaximumTextBlock)> _objectsByService;

        public DeskbandControl() : this(AppSettings.Load()) { }

        public DeskbandControl(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            this._objectsByService =
                new Dictionary<ITrafficRateService, (BandwidthPlotModel, TextBlock, TextBlock, TextBlock)>();

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

                var maximumTextBlock = new TextBlock { Text = "Maximum: 0.0 Mbps" };

                var container = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
                    RowDefinitions = { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } },
                    ToolTip = new Label
                    {
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Children =
                            {
                                new TextBlock { Text = snmpPoller.ToString() },
                                maximumTextBlock
                            }
                        }
                    }
                };

                var plotModel = new BandwidthPlotModel(100)
                {
                    PlotMargins = new OxyThickness(0),
                    Padding = new OxyThickness(0),
                    IsLegendVisible = false,
                    BandwidthAxis = { IsAxisVisible = false }
                };
                trafficRateService.TrafficRateUpdated += this.HandleTrafficRateUpdated;

                var plot = new PlotView { Model = plotModel };
                container.Children.Add(plot);

                var textBlockStackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Background = Brushes.White
                };
                var downTextBlock = new TextBlock
                {
                    FontSize = 10,
                    Foreground = Brushes.Blue,
                    Margin = new Thickness(0, 0, 2, 0),
                    Text = "D: -"
                };
                var upTextBlock = new TextBlock
                {
                    FontSize = 10,
                    Foreground = Brushes.Red,
                    Margin = new Thickness(0, 0, 2, 0),
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

                this._objectsByService[trafficRateService] = (
                    plotModel,
                    downTextBlock,
                    upTextBlock,
                    maximumTextBlock);

                trafficRateService.Start();
            }
        }

        void HandleTrafficRateUpdated(object sender, NetworkTraffic traffic)
        {
            var objects = this._objectsByService[(ITrafficRateService)sender];

            var megabits = traffic.AsMegabits();
            objects.Model.AddTraffic(megabits.InMegabits, megabits.OutMegabits);

            this.Dispatcher.Invoke(() =>
            {
                objects.DownTextBlock.Text = $"D: {megabits.InMegabits:F1} Mbps";
                objects.UpTextBlock.Text = $"U: {megabits.OutMegabits:F1} Mbps";
                objects.Model.InvalidatePlot(true);
                objects.MaximumTextBlock.Text = $"Maximum: {objects.Model.BandwidthAxis.DataMaximum:F1} Mbps";
            });
        }
    }
}
