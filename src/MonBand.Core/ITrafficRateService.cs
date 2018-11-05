using System;

namespace MonBand.Core
{
    public interface ITrafficRateService : IDisposable
    {
        event EventHandler<NetworkTraffic> TrafficRateUpdated;
        void Start();
    }
}
