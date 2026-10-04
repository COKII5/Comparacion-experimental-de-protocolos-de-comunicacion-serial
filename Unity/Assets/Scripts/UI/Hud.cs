using System;
using System.Globalization;
using System.IO;
using System.Text;
using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using PhysicalDigital.Measurements;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.UI
{
    public sealed class Hud : MonoBehaviour
    {
        private const float VirtualHeight = 1080f;
        private const float PanelWidth = 440f;
        private const float Margin = 22f;
        private const float SnapshotPeriod = 0.2f;
        private const float PortRefreshPeriod = 2f;
        private const float NoteSeconds = 3f;
        private const long UnrecognizedBytesThreshold = 200;
        private const float PadLabelOffset = 1.45f;

        private static readonly string[] ProtocolNames = { "CSV", "JSON", "Binary" };
        private static readonly string[] BackendNames = { "JsonUtility", "Newtonsoft" };
        private static readonly string[] PadNames = { "Green", "Red", "Yellow", "Blue" };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Color Accent = new Color(0.36f, 0.78f, 1f);
        private static readonly Color Good = new Color(0.45f, 0.9f, 0.55f);
        private static readonly Color Warn = new Color(1f, 0.8f, 0.35f);
        private static readonly Color Bad = new Color(1f, 0.42f, 0.42f);
        private static readonly Color Muted = new Color(0.68f, 0.72f, 0.82f);

        private SerialLink link;
        private ControllerInput input;
        private SequenceGame game;
        private MeasurementRunner runner;
        private S3App app;

        private bool showPanels = true;
        private bool stylesReady;
        private GUIStyle panel;
        private GUIStyle title;
        private GUIStyle heading;
        private GUIStyle label;
        private GUIStyle small;
        private GUIStyle mono;
        private GUIStyle button;
        private GUIStyle bigTitle;
        private GUIStyle subtitle;
        private GUIStyle message;
        private GUIStyle padLabel;
        private GUIStyle footer;
        private Texture2D panelTexture;
        private Texture2D barBackTexture;
        private Texture2D barFillTexture;
        private Texture2D barWarnTexture;
        private Texture2D dotOnTexture;
        private Texture2D dotOffTexture;

        private LinkSnapshot snapshot;
        private float snapshotTimer;
        private string[] ports = Array.Empty<string>();
        private float portsTimer;
        private string note = "";
        private float noteTimer;

        private bool layoutShowsPanels = true;
        private bool layoutShowsJsonBackend;
        private bool layoutShowsUnrecognizedWarning;
        private bool layoutShowsProgress;

        private void Start()
        {
            link = GetComponent<SerialLink>();
            input = GetComponent<ControllerInput>();
            game = GetComponent<SequenceGame>();
            runner = GetComponent<MeasurementRunner>();
            app = GetComponent<S3App>();
            ports = SerialLink.AvailablePorts();
        }

        private void Update()
        {
            snapshotTimer -= Time.unscaledDeltaTime;
            if (snapshotTimer <= 0f)
            {
                snapshot = link.GetSnapshot();
                snapshotTimer = SnapshotPeriod;
            }
            portsTimer -= Time.unscaledDeltaTime;
            if (portsTimer <= 0f)
            {
                ports = SerialLink.AvailablePorts();
                portsTimer = PortRefreshPeriod;
            }
            noteTimer -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (Event.current.type == EventType.Layout)
            {
                CaptureLayoutState();
            }
            HandleKeys();

            float scale = Screen.height / VirtualHeight;
            float width = Screen.width / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawGameHeader(width);
            DrawPadLabels(scale);
            DrawTempo(width);
            if (layoutShowsPanels)
            {
                DrawCommunicationPanel();
                DrawMeasurementPanel(width);
            }
            DrawFooter(width);
        }

        private void CaptureLayoutState()
        {
            layoutShowsPanels = showPanels;
            layoutShowsJsonBackend = link.protocol == ProtocolKind.Json;
            DecoderStats stats = snapshot.Decoder;
            layoutShowsUnrecognizedWarning = link.IsReady && stats != null
                && stats.FramesOk == 0 && stats.BytesTotal > UnrecognizedBytesThreshold;
            layoutShowsProgress = runner.IsRunning;
        }

        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown)
            {
                return;
            }
            if (e.keyCode == KeyCode.H)
            {
                showPanels = !showPanels;
                e.Use();
            }
            else if (e.keyCode == KeyCode.F12)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Invariant);
                string file = Path.Combine(runner.OutputFolder, $"screenshot_{link.ProtocolTag}_{stamp}.png");
                ScreenCapture.CaptureScreenshot(file);
                note = "Screenshot saved: " + Path.GetFileName(file);
                noteTimer = NoteSeconds;
                e.Use();
            }
        }

        private void DrawGameHeader(float width)
        {
            const float headerWidth = 900f;
            float x = (width - headerWidth) / 2f;
            GUI.Label(new Rect(x, 26f, headerWidth, 60f), "S3 · Sequence Game", bigTitle);
            GUI.Label(new Rect(x, 88f, headerWidth, 36f),
                $"Round {Mathf.Max(1, game.Round)}   ·   Score {game.Score}   ·   Best {game.Best}", subtitle);

            message.normal.textColor = PhaseColor(game.CurrentPhase);
            GUI.Label(new Rect(x - 100f, 150f, headerWidth + 200f, 44f), game.Message, message);

            if (game.CurrentPhase == SequenceGame.Phase.Input)
            {
                const float barWidth = 520f;
                Texture2D fill = game.InputTimeLeft01 < 0.3f ? barWarnTexture : barFillTexture;
                DrawBar(new Rect((width - barWidth) / 2f, 204f, barWidth, 10f), game.InputTimeLeft01, fill);
            }
        }

        private static Color PhaseColor(SequenceGame.Phase phase)
        {
            switch (phase)
            {
                case SequenceGame.Phase.GameOver:
                    return Bad;
                case SequenceGame.Phase.RoundWon:
                    return Good;
                case SequenceGame.Phase.Input:
                    return Accent;
                default:
                    return Color.white;
            }
        }

        private void DrawPadLabels(float scale)
        {
            Camera camera = Camera.main;
            if (camera == null || app.Pads == null)
            {
                return;
            }
            for (int i = 0; i < app.Pads.Length; i++)
            {
                Vector3 world = app.Pads[i].transform.position + Vector3.down * PadLabelOffset;
                Vector3 screen = camera.WorldToScreenPoint(world);
                float guiX = screen.x / scale;
                float guiY = (Screen.height - screen.y) / scale;
                Texture2D dot = input.IsHeld(i) ? dotOnTexture : dotOffTexture;
                GUI.DrawTexture(new Rect(guiX - 7f, guiY, 14f, 14f), dot);
                GUI.Label(new Rect(guiX - 80f, guiY + 18f, 160f, 28f), $"B{i + 1} · {PadNames[i]}", padLabel);
            }
        }

        private void DrawTempo(float width)
        {
            const float gaugeWidth = 620f;
            float x = (width - gaugeWidth) / 2f;
            float y = VirtualHeight - 190f;
            GUI.Label(new Rect(x, y, gaugeWidth, 26f), "POTENTIOMETER → TEMPO", heading);
            DrawBar(new Rect(x, y + 32f, gaugeWidth, 16f), input.Pot01, barFillTexture);
            string source = input.UsingSerial ? "Arduino" : "keyboard ↑/↓";
            GUI.Label(new Rect(x, y + 54f, gaugeWidth, 26f),
                string.Format(Invariant, "pot = {0} ({1})   ·   step {2:F2} s   ·   answer limit {3:F1} s",
                    input.PotRaw, source, game.StepSeconds, game.InputTimeout), small);
        }

        private void DrawFooter(float width)
        {
            string text = noteTimer > 0f
                ? note
                : "Keyboard without Arduino: 1-4 = buttons · ↑/↓ = potentiometer   |   H = panels   |   F12 = screenshot";
            GUI.Label(new Rect(0f, VirtualHeight - 44f, width, 30f), text, footer);
        }

        private void DrawCommunicationPanel()
        {
            Rect area = new Rect(Margin, Margin, PanelWidth, VirtualHeight - 2f * Margin - 60f);
            GUI.Box(area, GUIContent.none, panel);
            GUILayout.BeginArea(new Rect(area.x + 18f, area.y + 14f, area.width - 36f, area.height - 28f));

            GUILayout.Label("COMMUNICATION", title);
            GUILayout.Space(4f);

            GUILayout.Label("Protocol (must match the sketch)", small);
            int selectedProtocol = GUILayout.Toolbar((int)link.protocol, ProtocolNames, button, GUILayout.Height(34f));
            int selectedBackend = (int)link.jsonBackend;
            if (layoutShowsJsonBackend)
            {
                GUILayout.Space(4f);
                selectedBackend = GUILayout.Toolbar(selectedBackend, BackendNames, button, GUILayout.Height(30f));
            }
            if (selectedProtocol != (int)link.protocol || selectedBackend != (int)link.jsonBackend)
            {
                link.SetProtocol((ProtocolKind)selectedProtocol, (JsonBackend)selectedBackend);
            }

            GUILayout.Space(10f);
            GUILayout.Label("Serial port", small);
            GUILayout.BeginHorizontal();
            GUI.enabled = !link.IsOpen && ports.Length > 0;
            if (GUILayout.Button("◀", button, GUILayout.Width(40f), GUILayout.Height(32f)))
            {
                CyclePort(-1);
            }
            GUI.enabled = true;
            string portText = string.IsNullOrEmpty(link.portName) ? "(auto)" : link.portName;
            GUILayout.Label(portText, label, GUILayout.Width(100f), GUILayout.Height(32f));
            GUI.enabled = !link.IsOpen && ports.Length > 0;
            if (GUILayout.Button("▶", button, GUILayout.Width(40f), GUILayout.Height(32f)))
            {
                CyclePort(+1);
            }
            GUI.enabled = !runner.IsRunning;
            if (GUILayout.Button(link.IsOpen ? "Disconnect" : "Connect", button, GUILayout.Height(32f)))
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
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            label.normal.textColor = link.IsReady ? Good : link.IsOpen ? Warn : Bad;
            GUILayout.Label(link.Status, label);
            label.normal.textColor = Color.white;

            DecoderStats stats = snapshot.Decoder;
            if (stats != null)
            {
                GUILayout.Space(8f);
                GUILayout.Label("LIVE", heading);
                Metric("Frames/s", snapshot.FramesPerSecond.ToString("F0", Invariant));
                Metric("Bytes/s", snapshot.BytesPerSecond.ToString("F0", Invariant));
                Metric("Bytes per frame", stats.AvgFrameBytes.ToString("F1", Invariant));
                Metric("Deserialization", stats.AvgParseMicros.ToString("F2", Invariant) + " µs");
                Metric("Thread → game latency", input.AvgQueueLatencyMs.ToString("F2", Invariant) + " ms");
                Metric("Valid frames", stats.FramesOk.ToString(Invariant));
                long errorCount = stats.IntegrityErrors + stats.FormatErrors;
                Metric("Integrity / format errors", $"{stats.IntegrityErrors} / {stats.FormatErrors}",
                    errorCount > 0 ? Warn : Color.white);
                Metric("Lost packets (seq)", snapshot.LostPackets.ToString(Invariant),
                    snapshot.LostPackets > 0 ? Warn : Color.white);

                if (layoutShowsUnrecognizedWarning)
                {
                    label.normal.textColor = Bad;
                    GUILayout.Label($"No frames recognized. Is the {ProtocolNames[(int)link.protocol]} sketch loaded on the Arduino?", label);
                    label.normal.textColor = Color.white;
                }

                GUILayout.Space(8f);
                GUILayout.Label("LAST FRAME (readability)", heading);
                GUILayout.Label(FormatFrame(snapshot.LastFrame), mono);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset counters", button, GUILayout.Height(30f)))
            {
                link.ResetCounters();
            }
            GUILayout.EndArea();
        }

        private void CyclePort(int direction)
        {
            if (ports.Length == 0)
            {
                return;
            }
            int index = Array.IndexOf(ports, link.portName);
            index = index < 0 ? 0 : (index + direction + ports.Length) % ports.Length;
            link.portName = ports[index];
        }

        private string FormatFrame(byte[] frame)
        {
            if (frame == null || frame.Length == 0)
            {
                return "—";
            }
            if (link.protocol == ProtocolKind.Binary)
            {
                return "hex: " + BitConverter.ToString(frame).Replace('-', ' ');
            }
            return Encoding.ASCII.GetString(frame).TrimEnd('\n');
        }

        private void Metric(string name, string value)
        {
            Metric(name, value, Color.white);
        }

        private void Metric(string name, string value, Color valueColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, small, GUILayout.Width(250f));
            label.normal.textColor = valueColor;
            GUILayout.Label(value, label);
            label.normal.textColor = Color.white;
            GUILayout.EndHorizontal();
        }

        private void DrawMeasurementPanel(float width)
        {
            Rect area = new Rect(width - PanelWidth - Margin, Margin, PanelWidth, VirtualHeight - 2f * Margin - 60f);
            GUI.Box(area, GUIContent.none, panel);
            GUILayout.BeginArea(new Rect(area.x + 18f, area.y + 14f, area.width - 36f, area.height - 28f));

            GUILayout.Label("MEASUREMENTS", title);
            GUILayout.Label($"Active protocol: {link.ProtocolLabel}", small);
            GUILayout.Space(6f);

            GUI.enabled = !runner.IsRunning && link.IsReady;
            if (GUILayout.Button("1 · Value test", button, GUILayout.Height(34f)))
            {
                runner.StartValueTest();
            }
            if (GUILayout.Button($"2 · Round-trip latency (n={runner.pingSamples})", button, GUILayout.Height(34f)))
            {
                runner.StartLatency();
            }
            if (GUILayout.Button(string.Format(Invariant, "3 · Maximum throughput ({0:F0} s)", runner.throughputSeconds), button, GUILayout.Height(34f)))
            {
                runner.StartThroughput();
            }
            if (GUILayout.Button(string.Format(Invariant, "4 · Robustness (BER {0}, {1:F0} s)",
                    runner.bitErrorRate, runner.robustnessSeconds), button, GUILayout.Height(34f)))
            {
                runner.StartRobustness();
            }
            GUILayout.Space(4f);
            if (GUILayout.Button("▶ Full run (1→4 + summary.csv)", button, GUILayout.Height(40f)))
            {
                runner.StartFullRun();
            }
            GUI.enabled = true;

            if (layoutShowsProgress)
            {
                GUILayout.Space(8f);
                GUILayout.Label(runner.CurrentTask, small);
                Rect progressRect = GUILayoutUtility.GetRect(10f, 12f, GUILayout.ExpandWidth(true));
                DrawBar(progressRect, runner.Progress01, barFillTexture);
                if (GUILayout.Button("Cancel", button, GUILayout.Height(30f)))
                {
                    runner.Cancel();
                }
            }

            GUILayout.Space(10f);
            GUILayout.Label("RESULTS", heading);
            GUILayout.Label(runner.Results, label);

            GUILayout.FlexibleSpace();
            GUILayout.Label("Raw data in:", small);
            GUILayout.Label(runner.OutputFolder, small);
            GUILayout.EndArea();
        }

        private void DrawBar(Rect rect, float value01, Texture2D fill)
        {
            GUI.DrawTexture(rect, barBackTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), fill);
        }

        private void EnsureStyles()
        {
            if (stylesReady)
            {
                return;
            }
            stylesReady = true;

            panelTexture = Solid(new Color(0.05f, 0.06f, 0.1f, 0.82f));
            barBackTexture = Solid(new Color(1f, 1f, 1f, 0.12f));
            barFillTexture = Solid(Accent);
            barWarnTexture = Solid(Warn);
            dotOnTexture = Solid(Color.white);
            dotOffTexture = Solid(new Color(1f, 1f, 1f, 0.18f));

            panel = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, wordWrap = true, normal = { textColor = Color.white } };
            small = new GUIStyle(label) { fontSize = 16, normal = { textColor = Muted } };
            mono = new GUIStyle(label) { fontSize = 17, normal = { textColor = new Color(0.85f, 0.95f, 1f) } };
            heading = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Accent } };
            title = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 17 };
            bigTitle = new GUIStyle(label) { fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            subtitle = new GUIStyle(label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, normal = { textColor = Muted } };
            message = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            padLabel = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.UpperCenter, normal = { textColor = Muted } };
            footer = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        private static Texture2D Solid(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
