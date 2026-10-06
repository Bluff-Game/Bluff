using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Tekstur kartu 3D yang digambar prosedural: muka elemen, Topeng, punggung kartu, dan Kartu Kuasa.
    /// Sudutnya transparan (dipotong shader Bluff/Lit) sehingga kartu terlihat membulat.
    /// </summary>
    public static class CardArt
    {
        public const int Width = 512;
        public const int Height = 716;
        internal const float DesignWidth = 256f;
        internal const float DesignHeight = 358f;
        internal const float CornerRadius = 22f;
        internal const float EdgeInset = 1f;

        delegate float Shape(Vector2 p);

        static readonly Color paperColor = Theme.Hex("FAF4E5");
        static readonly Dictionary<int, Texture2D> faces = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, Texture2D> powers = new Dictionary<int, Texture2D>();
        static Texture2D back;
        static Texture2D powerBack;

        public static Color PowerColor(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return Theme.Hex("2DD4BF");
                case PowerType.Tameng: return Theme.Hex("60A5FA");
                case PowerType.Bakar: return Theme.Hex("FB923C");
                case PowerType.Badai: return Theme.Hex("A5B4FC");
                case PowerType.Cermin: return Theme.Hex("E2E8F0");
                case PowerType.Hadiah: return Theme.Hex("F472B6");
                default: return Theme.Gold;
            }
        }

        /// <summary>Buat semua tekstur sekarang (dipanggil saat layar loading supaya meja tidak tersendat).</summary>
        public static void Prewarm()
        {
            foreach (var e in Elements.Claimable)
                Face(e);
            Face(Element.Topeng);
            var _ = Back;
            _ = PowerBack;
            foreach (var p in Powers.All)
                Power(p);
        }

        public static Texture2D Face(Element element)
        {
            if (faces.TryGetValue((int)element, out var tex) && tex != null)
                return tex;
            bool wild = element == Element.Topeng;
            Color frame = wild ? Theme.Gold : Color.Lerp(paperColor, Theme.Gold, 0.75f);
            Color ink = PrintedInk(Theme.ElementColor(element));
            tex = Compose(paperColor, frame, ink, p => SpriteFactory.ElementShape(element, p), false);
            faces[(int)element] = tex;
            return tex;
        }

        public static Texture2D Back => back != null ? back : (back = Compose(Theme.CardBack,
            Color.Lerp(Theme.CardBack, Theme.Gold, 0.6f), Color.Lerp(Theme.CardBackLine, Theme.Gold, 0.55f),
            p => SpriteFactory.ElementShape(Element.Topeng, p), true));

        public static Texture2D PowerBack => powerBack != null ? powerBack : (powerBack = Compose(Theme.CardBack,
            Color.Lerp(Theme.CardBack, Theme.Gold, 0.6f), Color.Lerp(Theme.CardBackLine, Theme.Gold, 0.55f), StarShape, true));

        public static Texture2D Power(PowerType power)
        {
            if (powers.TryGetValue((int)power, out var tex) && tex != null)
                return tex;
            tex = Compose(paperColor, Color.Lerp(paperColor, Theme.Gold, 0.75f), PrintedInk(PowerColor(power)),
                p => SpriteFactory.PowerShape(power, p), false);
            powers[(int)power] = tex;
            return tex;
        }

        static Color PrintedInk(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            var ink = Color.HSVToRGB(hue, saturation < 0.1f ? saturation : Mathf.Max(saturation, 0.75f), Mathf.Min(value, 0.68f)).linear;
            float luminance = 0.2126f * ink.r + 0.7152f * ink.g + 0.0722f * ink.b;
            float scale = Mathf.Min(1f, 0.13f / Mathf.Max(luminance, 0.001f));
            return new Color(ink.r * scale, ink.g * scale, ink.b * scale, 1f).gamma;
        }

        static float StarShape(Vector2 p)
        {
            float a = Mathf.Abs(p.x) * 2.6f + Mathf.Abs(p.y) * 0.9f;
            float b = Mathf.Abs(p.y) * 2.6f + Mathf.Abs(p.x) * 0.9f;
            return Mathf.Min(a, b) * 0.4f - 0.16f;
        }

        static Texture2D Compose(Color background, Color frame, Color ink, Shape icon, bool reverse)
        {
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            var px = new Color32[Width * Height];
            var half = new Vector2(DesignWidth, DesignHeight) * 0.5f;
            var halfInner = half - Vector2.one * (CornerRadius + EdgeInset);
            var cornerCenter = new Vector2(-half.x + 25f, half.y - 38f);
            float iconSize = reverse ? 96f : 156f;
            const float cornerSize = 50f;
            const float stripSize = 40f;
            var stripOffset = new Vector2(0f, 62f);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var p = new Vector2((x + 0.5f) * DesignWidth / Width - half.x,
                        (y + 0.5f) * DesignHeight / Height - half.y);
                    float d = RoundedBox(p, halfInner, CornerRadius);
                    Color c = background;
                    if (reverse)
                    {
                        float diamond = (Mathf.Abs(p.x) * 0.85f + Mathf.Abs(p.y) * 0.62f - 57f) / 1.052f;
                        float u = (p.x + p.y) / 24f;
                        float v = (p.x - p.y) / 24f;
                        float line = Mathf.Min(Mathf.Abs(u - Mathf.Round(u)), Mathf.Abs(v - Mathf.Round(v))) * 24f / 1.414214f;
                        float field = Coverage(d + 22f) * Coverage(6f - diamond);
                        c = Color.Lerp(c, Theme.CardBackLine, Coverage(line - 0.3f) * field * 0.28f);
                        c = Color.Lerp(c, Theme.CardBackLine, Coverage(Mathf.Abs(diamond) - 0.4f) * 0.65f);
                    }

                    var corner = (p.y >= 0f ? p : -p) - cornerCenter;
                    var strip = corner + stripOffset;
                    float frameAlpha = Coverage(Mathf.Abs(d + 11f) - 0.4f);
                    if (!reverse)
                    {
                        float cornerClearance = Mathf.Max(Mathf.Abs(corner.x), Mathf.Abs(corner.y)) - cornerSize * 0.55f - 2f;
                        float stripClearance = Mathf.Max(Mathf.Abs(strip.x), Mathf.Abs(strip.y)) - stripSize * 0.55f - 2f;
                        frameAlpha *= 1f - Coverage(Mathf.Min(cornerClearance, stripClearance));
                    }
                    c = Color.Lerp(c, frame, frameAlpha);
                    c = Color.Lerp(c, ink, Coverage(IconDistance(icon, p, iconSize)));
                    if (!reverse)
                    {
                        c = Color.Lerp(c, ink, Coverage(IconDistance(icon, corner, cornerSize)));
                        c = Color.Lerp(c, ink, Coverage(IconDistance(icon, strip, stripSize)));
                    }

                    float grain = (PaperNoise(x, y) - 0.5f) * 0.018f + (PaperNoise(x / 2, y / 5) - 0.5f) * 0.009f;
                    c.r *= 1f + grain;
                    c.g *= 1f + grain;
                    c.b *= 1f + grain;
                    c.a = Coverage(d);
                    px[y * Width + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        static float IconDistance(Shape icon, Vector2 p, float size)
        {
            if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) > size * 0.55f)
                return size;
            return icon(p / size) * size;
        }

        static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance * Width / DesignWidth);

        static float PaperNoise(int x, int y)
        {
            unchecked
            {
                uint value = (uint)x * 374761393u + (uint)y * 668265263u + 144269u;
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value ^ (value >> 16)) / (float)uint.MaxValue;
            }
        }

        static float RoundedBox(Vector2 p, Vector2 halfInner, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x) - halfInner.x, Mathf.Abs(p.y) - halfInner.y);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }
    }
}
