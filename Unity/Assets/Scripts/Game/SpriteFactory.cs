using UnityEngine;

namespace PhysicalDigital.Game
{
    public static class SpriteFactory
    {
        private const int Resolution = 128;
        private const float CornerRadius01 = 0.22f;

        private static Sprite roundedSquare;
        private static Sprite roundedPanel;
        private static Sprite softCircle;

        public static Sprite RoundedSquare
        {
            get
            {
                if (roundedSquare == null)
                {
                    roundedSquare = MakeRoundedSquare(CornerRadius01);
                }
                return roundedSquare;
            }
        }

        public static Sprite RoundedPanel
        {
            get
            {
                if (roundedPanel == null)
                {
                    Texture2D texture = RoundedSquare.texture;
                    float border = Mathf.Ceil(CornerRadius01 * Resolution);
                    roundedPanel = Sprite.Create(texture, new Rect(0, 0, Resolution, Resolution), new Vector2(0.5f, 0.5f),
                        Resolution, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
                }
                return roundedPanel;
            }
        }

        public static Sprite SoftCircle
        {
            get
            {
                if (softCircle == null)
                {
                    softCircle = MakeSoftCircle();
                }
                return softCircle;
            }
        }

        private static Sprite MakeRoundedSquare(float radius01)
        {
            Texture2D texture = NewTexture("RoundedSquare");
            float radius = radius01 * Resolution;
            float half = Resolution / 2f;
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            return Finish(texture);
        }

        private static Sprite MakeSoftCircle()
        {
            Texture2D texture = NewTexture("SoftCircle");
            float half = Resolution / 2f;
            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    float alpha = Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }
            return Finish(texture);
        }

        private static Texture2D NewTexture(string name)
        {
            return new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        private static Sprite Finish(Texture2D texture)
        {
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, Resolution, Resolution), new Vector2(0.5f, 0.5f), Resolution);
        }
    }
}
