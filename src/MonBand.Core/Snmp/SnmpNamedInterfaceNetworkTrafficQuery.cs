using System.Net;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public class SnmpNamedInterfaceNetworkTrafficQuery : ISnmpNetworkTrafficQuery
    {
        readonly IPEndPoint _remoteEndPoint;
        readonly string _community;
        readonly string _interfaceName;
        readonly ISnmpNetworkInterfaceQuery _interfaceQuery;

        public SnmpNamedInterfaceNetworkTrafficQuery(IPEndPoint remoteEndPoint, string community, string interfaceName)
        {
            this._remoteEndPoint = remoteEndPoint;
            this._community = community;
            this._interfaceName = interfaceName;
            this._interfaceQuery = new SnmpNetworkInterfaceQuery(remoteEndPoint, community);
        }

        public async Task<NetworkTraffic> GetTotalTrafficBytesAsync()
        {
            var idsByName = await this._interfaceQuery.GetIdsByNameAsync().ConfigureAwait(false);
            if (!idsByName.TryGetValue(this._interfaceName, out var interfaceId))
            {
                return new NetworkTraffic();
            }

            return await new SnmpNetworkTrafficQuery(this._remoteEndPoint, this._community, interfaceId)
                .GetTotalTrafficBytesAsync().ConfigureAwait(false);
        }
    }
}
