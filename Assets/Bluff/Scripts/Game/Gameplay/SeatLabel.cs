using Bluff.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bluff.Game
{
    public sealed class SeatLabel : MonoBehaviour
    {
        RectTransform root;
        Text nameText;
        Text turnText;
        Image turnIndicator;
        Text countText;
        Text bulletsText;
        Text powerText;
        Image powerIcon;
        RectTransform bubble;
        CanvasGroup bubbleGroup;
        Text bubbleText;
        Button targetButton;
        bool active;
        float bubbleTimer;

        public int PlayerId { get; private set; }
        public RectTransform Root => root;

        public static SeatLabel Create(RectTransform parent, int playerId, string playerName, int maxBullets, System.Action<int> onTarget)
        {
            var rt = UIKit.CreateRect("Label " + playerId, parent);
            rt.Place(UIKit.Center, Vector2.zero, new Vector2(228f, 108f));
            var label = rt.gameObject.AddComponent<SeatLabel>();
            label.Build(playerId, playerName, maxBullets, onTarget);
            return label;
        }

        void Build(int playerId, string playerName, int maxBullets, System.Action<int> onTarget)
        {
            root = (RectTransform)transform;
            PlayerId = playerId;
            var panel = UIKit.CreatePanel(root, "Panel", Theme.Panel.WithAlpha(0.96f), 8f);
            panel.rectTransform.Stretch();
            turnIndicator = UIKit.CreateImage(panel.transform, "TurnIndicator", null, Theme.Gold);
            turnIndicator.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(0f, 1f));
            turnIndicator.rectTransform.sizeDelta = new Vector2(4f, -24f);
            turnIndicator.enabled = false;
            nameText = UIKit.CreateText(panel.transform, "Name", "", 23, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            nameText.rectTransform.Place(UIKit.Top, new Vector2(0f, -23f), new Vector2(196f, 32f));
            nameText.supportRichText = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            SetName(playerName);
            var cardsIcon = UIKit.CreateImage(panel.transform, "Cards icon", SpriteFactory.Cards, Theme.Muted);
            cardsIcon.rectTransform.Place(UIKit.TopLeft, new Vector2(29f,-59f),new Vector2(24f,24f));
            countText = UIKit.CreateText(panel.transform, "Cards", "0", 22, Theme.Text, TextAnchor.MiddleLeft);
            countText.rectTransform.Place(UIKit.TopLeft, new Vector2(79f, -59f), new Vector2(64f, 30f));
            var bulletIcon = UIKit.CreateImage(panel.transform,"Bullet icon",SpriteFactory.Bullet,Theme.Gold);
            bulletIcon.rectTransform.Place(UIKit.TopRight,new Vector2(-65f,-59f),new Vector2(22f,22f));
            bulletsText = UIKit.CreateText(panel.transform, "Bullets", maxBullets.ToString(), 22, Theme.Text, TextAnchor.MiddleRight);
            bulletsText.rectTransform.Place(UIKit.TopRight, new Vector2(-30f, -59f), new Vector2(32f, 30f));
            powerText = UIKit.CreateText(panel.transform, "Power", "", 18, Theme.Muted, TextAnchor.MiddleLeft);
            powerText.rectTransform.Place(UIKit.Top, new Vector2(17f, -91f), new Vector2(166f, 28f));
            powerIcon = UIKit.CreateImage(panel.transform, "PowerIcon", SpriteFactory.Cards, Theme.Muted);
            powerIcon.rectTransform.Place(UIKit.TopLeft, new Vector2(29f, -91f), new Vector2(24f, 24f));
            powerText.supportRichText = false;
            powerText.verticalOverflow = VerticalWrapMode.Truncate;
            turnText = UIKit.CreateText(root, "Turn", "Turn", 24, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            turnText.rectTransform.Place(UIKit.Top, new Vector2(0f, 22f), new Vector2(256f, 32f));
            turnText.gameObject.SetActive(false);
            bubble = UIKit.CreateRect("Bubble", root);
            bubble.Place(UIKit.Bottom, new Vector2(0f, -31f), new Vector2(228f, 52f));
            bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.blocksRaycasts = false;
            UIKit.CreatePanel(bubble, "Bg", Theme.PanelLight.WithAlpha(0.98f), 6f).rectTransform.Stretch();
            bubbleText = UIKit.CreateText(bubble, "Text", "", 20, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            bubbleText.rectTransform.Stretch(12f, 12f, 4f, 4f);
            bubbleText.supportRichText = false;
            bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
            bubbleGroup.alpha = 0f;
            targetButton = UIKit.CreateButton(root, "Target", "Give cards", new Vector2(256f, 60f), Theme.Gold, Theme.TextDark,
                () => onTarget?.Invoke(PlayerId), 26, 6f);
            targetButton.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(0f, 38f), new Vector2(256f, 60f));
            UIKit.AddButtonIcon(targetButton, SpriteFactory.PowerEmblem(PowerType.Hadiah), Color.white);
            targetButton.gameObject.SetActive(false);
        }

        public void SetName(string value) => nameText.text = Hud.StripTags(value);

        public void SetCount(int count)
        {
            countText.text = Mathf.Max(0, count).ToString();
            countText.color = count <= 1 ? Theme.Red : Theme.Text;
        }

        public void SetBullets(int count) => bulletsText.text = Mathf.Max(0, count).ToString();

        public void SetPower(PowerType known, bool spent)
        {
            powerIcon.sprite = known == PowerType.None ? SpriteFactory.Cards : SpriteFactory.PowerEmblem(known);
            powerIcon.color = known == PowerType.None ? Theme.Muted : Color.white.WithAlpha(spent ? .5f : 1f);
            powerText.text = known == PowerType.None ? "" : PowerText.Name(known) + (spent ? " - used" : "");
            SetPowerVisible(known != PowerType.None);
        }

        public void SetPowerVisible(bool value)
        {
            bool revealed = value && powerText.text.Length > 0;
            powerText.gameObject.SetActive(revealed);
            powerIcon.gameObject.SetActive(revealed);
            root.sizeDelta = new Vector2(228f, revealed ? 108f : 80f);
        }

        public void SetActiveTurn(bool value)
        {
            active = value;
            turnIndicator.enabled = value;
            turnText.gameObject.SetActive(value && !targetButton.gameObject.activeSelf);
        }

        public void ShowClaim(int count, Element claim, bool shielded)
        {
            bubbleText.text = count + " " + Theme.ElementName(claim) + (shielded ? " / Shield" : "");
            bubbleText.color = Theme.Text;
            ShowBubble(shielded ? 4f : 2.8f);
        }

        public void ShowText(string text, Color color, float seconds = 0f)
        {
            bubbleText.text = Hud.StripTags(text);
            bubbleText.color = color == Theme.Red ? Theme.Red : Theme.Text;
            ShowBubble(seconds);
        }

        void ShowBubble(float seconds)
        {
            bubbleGroup.alpha = 1f;
            bubbleTimer = seconds;
            if (isActiveAndEnabled) StartCoroutine(Tween.Punch(bubble, 0.04f, 0.25f));
        }

        public void HideBubble()
        {
            bubbleGroup.alpha = 0f;
            bubbleTimer = 0f;
        }

        public void SetTargetMode(bool value)
        {
            targetButton.gameObject.SetActive(value);
            turnText.gameObject.SetActive(active && !value);
        }

        public void Punch()
        {
            if (isActiveAndEnabled) StartCoroutine(Tween.Punch(root, 0.08f, 0.3f));
        }

        void Update()
        {
            if (active) turnIndicator.color = Theme.Gold.WithAlpha(0.9f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f);
            if (bubbleTimer <= 0f) return;
            bubbleTimer -= Time.deltaTime;
            if (bubbleTimer <= 0f) HideBubble();
        }
    }
}
