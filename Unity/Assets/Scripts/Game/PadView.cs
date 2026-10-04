using UnityEngine;

namespace PhysicalDigital.Game
{
    public sealed class PadView : MonoBehaviour
    {
        private const float GlowScale = 1.9f;
        private const float FadeSpeed = 7f;
        private const float LitScaleBoost = 0.07f;
        private const float GlowMaxAlpha = 0.6f;
        private const float ErrorBlinkSpeed = 8f;
        private const float DimAmount = 0.72f;
        private const float LitAmount = 0.22f;
        private const float ErrorBlendMax = 0.75f;

        private static readonly Color ErrorColor = new Color(1f, 0.18f, 0.2f);
        private static readonly Color DimTarget = new Color(0.06f, 0.07f, 0.11f);

        private SpriteRenderer body;
        private SpriteRenderer glow;
        private Color baseColor;
        private float size;
        private float flashTimer;
        private float errorTimer;
        private bool held;
        private float idle;
        private float intensity;

        public static PadView Create(Transform parent, string name, Vector3 position, float size, Color color)
        {
            GameObject padObject = new GameObject(name);
            padObject.transform.SetParent(parent, false);
            padObject.transform.localPosition = position;

            GameObject glowObject = new GameObject("Glow");
            glowObject.transform.SetParent(padObject.transform, false);
            glowObject.transform.localScale = Vector3.one * GlowScale;

            PadView pad = padObject.AddComponent<PadView>();
            pad.baseColor = color;
            pad.size = size;
            pad.glow = glowObject.AddComponent<SpriteRenderer>();
            pad.glow.sprite = SpriteFactory.SoftCircle;
            pad.glow.sortingOrder = 1;
            pad.body = padObject.AddComponent<SpriteRenderer>();
            pad.body.sprite = SpriteFactory.RoundedSquare;
            pad.body.sortingOrder = 2;
            pad.Apply();
            return pad;
        }

        public void Flash(float seconds)
        {
            flashTimer = Mathf.Max(flashTimer, seconds);
        }

        public void ErrorFlash(float seconds)
        {
            errorTimer = Mathf.Max(errorTimer, seconds);
        }

        public void SetHeld(bool value)
        {
            held = value;
        }

        public void SetIdle(float value)
        {
            idle = value;
        }

        private void Update()
        {
            flashTimer -= Time.deltaTime;
            errorTimer -= Time.deltaTime;
            float target = held || flashTimer > 0f ? 1f : idle;
            intensity = target >= intensity ? target : Mathf.MoveTowards(intensity, target, Time.deltaTime * FadeSpeed);
            Apply();
        }

        private void Apply()
        {
            Color dim = Color.Lerp(baseColor, DimTarget, DimAmount);
            Color lit = Color.Lerp(baseColor, Color.white, LitAmount);
            Color color = Color.Lerp(dim, lit, intensity);
            if (errorTimer > 0f)
            {
                color = Color.Lerp(color, ErrorColor, ErrorBlendMax * Mathf.PingPong(Time.time * ErrorBlinkSpeed, 1f));
            }
            body.color = color;
            glow.color = new Color(baseColor.r, baseColor.g, baseColor.b, GlowMaxAlpha * intensity);
            transform.localScale = Vector3.one * size * (1f + LitScaleBoost * intensity);
        }
    }
}
