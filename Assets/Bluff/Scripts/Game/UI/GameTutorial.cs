using System;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    [DefaultExecutionOrder(-10000)]
    public sealed class GameTutorial : MonoBehaviour
    {
        static readonly string[] StepNames = { "Select cards", "Claim", "BLUFF!", "Masks & powers" };
        static readonly string[] TopicNames = { "Objective", "Turns & claims", "BLUFF! & Let pass", "Passing & rounds", "Mask" };
        static GameTutorial current;
        static int closedFrame = -1;

        readonly Card[] sampleHand =
        {
            new Card(0, Element.Matahari),
            new Card(1, Element.Matahari),
            new Card(2, Element.Daun),
        };
        readonly bool[] selected = new bool[3];
        readonly int[] decisions = { -1, -1 };
        readonly bool[] powerDemoUsed = new bool[7];
        readonly List<Button> controls = new List<Button>();
        readonly List<Image> focusOutlines = new List<Image>();

        RectTransform owner;
        RectTransform view;
        RectTransform page;
        ScrollRect bodyScroll;
        ScrollRect topicScroll;
        EventSystem eventSystem;
        GameObject previousSelection;
        Vector2 viewSize;
        bool previousNavigation;
        bool ownsFocus;
        bool closing;
        bool finished;
        bool referenceMode;
        bool maskTried;
        bool selectionLimitReached;
        int step;
        int unlockedStep;
        int topic;
        int example;
        int builtFrame;
        float pageWidth;
        Element? claim;
        GameConfig config;
        Action onClosed;

        public bool IsOpen => !closing && !finished && isActiveAndEnabled;
        public static bool IsShowing => (current != null && current.isActiveAndEnabled) || closedFrame == Time.frameCount;

        public static GameTutorial Show(RectTransform parent, Action onClosed)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (!parent.gameObject.activeInHierarchy)
                throw new ArgumentException("Tutorial requires an active parent.", nameof(parent));
            if (current != null && !current.finished)
            {
                if (onClosed != null)
                {
                    current.onClosed -= onClosed;
                    current.onClosed += onClosed;
                }
                return current;
            }

            var parentCanvas = parent.GetComponentInParent<Canvas>();
            if (parentCanvas == null)
                throw new ArgumentException("Tutorial requires a parent inside a Canvas.", nameof(parent));
            UIKit.EnsureEventSystem();
            var canvas = UIKit.CreateCanvas("Modal Tutorial", null, short.MaxValue);
            canvas.targetDisplay = parentCanvas.rootCanvas.targetDisplay;
            if (parent.gameObject.scene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas.gameObject, parent.gameObject.scene);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;
            var tutorial = canvas.gameObject.AddComponent<GameTutorial>();
            tutorial.owner = parent;
            tutorial.onClosed = onClosed;
            tutorial.referenceMode = GameSettings.TutorialSeen;
            current = tutorial;
            try
            {
                var controller = parent.GetComponentInParent<GameController>();
                tutorial.config = controller != null && controller.Engine != null
                    ? controller.Engine.Config.Clone()
                    : new GameConfig
                    {
                        RefundBulletOnHit = GameSettings.RefundBulletOnHit,
                        WrongShotTakesWholePile = GameSettings.ClassicWrongShotPenalty,
                        EnablePowers = GameSettings.EnablePowers,
                    };
                tutorial.CaptureFocus();
                Canvas.ForceUpdateCanvases();
                tutorial.Render();
            }
            catch
            {
                tutorial.Close();
                throw;
            }
            return tutorial;
        }

        public void SetConfig(GameConfig rules)
        {
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));
            rules.Validate();
            if (!IsOpen)
                return;
            config = rules.Clone();
            Array.Clear(selected, 0, selected.Length);
            decisions[0] = decisions[1] = -1;
            claim = null;
            maskTried = selectionLimitReached = false;
            Array.Clear(powerDemoUsed, 0, powerDemoUsed.Length);
            step = example = unlockedStep = 0;
            Render();
        }

        public void Close()
        {
            if (closing || finished)
                return;
            closing = true;
            closedFrame = Time.frameCount;
            Destroy(gameObject);
        }

        void CompleteOrSkip()
        {
            if (!IsOpen)
                return;
            try
            {
                GameSettings.TutorialSeen = true;
            }
            finally
            {
                Close();
            }
        }

        void CaptureFocus()
        {
            eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;
            previousSelection = eventSystem.currentSelectedGameObject;
            previousNavigation = eventSystem.sendNavigationEvents;
            ownsFocus = true;
            eventSystem.sendNavigationEvents = false;
            eventSystem.SetSelectedGameObject(null);
        }

        void Render(bool preservePosition = false, string focusName = null)
        {
            float bodyPosition = preservePosition && bodyScroll != null ? bodyScroll.verticalNormalizedPosition : 1f;
            float topicPosition = topicScroll != null ? topicScroll.verticalNormalizedPosition : 1f;
            if (preservePosition && focusName == null && eventSystem != null && eventSystem.currentSelectedGameObject != null)
                focusName = eventSystem.currentSelectedGameObject.name;
            if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            controls.Clear();
            focusOutlines.Clear();
            bodyScroll = topicScroll = null;
            viewSize = ((RectTransform)transform).rect.size;
            view = UIKit.CreateImage(transform, "Guide", null, Theme.Panel, true).rectTransform;
            view.Stretch();
            float width = viewSize.x;
            var title = UIKit.CreateHeading(view, "Guide Title", "Guide", 42, Theme.Text, TextAnchor.MiddleLeft);
            IconAt(view, SpriteFactory.Emblem(GameIcon.Guide), 36f, 20f, 40f, Color.white);
            Place(title.rectTransform, 88f, 12f, width - 272f, 56f);
            var close = Control(view, "Close Guide", "Close", width - 160f, 16f, 120f, 48f, CompleteOrSkip);
            close.GetComponent<RectTransform>().Place(UIKit.TopRight, new Vector2(-100f, -40f), new Vector2(120f, 48f));
            UIKit.AddButtonIcon(close, SpriteFactory.Emblem(GameIcon.Close), Color.white, 30f, 8f);
            close.GetComponentInChildren<Text>().fontSize = 20;
            var practice = Control(view, "Mode Practice", "Practice", 40f, 78f, 172f, 48f, () => SetMode(false));
            var reference = Control(view, "Mode Reference", "Rulebook", 224f, 78f, 232f, 48f, () => SetMode(true));
            UIKit.AddButtonIcon(practice, SpriteFactory.Emblem(GameIcon.Play), Color.white, 32f, 10f);
            UIKit.AddButtonIcon(reference, SpriteFactory.Emblem(GameIcon.Guide), Color.white, 32f, 10f);
            reference.GetComponentInChildren<Text>().fontSize = 21;
            Mark(practice, !referenceMode);
            Mark(reference, referenceMode);

            if (referenceMode)
            {
                BuildTopics(width, topicPosition);
                bodyScroll = ScrollArea(view, "Reference Scroll", 328f, 40f, 146f, 84f);
            }
            else
            {
                float itemWidth = (width - 104f) / 4f;
                for (int i = 0; i < StepNames.Length; i++)
                {
                    int index = i;
                    var tab = Control(view, "Step " + (i + 1), StepNames[i],
                        40f + i * (itemWidth + 8f), 138f, itemWidth, 44f, () =>
                        {
                            if (index > unlockedStep)
                                return;
                            step = index;
                            Render();
                        });
                    tab.interactable = i <= unlockedStep;
                    UIKit.AddButtonIcon(tab, StepIcon(i), Color.white, 24f, 12f);
                    tab.GetComponentInChildren<Text>().fontSize = 18;
                    Mark(tab, i == step);
                }
                bodyScroll = ScrollArea(view, "Practice Scroll", 40f, 40f, 198f, 84f);
            }
            page = bodyScroll.content;
            pageWidth = Mathf.Max(240f, width - (referenceMode ? 392f : 104f));
            page.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, pageWidth);
            float height;
            if (referenceMode)
                height = BuildReference();
            else
            {
                switch (step)
                {
                    case 0: height = BuildHand(); break;
                    case 1: height = BuildClaim(); break;
                    case 2: height = BuildChallenge(); break;
                    default: height = BuildSpecialCards(); break;
                }
            }
            page.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height + 16f);
            if (referenceMode)
                FooterButton("Next", "Done", true, CompleteOrSkip);
            else
            {
                if (step > 0)
                    FooterButton("Back", "Back", false, () => { step--; Render(); });
                var next = FooterButton("Next", step == 3 ? "Done" : "Next", true, Advance);
                next.interactable = CanAdvance();
            }
            Canvas.ForceUpdateCanvases();
            bodyScroll.verticalNormalizedPosition = bodyPosition;
            builtFrame = Time.frameCount;
            if (focusName == null)
                focusName = referenceMode ? TopicControlName(topic) : step == 0 ? "Sample 0" : step == 1 ? "Claim Matahari" : step == 2 ? (config.StartingBullets > 0 ? "Accuse" : "Trust") : "Try Mask";
            Focus(controls.Find(button => button.name == focusName) ?? controls[0], !preservePosition);
        }

        void SetMode(bool reference)
        {
            referenceMode = reference;
            Render();
        }

        void OpenTopic(int index)
        {
            referenceMode = true;
            topic = index;
            Render(false, TopicControlName(index));
        }

        static string TopicControlName(int index) => index < TopicNames.Length ? "Topic " + index : "Power " + Powers.All[index - TopicNames.Length];

        void BuildTopics(float width, float position)
        {
            topicScroll = ScrollArea(view, "Topic Scroll", 40f, width - 304f, 146f, 84f);
            float y = 4f;
            for (int i = 0; i < TopicNames.Length + Powers.All.Count; i++)
            {
                if (i == TopicNames.Length)
                {
                    y += 12f;
                    TextAt(topicScroll.content, "Powers Heading", "Power cards", 24, Theme.Muted, 4f, y, 232f, 34f);
                    y += 42f;
                }
                int index = i;
                string label = i < TopicNames.Length ? TopicNames[i] : PowerText.Name(Powers.All[i - TopicNames.Length]);
                float height = i == 2 ? 66f : 50f;
                var button = Control(topicScroll.content, TopicControlName(i), label, 4f, y, 232f, height, () => OpenTopic(index));
                UIKit.AddButtonIcon(button, TopicIcon(i), Color.white, 28f, 12f);
                button.GetComponentInChildren<Text>().fontSize = 20;
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                Mark(button, topic == i);
                y += height + 8f;
            }
            topicScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y + 8f);
            Canvas.ForceUpdateCanvases();
            topicScroll.verticalNormalizedPosition = position;
        }

        int SelectedCount
        {
            get
            {
                int count = 0;
                foreach (bool value in selected)
                    if (value)
                        count++;
                return count;
            }
        }

        int SelectionLimit => Math.Min(sampleHand.Length, config.MaxCardsPerPlay);
        static string CardCount(int maximum) => maximum == 1 ? "1 card" : "1–" + maximum + " cards";

        float PageHeading(string title, string instruction)
        {
            var badge = UIKit.CreatePanel(page, "Topic Emblem", Theme.PanelLight, 8f);
            Place(badge.rectTransform, 4f, 0f, 56f, 56f);
            IconAt(badge.transform, referenceMode ? TopicIcon(topic) : StepIcon(step), 6f, 6f, 44f, Color.white);
            float y = Mathf.Max(56f, Paragraph(page, "Heading", title, 38, Theme.Text, 76f, 0f, pageWidth - 80f, true)) + 8f;
            return y + Paragraph(page, "Instruction", instruction, 22, Theme.Muted, 4f, y, pageWidth - 8f) + 22f;
        }

        static Sprite StepIcon(int index) => index == 0 ? SpriteFactory.Emblem(GameIcon.Cards) : index == 1 ? SpriteFactory.ElementEmblem(Element.Matahari) :
            index == 2 ? SpriteFactory.Emblem(GameIcon.Challenge) : SpriteFactory.ElementEmblem(Element.Topeng);

        static Sprite TopicIcon(int index) => index >= TopicNames.Length ? SpriteFactory.PowerEmblem(Powers.All[index - TopicNames.Length]) :
            index == 0 ? SpriteFactory.Emblem(GameIcon.Check) : index == 1 ? SpriteFactory.Emblem(GameIcon.Cards) : index == 2 ? SpriteFactory.Emblem(GameIcon.Challenge) :
            index == 3 ? SpriteFactory.Emblem(GameIcon.Restart) : SpriteFactory.ElementEmblem(Element.Topeng);

        static Image IconAt(Transform parent, Sprite sprite, float x, float y, float size, Color color)
        {
            var icon = UIKit.CreateImage(parent, "Icon", sprite, color);
            icon.preserveAspect = true;
            Place(icon.rectTransform, x, y, size, size);
            return icon;
        }

        float BuildHand()
        {
            float y = PageHeading("Select " + CardCount(SelectionLimit), "Click to select a card. Click again to deselect."
                + (config.MaxCardsPerPlay > sampleHand.Length ? " Match limit: " + config.MaxCardsPerPlay + " cards per play." : ""));
            const float cardWidth = 180f;
            for (int i = 0; i < sampleHand.Length; i++)
            {
                int index = i;
                var button = Control(page, "Sample " + i, "", 4f + i * 204f, y + (selected[i] ? 0f : 6f), cardWidth, 252f, () => ToggleCard(index));
                CardFace(button.transform, sampleHand[i].Element, false, 0f, 0f, cardWidth);
                Mark(button, selected[i]);
                if (selected[i])
                    TextAt(page, "Selection " + i, "Selected", 24, Theme.Gold, 4f + i * 204f, y + 266f, cardWidth, 34f, TextAnchor.MiddleCenter);
            }
            bool stacked = pageWidth < 960f;
            float x = stacked ? 4f : 640f;
            float infoY = stacked ? y + 316f : y + 12f;
            float infoWidth = pageWidth - x - 4f;
            infoY += Paragraph(page, "Play Heading", "Play face down", 32, Theme.Text, x, infoY, infoWidth, true) + 12f;
            infoY += Paragraph(page, "Play Rule", "Opponents see the count and your claim. The cards stay hidden.", 24, Theme.Text, x, infoY, infoWidth) + 16f;
            infoY += Paragraph(page, "Win Rule", "Empty your hand. Your final play must pass or prove honest.", 24, Theme.Text, x, infoY, infoWidth) + 20f;
            string status = selectionLimitReached
                ? "Maximum " + SelectionLimit + " cards. Deselect one to choose another."
                : SelectedCount == 0 ? "Select at least 1 card to continue." : SelectedCount + " cards selected. Next, choose your claim.";
            infoY += Paragraph(page, "Selection Summary", status, 24, selectionLimitReached ? Theme.Text : Theme.Gold, x, infoY, infoWidth);
            return Mathf.Max(y + 304f, infoY);
        }

        void ToggleCard(int index)
        {
            selectionLimitReached = !selected[index] && SelectedCount >= SelectionLimit;
            if (!selectionLimitReached)
            {
                selected[index] = !selected[index];
                claim = null;
                unlockedStep = SelectedCount > 0 ? Math.Min(unlockedStep, 1) : 0;
            }
            Render(true, "Sample " + index);
        }

        float BuildClaim()
        {
            float y = PageHeading("Choose an element", "The leader chooses an element. Everyone else follows that element.");
            int position = 0;
            for (int i = 0; i < sampleHand.Length; i++)
                if (selected[i])
                    CardFace(page, sampleHand[i].Element, false, 4f + position++ * 176f, y + 4f, 160f);
            bool stacked = pageWidth < 1040f;
            float x = stacked ? 4f : 548f;
            float claimY = stacked ? y + 248f : y;
            float buttonWidth = (pageWidth - x - 20f) / 2f;
            for (int i = 0; i < Elements.Claimable.Count; i++)
            {
                Element element = Elements.Claimable[i];
                var button = Control(page, "Claim " + element, Theme.ElementName(element),
                    x + i % 2 * (buttonWidth + 16f), claimY + i / 2 * 76f, buttonWidth, 64f, () =>
                    {
                        claim = element;
                        Render(true, "Claim " + element);
                    });
                UIKit.AddButtonIcon(button, SpriteFactory.ElementEmblem(element), Theme.Text, 30f);
                Mark(button, claim.HasValue && claim.Value == element);
            }
            float bottom = Mathf.Max(y + 236f, claimY + 216f) + 14f;
            string explanation;
            if (!claim.HasValue)
                explanation = "You may bluff. Pick an element to see the result.";
            else
            {
                bool honest = true;
                for (int i = 0; i < sampleHand.Length; i++)
                    if (selected[i] && !sampleHand[i].Matches(claim.Value))
                        honest = false;
                explanation = "«" + SelectedCount + " " + Theme.ElementName(claim.Value) + "» — " + (honest
                    ? "honest: every card matches."
                    : "a bluff: some cards do not match. This is allowed.");
            }
            bottom += Paragraph(page, "Claim Result", explanation, 24, Theme.Gold, 4f, bottom, pageWidth - 8f) + 10f;
            bottom += Paragraph(page, "Follow Round", "Next turn: follow the element or pass. The leader must play.", 24, Theme.Text, 4f, bottom, pageWidth - 8f) + 10f;
            bottom += Paragraph(page, "Burn Rule", "All " + (config.PlayerCount - 1) + " opponents pass in a row: the pile burns and the last player leads again.", 24, Theme.Text, 4f, bottom, pageWidth - 8f);
            return bottom;
        }

        float BuildChallenge()
        {
            float y = PageHeading("BLUFF! or let pass?", "An opponent claims '1 Sun'. Choose an action to see what happens.");
            bool answered = decisions[example] >= 0;
            bool revealed = decisions[example] == 1;
            CardFace(page, example == 0 ? Element.Daun : Element.Matahari, !revealed, 4f, y + 4f, 180f);
            TextAt(page, "Reveal Notice", revealed ? "Revealed card" : "Hidden card", 24, Theme.Text, 0f, y + 266f, 188f, 36f, TextAnchor.MiddleCenter);
            float x = 224f;
            float width = pageWidth - x - 4f;
            float infoY = y;
            infoY += Paragraph(page, "Pile", "Pile: 3 cards. The opponent still has cards in hand.", 24, Theme.Text, x, infoY, width) + 10f;
            infoY += Paragraph(page, "Bullets", "Practice starting bullets: " + config.StartingBullets + ". No powers."
                + (config.StartingBullets == 0 ? " BLUFF! requires a bullet." : " BLUFF! uses 1 bullet."), 24, Theme.Text, x, infoY, width) + 16f;
            float actionWidth = Mathf.Min(240f, (width - 16f) / 2f);
            var accuse = Control(page, "Accuse", "BLUFF!", x, infoY, actionWidth, 56f, () => Decide(true));
            accuse.interactable = config.StartingBullets > 0;
            var trust = Control(page, "Trust", "Let pass", x + actionWidth + 16f, infoY, actionWidth, 56f, () => Decide(false));
            UIKit.AddButtonIcon(accuse, SpriteFactory.Emblem(GameIcon.Challenge), Theme.Text, 26f, 12f);
            UIKit.AddButtonIcon(trust, SpriteFactory.Emblem(GameIcon.Check), Theme.Text, 26f, 12f);
            Mark(accuse, decisions[example] == 1);
            Mark(trust, decisions[example] == 0);
            infoY += 74f;
            infoY += Paragraph(page, "Challenge Result", answered ? ChallengeExplanation() : ChallengeRisk(), 24, Theme.Text, x, infoY, width) + 16f;
            if (answered)
            {
                Control(page, "Example " + (1 - example), "Try another play", x, infoY, 256f, 48f, () =>
                {
                    example = 1 - example;
                    Render(false, config.StartingBullets > 0 ? "Accuse" : "Trust");
                });
                infoY += 52f;
            }
            return Mathf.Max(y + 306f, infoY);
        }

        void Decide(bool accuse)
        {
            if (accuse && config.StartingBullets == 0)
                return;
            decisions[example] = accuse ? 1 : 0;
            Render(true, accuse ? "Accuse" : "Trust");
        }

        string ChallengeRisk()
        {
            return "BLUFF! against a lie: opponent takes all 3 cards; "
                + (config.RefundBulletOnHit ? "bullet refunded." : "1 bullet spent.")
                + "\nBLUFF! against an honest play: you take " + (config.WrongShotTakesWholePile ? "all 3 cards" : "the challenged card") + "; 1 bullet spent."
                + "\nLet pass: cards stay hidden. No bullet spent.";
        }

        string ChallengeExplanation()
        {
            if (decisions[example] == 0)
                return "Let pass: cards stay hidden. Your bullets: " + config.StartingBullets + ". If no one challenges, the next turn begins and the pile stays.";
            if (example == 0)
                return "Bluff: Leaf is not Sun. The opponent takes all 3 cards and you lead a new round. "
                    + (config.RefundBulletOnHit ? "Bullet refunded. Remaining: " + config.StartingBullets + "." : "1 bullet spent. Remaining: " + (config.StartingBullets - 1) + ".");
            return "Honest: the card is Sun. " + (config.WrongShotTakesWholePile
                ? "You take all 3 cards. The opponent leads a new round. "
                : "You take the challenged card. The other 2 stay on the table and the round continues. ")
                + "1 bullet spent. Remaining: " + (config.StartingBullets - 1) + ".";
        }

        float BuildSpecialCards()
        {
            float y = PageHeading("Try a Mask", "Example claim: '1 Sun'. Click the Mask to see if it matches.");
            var mask = Control(page, "Try Mask", "", 4f, y + (maskTried ? 0f : 6f), 180f, 252f, () =>
            {
                maskTried = true;
                Render(true, "Try Mask");
            });
            CardFace(mask.transform, Element.Topeng, false, 0f, 0f, 180f);
            Mark(mask, maskTried);
            if (maskTried)
                TextAt(page, "Mask Result", "Match", 24, Theme.Gold, 4f, y + 266f, 180f, 34f, TextAnchor.MiddleCenter);
            PowerFace(page, PowerType.MataElang, 208f, y + 6f, 180f);
            float x = pageWidth < 900f ? 4f : 432f;
            float infoY = pageWidth < 900f ? y + 316f : y;
            float width = pageWidth - x - 4f;
            infoY += Paragraph(page, "Wild Rule", maskTried ? "Honest. A Mask matches all 6 elements." : "A Mask can represent any element.", 24, Theme.Text, x, infoY, width) + 12f;
            if (config.WildCount == 0)
                infoY += Paragraph(page, "Wild Disabled", "This is an example only. Masks are disabled in this deck.", 24, Theme.Muted, x, infoY, width) + 12f;
            infoY += Paragraph(page, "Power Heading", config.EnablePowers ? "One power. One use." : "Powers disabled", 32, Theme.Text, x, infoY, width, true) + 10f;
            infoY += Paragraph(page, "Power Rule", config.EnablePowers
                ? "Each player gets a random power. Eagle Eye peeks at the latest play during the challenge window."
                : "No powers are dealt. Eagle Eye is only an example here.", 24, Theme.Text, x, infoY, width) + 16f;
            Control(page, "All Powers", "Explore powers", x, infoY, 260f, 50f, () => OpenTopic(TopicNames.Length));
            return Mathf.Max(y + 306f, infoY + 54f);
        }

        float BuildReference()
        {
            if (topic >= TopicNames.Length)
                return BuildPowerReference(Powers.All[topic - TopicNames.Length]);
            float y = PageHeading(TopicNames[topic], topic == 0 ? "Rules reflect your current match settings." : "");
            y = BuildRuleMap(y);
            switch (topic)
            {
                case 0:
                    Rule(ref y, "Winning", "Empty your hand first. Your final play must pass unchallenged or prove honest. If caught bluffing, take the pile and keep playing.");
                    int minimum = config.TotalCards / config.PlayerCount;
                    string deal = config.TotalCards % config.PlayerCount == 0 ? minimum.ToString() : minimum + "–" + (minimum + 1);
                    Rule(ref y, "Setup", config.PlayerCount + " players. " + config.TotalCards + " cards dealt: " + deal + " cards per player. Starting bullets: " + config.StartingBullets + " bullets.");
                    Rule(ref y, "The deck", "6 elements x " + config.CopiesPerElement + " cards, plus " + config.WildCount + " Masks. Elements: Sun, Moon, Star, Lightning, Water, Leaf.");
                    Rule(ref y, "Power cards", PowerAvailability());
                    break;
                case 1:
                    Rule(ref y, "Play cards", "On your turn, select " + CardCount(config.MaxCardsPerPlay) + " and play face down. Everyone sees the count and claim; the cards stay hidden.");
                    Rule(ref y, "Round element", "The leader chooses one of 6 elements. Other players follow it or pass until the round ends. The leader must play.");
                    Rule(ref y, "Bluffing is allowed", "All cards match or are Masks: honest. Any card does not match: bluff. You cannot lie about the card count.");
                    Rule(ref y, "After a play", "Other players may challenge with BLUFF! or let the play pass. With no challenge, the next turn begins. Let pass does not skip your turn.");
                    break;
                case 2:
                    Rule(ref y, "BLUFF!", "Challenge only the latest opponent play during the challenge window. You need 1 bullet; the first challenger acts. You cannot challenge yourself or Shield. Starting bullets: " + config.StartingBullets + ".");
                    Rule(ref y, "Opponent bluffed", "The player takes the whole pile. The challenger leads a new round. " + (config.RefundBulletOnHit ? "The challenger's bullet is refunded." : "No refund: 1 bullet spent.") + " Mirror can change this outcome.");
                    Rule(ref y, "Opponent was honest", (config.WrongShotTakesWholePile
                        ? "The challenger takes the whole pile. The player leads a new round."
                        : "The challenger takes only the challenged cards. The rest of the pile stays; the next player takes their turn.") + " The challenger loses 1 bullet. If this was the player's final card, that player wins.");
                    Rule(ref y, "Let pass", "Let pass keeps the cards hidden and saves your bullet. Others can still challenge. If nobody challenges before time runs out, the next turn begins, or the player wins with an empty hand.");
                    break;
                case 3:
                    Rule(ref y, "Pass", "On your turn, pass to skip playing. The round leader cannot pass.");
                    Rule(ref y, "Burning the pile", "If all other players pass in a row (" + (config.PlayerCount - 1) + " passes for " + config.PlayerCount + " players), the whole pile is removed from the game.");
                    Rule(ref y, "Next leader", "The last player to play cards leads a new round and chooses an element. Every new play resets the pass count.");
                    Rule(ref y, "Passing vs letting pass", "Let pass declines a challenge. It does not count toward consecutive passes and does not burn the pile.");
                    break;
                default:
                    CardFace(page, Element.Topeng, false, 4f, y, 180f);
                    float textY = y;
                    float x = 216f;
                    textY += Paragraph(page, "Wild Explanation", "Masks match every claimed element. Mask itself cannot be chosen as the round element.", 24, Theme.Text, x, textY, pageWidth - x - 4f) + 20f;
                    textY += Paragraph(page, "Wild Example", config.MaxCardsPerPlay >= 2
                        ? "Mask + Sun claimed as '2 Sun': honest. A Mask does not make a Leaf card match Sun."
                        : "Mask claimed as '1 Sun': honest. This match allows only 1 card per play.", 24, Theme.Text, x, textY, pageWidth - x - 4f) + 20f;
                    textY += Paragraph(page, "Wild Count", "Current deck: " + config.WildCount + " Masks. " + (config.WildCount == 0 ? "No Masks are dealt." : "There are " + config.MatchesPerElement + " matching cards per element, including Masks."), 24, Theme.Text, x, textY, pageWidth - x - 4f);
                    y = Mathf.Max(y + 268f, textY);
                    break;
            }
            return y;
        }

        string PowerAvailability() => config.EnablePowers
            ? "Each player gets one different, secret random power. It can be used once per match."
            : "Powers are disabled for this match. You can still explore them in this guide.";

        float BuildPowerReference(PowerType power)
        {
            float y = PageHeading(PowerText.Name(power), config.EnablePowers ? "Single-use, when its conditions are met." : "Powers are disabled for this match.");
            y = BuildPowerDemo(power, y);
            Rule(ref y, "Effect", PowerText.Long(power));
            string limit;
            switch (power)
            {
                case PowerType.MataElang:
                    limit = "Only you see the cards. Peeking neither challenges nor spends a bullet. Choose BLUFF! or Let pass afterwards.";
                    break;
                case PowerType.Tameng:
                    limit = "Arm when playing cards. Keep at least 1 card in hand; your final play cannot be protected.";
                    break;
                case PowerType.Bakar:
                case PowerType.Hadiah:
                    limit = "Select " + CardCount(Powers.MaxPowerCards) + "; keep at least 1 card. This power does not end your turn. Play cards or pass as the round allows.";
                    break;
                case PowerType.Badai:
                    limit = "Cannot be used by the round leader. After burning the pile, lead a new round with any element.";
                    break;
                default:
                    limit = "Triggers only when your bluff is caught and you still have cards. The challenger takes the pile and loses a bullet with no refund. Does not protect your final play.";
                    break;
            }
            Rule(ref y, "Requirements", limit);
            return y;
        }

        void Rule(ref float y, string title, string text)
        {
            var panel = UIKit.CreatePanel(page, title + " Panel", Theme.PanelLight.WithAlpha(.65f), 8f);
            float width = pageWidth - 8f;
            Sprite icon = title == "Winning" || title == "Let pass" || title == "Opponent was honest" ? SpriteFactory.Emblem(GameIcon.Check) :
                title == "BLUFF!" || title == "Opponent bluffed" ? SpriteFactory.Emblem(GameIcon.Challenge) :
                title == "Bluffing is allowed" ? SpriteFactory.ElementEmblem(Element.Topeng) :
                title == "Burning the pile" ? SpriteFactory.PowerEmblem(PowerType.Bakar) :
                title == "Requirements" ? SpriteFactory.Emblem(GameIcon.Clock) : TopicIcon(topic);
            IconAt(panel.transform, icon, 16f, 20f, 30f, Color.white);
            float height = 16f + Paragraph(panel.transform, title + " Heading", title, 28, Theme.Text, 64f, 16f, width - 84f, true) + 6f;
            height += Paragraph(panel.transform, title + " Rule", text, 22, Theme.Text, 64f, height, width - 84f) + 18f;
            Place(panel.rectTransform, 4f, y, width, height);
            y += height + 12f;
        }

        float BuildRuleMap(float y)
        {
            string[] labels;
            Sprite[] icons;
            int[] practiceSteps;
            switch (topic)
            {
                case 0:
                case 1:
                    labels = new[] { "Select cards", "Make a claim", "Challenge a play" };
                    icons = new[] { SpriteFactory.Emblem(GameIcon.Cards), SpriteFactory.ElementEmblem(Element.Matahari), SpriteFactory.Emblem(GameIcon.Challenge) };
                    practiceSteps = new[] { 0, 1, 2 };
                    break;
                case 2:
                    labels = new[] { "Read the claim", "BLUFF!", "Let pass" };
                    icons = new[] { SpriteFactory.Emblem(GameIcon.Cards), SpriteFactory.Emblem(GameIcon.Challenge), SpriteFactory.Emblem(GameIcon.Check) };
                    practiceSteps = new[] { 1, 2, 2 };
                    break;
                case 3:
                    labels = new[] { "Follow the element", "Pass", "New round" };
                    icons = new[] { SpriteFactory.ElementEmblem(Element.Matahari), SpriteFactory.Emblem(GameIcon.Back), SpriteFactory.Emblem(GameIcon.Restart) };
                    practiceSteps = new[] { 1, 1, 1 };
                    break;
                default:
                    labels = new[] { "Mask", "Every element", "Try a card" };
                    icons = new[] { SpriteFactory.ElementEmblem(Element.Topeng), SpriteFactory.ElementEmblem(Element.Matahari), SpriteFactory.Emblem(GameIcon.Play) };
                    practiceSteps = new[] { 3, 3, 3 };
                    break;
            }
            float width = (pageWidth - 32f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                int demoStep = practiceSteps[i];
                var tile = Control(page, "Visual Step " + i, labels[i], 4f + i * (width + 12f), y, width, 110f, () => StartPractice(demoStep));
                IconAt(tile.transform, icons[i], (width - 36f) * .5f, 14f, 36f, Color.white);
                var text = tile.GetComponentInChildren<Text>();
                text.fontSize = 20;
                Place(text.rectTransform, 8f, 62f, width - 16f, 36f);
            }
            y += 120f;
            y += Paragraph(page, "Interactive Hint", "Click an icon to try it in practice.", 19, Theme.Muted, 4f, y, pageWidth - 8f) + 18f;
            return y;
        }

        void StartPractice(int targetStep)
        {
            if (targetStep > 0 && SelectedCount == 0) selected[0] = true;
            if (targetStep > 1 && !claim.HasValue) claim = Element.Matahari;
            unlockedStep = Math.Max(unlockedStep, targetStep);
            step = targetStep;
            referenceMode = false;
            Render();
        }

        float BuildPowerDemo(PowerType power, float y)
        {
            bool used = powerDemoUsed[(int)power];
            string before;
            string after;
            string action;
            Sprite source;
            Sprite result;
            switch (power)
            {
                case PowerType.MataElang:
                    before = "Claim: 1 Sun"; after = "Revealed: 1 Leaf"; action = "Try peeking";
                    source = SpriteFactory.Emblem(GameIcon.Cards); result = SpriteFactory.ElementEmblem(Element.Daun); break;
                case PowerType.Tameng:
                    before = "Unprotected play"; after = "Blocks BLUFF!"; action = "Arm & play";
                    source = SpriteFactory.Emblem(GameIcon.Cards); result = SpriteFactory.PowerEmblem(PowerType.Tameng); break;
                case PowerType.Bakar:
                    before = "Hand: 5 cards"; after = "Burn 2, keep 3"; action = "Try burning 2";
                    source = SpriteFactory.Emblem(GameIcon.Cards); result = SpriteFactory.PowerEmblem(PowerType.Bakar); break;
                case PowerType.Badai:
                    before = "Follow the round"; after = "Lead a new round"; action = "Try Storm";
                    source = SpriteFactory.Emblem(GameIcon.Cards); result = SpriteFactory.Emblem(GameIcon.Restart); break;
                case PowerType.Cermin:
                    before = "Your bluff is caught"; after = "Challenger takes pile"; action = "Try a challenge";
                    source = SpriteFactory.Emblem(GameIcon.Challenge); result = SpriteFactory.PowerEmblem(PowerType.Cermin); break;
                default:
                    before = "Hand: 5 cards"; after = "2 cards to opponent"; action = "Try giving 2";
                    source = SpriteFactory.Emblem(GameIcon.Cards); result = SpriteFactory.PowerEmblem(PowerType.Hadiah); break;
            }
            var board = UIKit.CreatePanel(page, "Power Demo", Theme.PanelLight, 10f);
            float width = pageWidth - 8f;
            Place(board.rectTransform, 4f, y, width, 276f);
            TextAt(board.transform, "Demo Label", "POWER DEMO", 16, Theme.Muted, 18f, 12f, width - 36f, 24f);
            float cellWidth = (width - 64f) / 3f;
            float[] centers = { 20f + cellWidth * .5f, width * .5f, width - 20f - cellWidth * .5f };
            IconAt(board.transform, source, centers[0] - 23f, 56f, 46f, Color.white);
            var activate = Control(board.transform, "Demo Activate", "", centers[1] - 40f, 42f, 80f, 80f, () =>
            {
                powerDemoUsed[(int)power] = true;
                Render(true, "Demo Reset");
            });
            activate.interactable = !used;
            var ring = UIKit.CreateImage(activate.transform, "Charge Ring", SpriteFactory.Ring, used ? Theme.Disabled : Theme.Gold);
            ring.rectTransform.Stretch(2f, 2f, 2f, 2f);
            IconAt(activate.transform, SpriteFactory.PowerEmblem(power), 12f, 12f, 56f, Color.white.WithAlpha(used ? .45f : 1f));
            IconAt(board.transform, used ? result : SpriteFactory.Emblem(GameIcon.Clock), centers[2] - 23f, 56f, 46f, used ? Theme.Green : Theme.Muted);
            TextAt(board.transform, "Before", before, 18, Theme.Text, centers[0] - cellWidth * .5f, 132f, cellWidth, 54f, TextAnchor.UpperCenter);
            TextAt(board.transform, "Demo Charge", used ? "Used: 0/1" : power == PowerType.Cermin ? "Passive: 1/1" : "Ready: 1/1", 18, used ? Theme.Muted : Theme.Gold,
                centers[1] - cellWidth * .5f, 132f, cellWidth, 54f, TextAnchor.UpperCenter);
            TextAt(board.transform, "After", used ? after : "Click to see the effect", 18, used ? Theme.Green : Theme.Muted,
                centers[2] - cellWidth * .5f, 132f, cellWidth, 54f, TextAnchor.UpperCenter);
            var use = Control(board.transform, "Demo Use", action, 18f, 204f, width - 194f, 52f, () =>
            {
                powerDemoUsed[(int)power] = true;
                Render(true, "Demo Reset");
            }, true);
            use.interactable = !used;
            UIKit.AddButtonIcon(use, SpriteFactory.PowerEmblem(power), Color.white, 34f, 10f);
            var reset = Control(board.transform, "Demo Reset", "Reset", width - 158f, 204f, 140f, 52f, () =>
            {
                powerDemoUsed[(int)power] = false;
                Render(true, "Demo Activate");
            });
            UIKit.AddButtonIcon(reset, SpriteFactory.Emblem(GameIcon.Restart), Theme.Text, 26f, 12f);
            y += 288f;
            y += Paragraph(page, "Demo Notice", "Practice only. Powers do not recharge during a match.", 19, Theme.Muted, 4f, y, pageWidth - 8f) + 18f;
            return y;
        }

        void CardFace(Transform parent, Element element, bool faceDown, float x, float y, float width)
        {
            float height = width * 1.4f;
            var rect = UIKit.CreateRect("Sample Card", parent);
            Place(rect, x, y, width, height);
            var art = rect.gameObject.AddComponent<RawImage>();
            art.texture = faceDown ? CardArt.Back : CardArt.Face(element);
            art.raycastTarget = false;
            if (!faceDown)
                TextAt(rect, "Element", Theme.ElementName(element), 24, Theme.TextDark, 8f, height - 58f, width - 16f, 40f, TextAnchor.MiddleCenter);
        }

        void PowerFace(Transform parent, PowerType power, float x, float y, float width)
        {
            float height = width * 1.4f;
            var rect = UIKit.CreatePanel(parent, "Power Card", Theme.CardFace, 8f).rectTransform;
            Place(rect, x, y, width, height);
            var art = UIKit.CreateImage(rect, "Icon", SpriteFactory.PowerEmblem(power), Color.white);
            art.preserveAspect = true;
            Place(art.rectTransform, 28f, 32f, width - 56f, width - 56f);
            TextAt(rect, "Power Name", PowerText.Name(power), 24, Theme.TextDark, 8f, height - 58f, width - 16f, 40f, TextAnchor.MiddleCenter);
        }

        ScrollRect ScrollArea(Transform parent, string name, float left, float right, float top, float bottom)
        {
            var root = UIKit.CreateRect(name, parent);
            root.Stretch(left, right, top, bottom);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = UIKit.CreateImage(root, "Viewport", null, Color.clear, true).rectTransform;
            viewport.Stretch(0f, 24f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UIKit.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 42f;
            var track = UIKit.CreateImage(root, "Scrollbar", null, Theme.Background, true);
            track.rectTransform.anchorMin = new Vector2(1f, 0f);
            track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.pivot = new Vector2(1f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(12f, 0f);
            track.rectTransform.anchoredPosition = Vector2.zero;
            var handle = UIKit.CreateImage(track.transform, "Handle", null, Theme.Gold, true);
            handle.rectTransform.Stretch();
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        Button FooterButton(string name, string label, bool primary, Action action)
        {
            var button = Control(view, name, label, 0f, 0f, primary ? 292f : 168f, 56f, action, primary);
            button.GetComponent<RectTransform>().Place(primary ? UIKit.BottomRight : UIKit.BottomLeft,
                new Vector2(primary ? -186f : 124f, 42f), new Vector2(primary ? 292f : 168f, 56f));
            UIKit.AddButtonIcon(button, primary ? label == "Done" ? SpriteFactory.Emblem(GameIcon.Check) : SpriteFactory.Emblem(GameIcon.Play) : SpriteFactory.Emblem(GameIcon.Back),
                Color.white, 26f);
            if (!primary) button.GetComponentInChildren<Text>().fontSize = 21;
            return button;
        }

        Button Control(Transform parent, string name, string label, float x, float y, float width, float height, Action action, bool primary = false)
        {
            var button = UIKit.CreateButton(parent, name, label, new Vector2(width, height),
                primary ? Theme.Text : Theme.PanelLight, primary ? Theme.TextDark : Theme.Text,
                () => { if (IsOpen) action(); }, 24, 6f);
            var fx = button.GetComponent<ButtonFx>();
            if (fx != null)
                fx.enabled = false;
            Place(button.GetComponent<RectTransform>(), x, y, width, height);
            var outline = UIKit.CreateOutline(button.transform, "KeyboardFocus", Theme.Text, 5f);
            outline.rectTransform.Stretch(-3f, -3f, -3f, -3f);
            outline.enabled = false;
            controls.Add(button);
            focusOutlines.Add(outline);
            return button;
        }

        static void Mark(Button button, bool active)
        {
            var outline = UIKit.CreateOutline(button.transform, "Selected", Theme.Gold, 5f);
            outline.rectTransform.Stretch(-1f, -1f, -1f, -1f);
            outline.enabled = active;
            var focus = button.transform.Find("KeyboardFocus");
            if (focus != null)
                focus.SetAsLastSibling();
        }

        static float Paragraph(Transform parent, string name, string content, int size, Color color, float x, float y, float width, bool heading = false)
        {
            if (string.IsNullOrEmpty(content))
                return 0f;
            var text = heading
                ? UIKit.CreateHeading(parent, name, content, size, color, TextAnchor.UpperLeft)
                : UIKit.CreateText(parent, name, content, size, color, TextAnchor.UpperLeft);
            Place(text.rectTransform, x, y, width, size * 2f);
            float height = Mathf.Ceil(text.preferredHeight) + 4f;
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Place(text.rectTransform, x, y, width, height);
            return height;
        }

        static Text TextAt(Transform parent, string name, string content, int size, Color color, float x, float y, float width, float height, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var text = UIKit.CreateText(parent, name, content, size, color, anchor);
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.Place(UIKit.TopLeft, new Vector2(x + width * 0.5f, -y - height * 0.5f), new Vector2(width, height));
        }

        bool CanAdvance()
        {
            switch (step)
            {
                case 0: return SelectedCount > 0;
                case 1: return SelectedCount > 0 && claim.HasValue;
                case 2: return decisions[0] >= 0 || decisions[1] >= 0;
                default: return maskTried;
            }
        }

        void Advance()
        {
            if (!CanAdvance())
                return;
            if (step == 3)
            {
                CompleteOrSkip();
                return;
            }
            step++;
            unlockedStep = Math.Max(unlockedStep, step);
            Render();
        }

        void Focus(Button button, bool reveal = true)
        {
            if (eventSystem == null || button == null || !button.IsActive() || !button.IsInteractable())
                return;
            eventSystem.SetSelectedGameObject(button.gameObject);
            if (reveal)
                EnsureFocusVisible(button.GetComponent<RectTransform>());
            RefreshFocus();
        }

        void FocusFirst()
        {
            Focus(controls.Find(button => button != null && button.IsActive() && button.IsInteractable()));
        }

        void RefreshFocus()
        {
            if (eventSystem == null)
                return;
            for (int i = 0; i < controls.Count; i++)
                focusOutlines[i].enabled = controls[i].gameObject == eventSystem.currentSelectedGameObject;
        }

        void EnsureFocusVisible(RectTransform target)
        {
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll == null || !target.IsChildOf(scroll.content))
                return;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target);
            var viewport = scroll.viewport.rect;
            float offset = bounds.max.y > viewport.yMax - 6f ? viewport.yMax - 6f - bounds.max.y
                : bounds.min.y < viewport.yMin + 6f ? viewport.yMin + 6f - bounds.min.y : 0f;
            var position = scroll.content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + offset, 0f, Mathf.Max(0f, scroll.content.rect.height - viewport.height));
            scroll.content.anchoredPosition = position;
            scroll.StopMovement();
        }

        void MoveFocus(int direction)
        {
            if (eventSystem == null)
                return;
            int index = controls.FindIndex(button => button != null && button.gameObject == eventSystem.currentSelectedGameObject);
            for (int i = 0; i < controls.Count; i++)
            {
                index = (index + direction + controls.Count) % controls.Count;
                var button = controls[index];
                if (button != null && button.IsActive() && button.IsInteractable())
                {
                    Focus(button);
                    return;
                }
            }
        }

        void MoveSpatial(Vector2 direction)
        {
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
            {
                FocusFirst();
                return;
            }
            var from = eventSystem.currentSelectedGameObject.GetComponent<RectTransform>();
            if (from == null)
                return;
            Vector2 origin = from.TransformPoint(from.rect.center);
            float best = float.PositiveInfinity;
            Button candidate = null;
            foreach (var button in controls)
            {
                if (!button.IsActive() || !button.IsInteractable() || button.gameObject == from.gameObject)
                    continue;
                var rect = button.GetComponent<RectTransform>();
                Vector2 delta = (Vector2)rect.TransformPoint(rect.rect.center) - origin;
                float along = Vector2.Dot(delta, direction);
                if (along <= 1f)
                    continue;
                float across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                float score = along + across * 4f;
                if (score < best)
                {
                    best = score;
                    candidate = button;
                }
            }
            if (candidate != null)
                Focus(candidate);
        }

        void ScrollKeyboard(int pageDirection, int edge)
        {
            var scroll = bodyScroll;
            if (scroll == null)
                return;
            float range = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
            var position = scroll.content.anchoredPosition;
            position.y = edge < 0 ? 0f : edge > 0 ? range : Mathf.Clamp(position.y + pageDirection * scroll.viewport.rect.height * 0.8f, 0f, range);
            scroll.content.anchoredPosition = position;
            scroll.StopMovement();
        }

        void Update()
        {
            if (!IsOpen)
                return;
            if (owner == null || !owner.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }
            if ((((RectTransform)transform).rect.size - viewSize).sqrMagnitude > 1f)
                Render(true);
            if (!Application.isFocused)
                return;
            if (eventSystem != null)
            {
                eventSystem.sendNavigationEvents = false;
                var focused = eventSystem.currentSelectedGameObject;
                if (focused == null || !focused.activeInHierarchy || !focused.transform.IsChildOf(transform))
                    FocusFirst();
                RefreshFocus();
            }
            if (builtFrame == Time.frameCount)
                return;
            if (InputCompat.EscapePressed())
            {
                CompleteOrSkip();
                return;
            }
            int tab = 0;
            int pageDirection = 0;
            int edge = 0;
            Vector2 direction = Vector2.zero;
            bool submit = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                    tab = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? -1 : 1;
                if (keyboard.leftArrowKey.wasPressedThisFrame) direction = Vector2.left;
                else if (keyboard.rightArrowKey.wasPressedThisFrame) direction = Vector2.right;
                else if (keyboard.upArrowKey.wasPressedThisFrame) direction = Vector2.up;
                else if (keyboard.downArrowKey.wasPressedThisFrame) direction = Vector2.down;
                if (keyboard.pageDownKey.wasPressedThisFrame) pageDirection = 1;
                else if (keyboard.pageUpKey.wasPressedThisFrame) pageDirection = -1;
                if (keyboard.homeKey.wasPressedThisFrame) edge = -1;
                else if (keyboard.endKey.wasPressedThisFrame) edge = 1;
                submit = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab))
                tab = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) direction = Vector2.left;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) direction = Vector2.right;
            else if (Input.GetKeyDown(KeyCode.UpArrow)) direction = Vector2.up;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) direction = Vector2.down;
            if (Input.GetKeyDown(KeyCode.PageDown)) pageDirection = 1;
            else if (Input.GetKeyDown(KeyCode.PageUp)) pageDirection = -1;
            if (Input.GetKeyDown(KeyCode.Home)) edge = -1;
            else if (Input.GetKeyDown(KeyCode.End)) edge = 1;
            submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
            if (tab != 0)
                MoveFocus(tab);
            else if (direction != Vector2.zero)
                MoveSpatial(direction);
            else if (pageDirection != 0 || edge != 0)
                ScrollKeyboard(pageDirection, edge);
            else if (submit && eventSystem != null)
            {
                var button = controls.Find(value => value != null && value.gameObject == eventSystem.currentSelectedGameObject);
                if (button != null && button.IsActive() && button.IsInteractable())
                    button.onClick.Invoke();
            }
        }

        void OnDisable()
        {
            Finish();
            if (!closing)
            {
                closing = true;
                Destroy(gameObject);
            }
        }

        void OnDestroy() => Finish();

        void Finish()
        {
            if (finished)
                return;
            finished = true;
            closedFrame = Time.frameCount;
            if (current == this)
                current = null;
            if (ownsFocus && eventSystem != null)
            {
                ownsFocus = false;
                eventSystem.sendNavigationEvents = previousNavigation;
                var focused = eventSystem.currentSelectedGameObject;
                if (focused == null || focused.transform.IsChildOf(transform))
                    eventSystem.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
            }
            var callback = onClosed;
            onClosed = null;
            if (callback == null)
                return;
            foreach (Action listener in callback.GetInvocationList())
            {
                try
                {
                    listener();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
