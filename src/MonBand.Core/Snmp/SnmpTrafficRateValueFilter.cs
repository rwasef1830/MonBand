using System;
using MonBand.Core.Util.Statistics;

namespace MonBand.Core.Snmp;

class SnmpTrafficRateValueFilter
{
    readonly ZScore _zScore;
    double _lastNonPeakValue;

    public SnmpTrafficRateValueFilter(uint pollIntervalSeconds)
    {
        // These numbers have been picked carefully to try to catch the SNMP double-interval-read spike
        // which happens due to the counters getting updated twice during the imprecise poll interval
        // while also not discarding real traffic spikes or rate adjustments due to throttling.
        this._zScore = new ZScore(Math.Max(4 - (int)pollIntervalSeconds, 2), 2, 1);
    }

    public double FilterValue(double value)
    {
        var addResult = this._zScore.Add(value);

        switch (addResult.PeakType)
        {
            case ZScorePeakType.None:
            case ZScorePeakType.BelowAverage:
                this._lastNonPeakValue = value;
                return value;

            case ZScorePeakType.AboveAverage:
                return this._lastNonPeakValue;

            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
