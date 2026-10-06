using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Ruang kartu 3D: lantai, dinding, karpet, meja bundar, lampu gantung, kursi, dan karakter bertopeng.
    /// Kursi relatif 0 = pemain manusia (selatan, sudut pandang orang pertama), lalu searah jarum jam: barat, utara, timur.
    /// </summary>
    public sealed class TableScene : MonoBehaviour
    {
        public const float TableTop = 0.765f;

        /// <summary>Permukaan kain meja (sedikit di atas bibir kayu supaya tidak z-fighting).</summary>
        public const float FeltTop = TableTop + 0.002f;
        public const float TableRadius = 0.92f;
        const float SeatDistance = 1.2f;

        readonly Dictionary<int, Humanoid> characters = new Dictionary<int, Humanoid>();
        readonly Dictionary<int, List<Card3D>> fans = new Dictionary<int, List<Card3D>>();
        readonly Dictionary<int, List<Transform>> shells = new Dictionary<int, List<Transform>>();
        readonly Dictionary<int, Card3D> powerCards = new Dictionary<int, Card3D>();
        readonly Dictionary<int, int> relativeSeat = new Dictionary<int, int>();
        int playerCount;
        int humanId;
        Light lampLight;
        float lampBase;
        float lampFlicker;
        readonly List<Mesh> environmentMeshes = new List<Mesh>();

        public Transform PileAnchor { get; private set; }
        public Transform BurnAnchor { get; private set; }
        public Camera Camera { get; private set; }

        /// <summary>Bangun ruangan. humanId = -1 berarti semua kursi diisi karakter (dipakai di menu).</summary>
        public static TableScene Create(Transform parent, int playerCount, int humanId, IList<string> names)
        {
            var go = new GameObject("Table Scene");
            go.transform.SetParent(parent, false);
            var scene = go.AddComponent<TableScene>();
            scene.Build(playerCount, humanId);
            return scene;
        }

        // ================================================================== build

        void Build(int count, int human)
        {
            playerCount = count;
            humanId = human;
            SetupEnvironment();
            BuildRoom();
            BuildTable();
            BuildLamp();

            for (int id = 0; id < count; id++)
            {
                int relative = human >= 0 ? (id - human + count) % count : id;
                relativeSeat[id] = relative;
                var dir = SeatDirection(relative);
                if (id != human)
                {
                    BuildChair(dir);
                    var character = Humanoid.Create(transform, "Player " + id, HumanoidStyle.ForPlayer(id), true);
                    character.transform.position = dir * SeatDistance;
                    character.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                    character.LookAt(new Vector3(0f, TableTop, 0f));
                    characters[id] = character;
                    fans[id] = new List<Card3D>();
                }
                BuildShells(id, dir);
                BuildPowerSlot(id, dir);
            }
            SetupCamera();
        }

        /// <summary>
        /// Arah kursi dari tengah meja. Lawan disusun melengkung di depan pemain (150°, 90°, 30°) supaya ketiganya
        /// terlihat dari sudut pandang orang pertama.
        /// </summary>
        public static Vector3 SeatDirection(int relative)
        {
            switch (relative % 4)
            {
                case 0: return new Vector3(0f, 0f, -1f);
                case 1: return new Vector3(-0.866f, 0f, 0.5f);
                case 2: return new Vector3(0f, 0f, 1f);
                default: return new Vector3(0.866f, 0f, 0.5f);
            }
        }

        /// <summary>Empat arah mata angin untuk dinding ruangan.</summary>
        static Vector3 Cardinal(int side)
        {
            switch (side % 4)
            {
                case 0: return new Vector3(0f, 0f, -1f);
                case 1: return new Vector3(-1f, 0f, 0f);
                case 2: return new Vector3(0f, 0f, 1f);
                default: return new Vector3(1f, 0f, 0f);
            }
        }

        static void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Theme.Hex("707A80") * 0.55f;
            RenderSettings.ambientEquatorColor = Theme.Hex("827666") * 0.42f;
            RenderSettings.ambientGroundColor = Theme.Hex("40332C") * 0.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.024f;
            RenderSettings.fogColor = Theme.Hex("222B29");
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowDistance = 7f;
        }

        void SetupCamera()
        {
            Camera = Camera.main;
            if (Camera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                Camera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            Camera.orthographic = false;
            Camera.fieldOfView = 56f;
            Camera.nearClipPlane = 0.05f;
            Camera.farClipPlane = 40f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = RenderSettings.fogColor;
        }

        Transform Group(string name, Transform parent, Vector3 position)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        static Vector2[] Profile(params float[] coordinates)
        {
            var points = new Vector2[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
            return points;
        }

        static Vector3[] Circle(float radius, float height, int segments = 96)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                points[i] = new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
            }
            points[segments] = points[0];
            return points;
        }

        void Batch(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new List<MeshFilter>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null)
                    continue;
                var material = renderer.sharedMaterial;
                if (!groups.TryGetValue(material, out var list))
                {
                    list = new List<CombineInstance>();
                    groups.Add(material, list);
                }
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                sources.Add(filter);
            }
            foreach (var pair in groups)
            {
                var instances = new List<CombineInstance>();
                int vertices = 0;
                foreach (var instance in pair.Value)
                {
                    if (vertices + instance.mesh.vertexCount > 60000 && instances.Count > 0)
                    {
                        BatchMesh(root, pair.Key, instances);
                        instances.Clear();
                        vertices = 0;
                    }
                    instances.Add(instance);
                    vertices += instance.mesh.vertexCount;
                }
                if (instances.Count > 0)
                    BatchMesh(root, pair.Key, instances);
            }
            foreach (var filter in sources)
            {
                filter.GetComponent<MeshRenderer>().enabled = false;
                filter.sharedMesh = null;
                Art.ReleaseOwnedMesh(filter);
                Destroy(filter.gameObject);
            }
        }

        void BatchMesh(Transform root, Material material, List<CombineInstance> instances)
        {
            var mesh = new Mesh { name = root.name + " / " + material.name };
            mesh.CombineMeshes(instances.ToArray(), true, true);
            mesh.RecalculateBounds();
            environmentMeshes.Add(mesh);
            var go = Group(material.name, root, Vector3.zero).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            mesh.UploadMeshData(true);
        }

        void OnDestroy()
        {
            foreach (var mesh in environmentMeshes)
                if (mesh != null)
                    Destroy(mesh);
        }

        void BuildRoom()
        {
            var floor = Group("Walnut Floor", transform, Vector3.zero);
            var boards = new[] { Art.Wood(Theme.Hex("514034")), Art.Wood(Theme.Hex("574437")), Art.Wood(Theme.Hex("4C3A30")) };
            Art.BeveledBox("Subfloor", floor, new Vector3(0f, -0.065f, 0f), new Vector3(9.2f, 0.08f, 9.2f), 0f, Art.Wood(Theme.Hex("28251F")));
            for (int column = 0; column < 20; column++)
            {
                for (int row = -1; row < 5; row++)
                {
                    float start = Mathf.Max(-4.6f, -4.6f + row * 2.3f + (column % 3) * 0.76f);
                    float end = Mathf.Min(4.6f, -4.6f + (row + 1) * 2.3f + (column % 3) * 0.76f);
                    if (end <= start)
                        continue;
                    Art.BeveledBox("Tongue and Groove Board", floor, new Vector3(-4.37f + column * 0.46f, -0.013f, (start + end) * 0.5f),
                        new Vector3(0.456f, 0.026f, end - start - 0.004f), 0.002f, boards[(column + row + 3) % boards.Length]);
                }
            }
            Batch(floor);

            var rug = Group("Bound Wool Rug", transform, Vector3.zero);
            Art.Lathe("Wool Ground", rug, Vector3.zero, Profile(0f, 0.003f, 2.21f, 0.003f, 2.23f, 0.009f, 2.21f, 0.014f, 0f, 0.014f), Art.Fabric(Theme.Hex("4E3338")), 96);
            Art.Lathe("Woven Border", rug, Vector3.zero, Profile(2.06f, 0.015f, 1.99f, 0.015f), Art.Fabric(Theme.Hex("8C7960")), 96);
            Art.Lathe("Border Inner Thread", rug, Vector3.zero, Profile(1.965f, 0.015f, 1.953f, 0.015f), Art.Fabric(Theme.Hex("8C7960")), 96);
            Art.Tube("Bound Edge", rug, Circle(2.215f, 0.011f), 0.006f, Art.Fabric(Theme.Hex("372C2E")), 6);
            Batch(rug);

            var walnut = Art.Wood(Theme.Hex("48382F"));
            var recessed = Art.Wood(Theme.Hex("352D27"));
            var wall = Art.Fabric(Theme.Hex("3C4944"));
            for (int side = 0; side < 4; side++)
            {
                var dir = Cardinal(side);
                var section = Group("Library Wall " + side, transform, dir * 4.6f);
                section.localRotation = Quaternion.LookRotation(-dir);
                Art.BeveledBox("Plaster", section, new Vector3(0f, 2f, 0f), new Vector3(9.4f, 4f, 0.2f), 0f, wall);
                Art.BeveledBox("Skirting", section, new Vector3(0f, 0.09f, 0.17f), new Vector3(9.1f, 0.18f, 0.15f), 0.012f, walnut);
                Art.BeveledBox("Bottom Rail", section, new Vector3(0f, 0.235f, 0.155f), new Vector3(9.1f, 0.11f, 0.11f), 0.008f, walnut);
                Art.BeveledBox("Dado Rail", section, new Vector3(0f, 1.06f, 0.17f), new Vector3(9.1f, 0.1f, 0.13f), 0.012f, walnut);
                Art.BeveledBox("Dado Cap", section, new Vector3(0f, 1.13f, 0.18f), new Vector3(9.15f, 0.045f, 0.18f), 0.009f, walnut);
                for (int panel = 0; panel < 8; panel++)
                {
                    float x = (panel - 3.5f) * 1.12f;
                    Art.BeveledBox("Recessed Field", section, new Vector3(x, 0.65f, 0.119f), new Vector3(1.02f, 0.74f, 0.03f), 0.008f, recessed);
                    Frame(section, new Vector3(x, 0.65f, 0.158f), 1.02f, 0.74f, 0.028f, 0.036f, walnut);
                }
                for (int stile = 0; stile <= 8; stile++)
                    Art.BeveledBox("Panel Stile", section, new Vector3((stile - 4f) * 1.12f, 0.65f, 0.16f), new Vector3(0.085f, 0.73f, 0.12f), 0.008f, walnut);
                Art.BeveledBox("Picture Rail", section, new Vector3(0f, 3.3f, 0.13f), new Vector3(9.15f, 0.048f, 0.07f), 0.009f, walnut);
                Art.BeveledBox("Cornice Lower", section, new Vector3(0f, 3.79f, 0.15f), new Vector3(9.15f, 0.065f, 0.12f), 0.014f, walnut);
                Art.BeveledBox("Cornice Cove", section, new Vector3(0f, 3.87f, 0.18f), new Vector3(9.15f, 0.11f, 0.18f), 0.03f, walnut);
                Art.BeveledBox("Cornice Crown", section, new Vector3(0f, 3.955f, 0.21f), new Vector3(9.15f, 0.06f, 0.25f), 0.014f, walnut);
                for (int k = -1; k <= 1; k += 2)
                    BuildSconce(section, new Vector3(k * (side == 2 ? 1.25f : 2.1f), 2.16f, 0.13f), side == 2);
                Batch(section);
            }
            var ceiling = Group("Ceiling", transform, Vector3.zero);
            Art.BeveledBox("Ceiling Plaster", ceiling, new Vector3(0f, 4.04f, 0f), new Vector3(9.2f, 0.08f, 9.2f), 0f, Art.Lit(Theme.Hex("79786C"), 0.08f));
            Batch(ceiling);
            BuildBookshelf(-2.65f);
            BuildBookshelf(2.65f);
        }

        static void Frame(Transform parent, Vector3 center, float width, float height, float rail, float depth, Material material)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Art.BeveledBox("Frame Stile", parent, center + Vector3.right * (sign * (width - rail) * 0.5f), new Vector3(rail, height, depth), rail * 0.2f, material);
                Art.BeveledBox("Frame Rail", parent, center + Vector3.up * (sign * (height - rail) * 0.5f), new Vector3(width - rail * 2f, rail, depth), rail * 0.2f, material);
            }
        }

        void BuildBookshelf(float x)
        {
            var shelf = Group("Joinered Bookcase", transform, new Vector3(x, 0f, 4.16f));
            var wood = Art.Wood(Theme.Hex("48382F"));
            var dark = Art.Wood(Theme.Hex("302A25"));
            var paper = Art.Fabric(Theme.Hex("9A8D73"));
            var tooling = Art.Metal(Theme.Hex("9F8459"), 0.38f);
            var bindings = new[] { Art.Leather(Theme.Hex("543D3D")), Art.Leather(Theme.Hex("354743")), Art.Leather(Theme.Hex("39424B")), Art.Leather(Theme.Hex("685440")) };
            Art.BeveledBox("Recessed Back", shelf, new Vector3(0f, 1.48f, 0.19f), new Vector3(1.63f, 2.62f, 0.05f), 0.008f, dark);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Art.BeveledBox("Carcass Side", shelf, new Vector3(sign * 0.79f, 1.48f, -0.02f), new Vector3(0.09f, 2.66f, 0.48f), 0.012f, wood);
                Art.BeveledBox("Face Stile", shelf, new Vector3(sign * 0.79f, 1.5f, -0.27f), new Vector3(0.125f, 2.66f, 0.055f), 0.008f, wood);
            }
            Art.BeveledBox("Plinth", shelf, new Vector3(0f, 0.09f, -0.025f), new Vector3(1.8f, 0.18f, 0.58f), 0.024f, wood);
            Art.BeveledBox("Crown Bed", shelf, new Vector3(0f, 2.835f, -0.03f), new Vector3(1.76f, 0.09f, 0.55f), 0.016f, wood);
            Art.BeveledBox("Crown Cap", shelf, new Vector3(0f, 2.91f, -0.04f), new Vector3(1.84f, 0.065f, 0.6f), 0.016f, wood);
            for (int row = 0; row <= 4; row++)
            {
                float y = 0.26f + row * 0.63f;
                Art.BeveledBox("Housed Shelf", shelf, new Vector3(0f, y, -0.02f), new Vector3(1.5f, 0.055f, 0.43f), 0.007f, wood);
                Art.BeveledBox("Shelf Nosing", shelf, new Vector3(0f, y - 0.006f, -0.247f), new Vector3(1.48f, 0.067f, 0.035f), 0.009f, wood);
                if (row == 4)
                    continue;
                for (int book = 0; book < 7; book++)
                {
                    int variant = (book * 3 + row * 2 + (x > 0f ? 1 : 0)) % 4;
                    float height = 0.34f + variant * 0.039f;
                    float width = 0.128f + (book % 3) * 0.017f;
                    var p = new Vector3(-0.62f + book * 0.195f, y + 0.028f + height * 0.5f, -0.052f);
                    Art.BeveledBox("Page Block", shelf, p + new Vector3(0f, 0f, 0.007f), new Vector3(width - 0.015f, height - 0.025f, 0.258f), 0f, paper);
                    for (int cover = -1; cover <= 1; cover += 2)
                        Art.BeveledBox("Book Cover", shelf, p + Vector3.right * (cover * (width - 0.007f) * 0.5f), new Vector3(0.007f, height, 0.29f), 0f, bindings[variant]);
                    Art.BeveledBox("Rounded Spine", shelf, p + new Vector3(0f, 0f, -0.135f), new Vector3(width, height, 0.026f), 0.012f, bindings[variant]);
                    for (int band = -1; band <= 1; band += 2)
                        Art.BeveledBox("Spine Tooling", shelf, p + new Vector3(0f, band * (height * 0.5f - 0.06f), -0.149f), new Vector3(width * 0.68f, 0.008f, 0.003f), 0f, tooling);
                }
            }
            Batch(shelf);
        }

        void BuildSconce(Transform parent, Vector3 position, bool illuminated)
        {
            var root = Group("Library Sconce", parent, position);
            var brass = Art.Metal(Theme.Hex("A38A60"));
            var shade = Art.Fabric(Theme.Hex("B9AA8C"));
            var glow = Art.Lit(Theme.Hex("D5C6AA"), 0.23f, 0f, null, Theme.Hex("E1BC82") * 0.38f);
            Art.BeveledBox("Mounting Plate", root, Vector3.zero, new Vector3(0.14f, 0.3f, 0.055f), 0.025f, brass);
            Art.Tube("Swept Arm", root, new[] { new Vector3(0f, -0.06f, 0.025f), new Vector3(0f, -0.09f, 0.1f), new Vector3(0f, -0.085f, 0.19f), new Vector3(0f, -0.055f, 0.25f), new Vector3(0f, 0.01f, 0.28f) }, 0.014f, brass);
            var lamp = Group("Shade Assembly", root, new Vector3(0f, 0.015f, 0.28f));
            Art.Lathe("Socket", lamp, Vector3.zero, Profile(0f, -0.018f, 0.04f, -0.018f, 0.04f, 0f, 0.029f, 0.016f, 0.029f, 0.1f, 0f, 0.1f), brass, 24);
            Art.Lathe("Opal Bulb", lamp, Vector3.zero, Profile(0f, 0.066f, 0.026f, 0.066f, 0.04f, 0.104f, 0.045f, 0.145f, 0.032f, 0.18f, 0f, 0.195f), glow, 24);
            Art.Lathe("Linen Shade", lamp, Vector3.zero, Profile(0.134f, 0.05f, 0.143f, 0.052f, 0.145f, 0.062f, 0.091f, 0.3f, 0.083f, 0.3f, 0.137f, 0.06f, 0.134f, 0.05f), shade, 40);
            Art.Tube("Lower Binding", lamp, Circle(0.141f, 0.058f, 40), 0.005f, brass, 6);
            Art.Tube("Upper Binding", lamp, Circle(0.086f, 0.3f, 40), 0.004f, brass, 6);
            if (illuminated)
            {
                var light = Group("Sconce Wash", root, new Vector3(0f, 0.17f, 0.4f)).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = Theme.Hex("FFE1B8");
                light.intensity = 0.6f;
                light.range = 2.1f;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
            }
        }

        static Vector3[] RoundedLoop(float halfWidth, float halfDepth, float corner, float height)
        {
            const int steps = 6;
            var points = new Vector3[4 * steps + 1];
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                float cx = quadrant == 0 || quadrant == 3 ? halfWidth - corner : -halfWidth + corner;
                float cz = quadrant < 2 ? halfDepth - corner : -halfDepth + corner;
                for (int step = 0; step < steps; step++)
                {
                    float angle = (quadrant + step / (float)(steps - 1)) * Mathf.PI * 0.5f;
                    points[quadrant * steps + step] = new Vector3(cx + Mathf.Cos(angle) * corner, height, cz + Mathf.Sin(angle) * corner);
                }
            }
            points[points.Length - 1] = points[0];
            return points;
        }

        void BuildTable()
        {
            var table = Group("Crafted Card Table", transform, Vector3.zero);
            var wood = Art.Wood(Theme.Hex("654936"));
            var darkWood = Art.Wood(Theme.Hex("3D3028"));
            var leather = Art.Leather(Theme.Hex("302E28"));
            var felt = Art.Fabric(Theme.Hex("285747"));
            var brass = Art.Metal(Theme.Hex("AF9060"), 0.51f);
            var thread = Art.Fabric(Theme.Hex("968567"));
            Art.Lathe("Turned Pedestal", table, Vector3.zero, Profile(
                0f, 0.014f, 0.33f, 0.014f, 0.39f, 0.028f, 0.405f, 0.048f, 0.401f, 0.07f,
                0.375f, 0.089f, 0.32f, 0.098f, 0.30f, 0.115f, 0.275f, 0.125f,
                0.255f, 0.15f, 0.23f, 0.185f, 0.19f, 0.23f, 0.15f, 0.3f,
                0.136f, 0.365f, 0.14f, 0.445f, 0.16f, 0.52f, 0.196f, 0.574f,
                0.25f, 0.604f, 0.32f, 0.624f, 0.34f, 0.648f, 0.34f, 0.701f,
                0.35f, 0.709f, 0f, 0.709f), darkWood, 80);
            for (int support = 0; support < 4; support++)
            {
                var bearer = Group("Table Bearer", table, Vector3.zero);
                bearer.localRotation = Quaternion.Euler(0f, support * 90f + 45f, 0f);
                Art.BeveledBox("Tenoned Bearer", bearer, new Vector3(0f, 0.679f, 0.53f), new Vector3(0.09f, 0.06f, 0.68f), 0.009f, darkWood);
            }
            Art.Tube("Pedestal Foot Bead", table, Circle(0.382f, 0.079f, 80), 0.004f, brass, 6);
            Art.Tube("Collar Fillet", table, Circle(0.252f, 0.603f, 64), 0.004f, brass, 6);
            Art.Lathe("Undercut Apron", table, Vector3.zero, Profile(
                0.73f, 0.605f, 0.848f, 0.605f, 0.873f, 0.616f, 0.881f, 0.632f,
                0.881f, 0.679f, 0.899f, 0.697f, 0.967f, 0.705f, 0.967f, 0.723f,
                0.73f, 0.723f, 0.73f, 0.605f), darkWood, 96);
            Art.Lathe("Ogee Walnut Rail", table, Vector3.zero, Profile(
                0f, 0.709f, 0.983f, 0.709f, 1.016f, 0.713f, 1.039f, 0.725f,
                1.05f, 0.74f, 1.048f, 0.751f, 1.039f, 0.760f, 1.022f, TableTop,
                0.929f, TableTop, 0.918f, 0.758f, 0f, 0.758f), wood, 96);
            Art.Tube("Apron Stringing", table, Circle(0.883f, 0.637f), 0.0025f, brass, 6);
            Art.Tube("Outer Rail Inlay", table, Circle(1.031f, 0.763f), 0.0018f, brass, 6);
            Art.Lathe("Padded Leather Rail", table, Vector3.zero, Profile(
                0.946f, 0.765f, 1.016f, 0.765f, 1.021f, 0.773f, 1.017f, 0.782f,
                1.006f, 0.788f, 0.982f, 0.791f, 0.959f, 0.788f, 0.947f, 0.783f,
                0.941f, 0.775f, 0.946f, 0.765f), leather, 96);
            Art.Tube("Inner Leather Welt", table, Circle(0.944f, 0.774f), 0.0023f, leather, 6);
            Art.Tube("Rail Stitch Channel", table, Circle(1.009f, 0.787f), 0.0011f, thread, 6);
            Art.Lathe("Felt Bed", table, Vector3.zero, Profile(
                0f, FeltTop - 0.009f, TableRadius, FeltTop - 0.009f, TableRadius, FeltTop, 0f, FeltTop), felt, 96);
            Art.Lathe("Felt Retaining Fillet", table, Vector3.zero, Profile(
                0.922f, 0.759f, 0.938f, 0.759f, 0.939f, 0.766f, 0.936f, 0.769f,
                0.924f, 0.769f, 0.921f, 0.766f, 0.922f, 0.759f), brass, 96);
            var playArea = Group("Central Pile Inlay", table, new Vector3(0f, 0f, 0.05f));
            Art.Tube("Pile Boundary", playArea, RoundedLoop(0.205f, 0.175f, 0.038f, FeltTop - 0.0001f), 0.00085f, thread, 6);
            for (int seat = 0; seat < 4; seat++)
            {
                var marker = Group("Seat Rail Inlay", table, Vector3.up * 0.0003f);
                marker.localRotation = Quaternion.LookRotation(SeatDirection(seat));
                Art.BeveledBox("Seat Index", marker, new Vector3(0f, TableTop, 1.027f), new Vector3(0.025f, 0.0012f, 0.006f), 0.0004f, brass);
            }
            var bowlPos = new Vector3(0.44f, FeltTop, -0.12f);
            Art.Lathe("Brass Discard Dish", table, bowlPos, Profile(
                0f, 0.001f, 0.083f, 0.001f, 0.09f, 0.004f, 0.091f, 0.01f,
                0.101f, 0.014f, 0.116f, 0.025f, 0.131f, 0.043f, 0.143f, 0.059f,
                0.145f, 0.064f, 0.142f, 0.068f, 0.138f, 0.069f, 0.134f, 0.066f,
                0.124f, 0.049f, 0.11f, 0.032f, 0.095f, 0.022f, 0.082f, 0.018f,
                0f, 0.018f), brass, 64);
            Art.Lathe("Dish Well Patina", table, bowlPos, Profile(0.08f, 0.0184f, 0f, 0.0184f), Art.Metal(Theme.Hex("665847"), 0.3f), 48);
            Batch(table);

            PileAnchor = new GameObject("Pile").transform;
            PileAnchor.SetParent(transform, false);
            PileAnchor.position = new Vector3(0f, FeltTop + 0.003f, 0.05f);
            BurnAnchor = new GameObject("Burn").transform;
            BurnAnchor.SetParent(transform, false);
            BurnAnchor.position = bowlPos + Vector3.up * 0.065f;
        }

        void BuildLamp()
        {
            var pendant = Group("Enamel Library Pendant", transform, Vector3.zero);
            var enamel = Art.Lit(Theme.Hex("30493F"), 0.46f, 0.22f);
            var brass = Art.Metal(Theme.Hex("AF9060"), 0.51f);
            var cord = Art.Leather(Theme.Hex("282924"));
            var reflector = Art.Lit(Theme.Hex("B7B09A"), 0.32f, 0.12f);
            Art.Lathe("Ceiling Rose", pendant, new Vector3(0f, 3.88f, 0f), Profile(
                0f, 0f, 0.046f, 0f, 0.07f, 0.024f, 0.136f, 0.052f, 0.148f, 0.07f, 0.148f, 0.12f, 0f, 0.12f), brass, 40);
            Art.Tube("Braided Flex", pendant, new[] { new Vector3(0f, 2.295f, 0f), new Vector3(0f, 3.895f, 0f) }, 0.008f, cord, 8);
            Art.Lathe("Shade Socket Housing", pendant, new Vector3(0f, 2.16f, 0f), Profile(
                0f, 0f, 0.043f, 0f, 0.05f, 0.018f, 0.05f, 0.082f, 0.028f, 0.1f, 0.02f, 0.14f, 0f, 0.14f), brass, 40);
            Art.Lathe("Spun Enamel Shade", pendant, Vector3.zero, Profile(
                0.343f, 1.91f, 0.36f, 1.913f, 0.367f, 1.924f, 0.364f, 1.935f,
                0.344f, 1.948f, 0.31f, 1.979f, 0.257f, 2.039f, 0.2f, 2.098f,
                0.135f, 2.144f, 0.075f, 2.168f, 0.045f, 2.173f,
                0.045f, 2.16f, 0.075f, 2.155f, 0.13f, 2.131f, 0.194f, 2.085f,
                0.25f, 2.026f, 0.303f, 1.966f, 0.337f, 1.935f, 0.343f, 1.91f), enamel, 80);
            Art.Lathe("Inner Reflector", pendant, Vector3.zero, Profile(
                0.045f, 2.158f, 0.074f, 2.153f, 0.129f, 2.129f, 0.192f, 2.083f,
                0.248f, 2.024f, 0.301f, 1.964f, 0.335f, 1.933f), reflector, 80);
            Art.Tube("Rolled Brass Lip", pendant, Circle(0.357f, 1.918f, 80), 0.006f, brass, 8);
            Art.Lathe("Porcelain Lamp Holder", pendant, Vector3.zero, Profile(
                0f, 2.046f, 0.031f, 2.046f, 0.031f, 2.153f, 0f, 2.153f), reflector, 32);
            Art.Lathe("Frosted Bulb", pendant, Vector3.zero, Profile(
                0f, 1.944f, 0.033f, 1.954f, 0.051f, 1.98f, 0.052f, 2.012f,
                0.036f, 2.035f, 0.025f, 2.055f, 0f, 2.055f),
                Art.Lit(Theme.Hex("DCCFB1"), 0.28f, 0f, null, Theme.Hex("E9C795") * 0.55f), 40);
            Batch(pendant);

            var spotGo = new GameObject("LampLight");
            spotGo.transform.SetParent(transform, false);
            spotGo.transform.position = new Vector3(0f, 1.897f, 0f);
            spotGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            lampLight = spotGo.AddComponent<Light>();
            lampLight.type = LightType.Spot;
            lampLight.color = Theme.Hex("FFE9CA");
            lampLight.intensity = 1.65f;
            lampLight.range = 4.2f;
            lampLight.spotAngle = 112f;
            lampLight.innerSpotAngle = 72f;
            lampLight.shadows = LightShadows.Soft;
            lampLight.shadowStrength = 0.65f;
            lampLight.shadowBias = 0.025f;
            lampLight.shadowNormalBias = 0.12f;
            lampLight.shadowNearPlane = 0.08f;
            lampLight.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;
            lampLight.renderMode = LightRenderMode.ForcePixel;
            lampBase = lampLight.intensity;

            var fillGo = new GameObject("FillLight");
            fillGo.transform.SetParent(transform, false);
            fillGo.transform.rotation = Quaternion.Euler(28f, 8f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = Theme.Hex("DCE5E0");
            fill.intensity = 0.28f;
            fill.shadows = LightShadows.None;
        }

        void BuildChair(Vector3 dir)
        {
            var wood = Art.Wood(Theme.Hex("534033"));
            var cushion = Art.Leather(Theme.Hex("573B3E"));
            var piping = Art.Leather(Theme.Hex("392E2D"));
            var basePos = dir * (SeatDistance + 0.08f);
            var rot = Quaternion.LookRotation(-dir);
            var chair = new GameObject("Chair").transform;
            chair.SetParent(transform, false);
            chair.position = basePos;
            chair.rotation = rot;
            Art.BeveledBox("Seat Frame", chair, new Vector3(0f, 0.437f, 0f), new Vector3(0.5f, 0.065f, 0.48f), 0.017f, wood);
            Art.BeveledBox("Leather Seat", chair, new Vector3(0f, 0.477f, 0f), new Vector3(0.455f, 0.047f, 0.435f), 0.022f, cushion);
            Art.Tube("Seat Welt", chair, RoundedLoop(0.217f, 0.207f, 0.025f, 0.478f), 0.003f, piping, 6);
            Art.BeveledBox("Back Upholstery", chair, new Vector3(0f, 0.86f, -0.202f), new Vector3(0.394f, 0.545f, 0.06f), 0.028f, cushion);
            Frame(chair, new Vector3(0f, 0.86f, -0.22f), 0.5f, 0.75f, 0.056f, 0.075f, wood);
            Art.BeveledBox("Crest Rail", chair, new Vector3(0f, 1.237f, -0.22f), new Vector3(0.514f, 0.052f, 0.083f), 0.021f, wood);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                    Art.BeveledBox("Leg", chair, new Vector3(x * 0.21f, 0.215f, z * 0.2f), new Vector3(0.055f, 0.4f, 0.055f), 0.008f, wood);
                Art.BeveledBox("Side Stretcher", chair, new Vector3(x * 0.21f, 0.15f, 0f), new Vector3(0.029f, 0.035f, 0.37f), 0.006f, wood);
            }
            Art.BeveledBox("Cross Stretcher", chair, new Vector3(0f, 0.15f, 0f), new Vector3(0.42f, 0.029f, 0.032f), 0.006f, wood);
            Batch(chair);
        }

        // ================================================================== peluru & kartu kuasa di meja

        void BuildShells(int id, Vector3 dir)
        {
            var right = new Vector3(-dir.z, 0f, dir.x);
            var brass = Art.Lit(Theme.Hex("E0B05A"), 0.75f, 0.3f);
            var tip = Art.Lit(Theme.Hex("C2783A"), 0.7f, 0.3f);
            var list = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Shell").transform;
                root.SetParent(transform, false);
                root.position = dir * 0.7f + right * (0.26f + i * 0.045f) + Vector3.up * FeltTop;
                Art.Primitive(PrimitiveType.Cylinder, "Case", root, new Vector3(0f, 0.025f, 0f), new Vector3(0.022f, 0.025f, 0.022f), brass);
                Art.Primitive(PrimitiveType.Sphere, "Tip", root, new Vector3(0f, 0.055f, 0f), new Vector3(0.021f, 0.03f, 0.021f), tip);
                root.gameObject.SetActive(false);
                list.Add(root);
            }
            shells[id] = list;
        }

        void BuildPowerSlot(int id, Vector3 dir)
        {
            var card = Card3D.Create(transform, "PowerCard " + id);
            card.SetPower(PowerType.None);
            var right = new Vector3(-dir.z, 0f, dir.x);
            var pos = dir * 0.66f - right * 0.3f + Vector3.up * (FeltTop + 0.003f);
            card.transform.position = pos;
            // Berbaring telungkup, sisi atas kartu menghadap tengah meja.
            card.transform.rotation = Quaternion.LookRotation(Vector3.up, -dir);
            card.transform.localScale = Vector3.one * 0.8f;
            card.gameObject.SetActive(false);
            powerCards[id] = card;
        }

        public void SetBullets(int id, int count)
        {
            if (!shells.TryGetValue(id, out var list))
                return;
            for (int i = 0; i < list.Count; i++)
            {
                bool show = i < count;
                if (list[i].gameObject.activeSelf != show)
                {
                    list[i].gameObject.SetActive(show);
                    if (show)
                        StartCoroutine(Tween.Punch(list[i], 0.6f, 0.3f));
                }
            }
        }

        public Vector3 ShellPosition(int id, int index)
        {
            var list = shells[id];
            return list[Mathf.Clamp(index, 0, list.Count - 1)].position;
        }

        /// <summary>Tampilkan Kartu Kuasa di meja: tertutup (rahasia) atau terbuka (sudah dipakai / milik sendiri).</summary>
        public void SetPowerCard(int id, PowerType power, bool faceUp, bool spent)
        {
            if (!powerCards.TryGetValue(id, out var card))
                return;
            card.gameObject.SetActive(true);
            card.SetPower(power);
            var dir = SeatDirection(relativeSeat[id]);
            // Muka kartu ada di -Z lokal: telungkup = +Z ke atas, terbuka = +Z ke bawah. Sisi atas kartu menghadap tengah meja.
            card.transform.rotation = Quaternion.LookRotation(faceUp ? Vector3.down : Vector3.up, -dir);
            card.SetGlow(CardArt.PowerColor(power), spent ? 0f : faceUp ? 0.5f : 0f);
        }

        public Card3D PowerCardObject(int id) => powerCards.TryGetValue(id, out var c) ? c : null;

        // ================================================================== karakter & kipas kartu

        public Humanoid Character(int id) => characters.TryGetValue(id, out var c) ? c : null;

        public IEnumerable<Humanoid> Characters => characters.Values;

        /// <summary>Posisi kepala (untuk label nama) atau posisi kursi manusia.</summary>
        public Vector3 HeadPosition(int id)
        {
            if (characters.TryGetValue(id, out var c))
                return c.Head.position + Vector3.up * 0.12f;
            return SeatDirection(relativeSeat[id]) * 1.1f + Vector3.up * 1.15f;
        }

        /// <summary>
        /// Titik label nama di samping kepala lawan (ke arah tepi layar), supaya tidak tertutup UI di atas layar.
        /// </summary>
        public Vector3 LabelPosition(int id)
        {
            if (!characters.TryGetValue(id, out var c))
                return HeadPosition(id);
            int relative = relativeSeat[id];
            float side = relative == 1 ? -1f : 1f;
            var right = Camera != null ? Camera.transform.right : Vector3.right;
            float distance = relative == 2 ? 0.5f : 0.46f;
            return c.Head.position + right * side * distance + Vector3.down * (relative == 2 ? 0.16f : 0.06f);
        }

        /// <summary>Titik asal kartu yang ditaruh pemain ini (kipas kartunya / tangan pemain).</summary>
        public Vector3 HandPosition(int id)
        {
            if (characters.TryGetValue(id, out var c))
                return c.CardHold.position;
            return Camera != null ? Camera.transform.position + Camera.transform.forward * 0.5f - Camera.transform.up * 0.25f
                : SeatDirection(relativeSeat[id]) * 0.9f + Vector3.up;
        }

        public Vector3 SeatDirectionOf(int id) => SeatDirection(relativeSeat[id]);

        /// <summary>Atur jumlah kartu yang terlihat di kipas lawan (maks. 12 kartu ditampilkan).</summary>
        public void SetFanCount(int id, int count)
        {
            if (!fans.TryGetValue(id, out var fan))
                return;
            int visible = Mathf.Clamp(count, 0, 12);
            var hold = characters[id].CardHold;
            while (fan.Count < visible)
            {
                var card = Card3D.Create(hold, "FanCard");
                card.SetHiddenCard();
                card.SetCastShadows(false);
                fan.Add(card);
            }
            while (fan.Count > visible)
            {
                var last = fan[fan.Count - 1];
                fan.RemoveAt(fan.Count - 1);
                Destroy(last.gameObject);
            }
            LayoutFan(fan);
        }

        static void LayoutFan(List<Card3D> fan)
        {
            float spread = Mathf.Min(70f, 7f * Mathf.Max(0, fan.Count - 1));
            for (int i = 0; i < fan.Count; i++)
            {
                float t = fan.Count == 1 ? 0.5f : i / (float)(fan.Count - 1);
                float angle = Mathf.Lerp(spread * 0.5f, -spread * 0.5f, t);
                var rot = Quaternion.Euler(0f, 0f, angle);
                var tr = fan[i].transform;
                tr.localRotation = rot;
                tr.localPosition = rot * new Vector3(0f, 0.07f, 0f) + new Vector3(0f, -0.04f, i * 0.0025f);
                tr.localScale = Vector3.one * 0.85f;
            }
        }

        public List<Card3D> TakeFanCards(int id, int count)
        {
            var taken = new List<Card3D>();
            if (count <= 0 || !fans.TryGetValue(id, out var fan))
                return taken;
            int start = Mathf.Max(0, fan.Count - count);
            for (int i = start; i < fan.Count; i++)
            {
                var card = fan[i];
                if (card == null)
                    continue;
                card.Interactable = false;
                card.transform.SetParent(transform, true);
                taken.Add(card);
            }
            fan.RemoveRange(start, fan.Count - start);
            LayoutFan(fan);
            return taken;
        }

        public int FanCount(int id) => fans.TryGetValue(id, out var fan) ? fan.Count : 0;

        public void LookAll(Vector3 target)
        {
            foreach (var c in characters.Values)
                c.LookAt(target);
        }

        // ================================================================== suasana

        public void FlashLamp(float amount) => lampFlicker = Mathf.Max(lampFlicker, amount);

        void Update()
        {
            if (lampLight == null)
                return;
            lampFlicker = Mathf.MoveTowards(lampFlicker, 0f, Time.deltaTime * 2.5f);
            float wobble = Mathf.PerlinNoise(Time.time * 2f, 0.3f) * 0.025f;
            lampLight.intensity = lampBase * (0.9875f + wobble) + Mathf.Min(lampFlicker, 1f) * 0.75f;
        }
    }
}
