using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public interface ISnmpNetworkTrafficQuery
    {
        Task<NetworkTraffic> GetTotalTrafficBytesAsync();
    }
}
