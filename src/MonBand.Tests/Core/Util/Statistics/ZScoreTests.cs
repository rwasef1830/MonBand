using FluentAssertions;
using MonBand.Core.Util.Statistics;
using Xunit;

namespace MonBand.Tests.Core.Util.Statistics
{
    public class ZScoreTests
    {
        [Fact]
        public void Bandwidth_increase_scenario()
        {
            var zScore = new ZScore(5, 2, 1);
            var values = new[]
            {
                (0, ZScorePeakType.None, 0),
                (90, ZScorePeakType.None, 90),
                (90, ZScorePeakType.None, 90),
                (90, ZScorePeakType.None, 90),
                (90, ZScorePeakType.None, 90),
                (180, ZScorePeakType.AboveAverage, 180),
                (200, ZScorePeakType.AboveAverage, 200),
                (170, ZScorePeakType.None, 170),
                (150, ZScorePeakType.None, 150)
            };

            foreach (var (inputValue, expectedSignalIndicator, expectedAdjustedValue) in values)
            {
                var result = zScore.Add(inputValue);
                result.Should().BeEquivalentTo(new ZScoreAddResult(expectedSignalIndicator, expectedAdjustedValue));
            }
        }

        [Fact]
        public void Snmp_double_interval_rate_error_spike_during_climb_should_be_rejected()
        {
            var zScore = new ZScore(5, 2, 1);
            var values = new[]
            {
                (0, ZScorePeakType.None, 0),
                (90, ZScorePeakType.None, 90),
                (90, ZScorePeakType.None, 90),
                (90, ZScorePeakType.None, 90),
                (90, ZScorePeakType.None, 90),
                (180, ZScorePeakType.AboveAverage, 180),
                (90, ZScorePeakType.None, 90),
                (80, ZScorePeakType.None, 80),
                (10, ZScorePeakType.None, 10),
                (70, ZScorePeakType.None, 70),
                (5, ZScorePeakType.None, 5)
            };

            foreach (var (inputValue, expectedSignalIndicator, expectedAdjustedValue) in values)
            {
                var result = zScore.Add(inputValue);
                result.Should().BeEquivalentTo(new ZScoreAddResult(expectedSignalIndicator, expectedAdjustedValue));
            }
        }
    }
}
