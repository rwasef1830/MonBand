using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace MonBand.Windows.Models;

public class BandwidthPlotModel : PlotModel
{
    readonly byte _maxPoints;
    readonly AreaSeries _downloadBandwidthSeries;
    readonly AreaSeries _uploadBandwidthSeries;
    
    public LinearAxis BandwidthAxis { get; }

    public BandwidthPlotModel(byte maxPoints)
    {
        this._maxPoints = maxPoints;

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

        this.Series.Add(this._downloadBandwidthSeries);
        this.Series.Add(this._uploadBandwidthSeries);

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
        this.FillDataPoints(this._downloadBandwidthSeries);
        this.FillDataPoints(this._uploadBandwidthSeries);
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
        this.AddBandwidthDataPoint(inMegabits, this._downloadBandwidthSeries);
        this.AddBandwidthDataPoint(outMegabits, this._uploadBandwidthSeries);
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
