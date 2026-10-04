using System;

namespace PhysicalDigital.Protocols
{
    public sealed class JsonDecoder : LineDecoder
    {
        private const int MaxChecksum = 255;

        private readonly string name;
        private readonly Func<string, JsonPacket> deserialize;
        private readonly byte[] canonical = new byte[ControllerState.CanonicalPayloadLength];

        public JsonDecoder(string name, Func<string, JsonPacket> deserialize)
        {
            this.name = name;
            this.deserialize = deserialize;
        }

        public override string Name => name;

        protected override ParseResult ParseLine(string text, out ControllerState state)
        {
            state = default;
            if (text[0] != '{')
            {
                return ParseResult.FormatError;
            }

            JsonPacket packet = deserialize(text);
            if (packet == null || packet.ButtonBits == null || packet.ButtonBits.Length != ControllerState.ButtonCount)
            {
                return ParseResult.FormatError;
            }
            if (!InRange(packet.SequenceNumber, ushort.MaxValue) || !InRange(packet.PotValue, ControllerState.PotMax)
                || !InRange(packet.EchoId, ushort.MaxValue) || !InRange(packet.Checksum, MaxChecksum))
            {
                return ParseResult.FormatError;
            }

            int buttons = 0;
            for (int i = 0; i < ControllerState.ButtonCount; i++)
            {
                int bit = packet.ButtonBits[i];
                if (bit != 0 && bit != 1)
                {
                    return ParseResult.FormatError;
                }
                buttons |= bit << i;
            }

            state.Seq = (ushort)packet.SequenceNumber;
            state.Buttons = (byte)buttons;
            state.Pot = (ushort)packet.PotValue;
            state.Echo = (ushort)packet.EchoId;

            state.WriteCanonicalPayload(canonical, 0);
            if (Checksums.Sum8(canonical, 0, canonical.Length) != packet.Checksum)
            {
                state = default;
                return ParseResult.IntegrityError;
            }
            return ParseResult.Ok;
        }

        private static bool InRange(int value, int max)
        {
            return value >= 0 && value <= max;
        }
    }
}
