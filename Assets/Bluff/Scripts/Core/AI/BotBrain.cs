using System;
using System.Collections.Generic;
using System.Linq;

namespace Bluff.Core.AI
{
    public enum BotDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
    }

    public struct TurnDecision
    {
        public bool Pass;
        public List<int> CardIds;
        public Element Claim;

        /// <summary>Pasang Tameng pada taruhan ini.</summary>
        public bool UseShield;

        public static TurnDecision MakePass() => new TurnDecision { Pass = true };

        public static TurnDecision MakePlay(IEnumerable<Card> cards, Element claim)
        {
            return new TurnDecision { CardIds = cards.Select(c => c.Id).ToList(), Claim = claim };
        }
    }

    public struct ShootDecision
    {
        public bool Shoot;

        /// <summary>Waktu reaksi yang disarankan (detik) sejak jendela tembak dibuka.</summary>
        public double DelaySeconds;

        /// <summary>Perkiraan bot bahwa taruhan itu bohong (0..1), untuk debug.</summary>
        public double LieProbability;

        /// <summary>Pakai Mata Elang dulu (intip), lalu panggil DecideShoot lagi dengan informasi barunya.</summary>
        public bool UsePeek;
    }

    /// <summary>
    /// Otak pemain komputer. Memutuskan taruhan/pass dan kapan menembak hanya dari <see cref="GameSnapshot"/>
    /// miliknya dan <see cref="BotMemory"/>. Keputusan berbasis untung-rugi (expected value) dalam satuan kartu;
    /// tingkat kesulitan mengatur seberapa teliti ia menghitung kartu dan seberapa disiplin ia mengikuti hitungannya.
    /// </summary>
    public sealed class BotBrain
    {
        readonly Random rng;

        public BotBrain(int playerId, int playerCount, BotDifficulty difficulty, int seed)
        {
            PlayerId = playerId;
            Difficulty = difficulty;
            rng = new Random(seed);
            Memory = new BotMemory(playerId, playerCount);
            Bluffiness = 0.3 + rng.NextDouble() * 0.4;
            double baseThreshold = difficulty == BotDifficulty.Easy ? 0.30 : difficulty == BotDifficulty.Normal ? 0.36 : 0.38;
            ShootThreshold = baseThreshold + (rng.NextDouble() - 0.5) * 0.06;
        }

        public int PlayerId { get; }
        public BotDifficulty Difficulty { get; }
        public BotMemory Memory { get; }

        /// <summary>Kepribadian: 0.3 = hati-hati, 0.7 = suka nekat bohong.</summary>
        public double Bluffiness { get; }

        /// <summary>Seberapa yakin (peluang bohong) bot ini harus sebelum menembak.</summary>
        public double ShootThreshold { get; }

        /// <summary>Berikan setiap event yang sudah di-redact untuk bot ini.</summary>
        public void Observe(GameEvent redactedEvent) => Memory.Observe(redactedEvent);

        // ================================================================== giliran

        public TurnDecision DecideTurn(GameSnapshot s)
        {
            if (s.MyHand.Count == 0)
                return TurnDecision.MakePass();
            var decision = s.HasRoundElement ? Follow(s) : Lead(s);
            if (!decision.Pass)
                decision.UseShield = ShouldShield(s, decision);
            return decision;
        }

        // ================================================================== kartu kuasa

        /// <summary>
        /// Dipanggil di awal giliran bot, sebelum <see cref="DecideTurn"/>. Mengembalikan permintaan kuasa
        /// (Bakar / Badai / Hadiah) atau null. Setelah kuasa dipakai, panggil DecideTurn dengan snapshot baru.
        /// </summary>
        public PowerRequest DecideTurnPower(GameSnapshot s)
        {
            if (!s.HasPower || !s.IsMyTurn)
                return null;
            var hand = s.MyHand;
            switch (s.MyPower)
            {
                case PowerType.Bakar:
                {
                    if (hand.Count < 3)
                        return null;
                    double p = hand.Count <= 6 ? 0.8 : hand.Count <= 10 ? 0.35 : 0.1;
                    if (Difficulty == BotDifficulty.Easy)
                        p = 0.3;
                    if (rng.NextDouble() >= p)
                        return null;
                    var cards = PickPowerJunk(s, Math.Min(Powers.MaxPowerCards, hand.Count - 1));
                    if (cards.Count == 0)
                        return null;
                    var request = PowerRequest.Of(PowerType.Bakar);
                    request.CardIds = cards.Select(c => c.Id).ToList();
                    return request;
                }

                case PowerType.Badai:
                {
                    if (!s.HasRoundElement)
                        return null;
                    bool lacking = !hand.Any(c => c.Matches(s.RoundElement));
                    bool worth = lacking && (s.PileCount >= 4 || (Difficulty == BotDifficulty.Easy && rng.NextDouble() < 0.3));
                    return worth ? PowerRequest.Of(PowerType.Badai) : null;
                }

                case PowerType.Hadiah:
                {
                    if (hand.Count < 3)
                        return null;
                    int target = -1;
                    for (int p = 0; p < s.PlayerCount; p++)
                    {
                        if (p != PlayerId && (target < 0 || s.HandCounts[p] < s.HandCounts[target]))
                            target = p;
                    }
                    if (Difficulty == BotDifficulty.Easy)
                    {
                        do
                            target = rng.Next(s.PlayerCount);
                        while (target == PlayerId);
                    }
                    bool worth = s.HandCounts[target] <= 4 || hand.Count <= 5 || rng.NextDouble() < 0.08;
                    if (!worth)
                        return null;
                    var cards = PickPowerJunk(s, Math.Min(Powers.MaxPowerCards, hand.Count - 1));
                    if (cards.Count == 0)
                        return null;
                    var request = PowerRequest.Of(PowerType.Hadiah);
                    request.CardIds = cards.Select(c => c.Id).ToList();
                    request.TargetPlayer = target;
                    return request;
                }

                default:
                    return null;
            }
        }

        /// <summary>Tameng paling berharga untuk melindungi bohong yang mahal kalau ketahuan.</summary>
        bool ShouldShield(GameSnapshot s, TurnDecision decision)
        {
            if (s.MyPower != PowerType.Tameng || s.MyPowerUsed || decision.CardIds == null)
                return false;
            if (s.MyHand.Count - decision.CardIds.Count < 1)
                return false;
            bool lie = s.MyHand.Where(c => decision.CardIds.Contains(c.Id)).Any(c => !c.Matches(decision.Claim));
            if (!lie)
                return false;
            int risk = s.PileCount + decision.CardIds.Count;
            return risk >= 4 || decision.CardIds.Count >= 2 || s.MyHand.Count - decision.CardIds.Count <= 2;
        }

        /// <summary>Kartu yang paling tidak berguna untuk dibuang lewat Bakar / Hadiah (bukan Topeng, bukan elemen ronde).</summary>
        List<Card> PickPowerJunk(GameSnapshot s, int count)
        {
            var groupSize = new Dictionary<Element, int>();
            foreach (var card in s.MyHand)
            {
                groupSize.TryGetValue(card.Element, out int n);
                groupSize[card.Element] = n + 1;
            }
            return s.MyHand
                .Where(c => !c.IsWild && !(s.HasRoundElement && c.Element == s.RoundElement))
                .OrderBy(c => groupSize[c.Element])
                .ThenBy(c => rng.Next())
                .Take(count)
                .ToList();
        }

        TurnDecision Lead(GameSnapshot s)
        {
            var hand = s.MyHand;
            int max = s.Config.MaxCardsPerPlay;
            var groups = GroupByElement(hand);
            var wilds = hand.Where(c => c.IsWild).ToList();
            bool safe = OpponentsWithBullets(s) == 0;

            // Bisa habis dalam satu taruhan?
            if (hand.Count <= max)
            {
                if (groups.Count == 0)
                    return TurnDecision.MakePlay(hand, RandomElement());
                if (groups.Count == 1 || safe)
                    return TurnDecision.MakePlay(hand, groups[0][0].Element);
            }

            // Hanya Topeng tersisa (lebih banyak dari batas per taruhan).
            if (groups.Count == 0)
                return TurnDecision.MakePlay(wilds.Take(max), RandomElement());

            var element = groups[0][0].Element;
            var chosen = groups[0].Take(max).ToList();

            // Mendekati akhir: pakai Topeng untuk menambah taruhan jujur.
            if (chosen.Count < max && hand.Count <= max * 2)
                chosen.AddRange(wilds.Take(max - chosen.Count));

            TryAddBluffCard(s, hand, element, chosen);
            return TurnDecision.MakePlay(chosen, element);
        }

        TurnDecision Follow(GameSnapshot s)
        {
            var hand = s.MyHand;
            int max = s.Config.MaxCardsPerPlay;
            var element = s.RoundElement;
            var exact = hand.Where(c => !c.IsWild && c.Element == element).ToList();
            var wilds = hand.Where(c => c.IsWild).ToList();
            var others = hand.Where(c => !c.Matches(element)).ToList();
            bool safe = OpponentsWithBullets(s) == 0;

            if (hand.Count <= max && (others.Count == 0 || safe))
                return TurnDecision.MakePlay(hand, element);

            if (exact.Count > 0)
            {
                var chosen = exact.Take(max).ToList();
                if (chosen.Count < max && hand.Count <= max * 2)
                    chosen.AddRange(wilds.Take(max - chosen.Count));
                TryAddBluffCard(s, hand, element, chosen);
                return TurnDecision.MakePlay(chosen, element);
            }

            // Tidak punya elemen ronde, tapi punya Topeng dan sudah dekat akhir.
            if (wilds.Count > 0 && hand.Count <= max * 2)
                return TurnDecision.MakePlay(wilds.Take(max), element);

            // Tidak punya: bohong 1 kartu, atau pass?
            double pc = CatchChance(s, 1, hand.Count - 1);
            double ev = (1.0 - pc) * 1.0 - pc * (s.PileCount + 0.5);
            if (others.Count > 0 && WantsToBluff(s, ev))
            {
                int n = safe && others.Count >= 2 && rng.NextDouble() < 0.5 ? 2 : 1;
                return TurnDecision.MakePlay(PickJunk(hand, element, new List<Card>(), n), element);
            }
            return TurnDecision.MakePass();
        }

        /// <summary>Selipkan 1 kartu "sampah" ke taruhan jujur kalau untungnya sepadan dengan risikonya.</summary>
        void TryAddBluffCard(GameSnapshot s, List<Card> hand, Element element, List<Card> chosen)
        {
            int max = s.Config.MaxCardsPerPlay;
            if (chosen.Count >= max || hand.Count <= chosen.Count)
                return;
            if (!hand.Any(c => !c.Matches(element) && !chosen.Contains(c)))
                return;
            int handAfter = hand.Count - chosen.Count - 1;
            double pc = CatchChance(s, chosen.Count + 1, handAfter);
            // Kalau ketahuan: tumpukan + semua kartu taruhan ini kembali ke tangan.
            double ev = (1.0 - pc) * 1.0 - pc * (s.PileCount + chosen.Count + 0.5);
            if (WantsToBluff(s, ev - 0.15))
                chosen.AddRange(PickJunk(hand, element, chosen, 1));
        }

        bool WantsToBluff(GameSnapshot s, double ev)
        {
            double personality = (Bluffiness - 0.5) * 0.4;
            // Cermin = satu kali "kebal ketahuan": lebih berani bohong. Tameng juga melindungi satu taruhan.
            if (s.HasPower && (s.MyPower == PowerType.Cermin || s.MyPower == PowerType.Tameng) && s.MyHand.Count > 2)
                ev += 0.5;
            switch (Difficulty)
            {
                case BotDifficulty.Easy:
                    // Ceroboh: separuh waktu nekat tanpa banyak hitung.
                    return rng.NextDouble() < 0.5 ? ev > -0.8 : ev > 0.5 - personality;
                case BotDifficulty.Normal:
                    return ev > 0.1 - personality && rng.NextDouble() < 0.85;
                default:
                    return ev > 0.0 - personality && rng.NextDouble() < 0.92;
            }
        }

        /// <summary>Perkiraan peluang taruhanku (sebanyak <paramref name="claimCount"/> kartu) ditembak lawan.</summary>
        double CatchChance(GameSnapshot s, int claimCount, int handAfter)
        {
            int armed = OpponentsWithBullets(s);
            if (armed == 0)
                return 0.0;
            if (handAfter <= 0)
                return 0.97; // taruhan terakhir hampir pasti ditembak
            double shotRate = (Memory.ShotsSeen + 1.0) / (Memory.PlaysSeen + 6.0);
            double p = shotRate * armed / (s.PlayerCount - 1);
            p *= 1.0 + 0.35 * (claimCount - 1);
            if (handAfter <= 2)
                p *= 2.0;
            else if (handAfter <= 4)
                p *= 1.4;
            if (Difficulty != BotDifficulty.Easy && ClaimLooksCrowded(s, claimCount))
                p *= 1.8;
            return Clamp(p, 0.03, 0.95);
        }

        /// <summary>Total klaim ronde ini sudah banyak: bohong di sini gampang ketahuan oleh penghitung kartu.</summary>
        static bool ClaimLooksCrowded(GameSnapshot s, int extra)
        {
            int claimed = 0;
            foreach (var c in s.RoundClaims)
                claimed += c.Count;
            return claimed + extra >= s.Config.MatchesPerElement - 3;
        }

        // ================================================================== menembak

        public ShootDecision DecideShoot(GameSnapshot s)
        {
            if (!s.CanShoot)
                return new ShootDecision();

            int target = s.LastPlayerId;
            bool wasLeader = s.RoundClaims.Count == 1;
            double pLie = EstimateLieProbability(s, target, s.LastPlayCount, s.LastPlayClaim, wasLeader);

            double noise = Difficulty == BotDifficulty.Easy ? 0.25 : Difficulty == BotDifficulty.Normal ? 0.08 : 0.0;
            double perceived = Clamp(pLie + (rng.NextDouble() * 2.0 - 1.0) * noise, 0.0, 1.0);

            int targetLeft = s.HandCounts[target];
            bool shoot;
            double confidence;
            if (targetLeft == 0)
            {
                // Kalau tidak ditembak, dia langsung menang. Tidak ada ruginya menembak.
                shoot = Difficulty != BotDifficulty.Easy || rng.NextDouble() < 0.7;
                confidence = 1.0;
            }
            else
            {
                // Ambang curiga ala manusia: makin genting keadaannya, makin cepat tangan menarik pelatuk.
                double threshold = ShootThreshold;
                if (Difficulty != BotDifficulty.Easy)
                {
                    // Belajar dari tembakan sendiri: sering meleset -> lebih hati-hati, sering jitu -> lebih berani.
                    if (Memory.MyShots >= 3)
                        threshold += Clamp(0.4 - Memory.MyHits / (double)Memory.MyShots, -0.2, 0.4) * 0.3;
                    // Pemain yang sudah 2x ketahuan bohong lebih dicurigai.
                    if (Memory.TimesCaughtLying[target] >= 2)
                        threshold -= 0.05;
                }
                if (s.Bullets[PlayerId] == 1)
                    threshold += s.Config.RefundBulletOnHit ? 0.06 : 0.12;
                if (s.Config.WrongShotTakesWholePile)
                    threshold += Math.Min(0.2, s.PileCount / 40.0);
                if (targetLeft <= 2)
                    threshold -= 0.12;
                else if (targetLeft <= 4)
                    threshold -= 0.06;
                if (IsClosestOpponent(s, target))
                    threshold -= 0.03;
                if (s.MyHand.Count <= 3)
                    threshold += 0.08;

                shoot = perceived >= threshold;
                if (!shoot && Difficulty == BotDifficulty.Easy && s.PileCount <= 3)
                    shoot = rng.NextDouble() < 0.04; // tembakan iseng
                confidence = Clamp((perceived - threshold) * 3.0, 0.0, 1.0);
            }

            // Mata Elang: ragu-ragu di taruhan yang penting? Intip dulu, putuskan sesudahnya.
            bool peeked = Memory.PeekedCards != null && Memory.PeekedTarget == target;
            if (!peeked && s.HasPower && s.MyPower == PowerType.MataElang && targetLeft > 0 &&
                pLie >= 0.2 && pLie <= 0.75 && (s.PileCount >= 3 || targetLeft <= 3))
            {
                return new ShootDecision { UsePeek = true, DelaySeconds = 0.4 + rng.NextDouble() * 0.4, LieProbability = pLie };
            }

            double delay = (Difficulty == BotDifficulty.Easy ? 1.0 : 0.6) + (1.0 - confidence) * 1.1 + rng.NextDouble() * 0.6;
            if (targetLeft == 0)
                delay *= 0.6;
            if (peeked)
                delay = 0.5 + rng.NextDouble() * 0.5;
            return new ShootDecision { Shoot = shoot, DelaySeconds = delay, LieProbability = pLie };
        }

        /// <summary>Peluang (0..1) bahwa taruhan <paramref name="playerId"/> barusan itu bohong, menurut bot ini.</summary>
        public double EstimateLieProbability(GameSnapshot s, int playerId, int count, Element claim, bool wasLeader)
        {
            // Sudah diintip dengan Mata Elang: tidak perlu menebak.
            if (Memory.PeekedCards != null && Memory.PeekedTarget == playerId)
                return Memory.PeekedCards.TrueForAll(c => c.Matches(claim)) ? 0.0 : 1.0;
            var x = LieFeatures(s, playerId, count, claim, wasLeader, out bool impossible);
            if (impossible)
                return 1.0;
            if (Difficulty == BotDifficulty.Easy)
            {
                // Firasat kasar: klaim besar dan pemain yang hampir habis lebih mencurigakan.
                double guess = 0.12 + 0.12 * (count - 1);
                if (s.HandCounts[playerId] <= 3)
                    guess += 0.15;
                return Clamp(guess, 0.0, 1.0);
            }
            return LieModel.Predict(x);
        }

        /// <summary>
        /// Fitur untuk <see cref="LieModel"/> dari sudut pandang bot ini. Semakin sulit bot-nya, semakin banyak
        /// informasi yang ia pakai (kartu sendiri di tumpukan, kartu lawan yang pernah terlihat, klaim ronde ini).
        /// <paramref name="impossible"/> = klaim melebihi semua kartu cocok yang mungkin dipegang pemain itu.
        /// </summary>
        public double[] LieFeatures(GameSnapshot s, int playerId, int count, Element claim, bool wasLeader, out bool impossible)
        {
            var cfg = s.Config;
            bool useOwnHistory = Difficulty != BotDifficulty.Easy;
            bool useOpponentMemory = Difficulty == BotDifficulty.Hard;

            int knownCards = s.MyHand.Count;
            int knownMatches = BotMemory.CountMatches(s.MyHand, claim);
            if (useOwnHistory)
            {
                knownCards += Memory.MyCardsInPile.Count + Memory.MyBurnedCards.Count;
                knownMatches += BotMemory.CountMatches(Memory.MyCardsInPile, claim) + BotMemory.CountMatches(Memory.MyBurnedCards, claim);
            }

            int targetKnown = 0;
            int targetKnownMatches = 0;
            if (useOpponentMemory)
            {
                for (int p = 0; p < s.PlayerCount; p++)
                {
                    if (p == PlayerId)
                        continue;
                    var known = Memory.KnownInHand[p];
                    int matches = BotMemory.CountMatches(known, claim);
                    if (p == playerId)
                    {
                        targetKnown += known.Count;
                        targetKnownMatches += matches;
                    }
                    else
                    {
                        knownCards += known.Count;
                        knownMatches += matches;
                    }
                }
            }

            int unknownPool = Math.Max(0, cfg.TotalCards - knownCards - targetKnown);
            int available = Math.Max(0, cfg.MatchesPerElement - knownMatches);
            int unknownMatches = Math.Max(0, available - targetKnownMatches);
            impossible = count > available;

            // Klaim pemain lain sebelumnya di ronde ini (informasi publik). Klaimku sendiri sudah dihitung lewat kartuku.
            int prior = 0;
            int priorPlays = 0;
            if (useOwnHistory)
            {
                for (int i = 0; i < s.RoundClaims.Count - 1; i++)
                {
                    priorPlays++;
                    if (s.RoundClaims[i].PlayerId != PlayerId)
                        prior += s.RoundClaims[i].Count;
                }
            }

            int handBefore = s.HandCounts[playerId] + count;
            int unknownInTargetHand = Math.Max(0, handBefore - targetKnown);
            int need = count - targetKnownMatches;
            double pHave = need <= 0
                ? 1.0
                : Probability.HypergeometricAtLeast(unknownPool, unknownMatches, unknownInTargetHand, need);

            double lieHistory = useOwnHistory
                ? (Memory.TimesCaughtLying[playerId] + 1.0) / (Memory.TimesRevealed[playerId] + 4.0)
                : 0.25;
            int armed = 0;
            for (int p = 0; p < s.PlayerCount; p++)
            {
                if (p != playerId && s.Bullets[p] > 0)
                    armed++;
            }

            double leader = wasLeader ? 1.0 : 0.0;
            return new[]
            {
                1.0,
                pHave,
                leader,
                leader * pHave,
                count == 2 ? 1.0 : 0.0,
                count == 3 ? 1.0 : 0.0,
                handBefore / 13.0,
                (prior + count) / (double)Math.Max(1, available),
                lieHistory,
                Math.Max(0, s.PileCount - count) / 10.0,
                armed / (double)Math.Max(1, s.PlayerCount - 1),
                Math.Min(priorPlays, 6) / 6.0,
                handBefore <= 3 ? 1.0 : 0.0,
                count / (double)Math.Max(1, handBefore),
            };
        }

        // ================================================================== helpers

        int OpponentsWithBullets(GameSnapshot s)
        {
            int n = 0;
            for (int p = 0; p < s.PlayerCount; p++)
            {
                if (p != PlayerId && s.Bullets[p] > 0)
                    n++;
            }
            return n;
        }

        bool IsClosestOpponent(GameSnapshot s, int target)
        {
            for (int p = 0; p < s.PlayerCount; p++)
            {
                if (p != PlayerId && p != target && s.HandCounts[p] < s.HandCounts[target])
                    return false;
            }
            return true;
        }

        List<List<Card>> GroupByElement(IEnumerable<Card> hand)
        {
            return hand.Where(c => !c.IsWild)
                .GroupBy(c => c.Element)
                .Select(g => g.ToList())
                .OrderByDescending(g => g.Count)
                .ThenBy(g => rng.Next())
                .ToList();
        }

        /// <summary>Kartu untuk dibuang lewat bohong: bukan Topeng, tidak cocok klaim, dari kelompok paling kecil.</summary>
        List<Card> PickJunk(List<Card> hand, Element claim, List<Card> exclude, int count)
        {
            var groupSize = new Dictionary<Element, int>();
            foreach (var card in hand)
            {
                groupSize.TryGetValue(card.Element, out int n);
                groupSize[card.Element] = n + 1;
            }
            return hand.Where(c => !c.Matches(claim) && !exclude.Contains(c))
                .OrderBy(c => groupSize[c.Element])
                .ThenBy(c => rng.Next())
                .Take(count)
                .ToList();
        }

        Element RandomElement() => Elements.Claimable[rng.Next(Elements.ClaimableCount)];

        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
