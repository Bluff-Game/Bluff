using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Kartu di tangan pemain (sudut pandang orang pertama): kipas kartu di bawah layar yang menempel ke kamera.
    /// Klik kartu untuk memilih (maks. 3). Kartu baru meluncur masuk dan menyala sebentar.
    /// </summary>
    public sealed class PlayerHand3D : MonoBehaviour
    {
        const float Radius = 0.46f;
        const float SelectLift = 0.05f;
        const float HoverLift = 0.022f;
        const float HandScale = 0.8f;

        static readonly Vector3 RestPosition = new Vector3(0f, -0.172f, 0.55f);
        static readonly Quaternion RestRotation = Quaternion.Euler(-12f, 0f, 0f);
        static readonly Vector3 PlayRestPosition = new Vector3(0.28f, -0.4f, 0.43f);
        static readonly Quaternion PlayRestRotation = Quaternion.Euler(18f, -18f, -18f);

        readonly List<Card3D> cards = new List<Card3D>();
        readonly Dictionary<Card3D, float> highlightUntil = new Dictionary<Card3D, float>();
        Transform container;
        Transform leftGrip;
        Transform playHand;
        Transform playThumb;
        Transform playFingers;
        bool lowered;
        float lowerAmount;
        float shotAmount;
        int playVersion;
        readonly HashSet<Card3D> selected = new HashSet<Card3D>();

        public int MaxSelection { get; set; } = 3;
        public bool Interactable { get; private set; }
        public int Count => cards.Count;
        public int SelectedCount => selected.Count;
        public Transform PlayHold => playHand;

        public event System.Action SelectionChanged;
        public event System.Action<string> Warning;

        public static PlayerHand3D Create(Camera camera)
        {
            var go = new GameObject("Player Hand");
            go.transform.SetParent(camera.transform, false);
            var hand = go.AddComponent<PlayerHand3D>();
            hand.container = new GameObject("Left Fan").transform;
            hand.container.SetParent(go.transform, false);
            hand.container.localScale = Vector3.one * HandScale;
            hand.ApplyFanPose();
            hand.BuildHands();
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

        /// <summary>Titik dunia tempat kartu yang dibagikan "masuk" ke tangan.</summary>
        public Vector3 EntryPoint => container.position + container.up * -0.05f;

        public void SetInteractable(bool interactable)
        {
            Interactable = interactable;
            foreach (var c in cards)
                c.Interactable = interactable;
            if (!interactable)
                ClearSelection();
        }

        public void ClearSelection()
        {
            if (selected.Count == 0)
                return;
            selected.Clear();
            SelectionChanged?.Invoke();
        }

        /// <summary>Turunkan tangan (mis. saat kartu dibuka) supaya meja terlihat jelas.</summary>
        public void SetLowered(bool value) => lowered = value;

        public void SetShotPose(float amount)
        {
            shotAmount = float.IsNaN(amount) ? 0f : Mathf.Clamp01(amount);
            if (Time.timeScale > 0f && Time.deltaTime > 0f)
                ApplyFanPose();
        }

        void ApplyFanPose()
        {
            container.localPosition = RestPosition
                + RestRotation * new Vector3(0f, -0.16f * HandScale * lowerAmount, 0f)
                + new Vector3(-0.115f, -0.09f, -0.025f) * shotAmount;
            container.localRotation = RestRotation * Quaternion.Euler(8f * shotAmount, -20f * shotAmount, 18f * shotAmount);
        }

        public IEnumerator PlayPacket(System.Action onGrip, System.Action onRelease, float speed)
        {
            if (!isActiveAndEnabled || playHand == null)
                yield break;
            int version = ++playVersion;
            float rate = speed > 0f && !float.IsInfinity(speed) ? speed : 1f;
            try
            {
                while (IsCurrentPlay(version) && (Time.timeScale <= 0f || Time.deltaTime <= 0f))
                    yield return null;
                if (!IsCurrentPlay(version))
                    yield break;

                ApplyPlayPose(PlayRestPosition, PlayRestRotation, 1f);
                playHand.gameObject.SetActive(true);
                float opening = 1f;
                for (int phase = 0; phase < 5; phase++)
                {
                    if (!IsCurrentPlay(version))
                        yield break;
                    var startPosition = playHand.localPosition;
                    var startRotation = playHand.localRotation;
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
                            endPosition = new Vector3(0.12f, -0.24f, 0.86f);
                            endRotation = Quaternion.Euler(-68f, -8f, -12f);
                            break;
                        case 3:
                            duration = 0.12f;
                            endPosition = new Vector3(0.17f, -0.27f, 0.95f);
                            endRotation = Quaternion.Euler(-78f, -4f, -18f);
                            endOpening = 1f;
                            break;
                        default:
                            duration = 0.28f;
                            endPosition = PlayRestPosition;
                            endRotation = PlayRestRotation;
                            endOpening = 1f;
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
                        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                        opening = Mathf.Lerp(startOpening, endOpening, t);
                        var position = Vector3.Lerp(startPosition, endPosition, t);
                        if (elapsed < duration)
                            position += Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
                        ApplyPlayPose(position, Quaternion.Slerp(startRotation, endRotation, t), opening);
                    }

                    if (phase == 0)
                        onGrip?.Invoke();
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
                    HidePlayHand();
                }
            }
        }

        bool IsCurrentPlay(int version) => this != null && isActiveAndEnabled && playHand != null && version == playVersion;

        void GetPacketGripPose(out Vector3 position, out Quaternion rotation)
        {
            var center = Vector3.zero;
            int count = 0;
            foreach (var card in cards)
            {
                if (!selected.Contains(card))
                    continue;
                center += card.transform.position;
                count++;
            }
            position = transform.InverseTransformPoint(count > 0 ? center / count : container.position);
            rotation = Quaternion.Inverse(transform.rotation) * container.rotation;
        }

        void ApplyPlayPose(Vector3 position, Quaternion rotation, float opening)
        {
            playHand.localPosition = position;
            playHand.localRotation = rotation;
            playThumb.localRotation = Quaternion.Euler(-18f * opening, -14f * opening, 8f * opening);
            playFingers.localRotation = Quaternion.Euler(16f * opening, 0f, -5f * opening);
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
            HidePlayHand();
        }

        void BuildHands()
        {
            leftGrip = BuildGrip(container, "Left Grip", -1f, out _, out _);
            UpdateLeftGrip();
            playHand = new GameObject("Play Hold").transform;
            playHand.SetParent(transform, false);
            playHand.localScale = Vector3.one * HandScale;
            BuildGrip(playHand, "Right Pinch", 1f, out playThumb, out playFingers);
            HidePlayHand();
        }

        void UpdateLeftGrip()
        {
            leftGrip.localPosition = new Vector3(0f, -Card3D.Height * 0.5f, -Mathf.Max(0, cards.Count - 1) * 0.0009f);
            leftGrip.gameObject.SetActive(cards.Count > 0);
        }

        static Transform BuildGrip(Transform parent, string name, float side, out Transform thumb, out Transform fingers)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(0f, -Card3D.Height * 0.5f, 0f);
            var skin = Art.Lit(Theme.Hex("B88968"), 0.26f);
            var nails = Art.Lit(Theme.Hex("C49D80"), 0.32f);
            var sleeve = Art.Fabric(Theme.Hex("202A23"));
            var cuff = Art.Fabric(Theme.Hex("AAA797"));
            var palm = Art.Lathe("Palm", root, new Vector3(side * 0.026f, -0.067f, 0.018f), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.018f, 0.003f), new Vector2(0.03f, 0.021f),
                new Vector2(0.033f, 0.047f), new Vector2(0.026f, 0.063f), new Vector2(0f, 0.068f),
            }, skin, 24);
            palm.transform.localScale = new Vector3(1f, 1f, 0.5f);
            palm.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
            var thenar = Art.Lathe("Thumb Pad", root, new Vector3(side * 0.008f, -0.053f, 0.002f), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.012f, 0.007f), new Vector2(0.016f, 0.023f),
                new Vector2(0.011f, 0.04f), new Vector2(0f, 0.047f),
            }, skin, 20);
            thenar.transform.localScale = new Vector3(0.8f, 1f, 0.75f);
            thenar.transform.localRotation = Quaternion.Euler(0f, 0f, side * 20f);

            thumb = new GameObject("Thumb Pivot").transform;
            thumb.SetParent(root, false);
            thumb.localPosition = new Vector3(side * 0.008f, -0.032f, -0.004f);
            BuildFinger(thumb, "Thumb", new[]
            {
                Vector3.zero, new Vector3(-side * 0.012f, 0.022f, -0.014f),
                new Vector3(-side * 0.02f, 0.037f, -0.014f), new Vector3(-side * 0.024f, 0.041f, -0.009f),
            }, 0.009f, Vector3.back, skin, nails);
            fingers = new GameObject("Finger Pivot").transform;
            fingers.SetParent(root, false);
            fingers.localPosition = new Vector3(0f, -0.04f, 0.018f);
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
                view.Interactable = Interactable;
                view.Clicked = OnCardClicked;
                if (highlight)
                    highlightUntil[view] = Time.time + 2.5f;
                cards.Add(view);
            }
            Sort();
            UpdateLeftGrip();
        }

        /// <summary>Lepas kartu dari tangan (dikembalikan ke pemanggil untuk diterbangkan, lalu dihancurkan).</summary>
        public List<Card3D> TakeCards(IEnumerable<Card> list)
        {
            var taken = new List<Card3D>();
            foreach (var card in list)
            {
                int index = cards.FindIndex(v => v.Card.Id == card.Id);
                if (index < 0)
                    continue;
                var view = cards[index];
                cards.RemoveAt(index);
                selected.Remove(view);
                highlightUntil.Remove(view);
                view.Interactable = false;
                view.SetGlow(Color.clear, 0f);
                view.transform.SetParent(null, true);
                taken.Add(view);
            }
            UpdateLeftGrip();
            return taken;
        }

        void OnCardClicked(Card3D view)
        {
            if (!Interactable || Time.timeScale <= 0f || GameTutorial.IsShowing || InputCompat.RightMouseHeld() || InputCompat.ModalIsShowing())
                return;
            if (selected.Contains(view))
            {
                selected.Remove(view);
            }
            else
            {
                if (selected.Count >= MaxSelection)
                {
                    Warning?.Invoke("Maksimal " + MaxSelection + " kartu.");
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

        void Update()
        {
            float dt = Time.deltaTime;
            if (Time.timeScale <= 0f || dt <= 0f)
                return;
            lowerAmount = Mathf.MoveTowards(lowerAmount, lowered ? 1f : 0f, dt * 3f);
            ApplyFanPose();
            int n = cards.Count;
            float spread = Mathf.Min(52f, 4.6f * Mathf.Max(0, n - 1));
            float halfSpread = spread * 0.5f * Mathf.Deg2Rad;
            float edgeStep = n > 1 ? Radius * (Mathf.Sin(halfSpread) - Mathf.Sin(halfSpread - spread * Mathf.Deg2Rad / (n - 1))) : 0f;
            float extraSpacing = n > 1 ? Mathf.Max(0f, Card3D.Width * 0.26f - edgeStep) : 0f;
            float k = 1f - Mathf.Exp(-14f * dt);
            float now = Time.time;
            for (int i = 0; i < n; i++)
            {
                var card = cards[i];
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                float angle = Mathf.Lerp(spread * 0.5f, -spread * 0.5f, t);
                float rad = angle * Mathf.Deg2Rad;
                var rot = Quaternion.Euler(0f, 0f, angle);
                var pos = new Vector3(-Mathf.Sin(rad) * Radius + (i - (n - 1) * 0.5f) * extraSpacing,
                    Mathf.Cos(rad) * Radius - Radius, -i * 0.0018f);
                bool isSelected = selected.Contains(card);
                bool inspect = card.Hovered && Interactable && !InputCompat.RightMouseHeld() && !GameTutorial.IsShowing;
                float lift = (isSelected ? SelectLift : 0f) + (inspect ? HoverLift : 0f);
                pos += rot * new Vector3(0f, lift, 0f);
                if (inspect) pos.z -= 0.055f;
                card.transform.localPosition = Vector3.Lerp(card.transform.localPosition, pos, k);
                card.transform.localRotation = Quaternion.Slerp(card.transform.localRotation, inspect ? Quaternion.identity : rot, k);
                card.transform.localScale = Vector3.Lerp(card.transform.localScale, Vector3.one * (inspect ? 1.14f : 1f), k);

                bool highlighted = highlightUntil.TryGetValue(card, out float until) && until > now;
                if (isSelected)
                    card.SetGlow(Theme.Gold, 0.9f);
                else if (highlighted)
                    card.SetGlow(new Color(0.35f, 0.85f, 1f), 0.8f);
                else
                    card.SetGlow(Theme.Gold, 0f);
            }
        }
    }
}
