using PhysicalDigital.Protocols;

namespace PhysicalDigital.Communication
{
    public struct LinkSnapshot
    {
        public DecoderStats Decoder;
        public long LostPackets;
        public float FramesPerSecond;
        public float BytesPerSecond;
        public byte[] LastFrame;
    }
}
