using System;
using JetBrains.Annotations;

namespace MonBand.Core;

[PublicAPI]
public interface ITrafficRateService : IDisposable
{
    event EventHandler<NetworkTraffic> TrafficRateUpdated;
    void Start();
}
