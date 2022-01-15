using System;
using MonBand.Core.Util.Collections;

namespace MonBand.Core.Util.Statistics;

// Inspired from https://stackoverflow.com/a/22640362/111830
sealed class ZScore
{
    readonly CircularBuffer<double> _window;
    readonly double _threshold;
    readonly double _influence;

    public ZScore(int lag, double threshold, double influence)
    {
        if (lag <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lag));
        }

        if (threshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }

        if (influence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(influence));
        }

        this._window = new CircularBuffer<double>(lag);
        this._threshold = threshold;
        this._influence = influence;
    }

    public ZScoreAddResult Add(double value)
    {
        var adjustedValue = value;

        if (!this._window.IsFull)
        {
            this._window.PushBack(value);
            return new ZScoreAddResult(ZScorePeakType.None, value);
        }

        var average = this.GetInputAverage();
        var standardDeviation = this.GetInputStandardDeviation(average);
        var peakType = ZScorePeakType.None;

        if (Math.Abs(value - average) > this._threshold * standardDeviation)
        {
            peakType = value > average ? ZScorePeakType.AboveAverage : ZScorePeakType.BelowAverage;
            adjustedValue = this._influence * value + (1 - this._influence) * this._window.Back();
        }

        this._window.PushBack(adjustedValue);
        return new ZScoreAddResult(peakType, adjustedValue);
    }

    double GetInputAverage()
    {
        var sum = 0d;
        foreach (var v in this._window)
        {
            sum += v;
        }

        var average = sum / this._window.Count;
        return average;
    }

    double GetInputStandardDeviation(double average)
    {
        var sumOfDerivation = 0d;
        foreach (var v in this._window)
        {
            sumOfDerivation += Math.Pow(v - average, 2);
        }

        var standardDeviation = Math.Sqrt(sumOfDerivation / (this._window.Count - 1));
        return standardDeviation;
    }
}

readonly struct ZScoreAddResult
{
    public ZScorePeakType PeakType { get; }
    public double AdjustedValue { get; }

    public ZScoreAddResult(ZScorePeakType peakType, double adjustedValue)
    {
        this.PeakType = peakType;
        this.AdjustedValue = adjustedValue;
    }

    public override string ToString()
    {
        return $"Peak: {this.PeakType}; AdjustedValue: {this.AdjustedValue}";
    }
}

enum ZScorePeakType
{
    BelowAverage = -1,
    None = 0,
    AboveAverage = 1
}
