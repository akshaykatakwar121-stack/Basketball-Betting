using UnityEngine;

namespace BasketballBetting
{
    public enum AudioCue
    {
        UiClick,
        UiBack,
        CrowdCheer,
        CrowdGroan,
        Whistle,
        ShoeSqueak,
        BallRelease,
        Rim,
        Backboard,
        Swish,
        Bounce,
        Block,
        BetPlace,
        BetWin,
        BetLose,
        MeterLock
    }

    public sealed class BasketballAudio : MonoBehaviour
    {
        AudioSource _sfx;
        AudioSource _betting;
        AudioSource _whistleSrc;
        AudioClip _bounce, _release, _rim, _backboard, _swish, _block;
        AudioClip _cheer, _groan, _squeak, _whistle;
        AudioClip _betPlace, _betWin, _betLose, _meterLock;

        public static BasketballAudio Create(Transform parent)
        {
            var go = new GameObject("Audio");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var audio = go.AddComponent<BasketballAudio>();
            audio.Build();
            return audio;
        }

        void Build()
        {
            _sfx = AddSource(1f);
            _betting = AddSource(1f);
            _whistleSrc = AddSource(1f);

            _bounce = Impact(180, 90, 0.11f, 0.45f);
            _release = SoftNoise(0.08f, 0.2f, 1600);
            _rim = Impact(520, 210, 0.16f, 0.5f);
            _backboard = Impact(140, 70, 0.18f, 0.42f);
            _swish = SoftNoise(0.2f, 0.36f, 4200);
            _block = Impact(95, 40, 0.14f, 0.55f);
            _cheer = CrowdYell(true);
            _groan = CrowdYell(false);
            _squeak = Impact(1180, 900, 0.05f, 0.14f);
            _whistle = RefereeWhistle();
            _betPlace = Chip();
            _betWin = WinSting();
            _betLose = Impact(110, 70, 0.22f, 0.22f);
            _meterLock = Impact(740, 520, 0.05f, 0.16f);
        }

        public void Play(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.CrowdCheer:
                    OneShot(_sfx, _cheer, 0.72f, 1.02f);
                    break;
                case AudioCue.CrowdGroan:
                    OneShot(_sfx, _groan, 0.5f, 0.94f);
                    break;
                case AudioCue.Whistle:
                    OneShot(_whistleSrc, _whistle, 0.7f, 1f);
                    break;
                case AudioCue.ShoeSqueak:
                    OneShot(_sfx, _squeak, 0.22f, Random.Range(0.94f, 1.12f));
                    break;
                case AudioCue.BallRelease:
                    OneShot(_sfx, _release, 0.5f, 1.05f);
                    break;
                case AudioCue.Rim:
                    OneShot(_sfx, _rim, 0.78f, Random.Range(0.94f, 1.06f));
                    break;
                case AudioCue.Backboard:
                    OneShot(_sfx, _backboard, 0.7f, 0.9f);
                    break;
                case AudioCue.Swish:
                    OneShot(_sfx, _swish, 0.8f, 1.15f);
                    break;
                case AudioCue.Bounce:
                    OneShot(_sfx, _bounce, 0.62f, 0.96f);
                    break;
                case AudioCue.Block:
                    OneShot(_sfx, _block, 0.82f, 0.78f);
                    break;
                case AudioCue.BetPlace:
                    OneShot(_betting, _betPlace, 0.65f, 1f);
                    break;
                case AudioCue.BetWin:
                    OneShot(_betting, _betWin, 0.7f, 1f);
                    break;
                case AudioCue.BetLose:
                    OneShot(_betting, _betLose, 0.35f, 0.88f);
                    break;
                case AudioCue.MeterLock:
                    OneShot(_betting, _meterLock, 0.22f, 1.05f);
                    break;
                case AudioCue.UiClick:
                    OneShot(_betting, _betPlace, 0.38f, 1.28f);
                    break;
                case AudioCue.UiBack:
                    OneShot(_betting, _betLose, 0.18f, 1.45f);
                    break;
            }
        }

        /// <summary>Physical contact during a shot → sound.</summary>
        public void HandleContact(Shooting.ShotContactKind kind)
        {
            switch (kind)
            {
                case Shooting.ShotContactKind.Rim: Play(AudioCue.Rim); break;
                case Shooting.ShotContactKind.Backboard: Play(AudioCue.Backboard); break;
                case Shooting.ShotContactKind.Floor: Play(AudioCue.Bounce); break;
                case Shooting.ShotContactKind.Defender:
                    Play(AudioCue.Block);
                    Play(AudioCue.CrowdGroan);
                    break;
            }
        }

