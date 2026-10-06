using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    public sealed class Hud : MonoBehaviour
    {
        RectTransform root;
        Canvas canvas;
        GameSnapshot snapshot;
        TableScene scene;
        PlayerHand3D hand;
        int humanId;
        int maxPerPlay = 3;
        string[] names = new string[0];
        readonly Dictionary<int, SeatLabel> labels = new Dictionary<int, SeatLabel>();
        readonly List<RectTransform> visibleLabels = new List<RectTransform>();
        Text playerName;
        Text playerCount;
        Text playerBullets;
        RectTransform powerWidget;
        RectTransform powerDetails;
        Text powerName;
        Text powerDesc;
        Text powerReason;
        Button powerButton;
        Text powerButtonLabel;
        PowerType myPower;
        bool powerSpent;
        bool powerPinned;
        bool powerHovered;
        bool shieldArmed;
        bool targetMode;
        Text roundText;
        Text taskTitle;
        Text taskText;
        CanvasGroup bannerGroup;
        Text bannerText;
        Coroutine bannerRoutine;
        Text logText;
        RectTransform pileLabel;
        Text pileLabelText;
        CanvasGroup stampGroup;
        Text stampText;
        Image vignette;
        float vignetteAlpha;
        Color vignetteColor;
        RectTransform actionPanel;
        RectTransform turnControls;
        RectTransform shootControls;
        RectTransform elementPicker;
        readonly Dictionary<Element, Image> elementRings = new Dictionary<Element, Image>();
        Text followText;
        Text selectedText;
        Text claimPreview;
        Image timerFill;
        Text timerLabel;
        Button passButton;
        Button playButton;
        Text playLabel;
        Button bluffButton;
        Button letPassButton;
        Button peekButton;
        bool humanTurn;
        bool mustLead;
        bool manualElement;
        Element? chosenElement;
        bool windowOpen;
        bool humanCanShoot;
        bool humanCanPeek;
        bool actionPending;
        int shootTarget = -1;
        bool pileLabelHidden;
        int pileCount;
        float layoutWidth;
        string waitingHint = "Tunggu giliran lawan";

        public event System.Action<List<int>, Element, bool> PlayRequested;
        public event System.Action PassRequested;
        public event System.Action BluffRequested;
        public event System.Action LetPassRequested;
        public event System.Action PeekRequested;
        public event System.Action PauseRequested;
        public event System.Action HelpRequested;
        public event System.Action<PowerRequest> PowerRequested;

        public RectTransform Root => root;

        public static Hud Create(RectTransform parent, Canvas canvas, GameSnapshot snapshot, int humanId, TableScene scene, PlayerHand3D hand)
        {
            var rt = UIKit.CreateRect("HUD", parent);
            rt.Stretch();
            var hud = rt.gameObject.AddComponent<Hud>();
            hud.Build(canvas, snapshot, humanId, scene, hand);
            return hud;
        }

        void Build(Canvas c, GameSnapshot state, int human, TableScene table, PlayerHand3D playerHand)
        {
            root = (RectTransform)transform;
            canvas = c;
            scene = table;
            hand = playerHand;
            humanId = human;
            vignette = UIKit.CreateImage(root, "Vignette", SpriteFactory.Glow, Color.clear);
            vignette.rectTransform.Stretch(-600f, -600f, -400f, -400f);
            vignette.enabled = false;
            BuildTop();
            BuildPileLabel();
            BuildPlayerPanel();
            BuildActions();
            BuildPowerWidget();
            BuildStamp();
            if (hand != null)
            {
                hand.SelectionChanged += Refresh;
                hand.Warning += OnHandWarning;
                hand.SetInteractable(false);
            }
            SetSnapshot(state);
            Refresh();
        }

        static Text Text(Transform parent, string name, string value, int size = 26, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false)
        {
            var text = UIKit.CreateText(parent, name, value, size, Theme.Text, alignment, bold ? FontStyle.Bold : FontStyle.Normal);
            text.supportRichText = false;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        static Button Button(Transform parent, string name, string label, System.Action action, bool primary = false)
        {
            var button = UIKit.CreateButton(parent, name, label, new Vector2(200f, 60f), primary ? Theme.Gold : Theme.PanelLight,
                primary ? Theme.TextDark : Theme.Text, action, 26, 6f);
            var colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
            return button;
        }

        void BuildTop()
        {
            roundText = Text(root, "Round", "Bersiap…", 24);
            roundText.rectTransform.Place(UIKit.TopLeft, new Vector2(224f, -52f), new Vector2(400f, 60f));
            logText = Text(root, "LastAction", "", 22, TextAnchor.UpperLeft);
            logText.color = Theme.Muted;
            logText.rectTransform.Place(UIKit.TopLeft, new Vector2(224f, -126f), new Vector2(400f, 72f));
            var task = UIKit.CreatePanel(root, "Task", Theme.Panel.WithAlpha(0.96f), 8f);
            task.rectTransform.Place(UIKit.Top, new Vector2(0f, -78f), new Vector2(760f, 116f));
            taskTitle = Text(task.transform, "Title", "Menunggu giliran", 34, TextAnchor.MiddleCenter, true);
            taskTitle.rectTransform.Place(UIKit.Top, new Vector2(0f, -34f), new Vector2(720f, 46f));
            taskText = Text(task.transform, "Instruction", "Tunggu giliran lawan", 28, TextAnchor.MiddleCenter);
            taskText.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 30f), new Vector2(720f, 42f));
            var banner = UIKit.CreatePanel(root, "Banner", Theme.Panel.WithAlpha(0.97f), 8f);
            banner.rectTransform.Place(UIKit.Top, new Vector2(0f, -196f), new Vector2(760f, 96f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.alpha = 0f;
            bannerText = Text(banner.transform, "Message", "", 26, TextAnchor.MiddleCenter, true);
            bannerText.rectTransform.Stretch(20f, 20f, 10f, 10f);
            var help = Button(root, "Help", "Panduan", () => HelpRequested?.Invoke());
            help.GetComponent<RectTransform>().Place(UIKit.TopRight, new Vector2(-230f, -50f), new Vector2(152f, 60f));
            var pause = Button(root, "Pause", "Jeda", () => PauseRequested?.Invoke());
            pause.GetComponent<RectTransform>().Place(UIKit.TopRight, new Vector2(-80f, -50f), new Vector2(112f, 60f));
            var cameraHint = Text(root, "CameraHint", "Tahan klik kanan: lihat sekitar · R: kembali", 22, TextAnchor.MiddleCenter);
            cameraHint.color = Theme.Muted;
            cameraHint.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 30f), new Vector2(880f, 32f));
        }

        void BuildPileLabel()
        {
            var panel = UIKit.CreatePanel(root, "PileLabel", Theme.Panel.WithAlpha(0.94f), 6f);
            pileLabel = panel.rectTransform;
            pileLabel.Place(UIKit.Center, Vector2.zero, new Vector2(138f, 44f));
            pileLabelText = Text(pileLabel, "Count", "0 kartu", 24, TextAnchor.MiddleCenter, true);
            pileLabelText.rectTransform.Stretch();
            pileLabel.gameObject.SetActive(false);
        }

        void BuildPlayerPanel()
        {
            var panel = UIKit.CreatePanel(root, "PlayerPanel", Theme.Panel.WithAlpha(0.96f), 8f);
            panel.rectTransform.Place(UIKit.BottomLeft, new Vector2(216f, 84f), new Vector2(384f, 120f));
            var avatar = UIKit.CreatePanel(panel.transform, "AvatarMask", Theme.PanelLight, 6f);
            avatar.rectTransform.Place(UIKit.Left, new Vector2(64f, 0f), new Vector2(104f, 104f));
            avatar.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            AvatarPortrait.Create(avatar.transform, humanId).rectTransform.Stretch();
            playerName = Text(panel.transform, "Name", "Kamu", 28, TextAnchor.MiddleLeft, true);
            playerName.rectTransform.Place(UIKit.TopLeft, new Vector2(248f, -26f), new Vector2(240f, 38f));
            playerCount = Text(panel.transform, "Cards", "0 kartu", 26);
            playerCount.rectTransform.Place(UIKit.TopLeft, new Vector2(248f, -64f), new Vector2(240f, 32f));
            playerBullets = Text(panel.transform, "Bullets", "0 peluru", 24);
            playerBullets.color = Theme.Muted;
            playerBullets.rectTransform.Place(UIKit.TopLeft, new Vector2(248f, -96f), new Vector2(240f, 32f));
        }

        void BuildPowerWidget()
        {
            powerWidget = UIKit.CreateRect("PowerWidget", root);
            powerWidget.Place(UIKit.BottomLeft, new Vector2(216f, 198f), new Vector2(384f, 64f));
            var info = Button(powerWidget, "PowerInfo", "Kuasa", () =>
            {
                powerPinned = !powerPinned;
                ShowPowerDetails(powerPinned || powerHovered);
            });
            info.GetComponent<RectTransform>().Stretch();
            powerName = info.GetComponentInChildren<Text>();
            var details = UIKit.CreatePanel(powerWidget, "PowerDetails", Theme.Panel.WithAlpha(0.98f), 8f, true);
            powerDetails = details.rectTransform;
            powerDetails.Place(UIKit.Top, new Vector2(0f, 142f), new Vector2(384f, 284f));
            powerDesc = Text(powerDetails, "Description", "", 24, TextAnchor.UpperLeft);
            powerDesc.rectTransform.Place(UIKit.Top, new Vector2(0f, -65f), new Vector2(344f, 98f));
            powerReason = Text(powerDetails, "Availability", "", 22, TextAnchor.UpperLeft);
            powerReason.color = Theme.Muted;
            powerReason.rectTransform.Place(UIKit.Top, new Vector2(0f, -157f), new Vector2(344f, 74f));
            powerButton = Button(powerDetails, "UsePower", "Gunakan", OnPowerButton, true);
            powerButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 46f), new Vector2(344f, 60f));
            powerButtonLabel = powerButton.GetComponentInChildren<Text>();
            var trigger = powerWidget.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => { powerHovered = true; ShowPowerDetails(true); });
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => { powerHovered = false; ShowPowerDetails(powerPinned); });
            trigger.triggers.Add(exit);
            powerDetails.gameObject.SetActive(false);
            powerWidget.gameObject.SetActive(false);
        }

        void ShowPowerDetails(bool visible)
        {
            powerDetails.gameObject.SetActive(visible);
            RefreshPowerButton();
        }

        void BuildActions()
        {
            actionPanel = UIKit.CreatePanel(root, "ActionDock", Theme.Panel.WithAlpha(0.97f), 8f, true).rectTransform;
            turnControls = UIKit.CreateRect("TurnControls", actionPanel);
            turnControls.Stretch();
            selectedText = Text(turnControls, "SelectedCards", "Belum ada kartu dipilih", 24, TextAnchor.UpperLeft);
            selectedText.rectTransform.Place(UIKit.Top, new Vector2(0f, -48f), new Vector2(416f, 64f));
            elementPicker = UIKit.CreateRect("ClaimPicker", turnControls);
            elementPicker.Place(UIKit.Top, new Vector2(0f, -189f), new Vector2(416f, 202f));
            var title = Text(elementPicker, "Title", "Klaim sebagai", 24);
            title.rectTransform.Place(UIKit.Top, new Vector2(0f, -18f), new Vector2(416f, 32f));
            for (int i = 0; i < Elements.ClaimableCount; i++)
            {
                var element = Elements.Claimable[i];
                var button = Button(elementPicker, element.ToString(), Theme.ElementName(element), () =>
                {
                    if (!CanPlay || targetMode) return;
                    chosenElement = element;
                    manualElement = true;
                    Refresh();
                });
                button.GetComponent<RectTransform>().Place(UIKit.TopLeft,
                    new Vector2(66f + i % 3 * 142f, -78f - i / 3 * 84f), new Vector2(132f, 72f));
                button.GetComponentInChildren<Text>().fontSize = 24;
                var ring = UIKit.CreateOutline(button.transform, "Selected", Theme.Gold, 6f);
                ring.rectTransform.Stretch(-2f, -2f, -2f, -2f);
                elementRings[element] = ring;
            }
            followText = Text(turnControls, "RoundClaim", "", 26, TextAnchor.MiddleLeft, true);
            followText.rectTransform.Place(UIKit.Top, new Vector2(0f, -108f), new Vector2(416f, 42f));
            claimPreview = Text(turnControls, "ClaimPreview", "", 24, TextAnchor.MiddleLeft);
            claimPreview.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 106f), new Vector2(416f, 56f));
            passButton = Button(turnControls, "Pass", "Lewati", OnPassClicked);
            passButton.GetComponent<RectTransform>().Place(UIKit.BottomLeft, new Vector2(86f, 46f), new Vector2(132f, 60f));
            playButton = Button(turnControls, "Play", "Taruh kartu", OnPlayClicked, true);
            playButton.GetComponent<RectTransform>().Place(UIKit.BottomRight, new Vector2(-148f, 46f), new Vector2(256f, 60f));
            playLabel = playButton.GetComponentInChildren<Text>();
            shootControls = UIKit.CreateRect("ShootControls", actionPanel);
            shootControls.Stretch();
            timerLabel = Text(shootControls, "Claim", "", 26, TextAnchor.UpperLeft, true);
            timerLabel.rectTransform.Place(UIKit.Top, new Vector2(0f, -62f), new Vector2(416f, 92f));
            var track = UIKit.CreatePanel(shootControls, "TimeRemaining", Theme.PanelLight, 2f);
            track.rectTransform.Place(UIKit.Top, new Vector2(0f, -122f), new Vector2(416f, 6f));
            timerFill = UIKit.CreateImage(track.transform, "Remaining", null, Theme.Gold);
            timerFill.rectTransform.Stretch();
            bluffButton = Button(shootControls, "Bluff", "BLUFF!", OnBluffClicked, true);
            bluffButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 192f), new Vector2(416f, 64f));
            bluffButton.GetComponentInChildren<Text>().fontSize = 30;
            letPassButton = Button(shootControls, "LetPass", "Loloskan", OnLetPassClicked);
            letPassButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 120f), new Vector2(416f, 60f));
            peekButton = Button(shootControls, "Peek", "Intip · Mata Elang", OnPeekClicked);
            peekButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 48f), new Vector2(416f, 60f));
        }

        void BuildStamp()
        {
            var rt = UIKit.CreateRect("Stamp", root);
            rt.Place(UIKit.Center, new Vector2(0f, 150f), new Vector2(440f, 96f));
            stampGroup = rt.gameObject.AddComponent<CanvasGroup>();
            stampGroup.blocksRaycasts = false;
            stampGroup.alpha = 0f;
            UIKit.CreatePanel(rt, "Bg", Theme.Panel.WithAlpha(0.96f), 8f).rectTransform.Stretch();
            stampText = UIKit.CreateHeading(rt, "Text", "", 56, Theme.Text);
            stampText.supportRichText = false;
            stampText.rectTransform.Stretch(20f, 20f, 8f, 8f);
        }

        public void SetSnapshot(GameSnapshot state)
        {
            snapshot = state == null ? null : new GameSnapshot
            {
                ViewerId = state.ViewerId,
                Config = state.Config?.Clone(),
                PlayerCount = state.PlayerCount,
                Names = state.Names == null ? new string[0] : (string[])state.Names.Clone(),
                HandCounts = state.HandCounts == null ? new int[0] : (int[])state.HandCounts.Clone(),
                Bullets = state.Bullets == null ? new int[0] : (int[])state.Bullets.Clone(),
                PowerSpent = state.PowerSpent == null ? new bool[0] : (bool[])state.PowerSpent.Clone(),
                KnownPowers = state.KnownPowers == null ? new PowerType[0] : (PowerType[])state.KnownPowers.Clone(),
                Phase = state.Phase,
                CurrentPlayer = state.CurrentPlayer,
                RoundLeader = state.RoundLeader,
                HasRoundElement = state.HasRoundElement,
                RoundElement = state.RoundElement,
                LastPlayerId = state.LastPlayerId,
                LastPlayCount = state.LastPlayCount,
                LastPlayClaim = state.LastPlayClaim,
                LastPlayShielded = state.LastPlayShielded,
                PileCount = state.PileCount,
                Winner = state.Winner,
                MyPower = state.MyPower,
                MyPowerUsed = state.MyPowerUsed,
            };
            if (root == null || playerName == null) return;
            names = snapshot?.Names ?? new string[0];
            maxPerPlay = Mathf.Max(1, snapshot?.Config?.MaxCardsPerPlay ?? 3);
            if (hand != null) hand.MaxSelection = maxPerPlay;
            bool valid = ValidViewer;
            if (!valid || snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId)
            {
                humanTurn = false;
                shieldArmed = false;
                SetTargetMode(false);
                if (hand != null) hand.SetInteractable(false);
            }
            if (!valid || snapshot.Phase != GamePhase.ShootWindow)
            {
                windowOpen = false;
                humanCanShoot = false;
                humanCanPeek = false;
            }
            else
            {
                if (windowOpen && shootTarget != snapshot.LastPlayerId)
                {
                    windowOpen = false;
                    humanCanShoot = false;
                    humanCanPeek = false;
                }
                humanCanShoot &= CanShootSnapshot;
                humanCanPeek &= snapshot.MyPower == PowerType.MataElang && !snapshot.MyPowerUsed;
            }
            if (humanTurn)
            {
                bool lead = !snapshot.HasRoundElement;
                if (mustLead != lead)
                {
                    manualElement = false;
                    chosenElement = null;
                }
                mustLead = lead;
                if (!lead) chosenElement = snapshot.RoundElement;
            }
            waitingHint = snapshot == null ? "Menunggu data permainan" : snapshot.Phase == GamePhase.AwaitingPlay && snapshot.CurrentPlayer != humanId
                ? "Giliran " + PlayerName(snapshot.CurrentPlayer) : "Tunggu giliran lawan";
            if (snapshot != null)
            {
                for (int id = 0; id < snapshot.PlayerCount; id++)
                {
                    if (id == humanId) continue;
                    if (!labels.TryGetValue(id, out var label))
                    {
                        label = SeatLabel.Create(root, id, PlayerName(id), snapshot.Config?.StartingBullets ?? 2, OnTargetChosen);
                        label.transform.SetAsFirstSibling();
                        labels.Add(id, label);
                    }
                    label.SetName(PlayerName(id));
                    label.SetCount(Value(snapshot.HandCounts, id));
                    label.SetBullets(Value(snapshot.Bullets, id));
                    label.SetPower(Value(snapshot.KnownPowers, id), Value(snapshot.PowerSpent, id));
                    label.SetPowerVisible(snapshot.Config != null && snapshot.Config.EnablePowers);
                    label.SetActiveTurn(snapshot.Phase == GamePhase.AwaitingPlay && snapshot.CurrentPlayer == id);
                }
                SetPileCount(snapshot.PileCount);
                SetRoundBadge(snapshot.HasRoundElement ? snapshot.RoundElement : (Element?)null, snapshot.RoundLeader);
            }
            else
            {
                SetPileCount(0);
                SetRoundBadge(null, -1);
                foreach (var label in labels.Values) label.gameObject.SetActive(false);
            }
            playerName.text = valid ? PlayerName(humanId) : "Menunggu pemain";
            SetPlayerStatus(valid ? Value(snapshot.HandCounts, humanId) : 0, valid ? Value(snapshot.Bullets, humanId) : 0);
            myPower = valid && snapshot.Config != null && snapshot.Config.EnablePowers ? snapshot.MyPower : PowerType.None;
            powerSpent = valid && snapshot.MyPowerUsed;
            if (powerSpent || myPower == PowerType.None)
            {
                shieldArmed = false;
                SetTargetMode(false);
            }
            actionPending = false;
            Refresh();
        }

        static T Value<T>(T[] values, int id) => values != null && id >= 0 && id < values.Length ? values[id] : default;
        bool ValidViewer => snapshot != null && snapshot.Config != null && snapshot.ViewerId == humanId && humanId >= 0 &&
            humanId < snapshot.PlayerCount && humanId < snapshot.HandCounts.Length && humanId < snapshot.Bullets.Length;
        bool CanPlay => humanTurn && ValidViewer && snapshot.Phase == GamePhase.AwaitingPlay && snapshot.CurrentPlayer == humanId && !windowOpen && !actionPending;
        bool CanShootSnapshot => ValidViewer && snapshot.Phase == GamePhase.ShootWindow && snapshot.LastPlayerId >= 0 &&
            snapshot.LastPlayerId < snapshot.PlayerCount && snapshot.LastPlayerId != humanId && !snapshot.LastPlayShielded && Value(snapshot.Bullets, humanId) > 0;

        public SeatLabel Label(int id) => labels.TryGetValue(id, out var label) ? label : null;

        public void Toast(string message, Color color)
        {
            if (actionPending && color == Theme.Red)
            {
                actionPending = false;
                Refresh();
            }
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(message, color));
        }

        IEnumerator BannerRoutine(string message, Color color)
        {
            bannerText.text = StripTags(message);
            bannerText.color = color == Theme.Red ? Theme.Red : Theme.Text;
            yield return Tween.Fade(bannerGroup, 1f, 0.12f);
            yield return new WaitForSecondsRealtime(1.5f / GameSettings.Speed + 0.4f);
            yield return Tween.Fade(bannerGroup, 0f, 0.3f);
            bannerRoutine = null;
        }

        void OnHandWarning(string message) => Toast(message, Theme.Red);
        public void Log(string line) => logText.text = StripTags(line);

        public static string StripTags(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new System.Text.StringBuilder(value.Length);
            bool inTag = false;
            foreach (char c in value)
            {
                if (c == '<') inTag = true;
                else if (c == '>' && inTag) inTag = false;
                else if (!inTag) sb.Append(c);
            }
            return sb.ToString();
        }

        public void SetRoundBadge(Element? element, int leaderId)
        {
            roundText.text = element.HasValue ? "Elemen ronde: " + Theme.ElementName(element.Value) :
                leaderId >= 0 ? "Pemimpin: " + PlayerName(leaderId) : "Bersiap…";
        }

        public void SetPileCount(int count)
        {
            pileCount = Mathf.Max(0, count);
            pileLabelText.text = pileCount + " kartu";
            pileLabel.gameObject.SetActive(pileCount > 0 && !pileLabelHidden);
        }

        public void SetPileLabelHidden(bool hidden)
        {
            pileLabelHidden = hidden;
            pileLabel.gameObject.SetActive(pileCount > 0 && !hidden);
        }

        public void SetPlayerStatus(int count, int bullets)
        {
            playerCount.text = Mathf.Max(0, count) + " kartu";
            playerBullets.text = Mathf.Max(0, bullets) + " peluru";
        }

        public void SetMyPower(PowerType power, bool spent)
        {
            myPower = power;
            powerSpent = spent;
            if (spent)
            {
                shieldArmed = false;
                SetTargetMode(false);
            }
            Refresh();
        }

        public void FlashScreen(Color color, float strength)
        {
            vignetteColor = color;
            vignetteAlpha = Mathf.Max(vignetteAlpha, strength);
        }

        public IEnumerator ShowStamp(bool honest, string text = null)
        {
            stampText.text = StripTags(text ?? (honest ? "JUJUR!" : "BOHONG!"));
            stampText.color = honest ? Theme.Text : Theme.Red;
            stampGroup.alpha = 1f;
            yield return Tween.Run(0.3f, t => stampText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.08f, 1f, Tween.EaseOutCubic(t)));
        }

        public IEnumerator HideStamp() => Tween.Fade(stampGroup, 0f, 0.25f);

        public void BeginHumanTurn(GameSnapshot state)
        {
            SetSnapshot(state);
            humanTurn = ValidViewer && snapshot.Phase == GamePhase.AwaitingPlay && snapshot.CurrentPlayer == humanId;
            mustLead = humanTurn && !snapshot.HasRoundElement;
            manualElement = false;
            chosenElement = humanTurn && !mustLead ? snapshot.RoundElement : (Element?)null;
            shieldArmed = false;
            actionPending = false;
            SetTargetMode(false);
            if (hand != null) hand.SetInteractable(humanTurn);
            Refresh();
        }

        public void EndHumanTurn()
        {
            humanTurn = false;
            shieldArmed = false;
            SetTargetMode(false);
            if (hand != null) hand.SetInteractable(false);
            Refresh();
        }

        public void SetWaitingHint(string text)
        {
            waitingHint = StripTags(text);
            Refresh();
        }

        public void ShowShootWindow(bool humanCan, bool humanCanPeek, int targetId)
        {
            windowOpen = true;
            humanCanShoot = humanCan && CanShootSnapshot;
            this.humanCanPeek = humanCanPeek && PowerBlockReason() == null && myPower == PowerType.MataElang;
            shootTarget = targetId;
            humanTurn = false;
            shieldArmed = false;
            actionPending = false;
            SetTargetMode(false);
            if (hand != null) hand.SetInteractable(false);
            timerFill.rectTransform.anchorMax = Vector2.one;
            Refresh();
        }

        public void UpdateShootWindow(float fraction)
        {
            timerFill.rectTransform.anchorMax = new Vector2(float.IsNaN(fraction) ? 0f : Mathf.Clamp01(fraction), 1f);
        }

        public void HideShootWindow()
        {
            windowOpen = false;
            humanCanShoot = false;
            humanCanPeek = false;
            actionPending = false;
            Refresh();
        }

        public void HidePeekButton()
        {
            humanCanPeek = false;
            Refresh();
        }

        void OnPlayClicked()
        {
            if (!CanPlay || hand == null || !playButton.interactable || !chosenElement.HasValue) return;
            var ids = hand.SelectedIds;
            var claim = chosenElement.Value;
            bool shield = shieldArmed;
            actionPending = true;
            Refresh();
            PlayRequested?.Invoke(ids, claim, shield);
        }

        void OnPassClicked()
        {
            if (!CanPlay || mustLead || targetMode) return;
            actionPending = true;
            Refresh();
            PassRequested?.Invoke();
        }

        void OnBluffClicked()
        {
            if (!windowOpen || !humanCanShoot || !CanShootSnapshot || actionPending) return;
            actionPending = true;
            Refresh();
            BluffRequested?.Invoke();
        }

        void OnLetPassClicked()
        {
            if (!windowOpen || !humanCanShoot || actionPending) return;
            actionPending = true;
            Refresh();
            LetPassRequested?.Invoke();
        }

        void OnPeekClicked()
        {
            if (!windowOpen || !humanCanPeek || PowerBlockReason() != null || actionPending) return;
            humanCanPeek = false;
            Refresh();
            PeekRequested?.Invoke();
        }

        void OnPowerButton()
        {
            if (targetMode)
            {
                SetTargetMode(false);
                Refresh();
                return;
            }
            if (!powerButton.interactable || PowerBlockReason() != null || actionPending) return;
            switch (myPower)
            {
                case PowerType.Tameng:
                    shieldArmed = !shieldArmed;
                    break;
                case PowerType.MataElang:
                    OnPeekClicked();
                    return;
                case PowerType.Badai:
                case PowerType.Bakar:
                    var request = PowerRequest.Of(myPower);
                    if (myPower == PowerType.Bakar) request.CardIds = hand.SelectedIds;
                    actionPending = true;
                    powerPinned = false;
                    powerDetails.gameObject.SetActive(false);
                    Refresh();
                    PowerRequested?.Invoke(request);
                    return;
                case PowerType.Hadiah:
                    SetTargetMode(true);
                    break;
            }
            Refresh();
        }

        void OnTargetChosen(int target)
        {
            if (!targetMode || !CanPlay || PowerBlockReason() != null || target == humanId || target < 0 || target >= snapshot.PlayerCount) return;
            int count = hand != null ? hand.SelectedCount : 0;
            if (count < 1 || count > Powers.MaxPowerCards || Value(snapshot.HandCounts, humanId) - count < 1) return;
            var request = PowerRequest.Of(PowerType.Hadiah);
            request.CardIds = hand.SelectedIds;
            request.TargetPlayer = target;
            SetTargetMode(false);
            actionPending = true;
            Refresh();
            PowerRequested?.Invoke(request);
        }

        void SetTargetMode(bool value)
        {
            targetMode = value;
            foreach (var label in labels.Values) label.SetTargetMode(value);
        }

        public void Refresh()
        {
            if (playButton == null || powerButton == null) return;
            var selected = hand != null ? hand.SelectedCards : new List<Card>();
            int count = selected.Count;
            if (humanTurn && mustLead && !manualElement) chosenElement = SuggestElement(selected);
            bool canPlay = CanPlay;
            if (targetMode && (!canPlay || PowerBlockReason() != null || count < 1 || count > Powers.MaxPowerCards || Value(snapshot.HandCounts, humanId) - count < 1))
                SetTargetMode(false);
            if (shieldArmed && (PowerBlockReason() != null || Value(snapshot.HandCounts, humanId) - count < 1)) shieldArmed = false;
            bool turnVisible = humanTurn && !windowOpen && !actionPending;
            bool shootVisible = windowOpen && (humanCanShoot || humanCanPeek) && !actionPending;
            turnControls.gameObject.SetActive(turnVisible);
            shootControls.gameObject.SetActive(shootVisible);
            actionPanel.gameObject.SetActive(turnVisible || shootVisible);
            elementPicker.gameObject.SetActive(turnVisible && mustLead);
            followText.gameObject.SetActive(turnVisible && !mustLead);
            passButton.gameObject.SetActive(turnVisible && !mustLead);
            passButton.interactable = canPlay && !mustLead && !targetMode;
            foreach (var pair in elementRings)
                pair.Value.enabled = chosenElement.HasValue && chosenElement.Value == pair.Key;
            playButton.interactable = canPlay && count >= 1 && count <= maxPerPlay && chosenElement.HasValue && !targetMode;
            StyleAction(playButton, playLabel, playButton.interactable);
            playLabel.text = actionPending ? "Menunggu…" : count > 0 ? "Taruh " + count + " kartu" : "Taruh kartu";
            playButton.GetComponent<RectTransform>().Place(UIKit.BottomRight, new Vector2(mustLead ? -228f : -148f, 46f), new Vector2(mustLead ? 416f : 256f, 60f));
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0) summary.Append(" · ");
                summary.Append(Theme.ElementName(selected[i].Element));
            }
            selectedText.text = count == 0 ? "Belum ada kartu dipilih" : "Dipilih: " + summary;
            followText.text = chosenElement.HasValue ? "Klaim: " + Theme.ElementName(chosenElement.Value) : "";
            claimPreview.text = count == 0 ? "Kartu boleh berbeda dari klaim." : !chosenElement.HasValue ? "Pilih elemen klaim." :
                count + " kartu " + Theme.ElementName(chosenElement.Value) + " · " +
                (selected.TrueForAll(card => card.Matches(chosenElement.Value)) ? "jujur" : "bohong") + (shieldArmed ? "\nTameng aktif" : "");
            bluffButton.gameObject.SetActive(shootVisible && humanCanShoot);
            letPassButton.gameObject.SetActive(shootVisible && humanCanShoot);
            peekButton.gameObject.SetActive(shootVisible && humanCanPeek);
            bluffButton.interactable = humanCanShoot && !actionPending;
            letPassButton.interactable = humanCanShoot && !actionPending;
            peekButton.interactable = humanCanPeek && !actionPending && PowerBlockReason() == null;
            timerLabel.text = PlayerName(shootTarget) + " mengklaim\n" + (snapshot?.LastPlayCount ?? 0) + " kartu " + Theme.ElementName(snapshot?.LastPlayClaim ?? Element.Matahari);
            if (actionPending)
            {
                taskTitle.text = "Menunggu hasil";
                taskText.text = "Aksi sedang diproses";
            }
            else if (canPlay)
            {
                taskTitle.text = "Giliranmu";
                taskText.text = targetMode ? "Pilih penerima Hadiah" : count == 0 ? "Pilih 1–" + maxPerPlay + " kartu" :
                    !chosenElement.HasValue ? "Pilih elemen klaim" : "Periksa klaim, lalu taruh kartu";
            }
            else if (windowOpen)
            {
                taskTitle.text = humanCanShoot ? "Curiga klaim lawan?" : "Menunggu hasil";
                taskText.text = humanCanShoot ? "BLUFF! untuk menantang · atau Loloskan" : ShootWaitingReason();
            }
            else
            {
                taskTitle.text = snapshot != null && snapshot.Winner >= 0 ? "Permainan selesai" : "Menunggu giliran";
                taskText.text = string.IsNullOrWhiteSpace(waitingHint) ? "Tunggu giliran lawan" : waitingHint;
            }
            RefreshPowerButton();
            LayoutActions();
        }

        string ShootWaitingReason()
        {
            if (snapshot == null) return "Menunggu data permainan";
            if (snapshot.LastPlayShielded) return "Klaim dilindungi Tameng";
            if (snapshot.LastPlayerId == humanId) return "Lawan sedang memeriksa klaimmu";
            if (Value(snapshot.Bullets, humanId) <= 0) return "Peluru habis · tunggu hasil klaim";
            return "Menunggu keputusan lawan";
        }

        static void StyleAction(Button button, Text label, bool available)
        {
            button.GetComponent<Image>().color = available ? Theme.Gold : Theme.PanelLight;
            label.color = available ? Theme.TextDark : Theme.Muted;
        }

        string PowerBlockReason()
        {
            if (!ValidViewer || snapshot.Config == null || humanId >= snapshot.HandCounts.Length) return "Pemain tidak dikenal.";
            if (!snapshot.Config.EnablePowers || myPower == PowerType.None) return "Kamu tidak punya Kartu Kuasa.";
            if (powerSpent) return "Kartu Kuasa sudah dipakai.";
            int count = Value(snapshot.HandCounts, humanId);
            switch (myPower)
            {
                case PowerType.MataElang:
                    if (snapshot.Phase != GamePhase.ShootWindow || snapshot.LastPlayerId < 0 || snapshot.LastPlayerId == humanId)
                        return "Mata Elang dipakai saat lawan baru menaruh kartu.";
                    return null;
                case PowerType.Tameng:
                    if (snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId)
                        return "Tameng dipasang saat kamu menaruh kartu.";
                    if (count < 2) return "Tameng tidak bisa dipakai untuk taruhan terakhir.";
                    return null;
                case PowerType.Cermin:
                    return "Cermin aktif otomatis saat kamu ketahuan bohong.";
                case PowerType.Badai:
                    if (snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId) return "Badai dipakai di giliranmu.";
                    if (!snapshot.HasRoundElement) return "Badai dipakai saat mengikuti ronde, bukan saat memimpin.";
                    return null;
                default:
                    if (snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId) return "Kuasa ini dipakai di giliranmu.";
                    if (count < 2) return "Kartumu tinggal 1, simpan untuk taruhan terakhir.";
                    return null;
            }
        }

        void RefreshPowerButton()
        {
            powerWidget.gameObject.SetActive(myPower != PowerType.None);
            if (myPower == PowerType.None) return;
            powerName.text = "Kuasa: " + PowerText.Name(myPower) + (powerSpent ? " · terpakai" : shieldArmed ? " · aktif" : " · info");
            powerName.fontSize = 24;
            powerDesc.text = PowerText.Short(myPower);
            string reason = PowerBlockReason();
            int count = hand != null ? hand.SelectedCount : 0;
            bool available = reason == null && !actionPending;
            string label = "Gunakan " + PowerText.Name(myPower);
            switch (myPower)
            {
                case PowerType.Cermin:
                    available = false;
                    label = "Otomatis";
                    break;
                case PowerType.MataElang:
                    available &= windowOpen && humanCanPeek;
                    label = "Intip kartu";
                    break;
                case PowerType.Tameng:
                    available &= CanPlay;
                    if (available && Value(snapshot.HandCounts, humanId) - count < 1)
                    {
                        available = false;
                        reason = "Tameng tidak bisa dipakai untuk taruhan terakhir.";
                    }
                    label = shieldArmed ? "Batalkan Tameng" : "Pasang Tameng";
                    break;
                case PowerType.Badai:
                    available &= CanPlay;
                    break;
                default:
                    available &= CanPlay;
                    if (available && (count < 1 || count > Powers.MaxPowerCards))
                    {
                        available = false;
                        reason = "Pilih 1–" + Powers.MaxPowerCards + " kartu.";
                    }
                    else if (available && Value(snapshot.HandCounts, humanId) - count < 1)
                    {
                        available = false;
                        reason = "Sisakan minimal 1 kartu di tanganmu.";
                    }
                    label = targetMode ? "Batal memilih lawan" : myPower == PowerType.Bakar ? "Bakar kartu pilihan" : "Pilih penerima";
                    break;
            }
            if (powerSpent) label = "Sudah dipakai";
            powerButton.interactable = available || targetMode;
            powerButtonLabel.text = label;
            powerReason.text = actionPending ? "Menunggu hasil aksi" : reason ?? (shieldArmed ? "Dipakai bersama kartu yang kamu taruh." : "Sekali pakai.");
            StyleAction(powerButton, powerButtonLabel, powerButton.interactable);
        }

        void LayoutActions()
        {
            layoutWidth = root.rect.width;
            bool raised = layoutWidth < 1900f;
            int shootActions = (humanCanShoot ? 2 : 0) + (humanCanPeek ? 1 : 0);
            float height = turnControls.gameObject.activeSelf ? mustLead ? 440f : 280f : 152f + shootActions * 72f;
            float bottom = raised ? 374f : 24f;
            actionPanel.Place(UIKit.BottomRight, new Vector2(-252f, bottom + height * 0.5f), new Vector2(456f, height));
            float lowerAction = humanCanPeek ? 120f : 48f;
            bluffButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, lowerAction + 72f), new Vector2(416f, 64f));
            letPassButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, lowerAction), new Vector2(416f, 60f));
        }

        static Element? SuggestElement(List<Card> selected)
        {
            var counts = new int[Elements.ClaimableCount];
            Element? best = null;
            int bestCount = 0;
            foreach (var card in selected)
            {
                if (!card.Element.IsClaimable()) continue;
                int count = ++counts[(int)card.Element];
                if (count <= bestCount) continue;
                bestCount = count;
                best = card.Element;
            }
            return best;
        }

        string PlayerName(int id)
        {
            string name = StripTags(Value(names, id)).Trim();
            return string.IsNullOrEmpty(name) ? id == humanId ? "Kamu" : "Pemain " + (id + 1) : name;
        }

        public void ShowResult(GameSnapshot game, MatchStats stats, int human, System.Action rematch, System.Action menu)
        {
            if (game == null) return;
            EndHumanTurn();
            HideShootWindow();
            SetSnapshot(game);
            int winner = game.Winner;
            var modal = UIKit.CreateModal(root, winner < 0 ? "Permainan selesai" : winner == human ? "Kamu menang!" : PlayerName(winner) + " menang!", new Vector2(1180f, 820f), null);
            modal.Title.supportRichText = false;
            bool hasStats = stats != null && stats.Lies != null && stats.LiesCaught != null && stats.Shots != null && stats.Hits != null &&
                stats.Lies.Length >= game.PlayerCount && stats.LiesCaught.Length >= game.PlayerCount && stats.Shots.Length >= game.PlayerCount && stats.Hits.Length >= game.PlayerCount;
            var order = new List<int>();
            for (int i = 0; i < game.PlayerCount; i++) order.Add(i);
            order.Sort((a, b) => a == b ? 0 : a == winner ? -1 : b == winner ? 1 : Value(game.HandCounts, a).CompareTo(Value(game.HandCounts, b)));
            float[] columns = hasStats ? new[] { -324f, -66f, 168f, 392f } : new[] { -260f, 320f };
            float[] widths = hasStats ? new[] { 344f, 172f, 256f, 192f } : new[] { 500f, 260f };
            string[] headers = hasStats ? new[] { "Pemain", "Sisa kartu", "Bohong (ketahuan)", "Tembakan (jitu)" } : new[] { "Pemain", "Sisa kartu" };
            for (int c = 0; c < headers.Length; c++)
            {
                var header = Text(modal.Content, "Header" + c, headers[c], 24, c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, true);
                header.color = Theme.Muted;
                header.rectTransform.Place(UIKit.Top, new Vector2(columns[c], -42f), new Vector2(widths[c], 66f));
            }
            for (int row = 0; row < order.Count; row++)
            {
                int id = order[row];
                float y = -106f - row * 70f;
                UIKit.CreatePanel(modal.Content, "Row" + row, id == winner ? Theme.PanelLight : Theme.Text.WithAlpha(0.025f), 6f)
                    .rectTransform.Place(UIKit.Top, new Vector2(0f, y), new Vector2(1040f, 62f));
                var values = new List<string> { (row + 1) + ". " + PlayerName(id), Value(game.HandCounts, id) + " kartu" };
                if (hasStats)
                {
                    values.Add(stats.Lies[id] + " (" + stats.LiesCaught[id] + ")");
                    values.Add(stats.Shots[id] + " (" + stats.Hits[id] + ")");
                }
                for (int c = 0; c < values.Count; c++)
                {
                    var cell = Text(modal.Content, "Cell", values[c], 26, c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, c == 0);
                    cell.rectTransform.Place(UIKit.Top, new Vector2(columns[c], y), new Vector2(widths[c], 54f));
                }
            }
            if (rematch != null)
            {
                var again = Button(modal.Content, "Again", "Main lagi", () => { modal.Close(); rematch(); }, true);
                again.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(-164f, 44f), new Vector2(300f, 64f));
            }
            var toMenu = Button(modal.Content, "Menu", "Menu utama", () => { modal.Close(); menu?.Invoke(); });
            toMenu.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(rematch == null ? 0f : 164f, 44f), new Vector2(300f, 64f));
        }

        void LateUpdate()
        {
            if (!Mathf.Approximately(layoutWidth, root.rect.width)) LayoutActions();
            var cam = scene != null ? scene.Camera : null;
            if (cam != null)
            {
                foreach (var pair in labels)
                {
                    if (snapshot != null && pair.Key < snapshot.PlayerCount)
                        Project(pair.Value.Root, scene.LabelPosition(pair.Key), cam, Vector2.zero);
                    else pair.Value.gameObject.SetActive(false);
                }
                ArrangeLabels();
                if (pileCount > 0 && !pileLabelHidden && scene.PileAnchor != null)
                    Project(pileLabel, scene.PileAnchor.position + Vector3.up * 0.02f, cam, new Vector2(90f, 36f));
            }
            if (vignetteAlpha > 0.001f)
            {
                vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.unscaledDeltaTime * 1.5f);
                vignette.enabled = true;
                vignette.color = vignetteColor.WithAlpha(vignetteAlpha);
            }
            else vignette.enabled = false;
            if (Time.timeScale <= 0f || actionPending || GameTutorial.IsShowing || InputCompat.ModalIsShowing() || InputCompat.RightMouseHeld()) return;
            if (humanCanShoot && InputCompat.BluffPressed()) OnBluffClicked();
            else if (CanPlay)
            {
                if (InputCompat.ConfirmPressed() && playButton.interactable) OnPlayClicked();
                else if (InputCompat.PassPressed() && passButton.interactable) OnPassClicked();
            }
        }

        void ArrangeLabels()
        {
            visibleLabels.Clear();
            foreach (var label in labels.Values)
                if (label.gameObject.activeSelf) visibleLabels.Add(label.Root);
            visibleLabels.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
            if (visibleLabels.Count < 2) return;
            float left = root.rect.xMin + 172f;
            float right = root.rect.xMax - (actionPanel.gameObject.activeSelf ? 644f : 172f);
            const float spacing = 312f;
            if (right - left < spacing * (visibleLabels.Count - 1)) return;
            float previous = left - spacing;
            foreach (var label in visibleLabels)
            {
                var position = label.anchoredPosition;
                position.x = Mathf.Max(position.x, previous + spacing);
                label.anchoredPosition = position;
                previous = position.x;
            }
            float next = right + spacing;
            for (int i = visibleLabels.Count - 1; i >= 0; i--)
            {
                var position = visibleLabels[i].anchoredPosition;
                position.x = Mathf.Min(position.x, next - spacing);
                visibleLabels[i].anchoredPosition = position;
                next = position.x;
            }
        }

        void Project(RectTransform target, Vector3 world, Camera cam, Vector2 offset)
        {
            var screen = cam.WorldToScreenPoint(world);
            bool visible = screen.z > 0f;
            target.gameObject.SetActive(visible);
            if (!visible) return;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out var local)) return;
            var bounds = root.rect;
            var half = target.sizeDelta * 0.5f;
            local += offset;
            local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 24f, bounds.xMax - half.x - 24f);
            local.y = Mathf.Clamp(local.y, bounds.yMin + 440f + half.y, bounds.yMax - half.y - 260f);
            float dockLeft = bounds.xMax - 480f;
            float dockTop = bounds.yMin + actionPanel.anchoredPosition.y + actionPanel.sizeDelta.y * 0.5f;
            if (actionPanel.gameObject.activeSelf && local.x + half.x > dockLeft - 16f && local.y - half.y < dockTop + 80f)
                local.x = dockLeft - half.x - 16f;
            target.anchoredPosition = local;
        }

        void OnDestroy()
        {
            if (hand == null) return;
            hand.SelectionChanged -= Refresh;
            hand.Warning -= OnHandWarning;
        }
    }
}
