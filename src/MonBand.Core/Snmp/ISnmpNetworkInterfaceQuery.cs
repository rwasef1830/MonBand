using System.Collections.Generic;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public interface ISnmpNetworkInterfaceQuery
    {
        Task<IDictionary<string, int>> GetIdsByNameAsync();
    }
}
