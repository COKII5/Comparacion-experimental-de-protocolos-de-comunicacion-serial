namespace PhysicalDigital.Protocols
{
    public static class Checksums
    {
        public static byte Xor8(string text, int start, int count)
        {
            byte checksum = 0;
            for (int i = start; i < start + count; i++)
            {
                checksum ^= (byte)text[i];
            }
            return checksum;
        }

        public static byte Sum8(byte[] data, int offset, int count)
        {
            int sum = 0;
            for (int i = offset; i < offset + count; i++)
            {
                sum += data[i];
            }
            return (byte)(sum & 0xFF);
        }

        public static ushort Crc16Ccitt(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = offset; i < offset + count; i++)
            {
                crc ^= (ushort)(data[i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
                }
            }
            return crc;
        }
    }
}
