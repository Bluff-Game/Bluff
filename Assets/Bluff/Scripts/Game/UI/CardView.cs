using System.Collections;
using Bluff.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>
    /// Tampilan satu kartu (muka atau punggung). Di tangan pemain, kartu "mengejar" <see cref="TargetPosition"/>
    /// dengan lerp halus, jadi menyusun ulang tangan cukup dengan mengganti target.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float BaseWidth = 132f;
        public const float BaseHeight = 185f;
        public static readonly Vector2 BaseSize = new Vector2(BaseWidth, BaseHeight);

        RectTransform rect;
        GameObject face;
        GameObject back;
        Image frame;
        Image inner;
        Image icon;
        Image cornerIcon;
        Text nameLabel;
        Image glow;
        Color glowColor = Theme.Gold;
        float highlightTimer;
        bool hovered;

        public RectTransform Rect => rect;
        public Card Card { get; private set; }
        public bool HasCard { get; private set; }
        public bool FaceUp { get; private set; }
        public bool Selected { get; set; }
        public bool Interactable { get; set; }
        public bool FollowTarget { get; set; }
        public Vector2 TargetPosition { get; set; }

        public System.Action<CardView> Clicked;

        public static CardView Create(Transform parent, Vector2 size)
        {
            var rt = UIKit.CreateRect("Card", parent);
            rt.sizeDelta = size;
            var view = rt.gameObject.AddComponent<CardView>();
            view.Build(size);
            return view;
        }

        void Build(Vector2 size)
        {
            rect = (RectTransform)transform;
            float radius = size.x * 0.1f;
            float border = Mathf.Max(3f, size.x * 0.04f);

            glow = UIKit.CreatePanel(rect, "Glow", Theme.Gold, radius + 6f);
            glow.rectTransform.Stretch(-7f, -7f, -7f, -7f);
            glow.enabled = false;

            frame = UIKit.CreatePanel(rect, "Face", Color.white, radius, true);
            frame.rectTransform.Stretch();
            face = frame.gameObject;

            inner = UIKit.CreatePanel(frame.transform, "Inner", Theme.CardFace, Mathf.Max(2f, radius - border * 0.6f));
            inner.rectTransform.Stretch(border, border, border, border);

            icon = UIKit.CreateImage(inner.transform, "Icon", null, Color.white);
            icon.preserveAspect = true;
            icon.rectTransform.Anchor(new Vector2(0.16f, 0.28f), new Vector2(0.84f, 0.8f));

            cornerIcon = UIKit.CreateImage(inner.transform, "Corner", null, Color.white);
            cornerIcon.preserveAspect = true;
            cornerIcon.rectTransform.Anchor(new Vector2(0.05f, 0.81f), new Vector2(0.3f, 0.97f));

            nameLabel = UIKit.CreateText(inner.transform, "Name", "", Mathf.RoundToInt(size.y * 0.085f), Theme.TextDark, TextAnchor.MiddleCenter, FontStyle.Bold);
            nameLabel.rectTransform.Anchor(new Vector2(0f, 0.04f), new Vector2(1f, 0.25f));
            nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            var backImage = UIKit.CreateImage(rect, "Back", SpriteFactory.CardBack, Color.white, true);
            backImage.rectTransform.Stretch();
            back = backImage.gameObject;
            var emblem = UIKit.CreateImage(backImage.transform, "Emblem", SpriteFactory.ElementIcon(Element.Topeng), Theme.Gold.WithAlpha(0.9f));
            emblem.preserveAspect = true;
            emblem.rectTransform.Anchor(new Vector2(0.22f, 0.36f), new Vector2(0.78f, 0.64f));

            SetFaceUp(false);
        }

        public void SetCard(Card card)
        {
            Card = card;
            HasCard = true;
            var sprite = SpriteFactory.ElementIcon(card.Element);
            icon.sprite = sprite;
            cornerIcon.sprite = sprite;
            if (card.IsWild)
            {
                frame.color = Theme.Gold;
                inner.color = Theme.MaskCard;
                icon.color = Theme.Gold;
                cornerIcon.color = Theme.Gold;
                nameLabel.color = Theme.Gold;
                nameLabel.text = "MASK";
            }
            else
            {
                var color = Theme.ElementColor(card.Element);
                frame.color = color;
                inner.color = Theme.CardFace;
                icon.color = color;
                cornerIcon.color = color;
                nameLabel.color = Color.Lerp(color, Color.black, 0.25f);
                nameLabel.text = card.Element.ToString().ToUpperInvariant();
            }
        }

        public void SetFaceUp(bool faceUp)
        {
            FaceUp = faceUp;
            face.SetActive(faceUp);
            back.SetActive(!faceUp);
        }

        /// <summary>Sorot kartu sebentar (mis. kartu baru masuk ke tangan).</summary>
        public void Highlight(Color color, float seconds)
        {
            glowColor = color;
            highlightTimer = seconds;
        }

        /// <summary>Sorot permanen (dipakai di layar pembukaan kartu). Warna alpha 0 = matikan.</summary>
        public void SetGlow(Color color)
        {
            glowColor = color;
            highlightTimer = color.a > 0f ? float.MaxValue : 0f;
            glow.enabled = color.a > 0f;
            glow.color = color;
        }

        public IEnumerator Flip(bool toFaceUp, float duration)
        {
            float half = duration * 0.5f;
            Vector3 baseScale = rect.localScale;
            yield return Tween.Run(half, t =>
            {
                if (rect != null)
                    rect.localScale = new Vector3(baseScale.x * (1f - t), baseScale.y, 1f);
            });
            if (rect == null)
                yield break;
            SetFaceUp(toFaceUp);
            yield return Tween.Run(half, t =>
            {
                if (rect != null)
                    rect.localScale = new Vector3(baseScale.x * t, baseScale.y, 1f);
            });
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable)
                Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        void Update()
        {
            if (highlightTimer > 0f && highlightTimer < float.MaxValue)
                highlightTimer -= Time.unscaledDeltaTime;

            bool showGlow = Selected || highlightTimer > 0f;
            if (glow.enabled != showGlow)
                glow.enabled = showGlow;
            if (showGlow)
            {
                var c = Selected ? Theme.Gold : glowColor;
                if (!Selected && highlightTimer < 1f)
                    c.a *= Mathf.Clamp01(highlightTimer);
                glow.color = c;
            }

            if (!FollowTarget)
                return;
            Vector2 target = TargetPosition;
            if (Selected)
                target.y += 34f;
            else if (hovered && Interactable)
                target.y += 14f;
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
        }
    }
}
