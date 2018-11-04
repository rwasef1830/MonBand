using System.Collections.Generic;
using System.Threading.Tasks;

namespace MonBand.Core.Snmp
{
    public interface ISnmpInterfaceQuery
    {
        Task<IDictionary<string, int>> GetIdsByNameAsync();
    }
}
