using System.Collections.Generic;

namespace Bluff.Core
{
    public readonly struct ClaimInfo
    {
        public readonly int PlayerId;
        public readonly int Count;

        public ClaimInfo(int playerId, int count)
        {
            PlayerId = playerId;
            Count = count;
        }
    }

    /// <summary>
    /// Apa yang boleh diketahui satu pemain tentang meja: tangannya sendiri plus informasi publik.
    /// Bot hanya mengambil keputusan dari snapshot ini (dan ingatannya sendiri), bukan dari engine langsung.
    /// </summary>
    public sealed class GameSnapshot
    {
        public int ViewerId;
        public GameConfig Config;
        public int PlayerCount;
        public List<Card> MyHand;
        public int[] HandCounts;
        public int[] Bullets;
        public string[] Names;
        public GamePhase Phase;
        public int CurrentPlayer;
        public int RoundLeader;
        public bool HasRoundElement;
        public Element RoundElement;
        public int LastPlayerId = -1;
        public int LastPlayCount;
        public Element LastPlayClaim;
        public bool LastPlayShielded;
        public List<ClaimInfo> RoundClaims = new List<ClaimInfo>();
        public int PileCount;
        public int BurnedCount;
        public int ConsecutivePasses;
        public int Winner = -1;

        /// <summary>Kartu Kuasa milikku (None kalau kuasa dimatikan).</summary>
        public PowerType MyPower;

        public bool MyPowerUsed;

        /// <summary>Pemain yang kuasanya sudah dipakai (jenis kuasa yang sudah dipakai diketahui semua orang).</summary>
        public bool[] PowerSpent;

        /// <summary>Kuasa yang sudah diketahui (sudah dipakai); None kalau masih rahasia.</summary>
        public PowerType[] KnownPowers;

        public bool IsMyTurn => Phase == GamePhase.AwaitingPlay && CurrentPlayer == ViewerId;

        public bool MustLead => IsMyTurn && !HasRoundElement;

        public bool HasPower => MyPower != PowerType.None && !MyPowerUsed;

        public bool CanShoot =>
            Phase == GamePhase.ShootWindow &&
            LastPlayerId >= 0 &&
            LastPlayerId != ViewerId &&
            !LastPlayShielded &&
            ViewerId >= 0 &&
            Bullets[ViewerId] > 0;

        public static GameSnapshot Create(GameEngine engine, int viewerId)
        {
            int count = engine.PlayerCount;
            bool viewerValid = viewerId >= 0 && viewerId < count;
            var s = new GameSnapshot
            {
                ViewerId = viewerId,
                Config = engine.Config.Clone(),
                PlayerCount = count,
                MyHand = viewerValid ? new List<Card>(engine.Players[viewerId].Hand) : new List<Card>(),
                HandCounts = new int[count],
                Bullets = new int[count],
                Names = new string[count],
                PowerSpent = new bool[count],
                KnownPowers = new PowerType[count],
                Phase = engine.Phase,
                CurrentPlayer = engine.CurrentPlayer,
                RoundLeader = engine.RoundLeader,
                HasRoundElement = engine.HasRoundElement,
                RoundElement = engine.RoundElement,
                PileCount = engine.PileCount,
                BurnedCount = engine.BurnedCount,
                ConsecutivePasses = engine.ConsecutivePasses,
                Winner = engine.Winner,
                MyPower = viewerValid ? engine.Players[viewerId].Power : PowerType.None,
                MyPowerUsed = viewerValid && engine.Players[viewerId].PowerUsed,
            };
            for (int i = 0; i < count; i++)
            {
                var p = engine.Players[i];
                s.HandCounts[i] = p.HandCount;
                s.Bullets[i] = p.Bullets;
                s.Names[i] = p.Name;
                s.PowerSpent[i] = p.PowerUsed;
                s.KnownPowers[i] = p.PowerUsed || i == viewerId ? p.Power : PowerType.None;
            }
            if (engine.LastPlay != null)
            {
                s.LastPlayerId = engine.LastPlay.PlayerId;
                s.LastPlayCount = engine.LastPlay.Count;
                s.LastPlayClaim = engine.LastPlay.Claim;
                s.LastPlayShielded = engine.LastPlay.Shielded;
            }
            foreach (var play in engine.RoundPlays)
                s.RoundClaims.Add(new ClaimInfo(play.PlayerId, play.Count));
            return s;
        }
    }
}
