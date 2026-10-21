using Bluff.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bluff.Game
{
    // A shared motion recipe drives the shop preview and the in-world cosmetic flourish.
    public sealed class PowerFlourish : MonoBehaviour
    {
        const int Count = 12;
        PowerType power;
        int variant;
        float elapsed;
        float duration;
        bool preview;
        Transform[] pieces;
        Graphic[] graphics;
        SpriteRenderer[] renderers;
        Transform emblem;
        Graphic emblemGraphic;
        SpriteRenderer emblemRenderer;

        public static Color Tint(int variant, PowerType power) => variant == 1 ? Theme.Hex("E1BA70") : variant == 2 ? Theme.Hex("8DDCC0") : CardArt.PowerColor(power);

        public static void Play(Transform parent, Vector3 position, PowerType power, int variant)
        {
            if (variant == 0 || !Cosmetics.ValidEffect(variant) || power == PowerType.None) return;
            var go = new GameObject("Power " + power + " " + Cosmetics.EffectNames[variant]);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            if (Camera.main != null) go.transform.rotation = Camera.main.transform.rotation;
            go.AddComponent<PowerFlourish>().Build(power, variant, false);
        }

        public static PowerFlourish Preview(RectTransform parent, PowerType power, int variant)
        {
            var rect = UIKit.CreateRect("Animation Preview", parent);
            rect.Stretch();
            var effect = rect.gameObject.AddComponent<PowerFlourish>();
            effect.Build(power, variant, true);
            return effect;
        }

        void Build(PowerType value, int style, bool isPreview)
        {
            power = value; variant = style; preview = isPreview;
            duration = preview ? 2f : 1.6f / GameSettings.Speed;
            pieces = new Transform[Count];
            graphics = new Graphic[Count];
            renderers = new SpriteRenderer[Count];
            for (int i = 0; i < Count; i++)
            {
                Sprite sprite = power == PowerType.Cermin ? SpriteFactory.ElementIcon(Element.Bintang) :
                    power == PowerType.Hadiah ? SpriteFactory.Cards : SpriteFactory.ElementIcon(Element.Petir);
                if (preview)
                {
                    var graphic = UIKit.CreateImage(transform, "Mote " + i, sprite, Tint(variant, power));
                    graphic.rectTransform.Place(UIKit.Center, Vector2.zero, Vector2.one * 18f);
                    pieces[i] = graphic.transform; graphics[i] = graphic;
                }
                else
                {
                    var go = new GameObject("Mote " + i, typeof(SpriteRenderer));
                    go.transform.SetParent(transform, false);
                    renderers[i] = go.GetComponent<SpriteRenderer>(); renderers[i].sprite = sprite;
                    pieces[i] = go.transform;
                }
            }
            if (preview)
            {
                emblemGraphic = UIKit.CreateImage(transform, "Power Seal", SpriteFactory.PowerIcon(power), Tint(variant, power));
                ((RectTransform)emblemGraphic.transform).Place(UIKit.Center, Vector2.zero, new Vector2(68f, 68f));
                emblem = emblemGraphic.transform;
            }
            else
            {
                var go = new GameObject("Power Seal", typeof(SpriteRenderer)); go.transform.SetParent(transform, false);
                emblemRenderer = go.GetComponent<SpriteRenderer>(); emblemRenderer.sprite = SpriteFactory.PowerIcon(power); emblem = go.transform;
            }
            Animate(0f);
        }

        void Update()
        {
            elapsed += preview ? Time.unscaledDeltaTime : Time.deltaTime;
            if (!preview && elapsed >= duration) { Destroy(gameObject); return; }
            Animate(preview ? Mathf.Min(1f, elapsed % (duration + .65f) / duration) : elapsed / duration);
        }

        void LateUpdate()
        {
            if (!preview && Camera.main != null) transform.rotation = Camera.main.transform.rotation;
        }

        void Animate(float t)
        {
            float fade = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1f - t) * 4f);
            float reach = variant == 1 ? Tween.EaseOutCubic(Mathf.Clamp01(t * 2f)) : Mathf.Sin(Mathf.PI * t * .8f);
            float scale = preview ? 82f : .23f;
            for (int i = 0; i < Count; i++)
            {
                float a = i * Mathf.PI * 2f / Count + (variant == 2 ? t * Mathf.PI * 2f : -.35f + t * .7f);
                float radius = reach;
                Vector2 p;
                switch (power)
                {
                    case PowerType.MataElang: p = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * .55f); break;
                    case PowerType.Tameng: p = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius); break;
                    case PowerType.Bakar: p = new Vector2(Mathf.Sin(a * 1.7f) * (1f-t) * .8f, (t * 2f + i % 3 * .18f - .8f)); break;
                    case PowerType.Badai: p = new Vector2(Mathf.Cos(a + t * 9f) * (.25f + i / 14f), (i / 11f - .5f) * 1.8f); break;
                    case PowerType.Cermin: p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * t * 1.35f; break;
                    default: p = new Vector2(Mathf.Sin(a) * radius, Mathf.Sin(a * 2f) * radius * .7f); break;
                }
                pieces[i].localPosition = p * scale;
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + t * 90f);
                float size = Mathf.Lerp(.5f, 1f, fade) * (variant == 1 ? .8f : 1f);
                pieces[i].localScale = Vector3.one * (preview ? size : size * .016f);
                var color = Tint(variant, power).WithAlpha(variant == 0 ? 0f : fade * .9f);
                if (preview) graphics[i].color = color; else renderers[i].color = color;
            }
            emblem.localScale = Vector3.one * (preview ? 1f : .095f) * Mathf.Lerp(.55f, 1f, reach);
            var ink = Tint(variant, power).WithAlpha(fade);
            if (preview) emblemGraphic.color = ink; else emblemRenderer.color = ink;
        }
    }
}
