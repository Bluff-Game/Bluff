using System;

namespace Bluff.Core
{
    /// <summary>Parameter aturan. Nilai default = aturan resmi Bluff: Topeng &amp; Elemen.</summary>
    public sealed class GameConfig
    {
        public int PlayerCount = 4;
        public int CopiesPerElement = 8;
        public int WildCount = 4;
        public int StartingBullets = 2;
        public int MaxCardsPerPlay = 3;

        /// <summary>Kalau true, tembakan yang tepat (lawan memang bohong) tidak menghabiskan peluru.</summary>
        public bool RefundBulletOnHit = true;

        /// <summary>
        /// Hukuman salah tembak. false (default): penembak hanya mengambil kartu yang ia tuduh dan ronde berlanjut.
        /// true (klasik): penembak mengambil seluruh tumpukan dan penaruh memimpin ronde baru.
        /// </summary>
        public bool WrongShotTakesWholePile = false;

        /// <summary>Setiap pemain mendapat satu Kartu Kuasa acak (berbeda-beda).</summary>
        public bool EnablePowers = true;

        public int TotalCards => CopiesPerElement * Elements.ClaimableCount + WildCount;

        /// <summary>Jumlah kartu yang sah untuk satu elemen: salinan elemen itu + semua Topeng.</summary>
        public int MatchesPerElement => CopiesPerElement + WildCount;

        public GameConfig Clone() => (GameConfig)MemberwiseClone();

        public void Validate()
        {
            if (PlayerCount < 2 || PlayerCount > 6)
                throw new ArgumentOutOfRangeException(nameof(PlayerCount), "Jumlah pemain harus 2-6.");
            if (CopiesPerElement < 1)
                throw new ArgumentOutOfRangeException(nameof(CopiesPerElement));
            if (WildCount < 0)
                throw new ArgumentOutOfRangeException(nameof(WildCount));
            if (StartingBullets < 0)
                throw new ArgumentOutOfRangeException(nameof(StartingBullets));
            if (MaxCardsPerPlay < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxCardsPerPlay));
            if (TotalCards < PlayerCount)
                throw new ArgumentException("Kartu tidak cukup untuk semua pemain.");
        }
    }
}
