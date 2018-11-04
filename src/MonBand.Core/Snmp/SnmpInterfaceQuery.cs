using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace MonBand.Core.Snmp
{
    public class SnmpInterfaceQuery : ISnmpInterfaceQuery
    {
        static readonly ObjectIdentifier s_NetworkInterfaceNameOid = new ObjectIdentifier("1.3.6.1.2.1.2.2.1.2");

        readonly IPEndPoint _remoteEndPoint;
        readonly OctetString _community;

        public SnmpInterfaceQuery(IPEndPoint remoteEndPoint, string community)
        {
            this._remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            this._community = new OctetString(community ?? string.Empty);
        }

        public async Task<IDictionary<string, int>> GetIdsByNameAsync()
        {
            var variables = new List<Variable>();
            // The native async version doesn't have a timeout mechanism and can hang indefinitely.
            await Task.Run(
                    () => Messenger
                        .Walk(
                            VersionCode.V1,
                            this._remoteEndPoint,
                            this._community,
                            s_NetworkInterfaceNameOid,
                            variables,
                            5000,
                            WalkMode.WithinSubtree))
                .ConfigureAwait(false);

            return variables.ToDictionary(
                x => x.Data.ToString(),
                x =>
                {
                    var oidString = x.Id.ToString();
                    return int.Parse(oidString.Substring(oidString.LastIndexOf('.') + 1));
                });
        }
    }
}
