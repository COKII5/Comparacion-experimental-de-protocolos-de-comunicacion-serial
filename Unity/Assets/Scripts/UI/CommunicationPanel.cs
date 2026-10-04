using System;
using System.Globalization;
using System.Text;
using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using PhysicalDigital.Measurements;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.UI
{
    public sealed class CommunicationPanel
    {
        private const long UnrecognizedBytesThreshold = 200;
        private const float PortRefreshPeriod = 2f;
        private const float ProtocolToolbarHeight = 34f;
        private const float BackendToolbarHeight = 30f;
        private const float RowHeight = 32f;
        private const float ArrowButtonWidth = 40f;
        private const float PortLabelWidth = 100f;
        private const float ResetButtonHeight = 30f;
        private const float SmallGap = 4f;
        private const float SectionGap = 8f;
        private const float LargeGap = 10f;
        private const string EmptyFrameText = "—";

        private static readonly string[] ProtocolNames = { "CSV", "JSON", "Binary" };
        private static readonly string[] BackendNames = { "JsonUtility", "Newtonsoft" };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly SerialLink link;
        private readonly ControllerInput input;
        private readonly MeasurementRunner runner;
        private string[] ports;
        private float portsTimer;
        private bool layoutShowsJsonBackend;
        private bool layoutShowsUnrecognizedWarning;

        public CommunicationPanel(SerialLink link, ControllerInput input, MeasurementRunner runner)
        {
            this.link = link;
            this.input = input;
            this.runner = runner;
            ports = SerialLink.AvailablePorts();
        }

        public void Tick(float deltaSeconds)
        {
            portsTimer -= deltaSeconds;
            if (portsTimer <= 0f)
            {
                ports = SerialLink.AvailablePorts();
                portsTimer = PortRefreshPeriod;
            }
        }

        public void CaptureLayoutState(LinkSnapshot snapshot)
        {
            layoutShowsJsonBackend = link.Protocol == ProtocolKind.Json;
            DecoderStats stats = snapshot.Decoder;
            layoutShowsUnrecognizedWarning = link.IsReady && stats != null
                && stats.FramesOk == 0 && stats.BytesTotal > UnrecognizedBytesThreshold;
        }

        public void Draw(HudTheme theme, LinkSnapshot snapshot)
        {
            theme.BeginPanel(HudTheme.PanelArea(HudTheme.Margin));
            GUILayout.Label("COMMUNICATION", theme.Title);
            GUILayout.Space(SmallGap);
            DrawProtocolSelector(theme);
            GUILayout.Space(LargeGap);
            DrawPortSelector(theme);
            DrawStatus(theme);
            if (snapshot.Decoder != null)
            {
                DrawLiveMetrics(theme, snapshot);
                DrawLastFrame(theme, snapshot.LastFrame);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset counters", theme.Button, GUILayout.Height(ResetButtonHeight)))
            {
                link.ResetCounters();
            }
            theme.EndPanel();
        }

        private void DrawProtocolSelector(HudTheme theme)
        {
            GUILayout.Label("Protocol (the Arduino switches automatically)", theme.Small);
            int selectedProtocol = GUILayout.Toolbar((int)link.Protocol, ProtocolNames, theme.Button, GUILayout.Height(ProtocolToolbarHeight));
            int selectedBackend = (int)link.JsonBackend;
            if (layoutShowsJsonBackend)
            {
                GUILayout.Space(SmallGap);
                selectedBackend = GUILayout.Toolbar(selectedBackend, BackendNames, theme.Button, GUILayout.Height(BackendToolbarHeight));
            }
            if (selectedProtocol != (int)link.Protocol || selectedBackend != (int)link.JsonBackend)
            {
                link.SetProtocol((ProtocolKind)selectedProtocol, (JsonBackend)selectedBackend);
            }
        }

        private void DrawPortSelector(HudTheme theme)
        {
            GUILayout.Label("Serial port", theme.Small);
            GUILayout.BeginHorizontal();
            bool canChoosePort = !link.IsOpen && ports.Length > 0;
            GUI.enabled = canChoosePort;
            if (GUILayout.Button("<", theme.Button, GUILayout.Width(ArrowButtonWidth), GUILayout.Height(RowHeight)))
            {
                CyclePort(-1);
            }
            GUI.enabled = true;
            string portText = string.IsNullOrEmpty(link.PortName) ? "(auto)" : link.PortName;
            GUILayout.Label(portText, theme.Label, GUILayout.Width(PortLabelWidth), GUILayout.Height(RowHeight));
            GUI.enabled = canChoosePort;
            if (GUILayout.Button(">", theme.Button, GUILayout.Width(ArrowButtonWidth), GUILayout.Height(RowHeight)))
            {
                CyclePort(+1);
            }
            GUI.enabled = !runner.IsRunning;
            if (GUILayout.Button(link.IsOpen ? "Disconnect" : "Connect", theme.Button, GUILayout.Height(RowHeight)))
            {
                ToggleConnection();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawStatus(HudTheme theme)
        {
            Color statusColor = link.IsReady ? HudTheme.Good : link.IsOpen ? HudTheme.Warn : HudTheme.Bad;
            theme.ColoredLabel(link.Status, statusColor);
        }

        private void DrawLiveMetrics(HudTheme theme, LinkSnapshot snapshot)
        {
            DecoderStats stats = snapshot.Decoder;
            GUILayout.Space(SectionGap);
            GUILayout.Label("LIVE", theme.Heading);
            theme.Metric("Frames/s", snapshot.FramesPerSecond.ToString("F0", Invariant));
            theme.Metric("Bytes/s", snapshot.BytesPerSecond.ToString("F0", Invariant));
            theme.Metric("Bytes per frame", stats.AvgFrameBytes.ToString("F1", Invariant));
            theme.Metric("Deserialization", stats.AvgParseMicros.ToString("F2", Invariant) + " µs");
            theme.Metric("Thread → game latency", input.AvgQueueLatencyMs.ToString("F2", Invariant) + " ms");
            theme.Metric("Valid frames", stats.FramesOk.ToString(Invariant));
            long errorCount = stats.IntegrityErrors + stats.FormatErrors;
            theme.Metric("Integrity / format errors", $"{stats.IntegrityErrors} / {stats.FormatErrors}",
                errorCount > 0 ? HudTheme.Warn : Color.white);
            theme.Metric("Discarded bytes (resync)", stats.DiscardedBytes.ToString(Invariant));
            theme.Metric("Lost packets (seq)", snapshot.LostPackets.ToString(Invariant),
                snapshot.LostPackets > 0 ? HudTheme.Warn : Color.white);

            if (layoutShowsUnrecognizedWarning)
            {
                theme.ColoredLabel($"No {ProtocolNames[(int)link.Protocol]} frames recognized. Is SerialController uploaded?", HudTheme.Bad);
            }
        }

        private void DrawLastFrame(HudTheme theme, byte[] frame)
        {
            GUILayout.Space(SectionGap);
            GUILayout.Label("LAST FRAME (readability)", theme.Heading);
            GUILayout.Label(FormatFrame(frame), theme.Mono);
        }

        private void ToggleConnection()
        {
            if (link.IsOpen)
            {
                link.Disconnect();
            }
            else
            {
                link.Connect();
            }
        }

        private void CyclePort(int direction)
        {
            if (ports.Length == 0)
            {
                return;
            }
            int index = Array.IndexOf(ports, link.PortName);
            index = index < 0 ? 0 : (index + direction + ports.Length) % ports.Length;
            link.PortName = ports[index];
        }

        private string FormatFrame(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
            {
                return EmptyFrameText;
            }
            if (link.Protocol == ProtocolKind.Binary)
            {
                return "hex: " + BitConverter.ToString(frame).Replace('-', ' ');
            }
            return Encoding.ASCII.GetString(frame).TrimEnd('\n');
        }
    }
}
