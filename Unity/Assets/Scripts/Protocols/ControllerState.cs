namespace PhysicalDigital.Protocols
{
    public struct ControllerState
    {
        public const int ButtonCount = 4;
        public const int PotMax = 1023;
        public const int CanonicalPayloadLength = 7;

        public ushort Seq;
        public byte Buttons;
        public ushort Pot;
        public ushort Echo;

        public long RxTimestamp;
        public int FrameBytes;

        public static readonly ControllerState TestValue = new ControllerState
        {
            Seq = 0x7D7E,
            Buttons = 0x0A,
            Pot = 0x027E,
            Echo = 0x007D,
        };

        public static ControllerState FromCanonicalPayload(byte[] source, int offset)
        {
            int index = offset;
            ControllerState state = default;
            state.Seq = LittleEndian.ReadUInt16(source, index);
            index += LittleEndian.UInt16Size;
            state.Buttons = source[index];
            index++;
            state.Pot = LittleEndian.ReadUInt16(source, index);
            index += LittleEndian.UInt16Size;
            state.Echo = LittleEndian.ReadUInt16(source, index);
            return state;
        }

        public bool IsPressed(int index)
        {
            return (Buttons & (1 << index)) != 0;
        }

        public bool SameValues(in ControllerState other)
        {
            return Seq == other.Seq && Buttons == other.Buttons && Pot == other.Pot && Echo == other.Echo;
        }

        public void WriteCanonicalPayload(byte[] destination, int offset)
        {
            int index = LittleEndian.WriteUInt16(destination, offset, Seq);
            destination[index] = Buttons;
            index++;
            index = LittleEndian.WriteUInt16(destination, index, Pot);
            LittleEndian.WriteUInt16(destination, index, Echo);
        }

        public override string ToString()
        {
            return $"seq={Seq} b=[{Bit(0)},{Bit(1)},{Bit(2)},{Bit(3)}] pot={Pot} echo={Echo}";
        }

        private int Bit(int index)
        {
            return IsPressed(index) ? 1 : 0;
        }
    }
}
