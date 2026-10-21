using UnityEngine;
using Bluff.Core;

namespace Bluff.Game
{
    public enum GameIcon { Duel, Online, Guide, Settings, Exit, Profile, Shop, Costume, Effects, Coin, Pause, Play, Home, Close, Restart, Laugh, Applaud, Cards, Challenge, Check, Clock, Back, Sun, Moon, Star, Lightning, Water, Leaf, Mask, MataElang, Tameng, Bakar, Badai, Cermin, Hadiah, Lock }

    public static partial class SpriteFactory
    {
        public static Sprite Emblem(GameIcon icon) => Get("emblem-" + icon, () =>
        {
            var texture = Resources.Load<Texture2D>("Icons/" + icon) ?? GenerateEmblem(icon);
            return MakeSprite(texture, Vector4.zero);
        });

        public static Sprite PowerEmblem(PowerType power) => Emblem((GameIcon)((int)GameIcon.MataElang + Mathf.Clamp((int)power - 1, 0, 5)));

        public static Sprite ElementEmblem(Element element) => Emblem((GameIcon)((int)GameIcon.Sun + (int)element));

        // Original minted brass badges. The same distance-field glyphs remain readable at HUD size.
        public static Texture2D GenerateEmblem(GameIcon icon)
        {
            const int size = 256;
            var texture = NewTexture(size, size, true);
            texture.name = "Bluf " + icon;
            var pixels = new Color[size * size];
            Color dark = Theme.Hex("394A3C"), brass = Theme.Hex("B69455"), light = Theme.Hex("F5DE9A");
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size - .5f, (y + .5f) / size - .5f);
                    float r = p.magnitude;
                    float a = Mathf.Atan2(p.y, p.x);
                    float edge = .463f + .004f * Mathf.Cos(a * 12f);
                    float alpha = Mathf.Clamp01((edge - r) * size + .5f);
                    float metal = .55f + p.y * .55f - p.x * .25f + .12f * Mathf.Sin((p.x + p.y) * 14f);
                    var c = Color.Lerp(dark * .55f, dark, Mathf.Clamp01(.6f + p.y));
                    if (r > .402f) c = Color.Lerp(brass * .42f, light, Mathf.Clamp01(metal + Mathf.Sin(r * 620f) * .16f));
                    if (Mathf.Abs(r - .378f) < .0035f) c = brass * .8f;
                    if (r > .414f && r < .444f && Mathf.Cos(a * 36f) > .78f) c = brass * .55f;
                    var q = p / .64f;
                    float shape = EmblemShape(icon, q) * .64f;
                    float ink = Mathf.Clamp01(.5f - shape * size);
                    float bevel = Mathf.Clamp01((EmblemShape(icon, q + new Vector2(-.006f, .009f)) * .64f - shape) * 80f + .5f);
                    var gold = Color.Lerp(brass * .75f, light, Mathf.Clamp01(metal * .55f + bevel * .45f));
                    c = Color.Lerp(c, gold, ink);
                    // Four small diamonds connect every badge to the engraved card-back motif.
                    var diamond = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    float corner = Mathf.Min(Mathf.Abs(diamond.x - .34f) + diamond.y, diamond.x + Mathf.Abs(diamond.y - .34f));
                    if (corner < .016f) c = light;
                    c.a = alpha;
                    pixels[y * size + x] = c;
                }
            texture.SetPixels(pixels);
            texture.Apply(true, false);
            return texture;
        }

        static float EmblemShape(GameIcon icon, Vector2 p)
        {
            if (icon >= GameIcon.MataElang && icon <= GameIcon.Hadiah) return PowerSdf((PowerType)((int)icon - (int)GameIcon.MataElang + 1), p);
            if (icon >= GameIcon.Sun && icon <= GameIcon.Mask) return ElementSdf((Element)((int)icon - (int)GameIcon.Sun), p);
            switch (icon)
            {
                case GameIcon.Lock:
                    float shackle = Mathf.Abs((p-new Vector2(0f,.13f)).magnitude-.17f)-.035f;
                    return Mathf.Min(Mathf.Max(shackle,.08f-p.y),SdBox(p-new Vector2(0f,-.13f),new Vector2(.25f,.19f)));
                case GameIcon.Cards: return CardsSdf(p);
                case GameIcon.Challenge: return CrosshairSdf(p);
                case GameIcon.Clock: return ClockSdf(p);
                case GameIcon.Back: return BackArrowSdf(p);
                case GameIcon.Check: return Mathf.Min(SdSegment(p,new Vector2(-.28f,0f),new Vector2(-.08f,-.2f)),SdSegment(p,new Vector2(-.08f,-.2f),new Vector2(.29f,.24f)))-.035f;
                case GameIcon.Duel: return Mathf.Min(CardsSdf(p), SdPolygon((p - new Vector2(.08f, 0f)) * 2.5f, PlayPoly) / 2.5f);
                case GameIcon.Online:
                    return Mathf.Min(Mathf.Max(Mathf.Abs(p.magnitude - .32f) - .022f, -.22f - p.y),
                        Mathf.Min(CardsSdf(p * 1.8f) / 1.8f, Mathf.Min((p - new Vector2(-.31f, .06f)).magnitude, (p - new Vector2(.31f, .06f)).magnitude) - .055f));
                case GameIcon.Guide: return GuideSdf(p);
                case GameIcon.Pause: return PauseSdf(p);
                case GameIcon.Play: return SdPolygon(p, PlayPoly);
                case GameIcon.Home: return HomeSdf(p);
                case GameIcon.Restart: return RestartSdf(p);
                case GameIcon.Laugh:
                    float eyes = Mathf.Min((p-new Vector2(-.12f,.12f)).magnitude,(p-new Vector2(.12f,.12f)).magnitude)-.035f;
                    float smile = Mathf.Max(Mathf.Abs((p-new Vector2(0f,-.015f)).magnitude-.20f)-.026f,p.y+.02f);
                    return Mathf.Min(Mathf.Abs(p.magnitude-.34f)-.025f,Mathf.Min(eyes,smile));
                case GameIcon.Applaud:
                    return Mathf.Min(CardsSdf(p),SunSdf((p-new Vector2(.17f,.23f))*3.6f)/3.6f);
                case GameIcon.Close: return Mathf.Min(SdSegment(p, new Vector2(-.27f,-.27f), new Vector2(.27f,.27f)), SdSegment(p,new Vector2(-.27f,.27f),new Vector2(.27f,-.27f))) - .035f;
                case GameIcon.Profile:
                    return Mathf.Min(MaskSdf((p + new Vector2(0f,.15f)) * 1.2f) / 1.2f,
                        Mathf.Min(SdBox(p - new Vector2(0f,.23f), new Vector2(.21f,.13f)), SdBox(p - new Vector2(0f,.09f),new Vector2(.34f,.032f))));
                case GameIcon.Coin:
                    return Mathf.Min(Mathf.Abs(p.magnitude - .35f) - .026f, Mathf.Abs(Mathf.Abs(p.x) + Mathf.Abs(p.y) - .18f) - .024f);
                case GameIcon.Effects:
                    return Mathf.Min(SdSegment(p,new Vector2(-.28f,-.32f),new Vector2(.16f,.2f)) - .032f,
                        Mathf.Min(SunSdf((p - new Vector2(.17f,.22f)) * 2.7f) / 2.7f, (p - new Vector2(-.25f,.22f)).magnitude - .045f));
                case GameIcon.Settings:
                    return Mathf.Min(Mathf.Abs(p.magnitude - (.27f + .04f * Mathf.Cos(Mathf.Atan2(p.y,p.x) * 8f))) - .035f,
                        Mathf.Abs(p.magnitude - .1f) - .024f);
                case GameIcon.Exit:
                    return Mathf.Min(Mathf.Abs(RoundedBox(p - new Vector2(-.1f,0f), new Vector2(.21f,.33f),.02f)) - .026f, BackArrowSdf(-p - new Vector2(-.18f,0f)) * .75f);
                case GameIcon.Shop:
                    return Mathf.Min(Mathf.Abs(RoundedBox(p - new Vector2(0f,-.12f),new Vector2(.31f,.19f),.02f)) - .027f,
                        Mathf.Min(SdBox(p - new Vector2(0f,.19f),new Vector2(.36f,.09f)), Mathf.Abs((p - new Vector2(0f,-.13f)).magnitude - .1f) - .018f));
                default:
                    float coat = Mathf.Abs(SdPolygon(p, CoatPoly)) - .026f;
                    float lapel = Mathf.Min(SdSegment(p,new Vector2(-.13f,.32f),new Vector2(0f,-.05f)),SdSegment(p,new Vector2(.13f,.32f),new Vector2(0f,-.05f))) - .021f;
                    return Mathf.Min(coat, Mathf.Min(lapel, (p - new Vector2(0f,-.22f)).magnitude - .027f));
            }
        }

        static readonly Vector2[] CoatPoly = { new Vector2(-.13f,.32f),new Vector2(-.34f,.18f),new Vector2(-.43f,-.08f),new Vector2(-.25f,-.13f),new Vector2(-.23f,-.36f),new Vector2(.23f,-.36f),new Vector2(.25f,-.13f),new Vector2(.43f,-.08f),new Vector2(.34f,.18f),new Vector2(.13f,.32f),new Vector2(0f,.2f) };
    }
}
