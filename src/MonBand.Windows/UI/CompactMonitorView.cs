using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MonBand.Core;
using MonBand.Windows.Models;
using OxyPlot;

namespace MonBand.Windows.UI;

public class CompactMonitorView : UserControl
{
    public static readonly DependencyProperty MonitorNameProperty = DependencyProperty.Register(
        nameof(MonitorName),
        typeof(string),
        typeof(CompactMonitorView));

    public static readonly DependencyProperty LoggerFactoryProperty = DependencyProperty.Register(
        nameof(LoggerFactory),
        typeof(ILoggerFactory),
        typeof(CompactMonitorView),
        new PropertyMetadata
        {
            DefaultValue = NullLoggerFactory.Instance
        });

    readonly BandwidthPlotModel _plotModel;
    TextBlock _textBlockMaximum = null!;
    TextBlock _textBlockAverageDownloadBandwidth = null!;
    TextBlock _textBlockAverageUploadBandwidth = null!;
    TextBlock _textBlockDownloadBandwidth = null!;
    TextBlock _textBlockUploadBandwidth = null!;

    public string MonitorName
    {
        get => (string)this.GetValue(MonitorNameProperty);
        set => this.SetValue(MonitorNameProperty, value);
    }
    
    public ILoggerFactory LoggerFactory
    {
        get => (ILoggerFactory)this.GetValue(LoggerFactoryProperty);
        set => this.SetValue(LoggerFactoryProperty, value);
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
        this._textBlockAverageDownloadBandwidth = new TextBlock { Text = "Average download: 0.0 Mbps" };
        this._textBlockAverageUploadBandwidth = new TextBlock { Text = "Average upload: 0.0 Mbps" };

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
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            ToolTip = new Label
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        monitorNameTextBlock,
                        this._textBlockMaximum,
                        this._textBlockAverageDownloadBandwidth,
                        this._textBlockAverageUploadBandwidth
                    }
                }
            },
            Children =
            {
                this.CreatePlotView(),
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

    PlotView CreatePlotView()
    {
        var plotView = new PlotView
        {
            Model = this._plotModel
        };
        plotView.SetBinding(
            PlotView.LoggerFactoryProperty,
            new Binding(nameof(this.LoggerFactory)) { Source = this });
        return plotView;
    }

    public void AddTraffic(NetworkTraffic traffic)
    {
        var (inMegabits, outMegabits) = traffic.AsMegabits();
        this._plotModel.AddTraffic(inMegabits, outMegabits);
        this._textBlockDownloadBandwidth.Text = $"D: {inMegabits:F1} Mbps";
        this._textBlockUploadBandwidth.Text = $"U: {outMegabits:F1} Mbps";
        this._plotModel.InvalidatePlot(true);
        this._textBlockMaximum.Text = $"Maximum: {this._plotModel.BandwidthAxis.DataMaximum:F1} Mbps";

        var averageDownloadBandwidth = this._plotModel.DownloadBandwidthSeries.Points.Average(x => x.Y);
        var averageUploadBandwidth = this._plotModel.UploadBandwidthSeries.Points.Average(x => x.Y);
        this._textBlockAverageDownloadBandwidth.Text = $"Average download: {averageDownloadBandwidth:F1} Mbps";
        this._textBlockAverageUploadBandwidth.Text = $"Average upload: {averageUploadBandwidth:F2} Mbps";
    }
}
