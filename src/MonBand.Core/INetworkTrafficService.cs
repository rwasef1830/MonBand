using System;

namespace MonBand.Core
{
    public interface INetworkTrafficService : IDisposable
    {
        event EventHandler<NetworkTraffic> TrafficRateUpdated;
        void Start();
    }
}
