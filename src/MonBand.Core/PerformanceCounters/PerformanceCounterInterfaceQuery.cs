using System.Diagnostics;

namespace MonBand.Core.PerformanceCounters;

public static class PerformanceCounterInterfaceQuery
{
    public static string[] GetInterfaceNames()
    {
        var category = new PerformanceCounterCategory("Network Interface");
        return category.GetInstanceNames();
    }
}