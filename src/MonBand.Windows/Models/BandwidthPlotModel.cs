using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace MonBand.Windows.Models;

public class BandwidthPlotModel : PlotModel
{
    readonly byte _maxPoints;
    
    public AreaSeries DownloadBandwidthSeries { get; }
    public AreaSeries UploadBandwidthSeries { get; }
    public LinearAxis BandwidthAxis { get; }

    public BandwidthPlotModel(byte maxPoints)
    {
        this._maxPoints = maxPoints;

        this.DownloadBandwidthSeries = new AreaSeries
        {
            Title = "Download",
            Color = OxyColor.FromArgb(255, 0, 0, 255),
            Fill = OxyColor.FromArgb(255, 0, 0, 255)
        };

        this.UploadBandwidthSeries = new AreaSeries
        {
            Title = "Upload",
            Color = OxyColor.FromArgb(180, 255, 0, 0),
            Fill = OxyColor.FromArgb(180, 255, 0, 0)
        };

        this.Series.Add(this.DownloadBandwidthSeries);
        this.Series.Add(this.UploadBandwidthSeries);

        var downloadBandwidthAxis = new LinearAxis
        {
            TickStyle = TickStyle.Inside,
            Position = AxisPosition.Left,
            Title = "Bandwidth",
            Unit = "Mbps",
            IntervalLength = 20,
            TitleFontSize = 10,
            AbsoluteMinimum = 0
        };

        var uploadBandwidthAxis = new LinearAxis
        {
            TickStyle = TickStyle.None,
            Position = AxisPosition.Bottom,
            IsAxisVisible = false
        };

        this.BandwidthAxis = downloadBandwidthAxis;
        this.Axes.Add(downloadBandwidthAxis);
        this.Axes.Add(uploadBandwidthAxis);

        var primaryLegend = new Legend
        {
            LegendMargin = 0,
            LegendFontSize = 10,
            LegendPlacement = LegendPlacement.Inside,
            LegendPosition = LegendPosition.LeftTop,
            LegendItemSpacing = 0
        };
        this.Legends.Add(primaryLegend);

        this.Reset();
    }

    public void Reset()
    {
        this.FillDataPoints(this.DownloadBandwidthSeries);
        this.FillDataPoints(this.UploadBandwidthSeries);
    }

    void FillDataPoints(DataPointSeries series)
    {
        series.Points.Clear();
        for (int i = 0; i < this._maxPoints; i++)
        {
            series.Points.Add(new DataPoint(i, 0));
        }
    }

    public void AddTraffic(double inMegabits, double outMegabits)
    {
        this.AddBandwidthDataPoint(inMegabits, this.DownloadBandwidthSeries);
        this.AddBandwidthDataPoint(outMegabits, this.UploadBandwidthSeries);
    }

    void AddBandwidthDataPoint(double megabits, DataPointSeries series)
    {
        double x = series.Points.Count > 0
            ? series.Points[^1].X + 1
            : 0;

        if (series.Points.Count >= this._maxPoints)
        {
            series.Points.RemoveAt(0);
        }

        series.Points.Add(new DataPoint(x, megabits));
    }
}
