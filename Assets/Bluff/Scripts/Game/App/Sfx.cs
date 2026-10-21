using System.Collections.Generic;
using UnityEngine;
using Bluff.Core;

namespace Bluff.Game
{
    public enum SfxType
    {
        Click,
        Card,
        Pass,
        Turn,
        Shoot,
        Honest,
        Lie,
        Burn,
        Take,
        Win,
        Lose,
        Tick,
        Power,
        Deal,
    }

    /// <summary>Efek suara yang disintesis saat runtime (tanpa file audio). Volume mengikuti pengaturan.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int SampleRate = 44100;

        static Sfx instance;
        AudioSource source;
        readonly Dictionary<SfxType, AudioClip> clips = new Dictionary<SfxType, AudioClip>();
        readonly Dictionary<int, AudioClip> powerClips = new Dictionary<int, AudioClip>();

        public static void Power(GameObject owner, PowerType power, int variant, bool preview)
        {
            if(GameSettings.Volume<=0f || owner==null) return;
            Ensure();
            int key=(int)power*3+Mathf.Clamp(variant,0,2);
            if(!instance.powerClips.TryGetValue(key,out var clip))
            {
                float filtered=0f;
                clip=Make(power+"-"+variant,2.35f,(t,rnd)=>
                {
                    float noise=(float)rnd.NextDouble()*2f-1f;
                    filtered=Mathf.Lerp(filtered,noise,.06f);
                    float tone=power==PowerType.Cermin?920f:power==PowerType.Tameng?180f:power==PowerType.MataElang?740f:power==PowerType.Hadiah?440f:110f;
                    float env=Mathf.Clamp01(t/.045f)*Mathf.Exp(-t*1.8f);
                    float wave;
                    if(variant==1)
                    {
                        // Struck brass: staggered inharmonic resonances and a short metallic attack.
                        float beat=t% .28f;
                        wave=(Mathf.Sin(6.283f*tone*t)+.35f*Mathf.Sin(6.283f*tone*2.76f*t))*env*.17f;
                        wave+=noise*Mathf.Exp(-beat*65f)*Mathf.Exp(-t*2f)*.09f;
                    }
                    else if(variant==2)
                    {
                        // Flowing jade: rising water-like glissandi, soft wind, and delayed droplets.
                        wave=Mathf.Sin(6.283f*(tone*.65f*t+90f*t*t)+Mathf.Sin(t*9f)*2f)*env*.14f;
                        wave+=filtered*Mathf.Sin(Mathf.Clamp01(t/2.35f)*Mathf.PI)*.25f;
                        for(int drop=0;drop<4;drop++) {float d=t-.2f-drop*.26f;if(d>0f) wave+=Mathf.Sin(6.283f*(1300f*d-250f*d*d))*Mathf.Exp(-d*14f)*.07f;}
                    }
                    else wave=(Mathf.Sin(6.283f*tone*t)*.18f+filtered*.18f)*env;
                    if(power==PowerType.Bakar) wave+=noise*noise*noise*env*(.08f+.08f*Mathf.Sin(t*43f));
                    if(power==PowerType.Badai) wave+=filtered*env*(.6f+.35f*Mathf.Sin(t*17f));
                    if(power==PowerType.Cermin)
                    {
                        float hit=t-1.05f;
                        if(hit>0f) wave+=(noise*.14f+Mathf.Sin(6.283f*(variant==1?1700f:variant==2?2400f:3100f)*hit)*.12f)*Mathf.Exp(-hit*9f);
                    }
                    return Mathf.Clamp(wave,-.75f,.75f);
                });
                instance.powerClips[key]=clip;
            }
            var audio=owner.AddComponent<AudioSource>();audio.playOnAwake=false;
            audio.spatialBlend=preview?0f:.65f;audio.minDistance=1f;audio.maxDistance=7f;
            audio.pitch=preview?1f:Mathf.Clamp(GameSettings.Speed,.1f,3f);
            audio.volume=GameSettings.Volume*.8f;audio.clip=clip;audio.Play();
        }

        public static void Play(SfxType type, float volume = 1f)
        {
            if (GameSettings.Volume <= 0f)
                return;
            Ensure();
            if (instance.clips.TryGetValue(type, out var clip))
                instance.source.PlayOneShot(clip, volume * GameSettings.Volume);
        }

