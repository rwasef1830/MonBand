using System.Threading;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public interface ISnmpTrafficQuery
    {
        string InterfaceId { get; }
        Task<NetworkTraffic?> GetTotalTrafficBytesAsync(CancellationToken cancellationToken);
    }
}
