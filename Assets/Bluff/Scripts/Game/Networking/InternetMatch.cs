using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using Bluff.Core;
using Bluff.Core.AI;
using UnityEngine;

namespace Bluff.Game
{
    internal sealed class InternetMatch
    {
        /// <summary>Time a human has to act on their turn before the host acts for them.</summary>
        public const float TurnSeconds = 30f;
        /// <summary>Turns in a row a human may time out before a bot takes over their seat.</summary>
        public const int IdleTurnsBeforeAutopilot = 2;

        readonly InternetRoom room;
        readonly List<GameEvent> pending = new List<GameEvent>();
        // Every seat has a brain observing the match, so a bot can take over a human seat with full memory.
        readonly BotBrain[] brains = new BotBrain[4];
        readonly bool[] bots = new bool[4];
        readonly int[] timeouts = new int[4];
        readonly bool[] passed = new bool[4];
        readonly int seed = CreateSeed();
        readonly float speed = Mathf.Clamp(GameSettings.Speed, 0.75f, 1.5f);
        readonly float shootSeconds = GameSettings.ShootWindowSeconds;
        public GameEngine Engine { get; }
        public MatchStats Stats { get; } = new MatchStats();

        public InternetMatch(InternetRoom room)
        {
            this.room = room;
            var names = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                names.Add(room.Seats[i].Name);
                bots[i] = room.Seats[i].IsBot;
                brains[i] = new BotBrain(i, 4, bots[i] ? GameSettings.Difficulty : BotDifficulty.Normal, seed + i * 7919);
            }
            Engine = new GameEngine(new GameConfig
            {
                RefundBulletOnHit = GameSettings.RefundBulletOnHit,
                WrongShotTakesWholePile = GameSettings.ClassicWrongShotPenalty,
                EnablePowers = GameSettings.EnablePowers
            }, names, bots);
            Engine.EventRaised += Observe;
        }

        static int CreateSeed()
        {
            var bytes = new byte[4];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        void Observe(GameEvent e)
        {
            pending.Add(e);
            Stats.Observe(e);
            for (int i = 0; i < 4; i++) brains[i].Observe(e.RedactedFor(i));
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        /// <summary>True when the host's bot logic plays this seat: a bot seat, or a human seat on autopilot.</summary>
        bool BotPlays(int id) => bots[id] || room.IsAutopilot(id);

        public IEnumerator Run()
        {
            while (room.InMatch && !room.AllSceneReady) yield return null;
            if (!room.InMatch) yield break;
            yield return room.Publish(Engine, Array.Empty<GameEvent>(), true);
            if (!room.InMatch) yield break;
            Engine.Start(seed);
            yield return Flush();
            while (room.InMatch && Engine.Phase != GamePhase.GameOver)
            {
                if (Engine.Phase == GamePhase.AwaitingPlay)
                {
                    if (BotPlays(Engine.CurrentPlayer)) yield return BotTurn(Engine.CurrentPlayer);
                    else yield return HumanTurn();
                }
                else if (Engine.Phase == GamePhase.ShootWindow)
                    yield return ShootWindow();
                yield return Flush();
            }
        }

        IEnumerator Flush()
        {
            if (pending.Count == 0 || !room.InMatch) yield break;
            var events = pending.ToArray();
            pending.Clear();
            yield return room.Publish(Engine, events);
        }

        IEnumerator HumanTurn()
        {
            int player = Engine.CurrentPlayer;
            double deadline = Now + TurnSeconds;
            room.Open(TurnSeconds);
            while (room.InMatch && Engine.Phase == GamePhase.AwaitingPlay && Engine.CurrentPlayer == player && !room.IsAutopilot(player))
            {
                if (Now >= deadline)
                {
                    TimeOut(player);
                    yield break;
                }
                if (!room.TakeIntent(out var p)) { yield return null; continue; }
                if (p.Seat != player) { room.Reject(p.Seat, "It is not your turn."); continue; }
                ActionResult result;
                switch (p.Action)
                {
                    case RoomAction.Play:
                        result = Engine.TryPlay(p.Seat, p.Cards, p.Claim, p.Flag);
                        break;
                    case RoomAction.Pass:
                        result = Engine.TryPass(p.Seat);
                        break;
                    case RoomAction.Power:
                        result = Engine.TryUsePower(p.Seat, new PowerRequest { Power = p.Power, CardIds = p.Cards, TargetPlayer = p.Target });
                        break;
                    default:
                        room.Reject(p.Seat, "That action is unavailable on your turn.");
                        continue;
                }
                if (!result.Ok) { room.Reject(p.Seat, result.Error); continue; }
                timeouts[player] = 0;
                // Animations do not count against the turn timer.
                double before = Now;
                yield return Flush();
                deadline += Now - before;
                if (room.InMatch && Engine.Phase == GamePhase.AwaitingPlay && Engine.CurrentPlayer == player)
                    room.Open(Mathf.Max(1f, (float)(deadline - Now)));
            }
        }

        /// <summary>A player took back control: they get the full allowance of timeouts again.</summary>
        public void ResetIdle(int id) => timeouts[id] = 0;

        /// <summary>The turn timer ran out: pass when allowed, otherwise play one card honestly.</summary>
        void TimeOut(int player)
        {
            if (!Engine.HasRoundElement || !Engine.TryPass(player).Ok)
            {
                var card = Engine.Players[player].Hand[0];
                var claim = Engine.HasRoundElement ? Engine.RoundElement : card.IsWild ? Element.Matahari : card.Element;
                Engine.TryPlay(player, new[] { card.Id }, claim);
            }
            if (++timeouts[player] >= IdleTurnsBeforeAutopilot)
                room.SetAutopilot(player, true);
            else
                room.Reject(player, "Time ran out, so the table acted for you.");
        }

        IEnumerator BotTurn(int id)
        {
            var brain = brains[id];
            yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.7f, 1.4f) / speed);
            if (!room.InMatch) yield break;
            var power = brain.DecideTurnPower(GameSnapshot.Create(Engine, id));
            if (power != null && Engine.TryUsePower(id, power).Ok)
            {
                yield return Flush();
                yield return new WaitForSecondsRealtime(0.4f / speed);
                if (!room.InMatch || Engine.Phase != GamePhase.AwaitingPlay || Engine.CurrentPlayer != id) yield break;
            }
            var decision = brain.DecideTurn(GameSnapshot.Create(Engine, id));
            var result = decision.Pass ? Engine.TryPass(id) : Engine.TryPlay(id, decision.CardIds, decision.Claim, decision.UseShield);
            if (result.Ok) yield break;
            if (Engine.HasRoundElement) Engine.TryPass(id);
            else
            {
                var card = Engine.Players[id].Hand[0];
                Engine.TryPlay(id, new[] { card.Id }, card.IsWild ? Element.Matahari : card.Element);
            }
        }

