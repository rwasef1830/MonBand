using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using MonBand.Core.Util.Net;
using MonBand.Core.Util.Threading;

namespace MonBand.Core.Snmp;

public class SnmpTrafficQuery : ISnmpTrafficQuery
{
    const string c_NetworkInterfaceReceivedOctetsPrefix = "1.3.6.1.2.1.2.2.1.10.";
    const string c_NetworkInterfaceSentOctetsPrefix = "1.3.6.1.2.1.2.2.1.16.";
    const string c_NetworkInterfaceReceivedOctets64Prefix = "1.3.6.1.2.1.31.1.1.1.6.";
    const string c_NetworkInterfaceSentOctets64Prefix = "1.3.6.1.2.1.31.1.1.1.10.";

    readonly EndPoint _remoteEndPoint;
    readonly OctetString _community;
    readonly string _receivedOctetsOid;
    readonly string _sentOctetsOid;
    readonly string _receivedOctets64Oid;
    readonly string _sentOctets64Oid;
    readonly Variable[] _requestVariables;

    public string InterfaceId { get; }

    public SnmpTrafficQuery(EndPoint remoteEndPoint, string community, int interfaceNumber)
    {
        this._community = new OctetString(community);
        this._remoteEndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
        this._receivedOctetsOid = c_NetworkInterfaceReceivedOctetsPrefix + interfaceNumber;
        this._sentOctetsOid = c_NetworkInterfaceSentOctetsPrefix + interfaceNumber;
        this._receivedOctets64Oid = c_NetworkInterfaceReceivedOctets64Prefix + interfaceNumber;
        this._sentOctets64Oid = c_NetworkInterfaceSentOctets64Prefix + interfaceNumber;
        this._requestVariables = new[]
        {
            new Variable(new ObjectIdentifier(this._receivedOctetsOid)),
            new Variable(new ObjectIdentifier(this._sentOctetsOid)),
            new Variable(new ObjectIdentifier(this._receivedOctets64Oid)),
            new Variable(new ObjectIdentifier(this._sentOctets64Oid))
        };

        this.InterfaceId = interfaceNumber.ToString();
    }

    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
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

        ulong? totalReceivedBytes = null;
        ulong? totalSentBytes = null;
        ulong? totalReceivedBytes64 = null;
        ulong? totalSentBytes64 = null;

        foreach (var variable in result)
        {
            if (variable.Data is NoSuchInstance or NoSuchObject)
            {
                continue;
            }

            if (variable.Id.ToString() == this._receivedOctetsOid)
            {
                totalReceivedBytes = ((Counter32)variable.Data).ToUInt32();
                continue;
            }

            if (variable.Id.ToString() == this._sentOctetsOid)
            {
                totalSentBytes = ((Counter32)variable.Data).ToUInt32();
                continue;
            }

            if (variable.Id.ToString() == this._receivedOctets64Oid)
            {
                totalReceivedBytes64 = ((Counter64)variable.Data).ToUInt64();
                continue;
            }

            if (variable.Id.ToString() == this._sentOctets64Oid)
            {
                totalSentBytes64 = ((Counter64)variable.Data).ToUInt64();
            }
        }

        bool is64BitCounter = totalReceivedBytes64.HasValue && totalSentBytes64.HasValue;
        totalReceivedBytes64 ??= totalReceivedBytes;
        totalSentBytes64 ??= totalSentBytes;

        if (totalReceivedBytes64 == null || totalSentBytes64 == null)
        {
            return null;
        }

        return new NetworkTraffic(totalReceivedBytes64.Value, totalSentBytes64.Value, is64BitCounter);
    }
}
