using UnityEngine;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>Compact voice strip: mute, deafen, and volume down/up with the current level and connection dot.</summary>
    public sealed class VoiceControls : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(384f, 64f);

        Button mute;
        Button deafen;
        Button quieter;
        Button louder;
        Image muteIcon;
        Image deafenIcon;
        Image status;
        Text level;
        bool hotkeys;

        /// <param name="hotkeys">M toggles mute; only for screens without text input.</param>
        public static VoiceControls Create(Transform parent, bool hotkeys)
        {
            var rt = UIKit.CreateRect("VoiceControls", parent);
            rt.sizeDelta = Size;
            var controls = rt.gameObject.AddComponent<VoiceControls>();
            controls.hotkeys = hotkeys;
            controls.Build(rt);
            return controls;
        }

        void Build(RectTransform rt)
        {
            mute = IconButton(rt, "Mute", GameIcon.Mic, RoomVoice.ToggleMute, out muteIcon);
            mute.GetComponent<RectTransform>().Place(UIKit.Left, new Vector2(32f, 0f), new Vector2(64f, 64f));
            deafen = IconButton(rt, "Deafen", GameIcon.Headset, RoomVoice.ToggleDeafen, out deafenIcon);
            deafen.GetComponent<RectTransform>().Place(UIKit.Left, new Vector2(108f, 0f), new Vector2(64f, 64f));

            var volume = UIKit.CreatePanel(rt, "Volume", Theme.Panel.WithAlpha(0.92f), 8f, true);
            volume.rectTransform.Place(UIKit.Right, new Vector2(-117f, 0f), new Vector2(234f, 64f));
            quieter = UIKit.CreateButton(volume.transform, "Quieter", "-", new Vector2(52f, 52f), Theme.PanelLight, Theme.Text, () => RoomVoice.ChangeVolume(-1), 34);
            quieter.GetComponent<RectTransform>().Place(UIKit.Left, new Vector2(32f, 0f), new Vector2(52f, 52f));
            louder = UIKit.CreateButton(volume.transform, "Louder", "+", new Vector2(52f, 52f), Theme.PanelLight, Theme.Text, () => RoomVoice.ChangeVolume(1), 34);
            louder.GetComponent<RectTransform>().Place(UIKit.Right, new Vector2(-32f, 0f), new Vector2(52f, 52f));
            level = UIKit.CreateText(volume.transform, "Level", "", 24, Theme.Text, TextAnchor.MiddleCenter);
            level.rectTransform.Place(UIKit.Center, new Vector2(0f, 7f), new Vector2(110f, 32f));
            var caption = UIKit.CreateText(volume.transform, "Caption", "Voice", 16, Theme.Muted, TextAnchor.MiddleCenter);
            caption.rectTransform.Place(UIKit.Center, new Vector2(6f, -16f), new Vector2(90f, 22f));
            status = UIKit.CreateImage(volume.transform, "Status", SpriteFactory.Circle, Theme.Muted);
            status.rectTransform.Place(UIKit.Center, new Vector2(-28f, -16f), new Vector2(10f, 10f));
            // OnEnable already ran inside AddComponent, before these controls existed.
            Refresh();
        }

        static Button IconButton(Transform parent, string name, GameIcon icon, System.Action action, out Image image)
        {
            var button = UIKit.CreateButton(parent, name, "", new Vector2(64f, 64f), Theme.Panel, Theme.Text, action, 19, 8f);
            image = UIKit.CreateImage(button.transform, "Icon", SpriteFactory.Emblem(icon), Color.white);
            image.preserveAspect = true;
            image.rectTransform.Place(UIKit.Center, Vector2.zero, new Vector2(48f, 48f));
            return button;
        }

        void OnEnable()
        {
            RoomVoice.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => RoomVoice.Changed -= Refresh;

        void Update()
        {
            if (hotkeys && InputCompat.MutePressed()) RoomVoice.ToggleMute();
        }

        void Refresh()
        {
            if (mute == null) return;
            bool micOff = RoomVoice.MicSilenced;
            muteIcon.sprite = SpriteFactory.Emblem(micOff ? GameIcon.MicOff : GameIcon.Mic);
            deafenIcon.sprite = SpriteFactory.Emblem(RoomVoice.Deafened ? GameIcon.HeadsetOff : GameIcon.Headset);
            Tint(mute, micOff);
            Tint(deafen, RoomVoice.Deafened);
            level.text = RoomVoice.Volume + "%";
            quieter.interactable = RoomVoice.Volume > 0;
            louder.interactable = RoomVoice.Volume < GameSettings.VoiceVolumeMax;
            switch (RoomVoice.State)
            {
                case RoomVoice.VoiceState.Connected: status.color = Theme.Green; break;
                case RoomVoice.VoiceState.Connecting: status.color = Theme.Gold; break;
                case RoomVoice.VoiceState.Unavailable: status.color = Theme.Red; break;
                default: status.color = Theme.Muted; break;
            }
        }

        static void Tint(Button button, bool off) => button.targetGraphic.color = off ? Theme.Red : Theme.Panel;
    }
}
