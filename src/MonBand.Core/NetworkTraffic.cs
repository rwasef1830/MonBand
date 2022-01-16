namespace MonBand.Core;

public readonly struct NetworkTraffic
{
    public ulong InBytes { get; }
    public ulong OutBytes { get; }
    public bool Is64BitCounter { get; }

    public NetworkTraffic(ulong inBytes, ulong outBytes, bool is64BitCounter = true)
    {
        this.InBytes = inBytes;
        this.OutBytes = outBytes;
        this.Is64BitCounter = is64BitCounter;
    }

    public override string ToString()
    {
        return $"In: {this.InBytes}; Out: {this.OutBytes} (64-bit: {this.Is64BitCounter})";
    }

    public (double InMegabits, double OutMegabits) AsMegabits()
    {
        var inMegabits = (double)this.InBytes * 8 / (1000 * 1000);
        var outMegabits = (double)this.OutBytes * 8 / (1000 * 1000);
        return (inMegabits, outMegabits);
    }
}
