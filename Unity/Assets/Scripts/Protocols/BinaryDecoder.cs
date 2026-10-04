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

        private const int LengthFieldSize = 1;
        private const int TypeOffset = 1;
        private const int PayloadOffset = 2;
        private const int MaxLength = 32;
        private const int CrcLength = 2;
        private const int WorstCaseEscapeFactor = 2;
        private const int MaxBodyLength = LengthFieldSize + MaxLength + CrcLength;
        private const int MaxRawLength = 1 + WorstCaseEscapeFactor * MaxBodyLength;
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
        private int bodyIndex;
        private int bodyBytesNeeded;
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
                BeginFrame(value);
                return false;
            }
            if (stage == Stage.WaitStart)
            {
                Stats.DiscardedBytes++;
                return false;
            }

            AppendRaw(value);
            if (!TryUnescape(ref value))
            {
                return false;
            }
            if (stage == Stage.Length)
            {
                AcceptLength(value);
                return false;
            }
            return AcceptBodyByte(value, out state);
        }

        public byte[] GetLastFrame()
        {
            byte[] copy = new byte[lastFrameLength];
            Array.Copy(lastFrame, copy, lastFrameLength);
            return copy;
        }

        private void BeginFrame(byte startByte)
        {
            if (stage != Stage.WaitStart)
            {
                Stats.FormatErrors++;
            }
            stage = Stage.Length;
            escaping = false;
            rawLength = 0;
            AppendRaw(startByte);
        }

        private bool TryUnescape(ref byte value)
        {
            if (value == Escape)
            {
                if (escaping)
                {
                    Abort();
                    return false;
                }
                escaping = true;
                return false;
            }
            if (escaping)
            {
                value ^= EscapeXor;
                escaping = false;
            }
            return true;
        }

        private void AcceptLength(byte length)
        {
            if (length < 1 || length > MaxLength)
            {
                Abort();
                return;
            }
            body[0] = length;
            bodyIndex = LengthFieldSize;
            bodyBytesNeeded = LengthFieldSize + length + CrcLength;
            stage = Stage.Body;
        }

        private bool AcceptBodyByte(byte value, out ControllerState state)
        {
            state = default;
            body[bodyIndex] = value;
            bodyIndex++;
            if (bodyIndex < bodyBytesNeeded)
            {
                return false;
            }
            stage = Stage.WaitStart;
            return Complete(out state);
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
            long start = Stopwatch.GetTimestamp();
            ParseResult result = Parse(out state);
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

        private ParseResult Parse(out ControllerState state)
        {
            state = default;
            int length = body[0];
            int crcOffset = LengthFieldSize + length;
            ushort received = LittleEndian.ReadUInt16(body, crcOffset);
            if (Checksums.Crc16Ccitt(body, 0, crcOffset) != received)
            {
                return ParseResult.IntegrityError;
            }
            if (length != StateLength || body[TypeOffset] != TypeState)
            {
                return ParseResult.FormatError;
            }

            state = ControllerState.FromCanonicalPayload(body, PayloadOffset);
            if (state.Buttons > MaxButtonsMask || state.Pot > ControllerState.PotMax)
            {
                return ParseResult.FormatError;
            }
            return ParseResult.Ok;
        }

        private void Abort()
        {
            Stats.FormatErrors++;
            stage = Stage.WaitStart;
            escaping = false;
        }
    }
}
