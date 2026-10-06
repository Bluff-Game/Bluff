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
            rt.Place(UIKit.Center, Vector2.zero, new Vector2(296f, 142f));
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
            nameText = UIKit.CreateText(panel.transform, "Name", "", 28, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            nameText.rectTransform.Place(UIKit.Top, new Vector2(0f, -28f), new Vector2(256f, 38f));
            nameText.supportRichText = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            SetName(playerName);
            countText = UIKit.CreateText(panel.transform, "Cards", "0 kartu", 26, Theme.Text, TextAnchor.MiddleLeft);
            countText.rectTransform.Place(UIKit.TopLeft, new Vector2(84f, -70f), new Vector2(128f, 36f));
            bulletsText = UIKit.CreateText(panel.transform, "Bullets", maxBullets + " peluru", 24, Theme.Text, TextAnchor.MiddleRight);
            bulletsText.rectTransform.Place(UIKit.TopRight, new Vector2(-84f, -70f), new Vector2(128f, 36f));
            powerText = UIKit.CreateText(panel.transform, "Power", "Kuasa: rahasia", 22, Theme.Muted, TextAnchor.MiddleLeft);
            powerText.rectTransform.Place(UIKit.Top, new Vector2(0f, -113f), new Vector2(256f, 34f));
            powerText.supportRichText = false;
            powerText.verticalOverflow = VerticalWrapMode.Truncate;
            turnText = UIKit.CreateText(root, "Turn", "Giliran", 24, Theme.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            turnText.rectTransform.Place(UIKit.Top, new Vector2(0f, 22f), new Vector2(256f, 32f));
            turnText.gameObject.SetActive(false);
            bubble = UIKit.CreateRect("Bubble", root);
            bubble.Place(UIKit.Bottom, new Vector2(0f, -40f), new Vector2(296f, 68f));
            bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            bubbleGroup.blocksRaycasts = false;
            UIKit.CreatePanel(bubble, "Bg", Theme.PanelLight.WithAlpha(0.98f), 6f).rectTransform.Stretch();
            bubbleText = UIKit.CreateText(bubble, "Text", "", 24, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            bubbleText.rectTransform.Stretch(12f, 12f, 4f, 4f);
            bubbleText.supportRichText = false;
            bubbleText.verticalOverflow = VerticalWrapMode.Truncate;
            bubbleGroup.alpha = 0f;
            targetButton = UIKit.CreateButton(root, "Target", "Beri kartu", new Vector2(256f, 60f), Theme.Gold, Theme.TextDark,
                () => onTarget?.Invoke(PlayerId), 26, 6f);
            targetButton.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(0f, 38f), new Vector2(256f, 60f));
            targetButton.gameObject.SetActive(false);
        }

        public void SetName(string value) => nameText.text = Hud.StripTags(value);

        public void SetCount(int count)
        {
            countText.text = Mathf.Max(0, count) + " kartu";
            countText.color = count <= 1 ? Theme.Red : Theme.Text;
        }

        public void SetBullets(int count) => bulletsText.text = Mathf.Max(0, count) + " peluru";

        public void SetPower(PowerType known, bool spent)
        {
            powerText.text = spent ? "Terpakai: " + (known == PowerType.None ? "kuasa" : PowerText.Name(known)) :
                "Kuasa: " + (known == PowerType.None ? "rahasia" : PowerText.Name(known));
        }

        public void SetPowerVisible(bool value)
        {
            powerText.gameObject.SetActive(value);
            root.sizeDelta = new Vector2(296f, value ? 142f : 100f);
        }

        public void SetActiveTurn(bool value)
        {
            active = value;
            turnIndicator.enabled = value;
            turnText.gameObject.SetActive(value && !targetButton.gameObject.activeSelf);
        }

        public void ShowClaim(int count, Element claim, bool shielded)
        {
            bubbleText.text = count + " kartu " + Theme.ElementName(claim) + (shielded ? "\nTameng aktif" : "");
            bubbleText.color = Theme.Text;
            ShowBubble(shielded ? 4f : 0f);
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
