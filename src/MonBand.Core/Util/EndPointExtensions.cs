using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MonBand.Core.Util
{
    public static class EndPointExtensions
    {
        public static async Task<IPEndPoint> ResolveAsync(this EndPoint endPoint)
        {
            switch (endPoint)
            {
                case null:
                    throw new ArgumentNullException(nameof(endPoint));

                case IPEndPoint ipEndPoint:
                    return ipEndPoint;

                case DnsEndPoint dnsEndPoint:
                {
                    var ipAddresses = await Dns.GetHostAddressesAsync(dnsEndPoint.Host).ConfigureAwait(false);
                    var firstIpAddress = ipAddresses.First();
                    return new IPEndPoint(firstIpAddress, dnsEndPoint.Port);
                }

                default:
                    throw new NotSupportedException(
                        $"EndPoint of type '{endPoint.GetType().FullName}' is not supported.");
            }
        }
    }
}
