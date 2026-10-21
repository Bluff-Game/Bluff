using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Bluff.Game
{
    public sealed class LoadingController : BluffScreen
    {
        const float DoorX = 0.6f;
        const float DoorZ = 1.25f;
        // Pull from the latch side, then release before walking around the open leaf.
        const float OpenAngle = -82f;

        static readonly string[] Tips =
        {
            "Tip: save a Mask for an honest final play.",
            "Tip: each element has 12 matching cards, including 4 Masks. Count the claims!",
            "Tip: opponents without bullets cannot challenge you.",
            "Tip: a bigger pile makes a bluff more costly.",
            "Tip: every power is single-use. Choose your moment.",
            "Tip: a wrong challenge takes the challenged cards under the default rules.",
        };

        enum EntryPhase { Approach, Turn, Contact, Depress, Open, Release, Enter, Fade, Retry, Error }

        Humanoid walker;
        Transform door;
        Transform doorPanel;
        Transform handleLever;
        Transform handleGrip;
        Camera cam;
        Image barFill;
        Text percentText;
        Text tipText;
        CanvasGroup fade;
        EntryPhase entryPhase;
        float progressShown;
        float entryProgress;
        float loadProgress;
        bool loadReady;
        bool entered;
        int gripGeneration;
        bool doorMotionFailed;
        bool continueRequested;
        Button continueButton;

        public string Phase => entryPhase.ToString();

        protected override void Awake()
        {
            base.Awake();
            BuildStage();
            BuildUi();
        }

        void Start() => StartCoroutine(Run());

        void BuildStage()
        {
            cam = Camera.main;
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Hex("101510");
            cam.transform.position = new Vector3(2.65f, 2f, -3.8f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(0.15f, 1.05f, 0.7f) - cam.transform.position);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.21f, 0.22f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 7f;
            RenderSettings.fogEndDistance = 12f;
            RenderSettings.fogColor = cam.backgroundColor;

            var world = new GameObject("Corridor").transform;
            world.SetParent(transform, false);
            var wall = Art.Fabric(Theme.Hex("283127"));
            var floor = Art.Wood(Theme.Hex("392C20"));
            var runner = Art.Fabric(Theme.Hex("26382B"));
            Art.Primitive(PrimitiveType.Cube, "Floor", world, new Vector3(0f, -0.05f, -1.3f), new Vector3(30f, 0.1f, 8f), floor);
            Art.Primitive(PrimitiveType.Cube, "Runner", world, new Vector3(0f, 0.004f, 0.1f), new Vector3(30f, 0.006f, 1.1f), runner);
            float leftEdge = DoorX - 0.61f;
            float rightEdge = DoorX + 0.61f;
            Art.Primitive(PrimitiveType.Cube, "WallLeft", world, new Vector3((-15f + leftEdge) * 0.5f, 1.5f, DoorZ), new Vector3(leftEdge + 15f, 3f, 0.2f), wall, true);
            Art.Primitive(PrimitiveType.Cube, "WallRight", world, new Vector3((15f + rightEdge) * 0.5f, 1.5f, DoorZ), new Vector3(15f - rightEdge, 3f, 0.2f), wall, true);
            Art.Primitive(PrimitiveType.Cube, "WallAboveDoor", world, new Vector3(DoorX, 2.7f, DoorZ), new Vector3(1.22f, 0.6f, 0.2f), wall, true);
            Art.Primitive(PrimitiveType.Cube, "RoomFloor", world, new Vector3(DoorX, -0.05f, 3.1f), new Vector3(4f, 0.1f, 3.2f), floor);
            Art.Primitive(PrimitiveType.Cube, "RoomRunner", world, new Vector3(DoorX, 0.004f, 2.7f), new Vector3(0.94f, 0.006f, 3f), runner);
            Art.Primitive(PrimitiveType.Cube, "RoomBackWall", world, new Vector3(DoorX, 1.5f, 4.7f), new Vector3(4f, 3f, 0.2f), wall);
            Art.Primitive(PrimitiveType.Cube, "RoomLeftWall", world, new Vector3(DoorX - 2f, 1.5f, 3f), new Vector3(0.2f, 3f, 3.4f), wall);
            Art.Primitive(PrimitiveType.Cube, "RoomRightWall", world, new Vector3(DoorX + 2f, 1.5f, 3f), new Vector3(0.2f, 3f, 3.4f), wall);

            Art.Primitive(PrimitiveType.Cube,"Ceiling",world,new Vector3(0f,3.1f,-.8f),new Vector3(30f,.12f,8f),wall);
            var seam = Art.Wood(Theme.Hex("211B13"));
            var gold = Art.Metal(Theme.GoldDark, 0.4f);
            var panel = Art.Wood(Theme.Hex("3A3022"));
            var painting = Art.Fabric(Theme.Hex("344334"));
            var lamp = Art.Lit(Theme.Hex("FFD9A0"), 0.5f, 0f, null, Theme.Hex("FFB860") * 0.5f);
            for (int i = -8; i <= 8; i++)
            {
                float x = i * 1.6f;
                Art.Primitive(PrimitiveType.Cube, "Seam", world, new Vector3(x, 0.002f, -0.2f), new Vector3(0.02f, 0.004f, 2.6f), seam);
                // Clip each panel to the door opening; never drop an entire wall segment.
                for(int side=0;side<2;side++)
                {
                    float lo=side==0?x-.8f:Mathf.Max(x-.8f,rightEdge);
                    float hi=side==0?Mathf.Min(x+.8f,leftEdge):x+.8f;
                    if(hi<=lo) continue;
                    float mid=(lo+hi)*.5f, width=hi-lo;
                    Art.BeveledBox("Wall panel",world,new Vector3(mid,.5f,1.12f),new Vector3(width-.008f,.91f,.06f),.008f,panel);
                    Art.Primitive(PrimitiveType.Cube,"Chair rail",world,new Vector3(mid,1f,1.09f),new Vector3(width,.036f,.055f),gold);
                    Art.Primitive(PrimitiveType.Cube,"Skirting",world,new Vector3(mid,.06f,1.08f),new Vector3(width,.10f,.08f),seam);
                    Art.Primitive(PrimitiveType.Cube,"Panel stile",world,new Vector3(lo+.025f,.5f,1.08f),new Vector3(.035f,.8f,.025f),gold);
                }
                if (x + 0.8f > leftEdge && x - 0.8f < rightEdge) continue;
                var seg = new GameObject("Segment").transform;
                seg.SetParent(world, false);seg.localPosition = new Vector3(x, 0f, 0f);
                if (i % 2 == 0)
                {
                    Art.BeveledBox("Sconce backplate",seg,new Vector3(0f,1.85f,1.11f),new Vector3(.13f,.28f,.05f),.025f,gold);
                    Art.Tube("Sconce arm",seg,new[]{new Vector3(0,1.78f,1.08f),new Vector3(0,1.75f,.94f),new Vector3(0,1.86f,.92f)},.017f,gold,10);
                    Art.SmoothCylinder("Lamp shade",seg,new Vector3(0,1.97f,.92f),new Vector3(.24f,.08f,.24f),gold);
                    Art.Primitive(PrimitiveType.Sphere, "Sconce", seg, new Vector3(0f, 1.9f, .92f), new Vector3(0.14f, 0.18f, 0.14f), lamp);
                    if (x + 1.25f <= leftEdge || x + 0.35f >= rightEdge)
                    {
                        Art.Primitive(PrimitiveType.Cube, "Frame", seg, new Vector3(0.8f, 1.75f, 1.13f), new Vector3(0.9f, 0.6f, 0.04f), gold);
                        Art.Primitive(PrimitiveType.Cube, "Painting", seg, new Vector3(0.8f, 1.75f, 1.11f), new Vector3(0.78f, 0.5f, 0.03f), painting);
                        var relief=Art.Primitive(PrimitiveType.Quad,"Framed emblem",seg,new Vector3(.8f,1.75f,1.088f),new Vector3(.42f,.42f,1f),Art.Lit(Color.white,.3f,0f,SpriteFactory.Emblem(i%4==0?GameIcon.Cards:GameIcon.Mask).texture));
                        relief.transform.localRotation=Quaternion.identity;
                    }
                }
            }
            Art.Primitive(PrimitiveType.Cube, "RoomPanel", world, new Vector3(DoorX, 0.5f, 4.57f), new Vector3(3.8f, 0.9f, 0.06f), panel);
            Art.Primitive(PrimitiveType.Cube, "RoomTrim", world, new Vector3(DoorX, 1f, 4.55f), new Vector3(3.8f, 0.03f, 0.04f), gold);

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.transform.SetParent(transform, false);
            key.type = LightType.Spot;
            key.transform.position = new Vector3(-0.3f, 3.2f, -1.2f);
            key.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, 0.8f, 0.6f) - key.transform.position);
            key.spotAngle = 85f;
            key.range = 7f;
            key.intensity = 2.2f;
            key.color = Theme.Hex("FFE6C2");
            key.shadows = LightShadows.Soft;

            var fill = new GameObject("Fill").AddComponent<Light>();
            fill.transform.SetParent(transform, false);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(20f, -30f, 0f);
            fill.intensity = 0.35f;
            fill.color = Theme.Hex("CAD0AD");
            fill.shadows = LightShadows.None;

            BuildDoor(world);
            walker = Humanoid.Create(transform, "Walker", Cosmetics.LocalStyle, false);
            walker.transform.position = new Vector3(-1.65f, 0f, GripRootPosition().z);
            walker.transform.rotation = Quaternion.LookRotation(Vector3.right);
            walker.SetStandingLocomotionPose(walker.transform.position, walker.transform.rotation);
        }

        void BuildDoor(Transform world)
        {
            door = new GameObject("Door").transform;
            door.SetParent(world, false);
            door.localPosition = new Vector3(DoorX, 0f, DoorZ);
            var wood = Art.Wood(Theme.Hex("423223"));
            var panelWood = Art.Wood(Theme.Hex("30271D"));
            var gold = Art.Metal(Theme.Gold, 0.48f);
            var darkMetal = Art.Metal(Theme.Hex("282A22"), 0.3f);
            Art.BeveledBox("FrameL", door, new Vector3(-0.55f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.25f), 0.012f, wood);
            Art.BeveledBox("FrameR", door, new Vector3(0.55f, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.25f), 0.012f, wood);
            Art.BeveledBox("FrameTop", door, new Vector3(0f, 2.33f, 0f), new Vector3(1.22f, 0.14f, 0.25f), 0.012f, wood);
            var hinge = new GameObject("Hinge").transform;
            hinge.SetParent(door, false);
            hinge.localPosition = new Vector3(0.49f, 0f, -0.04f);
            doorPanel = hinge;
            Art.BeveledBox("StileL", hinge, new Vector3(-0.915f, 1.12f, 0f), new Vector3(0.13f, 2.2f, 0.1f), 0.008f, wood);
            Art.BeveledBox("StileR", hinge, new Vector3(-0.065f, 1.12f, 0f), new Vector3(0.13f, 2.2f, 0.1f), 0.008f, wood);
            Art.BeveledBox("BottomRail", hinge, new Vector3(-0.49f, 0.1f, 0f), new Vector3(0.72f, 0.16f, 0.1f), 0.006f, wood);
            Art.BeveledBox("LockRail", hinge, new Vector3(-0.49f, 0.99f, 0f), new Vector3(0.72f, 0.16f, 0.1f), 0.006f, wood);
            Art.BeveledBox("TopRail", hinge, new Vector3(-0.49f, 2.14f, 0f), new Vector3(0.72f, 0.16f, 0.1f), 0.006f, wood);
            BuildDoorInset(hinge, 0.545f, 0.73f, panelWood, wood);
            BuildDoorInset(hinge, 1.565f, 0.99f, panelWood, wood);
            Art.BeveledBox("HandleBackplate", hinge, new Vector3(-0.915f, 1.05f, -0.06f), new Vector3(0.065f, 0.24f, 0.018f), 0.008f, gold);
            var spindle = Art.SmoothCylinder("HandleSpindle", hinge, new Vector3(-0.915f, 1.08f, -0.1f), new Vector3(0.034f, 0.035f, 0.034f), gold);
            spindle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handleLever = new GameObject("HandleLeverPivot").transform;
            handleLever.SetParent(hinge, false);
            handleLever.localPosition = new Vector3(-0.915f, 1.08f, -0.134f);
            Art.Tube("HandleLever", handleLever, new[]
            {
                Vector3.zero, new Vector3(0.025f, 0f, -0.011f),
                new Vector3(0.115f, -0.006f, -0.011f), new Vector3(0.14f, -0.017f, 0f),
            }, 0.012f, gold, 12);
            handleGrip = new GameObject("HandleGrip").transform;
            handleGrip.SetParent(handleLever, false);
            handleGrip.localPosition = new Vector3(0.075f, -0.003f, -0.011f);
            var keyhole = Art.SmoothCylinder("Keyhole", hinge, new Vector3(-0.915f, 1.002f, -0.07f), new Vector3(0.016f, 0.0015f, 0.016f), darkMetal);
            keyhole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Art.BeveledBox("KeyholeSlot", hinge, new Vector3(-0.915f, 0.989f, -0.07f), new Vector3(0.009f, 0.022f, 0.003f), 0.001f, darkMetal);
            for (int i = 0; i < 2; i++)
            {
                float y = 0.95f + i * 0.2f;
                var screw = Art.SmoothCylinder("HandleScrew", hinge, new Vector3(-0.915f, y, -0.071f), new Vector3(0.01f, 0.002f, 0.01f), gold);
                screw.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Art.BeveledBox("ScrewSlot", hinge, new Vector3(-0.915f, y, -0.0735f), new Vector3(0.007f, 0.0015f, 0.001f), 0.0004f, darkMetal);
            }
            for (int i = 0; i < 3; i++)
                Art.SmoothCylinder("HingeBarrel", hinge, new Vector3(0f, 0.3f + i * 0.82f, -0.048f), new Vector3(0.038f, 0.055f, 0.038f), gold);
            doorPanel.gameObject.AddComponent<BoxCollider>().center = new Vector3(-0.49f, 1.12f, 0f);
            doorPanel.GetComponent<BoxCollider>().size = new Vector3(0.98f, 2.2f, 0.1f);
            var roomLight = new GameObject("RoomLight").AddComponent<Light>();
            roomLight.transform.SetParent(world, false);
            roomLight.transform.position = new Vector3(DoorX, 2.15f, 2.8f);
            roomLight.type = LightType.Point;
            roomLight.range = 4f;
            roomLight.intensity = 0.85f;
            roomLight.color = Theme.Hex("FFE0B5");
            roomLight.shadows = LightShadows.Soft;
        }

        static void BuildDoorInset(Transform parent, float y, float height, Material panel, Material molding)
        {
            Art.BeveledBox("InsetPanel", parent, new Vector3(-0.49f, y, 0f), new Vector3(0.72f, height, 0.034f), 0.006f, panel);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * 0.033f;
                Art.BeveledBox("PanelMoldingL", parent, new Vector3(-0.835f, y, z), new Vector3(0.03f, height, 0.03f), 0.005f, molding);
                Art.BeveledBox("PanelMoldingR", parent, new Vector3(-0.145f, y, z), new Vector3(0.03f, height, 0.03f), 0.005f, molding);
                Art.BeveledBox("PanelMoldingTop", parent, new Vector3(-0.49f, y + height * 0.5f - 0.015f, z), new Vector3(0.66f, 0.03f, 0.03f), 0.005f, molding);
                Art.BeveledBox("PanelMoldingBottom", parent, new Vector3(-0.49f, y - height * 0.5f + 0.015f, z), new Vector3(0.66f, 0.03f, 0.03f), 0.005f, molding);
            }
        }

        void BuildUi()
        {
            var logo = UIKit.CreateLogo(Root, "Logo");
            logo.rectTransform.Place(UIKit.TopLeft, new Vector2(150f, -70f), new Vector2(220f, 70f));
            logo.preserveAspect = true;
            var sub = UIKit.CreateText(Root, "Sub", "Entering the card room...", 24, Theme.Muted, TextAnchor.MiddleLeft);
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

        IEnumerator Run()
        {
            var op = SceneFlow.LoadGameAsync();
            StartCoroutine(Prepare(op));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0)
                    yield return ResetEntry();
                yield return TryEnter();
                if (entered)
                    break;
            }
            if (!entered)
                yield return WaitForContinue();
            if (!entered && !continueRequested)
                yield break;
            entryPhase = EntryPhase.Fade;
            yield return Tween.Fade(fade, 1f, 0.3f);
            if (op != null)
                op.allowSceneActivation = true;
            else
                SceneFlow.SwapInPlace<GameController>();
        }

        IEnumerator TryEnter()
        {
            entered = false;
            doorMotionFailed = false;
            entryPhase = EntryPhase.Approach;
            yield return Tween.Fade(fade, 0f, 0.3f);
            yield return MoveWalker(GripRootPosition(), 1.5f, Quaternion.LookRotation(Vector3.right));
            entryProgress = 0.24f;
            entryPhase = EntryPhase.Turn;
            // Face the handle directly. Rotating to world identity made the character turn away from the door.
            yield return MoveWalker(walker.transform.position, 0.38f, GripFacing());
            while (!loadReady)
                yield return null;

            entryPhase = EntryPhase.Contact;
            if (!walker.CanTrackGrip(handleGrip))
            {
                yield return MoveWalker(GripRootPosition(), 0.25f, GripFacing());
                if (!walker.CanTrackGrip(handleGrip))
                {
                    Debug.LogError("[Bluff] Loading handle is outside the standing grip workspace.", this);
                    yield break;
                }
            }
            gripGeneration = walker.BeginGripTracking(handleGrip);
            if (gripGeneration == 0)
                yield break;
            try
            {
                yield return Tween.Run(0.5f, t => walker.SetTrackedGrip(gripGeneration, Smooth(t), Smooth(Mathf.InverseLerp(0.65f, 1f, t))));
                float contactWait = 0f;
                while (!walker.HasGripContact(gripGeneration) && contactWait < 0.35f)
                {
                    contactWait += Time.deltaTime;
                    yield return null;
                }
                if (!walker.HasGripContact(gripGeneration))
                {
                    Debug.LogError("[Bluff] Loading door stayed closed: handle contact was not established.", this);
                    yield break;
                }
                entryProgress = 0.4f;
                entryPhase = EntryPhase.Depress;
                yield return AnimateDoor(0.35f, 0f, 0f, 0f, -28f);
                yield return WaitForGripContact(gripGeneration, 0.12f);
                if (doorMotionFailed || !walker.HasGripContact(gripGeneration))
                    yield break;
                entryPhase = EntryPhase.Open;
                yield return AnimateDoor(2.25f, 0f, OpenAngle, -28f, -28f);
                yield return WaitForGripContact(gripGeneration, 0.12f);
                if (doorMotionFailed || !walker.HasGripContact(gripGeneration) || Quaternion.Angle(doorPanel.localRotation, Quaternion.Euler(0f, OpenAngle, 0f)) > 0.5f)
                    yield break;
                entryProgress = 0.7f;
                entryPhase = EntryPhase.Release;
                yield return Tween.Run(0.35f, t =>
                {
                    walker.SetTrackedGrip(gripGeneration, 1f - Smooth(Mathf.InverseLerp(0.45f, 1f, t)), 1f - Smooth(Mathf.InverseLerp(0f, 0.45f, t)));
                    handleLever.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-28f, 0f, Smooth(Mathf.InverseLerp(0.45f, 1f, t))));
                });
            }
            finally
            {
                if (walker != null)
                    walker.EndGripTracking(gripGeneration);
                gripGeneration = 0;
            }

            entryPhase = EntryPhase.Enter;
            walker.LookAt(new Vector3(DoorX,1.5f,3.2f));
            yield return MoveWalker(new Vector3(walker.transform.position.x,0f,-.24f),.4f,Quaternion.Euler(0,-80f,0));
            yield return MoveWalker(new Vector3(DoorX, 0f, -.24f), .65f, Quaternion.identity);
            entryProgress = 0.8f;
            walker.LookAt(new Vector3(DoorX, 1.5f, 3.2f));
            yield return MoveWalker(new Vector3(DoorX, 0f, DoorZ + 0.8f), 1.95f, Quaternion.identity);
            entered = loadReady && walker.transform.position.z > DoorZ + 0.35f;
            if (!entered)
                yield break;
            entryProgress = 1f;
            progressShown = 1f;
            SetProgress(progressShown);
        }

        IEnumerator ResetEntry()
        {
            entryPhase = EntryPhase.Retry;
            if (walker != null)
            {
                walker.EndGripTracking(gripGeneration);
                walker.SetWalking(0f);
            }
            gripGeneration = 0;
            tipText.text = "Retrying entry...";
            yield return Tween.Fade(fade, 1f, 0.2f);
            doorPanel.localRotation = Quaternion.identity;
            handleLever.localRotation = Quaternion.identity;
            if (walker != null)
            {
                walker.gameObject.SetActive(false);
                Destroy(walker.gameObject);
            }
            walker = Humanoid.Create(transform, "Walker", Cosmetics.LocalStyle, false);
            walker.transform.position = new Vector3(-1.65f, 0f, GripRootPosition().z);
            walker.transform.rotation = Quaternion.LookRotation(Vector3.right);
            walker.SetStandingLocomotionPose(walker.transform.position, walker.transform.rotation);
            entryProgress = 0f;
            progressShown = Mathf.Min(progressShown, loadProgress * 0.75f);
            SetProgress(progressShown);
            yield return null;
        }

        IEnumerator WaitForContinue()
        {
            entryPhase = EntryPhase.Error;
            if (walker != null)
            {
                walker.EndGripTracking(gripGeneration);
                walker.SetWalking(0f);
            }
            gripGeneration = 0;
            continueRequested = false;
            var panel = UIKit.CreatePanel(Root, "EntryError", Theme.Panel, 10f, true);
            panel.rectTransform.Place(UIKit.Center, Vector2.zero, new Vector2(760f, 240f));
            var message = UIKit.CreateText(panel.transform, "Message", "Could not complete the entry", 28, Theme.Text, TextAnchor.MiddleCenter);
            message.rectTransform.Place(UIKit.Center, new Vector2(0f, 62f), new Vector2(712f, 48f));
            var detail = UIKit.CreateText(panel.transform, "Detail", "Continue directly to the table.", 24, Theme.Muted, TextAnchor.MiddleCenter);
            detail.rectTransform.Place(UIKit.Center, new Vector2(0f, 10f), new Vector2(680f, 40f));
            continueButton = UIKit.CreateButton(panel.transform, "ContinueToGame", "Continue to table", new Vector2(340f, 64f), Theme.Gold, Theme.TextDark, ContinueToGame, 28);
            continueButton.GetComponent<RectTransform>().Place(UIKit.Center, new Vector2(0f, -66f), new Vector2(340f, 64f));
            continueButton.interactable = loadReady;
            continueButton.Select();
            while (!continueRequested)
                yield return null;
            continueButton.interactable = false;
            panel.gameObject.SetActive(false);
            Destroy(panel.gameObject);
            continueButton = null;
        }

        void ContinueToGame()
        {
            if (!isActiveAndEnabled || entryPhase != EntryPhase.Error || !loadReady || continueRequested)
                return;
            continueRequested = true;
            if (continueButton != null)
                continueButton.interactable = false;
        }

        IEnumerator Prepare(AsyncOperation op)
        {
            float tipTimer = 0f;
            int tipIndex = Random.Range(0, Tips.Length);
            using (var prewarm = PrewarmSteps().GetEnumerator())
            {
                bool done = false;
                while (!loadReady)
                {
                    if (!done)
                        done = !prewarm.MoveNext();
                    loadProgress = Mathf.Min(op != null ? Mathf.Clamp01(op.progress / 0.9f) : 1f, done ? 1f : 0.85f);
                    loadReady = done && loadProgress >= 1f;
                    tipTimer += Time.deltaTime;
                    if (tipTimer > 2.6f)
                    {
                        tipTimer = 0f;
                        tipIndex = (tipIndex + 1) % Tips.Length;
                        tipText.text = Tips[tipIndex];
                    }
                    yield return null;
                }
            }
        }

        IEnumerator MoveWalker(Vector3 destination, float duration, Quaternion facing)
        {
            Vector3 origin = walker.transform.position;
            Quaternion rotation = walker.transform.rotation;
            yield return Tween.Run(duration, t => walker.SetStandingLocomotionPose(Vector3.Lerp(origin, destination, Smooth(t)), Quaternion.Slerp(rotation, facing, Smooth(t))));
            walker.SetWalking(0f);
        }

        IEnumerator AnimateDoor(float duration, float fromAngle, float toAngle, float fromLever, float toLever)
        {
            doorMotionFailed = false;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (walker == null || !walker.isActiveAndEnabled || gripGeneration == 0)
                {
                    doorMotionFailed = true;
                    Debug.LogError("[Bluff] Loading door stopped: tracked grip became invalid.", this);
                    yield break;
                }
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                float t = Smooth(elapsed / duration);
                if(Mathf.Abs(toAngle-fromAngle)>.1f) entryProgress=Mathf.Lerp(.4f,.7f,t);
                Quaternion previousDoor = doorPanel.localRotation;
                Quaternion previousLever = handleLever.localRotation;
                Vector3 previousRoot = walker.transform.position;
                Quaternion previousFacing = walker.transform.rotation;
                doorPanel.localRotation = Quaternion.Euler(0f, Mathf.Lerp(fromAngle, toAngle, t), 0f);
                handleLever.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromLever, toLever, t));
                walker.SetStandingLocomotionPose(GripRootPosition(), GripFacing());
                if (!walker.CanTrackGrip(handleGrip))
                {
                    doorPanel.localRotation = previousDoor;
                    handleLever.localRotation = previousLever;
                    walker.SetStandingLocomotionPose(previousRoot, previousFacing);
                    doorMotionFailed = true;
                    Debug.LogError("[Bluff] Loading door stopped before exceeding arm reach.", this);
                    yield break;
                }
                if (!walker.SetTrackedGrip(gripGeneration, 1f, 1f))
                {
                    doorMotionFailed = true;
                    yield break;
                }
                yield return null;
            }
            walker.SetWalking(0f);
        }

        IEnumerator WaitForGripContact(int generation, float timeout)
        {
            float elapsed = 0f;
            while (elapsed < timeout && !walker.HasGripContact(generation))
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        Vector3 GripRootPosition()
        {
            // Shoulder stays in front of the handle as two short side/back steps clear the swing.
            var position=handleGrip.position-GripFacing()*new Vector3(.20f,0f,.27f);
            position.y=0f;
            return position;
        }

        Quaternion GripFacing()
        {
            float swing=Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(0f,doorPanel.localEulerAngles.y))/Mathf.Abs(OpenAngle));
            return Quaternion.Euler(0f,Mathf.Lerp(0f,-62f,Smooth(swing)),0f);
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

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
            percentText.text = (entered && loadReady ? 100 : Mathf.Min(99, Mathf.FloorToInt(value * 100f))) + "%";
        }

        void Update()
        {
            float target = entered && loadReady ? 1f : Mathf.Min(0.99f, loadProgress * 0.75f + entryProgress * 0.24f);
            progressShown = Mathf.MoveTowards(progressShown, target, Time.deltaTime * 0.8f);
            SetProgress(progressShown);
        }

        protected override void OnDisable()
        {
            StopAllCoroutines();
            if (walker != null && gripGeneration != 0)
                walker.EndGripTracking(gripGeneration);
            base.OnDisable();
        }
    }
}