        static void Ensure()
        {
            if (instance != null)
                return;
            var go = new GameObject("Bluff Sfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Sfx>();
        }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            Build();
        }

        void Build()
        {
            clips[SfxType.Click] = Make("click", 0.06f, (t, rnd) => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 70f) * 0.35f);
            clips[SfxType.Card] = MakeNoise("card", 0.11f, 0.35f, 0.25f, 45f);
            clips[SfxType.Pass] = Make("pass", 0.14f, (t, rnd) => Mathf.Sin(2f * Mathf.PI * 260f * t) * Mathf.Exp(-t * 30f) * 0.45f);
            clips[SfxType.Turn] = Make("turn", 0.45f, (t, rnd) =>
                (Mathf.Sin(2f * Mathf.PI * 880f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 1320f * t)) * Mathf.Exp(-t * 7f) * 0.22f);
            clips[SfxType.Shoot] = Make("shoot", 0.55f, (t, rnd) =>
            {
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 14f) * 0.7f;
                float thump = Mathf.Sin(2f * Mathf.PI * (90f - 40f * t) * t) * Mathf.Exp(-t * 7f) * 0.8f;
                return Mathf.Clamp(noise + thump, -1f, 1f) * 0.7f;
            });
            clips[SfxType.Honest] = Make("honest", 0.6f, (t, rnd) => Arpeggio(t, new[] { 523.25f, 659.25f, 783.99f }, 0.09f) * 0.3f);
            clips[SfxType.Lie] = Make("lie", 0.7f, (t, rnd) =>
            {
                float f = t < 0.3f ? 330f - 60f * t : 220f - 40f * (t - 0.3f);
                float wave = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) * 0.5f + Mathf.Sin(2f * Mathf.PI * f * t) * 0.5f;
                float env = t < 0.3f ? Mathf.Exp(-t * 4f) : Mathf.Exp(-(t - 0.3f) * 5f);
                return wave * env * 0.22f;
            });
            clips[SfxType.Burn] = Make("burn", 0.7f, (t, rnd) =>
            {
                float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float env = Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI);
                return noise * env * 0.18f;
            });
            clips[SfxType.Take] = MakeNoise("take", 0.35f, 0.4f, 0.15f, 9f);
            clips[SfxType.Win] = Make("win", 1.3f, (t, rnd) => Arpeggio(t, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.13f) * 0.3f);
            clips[SfxType.Lose] = Make("lose", 1.1f, (t, rnd) => Arpeggio(t, new[] { 392f, 349.23f, 311.13f, 261.63f }, 0.18f) * 0.28f);
            clips[SfxType.Tick] = Make("tick", 0.03f, (t, rnd) => Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 150f) * 0.25f);
            clips[SfxType.Power] = Make("power", 0.9f, (t, rnd) =>
            {
                float shimmer = Mathf.Sin(2f * Mathf.PI * 2637f * t) * Mathf.Exp(-t * 6f) * 0.15f * (0.5f + 0.5f * Mathf.Sin(t * 60f));
                return Arpeggio(t, new[] { 659.25f, 880f, 1174.66f, 1567.98f }, 0.07f) * 0.26f + shimmer;
            });
            clips[SfxType.Deal] = MakeNoise("deal", 0.05f, 0.3f, 0.35f, 80f);
        }

        static float Arpeggio(float t, float[] notes, float step)
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float start = i * step;
                if (t < start)
                    break;
                float local = t - start;
                sum += Mathf.Sin(2f * Mathf.PI * notes[i] * local) * Mathf.Exp(-local * 3.5f);
            }
            return sum / notes.Length * 1.6f;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, System.Random, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float fadeOut = Mathf.Clamp01((count - i) / (SampleRate * 0.01f));
                data[i] = wave(t, rnd) * fadeOut;
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Desis kertas: noise yang di-low-pass sederhana dengan selubung cepat.</summary>
        static AudioClip MakeNoise(string name, float seconds, float gain, float smoothing, float decay)
        {
            float last = 0f;
            return Make(name, seconds, (t, rnd) =>
            {
                float n = (float)(rnd.NextDouble() * 2.0 - 1.0);
                last = Mathf.Lerp(last, n, smoothing);
                float attack = Mathf.Clamp01(t / 0.008f);
                return last * attack * Mathf.Exp(-t * decay) * gain * 2f;
            });
        }
    }
}
