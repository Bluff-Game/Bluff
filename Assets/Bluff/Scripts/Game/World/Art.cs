using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Bluff.Game
{
    /// <summary>
    /// Pabrik material &amp; primitif 3D. Semua objek dunia dibangun lewat kode (tanpa model/asset luar),
    /// dengan shader dari Resources/BluffShaders supaya ikut ter-build (termasuk WebGL).
    /// </summary>
    public static class Art
    {
        static Shader lit;
        static Shader unlit;
        static Shader additive;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static Texture2D softTexture;
        static Texture2D sparkTexture;
        static Texture2D ringTexture;
        static Texture2D streakTexture;

        static Shader LitShader => lit != null ? lit : (lit = FindShader("Bluff/Lit", "Standard"));
        static Shader UnlitShader => unlit != null ? unlit : (unlit = FindShader("Bluff/Unlit", "Sprites/Default"));
        static Shader AdditiveShader => additive != null ? additive : (additive = FindShader("Bluff/Additive", "Sprites/Default"));

        static Shader FindShader(string name, string fallback)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning("[Bluff] Shader " + name + " tidak ditemukan, memakai " + fallback);
                shader = Shader.Find(fallback);
            }
            return shader;
        }

        // ------------------------------------------------------------------ materials

        /// <summary>Material berpencahayaan (di-cache per kombinasi parameter). Jangan diubah langsung; pakai <see cref="LitInstance"/>.</summary>
        public static Material Lit(Color color, float smoothness = 0.25f, float metallic = 0f, Texture texture = null, Color? emission = null, Color? rim = null)
        {
            string key = "lit|" + ColorKey(color) + "|" + smoothness.ToString("0.00") + "|" + metallic.ToString("0.00") + "|" +
                         (texture != null ? texture.GetInstanceID().ToString() : "-") + "|" +
                         (emission.HasValue ? ColorKey(emission.Value) : "-") + "|" + (rim.HasValue ? ColorKey(rim.Value) : "-");
            if (cache.TryGetValue(key, out var mat) && mat != null)
                return mat;
            mat = LitInstance(color, smoothness, metallic, texture, emission, rim);
            cache[key] = mat;
            return mat;
        }

        /// <summary>Material berpencahayaan baru yang boleh diubah-ubah (mis. warnanya dianimasikan).</summary>
        public static Material LitInstance(Color color, float smoothness = 0.25f, float metallic = 0f, Texture texture = null, Color? emission = null, Color? rim = null)
        {
            var mat = new Material(LitShader) { name = "BluffLit", hideFlags = HideFlags.DontUnloadUnusedAsset };
            mat.SetColor("_Color", color);
            mat.SetFloat("_Glossiness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (texture != null)
                mat.SetTexture("_MainTex", texture);
            mat.SetColor("_EmissionColor", emission ?? Color.black);
            mat.SetColor("_RimColor", rim ?? new Color(1f, 1f, 1f, 0f));
            return mat;
        }

        static readonly Texture2D[] surfaceTextures = new Texture2D[4];
        static readonly Texture2D[] surfaceNormals = new Texture2D[4];

        public static Material Wood(Color color) => Surface(color, 0, 0.34f, 0f, 2f, 0.24f);

        public static Material Fabric(Color color) => Surface(color, 1, 0.12f, 0f, 14f, 0.3f);

        public static Material Leather(Color color) => Surface(color, 2, 0.3f, 0f, 6f, 0.32f);

        public static Material Metal(Color color, float smoothness = 0.55f) => Surface(color, 3, smoothness, 0.82f, 3f, 0.12f);

        static Material Surface(Color color, int kind, float smoothness, float metallic, float scale, float relief)
        {
            smoothness = Mathf.Clamp01(smoothness);
            string key = "surface|" + kind + "|" + ColorKey(color) + "|" + Number(smoothness);
            if (cache.TryGetValue(key, out var material) && material != null)
                return material;
            if (surfaceTextures[kind] == null)
                BuildSurfaceTextures(kind);
            material = LitInstance(color, smoothness, metallic);
            material.name = new[] { "Bluff Walnut", "Bluff Woven Cloth", "Bluff Grained Leather", "Bluff Brushed Metal" }[kind];
            material.enableInstancing = true;
            if (material.HasProperty("_SurfaceTex"))
            {
                material.EnableKeyword("_SURFACE_DETAIL");
                material.SetTexture("_SurfaceTex", surfaceTextures[kind]);
                material.SetTexture("_SurfaceNormal", surfaceNormals[kind]);
                material.SetFloat("_SurfaceScale", scale);
                material.SetFloat("_SurfaceRelief", relief);
            }
            cache[key] = material;
            return material;
        }

        static float GrainNoise(int x, int y, int salt)
        {
            unchecked
            {
                uint value = (uint)(x * 374761393 + y * 668265263 + salt * 144269);
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value ^ (value >> 16)) / (float)uint.MaxValue;
            }
        }

        static void BuildSurfaceTextures(int kind)
        {
            const int size = 256;
            var heights = new float[size * size];
            var pixels = new Color[size * size];
            var normals = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * Mathf.PI * 2f;
                    float v = y / (float)size * Mathf.PI * 2f;
                    float noise = GrainNoise(x, y, kind + 1);
                    float height;
                    float tone;
                    float polish;
                    if (kind == 0)
                    {
                        float bend = Mathf.Sin(v) * 0.7f + Mathf.Sin(v * 3f + Mathf.Sin(u)) * 0.22f;
                        float grain = Mathf.Sin(u * 9f + bend + Mathf.Sin(u * 2f - v) * 0.35f);
                        float pore = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(u * 39f + bend * 4f)), 14f);
                        height = grain * 0.16f - pore * 0.15f + noise * 0.08f;
                        tone = 0.83f + grain * 0.085f - pore * 0.075f + noise * 0.035f;
                        polish = 0.78f + grain * 0.1f - pore * 0.12f;
                    }
                    else if (kind == 1)
                    {
                        float warp = Mathf.Sin(u * 32f);
                        float weft = Mathf.Sin(v * 32f);
                        height = warp * weft * 0.18f + (warp + weft) * 0.07f + noise * 0.13f;
                        tone = 0.89f + height * 0.13f + noise * 0.035f;
                        polish = 0.55f + noise * 0.22f;
                    }
                    else if (kind == 2)
                    {
                        float cells = Mathf.Sin(u * 17f + Mathf.Sin(v * 11f)) * Mathf.Sin(v * 19f + Mathf.Sin(u * 13f));
                        float crease = Mathf.Pow(Mathf.Abs(cells), 0.45f);
                        height = crease * 0.3f + noise * 0.1f;
                        tone = 0.82f + crease * 0.13f + noise * 0.045f;
                        polish = 0.62f + crease * 0.3f;
                    }
                    else
                    {
                        float brush = GrainNoise(x, 0, 19);
                        height = brush * 0.18f + noise * 0.025f;
                        tone = 0.92f + brush * 0.06f;
                        polish = 0.72f + brush * 0.23f;
                    }
                    int index = y * size + x;
                    heights[index] = height;
                    pixels[index] = new Color(tone, tone, tone, Mathf.Clamp01(polish));
                }
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                    float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                    var n = new Vector3(-dx * 2f, -dy * 2f, 1f).normalized;
                    normals[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            surfaceTextures[kind] = SurfaceTexture("Bluff Surface " + kind, size, pixels);
            surfaceNormals[kind] = SurfaceTexture("Bluff Surface Normal " + kind, size, normals);
        }

        static Texture2D SurfaceTexture(string name, int size, Color[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        public static Material Unlit(Color color, Texture texture = null) => Flat(UnlitShader, "unlit", color, texture);

        public static Material Additive(Color color, Texture texture = null) => Flat(AdditiveShader, "add", color, texture);

        public static Material UnlitInstance(Color color, Texture texture = null) => FlatInstance(UnlitShader, color, texture);

        public static Material AdditiveInstance(Color color, Texture texture = null) => FlatInstance(AdditiveShader, color, texture);

        static Material Flat(Shader shader, string kind, Color color, Texture texture)
        {
            string key = kind + "|" + ColorKey(color) + "|" + (texture != null ? texture.GetInstanceID().ToString() : "-");
            if (cache.TryGetValue(key, out var mat) && mat != null)
                return mat;
            mat = FlatInstance(shader, color, texture);
            cache[key] = mat;
            return mat;
        }

        static Material FlatInstance(Shader shader, Color color, Texture texture)
        {
            var mat = new Material(shader) { name = "BluffFlat", hideFlags = HideFlags.DontUnloadUnusedAsset };
            mat.SetColor("_Color", color);
            if (texture != null)
                mat.SetTexture("_MainTex", texture);
            return mat;
        }

        static string ColorKey(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        // ------------------------------------------------------------------ primitives

        /// <summary>Primitif Unity dengan material, tanpa collider (kecuali diminta).</summary>
        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale,
            Material material, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    Object.Destroy(collider);
            }
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = localScale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        const int MeshCacheLimit = 160;

        static bool TryMesh(string key, out Mesh mesh) => meshCache.TryGetValue(key, out mesh) && mesh != null;

        static Mesh StoreMesh(string key, Mesh mesh)
        {
            if (meshCache.Count < MeshCacheLimit)
            {
                mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                meshCache[key] = mesh;
            }
            return mesh;
        }

        static GameObject MeshObject(string name, Transform parent, Vector3 position, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            if (!meshCache.ContainsValue(mesh))
                go.AddComponent<OwnedMesh>().Mesh = mesh;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        internal static void ReleaseOwnedMesh(MeshFilter filter)
        {
            var owner = filter.GetComponent<OwnedMesh>();
            if (owner != null)
            {
                owner.Release();
                Object.Destroy(owner);
            }
        }

        static Mesh FinishMesh(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv,
            List<Vector4> tangents, List<int> indices)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTangents(tangents);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Validate(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("Geometry requires finite values.", name);
        }

        public static GameObject BeveledBox(string name, Transform parent, Vector3 position, Vector3 size, float bevel, Material material)
        {
            Validate(size.x, nameof(size));
            Validate(size.y, nameof(size));
            Validate(size.z, nameof(size));
            Validate(bevel, nameof(bevel));
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f || bevel < 0f)
                throw new ArgumentOutOfRangeException(nameof(size));
            bevel = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.49f);
            string key = "box|" + Number(size.x) + "|" + Number(size.y) + "|" + Number(size.z) + "|" + Number(bevel);
            if (!TryMesh(key, out var mesh))
            {
                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uv = new List<Vector2>();
                var tangents = new List<Vector4>();
                var indices = new List<int>();
                var half = size * 0.5f;
                var inner = half - Vector3.one * bevel;
                var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        var n = axes[axis] * sign;
                        var u = axis == 0 ? Vector3.forward : Vector3.right;
                        var v = Vector3.Cross(n, u);
                        float hu = Vector3.Dot(half, new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)));
                        float hv = Vector3.Dot(half, new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)));
                        var us = bevel > 0f ? new[] { -hu, -hu + bevel, hu - bevel, hu } : new[] { -hu, hu };
                        var vs = bevel > 0f ? new[] { -hv, -hv + bevel, hv - bevel, hv } : new[] { -hv, hv };
                        int start = vertices.Count;
                        for (int y = 0; y < vs.Length; y++)
                        {
                            for (int x = 0; x < us.Length; x++)
                            {
                                var p = n * half[axis] + u * us[x] + v * vs[y];
                                var clamped = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                                var normal = bevel > 0f ? (p - clamped).normalized : n;
                                vertices.Add(bevel > 0f ? clamped + normal * bevel : p);
                                normals.Add(normal);
                                uv.Add(new Vector2(us[x] + hu, vs[y] + hv));
                                var tangent = (u - normal * Vector3.Dot(normal, u)).normalized;
                                tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
                            }
                        }
                        for (int y = 0; y < vs.Length - 1; y++)
                        {
                            for (int x = 0; x < us.Length - 1; x++)
                            {
                                int a = start + y * us.Length + x;
                                int b = a + us.Length;
                                indices.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                            }
                        }
                    }
                }
                mesh = StoreMesh(key, FinishMesh("Bluff Beveled Box", vertices, normals, uv, tangents, indices));
            }
            return MeshObject(name, parent, position, mesh, material);
        }

        public static GameObject Lathe(string name, Transform parent, Vector3 position, Vector2[] profile, Material material, int segments = 64)
        {
            if (profile == null || profile.Length < 2 || profile.Length > 128)
                throw new ArgumentException("A lathe profile needs 2 to 128 points.", nameof(profile));
            segments = Mathf.Clamp(segments, 8, 128);
            var key = new StringBuilder("lathe|").Append(segments);
            for (int i = 0; i < profile.Length; i++)
            {
                Validate(profile[i].x, nameof(profile));
                Validate(profile[i].y, nameof(profile));
                if (profile[i].x < 0f || (i > 0 && (profile[i] - profile[i - 1]).sqrMagnitude < 1e-12f))
                    throw new ArgumentException("Lathe radii must be nonnegative and neighboring points distinct.", nameof(profile));
                key.Append('|').Append(Number(profile[i].x)).Append(',').Append(Number(profile[i].y));
            }
            string cacheKey = key.ToString();
            if (!TryMesh(cacheKey, out var mesh))
                mesh = StoreMesh(cacheKey, BuildLathe(profile, segments));
            return MeshObject(name, parent, position, mesh, material);
        }

        static Mesh BuildLathe(Vector2[] profile, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var tangents = new List<Vector4>();
            var indices = new List<int>();
            int edges = profile.Length - 1;
            var slopes = new Vector2[edges];
            var distances = new float[profile.Length];
            float maxRadius = 0f;
            for (int i = 0; i < edges; i++)
            {
                var delta = profile[i + 1] - profile[i];
                slopes[i] = new Vector2(delta.y, -delta.x).normalized;
                distances[i + 1] = distances[i] + delta.magnitude;
                maxRadius = Mathf.Max(maxRadius, Mathf.Max(profile[i].x, profile[i + 1].x));
            }
            if (maxRadius <= 0f)
                throw new ArgumentException("A lathe must have a nonzero radius.", nameof(profile));
            float circumference = Mathf.Max(1f, Mathf.Round(Mathf.PI * 2f * maxRadius));
            bool closed = (profile[0] - profile[edges]).sqrMagnitude < 1e-12f;
            for (int edge = 0; edge < edges; edge++)
            {
                bool planar = Mathf.Abs(profile[edge + 1].y - profile[edge].y) < 1e-7f;
                int start = vertices.Count;
                for (int end = 0; end < 2; end++)
                {
                    int row = edge + end;
                    var p = profile[row];
                    var slope = slopes[edge];
                    int neighbor = end == 0 ? edge - 1 : edge + 1;
                    if (closed)
                        neighbor = (neighbor + edges) % edges;
                    if (!planar && neighbor >= 0 && neighbor < edges && Mathf.Abs(slopes[neighbor].x) > 1e-6f && Vector2.Dot(slope, slopes[neighbor]) > 0.55f)
                        slope = (slope + slopes[neighbor]).normalized;
                    if (p.x == 0f)
                        slope = new Vector2(0f, Mathf.Sign(slopes[edge].y));
                    for (int i = 0; i <= segments; i++)
                    {
                        float angle = i == segments ? 0f : i / (float)segments * Mathf.PI * 2f;
                        float c = Mathf.Cos(angle);
                        float s = Mathf.Sin(angle);
                        vertices.Add(new Vector3(c * p.x, p.y, s * p.x));
                        normals.Add(new Vector3(c * slope.x, slope.y, s * slope.x));
                        if (planar)
                        {
                            uv.Add(new Vector2(c * p.x, s * p.x));
                            tangents.Add(new Vector4(1f, 0f, 0f, -Mathf.Sign(slope.y)));
                        }
                        else
                        {
                            uv.Add(new Vector2(i / (float)segments * circumference, distances[row]));
                            tangents.Add(new Vector4(-s, 0f, c, -1f));
                        }
                    }
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i;
                    int b = a + segments + 1;
                    if (profile[edge].x > 0f)
                        indices.AddRange(new[] { a, b, a + 1 });
                    if (profile[edge + 1].x > 0f)
                        indices.AddRange(new[] { a + 1, b, b + 1 });
                }
            }
            return FinishMesh("Bluff Lathe", vertices, normals, uv, tangents, indices);
        }

        public static GameObject Tube(string name, Transform parent, Vector3[] points, float radius, Material material, int sides = 8)
        {
            Validate(radius, nameof(radius));
            if (points == null || points.Length < 2 || points.Length > 512 || radius <= 0f)
                throw new ArgumentException("A tube needs 2 to 512 points and a positive radius.", nameof(points));
            sides = Mathf.Clamp(sides, 3, 16);
            var key = new StringBuilder("tube|").Append(Number(radius)).Append('|').Append(sides);
            for (int i = 0; i < points.Length; i++)
            {
                Validate(points[i].x, nameof(points));
                Validate(points[i].y, nameof(points));
                Validate(points[i].z, nameof(points));
                if (i > 0 && (points[i] - points[i - 1]).sqrMagnitude < 1e-12f)
                    throw new ArgumentException("Neighboring tube points must be distinct.", nameof(points));
                key.Append('|').Append(Number(points[i].x)).Append(',').Append(Number(points[i].y)).Append(',').Append(Number(points[i].z));
            }
            string cacheKey = key.ToString();
            if (!TryMesh(cacheKey, out var mesh))
                mesh = StoreMesh(cacheKey, BuildTube(points, radius, sides));
            return MeshObject(name, parent, Vector3.zero, mesh, material);
        }

        static Mesh BuildTube(Vector3[] points, float radius, int sides)
        {
            bool closed = points.Length > 3 && (points[0] - points[points.Length - 1]).sqrMagnitude < 1e-10f;
            int count = closed ? points.Length - 1 : points.Length;
            int rows = closed ? count + 1 : count;
            var forward = new Vector3[rows];
            var frames = new Vector3[rows];
            var distances = new float[rows];
            for (int row = 0; row < rows; row++)
            {
                int current = row % count;
                var incoming = closed || row > 0 ? (points[current] - points[(current + count - 1) % count]).normalized : (points[1] - points[0]).normalized;
                var outgoing = closed || row < count - 1 ? (points[(current + 1) % count] - points[current]).normalized : incoming;
                if ((incoming + outgoing).sqrMagnitude < 1e-6f)
                    throw new ArgumentException("A tube cannot reverse direction at a point.", nameof(points));
                forward[row] = (incoming + outgoing).normalized;
                if (row > 0)
                {
                    distances[row] = distances[row - 1] + Vector3.Distance(points[current], points[(row - 1) % count]);
                    frames[row] = Quaternion.FromToRotation(forward[row - 1], forward[row]) * frames[row - 1];
                }
                else
                {
                    var axis = Mathf.Abs(forward[row].y) < 0.9f ? Vector3.up : Vector3.right;
                    frames[row] = Vector3.Cross(forward[row], axis).normalized;
                }
                frames[row] = (frames[row] - forward[row] * Vector3.Dot(frames[row], forward[row])).normalized;
            }
            if (closed)
            {
                float twist = Vector3.SignedAngle(frames[rows - 1], frames[0], forward[0]);
                for (int row = 1; row < rows; row++)
                    frames[row] = Quaternion.AngleAxis(twist * distances[row] / distances[rows - 1], forward[row]) * frames[row];
                frames[rows - 1] = frames[0];
                forward[rows - 1] = forward[0];
            }
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var tangents = new List<Vector4>();
            var indices = new List<int>();
            for (int row = 0; row < rows; row++)
            {
                var binormal = Vector3.Cross(forward[row], frames[row]);
                for (int side = 0; side <= sides; side++)
                {
                    float angle = side == sides ? 0f : side / (float)sides * Mathf.PI * 2f;
                    var normal = frames[row] * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                    var tangent = -frames[row] * Mathf.Sin(angle) + binormal * Mathf.Cos(angle);
                    vertices.Add(points[row % count] + normal * radius);
                    normals.Add(normal);
                    uv.Add(new Vector2(side / (float)sides * Mathf.PI * 2f * radius, distances[row]));
                    tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
                }
                if (row == 0)
                    continue;
                for (int side = 0; side < sides; side++)
                {
                    int a = (row - 1) * (sides + 1) + side;
                    int b = a + sides + 1;
                    indices.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }
            if (!closed)
            {
                for (int end = 0; end < 2; end++)
                {
                    int row = end == 0 ? 0 : rows - 1;
                    int center = vertices.Count;
                    var normal = forward[row] * (end == 0 ? -1f : 1f);
                    var axis = frames[row];
                    var binormal = Vector3.Cross(forward[row], axis);
                    vertices.Add(points[row]);
                    normals.Add(normal);
                    uv.Add(Vector2.zero);
                    tangents.Add(new Vector4(axis.x, axis.y, axis.z, end == 0 ? -1f : 1f));
                    for (int side = 0; side <= sides; side++)
                    {
                        float angle = side == sides ? 0f : side / (float)sides * Mathf.PI * 2f;
                        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        vertices.Add(points[row] + axis * offset.x + binormal * offset.y);
                        normals.Add(normal);
                        uv.Add(offset);
                        tangents.Add(new Vector4(axis.x, axis.y, axis.z, end == 0 ? -1f : 1f));
                        if (side < sides)
                        {
                            int a = center + side + 1;
                            int b = a + 1;
                            indices.AddRange(end == 0 ? new[] { center, b, a } : new[] { center, a, b });
                        }
                    }
                }
            }
            return FinishMesh("Bluff Tube", vertices, normals, uv, tangents, indices);
        }

        static Mesh smoothCylinder;

        /// <summary>
        /// Silinder halus (64 sisi) berukuran sama dengan primitif Unity (tinggi 2, jari-jari 0.5) untuk benda bundar
        /// yang terlihat dekat (meja, mangkuk).
        /// </summary>
        public static GameObject SmoothCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            if (smoothCylinder == null)
                smoothCylinder = BuildCylinderMesh(64);
            var go = new GameObject(name);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = smoothCylinder;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        static Mesh BuildCylinderMesh(int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Sisi: dua cincin dengan normal ke luar.
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(n * 0.5f + Vector3.up);
                vertices.Add(n * 0.5f + Vector3.down);
                normals.Add(n);
                normals.Add(n);
                uvs.Add(new Vector2(i / (float)segments, 1f));
                uvs.Add(new Vector2(i / (float)segments, 0f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }

            // Tutup atas & bawah.
            for (int cap = 0; cap < 2; cap++)
            {
                float y = cap == 0 ? 1f : -1f;
                var normal = cap == 0 ? Vector3.up : Vector3.down;
                int center = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(normal);
                uvs.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f));
                    normals.Add(normal);
                    uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                }
                for (int i = 0; i < segments; i++)
                {
                    if (cap == 0)
                        triangles.AddRange(new[] { center, center + i + 2, center + i + 1 });
                    else
                        triangles.AddRange(new[] { center, center + i + 1, center + i + 2 });
                }
            }

            var mesh = new Mesh { name = "BluffCylinder" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return mesh;
        }

        /// <summary>Quad datar (menghadap -Z lokal) dengan material, tanpa collider dan tanpa bayangan.</summary>
        public static GameObject Quad(string name, Transform parent, Vector3 localPosition, Vector2 size, Material material)
        {
            var go = Primitive(PrimitiveType.Quad, name, parent, localPosition, new Vector3(size.x, size.y, 1f), material);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        /// <summary>Segmen kapsul dari titik a ke b (dalam ruang lokal parent) dengan jari-jari r.</summary>
        public static GameObject Limb(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material material)
        {
            var dir = b - a;
            float length = dir.magnitude;
            var go = Primitive(PrimitiveType.Capsule, name, parent, (a + b) * 0.5f,
                new Vector3(radius * 2f, Mathf.Max(length * 0.5f, radius), radius * 2f), material);
            if (length > 1e-4f)
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / length);
            return go;
        }

        // ------------------------------------------------------------------ textures

        /// <summary>Titik bulat lembut (untuk partikel asap, cahaya, bayangan).</summary>
        public static Texture2D SoftTexture => softTexture != null ? softTexture : (softTexture = BuildRadial(64, 2f));

        /// <summary>Bintang empat sudut (kilau, percikan).</summary>
        public static Texture2D SparkTexture => sparkTexture != null ? sparkTexture : (sparkTexture = BuildSpark(64));

        /// <summary>Cincin lembut (gelombang kejut, tameng).</summary>
        public static Texture2D RingTexture => ringTexture != null ? ringTexture : (ringTexture = BuildRing(128));

        /// <summary>Garis cahaya horizontal (jejak peluru).</summary>
        public static Texture2D StreakTexture => streakTexture != null ? streakTexture : (streakTexture = BuildStreak(128, 16));

        static Texture2D NewTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
        }

        static Texture2D BuildRadial(int size, float power)
        {
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Pow(t, power) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D BuildSpark(int size)
        {
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f;
                    float dy = Mathf.Abs((y + 0.5f) / size - 0.5f) * 2f;
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                    float rays = Mathf.Clamp01(1f - dx * 6f) * Mathf.Clamp01(1f - dy) + Mathf.Clamp01(1f - dy * 6f) * Mathf.Clamp01(1f - dx);
                    float a = Mathf.Clamp01(core * core + rays * 0.8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D BuildRing(int size)
        {
            var tex = NewTexture(size, size);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) * 8f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Texture2D BuildStreak(int w, int h)
        {
            var tex = NewTexture(w, h);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - v) * Mathf.Clamp01(u * 1.2f) * Mathf.Clamp01((1f - u) * 6f);
                    px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }
    }

    internal sealed class OwnedMesh : MonoBehaviour
    {
        internal Mesh Mesh;

        internal void Release()
        {
            var mesh = Mesh;
            Mesh = null;
            if (mesh != null)
                Destroy(mesh);
        }

        void OnDestroy() => Release();
    }
}
