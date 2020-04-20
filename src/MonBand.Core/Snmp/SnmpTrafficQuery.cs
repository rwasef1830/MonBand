using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using MonBand.Core.Util.Net;
using MonBand.Core.Util.Threading;

namespace MonBand.Core.Snmp
{
    public class SnmpTrafficQuery : ISnmpTrafficQuery
    {
        const string c_NetworkInterfaceReceivedOctetsPrefix = "1.3.6.1.2.1.2.2.1.10.";
        const string c_NetworkInterfaceSentOctetsPrefix = "1.3.6.1.2.1.2.2.1.16.";

        readonly EndPoint _remoteEndPoint;
        readonly OctetString _community;
        readonly string _receivedOctetsOid;
        readonly string _sentOctetsOid;
        readonly Variable[] _requestVariables;

        public string InterfaceId { get; }

        public SnmpTrafficQuery(EndPoint remoteEndPoint, string community, int interfaceNumber)
        {
            this._community = new OctetString(community ?? string.Empty);
            this._remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            this._receivedOctetsOid = c_NetworkInterfaceReceivedOctetsPrefix + interfaceNumber;
            this._sentOctetsOid = c_NetworkInterfaceSentOctetsPrefix + interfaceNumber;
            this._requestVariables = new[]
            {
                new Variable(new ObjectIdentifier(this._receivedOctetsOid)),
                new Variable(new ObjectIdentifier(this._sentOctetsOid))
            };

            this.InterfaceId = interfaceNumber.ToString();
        }

        public async Task<NetworkTraffic?> GetTotalTrafficBytesAsync(CancellationToken cancellationToken)
        {
            var remoteIpEndPoint = await this._remoteEndPoint.ResolveAsync()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false);

            // The native async version doesn't have a timeout mechanism and can hang indefinitely.
            var result = await Task.Run(
                    () => Messenger.Get(
                        VersionCode.V2,
                        remoteIpEndPoint,
                        this._community,
                        this._requestVariables,
                        5000),
                    cancellationToken)
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false);

            long? totalReceivedBytes = null;
            long? totalSentBytes = null;

            foreach (var variable in result)
            {
                if (variable.Id.ToString() == this._receivedOctetsOid)
                {
                    totalReceivedBytes = ((Counter32)variable.Data).ToUInt32();
                    continue;
                }

                if (variable.Id.ToString() == this._sentOctetsOid)
                {
                    totalSentBytes = ((Counter32)variable.Data).ToUInt32();
                }
            }

            if (totalReceivedBytes == null || totalSentBytes == null)
            {
                return null;
            }

            return new NetworkTraffic(totalReceivedBytes.Value, totalSentBytes.Value);
        }
    }
}
