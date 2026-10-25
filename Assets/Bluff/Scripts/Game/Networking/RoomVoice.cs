using System;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Voice chat for the current room: everyone in a room shares one Vivox group channel named after the room code.
    /// Mute, deafen and volume are local preferences kept in <see cref="GameSettings"/> and applied whenever voice connects.
    /// Voice is best effort: if it fails, the room keeps working without it.
    /// </summary>
    public static class RoomVoice
    {
        public enum VoiceState { Off, Connecting, Connected, Unavailable }

        static bool initialized;
        static Task joining;
        static int generation;

        public static VoiceState State { get; private set; }
        public static event Action Changed;

        public static bool Muted => GameSettings.VoiceMuted;
        public static bool Deafened => GameSettings.VoiceDeafened;
        public static int Volume => GameSettings.VoiceVolume;
        /// <summary>The microphone is silent, either muted directly or because the player is deafened.</summary>
        public static bool MicSilenced => Muted || Deafened;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            initialized = false;
            joining = null;
            generation = 0;
            State = VoiceState.Off;
            Changed = null;
        }

        public static void ToggleMute()
        {
            GameSettings.VoiceMuted = !Muted;
            Apply();
        }

        public static void ToggleDeafen()
        {
            GameSettings.VoiceDeafened = !Deafened;
            Apply();
        }

        public static void ChangeVolume(int steps)
        {
            GameSettings.VoiceVolume = Volume + steps * GameSettings.VoiceVolumeStep;
            Apply();
        }

        /// <summary>Joins the room's channel. Unity Services must already be initialized and signed in.</summary>
        internal static void Join(string roomCode)
        {
            if (State == VoiceState.Connecting || State == VoiceState.Connected) return;
            joining = JoinAsync("bluff" + RoomProtocol.Version + "_" + roomCode, ++generation);
        }

        static async Task JoinAsync(string channel, int run)
        {
            SetState(VoiceState.Connecting);
            try
            {
                var vivox = VivoxService.Instance;
                if (!initialized)
                {
                    await vivox.InitializeAsync();
                    initialized = true;
                    vivox.ConnectionRecovering += () => SetState(VoiceState.Connecting);
                    vivox.ConnectionRecovered += () => SetState(VoiceState.Connected);
                    vivox.ConnectionFailedToRecover += () => SetState(VoiceState.Unavailable);
                }
                if (run != generation) return;
                if (!vivox.IsLoggedIn) await vivox.LoginAsync();
                if (run != generation) return;
                await vivox.JoinGroupChannelAsync(channel, ChatCapability.AudioOnly);
                if (run != generation) return;
                SetState(VoiceState.Connected);
                Apply();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] Voice chat is unavailable: " + e.Message);
                if (run == generation) SetState(VoiceState.Unavailable);
            }
        }

        /// <summary>Leaves voice; call before signing out of Authentication.</summary>
        internal static async Task LeaveAsync()
        {
            generation++;
            var pending = joining;
            joining = null;
            if (pending != null) await Task.WhenAny(pending, Task.Delay(5000));
            try
            {
                if (initialized && VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LeaveAllChannelsAsync();
                    await VivoxService.Instance.LogoutAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] Leaving voice failed: " + e.Message);
            }
            SetState(VoiceState.Off);
        }

        static void Apply()
        {
            if (initialized && VivoxService.Instance.IsLoggedIn)
            {
                var vivox = VivoxService.Instance;
                if (MicSilenced) vivox.MuteInputDevice(); else vivox.UnmuteInputDevice();
                if (Deafened) vivox.MuteOutputDevice(); else vivox.UnmuteOutputDevice();
                // Vivox volume runs from -50 to 50 around a default of 0; 100% maps to 0.
                vivox.SetOutputDeviceVolume(Mathf.Clamp((Volume - 100) / 2, -50, 50));
            }
            Changed?.Invoke();
        }

        static void SetState(VoiceState value)
        {
            if (State == value) return;
            State = value;
            Changed?.Invoke();
        }
    }
}
