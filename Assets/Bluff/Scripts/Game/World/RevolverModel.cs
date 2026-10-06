using System.Collections.Generic;
using UnityEngine;

namespace Bluff.Game
{
    public static class RevolverModel
    {
        enum AssemblyPart
        {
            Body,
            Hammer,
            Trigger,
            Cylinder,
        }

        struct Part
        {
            public AssemblyPart Assembly;
            public Mesh Mesh;
            public Material Material;
        }

        static Part[] parts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitializeCache()
        {
            ReleaseParts();
            Application.quitting -= ReleaseParts;
            Application.quitting += ReleaseParts;
        }

        static void ReleaseParts()
        {
            if (parts == null)
                return;
            foreach (var part in parts)
                if (part.Mesh != null)
                    Object.Destroy(part.Mesh);
            parts = null;
        }

        public static Transform Build(Transform parent)
        {
            if (parent == null)
                return null;
            var mechanism = parent.GetComponent<RevolverMechanism>();
            if (mechanism != null && mechanism.Muzzle != null)
                return mechanism.Muzzle;
            if (parts == null || parts.Length == 0 || parts[0].Mesh == null)
            {
                ReleaseParts();
                parts = BuildParts();
            }
            var visuals = Child("RevolverVisuals", parent, Vector3.zero);
            var pivots = CreatePivots(visuals);
            foreach (var part in parts)
            {
                var child = Child(part.Mesh.name, pivots[(int)part.Assembly], Vector3.zero);
                child.gameObject.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                child.gameObject.AddComponent<MeshRenderer>().sharedMaterial = part.Material;
            }
            if (mechanism == null)
                mechanism = parent.gameObject.AddComponent<RevolverMechanism>();
            mechanism.HammerPivot = pivots[(int)AssemblyPart.Hammer];
            mechanism.TriggerPivot = pivots[(int)AssemblyPart.Trigger];
            mechanism.CylinderPivot = pivots[(int)AssemblyPart.Cylinder];
            mechanism.HammerBindRotation = mechanism.HammerPivot.localRotation;
            mechanism.TriggerBindRotation = mechanism.TriggerPivot.localRotation;
            mechanism.CylinderBindRotation = mechanism.CylinderPivot.localRotation;
            mechanism.Muzzle = Child("Muzzle", parent, new Vector3(0f, 0.03f, 0.17f));
            return mechanism.Muzzle;
        }

        public static void SetMechanism(Transform parent, float cock, float trigger, float cylinderTurn)
        {
            if (parent == null)
                return;
            var mechanism = parent.GetComponent<RevolverMechanism>();
            if (mechanism == null)
                return;
            if (mechanism.HammerPivot != null)
                mechanism.HammerPivot.localRotation = mechanism.HammerBindRotation * Quaternion.AngleAxis(-42f * Mathf.Clamp01(cock), Vector3.right);
            if (mechanism.TriggerPivot != null)
                mechanism.TriggerPivot.localRotation = mechanism.TriggerBindRotation * Quaternion.AngleAxis(24f * Mathf.Clamp01(trigger), Vector3.right);
            if (mechanism.CylinderPivot != null)
                mechanism.CylinderPivot.localRotation = mechanism.CylinderBindRotation * Quaternion.AngleAxis(60f * cylinderTurn, Vector3.forward);
        }

        static Transform[] CreatePivots(Transform parent)
        {
            return new[]
            {
                Child("Body", parent, Vector3.zero),
                Child("HammerPivot", parent, new Vector3(0f, 0.037f, -0.03f)),
                Child("TriggerPivot", parent, new Vector3(0f, -0.005f, 0.011f)),
                Child("CylinderPivot", parent, new Vector3(0f, 0.03f, 0.0225f)),
            };
        }

