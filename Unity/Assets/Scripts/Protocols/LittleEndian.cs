namespace PhysicalDigital.Protocols
{
    public static class LittleEndian
    {
        public const int UInt16Size = 2;

        private const int BitsPerByte = 8;
        private const int ByteMask = 0xFF;

        public static int WriteUInt16(byte[] destination, int index, ushort value)
        {
            destination[index] = (byte)(value & ByteMask);
            destination[index + 1] = (byte)(value >> BitsPerByte);
            return index + UInt16Size;
        }

        public static ushort ReadUInt16(byte[] source, int index)
        {
            return (ushort)(source[index] | (source[index + 1] << BitsPerByte));
        }
    }
}
