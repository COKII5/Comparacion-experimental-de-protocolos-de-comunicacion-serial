using System.Diagnostics;

namespace PhysicalDigital.Protocols
{
    public sealed class DecoderStats
    {
        private const double MicrosecondsPerSecond = 1e6;

        public long BytesTotal;
        public long BytesInFrames;
        public long FramesOk;
        public long FormatErrors;
        public long IntegrityErrors;
        public long DiscardedBytes;
        public long ParseTicks;
        public long ParsedFrames;

        public double AvgFrameBytes => FramesOk > 0 ? (double)BytesInFrames / FramesOk : 0.0;

        public double AvgParseMicros =>
            ParsedFrames > 0 ? ParseTicks * MicrosecondsPerSecond / Stopwatch.Frequency / ParsedFrames : 0.0;

        public DecoderStats Clone()
        {
            return (DecoderStats)MemberwiseClone();
        }
    }
}