        static Part[] BuildParts()
        {
            var root = Child("RevolverSource", null, Vector3.zero);
            root.gameObject.SetActive(false);
            var generated = new List<Mesh>();
            var result = new List<Part>();
            try
            {
                var pivots = CreatePivots(root);
                var assemblies = new[]
                {
                    Child("BodySource", root, Vector3.zero),
                    Child("HammerSource", root, Vector3.zero),
                    Child("TriggerSource", root, Vector3.zero),
                    Child("CylinderSource", root, Vector3.zero),
                };
                BuildGeometry(assemblies[0], assemblies[1], assemblies[2], assemblies[3], generated);
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var pivot = pivots[i];
                    var groups = new Dictionary<Material, List<CombineInstance>>();
                    foreach (var filter in assemblies[i].GetComponentsInChildren<MeshFilter>(true))
                    {
                        var material = filter.GetComponent<MeshRenderer>().sharedMaterial;
                        if (!groups.TryGetValue(material, out var instances))
                        {
                            instances = new List<CombineInstance>();
                            groups.Add(material, instances);
                        }
                        instances.Add(new CombineInstance
                        {
                            mesh = filter.sharedMesh,
                            transform = pivot.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                        });
                    }
                    foreach (var group in groups)
                    {
                        var mesh = new Mesh { name = "Revolver" + (AssemblyPart)i + "Surface" + result.Count, hideFlags = HideFlags.DontUnloadUnusedAsset };
                        result.Add(new Part { Assembly = (AssemblyPart)i, Mesh = mesh, Material = group.Key });
                        mesh.CombineMeshes(group.Value.ToArray(), true, true);
                        mesh.RecalculateBounds();
                    }
                }
                return result.ToArray();
            }
            catch
            {
                foreach (var part in result)
                    Object.Destroy(part.Mesh);
                throw;
            }
            finally
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    filter.sharedMesh = null;
                    Art.ReleaseOwnedMesh(filter);
                }
                Object.Destroy(root.gameObject);
                foreach (var mesh in generated)
                    Object.Destroy(mesh);
            }
        }

        static void BuildGeometry(Transform assembly, Transform hammerAssembly, Transform triggerAssembly, Transform cylinderAssembly, List<Mesh> generated)
        {
            var steel = Art.Metal(Theme.Hex("252F3A"), 0.65f);
            var edges = Art.Metal(Theme.Hex("59616A"), 0.59f);
            var dark = Art.Metal(Theme.Hex("0B1018"), 0.28f);
            var walnut = Art.Wood(Theme.Hex("583521"));
            var brass = Art.Metal(Theme.Hex("9B8352"), 0.46f);

            Art.BeveledBox("RearFrame", assembly, new Vector3(0f, 0.024f, -0.019f), new Vector3(0.033f, 0.054f, 0.024f), 0.005f, steel);
            Art.BeveledBox("TopStrap", assembly, new Vector3(0f, 0.059f, 0.015f), new Vector3(0.026f, 0.011f, 0.084f), 0.003f, steel);
            Art.BeveledBox("LowerFrame", assembly, new Vector3(0f, -0.004f, 0.008f), new Vector3(0.025f, 0.012f, 0.074f), 0.003f, steel);
            Art.BeveledBox("BarrelShoulder", assembly, new Vector3(0f, 0.032f, 0.053f), new Vector3(0.03f, 0.045f, 0.021f), 0.005f, steel);
            Art.BeveledBox("SidePlate", assembly, new Vector3(0.017f, 0.02f, -0.021f), new Vector3(0.002f, 0.034f, 0.019f), 0.001f, edges);

            var barrelProfile = new[]
            {
                new Vector2(0.015f, 0.05f), new Vector2(0.015f, 0.068f), new Vector2(0.0125f, 0.077f),
                new Vector2(0.012f, 0.159f), new Vector2(0.0128f, 0.164f), new Vector2(0.0118f, 0.168f),
                new Vector2(0.0072f, 0.168f), new Vector2(0.0065f, 0.163f), new Vector2(0.0065f, 0.143f),
                new Vector2(0.0065f, 0.05f),
            };
            RadialMesh("HollowBarrel", assembly, new Vector3(0f, 0.03f, 0f), barrelProfile, steel, generated, false);
            RadialMesh("MuzzleCrown", assembly, new Vector3(0f, 0.03f, 0f), new[]
            {
                new Vector2(0.0119f, 0.1668f), new Vector2(0.0118f, 0.1682f),
                new Vector2(0.0072f, 0.1682f), new Vector2(0.007f, 0.1668f),
            }, edges, generated, false);
            Axial("BoreShadow", assembly, new Vector3(0f, 0.03f, 0.143f), new[]
            {
                new Vector2(0.0067f, 0f), new Vector2(0.0067f, 0.001f),
            }, dark, 24);
            Art.BeveledBox("BarrelRib", assembly, new Vector3(0f, 0.0435f, 0.108f), new Vector3(0.008f, 0.005f, 0.107f), 0.0015f, steel);
            Axial("EjectorHousing", assembly, new Vector3(0.012f, 0.013f, 0.047f), new[]
            {
                new Vector2(0.004f, 0f), new Vector2(0.0045f, 0.004f), new Vector2(0.0035f, 0.083f),
                new Vector2(0.0025f, 0.088f),
            }, steel, 20);
            Axial("EjectorTip", assembly, new Vector3(0.012f, 0.013f, 0.135f), new[]
            {
                new Vector2(0.0032f, 0f), new Vector2(0.0032f, 0.005f),
            }, edges, 16);

            RadialMesh("FlutedCylinder", cylinderAssembly, new Vector3(0f, 0.03f, 0f), new[]
            {
                new Vector2(0.024f, -0.004f), new Vector2(0.027f, 0f), new Vector2(0.027f, 0.007f),
                new Vector2(0.027f, 0.012f), new Vector2(0.027f, 0.02f), new Vector2(0.027f, 0.034f),
                new Vector2(0.027f, 0.04f), new Vector2(0.027f, 0.045f), new Vector2(0.0245f, 0.049f),
            }, steel, generated, true);
            Axial("CylinderRearBand", cylinderAssembly, new Vector3(0f, 0.03f, -0.003f), new[]
            {
                new Vector2(0.0245f, 0f), new Vector2(0.0272f, 0.0015f),
                new Vector2(0.0272f, 0.003f), new Vector2(0.0265f, 0.0045f),
            }, edges);
            Axial("CylinderFrontFace", cylinderAssembly, new Vector3(0f, 0.03f, 0.045f), new[]
            {
                new Vector2(0.0268f, 0f), new Vector2(0.0265f, 0.002f), new Vector2(0.0245f, 0.004f),
            }, edges);
            Axial("CylinderAxle", assembly, new Vector3(0f, 0.03f, 0.049f), new[]
            {
                new Vector2(0.0045f, 0f), new Vector2(0.0045f, 0.005f),
            }, steel, 20);
            for (int i = 0; i < 6; i++)
            {
                float angle = (i + 0.5f) * Mathf.PI * 2f / 6f;
                var center = new Vector3(Mathf.Cos(angle) * 0.016f, 0.03f + Mathf.Sin(angle) * 0.016f, 0.0493f);
                Axial("CylinderFaceRecess", cylinderAssembly, center, new[] { new Vector2(0.0054f, 0f), new Vector2(0.0048f, 0.0005f) }, dark, 20);
                var notch = Art.BeveledBox("CylinderStopNotch", cylinderAssembly,
                    new Vector3(Mathf.Cos(angle) * 0.0268f, 0.03f + Mathf.Sin(angle) * 0.0268f, 0.007f),
                    new Vector3(0.0018f, 0.006f, 0.0045f), 0.0007f, dark);
                notch.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            }

            BuildGrip(assembly, steel, walnut, brass, dark, generated);
            Art.Tube("TriggerGuard", assembly, new[]
            {
                new Vector3(0f, -0.004f, -0.018f), new Vector3(0f, -0.024f, -0.017f),
                new Vector3(0f, -0.035f, -0.009f), new Vector3(0f, -0.039f, 0.007f),
                new Vector3(0f, -0.037f, 0.02f), new Vector3(0f, -0.029f, 0.03f),
                new Vector3(0f, -0.016f, 0.034f), new Vector3(0f, -0.006f, 0.03f),
                new Vector3(0f, -0.004f, 0.022f),
            }, 0.0035f, steel, 12);
            Art.Tube("CurvedTrigger", triggerAssembly, new[]
            {
                new Vector3(0f, -0.005f, 0.011f), new Vector3(0f, -0.015f, 0.009f),
                new Vector3(0f, -0.023f, 0.003f), new Vector3(0f, -0.025f, -0.002f),
            }, 0.0023f, edges, 10);
            var hammer = Art.BeveledBox("Hammer", hammerAssembly, new Vector3(0f, 0.049f, -0.035f), new Vector3(0.011f, 0.025f, 0.011f), 0.002f, edges);
            hammer.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            var spur = Art.BeveledBox("HammerSpur", hammerAssembly, new Vector3(0f, 0.061f, -0.042f), new Vector3(0.018f, 0.005f, 0.021f), 0.002f, steel);
            spur.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            for (int i = 0; i < 4; i++)
                Art.BeveledBox("HammerSerration", hammerAssembly, new Vector3(0f, 0.064f + i * 0.0007f, -0.035f - i * 0.004f), new Vector3(0.014f, 0.001f, 0.001f), 0.0003f, edges);
            Art.BeveledBox("FrontSight", assembly, new Vector3(0f, 0.051f, 0.155f), new Vector3(0.0035f, 0.011f, 0.016f), 0.001f, dark);
            foreach (int side in new[] { -1, 1 })
            {
                Art.BeveledBox("RearSightEar", assembly, new Vector3(side * 0.006f, 0.066f, -0.018f), new Vector3(0.006f, 0.007f, 0.011f), 0.001f, dark);
                Screw("FrameScrew", assembly, new Vector3(side * 0.0178f, 0.024f, -0.02f), side, 0.0038f, edges, dark);
                Screw("LowerFrameScrew", assembly, new Vector3(side * 0.013f, -0.005f, -0.024f), side, 0.0028f, edges, dark);
            }
            Art.BeveledBox("CylinderLatch", assembly, new Vector3(-0.02f, 0.037f, -0.022f), new Vector3(0.007f, 0.01f, 0.015f), 0.002f, steel);
        }

        static void BuildGrip(Transform parent, Material steel, Material walnut, Material brass, Material dark, List<Mesh> generated)
        {
            var outline = new[]
            {
                new Vector2(-0.024f, 0.017f), new Vector2(-0.018f, 0.001f), new Vector2(-0.026f, -0.016f),
                new Vector2(-0.034f, -0.03f), new Vector2(-0.029f, -0.05f), new Vector2(-0.025f, -0.062f),
                new Vector2(-0.043f, -0.068f), new Vector2(-0.065f, -0.065f), new Vector2(-0.069f, -0.058f),
                new Vector2(-0.059f, -0.036f), new Vector2(-0.05f, -0.009f), new Vector2(-0.046f, 0.009f),
            };
            GripMesh("GripFrame", parent, outline, 0f, 0.014f, 1f, steel, generated);
            foreach (int side in new[] { -1, 1 })
            {
                GripMesh("WalnutGrip", parent, outline, side * 0.014f, 0.0045f, 0.88f, walnut, generated);
                Screw("GripScrew", parent, new Vector3(side * 0.019f, -0.031f, -0.044f), side, 0.0035f, brass, dark);
                for (int i = 0; i < 6; i++)
                {
                    float y = -0.016f - i * 0.006f;
                    float z = -0.042f - i * 0.002f;
                    Art.Tube("GripCheckering", parent, new[]
                    {
                        new Vector3(side * 0.01855f, y + 0.002f, z - 0.005f),
                        new Vector3(side * 0.01865f, y - 0.004f, z + 0.005f),
                    }, 0.00035f, dark, 5);
                    Art.Tube("GripCheckeringCross", parent, new[]
                    {
                        new Vector3(side * 0.01855f, y - 0.004f, z - 0.005f),
                        new Vector3(side * 0.01865f, y + 0.002f, z + 0.005f),
                    }, 0.00035f, dark, 5);
                }
            }
        }

        static void GripMesh(string name, Transform parent, Vector2[] outline, float centerX, float halfWidth, float scale, Material material, List<Mesh> generated)
        {
            var center = new Vector2(-0.044f, -0.027f);
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var widths = new[] { -halfWidth, -halfWidth * 0.65f, halfWidth * 0.65f, halfWidth };
            for (int layer = 0; layer < widths.Length; layer++)
            {
                float inset = layer == 0 || layer == 3 ? 0.92f : 1f;
                for (int i = 0; i < outline.Length; i++)
                {
                    var point = center + (outline[i] - center) * scale * inset;
                    vertices.Add(new Vector3(centerX + widths[layer], point.y, point.x));
                    uv.Add(new Vector2((point.x + 0.07f) * 18f, (point.y + 0.07f) * 12f));
                    if (layer == 0)
                        continue;
                    int a = (layer - 1) * outline.Length + i;
                    int b = (layer - 1) * outline.Length + (i + 1) % outline.Length;
                    Quad(triangles, a, b, b + outline.Length, a + outline.Length);
                }
            }
            for (int cap = 0; cap < 2; cap++)
            {
                int start = cap == 0 ? 0 : 3 * outline.Length;
                int pivot = vertices.Count;
                vertices.Add(new Vector3(centerX + (cap == 0 ? -halfWidth : halfWidth), center.y, center.x));
                uv.Add(new Vector2((center.x + 0.07f) * 18f, (center.y + 0.07f) * 12f));
                for (int i = 0; i < outline.Length; i++)
                {
                    int a = start + i;
                    int b = start + (i + 1) % outline.Length;
                    triangles.AddRange(cap == 0 ? new[] { pivot, b, a } : new[] { pivot, a, b });
                }
            }
            MeshObject(name, parent, Vector3.zero, vertices, uv, triangles, material, generated);
        }

        static void RadialMesh(string name, Transform parent, Vector3 position, Vector2[] profile, Material material, List<Mesh> generated, bool fluted)
        {
            const int segments = 96;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 0; row < profile.Length; row++)
            {
                float z = profile[row].y;
                float flute = fluted ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.007f, 0.017f, z)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.034f, 0.045f, z))) : 0f;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    float radius = profile[row].x - 0.0034f * flute * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * 6f)), 1.7f);
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z));
                    uv.Add(new Vector2(i / (float)segments, row / (float)(profile.Length - 1)));
                    if (row > 0 && i < segments)
                    {
                        int a = (row - 1) * (segments + 1) + i;
                        Quad(triangles, a, a + 1, a + segments + 2, a + segments + 1);
                    }
                }
            }
            if (!fluted)
            {
                int last = (profile.Length - 1) * (segments + 1);
                for (int i = 0; i < segments; i++)
                    Quad(triangles, last + i, last + i + 1, i + 1, i);
            }
            var mesh = MeshObject(name, parent, position, vertices, uv, triangles, material, generated);
            var normals = mesh.normals;
            for (int row = 0; row < profile.Length; row++)
            {
                int start = row * (segments + 1);
                var normal = (normals[start] + normals[start + segments]).normalized;
                normals[start] = normal;
                normals[start + segments] = normal;
            }
            mesh.normals = normals;
            mesh.RecalculateTangents();
        }

        static Mesh MeshObject(string name, Transform parent, Vector3 position, List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Material material, List<Mesh> generated)
        {
            var mesh = new Mesh { name = name };
            generated.Add(mesh);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var child = Child(name, parent, position);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return mesh;
        }

        static void Quad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.AddRange(new[] { a, b, c, a, c, d });
        }

        static void Axial(string name, Transform parent, Vector3 position, Vector2[] profile, Material material, int segments = 48)
        {
            var closed = new Vector2[profile.Length + 2];
            closed[0] = new Vector2(0f, profile[0].y);
            System.Array.Copy(profile, 0, closed, 1, profile.Length);
            closed[closed.Length - 1] = new Vector2(0f, profile[profile.Length - 1].y);
            var part = Art.Lathe(name, parent, position, closed, material, segments);
            part.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        static void Screw(string name, Transform parent, Vector3 position, int side, float radius, Material metal, Material dark)
        {
            var screw = Art.Lathe(name, parent, position, new[]
            {
                new Vector2(0f, -0.0006f), new Vector2(radius * 0.85f, -0.0006f), new Vector2(radius, 0f),
                new Vector2(radius * 0.85f, 0.0009f), new Vector2(0f, 0.0009f),
            }, metal, 20);
            screw.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 90f);
            var slot = Art.BeveledBox("ScrewSlot", parent, position + Vector3.right * (side * 0.001f), new Vector3(0.0004f, radius * 1.3f, 0.0007f), 0.00015f, dark);
            slot.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
        }

        static Transform Child(string name, Transform parent, Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }
    }

    internal sealed class RevolverMechanism : MonoBehaviour
    {
        internal Transform HammerPivot;
        internal Transform TriggerPivot;
        internal Transform CylinderPivot;
        internal Transform Muzzle;
        internal Quaternion HammerBindRotation;
        internal Quaternion TriggerBindRotation;
        internal Quaternion CylinderBindRotation;
    }
}
