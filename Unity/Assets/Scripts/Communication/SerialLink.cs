#if NET_4_6 || NET_UNITY_4_8
#define SERIAL_PORTS
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;
using PhysicalDigital.Protocols;
using UnityEngine;
#if SERIAL_PORTS
using System.IO.Ports;
#endif

namespace PhysicalDigital.Communication
{
    public enum JsonBackend
    {
        JsonUtility = 0,
        Newtonsoft = 1,
    }

    public struct LinkSnapshot
    {
        public DecoderStats Decoder;
        public long LostPackets;
        public float FramesPerSecond;
        public float BytesPerSecond;
        public byte[] LastFrame;
    }

    public struct TestFrame
    {
        public ControllerState State;
        public byte[] Raw;
        public string Protocol;
    }

    public struct RttSample
    {
        public double Milliseconds;
        public int FrameBytes;
    }

    public struct RobustnessCounters
    {
        public long Accepted;
        public long Undetected;
        public long BitsFlipped;
        public long BytesSeen;
    }

    public sealed class SerialLink : MonoBehaviour
    {
        public const int DefaultPeriodMs = 20;

        private const int ReadBufferSize = 512;
        private const int ReadTimeoutMs = 50;
        private const int WriteTimeoutMs = 200;
        private const int ThreadJoinTimeoutMs = 500;
        private const int MaxInboxSize = 4096;
        private const int MaxPingId = 65535;
        private const int MaxPlausibleSeqGap = 1000;
        private const int BitsPerByte = 8;

        [Header("Port")]
        [Tooltip("Empty = on Connect, the highest-numbered available COM port is used.")]
        public string portName = "";
        public int baudRate = 115200;
        public bool autoConnect = true;
        [Tooltip("The Uno resets when the port opens; commands are delayed by this many seconds.")]
        public float bootDelay = 2f;

        [Header("Protocol (must match the sketch loaded on the Arduino)")]
        public ProtocolKind protocol = ProtocolKind.Csv;
        public JsonBackend jsonBackend = JsonBackend.JsonUtility;

        private readonly object sync = new object();
        private readonly Queue<ControllerState> inbox = new Queue<ControllerState>();
        private readonly Dictionary<int, RttSample> rttResults = new Dictionary<int, RttSample>();
        private readonly System.Random random = new System.Random();
        private IProtocolDecoder decoder;
        private Thread readThread;
        private volatile bool running;
        private volatile string readError;
        private float openedAt;
        private string status = "Disconnected";

        private bool haveSeq;
        private ushort lastSeq;
        private long lostPackets;
        private int pendingPing = -1;
        private long pingSentTicks;
        private int nextPingId = 1;

        private bool awaitingTest;
        private bool hasTestResult;
        private TestFrame testResult;
        private bool robustnessMode;
        private double bitErrorRate;
        private RobustnessCounters robust;

        private float rateTimer;
        private long rateFrames;
        private long rateBytes;
        private float framesPerSecond;
        private float bytesPerSecond;

#if SERIAL_PORTS
        private SerialPort port;
#endif

        public string Status => status;
        public bool IsOpen => running;
        public bool IsReady => running && Time.realtimeSinceStartup - openedAt >= bootDelay;

        public string ProtocolLabel
        {
            get
            {
                switch (protocol)
                {
                    case ProtocolKind.Csv:
                        return "CSV";
                    case ProtocolKind.Json:
                        return jsonBackend == JsonBackend.Newtonsoft ? "JSON (Newtonsoft)" : "JSON (JsonUtility)";
                    default:
                        return "Binary";
                }
            }
        }

        public string ProtocolTag => ProtocolLabel.Replace(" (", "_").Replace(")", "");

        public static double TicksToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private void Awake()
        {
            decoder = CreateDecoder();
        }

