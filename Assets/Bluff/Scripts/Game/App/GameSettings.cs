using Bluff.Core.AI;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>Pengaturan pemain, disimpan di PlayerPrefs.</summary>
    public static class GameSettings
    {
        const string KeyName = "bluff.playerName";
        const string KeyDifficulty = "bluff.difficulty";
        const string KeyWindow = "bluff.shootWindow";
        const string KeySpeed = "bluff.speed";
        const string KeyVolume = "bluff.volume";
        const string KeyRefund = "bluff.refund";
        const string KeyClassicPenalty = "bluff.classicPenalty";
        const string KeyPowers = "bluff.powers";
        const string KeyTutorialSeen = "bluff.tutorialSeen";
        const string KeyLookSensitivity = "bluff.lookSensitivity";

        public static readonly float[] ShootWindowOptions = { 2f, 3f, 4f, 5f, 6f };
        public static readonly string[] SpeedNames = { "Santai", "Normal", "Cepat" };
        public static readonly float[] SpeedValues = { 0.75f, 1f, 1.5f };
        public static readonly string[] DifficultyNames = { "Mudah", "Normal", "Sulit" };
        public static readonly float[] VolumeOptions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        public static string PlayerName
        {
            get
            {
                // '<' dan '>' dibuang supaya nama tidak merusak rich text.
                string name = PlayerPrefs.GetString(KeyName, "Kamu").Replace("<", "").Replace(">", "").Trim();
                return string.IsNullOrEmpty(name) ? "Kamu" : name;
            }
            set => Save(() => PlayerPrefs.SetString(KeyName, value));
        }

        public static BotDifficulty Difficulty
        {
            get => (BotDifficulty)Mathf.Clamp(PlayerPrefs.GetInt(KeyDifficulty, 1), 0, 2);
            set => Save(() => PlayerPrefs.SetInt(KeyDifficulty, (int)value));
        }

        public static int ShootWindowIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyWindow, 1), 0, ShootWindowOptions.Length - 1);
            set => Save(() => PlayerPrefs.SetInt(KeyWindow, value));
        }

        public static float ShootWindowSeconds => ShootWindowOptions[ShootWindowIndex];

        public static int SpeedIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeySpeed, 1), 0, SpeedValues.Length - 1);
            set => Save(() => PlayerPrefs.SetInt(KeySpeed, value));
        }

        /// <summary>Pengali kecepatan animasi &amp; jeda bot (lebih besar = lebih cepat).</summary>
        public static float Speed => BluffDebug.SpeedOverride > 0f ? BluffDebug.SpeedOverride : SpeedValues[SpeedIndex];

        public static int VolumeIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyVolume, 3), 0, VolumeOptions.Length - 1);
            set => Save(() => PlayerPrefs.SetInt(KeyVolume, value));
        }

        public static float Volume => VolumeOptions[VolumeIndex];

        /// <summary>Tembakan yang tepat tidak menghabiskan peluru.</summary>
        public static bool RefundBulletOnHit
        {
            get => PlayerPrefs.GetInt(KeyRefund, 1) == 1;
            set => Save(() => PlayerPrefs.SetInt(KeyRefund, value ? 1 : 0));
        }

        /// <summary>Aturan klasik: salah tembak = penembak mengambil seluruh tumpukan.</summary>
        public static bool ClassicWrongShotPenalty
        {
            get => PlayerPrefs.GetInt(KeyClassicPenalty, 0) == 1;
            set => Save(() => PlayerPrefs.SetInt(KeyClassicPenalty, value ? 1 : 0));
        }

        /// <summary>Setiap pemain mendapat satu Kartu Kuasa.</summary>
        public static bool EnablePowers
        {
            get => PlayerPrefs.GetInt(KeyPowers, 1) == 1;
            set => Save(() => PlayerPrefs.SetInt(KeyPowers, value ? 1 : 0));
        }

        public static bool TutorialSeen
        {
            get => PlayerPrefs.GetInt(KeyTutorialSeen, 0) == 1;
            set => Save(() => PlayerPrefs.SetInt(KeyTutorialSeen, value ? 1 : 0));
        }

        public static float LookSensitivity
        {
            get => ClampLookSensitivity(PlayerPrefs.GetFloat(KeyLookSensitivity, 1f));
            set => Save(() => PlayerPrefs.SetFloat(KeyLookSensitivity, ClampLookSensitivity(value)));
        }

        static float ClampLookSensitivity(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0.25f, 3f);

        static void Save(System.Action write)
        {
            write();
            PlayerPrefs.Save();
        }
    }
}
