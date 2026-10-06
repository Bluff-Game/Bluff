using System;
using System.Collections.Generic;

namespace Bluff.Core
{
    public enum GamePhase
    {
        NotStarted,

        /// <summary>Menunggu pemain yang sedang giliran untuk menaruh kartu atau Pass.</summary>
        AwaitingPlay,

        /// <summary>Taruhan terakhir boleh ditembak (BLUFF!) oleh pemain lain yang masih punya peluru.</summary>
        ShootWindow,

        GameOver,
    }

    /// <summary>
    /// Aturan Bluff: Topeng &amp; Elemen, tanpa ketergantungan ke Unity. Ini satu-satunya sumber kebenaran
    /// permainan: UI, bot, dan nanti host online hanya memanggil Try*/CloseShootWindow lalu bereaksi ke event.
    /// Jangan memanggil aksi engine dari dalam handler <see cref="EventRaised"/>; antrekan dulu.
    /// </summary>
    public sealed class GameEngine
    {
        readonly GameConfig config;
        readonly List<PlayerState> players = new List<PlayerState>();
        readonly List<Card> pile = new List<Card>();
        readonly List<Card> burned = new List<Card>();
        readonly List<Play> roundPlays = new List<Play>();
        readonly List<GameEvent> log = new List<GameEvent>();
        int sequence;

        public GameEngine(GameConfig config, IList<string> names, IList<bool> isBot = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            config.Validate();
            if (names == null || names.Count != config.PlayerCount)
                throw new ArgumentException("Jumlah nama harus sama dengan PlayerCount.", nameof(names));

            this.config = config.Clone();
            for (int i = 0; i < names.Count; i++)
                players.Add(new PlayerState(i, names[i], isBot != null && isBot[i]));
        }

        public event Action<GameEvent> EventRaised;

        public GameConfig Config => config;
        public IReadOnlyList<PlayerState> Players => players;
        public int PlayerCount => players.Count;
        public GamePhase Phase { get; private set; } = GamePhase.NotStarted;
        public int CurrentPlayer { get; private set; } = -1;
        public int RoundLeader { get; private set; } = -1;
        public bool HasRoundElement { get; private set; }
        public Element RoundElement { get; private set; }
        public Play LastPlay { get; private set; }
        public IReadOnlyList<Play> RoundPlays => roundPlays;
        public IReadOnlyList<Card> Pile => pile;
        public int PileCount => pile.Count;
        public int BurnedCount => burned.Count;
        public int ConsecutivePasses { get; private set; }
        public int Winner { get; private set; } = -1;
        public int TurnCount { get; private set; }
        public IReadOnlyList<GameEvent> Log => log;

        public int NextPlayer(int playerId) => (playerId + 1) % players.Count;

        // ------------------------------------------------------------------ setup

        /// <summary>Kocok dek dengan seed tertentu, bagi kartu dan Kartu Kuasa, lalu undi pemain pertama.</summary>
        public void Start(int seed)
        {
            var rng = new Random(seed);
            var deck = Deck.Create(config);
            Deck.Shuffle(deck, rng);
            var powers = new List<PowerType>(Powers.All);
            Deck.Shuffle(powers, rng);
            StartWithDeck(deck, rng.Next(players.Count), powers);
        }

        /// <summary>
        /// Mulai dengan urutan dek tertentu (untuk tes atau host online). Kartu ke-i jatuh ke pemain i % PlayerCount;
        /// sisa yang tidak bisa dibagi rata langsung hangus. Kuasa ke-i untuk pemain ke-i (kalau kuasa aktif).
        /// </summary>
        public void StartWithDeck(IList<Card> deck, int firstPlayer, IList<PowerType> powers = null)
        {
            if (Phase != GamePhase.NotStarted)
                throw new InvalidOperationException("Permainan sudah dimulai.");
            if (firstPlayer < 0 || firstPlayer >= players.Count)
                throw new ArgumentOutOfRangeException(nameof(firstPlayer));

            int perPlayer = deck.Count / players.Count;
            int dealt = perPlayer * players.Count;
            for (int i = 0; i < dealt; i++)
                players[i % players.Count].hand.Add(deck[i]);
            for (int i = dealt; i < deck.Count; i++)
                burned.Add(deck[i]);
            foreach (var p in players)
                p.Bullets = config.StartingBullets;

            if (config.EnablePowers)
            {
                var source = powers != null ? new List<PowerType>(powers) : new List<PowerType>(Powers.All);
                for (int i = 0; i < players.Count; i++)
                    players[i].Power = source.Count > 0 ? source[i % source.Count] : PowerType.None;
            }

            Emit(new GameEvent { Type = GameEventType.GameStarted, PlayerId = firstPlayer, Count = perPlayer });
            foreach (var p in players)
            {
                Emit(new GameEvent
                {
                    Type = GameEventType.HandDealt,
                    PlayerId = p.Id,
                    Count = p.HandCount,
                    Cards = new List<Card>(p.hand),
                });
            }
            if (config.EnablePowers)
            {
                foreach (var p in players)
                    Emit(new GameEvent { Type = GameEventType.PowerDealt, PlayerId = p.Id, Power = p.Power });
            }
            BeginRound(firstPlayer, RoundStartReason.GameStart);
        }