        private void Start()
        {
            if (autoConnect && (!string.IsNullOrEmpty(portName) || AvailablePorts().Length == 1))
            {
                Connect();
            }
            else
            {
                status = "Choose the port and press Connect";
            }
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void Update()
        {
            string error = readError;
            if (error != null)
            {
                Disconnect();
                status = "Error: " + error;
            }
            else if (running)
            {
                status = IsReady ? $"Connected to {portName}" : $"Opening {portName} (Arduino reset)...";
            }

            rateTimer += Time.unscaledDeltaTime;
            if (rateTimer >= 1f)
            {
                long frames;
                long bytes;
                lock (sync)
                {
                    frames = decoder.Stats.FramesOk;
                    bytes = decoder.Stats.BytesTotal;
                }
                framesPerSecond = Math.Max(0L, frames - rateFrames) / rateTimer;
                bytesPerSecond = Math.Max(0L, bytes - rateBytes) / rateTimer;
                rateFrames = frames;
                rateBytes = bytes;
                rateTimer = 0f;
            }
        }

        public static string[] AvailablePorts()
        {
#if SERIAL_PORTS
            try
            {
                string[] ports = SerialPort.GetPortNames();
                Array.Sort(ports, ComparePortNames);
                return ports;
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
#else
            return Array.Empty<string>();
#endif
        }

        private static int ComparePortNames(string a, string b)
        {
            int byLength = a.Length.CompareTo(b.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a, b);
        }

        public void Connect()
        {
            Disconnect();
#if SERIAL_PORTS
            if (string.IsNullOrEmpty(portName))
            {
                string[] ports = AvailablePorts();
                if (ports.Length == 0)
                {
                    status = "No COM ports available";
                    return;
                }
                portName = ports[ports.Length - 1];
            }
            try
            {
                port = new SerialPort(portName, baudRate, Parity.None, BitsPerByte, StopBits.One)
                {
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs,
                    DtrEnable = true,
                    NewLine = "\n",
                };
                port.Open();
                port.DiscardInBuffer();
            }
            catch (Exception e)
            {
                status = $"Could not open {portName}: {e.Message}";
                port = null;
                return;
            }

            lock (sync)
            {
                decoder = CreateDecoder();
                inbox.Clear();
                ResetTracking();
            }
            rateFrames = 0;
            rateBytes = 0;
            readError = null;
            openedAt = Time.realtimeSinceStartup;
            running = true;
            readThread = new Thread(ReadLoop) { IsBackground = true, Name = "SerialLink" };
            readThread.Start();
#else
            status = "Set Api Compatibility Level to .NET Framework (Project Settings > Player)";
#endif
        }

        public void Disconnect()
        {
            running = false;
            if (readThread != null)
            {
                readThread.Join(ThreadJoinTimeoutMs);
                readThread = null;
            }
#if SERIAL_PORTS
            if (port != null)
            {
                try
                {
                    port.Close();
                }
                catch (Exception)
                {
                }
                port = null;
            }
#endif
            readError = null;
            framesPerSecond = 0f;
            bytesPerSecond = 0f;
            status = "Disconnected";
        }

        public void SetProtocol(ProtocolKind kind, JsonBackend backend)
        {
            protocol = kind;
            jsonBackend = backend;
            lock (sync)
            {
                decoder = CreateDecoder();
                inbox.Clear();
                ResetTracking();
            }
            rateFrames = 0;
            rateBytes = 0;
        }

        public void ResetCounters()
        {
            SetProtocol(protocol, jsonBackend);
        }

        private IProtocolDecoder CreateDecoder()
        {
            switch (protocol)
            {
                case ProtocolKind.Csv:
                    return new CsvDecoder();
                case ProtocolKind.Json:
                    if (jsonBackend == JsonBackend.Newtonsoft)
                    {
                        return new JsonDecoder("JSON (Newtonsoft)", DeserializeWithNewtonsoft);
                    }
                    return new JsonDecoder("JSON (JsonUtility)", DeserializeWithJsonUtility);
                default:
                    return new BinaryDecoder();
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

        private void ResetTracking()
        {
            haveSeq = false;
            lostPackets = 0;
            pendingPing = -1;
            rttResults.Clear();
            awaitingTest = false;
            hasTestResult = false;
            robustnessMode = false;
            bitErrorRate = 0.0;
            robust = default;
        }

#if SERIAL_PORTS
        private void ReadLoop()
        {
            byte[] buffer = new byte[ReadBufferSize];
            while (running)
            {
                int count;
                try
                {
                    count = port.Read(buffer, 0, buffer.Length);
                }
                catch (TimeoutException)
                {
                    continue;
                }
                catch (Exception e)
                {
                    if (running)
                    {
                        readError = e.Message;
                    }
                    return;
                }

                lock (sync)
                {
                    for (int i = 0; i < count; i++)
                    {
                        byte value = buffer[i];
                        if (bitErrorRate > 0.0)
                        {
                            value = InjectBitErrors(value);
                        }
                        if (decoder.Feed(value, out ControllerState state))
                        {
                            state.RxTimestamp = Stopwatch.GetTimestamp();
                            OnFrame(state);
                        }
                    }
                }
            }
        }
#endif

        private byte InjectBitErrors(byte value)
        {
            robust.BytesSeen++;
            for (int bit = 0; bit < BitsPerByte; bit++)
            {
                if (random.NextDouble() < bitErrorRate)
                {
                    value ^= (byte)(1 << bit);
                    robust.BitsFlipped++;
                }
            }
            return value;
        }

        private void OnFrame(ControllerState state)
        {
            if (robustnessMode)
            {
                robust.Accepted++;
                if (!state.SameValues(ControllerState.TestValue))
                {
                    robust.Undetected++;
                }
                return;
            }

            if (state.SameValues(ControllerState.TestValue))
            {
                if (awaitingTest)
                {
                    testResult = new TestFrame { State = state, Raw = decoder.GetLastFrame(), Protocol = decoder.Name };
                    hasTestResult = true;
                    awaitingTest = false;
                }
                return;
            }

            if (haveSeq)
            {
                int gap = (state.Seq - lastSeq - 1) & 0xFFFF;
                if (gap > 0 && gap < MaxPlausibleSeqGap)
                {
                    lostPackets += gap;
                }
            }
            lastSeq = state.Seq;
            haveSeq = true;

            if (pendingPing > 0 && state.Echo == pendingPing)
            {
                rttResults[pendingPing] = new RttSample
                {
                    Milliseconds = TicksToMs(state.RxTimestamp - pingSentTicks),
                    FrameBytes = state.FrameBytes,
                };
                pendingPing = -1;
            }

            inbox.Enqueue(state);
            if (inbox.Count > MaxInboxSize)
            {
                inbox.Dequeue();
            }
        }

        public void Drain(List<ControllerState> into)
        {
            lock (sync)
            {
                while (inbox.Count > 0)
                {
                    into.Add(inbox.Dequeue());
                }
            }
        }

        public bool SendCommand(string command)
        {
#if SERIAL_PORTS
            if (!running || port == null)
            {
                return false;
            }
            try
            {
                port.Write(command + "\n");
                return true;
            }
            catch (Exception e)
            {
                readError = e.Message;
                return false;
            }
#else
            return false;
#endif
        }

        public int SendPing()
        {
            int id;
            lock (sync)
            {
                id = nextPingId;
                nextPingId = nextPingId >= MaxPingId ? 1 : nextPingId + 1;
                rttResults.Remove(id);
                pendingPing = id;
                pingSentTicks = Stopwatch.GetTimestamp();
            }
            SendCommand("P" + id);
            return id;
        }

        public bool TryTakeRtt(int id, out RttSample sample)
        {
            lock (sync)
            {
                if (rttResults.TryGetValue(id, out sample))
                {
                    rttResults.Remove(id);
                    return true;
                }
            }
            sample = default;
            return false;
        }

        public void BeginTestCapture()
        {
            lock (sync)
            {
                awaitingTest = true;
                hasTestResult = false;
            }
        }

        public bool TryGetTestResult(out TestFrame result)
        {
            lock (sync)
            {
                result = testResult;
                return hasTestResult;
            }
        }

        public void BeginRobustness(double ber)
        {
            lock (sync)
            {
                robust = default;
                bitErrorRate = ber;
                robustnessMode = true;
            }
        }

        public RobustnessCounters EndRobustness()
        {
            lock (sync)
            {
                robustnessMode = false;
                bitErrorRate = 0.0;
                haveSeq = false;
                return robust;
            }
        }

        public LinkSnapshot GetSnapshot()
        {
            lock (sync)
            {
                return new LinkSnapshot
                {
                    Decoder = decoder.Stats.Clone(),
                    LostPackets = lostPackets,
                    FramesPerSecond = framesPerSecond,
                    BytesPerSecond = bytesPerSecond,
                    LastFrame = decoder.GetLastFrame(),
                };
            }
        }
    }
}
