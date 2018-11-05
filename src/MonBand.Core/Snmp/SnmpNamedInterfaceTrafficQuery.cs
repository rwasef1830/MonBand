using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public class SnmpNamedInterfaceTrafficQuery : ISnmpTrafficQuery
    {
        readonly IPEndPoint _remoteEndPoint;
        readonly string _community;
        readonly string _interfaceName;
        readonly ISnmpInterfaceQuery _interfaceQuery;

        public SnmpNamedInterfaceTrafficQuery(IPEndPoint remoteEndPoint, string community, string interfaceName)
        {
            this._remoteEndPoint = remoteEndPoint;
            this._community = community;
            this._interfaceName = interfaceName;
            this._interfaceQuery = new SnmpInterfaceQuery(remoteEndPoint, community);
        }

        public async Task<NetworkTraffic> GetTotalTrafficBytesAsync(CancellationToken cancellationToken)
        {
            var idsByName = await this._interfaceQuery
                .GetIdsByNameAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!idsByName.TryGetValue(this._interfaceName, out var interfaceId))
            {
                return new NetworkTraffic();
            }

            return await new SnmpTrafficQuery(this._remoteEndPoint, this._community, interfaceId)
                .GetTotalTrafficBytesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
