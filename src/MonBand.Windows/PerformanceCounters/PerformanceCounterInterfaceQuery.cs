using System.Diagnostics;

namespace MonBand.Windows.PerformanceCounters;

public static class PerformanceCounterInterfaceQuery
{
    public static string[] GetInterfaceNames()
    {
        var category = new PerformanceCounterCategory("Network Interface");
        return category.GetInstanceNames();
    }
}