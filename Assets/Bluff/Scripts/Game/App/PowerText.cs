using Bluff.Core;

namespace Bluff.Game
{
    /// <summary>Nama &amp; penjelasan Kartu Kuasa untuk UI.</summary>
    public static class PowerText
    {
        public static string Name(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return "Mata Elang";
                case PowerType.Tameng: return "Tameng";
                case PowerType.Bakar: return "Bakar";
                case PowerType.Badai: return "Badai";
                case PowerType.Cermin: return "Cermin";
                case PowerType.Hadiah: return "Hadiah";
                default: return "-";
            }
        }

        /// <summary>Penjelasan singkat untuk widget kartu kuasa.</summary>
        public static string Short(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return "Saat lawan menaruh kartu, intip isinya diam-diam.";
                case PowerType.Tameng: return "Pasang saat menaruh: taruhan itu kebal tembakan.";
                case PowerType.Bakar: return "Di giliranmu, buang 1–2 kartu ke api.";
                case PowerType.Badai: return "Saat mengikuti ronde: hanguskan tumpukan, kamu memimpin.";
                case PowerType.Cermin: return "Saat kamu ketahuan bohong, tembakan memantul ke penembak.";
                case PowerType.Hadiah: return "Di giliranmu, berikan 1–2 kartu ke satu lawan.";
                default: return "";
            }
        }

        /// <summary>Penjelasan lengkap untuk halaman aturan.</summary>
        public static string Long(PowerType power)
        {
            switch (power)
            {
                case PowerType.MataElang: return "Saat jendela tembak terbuka setelah lawan menaruh kartu, intip kartu itu diam-diam, lalu putuskan mau menembak atau tidak.";
                case PowerType.Tameng: return "Pasang bersama taruhanmu: taruhan itu tidak bisa ditembak. Tidak berlaku untuk taruhan terakhir.";
                case PowerType.Bakar: return "Di giliranmu, buang 1–2 kartu dari tangan langsung ke tumpukan hangus (sisakan minimal 1 kartu).";
                case PowerType.Badai: return "Di giliranmu saat mengikuti ronde: tumpukan tengah hangus dan kamu memimpin ronde baru dengan elemen bebas.";
                case PowerType.Cermin: return "Otomatis: pertama kali kamu ketahuan bohong, tembakan memantul. Penembak yang mengambil tumpukan dan kamu memimpin ronde baru. Tidak berlaku untuk taruhan terakhir.";
                case PowerType.Hadiah: return "Di giliranmu, berikan 1–2 kartu pilihanmu kepada satu lawan (sisakan minimal 1 kartu).";
                default: return "";
            }
        }
    }
}
