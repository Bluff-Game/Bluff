using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    [DefaultExecutionOrder(300)]
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
        bool powerAvailable;
        Image powerIcon;
        Image powerRing;
        Image powerStateIcon;
        Text powerStatus;
        Text powerCharge;
        float shootFraction = 1f;
        PowerType myPower;
        bool powerSpent;
        bool powerPinned;
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
        string waitingHint = "Waiting for opponents";
        bool lastTurnVisible;
        bool lastShootVisible;

        public event System.Action<List<int>, Element, bool> PlayRequested;
        public event System.Action PassRequested;
        public event System.Action BluffRequested;
        public event System.Action LetPassRequested;
        public event System.Action PeekRequested;
        public event System.Action PauseRequested;
        public event System.Action HelpRequested;
        public event System.Action<int,int> EmoteRequested;
        RectTransform emoteTray, recipientTray;
        Button emoteButton;
        float emoteReady;
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
            roundText = Text(root, "Round", "Getting ready...", 24);
            roundText.rectTransform.Place(UIKit.TopLeft, new Vector2(224f, -52f), new Vector2(400f, 60f));
            logText = Text(root, "LastAction", "", 22, TextAnchor.UpperLeft);
            logText.supportRichText = true;
            logText.color = Theme.Muted;
            logText.rectTransform.Place(UIKit.TopLeft, new Vector2(224f, -126f), new Vector2(400f, 72f));
            var task = UIKit.CreatePanel(root, "Task", Theme.Panel.WithAlpha(0.96f), 8f);
            task.rectTransform.Place(UIKit.Top, new Vector2(0f, -62f), new Vector2(540f, 84f));
            taskTitle = Text(task.transform, "Title", "Waiting", 28, TextAnchor.MiddleCenter, true);
            taskTitle.rectTransform.Place(UIKit.Top, new Vector2(0f, -26f), new Vector2(500f, 36f));
            taskText = Text(task.transform, "Instruction", "Waiting for opponents", 22, TextAnchor.MiddleCenter);
            taskText.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 23f), new Vector2(500f, 32f));
            var banner = UIKit.CreatePanel(root, "Banner", Theme.Panel.WithAlpha(0.97f), 8f);
            banner.rectTransform.Place(UIKit.Top, new Vector2(0f, -196f), new Vector2(760f, 96f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.alpha = 0f;
            bannerText = Text(banner.transform, "Message", "", 26, TextAnchor.MiddleCenter, true);
            bannerText.rectTransform.Stretch(20f, 20f, 10f, 10f);
            var help = UIKit.CreateLabeledIconButton(root, "Help", "Guide", SpriteFactory.Emblem(GameIcon.Guide), () => HelpRequested?.Invoke());
            help.GetComponent<RectTransform>().Place(UIKit.TopRight, new Vector2(-168f, -62f), new Vector2(88f, 84f));
            var pause = UIKit.CreateLabeledIconButton(root, "Pause", "Pause", SpriteFactory.Emblem(GameIcon.Pause), () => PauseRequested?.Invoke());
            pause.GetComponent<RectTransform>().Place(UIKit.TopRight, new Vector2(-68f, -62f), new Vector2(88f, 84f));
            emoteButton = UIKit.CreateLabeledIconButton(root,"Emotes","Emote",SpriteFactory.Emblem(GameIcon.Laugh),()=>{recipientTray.gameObject.SetActive(false);emoteTray.gameObject.SetActive(!emoteTray.gameObject.activeSelf);});
            emoteButton.GetComponent<RectTransform>().Place(UIKit.TopRight,new Vector2(-268f,-62f),new Vector2(88f,84f));
            emoteTray = UIKit.CreatePanel(root,"Emote Tray",Theme.Panel,8f,true).rectTransform;
            emoteTray.Place(UIKit.TopRight,new Vector2(-200f,-158f),new Vector2(360f,84f));
            for (int i=0;i<2;i++)
            {
                int value=i;
                var react = UIKit.CreateButton(emoteTray,"Emote " + i,i==0?"Laugh":"Salute",new Vector2(164f,60f),Theme.PanelLight,Theme.Text,()=>{
                    if(Time.unscaledTime<emoteReady) return;
                    emoteTray.gameObject.SetActive(false); ChooseEmoteRecipient(value);
                },20,6f);
                react.GetComponent<RectTransform>().Place(UIKit.Center,new Vector2(i==0?-88f:88f,0f),new Vector2(164f,60f));
                UIKit.AddButtonIcon(react,SpriteFactory.Emblem(i==0?GameIcon.Laugh:GameIcon.Applaud),Color.white,30f,8f);
            }
            emoteTray.gameObject.SetActive(false);
            recipientTray=UIKit.CreatePanel(root,"Emote recipients",Theme.Panel,8f,true).rectTransform;
            recipientTray.Place(UIKit.TopRight,new Vector2(-200f,-220f),new Vector2(360f,208f));
            recipientTray.gameObject.SetActive(false);
        }

        void ChooseEmoteRecipient(int value)
        {
            foreach(Transform child in recipientTray) {child.gameObject.SetActive(false);Destroy(child.gameObject);}
            int row=0;
            for(int seat=0;seat<names.Length;seat++)
            {
                if(seat==humanId) continue;
                int target=seat;
                var button=UIKit.CreateButton(recipientTray,"Emote to "+seat,PlayerName(seat),new Vector2(336f,56f),Theme.PanelLight,Theme.Text,()=>{
                    if(Time.unscaledTime<emoteReady || Time.timeScale<=0f || snapshot==null || snapshot.Phase==GamePhase.GameOver) return;
                    emoteReady=Time.unscaledTime+4f;recipientTray.gameObject.SetActive(false);
                    EmoteRequested?.Invoke(value,target);
                },21,6f);
                button.GetComponent<RectTransform>().Place(UIKit.Top,new Vector2(0,-38f-row++*64f),new Vector2(336f,56f));
                UIKit.AddButtonIcon(button,SpriteFactory.Emblem(value==0?GameIcon.Laugh:GameIcon.Applaud),Color.white,32f,8f);
            }
            recipientTray.gameObject.SetActive(true);
        }

        void BuildPileLabel()
        {
            var panel = UIKit.CreatePanel(root, "PileLabel", Theme.Panel.WithAlpha(0.94f), 6f);
            pileLabel = panel.rectTransform;
            pileLabel.Place(UIKit.Center, Vector2.zero, new Vector2(138f, 44f));
            pileLabelText = Text(pileLabel, "Count", "0 cards", 24, TextAnchor.MiddleCenter, true);
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
            AvatarPortrait.Create(avatar.transform, humanId, Cosmetics.OutfitForSeat(humanId, humanId)).rectTransform.Stretch();
            playerName = Text(panel.transform, "Name", "You", 28, TextAnchor.MiddleLeft, true);
            playerName.rectTransform.Place(UIKit.TopLeft, new Vector2(248f, -26f), new Vector2(240f, 38f));
            playerCount = Text(panel.transform, "Cards", "0 cards", 26);
            playerCount.rectTransform.Place(UIKit.TopLeft, new Vector2(248f, -64f), new Vector2(240f, 32f));
            playerBullets = Text(panel.transform, "Bullets", "0 bullets", 24);
            playerBullets.color = Theme.Muted;
            playerBullets.rectTransform.Place(UIKit.TopLeft, new Vector2(248f, -96f), new Vector2(240f, 32f));
        }

        void BuildPowerWidget()
        {
            powerWidget = UIKit.CreatePanel(root, "PowerWidget", Theme.Panel.WithAlpha(.98f), 8f, true).rectTransform;
            powerWidget.Place(UIKit.BottomLeft, new Vector2(216f, 216f), new Vector2(384f, 112f));
            var quick = UIKit.CreateIconButton(powerWidget, "ActivatePower", null, new Vector2(96f, 96f), Theme.PanelLight, Theme.Gold, OnQuickPower);
            quick.GetComponent<RectTransform>().Place(UIKit.Left, new Vector2(56f, 0f), new Vector2(96f, 96f));
            powerIcon = quick.transform.Find("Icon").GetComponent<Image>();
            var track = UIKit.CreateImage(quick.transform, "RingTrack", SpriteFactory.Ring, Theme.Border);
            track.rectTransform.Stretch(2f, 2f, 2f, 2f);
            powerRing = UIKit.CreateImage(quick.transform, "AvailabilityRing", SpriteFactory.Ring, Theme.Gold);
            powerRing.rectTransform.Stretch(2f, 2f, 2f, 2f);
            powerRing.type = Image.Type.Filled;
            powerRing.fillMethod = Image.FillMethod.Radial360;
            powerRing.fillOrigin = (int)Image.Origin360.Top;
            powerRing.fillClockwise = true;
            powerName = Text(powerWidget, "PowerName", "", 26, TextAnchor.MiddleLeft, true);
            powerName.rectTransform.Place(UIKit.TopLeft, new Vector2(238f, -25f), new Vector2(244f, 34f));
            powerStateIcon = UIKit.CreateImage(powerWidget, "StateIcon", SpriteFactory.Clock, Theme.Muted);
            powerStateIcon.rectTransform.Place(UIKit.TopLeft, new Vector2(128f, -61f), new Vector2(24f, 24f));
            powerStatus = Text(powerWidget, "PowerStatus", "", 21);
            powerStatus.rectTransform.Place(UIKit.TopLeft, new Vector2(250f, -61f), new Vector2(208f, 30f));
            powerCharge = Text(powerWidget, "Charge", "1 / match", 18);
            powerCharge.color = Theme.Muted;
            powerCharge.rectTransform.Place(UIKit.BottomLeft, new Vector2(204f, 21f), new Vector2(176f, 26f));
            var info = Button(powerWidget, "PowerInfo", "Info", () =>
            {
                powerPinned = !powerPinned;
                ShowPowerDetails(powerPinned);
            });
            info.GetComponent<RectTransform>().Place(UIKit.BottomRight, new Vector2(-42f, 24f), new Vector2(64f, 36f));
            info.GetComponentInChildren<Text>().fontSize = 18;
            var details = UIKit.CreatePanel(powerWidget, "PowerDetails", Theme.Panel.WithAlpha(0.98f), 8f, true);
            powerDetails = details.rectTransform;
            powerDetails.Place(UIKit.Top, new Vector2(0f, 146f), new Vector2(384f, 216f));
            powerDesc = Text(powerDetails, "Description", "", 24, TextAnchor.UpperLeft);
            powerDesc.rectTransform.Place(UIKit.Top, new Vector2(0f, -65f), new Vector2(344f, 98f));
            powerReason = Text(powerDetails, "Availability", "", 22, TextAnchor.UpperLeft);
            powerReason.color = Theme.Muted;
            powerReason.rectTransform.Place(UIKit.Top, new Vector2(0f, -157f), new Vector2(344f, 74f));
            powerDetails.gameObject.SetActive(false);
            powerWidget.gameObject.SetActive(false);
        }

        void ShowPowerDetails(bool visible)
        {
            powerDetails.gameObject.SetActive(visible);
            RefreshPowerButton();
        }

        void OnQuickPower()
        {
            if (Time.timeScale <= 0f || GameTutorial.IsShowing || InputCompat.ModalIsShowing()) return;
            if (powerAvailable)
                OnPowerButton();
            else
            {
                powerPinned = true;
                ShowPowerDetails(true);
            }
        }

        void BuildActions()
        {
            actionPanel = UIKit.CreatePanel(root, "ActionDock", Theme.Panel.WithAlpha(0.92f), 10f, true).rectTransform;
            UIKit.CreateOutline(actionPanel, "FocusBorder", Theme.Border.WithAlpha(0.75f), 10f).rectTransform.Stretch();
            turnControls = UIKit.CreateRect("TurnControls", actionPanel);
            turnControls.Stretch();
            selectedText = Text(turnControls, "SelectedCards", "Select your cards", 24, TextAnchor.UpperLeft);
            selectedText.rectTransform.Place(UIKit.Top, new Vector2(0f, -48f), new Vector2(416f, 64f));
            elementPicker = UIKit.CreateRect("ClaimPicker", turnControls);
            elementPicker.Place(UIKit.Top, new Vector2(0f, -189f), new Vector2(416f, 202f));
            var title = Text(elementPicker, "Title", "Claim as", 24);
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
                var elementIcon = UIKit.CreateImage(button.transform, "Icon", SpriteFactory.ElementIcon(element), Theme.Text);
                elementIcon.rectTransform.Place(UIKit.Top, new Vector2(0f, -21f), new Vector2(30f, 30f));
                button.GetComponentInChildren<Text>().rectTransform.Place(UIKit.Bottom, new Vector2(0f, 19f), new Vector2(128f, 30f));
                button.GetComponentInChildren<Text>().fontSize = 21;
                var ring = UIKit.CreateOutline(button.transform, "Selected", Theme.Gold, 6f);
                ring.rectTransform.Stretch(-2f, -2f, -2f, -2f);
                elementRings[element] = ring;
            }
            followText = Text(turnControls, "RoundClaim", "", 26, TextAnchor.MiddleLeft, true);
            followText.rectTransform.Place(UIKit.Top, new Vector2(0f, -108f), new Vector2(416f, 42f));
            claimPreview = Text(turnControls, "ClaimPreview", "", 24, TextAnchor.MiddleLeft);
            claimPreview.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 106f), new Vector2(416f, 56f));
            passButton = Button(turnControls, "Pass", "Pass", OnPassClicked);
            passButton.GetComponent<RectTransform>().Place(UIKit.BottomLeft, new Vector2(86f, 46f), new Vector2(132f, 60f));
            playButton = Button(turnControls, "Play", "Play cards", OnPlayClicked, true);
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
            UIKit.AddButtonIcon(bluffButton, SpriteFactory.Crosshair, Theme.TextDark);
            letPassButton = Button(shootControls, "LetPass", "Let pass", OnLetPassClicked);
            letPassButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 120f), new Vector2(416f, 60f));
            UIKit.AddButtonIcon(letPassButton, SpriteFactory.Check, Theme.Text);
            peekButton = Button(shootControls, "Peek", "Peek - Eagle Eye", OnPeekClicked);
            peekButton.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(0f, 48f), new Vector2(416f, 60f));
            UIKit.AddButtonIcon(peekButton, SpriteFactory.PowerEmblem(PowerType.MataElang), Color.white);
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
            waitingHint = snapshot == null ? "Waiting for match data" : snapshot.Phase == GamePhase.AwaitingPlay && snapshot.CurrentPlayer != humanId
                ? "Turn: " + PlayerName(snapshot.CurrentPlayer) : "Waiting for opponents";
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
            playerName.text = valid ? PlayerName(humanId) : "Waiting for players";
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
        public void Log(string line) => logText.text = line ?? string.Empty;

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
            roundText.text = element.HasValue ? "Round element: " + Theme.ElementName(element.Value) :
                leaderId >= 0 ? "Leader: " + PlayerName(leaderId) : "Getting ready...";
        }

        public void SetPileCount(int count)
        {
            pileCount = Mathf.Max(0, count);
            pileLabelText.text = CardCount(pileCount);
            pileLabel.gameObject.SetActive(pileCount > 0 && !pileLabelHidden);
        }

        public void SetPileLabelHidden(bool hidden)
        {
            pileLabelHidden = hidden;
            pileLabel.gameObject.SetActive(pileCount > 0 && !hidden);
        }

        public void SetPlayerStatus(int count, int bullets)
        {
            playerCount.text = CardCount(Mathf.Max(0, count));
            playerBullets.text = Mathf.Max(0, bullets) + (bullets == 1 ? " bullet" : " bullets");
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
            stampText.text = StripTags(text ?? (honest ? "HONEST!" : "BLUFF!"));
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
            shootFraction = 1f;
            timerFill.rectTransform.anchorMax = Vector2.one;
            Refresh();
        }

        public void UpdateShootWindow(float fraction)
        {
            shootFraction = float.IsNaN(fraction) ? 0f : Mathf.Clamp01(fraction);
            timerFill.rectTransform.anchorMax = new Vector2(shootFraction, 1f);
            if (myPower == PowerType.MataElang && !powerSpent && powerAvailable)
                powerRing.fillAmount = shootFraction;
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
            if (Time.timeScale <= 0f || GameTutorial.IsShowing || InputCompat.ModalIsShowing()) return;
            if (targetMode)
            {
                SetTargetMode(false);
                Refresh();
                return;
            }
            if (!powerAvailable || PowerBlockReason() != null || actionPending) return;
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
            if (playButton == null || powerWidget == null) return;
            var selected = hand != null ? hand.SelectedCards : new List<Card>();
            int count = selected.Count;
            if (humanTurn && mustLead && !manualElement) chosenElement = SuggestElement(selected);
            bool canPlay = CanPlay;
            if (targetMode && (!canPlay || PowerBlockReason() != null || count < 1 || count > Powers.MaxPowerCards || Value(snapshot.HandCounts, humanId) - count < 1))
                SetTargetMode(false);
            if (shieldArmed && (PowerBlockReason() != null || Value(snapshot.HandCounts, humanId) - count < 1)) shieldArmed = false;
            bool turnVisible = humanTurn && !windowOpen && !actionPending;
            bool shootVisible = windowOpen && (humanCanShoot || humanCanPeek) && !actionPending;
            bool actionModeChanged = turnVisible != lastTurnVisible || shootVisible != lastShootVisible;
            lastTurnVisible = turnVisible;
            lastShootVisible = shootVisible;
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
            playLabel.text = actionPending ? "Waiting..." : count > 0 ? "Play " + CardCount(count) : "Play cards";
            playButton.GetComponent<RectTransform>().Place(UIKit.BottomRight, new Vector2(mustLead ? -228f : -148f, 46f), new Vector2(mustLead ? 416f : 256f, 60f));
            var summary = new System.Text.StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                if (i > 0) summary.Append(" · ");
                summary.Append(Theme.ElementName(selected[i].Element));
            }
            selectedText.text = count == 0 ? "Select your cards" : "Selected: " + summary;
            followText.text = chosenElement.HasValue ? "Claim: " + Theme.ElementName(chosenElement.Value) : "";
            claimPreview.text = count == 0 ? "You may bluff." : !chosenElement.HasValue ? "Choose an element." :
                CardCount(count) + " " + Theme.ElementName(chosenElement.Value) + " · " +
                (selected.TrueForAll(card => card.Matches(chosenElement.Value)) ? "honest" : "bluff") + (shieldArmed ? "\nShield active" : "");
            bluffButton.gameObject.SetActive(shootVisible && humanCanShoot);
            letPassButton.gameObject.SetActive(shootVisible && humanCanShoot);
            peekButton.gameObject.SetActive(shootVisible && humanCanPeek);
            bluffButton.interactable = humanCanShoot && !actionPending;
            letPassButton.interactable = humanCanShoot && !actionPending;
            peekButton.interactable = humanCanPeek && !actionPending && PowerBlockReason() == null;
            timerLabel.text = PlayerName(shootTarget) + " claims\n" + CardCount(snapshot?.LastPlayCount ?? 0) + " " + Theme.ElementName(snapshot?.LastPlayClaim ?? Element.Matahari);
            if (actionPending)
            {
                taskTitle.text = "Waiting for result";
                taskText.text = "Resolving action";
            }
            else if (canPlay)
            {
                taskTitle.text = "Your turn";
                taskText.text = targetMode ? "Choose a recipient" : count == 0 ? "Select 1-" + maxPerPlay + " cards" :
                    !chosenElement.HasValue ? "Choose an element" : "Ready to play";
            }
            else if (windowOpen)
            {
                taskTitle.text = humanCanShoot ? "Call their bluff?" : "Waiting for result";
                taskText.text = humanCanShoot ? "Challenge or let it pass" : ShootWaitingReason();
            }
            else
            {
                taskTitle.text = snapshot != null && snapshot.Winner >= 0 ? "Match complete" : "Waiting";
                taskText.text = string.IsNullOrWhiteSpace(waitingHint) ? "Waiting for opponents" : waitingHint;
            }
            RefreshPowerButton();
            LayoutActions();
            if (actionModeChanged)
                FocusFirstAction();
        }

        void FocusFirstAction()
        {
            if (EventSystem.current == null || !actionPanel.gameObject.activeInHierarchy)
                return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                var selectable = selected.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.IsInteractable())
                    return;
            }
            foreach (var button in actionPanel.GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.activeInHierarchy && button.interactable)
                {
                    button.Select();
                    return;
                }
            }
        }

        string ShootWaitingReason()
        {
            if (snapshot == null) return "Waiting for match data";
            if (snapshot.LastPlayShielded) return "Protected by Shield";
            if (snapshot.LastPlayerId == humanId) return "Opponents are deciding";
            if (Value(snapshot.Bullets, humanId) <= 0) return "No bullets left";
            return "Waiting for opponents";
        }

        static void StyleAction(Button button, Text label, bool available)
        {
            button.GetComponent<Image>().color = available ? Theme.Gold : Theme.PanelLight;
            label.color = available ? Theme.TextDark : Theme.Muted;
        }

        string PowerBlockReason()
        {
            if (!ValidViewer || snapshot.Config == null || humanId >= snapshot.HandCounts.Length) return "Unknown player.";
            if (!snapshot.Config.EnablePowers || myPower == PowerType.None) return "You have no power card.";
            if (powerSpent) return "Your power has been used.";
            int count = Value(snapshot.HandCounts, humanId);
            switch (myPower)
            {
                case PowerType.MataElang:
                    if (snapshot.Phase != GamePhase.ShootWindow || snapshot.LastPlayerId < 0 || snapshot.LastPlayerId == humanId)
                        return "Use Eagle Eye after an opponent plays cards.";
                    return null;
                case PowerType.Tameng:
                    if (snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId)
                        return "Arm Shield when playing your cards.";
                    if (count < 2) return "Shield cannot protect your final play.";
                    return null;
                case PowerType.Cermin:
                    return "Mirror activates automatically when your bluff is caught.";
                case PowerType.Badai:
                    if (snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId) return "Use Storm on your turn.";
                    if (!snapshot.HasRoundElement) return "Use Storm when following a round. The leader cannot use it.";
                    return null;
                default:
                    if (snapshot.Phase != GamePhase.AwaitingPlay || snapshot.CurrentPlayer != humanId) return "Use this power on your turn.";
                    if (count < 2) return "Keep your last card for your final play.";
                    return null;
            }
        }

        void RefreshPowerButton()
        {
            powerWidget.gameObject.SetActive(myPower != PowerType.None);
            if (myPower == PowerType.None) return;
            powerName.text = PowerText.Name(myPower);
            powerDesc.text = PowerText.Short(myPower);
            string reason = PowerBlockReason();
            int count = hand != null ? hand.SelectedCount : 0;
            bool available = reason == null && !actionPending;
            switch (myPower)
            {
                case PowerType.Cermin:
                    available = false;
                    break;
                case PowerType.MataElang:
                    available &= windowOpen && humanCanPeek;
                    break;
                case PowerType.Tameng:
                    available &= CanPlay;
                    if (available && Value(snapshot.HandCounts, humanId) - count < 1)
                    {
                        available = false;
                        reason = "Shield cannot protect your final play.";
                    }
                    break;
                case PowerType.Badai:
                    available &= CanPlay;
                    break;
                default:
                    available &= CanPlay;
                    if (available && (count < 1 || count > Powers.MaxPowerCards))
                    {
                        available = false;
                        reason = "Select 1-" + Powers.MaxPowerCards + " cards.";
                    }
                    else if (available && Value(snapshot.HandCounts, humanId) - count < 1)
                    {
                        available = false;
                        reason = "Keep at least 1 card in your hand.";
                    }
                    break;
            }
            powerAvailable = available || targetMode;
            powerReason.text = actionPending ? "Resolving" : reason ?? (shieldArmed ? "Applies to your next play." : "Single-use.");
            bool passive = myPower == PowerType.Cermin && !powerSpent;
            bool active = shieldArmed || targetMode;
            Color stateColor = powerSpent ? Theme.Muted : active || passive ? Theme.Green : available ? Theme.Gold : Theme.Muted;
            powerIcon.sprite = SpriteFactory.PowerEmblem(myPower);
            powerIcon.rectTransform.Stretch(10f, 10f, 10f, 10f);
            powerIcon.color = powerSpent ? Color.white.WithAlpha(.35f) : Color.white;
            powerRing.color = stateColor;
            powerRing.fillAmount = powerSpent || (!available && !active && !passive) ? 0f :
                myPower == PowerType.MataElang && windowOpen ? shootFraction : 1f;
            powerStatus.text = powerSpent ? "Used" : actionPending ? "Resolving..." : targetMode ? "Choose opponent" : shieldArmed ? "Armed" :
                passive ? "Passive" : available ? "Ready" : CanPlay && myPower.NeedsCards() ?
                count >= 1 && count <= Powers.MaxPowerCards ? "Keep 1 card" : "Select 1-2 cards" : "Unavailable";
            powerStatus.color = stateColor;
            powerStateIcon.sprite = powerSpent ? SpriteFactory.Close : active || passive ? SpriteFactory.Check : available ? SpriteFactory.Play : SpriteFactory.Clock;
            powerStateIcon.color = stateColor;
            powerCharge.text = powerSpent ? "0 / match" : "1 / match";
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

        static string CardCount(int count) => count + (count == 1 ? " card" : " cards");

        string PlayerName(int id)
        {
            string name = StripTags(Value(names, id)).Trim();
            return string.IsNullOrEmpty(name) ? id == humanId ? "You" : "Player " + (id + 1) : name;
        }

        public void ShowResult(GameSnapshot game, MatchStats stats, int human, System.Action rematch, System.Action menu, int coinsEarned = 0)
        {
            if (game == null) return;
            EndHumanTurn();
            HideShootWindow();
            SetSnapshot(game);
            int winner = game.Winner;
            var modal = UIKit.CreateModal(root, winner < 0 ? "Match complete" : winner == human ? "You win!" : PlayerName(winner) + " wins!", new Vector2(1180f, 820f), null);
            modal.Title.supportRichText = false;
            if (coinsEarned > 0)
            {
                var reward = Text(modal.Content, "Coin Reward", "+" + coinsEarned + " coins - balance " + Cosmetics.Coins, 24, TextAnchor.MiddleCenter, true);
                reward.color = Theme.Gold;
                reward.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 126f), new Vector2(900f,40f));
            }
            bool hasStats = stats != null && stats.Lies != null && stats.LiesCaught != null && stats.Shots != null && stats.Hits != null &&
                stats.Lies.Length >= game.PlayerCount && stats.LiesCaught.Length >= game.PlayerCount && stats.Shots.Length >= game.PlayerCount && stats.Hits.Length >= game.PlayerCount;
            var order = new List<int>();
            for (int i = 0; i < game.PlayerCount; i++) order.Add(i);
            order.Sort((a, b) => a == b ? 0 : a == winner ? -1 : b == winner ? 1 : Value(game.HandCounts, a).CompareTo(Value(game.HandCounts, b)));
            float[] columns = hasStats ? new[] { -324f, -66f, 168f, 392f } : new[] { -260f, 320f };
            float[] widths = hasStats ? new[] { 344f, 172f, 256f, 192f } : new[] { 500f, 260f };
            string[] headers = hasStats ? new[] { "Player", "Cards left", "Bluffs (caught)", "Shots (hits)" } : new[] { "Player", "Cards left" };
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
                var values = new List<string> { (row + 1) + ". " + PlayerName(id), CardCount(Value(game.HandCounts, id)) };
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
                var again = Button(modal.Content, "Again", "Play again", () => { modal.Close(); rematch(); }, true);
                again.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(-164f, 44f), new Vector2(300f, 64f));
            }
            var toMenu = Button(modal.Content, "Menu", "Main menu", () => { modal.Close(); menu?.Invoke(); });
            toMenu.GetComponent<RectTransform>().Place(UIKit.Bottom, new Vector2(rematch == null ? 0f : 164f, 44f), new Vector2(300f, 64f));
        }

        void LateUpdate()
        {
            if (emoteButton != null) emoteButton.interactable = Time.unscaledTime >= emoteReady && Time.timeScale > 0f && !GameTutorial.IsShowing;
            if (emoteTray != null && (Time.timeScale <= 0f || GameTutorial.IsShowing || InputCompat.ModalIsShowing())) emoteTray.gameObject.SetActive(false);
            if (recipientTray != null && (Time.timeScale <= 0f || GameTutorial.IsShowing || InputCompat.ModalIsShowing())) recipientTray.gameObject.SetActive(false);
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
                    Project(pileLabel, scene.PileAnchor.position + new Vector3(.19f,0.04f,0f), cam, Vector2.zero, true);
            }
            if (vignetteAlpha > 0.001f)
            {
                vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, 0f, Time.unscaledDeltaTime * 1.5f);
                vignette.enabled = true;
                vignette.color = vignetteColor.WithAlpha(vignetteAlpha);
            }
            else vignette.enabled = false;
            if (Time.timeScale <= 0f || actionPending || GameTutorial.IsShowing || InputCompat.ModalIsShowing() || InputCompat.RightMouseHeld()) return;
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var focusedButton = focused != null ? focused.GetComponent<Button>() : null;
            bool focusedControl = focusedButton != null && focusedButton.IsActive() && focusedButton.IsInteractable();
            if (humanCanShoot && InputCompat.BluffPressed(!focusedControl || focusedButton == bluffButton)) OnBluffClicked();
            else if (CanPlay)
            {
                bool anotherButton = focusedControl && focusedButton != playButton;
                // Enter on a focused utility/ability button must not also submit the selected cards.
                if (InputCompat.ConfirmPressed() && playButton.interactable && !anotherButton) OnPlayClicked();
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
            float left = root.rect.xMin + 138f;
            float right = root.rect.xMax - 138f;
            const float spacing = 244f;
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

        void Project(RectTransform target, Vector3 world, Camera cam, Vector2 offset, bool atTable = false)
        {
            var screen = cam.WorldToScreenPoint(world);
            var viewport = cam.WorldToViewportPoint(world);
            bool visible = viewport.z > cam.nearClipPlane && viewport.x > .02f && viewport.x < .98f && viewport.y > .02f && viewport.y < .98f;
            if (atTable && visible && hand != null && hand.OccludesWorldPoint(cam,world)) visible = false;
            target.gameObject.SetActive(visible);
            if (!visible) return;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, uiCamera, out var local)) return;
            var bounds = root.rect;
            var half = target.sizeDelta * 0.5f;
            local += offset;
            if (atTable)
            {
                // A world label must never be clamped into the sky when its anchor leaves the view.
                target.anchoredPosition = local;
                bool inside = local.x-half.x > bounds.xMin && local.x+half.x < bounds.xMax && local.y-half.y > bounds.yMin && local.y+half.y < bounds.yMax;
                target.gameObject.SetActive(inside);
                return;
            }
            if (local.y - half.y < bounds.yMin + 360f)
            {
                target.gameObject.SetActive(false);
                return;
            }
            local.x = Mathf.Clamp(local.x, bounds.xMin + half.x + 24f, bounds.xMax - half.x - 24f);
            local.y = Mathf.Clamp(local.y, bounds.yMin + half.y + 24f, bounds.yMax - half.y - 120f);
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
