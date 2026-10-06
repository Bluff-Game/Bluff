using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>
    /// Helper kecil untuk membangun uGUI lewat kode (tanpa prefab), supaya scene cukup berisi satu komponen.
    /// Layout memakai resolusi referensi 1920x1080 (landscape).
    /// </summary>
    public static class UIKit
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        static Font font;
        static Font semibold;
        static Font italic;
        static Font semiboldItalic;
        static Font display;

        public static Font Font => font != null ? font : (font = Resources.Load<Font>("Fonts/Barlow-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        static Font TextFont(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return semibold != null ? semibold : (semibold = Resources.Load<Font>("Fonts/Barlow-SemiBold") ?? Font);
                case FontStyle.Italic:
                    return italic != null ? italic : (italic = Resources.Load<Font>("Fonts/Barlow-Italic") ?? Font);
                case FontStyle.BoldAndItalic:
                    return semiboldItalic != null ? semiboldItalic : (semiboldItalic = Resources.Load<Font>("Fonts/Barlow-SemiBoldItalic") ?? Font);
                default:
                    return Font;
            }
        }

        public static Text CreateHeading(Transform parent, string name, string content, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = CreateText(parent, name, content, size, color, anchor);
            if (display == null)
                display = Resources.Load<Font>("Fonts/CormorantGaramond-SemiBold") ?? Font;
            text.font = display;
            return text;
        }

        // ------------------------------------------------------------------ scene plumbing

        public static void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                go.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Background;
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                cam.gameObject.AddComponent<AudioListener>();
        }

        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var module = go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            module.AssignDefaultActions();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.layer = 5;
            if (parent != null)
                go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // tinggi tetap 1080, lebar menyesuaikan layar
            return canvas;
        }

        // ------------------------------------------------------------------ rect helpers

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Anchor di satu titik (0..1), lalu posisi + ukuran dalam unit referensi.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Isi penuh parent dengan jarak tepi (kiri, kanan, atas, bawah).</summary>
        public static RectTransform Stretch(this RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor relatif (min, max) dengan offset nol: ukuran mengikuti persentase parent.</summary>
        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 Top = new Vector2(0.5f, 1f);
        public static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        public static readonly Vector2 Left = new Vector2(0f, 0.5f);
        public static readonly Vector2 Right = new Vector2(1f, 0.5f);
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        // ------------------------------------------------------------------ graphics

        public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        static Sprite wordmark;

        public static Image CreateLogo(Transform parent, string name)
        {
            if (wordmark == null)
            {
                var texture = Resources.Load<Texture2D>("Brand/BlufWordmark");
                if (texture != null)
                    wordmark = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            var image = CreateImage(parent, name, wordmark, Theme.Text);
            image.preserveAspect = true;
            return image;
        }

        public static Image CreatePanel(Transform parent, string name, Color color, float radius, bool raycast = false)
        {
            var img = CreateImage(parent, name, SpriteFactory.RoundedRect, color, raycast);
            img.type = Image.Type.Sliced;
            SetRadius(img, radius);
            return img;
        }

        public static Image CreateOutline(Transform parent, string name, Color color, float radius)
        {
            var img = CreateImage(parent, name, SpriteFactory.RoundedOutline, color);
            img.type = Image.Type.Sliced;
            SetRadius(img, radius);
            return img;
        }

        /// <summary>Atur radius sudut sprite rounded-rect 9-slice (dalam unit UI).</summary>
        public static void SetRadius(Image img, float radius)
        {
            img.pixelsPerUnitMultiplier = SpriteFactory.RoundedRectRadius / Mathf.Max(1f, radius);
        }

        public static Text CreateText(Transform parent, string name, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var rt = CreateRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = TextFont(style);
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = FontStyle.Normal;
            text.lineSpacing = 1.08f;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static void AddShadow(Graphic graphic, Color color, Vector2 distance)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        public static void AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
        }

        // ------------------------------------------------------------------ controls

        public static Button CreateButton(Transform parent, string name, string label, Vector2 size, Color background, Color foreground,
            System.Action onClick, int fontSize = 30, float radius = 8f)
        {
            var img = CreatePanel(parent, name, background, radius, true);
            img.rectTransform.sizeDelta = size;
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText(img.transform, "Label", label, fontSize, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
                text.rectTransform.Stretch(10f, 10f, 4f, 4f);
            }
            if (onClick != null)
            {
                button.onClick.AddListener(() =>
                {
                    Sfx.Play(SfxType.Click);
                    onClick();
                });
            }
            img.gameObject.AddComponent<ButtonFx>();
            return button;
        }

        public static Button CreateIconButton(Transform parent, string name, Sprite icon, Vector2 size, Color background, Color iconColor, System.Action onClick)
        {
            var button = CreateButton(parent, name, null, size, background, iconColor, onClick, 30, size.y * 0.5f);
            var img = CreateImage(button.transform, "Icon", icon, iconColor);
            img.preserveAspect = true;
            img.rectTransform.Stretch(size.x * 0.22f, size.x * 0.22f, size.y * 0.22f, size.y * 0.22f);
            return button;
        }

        public static InputField CreateInput(Transform parent, string value, string placeholder, int characterLimit, Vector2 size)
        {
            var bg = CreatePanel(parent, "Input", Theme.Background, 6f, true);
            bg.rectTransform.sizeDelta = size;
            var outline = CreateOutline(bg.transform, "Outline", Theme.Border, 6f);
            outline.rectTransform.Stretch();

            var text = CreateText(bg.transform, "Text", "", 32, Theme.Text, TextAnchor.MiddleLeft);
            text.rectTransform.Stretch(22f, 22f, 6f, 6f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var hint = CreateText(bg.transform, "Placeholder", placeholder, 32, Theme.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
            hint.rectTransform.Stretch(22f, 22f, 6f, 6f);

            var input = bg.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = characterLimit;
            input.lineType = InputField.LineType.SingleLine;
            input.text = value;
            return input;
        }

        /// <summary>Baris pengaturan: label di kiri, tombol &lt; nilai &gt; di kanan. Mengembalikan RectTransform barisnya.</summary>
        public static RectTransform CreateSelector(Transform parent, string label, string[] options, int index, System.Action<int> onChanged, float width)
        {
            var row = CreateRect("Row " + label, parent);
            row.sizeDelta = new Vector2(width, 76f);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 76f;
            layout.preferredWidth = width;

            var title = CreateText(row, "Label", label, 30, Theme.Text, TextAnchor.MiddleLeft);
            title.rectTransform.Anchor(new Vector2(0f, 0f), new Vector2(0.5f, 1f));

            var valueBg = CreatePanel(row, "ValueBg", Theme.Background, 6f);
            valueBg.rectTransform.Anchor(new Vector2(0.52f, 0.1f), new Vector2(1f, 0.9f));
            var value = CreateText(valueBg.transform, "Value", options[index], 28, Theme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            value.rectTransform.Stretch(70f, 70f, 0f, 0f);

            int current = index;
            System.Action<int> shift = delta =>
            {
                current = (current + delta + options.Length) % options.Length;
                value.text = options[current];
                onChanged(current);
            };
            var prev = CreateButton(valueBg.transform, "Prev", "<", new Vector2(60f, 50f), Theme.PanelLight, Theme.Text, () => shift(-1), 30, 12f);
            prev.GetComponent<RectTransform>().Place(new Vector2(0f, 0.5f), new Vector2(36f, 0f), new Vector2(56f, 46f));
            var next = CreateButton(valueBg.transform, "Next", ">", new Vector2(60f, 50f), Theme.PanelLight, Theme.Text, () => shift(1), 30, 12f);
            next.GetComponent<RectTransform>().Place(new Vector2(1f, 0.5f), new Vector2(-36f, 0f), new Vector2(56f, 46f));
            return row;
        }

        // ------------------------------------------------------------------ modal & scroll

        public sealed class Modal
        {
            public GameObject Root;
            public RectTransform Panel;
            public RectTransform Content;
            public Text Title;

            public void Close()
            {
                if (Root != null)
                    Object.Destroy(Root);
            }
        }

        /// <summary>Panel dialog di tengah layar dengan latar gelap. onClose null = tanpa tombol tutup.</summary>
        public static Modal CreateModal(Transform parent, string title, Vector2 size, System.Action onClose)
        {
            var dim = CreateImage(parent, "Modal " + title, null, new Color(0f, 0f, 0f, 0.7f), true);
            dim.rectTransform.Stretch();

            var panel = CreatePanel(dim.transform, "Panel", Theme.Panel, 10f, true);
            panel.rectTransform.Place(Center, Vector2.zero, size);
            var border = CreateOutline(panel.transform, "Border", Theme.Border.WithAlpha(0.6f), 10f);
            border.rectTransform.Stretch();

            var titleText = CreateHeading(panel.transform, "Title", title, 50, Theme.Text);
            var titleRt = titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -26f);
            titleRt.sizeDelta = new Vector2(-160f, 70f);
            AddShadow(titleText, new Color(0f, 0f, 0f, 0.5f), new Vector2(2f, -2f));

            var modal = new Modal { Root = dim.gameObject, Panel = panel.rectTransform, Title = titleText };
            if (onClose != null)
            {
                var close = CreateButton(panel.transform, "Close", "X", new Vector2(64f, 64f), Theme.PanelLight, Theme.Text, () =>
                {
                    modal.Close();
                    onClose();
                }, 30, 32f);
                close.GetComponent<RectTransform>().Place(TopRight, new Vector2(-50f, -50f), new Vector2(64f, 64f));
            }

            var content = CreateRect("Content", panel.transform);
            content.Stretch(48f, 48f, 112f, 40f);
            modal.Content = content;
            return modal;
        }

        /// <summary>Teks panjang yang bisa di-scroll (roda mouse / seret).</summary>
        public static Text CreateScrollText(RectTransform parent, string content, int fontSize)
        {
            var root = CreateRect("Scroll", parent);
            root.Stretch();
            var scroll = root.gameObject.AddComponent<ScrollRect>();

            var viewport = CreateRect("Viewport", root);
            viewport.Stretch(0f, 26f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            var body = CreateRect("Body", viewport);
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.anchoredPosition = Vector2.zero;
            body.sizeDelta = Vector2.zero;
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(4, 12, 4, 30);
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var text = CreateText(body, "Text", content, fontSize, Theme.Text, TextAnchor.UpperLeft);
            text.lineSpacing = 1.12f;

            var track = CreatePanel(root, "Scrollbar", new Color(1f, 1f, 1f, 0.06f), 6f, true);
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(1f, 0f);
            trackRt.anchorMax = new Vector2(1f, 1f);
            trackRt.pivot = new Vector2(1f, 0.5f);
            trackRt.anchoredPosition = Vector2.zero;
            trackRt.sizeDelta = new Vector2(12f, 0f);
            var area = CreateRect("Sliding Area", trackRt);
            area.Stretch();
            var handle = CreatePanel(area, "Handle", Theme.Gold.WithAlpha(0.55f), 6f, true);
            handle.rectTransform.Stretch();
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.content = body;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return text;
        }

        /// <summary>Latar layar: gradasi gelap dengan cahaya lembut di tengah.</summary>
        public static void CreateBackdrop(Transform parent, bool withTable)
        {
            var bg = CreateImage(parent, "Background", null, Theme.Background);
            bg.rectTransform.Stretch();
            var glow = CreateImage(parent, "BackgroundGlow", SpriteFactory.Glow, Theme.BackgroundTop.WithAlpha(0.9f));
            glow.rectTransform.Place(Center, new Vector2(0f, 120f), new Vector2(2600f, 1600f));

            if (!withTable)
                return;
            var rim = CreateImage(parent, "TableRim", SpriteFactory.Circle, Theme.FeltRim);
            rim.rectTransform.Place(Center, new Vector2(0f, 20f), new Vector2(1560f, 800f));
            var felt = CreateImage(parent, "TableFelt", SpriteFactory.Circle, Theme.Felt);
            felt.rectTransform.Place(Center, new Vector2(0f, 20f), new Vector2(1520f, 765f));
            var sheen = CreateImage(parent, "TableSheen", SpriteFactory.Glow, Theme.FeltLight.WithAlpha(0.55f));
            sheen.rectTransform.Place(Center, new Vector2(0f, 40f), new Vector2(1200f, 600f));
        }
    }
}
