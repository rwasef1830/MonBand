namespace MonBand.Core
{
    public readonly struct NetworkTraffic
    {
        public long InBytes { get; }
        public long OutBytes { get; }

        public NetworkTraffic(long inBytes, long outBytes)
        {
            this.InBytes = inBytes;
            this.OutBytes = outBytes;
        }

        public override string ToString()
        {
            return $"In: {this.InBytes}; Out: {this.OutBytes}";
        }
    }
}
