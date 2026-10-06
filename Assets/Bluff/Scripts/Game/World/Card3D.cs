using System.Collections;
using Bluff.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Bluff.Game
{
    /// <summary>
    /// Kartu 3D: muka (menghadap -Z lokal) dan punggung (menghadap +Z). Lebar di sumbu X, tinggi di sumbu Y.
    /// Bisa dipilih dengan mouse lewat PhysicsRaycaster di kamera.
    /// </summary>
    public sealed class Card3D : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float Width = 0.1f;
        public const float Height = 0.14f;
        public const float Thickness = 0.0015f;

        static Mesh edgeMesh;

        MeshRenderer frontRenderer;
        MeshRenderer backRenderer;
        MeshRenderer edgeRenderer;
        MeshRenderer glowRenderer;
        Material glowMaterial;
        Texture2D frontTexture;
        Texture2D backTexture;
        float frontSmoothness = 0.22f;
        float backSmoothness = 0.22f;
        bool handLighting;
        BoxCollider box;
        float glowAlpha;
        float glowTarget;
        Color glowColor = Theme.Gold;

        public Card Card { get; private set; }
        public bool HasCard { get; private set; }
        public PowerType Power { get; private set; }
        public bool Hovered { get; private set; }
        public bool Interactable { get; set; }
        public System.Action<Card3D> Clicked;
        public System.Action<Card3D, bool> HoverChanged;

        public static Card3D Create(Transform parent, string name = "Card3D", bool clickable = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var card = go.AddComponent<Card3D>();
            card.Build(clickable);
            return card;
        }

        void Build(bool clickable)
        {
            frontTexture = backTexture = CardArt.Back;
            var front = Art.Quad("Front", transform, new Vector3(0f, 0f, -Thickness * 0.5f), new Vector2(Width, Height), Art.Lit(Color.white, frontSmoothness, 0f, frontTexture));
            frontRenderer = front.GetComponent<MeshRenderer>();
            frontRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            frontRenderer.receiveShadows = true;

            var back = Art.Quad("Back", transform, new Vector3(0f, 0f, Thickness * 0.5f), new Vector2(Width, Height), Art.Lit(Color.white, backSmoothness, 0f, backTexture));
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            backRenderer = back.GetComponent<MeshRenderer>();
            backRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            backRenderer.receiveShadows = true;

            if (edgeMesh == null)
                edgeMesh = BuildEdgeMesh();
            var edge = new GameObject("Edge");
            edge.transform.SetParent(transform, false);
            edge.AddComponent<MeshFilter>().sharedMesh = edgeMesh;
            edgeRenderer = edge.AddComponent<MeshRenderer>();
            edgeRenderer.sharedMaterial = Art.Lit(Color.Lerp(Theme.CardFace, Theme.Gold, 0.18f), 0.16f);
            edgeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            edgeRenderer.receiveShadows = true;

            glowMaterial = Art.AdditiveInstance(new Color(1f, 1f, 1f, 0f), Art.SoftTexture);
            var glow = Art.Quad("Glow", transform, new Vector3(0f, 0f, 0.004f), new Vector2(Width * 1.9f, Height * 1.6f), glowMaterial);
            glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.enabled = false;

            if (clickable)
            {
                box = gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(Width, Height, 0.01f);
            }
        }

        static Mesh BuildEdgeMesh()
        {
            const int cornerSegments = 16;
            const int perimeterCount = (cornerSegments + 1) * 4;
            var vertices = new Vector3[(perimeterCount + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var tangents = new Vector4[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[perimeterCount * 6];
            float scaleX = Width / CardArt.DesignWidth;
            float scaleY = Height / CardArt.DesignHeight;
            float centerX = CardArt.DesignWidth * 0.5f - CardArt.CornerRadius - CardArt.EdgeInset;
            float centerY = CardArt.DesignHeight * 0.5f - CardArt.CornerRadius - CardArt.EdgeInset;

            for (int i = 0; i <= perimeterCount; i++)
            {
                int point = i % perimeterCount;
                int corner = point / (cornerSegments + 1);
                int step = point % (cornerSegments + 1);
                float angle = (corner + step / (float)cornerSegments) * Mathf.PI * 0.5f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);
                float signX = corner == 0 || corner == 3 ? 1f : -1f;
                float signY = corner < 2 ? 1f : -1f;
                var position = new Vector3((signX * centerX + x * CardArt.CornerRadius) * scaleX,
                    (signY * centerY + y * CardArt.CornerRadius) * scaleY, -Thickness * 0.5f);
                var normal = new Vector3(x / scaleX, y / scaleY, 0f).normalized;
                int a = i * 2;
                vertices[a] = position;
                vertices[a + 1] = position + Vector3.forward * Thickness;
                normals[a] = normals[a + 1] = normal;
                tangents[a] = tangents[a + 1] = new Vector4(-normal.y, normal.x, 0f, 1f);
                uv[a] = new Vector2(i / (float)perimeterCount, 0f);
                uv[a + 1] = new Vector2(i / (float)perimeterCount, 1f);
                if (i == perimeterCount)
                    continue;
                int t = i * 6;
                triangles[t] = a;
                triangles[t + 1] = a + 2;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 2;
                triangles[t + 4] = a + 3;
                triangles[t + 5] = a + 1;
            }

            var mesh = new Mesh
            {
                name = "Bluff Card Edge",
                hideFlags = HideFlags.DontUnloadUnusedAsset,
                vertices = vertices,
                normals = normals,
                tangents = tangents,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        public void SetCard(Card card)
        {
            Card = card;
            HasCard = true;
            Power = PowerType.None;
            SetFaces(CardArt.Face(card.Element), CardArt.Back, card.IsWild ? 0.26f : 0.22f);
        }

        public void SetHiddenCard()
        {
            HasCard = false;
            Power = PowerType.None;
            SetFaces(CardArt.Back, CardArt.Back, 0.22f);
        }

        /// <summary>Kartu Kuasa. power None = kuasa rahasia (muka disembunyikan, hanya punggung kuasa).</summary>
        public void SetPower(PowerType power)
        {
            HasCard = false;
            Power = power;
            var face = power == PowerType.None ? CardArt.PowerBack : CardArt.Power(power);
            SetFaces(face, CardArt.PowerBack, 0.24f);
        }

        public void SetHandLighting(bool enabled)
        {
            handLighting = enabled;
            ApplyFaces();
        }

        void SetFaces(Texture2D front, Texture2D back, float smoothness)
        {
            frontTexture = front;
            backTexture = back;
            frontSmoothness = smoothness;
            backSmoothness = 0.22f;
            ApplyFaces();
        }

        void ApplyFaces()
        {
            frontRenderer.sharedMaterial = handLighting
                ? Art.Unlit(Color.white, frontTexture)
                : Art.Lit(Color.white, frontSmoothness, 0f, frontTexture);
            backRenderer.sharedMaterial = handLighting
                ? Art.Unlit(Color.white, backTexture)
                : Art.Lit(Color.white, backSmoothness, 0f, backTexture);
            frontRenderer.receiveShadows = !handLighting;
            backRenderer.receiveShadows = !handLighting;
        }

        /// <summary>Sorot kartu dengan cahaya lembut di belakangnya (alpha 0 = mati).</summary>
        public void SetGlow(Color color, float strength)
        {
            glowColor = color;
            glowTarget = strength;
        }

        public void SetCastShadows(bool cast)
        {
            var mode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            frontRenderer.shadowCastingMode = mode;
            backRenderer.shadowCastingMode = mode;
            edgeRenderer.shadowCastingMode = mode;
        }

        /// <summary>Balik kartu di tempat: putar 180° pada sumbu lokal Y.</summary>
        public IEnumerator Flip(float duration)
        {
            var start = transform.localRotation;
            var end = start * Quaternion.Euler(0f, 180f, 0f);
            var basePos = transform.localPosition;
            yield return Tween.Run(duration, t =>
            {
                if (this == null)
                    return;
                float k = Tween.EaseInOutCubic(t);
                transform.localRotation = Quaternion.Slerp(start, end, k);
                transform.localPosition = basePos + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.04f;
            });
        }

        /// <summary>Terbang ke posisi/rotasi dunia dengan lengkungan (busur) dan sedikit putaran.</summary>
        public IEnumerator FlyTo(Vector3 worldPos, Quaternion worldRot, float duration, float arc = 0.12f, float spin = 0f)
        {
            var startPos = transform.position;
            var startRot = transform.rotation;
            var startScale = transform.localScale;
            yield return Tween.Run(duration, t =>
            {
                if (this == null)
                    return;
                float k = Tween.EaseInOutCubic(t);
                var pos = Vector3.Lerp(startPos, worldPos, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                transform.position = pos;
                transform.rotation = Quaternion.Slerp(startRot, worldRot, k) * Quaternion.Euler(0f, 0f, spin * Mathf.Sin(k * Mathf.PI));
                transform.localScale = startScale;
            });
        }

        static bool PointerInputBlocked => InputCompat.RightMouseHeld() || GameTutorial.IsShowing || InputCompat.ModalIsShowing();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || PointerInputBlocked)
                return;
            if (Interactable)
                Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left || PointerInputBlocked)
                return;
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        void SetHovered(bool hovered)
        {
            if (Hovered == hovered)
                return;
            Hovered = hovered;
            HoverChanged?.Invoke(this, hovered);
        }

        void OnDisable()
        {
            SetHovered(false);
        }

        void Update()
        {
            if (Hovered && PointerInputBlocked)
                SetHovered(false);
            if (Mathf.Abs(glowAlpha - glowTarget) > 0.001f)
                glowAlpha = Mathf.MoveTowards(glowAlpha, glowTarget, Time.unscaledDeltaTime * 4f);
            bool show = glowAlpha > 0.01f;
            if (glowRenderer.enabled != show)
                glowRenderer.enabled = show;
            if (show)
            {
                float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 6f) * 0.15f;
                glowMaterial.SetColor("_Color", glowColor.WithAlpha(glowAlpha * pulse));
            }
        }

        void OnDestroy()
        {
            if (glowMaterial != null)
                Destroy(glowMaterial);
        }
    }
}
