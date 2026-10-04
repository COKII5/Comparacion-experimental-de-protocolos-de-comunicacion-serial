using UnityEngine;

namespace PhysicalDigital.UI
{
    public sealed class HudTheme
    {
        public const float VirtualHeight = 1080f;
        public const float PanelWidth = 440f;
        public const float Margin = 22f;

        public static readonly Color Accent = new Color(0.36f, 0.78f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.9f, 0.55f);
        public static readonly Color Warn = new Color(1f, 0.8f, 0.35f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.42f);
        public static readonly Color Muted = new Color(0.68f, 0.72f, 0.82f);

        private const float PanelPaddingX = 18f;
        private const float PanelPaddingY = 14f;
        private const float PanelBottomReserve = 60f;
        private const float MetricNameWidth = 250f;
        private const int LabelFontSize = 19;
        private const int SmallFontSize = 16;
        private const int MonoFontSize = 17;
        private const int HeadingFontSize = 15;
        private const int TitleFontSize = 22;
        private const int ButtonFontSize = 17;
        private const int BigTitleFontSize = 46;
        private const int SubtitleFontSize = 24;
        private const int MessageFontSize = 30;
        private const int PadLabelFontSize = 18;

        private static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.1f, 0.82f);
        private static readonly Color BarBackColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color DotOffColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color MonoColor = new Color(0.85f, 0.95f, 1f);

        private readonly Texture2D barBackTexture;

        public HudTheme()
        {
            barBackTexture = Solid(BarBackColor);
            BarFillTexture = Solid(Accent);
            BarWarnTexture = Solid(Warn);
            DotOnTexture = Solid(Color.white);
            DotOffTexture = Solid(DotOffColor);

            Panel = new GUIStyle(GUI.skin.box) { normal = { background = Solid(PanelColor) } };
            Label = new GUIStyle(GUI.skin.label) { fontSize = LabelFontSize, richText = true, wordWrap = true, normal = { textColor = Color.white } };
            Small = new GUIStyle(Label) { fontSize = SmallFontSize, normal = { textColor = Muted } };
            Mono = new GUIStyle(Label) { fontSize = MonoFontSize, normal = { textColor = MonoColor } };
            Heading = new GUIStyle(Label) { fontSize = HeadingFontSize, fontStyle = FontStyle.Bold, normal = { textColor = Accent } };
            Title = new GUIStyle(Label) { fontSize = TitleFontSize, fontStyle = FontStyle.Bold };
            Button = new GUIStyle(GUI.skin.button) { fontSize = ButtonFontSize };
            BigTitle = new GUIStyle(Label) { fontSize = BigTitleFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            Subtitle = new GUIStyle(Label) { fontSize = SubtitleFontSize, alignment = TextAnchor.MiddleCenter, normal = { textColor = Muted } };
            Message = new GUIStyle(Label) { fontSize = MessageFontSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            PadLabel = new GUIStyle(Label) { fontSize = PadLabelFontSize, alignment = TextAnchor.UpperCenter, normal = { textColor = Muted } };
            Footer = new GUIStyle(Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        public GUIStyle Panel { get; }
        public GUIStyle Label { get; }
        public GUIStyle Small { get; }
        public GUIStyle Mono { get; }
        public GUIStyle Heading { get; }
        public GUIStyle Title { get; }
        public GUIStyle Button { get; }
        public GUIStyle BigTitle { get; }
        public GUIStyle Subtitle { get; }
        public GUIStyle Message { get; }
        public GUIStyle PadLabel { get; }
        public GUIStyle Footer { get; }
        public Texture2D BarFillTexture { get; }
        public Texture2D BarWarnTexture { get; }
        public Texture2D DotOnTexture { get; }
        public Texture2D DotOffTexture { get; }

        public static Rect PanelArea(float left)
        {
            return new Rect(left, Margin, PanelWidth, VirtualHeight - 2f * Margin - PanelBottomReserve);
        }

        public void BeginPanel(Rect area)
        {
            GUI.Box(area, GUIContent.none, Panel);
            GUILayout.BeginArea(new Rect(area.x + PanelPaddingX, area.y + PanelPaddingY,
                area.width - 2f * PanelPaddingX, area.height - 2f * PanelPaddingY));
        }

        public void EndPanel()
        {
            GUILayout.EndArea();
        }

        public void DrawBar(Rect rect, float value01, Texture2D fill)
        {
            GUI.DrawTexture(rect, barBackTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value01), rect.height), fill);
        }

        public void ColoredLabel(string text, Color color)
        {
            Label.normal.textColor = color;
            GUILayout.Label(text, Label);
            Label.normal.textColor = Color.white;
        }

        public void Metric(string name, string value)
        {
            Metric(name, value, Color.white);
        }

        public void Metric(string name, string value, Color valueColor)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, Small, GUILayout.Width(MetricNameWidth));
            ColoredLabel(value, valueColor);
            GUILayout.EndHorizontal();
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
