using Bluff.Core;

namespace Bluff.Game
{
    /// <summary>
    /// Kait untuk mode demo &amp; pengujian otomatis (mis. mengambil screenshot tanpa pemain).
    /// Tidak dipakai saat bermain biasa.
    /// </summary>
    public static class BluffDebug
    {
        /// <summary>Kursi pemain manusia dimainkan oleh bot.</summary>
        public static bool AutoPlay;

        /// <summary>Seed tetap untuk mengocok kartu (0 = acak).</summary>
        public static int FixedSeed;

        /// <summary>Pengali kecepatan paksa (0 = pakai pengaturan pemain).</summary>
        public static float SpeedOverride;

        /// <summary>Dipanggil setiap kali satu event selesai dianimasikan di meja.</summary>
        public static event System.Action<GameEvent> Presented;

        internal static void RaisePresented(GameEvent e) => Presented?.Invoke(e);

        public static void Reset()
        {
            AutoPlay = false;
            FixedSeed = 0;
            SpeedOverride = 0f;
            Presented = null;
        }
    }
}
