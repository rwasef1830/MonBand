using OxyPlot;
using OxyPlot.Series;

namespace MonBand.Windows.UI.Helpers
{
    static class BandwidthSeriesHelper
    {
        const int c_MaxPlotPoints = 100;

        public static void Reset(DataPointSeries series)
        {
            series.Points.Clear();
            for (int i = 0; i < c_MaxPlotPoints; i++)
            {
                series.Points.Add(new DataPoint(i, 0));
            }
        }

        public static void AddPoint(long bytes, DataPointSeries series)
        {
            double x = series.Points.Count > 0
                ? series.Points[series.Points.Count - 1].X + 1
                : 0;
            double y = ConvertToMegabits(bytes);

            if (series.Points.Count >= c_MaxPlotPoints)
            {
                series.Points.RemoveAt(0);
            }

            series.Points.Add(new DataPoint(x, y));
        }

        public static double ConvertToMegabits(long bytes)
        {
            return (double)bytes * 8 / (1000 * 1000);
        }
    }
}
