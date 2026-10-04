using System;
using System.Diagnostics;
using System.Text;

namespace PhysicalDigital.Protocols
{
    public abstract class LineDecoder : IProtocolDecoder
    {
        private const int MaxLine = 128;
        private const int MaxDigits = 5;
        private const int DecimalBase = 10;
        private const byte LineFeed = (byte)'\n';
        private const byte CarriageReturn = (byte)'\r';

        private readonly byte[] line = new byte[MaxLine];
        private readonly byte[] lastFrame = new byte[MaxLine + 1];
        private int length;
        private bool overflow;
        private int lastFrameLength;

        public abstract string Name { get; }
        public DecoderStats Stats { get; } = new DecoderStats();

        protected abstract ParseResult ParseLine(string text, out ControllerState state);

        public bool Feed(byte value, out ControllerState state)
        {
            state = default;
            Stats.BytesTotal++;
            if (value != LineFeed)
            {
                Accumulate(value);
                return false;
            }
            return CompleteLine(out state);
        }

        public byte[] GetLastFrame()
        {
            byte[] copy = new byte[lastFrameLength];
            Array.Copy(lastFrame, copy, lastFrameLength);
            return copy;
        }

        protected static bool TryParseUInt(string text, int min, int max, out int value)
        {
            value = 0;
            if (text.Length == 0 || text.Length > MaxDigits)
            {
                return false;
            }
            for (int i = 0; i < text.Length; i++)
            {
                char digit = text[i];
                if (digit < '0' || digit > '9')
                {
                    return false;
                }
                value = value * DecimalBase + (digit - '0');
            }
            return value >= min && value <= max;
        }

        private void Accumulate(byte value)
        {
            if (length < MaxLine)
            {
                line[length] = value;
                length++;
            }
            else
            {
                overflow = true;
            }
        }

        private bool CompleteLine(out ControllerState state)
        {
            state = default;
            int frameBytes = length + 1;
            int contentLength = length;
            bool wasOverflow = overflow;
            length = 0;
            overflow = false;

            if (wasOverflow)
            {
                Stats.FormatErrors++;
                return false;
            }
            if (contentLength > 0 && line[contentLength - 1] == CarriageReturn)
            {
                contentLength--;
            }
            if (contentLength == 0)
            {
                Stats.DiscardedBytes += frameBytes;
                return false;
            }

            ParseResult result = TimedParse(contentLength, out state);
            return Record(result, contentLength, frameBytes, ref state);
        }

        private ParseResult TimedParse(int contentLength, out ControllerState state)
        {
            long start = Stopwatch.GetTimestamp();
            ParseResult result;
            try
            {
                string text = Encoding.ASCII.GetString(line, 0, contentLength);
                result = ParseLine(text, out state);
            }
            catch (Exception)
            {
                state = default;
                result = ParseResult.FormatError;
            }
            Stats.ParseTicks += Stopwatch.GetTimestamp() - start;
            Stats.ParsedFrames++;
            return result;
        }

        private bool Record(ParseResult result, int contentLength, int frameBytes, ref ControllerState state)
        {
            switch (result)
            {
                case ParseResult.Ok:
                    Stats.FramesOk++;
                    Stats.BytesInFrames += frameBytes;
                    state.FrameBytes = frameBytes;
                    Array.Copy(line, lastFrame, contentLength);
                    lastFrame[contentLength] = LineFeed;
                    lastFrameLength = contentLength + 1;
                    return true;
                case ParseResult.IntegrityError:
                    Stats.IntegrityErrors++;
                    break;
                default:
                    Stats.FormatErrors++;
                    break;
            }
            state = default;
            return false;
        }
    }
}
