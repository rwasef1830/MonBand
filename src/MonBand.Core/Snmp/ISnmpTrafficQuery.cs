using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public interface ISnmpTrafficQuery
    {
        Task<NetworkTraffic> GetTotalTrafficBytesAsync();
    }
}
