namespace PhysicalDigital.Protocols
{
    public static class Checksums
    {
        private const int ByteMask = 0xFF;
        private const int BitsPerByte = 8;
        private const ushort CrcInitial = 0xFFFF;
        private const ushort CrcPolynomial = 0x1021;
        private const ushort CrcTopBit = 0x8000;

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
            return (byte)(sum & ByteMask);
        }

        public static ushort Crc16Ccitt(byte[] data, int offset, int count)
        {
            ushort crc = CrcInitial;
            for (int i = offset; i < offset + count; i++)
            {
                crc ^= (ushort)(data[i] << BitsPerByte);
                for (int bit = 0; bit < BitsPerByte; bit++)
                {
                    crc = (crc & CrcTopBit) != 0 ? (ushort)((crc << 1) ^ CrcPolynomial) : (ushort)(crc << 1);
                }
            }
            return crc;
        }
    }
}
