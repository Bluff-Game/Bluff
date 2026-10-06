using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Semua grafis game dibuat lewat kode (signed distance field + anti-aliasing), jadi proyek
    /// tidak butuh file gambar sama sekali. Sprite di-cache dan dibuat sekali saja.
    /// Koordinat bentuk: p dalam [-0.5, 0.5], sumbu y ke atas.
    /// </summary>
    public static class SpriteFactory
    {
        /// <summary>Radius sudut (pixel tekstur) dari sprite rounded-rect 9-slice.</summary>
        public const float RoundedRectRadius = 40f;

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        delegate float Sdf(Vector2 p);

        public static Sprite RoundedRect => Get("rounded", () => BuildRoundedRect(128, RoundedRectRadius, 0f));
        public static Sprite RoundedOutline => Get("rounded-outline", () => BuildRoundedRect(128, RoundedRectRadius, 10f));
        public static Sprite Circle => Get("circle", () => BuildShape(128, p => p.magnitude - 0.47f, false));
        public static Sprite Ring => Get("ring", () => BuildShape(256, p => Mathf.Abs(p.magnitude - 0.45f) - 0.03f, false));
        public static Sprite Glow => Get("glow", BuildGlow);
        public static Sprite CardBack => Get("card-back", BuildCardBack);
        public static Sprite Bullet => Get("bullet", () => BuildShape(96, BulletSdf, true));
        public static Sprite Crosshair => Get("crosshair", () => BuildShape(192, CrosshairSdf, true));
        public static Sprite Pause => Get("pause", () => BuildShape(96, PauseSdf, true));
        public static Sprite Back => Get("back-arrow", () => BuildShape(96, BackArrowSdf, true));

        public static Sprite ElementIcon(Element element) => Get("element-" + (int)element, () => BuildShape(256, p => ElementSdf(element, p), true));

        public static Sprite PowerIcon(PowerType power) => Get("power-" + (int)power, () => BuildShape(256, p => PowerSdf(power, p), true));

        public static Sprite SoftDot => Get("soft-dot", BuildGlow);

        /// <summary>Putih yang memudar dari kiri (pekat) ke kanan (transparan), untuk bayangan di belakang menu.</summary>
        public static Sprite HorizontalFade => Get("hfade", () =>
        {
            const int w = 256;
            var tex = NewTexture(w, 4, false);
            var px = new Color32[w * 4];
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                byte a = (byte)(Mathf.Clamp01(1f - Mathf.Pow(t, 1.6f)) * 255f);
                for (int y = 0; y < 4; y++)
                    px[y * w + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return MakeSprite(tex, Vector4.zero);
        });

        /// <summary>Bentuk ikon elemen sebagai jarak bertanda (p dalam [-0.5, 0.5]); dipakai juga untuk tekstur kartu 3D.</summary>
        public static float ElementShape(Element element, Vector2 p) => ElementSdf(element, p);

        /// <summary>Bentuk ikon Kartu Kuasa sebagai jarak bertanda (p dalam [-0.5, 0.5]).</summary>
        public static float PowerShape(PowerType power, Vector2 p) => PowerSdf(power, p);

        static Sprite Get(string key, System.Func<Sprite> build)
        {
            if (!cache.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = build();
                cache[key] = sprite;
            }
            return sprite;
        }

        // ------------------------------------------------------------------ builders

        static Texture2D NewTexture(int width, int height, bool mipmaps)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            return tex;
        }

        static Sprite MakeSprite(Texture2D tex, Vector4 border)
        {
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return sprite;
        }

        static Sprite BuildShape(int size, Sdf sdf, bool mipmaps)
        {
            var tex = NewTexture(size, size, mipmaps);
            var pixels = new Color32[size * size];
            float inv = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) * inv - 0.5f, (y + 0.5f) * inv - 0.5f);
                    float d = sdf(p) * size;
                    byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(mipmaps, true);
            return MakeSprite(tex, Vector4.zero);
        }

        static Sprite BuildRoundedRect(int size, float radius, float outline)
        {
            var tex = NewTexture(size, size, false);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float inner = half - radius - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    float d = RoundedBox(p, new Vector2(inner, inner), radius);
                    if (outline > 0f)
                        d = Mathf.Abs(d + outline * 0.5f) - outline * 0.5f;
                    byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            float border = radius + 2f;
            return MakeSprite(tex, new Vector4(border, border, border, border));
        }

        static Sprite BuildGlow()
        {
            const int size = 128;
            var tex = NewTexture(size, size, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float t = Mathf.Clamp01(1f - p.magnitude / 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(t * t * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return MakeSprite(tex, Vector4.zero);
        }

        /// <summary>Punggung kartu: ungu tua dengan kisi berlian dan bingkai emas tipis.</summary>
        static Sprite BuildCardBack()
        {
            const int w = 200, h = 280;
            const float radius = 18f;
            var tex = NewTexture(w, h, true);
            var pixels = new Color32[w * h];
            Color baseColor = Theme.CardBack;
            Color lineColor = Theme.CardBackLine;
            Color frameColor = Theme.Gold;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                    float d = RoundedBox(p, new Vector2(w * 0.5f - radius - 1f, h * 0.5f - radius - 1f), radius);
                    float alpha = Mathf.Clamp01(0.5f - d);

                    Color c = baseColor;
                    if (d < -14f)
                    {
                        float u = (p.x + p.y) / 22f;
                        float v = (p.x - p.y) / 22f;
                        float line = Mathf.Min(Mathf.Abs(u - Mathf.Round(u)), Mathf.Abs(v - Mathf.Round(v))) * 22f / 1.414f;
                        float la = Mathf.Clamp01(1.5f - line);
                        c = Color.Lerp(c, lineColor, la * 0.8f);
                    }
                    float frame = Mathf.Abs(d + 9f) - 1.6f;
                    c = Color.Lerp(c, frameColor, Mathf.Clamp01(0.5f - frame) * 0.85f);
                    c.a = alpha;
                    pixels[y * w + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return MakeSprite(tex, Vector4.zero);
        }

        // ------------------------------------------------------------------ shapes

        static float ElementSdf(Element element, Vector2 p)
        {
            switch (element)
            {
                case Element.Matahari: return SunSdf(p);
                case Element.Bulan: return MoonSdf(p);
                case Element.Bintang: return SdPolygon(p, StarPoly) - 0.012f;
                case Element.Petir: return SdPolygon(p, BoltPoly) - 0.008f;
                case Element.Air: return DropSdf(p);
                case Element.Daun: return LeafSdf(p);
                default: return MaskSdf(p);
            }
        }

        // ------------------------------------------------------------------ ikon kartu kuasa

        static float PowerSdf(PowerType power, Vector2 p)
        {
            switch (power)
            {
                case PowerType.MataElang: return EyeSdf(p);
                case PowerType.Tameng: return ShieldSdf(p);
                case PowerType.Bakar: return FlameSdf(p);
                case PowerType.Badai: return TornadoSdf(p);
                case PowerType.Cermin: return MirrorSdf(p);
                case PowerType.Hadiah: return GiftSdf(p);
                default: return p.magnitude - 0.3f;
            }
        }

        static float EyeSdf(Vector2 p)
        {
            float eye = Mathf.Max((p - new Vector2(0f, -0.25f)).magnitude - 0.45f, (p - new Vector2(0f, 0.25f)).magnitude - 0.45f);
            float r = p.magnitude;
            float gap = Mathf.Abs(r - 0.165f) - 0.025f;
            float d = Mathf.Max(eye, -gap);
            float shine = (p - new Vector2(0.05f, 0.05f)).magnitude - 0.04f;
            return Mathf.Max(d, -shine);
        }

        static readonly Vector2[] ShieldPoly =
        {
            new Vector2(-0.36f, 0.4f), new Vector2(0.36f, 0.4f), new Vector2(0.36f, 0.06f), new Vector2(0f, -0.46f), new Vector2(-0.36f, 0.06f),
        };

        static readonly Vector2[] ShieldInner =
        {
            new Vector2(-0.24f, 0.29f), new Vector2(0.24f, 0.29f), new Vector2(0.24f, 0.04f), new Vector2(0f, -0.3f), new Vector2(-0.24f, 0.04f),
        };

        static float ShieldSdf(Vector2 p)
        {
            float outer = SdPolygon(p, ShieldPoly) - 0.02f;
            float line = Mathf.Abs(SdPolygon(p, ShieldInner)) - 0.022f;
            return Mathf.Max(outer, -line);
        }

        static readonly Vector2[] FlameOuter = { new Vector2(0.06f, 0.48f), new Vector2(-0.27f, -0.06f), new Vector2(0.29f, -0.07f) };
        static readonly Vector2[] FlameInner = { new Vector2(0.04f, 0.14f), new Vector2(-0.13f, -0.15f), new Vector2(0.17f, -0.16f) };

        static float FlameSdf(Vector2 p)
        {
            float outer = Mathf.Min((p - new Vector2(0.01f, -0.16f)).magnitude - 0.3f, SdPolygon(p, FlameOuter));
            float inner = Mathf.Min((p - new Vector2(0.02f, -0.21f)).magnitude - 0.14f, SdPolygon(p, FlameInner));
            return Mathf.Max(outer, -inner);
        }

        static float TornadoSdf(Vector2 p)
        {
            float d = float.MaxValue;
            float[] ys = { 0.34f, 0.18f, 0.03f, -0.12f, -0.26f, -0.38f };
            float[] half = { 0.4f, 0.32f, 0.24f, 0.17f, 0.11f, 0.06f };
            float[] shift = { -0.02f, 0.04f, -0.03f, 0.04f, -0.02f, 0.03f };
            for (int i = 0; i < ys.Length; i++)
            {
                var a = new Vector2(shift[i] - half[i], ys[i]);
                var b = new Vector2(shift[i] + half[i], ys[i]);
                d = Mathf.Min(d, SdSegment(p, a, b) - 0.05f);
            }
            return d;
        }

        static float MirrorSdf(Vector2 p)
        {
            float frame = Mathf.Abs(SdEllipse(p - new Vector2(0f, 0.1f), new Vector2(0.28f, 0.34f))) - 0.045f;
            float handle = SdSegment(p, new Vector2(0f, -0.24f), new Vector2(0f, -0.47f)) - 0.05f;
            float shine1 = SdSegment(p, new Vector2(-0.12f, 0.06f), new Vector2(0.02f, 0.26f)) - 0.025f;
            float shine2 = SdSegment(p, new Vector2(-0.04f, -0.04f), new Vector2(0.08f, 0.13f)) - 0.02f;
            return Mathf.Min(Mathf.Min(frame, handle), Mathf.Min(shine1, shine2));
        }

        static float GiftSdf(Vector2 p)
        {
            float body = SdBox(p - new Vector2(0f, -0.15f), new Vector2(0.32f, 0.23f));
            float lid = SdBox(p - new Vector2(0f, 0.15f), new Vector2(0.37f, 0.07f));
            float box = Mathf.Min(body, lid);
            box = Mathf.Max(box, -(Mathf.Abs(p.y - 0.07f) - 0.012f));
            box = Mathf.Max(box, -(Mathf.Abs(p.x) - 0.035f));
            float bowL = Mathf.Abs((p - new Vector2(-0.12f, 0.32f)).magnitude - 0.09f) - 0.03f;
            float bowR = Mathf.Abs((p - new Vector2(0.12f, 0.32f)).magnitude - 0.09f) - 0.03f;
            return Mathf.Min(box, Mathf.Min(bowL, bowR));
        }

        static float SunSdf(Vector2 p)
        {
            float d = p.magnitude - 0.2f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                d = Mathf.Min(d, SdSegment(p, dir * 0.29f, dir * 0.43f) - 0.038f);
            }
            return d;
        }

        static float MoonSdf(Vector2 p)
        {
            float outer = (p - new Vector2(-0.03f, 0f)).magnitude - 0.4f;
            float inner = (p - new Vector2(0.15f, 0.09f)).magnitude - 0.33f;
            return Mathf.Max(outer, -inner);
        }

        static readonly Vector2[] StarPoly = BuildStar(0.47f, 0.2f);

        static Vector2[] BuildStar(float outer, float inner)
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                v[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.03f);
            }
            return v;
        }

        static readonly Vector2[] BoltPoly =
        {
            new Vector2(0.08f, 0.46f),
            new Vector2(0.30f, 0.46f),
            new Vector2(0.10f, 0.08f),
            new Vector2(0.26f, 0.08f),
            new Vector2(-0.14f, -0.48f),
            new Vector2(-0.02f, -0.04f),
            new Vector2(-0.20f, -0.04f),
        };

        static readonly Vector2[] DropTri =
        {
            new Vector2(0f, 0.47f),
            new Vector2(-0.257f, 0.055f),
            new Vector2(0.257f, 0.055f),
        };

        static float DropSdf(Vector2 p)
        {
            float circle = (p - new Vector2(0f, -0.1f)).magnitude - 0.3f;
            float d = Mathf.Min(circle, SdPolygon(p, DropTri));
            float shine = (p - new Vector2(-0.11f, -0.13f)).magnitude - 0.065f;
            return Mathf.Max(d, -shine);
        }

        static float LeafSdf(Vector2 p)
        {
            var q = Rotate(p, -0.6f);
            float a = (q - new Vector2(-0.2f, 0f)).magnitude - 0.42f;
            float b = (q - new Vector2(0.2f, 0f)).magnitude - 0.42f;
            float leaf = Mathf.Max(a, b);
            float vein = Mathf.Max(Mathf.Abs(q.x) - 0.018f, q.y - 0.24f);
            leaf = Mathf.Max(leaf, -vein);
            float stem = SdSegment(q, new Vector2(0f, -0.3f), new Vector2(0f, -0.47f)) - 0.025f;
            return Mathf.Min(leaf, stem);
        }

        static readonly Vector2[] MaskNotch =
        {
            new Vector2(0f, -0.08f),
            new Vector2(0.1f, -0.3f),
            new Vector2(-0.1f, -0.3f),
        };

        static float MaskSdf(Vector2 p)
        {
            float face = SdEllipse(p - new Vector2(0f, 0.03f), new Vector2(0.45f, 0.26f));
            float d = Mathf.Max(face, -SdPolygon(p, MaskNotch));
            float eyeL = SdEllipse(Rotate(p - new Vector2(-0.17f, 0.06f), 0.25f), new Vector2(0.11f, 0.06f));
            float eyeR = SdEllipse(Rotate(p - new Vector2(0.17f, 0.06f), -0.25f), new Vector2(0.11f, 0.06f));
            return Mathf.Max(d, -Mathf.Min(eyeL, eyeR));
        }

        static float BulletSdf(Vector2 p)
        {
            float body = SdBox(p - new Vector2(0f, -0.2f), new Vector2(0.15f, 0.22f));
            float tip = Mathf.Max(SdEllipse(p - new Vector2(0f, 0.03f), new Vector2(0.15f, 0.42f)), 0.03f - p.y);
            float d = Mathf.Min(body, tip);
            float groove = Mathf.Abs(p.y + 0.02f) - 0.018f;
            return Mathf.Max(d, -groove);
        }

        static float CrosshairSdf(Vector2 p)
        {
            float ring = Mathf.Abs(p.magnitude - 0.3f) - 0.035f;
            float lines = Mathf.Min(
                Mathf.Min(SdSegment(p, new Vector2(0f, 0.14f), new Vector2(0f, 0.46f)), SdSegment(p, new Vector2(0f, -0.14f), new Vector2(0f, -0.46f))),
                Mathf.Min(SdSegment(p, new Vector2(0.14f, 0f), new Vector2(0.46f, 0f)), SdSegment(p, new Vector2(-0.14f, 0f), new Vector2(-0.46f, 0f)))) - 0.03f;
            float dot = p.magnitude - 0.045f;
            return Mathf.Min(ring, Mathf.Min(lines, dot));
        }

        static float PauseSdf(Vector2 p)
        {
            float left = SdBox(p - new Vector2(-0.13f, 0f), new Vector2(0.075f, 0.3f)) - 0.02f;
            float right = SdBox(p - new Vector2(0.13f, 0f), new Vector2(0.075f, 0.3f)) - 0.02f;
            return Mathf.Min(left, right);
        }

        static float BackArrowSdf(Vector2 p)
        {
            float shaft = SdSegment(p, new Vector2(-0.28f, 0f), new Vector2(0.3f, 0f)) - 0.05f;
            float upper = SdSegment(p, new Vector2(-0.28f, 0f), new Vector2(-0.05f, 0.23f)) - 0.05f;
            float lower = SdSegment(p, new Vector2(-0.28f, 0f), new Vector2(-0.05f, -0.23f)) - 0.05f;
            return Mathf.Min(shaft, Mathf.Min(upper, lower));
        }

        // ------------------------------------------------------------------ sdf helpers

        static float RoundedBox(Vector2 p, Vector2 halfInner, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x) - halfInner.x, Mathf.Abs(p.y) - halfInner.y);
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        static float SdBox(Vector2 p, Vector2 half) => RoundedBox(p, half, 0f);

        static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }

        static float SdEllipse(Vector2 p, Vector2 r)
        {
            float k0 = new Vector2(p.x / r.x, p.y / r.y).magnitude;
            float k1 = new Vector2(p.x / (r.x * r.x), p.y / (r.y * r.y)).magnitude;
            return k1 < 1e-6f ? -Mathf.Min(r.x, r.y) : k0 * (k0 - 1f) / k1;
        }

        /// <summary>Jarak bertanda ke poligon sederhana (Inigo Quilez).</summary>
        static float SdPolygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y;
                bool c2 = p.y < v[j].y;
                bool c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                    s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        static Vector2 Rotate(Vector2 p, float angle)
        {
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }
    }
}
