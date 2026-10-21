using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    [DefaultExecutionOrder(200)]
    public sealed class PlayerHand3D : MonoBehaviour
    {
        const float Radius = 0.46f;
        const float SelectLift = 0.022f;
        const float HoverLift = 0.012f;
        const float HandScale = 0.7f;
        const float TableRadius = 1.06f;
        const float TableClearance = 0.803f;

        static readonly Vector3 RestPosition = new Vector3(0f, -0.127f, 0.43f);
        static readonly Quaternion RestRotation = Quaternion.Euler(-12f, 0f, 0f);
        static readonly Vector3 PlayRestPosition = new Vector3(0.28f, -0.4f, 0.43f);
        static readonly Quaternion PlayRestRotation = Quaternion.Euler(18f, -18f, -18f);

        readonly List<Card3D> cards = new List<Card3D>();
        readonly Dictionary<Card3D, float> highlightUntil = new Dictionary<Card3D, float>();
        readonly List<Card3D> pendingPacket = new List<Card3D>(3);
        Camera viewCamera;
        CameraRig rig;
        Vector3 bodyPosition;
        Quaternion bodyRotation;
        Vector3 outward;
        Bounds layoutBounds;
        Bounds leftBounds;
        Bounds rightBounds;
        GripBounds[] playBounds;
        Vector3 fanPosition;
        float fanScale = HandScale;
        Vector3 playPosition;
        Quaternion playRotation;
        float playOpening;
        int maxSelection = 3;
        bool playingPacket;
        bool inputSuspended;

        struct GripBounds
        {
            public Transform Transform;
            public Bounds Bounds;
        }

        Transform container;
        Transform leftGrip;
        Transform rightGrip;
        Transform playHand;
        Transform playThumb;
        Transform playFingers;
        bool lowered;
        float lowerAmount;
        float shotAmount;
        int playVersion;
        readonly HashSet<Card3D> selected = new HashSet<Card3D>();

        public int MaxSelection
        {
            get => maxSelection;
            set
            {
                maxSelection = Mathf.Clamp(value, 1, 3);
                bool changed = false;
                for (int i = cards.Count - 1; i >= 0 && selected.Count > maxSelection; i--)
                    changed |= selected.Remove(cards[i]);
                if (changed)
                    SelectionChanged?.Invoke();
            }
        }
        public bool Interactable { get; private set; }
        public int Count => cards.Count;
        public int SelectedCount => selected.Count;
        public int VisibleCount => Count;
        public Transform PlayHold => playHand;

        public event System.Action SelectionChanged;
        public event System.Action<string> Warning;

        public static PlayerHand3D Create(Camera camera)
        {
            var go = new GameObject("Player Hand");
            go.transform.SetParent(camera.transform.parent, false);
            var hand = go.AddComponent<PlayerHand3D>();
            hand.viewCamera = camera;
            hand.rig = camera.GetComponent<CameraRig>();
            hand.bodyPosition = camera.transform.position;
            hand.bodyRotation = camera.transform.rotation;
            hand.outward = Vector3.ProjectOnPlane(-camera.transform.forward, Vector3.up).normalized;
            hand.transform.SetPositionAndRotation(hand.bodyPosition, hand.bodyRotation);
            hand.container = new GameObject("Left Fan").transform;
            hand.container.SetParent(go.transform, false);
            hand.fanPosition = RestPosition;
            hand.container.localScale = Vector3.one * HandScale;
            hand.BuildHands();
            hand.RefreshLayout();
            return hand;
        }

        public List<Card> SelectedCards
        {
            get
            {
                var list = new List<Card>();
                foreach (var c in cards)
                {
                    if (selected.Contains(c))
                        list.Add(c.Card);
                }
                return list;
            }
        }

        public List<int> SelectedIds
        {
            get
            {
                var list = new List<int>();
                foreach (var c in cards)
                {
                    if (selected.Contains(c))
                        list.Add(c.Card.Id);
                }
                return list;
            }
        }

        public Vector3 EntryPoint => container.position + container.up * -0.015f;

        public bool OccludesWorldPoint(Camera camera, Vector3 world)
        {
            foreach (var card in cards)
            {
                if (card == null || !card.gameObject.activeInHierarchy) continue;
                var start = card.transform.InverseTransformPoint(camera.transform.position);
                var end = card.transform.InverseTransformPoint(world);
                float depth = end.z - start.z;
                if (Mathf.Abs(depth) < .00001f) continue;
                float t = -start.z / depth;
                if (t <= 0f || t >= 1f) continue;
                var point = Vector3.Lerp(start,end,t);
                if (Mathf.Abs(point.x) < Card3D.Width*.5f && Mathf.Abs(point.y) < Card3D.Height*.5f) return true;
            }
            return false;
        }

        void RefreshLayout()
        {
            bool hasBounds = false;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool entering = !card.gameObject.activeSelf;
                card.gameObject.SetActive(true);
                card.Interactable = Interactable && !playingPacket && !inputSuspended;
                GetCardPose(i, out var position, out var rotation);
                if (entering)
                {
                    card.transform.localPosition = position;
                    card.transform.localRotation = rotation;
                    card.transform.localScale = Vector3.one;
                }
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = Card3D.BoundsCorner(Card3D.GeometryBounds, corner);
                    Encapsulate(ref layoutBounds, ref hasBounds, position + rotation * offset);
                    Encapsulate(ref layoutBounds, ref hasBounds, position + rotation * (offset + Vector3.up * SelectLift));
                    Encapsulate(ref layoutBounds, ref hasBounds, position + rotation *
                        (Vector3.up * (SelectLift + HoverLift)) + Vector3.back * 0.018f + offset * 1.1f);
                }
            }
            if (!hasBounds)
                layoutBounds = new Bounds(Vector3.zero, Vector3.zero);
            UpdateGrips();
            FitLayout();
            ApplyFanPose();
        }

        void GetCardPose(int index, out Vector3 position, out Quaternion rotation)
        {
            int count = Mathf.Max(0, VisibleCount);
            if (count <= 8)
            {
                float spread = 4.6f * Mathf.Max(0, count - 1);
                float fanAngle = count <= 1 ? 0f : spread * (0.5f - index / (float)(count - 1));
                float rad = fanAngle * Mathf.Deg2Rad;
                position = new Vector3(-Mathf.Sin(rad) * Radius, Mathf.Cos(rad) * Radius - Radius,
                    -index * 0.0018f);
                rotation = Quaternion.Euler(0f, 0f, fanAngle);
                return;
            }

            // 9-16 cards use two compact rows; larger hands get a third row.
            int rows = count <= 16 ? 2 : 3;
            int firstRowCount = Mathf.CeilToInt(count / (float)rows);
            int secondRowCount = rows == 2 ? count - firstRowCount : Mathf.CeilToInt((count - firstRowCount) * 0.5f);
            int row;
            int rowStart;
            int rowCount;
            if (index < firstRowCount)
            {
                row = 0;
                rowStart = 0;
                rowCount = firstRowCount;
            }
            else if (index < firstRowCount + secondRowCount)
            {
                row = 1;
                rowStart = firstRowCount;
                rowCount = secondRowCount;
            }
            else
            {
                row = 2;
                rowStart = firstRowCount + secondRowCount;
                rowCount = count - rowStart;
            }
            int rowIndex = index - rowStart;
            float center = (rowCount - 1) * 0.5f;
            float spacing = rows == 2 ? 0.058f : 0.052f;
            float x = (rowIndex - center) * spacing;
            float angle = (center - rowIndex) * (rows == 2 ? 4.2f : 3.6f);
            float y = rows == 2
                ? (row == 0 ? 0.045f : -0.073f)
                : (row == 0 ? 0.088f : row == 1 ? -0.018f : -0.122f);
            y -= Mathf.Abs(x) * 0.025f;
            float z = 0.012f - row * 0.045f;
            z -= rowIndex * 0.0016f;
            position = new Vector3(x, y, z);
            rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void FitLayout()
        {
            if (VisibleCount == 0 || viewCamera == null || (rig != null && (rig.IsLooking || !rig.IsViewCentered)))
                return;
            float width = viewCamera.aspect * 1080f;
            float tangent = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float left = (452f / width * 2f - 1f) * tangent * viewCamera.aspect;
            float rightEdge = width < 1900f ? width - 32f : Mathf.Min(1423f, width - 497f);
            float right = (rightEdge / width * 2f - 1f) * tangent * viewCamera.aspect;
            float bottom = (90f / 1080f * 2f - 1f) * tangent;
            float top = ((width < 1900f ? 340f : 420f) / 1080f * 2f - 1f) * tangent;
            float depth = RestPosition.z;
            var rotation = Quaternion.Inverse(viewCamera.transform.rotation) * bodyRotation * RestRotation;
            fanScale = HandScale;
            for (int i = 0; i < 8; i++)
            {
                var offset = rotation * (Card3D.BoundsCorner(layoutBounds, i) - layoutBounds.center);
                LimitScale(ref fanScale, left * offset.z - offset.x, (right - left) * depth * 0.5f);
                LimitScale(ref fanScale, offset.x - right * offset.z, (right - left) * depth * 0.5f);
                LimitScale(ref fanScale, bottom * offset.z - offset.y, (top - bottom) * depth * 0.5f);
                LimitScale(ref fanScale, offset.y - top * offset.z, (top - bottom) * depth * 0.5f);
            }
            fanScale = Mathf.Max(0.1f, fanScale);
            var cameraCenter = new Vector3((left + right) * 0.5f * depth, (bottom + top) * 0.5f * depth, depth);
            fanPosition = transform.InverseTransformPoint(viewCamera.transform.TransformPoint(cameraCenter))
                - RestRotation * layoutBounds.center * fanScale;
            float minimum = Mathf.Min(FanMinimumHeight(layoutBounds, Vector3.zero, Quaternion.identity),
                Mathf.Min(FanMinimumHeight(leftBounds, leftGrip.localPosition, leftGrip.localRotation),
                    FanMinimumHeight(rightBounds, rightGrip.localPosition, rightGrip.localRotation)));
            float cameraHeight = viewCamera.transform.position.y;
            if (minimum < TableClearance + 0.003f && cameraHeight > TableClearance + 0.003f)
            {
                float fit = (cameraHeight - TableClearance - 0.003f) / (cameraHeight - minimum);
                fanPosition = transform.InverseTransformPoint(Vector3.Lerp(viewCamera.transform.position,
                    transform.TransformPoint(fanPosition), fit));
                fanScale *= fit;
            }
        }

        float FanMinimumHeight(Bounds bounds, Vector3 position, Quaternion rotation)
        {
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < 8; i++)
            {
                var point = position + rotation * Card3D.BoundsCorner(bounds, i);
                minimum = Mathf.Min(minimum, transform.TransformPoint(fanPosition + RestRotation * point * fanScale).y);
            }
            return minimum;
        }

        static void LimitScale(ref float scale, float extent, float available)
        {
            if (extent > 0f)
                scale = Mathf.Min(scale, available / extent);
        }

        static void Encapsulate(ref Bounds bounds, ref bool initialized, Vector3 point)
        {
            if (initialized)
                bounds.Encapsulate(point);
            else
            {
                bounds = new Bounds(point, Vector3.zero);
                initialized = true;
            }
        }

        public void SetInteractable(bool interactable)
        {
            Interactable = interactable;
            foreach (var c in cards)
                c.Interactable = interactable && c.gameObject.activeSelf && !playingPacket && !inputSuspended;
            if (!interactable)
                ClearSelection();
        }

        public void SuspendInput(bool suspended)
        {
            // A temporary camera cut must not discard the player's selected cards.
            inputSuspended = suspended;
            foreach (var card in cards)
                card.Interactable = Interactable && card.gameObject.activeSelf && !playingPacket && !suspended;
        }

        public void ClearSelection()
        {
            if (selected.Count == 0)
                return;
            selected.Clear();
            SelectionChanged?.Invoke();
        }

        public void SetLowered(bool value) => lowered = value;

        public void SetShotPose(float amount)
        {
            if (Time.timeScale <= 0f || Time.deltaTime <= 0f)
                return;
            shotAmount = float.IsNaN(amount) ? 0f : Mathf.Clamp01(amount);
            ApplyFanPose();
        }

        void ApplyFanPose()
        {
            rightGrip.gameObject.SetActive(VisibleCount >= 6 && !playingPacket && shotAmount < 0.01f);
            container.localScale = Vector3.one * fanScale;
            container.localPosition = fanPosition + new Vector3(-0.07f, -0.04f, -0.025f) * shotAmount;
            container.localRotation = RestRotation * Quaternion.Euler(8f * shotAmount, -20f * shotAmount, 18f * shotAmount);
            bool initialized = false;
            var bounds = new Bounds();
            if (VisibleCount > 0)
            {
                AddBounds(ref bounds, ref initialized, leftGrip.localToWorldMatrix, leftBounds);
                AddBounds(ref bounds, ref initialized, rightGrip.localToWorldMatrix, rightBounds);
            }
            for (int i = 0; i < VisibleCount; i++)
                AddBounds(ref bounds, ref initialized, cards[i].transform.localToWorldMatrix, Card3D.GeometryBounds);
            if (!initialized)
                return;
            float nearest = Vector3.Dot(bounds.center, outward) - Vector3.Dot(bounds.extents,
                new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z)));
            float retract = Mathf.Max(0f, TableRadius + 0.06f - nearest);
            var offset = outward * (retract * Mathf.SmoothStep(0f, 1f, lowerAmount * 2f))
                + Vector3.down * (0.24f * Mathf.SmoothStep(0f, 1f, lowerAmount * 2f - 1f));
            bounds.center += offset;
            container.position += offset + ClearanceOffset(bounds);
        }

        static void AddBounds(ref Bounds bounds, ref bool initialized, Matrix4x4 matrix, Bounds local)
        {
            for (int i = 0; i < 8; i++)
                Encapsulate(ref bounds, ref initialized, matrix.MultiplyPoint3x4(Card3D.BoundsCorner(local, i)));
        }

        static Vector3 ClearanceOffset(Bounds bounds)
        {
            float x = Mathf.Max(0f, Mathf.Abs(bounds.center.x) - bounds.extents.x);
            float z = Mathf.Max(0f, Mathf.Abs(bounds.center.z) - bounds.extents.z);
            float distance = Mathf.Sqrt(x * x + z * z);
            float weight = 1f - Mathf.SmoothStep(0f, 1f, (distance - TableRadius) / 0.06f);
            return Vector3.up * (Mathf.Max(0f, TableClearance - bounds.min.y) * weight);
        }

        public void PreparePlayPacket(IEnumerable<Card> packet)
        {
            if (playingPacket)
                return;
            pendingPacket.Clear();
            if (packet == null)
                return;
            foreach (var value in packet)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    if (card.Card.Id != value.Id || pendingPacket.Contains(card))
                        continue;
                    if (!card.gameObject.activeSelf)
                    {
                        GetCardPose(i, out var position, out var rotation);
                        card.transform.localPosition = position + rotation * Vector3.up * SelectLift;
                        card.transform.localRotation = rotation;
                        card.transform.localScale = Vector3.one;
                    }
                    pendingPacket.Add(card);
                    break;
                }
            }
        }

        public IEnumerator PlayPacket(System.Action onGrip, System.Action onRelease, float speed)
        {
            if (!isActiveAndEnabled || playHand == null)
            {
                pendingPacket.Clear();
                yield break;
            }
            if (playingPacket)
                yield break;
            playingPacket = true;
            foreach (var card in cards)
                card.Interactable = false;
            int version = ++playVersion;
            float rate = speed > 0f && !float.IsInfinity(speed) ? speed : 1f;
            try
            {
                while (IsCurrentPlay(version) && (Time.timeScale <= 0f || Time.deltaTime <= 0f))
                    yield return null;
                if (!IsCurrentPlay(version))
                    yield break;

                bool wasSupporting = rightGrip.gameObject.activeSelf;
                playHand.localScale = Vector3.one * (wasSupporting ? fanScale : HandScale);
                ApplyPlayPose(PlayRestPosition, PlayRestRotation, 1f);
                if (wasSupporting)
                {
                    GetSupportPose(out var supportPosition, out var supportRotation);
                    ApplyPlayPose(supportPosition, supportRotation, 0f);
                }
                rightGrip.gameObject.SetActive(false);
                playHand.gameObject.SetActive(true);
                float opening = wasSupporting ? 0f : 1f;
                for (int phase = 0; phase < 5; phase++)
                {
                    if (!IsCurrentPlay(version))
                        yield break;
                    var startPosition = playPosition;
                    var startRotation = playRotation;
                    float startScale = playHand.localScale.x;
                    float startOpening = opening;
                    var endPosition = startPosition;
                    var endRotation = startRotation;
                    float endOpening = 0f;
                    float duration;
                    float arc = 0f;
                    switch (phase)
                    {
                        case 0:
                            duration = 0.24f;
                            arc = 0.015f;
                            break;
                        case 1:
                            duration = 0.1f;
                            break;
                        case 2:
                            duration = 0.32f;
                            arc = 0.035f;
                            endPosition = new Vector3(0.12f, -0.13f, 0.82f);
                            endRotation = Quaternion.Euler(-68f, -8f, -12f);
                            break;
                        case 3:
                            duration = 0.12f;
                            endPosition = new Vector3(0.17f, -0.13f, 0.88f);
                            endRotation = Quaternion.Euler(-78f, -4f, -18f);
                            endOpening = 1f;
                            break;
                        default:
                            duration = 0.28f;
                            GetSupportPose(out endPosition, out endRotation);
                            endOpening = 0f;
                            break;
                    }

                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        yield return null;
                        if (!IsCurrentPlay(version))
                            yield break;
                        if (Time.timeScale <= 0f || Time.deltaTime <= 0f)
                            continue;
                        elapsed = Mathf.Min(duration, elapsed + Time.deltaTime * rate);
                        if (phase == 0)
                            GetPacketGripPose(out endPosition, out endRotation);
                        else if (phase == 4)
                            GetSupportPose(out endPosition, out endRotation);
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                        float endScale = phase == 4 && VisibleCount >= 6 ? fanScale : phase >= 2 ? HandScale : startScale;
                        playHand.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
                        opening = Mathf.Lerp(startOpening, endOpening, t);
                        var position = Vector3.Lerp(startPosition, endPosition, t);
                        if (elapsed < duration)
                            position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                        ApplyPlayPose(position, Quaternion.Slerp(startRotation, endRotation, t), opening);
                    }

                    if (phase == 0)
                    {
                        onGrip?.Invoke();
                        ApplyPlayPose(playPosition, playRotation, opening);
                    }
                    else if (phase == 2)
                    {
                        opening = 0.45f;
                        ApplyPlayPose(endPosition, endRotation, opening);
                        onRelease?.Invoke();
                    }
                    if (!IsCurrentPlay(version))
                        yield break;
                }
            }
            finally
            {
                if (this != null && version == playVersion)
                {
                    playVersion++;
                    playingPacket = false;
                    pendingPacket.Clear();
                    HidePlayHand();
                    UpdateGrips();
                    foreach (var card in cards)
                        card.Interactable = Interactable && card.gameObject.activeSelf && !inputSuspended;
                }
            }
        }

        bool IsCurrentPlay(int version) => this != null && isActiveAndEnabled && playHand != null && version == playVersion;

        void GetSupportPose(out Vector3 position, out Quaternion rotation)
        {
            position = VisibleCount >= 6
                ? transform.InverseTransformPoint(rightGrip.position - rightGrip.rotation *
                    (Vector3.down * Card3D.Height * 0.5f * fanScale)) : PlayRestPosition;
            rotation = VisibleCount >= 6 ? Quaternion.Inverse(transform.rotation) * rightGrip.rotation : PlayRestRotation;
        }

        void GetPacketGripPose(out Vector3 position, out Quaternion rotation)
        {
            var center = Vector3.zero;
            int count = 0;
            foreach (var card in pendingPacket)
            {
                if (card == null)
                    continue;
                center += card.transform.position;
                count++;
            }
            position = transform.InverseTransformPoint(count > 0 ? center / count : container.position);
            rotation = Quaternion.Inverse(transform.rotation) * container.rotation;
        }

        void ApplyPlayPose(Vector3 position, Quaternion rotation, float opening)
        {
            playPosition = position;
            playRotation = rotation;
            playOpening = opening;
            transform.SetPositionAndRotation(bodyPosition, bodyRotation);
            playHand.localPosition = position;
            playHand.localRotation = rotation;
            playThumb.localRotation = Quaternion.Euler(-18f * opening, -14f * opening, 8f * opening);
            playFingers.localRotation = Quaternion.Euler(16f * opening, 0f, -5f * opening);
            bool initialized = false;
            var bounds = new Bounds();
            foreach (var part in playBounds)
                AddBounds(ref bounds, ref initialized, part.Transform.localToWorldMatrix, part.Bounds);
            foreach (var card in pendingPacket)
                if (card != null && card.transform.IsChildOf(playHand))
                    AddBounds(ref bounds, ref initialized, card.transform.localToWorldMatrix, Card3D.GeometryBounds);
            playHand.position += ClearanceOffset(bounds);
        }

        void HidePlayHand()
        {
            if (playHand == null)
                return;
            playHand.gameObject.SetActive(false);
            ApplyPlayPose(PlayRestPosition, PlayRestRotation, 1f);
        }

        void OnDisable()
        {
            playVersion++;
            playingPacket = false;
            pendingPacket.Clear();
            HidePlayHand();
            if (rightGrip != null)
                UpdateGrips();
            foreach (var card in cards)
                card.Interactable = Interactable && card.gameObject.activeSelf && !inputSuspended;
        }

        void BuildHands()
        {
            leftGrip = BuildGrip(container, "Left Grip", -1f, out _, out _);
            rightGrip = BuildGrip(container, "Right Grip", 1f, out _, out _);
            bool initialized = false;
            foreach (var filter in leftGrip.GetComponentsInChildren<MeshFilter>())
                AddBounds(ref leftBounds, ref initialized, leftGrip.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                    filter.sharedMesh.bounds);
            initialized = false;
            foreach (var filter in rightGrip.GetComponentsInChildren<MeshFilter>())
                AddBounds(ref rightBounds, ref initialized, rightGrip.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                    filter.sharedMesh.bounds);
            UpdateGrips();
            playHand = new GameObject("Play Hold").transform;
            playHand.SetParent(transform, false);
            playHand.localScale = Vector3.one * HandScale;
            var pinch = BuildGrip(playHand, "Right Pinch", 1f, out playThumb, out playFingers);
            playBounds = new GripBounds[3];
            playBounds[0].Transform = pinch;
            playBounds[1].Transform = playThumb;
            playBounds[2].Transform = playFingers;
            var initializedParts = new bool[3];
            foreach (var filter in pinch.GetComponentsInChildren<MeshFilter>())
            {
                int part = filter.transform.IsChildOf(playThumb) ? 1 : filter.transform.IsChildOf(playFingers) ? 2 : 0;
                AddBounds(ref playBounds[part].Bounds, ref initializedParts[part],
                    playBounds[part].Transform.worldToLocalMatrix * filter.transform.localToWorldMatrix, filter.sharedMesh.bounds);
            }
            HidePlayHand();
        }

        void UpdateGrips()
        {
            bool visible = VisibleCount > 0;
            int bottomCount = VisibleCount <= 8 ? VisibleCount : VisibleCount <= 16 ? VisibleCount / 2 : VisibleCount / 3;
            int start = VisibleCount - bottomCount;
            int leftIndex = start + Mathf.Max(0, (bottomCount - 1) / 3);
            int rightIndex = start + Mathf.Max(0, (bottomCount - 1) * 2 / 3);
            GetCardPose(leftIndex, out var left, out var leftRotation);
            GetCardPose(rightIndex, out var right, out var rightRotation);
            leftGrip.localPosition = left + leftRotation * (Vector3.down * Card3D.Height * 0.5f);
            rightGrip.localPosition = right + rightRotation * (Vector3.down * Card3D.Height * 0.5f);
            leftGrip.localRotation = leftRotation;
            rightGrip.localRotation = rightRotation;
            leftGrip.gameObject.SetActive(visible);
            rightGrip.gameObject.SetActive(VisibleCount >= 6 && !playingPacket && shotAmount < 0.01f);
        }

        static Transform BuildGrip(Transform parent, string name, float side, out Transform thumb, out Transform fingers)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, -Card3D.Height * 0.5f, 0f);
            var skin = Art.Lit(Cosmetics.LocalStyle.Skin, 0.26f);
            var nails = Art.Lit(Theme.Hex("C49D80"), 0.32f);
            var sleeve = Art.Fabric(Color.Lerp(Cosmetics.LocalStyle.Shirt, Theme.Hex("18202A"), .48f));
            var cuff = Art.Fabric(Theme.Hex("AAA797"));
            var palm = Art.Lathe("Palm", root, new Vector3(side * 0.026f, -0.067f, 0.018f), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.018f, 0.003f), new Vector2(0.03f, 0.021f),
                new Vector2(0.033f, 0.047f), new Vector2(0.026f, 0.063f), new Vector2(0f, 0.068f),
            }, skin, 24);
            palm.transform.localScale = new Vector3(1f, 1f, 0.5f);
            palm.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
            var thenar = Art.Lathe("Thumb Pad", root, new Vector3(side * 0.008f, -0.065f, 0.018f), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.012f, 0.007f), new Vector2(0.016f, 0.023f),
                new Vector2(0.011f, 0.04f), new Vector2(0f, 0.047f),
            }, skin, 20);
            thenar.transform.localScale = new Vector3(0.8f, 1f, 0.75f);
            thenar.transform.localRotation = Quaternion.Euler(0f, 0f, side * 20f);

            thumb = new GameObject("Thumb Pivot").transform;
            thumb.SetParent(root, false);
            thumb.localPosition = new Vector3(side * 0.008f, -0.044f, 0.014f);
            BuildFinger(thumb, "Thumb", new[]
            {
                Vector3.zero, new Vector3(-side * 0.012f, 0.012f, -0.036f),
                new Vector3(-side * 0.02f, 0.047f, -0.034f), new Vector3(-side * 0.024f, 0.055f, -0.031f),
            }, 0.009f, Vector3.back, skin, nails);
            fingers = new GameObject("Finger Pivot").transform;
            fingers.SetParent(root, false);
            fingers.localPosition = new Vector3(0f, -0.044f, 0.034f);
            for (int i = 0; i < 4; i++)
            {
                float x = side * (0.013f + i * 0.012f);
                float length = i == 0 ? 0.048f : i == 1 ? 0.052f : i == 2 ? 0.044f : 0.034f;
                float radius = i == 3 ? 0.0062f : 0.0075f;
                BuildFinger(fingers, i == 0 ? "Index" : i == 1 ? "Middle" : i == 2 ? "Ring" : "Little", new[]
                {
                    new Vector3(x, 0f, 0f), new Vector3(x + side * 0.003f, length * 0.58f, 0.009f),
                    new Vector3(x, length, 0.002f), new Vector3(x - side * 0.002f, length - 0.001f, -0.009f),
                }, radius, Vector3.forward, skin, nails);
            }

            var wristPosition = new Vector3(side * 0.033f, -0.053f, 0.015f);
            var wristRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(side * 0.065f, -0.12f, -0.065f).normalized);
            var wrist = Art.Lathe("Wrist", root, wristPosition, new[]
            {
                new Vector2(0f, 0f), new Vector2(0.019f, 0f), new Vector2(0.021f, 0.027f),
                new Vector2(0.023f, 0.05f), new Vector2(0f, 0.05f),
            }, skin, 24);
            wrist.transform.localRotation = wristRotation;
            var shirtCuff = Art.Lathe("Shirt Cuff", root, wristPosition, new[]
            {
                new Vector2(0.02f, 0.031f), new Vector2(0.025f, 0.031f), new Vector2(0.026f, 0.052f),
                new Vector2(0.022f, 0.052f), new Vector2(0.02f, 0.031f),
            }, cuff, 24);
            shirtCuff.transform.localRotation = wristRotation;
            var forearm = Art.Lathe("Sleeve", root, wristPosition, new[]
            {
                new Vector2(0.023f, 0.047f), new Vector2(0.028f, 0.047f), new Vector2(0.032f, 0.083f),
                new Vector2(0.039f, 0.18f), new Vector2(0f, 0.18f),
            }, sleeve, 24);
            forearm.transform.localRotation = wristRotation;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        static void BuildFinger(Transform parent, string name, Vector3[] joints, float radius, Vector3 nailNormal, Material skin, Material nails)
        {
            Art.Tube(name, parent, joints, radius, skin, 12);
            var tip = joints[joints.Length - 1];
            var direction = (tip - joints[joints.Length - 2]).normalized;
            var pad = Art.Lathe(name + " Tip", parent, tip, new[]
            {
                new Vector2(radius, 0f), new Vector2(radius * 0.86f, radius * 0.45f),
                new Vector2(radius * 0.5f, radius * 0.78f), new Vector2(0f, radius * 0.9f),
            }, skin, 12);
            pad.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            var nail = Art.BeveledBox(name + " Nail", parent, tip - direction * radius * 0.55f + nailNormal * radius * 0.88f,
                new Vector3(radius * 1.12f, radius * 1.35f, 0.0012f), 0.0005f, nails);
            nail.transform.localRotation = Quaternion.LookRotation(nailNormal, direction);
        }

        public void AddCards(IEnumerable<Card> list, Vector3 fromWorld, bool highlight)
        {
            foreach (var card in list)
            {
                var view = Card3D.Create(container, "HandCard", true);
                view.SetCard(card);
                view.SetHandLighting(true);
                view.SetCastShadows(false);
                view.transform.position = fromWorld;
                view.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f));
                view.gameObject.SetActive(false);
                view.Interactable = Interactable && !playingPacket && !inputSuspended;
                view.Clicked = OnCardClicked;
                if (highlight)
                    highlightUntil[view] = Time.time + 2.5f;
                cards.Add(view);
            }
            Sort();
            RefreshLayout();
        }

        public List<Card3D> TakeCards(IEnumerable<Card> list)
        {
            var taken = new List<Card3D>();
            bool selectionChanged = false;
            foreach (var card in list)
            {
                int index = cards.FindIndex(v => v.Card.Id == card.Id);
                if (index < 0)
                    continue;
                var view = cards[index];
                cards.RemoveAt(index);
                selectionChanged |= selected.Remove(view);
                highlightUntil.Remove(view);
                view.gameObject.SetActive(true);
                view.Interactable = false;
                view.SetGlow(Color.clear, 0f);
                view.transform.SetParent(null, true);
                taken.Add(view);
            }
            RefreshLayout();
            if (selectionChanged)
                SelectionChanged?.Invoke();
            return taken;
        }

        void OnCardClicked(Card3D view)
        {
            if (!Interactable || inputSuspended || playingPacket || view == null || !view.gameObject.activeSelf || !cards.Contains(view) ||
                Time.timeScale <= 0f || GameTutorial.IsShowing || InputCompat.RightMouseHeld() || InputCompat.ModalIsShowing())
                return;
            if (selected.Contains(view))
            {
                selected.Remove(view);
            }
            else
            {
                if (selected.Count >= MaxSelection)
                {
                    Warning?.Invoke("Maximum " + MaxSelection + " cards.");
                    return;
                }
                selected.Add(view);
            }
            Sfx.Play(SfxType.Card, 0.5f);
            SelectionChanged?.Invoke();
        }

        void Sort()
        {
            cards.Sort((a, b) =>
            {
                int byElement = ((int)a.Card.Element).CompareTo((int)b.Card.Element);
                return byElement != 0 ? byElement : a.Card.Id.CompareTo(b.Card.Id);
            });
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (Time.timeScale <= 0f || dt <= 0f)
                return;
            transform.SetPositionAndRotation(bodyPosition, bodyRotation);
            lowerAmount = Mathf.MoveTowards(lowerAmount, lowered ? 1f : 0f, dt * 3f);
            FitLayout();
            float k = 1f - Mathf.Exp(-14f * dt);
            float now = Time.time;
            bool canInspect = !inputSuspended && !playingPacket && !lowered && shotAmount <= 0f && !InputCompat.RightMouseHeld() &&
                !GameTutorial.IsShowing && !InputCompat.ModalIsShowing();
            for (int i = 0; i < VisibleCount; i++)
            {
                var card = cards[i];
                GetCardPose(i, out var basePosition, out var baseRotation);
                bool isSelected = selected.Contains(card);
                bool inspect = card.Hovered && canInspect;
                float lift = (isSelected ? SelectLift : 0f) + (inspect ? HoverLift : 0f);
                var pos = basePosition + baseRotation * new Vector3(0f, lift, 0f);
                if (inspect)
                    pos.z -= 0.018f;
                card.transform.localPosition = Vector3.Lerp(card.transform.localPosition, pos, k);
                card.transform.localRotation = Quaternion.Slerp(card.transform.localRotation, inspect ? Quaternion.identity : baseRotation, k);
                card.transform.localScale = Vector3.Lerp(card.transform.localScale, Vector3.one * (inspect ? 1.06f : 1f), k);

                bool highlighted = highlightUntil.TryGetValue(card, out float until) && until > now;
                if (isSelected)
                    card.SetGlow(Theme.Gold, 0.9f);
                else if (inspect)
                    card.SetGlow(Theme.Text, 0.55f);
                else if (highlighted)
                    card.SetGlow(Theme.Muted, 0.5f);
                else
                    card.SetGlow(Theme.Muted, 0f);
            }
            ApplyFanPose();
            if (playHand != null && playHand.gameObject.activeSelf)
                ApplyPlayPose(playPosition, playRotation, playOpening);
        }
    }
}
