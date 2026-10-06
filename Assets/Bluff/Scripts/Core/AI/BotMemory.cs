using System.Collections.Generic;

namespace Bluff.Core.AI
{
    /// <summary>
    /// Ingatan bot, dibangun hanya dari event yang sudah di-redact untuknya (tidak bisa mengintip kartu lawan).
    /// </summary>
    public sealed class BotMemory
    {
        readonly int me;
        readonly List<Card> lastRevealed = new List<Card>();

        // Lawan baru menaruh kartu: kartu yang aku tahu ada di tangannya baru "dilupakan" setelah jendela tembak
        // selesai, supaya perhitungan saat memutuskan menembak masih memakai isi tangannya sebelum menaruh.
        int pendingPlayer = -1;
        int pendingCount;
        Element pendingClaim;

        public BotMemory(int playerId, int playerCount)
        {
            me = playerId;
            KnownInHand = new List<Card>[playerCount];
            for (int i = 0; i < playerCount; i++)
                KnownInHand[i] = new List<Card>();
            TimesRevealed = new int[playerCount];
            TimesCaughtLying = new int[playerCount];
        }

        /// <summary>Kartu milikku yang sekarang ada di tumpukan tengah.</summary>
        public List<Card> MyCardsInPile { get; } = new List<Card>();

        /// <summary>Kartu milikku yang sudah hangus (keluar dari permainan).</summary>
        public List<Card> MyBurnedCards { get; } = new List<Card>();

        /// <summary>Kartu yang aku tahu pasti ada di tangan pemain lain (karena mereka mengambil tumpukan).</summary>
        public List<Card>[] KnownInHand { get; }

        public int[] TimesRevealed { get; }
        public int[] TimesCaughtLying { get; }

        /// <summary>Taruhan pemain lain yang sudah dilihat (untuk memperkirakan seberapa sering meja menembak).</summary>
        public int PlaysSeen { get; private set; }

        /// <summary>Tembakan yang sudah dilihat, termasuk tembakan ke taruhan terakhir seseorang.</summary>
        public int ShotsSeen { get; private set; }

        /// <summary>Tembakanku sendiri (selain ke taruhan terakhir lawan) dan berapa yang tepat sasaran.</summary>
        public int MyShots { get; private set; }

        public int MyHits { get; private set; }

        int lastShooter = -1;
        bool lastShotWasFinal;

        /// <summary>Hasil Mata Elang untuk taruhan yang sedang di jendela tembak (null kalau tidak mengintip).</summary>
        public List<Card> PeekedCards { get; private set; }

        public int PeekedTarget { get; private set; } = -1;

