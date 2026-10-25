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
        const string KeyVoiceMuted = "bluff.voiceMuted";
        const string KeyVoiceDeafened = "bluff.voiceDeafened";
        const string KeyVoiceVolume = "bluff.voiceVolume";

        /// <summary>Voice volume is a percentage of the normal level, adjusted in these steps.</summary>
        public const int VoiceVolumeStep = 10;
        public const int VoiceVolumeMax = 200;

        public static readonly float[] ShootWindowOptions = { 2f, 3f, 4f, 5f, 6f };
        public static readonly string[] SpeedNames = { "Relaxed", "Normal", "Fast" };
        public static readonly float[] SpeedValues = { 0.75f, 1f, 1.5f };
        public static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
        public static readonly float[] VolumeOptions = { 0f, 0.25f, 0.5f, 0.75f, 1f };

        public static string PlayerName
        {
            get
            {
                // '<' dan '>' dibuang supaya nama tidak merusak rich text.
                string name = RoomProtocol.CleanName(PlayerPrefs.GetString(KeyName, "You"));
                return string.IsNullOrEmpty(name) ? "You" : name;
            }
            set => Save(() => PlayerPrefs.SetString(KeyName, RoomProtocol.CleanName(value)));
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

        /// <summary>Your microphone is not sent to the room.</summary>
        public static bool VoiceMuted
        {
            get => PlayerPrefs.GetInt(KeyVoiceMuted, 0) == 1;
            set => Save(() => PlayerPrefs.SetInt(KeyVoiceMuted, value ? 1 : 0));
        }

        /// <summary>You hear nobody, and your microphone is muted too.</summary>
        public static bool VoiceDeafened
        {
            get => PlayerPrefs.GetInt(KeyVoiceDeafened, 0) == 1;
            set => Save(() => PlayerPrefs.SetInt(KeyVoiceDeafened, value ? 1 : 0));
        }

        /// <summary>Incoming voice volume in percent (100 = normal).</summary>
        public static int VoiceVolume
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyVoiceVolume, 100), 0, VoiceVolumeMax);
            set => Save(() => PlayerPrefs.SetInt(KeyVoiceVolume, Mathf.Clamp(value, 0, VoiceVolumeMax)));
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
