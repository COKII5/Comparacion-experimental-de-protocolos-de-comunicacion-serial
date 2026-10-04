using Newtonsoft.Json;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.Communication
{
    public static class DecoderFactory
    {
        public static IProtocolDecoder Create(ProtocolKind protocol, JsonBackend backend)
        {
            switch (protocol)
            {
                case ProtocolKind.Csv:
                    return new CsvDecoder();
                case ProtocolKind.Json:
                    return backend == JsonBackend.Newtonsoft
                        ? new JsonDecoder(Label(protocol, backend), DeserializeWithNewtonsoft)
                        : new JsonDecoder(Label(protocol, backend), DeserializeWithJsonUtility);
                default:
                    return new BinaryDecoder();
            }
        }

        public static string Label(ProtocolKind protocol, JsonBackend backend)
        {
            switch (protocol)
            {
                case ProtocolKind.Csv:
                    return "CSV";
                case ProtocolKind.Json:
                    return backend == JsonBackend.Newtonsoft ? "JSON (Newtonsoft)" : "JSON (JsonUtility)";
                default:
                    return "Binary";
            }
        }

        private static JsonPacket DeserializeWithJsonUtility(string json)
        {
            return JsonUtility.FromJson<JsonPacket>(json);
        }

        private static JsonPacket DeserializeWithNewtonsoft(string json)
        {
            return JsonConvert.DeserializeObject<JsonPacket>(json);
        }
    }
}
