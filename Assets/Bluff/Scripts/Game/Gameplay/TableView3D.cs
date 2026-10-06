using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using UnityEngine;

namespace Bluff.Game
{
    [DefaultExecutionOrder(250)]
    public sealed class TableView3D : MonoBehaviour
    {
        TableScene scene;
        PlayerHand3D hand;
        Transform handFan;
        Hud hud;
        CameraRig rig;
        GameSnapshot snapshot;
        PowerType[] knownPowers;
        int humanId;
        string[] names;
        bool presenting;
        bool resolutionFocus;
        int presentationVersion;
        System.Exception taskError;
        readonly List<PresentationTask> tasks = new List<PresentationTask>();
        readonly HashSet<Card3D> transientCards = new HashSet<Card3D>();

        sealed class PresentationTask
        {
            public Coroutine Coroutine;
            public readonly Stack<IEnumerator> Stack = new Stack<IEnumerator>();
            public bool Done;
        }
        readonly List<Card3D> pile = new List<Card3D>();
        readonly List<Card3D> lastPlay = new List<Card3D>();
        readonly List<Card3D> revealed = new List<Card3D>();
        bool stormPending;
        bool roundElementShown;
        Transform fpGun;
        Transform fpMuzzle;
        Transform fpIndexFinger;
        Transform fpThumb;
        bool fpShotActive;
        bool fpFired;
        bool fpCylinderAdvanced;
        int fpTarget = -1;
        int fpCylinderIndex;
        int fpCylinderStart;
        float fpShotTime;
        float fpShotSpeed;
        System.Action<Vector3, Vector3> fpOnFire;
        System.Action pendingShotFire;

        const float DrawTime = 0.4f;
        const float CockTime = 0.58f;
        const float FireTime = 0.74f;
        const float KickTime = 0.82f;
        const float RecoverTime = 1.07f;
        const float ShotEndTime = 1.32f;
        static readonly Vector3 GunRest = new Vector3(0.24f, -0.42f, 0.5f);
        static readonly Vector3 GunAim = new Vector3(0.27f, -0.19f, 0.5f);

        static float Speed => GameSettings.Speed > 0f && !float.IsInfinity(GameSettings.Speed) ? GameSettings.Speed : 1f;

        public Hud Hud => hud;
        public TableScene Scene => scene;
        public PlayerHand3D Hand => hand;

        public static TableView3D Create(Transform worldParent, RectTransform uiRoot, Canvas canvas, GameSnapshot snapshot, int humanId)
        {
            var go = new GameObject("Table View 3D");
            go.transform.SetParent(worldParent, false);
            var view = go.AddComponent<TableView3D>();
            view.Build(uiRoot, canvas, snapshot, humanId);
            return view;
        }

        void Build(RectTransform uiRoot, Canvas canvas, GameSnapshot state, int human)
        {
            if (state == null)
                throw new System.ArgumentNullException(nameof(state));
            snapshot = state;
            humanId = human;
            names = new string[state.PlayerCount];
            knownPowers = new PowerType[state.PlayerCount];
            for (int i = 0; i < names.Length; i++)
                names[i] = state.Names != null && i < state.Names.Length ? state.Names[i] : "Pemain " + (i + 1);

            scene = TableScene.Create(transform, state.PlayerCount, human, names);
            var cam = scene.Camera;
            rig = CameraRig.Attach(cam, new Vector3(0f, 1.26f, -1.38f), new Vector3(0f, 0.72f, 0.3f), 54f);
            if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
                cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            hand = PlayerHand3D.Create(cam);
            handFan = hand.transform.Find("Left Fan");
            hand.MaxSelection = Mathf.Max(1, state.Config?.MaxCardsPerPlay ?? 3);
            hud = Hud.Create(uiRoot, canvas, state, human, scene, hand);
            BuildFirstPersonGun(cam.transform);
        }

        public void SetSnapshot(GameSnapshot state)
        {
            if (presenting)
                throw new System.InvalidOperationException("Complete the event batch before applying its snapshot.");
            snapshot = state ?? throw new System.ArgumentNullException(nameof(state));
            hud.SetSnapshot(snapshot);
        }

        void BuildFirstPersonGun(Transform cam)
        {
            fpGun = new GameObject("FP Revolver").transform;
            fpGun.SetParent(cam, false);
            fpGun.localPosition = new Vector3(0.24f, -0.42f, 0.5f);
            fpMuzzle = RevolverModel.Build(fpGun);
            BuildFirstPersonHand();
            fpGun.gameObject.SetActive(false);
        }

        void BuildFirstPersonHand()
        {
            var skin = Art.Lit(Theme.Hex("B88968"), 0.26f);
            var nails = Art.Lit(Theme.Hex("C49D80"), 0.32f);
            var sleeve = Art.Fabric(Theme.Hex("202A23"));
            var cuff = Art.Fabric(Theme.Hex("AAA797"));
            var palm = Art.Lathe("Palm", fpGun, new Vector3(0.025f, -0.078f, -0.052f), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.019f, 0.002f), new Vector2(0.026f, 0.017f),
                new Vector2(0.03f, 0.049f), new Vector2(0.024f, 0.067f), new Vector2(0.011f, 0.077f),
                new Vector2(0f, 0.079f),
            }, skin, 24);
            palm.transform.localScale = new Vector3(0.48f, 1f, 0.82f);
            var heel = Art.Lathe("Thenar", fpGun, new Vector3(0.013f, -0.064f, -0.061f), new[]
            {
                new Vector2(0f, 0f), new Vector2(0.013f, 0.008f), new Vector2(0.017f, 0.021f),
                new Vector2(0.011f, 0.04f), new Vector2(0f, 0.048f),
            }, skin, 20);
            heel.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            heel.transform.localScale = new Vector3(0.8f, 1f, 0.65f);

            for (int i = 0; i < 3; i++)
            {
                float y = -0.031f - i * 0.017f;
                float z = -0.025f - i * 0.005f;
                float radius = 0.0084f - i * 0.0008f;
                BuildGripFinger(i == 0 ? "MiddleFinger" : i == 1 ? "RingFinger" : "LittleFinger", new[]
                {
                    new Vector3(0.03f, y, -0.05f - i * 0.004f),
                    new Vector3(0.038f, y - 0.002f, z - 0.017f),
                    new Vector3(0.02f, y - 0.004f, z),
                    new Vector3(-0.009f, y - 0.003f, z),
                    new Vector3(-0.024f, y + 0.001f, z - 0.015f),
                }, radius, Vector3.left, skin, nails);
            }
            fpIndexFinger = BuildGripFinger("IndexFinger", new[]
            {
                new Vector3(0.027f, -0.006f, -0.042f), new Vector3(0.035f, -0.011f, -0.019f),
                new Vector3(0.03f, -0.021f, 0.004f), new Vector3(0.012f, -0.023f, 0.012f),
                new Vector3(0.003f, -0.021f, 0.004f),
            }, 0.0075f, Vector3.up, skin, nails);
            fpThumb = BuildGripFinger("Thumb", new[]
            {
                new Vector3(0.004f, -0.034f, -0.065f), new Vector3(-0.018f, -0.014f, -0.064f),
                new Vector3(-0.029f, -0.007f, -0.045f), new Vector3(-0.027f, -0.008f, -0.025f),
            }, 0.0095f, Vector3.left, skin, nails);

