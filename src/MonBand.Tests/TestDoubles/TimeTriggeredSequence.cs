using System;
using System.Collections;
using System.Collections.Generic;
using MonBand.Core.Util;

namespace MonBand.Tests.TestDoubles
{
    class TimeTriggeredSequence<T> : IEnumerable<T>
    {
        readonly ITimeProvider _timeProvider;
        readonly IReadOnlyList<T> _sequence;
        readonly TimeSpan _moveNextAfterInterval;
        readonly DateTimeOffset _initialTime;

        public TimeTriggeredSequence(
            ITimeProvider timeProvider,
            IReadOnlyList<T> sequence,
            TimeSpan moveNextAfterInterval)
        {
            if (moveNextAfterInterval <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(moveNextAfterInterval));
            }

            this._timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this._sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
            this._moveNextAfterInterval = moveNextAfterInterval;
            this._initialTime = this._timeProvider.UtcNow;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new TimeTriggeredSequenceEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        class TimeTriggeredSequenceEnumerator : IEnumerator<T>
        {
            readonly TimeTriggeredSequence<T> _sequence;
            int _index;

            public TimeTriggeredSequenceEnumerator(TimeTriggeredSequence<T> sequence)
            {
                this._sequence = sequence;
            }

            public bool MoveNext()
            {
                var moveAfterIntervalMs = this._sequence._moveNextAfterInterval.TotalMilliseconds;
                var timeDelta = this._sequence._timeProvider.UtcNow - this._sequence._initialTime;
                var timeDeltaMs = timeDelta.TotalMilliseconds;
                var newIndex = (int)(timeDeltaMs / moveAfterIntervalMs);

                if (newIndex >= this._sequence._sequence.Count)
                {
                    return false;
                }

                this._index = newIndex;
                return true;
            }

            public void Reset()
            {
                throw new NotSupportedException();
            }

            public T Current => this._sequence._sequence[this._index];

            object IEnumerator.Current => this.Current;

            public void Dispose()
            {
            }
        }
    }
}
