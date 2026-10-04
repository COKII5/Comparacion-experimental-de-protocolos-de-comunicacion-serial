namespace PhysicalDigital.Protocols
{
    public sealed class CsvDecoder : LineDecoder
    {
        private const int FieldCount = 7;
        private const int SeqField = 0;
        private const int FirstButtonField = 1;
        private const int PotField = 5;
        private const int EchoField = 6;
        private const int ChecksumHexDigits = 2;
        private const int BitsPerNibble = 4;
        private const int HexLetterBase = 10;
        private const char ChecksumSeparator = '*';
        private const char FieldSeparator = ',';

        public override string Name => "CSV";

        protected override ParseResult ParseLine(string text, out ControllerState state)
        {
            state = default;
            ParseResult checksumResult = VerifyChecksum(text, out string body);
            if (checksumResult != ParseResult.Ok)
            {
                return checksumResult;
            }
            return ParseFields(body.Split(FieldSeparator), out state);
        }

        private static ParseResult VerifyChecksum(string text, out string body)
        {
            body = "";
            int separator = text.LastIndexOf(ChecksumSeparator);
            if (separator < 0 || separator + 1 + ChecksumHexDigits != text.Length)
            {
                return ParseResult.FormatError;
            }
            if (!TryParseHexByte(text, separator + 1, out byte expected))
            {
                return ParseResult.FormatError;
            }
            if (Checksums.Xor8(text, 0, separator) != expected)
            {
                return ParseResult.IntegrityError;
            }
            body = text.Substring(0, separator);
            return ParseResult.Ok;
        }

        private static ParseResult ParseFields(string[] fields, out ControllerState state)
        {
            state = default;
            if (fields.Length != FieldCount)
            {
                return ParseResult.FormatError;
            }
            if (!TryParseUInt(fields[SeqField], 0, ushort.MaxValue, out int seq)
                || !TryParseButtons(fields, out int buttons)
                || !TryParseUInt(fields[PotField], 0, ControllerState.PotMax, out int pot)
                || !TryParseUInt(fields[EchoField], 0, ushort.MaxValue, out int echo))
            {
                return ParseResult.FormatError;
            }

            state.Seq = (ushort)seq;
            state.Buttons = (byte)buttons;
            state.Pot = (ushort)pot;
            state.Echo = (ushort)echo;
            return ParseResult.Ok;
        }

        private static bool TryParseButtons(string[] fields, out int buttons)
        {
            buttons = 0;
            for (int i = 0; i < ControllerState.ButtonCount; i++)
            {
                if (!TryParseUInt(fields[FirstButtonField + i], 0, 1, out int bit))
                {
                    return false;
                }
                buttons |= bit << i;
            }
            return true;
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
            value = (byte)((high << BitsPerNibble) | low);
            return true;
        }

        private static int HexValue(char hexDigit)
        {
            if (hexDigit >= '0' && hexDigit <= '9')
            {
                return hexDigit - '0';
            }
            if (hexDigit >= 'A' && hexDigit <= 'F')
            {
                return hexDigit - 'A' + HexLetterBase;
            }
            if (hexDigit >= 'a' && hexDigit <= 'f')
            {
                return hexDigit - 'a' + HexLetterBase;
            }
            return -1;
        }
    }
}
