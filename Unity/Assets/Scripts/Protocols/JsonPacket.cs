using System;
using Newtonsoft.Json;
using UnityEngine;

namespace PhysicalDigital.Protocols
{
    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class JsonPacket
    {
        [SerializeField, JsonProperty] private int seq = -1;
        [SerializeField, JsonProperty] private int[] b;
        [SerializeField, JsonProperty] private int pot = -1;
        [SerializeField, JsonProperty] private int echo = -1;
        [SerializeField, JsonProperty] private int ck = -1;

        public int SequenceNumber => seq;
        public int[] ButtonBits => b;
        public int PotValue => pot;
        public int EchoId => echo;
        public int Checksum => ck;
    }
}
