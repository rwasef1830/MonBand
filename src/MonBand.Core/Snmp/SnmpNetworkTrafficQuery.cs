using System;
using System.Net;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;

namespace MonBand.Core.Snmp
{
    public class SnmpNetworkTrafficQuery : ISnmpNetworkTrafficQuery
    {
        const string c_NetworkInterfaceReceivedOctetsPrefix = "1.3.6.1.2.1.2.2.1.10.";
        const string c_NetworkInterfaceSentOctetsPrefix = "1.3.6.1.2.1.2.2.1.16.";

        readonly IPEndPoint _remoteEndPoint;
        readonly OctetString _community;
        readonly string _receivedOctetsOid;
        readonly string _sentOctetsOid;

        public SnmpNetworkTrafficQuery(IPEndPoint remoteEndPoint, string community, int interfaceNumber)
        {
            this._community = new OctetString(community ?? "public");
            this._remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            this._receivedOctetsOid = c_NetworkInterfaceReceivedOctetsPrefix + interfaceNumber;
            this._sentOctetsOid = c_NetworkInterfaceSentOctetsPrefix + interfaceNumber;
        }

        public async Task<NetworkTraffic> GetTotalTrafficBytesAsync()
        {
            var result = await Messenger
                .GetAsync(
                    VersionCode.V2,
                    this._remoteEndPoint,
                    this._community,
                    new[]
                    {
                        new Variable(new ObjectIdentifier(this._receivedOctetsOid)),
                        new Variable(new ObjectIdentifier(this._sentOctetsOid))
                    })
                .ConfigureAwait(false);

            long totalReceivedBytes = 0;
            long totalSentBytes = 0;

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

            return new NetworkTraffic(totalReceivedBytes, totalSentBytes);
        }
    }
}
