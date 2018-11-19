using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MonBand.Core;
using MonBand.Windows.Models;
using OxyPlot;
using OxyPlot.Wpf;

namespace MonBand.Windows.UI
{
    public class CompactMonitorView : UserControl
    {
        public static readonly DependencyProperty MonitorNameProperty = DependencyProperty.Register(
            nameof(MonitorName),
            typeof(string),
            typeof(CompactMonitorView));

        readonly BandwidthPlotModel _plotModel;
        TextBlock _textBlockMaximum;
        TextBlock _textBlockDownloadBandwidth;
        TextBlock _textBlockUploadBandwidth;

        public string MonitorName
        {
            get => (string)this.GetValue(MonitorNameProperty);
            set => this.SetValue(MonitorNameProperty, value);
        }

        public CompactMonitorView()
        {
            this._plotModel = new BandwidthPlotModel(100)
            {
                PlotMargins = new OxyThickness(0),
                Padding = new OxyThickness(0),
                IsLegendVisible = false,
                BandwidthAxis = { IsAxisVisible = false }
            };

            this.Content = this.CreateControls();
        }

        Grid CreateControls()
        {
            var monitorNameTextBlock = new TextBlock();
            monitorNameTextBlock.SetBinding(
                TextBlock.TextProperty,
                new Binding(nameof(this.MonitorName)) { Source = this });

            this._textBlockMaximum = new TextBlock { Text = "Maximum: 0.0 Mbps" };

            this._textBlockDownloadBandwidth = new TextBlock
            {
                FontSize = 10,
                Foreground = Brushes.Blue,
                Text = "D: -",
                Margin = new Thickness(1, 1, 2, 0)
            };
            this._textBlockUploadBandwidth = new TextBlock
            {
                FontSize = 10,
                Foreground = Brushes.Red,
                Text = "U: -",
                Margin = new Thickness(1, 0, 2, 2)
            };

            var rootGrid = new Grid
            {
                ColumnDefinitions =
                {
                    { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } }
                },
                RowDefinitions =
                {
                    { new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } }
                },
                ToolTip = new Label
                {
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Children =
                        {
                            monitorNameTextBlock,
                            this._textBlockMaximum
                        }
                    }
                },
                Children =
                {
                    new PlotView { Model = this._plotModel },
                    new Label
                    {
                        Padding = new Thickness(1),
                        Content = new StackPanel
                        {
                            Orientation = Orientation.Vertical,
                            Background = Brushes.White,
                            Children =
                            {
                                this._textBlockDownloadBandwidth,
                                this._textBlockUploadBandwidth
                            }
                        }
                    }
                }
            };


            return rootGrid;
        }

        public void AddTraffic(NetworkTraffic traffic)
        {
            var megabits = traffic.AsMegabits();
            this._plotModel.AddTraffic(megabits.InMegabits, megabits.OutMegabits);
            this._textBlockDownloadBandwidth.Text = $"D: {megabits.InMegabits:F1} Mbps";
            this._textBlockUploadBandwidth.Text = $"U: {megabits.OutMegabits:F1} Mbps";
            this._plotModel.InvalidatePlot(true);
            this._textBlockMaximum.Text = $"Maximum: {this._plotModel.BandwidthAxis.DataMaximum:F1} Mbps";
        }
    }
}
