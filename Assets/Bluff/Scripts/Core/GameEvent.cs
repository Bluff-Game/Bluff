using System.Collections.Generic;

namespace Bluff.Core
{
    public enum GameEventType
    {
        GameStarted,
        HandDealt,
        RoundStarted,
        TurnStarted,
        CardsPlayed,
        ShootWindowOpened,
        ShootWindowClosed,
        Passed,
        ShotFired,
        CardsRevealed,
        BulletRefunded,
        PileTaken,
        PileBurned,
        GameOver,

        /// <summary>Kartu Kuasa dibagikan (jenisnya rahasia, hanya pemiliknya yang tahu).</summary>
        PowerDealt,

        /// <summary>Kartu Kuasa dipakai (jenisnya sekarang diketahui semua orang).</summary>
        PowerUsed,

        /// <summary>Hasil Mata Elang: kartu taruhan lawan, hanya untuk pemakainya.</summary>
        PowerPeek,
    }

    public enum RoundStartReason
    {
        GameStart,
        AfterChallenge,
        AfterBurn,
    }

    /// <summary>
    /// Semua yang terjadi di meja dilaporkan sebagai event berurutan. UI menganimasikannya,
    /// bot belajar darinya, dan nanti host online meneruskannya ke client (lewat <see cref="RedactedFor"/>).
    /// </summary>
    public sealed class GameEvent
    {
        public int Sequence;
        public GameEventType Type;

        /// <summary>
        /// Pelaku utama: penaruh (CardsPlayed), penembak (ShotFired), pemilik kartu yang dibuka (CardsRevealed),
        /// pengambil tumpukan (PileTaken), pemimpin ronde (RoundStarted, PileBurned), pemenang (GameOver),
        /// pemakai kuasa (PowerUsed, PowerPeek).
        /// </summary>
        public int PlayerId = -1;

        /// <summary>
        /// Pihak kedua: target tembakan (ShotFired), penembak (CardsRevealed), target kuasa (PowerUsed Hadiah/Mata Elang),
        /// penembak yang terpantul (PowerUsed Cermin).
        /// </summary>
        public int OtherPlayerId = -1;

        public int Count;
        public Element Claim;
        public bool Honest;

        /// <summary>PileTaken: hanya kartu yang dituduh (salah tembak) yang diambil, bukan seluruh tumpukan. Kartunya publik.</summary>
        public bool Partial;

        /// <summary>CardsPlayed / ShootWindowOpened: taruhan dilindungi Tameng.</summary>
        public bool Shielded;

        /// <summary>PowerDealt / PowerUsed: jenis kuasa. Pada PowerDealt milik orang lain nilainya disembunyikan (None).</summary>
        public PowerType Power;

        public RoundStartReason Reason;

        /// <summary>Kartu yang terlibat. Null kalau disembunyikan dari penonton (lihat <see cref="RedactedFor"/>).</summary>
        public List<Card> Cards;

        // Keadaan publik meja SETELAH event ini terjadi.
        public int[] HandCounts;
        public int[] Bullets;
        public bool[] PowerSpent;
        public int PileCount;
        public int BurnedCount;
        public int CurrentPlayer;

        /// <summary>Event yang membawa kartu yang tidak boleh dilihat semua orang.</summary>
        public bool HasPrivateCards =>
            Type == GameEventType.HandDealt ||
            Type == GameEventType.CardsPlayed ||
            (Type == GameEventType.PileTaken && !Partial) ||
            Type == GameEventType.PileBurned ||
            Type == GameEventType.PowerPeek ||
            (Type == GameEventType.PowerUsed && Cards != null);

        /// <summary>Apakah <paramref name="viewerId"/> boleh melihat isi <see cref="Cards"/> event ini.</summary>
        public bool CanSeeCards(int viewerId)
        {
            switch (Type)
            {
                case GameEventType.HandDealt:
                case GameEventType.CardsPlayed:
                case GameEventType.PowerPeek:
                    return viewerId == PlayerId;
                case GameEventType.PileTaken:
                    return Partial || viewerId == PlayerId;
                case GameEventType.PileBurned:
                    return false;
                case GameEventType.PowerUsed:
                    // Bakar: dibuang tertutup. Hadiah: hanya pemberi dan penerima yang tahu.
                    return viewerId == PlayerId || (Power == PowerType.Hadiah && viewerId == OtherPlayerId);
                default:
                    return true;
            }
        }

        /// <summary>Salinan event yang aman dilihat <paramref name="viewerId"/>: kartu & kuasa rahasia milik orang lain dihapus.</summary>
        public GameEvent RedactedFor(int viewerId)
        {
            bool hideCards = Cards != null && !CanSeeCards(viewerId);
            bool hidePower = Type == GameEventType.PowerDealt && viewerId != PlayerId && Power != PowerType.None;
            bool hideHonesty = Type == GameEventType.PowerPeek && viewerId != PlayerId;
            if (!hideCards && !hidePower && !hideHonesty)
                return this;
            var copy = (GameEvent)MemberwiseClone();
            if (hideCards)
                copy.Cards = null;
            if (hidePower)
                copy.Power = PowerType.None;
            if (hideHonesty)
                copy.Honest = false;
            return copy;
        }

        public override string ToString()
        {
            return "#" + Sequence + " " + Type + " p" + PlayerId +
                   (OtherPlayerId >= 0 ? " o" + OtherPlayerId : "") +
                   (Count > 0 ? " n" + Count : "") +
                   (Power != PowerType.None ? " " + Power : "");
        }
    }
}
