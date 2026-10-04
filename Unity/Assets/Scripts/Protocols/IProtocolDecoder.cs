using System.Diagnostics;

namespace PhysicalDigital.Protocols
{
    public enum ProtocolKind
    {
        Csv = 0,
        Json = 1,
        Binary = 2,
    }

    public enum ParseResult
    {
        Ok,
        FormatError,
        IntegrityError,
    }

    public sealed class DecoderStats
    {
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
            ParsedFrames > 0 ? ParseTicks * 1e6 / Stopwatch.Frequency / ParsedFrames : 0.0;

        public DecoderStats Clone()
        {
            return (DecoderStats)MemberwiseClone();
        }
    }

    public interface IProtocolDecoder
    {
        string Name { get; }
        DecoderStats Stats { get; }

        bool Feed(byte value, out ControllerState state);

        byte[] GetLastFrame();

        void Reset();
    }
}
