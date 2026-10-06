using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bluff.Game
{
    public sealed class AvatarPortrait : MonoBehaviour
    {
        sealed class Entry
        {
            public Texture Texture;
            public int Users;
        }

        static readonly Dictionary<(int scene, int player), Entry> cache = new Dictionary<(int, int), Entry>();
        (int scene, int player) key;
        Entry entry;
        RawImage image;

        public static RawImage Create(Transform parent, int playerId)
        {
            var rect = UIKit.CreateRect("AvatarPortrait", parent);
            var portrait = rect.gameObject.AddComponent<AvatarPortrait>();
            portrait.image = rect.gameObject.AddComponent<RawImage>();
            portrait.image.raycastTarget = false;
            portrait.key = (parent.gameObject.scene.handle, Mathf.Max(0, playerId));
            if (!cache.TryGetValue(portrait.key, out portrait.entry))
            {
                portrait.entry = new Entry { Texture = Capture(parent.gameObject.scene, portrait.key.player) };
                cache.Add(portrait.key, portrait.entry);
            }
            portrait.entry.Users++;
            portrait.image.texture = portrait.entry.Texture;
            rect.Stretch();
            return portrait.image;
        }

        static Texture Capture(Scene ownerScene, int playerId)
        {
            var style = HumanoidStyle.ForPlayer(playerId);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32) || GraphicsSettings.currentRenderPipeline != null)
                return DrawFallback(style);
            int layer = FindUnusedLayer();
            if (layer < 0) return DrawFallback(style);
            GameObject preview = null;
            Camera camera = null;
            RenderTexture texture = null;
            var previousTarget = RenderTexture.active;
            var randomState = Random.state;
            var cameras = new Dictionary<Camera, int>();
            var lights = new Dictionary<Light, int>();
            try
            {
                int mask = 1 << layer;
                foreach (var existing in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    cameras[existing] = existing.cullingMask;
                    existing.cullingMask &= ~mask;
                }
                foreach (var existing in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    lights[existing] = existing.cullingMask;
                    existing.cullingMask &= ~mask;
                }
                preview = new GameObject("Avatar Snapshot");
                if (ownerScene.IsValid() && ownerScene.isLoaded) SceneManager.MoveGameObjectToScene(preview, ownerScene);
                preview.hideFlags = HideFlags.DontSave;
                preview.transform.position = new Vector3(12000f, -12000f, 12000f);
                var character = Humanoid.Create(preview.transform, "Portrait Character", style, true);
                character.enabled = false;
                foreach (var child in preview.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
                foreach (var collider in preview.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var ambient = new SphericalHarmonicsL2();
                ambient.AddAmbientLight(new Color(0.34f, 0.34f, 0.34f));
                var properties = new MaterialPropertyBlock();
                properties.CopySHCoefficientArraysFrom(new[] { ambient });
                foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.lightProbeUsage = LightProbeUsage.CustomProvided;
                    renderer.SetPropertyBlock(properties);
                }
                var focus = character.Head.position + new Vector3(0f, 0.115f, 0.02f);
                var cameraObject = new GameObject("Portrait Camera", typeof(Camera));
                cameraObject.transform.SetParent(preview.transform, false);
                camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 0.35f;
                camera.aspect = 1f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 4f;
                camera.cullingMask = mask;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Theme.PanelLight;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                camera.renderingPath = RenderingPath.Forward;
                camera.transform.position = focus + new Vector3(0.12f, 0.02f, 1.5f);
                camera.transform.LookAt(focus);
                AddLight(preview.transform, focus + new Vector3(-0.65f, 0.6f, 0.95f), mask, 2.2f, new Color(1f, 0.93f, 0.82f));
                AddLight(preview.transform, focus + new Vector3(0.6f, 0.16f, 0.7f), mask, 1.2f, new Color(0.84f, 0.91f, 1f));
                texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Avatar " + playerId,
                    hideFlags = HideFlags.DontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                };
                if (!texture.Create()) throw new System.InvalidOperationException("Portrait render target unavailable.");
                camera.targetTexture = texture;
                camera.Render();
                return texture;
            }
            catch (System.Exception)
            {
                if (camera != null) camera.targetTexture = null;
                RenderTexture.active = previousTarget;
                if (texture != null)
                {
                    texture.Release();
                    Destroy(texture);
                }
                return DrawFallback(style);
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                RenderTexture.active = previousTarget;
                Random.state = randomState;
                if (preview != null)
                {
                    preview.SetActive(false);
                    Destroy(preview);
                }
                foreach (var pair in cameras) if (pair.Key != null) pair.Key.cullingMask = pair.Value;
                foreach (var pair in lights) if (pair.Key != null) pair.Key.cullingMask = pair.Value;
            }
        }

        static int FindUnusedLayer()
        {
            int used = 0;
            foreach (var item in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                used |= 1 << item.gameObject.layer;
            for (int layer = 30; layer >= 8; layer--)
                if ((used & (1 << layer)) == 0 && string.IsNullOrEmpty(LayerMask.LayerToName(layer))) return layer;
            return -1;
        }

        static void AddLight(Transform parent, Vector3 position, int mask, float intensity, Color color)
        {
            var go = new GameObject("Portrait Light", typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.cullingMask = mask;
            light.range = 3.5f;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
        }

        static Texture2D DrawFallback(HumanoidStyle style)
        {
            const int size = 128;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    float v = y / (float)(size - 1);
                    Color color = Theme.PanelLight;
                    if (Ellipse(u, v, 0.5f, 0.05f, 0.42f, 0.27f)) color = style.Shirt;
                    if (Mathf.Abs(u - 0.5f) < 0.09f && v > 0.18f && v < 0.38f) color = style.Skin;
                    if (Ellipse(u, v, 0.5f, 0.52f, 0.23f, 0.29f)) color = style.Skin;
                    if (Ellipse(u, v, 0.5f, 0.75f, 0.24f, 0.12f)) color = style.Hair;
                    if (Ellipse(u, v, 0.4f, 0.57f, 0.14f, 0.085f) || Ellipse(u, v, 0.6f, 0.57f, 0.14f, 0.085f)) color = style.Mask;
                    if (Ellipse(u, v, 0.4f, 0.57f, 0.052f, 0.033f) || Ellipse(u, v, 0.6f, 0.57f, 0.052f, 0.033f)) color = Theme.Background;
                    if (Mathf.Abs(u - 0.5f) < 0.066f && Mathf.Abs(v - 0.37f) < 0.008f) color = Theme.Background;
                    bool brim = v > 0.74f && v < 0.78f && u > 0.2f && u < 0.8f;
                    switch (style.HatStyle)
                    {
                        case HatStyle.TopHat:
                            if (brim || (v > 0.77f && v < 0.97f && u > 0.32f && u < 0.68f)) color = style.Hat;
                            break;
                        case HatStyle.Beret:
                            if (Ellipse(u, v, 0.54f, 0.8f, 0.28f, 0.12f)) color = style.Hat;
                            break;
                        case HatStyle.Cap:
                            if (brim || Ellipse(u, v, 0.5f, 0.81f, 0.24f, 0.12f)) color = style.Hat;
                            break;
                        case HatStyle.Crown:
                            if (u > 0.29f && u < 0.71f && v > 0.74f && v < 0.82f + 0.07f * Mathf.Abs(Mathf.Cos(u * 32f))) color = Theme.Gold;
                            break;
                        case HatStyle.Bandana:
                            if (v > 0.7f && v < 0.77f && u > 0.27f && u < 0.73f) color = style.Hat;
                            break;
                    }
                    pixels[y * size + x] = color;
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Masked Avatar",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx;
            float dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        void OnDestroy()
        {
            if (entry == null) return;
            if (image != null) image.texture = null;
            entry.Users--;
            if (entry.Users > 0) return;
            cache.Remove(key);
            if (entry.Texture is RenderTexture renderTexture) renderTexture.Release();
            if (entry.Texture != null) Destroy(entry.Texture);
            entry = null;
        }
    }
}
