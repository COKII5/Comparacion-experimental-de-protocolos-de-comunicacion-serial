using System;
using System.Diagnostics;

namespace PhysicalDigital.Protocols
{
    public sealed class BinaryDecoder : IProtocolDecoder
    {
        public const byte StartOfFrame = 0x7E;
        public const byte Escape = 0x7D;
        public const byte EscapeXor = 0x20;
        public const byte TypeState = 0x01;
        public const byte StateLength = 8;

        private const int MaxLength = 32;
        private const int CrcLength = 2;
        private const int MaxBodyLength = 1 + MaxLength + CrcLength;
        private const int MaxRawLength = 1 + 2 * MaxBodyLength;
        private const byte MaxButtonsMask = 0x0F;

        private enum Stage
        {
            WaitStart,
            Length,
            Body,
        }

        private readonly byte[] body = new byte[MaxBodyLength];
        private readonly byte[] raw = new byte[MaxRawLength];
        private readonly byte[] lastFrame = new byte[MaxRawLength];
        private Stage stage = Stage.WaitStart;
        private bool escaping;
        private int index;
        private int needed;
        private int rawLength;
        private int lastFrameLength;

        public string Name => "Binary";
        public DecoderStats Stats { get; } = new DecoderStats();

        public bool Feed(byte value, out ControllerState state)
        {
            state = default;
            Stats.BytesTotal++;

            if (value == StartOfFrame)
            {
                if (stage != Stage.WaitStart)
                {
                    Stats.FormatErrors++;
                }
                stage = Stage.Length;
                escaping = false;
                rawLength = 0;
                AppendRaw(value);
                return false;
            }

            if (stage == Stage.WaitStart)
            {
                Stats.DiscardedBytes++;
                return false;
            }

            AppendRaw(value);

            if (value == Escape)
            {
                if (escaping)
                {
                    return Abort();
                }
                escaping = true;
                return false;
            }
            if (escaping)
            {
                value ^= EscapeXor;
                escaping = false;
            }

            if (stage == Stage.Length)
            {
                if (value < 1 || value > MaxLength)
                {
                    return Abort();
                }
                body[0] = value;
                index = 1;
                needed = 1 + value + CrcLength;
                stage = Stage.Body;
                return false;
            }

            body[index] = value;
            index++;
            if (index < needed)
            {
                return false;
            }

            stage = Stage.WaitStart;
            return Complete(out state);
        }

        public byte[] GetLastFrame()
        {
            byte[] copy = new byte[lastFrameLength];
            Array.Copy(lastFrame, copy, lastFrameLength);
            return copy;
        }

        public void Reset()
        {
            stage = Stage.WaitStart;
            escaping = false;
            rawLength = 0;
            lastFrameLength = 0;
        }

        private void AppendRaw(byte value)
        {
            if (rawLength < raw.Length)
            {
                raw[rawLength] = value;
                rawLength++;
            }
        }

        private bool Complete(out ControllerState state)
        {
            state = default;
            long start = Stopwatch.GetTimestamp();
            ParseResult result = Parse(ref state);
            Stats.ParseTicks += Stopwatch.GetTimestamp() - start;
            Stats.ParsedFrames++;

            if (result == ParseResult.Ok)
            {
                Stats.FramesOk++;
                Stats.BytesInFrames += rawLength;
                state.FrameBytes = rawLength;
                Array.Copy(raw, lastFrame, rawLength);
                lastFrameLength = rawLength;
                return true;
            }
            if (result == ParseResult.IntegrityError)
            {
                Stats.IntegrityErrors++;
            }
            else
            {
                Stats.FormatErrors++;
            }
            state = default;
            return false;
        }

        private ParseResult Parse(ref ControllerState state)
        {
            int length = body[0];
            ushort received = (ushort)(body[1 + length] | (body[2 + length] << 8));
            if (Checksums.Crc16Ccitt(body, 0, 1 + length) != received)
            {
                return ParseResult.IntegrityError;
            }
            if (length != StateLength || body[1] != TypeState)
            {
                return ParseResult.FormatError;
            }

            state.Seq = (ushort)(body[2] | (body[3] << 8));
            state.Buttons = body[4];
            state.Pot = (ushort)(body[5] | (body[6] << 8));
            state.Echo = (ushort)(body[7] | (body[8] << 8));
            if (state.Buttons > MaxButtonsMask || state.Pot > ControllerState.PotMax)
            {
                return ParseResult.FormatError;
            }
            return ParseResult.Ok;
        }

        private bool Abort()
        {
            Stats.FormatErrors++;
            stage = Stage.WaitStart;
            escaping = false;
            return false;
        }
    }
}
