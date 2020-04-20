using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public class SnmpNamedInterfaceTrafficQuery : ISnmpTrafficQuery
    {
        readonly EndPoint _remoteEndPoint;
        readonly string _community;
        readonly ISnmpInterfaceQuery _interfaceQuery;
        SnmpTrafficQuery _currentTrafficQuery;

        public string InterfaceId { get; }

        public SnmpNamedInterfaceTrafficQuery(EndPoint remoteEndPoint, string community, string interfaceName)
        {
            this._remoteEndPoint = remoteEndPoint;
            this._community = community;
            this.InterfaceId = interfaceName;
            this._interfaceQuery = new SnmpInterfaceQuery(remoteEndPoint, community);
        }

        public async Task<NetworkTraffic?> GetTotalTrafficBytesAsync(CancellationToken cancellationToken)
        {
            if (this._currentTrafficQuery == null
                && !await this.ResolveInterfaceOidAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var queryResult = await this._currentTrafficQuery
                .GetTotalTrafficBytesAsync(cancellationToken)
                .ConfigureAwait(false);

            return queryResult == null && await this.ResolveInterfaceOidAsync(cancellationToken).ConfigureAwait(false)
                ? await this._currentTrafficQuery
                    .GetTotalTrafficBytesAsync(cancellationToken)
                    .ConfigureAwait(false)
                : queryResult;
        }

        async Task<bool> ResolveInterfaceOidAsync(CancellationToken cancellationToken)
        {
            var idsByName = await this._interfaceQuery
                .GetIdsByNameAsync(cancellationToken)
                .ConfigureAwait(false);

            if (idsByName.TryGetValue(this.InterfaceId, out var interfaceOid))
            {
                this._currentTrafficQuery = new SnmpTrafficQuery(
                    this._remoteEndPoint,
                    this._community,
                    interfaceOid);
                return true;
            }

            this._currentTrafficQuery = null;
            return false;
        }
    }
}
