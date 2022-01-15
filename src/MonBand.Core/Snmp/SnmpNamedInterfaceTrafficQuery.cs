using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp;

public class SnmpNamedInterfaceTrafficQuery : ISnmpTrafficQuery
{
    readonly EndPoint _remoteEndPoint;
    readonly string _community;
    readonly ISnmpInterfaceQuery _interfaceQuery;
    SnmpTrafficQuery? _currentTrafficQuery;

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
        this._currentTrafficQuery ??= await this.TryGetCurrentTrafficQueryAsync(cancellationToken)
            .ConfigureAwait(false);
        if (this._currentTrafficQuery == null)
        {
            return null;
        }

        var queryResult = await this._currentTrafficQuery
            .GetTotalTrafficBytesAsync(cancellationToken)
            .ConfigureAwait(false);
        
        if (queryResult != null)
        {
            return queryResult;
        }

        // The interface OID may have changed since the last time we asked for it, so look it up again
        this._currentTrafficQuery = await this.TryGetCurrentTrafficQueryAsync(cancellationToken)
            .ConfigureAwait(false);
        if (this._currentTrafficQuery == null)
        {
            return null;
        }
            
        queryResult = await this._currentTrafficQuery
            .GetTotalTrafficBytesAsync(cancellationToken)
            .ConfigureAwait(false);

        return queryResult;
    }

    async Task<SnmpTrafficQuery?> TryGetCurrentTrafficQueryAsync(CancellationToken cancellationToken)
    {
        var idsByName = await this._interfaceQuery
            .GetIdsByNameAsync(cancellationToken)
            .ConfigureAwait(false);

        if (idsByName.TryGetValue(this.InterfaceId, out var interfaceOid))
        {
            return new SnmpTrafficQuery(
                this._remoteEndPoint,
                this._community,
                interfaceOid);
        }

        return null;
    }
}