        public void Observe(GameEvent e)
        {
            switch (e.Type)
            {
                case GameEventType.PowerPeek:
                    if (e.PlayerId == me && e.Cards != null)
                    {
                        PeekedCards = new List<Card>(e.Cards);
                        PeekedTarget = e.OtherPlayerId;
                    }
                    break;

                case GameEventType.PowerUsed when e.Power == PowerType.Hadiah:
                    if (e.Cards != null && e.PlayerId == me)
                    {
                        // Aku tahu persis kartu apa yang sekarang dipegang penerima.
                        foreach (var card in e.Cards)
                            AddUnique(KnownInHand[e.OtherPlayerId], card);
                    }
                    else if (e.Cards != null && e.OtherPlayerId == me)
                    {
                        KnownInHand[e.PlayerId].RemoveAll(c => e.Cards.Contains(c));
                    }
                    break;

                case GameEventType.PowerUsed when e.Power == PowerType.Bakar:
                    if (e.Cards != null && e.PlayerId == me)
                        MyBurnedCards.AddRange(e.Cards);
                    break;

                case GameEventType.RoundStarted:
                    ClearPeek();
                    break;

                case GameEventType.ShotFired:
                    ShotsSeen++;
                    lastShooter = e.PlayerId;
                    lastShotWasFinal = e.HandCounts != null && e.HandCounts[e.OtherPlayerId] == 0;
                    if (e.PlayerId == me && !lastShotWasFinal)
                        MyShots++;
                    ApplyPendingForget();
                    break;

                case GameEventType.ShootWindowClosed:
                    ApplyPendingForget();
                    ClearPeek();
                    break;

                case GameEventType.CardsPlayed:
                    PlaysSeen++;
                    ClearPeek();
                    if (e.PlayerId == me)
                    {
                        if (e.Cards != null)
                            MyCardsInPile.AddRange(e.Cards);
                    }
                    else
                    {
                        ApplyPendingForget();
                        pendingPlayer = e.PlayerId;
                        pendingCount = e.Count;
                        pendingClaim = e.Claim;
                    }
                    break;

                case GameEventType.CardsRevealed:
                    lastRevealed.Clear();
                    if (e.Cards != null)
                        lastRevealed.AddRange(e.Cards);
                    TimesRevealed[e.PlayerId]++;
                    if (!e.Honest)
                    {
                        TimesCaughtLying[e.PlayerId]++;
                        if (lastShooter == me && !lastShotWasFinal)
                            MyHits++;
                    }
                    break;

                case GameEventType.PileTaken when e.Partial:
                    // Salah tembak: hanya kartu yang dibuka berpindah ke penembak; sisa tumpukan tetap di tengah.
                    if (e.PlayerId != me)
                    {
                        foreach (var card in lastRevealed)
                            AddUnique(KnownInHand[e.PlayerId], card);
                    }
                    MyCardsInPile.RemoveAll(c => lastRevealed.Contains(c));
                    lastRevealed.Clear();
                    break;

                case GameEventType.PileTaken:
                    if (e.PlayerId != me)
                    {
                        var known = KnownInHand[e.PlayerId];
                        foreach (var card in MyCardsInPile)
                            AddUnique(known, card);
                        foreach (var card in lastRevealed)
                            AddUnique(known, card);
                    }
                    MyCardsInPile.Clear();
                    lastRevealed.Clear();
                    break;

                case GameEventType.PileBurned:
                    MyBurnedCards.AddRange(MyCardsInPile);
                    MyCardsInPile.Clear();
                    lastRevealed.Clear();
                    break;
            }

            if (e.HandCounts != null)
                TrimToHandCounts(e.HandCounts);
        }

        void ClearPeek()
        {
            PeekedCards = null;
            PeekedTarget = -1;
        }

        public static int CountMatches(IEnumerable<Card> cards, Element claim)
        {
            int n = 0;
            foreach (var card in cards)
            {
                if (card.Matches(claim))
                    n++;
            }
            return n;
        }

        void ApplyPendingForget()
        {
            if (pendingPlayer < 0)
                return;
            int player = pendingPlayer;
            pendingPlayer = -1;
            ForgetPlayed(player, pendingCount, pendingClaim);
        }

        /// <summary>Lawan menaruh kartu: anggap ia memakai kartu cocok yang aku tahu ada di tangannya.</summary>
        void ForgetPlayed(int playerId, int count, Element claim)
        {
            var known = KnownInHand[playerId];
            int removed = 0;
            for (int pass = 0; pass < 2 && removed < count; pass++)
            {
                for (int i = known.Count - 1; i >= 0 && removed < count; i--)
                {
                    bool exact = !known[i].IsWild && known[i].Element == claim;
                    bool wild = known[i].IsWild;
                    if ((pass == 0 && exact) || (pass == 1 && wild))
                    {
                        known.RemoveAt(i);
                        removed++;
                    }
                }
            }
        }

        void TrimToHandCounts(int[] handCounts)
        {
            for (int p = 0; p < KnownInHand.Length && p < handCounts.Length; p++)
            {
                var known = KnownInHand[p];
                int limit = handCounts[p] + (p == pendingPlayer ? pendingCount : 0);
                if (known.Count > limit)
                    known.RemoveRange(limit, known.Count - limit);
            }
        }

        static void AddUnique(List<Card> list, Card card)
        {
            if (!list.Contains(card))
                list.Add(card);
        }
    }
}
