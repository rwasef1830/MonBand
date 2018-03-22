using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace MonBand.Core.Snmp
{
    public class SnmpNetworkInterfaceQuery : ISnmpNetworkInterfaceQuery
    {
        static readonly ObjectIdentifier s_NetworkInterfaceCountOid = new ObjectIdentifier("1.3.6.1.2.1.2.1.0");
        const string c_NetworkInterfaceNameOidPrefix = "1.3.6.1.2.1.2.2.1.2.";

        readonly IPEndPoint _remoteEndPoint;
        readonly OctetString _community;

        public SnmpNetworkInterfaceQuery(IPEndPoint remoteEndPoint, string community)
        {
            this._remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            this._community = new OctetString(community ?? "public");
        }

        public async Task<IDictionary<string, int>> GetIdsByNameAsync()
        {
            var result = await Messenger
                .GetAsync(
                    VersionCode.V1,
                    this._remoteEndPoint,
                    this._community,
                    new List<Variable> { new Variable(s_NetworkInterfaceCountOid) })
                .ConfigureAwait(false);
            var number = ((Integer32)result[0].Data).ToInt32();

            var nameOids = new List<Variable>();
            for (int i = 1; i <= number; i++)
            {
                nameOids.Add(new Variable(new ObjectIdentifier(c_NetworkInterfaceNameOidPrefix + i)));
            }

            result = await Messenger
                .GetAsync(VersionCode.V2, this._remoteEndPoint, this._community, nameOids)
                .ConfigureAwait(false);

            return result.ToDictionary(
                x => x.Data.ToString(),
                x =>
                {
                    var oidString = x.Id.ToString();
                    return int.Parse(oidString.Substring(oidString.LastIndexOf('.') + 1));
                });
        }
    }
}
