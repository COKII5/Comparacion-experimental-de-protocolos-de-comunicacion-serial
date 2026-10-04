using System.Globalization;
using PhysicalDigital.Communication;
using PhysicalDigital.Measurements;
using UnityEngine;

namespace PhysicalDigital.UI
{
    public sealed class MeasurementPanel
    {
        private const float TestButtonHeight = 34f;
        private const float FullRunButtonHeight = 40f;
        private const float CancelButtonHeight = 30f;
        private const float ProgressBarMinWidth = 10f;
        private const float ProgressBarHeight = 12f;
        private const float SmallGap = 4f;
        private const float MediumGap = 6f;
        private const float SectionGap = 8f;
        private const float LargeGap = 10f;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly SerialLink link;
        private readonly MeasurementRunner runner;
        private bool layoutShowsProgress;

        public MeasurementPanel(SerialLink link, MeasurementRunner runner)
        {
            this.link = link;
            this.runner = runner;
        }

        public void CaptureLayoutState()
        {
            layoutShowsProgress = runner.IsRunning;
        }

        public void Draw(HudTheme theme, float width)
        {
            theme.BeginPanel(HudTheme.PanelArea(width - HudTheme.PanelWidth - HudTheme.Margin));
            GUILayout.Label("MEASUREMENTS", theme.Title);
            GUILayout.Label($"Active protocol: {link.ProtocolLabel}", theme.Small);
            GUILayout.Space(MediumGap);
            DrawTestButtons(theme);
            if (layoutShowsProgress)
            {
                DrawProgress(theme);
            }
            DrawResults(theme);
            theme.EndPanel();
        }

        private void DrawTestButtons(HudTheme theme)
        {
            GUI.enabled = !runner.IsRunning && link.IsReady;
            if (TestButton(theme, "1 · Value test"))
            {
                runner.StartValueTest();
            }
            if (TestButton(theme, $"2 · Round-trip latency (n={runner.PingSamples})"))
            {
                runner.StartLatency();
            }
            if (TestButton(theme, string.Format(Invariant, "3 · Maximum throughput ({0:F0} s)", runner.ThroughputSeconds)))
            {
                runner.StartThroughput();
            }
            if (TestButton(theme, string.Format(Invariant, "4 · Robustness (BER {0}, {1:F0} s)", runner.BitErrorRate, runner.RobustnessSeconds)))
            {
                runner.StartRobustness();
            }
            GUILayout.Space(SmallGap);
            if (GUILayout.Button("Full run (1-4 + summary.csv)", theme.Button, GUILayout.Height(FullRunButtonHeight)))
            {
                runner.StartFullRun();
            }
            GUI.enabled = true;
        }

        private static bool TestButton(HudTheme theme, string text)
        {
            return GUILayout.Button(text, theme.Button, GUILayout.Height(TestButtonHeight));
        }

        private void DrawProgress(HudTheme theme)
        {
            GUILayout.Space(SectionGap);
            GUILayout.Label(runner.CurrentTask, theme.Small);
            Rect progressRect = GUILayoutUtility.GetRect(ProgressBarMinWidth, ProgressBarHeight, GUILayout.ExpandWidth(true));
            theme.DrawBar(progressRect, runner.Progress01, theme.BarFillTexture);
            if (GUILayout.Button("Cancel", theme.Button, GUILayout.Height(CancelButtonHeight)))
            {
                runner.Cancel();
            }
        }

        private void DrawResults(HudTheme theme)
        {
            GUILayout.Space(LargeGap);
            GUILayout.Label("RESULTS", theme.Heading);
            GUILayout.Label(runner.Results, theme.Label);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Raw data in:", theme.Small);
            GUILayout.Label(runner.Storage.OutputFolder, theme.Small);
        }
    }
}
