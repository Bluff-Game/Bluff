using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>
    /// Layar loading: tokoh bertopeng berjalan menyusuri lorong menuju ruang kartu, bar progres terisi,
    /// lalu pintu terbuka dan permainan dimulai. Sekaligus menyiapkan tekstur kartu supaya meja langsung mulus.
    /// </summary>
    public sealed class LoadingController : BluffScreen
    {
        const float MinDuration = 3.4f;
        const float ScrollSpeed = 1.35f;
        const float WalkerX = -0.9f;

        static readonly string[] Tips =
        {
            "Tips: simpan Topeng untuk taruhan terakhir, karena dijamin jujur.",
            "Tips: tiap elemen hanya punya 12 kartu sah (8 + 4 Topeng). Hitung klaim lawan!",
            "Tips: lawan yang pelurunya habis tidak bisa menembakmu.",
            "Tips: bohong saat tumpukan kecil itu murah, saat tumpukan besar itu taruhan besar.",
            "Tips: Kartu Kuasa hanya bisa dipakai sekali. Pilih momennya baik-baik.",
            "Tips: salah tembak cuma mengambil kartu yang kamu tuduh, jadi jangan terlalu takut menembak.",
        };

        readonly List<Transform> scrolling = new List<Transform>();
        Humanoid walker;
        Transform door;
        Transform doorPanel;
        Light doorLight;
        Material doorGlow;
        Camera cam;
        Image barFill;
        Text percentText;
        Text tipText;
        CanvasGroup fade;
        float scroll = ScrollSpeed;
        float progressShown;

        protected override void Awake()
        {
            base.Awake();
            BuildStage();
            BuildUi();
        }

        void Start() => StartCoroutine(Run());

        // ================================================================== panggung 3D

        void BuildStage()
        {
            cam = Camera.main;
            cam.orthographic = false;
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Hex("101510");
            cam.transform.position = new Vector3(0f, 1.05f, -3.7f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.92f, 0f) - cam.transform.position);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.21f, 0.22f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 4f;
            RenderSettings.fogEndDistance = 9f;
            RenderSettings.fogColor = cam.backgroundColor;

            var world = new GameObject("Corridor").transform;
            world.SetParent(transform, false);

            Art.Primitive(PrimitiveType.Cube, "Floor", world, new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 3f), Art.Wood(Theme.Hex("392C20")));
            Art.Primitive(PrimitiveType.Cube, "Runner", world, new Vector3(0f, 0.004f, 0.1f), new Vector3(30f, 0.006f, 1.1f), Art.Fabric(Theme.Hex("26382B")));
            Art.Primitive(PrimitiveType.Cube, "Wall", world, new Vector3(0f, 1.5f, 1.25f), new Vector3(30f, 3f, 0.2f), Art.Fabric(Theme.Hex("283127")));

            // Elemen yang bergulir ke kiri (memberi kesan berjalan).
            var seam = Art.Wood(Theme.Hex("211B13"));
            var gold = Art.Metal(Theme.GoldDark, 0.4f);
            var panel = Art.Wood(Theme.Hex("3A3022"));
            var painting = Art.Fabric(Theme.Hex("344334"));
            var lamp = Art.Lit(Theme.Hex("FFD9A0"), 0.5f, 0f, null, Theme.Hex("FFB860") * 2f);
            for (int i = -8; i <= 8; i++)
            {
                var seg = new GameObject("Segment").transform;
                seg.SetParent(world, false);
                seg.localPosition = new Vector3(i * 1.6f, 0f, 0f);
                Art.Primitive(PrimitiveType.Cube, "Seam", seg, new Vector3(0f, 0.002f, -0.2f), new Vector3(0.02f, 0.004f, 2.6f), seam);
                Art.Primitive(PrimitiveType.Cube, "Panel", seg, new Vector3(0f, 0.5f, 1.12f), new Vector3(1.5f, 0.9f, 0.06f), panel);
                Art.Primitive(PrimitiveType.Cube, "Trim", seg, new Vector3(0f, 1.0f, 1.1f), new Vector3(1.6f, 0.03f, 0.04f), gold);
                if (i % 2 == 0)
                {
                    Art.Primitive(PrimitiveType.Sphere, "Sconce", seg, new Vector3(0f, 1.9f, 1.1f), new Vector3(0.14f, 0.18f, 0.14f), lamp);
                    Art.Primitive(PrimitiveType.Cube, "Frame", seg, new Vector3(0.8f, 1.75f, 1.13f), new Vector3(0.9f, 0.6f, 0.04f), gold);
                    Art.Primitive(PrimitiveType.Cube, "Painting", seg, new Vector3(0.8f, 1.75f, 1.11f), new Vector3(0.78f, 0.5f, 0.03f),
                        painting);
                }
                scrolling.Add(seg);
            }

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Spot;
            key.transform.position = new Vector3(WalkerX, 3f, -0.6f);
            key.transform.rotation = Quaternion.LookRotation(new Vector3(WalkerX, 0.8f, 0f) - key.transform.position);
            key.spotAngle = 55f;
            key.range = 6f;
            key.intensity = 3f;
            key.color = Theme.Hex("FFE6C2");
            key.shadows = LightShadows.Soft;

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(20f, -30f, 0f);
            fill.intensity = 0.35f;
            fill.color = Theme.Hex("CAD0AD");
            fill.shadows = LightShadows.None;

            walker = Humanoid.Create(transform, "Walker", HumanoidStyle.ForPlayer(0), false);
            walker.transform.position = new Vector3(WalkerX, 0f, 0f);
            walker.transform.rotation = Quaternion.LookRotation(Vector3.right);
            walker.SetWalking(1f);

            BuildDoor(world);
        }

        void BuildDoor(Transform world)
        {
            door = new GameObject("Door").transform;
            door.SetParent(world, false);
            door.localPosition = new Vector3(9f, 0f, 0.9f);
            var wood = Art.Wood(Theme.Hex("423223"));
            var panelWood = Art.Wood(Theme.Hex("30271D"));
            var gold = Art.Metal(Theme.Gold, 0.48f);
            var darkMetal = Art.Metal(Theme.Hex("282A22"), 0.3f);
            Art.BeveledBox("FrameL", door, new Vector3(-0.55f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.25f), 0.012f, wood);
            Art.BeveledBox("FrameR", door, new Vector3(0.55f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.25f), 0.012f, wood);
            Art.BeveledBox("FrameTop", door, new Vector3(0f, 2.33f, 0f), new Vector3(1.22f, 0.14f, 0.25f), 0.012f, wood);
            doorGlow = Art.UnlitInstance(Theme.Hex("FFD08A").WithAlpha(0f));
            var glow = Art.Quad("Glow", door, new Vector3(0f, 1.12f, 0.12f), new Vector2(1f, 2.2f), doorGlow);
            glow.transform.localRotation = Quaternion.identity;
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(door, false);
            hinge.localPosition = new Vector3(-0.49f, 0f, -0.04f);
            doorPanel = hinge;
            Art.BeveledBox("StileL", hinge, new Vector3(0.065f, 1.12f, 0f), new Vector3(0.13f, 2.2f, 0.1f), 0.008f, wood);
            Art.BeveledBox("StileR", hinge, new Vector3(0.915f, 1.12f, 0f), new Vector3(0.13f, 2.2f, 0.1f), 0.008f, wood);
            Art.BeveledBox("BottomRail", hinge, new Vector3(0.49f, 0.1f, 0f), new Vector3(0.72f, 0.16f, 0.1f), 0.006f, wood);
            Art.BeveledBox("LockRail", hinge, new Vector3(0.49f, 0.99f, 0f), new Vector3(0.72f, 0.16f, 0.1f), 0.006f, wood);
            Art.BeveledBox("TopRail", hinge, new Vector3(0.49f, 2.14f, 0f), new Vector3(0.72f, 0.16f, 0.1f), 0.006f, wood);
            BuildDoorInset(hinge, 0.545f, 0.73f, panelWood, wood);
            BuildDoorInset(hinge, 1.565f, 0.99f, panelWood, wood);
            Art.BeveledBox("HandleBackplate", hinge, new Vector3(0.915f, 1.05f, -0.06f), new Vector3(0.065f, 0.24f, 0.018f), 0.008f, gold);
            var spindle = Art.SmoothCylinder("HandleSpindle", hinge, new Vector3(0.915f, 1.08f, -0.1f), new Vector3(0.034f, 0.035f, 0.034f), gold);
            spindle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Art.Tube("HandleLever", hinge, new[]
            {
                new Vector3(0.915f, 1.08f, -0.134f), new Vector3(0.89f, 1.08f, -0.145f),
                new Vector3(0.80f, 1.074f, -0.145f), new Vector3(0.775f, 1.063f, -0.134f),
            }, 0.012f, gold, 12);
            var keyhole = Art.SmoothCylinder("Keyhole", hinge, new Vector3(0.915f, 1.002f, -0.07f), new Vector3(0.016f, 0.0015f, 0.016f), darkMetal);
            keyhole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Art.BeveledBox("KeyholeSlot", hinge, new Vector3(0.915f, 0.989f, -0.07f), new Vector3(0.009f, 0.022f, 0.003f), 0.001f, darkMetal);
            for (int i = 0; i < 2; i++)
            {
                float y = 0.95f + i * 0.2f;
                var screw = Art.SmoothCylinder("HandleScrew", hinge, new Vector3(0.915f, y, -0.071f), new Vector3(0.01f, 0.002f, 0.01f), gold);
                screw.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Art.BeveledBox("ScrewSlot", hinge, new Vector3(0.915f, y, -0.0735f), new Vector3(0.007f, 0.0015f, 0.001f), 0.0004f, darkMetal);
            }
            for (int i = 0; i < 3; i++)
                Art.SmoothCylinder("HingeBarrel", hinge, new Vector3(0f, 0.3f + i * 0.82f, -0.048f), new Vector3(0.038f, 0.055f, 0.038f), gold);
            doorLight = new GameObject("DoorLight").AddComponent<Light>();
            doorLight.transform.SetParent(door, false);
            doorLight.transform.localPosition = new Vector3(0f, 1.2f, -0.6f);
            doorLight.type = LightType.Point;
            doorLight.range = 5f;
            doorLight.intensity = 0f;
            doorLight.color = Theme.Hex("FFD08A");
            scrolling.Add(door);
        }

        static void BuildDoorInset(Transform parent, float y, float height, Material panel, Material molding)
        {
            Art.BeveledBox("InsetPanel", parent, new Vector3(0.49f, y, 0f), new Vector3(0.72f, height, 0.034f), 0.006f, panel);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 0.033f;
                Art.BeveledBox("PanelMoldingL", parent, new Vector3(0.145f, y, z), new Vector3(0.03f, height, 0.03f), 0.005f, molding);
                Art.BeveledBox("PanelMoldingR", parent, new Vector3(0.835f, y, z), new Vector3(0.03f, height, 0.03f), 0.005f, molding);
                Art.BeveledBox("PanelMoldingTop", parent, new Vector3(0.49f, y + height * 0.5f - 0.015f, z), new Vector3(0.66f, 0.03f, 0.03f), 0.005f, molding);
                Art.BeveledBox("PanelMoldingBottom", parent, new Vector3(0.49f, y - height * 0.5f + 0.015f, z), new Vector3(0.66f, 0.03f, 0.03f), 0.005f, molding);
            }
        }

        // ================================================================== UI

        void BuildUi()
        {
            var logo = UIKit.CreateLogo(Root, "Logo");
            logo.rectTransform.Place(UIKit.TopLeft, new Vector2(150f, -70f), new Vector2(220f, 70f));
            logo.preserveAspect = true;
            var sub = UIKit.CreateText(Root, "Sub", "Memasuki ruang kartu…", 24, Theme.Muted, TextAnchor.MiddleLeft);
            sub.rectTransform.Place(UIKit.TopLeft, new Vector2(260f, -124f), new Vector2(440f, 34f));

            var barBg = UIKit.CreateImage(Root, "BarBg", null, Theme.Text.WithAlpha(0.12f));
            barBg.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 120f), new Vector2(1000f, 6f));
            barFill = UIKit.CreateImage(barBg.transform, "Fill", null, Theme.Gold);
            barFill.rectTransform.Anchor(Vector2.zero, new Vector2(0f, 1f));
            percentText = UIKit.CreateText(Root, "Percent", "0%", 24, Theme.Text, TextAnchor.MiddleCenter);
            percentText.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 172f), new Vector2(300f, 40f));
            tipText = UIKit.CreateText(Root, "Tip", Tips[Random.Range(0, Tips.Length)], 28, Theme.Text, TextAnchor.MiddleCenter);
            tipText.rectTransform.Place(UIKit.Bottom, new Vector2(0f, 64f), new Vector2(1400f, 44f));

            var black = UIKit.CreateImage(Root, "Fade", null, Color.black);
            black.rectTransform.Stretch();
            fade = black.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 1f;
            fade.blocksRaycasts = false;
        }

        // ================================================================== alur

        IEnumerator Run()
        {
            yield return Tween.Fade(fade, 0f, 0.4f);

            var op = SceneFlow.LoadGameAsync();
            float elapsed = 0f;
            float tipTimer = 0f;
            int tipIndex = Random.Range(0, Tips.Length);
            var prewarm = PrewarmSteps().GetEnumerator();
            bool prewarmDone = false;

            while (true)
            {
                if (!prewarmDone)
                    prewarmDone = !prewarm.MoveNext();
                elapsed += Time.deltaTime;
                tipTimer += Time.deltaTime;
                if (tipTimer > 2.6f)
                {
                    tipTimer = 0f;
                    tipIndex = (tipIndex + 1) % Tips.Length;
                    tipText.text = Tips[tipIndex];
                }
                float loadProgress = op != null ? Mathf.Clamp01(op.progress / 0.9f) : 1f;
                float target = Mathf.Min(loadProgress, prewarmDone ? 1f : 0.85f, elapsed / MinDuration);
                progressShown = Mathf.MoveTowards(progressShown, target, Time.deltaTime * 0.8f);
                SetProgress(progressShown);
                if (progressShown >= 0.999f && prewarmDone && loadProgress >= 1f)
                    break;
                yield return null;
            }

            // Pintu ruang kartu mendekat, tokoh berhenti, pintu terbuka.
            door.localPosition = new Vector3(WalkerX + 4.2f, 0f, 0.9f);
            while (door.localPosition.x > WalkerX + 1.25f)
                yield return null;
            scroll = 0f;
            walker.SetWalking(0f);
            walker.LookAt(door.position + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.25f);
            Sfx.Play(SfxType.Power, 0.6f);
            yield return Tween.Run(0.7f, t =>
            {
                doorPanel.localRotation = Quaternion.Euler(0f, -100f * Tween.EaseInOutCubic(t), 0f);
                doorLight.intensity = 6f * t;
                doorGlow.SetColor("_Color", Theme.Hex("FFD08A").WithAlpha(t));
            });
            walker.SetWalking(1f);
            yield return Tween.Run(0.6f, t =>
            {
                cam.fieldOfView = Mathf.Lerp(38f, 30f, t);
                fade.alpha = t;
            });

            if (op != null)
                op.allowSceneActivation = true;
            else
                SceneFlow.SwapInPlace<GameController>();
        }

        /// <summary>Siapkan tekstur kartu sedikit demi sedikit (satu per frame) supaya animasi tetap mulus.</summary>
        static IEnumerable<int> PrewarmSteps()
        {
            foreach (var e in Elements.Claimable)
            {
                CardArt.Face(e);
                yield return 0;
            }
            CardArt.Face(Element.Topeng);
            yield return 0;
            var _ = CardArt.Back;
            yield return 0;
            _ = CardArt.PowerBack;
            foreach (var p in Powers.All)
            {
                CardArt.Power(p);
                yield return 0;
            }
        }

        void SetProgress(float value)
        {
            barFill.rectTransform.anchorMax = new Vector2(value, 1f);
            percentText.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        void Update()
        {
            if (scroll <= 0f)
                return;
            float dx = scroll * Time.deltaTime;
            foreach (var t in scrolling)
            {
                var p = t.localPosition;
                p.x -= dx;
                if (t != door && p.x < -13.6f)
                    p.x += 27.2f;
                t.localPosition = p;
            }
        }
    }
}