        public void PlayRelease() => Play(AudioCue.BallRelease);

        public void PlayBasket()
        {
            Play(AudioCue.Swish);
            Play(AudioCue.CrowdCheer);
        }

        AudioSource AddSource(float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = volume;
            return src;
        }

        static void OneShot(AudioSource src, AudioClip clip, float volume, float pitch)
        {
            if (src == null || clip == null)
                return;
            src.pitch = pitch;
            src.PlayOneShot(clip, volume);
        }

        static AudioClip RefereeWhistle()
        {
            int samples = Mathf.CeilToInt(44100 * 0.62f);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / 44100f;
                float env = 0f;
                if (t < 0.22f)
                    env = t < 0.02f ? t / 0.02f : (t > 0.17f ? Mathf.Max(0f, 1f - (t - 0.17f) / 0.05f) : 1f);
                else if (t > 0.28f && t < 0.56f)
                {
                    float u = t - 0.28f;
                    env = u < 0.02f ? u / 0.02f : (u > 0.22f ? Mathf.Max(0f, 1f - (u - 0.22f) / 0.06f) : 1f);
                }
                float trill = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(t * 70f));
                float a = Mathf.Sin(2f * Mathf.PI * 2040f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 2440f * t);
                data[i] = (a * 0.52f + b * 0.48f) * trill * env * 0.48f;
            }
            return Clip("whistle", data);
        }

        static AudioClip CrowdYell(bool cheer)
        {
            int samples = Mathf.CeilToInt(44100 * (cheer ? 0.85f : 0.7f));
            var data = new float[samples];
            float[] hz = cheer
                ? new[] { 196f, 247f, 294f, 370f, 440f }
                : new[] { 147f, 175f, 196f, 220f };
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float env = Mathf.Sin(Mathf.PI * Mathf.Pow(t, cheer ? 0.7f : 1.1f));
                float s = 0f;
                for (int n = 0; n < hz.Length; n++)
                {
                    float vib = 1f + 0.012f * Mathf.Sin(2f * Mathf.PI * (5f + n) * i / 44100f);
                    s += Mathf.Sin(2f * Mathf.PI * hz[n] * vib * i / 44100f);
                }
                data[i] = s / hz.Length * env * (cheer ? 0.34f : 0.28f);
            }
            return Clip(cheer ? "cheer" : "groan", data);
        }

        static AudioClip Impact(float high, float low, float duration, float volume)
        {
            int samples = Mathf.CeilToInt(44100 * duration);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float env = Mathf.Pow(1f - i / (float)samples, 2.4f);
                float click = i < 80 ? Random.Range(-0.4f, 0.4f) * (1f - i / 80f) : 0f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * high * i / 44100f) * 0.35f
                           + Mathf.Sin(2f * Mathf.PI * low * i / 44100f) * 0.45f
                           + click) * env * volume;
            }
            return Clip("hit", data);
        }

        static AudioClip SoftNoise(float duration, float volume, float filter)
        {
            int samples = Mathf.CeilToInt(44100 * duration);
            var data = new float[samples];
            float prev = 0f;
            float a = filter / (filter + 44100f);
            for (int i = 0; i < samples; i++)
            {
                prev += a * (Random.Range(-1f, 1f) - prev);
                data[i] = prev * Mathf.Sin(Mathf.PI * i / (float)samples) * volume;
            }
            return Clip("noise", data);
        }

        static AudioClip Chip()
        {
            int samples = Mathf.CeilToInt(44100 * 0.1f);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float env = Mathf.Pow(1f - i / (float)samples, 5f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * 2400f * i / 44100f) * 0.25f
                           + Mathf.Sin(2f * Mathf.PI * 190f * i / 44100f) * 0.5f) * env;
            }
            return Clip("chip", data);
        }

        static AudioClip WinSting()
        {
            int[] hz = { 523, 659, 784, 1046 };
            int samples = Mathf.CeilToInt(44100 * 0.62f);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float env = Mathf.Pow(1f - i / (float)samples, 1.25f);
                float s = 0f;
                for (int n = 0; n < hz.Length; n++)
                    s += Mathf.Sin(2f * Mathf.PI * hz[n] * i / 44100f);
                data[i] = s / hz.Length * env * 0.38f;
            }
            return Clip("win", data);
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, 44100, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
