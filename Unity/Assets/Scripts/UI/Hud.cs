using System.Globalization;
using System.IO;
using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using PhysicalDigital.Measurements;
using UnityEngine;

namespace PhysicalDigital.UI
{
    public sealed class Hud : MonoBehaviour
    {
        private const float SnapshotPeriod = 0.2f;
        private const float NoteSeconds = 3f;

        private const float HeaderWidth = 900f;
        private const float HeaderTop = 26f;
        private const float HeaderHeight = 60f;
        private const float ScoreTop = 88f;
        private const float ScoreHeight = 36f;
        private const float MessageTop = 150f;
        private const float MessageHeight = 44f;
        private const float MessageOverhang = 100f;
        private const float TimerBarWidth = 520f;
        private const float TimerBarTop = 204f;
        private const float TimerBarHeight = 10f;
        private const float TimerWarningFraction = 0.3f;

        private const float PadLabelOffset = 1.45f;
        private const float DotSize = 14f;
        private const float PadLabelGap = 18f;
        private const float PadLabelWidth = 160f;
        private const float PadLabelHeight = 28f;

        private const float GaugeWidth = 620f;
        private const float GaugeBottomOffset = 190f;
        private const float GaugeTitleHeight = 26f;
        private const float GaugeBarOffset = 32f;
        private const float GaugeBarHeight = 16f;
        private const float GaugeTextOffset = 54f;
        private const float GaugeTextHeight = 26f;

        private const float FooterBottomOffset = 44f;
        private const float FooterHeight = 30f;

        private static readonly string[] PadNames = { "Green", "Red", "Yellow", "Blue" };
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private SerialLink link;
        private ControllerInput input;
        private SequenceGame game;
        private MeasurementRunner runner;
        private S3App app;
        private HudTheme theme;
        private CommunicationPanel communicationPanel;
        private MeasurementPanel measurementPanel;

        private LinkSnapshot snapshot;
        private float snapshotTimer;
        private string note = "";
        private float noteTimer;
        private bool showPanels = true;
        private bool layoutShowsPanels = true;

        private void Start()
        {
            link = GetComponent<SerialLink>();
            input = GetComponent<ControllerInput>();
            game = GetComponent<SequenceGame>();
            runner = GetComponent<MeasurementRunner>();
            app = GetComponent<S3App>();
            communicationPanel = new CommunicationPanel(link, input, runner);
            measurementPanel = new MeasurementPanel(link, runner);
        }

        private void Update()
        {
            snapshotTimer -= Time.unscaledDeltaTime;
            if (snapshotTimer <= 0f)
            {
                snapshot = link.GetSnapshot();
                snapshotTimer = SnapshotPeriod;
            }
            communicationPanel.Tick(Time.unscaledDeltaTime);
            noteTimer -= Time.unscaledDeltaTime;
        }

        private void OnGUI()
        {
            if (theme == null)
            {
                theme = new HudTheme();
            }
            if (Event.current.type == EventType.Layout)
            {
                CaptureLayoutState();
            }
            HandleKeys();

            float scale = Screen.height / HudTheme.VirtualHeight;
            float width = Screen.width / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            DrawGameHeader(width);
            DrawPadLabels(scale);
            DrawTempo(width);
            if (layoutShowsPanels)
            {
                communicationPanel.Draw(theme, snapshot);
                measurementPanel.Draw(theme, width);
            }
            DrawFooter(width);
        }

        private void CaptureLayoutState()
        {
            layoutShowsPanels = showPanels;
            communicationPanel.CaptureLayoutState(snapshot);
            measurementPanel.CaptureLayoutState();
        }

        private void HandleKeys()
        {
            Event currentEvent = Event.current;
            if (currentEvent.type != EventType.KeyDown)
            {
                return;
            }
            if (currentEvent.keyCode == KeyCode.H)
            {
                showPanels = !showPanels;
                currentEvent.Use();
            }
            else if (currentEvent.keyCode == KeyCode.F12)
            {
                note = SaveScreenshot();
                noteTimer = NoteSeconds;
                currentEvent.Use();
            }
        }

