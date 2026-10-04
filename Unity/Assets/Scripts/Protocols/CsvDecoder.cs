namespace PhysicalDigital.Protocols
{
    public sealed class CsvDecoder : LineDecoder
    {
        private const int FieldCount = 7;
        private const int ChecksumHexDigits = 2;

        public override string Name => "CSV";

        protected override ParseResult ParseLine(string text, out ControllerState state)
        {
            state = default;

            int star = text.LastIndexOf('*');
            if (star < 0 || star + 1 + ChecksumHexDigits != text.Length)
            {
                return ParseResult.FormatError;
            }
            if (!TryParseHexByte(text, star + 1, out byte expected))
            {
                return ParseResult.FormatError;
            }
            if (Checksums.Xor8(text, 0, star) != expected)
            {
                return ParseResult.IntegrityError;
            }

            string[] fields = text.Substring(0, star).Split(',');
            if (fields.Length != FieldCount)
            {
                return ParseResult.FormatError;
            }

            if (!TryParseUInt(fields[0], 0, ushort.MaxValue, out int seq))
            {
                return ParseResult.FormatError;
            }
            int buttons = 0;
            for (int i = 0; i < ControllerState.ButtonCount; i++)
            {
                if (!TryParseUInt(fields[1 + i], 0, 1, out int bit))
                {
                    return ParseResult.FormatError;
                }
                buttons |= bit << i;
            }
            if (!TryParseUInt(fields[5], 0, ControllerState.PotMax, out int pot))
            {
                return ParseResult.FormatError;
            }
            if (!TryParseUInt(fields[6], 0, ushort.MaxValue, out int echo))
            {
                return ParseResult.FormatError;
            }

            state.Seq = (ushort)seq;
            state.Buttons = (byte)buttons;
            state.Pot = (ushort)pot;
            state.Echo = (ushort)echo;
            return ParseResult.Ok;
        }

        private static bool TryParseHexByte(string text, int index, out byte value)
        {
            value = 0;
            int high = HexValue(text[index]);
            int low = HexValue(text[index + 1]);
            if (high < 0 || low < 0)
            {
                return false;
            }
            value = (byte)((high << 4) | low);
            return true;
        }

        private static int HexValue(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }
            if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            return -1;
        }
    }
}