        // ------------------------------------------------------------------ actions

        public ActionResult TryPlay(int playerId, IList<int> cardIds, Element claim) => TryPlay(playerId, cardIds, claim, false);

        /// <param name="useShield">Pakai Tameng: taruhan ini tidak bisa ditembak (bukan taruhan terakhir).</param>
        public ActionResult TryPlay(int playerId, IList<int> cardIds, Element claim, bool useShield)
        {
            if (Phase != GamePhase.AwaitingPlay)
                return ActionResult.Fail("Belum saatnya menaruh kartu.");
            if (playerId != CurrentPlayer)
                return ActionResult.Fail("Bukan giliranmu.");
            if (cardIds == null || cardIds.Count < 1)
                return ActionResult.Fail("Pilih minimal 1 kartu.");
            if (cardIds.Count > config.MaxCardsPerPlay)
                return ActionResult.Fail("Maksimal " + config.MaxCardsPerPlay + " kartu per giliran.");
            if (!claim.IsClaimable())
                return ActionResult.Fail("Klaim harus salah satu dari 6 elemen.");
            if (HasRoundElement && claim != RoundElement)
                return ActionResult.Fail("Ronde ini wajib klaim " + RoundElement + ".");

            var player = players[playerId];
            var result = CollectCards(player, cardIds, out var chosen);
            if (!result.Ok)
                return result;
            if (useShield)
            {
                if (player.Power != PowerType.Tameng || player.PowerUsed)
                    return ActionResult.Fail("Kamu tidak punya Tameng yang bisa dipakai.");
                if (player.HandCount - chosen.Count < 1)
                    return ActionResult.Fail("Tameng tidak bisa dipakai untuk taruhan terakhir.");
            }

            foreach (var card in chosen)
                player.hand.RemoveAt(IndexOfCard(player.hand, card.Id));
            pile.AddRange(chosen);

            if (!HasRoundElement)
            {
                HasRoundElement = true;
                RoundElement = claim;
            }
            if (useShield)
            {
                player.PowerUsed = true;
                Emit(new GameEvent { Type = GameEventType.PowerUsed, PlayerId = playerId, Power = PowerType.Tameng, Count = chosen.Count });
            }
            var play = new Play(playerId, claim, chosen.AsReadOnly(), useShield);
            LastPlay = play;
            roundPlays.Add(play);
            ConsecutivePasses = 0;

            Emit(new GameEvent
            {
                Type = GameEventType.CardsPlayed,
                PlayerId = playerId,
                Count = chosen.Count,
                Claim = claim,
                Shielded = useShield,
                Cards = new List<Card>(chosen),
            });

            Phase = GamePhase.ShootWindow;
            Emit(new GameEvent { Type = GameEventType.ShootWindowOpened, PlayerId = playerId, Count = chosen.Count, Claim = claim, Shielded = useShield });

            // Tidak ada yang bisa menembak (Tameng / peluru habis): jendela langsung ditutup.
            if (!AnyEligibleShooter())
                CloseWindowInternal();
            return ActionResult.Success;
        }

        public ActionResult TryPass(int playerId)
        {
            if (Phase != GamePhase.AwaitingPlay)
                return ActionResult.Fail("Belum saatnya memilih Pass.");
            if (playerId != CurrentPlayer)
                return ActionResult.Fail("Bukan giliranmu.");
            if (!HasRoundElement)
                return ActionResult.Fail("Pemimpin ronde wajib menaruh kartu.");

            ConsecutivePasses++;
            Emit(new GameEvent { Type = GameEventType.Passed, PlayerId = playerId });

            if (ConsecutivePasses >= players.Count - 1)
            {
                // Semua pemain lain pass: tumpukan hangus, penaruh terakhir memimpin ronde baru.
                int leader = LastPlay.PlayerId;
                BurnPile(leader);
                BeginRound(leader, RoundStartReason.AfterBurn);
            }
            else
            {
                StartTurn(NextPlayer(playerId));
            }
            return ActionResult.Success;
        }

