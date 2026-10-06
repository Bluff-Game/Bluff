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
        static readonly Vector2 PanelSize = new Vector2(1080f, 800f);
        static readonly Vector2 CardSize = new Vector2(160f, 224f);
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
        readonly List<Button> controls = new List<Button>();
        readonly List<Image> focusOutlines = new List<Image>();
        readonly List<Image> selectionOutlines = new List<Image>();
        readonly List<Text> selectionLabels = new List<Text>();
        readonly List<Image> claimOutlines = new List<Image>();

        RectTransform panel;
        RectTransform page;
        Text progress;
        Text heading;
        Text introduction;
        Text feedback;
        Text selectionSummary;
        Text nextLabel;
        Button next;
        Button back;
        Button skip;
        EventSystem eventSystem;
        GameObject previousSelection;
        bool previousNavigation;
        bool ownsFocus;
        bool closing;
        bool finished;
        int step;
        int example;
        int builtFrame;
        Element? claim;
        GameConfig config;
        Action onClosed;

        public bool IsOpen => !closing && !finished && isActiveAndEnabled;
        public static bool IsShowing => (current != null && current.isActiveAndEnabled) || closedFrame == Time.frameCount;

        public static GameTutorial Show(RectTransform parent, System.Action onClosed)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            if (!parent.gameObject.activeInHierarchy)
                throw new ArgumentException("Tutorial memerlukan parent yang aktif.", nameof(parent));
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
                throw new ArgumentException("Tutorial memerlukan parent di dalam Canvas.", nameof(parent));
            UIKit.EnsureEventSystem();
            var root = UIKit.CreateRect("Modal Tutorial", parentCanvas.rootCanvas.transform);
            root.Stretch();
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;
            root.gameObject.AddComponent<GraphicRaycaster>();
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            group.blocksRaycasts = true;
            group.interactable = true;
            var tutorial = root.gameObject.AddComponent<GameTutorial>();
            tutorial.onClosed = onClosed;
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
                tutorial.Build();
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
            step = example = 0;
            RenderStep();
        }

        public void Close()
        {
            if (closing || finished)
                return;
            closing = true;
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

        void Build()
        {
            var dim = UIKit.CreateImage(transform, "Backdrop", null, new Color(0.02f, 0.03f, 0.025f, 0.88f), true);
            dim.rectTransform.Stretch();
            panel = UIKit.CreatePanel(transform, "Panel", Theme.Panel, 12f, true).rectTransform;
            panel.Place(UIKit.Center, Vector2.zero, PanelSize);
            UIKit.CreateOutline(panel, "Border", Theme.Border, 12f).rectTransform.Stretch();
            progress = TextAt(panel, "Progress", "", 24, Theme.Muted, 48f, 28f, 760f, 36f);
            heading = UIKit.CreateHeading(panel, "Heading", "", 52, Theme.Text, TextAnchor.MiddleLeft);
            Place(heading.rectTransform, 48f, 78f, 984f, 66f);
            introduction = TextAt(panel, "Introduction", "", 24, Theme.Text, 48f, 148f, 984f, 62f);
            skip = Control(panel, "Skip", "Lewati", 864f, 24f, 168f, 48f, CompleteOrSkip);
            back = Control(panel, "Back", "Kembali", 48f, 712f, 168f, 56f, () =>
            {
                if (step <= 0)
                    return;
                step--;
                RenderStep();
            });
            next = Control(panel, "Next", "Lanjut", 808f, 712f, 224f, 56f, Advance, true);
            nextLabel = next.GetComponentInChildren<Text>();
            TextAt(panel, "PracticeNotice", "Latihan lokal · tidak mengubah permainan", 20, Theme.Muted, 244f, 720f, 544f, 40f);
            FitPanel();
            RenderStep();
        }

        void RenderStep()
        {
            if (page != null)
            {
                page.gameObject.SetActive(false);
                Destroy(page.gameObject);
            }
            foreach (var outline in focusOutlines)
                if (outline != null)
                    outline.enabled = false;
            controls.Clear();
            focusOutlines.Clear();
            selectionOutlines.Clear();
            selectionLabels.Clear();
            claimOutlines.Clear();
            page = UIKit.CreateRect("Step " + (step + 1), panel);
            Place(page, 48f, 216f, 984f, 474f);
            progress.text = (step + 1) + "/4";
            back.gameObject.SetActive(step > 0);
            nextLabel.text = step == 3 ? "Mulai bermain" : "Lanjut";
            switch (step)
            {
                case 0: BuildHand(); break;
                case 1: BuildClaim(); break;
                case 2: BuildChallenge(); break;
                default: BuildSpecialCards(); break;
            }
            if (step > 0)
                Register(back);
            Register(next);
            Register(skip);
            next.interactable = CanAdvance();
            builtFrame = Time.frameCount;
            FocusFirst();
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

        void BuildHand()
        {
            heading.text = "Habiskan kartu di tangan";
            introduction.text = "Kosongkan tangan lebih dulu. Menang jika taruhan terakhir tidak dituduh, atau dituduh tetapi terbukti jujur.";
            string count = SelectionLimit == 1 ? "1 kartu" : "1–" + SelectionLimit + " kartu";
            TextAt(page, "Task", "Pilih " + count + " dari tangan contoh ini.", 26, Theme.Text, 0f, 0f, 984f, 40f);
            for (int i = 0; i < sampleHand.Length; i++)
            {
                int index = i;
                var button = Control(page, "Sample " + i, "", i * 178f, 64f, CardSize.x, CardSize.y, () => ToggleCard(index));
                CardFace(button.transform, sampleHand[i].Element, false, 0f, 0f);
                var outline = UIKit.CreateOutline(button.transform, "Selected", Theme.Gold, 10f);
                outline.rectTransform.Stretch(-3f, -3f, -3f, -3f);
                selectionOutlines.Add(outline);
                selectionLabels.Add(TextAt(page, "Selection " + i, "", 22, Theme.Text, i * 178f, 296f, 160f, 34f, TextAnchor.MiddleCenter));
            }
            var goal = UIKit.CreateHeading(page, "Goal", "Taruh tertutup,\nsebutkan klaimmu.", 36, Theme.Text, TextAnchor.UpperLeft);
            Place(goal.rectTransform, 586f, 74f, 398f, 98f);
            TextAt(page, "PlayRule", "Saat giliranmu, taruh 1–" + config.MaxCardsPerPlay + " kartu tertutup.\nLawan hanya melihat jumlah dan klaimmu, bukan isi kartu.", 24, Theme.Text, 586f, 190f, 398f, 130f);
            feedback = Feedback(352f, 116f);
            RefreshSelection();
        }

        void ToggleCard(int index)
        {
            if (!selected[index] && SelectedCount >= SelectionLimit)
            {
                feedback.text = "Maksimal " + SelectionLimit + " kartu dalam contoh ini. Klik kartu yang dipilih untuk membatalkannya.";
                return;
            }
            selected[index] = !selected[index];
            claim = null;
            RefreshSelection();
            next.interactable = CanAdvance();
        }

        void RefreshSelection()
        {
            for (int i = 0; i < selected.Length; i++)
            {
                selectionOutlines[i].enabled = selected[i];
                selectionLabels[i].text = selected[i] ? "Dipilih" : "Pilih";
            }
            int count = SelectedCount;
            feedback.text = count == 0
                ? "Klik kartu untuk memilihnya. Kamu akan menentukan klaim pada langkah berikutnya."
                : count + " kartu dipilih; " + (sampleHand.Length - count) + " tersisa di tangan contoh.\nBelum tentu aman: lawan masih boleh menuduh taruhan terakhirmu.";
        }

        void BuildClaim()
        {
            heading.text = "Kartu asli, klaim pilihanmu";
            introduction.text = "Kamu membuka ronde baru: pilih salah satu dari 6 elemen. Klaimmu tidak harus sama dengan kartu.";
            TextAt(page, "SelectedTitle", "Kartu yang kamu pilih", 24, Theme.Muted, 0f, 0f, 550f, 38f);
            int position = 0;
            for (int i = 0; i < sampleHand.Length; i++)
            {
                if (selected[i])
                    CardFace(page, sampleHand[i].Element, false, position++ * 178f, 54f);
            }
            TextAt(page, "ClaimTitle", "Kamu mengaku membawa…", 24, Theme.Text, 568f, 0f, 416f, 38f);
            for (int i = 0; i < Elements.Claimable.Count; i++)
            {
                Element element = Elements.Claimable[i];
                var button = Control(page, "Claim " + element, "", 568f + i % 2 * 214f, 54f + i / 2 * 82f, 202f, 70f, () => ChooseClaim(element));
                var icon = UIKit.CreateImage(button.transform, "Icon", SpriteFactory.ElementIcon(element), Theme.Text);
                icon.preserveAspect = true;
                Place(icon.rectTransform, 14f, 20f, 30f, 30f);
                TextAt(button.transform, "Name", Theme.ElementName(element), 24, Theme.Text, 54f, 12f, 136f, 46f);
                var outline = UIKit.CreateOutline(button.transform, "Chosen", Theme.Gold, 8f);
                outline.rectTransform.Stretch(-2f, -2f, -2f, -2f);
                claimOutlines.Add(outline);
            }
            selectionSummary = TextAt(page, "ClaimPreview", "", 24, Theme.Text, 0f, 292f, 984f, 40f);
            feedback = Feedback(346f, 122f);
            RefreshClaim();
        }

        void ChooseClaim(Element element)
        {
            claim = element;
            RefreshClaim();
            next.interactable = CanAdvance();
        }

        void RefreshClaim()
        {
            for (int i = 0; i < claimOutlines.Count; i++)
                claimOutlines[i].enabled = claim.HasValue && claim.Value == Elements.Claimable[i];
            if (!claim.HasValue)
            {
                selectionSummary.text = "Pilih elemen untuk melihat apakah klaimmu jujur atau bohong.";
                feedback.text = "Pada ronde yang sudah berjalan, wajib ikuti elemen ronde.\nKamu boleh Pass, tetapi pembuka ronde wajib menaruh kartu.";
                return;
            }
            bool honest = true;
            for (int i = 0; i < sampleHand.Length; i++)
                if (selected[i] && !sampleHand[i].Matches(claim.Value))
                    honest = false;
            selectionSummary.text = "Klaimmu: «" + SelectedCount + " " + Theme.ElementName(claim.Value) + "».";
            feedback.text = (honest
                ? "Jujur: semua kartu cocok dengan klaimmu."
                : "Bohong: ada kartu yang tidak cocok. Ini boleh, tetapi berisiko dituduh.")
                + "\nSesudahmu, klaim tetap " + Theme.ElementName(claim.Value) + " sampai ronde berakhir; pemain berikutnya boleh Pass.";
        }

        void BuildChallenge()
        {
            heading.text = "Tuduh bohong atau percaya?";
            introduction.text = "Coba dua taruhan contoh. Dalam permainan, hanya taruhan terbaru yang bisa dituduh selama jendela tuduhan.";
            for (int i = 0; i < decisions.Length; i++)
            {
                int index = i;
                string label = "Contoh " + (i + 1) + (decisions[i] >= 0 ? " · selesai" : "");
                var tab = Control(page, "Example " + i, label, i * 230f, 0f, 218f, 46f, () =>
                {
                    example = index;
                    RenderStep();
                });
                UIKit.CreateOutline(tab.transform, "Active", Theme.Gold, 8f).rectTransform.Stretch();
                tab.transform.Find("Active").gameObject.SetActive(example == i);
            }
            TextAt(page, "NoPower", "Contoh tanpa Kartu Kuasa", 22, Theme.Muted, 530f, 0f, 454f, 46f, TextAnchor.MiddleRight);
            bool answered = decisions[example] >= 0;
            CardFace(page, example == 0 ? Element.Daun : Element.Matahari, !answered, 56f, 58f);
            TextAt(page, "RevealNotice", answered ? "Dibuka untuk latihan" : "Kartu contoh tertutup", 20, Theme.Muted, 0f, 284f, 272f, 30f, TextAnchor.MiddleCenter);
            var title = UIKit.CreateHeading(page, "OpponentClaim", "Lawan: «1 Matahari»", 38, Theme.Text, TextAnchor.MiddleLeft);
            Place(title.rectTransform, 304f, 70f, 680f, 52f);
            TextAt(page, "Pile", "2 kartu sebelumnya + 1 kartu ini = 3 di tumpukan.\nLawan masih punya kartu di tangan.", 24, Theme.Text, 304f, 132f, 680f, 76f);
            TextAt(page, "Bullets", "Peluru latihan: " + config.StartingBullets + (config.StartingBullets == 0 ? " · kamu hanya bisa percaya." : " · tuduhan memakai 1 peluru."), 22, Theme.Muted, 304f, 206f, 680f, 40f);
            var accuse = Control(page, "Accuse", "Tuduh bohong", 304f, 264f, 316f, 56f, () => Decide(true));
            accuse.interactable = config.StartingBullets > 0;
            Control(page, "Trust", "Percaya", 638f, 264f, 316f, 56f, () => Decide(false));
            feedback = Feedback(330f, 138f);
            feedback.fontSize = 22;
            feedback.text = answered ? ChallengeExplanation() : "Pilih tindakan, lalu lihat isi kartu contoh dan akibatnya.\nSelesaikan kedua contoh sebelum melanjutkan.";
        }

        void Decide(bool accuse)
        {
            if (accuse && config.StartingBullets == 0)
                return;
            decisions[example] = accuse ? 1 : 0;
            RenderStep();
        }

        string ChallengeExplanation()
        {
            bool honest = example == 1;
            bool accused = decisions[example] == 1;
            string result;
            if (!accused)
            {
                result = honest ? "Percaya: Matahari memang jujur." : "Percaya: Daun bukan Matahari; bohong lolos.";
                result += " Tanpa tuduhan lain, giliran lanjut; pelurumu tetap " + config.StartingBullets + ".";
                if (config.StartingBullets == 0)
                    result += "\nTanpa peluru, kamu tidak bisa menuduh.";
                else if (honest)
                    result += config.WrongShotTakesWholePile
                        ? "\nJika dituduh: kamu ambil semua 3 kartu; 1 peluru habis."
                        : "\nJika dituduh: kamu ambil 1 kartu tertuduh; 1 peluru habis.";
                else
                    result += "\nJika dituduh: lawan ambil semua 3 kartu; " + (config.RefundBulletOnHit ? "peluru kembali." : "1 peluru habis.");
            }
            else if (!honest)
            {
                result = "Tuduhan tepat: isinya Daun. Lawan mengambil semua 3 kartu; kamu membuka ronde baru. ";
                result += config.RefundBulletOnHit
                    ? "1 peluru dikembalikan: tetap " + config.StartingBullets + "."
                    : "Refund nonaktif: 1 peluru habis, tersisa " + (config.StartingBullets - 1) + ".";
            }
            else
            {
                result = "Salah tuduh: isinya Matahari. ";
                result += config.WrongShotTakesWholePile
                    ? "Kamu mengambil semua 3 kartu; lawan membuka ronde baru. "
                    : "Kamu hanya mengambil 1 kartu yang dituduh; ronde berlanjut. ";
                result += "1 peluru habis, tersisa " + (config.StartingBullets - 1) + ".";
            }
            return result + "\n" + (decisions[0] < 0 || decisions[1] < 0
                ? "Coba contoh lainnya. Di game, percaya tidak membuka kartu."
                : "Kedua contoh selesai. Di game, percaya tidak membuka kartu.");
        }

        void BuildSpecialCards()
        {
            string wildExample = config.MaxCardsPerPlay >= 2
                ? "Topeng + Matahari, diklaim «2 Matahari» = jujur."
                : "Topeng, diklaim «1 Matahari» = jujur.";
            heading.text = "Topeng dan Kartu Kuasa";
            introduction.text = "Topeng membantu klaimmu. Kartu Kuasa punya aturan sendiri—baca efeknya sebelum dipakai.";
            CardFace(page, Element.Topeng, false, 0f, 8f);
            var wildTitle = UIKit.CreateHeading(page, "WildTitle", "Topeng selalu cocok", 36, Theme.Text, TextAnchor.MiddleLeft);
            Place(wildTitle.rectTransform, 192f, 8f, 792f, 50f);
            TextAt(page, "WildRule", "Topeng sah untuk semua 6 elemen.\nContoh: " + wildExample + "\nTopeng bukan elemen yang bisa dipilih sebagai klaim."
                + (config.WildCount == 0 ? "\nSetelan ini tidak memasukkan Topeng ke dek." : ""), 24, Theme.Text, 192f, 68f, 792f, 154f);
            var powerRect = UIKit.CreatePanel(page, "PowerExample", Theme.CardFace, 10f).rectTransform;
            Place(powerRect, 0f, 248f, 160f, 224f);
            var art = UIKit.CreateImage(powerRect, "Icon", SpriteFactory.PowerIcon(PowerType.MataElang), Theme.TextDark);
            art.preserveAspect = true;
            Place(art.rectTransform, 30f, 36f, 100f, 100f);
            TextAt(powerRect, "Name", PowerText.Name(PowerType.MataElang), 22, Theme.TextDark, 8f, 160f, 144f, 42f, TextAnchor.MiddleCenter);
            var powerTitle = UIKit.CreateHeading(page, "PowerTitle", config.EnablePowers ? "Satu kuasa, sekali per permainan" : "Kartu Kuasa dinonaktifkan", 36, Theme.Text, TextAnchor.MiddleLeft);
            Place(powerTitle.rectTransform, 192f, 248f, 792f, 50f);
            TextAt(page, "PowerRule", (config.EnablePowers
                ? "Setiap pemain mendapat 1 kuasa acak, hanya sekali pakai."
                : "Tidak ada kuasa dibagikan dengan setelan ini.")
                + "\nContoh Mata Elang: intip taruhan terbaru lawan saat jendela tuduhan.\nKuasa lain punya waktu dan batas berbeda; Cermin aktif otomatis.\nLatihan ini tidak memakai atau memberikan kuasa sungguhan.", 23, Theme.Text, 192f, 306f, 792f, 166f);
        }

        void CardFace(Transform parent, Element element, bool faceDown, float x, float y)
        {
            var rect = UIKit.CreateRect("Sample Card", parent);
            Place(rect, x, y, CardSize.x, CardSize.y);
            var art = rect.gameObject.AddComponent<RawImage>();
            art.texture = faceDown ? CardArt.Back : CardArt.Face(element);
            art.raycastTarget = false;
            if (!faceDown)
                TextAt(rect, "Element", Theme.ElementName(element), 22, element.IsWild() ? Theme.Text : Theme.TextDark,
                    8f, 173f, 144f, 34f, TextAnchor.MiddleCenter);
        }

        Text Feedback(float y, float height)
        {
            var box = UIKit.CreatePanel(page, "Feedback", Theme.PanelLight, 8f).rectTransform;
            Place(box, 0f, y, 984f, height);
            var text = UIKit.CreateText(box, "Explanation", "", 24, Theme.Text, TextAnchor.MiddleLeft);
            text.rectTransform.Stretch(20f, 20f, 12f, 12f);
            return text;
        }

        Button Control(Transform parent, string name, string label, float x, float y, float width, float height, Action action, bool primary = false)
        {
            var button = UIKit.CreateButton(parent, name, label, new Vector2(width, height),
                primary ? Theme.Text : Theme.PanelLight, primary ? Theme.TextDark : Theme.Text,
                () => { if (IsOpen) action(); }, 24, 8f);
            Place(button.GetComponent<RectTransform>(), x, y, width, height);
            var outline = UIKit.CreateOutline(button.transform, "KeyboardFocus", Theme.Text, 8f);
            outline.rectTransform.Stretch(-5f, -5f, -5f, -5f);
            outline.enabled = false;
            Register(button);
            return button;
        }

        void Register(Button button)
        {
            controls.Add(button);
            focusOutlines.Add(button.transform.Find("KeyboardFocus").GetComponent<Image>());
        }

        static Text TextAt(Transform parent, string name, string content, int size, Color color,
            float x, float y, float width, float height, TextAnchor anchor = TextAnchor.MiddleLeft)
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
                case 1: return claim.HasValue;
                case 2: return decisions[0] >= 0 && decisions[1] >= 0;
                default: return true;
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
            RenderStep();
        }

        void FitPanel()
        {
            if (panel == null)
                return;
            var bounds = ((RectTransform)transform).rect;
            float scale = Mathf.Min(1f, (bounds.width - 48f) / PanelSize.x, (bounds.height - 48f) / PanelSize.y);
            panel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        void FocusFirst()
        {
            if (eventSystem == null)
                return;
            foreach (var button in controls)
            {
                if (button != null && button.IsActive() && button.IsInteractable())
                {
                    eventSystem.SetSelectedGameObject(button.gameObject);
                    return;
                }
            }
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
                    eventSystem.SetSelectedGameObject(button.gameObject);
                    return;
                }
            }
        }

        void Update()
        {
            if (!IsOpen)
                return;
            FitPanel();
            if (!Application.isFocused)
                return;
            if (eventSystem != null)
            {
                eventSystem.sendNavigationEvents = false;
                var focused = eventSystem.currentSelectedGameObject;
                if (focused == null || !focused.transform.IsChildOf(transform))
                    FocusFirst();
                for (int i = 0; i < controls.Count; i++)
                    focusOutlines[i].enabled = controls[i].gameObject == eventSystem.currentSelectedGameObject;
            }
            if (builtFrame == Time.frameCount)
                return;
            if (InputCompat.EscapePressed())
            {
                CompleteOrSkip();
                return;
            }
            int move = 0;
            bool submit = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                    move = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed ? -1 : 1;
                else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame)
                    move = -1;
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame)
                    move = 1;
                submit = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Tab))
                move = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                move = -1;
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                move = 1;
            submit = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
            if (move != 0)
                MoveFocus(move);
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
