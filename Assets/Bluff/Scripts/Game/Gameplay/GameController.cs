using System.Collections;
using System.Collections.Generic;
using Bluff.Core;
using Bluff.Core.AI;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Pengatur alur permainan offline: 1 pemain manusia (sudut pandang orang pertama) + 3 komputer.
    /// Engine (Bluff.Core) memegang aturan; controller ini memutuskan KAPAN hal terjadi (giliran bot, jendela tembak,
    /// Kartu Kuasa, jeda animasi) dan meneruskan event ke <see cref="TableView3D"/> untuk dianimasikan.
    /// </summary>
    public sealed class GameController : BluffScreen
    {
        public const int HumanId = 0;

        static readonly string[] BotNames = { "Raka", "Sinta", "Bima", "Dewi", "Joko", "Laras", "Bayu", "Tari" };

        GameEngine engine;
        BotBrain[] brains;
        TableView3D view;
        readonly Queue<GameEvent> pending = new Queue<GameEvent>();
        readonly MatchStats stats = new MatchStats();
        int seed;
        bool humanActed;
        bool humanPowerUsed;
        bool humanShotRequested;
        bool humanLetPass;
        bool humanPeekRequested;
        bool paused;
        UIKit.Modal pauseModal;

        static float Speed => GameSettings.Speed;
        static bool AutoPlay => BluffDebug.AutoPlay;

        InternetRoom room;
        GameSnapshot snapshot;
        int localSeat;
        int displayedRevision;
        bool onlineInput;
        bool submitted;
        bool onlineLetPass;
        bool resultShown;
        readonly string rewardId = System.Guid.NewGuid().ToString("N");
        float windowDuration;
        double windowEnds;

        public GameEngine Engine => room != null ? room.HostEngine : engine;

        protected override void Awake()
        {
            base.Awake();
            if (InternetRoom.Current != null && InternetRoom.Current.InMatch)
            {
                room = InternetRoom.Current;
                localSeat = room.LocalSeat;
                return;
            }
            localSeat = HumanId;
            seed = BluffDebug.FixedSeed != 0 ? BluffDebug.FixedSeed : System.Environment.TickCount;
            var config = new GameConfig
            {
                RefundBulletOnHit = GameSettings.RefundBulletOnHit,
                WrongShotTakesWholePile = GameSettings.ClassicWrongShotPenalty,
                EnablePowers = GameSettings.EnablePowers,
            };

            var rng = new System.Random(seed);
            var pool = new List<string>(BotNames);
            var names = new List<string> { GameSettings.PlayerName };
            var isBot = new List<bool> { false };
            for (int i = 1; i < config.PlayerCount; i++)
            {
                int pick = rng.Next(pool.Count);
                names.Add(pool[pick]);
                pool.RemoveAt(pick);
                isBot.Add(true);
            }

            engine = new GameEngine(config, names, isBot);
            brains = new BotBrain[config.PlayerCount];
            for (int i = 0; i < config.PlayerCount; i++)
            {
                if (i != HumanId || AutoPlay)
                    brains[i] = new BotBrain(i, config.PlayerCount, i == HumanId ? BotDifficulty.Normal : GameSettings.Difficulty, seed + i * 7919);
            }
            engine.EventRaised += OnEngineEvent;

            BuildView(GameSnapshot.Create(engine, localSeat));
        }

        void BuildView(GameSnapshot state)
        {
            snapshot = state;
            view = TableView3D.Create(transform, Root, Canvas, snapshot, localSeat);
            var hud = view.Hud;
            hud.EmoteRequested += (value,target) => { if (room != null) room.SendEmote(value,target); else view.ShowEmote(localSeat,target,value); };
            if (room != null) room.EmoteReceived += OnEmote;
            hud.PlayRequested += OnHumanPlay;
            hud.PassRequested += OnHumanPass;
            hud.BluffRequested += () =>
            {
                if (room != null) SubmitOnline(new RoomPacket { Action = RoomAction.Shoot });
                else humanShotRequested = true;
            };
            hud.LetPassRequested += () =>
            {
                if (room != null) SubmitOnline(new RoomPacket { Action = RoomAction.LetPass });
                else humanLetPass = true;
            };
            hud.PeekRequested += () =>
            {
                if (room != null) SubmitOnline(new RoomPacket { Action = RoomAction.Power, Power = PowerType.MataElang });
                else humanPeekRequested = true;
            };
            hud.PowerRequested += OnHumanPower;
            hud.PauseRequested += TogglePause;
            hud.HelpRequested += ShowRules;
        }

        void Start()
        {
            if (room != null)
            {
                room.AttachScene();
                StartCoroutine(RunOnline());
            }
            else StartCoroutine(RunGame());
        }

        IEnumerator RunOnline()
        {
            while (room != null && room.InMatch)
            {
                if (!room.TakePresentation(out var packet))
                {
                    yield return null;
                    continue;
                }
                if (packet.Type == RoomMessage.Bootstrap)
                {
                    BuildView(packet.Snapshot);
                    displayedRevision = packet.Revision;
                    room.Presented(displayedRevision);
                }
                else if (packet.Type == RoomMessage.Batch)
                {
                    onlineInput = false;
                    submitted = false;
                    view.EndHumanTurn();
                    view.HideShootWindow();
                    foreach (var e in packet.Events)
                    {
                        yield return view.Present(e);
                        BluffDebug.RaisePresented(e);
                        if (room == null || !room.InMatch) yield break;
                    }
                    if (packet.Snapshot.Phase != GamePhase.ShootWindow || snapshot.Phase != GamePhase.ShootWindow ||
                        packet.Snapshot.LastPlayerId != snapshot.LastPlayerId || packet.Snapshot.RoundClaims.Count != snapshot.RoundClaims.Count)
                        onlineLetPass = false;
                    snapshot = packet.Snapshot;
                    view.SetSnapshot(snapshot);
                    displayedRevision = packet.Revision;
                    room.Presented(displayedRevision);
                    if (snapshot.Phase == GamePhase.GameOver && !resultShown)
                    {
                        resultShown = true;
                        Resume();
                        Sfx.Play(snapshot.Winner == localSeat ? SfxType.Win : SfxType.Lose);
                        int coins = Cosmetics.RewardMatch(rewardId, snapshot.Winner == localSeat);
                        view.Hud.ShowResult(snapshot, room.IsHost ? room.HostStats : null, localSeat, room.ReturnToRoom, LeaveOnline, coins);
                    }
                }
                else if (packet.Type == RoomMessage.Open && packet.Revision == displayedRevision)
                {
                    onlineInput = packet.Flag;
                    submitted = false;
                    windowDuration = packet.Seconds;
                    windowEnds = Time.realtimeSinceStartupAsDouble + packet.Seconds;
                    RefreshOnlineInput();
                }
                else if (packet.Type == RoomMessage.Rejected && view != null)
                {
                    submitted = false;
                    view.Toast(packet.Text, Theme.Red);
                    RefreshOnlineInput();
                }
            }
        }

        void RefreshOnlineInput()
        {
            if (view == null || snapshot == null || !onlineInput || paused || submitted || GameTutorial.IsShowing) return;
            if (snapshot.IsMyTurn) view.BeginHumanTurn(snapshot);
            else if (snapshot.Phase == GamePhase.ShootWindow && !onlineLetPass)
                view.ShowShootWindow(snapshot.CanShoot, snapshot.HasPower && snapshot.MyPower == PowerType.MataElang && snapshot.LastPlayerId != localSeat, snapshot.LastPlayerId);
        }

        void SubmitOnline(RoomPacket packet)
        {
            if (room == null || !room.InMatch || !onlineInput || !room.InputOpen || paused || submitted || GameTutorial.IsShowing) return;
            submitted = true;
            if (packet.Action == RoomAction.LetPass) onlineLetPass = true;
            view.EndHumanTurn();
            view.HideShootWindow();
            room.Submit(packet);
        }

        async void LeaveOnline()
        {
            Resume();
            if (room != null) await room.LeaveAsync();
        }

        void OnEmote(int seat,int target,int value) { if(view!=null) view.ShowEmote(seat,target,value); }

        void OnDestroy()
        {
            if(room!=null) room.EmoteReceived -= OnEmote;
            Time.timeScale = 1f;
            if (engine != null)
                engine.EventRaised -= OnEngineEvent;
            if (room != null)
                room.DetachScene();
        }

        void Update()
        {
            if (view != null) view.SetInputBlocked(paused || GameTutorial.IsShowing);
            if (room != null && view != null && onlineInput && !room.InputOpen)
            {
                onlineInput = false;
                view.EndHumanTurn();
                view.HideShootWindow();
            }
            if (room != null && view != null && onlineInput && snapshot.Phase == GamePhase.ShootWindow && windowDuration > 0)
                view.UpdateShootWindow(Mathf.Clamp01((float)(windowEnds - Time.realtimeSinceStartupAsDouble) / windowDuration));
            if (InputCompat.EscapePressed())
                TogglePause();
        }

        void OnEngineEvent(GameEvent e)
        {
            // Jangan memanggil engine dari sini: cukup antrekan, dianimasikan berurutan oleh RunGame.
            pending.Enqueue(e);
            stats.Observe(e);
            for (int i = 0; i < brains.Length; i++)
            {
                if (brains[i] != null)
                    brains[i].Observe(e.RedactedFor(i));
            }
        }

        // ================================================================== alur utama

        IEnumerator RunGame()
        {
            yield return null;
            if (!AutoPlay && !GameSettings.TutorialSeen)
            {
                bool closed = false;
                GameTutorial.Show(Root, () => closed = true);
                while (!closed) yield return null;
                yield return null;
            }
            engine.Start(seed);
            yield return PresentPending();

            while (engine.Phase != GamePhase.GameOver)
            {
                if (engine.Phase == GamePhase.AwaitingPlay)
                {
                    int current = engine.CurrentPlayer;
                    if (current == HumanId && !AutoPlay)
                        yield return HumanTurn();
                    else
                        yield return BotTurn(current);
                }
                else if (engine.Phase == GamePhase.ShootWindow)
                {
                    yield return ShootWindow();
                }
                yield return PresentPending();
            }

            yield return new WaitForSeconds(0.6f);
            Sfx.Play(engine.Winner == HumanId ? SfxType.Win : SfxType.Lose);
            int coins = AutoPlay ? 0 : Cosmetics.RewardMatch(rewardId, engine.Winner == localSeat);
            view.Hud.ShowResult(GameSnapshot.Create(engine, localSeat), stats, localSeat, SceneFlow.GoToGame, SceneFlow.GoToMenu, coins);
        }

        IEnumerator PresentPending()
        {
            while (pending.Count > 0)
            {
                var e = pending.Dequeue();
                yield return view.Present(e.RedactedFor(HumanId));
                BluffDebug.RaisePresented(e);
            }
            snapshot = GameSnapshot.Create(engine, localSeat);
            view.SetSnapshot(snapshot);
        }

        // ------------------------------------------------------------------ giliran manusia

        IEnumerator HumanTurn()
        {
            humanActed = false;
            humanPowerUsed = false;
            view.BeginHumanTurn(GameSnapshot.Create(engine, HumanId));
            while (!humanActed)
            {
                if (humanPowerUsed)
                {
                    // Kuasa di awal giliran (Bakar / Badai / Hadiah): animasikan, lalu lanjutkan giliran.
                    humanPowerUsed = false;
                    view.EndHumanTurn();
                    yield return PresentPending();
                    if (engine.Phase != GamePhase.AwaitingPlay || engine.CurrentPlayer != HumanId)
                        yield break;
                    view.BeginHumanTurn(GameSnapshot.Create(engine, HumanId));
                }
                yield return null;
            }
            view.EndHumanTurn();
        }

        bool HumanCanActNow => !humanActed && !paused && engine.CurrentPlayer == HumanId && engine.Phase == GamePhase.AwaitingPlay;

        void OnHumanPlay(List<int> cardIds, Element claim, bool shield)
        {
            if (room != null)
            {
                SubmitOnline(new RoomPacket { Action = RoomAction.Play, Cards = new List<int>(cardIds), Claim = claim, Flag = shield });
                return;
            }
            if (!HumanCanActNow)
                return;
            var result = engine.TryPlay(HumanId, cardIds, claim, shield);
            if (result.Ok)
                humanActed = true;
            else
                view.Toast(result.Error, Theme.Red);
        }

        void OnHumanPass()
        {
            if (room != null)
            {
                SubmitOnline(new RoomPacket { Action = RoomAction.Pass });
                return;
            }
            if (!HumanCanActNow)
                return;
            var result = engine.TryPass(HumanId);
            if (result.Ok)
                humanActed = true;
            else
                view.Toast(result.Error, Theme.Red);
        }

        void OnHumanPower(PowerRequest request)
        {
            if (room != null)
            {
                SubmitOnline(new RoomPacket { Action = RoomAction.Power, Power = request.Power, Cards = new List<int>(request.CardIds), Target = request.TargetPlayer });
                return;
            }
            if (!HumanCanActNow || humanPowerUsed)
                return;
            var result = engine.TryUsePower(HumanId, request);
            if (result.Ok)
                humanPowerUsed = true;
            else
                view.Toast(result.Error, Theme.Red);
        }

        // ------------------------------------------------------------------ giliran bot

        IEnumerator BotTurn(int botId)
        {
            var brain = brains[botId];
            yield return new WaitForSeconds(Random.Range(0.7f, 1.4f) / Speed);

            var power = brain.DecideTurnPower(GameSnapshot.Create(engine, botId));
            if (power != null && engine.TryUsePower(botId, power).Ok)
            {
                yield return PresentPending();
                yield return new WaitForSeconds(0.4f / Speed);
                if (engine.Phase != GamePhase.AwaitingPlay || engine.CurrentPlayer != botId)
                    yield break;
            }

            var decision = brain.DecideTurn(GameSnapshot.Create(engine, botId));
            var result = decision.Pass
                ? engine.TryPass(botId)
                : engine.TryPlay(botId, decision.CardIds, decision.Claim, decision.UseShield);
            if (result.Ok)
                yield break;

            // Seharusnya tidak terjadi (sudah diuji ribuan game), tapi jangan sampai permainan macet.
            Debug.LogWarning("Bot " + botId + " made an invalid move:" + result.Error);
            if (engine.HasRoundElement)
            {
                engine.TryPass(botId);
            }
            else
            {
                var card = engine.Players[botId].Hand[0];
                engine.TryPlay(botId, new List<int> { card.Id }, card.IsWild ? Element.Matahari : card.Element);
            }
        }

        // ------------------------------------------------------------------ jendela tembak

        /// <summary>
        /// Bot memutuskan dulu (boleh mengintip dengan Mata Elang), lalu manusia boleh menekan BLUFF! / INTIP sampai
        /// waktu habis. Siapa yang paling cepat, dialah yang menembak.
        /// </summary>
        IEnumerator ShootWindow()
        {
            int target = engine.LastPlay.PlayerId;
            int botShooter = -1;
            double botDelay = double.MaxValue;
            for (int i = 0; i < brains.Length; i++)
            {
                if (brains[i] == null || !engine.CanShoot(i))
                    continue;
                var decision = brains[i].DecideShoot(GameSnapshot.Create(engine, i));
                if (decision.UsePeek && engine.TryUsePower(i, PowerRequest.Of(PowerType.MataElang)).Ok)
                {
                    yield return PresentPending();
                    if (engine.Phase != GamePhase.ShootWindow)
                        yield break;
                    decision = brains[i].DecideShoot(GameSnapshot.Create(engine, i));
                }
                if (decision.Shoot && decision.DelaySeconds < botDelay)
                {
                    botDelay = decision.DelaySeconds;
                    botShooter = i;
                }
            }

            bool humanCan = !AutoPlay && engine.CanShoot(HumanId);
            bool humanCanPeek = !AutoPlay && engine.Players[HumanId].Power == PowerType.MataElang && engine.PowerBlockReason(HumanId) == null;
            float botAt = botShooter >= 0 ? (float)botDelay / Mathf.Sqrt(Speed) : float.MaxValue;
            float duration = humanCan || humanCanPeek
                ? GameSettings.ShootWindowSeconds
                : botShooter >= 0 ? botAt + 0.05f : 0.6f / Speed;
            if (botShooter >= 0 && botAt > duration)
                botAt = duration * 0.85f;

            humanShotRequested = false;
            humanLetPass = false;
            humanPeekRequested = false;
            view.ShowShootWindow(humanCan, humanCanPeek, target);

            float t = 0f;
            while (true)
            {
                if (humanPeekRequested)
                {
                    humanPeekRequested = false;
                    if (engine.TryUsePower(HumanId, PowerRequest.Of(PowerType.MataElang)).Ok)
                    {
                        view.Hud.HidePeekButton();
                        yield return PresentPending();
                        duration += 2.5f;
                        if (botShooter >= 0)
                            botAt += 2.5f;
                    }
                }
                if (humanCan && humanShotRequested && engine.CanShoot(HumanId))
                {
                    view.HideShootWindow();
                    engine.TryShoot(HumanId);
                    yield break;
                }
                if (botShooter >= 0 && (t >= botAt || humanLetPass))
                {
                    view.HideShootWindow();
                    engine.TryShoot(botShooter);
                    yield break;
                }
                if (t >= duration || humanLetPass)
                    break;
                view.UpdateShootWindow(1f - t / duration);
                yield return null;
                t += Time.deltaTime;
            }
            view.HideShootWindow();
            engine.CloseShootWindow();
        }

        // ================================================================== jeda

        void TogglePause()
        {
            if (GameTutorial.IsShowing || (room != null ? snapshot == null || snapshot.Phase == GamePhase.GameOver : engine.Phase == GamePhase.GameOver))
                return;
            if (paused)
            {
                Resume();
                return;
            }
            paused = true;
            Time.timeScale = room == null ? 0f : 1f;
            if (room != null)
            {
                view.EndHumanTurn();
                view.HideShootWindow();
            }
            pauseModal = UIKit.CreateModal(Root, room == null ? "Pause" : "Menu - online play continues", new Vector2(640f, 620f), Resume);
            var content = pauseModal.Content;
            AddPauseButton(content, "Resume", SpriteFactory.Emblem(GameIcon.Play), 0, Theme.Gold, Theme.TextDark, Resume);
            AddPauseButton(content, "Guide", SpriteFactory.Emblem(GameIcon.Guide), 1, Theme.PanelLight, Theme.Text, ShowRules);
            if (room == null)
            {
                AddPauseButton(content, "Restart match", SpriteFactory.Emblem(GameIcon.Restart), 2, Theme.PanelLight, Theme.Text, SceneFlow.GoToGame);
                AddPauseButton(content, "Main menu", SpriteFactory.Emblem(GameIcon.Home), 3, Theme.Panel, Theme.Muted, SceneFlow.GoToMenu);
            }
            else AddPauseButton(content, "Leave room", SpriteFactory.Emblem(GameIcon.Exit), 2, Theme.Panel, Theme.Muted, LeaveOnline);
        }

        static void AddPauseButton(RectTransform parent, string label, Sprite icon, int index, Color bg, Color fg, System.Action onClick)
        {
            var button = UIKit.CreateButton(parent, label, label, new Vector2(460f, 82f), bg, fg, onClick, 32, 8f);
            button.GetComponent<RectTransform>().Place(UIKit.Top, new Vector2(0f, -50f - index * 104f), new Vector2(460f, 82f));
            UIKit.AddButtonIcon(button, icon, Color.white, 44f, 20f);
        }

        void Resume()
        {
            if (pauseModal != null)
                pauseModal.Close();
            pauseModal = null;
            paused = false;
            Time.timeScale = 1f;
            if (room != null) RefreshOnlineInput();
        }

        void ShowRules()
        {
            if (GameTutorial.IsShowing || view == null) return;
            bool pauseForGuide = room == null && !paused;
            if (pauseForGuide) Time.timeScale = 0f;
            view.SetInputBlocked(true);
            var tutorial = GameTutorial.Show(Root, () =>
            {
                if (this == null) return;
                if (pauseForGuide && !paused) Time.timeScale = 1f;
                view.SetInputBlocked(paused);
                if (room != null) StartCoroutine(RestoreOnlineInput());
            });
            if (snapshot != null) tutorial.SetConfig(snapshot.Config);
        }

        IEnumerator RestoreOnlineInput()
        {
            yield return null;
            RefreshOnlineInput();
        }
    }
}