        private string SaveScreenshot()
        {
            MeasurementStorage storage = runner.Storage;
            if (!storage.TryCreateOutputFolder())
            {
                return "Could not create the folder " + storage.OutputFolder;
            }
            string file = Path.Combine(storage.OutputFolder, MeasurementStorage.TimestampedName("screenshot", link.ProtocolTag, "png"));
            ScreenCapture.CaptureScreenshot(file);
            return "Screenshot saved: " + Path.GetFileName(file);
        }

        private void DrawGameHeader(float width)
        {
            float left = (width - HeaderWidth) / 2f;
            GUI.Label(new Rect(left, HeaderTop, HeaderWidth, HeaderHeight), "S3 · Sequence Game", theme.BigTitle);
            GUI.Label(new Rect(left, ScoreTop, HeaderWidth, ScoreHeight),
                $"Round {Mathf.Max(1, game.Round)}   ·   Score {game.Score}   ·   Best {game.Best}", theme.Subtitle);

            theme.Message.normal.textColor = PhaseColor(game.CurrentPhase);
            GUI.Label(new Rect(left - MessageOverhang, MessageTop, HeaderWidth + 2f * MessageOverhang, MessageHeight), game.Message, theme.Message);

            if (game.CurrentPhase == SequenceGame.Phase.Input)
            {
                Texture2D fill = game.InputTimeLeft01 < TimerWarningFraction ? theme.BarWarnTexture : theme.BarFillTexture;
                Rect timerRect = new Rect((width - TimerBarWidth) / 2f, TimerBarTop, TimerBarWidth, TimerBarHeight);
                theme.DrawBar(timerRect, game.InputTimeLeft01, fill);
            }
        }

        private static Color PhaseColor(SequenceGame.Phase phase)
        {
            switch (phase)
            {
                case SequenceGame.Phase.GameOver:
                    return HudTheme.Bad;
                case SequenceGame.Phase.RoundWon:
                    return HudTheme.Good;
                case SequenceGame.Phase.Input:
                    return HudTheme.Accent;
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
                Texture2D dot = input.IsHeld(i) ? theme.DotOnTexture : theme.DotOffTexture;
                GUI.DrawTexture(new Rect(guiX - DotSize / 2f, guiY, DotSize, DotSize), dot);
                Rect labelRect = new Rect(guiX - PadLabelWidth / 2f, guiY + PadLabelGap, PadLabelWidth, PadLabelHeight);
                GUI.Label(labelRect, $"B{i + 1} · {PadNames[i]}", theme.PadLabel);
            }
        }

        private void DrawTempo(float width)
        {
            float left = (width - GaugeWidth) / 2f;
            float top = HudTheme.VirtualHeight - GaugeBottomOffset;
            GUI.Label(new Rect(left, top, GaugeWidth, GaugeTitleHeight), "POTENTIOMETER → TEMPO", theme.Heading);
            theme.DrawBar(new Rect(left, top + GaugeBarOffset, GaugeWidth, GaugeBarHeight), input.Pot01, theme.BarFillTexture);
            string source = input.UsingSerial ? "Arduino" : "keyboard ↑/↓";
            GUI.Label(new Rect(left, top + GaugeTextOffset, GaugeWidth, GaugeTextHeight),
                string.Format(Invariant, "pot = {0} ({1})   ·   step {2:F2} s   ·   answer limit {3:F1} s",
                    input.PotRaw, source, game.StepSeconds, game.InputTimeout), theme.Small);
        }

        private void DrawFooter(float width)
        {
            string text = noteTimer > 0f
                ? note
                : "Keyboard without Arduino: 1-4 = buttons · ↑/↓ = potentiometer   |   H = panels   |   F12 = screenshot";
            GUI.Label(new Rect(0f, HudTheme.VirtualHeight - FooterBottomOffset, width, FooterHeight), text, theme.Footer);
        }
    }
}
