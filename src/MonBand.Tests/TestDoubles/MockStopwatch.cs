using System;
using MonBand.Core.Util;

namespace MonBand.Tests.TestDoubles
{
    class MockStopwatch : IStopwatch
    {
        readonly object _stateChangeLocker = new object();

        bool _isRunning;
        long _elapsedTicks;

        public bool IsRunning
        {
            get
            {
                lock (this._stateChangeLocker)
                {
                    return this._isRunning;
                }
            }
        }

        public TimeSpan Elapsed
        {
            get
            {
                lock (this._stateChangeLocker)
                {
                    return new TimeSpan(this._elapsedTicks);
                }
            }
        }

        public MockStopwatch()
        {
            this.Reset();
        }

        public void Start()
        {
            lock (this._stateChangeLocker)
            {
                this._isRunning = true;
            }
        }

        public void Stop()
        {
            lock (this._stateChangeLocker)
            {
                this._isRunning = false;
            }
        }

        public void Reset()
        {
            lock (this._stateChangeLocker)
            {
                this.Stop();
                this._elapsedTicks = 0;
            }
        }

        public void Restart()
        {
            lock (this._stateChangeLocker)
            {
                this.Stop();
                this.Reset();
                this.Start();
            }
        }

        public void NotifyTimePassed(TimeSpan timePassed)
        {
            lock (this._stateChangeLocker)
            {
                if (this.IsRunning)
                {
                    this._elapsedTicks += timePassed.Ticks;
                }
            }
        }
    }
}
