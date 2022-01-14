using System;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace MonBand.Windows.Models
{
    public class BandwidthPlotModel : PlotModel
    {
        readonly byte _maxPoints;

        public LinearAxis BandwidthAxis => this.Axes.Count > 0 ? this.Axes[0] as LinearAxis : null;
        AreaSeries DownloadBandwidthSeries => this.Series.Count > 0 ? this.Series[0] as AreaSeries : null;
        AreaSeries UploadBandwidthSeries => this.Series.Count > 1 ? this.Series[1] as AreaSeries : null;

        public BandwidthPlotModel(byte maxPoints)
        {
            this._maxPoints = maxPoints;

            var downloadBandwidthSeries = new AreaSeries
            {
                Title = "Download",
                Color = OxyColor.FromArgb(255, 0, 0, 255)
            };

            var uploadBandwidthSeries = new AreaSeries
            {
                Title = "Upload",
                Color = OxyColor.FromArgb(255, 255, 0, 0)
            };

            this.Series.Add(downloadBandwidthSeries);
            this.Series.Add(uploadBandwidthSeries);

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
            if (series == null) throw new ArgumentNullException(nameof(series));

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
                ? series.Points[series.Points.Count - 1].X + 1
                : 0;

            if (series.Points.Count >= this._maxPoints)
            {
                series.Points.RemoveAt(0);
            }

            series.Points.Add(new DataPoint(x, megabits));
        }
    }
}