        IEnumerator ShootWindow()
        {
            Array.Clear(passed, 0, passed.Length);
            int botShooter = -1;
            double botDelay = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                if (!BotPlays(i) || !Engine.CanShoot(i)) continue;
                var decision = brains[i].DecideShoot(GameSnapshot.Create(Engine, i));
                if (decision.UsePeek && Engine.TryUsePower(i, PowerRequest.Of(PowerType.MataElang)).Ok)
                {
                    yield return Flush();
                    if (!room.InMatch) yield break;
                    decision = brains[i].DecideShoot(GameSnapshot.Create(Engine, i));
                }
                if (decision.Shoot && decision.DelaySeconds < botDelay)
                {
                    botShooter = i;
                    botDelay = decision.DelaySeconds;
                }
            }
            bool anyHuman = false;
            for (int i = 0; i < 4; i++) if (HumanEligible(i)) anyHuman = true;
            float botAt = botShooter < 0 ? float.MaxValue : (float)botDelay / Mathf.Sqrt(speed);
            float duration = anyHuman ? shootSeconds : botShooter >= 0 ? botAt + 0.05f : 0.6f / speed;
            if (botShooter >= 0 && botAt > duration) botAt = duration * 0.85f;
            double start = Time.realtimeSinceStartupAsDouble;
            room.Open(duration);
            while (room.InMatch && Engine.Phase == GamePhase.ShootWindow)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - start;
                if (elapsed >= duration) break;
                bool changed = false;
                while (room.TakeIntent(out var p))
                {
                    if (BotPlays(p.Seat)) continue;
                    if (passed[p.Seat]) { room.Reject(p.Seat, "You already let this play pass."); continue; }
                    if (p.Action == RoomAction.LetPass && HumanEligible(p.Seat))
                    {
                        passed[p.Seat] = true;
                        continue;
                    }
                    if (p.Action == RoomAction.Shoot)
                    {
                        var result = Engine.TryShoot(p.Seat);
                        if (result.Ok) yield break;
                        room.Reject(p.Seat, result.Error);
                    }
                    else if (p.Action == RoomAction.Power && p.Power == PowerType.MataElang)
                    {
                        var result = Engine.TryUsePower(p.Seat, PowerRequest.Of(PowerType.MataElang));
                        if (!result.Ok) { room.Reject(p.Seat, result.Error); continue; }
                        double before = Time.realtimeSinceStartupAsDouble;
                        yield return Flush();
                        if (!room.InMatch) yield break;
                        start += Time.realtimeSinceStartupAsDouble - before;
                        duration += 2.5f;
                        if (botShooter >= 0) botAt += 2.5f;
                        room.Open(Mathf.Max(0, duration - (float)(Time.realtimeSinceStartupAsDouble - start)));
                        changed = true;
                        break;
                    }
                    else room.Reject(p.Seat, "Wait for your turn to do this.");
                }
                if (changed) continue;
                bool allPassed = anyHuman;
                for (int i = 0; i < 4; i++) if (HumanEligible(i) && !passed[i]) allPassed = false;
                if (botShooter >= 0 && (elapsed >= botAt || allPassed))
                {
                    Engine.TryShoot(botShooter);
                    yield break;
                }
                if (allPassed) break;
                yield return null;
            }
            if (room.InMatch && Engine.Phase == GamePhase.ShootWindow) Engine.CloseShootWindow();
        }

        bool HumanEligible(int id) => !BotPlays(id) && (Engine.CanShoot(id) ||
            (Engine.Players[id].Power == PowerType.MataElang && Engine.PowerBlockReason(id) == null));
    }
}
