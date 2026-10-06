using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bluff.Game
{
    public enum HatStyle
    {
        None,
        TopHat,
        Beret,
        Cap,
        Crown,
        Bandana,
    }

    /// <summary>Warna &amp; aksesori satu karakter.</summary>
    public struct HumanoidStyle
    {
        public Color Shirt;
        public Color Skin;
        public Color Pants;
        public Color Mask;
        public Color Hat;
        public Color Hair;
        public HatStyle HatStyle;

        static readonly Color[] Skins = { Theme.Hex("C68B59"), Theme.Hex("8D5524"), Theme.Hex("E0AC69"), Theme.Hex("A86B3C"), Theme.Hex("F1C27D") };
        static readonly HatStyle[] Hats = { HatStyle.TopHat, HatStyle.Beret, HatStyle.Cap, HatStyle.Crown, HatStyle.Bandana };

        public static HumanoidStyle ForPlayer(int playerId)
        {
            var color = Theme.PlayerColor(playerId);
            return new HumanoidStyle
            {
                Shirt = Color.Lerp(color, Color.black, 0.25f),
                Skin = Skins[playerId % Skins.Length],
                Pants = Theme.Hex("2A2F3D"),
                Mask = playerId % 2 == 0 ? Theme.Gold : Theme.Hex("E8E6F0"),
                Hat = Color.Lerp(color, Color.black, 0.55f),
                Hair = Theme.Hex("2B1B12"),
                HatStyle = Hats[playerId % Hats.Length],
            };
        }
    }

    public sealed class Humanoid : MonoBehaviour
    {
        const float UpperArm = 0.27f;
        const float Forearm = 0.25f;
        const float Thigh = 0.42f;
        const float Shin = 0.42f;

        public enum ArmMode
        {
            HoldCards,
            Reach,
            Aim,
            Celebrate,
            FacePalm,
            Wave,
            Rest,
            Walk,
        }

        // Sendi.
        Transform hips;
        Transform spine;
        Transform chest;
        Transform neck;
        Transform head;
        Transform shoulderL;
        Transform shoulderR;
        Transform elbowL;
        Transform elbowR;
        Transform handL;
        Transform handR;
        Transform hipL;
        Transform hipR;
        Transform kneeL;
        Transform kneeR;

        Material maskMaterial;
        Color maskColor;
        Transform eyeL;
        Transform eyeR;
        GameObject gun;
        Transform muzzle;
        Vector3 muzzleOffset;
        Quaternion barrelLocal;

        bool seated;
        float phase;
        float blinkTimer = 2f;
        float maskFlash;
        Color maskFlashColor;

        // Target animasi (dihaluskan setiap frame).
        ArmMode armMode = ArmMode.HoldCards;
        Vector3 reachTarget;
        Vector3 aimTarget;
        Vector3 lookTarget;
        bool hasLookTarget;
        float lean;
        float leanTarget;
        float slump;
        float slumpTarget;
        float bounce;
        Vector3 handLPos;
        Vector3 handRPos;
        bool handsInitialized;
        float walkSpeed;
        Quaternion wristLRotation;
        Quaternion wristRRotation;
        readonly Transform[] fingersL = new Transform[5];
        readonly Transform[] fingersR = new Transform[5];

        enum HandAction { None, Packet, Shot, Recoil }

        const float PacketGripTime = 0.38f;
        const float PacketReleaseTime = 0.74f;
        const float PacketEndTime = 1.1f;
        const float DrawTime = 0.4f;
        const float CockTime = 0.58f;
        const float FireTime = 0.74f;
        const float KickTime = 0.82f;
        const float RecoverTime = 1.07f;
        const float ShotEndTime = 1.32f;
        static readonly Quaternion FanRotation = Quaternion.Euler(-18f, 0f, 0f);
        static readonly Quaternion LeftWrist = Quaternion.Euler(-18f, 0f, 65f);
        static readonly Quaternion RightWrist = Quaternion.Euler(-18f, 0f, -65f);

        HandAction action;
        int poseGeneration;
        float actionTime;
        float actionSpeed = 1f;
        Vector3 startHandL;
        Vector3 startHandR;
        Quaternion startWristL;
        Quaternion startWristR;
        Vector3 packetDestination;
        System.Action gripCallback;
        System.Action releaseCallback;
        System.Func<Vector3> shotTarget;
        System.Action<Vector3,Vector3> fireCallback;
        bool gripDelivered;
        bool releaseDelivered;
        bool fireDelivered;
        bool cylinderAdvanced;
        int cylinderIndex;
        int shotCylinderStart;
        float cock;
        float trigger;
        float cylinderTurn;
        float wristKick;
        float shotWeight;
        float gripR;
        float gripRTarget;
        Vector3 releaseWrist;
        Vector3 pinchPosition;
        Quaternion pinchRotation;

        public Transform CardHold { get; private set; }
        public Transform PlayHold { get; private set; }

        public Transform Head => head;
        public Transform RightHand => handR;
        public Transform Muzzle => muzzle;
        public HumanoidStyle Style { get; private set; }

        public static Humanoid Create(Transform parent, string name, HumanoidStyle style, bool seated)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Humanoid>();
            h.Build(style, seated);
            return h;
        }

        readonly List<Mesh> meshes = new List<Mesh>();

        void Build(HumanoidStyle style, bool isSeated)
        {
            Style = style;
            seated = isSeated;
            phase = Random.Range(0f, 10f);
            var skin = Art.Lit(style.Skin, 0.32f, 0f, null, null, new Color(1f, 0.83f, 0.68f, 0.12f));
            var jacketColor = Color.Lerp(style.Shirt, Theme.Hex("18202A"), 0.48f);
            var jacket = Art.Fabric(jacketColor);
            var pants = Art.Fabric(Color.Lerp(style.Pants, Theme.Hex("1C2028"), 0.35f));
            var linen = Art.Fabric(Theme.Hex("C8C1AD"));
            var buttons = Art.Metal(Theme.Hex("96815D"), 0.42f);
            maskColor = style.Mask;
            maskMaterial = Art.LitInstance(style.Mask, 0.5f, 0.22f, null, Color.black, new Color(1f, 0.94f, 0.8f, 0.14f));

            hips = Joint("Hips", transform, isSeated ? new Vector3(0f, 0.52f, -0.06f) : new Vector3(0f, 0.92f, 0f));
            spine = Joint("Spine", hips, new Vector3(0f, 0.1f, 0f));
            chest = Joint("Chest", spine, new Vector3(0f, 0.2f, 0f));
            neck = Joint("Neck", chest, new Vector3(0f, 0.2f, 0f));
            head = Joint("Head", neck, new Vector3(0f, 0.07f, 0f));

            Loft("TrouserSeat", hips, Vector3.zero, new[]
            {
                new Vector4(0.12f, -0.09f, 0.09f, 0f), new Vector4(0.165f, -0.03f, 0.118f, -0.008f),
                new Vector4(0.157f, 0.055f, 0.112f, 0f), new Vector4(0.143f, 0.08f, 0.1f, 0f),
            }, pants, 0.75f);
            BuildJacket(jacket, jacketColor, linen, buttons);
            Loft("NeckMesh", neck, Vector3.zero, new[]
            {
                new Vector4(0.047f, -0.028f, 0.044f, 0f), new Vector4(0.041f, 0.025f, 0.041f, 0f),
                new Vector4(0.048f, 0.085f, 0.044f, -0.006f),
            }, skin);
            BuildHead(style, skin);
            BuildHat(style);

            shoulderL = Joint("ShoulderL", chest, new Vector3(-0.2f, 0.15f, 0f));
            shoulderR = Joint("ShoulderR", chest, new Vector3(0.2f, 0.15f, 0f));
            elbowL = Joint("ElbowL", shoulderL, new Vector3(0f, -UpperArm, 0f));
            elbowR = Joint("ElbowR", shoulderR, new Vector3(0f, -UpperArm, 0f));
            handL = Joint("HandL", elbowL, new Vector3(0f, -Forearm, 0f));
            handR = Joint("HandR", elbowR, new Vector3(0f, -Forearm, 0f));
            foreach (var shoulder in new[] { shoulderL, shoulderR })
                Loft("TailoredSleeve", shoulder, Vector3.zero, new[]
                {
                    new Vector4(0.047f, -UpperArm - 0.008f, 0.049f, 0f),
                    new Vector4(0.055f, -0.16f, 0.056f, -0.005f),
                    new Vector4(0.065f, -0.045f, 0.066f, 0f),
                    new Vector4(0.06f, 0.015f, 0.057f, 0f), new Vector4(0.032f, 0.045f, 0.037f, 0f),
                }, jacket, 0.9f);
            foreach (var elbow in new[] { elbowL, elbowR })
            {
                Loft("SleeveForearm", elbow, Vector3.zero, new[]
                {
                    new Vector4(0.035f, -Forearm + 0.013f, 0.036f, 0f),
                    new Vector4(0.036f, -Forearm + 0.042f, 0.038f, 0f),
                    new Vector4(0.046f, -0.11f, 0.048f, 0f), new Vector4(0.05f, -0.017f, 0.05f, 0f),
                    new Vector4(0.043f, 0.018f, 0.044f, 0f),
                }, jacket, 0.9f);
                Loft("ShirtCuff", elbow, Vector3.zero, new[]
                {
                    new Vector4(0.032f, -Forearm - 0.006f, 0.033f, 0f),
                    new Vector4(0.034f, -Forearm + 0.003f, 0.035f, 0f),
                    new Vector4(0.034f, -Forearm + 0.026f, 0.035f, 0f),
                }, linen, 0.8f);
                for (int i = 0; i < 3; i++)
                    Button("SleeveButton", elbow, new Vector3(0.033f, -0.197f + i * 0.018f, 0.022f), 0.004f, buttons);
            }
            BuildHand(handL, 1f, skin, style.Skin);
            BuildHand(handR, -1f, skin, style.Skin);

            hipL = Joint("HipL", hips, new Vector3(-0.09f, -0.04f, 0f));
            hipR = Joint("HipR", hips, new Vector3(0.09f, -0.04f, 0f));
            kneeL = Joint("KneeL", hipL, new Vector3(0f, -Thigh, 0f));
            kneeR = Joint("KneeR", hipR, new Vector3(0f, -Thigh, 0f));
            foreach (var hip in new[] { hipL, hipR })
                Loft("TrouserThigh", hip, Vector3.zero, new[]
                {
                    new Vector4(0.058f, -Thigh - 0.016f, 0.061f, 0f),
                    new Vector4(0.064f, -0.3f, 0.065f, 0f), new Vector4(0.078f, -0.12f, 0.082f, -0.007f),
                    new Vector4(0.081f, 0.018f, 0.086f, -0.006f),
                }, pants, 0.8f);
            foreach (var knee in new[] { kneeL, kneeR })
            {
                Loft("TrouserShin", knee, Vector3.zero, new[]
                {
                    new Vector4(0.048f, -Shin + 0.016f, 0.048f, 0f),
                    new Vector4(0.05f, -Shin + 0.034f, 0.049f, 0f),
                    new Vector4(0.047f, -0.32f, 0.048f, 0f), new Vector4(0.058f, -0.17f, 0.062f, -0.006f),
                    new Vector4(0.059f, -0.02f, 0.061f, 0f), new Vector4(0.052f, 0.015f, 0.053f, 0f),
                }, pants, 0.8f);
                Art.Tube("PressedCrease", knee, new[]
                {
                    new Vector3(0f, -0.04f, 0.061f), new Vector3(0f, -0.18f, 0.056f),
                    new Vector3(0f, -0.32f, 0.048f), new Vector3(0f, -0.395f, 0.049f),
                }, 0.0012f, pants, 6);
                BuildShoe(knee);
            }

            CardHold = Joint("CardHold", handL, new Vector3(0.024f, -0.04f, 0.054f));
            CardHold.localRotation = Quaternion.Inverse(LeftWrist) * FanRotation;
            PlayHold = Joint("PlayHold", handR, new Vector3(-0.024f, -0.04f, 0.054f));
            PlayHold.localRotation = Quaternion.Inverse(RightWrist) * FanRotation;
            BuildGun();
            BatchVisuals();

            if (seated)
            {
                hipL.localRotation = Quaternion.Euler(-88f, -4f, 0f);
                hipR.localRotation = Quaternion.Euler(-88f, 4f, 0f);
                kneeL.localRotation = Quaternion.Euler(88f, 0f, 0f);
                kneeR.localRotation = Quaternion.Euler(88f, 0f, 0f);
            }
            UpdateArms(0f);
            UpdateGrip(0f);
        }

        static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        void BuildJacket(Material jacket, Color color, Material linen, Material buttons)
        {
            var waistcoat = Art.Fabric(Color.Lerp(color, Theme.Hex("333238"), 0.6f));
            var facing = Art.Fabric(Color.Lerp(color, Theme.Hex("11161D"), 0.46f));
            var seam = Art.Fabric(Color.Lerp(color, Theme.Hex("A49B87"), 0.12f));
            Loft("JacketSkirt", spine, Vector3.zero, new[]
            {
                new Vector4(0.162f, -0.065f, 0.119f, -0.006f), new Vector4(0.169f, -0.045f, 0.124f, -0.006f),
                new Vector4(0.148f, 0.07f, 0.106f, 0f), new Vector4(0.151f, 0.12f, 0.108f, 0f),
            }, jacket, 0.68f);
            Loft("TailoredJacket", chest, Vector3.zero, new[]
            {
                new Vector4(0.147f, -0.13f, 0.106f, 0f), new Vector4(0.152f, -0.08f, 0.109f, 0f),
                new Vector4(0.181f, 0.045f, 0.123f, -0.006f), new Vector4(0.192f, 0.13f, 0.113f, -0.008f),
                new Vector4(0.171f, 0.16f, 0.098f, -0.01f), new Vector4(0.067f, 0.187f, 0.062f, 0f),
            }, jacket, 0.7f);
            Panel("Waistcoat", chest, new[]
            {
                new Vector3(-0.08f, -0.12f, 0.112f), new Vector3(0f, -0.148f, 0.116f),
                new Vector3(0.08f, -0.12f, 0.112f), new Vector3(0.1f, 0.06f, 0.124f),
                new Vector3(0.057f, 0.15f, 0.108f), new Vector3(-0.057f, 0.15f, 0.108f),
                new Vector3(-0.1f, 0.06f, 0.124f),
            }, 0.008f, waistcoat);
            Panel("ShirtFront", chest, new[]
            {
                new Vector3(-0.052f, 0.165f, 0.11f), new Vector3(0f, 0.014f, 0.135f),
                new Vector3(0.052f, 0.165f, 0.11f),
            }, 0.005f, linen);
            Loft("ShirtCollar", chest, Vector3.zero, new[]
            {
                new Vector4(0.06f, 0.172f, 0.058f, 0f), new Vector4(0.058f, 0.207f, 0.052f, 0f),
            }, linen, 0.82f);
            for (int side = -1; side <= 1; side += 2)
            {
                Panel("NotchedLapel", chest, new[]
                {
                    new Vector3(side * 0.025f, -0.081f, 0.131f), new Vector3(side * 0.139f, 0.089f, 0.119f),
                    new Vector3(side * 0.115f, 0.118f, 0.122f), new Vector3(side * 0.061f, 0.144f, 0.123f),
                }, 0.008f, facing);
                Panel("LapelGorge", chest, new[]
                {
                    new Vector3(side * 0.061f, 0.145f, 0.123f), new Vector3(side * 0.111f, 0.135f, 0.115f),
                    new Vector3(side * 0.126f, 0.15f, 0.102f), new Vector3(side * 0.053f, 0.189f, 0.073f),
                }, 0.006f, facing);
                Panel("CollarPoint", chest, new[]
                {
                    new Vector3(side * 0.003f, 0.153f, 0.131f), new Vector3(side * 0.032f, 0.112f, 0.141f),
                    new Vector3(side * 0.055f, 0.166f, 0.121f), new Vector3(side * 0.026f, 0.188f, 0.099f),
                }, 0.004f, linen);
                Art.Tube("LapelStitch", chest, new[]
                {
                    new Vector3(side * 0.033f, -0.057f, 0.136f), new Vector3(side * 0.128f, 0.086f, 0.125f),
                    new Vector3(side * 0.112f, 0.111f, 0.128f),
                }, 0.0011f, seam, 6);
                Art.BeveledBox("JacketPocketWelt", chest, new Vector3(side * 0.111f, -0.074f, 0.114f),
                    new Vector3(0.056f, 0.011f, 0.008f), 0.002f, facing);
            }
            var tie = Art.Fabric(Theme.Hex("3D2931"));
            Panel("TieBlade", chest, new[]
            {
                new Vector3(0f, 0.028f, 0.144f), new Vector3(0.013f, 0.051f, 0.145f),
                new Vector3(0.006f, 0.136f, 0.141f), new Vector3(-0.006f, 0.136f, 0.141f),
                new Vector3(-0.013f, 0.051f, 0.145f),
            }, 0.004f, tie);
            Art.BeveledBox("TieKnot", chest, new Vector3(0f, 0.144f, 0.141f), new Vector3(0.025f, 0.026f, 0.016f), 0.005f, tie);
            for (int i = 0; i < 4; i++)
                Button("WaistcoatButton", chest, new Vector3(0f, 0.007f - i * 0.034f, 0.132f - i * 0.004f), 0.0048f, buttons);
            Art.Tube("WaistcoatPlacket", chest, new[]
            {
                new Vector3(-0.012f, 0.015f, 0.137f), new Vector3(-0.012f, -0.06f, 0.129f),
                new Vector3(-0.012f, -0.126f, 0.122f),
            }, 0.0012f, facing, 6);
            Art.BeveledBox("BreastPocket", chest, new Vector3(-0.127f, 0.06f, 0.112f), new Vector3(0.044f, 0.009f, 0.01f), 0.002f, facing);
        }

        void BuildHead(HumanoidStyle style, Material skin)
        {
            Loft("SculptedHead", head, Vector3.zero, new[]
            {
                new Vector4(0.042f, -0.014f, 0.043f, 0.025f), new Vector4(0.075f, 0.011f, 0.07f, 0.014f),
                new Vector4(0.102f, 0.052f, 0.092f, 0.002f), new Vector4(0.118f, 0.099f, 0.104f, -0.008f),
                new Vector4(0.12f, 0.154f, 0.111f, -0.012f), new Vector4(0.104f, 0.214f, 0.104f, -0.018f),
                new Vector4(0.069f, 0.251f, 0.071f, -0.018f), new Vector4(0.015f, 0.267f, 0.02f, -0.018f),
            }, skin, 0.88f);
            var hair = Art.Fabric(style.Hair);
            Loft("CombedHair", head, Vector3.zero, new[]
            {
                new Vector4(0.095f, 0.194f, 0.1f, -0.029f), new Vector4(0.112f, 0.214f, 0.109f, -0.02f),
                new Vector4(0.09f, 0.254f, 0.088f, -0.019f), new Vector4(0.027f, 0.276f, 0.032f, -0.019f),
            }, hair, 0.9f);
            var crease = Art.Lit(Color.Lerp(style.Skin, Theme.Hex("512E27"), 0.5f), 0.24f);
            foreach (int side in new[] { -1, 1 })
            {
                Loft("Ear", head, new Vector3(side * 0.119f, 0.095f, -0.015f), new[]
                {
                    new Vector4(0.007f, -0.031f, 0.012f, 0f), new Vector4(0.017f, -0.01f, 0.02f, 0f),
                    new Vector4(0.018f, 0.021f, 0.02f, 0f), new Vector4(0.01f, 0.035f, 0.012f, 0f),
                }, skin);
                Art.Tube("EarFold", head, new[]
                {
                    new Vector3(side * 0.125f, 0.078f, 0.004f), new Vector3(side * 0.132f, 0.1f, 0.007f),
                    new Vector3(side * 0.13f, 0.117f, 0.001f),
                }, 0.002f, crease, 6);
                Art.BeveledBox("Sideburn", head, new Vector3(side * 0.11f, 0.156f, -0.026f), new Vector3(0.018f, 0.065f, 0.027f), 0.008f, hair);
            }
            Panel("Nose", head, new[]
            {
                new Vector3(-0.016f, 0.07f, 0.108f), new Vector3(0f, 0.066f, 0.138f),
                new Vector3(0.016f, 0.07f, 0.108f), new Vector3(0f, 0.116f, 0.124f),
            }, 0.016f, skin);
            Art.Tube("Mouth", head, new[]
            {
                new Vector3(-0.025f, 0.037f, 0.094f), new Vector3(-0.01f, 0.035f, 0.101f),
                new Vector3(0f, 0.036f, 0.103f), new Vector3(0.012f, 0.035f, 0.101f),
                new Vector3(0.025f, 0.037f, 0.094f),
            }, 0.0018f, crease, 6);
            BuildMask();
            var eyes = Art.Lit(Theme.Hex("191C20"), 0.72f);
            eyeL = Art.Primitive(PrimitiveType.Sphere, "EyeL", head, new Vector3(-0.05f, 0.13f, 0.109f), new Vector3(0.05f, 0.032f, 0.02f), eyes).transform;
            eyeR = Art.Primitive(PrimitiveType.Sphere, "EyeR", head, new Vector3(0.05f, 0.13f, 0.109f), new Vector3(0.05f, 0.032f, 0.02f), eyes).transform;
            var shine = Art.Lit(Theme.Hex("DED8C5"), 0.7f);
            foreach (var eye in new[] { eyeL, eyeR })
                Art.Primitive(PrimitiveType.Sphere, "Catchlight", eye, new Vector3(-0.13f, 0.15f, 0.44f), new Vector3(0.13f, 0.18f, 0.12f), shine);
        }

        static Vector3 MaskPoint(float x, float y)
        {
            return new Vector3(x, y, 0.14f - 2.1f * x * x - 0.2f * (y - 0.13f) * (y - 0.13f));
        }

        void BuildMask()
        {
            var edge = Art.Metal(Color.Lerp(maskColor, Theme.Hex("705C42"), 0.47f), 0.46f);
            var strap = Art.Leather(Theme.Hex("211D1C"));
            for (int side = -1; side <= 1; side += 2)
            {
                const int segments = 40;
                var vertices = new List<Vector3>();
                var uv = new List<Vector2>();
                var triangles = new List<int>();
                var inner = new Vector3[segments + 1];
                var outer = new Vector3[segments + 1];
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float c = Mathf.Cos(a);
                    float s = Mathf.Sin(a);
                    inner[i] = MaskPoint(side * (0.05f + 0.029f * c), 0.13f + 0.019f * s * (0.78f + 0.22f * Mathf.Abs(s)) + 0.003f * c);
                    outer[i] = MaskPoint(side * (0.05f + (c > 0f ? 0.08f : 0.044f) * c),
                        0.132f + (s > 0f ? 0.056f : 0.044f) * s + 0.014f * Mathf.Max(0f, c));
                    vertices.Add(inner[i]);
                    vertices.Add(outer[i]);
                    vertices.Add(inner[i] - Vector3.forward * 0.009f);
                    vertices.Add(outer[i] - Vector3.forward * 0.009f);
                    for (int j = 0; j < 4; j++)
                        uv.Add(new Vector2(i / (float)segments, j % 2));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = i * 4;
                    int b = a + 4;
                    AddQuad(triangles, a, a + 1, b + 1, b, side < 0);
                    AddQuad(triangles, a + 3, a + 2, b + 2, b + 3, side < 0);
                    AddQuad(triangles, a + 2, a, b, b + 2, side < 0);
                    AddQuad(triangles, a + 1, a + 3, b + 3, b + 1, side < 0);
                }
                MeshObject("MaskShell", head, Vector3.zero, vertices, uv, triangles, maskMaterial);
                Art.Tube("EyeOpeningRim", head, inner, 0.0018f, edge, 6);
                Art.Tube("MaskOuterRim", head, outer, 0.0015f, edge, 6);
                Art.Tube("MaskRibbon", head, new[]
                {
                    new Vector3(side * 0.125f, 0.147f, 0.102f), new Vector3(side * 0.127f, 0.15f, 0.02f),
                    new Vector3(side * 0.119f, 0.15f, -0.055f), new Vector3(side * 0.08f, 0.15f, -0.113f),
                    new Vector3(0f, 0.15f, -0.129f),
                }, 0.005f, strap, 8);
                Button("MaskRivet", head, MaskPoint(side * 0.118f, 0.147f) + Vector3.forward * 0.002f, 0.003f, edge);
            }
            Panel("MaskBridgeLeft", head, new[]
            {
                new Vector3(-0.019f, 0.159f, 0.14f), new Vector3(-0.015f, 0.109f, 0.143f),
                new Vector3(0f, 0.09f, 0.161f), new Vector3(0f, 0.175f, 0.156f),
            }, 0.009f, maskMaterial);
            Panel("MaskBridgeRight", head, new[]
            {
                new Vector3(0f, 0.175f, 0.156f), new Vector3(0f, 0.09f, 0.161f),
                new Vector3(0.015f, 0.109f, 0.143f), new Vector3(0.019f, 0.159f, 0.14f),
            }, 0.009f, maskMaterial);
        }

        void BuildHand(Transform hand, float thumbSide, Material skin, Color skinColor)
        {
            Loft("Palm", hand, Vector3.zero, new[]
            {
                new Vector4(0.026f, -0.041f, 0.018f, 0.006f), new Vector4(0.035f, -0.028f, 0.022f, 0.005f),
                new Vector4(0.033f, -0.01f, 0.022f, 0.001f), new Vector4(0.025f, 0.009f, 0.023f, 0f),
            }, skin, 0.78f, 20);
            var nails = Art.Lit(Color.Lerp(skinColor, Theme.Hex("D9B9A0"), 0.4f), 0.37f);
            var creases = Art.Lit(Color.Lerp(skinColor, Theme.Hex("725448"), 0.27f), 0.23f);
            for (int i = 0; i < 4; i++)
            {
                float x = thumbSide * (0.025f - i * 0.017f);
                float length = i == 3 ? 0.041f : i == 1 ? 0.059f : 0.053f;
                var root = new Vector3(x, -0.03f, 0.012f);
                var middle = new Vector3(x, -0.036f - length * 0.28f, 0.027f);
                var curl = new Vector3(x, -0.034f - length * 0.27f, 0.05f);
                var tip = new Vector3(x, -0.013f - length * 0.2f, 0.061f);
                var pivot = Joint("FingerPivot" + i, hand, root);
                (hand == handL ? fingersL : fingersR)[i] = pivot;
                Finger("Finger" + i, pivot, new[] { Vector3.zero, middle - root, curl - root, tip - root }, new[] { 0.009f, 0.008f, 0.007f, 0.0055f }, skin);
                var nail = Art.BeveledBox("Fingernail", pivot, tip - root + new Vector3(0f, -0.003f, -0.002f), new Vector3(0.01f, 0.009f, 0.002f), 0.001f, nails);
                nail.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
                Art.Tube("KnuckleCrease", pivot, new[]
                {
                    middle - root + new Vector3(-0.004f, 0.002f, -0.007f), middle - root + new Vector3(0f, 0.001f, -0.008f),
                    middle - root + new Vector3(0.004f, 0.002f, -0.007f),
                }, 0.0007f, creases, 5);
            }
            var thumbTip = new Vector3(thumbSide * 0.024f, -0.058f, 0.047f);
            var thumbRoot = new Vector3(thumbSide * 0.022f, -0.016f, 0.004f);
            var thumb = Joint("ThumbPivot", hand, thumbRoot);
            (hand == handL ? fingersL : fingersR)[4] = thumb;
            Finger("Thumb", thumb, new[]
            {
                Vector3.zero, new Vector3(thumbSide * 0.044f, -0.03f, 0.02f) - thumbRoot,
                new Vector3(thumbSide * 0.039f, -0.048f, 0.039f) - thumbRoot, thumbTip - thumbRoot,
            }, new[] { 0.014f, 0.012f, 0.009f, 0.0065f }, skin);
            var thumbnail = Art.BeveledBox("Thumbnail", thumb, thumbTip - thumbRoot + new Vector3(0f, 0f, 0.006f), new Vector3(0.01f, 0.012f, 0.002f), 0.001f, nails);
            thumbnail.transform.localRotation = Quaternion.Euler(0f, 0f, thumbSide * 35f);
        }

        void Finger(string name, Transform parent, Vector3[] points, float[] radii, Material material)
        {
            const int sides = 10;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int i = 0; i < points.Length; i++)
            {
                var tangent = (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(0, i - 1)]).normalized;
                var across = Vector3.Cross(tangent, Vector3.right).normalized;
                var normal = Vector3.Cross(across, tangent).normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float a = j * Mathf.PI * 2f / sides;
                    vertices.Add(points[i] + (normal * Mathf.Cos(a) + across * Mathf.Sin(a)) * radii[i]);
                    uv.Add(new Vector2(j / (float)sides, i / (float)(points.Length - 1)));
                    if (i > 0 && j < sides)
                    {
                        int current = i * (sides + 1) + j;
                        AddQuad(triangles, current - sides - 1, current, current + 1, current - sides, true);
                    }
                }
            }
            int end = (points.Length - 1) * (sides + 1);
            for (int j = 1; j < sides - 1; j++)
            {
                triangles.AddRange(new[] { 0, j + 1, j });
                triangles.AddRange(new[] { end, end + j, end + j + 1 });
            }
            MeshObject(name, parent, Vector3.zero, vertices, uv, triangles, material);
        }

        void BuildShoe(Transform knee)
        {
            var leather = Art.Leather(Theme.Hex("252020"));
            var sole = Art.Leather(Theme.Hex("100F13"));
            var welt = Art.Leather(Theme.Hex("4C3C2E"));
            var lace = Art.Fabric(Theme.Hex("141417"));
            var shoe = Joint("OxfordShoe", knee, new Vector3(0f, -Shin - 0.048f, 0f));
            Loft("Outsole", shoe, Vector3.zero, new[]
            {
                new Vector4(0.046f, 0f, 0.101f, 0.047f), new Vector4(0.053f, 0.004f, 0.108f, 0.047f),
                new Vector4(0.052f, 0.013f, 0.106f, 0.047f), new Vector4(0.048f, 0.016f, 0.102f, 0.047f),
            }, sole, 0.76f);
            Loft("Welt", shoe, Vector3.zero, new[]
            {
                new Vector4(0.051f, 0.013f, 0.105f, 0.047f), new Vector4(0.051f, 0.018f, 0.105f, 0.047f),
            }, welt, 0.76f);
            Loft("LeatherUpper", shoe, Vector3.zero, new[]
            {
                new Vector4(0.048f, 0.017f, 0.102f, 0.047f), new Vector4(0.049f, 0.028f, 0.1f, 0.047f),
                new Vector4(0.046f, 0.047f, 0.09f, 0.046f), new Vector4(0.04f, 0.063f, 0.055f, 0.012f),
                new Vector4(0.036f, 0.081f, 0.04f, 0f),
            }, leather, 0.78f);
            Art.BeveledBox("Heel", shoe, new Vector3(0f, 0.003f, -0.027f), new Vector3(0.088f, 0.016f, 0.059f), 0.006f, sole);
            Art.Tube("ToeCapSeam", shoe, new[]
            {
                new Vector3(-0.046f, 0.027f, 0.09f), new Vector3(-0.036f, 0.047f, 0.088f),
                new Vector3(0f, 0.054f, 0.086f), new Vector3(0.036f, 0.047f, 0.088f),
                new Vector3(0.046f, 0.027f, 0.09f),
            }, 0.0013f, welt, 6);
            for (int i = 0; i < 4; i++)
            {
                float z = 0.012f + i * 0.014f;
                float y = 0.081f - i * 0.006f;
                Art.Tube("Laces", shoe, new[]
                {
                    new Vector3(-0.016f, y, z), new Vector3(0f, y + 0.001f, z + 0.006f),
                    new Vector3(0.016f, y - 0.003f, z + 0.009f),
                }, 0.0018f, lace, 6);
            }
        }

        void BuildHat(HumanoidStyle style)
        {
            var hat = Art.Fabric(Color.Lerp(style.Hat, Theme.Hex("20212A"), 0.3f));
            var ribbon = Art.Fabric(Theme.Hex("18171E"));
            var band = Art.Metal(Theme.Hex("A89059"), 0.4f);
            switch (style.HatStyle)
            {
                case HatStyle.TopHat:
                    Loft("BoundBrim", head, new Vector3(0f, 0.231f, -0.014f), new[]
                    {
                        new Vector4(0.17f, 0f, 0.153f, 0f), new Vector4(0.177f, 0.004f, 0.16f, 0f),
                        new Vector4(0.172f, 0.014f, 0.155f, 0f),
                    }, hat);
                    Loft("FeltCrown", head, new Vector3(0f, 0.24f, -0.014f), new[]
                    {
                        new Vector4(0.116f, 0f, 0.112f, 0f), new Vector4(0.115f, 0.029f, 0.111f, 0f),
                        new Vector4(0.105f, 0.118f, 0.103f, 0f), new Vector4(0.115f, 0.172f, 0.113f, 0f),
                        new Vector4(0.111f, 0.18f, 0.109f, 0f),
                    }, hat);
                    Loft("GrosgrainBand", head, new Vector3(0f, 0.251f, -0.014f), new[]
                    {
                        new Vector4(0.117f, 0f, 0.113f, 0f), new Vector4(0.115f, 0.033f, 0.111f, 0f),
                    }, ribbon);
                    Art.BeveledBox("RibbonFold", head, new Vector3(-0.113f, 0.267f, 0.005f), new Vector3(0.008f, 0.026f, 0.034f), 0.003f, ribbon);
                    break;
                case HatStyle.Beret:
                    var beret = Joint("Beret", head, new Vector3(0.013f, 0.221f, -0.015f));
                    beret.localRotation = Quaternion.Euler(-3f, 0f, -11f);
                    Loft("LeatherBinding", beret, Vector3.zero, new[]
                    {
                        new Vector4(0.111f, 0f, 0.109f, 0f), new Vector4(0.116f, 0.015f, 0.113f, 0f),
                    }, Art.Leather(Theme.Hex("272126")));
                    Loft("WoolBeret", beret, Vector3.zero, new[]
                    {
                        new Vector4(0.113f, 0.01f, 0.111f, 0f), new Vector4(0.143f, 0.026f, 0.135f, 0f),
                        new Vector4(0.147f, 0.042f, 0.136f, -0.003f), new Vector4(0.113f, 0.067f, 0.111f, -0.009f),
                        new Vector4(0.044f, 0.083f, 0.047f, -0.018f), new Vector4(0.006f, 0.085f, 0.006f, -0.018f),
                    }, hat);
                    Art.Tube("BeretStem", beret, new[] { new Vector3(0f, 0.083f, -0.018f), new Vector3(0.005f, 0.098f, -0.02f) }, 0.003f, hat, 8);
                    break;
                case HatStyle.Cap:
                    Loft("CapBand", head, new Vector3(0f, 0.214f, -0.012f), new[]
                    {
                        new Vector4(0.117f, 0f, 0.112f, 0f), new Vector4(0.121f, 0.021f, 0.117f, 0f),
                    }, ribbon);
                    Loft("FlatCap", head, new Vector3(0f, 0.222f, -0.012f), new[]
                    {
                        new Vector4(0.121f, 0f, 0.131f, 0.012f), new Vector4(0.137f, 0.018f, 0.145f, 0.012f),
                        new Vector4(0.121f, 0.048f, 0.12f, -0.009f), new Vector4(0.077f, 0.073f, 0.073f, -0.029f),
                        new Vector4(0.012f, 0.083f, 0.016f, -0.035f),
                    }, hat);
                    BuildVisor(hat);
                    Art.Tube("CapCenterSeam", head, new[]
                    {
                        new Vector3(0f, 0.235f, 0.145f), new Vector3(0f, 0.272f, 0.092f),
                        new Vector3(0f, 0.3f, 0.021f), new Vector3(0f, 0.306f, -0.047f),
                        new Vector3(0f, 0.28f, -0.113f), new Vector3(0f, 0.236f, -0.145f),
                    }, 0.0014f, ribbon, 6);
                    break;
                case HatStyle.Crown:
                    BuildCoronet(band, ribbon);
                    break;
                case HatStyle.Bandana:
                    Loft("FoldedBandana", head, new Vector3(0f, 0.179f, -0.012f), new[]
                    {
                        new Vector4(0.117f, 0f, 0.109f, 0f), new Vector4(0.124f, 0.009f, 0.116f, 0f),
                        new Vector4(0.123f, 0.019f, 0.115f, 0f), new Vector4(0.118f, 0.026f, 0.112f, 0f),
                        new Vector4(0.122f, 0.034f, 0.114f, 0f), new Vector4(0.111f, 0.046f, 0.106f, 0f),
                    }, hat);
                    Art.BeveledBox("BandanaKnot", head, new Vector3(0.025f, 0.205f, -0.131f), new Vector3(0.04f, 0.03f, 0.027f), 0.009f, hat);
                    Panel("BandanaTailLeft", head, new[]
                    {
                        new Vector3(0.012f, 0.204f, -0.143f), new Vector3(-0.029f, 0.13f, -0.151f),
                        new Vector3(-0.004f, 0.144f, -0.153f), new Vector3(0.036f, 0.195f, -0.146f),
                    }, 0.005f, hat);
                    Panel("BandanaTailRight", head, new[]
                    {
                        new Vector3(0.029f, 0.197f, -0.149f), new Vector3(0.046f, 0.122f, -0.162f),
                        new Vector3(0.064f, 0.14f, -0.157f), new Vector3(0.047f, 0.2f, -0.142f),
                    }, 0.005f, hat);
                    break;
            }
        }

        void BuildVisor(Material material)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            const int segments = 24;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-1.2f, 1.2f, i / (float)segments);
                float x = Mathf.Sin(angle);
                float z = Mathf.Cos(angle);
                vertices.Add(new Vector3(x * 0.108f, 0.219f, z * 0.088f + 0.01f));
                vertices.Add(new Vector3(x * 0.119f, 0.207f + Mathf.Abs(x) * 0.01f, z * 0.166f + 0.01f));
                vertices.Add(vertices[vertices.Count - 2] - Vector3.up * 0.006f);
                vertices.Add(vertices[vertices.Count - 2] - Vector3.up * 0.006f);
                for (int j = 0; j < 4; j++)
                    uv.Add(new Vector2(i / (float)segments, j % 2));
                if (i == segments)
                    continue;
                int a = i * 4;
                AddQuad(triangles, a, a + 1, a + 5, a + 4, false);
                AddQuad(triangles, a + 3, a + 2, a + 6, a + 7, false);
                AddQuad(triangles, a + 1, a + 3, a + 7, a + 5, false);
                AddQuad(triangles, a + 2, a, a + 4, a + 6, false);
            }
            AddQuad(triangles, 0, 2, 3, 1, false);
            int end = segments * 4;
            AddQuad(triangles, end, end + 1, end + 3, end + 2, false);
            MeshObject("CurvedVisor", head, Vector3.zero, vertices, uv, triangles, material);
        }

        void BuildCoronet(Material metal, Material lining)
        {
            Loft("CoronetLining", head, new Vector3(0f, 0.234f, -0.013f), new[]
            {
                new Vector4(0.109f, 0f, 0.109f, 0f), new Vector4(0.111f, 0.023f, 0.111f, 0f),
            }, lining);
            const int segments = 100;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var lip = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float height = 0.024f + 0.035f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a * 5f), 2f);
                var outward = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var bottom = new Vector3(0f, 0.242f, -0.013f);
                vertices.Add(bottom + outward * 0.115f);
                vertices.Add(bottom + outward * 0.118f + Vector3.up * height);
                vertices.Add(bottom + outward * 0.109f);
                vertices.Add(bottom + outward * 0.112f + Vector3.up * height);
                lip[i] = vertices[vertices.Count - 3];
                for (int j = 0; j < 4; j++)
                    uv.Add(new Vector2(i / (float)segments, j % 2));
                if (i == segments)
                    continue;
                int b = i * 4;
                AddQuad(triangles, b, b + 4, b + 5, b + 1, false);
                AddQuad(triangles, b + 2, b + 3, b + 7, b + 6, false);
                AddQuad(triangles, b + 1, b + 5, b + 7, b + 3, false);
                AddQuad(triangles, b, b + 2, b + 6, b + 4, false);
            }
            MeshObject("ScallopedCoronet", head, Vector3.zero, vertices, uv, triangles, metal);
            Art.Tube("RolledCoronetEdge", head, lip, 0.002f, metal, 8);
        }

        void BuildGun()
        {
            gun = new GameObject("Revolver");
            var t = gun.transform;
            t.SetParent(handR, false);
            t.localPosition = new Vector3(0f, -0.077f, 0.062f);
            t.localRotation = Quaternion.FromToRotation(Vector3.forward, Vector3.down);
            muzzle = RevolverModel.Build(t);
            muzzleOffset = handR.InverseTransformPoint(muzzle.position);
            barrelLocal = Quaternion.Inverse(handR.rotation) * muzzle.rotation;
            gun.SetActive(false);
        }

        static void Button(string name, Transform parent, Vector3 position, float radius, Material material)
        {
            var button = Art.Lathe(name, parent, position, new[]
            {
                new Vector2(0f, -0.002f), new Vector2(radius * 0.86f, -0.002f), new Vector2(radius, -0.001f),
                new Vector2(radius, 0.001f), new Vector2(radius * 0.75f, 0.0025f), new Vector2(0f, 0.0025f),
            }, material, 16);
            button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        GameObject Loft(string name, Transform parent, Vector3 position, Vector4[] profile, Material material, float roundness = 1f, int segments = 32)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++)
            {
                var ring = profile[row];
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float x = Mathf.Sin(a);
                    float z = Mathf.Cos(a);
                    vertices.Add(new Vector3(Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), roundness) * ring.x, ring.y,
                        Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z), roundness) * ring.z + ring.w));
                    uv.Add(new Vector2(i / (float)segments, row / (float)(profile.Length - 1)));
                    if (row > 0 && i < segments)
                    {
                        int b = row * (segments + 1) + i;
                        AddQuad(triangles, b - segments - 1, b - segments, b + 1, b, false);
                    }
                }
            }
            for (int cap = 0; cap < 2; cap++)
            {
                int row = cap == 0 ? 0 : profile.Length - 1;
                int center = vertices.Count;
                vertices.Add(new Vector3(0f, profile[row].y, profile[row].w));
                uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    var point = vertices[row * (segments + 1) + i];
                    vertices.Add(point);
                    uv.Add(new Vector2(point.x / profile[row].x * 0.5f + 0.5f, (point.z - profile[row].w) / profile[row].z * 0.5f + 0.5f));
                    if (i < segments)
                        triangles.AddRange(cap == 0 ? new[] { center, center + i + 2, center + i + 1 } : new[] { center, center + i + 1, center + i + 2 });
                }
            }
            var go = MeshObject(name, parent, position, vertices, uv, triangles, material);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var normals = mesh.normals;
            for (int row = 0; row < profile.Length; row++)
            {
                int a = row * (segments + 1);
                var normal = (normals[a] + normals[a + segments]).normalized;
                normals[a] = normal;
                normals[a + segments] = normal;
            }
            mesh.normals = normals;
            mesh.RecalculateTangents();
            return go;
        }

        void Panel(string name, Transform parent, Vector3[] outline, float depth, Material material)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            float area = 0f;
            for (int i = 0; i < outline.Length; i++)
            {
                var next = outline[(i + 1) % outline.Length];
                area += outline[i].x * next.y - next.x * outline[i].y;
            }
            if (area < 0f)
                System.Array.Reverse(outline);
            for (int face = 0; face < 2; face++)
            {
                for (int i = 0; i < outline.Length; i++)
                {
                    vertices.Add(outline[i] - Vector3.forward * (face * depth));
                    uv.Add(new Vector2(outline[i].x * 4f, outline[i].y * 4f));
                }
                int start = face * outline.Length;
                for (int i = 1; i < outline.Length - 1; i++)
                    triangles.AddRange(face == 0 ? new[] { start, start + i, start + i + 1 } : new[] { start, start + i + 1, start + i });
            }
            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                int start = vertices.Count;
                vertices.Add(outline[i]);
                vertices.Add(outline[i] - Vector3.forward * depth);
                vertices.Add(outline[next] - Vector3.forward * depth);
                vertices.Add(outline[next]);
                uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                AddQuad(triangles, start, start + 1, start + 2, start + 3, false);
            }
            MeshObject(name, parent, Vector3.zero, vertices, uv, triangles, material);
        }

        static void AddQuad(List<int> triangles, int a, int b, int c, int d, bool reverse)
        {
            triangles.AddRange(reverse ? new[] { a, c, b, a, d, c } : new[] { a, b, c, a, c, d });
        }

        GameObject MeshObject(string name, Transform parent, Vector3 position, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        void BatchVisuals()
        {
            var anchors = new List<Transform> { hips, spine, chest, neck, head, shoulderL, shoulderR, elbowL, elbowR, handL, handR, hipL, hipR, kneeL, kneeR };
            anchors.AddRange(fingersL);
            anchors.AddRange(fingersR);
            foreach (var anchor in anchors)
            {
                var groups = new Dictionary<Material, List<MeshFilter>>();
                foreach (var filter in anchor.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                        continue;
                    var part = filter.transform;
                    if (part.IsChildOf(gun.transform) || part.IsChildOf(eyeL) || part.IsChildOf(eyeR))
                        continue;
                    var owner = part.parent;
                    while (owner != null && !anchors.Contains(owner))
                        owner = owner.parent;
                    if (owner != anchor)
                        continue;
                    var material = renderer.sharedMaterial;
                    if (!groups.TryGetValue(material, out var filters))
                    {
                        filters = new List<MeshFilter>();
                        groups.Add(material, filters);
                    }
                    filters.Add(filter);
                }
                foreach (var group in groups)
                {
                    if (group.Value.Count < 2)
                        continue;
                    var combine = new CombineInstance[group.Value.Count];
                    for (int i = 0; i < combine.Length; i++)
                        combine[i] = new CombineInstance
                        {
                            mesh = group.Value[i].sharedMesh,
                            transform = anchor.worldToLocalMatrix * group.Value[i].transform.localToWorldMatrix,
                        };
                    var mesh = new Mesh { name = anchor.name + "TailoredSurface" };
                    mesh.CombineMeshes(combine, true, true);
                    mesh.RecalculateBounds();
                    meshes.Add(mesh);
                    var surface = Joint(mesh.name, anchor, Vector3.zero);
                    surface.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    surface.gameObject.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
                    foreach (var filter in group.Value)
                    {
                        var renderer = filter.GetComponent<MeshRenderer>();
                        renderer.enabled = false;
                        var source = filter.sharedMesh;
                        filter.sharedMesh = null;
                        Art.ReleaseOwnedMesh(filter);
                        if (meshes.Remove(source))
                            Destroy(source);
                        Destroy(renderer);
                        Destroy(filter);
                    }
                }
            }
        }

        // ================================================================== kontrol animasi

        public void LookAt(Vector3 worldPoint)
        {
            lookTarget = worldPoint;
            hasLookTarget = true;
        }

        public void HoldCards() => SetArmMode(ArmMode.HoldCards);

        public void SetArmMode(ArmMode mode)
        {
            CancelAction();
            armMode = mode;
            leanTarget = 0f;
            slumpTarget = 0f;
            bounce = 0f;
            if (gun != null)
                gun.SetActive(mode == ArmMode.Aim);
        }

        bool OwnsPose(int generation) => this != null && isActiveAndEnabled && generation == poseGeneration;

        void CancelAction()
        {
            poseGeneration++;
            action = HandAction.None;
            gripCallback = null;
            releaseCallback = null;
            shotTarget = null;
            fireCallback = null;
            wristKick = 0f;
            shotWeight = 0f;
            cock = trigger = 0f;
            cylinderTurn = cylinderIndex;
            if (gun != null)
                RevolverModel.SetMechanism(gun.transform, 0f, 0f, cylinderTurn);
        }

        int BeginAction(HandAction next, float speed)
        {
            SetArmMode(next == HandAction.Packet ? ArmMode.Reach : ArmMode.Aim);
            action = next;
            if (next == HandAction.Shot)
                gun.SetActive(false);
            actionTime = 0f;
            actionSpeed = float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f ? 1f : speed;
            startHandL = chest.InverseTransformPoint(handL.position);
            startHandR = chest.InverseTransformPoint(handR.position);
            startWristL = Quaternion.Inverse(transform.rotation) * handL.rotation;
            startWristR = Quaternion.Inverse(transform.rotation) * handR.rotation;
            gripDelivered = releaseDelivered = fireDelivered = cylinderAdvanced = false;
            shotCylinderStart = cylinderIndex;
            return poseGeneration;
        }

        ArmMode IdleMode => walkSpeed > 0f ? ArmMode.Walk : ArmMode.HoldCards;

        public void SetWalking(float speed)
        {
            walkSpeed = Mathf.Max(0f, speed);
            ArmMode locomotion = walkSpeed > 0f ? ArmMode.Walk : ArmMode.Rest;
            if (action == HandAction.None && armMode != locomotion
                && (armMode == ArmMode.Walk || armMode == ArmMode.Rest || armMode == ArmMode.HoldCards))
                SetArmMode(locomotion);
            if (walkSpeed <= 0f && !seated)
            {
                hipL.localRotation = Quaternion.identity;
                hipR.localRotation = Quaternion.identity;
                kneeL.localRotation = Quaternion.identity;
                kneeR.localRotation = Quaternion.identity;
                hips.localRotation = Quaternion.identity;
            }
        }

        public IEnumerator PlayPacket(Vector3 destination, System.Action onGrip, System.Action onRelease, float speed)
        {
            if (this == null || !isActiveAndEnabled)
                yield break;
            int generation = BeginAction(HandAction.Packet, speed);
            packetDestination = destination;
            gripCallback = onGrip;
            releaseCallback = onRelease;
            try
            {
                while (OwnsPose(generation) && action == HandAction.Packet)
                    yield return null;
            }
            finally
            {
                if (OwnsPose(generation))
                    SetArmMode(IdleMode);
            }
        }

        public IEnumerator Shoot(System.Func<Vector3> target, System.Action<Vector3,Vector3> onFire, float speed)
        {
            if (this == null || !isActiveAndEnabled || target == null)
                yield break;
            int generation = BeginAction(HandAction.Shot, speed);
            shotTarget = target;
            fireCallback = onFire;
            try
            {
                while (OwnsPose(generation) && action == HandAction.Shot)
                    yield return null;
            }
            finally
            {
                if (OwnsPose(generation))
                    SetArmMode(IdleMode);
            }
        }

        public IEnumerator Reach(Vector3 tableWorldPos, float duration)
        {
            reachTarget = tableWorldPos;
            SetArmMode(ArmMode.Reach);
            int generation = poseGeneration;
            leanTarget = 0.35f;
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            if (OwnsPose(generation))
                SetArmMode(IdleMode);
        }

        public void Aim(Vector3 targetWorldPos)
        {
            SetArmMode(ArmMode.Aim);
            aimTarget = targetWorldPos;
            LookAt(targetWorldPos);
        }

        public IEnumerator Recoil()
        {
            if (this == null || !isActiveAndEnabled || armMode != ArmMode.Aim)
                yield break;
            int generation = BeginAction(HandAction.Recoil, 1f);
            try
            {
                while (OwnsPose(generation) && action == HandAction.Recoil)
                    yield return null;
            }
            finally
            {
                if (OwnsPose(generation))
                    SetArmMode(ArmMode.Aim);
            }
        }

        public void LowerGun()
        {
            if (armMode == ArmMode.Aim)
                SetArmMode(IdleMode);
        }

        public IEnumerator Flinch()
        {
            ArmMode mode = armMode == ArmMode.FacePalm || armMode == ArmMode.Rest ? armMode : IdleMode;
            float previousSlump = slumpTarget;
            SetArmMode(mode);
            if (mode == ArmMode.FacePalm || mode == ArmMode.Rest)
                slumpTarget = previousSlump;
            int generation = poseGeneration;
            leanTarget = -1f;
            yield return new WaitForSeconds(0.15f);
            if (OwnsPose(generation))
                leanTarget = 0f;
        }

        public IEnumerator Caught(float duration)
        {
            SetArmMode(ArmMode.FacePalm);
            int generation = poseGeneration;
            Flash(Theme.Red);
            slumpTarget = 1f;
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            if (OwnsPose(generation))
                SetArmMode(IdleMode);
        }

        public IEnumerator Proud(float duration)
        {
            SetArmMode(IdleMode);
            int generation = poseGeneration;
            Flash(Theme.Green);
            leanTarget = -0.5f;
            bounce = 1f;
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            if (OwnsPose(generation))
                leanTarget = 0f;
        }

        public IEnumerator Wave(float duration)
        {
            SetArmMode(ArmMode.Wave);
            int generation = poseGeneration;
            yield return new WaitForSeconds(Mathf.Max(0f, duration));
            if (OwnsPose(generation))
                SetArmMode(IdleMode);
        }

        public void Celebrate()
        {
            SetArmMode(ArmMode.Celebrate);
            Flash(Theme.Gold);
        }

        public void Defeated()
        {
            SetArmMode(ArmMode.Rest);
            slumpTarget = 1.2f;
        }

        public void Flash(Color color)
        {
            maskFlash = 1f;
            maskFlashColor = color;
        }

        // ================================================================== update

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || chest == null)
                return;
            if (action == HandAction.Shot && shotTarget != null)
            {
                int generation = poseGeneration;
                Vector3 target;
                try
                {
                    target = shotTarget();
                }
                catch (System.Exception exception)
                {
                    if (OwnsPose(generation))
                        SetArmMode(IdleMode);
                    Debug.LogException(exception, this);
                    return;
                }
                if (!OwnsPose(generation))
                    return;
                if (!IsFinite(target))
                    return;
                aimTarget = target;
                LookAt(target);
            }
            AdvanceAction(dt);
            phase += dt;
            lean = Mathf.Lerp(lean, leanTarget, 1f - Mathf.Exp(-10f * dt));
            slump = Mathf.Lerp(slump, slumpTarget, 1f - Mathf.Exp(-6f * dt));
            bounce = Mathf.MoveTowards(bounce, 0f, dt * 2f);

            if (walkSpeed > 0f)
                UpdateWalk(dt);
            else
                UpdateSeatedBody();

            UpdateHead(dt);
            UpdateArms(dt);
            UpdateGrip(dt);
            UpdateFace(dt);
            DispatchMarkers();
        }

        static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        static float Curve(float time, float start, float end)
        {
            float t = Mathf.Clamp01((time - start) / (end - start));
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        void AdvanceAction(float dt)
        {
            if (action == HandAction.None)
                return;
            float limit = action == HandAction.Packet
                ? !gripDelivered ? PacketGripTime : !releaseDelivered ? PacketReleaseTime : PacketEndTime
                : action == HandAction.Shot ? !fireDelivered ? FireTime : ShotEndTime : 0.33f;
            actionTime = Mathf.Min(limit, actionTime + dt * actionSpeed);
            if (action == HandAction.Shot)
                gun.SetActive(actionTime >= 0.12f);
            if (action == HandAction.Packet)
            {
                leanTarget = 0.28f * Curve(actionTime, PacketGripTime, PacketReleaseTime)
                    * (1f - Curve(actionTime, PacketReleaseTime + 0.08f, PacketEndTime));
                return;
            }
            if (action == HandAction.Recoil)
            {
                wristKick = Curve(actionTime, 0f, 0.08f) * (1f - Curve(actionTime, 0.08f, 0.33f));
                shotWeight = 1f;
                trigger = 1f - Curve(actionTime, 0.08f, 0.33f);
            }
            else
            {
                shotWeight = Curve(actionTime, 0f, DrawTime) * (1f - Curve(actionTime, RecoverTime, ShotEndTime));
                float cockProgress = Curve(actionTime, DrawTime, CockTime);
                if (!cylinderAdvanced && actionTime >= CockTime)
                {
                    cylinderIndex++;
                    cylinderAdvanced = true;
                }
                cylinderTurn = shotCylinderStart + cockProgress;
                cock = cockProgress * (1f - Curve(actionTime, FireTime - 0.025f, FireTime));
                trigger = Curve(actionTime, CockTime, FireTime) * (1f - Curve(actionTime, KickTime, RecoverTime));
                wristKick = Curve(actionTime, FireTime, KickTime) * (1f - Curve(actionTime, KickTime, RecoverTime));
            }
            leanTarget = -0.12f * shotWeight - 0.35f * wristKick;
        }

        void DispatchMarkers()
        {
            if (Time.deltaTime <= 0f || !isActiveAndEnabled)
                return;
            int generation = poseGeneration;
            System.Action marker = null;
            if (action == HandAction.Packet)
            {
                if (!gripDelivered && actionTime >= PacketGripTime)
                {
                    if (Vector3.Distance(PlayHold.position, pinchPosition) > 0.008f
                        || Quaternion.Angle(PlayHold.rotation, pinchRotation) > 2f || gripR < 0.98f)
                        return;
                    gripDelivered = true;
                    marker = gripCallback;
                    gripCallback = null;
                }
                else if (gripDelivered && !releaseDelivered && actionTime >= PacketReleaseTime)
                {
                    if (Vector3.Distance(handR.position, releaseWrist) > 0.008f || gripR > 0.02f)
                        return;
                    releaseDelivered = true;
                    marker = releaseCallback;
                    releaseCallback = null;
                }
                else if (releaseDelivered && actionTime >= PacketEndTime)
                {
                    SetArmMode(IdleMode);
                    return;
                }
            }
            else if (action == HandAction.Shot)
            {
                if (!fireDelivered && actionTime >= FireTime)
                {
                    Vector3 direction = aimTarget - muzzle.position;
                    if (direction.sqrMagnitude < 1e-6f || Vector3.Angle(muzzle.forward, direction) > 2f)
                        return;
                    fireDelivered = true;
                    var fire = fireCallback;
                    fireCallback = null;
                    Vector3 position = muzzle.position;
                    Vector3 forward = muzzle.forward;
                    marker = fire == null ? null : () => fire(position, forward);
                }
                else if (fireDelivered && actionTime >= ShotEndTime)
                {
                    SetArmMode(IdleMode);
                    return;
                }
            }
            else if (action == HandAction.Recoil && actionTime >= 0.33f)
            {
                SetArmMode(ArmMode.Aim);
                return;
            }
            if (marker == null)
                return;
            try
            {
                marker();
            }
            catch (System.Exception exception)
            {
                if (OwnsPose(generation))
                    SetArmMode(IdleMode);
                Debug.LogException(exception, this);
            }
        }

        void UpdateSeatedBody()
        {
            float breathe = Mathf.Sin(phase * 1.6f);
            float hop = Mathf.Abs(Mathf.Sin(bounce * Mathf.PI * 3f)) * bounce * 0.06f;
            hips.localPosition = new Vector3(0f, (seated ? 0.52f : 0.92f) + hop, seated ? -0.06f : 0f);
            hips.localRotation = Quaternion.identity;
            spine.localRotation = Quaternion.Euler(lean * 14f + slump * 18f + breathe * 1.2f, Mathf.Sin(phase * 0.5f) * 3f, 0f);
            chest.localRotation = Quaternion.Euler(slump * 10f + breathe * 1.5f - wristKick * 3f, shotWeight * -4f, wristKick * -2f);
        }

        void UpdateWalk(float dt)
        {
            float t = phase * walkSpeed * 6.5f;
            float swing = Mathf.Sin(t);
            hips.localPosition = new Vector3(0f, 0.92f + Mathf.Abs(Mathf.Cos(t)) * 0.035f, 0f);
            hips.localRotation = Quaternion.Euler(0f, swing * 6f, 0f);
            spine.localRotation = Quaternion.Euler(4f + lean * 14f + slump * 18f, -swing * 8f, 0f);
            chest.localRotation = Quaternion.Euler(slump * 10f - wristKick * 3f, shotWeight * -4f, wristKick * -2f);
            hipL.localRotation = Quaternion.Euler(-swing * 30f, 0f, 0f);
            hipR.localRotation = Quaternion.Euler(swing * 30f, 0f, 0f);
            kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(t + 1.2f)) * 50f, 0f, 0f);
            kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(t + 1.2f + Mathf.PI)) * 50f, 0f, 0f);
        }

        void UpdateHead(float dt)
        {
            Quaternion target = Quaternion.Euler(slump * 28f, 0f, 0f);
            if (hasLookTarget && slump < 0.5f)
            {
                var local = neck.parent.InverseTransformPoint(lookTarget) - neck.localPosition;
                if (local.sqrMagnitude > 1e-4f)
                {
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65f, 65f);
                    float pitch = Mathf.Clamp(-Mathf.Atan2(local.y - 0.1f, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 35f);
                    target = Quaternion.Euler(pitch + slump * 28f, yaw, 0f);
                }
            }
            if (armMode == ArmMode.Wave)
                target *= Quaternion.Euler(0f, Mathf.Sin(phase * 12f) * 18f, 0f);
            neck.localRotation = Quaternion.Slerp(neck.localRotation, target, 1f - Mathf.Exp(-8f * dt));
        }

        void UpdateArms(float dt)
        {
            Vector3 targetL = chest.TransformPoint(new Vector3(-0.11f, -0.07f, 0.28f));
            Vector3 targetR = chest.TransformPoint(new Vector3(0.13f, -0.09f, 0.26f));
            Quaternion rotationL = LeftWrist;
            Quaternion rotationR = RightWrist;
            gripRTarget = 0.35f;
            switch (armMode)
            {
                case ArmMode.Reach:
                    targetR = Reachable(shoulderR, reachTarget + transform.up * 0.05f, 0.46f);
                    break;
                case ArmMode.Aim:
                    targetL = chest.TransformPoint(new Vector3(-0.13f, -0.125f, 0.26f));
                    targetR = AimHandPosition();
                    gripRTarget = 1f;
                    break;
                case ArmMode.Celebrate:
                    float wiggle = Mathf.Sin(phase * 10f) * 0.035f;
                    targetL = chest.TransformPoint(new Vector3(-0.30f - wiggle, 0.54f, 0.08f));
                    targetR = chest.TransformPoint(new Vector3(0.30f - wiggle, 0.54f, 0.08f));
                    rotationL = Quaternion.Euler(0f, 0f, -12f) * LeftWrist;
                    rotationR = Quaternion.Euler(0f, 0f, 12f) * RightWrist;
                    gripRTarget = 0f;
                    break;
                case ArmMode.FacePalm:
                    targetL = chest.TransformPoint(new Vector3(-0.17f, -0.18f, 0.24f));
                    targetR = head.TransformPoint(new Vector3(0.055f, 0.05f, 0.19f));
                    rotationR = Quaternion.Inverse(transform.rotation) * head.rotation * Quaternion.Euler(0f, 180f, 165f);
                    gripRTarget = 0f;
                    break;
                case ArmMode.Wave:
                    targetR = chest.TransformPoint(new Vector3(0.35f + Mathf.Sin(phase * 12f) * 0.045f, 0.37f, 0.14f));
                    rotationR = Quaternion.Euler(0f, 0f, 165f + Mathf.Sin(phase * 12f) * 18f);
                    gripRTarget = 0f;
                    break;
                case ArmMode.Rest:
                    targetL = hips.TransformPoint(new Vector3(-0.15f, 0.1f, 0.28f));
                    targetR = hips.TransformPoint(new Vector3(0.15f, 0.1f, 0.28f));
                    rotationR = Quaternion.Euler(-65f, 0f, -20f);
                    break;
                case ArmMode.Walk:
                    float swing = Mathf.Sin(phase * walkSpeed * 6.5f);
                    targetL = chest.TransformPoint(new Vector3(-0.24f, -0.27f, 0.1f - swing * 0.12f));
                    targetR = chest.TransformPoint(new Vector3(0.24f, -0.27f, 0.1f + swing * 0.12f));
                    rotationL = Quaternion.Euler(swing * 12f, 0f, 0f) * LeftWrist;
                    rotationR = Quaternion.Euler(-swing * 12f, 0f, 0f) * RightWrist;
                    break;
            }

            if (action == HandAction.Packet)
            {
                float approach = Curve(actionTime, 0f, PacketGripTime - 0.08f);
                targetL = Vector3.Lerp(chest.TransformPoint(startHandL), chest.TransformPoint(new Vector3(-0.11f, -0.07f, 0.28f)), approach);
                rotationL = Quaternion.Slerp(startWristL, LeftWrist, approach);
            }
            else if (action == HandAction.Shot)
            {
                float draw = Curve(actionTime, 0f, DrawTime);
                float lower = Curve(actionTime, RecoverTime, ShotEndTime);
                targetL = Vector3.Lerp(chest.TransformPoint(startHandL), targetL, draw);
                targetL = Vector3.Lerp(targetL, chest.TransformPoint(new Vector3(-0.11f, -0.07f, 0.28f)), lower);
                rotationL = Quaternion.Slerp(startWristL, LeftWrist, draw);
            }

            float k = handsInitialized ? 1f - Mathf.Exp(-12f * dt) : 1f;
            float actionBlend = action == HandAction.None ? k : 1f;
            handLPos = Vector3.Lerp(handLPos, chest.InverseTransformPoint(targetL), actionBlend);
            wristLRotation = handsInitialized ? Quaternion.Slerp(wristLRotation, rotationL, actionBlend) : rotationL;
            SolveArm(shoulderL, elbowL, chest.TransformPoint(handLPos), chest.TransformPoint(new Vector3(-0.6f, -0.35f, 0.02f)));
            handL.rotation = transform.rotation * wristLRotation;

            if (action == HandAction.Packet)
                PacketPose(ref targetR, ref rotationR);
            else if (action == HandAction.Shot)
            {
                Vector3 drawHand = chest.TransformPoint(new Vector3(0.25f, -0.20f, 0.09f));
                if (actionTime < 0.12f)
                    targetR = Vector3.Lerp(chest.TransformPoint(startHandR), drawHand, Curve(actionTime, 0f, 0.12f));
                else
                    targetR = Vector3.Lerp(drawHand, AimHandPosition(), Curve(actionTime, 0.12f, DrawTime));
                targetR = Vector3.Lerp(targetR, chest.TransformPoint(new Vector3(0.13f, -0.09f, 0.26f)), Curve(actionTime, RecoverTime, ShotEndTime));
                gripRTarget = Curve(actionTime, 0f, 0.12f) * (1f - Curve(actionTime, RecoverTime, ShotEndTime));
            }
            if (armMode == ArmMode.Aim)
                targetR += transform.up * (wristKick * 0.012f) - (aimTarget - shoulderR.position).normalized * (wristKick * 0.035f);

            handRPos = Vector3.Lerp(handRPos, chest.InverseTransformPoint(targetR), actionBlend);
            SolveArm(shoulderR, elbowR, chest.TransformPoint(handRPos), chest.TransformPoint(new Vector3(0.6f, -0.35f, 0.02f)));
            if (armMode == ArmMode.Aim)
            {
                Quaternion aim = AimWrist(handR.position);
                float cockMotion = action == HandAction.Shot ? Curve(actionTime, DrawTime, CockTime)
                    * (1f - Curve(actionTime, CockTime, FireTime)) : 0f;
                aim *= barrelLocal * Quaternion.Euler(-19f * wristKick - 3f * cockMotion, 0f, -3f * wristKick + 5f * cockMotion) * Quaternion.Inverse(barrelLocal);
                rotationR = Quaternion.Inverse(transform.rotation) * aim;
                if (action == HandAction.Shot)
                {
                    Quaternion draw = Quaternion.Slerp(startWristR, Quaternion.Euler(12f, -8f, 0f), Curve(actionTime, 0f, 0.12f));
                    rotationR = Quaternion.Slerp(draw, rotationR, Curve(actionTime, 0.12f, DrawTime));
                    rotationR = Quaternion.Slerp(rotationR, RightWrist, Curve(actionTime, RecoverTime, ShotEndTime));
                }
                wristRRotation = rotationR;
            }
            else
                wristRRotation = handsInitialized ? Quaternion.Slerp(wristRRotation, rotationR, actionBlend) : rotationR;
            handR.rotation = transform.rotation * wristRRotation;
            handsInitialized = true;
        }

        Vector3 AimHandPosition()
        {
            Vector3 direction = aimTarget - shoulderR.position;
            if (direction.sqrMagnitude < 1e-6f)
                direction = transform.forward;
            float reach = Mathf.Clamp(direction.magnitude - 0.36f, 0.05f, 0.44f);
            return shoulderR.position + direction.normalized * reach;
        }

        Quaternion AimWrist(Vector3 position)
        {
            Vector3 direction = aimTarget - position;
            if (direction.sqrMagnitude < 1e-6f)
                direction = transform.forward;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, transform.up)) > 0.98f ? transform.forward : transform.up;
            Quaternion basis = Quaternion.LookRotation(direction, up);
            Vector3 barrelOffset = Quaternion.Inverse(barrelLocal) * muzzleOffset;
            float forwardDistance = Mathf.Sqrt(Mathf.Max(0.000001f, direction.sqrMagnitude
                - barrelOffset.x * barrelOffset.x - barrelOffset.y * barrelOffset.y));
            Quaternion correction = Quaternion.FromToRotation(new Vector3(barrelOffset.x, barrelOffset.y, forwardDistance), Vector3.forward);
            return basis * correction * Quaternion.Inverse(barrelLocal);
        }

        static Vector3 Reachable(Transform shoulder, Vector3 target, float reach)
        {
            Vector3 direction = target - shoulder.position;
            if (direction.sqrMagnitude < 1e-6f)
                direction = Vector3.down * 0.05f;
            return shoulder.position + direction.normalized * Mathf.Clamp(direction.magnitude, 0.05f, reach);
        }

        void PacketPose(ref Vector3 targetR, ref Quaternion rotationR)
        {
            pinchPosition = CardHold.TransformPoint(new Vector3(0.016f, 0f, -0.004f));
            pinchRotation = CardHold.rotation;
            Quaternion pinchWrist = pinchRotation * Quaternion.Inverse(PlayHold.localRotation);
            Vector3 pickup = pinchPosition - pinchWrist * PlayHold.localPosition;
            Quaternion releaseRotation = transform.rotation * Quaternion.Euler(-10f, 0f, -8f) * RightWrist;
            Vector3 destination = IsFinite(packetDestination) ? packetDestination : chest.TransformPoint(new Vector3(0.13f, -0.02f, 0.8f));
            releaseWrist = Reachable(shoulderR, destination - releaseRotation * PlayHold.localPosition, 0.45f);
            Quaternion wrist;
            if (actionTime <= PacketGripTime)
            {
                float t = Curve(actionTime, 0f, PacketGripTime - 0.06f);
                targetR = Vector3.Lerp(chest.TransformPoint(startHandR), pickup, t) + transform.up * (0.035f * Mathf.Sin(t * Mathf.PI));
                wrist = Quaternion.Slerp(transform.rotation * startWristR, pinchWrist, t);
                gripRTarget = Curve(actionTime, PacketGripTime - 0.10f, PacketGripTime);
            }
            else if (actionTime <= PacketReleaseTime)
            {
                float t = Curve(actionTime, PacketGripTime, PacketReleaseTime);
                targetR = Vector3.Lerp(pickup, releaseWrist, t) + transform.up * (0.045f * Mathf.Sin(t * Mathf.PI));
                wrist = Quaternion.Slerp(pinchWrist, releaseRotation, t);
                gripRTarget = 1f - Curve(actionTime, PacketReleaseTime - 0.055f, PacketReleaseTime);
            }
            else
            {
                float follow = Curve(actionTime, PacketReleaseTime, PacketReleaseTime + 0.08f);
                float recover = Curve(actionTime, PacketReleaseTime + 0.08f, PacketEndTime);
                Vector3 followHand = Reachable(shoulderR, releaseWrist + transform.forward * 0.025f - transform.up * 0.018f, 0.48f);
                targetR = Vector3.Lerp(Vector3.Lerp(releaseWrist, followHand, follow), chest.TransformPoint(new Vector3(0.13f, -0.09f, 0.26f)), recover);
                wrist = Quaternion.Slerp(releaseRotation * Quaternion.Euler(-8f * follow, 0f, 0f), transform.rotation * RightWrist, recover);
                gripRTarget = 0.35f * recover;
            }
            targetR = Reachable(shoulderR, targetR, 0.49f);
            rotationR = Quaternion.Inverse(transform.rotation) * wrist;
        }

        void UpdateGrip(float dt)
        {
            gripR = action == HandAction.None ? Mathf.Lerp(gripR, gripRTarget, 1f - Mathf.Exp(-16f * dt)) : gripRTarget;
            for (int i = 0; i < 4; i++)
            {
                fingersL[i].localRotation = Quaternion.identity;
                float angle = Mathf.Lerp(32f, 0f, gripR);
                if (armMode == ArmMode.Aim && i == 0)
                    angle = Mathf.Lerp(18f, -8f, trigger);
                fingersR[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
            }
            fingersL[4].localRotation = Quaternion.identity;
            fingersR[4].localRotation = Quaternion.Euler(0f, -cock * 16f, (1f - gripR) * -12f - cock * 8f);
            if (gun != null)
                RevolverModel.SetMechanism(gun.transform, cock, trigger, cylinderTurn);
        }

        void UpdateFace(float dt)
        {
            blinkTimer -= dt;
            float blink = 1f;
            if (blinkTimer < 0.12f)
                blink = 0.15f;
            if (blinkTimer < 0f)
                blinkTimer = Random.Range(2f, 5f);
            float squint = slump > 0.5f ? 0.5f : 1f;
            var eyeScale = new Vector3(0.05f, 0.032f * blink * squint, 0.02f);
            eyeL.localScale = eyeScale;
            eyeR.localScale = eyeScale;

            if (maskFlash > 0f)
            {
                maskFlash = Mathf.MoveTowards(maskFlash, 0f, dt * 0.8f);
                var c = Color.Lerp(maskColor, maskFlashColor, Mathf.Clamp01(maskFlash * 1.3f));
                maskMaterial.SetColor("_Color", c);
                maskMaterial.SetColor("_EmissionColor", maskFlashColor * (maskFlash * 0.6f));
            }
        }

        /// <summary>IK dua tulang: arahkan bahu &amp; siku supaya tangan mencapai target, siku condong ke arah pole.</summary>
        static void SolveArm(Transform shoulder, Transform elbow, Vector3 target, Vector3 pole)
        {
            Vector3 s = shoulder.position;
            Vector3 toTarget = target - s;
            float d = Mathf.Clamp(toTarget.magnitude, 0.05f, UpperArm + Forearm - 0.002f);
            Vector3 dir = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector3.down;
            float cosA = (UpperArm * UpperArm + d * d - Forearm * Forearm) / (2f * UpperArm * d);
            float a = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f));
            Vector3 bend = Vector3.ProjectOnPlane(pole - s, dir);
            if (bend.sqrMagnitude < 1e-6f)
                bend = Vector3.ProjectOnPlane(Vector3.down, dir);
            if (bend.sqrMagnitude < 1e-6f)
                bend = Vector3.ProjectOnPlane(Vector3.right, dir);
            bend.Normalize();
            Vector3 elbowPos = s + (dir * Mathf.Cos(a) + bend * Mathf.Sin(a)) * UpperArm;
            AimBone(shoulder, elbowPos);
            AimBone(elbow, s + dir * d);
        }

        /// <summary>Putar tulang supaya sumbu -Y lokalnya menunjuk ke titik dunia.</summary>
        static void AimBone(Transform bone, Vector3 worldTarget)
        {
            Vector3 dirWorld = worldTarget - bone.position;
            if (dirWorld.sqrMagnitude < 1e-8f)
                return;
            Vector3 dirParent = bone.parent.InverseTransformDirection(dirWorld.normalized);
            bone.localRotation = Quaternion.FromToRotation(Vector3.down, dirParent);
        }

        void OnDisable()
        {
            CancelAction();
            armMode = IdleMode;
            leanTarget = slumpTarget = 0f;
            if (gun != null)
                gun.SetActive(false);
        }

        void OnDestroy()
        {
            CancelAction();
            if (maskMaterial != null)
                Destroy(maskMaterial);
            foreach (var mesh in meshes)
                if (mesh != null)
                    Destroy(mesh);
        }
    }
}
