using System;

namespace PhysicalDigital.Protocols
{
    [Serializable]
    public sealed class JsonPacket
    {
        public int seq = -1;
        public int[] b;
        public int pot = -1;
        public int echo = -1;
        public int ck = -1;
    }

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
            if (packet == null || packet.b == null || packet.b.Length != ControllerState.ButtonCount)
            {
                return ParseResult.FormatError;
            }
            if (!InRange(packet.seq, ushort.MaxValue) || !InRange(packet.pot, ControllerState.PotMax)
                || !InRange(packet.echo, ushort.MaxValue) || !InRange(packet.ck, MaxChecksum))
            {
                return ParseResult.FormatError;
            }

            int buttons = 0;
            for (int i = 0; i < ControllerState.ButtonCount; i++)
            {
                int bit = packet.b[i];
                if (bit != 0 && bit != 1)
                {
                    return ParseResult.FormatError;
                }
                buttons |= bit << i;
            }

            state.Seq = (ushort)packet.seq;
            state.Buttons = (byte)buttons;
            state.Pot = (ushort)packet.pot;
            state.Echo = (ushort)packet.echo;

            state.WriteCanonicalPayload(canonical, 0);
            if (Checksums.Sum8(canonical, 0, canonical.Length) != packet.ck)
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