        public bool CanShoot(int playerId)
        {
            return Phase == GamePhase.ShootWindow &&
                   LastPlay != null &&
                   !LastPlay.Shielded &&
                   playerId >= 0 && playerId < players.Count &&
                   playerId != LastPlay.PlayerId &&
                   players[playerId].Bullets > 0;
        }

        public bool AnyEligibleShooter()
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (CanShoot(i))
                    return true;
            }
            return false;
        }

        public ActionResult TryShoot(int shooterId)
        {
            if (Phase != GamePhase.ShootWindow || LastPlay == null)
                return ActionResult.Fail("Tidak ada taruhan yang bisa ditembak sekarang.");
            if (shooterId < 0 || shooterId >= players.Count)
                return ActionResult.Fail("Pemain tidak dikenal.");
            if (shooterId == LastPlay.PlayerId)
                return ActionResult.Fail("Tidak bisa menembak taruhan sendiri.");
            if (LastPlay.Shielded)
                return ActionResult.Fail("Taruhan ini dilindungi Tameng.");
            if (players[shooterId].Bullets <= 0)
                return ActionResult.Fail("Pelurumu sudah habis.");

            var shooter = players[shooterId];
            var play = LastPlay;
            var target = players[play.PlayerId];

            shooter.Bullets--;
            Emit(new GameEvent { Type = GameEventType.ShotFired, PlayerId = shooterId, OtherPlayerId = target.Id, Count = play.Count, Claim = play.Claim });

            bool honest = play.IsHonest;
            Emit(new GameEvent
            {
                Type = GameEventType.CardsRevealed,
                PlayerId = target.Id,
                OtherPlayerId = shooterId,
                Count = play.Count,
                Claim = play.Claim,
                Honest = honest,
                Cards = new List<Card>(play.Cards),
            });

            // Cermin: ketahuan bohong, tapi tembakannya memantul ke penembak.
            if (!honest && target.Power == PowerType.Cermin && !target.PowerUsed && target.HandCount > 0)
            {
                target.PowerUsed = true;
                Emit(new GameEvent { Type = GameEventType.PowerUsed, PlayerId = target.Id, OtherPlayerId = shooterId, Power = PowerType.Cermin, Count = pile.Count });
                var reflected = new List<Card>(pile);
                shooter.hand.AddRange(pile);
                pile.Clear();
                Emit(new GameEvent { Type = GameEventType.PileTaken, PlayerId = shooterId, OtherPlayerId = target.Id, Count = reflected.Count, Cards = reflected });
                BeginRound(target.Id, RoundStartReason.AfterChallenge);
                return ActionResult.Success;
            }

            if (!honest && config.RefundBulletOnHit)
            {
                shooter.Bullets++;
                Emit(new GameEvent { Type = GameEventType.BulletRefunded, PlayerId = shooterId });
            }

            if (honest && !config.WrongShotTakesWholePile)
            {
                // Salah tembak: penembak menelan kartu yang ia tuduh, ronde berlanjut seperti biasa.
                var accused = new List<Card>(play.Cards);
                foreach (var card in accused)
                    pile.RemoveAt(IndexOfCard(pile, card.Id));
                shooter.hand.AddRange(accused);
                Emit(new GameEvent
                {
                    Type = GameEventType.PileTaken,
                    PlayerId = shooterId,
                    OtherPlayerId = target.Id,
                    Count = accused.Count,
                    Cards = accused,
                    Partial = true,
                });
                if (target.HandCount == 0)
                    EndGame(target.Id);
                else
                    StartTurn(NextPlayer(target.Id));
                return ActionResult.Success;
            }

            int loserId = honest ? shooterId : target.Id;
            int winnerId = honest ? target.Id : shooterId;
            var taken = new List<Card>(pile);
            players[loserId].hand.AddRange(pile);
            pile.Clear();
            Emit(new GameEvent { Type = GameEventType.PileTaken, PlayerId = loserId, OtherPlayerId = winnerId, Count = taken.Count, Cards = taken });

            if (honest && target.HandCount == 0)
            {
                EndGame(target.Id);
                return ActionResult.Success;
            }
            BeginRound(winnerId, RoundStartReason.AfterChallenge);
            return ActionResult.Success;
        }

        /// <summary>Waktu jendela tembak habis tanpa ada yang menembak.</summary>
        public ActionResult CloseShootWindow()
        {
            if (Phase != GamePhase.ShootWindow)
                return ActionResult.Fail("Jendela tembak tidak sedang terbuka.");
            CloseWindowInternal();
            return ActionResult.Success;
        }

        // ------------------------------------------------------------------ kartu kuasa

        /// <summary>
        /// Apakah kuasa pemain ini bisa dipakai SEKARANG (hanya cek waktu, belum cek kartu/target).
        /// Mengembalikan null kalau bisa, atau alasan kalau tidak. Tameng dipakai lewat TryPlay(useShield).
        /// </summary>
        public string PowerBlockReason(int playerId)
        {
            if (playerId < 0 || playerId >= players.Count)
                return "Pemain tidak dikenal.";
            var player = players[playerId];
            if (player.Power == PowerType.None)
                return "Kamu tidak punya Kartu Kuasa.";
            if (player.PowerUsed)
                return "Kartu Kuasa sudah dipakai.";
            switch (player.Power)
            {
                case PowerType.MataElang:
                    if (Phase != GamePhase.ShootWindow || LastPlay == null || LastPlay.PlayerId == playerId)
                        return "Mata Elang dipakai saat lawan baru menaruh kartu.";
                    return null;
                case PowerType.Tameng:
                    if (Phase != GamePhase.AwaitingPlay || CurrentPlayer != playerId)
                        return "Tameng dipasang saat kamu menaruh kartu.";
                    if (player.HandCount < 2)
                        return "Tameng tidak bisa dipakai untuk taruhan terakhir.";
                    return null;
                case PowerType.Cermin:
                    return "Cermin aktif otomatis saat kamu ketahuan bohong.";
                case PowerType.Badai:
                    if (Phase != GamePhase.AwaitingPlay || CurrentPlayer != playerId)
                        return "Badai dipakai di giliranmu.";
                    if (!HasRoundElement)
                        return "Badai dipakai saat mengikuti ronde, bukan saat memimpin.";
                    return null;
                default:
                    if (Phase != GamePhase.AwaitingPlay || CurrentPlayer != playerId)
                        return "Kuasa ini dipakai di giliranmu.";
                    if (player.HandCount < 2)
                        return "Kartumu tinggal 1, simpan untuk taruhan terakhir.";
                    return null;
            }
        }

        public ActionResult TryUsePower(int playerId, PowerRequest request)
        {
            if (request == null)
                return ActionResult.Fail("Permintaan kuasa kosong.");
            string reason = PowerBlockReason(playerId);
            if (reason != null)
                return ActionResult.Fail(reason);
            var player = players[playerId];
            if (request.Power != player.Power)
                return ActionResult.Fail("Itu bukan Kartu Kuasa milikmu.");

            switch (player.Power)
            {
                case PowerType.MataElang:
                {
                    player.PowerUsed = true;
                    var play = LastPlay;
                    Emit(new GameEvent { Type = GameEventType.PowerUsed, PlayerId = playerId, OtherPlayerId = play.PlayerId, Power = PowerType.MataElang, Count = play.Count });
                    Emit(new GameEvent
                    {
                        Type = GameEventType.PowerPeek,
                        PlayerId = playerId,
                        OtherPlayerId = play.PlayerId,
                        Count = play.Count,
                        Claim = play.Claim,
                        Honest = play.IsHonest,
                        Cards = new List<Card>(play.Cards),
                    });
                    return ActionResult.Success;
                }

                case PowerType.Badai:
                {
                    player.PowerUsed = true;
                    Emit(new GameEvent { Type = GameEventType.PowerUsed, PlayerId = playerId, Power = PowerType.Badai, Count = pile.Count });
                    BurnPile(playerId);
                    BeginRound(playerId, RoundStartReason.AfterBurn);
                    return ActionResult.Success;
                }

                case PowerType.Bakar:
                case PowerType.Hadiah:
                {
                    var ids = request.CardIds;
                    if (ids == null || ids.Count < 1 || ids.Count > Powers.MaxPowerCards)
                        return ActionResult.Fail("Pilih 1–" + Powers.MaxPowerCards + " kartu dulu.");
                    var result = CollectCards(player, ids, out var chosen);
                    if (!result.Ok)
                        return result;
                    if (player.HandCount - chosen.Count < 1)
                        return ActionResult.Fail("Sisakan minimal 1 kartu di tanganmu.");

                    if (player.Power == PowerType.Hadiah)
                    {
                        int target = request.TargetPlayer;
                        if (target < 0 || target >= players.Count || target == playerId)
                            return ActionResult.Fail("Pilih lawan yang akan menerima hadiah.");
                        foreach (var card in chosen)
                            player.hand.RemoveAt(IndexOfCard(player.hand, card.Id));
                        players[target].hand.AddRange(chosen);
                        player.PowerUsed = true;
                        Emit(new GameEvent { Type = GameEventType.PowerUsed, PlayerId = playerId, OtherPlayerId = target, Power = PowerType.Hadiah, Count = chosen.Count, Cards = chosen });
                    }
                    else
                    {
                        foreach (var card in chosen)
                            player.hand.RemoveAt(IndexOfCard(player.hand, card.Id));
                        burned.AddRange(chosen);
                        player.PowerUsed = true;
                        Emit(new GameEvent { Type = GameEventType.PowerUsed, PlayerId = playerId, Power = PowerType.Bakar, Count = chosen.Count, Cards = chosen });
                    }
                    return ActionResult.Success;
                }

                default:
                    return ActionResult.Fail("Kuasa ini tidak bisa dipakai sekarang.");
            }
        }

        // ------------------------------------------------------------------ internals

        ActionResult CollectCards(PlayerState player, IList<int> cardIds, out List<Card> chosen)
        {
            chosen = new List<Card>(cardIds.Count);
            for (int i = 0; i < cardIds.Count; i++)
            {
                int id = cardIds[i];
                for (int j = 0; j < i; j++)
                {
                    if (cardIds[j] == id)
                        return ActionResult.Fail("Kartu yang sama dipilih dua kali.");
                }
                int index = IndexOfCard(player.hand, id);
                if (index < 0)
                    return ActionResult.Fail("Kartu itu tidak ada di tanganmu.");
                chosen.Add(player.hand[index]);
            }
            return ActionResult.Success;
        }

        void BurnPile(int leader)
        {
            var cards = new List<Card>(pile);
            burned.AddRange(pile);
            pile.Clear();
            Emit(new GameEvent { Type = GameEventType.PileBurned, PlayerId = leader, Count = cards.Count, Cards = cards });
        }

        void CloseWindowInternal()
        {
            Emit(new GameEvent { Type = GameEventType.ShootWindowClosed, PlayerId = LastPlay.PlayerId });
            if (players[LastPlay.PlayerId].HandCount == 0)
            {
                EndGame(LastPlay.PlayerId);
                return;
            }
            StartTurn(NextPlayer(LastPlay.PlayerId));
        }

        void BeginRound(int leader, RoundStartReason reason)
        {
            RoundLeader = leader;
            HasRoundElement = false;
            LastPlay = null;
            roundPlays.Clear();
            ConsecutivePasses = 0;
            CurrentPlayer = leader;
            Phase = GamePhase.AwaitingPlay;
            Emit(new GameEvent { Type = GameEventType.RoundStarted, PlayerId = leader, Reason = reason });
            StartTurn(leader);
        }

        void StartTurn(int playerId)
        {
            CurrentPlayer = playerId;
            Phase = GamePhase.AwaitingPlay;
            TurnCount++;
            Emit(new GameEvent { Type = GameEventType.TurnStarted, PlayerId = playerId });
        }

        void EndGame(int winner)
        {
            Winner = winner;
            Phase = GamePhase.GameOver;
            CurrentPlayer = -1;
            Emit(new GameEvent { Type = GameEventType.GameOver, PlayerId = winner });
        }

        void Emit(GameEvent e)
        {
            e.Sequence = ++sequence;
            e.HandCounts = new int[players.Count];
            e.Bullets = new int[players.Count];
            e.PowerSpent = new bool[players.Count];
            for (int i = 0; i < players.Count; i++)
            {
                e.HandCounts[i] = players[i].HandCount;
                e.Bullets[i] = players[i].Bullets;
                e.PowerSpent[i] = players[i].PowerUsed;
            }
            e.PileCount = pile.Count;
            e.BurnedCount = burned.Count;
            e.CurrentPlayer = CurrentPlayer;
            log.Add(e);
            EventRaised?.Invoke(e);
        }

        static int IndexOfCard(List<Card> cards, int id)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Id == id)
                    return i;
            }
            return -1;
        }
    }
}
