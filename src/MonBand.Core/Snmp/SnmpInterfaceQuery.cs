using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using MonBand.Core.Util.Net;
using MonBand.Core.Util.Threading;

namespace MonBand.Core.Snmp
{
    public class SnmpInterfaceQuery : ISnmpInterfaceQuery
    {
        static readonly ObjectIdentifier s_NetworkInterfaceNameOid = new ObjectIdentifier("1.3.6.1.2.1.2.2.1.2");

        readonly EndPoint _remoteEndPoint;
        readonly OctetString _community;

        public SnmpInterfaceQuery(EndPoint remoteEndPoint, string community)
        {
            this._remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            this._community = new OctetString(community ?? string.Empty);
        }

        public async Task<IDictionary<string, int>> GetIdsByNameAsync(CancellationToken cancellationToken)
        {
            var remoteIpEndPoint = await this._remoteEndPoint.ResolveAsync()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false);

            var variables = new List<Variable>();
            // The native async version doesn't have a timeout mechanism and can hang indefinitely.
            await Task.Run(
                    () => Messenger
                        .Walk(
                            VersionCode.V1,
                            remoteIpEndPoint,
                            this._community,
                            s_NetworkInterfaceNameOid,
                            variables,
                            5000,
                            WalkMode.WithinSubtree),
                    cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false);

            return variables
                .Where(x => !(x.Data is NoSuchInstance) && !(x.Data is NoSuchObject))
                .ToDictionary(
                    x => x.Data.ToString(),
                    x =>
                    {
                        var oidString = x.Id.ToString();
                        return int.Parse(oidString.Substring(oidString.LastIndexOf('.') + 1));
                    });
        }
    }
}
