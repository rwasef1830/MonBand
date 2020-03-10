using System;
using FluentAssertions;
using Xunit;

namespace MonBand.Tests.TestDoubles
{
    public class TimeTriggeredSequenceTests
    {
        [Fact]
        public void Enumerator_only_yields_next_item_when_time_interval_passes()
        {
            var timeProvider = new ManualTimeProvider();
            var moveInterval = TimeSpan.FromSeconds(1);

            var sequence = new TimeTriggeredSequence<string>(
                timeProvider,
                new[] { "Hello", "World" },
                moveInterval);

            using var enumerator = sequence.GetEnumerator();
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be("Hello");

            timeProvider.Advance(TimeSpan.FromSeconds(0.5));
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be("Hello");

            timeProvider.Advance(TimeSpan.FromSeconds(0.5));
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be("World");

            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be("World");

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            enumerator.MoveNext().Should().BeFalse();
        }
    }
}
