using System.Collections.Generic;
using System.Diagnostics;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.Communication
{
    public sealed class SerialLink : MonoBehaviour
    {
        public const int DefaultPeriodMs = 20;

        private const int MaxInboxSize = 4096;
        private const int MinBaudRate = 300;
        private const string CommandTerminator = "\n";
        private const string ProtocolCommand = "M";
        private const float ProtocolSettleSeconds = 0.15f;
        private const float NoDeadline = -1f;

        [Header("Port")]
        [Tooltip("Empty = on Connect, the highest-numbered available COM port is used.")]
        [SerializeField] private string portName = "";
        [SerializeField] private int baudRate = 115200;
        [SerializeField] private bool autoConnect = true;
        [Tooltip("The Uno resets when the port opens; commands are delayed by this many seconds.")]
        [SerializeField] private float bootDelay = 2f;

        [Header("Protocol (sent to the Arduino with the M command)")]
        [SerializeField] private ProtocolKind protocol = ProtocolKind.Csv;
        [SerializeField] private JsonBackend jsonBackend = JsonBackend.JsonUtility;

        private readonly object sync = new object();
        private readonly Queue<ControllerState> inbox = new Queue<ControllerState>();
        private readonly SequenceTracker sequence = new SequenceTracker();
        private readonly PingTracker pings = new PingTracker();
        private readonly TestValueCapture testCapture = new TestValueCapture();
        private readonly BitErrorInjector errorInjector = new BitErrorInjector();
        private readonly RateMeter rates = new RateMeter();
        private SerialPortConnection connection;
        private IProtocolDecoder decoder;
        private float openedAt;
        private string status = "Disconnected";
        private bool protocolPending;
        private float settleDeadline = NoDeadline;

        public string PortName
        {
            get => portName;
            set => portName = value;
        }

        public int BaudRate => baudRate;
        public ProtocolKind Protocol => protocol;
        public JsonBackend JsonBackend => jsonBackend;
        public string Status => status;
        public bool IsOpen => connection.IsOpen;
        public bool IsReady => IsBooted && !protocolPending && settleDeadline == NoDeadline;
        public string ProtocolLabel => DecoderFactory.Label(protocol, jsonBackend);
        public string ProtocolTag => ProtocolLabel.Replace(" (", "_").Replace(")", "");

        private bool IsBooted => connection.IsOpen && Time.realtimeSinceStartup - openedAt >= bootDelay;

        public static string[] AvailablePorts()
        {
            return SerialPortConnection.AvailablePorts();
        }

        private void Awake()
        {
            connection = new SerialPortConnection(OnBytesReceived);
            decoder = DecoderFactory.Create(protocol, jsonBackend);
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

        private void OnValidate()
        {
            baudRate = Mathf.Max(MinBaudRate, baudRate);
            bootDelay = Mathf.Max(0f, bootDelay);
        }

        private void OnDisable()
        {
            Disconnect();
        }

        private void Update()
        {
            SyncProtocol();
            UpdateStatus();
            if (rates.Advance(Time.unscaledDeltaTime))
            {
                SampleRates();
            }
        }

        public void Connect()
        {
            Disconnect();
            if (string.IsNullOrEmpty(portName) && !TrySelectDefaultPort())
            {
                return;
            }
            ResetSession();
            if (!connection.TryOpen(portName, baudRate, out string failure))
            {
                status = $"Could not open {portName}: {failure}";
                return;
            }
            openedAt = Time.realtimeSinceStartup;
            protocolPending = true;
        }

        public void Disconnect()
        {
            connection.Close();
            rates.Clear();
            protocolPending = false;
            settleDeadline = NoDeadline;
            status = "Disconnected";
        }

        public void SetProtocol(ProtocolKind kind, JsonBackend backend)
        {
            protocol = kind;
            jsonBackend = backend;
            ResetSession();
            protocolPending = connection.IsOpen;
        }

        public void ResetCounters()
        {
            ResetSession();
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
            return connection.TryWrite(command + CommandTerminator);
        }

        public int SendPing()
        {
            int id;
            lock (sync)
            {
                id = pings.Begin(Stopwatch.GetTimestamp());
            }
            SendCommand("P" + id);
            return id;
        }

        public bool TryTakeRtt(int id, out RttSample sample)
        {
            lock (sync)
            {
                return pings.TryTake(id, out sample);
            }
        }

        public void BeginTestCapture()
        {
            lock (sync)
            {
                testCapture.Begin();
            }
        }

        public bool TryGetTestResult(out TestFrame result)
        {
            lock (sync)
            {
                return testCapture.TryGet(out result);
            }
        }

        public void BeginRobustness(double bitErrorRate)
        {
            lock (sync)
            {
                errorInjector.Begin(bitErrorRate);
            }
        }

        public RobustnessCounters EndRobustness()
        {
            lock (sync)
            {
                sequence.Resync();
                return errorInjector.End();
            }
        }

        public LinkSnapshot GetSnapshot()
        {
            lock (sync)
            {
                return new LinkSnapshot
                {
                    Decoder = decoder.Stats.Clone(),
                    LostPackets = sequence.LostPackets,
                    FramesPerSecond = rates.FramesPerSecond,
                    BytesPerSecond = rates.BytesPerSecond,
                    LastFrame = decoder.GetLastFrame(),
                };
            }
        }

        private bool TrySelectDefaultPort()
        {
            string[] ports = AvailablePorts();
            if (ports.Length == 0)
            {
                status = "No COM ports available";
                return false;
            }
            portName = ports[ports.Length - 1];
            return true;
        }

        private void ResetSession()
        {
            lock (sync)
            {
                decoder = DecoderFactory.Create(protocol, jsonBackend);
                inbox.Clear();
                sequence.Reset();
                pings.Reset();
                testCapture.Reset();
                errorInjector.Reset();
            }
            rates.Restart();
        }

        private void UpdateStatus()
        {
            string error = connection.Error;
            if (error != null)
            {
                Disconnect();
                status = "Error: " + error;
            }
            else if (IsReady)
            {
                status = $"Connected to {portName}";
            }
            else if (IsBooted)
            {
                status = $"Switching the Arduino to {ProtocolLabel}...";
            }
            else if (connection.IsOpen)
            {
                status = $"Opening {portName} (Arduino reset)...";
            }
        }

        private void SyncProtocol()
        {
            if (protocolPending && IsBooted && SendCommand(ProtocolCommand + (int)protocol))
            {
                protocolPending = false;
                settleDeadline = Time.realtimeSinceStartup + ProtocolSettleSeconds;
            }
            else if (settleDeadline != NoDeadline && Time.realtimeSinceStartup >= settleDeadline)
            {
                settleDeadline = NoDeadline;
                ResetSession();
            }
        }

        private void SampleRates()
        {
            long frames;
            long bytes;
            lock (sync)
            {
                frames = decoder.Stats.FramesOk;
                bytes = decoder.Stats.BytesTotal;
            }
            rates.Sample(frames, bytes);
        }

        private void OnBytesReceived(byte[] buffer, int count)
        {
            lock (sync)
            {
                for (int i = 0; i < count; i++)
                {
                    byte value = errorInjector.Apply(buffer[i]);
                    if (decoder.Feed(value, out ControllerState state))
                    {
                        state.RxTimestamp = Stopwatch.GetTimestamp();
                        RouteFrame(state);
                    }
                }
            }
        }

        private void RouteFrame(ControllerState state)
        {
            if (errorInjector.IsActive)
            {
                errorInjector.CountAccepted(state);
                return;
            }
            if (state.SameValues(ControllerState.TestValue))
            {
                testCapture.Offer(state, decoder);
                return;
            }
            sequence.Track(state.Seq);
            pings.Match(state);
            EnqueueState(state);
        }

        private void EnqueueState(ControllerState state)
        {
            inbox.Enqueue(state);
            if (inbox.Count > MaxInboxSize)
            {
                inbox.Dequeue();
            }
        }
    }
}