            var wristPosition = new Vector3(0.022f, -0.067f, -0.071f);
            var wristRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(0.016f, -0.075f, -0.135f).normalized);
            var wrist = Art.Lathe("Wrist", fpGun, wristPosition, new[]
            {
                new Vector2(0f, 0f), new Vector2(0.018f, 0f), new Vector2(0.021f, 0.024f),
                new Vector2(0.023f, 0.043f), new Vector2(0f, 0.043f),
            }, skin, 24);
            wrist.transform.localRotation = wristRotation;
            var shirtCuff = Art.Lathe("ShirtCuff", fpGun, wristPosition, new[]
            {
                new Vector2(0.019f, 0.025f), new Vector2(0.024f, 0.025f), new Vector2(0.025f, 0.041f),
                new Vector2(0.021f, 0.041f), new Vector2(0.019f, 0.025f),
            }, cuff, 24);
            shirtCuff.transform.localRotation = wristRotation;
            var forearm = Art.Lathe("Sleeve", fpGun, wristPosition, new[]
            {
                new Vector2(0.022f, 0.037f), new Vector2(0.027f, 0.037f), new Vector2(0.03f, 0.066f),
                new Vector2(0.036f, 0.17f), new Vector2(0f, 0.17f),
            }, sleeve, 24);
            forearm.transform.localRotation = wristRotation;
        }

        Transform BuildGripFinger(string name, Vector3[] joints, float radius, Vector3 nailNormal, Material skin, Material nails)
        {
            var pivot = new GameObject(name + " Pivot").transform;
            pivot.SetParent(fpGun, false);
            pivot.localPosition = joints[0];
            var points = new Vector3[joints.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = joints[i] - joints[0];
            Art.Tube(name, pivot, points, radius, skin, 12);
            var tip = points[points.Length - 1];
            var direction = (tip - points[points.Length - 2]).normalized;
            var pad = Art.Lathe(name + "Tip", pivot, tip, new[]
            {
                new Vector2(radius, 0f), new Vector2(radius * 0.86f, radius * 0.45f),
                new Vector2(radius * 0.5f, radius * 0.78f), new Vector2(0f, radius * 0.9f),
            }, skin, 12);
            pad.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            var nail = Art.BeveledBox(name + "Nail", pivot, tip - direction * radius * 0.55f + nailNormal * radius * 0.88f,
                new Vector3(radius * 1.12f, radius * 1.35f, 0.0012f), 0.0005f, nails);
            nail.transform.localRotation = Quaternion.LookRotation(nailNormal, direction);
            return pivot;
        }

        // ================================================================== input diteruskan dari HUD

        public void SetInputBlocked(bool blocked)
        {
            if (rig != null)
                rig.SetInputBlocked(blocked);
        }

        public void BeginHumanTurn(GameSnapshot s) => hud.BeginHumanTurn(s);

        public void EndHumanTurn() => hud.EndHumanTurn();

        public void ShowShootWindow(bool humanCan, bool humanCanPeek, int targetId)
        {
            hud.ShowShootWindow(humanCan, humanCanPeek, targetId);
            foreach (var c in scene.Characters)
                c.LookAt(scene.PileAnchor.position);
        }

        public void UpdateShootWindow(float fraction) => hud.UpdateShootWindow(fraction);

        public void HideShootWindow() => hud.HideShootWindow();

        public void Toast(string message, Color color) => hud.Toast(message, color);

        // ================================================================== presentasi event

        PresentationTask presentationTask;

        PresentationTask StartTask(IEnumerator routine)
        {
            var task = new PresentationTask();
            task.Stack.Push(routine);
            tasks.Add(task);
            task.Coroutine = StartCoroutine(RunTask(task));
            return task;
        }

        IEnumerator RunTask(PresentationTask task)
        {
            try
            {
                while (!task.Done && taskError == null && isActiveAndEnabled && task.Stack.Count > 0)
                {
                    var current = task.Stack.Peek();
                    bool next;
                    try
                    {
                        next = current.MoveNext();
                    }
                    catch (System.Exception exception)
                    {
                        taskError = exception;
                        break;
                    }
                    if (task.Done)
                        break;
                    if (!next)
                    {
                        task.Stack.Pop();
                        (current as System.IDisposable)?.Dispose();
                    }
                    else if (current.Current is IEnumerator nested)
                        task.Stack.Push(nested);
                    else
                        yield return current.Current;
                }
            }
            finally
            {
                task.Done = true;
                DisposeTask(task);
            }
        }

        static void DisposeTask(PresentationTask task)
        {
            while (task.Stack.Count > 0)
                (task.Stack.Pop() as System.IDisposable)?.Dispose();
        }

        void CancelTask(PresentationTask task)
        {
            if (task == null || task.Done)
                return;
            task.Done = true;
            if (task.Coroutine != null)
                StopCoroutine(task.Coroutine);
            DisposeTask(task);
        }

        void CancelTasks()
        {
            CancelTask(presentationTask);
            foreach (var task in tasks)
                CancelTask(task);
            tasks.Clear();
            presentationTask = null;
        }

        IEnumerator WaitTasks(IEnumerable<PresentationTask> pending = null)
        {
            while (true)
            {
                if (taskError != null)
                    throw new System.InvalidOperationException("Presentation task failed.", taskError);
                bool running = false;
                foreach (var task in pending ?? tasks)
                    running |= !task.Done;
                if (!running)
                    yield break;
                yield return null;
            }
        }

        public IEnumerator Present(GameEvent e)
        {
            if (e == null || !isActiveAndEnabled)
                yield break;
            if (presenting)
                throw new System.InvalidOperationException("Present events sequentially.");
            presenting = true;
            taskError = null;
            int version = ++presentationVersion;
            bool complete = false;
            presentationTask = new PresentationTask();
            presentationTask.Stack.Push(PresentEvent(e.RedactedFor(humanId)));
            try
            {
                presentationTask.Coroutine = StartCoroutine(RunTask(presentationTask));
                while (version == presentationVersion && !presentationTask.Done)
                    yield return null;
                if (version != presentationVersion || !isActiveAndEnabled)
                    yield break;
                if (taskError != null)
                    throw new System.InvalidOperationException("Could not present " + e.Type, taskError);
                complete = true;
            }
            finally
            {
                if (version == presentationVersion)
                {
                    CancelTasks();
                    presenting = false;
                    if (!complete)
                        ResetPresentation();
                }
            }
        }

        IEnumerator PresentEvent(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.GameStarted:
                    yield return PresentDeal(e);
                    break;
                case GameEventType.HandDealt:
                    if (e.PlayerId == humanId && e.Cards != null)
                        ReceiveCards(e.Cards, false);
                    break;
                case GameEventType.PowerDealt:
                    PresentPowerDealt(e);
                    break;
                case GameEventType.RoundStarted:
                    PresentRoundStarted(e);
                    yield return Wait(0.2f);
                    break;
                case GameEventType.TurnStarted:
                    PresentTurn(e);
                    break;
                case GameEventType.CardsPlayed:
                    yield return PresentPlay(e);
                    break;
                case GameEventType.Passed:
                    yield return PresentPass(e);
                    break;
                case GameEventType.ShotFired:
                    yield return PresentShot(e);
                    break;
                case GameEventType.CardsRevealed:
                    yield return PresentReveal(e);
                    break;
                case GameEventType.BulletRefunded:
                    Fx.Sparkle(scene.ShellPosition(e.PlayerId, Mathf.Max(0, e.Bullets[e.PlayerId] - 1)) + Vector3.up * 0.04f, Theme.Gold, 18, 0.03f);
                    hud.Log("Tembakan jitu! Peluru " + Name(e.PlayerId) + " kembali.");
                    break;
                case GameEventType.PileTaken:
                    yield return PresentPileTaken(e);
                    break;
                case GameEventType.PileBurned:
                    yield return PresentBurn(e);
                    break;
                case GameEventType.PowerUsed:
                    yield return PresentPower(e);
                    break;
                case GameEventType.PowerPeek:
                    if (e.Cards != null)
                        yield return PresentPeek(e);
                    break;
                case GameEventType.GameOver:
                    yield return PresentGameOver(e);
                    break;
            }
            yield return WaitTasks();
            ApplyPublicState(e);
            if (e.Type == GameEventType.PileTaken || e.Type == GameEventType.PileBurned || e.Type == GameEventType.GameOver ||
                (e.Type == GameEventType.CardsPlayed && !resolutionFocus))
                ReleaseFocus();
        }

        void ApplyPublicState(GameEvent e)
        {
            bool pendingPacket = e.Type == GameEventType.PowerUsed && e.Power == PowerType.Tameng;
            for (int id = 0; id < names.Length; id++)
            {
                bool hasCount = e.HandCounts != null && id < e.HandCounts.Length;
                bool hasBullets = e.Bullets != null && id < e.Bullets.Length;
                bool spent = e.PowerSpent != null && id < e.PowerSpent.Length && e.PowerSpent[id];
                if (hasBullets)
                    scene.SetBullets(id, e.Bullets[id]);
                if (id == humanId)
                {
                    if (hasCount && hasBullets)
                        hud.SetPlayerStatus(e.HandCounts[id], e.Bullets[id]);
                    hud.SetMyPower(knownPowers[id], spent);
                    continue;
                }
                if (hasCount && !pendingPacket)
                    scene.SetFanCount(id, e.HandCounts[id]);
                var label = hud.Label(id);
                if (hasCount)
                    label?.SetCount(e.HandCounts[id]);
                if (hasBullets)
                    label?.SetBullets(e.Bullets[id]);
                label?.SetPower(knownPowers[id], spent);
            }
            hud.SetPileCount(e.PileCount);
            if (!pendingPacket && e.Type != GameEventType.GameStarted && e.Type != GameEventType.HandDealt &&
                e.HandCounts != null && humanId >= 0 && humanId < e.HandCounts.Length && hand.Count != e.HandCounts[humanId])
                Debug.LogWarning("[Bluff] Tampilan tangan (" + hand.Count + ") tidak sama dengan event (" + e.HandCounts[humanId] + ") setelah " + e);
        }

        // ------------------------------------------------------------------ bagi kartu & kuasa

        IEnumerator PresentDeal(GameEvent e)
        {
            hud.Log("Kartu dibagikan: " + e.Count + " kartu per pemain.");
            for (int id = 0; id < names.Length; id++)
                scene.SetFanCount(id, 0);
            float stagger = 0.02f / Speed;
            float duration = 0.32f / Speed;
            var counts = new int[names.Length];
            for (int r = 0; r < e.Count; r++)
            {
                for (int p = 0; p < names.Length; p++)
                {
                    int id = p;
                    counts[id]++;
                    int count = counts[id];
                    StartTask(DealOne(() => HandDestination(id), () => HandRotation(id), () => HandScale(id),
                        duration, () => scene.SetFanCount(id, count)));
                    if (p == 0 && r % 2 == 0)
                        Sfx.Play(SfxType.Deal, 0.5f);
                    yield return new WaitForSeconds(stagger);
                }
            }
            yield return new WaitForSeconds(duration);
        }

        Vector3 HandDestination(int id) => id == humanId ? hand.EntryPoint : scene.HandPosition(id);

        Quaternion HandRotation(int id) => id == humanId ? handFan.rotation : scene.Character(id).CardHold.rotation;

        Vector3 HandScale(int id) => id == humanId ? handFan.lossyScale : scene.Character(id).CardHold.lossyScale * 0.85f;

        IEnumerator DealOne(System.Func<Vector3> dest, System.Func<Quaternion> rotation, System.Func<Vector3> scale,
            float duration, System.Action onArrive)
        {
            var card = Card3D.Create(transform, "Deal");
            transientCards.Add(card);
            try
            {
                card.SetHiddenCard();
                card.SetCastShadows(false);
                card.transform.position = scene.PileAnchor.position + Vector3.up * 0.02f;
                card.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                yield return FlyLive(card, dest, rotation, scale, duration, 0.1f);
                onArrive?.Invoke();
            }
            finally
            {
                transientCards.Remove(card);
                if (card != null)
                    Destroy(card.gameObject);
            }
        }

        IEnumerator FlyLive(Card3D card, System.Func<Vector3> dest, System.Func<Quaternion> rotation,
            System.Func<Vector3> scale, float duration, float arc)
        {
            var start = card.transform.position;
            var startRotation = card.transform.rotation;
            var startScale = card.transform.lossyScale;
            card.transform.SetParent(null, true);
            yield return Tween.Run(duration, t =>
            {
                if (card == null)
                    return;
                float k = Tween.EaseInOutCubic(t);
                card.transform.position = Vector3.Lerp(start, dest(), k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * arc;
                card.transform.rotation = Quaternion.Slerp(startRotation, rotation(), k);
                card.transform.localScale = Vector3.Lerp(startScale, scale(), k);
            });
            if (card != null)
            {
                card.transform.SetPositionAndRotation(dest(), rotation());
                card.transform.localScale = scale();
            }
        }

        void ReceiveCards(IEnumerable<Card> cards, bool highlight)
        {
            var received = new HashSet<int>();
            foreach (var card in cards)
                received.Add(card.Id);
            hand.AddCards(cards, hand.EntryPoint, highlight);
            foreach (var view in hand.GetComponentsInChildren<Card3D>())
            {
                if (!view.HasCard || !received.Contains(view.Card.Id))
                    continue;
                view.transform.SetPositionAndRotation(hand.EntryPoint, HandRotation(humanId));
                view.transform.localScale = Vector3.one;
                view.SetHandLighting(true);
            }
        }

        void PresentPowerDealt(GameEvent e)
        {
            bool mine = e.PlayerId == humanId;
            knownPowers[e.PlayerId] = e.Power;
            scene.SetPowerCard(e.PlayerId, mine ? e.Power : PowerType.None, mine, false);
            if (mine && e.Power != PowerType.None)
            {
                hud.SetMyPower(e.Power, false);
                hud.Log("Kartu Kuasamu: " + PowerText.Name(e.Power) + ".");
            }
        }

        // ------------------------------------------------------------------ ronde & giliran

        void PresentRoundStarted(GameEvent e)
        {
            roundElementShown = false;
            lastPlay.Clear();
            hud.SetRoundBadge(null, e.PlayerId);
            for (int id = 0; id < names.Length; id++)
                hud.Label(id)?.HideBubble();
            if (e.Reason == RoundStartReason.GameStart)
                hud.Toast(Name(e.PlayerId, false) + " memulai permainan!", Theme.Text);
            hud.Log("Ronde baru, dipimpin " + Name(e.PlayerId) + ".");
        }

        void PresentTurn(GameEvent e)
        {
            for (int id = 0; id < names.Length; id++)
                hud.Label(id)?.SetActiveTurn(id == e.PlayerId);
            var focus = scene.HeadPosition(e.PlayerId);
            if (e.PlayerId == humanId)
            {
                scene.LookAll(scene.Camera.transform.position);
                rig.Glance(scene.PileAnchor.position);
                Sfx.Play(SfxType.Turn, 0.8f);
            }
            else
            {
                scene.LookAll(focus);
                scene.Character(e.PlayerId)?.LookAt(scene.PileAnchor.position);
                rig.Glance(focus);
                hud.SetWaitingHint("Menunggu " + Name(e.PlayerId) + "…");
            }
        }

        // ------------------------------------------------------------------ menaruh & pass

        IEnumerator PresentPlay(GameEvent e)
        {
            bool mine = e.PlayerId == humanId;
            var character = scene.Character(e.PlayerId);
            var hold = mine ? hand.PlayHold : character != null ? character.PlayHold : null;
            if (hold == null || e.Count < 1 || e.Count > 12 || (mine && (e.Cards == null || e.Cards.Count != e.Count)))
                throw new System.InvalidOperationException("Packet source is unavailable.");
            if (e.HandCounts == null || e.PlayerId < 0 || e.PlayerId >= e.HandCounts.Length || e.HandCounts[e.PlayerId] < 0)
                throw new System.InvalidOperationException("Packet hand count is unavailable.");
            if (!roundElementShown)
            {
                hud.SetRoundBadge(e.Claim, e.PlayerId);
                roundElementShown = true;
            }
            var packet = new List<Card3D>();
            var flightTasks = new List<PresentationTask>();
            var slots = new List<Vector3>();
            var rotations = new List<Quaternion>();
            PresentationTask tightening = null;
            bool gripped = false;
            bool released = false;
            bool finished = false;
            bool active = true;
            float speed = Speed;
            int previousCount = mine ? hand.Count : scene.FanCount(e.PlayerId);
            int afterCount = e.HandCounts[e.PlayerId];
            try
            {
                yield return CenterView();
                System.Action grip = () =>
                {
                    if (!active || gripped || !isActiveAndEnabled)
                        return;
                    gripped = true;
                    packet.AddRange(mine ? hand.TakeCards(e.Cards) : scene.TakeFanCards(e.PlayerId, e.Count));
                    if (packet.Count != e.Count)
                        throw new System.InvalidOperationException("Packet card count does not match the presented event.");
                    foreach (var card in packet)
                    {
                        card.transform.SetParent(hold, true);
                        card.Interactable = false;
                        if (mine)
                            card.SetHandLighting(true);
                    }
                    tightening = StartTask(TightenPacket(packet, 0.18f / speed));
                };
                System.Action release = () =>
                {
                    if (!active || released || !gripped || !isActiveAndEnabled)
                        return;
                    CancelTask(tightening);
                    lastPlay.Clear();
                    released = true;
                    for (int i = 0; i < packet.Count; i++)
                    {
                        var card = packet[i];
                        card.transform.SetParent(transform, true);
                        card.SetHandLighting(false);
                        card.SetCastShadows(true);
                        var slot = NextPileSlot(out var rotation);
                        slots.Add(slot);
                        rotations.Add(rotation);
                        pile.Add(card);
                        lastPlay.Add(card);
                        flightTasks.Add(StartTask(FlyDelayed(card, slot, rotation, (0.45f + i * 0.04f) / speed, i == 0)));
                    }
                    if (!mine)
                        scene.SetFanCount(e.PlayerId, afterCount);
                    Sfx.Play(SfxType.Card);
                    hud.Label(e.PlayerId)?.ShowClaim(e.Count, e.Claim, e.Shielded);
                    hud.Log(Name(e.PlayerId) + ": " + e.Count + " × " + Theme.ColoredElement(e.Claim) + (e.Shielded ? " (Tameng)" : ""));
                };
                if (mine)
                    yield return hand.PlayPacket(grip, release, speed);
                else
                    yield return character.PlayPacket(scene.PileAnchor.position, grip, release, speed);
                if (!released)
                    throw new System.InvalidOperationException("Packet animation ended before release.");
                yield return WaitTasks();
                finished = true;
            }
            finally
            {
                active = false;
                CancelTask(tightening);
                foreach (var task in flightTasks)
                    CancelTask(task);
                if (!finished && gripped)
                {
                    if (!released)
                    {
                        if (mine && hand != null)
                        {
                            var returned = new List<Card>();
                            foreach (var card in packet)
                                if (card != null && card.HasCard)
                                    returned.Add(card.Card);
                            ReceiveCards(returned, false);
                        }
                        foreach (var card in packet)
                            if (card != null)
                                Destroy(card.gameObject);
                        if (!mine && scene != null)
                            scene.SetFanCount(e.PlayerId, previousCount);
                    }
                    else
                    {
                        for (int i = 0; i < packet.Count && i < slots.Count; i++)
                        {
                            if (packet[i] == null)
                                continue;
                            packet[i].transform.SetParent(transform, true);
                            packet[i].transform.SetPositionAndRotation(slots[i], rotations[i]);
                        }
                        if (!mine && scene != null)
                            scene.SetFanCount(e.PlayerId, afterCount);
                    }
                }
                if (!finished && !resolutionFocus && rig != null)
                    rig.SetActionFocus(false);
            }
            if (e.Shielded)
            {
                Fx.Shield(scene.PileAnchor.position + Vector3.up * 0.02f, 0.22f);
                Sfx.Play(SfxType.Power, 0.7f);
                hud.Toast(Name(e.PlayerId, false) + " memasang Tameng: taruhan ini kebal tembakan!", Theme.Text);
                yield return Wait(0.6f);
            }
            if (afterCount == 0)
                hud.Toast(Name(e.PlayerId, false) + " menaruh kartu terakhirnya!", Theme.Text);
            else if (afterCount == 1 && !mine)
                hud.Toast(Name(e.PlayerId, false) + " tinggal 1 kartu!", Theme.Text);
        }

        IEnumerator TightenPacket(List<Card3D> packet, float duration)
        {
            var positions = new Vector3[packet.Count];
            var rotations = new Quaternion[packet.Count];
            for (int i = 0; i < packet.Count; i++)
            {
                positions[i] = packet[i].transform.localPosition;
                rotations[i] = packet[i].transform.localRotation;
            }
            yield return Tween.Run(duration, t =>
            {
                float k = Tween.EaseInOutCubic(t);
                for (int i = 0; i < packet.Count; i++)
                {
                    if (packet[i] == null)
                        continue;
                    packet[i].transform.localPosition = Vector3.Lerp(positions[i], new Vector3(0f, 0f, -i * 0.002f), k);
                    packet[i].transform.localRotation = Quaternion.Slerp(rotations[i], Quaternion.identity, k);
                }
            });
        }

        IEnumerator FlyDelayed(Card3D card, Vector3 pos, Quaternion rot, float duration, bool dust)
        {
            if (card == null)
                yield break;
            yield return card.FlyTo(pos, rot, duration, 0.14f, Random.Range(-25f, 25f));
            if (dust)
                Fx.CardDust(pos);
            Sfx.Play(SfxType.Deal, 0.35f);
        }

        Vector3 NextPileSlot(out Quaternion rotation)
        {
            int i = pile.Count;
            var offset = Random.insideUnitCircle * 0.075f;
            var forward = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            rotation = Quaternion.LookRotation(Vector3.up, forward);
            return scene.PileAnchor.position + new Vector3(offset.x, i * 0.0017f, offset.y);
        }

        IEnumerator PresentPass(GameEvent e)
        {
            hud.Label(e.PlayerId)?.ShowText("Pass", Theme.Muted);
            Sfx.Play(SfxType.Pass);
            hud.Log(Name(e.PlayerId) + " pass.");
            var character = scene.Character(e.PlayerId);
            if (character != null)
                StartTask(character.Wave(0.7f / Speed));
            yield return Wait(0.45f);
        }

        // ------------------------------------------------------------------ tembakan & pembukaan

        Vector3 TargetPoint(int id)
        {
            if (id == humanId)
                return scene.Camera.transform.position - Vector3.up * 0.12f;
            return scene.Character(id).Head.position + Vector3.up * 0.05f;
        }

        IEnumerator CenterView()
        {
            rig.SetActionFocus(true);
            float elapsed = 0f;
            do
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            while (isActiveAndEnabled && !rig.IsViewCentered && elapsed < 1.2f);
        }

        void ReleaseFocus()
        {
            resolutionFocus = false;
            if (rig == null)
                return;
            rig.ClearFocus();
            rig.SetActionFocus(false);
        }

        IEnumerator PresentShot(GameEvent e)
        {
            int shooter = e.PlayerId;
            int target = e.OtherPlayerId;
            bool fired = false;
            bool complete = false;
            bool active = true;
            resolutionFocus = true;
            hud.Label(shooter)?.ShowText("BLUFF!", Theme.Text, 2f / Speed);
            hud.Log(Name(shooter) + " menembak " + Name(target) + "!");
            hud.Toast(Name(shooter, false) + " berteriak BLUFF! ke " + Name(target, false), Theme.Text);
            try
            {
                yield return CenterView();
                System.Action<Vector3, Vector3> fire = (muzzle, forward) =>
                {
                    if (!active || fired || !isActiveAndEnabled)
                        return;
                    fired = true;
                    FireEffects(target, muzzle, forward);
                };
                if (shooter == humanId)
                    yield return FirstPersonShot(target, fire, Speed);
                else
                {
                    var character = scene.Character(shooter);
                    if (character == null)
                        throw new System.InvalidOperationException("Shooter is unavailable.");
                    yield return character.Shoot(() => TargetPoint(target), (muzzle, forward) =>
                    {
                        if (!active || fired || pendingShotFire != null || !isActiveAndEnabled)
                            return;
                        pendingShotFire = () =>
                        {
                            if (!active || character == null || character.Muzzle == null)
                                return;
                            var point = TargetPoint(target);
                            character.RightHand.rotation = AimRotation(character.RightHand, character.Muzzle, point, character.transform.up);
                            var direction = point - character.Muzzle.position;
                            if (direction.sqrMagnitude < 0.000001f || Vector3.Angle(character.Muzzle.forward, direction) > 2f)
                                throw new System.InvalidOperationException("Shot barrel is not aligned with its target.");
                            fire(character.Muzzle.position, character.Muzzle.forward);
                        };
                    }, Speed);
                }
                if (!fired)
                    throw new System.InvalidOperationException("Shot animation ended before firing.");
                yield return WaitTasks();
                complete = true;
            }
            finally
            {
                active = false;
                pendingShotFire = null;
                ResetFirstPersonShot();
                if (!complete)
                    ReleaseFocus();
            }
        }

        void FireEffects(int target, Vector3 muzzle, Vector3 forward)
        {
            var direction = forward.normalized;
            float distance = Vector3.Distance(muzzle, TargetPoint(target));
            Fx.MuzzleFlash(muzzle, direction);
            Fx.Tracer(muzzle, muzzle + direction * distance, Theme.Hex("FFD27A"));
            Sfx.Play(SfxType.Shoot);
            scene.FlashLamp(0.6f);
            rig.Shake(target == humanId ? 0.06f : 0.03f, 0.35f / Speed);
            if (target == humanId)
                hud.FlashScreen(Theme.Red, 0.55f);
            else if (scene.Character(target) != null)
                StartTask(FlinchAtSpeed(scene.Character(target)));
        }

        IEnumerator FlinchAtSpeed(Humanoid character)
        {
            var reaction = character.Flinch();
            try
            {
                if (reaction.MoveNext())
                    yield return Wait(0.15f);
                while (reaction.MoveNext())
                    yield return reaction.Current;
            }
            finally
            {
                (reaction as System.IDisposable)?.Dispose();
            }
        }

        IEnumerator FirstPersonShot(int target, System.Action<Vector3, Vector3> fire, float speed)
        {
            fpTarget = target;
            fpOnFire = fire;
            fpFired = false;
            fpCylinderAdvanced = false;
            fpCylinderStart = fpCylinderIndex;
            fpShotTime = 0f;
            fpShotSpeed = speed;
            fpShotActive = true;
            try
            {
                while (isActiveAndEnabled && fpShotActive)
                    yield return null;
            }
            finally
            {
                ResetFirstPersonShot();
            }
        }

        static float ShotCurve(float time, float start, float end)
        {
            float t = Mathf.Clamp01((time - start) / (end - start));
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static Quaternion AimRotation(Transform pivot, Transform muzzle, Vector3 target, Vector3 up)
        {
            var direction = target - pivot.position;
            if (direction.sqrMagnitude < 0.000001f)
                return pivot.rotation;
            if (Mathf.Abs(Vector3.Dot(direction.normalized, up)) > 0.98f)
                up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.right)) > 0.98f ? Vector3.forward : Vector3.right;
            var barrelRotation = Quaternion.Inverse(pivot.rotation) * muzzle.rotation;
            var muzzleOffset = Quaternion.Inverse(pivot.rotation) * (muzzle.position - pivot.position);
            var barrelOffset = Quaternion.Inverse(barrelRotation) * muzzleOffset;
            float forwardDistance = Mathf.Sqrt(Mathf.Max(0.000001f, direction.sqrMagnitude - barrelOffset.x * barrelOffset.x - barrelOffset.y * barrelOffset.y));
            var correction = Quaternion.FromToRotation(new Vector3(barrelOffset.x, barrelOffset.y, forwardDistance), Vector3.forward);
            return Quaternion.LookRotation(direction, up) * correction * Quaternion.Inverse(barrelRotation);
        }

        void FlushShotFire()
        {
            var fire = pendingShotFire;
            pendingShotFire = null;
            try
            {
                fire?.Invoke();
            }
            catch (System.Exception exception)
            {
                taskError = exception;
            }
        }

        void LateUpdate()
        {
            if (Time.timeScale <= 0f || Time.deltaTime <= 0f)
                return;
            FlushShotFire();
            if (!fpShotActive || fpGun == null)
                return;
            fpShotTime = Mathf.Min(fpFired ? ShotEndTime : FireTime, fpShotTime + Time.deltaTime * fpShotSpeed);
            float draw = ShotCurve(fpShotTime, 0f, DrawTime);
            float lower = ShotCurve(fpShotTime, RecoverTime, ShotEndTime);
            float weight = draw * (1f - lower);
            float cockProgress = ShotCurve(fpShotTime, DrawTime, CockTime);
            float cock = cockProgress * (1f - ShotCurve(fpShotTime, FireTime - 0.025f, FireTime));
            float trigger = ShotCurve(fpShotTime, CockTime, FireTime) * (1f - ShotCurve(fpShotTime, KickTime, RecoverTime));
            float kick = ShotCurve(fpShotTime, FireTime, KickTime) * (1f - ShotCurve(fpShotTime, KickTime, RecoverTime));
            float cockMotion = cockProgress * (1f - ShotCurve(fpShotTime, CockTime, FireTime));
            if (!fpCylinderAdvanced && fpShotTime >= CockTime)
            {
                fpCylinderAdvanced = true;
                fpCylinderIndex++;
            }
            hand.SetShotPose(weight);
            fpGun.gameObject.SetActive(fpShotTime >= 0.1f && fpShotTime < ShotEndTime);
            fpGun.localPosition = Vector3.Lerp(GunRest, GunAim, weight) + new Vector3(0f, kick * 0.016f, -kick * 0.045f);
            var target = TargetPoint(fpTarget);
            var aim = AimRotation(fpGun, fpMuzzle, target, scene.Camera.transform.up);
            var rest = fpGun.parent.rotation * Quaternion.Euler(38f, -12f, -16f);
            fpGun.rotation = Quaternion.Slerp(rest, aim, weight) * Quaternion.Euler(-18f * kick - 7f * cockMotion, 0f, -3f * kick);
            RevolverModel.SetMechanism(fpGun, cock, trigger, fpCylinderStart + cockProgress);
            fpIndexFinger.localRotation = Quaternion.Euler(Mathf.Lerp(18f, -8f, trigger), 0f, 0f);
            fpThumb.localRotation = Quaternion.Euler(-10f * cockMotion, -16f * cockMotion, -8f * cockMotion);
            if (!fpFired && fpShotTime >= FireTime)
            {
                var direction = target - fpMuzzle.position;
                if (direction.sqrMagnitude < 0.000001f || Vector3.Angle(fpMuzzle.forward, direction) > 2f)
                {
                    taskError = new System.InvalidOperationException("First-person barrel is not aligned with its target.");
                    ResetFirstPersonShot();
                    return;
                }
                fpFired = true;
                var fire = fpOnFire;
                fpOnFire = null;
                try
                {
                    fire?.Invoke(fpMuzzle.position, fpMuzzle.forward);
                }
                catch (System.Exception exception)
                {
                    taskError = exception;
                    ResetFirstPersonShot();
                }
            }
            if (fpShotTime >= ShotEndTime)
                ResetFirstPersonShot();
        }

        void ResetFirstPersonShot()
        {
            fpShotActive = false;
            fpFired = false;
            fpCylinderAdvanced = false;
            fpShotTime = 0f;
            fpOnFire = null;
            fpTarget = -1;
            if (hand != null)
                hand.SetShotPose(0f);
            if (fpGun == null)
                return;
            fpGun.localPosition = GunRest;
            fpGun.localRotation = Quaternion.identity;
            RevolverModel.SetMechanism(fpGun, 0f, 0f, fpCylinderIndex);
            if (fpIndexFinger != null)
                fpIndexFinger.localRotation = Quaternion.identity;
            if (fpThumb != null)
                fpThumb.localRotation = Quaternion.identity;
            fpGun.gameObject.SetActive(false);
        }

        void ResetPresentation()
        {
            pendingShotFire = null;
            ResetFirstPersonShot();
            ReleaseFocus();
            if (hand != null)
            {
                hand.SetLowered(false);
                hand.SetInteractable(false);
            }
            if (hud != null)
                hud.SetPileLabelHidden(false);
            if (scene != null)
                foreach (var character in scene.Characters)
                    character.HoldCards();
            foreach (var card in transientCards)
                if (card != null)
                    Destroy(card.gameObject);
            transientCards.Clear();
        }

        void OnDisable()
        {
            presentationVersion++;
            try
            {
                CancelTasks();
            }
            finally
            {
                presenting = false;
                SetInputBlocked(false);
                ResetPresentation();
            }
        }

        IEnumerator PresentReveal(GameEvent e)
        {
            int owner = e.PlayerId;
            int shooter = e.OtherPlayerId;
            resolutionFocus = true;
            yield return CenterView();
            var cards = e.Cards ?? new List<Card>();
            hand.SetLowered(true);
            hud.SetPileLabelHidden(true);

            // Ambil kartu taruhan terakhir dari tumpukan (atau buat baru kalau tidak ada).
            revealed.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                Card3D view;
                if (i < lastPlay.Count && lastPlay[i] != null)
                {
                    view = lastPlay[i];
                    pile.Remove(view);
                }
                else
                {
                    view = Card3D.Create(transform, "Revealed");
                    view.transform.position = scene.PileAnchor.position;
                }
                view.SetCard(cards[i]);
                revealed.Add(view);
            }
            lastPlay.Clear();

            var cam = scene.Camera.transform;
            var center = cam.position + cam.forward * 0.62f + cam.up * 0.03f;
            float spacing = 0.118f;
            var facing = Quaternion.LookRotation(cam.forward, cam.up);
            var faceDown = facing * Quaternion.Euler(0f, 180f, 0f);
            rig.Focus(scene.PileAnchor.position, 0.25f, 50f);
            for (int i = 0; i < revealed.Count; i++)
            {
                var pos = center + cam.right * ((i - (revealed.Count - 1) * 0.5f) * spacing);
                StartTask(revealed[i].FlyTo(pos, faceDown, 0.4f / Speed, 0.05f, 0f));
            }
            yield return WaitTasks();

            foreach (var view in revealed)
            {
                Sfx.Play(SfxType.Card, 0.7f);
                yield return view.Flip(0.28f / Speed);
                bool ok = view.HasCard && view.Card.Matches(e.Claim);
                view.SetGlow(ok ? Theme.Green : Theme.Red, 0.9f);
                yield return new WaitForSeconds(0.08f / Speed);
            }

            Sfx.Play(e.Honest ? SfxType.Honest : SfxType.Lie);
            Fx.Shockwave(scene.PileAnchor.position, e.Honest ? Theme.Green : Theme.Red, 1.2f);
            yield return hud.ShowStamp(e.Honest);

            var ownerCharacter = scene.Character(owner);
            var shooterCharacter = scene.Character(shooter);
            if (e.Honest)
            {
                if (ownerCharacter != null)
                {
                    StartTask(ownerCharacter.Proud(1.2f / Speed));
                    Fx.Sparkle(ownerCharacter.Head.position, Theme.Green, 25, 0.15f);
                }
                if (shooterCharacter != null)
                    StartTask(shooterCharacter.Caught(1.4f / Speed));
                hud.Log("JUJUR: " + Name(owner) + " tidak bohong. " + Name(shooter) + " " + Theme.Colored("salah tembak.", Theme.Red));
            }
            else
            {
                if (ownerCharacter != null)
                {
                    StartTask(ownerCharacter.Caught(1.8f / Speed));
                    Fx.Sweat(ownerCharacter.Head.position);
                }
                else
                {
                    hud.FlashScreen(Theme.Red, 0.4f);
                }
                if (shooterCharacter != null)
                    StartTask(shooterCharacter.Proud(1f / Speed));
                hud.Log(Theme.Colored("BOHONG", Theme.Red) + ": " + Name(owner) + " ketahuan!");
            }
            yield return Wait(1.1f);
        }

        IEnumerator PresentPileTaken(GameEvent e)
        {
            resolutionFocus = true;
            yield return CenterView();
            int taker = e.PlayerId;
            string who = Name(taker, false);
            string message = e.Partial
                ? who + " mengambil " + e.Count + " kartu yang dituduh."
                : who + " mengambil " + e.Count + " kartu dari tumpukan!";

            yield return hud.HideStamp();
            rig.ClearFocus();
            var moving = new List<Card3D>(revealed);
            revealed.Clear();
            if (!e.Partial)
            {
                moving.AddRange(pile);
                pile.Clear();
                lastPlay.Clear();
            }

            float duration = 0.5f / Speed;
            float stagger = Mathf.Min(0.04f, 0.6f / Mathf.Max(1, moving.Count)) / Speed;
            for (int i = 0; i < moving.Count; i++)
            {
                var card = moving[i];
                card.SetGlow(Color.clear, 0f);
                StartTask(FlyAndDestroy(card, () => HandDestination(taker), duration, i * stagger,
                    () => HandRotation(taker), () => HandScale(taker)));
            }
            Sfx.Play(SfxType.Take);
            hud.Toast(message, taker == humanId ? Theme.Red : Theme.Text);
            hud.Log(Hud.StripTags(message));
            hud.Label(taker)?.Punch();
            yield return WaitTasks();
            if (taker == humanId && e.Cards != null)
                ReceiveCards(e.Cards, true);
            hand.SetLowered(false);
            hud.SetPileLabelHidden(false);
        }

        IEnumerator FlyAndDestroy(Card3D card, System.Func<Vector3> dest, float duration, float delay,
            System.Func<Quaternion> rotation = null, System.Func<Vector3> scale = null)
        {
            if (card == null)
                yield break;
            transientCards.Add(card);
            try
            {
                card.SetHandLighting(false);
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);
                if (card == null)
                    yield break;
                var rot = card.transform.rotation * Quaternion.Euler(0f, 0f, Random.Range(-60f, 60f));
                var size = card.transform.lossyScale;
                yield return FlyLive(card, dest, rotation ?? (() => rot), scale ?? (() => size), duration, 0.18f);
            }
            finally
            {
                transientCards.Remove(card);
                if (card != null)
                    Destroy(card.gameObject);
            }
        }

        IEnumerator PresentBurn(GameEvent e)
        {
            resolutionFocus = true;
            yield return CenterView();
            yield return hud.HideStamp();
            var cards = new List<Card3D>(pile);
            cards.AddRange(revealed);
            pile.Clear();
            revealed.Clear();
            lastPlay.Clear();
            bool storm = stormPending;
            stormPending = false;

            if (!storm)
            {
                string message = e.Count > 0 ? "Semua pass: " + e.Count + " kartu hangus!" : "Semua pass: ronde selesai.";
                hud.Toast(message, Theme.Text);
                hud.Log(message);
                Sfx.Play(SfxType.Burn);
            }
            float duration = 0.55f / Speed;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (storm)
                    StartTask(SwirlAway(card, i * 0.03f / Speed));
                else
                    StartTask(FlyAndDestroy(card, () => scene.BurnAnchor.position, duration, i * 0.03f / Speed));
            }
            if (!storm && cards.Count > 0)
            {
                yield return new WaitForSeconds(duration * 0.8f);
                Fx.Fire(scene.BurnAnchor.position, 1.2f);
            }
            yield return WaitTasks();
            yield return Wait(0.4f);
            hand.SetLowered(false);
            hud.SetPileLabelHidden(false);
        }

        IEnumerator SwirlAway(Card3D card, float delay)
        {
            if (card == null)
                yield break;
            transientCards.Add(card);
            try
            {
                if (delay > 0f)
                    yield return new WaitForSeconds(delay);
                var scale = card.transform.localScale;
                float angle0 = Random.Range(0f, 360f);
                yield return Tween.Run(1f / Speed, t =>
                {
                    if (card == null)
                        return;
                    float a = (angle0 + t * 540f) * Mathf.Deg2Rad;
                    float r = 0.1f + t * 0.35f;
                    card.transform.position = scene.PileAnchor.position + new Vector3(Mathf.Cos(a) * r, t * 1.2f, Mathf.Sin(a) * r);
                    card.transform.rotation = Quaternion.Euler(t * 720f, t * 360f, 0f);
                    card.transform.localScale = scale * (1f - t);
                });
            }
            finally
            {
                transientCards.Remove(card);
                if (card != null)
                    Destroy(card.gameObject);
            }
        }

        // ------------------------------------------------------------------ kartu kuasa

        IEnumerator PresentPower(GameEvent e)
        {
            int user = e.PlayerId;
            knownPowers[user] = e.Power;
            hud.Toast(Name(user, false) + " memakai " + PowerText.Name(e.Power).ToUpperInvariant() + "!", Theme.Text);
            hud.Log(Name(user) + " memakai " + PowerText.Name(e.Power) + ".");
            Sfx.Play(SfxType.Power);
            StartTask(RevealPowerCard(user, e.Power));
            hud.Label(user)?.SetPower(e.Power, true);

            switch (e.Power)
            {
                case PowerType.MataElang:
                    Fx.Peek(user == humanId ? scene.Camera.transform.position + scene.Camera.transform.forward * 0.4f : scene.Character(user).Head.position);
                    yield return Wait(0.4f);
                    break;

                case PowerType.Tameng:
                    yield return Wait(0.3f);
                    break;

                case PowerType.Bakar:
                    yield return PresentBurnCards(e);
                    break;

                case PowerType.Badai:
                    stormPending = true;
                    Fx.Storm(scene.PileAnchor.position);
                    Sfx.Play(SfxType.Burn);
                    yield return Wait(0.5f);
                    break;

                case PowerType.Cermin:
                {
                    int shooter = e.OtherPlayerId;
                    var from = user == humanId ? scene.Camera.transform.position + scene.Camera.transform.forward * 0.3f : scene.Character(user).Head.position;
                    Fx.MirrorFlash(from);
                    Fx.Tracer(from, TargetPoint(shooter), Color.white);
                    yield return hud.ShowStamp(false, "PANTUL!");
                    if (shooter == humanId)
                        hud.FlashScreen(Color.white, 0.6f);
                    else
                        StartTask(FlinchAtSpeed(scene.Character(shooter)));
                    rig.Shake(0.04f, 0.3f / Speed);
                    yield return Wait(0.9f);
                    break;
                }

                case PowerType.Hadiah:
                    yield return PresentGift(e);
                    break;
            }
        }

        IEnumerator RevealPowerCard(int user, PowerType power)
        {
            var card = scene.PowerCardObject(user);
            if (card == null)
                yield break;
            if (user != humanId)
            {
                card.SetPower(power);
                yield return card.Flip(0.35f / Speed);
            }
            scene.SetPowerCard(user, power, true, false);
            card.SetGlow(CardArt.PowerColor(power), 1f);
            yield return Wait(1.6f);
            if (card != null)
                card.SetGlow(Color.clear, 0f);
        }

        IEnumerator PresentBurnCards(GameEvent e)
        {
            var flights = new List<Card3D>();
            if (e.PlayerId == humanId && e.Cards != null)
            {
                flights.AddRange(hand.TakeCards(e.Cards));
            }
            else
            {
                flights.AddRange(scene.TakeFanCards(e.PlayerId, e.Count));
            }
            float duration = 0.5f / Speed;
            for (int i = 0; i < flights.Count; i++)
                StartTask(FlyAndDestroy(flights[i], () => scene.BurnAnchor.position, duration, i * 0.08f / Speed));
            yield return new WaitForSeconds(duration * 0.8f);
            Fx.Fire(scene.BurnAnchor.position, 1f, 0.8f);
            Sfx.Play(SfxType.Burn);
            yield return Wait(0.6f);
        }

        IEnumerator PresentGift(GameEvent e)
        {
            int giver = e.PlayerId;
            int receiver = e.OtherPlayerId;
            var flights = new List<Card3D>();
            var arrivals = new List<PresentationTask>();
            if (giver == humanId && e.Cards != null)
            {
                flights.AddRange(hand.TakeCards(e.Cards));
            }
            else
            {
                flights.AddRange(scene.TakeFanCards(giver, e.Count));
            }
            float duration = 0.6f / Speed;
            for (int i = 0; i < flights.Count; i++)
            {
                flights[i].SetGlow(CardArt.PowerColor(PowerType.Hadiah), 0.8f);
                arrivals.Add(StartTask(FlyAndDestroy(flights[i], () => HandDestination(receiver), duration, i * 0.1f / Speed,
                    () => HandRotation(receiver), () => HandScale(receiver))));
            }
            Fx.Gift(scene.HandPosition(giver));
            yield return WaitTasks(arrivals);
            Fx.Gift(HandDestination(receiver));
            if (receiver == humanId && e.Cards != null)
                ReceiveCards(e.Cards, true);
            hud.Log(Name(giver) + " memberi " + e.Count + " kartu kepada " + Name(receiver) + ".");
        }

        IEnumerator PresentPeek(GameEvent e)
        {
            var views = new List<Card3D>();
            var origins = new List<Vector3>();
            var originRots = new List<Quaternion>();
            var flights = new List<PresentationTask>();
            try
            {
                yield return CenterView();
                var cam = scene.Camera.transform;
                for (int i = 0; i < e.Cards.Count && i < lastPlay.Count; i++)
                {
                    var view = lastPlay[i];
                    view.SetCard(e.Cards[i]);
                    views.Add(view);
                    origins.Add(view.transform.position);
                    originRots.Add(view.transform.rotation);
                }
                hand.SetLowered(true);
                hud.SetPileLabelHidden(true);
                var center = cam.position + cam.forward * 0.6f - cam.up * 0.02f;
                var facing = Quaternion.LookRotation(cam.forward, cam.up);
                for (int i = 0; i < views.Count; i++)
                {
                    var pos = center + cam.right * ((i - (views.Count - 1) * 0.5f) * 0.118f);
                    views[i].SetGlow(CardArt.PowerColor(PowerType.MataElang), 0.9f);
                    flights.Add(StartTask(views[i].FlyTo(pos, facing, 0.35f / Speed, 0.04f, 0f)));
                }
                yield return WaitTasks();
                hud.Toast(e.Honest ? "Mata Elang: kartunya JUJUR." : "Mata Elang: dia BOHONG!", Theme.Text);
                yield return Wait(1.8f);
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i] == null)
                        continue;
                    views[i].SetGlow(Color.clear, 0f);
                    views[i].SetHiddenCard();
                    flights.Add(StartTask(views[i].FlyTo(origins[i], originRots[i], 0.3f / Speed, 0.04f, 0f)));
                }
                yield return WaitTasks();
            }
            finally
            {
                foreach (var flight in flights)
                    CancelTask(flight);
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i] == null)
                        continue;
                    views[i].SetHiddenCard();
                    views[i].SetGlow(Color.clear, 0f);
                    views[i].transform.SetPositionAndRotation(origins[i], originRots[i]);
                }
                if (hand != null)
                    hand.SetLowered(false);
                if (hud != null)
                    hud.SetPileLabelHidden(false);
                if (!resolutionFocus && rig != null)
                    rig.SetActionFocus(false);
            }
        }

        // ------------------------------------------------------------------ akhir permainan

        IEnumerator PresentGameOver(GameEvent e)
        {
            yield return hud.HideStamp();
            rig.ClearFocus();
            hand.SetLowered(false);
            hud.SetPileLabelHidden(false);
            for (int id = 0; id < names.Length; id++)
                hud.Label(id)?.SetActiveTurn(false);
            foreach (var c in scene.Characters)
                c.LookAt(e.PlayerId == humanId ? scene.Camera.transform.position : TargetPoint(e.PlayerId));
            if (e.PlayerId == humanId)
            {
                Fx.Confetti(scene.Camera.transform.position + scene.Camera.transform.forward * 0.9f - Vector3.up * 0.3f);
            }
            else
            {
                scene.Character(e.PlayerId)?.Celebrate();
                Fx.Confetti(scene.HandPosition(e.PlayerId) + Vector3.up * 0.3f);
            }
            for (int id = 0; id < names.Length; id++)
            {
                if (id != e.PlayerId && id != humanId)
                    scene.Character(id)?.Defeated();
            }
            hud.Log(Name(e.PlayerId) + " menang!");
            yield return Wait(1.2f);
        }

        string Name(int playerId, bool emphasized = true)
        {
            string n = playerId == humanId ? "Kamu" : names[playerId];
            return emphasized ? "<b>" + n + "</b>" : n;
        }

        static WaitForSeconds Wait(float seconds) => new WaitForSeconds(seconds / Speed);
    }
}
