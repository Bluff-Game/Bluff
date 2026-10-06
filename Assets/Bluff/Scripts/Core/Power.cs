using System;
using System.Collections.Generic;

namespace Bluff.Core
{
    /// <summary>Kartu Kuasa: setiap pemain mendapat satu (rahasia), dan masing-masing hanya bisa dipakai sekali.</summary>
    public enum PowerType
    {
        None = 0,

        /// <summary>Saat jendela tembak: intip diam-diam kartu yang baru ditaruh lawan.</summary>
        MataElang = 1,

        /// <summary>Dipasang bersama taruhan: taruhan itu tidak bisa ditembak (bukan taruhan terakhir).</summary>
        Tameng = 2,

        /// <summary>Di giliran sendiri: buang 1–2 kartu dari tangan ke tumpukan hangus (sisakan minimal 1).</summary>
        Bakar = 3,

        /// <summary>Di giliran sendiri saat mengikuti ronde: hanguskan tumpukan tengah lalu pimpin ronde baru.</summary>
        Badai = 4,

        /// <summary>Otomatis: pertama kali ketahuan bohong, tembakan memantul ke penembak (bukan taruhan terakhir).</summary>
        Cermin = 5,

        /// <summary>Di giliran sendiri: berikan 1–2 kartu pilihan ke satu lawan (sisakan minimal 1).</summary>
        Hadiah = 6,
    }

    public static class Powers
    {
        public static readonly IReadOnlyList<PowerType> All = Array.AsReadOnly(new[]
        {
            PowerType.MataElang, PowerType.Tameng, PowerType.Bakar, PowerType.Badai, PowerType.Cermin, PowerType.Hadiah,
        });

        /// <summary>Kuasa yang dipakai di awal giliran sendiri (sebelum menaruh / pass).</summary>
        public static bool IsTurnPower(this PowerType power) =>
            power == PowerType.Bakar || power == PowerType.Badai || power == PowerType.Hadiah;

        /// <summary>Kuasa yang butuh kartu dari tangan (1–2 kartu).</summary>
        public static bool NeedsCards(this PowerType power) => power == PowerType.Bakar || power == PowerType.Hadiah;

        public const int MaxPowerCards = 2;
    }

    /// <summary>Argumen pemakaian kuasa. Kartu untuk Bakar/Hadiah, target untuk Hadiah.</summary>
    public sealed class PowerRequest
    {
        public PowerType Power;
        public List<int> CardIds = new List<int>();
        public int TargetPlayer = -1;

        public static PowerRequest Of(PowerType power) => new PowerRequest { Power = power };
    }
}
