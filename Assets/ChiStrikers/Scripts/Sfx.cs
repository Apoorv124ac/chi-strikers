using UnityEngine;

namespace ChiStrikers
{
    /// <summary>All sounds are synthesised at start-up, so the game needs no audio files.</summary>
    public static class Sfx
    {
        const int Rate = 44100;
        static AudioSource oneShots, crowd;
        public static AudioClip Kick, Pass, Whistle, LongWhistle, Roar, Whoosh, Boom, Chime, Thud, Crowd;

        public static void Init(GameObject host)
        {
            oneShots = host.AddComponent<AudioSource>();
            oneShots.playOnAwake = false;
            crowd = host.AddComponent<AudioSource>();
            crowd.loop = true;
            crowd.volume = 0.22f;

            var rng = new System.Random(7);
            float Noise() => (float)(rng.NextDouble() * 2 - 1);

            Kick = Make("kick", 0.18f, (t, i) => Mathf.Sin(2 * Mathf.PI * (140 - 260 * t) * t) * Mathf.Exp(-t * 28) * 0.9f + Noise() * Mathf.Exp(-t * 90) * 0.5f);
            Pass = Make("pass", 0.12f, (t, i) => Mathf.Sin(2 * Mathf.PI * 190 * t) * Mathf.Exp(-t * 40) * 0.6f + Noise() * Mathf.Exp(-t * 120) * 0.3f);
            Thud = Make("thud", 0.3f, (t, i) => Mathf.Sin(2 * Mathf.PI * (90 - 100 * t) * t) * Mathf.Exp(-t * 14) * 0.9f + Noise() * Mathf.Exp(-t * 30) * 0.25f);
            Whistle = Make("whistle", 0.35f, (t, i) => Mathf.Sin(2 * Mathf.PI * (2900 + 120 * Mathf.Sin(t * 2 * Mathf.PI * 32)) * t) * 0.35f * Env(t, 0.35f, 0.01f, 0.05f));
            LongWhistle = Make("longwhistle", 1.1f, (t, i) =>
            {
                float seg = t < 0.3f ? 1 : t < 0.42f ? 0 : 1;
                return Mathf.Sin(2 * Mathf.PI * (2900 + 120 * Mathf.Sin(t * 2 * Mathf.PI * 32)) * t) * 0.33f * seg * Env(t, 1.1f, 0.01f, 0.1f);
            });
            float lp = 0;
            Roar = Make("roar", 3.2f, (t, i) =>
            {
                lp += (Noise() - lp) * 0.08f;
                return lp * 2.2f * Env(t, 3.2f, 0.25f, 1.8f);
            });
            float lp2 = 0;
            Whoosh = Make("whoosh", 0.8f, (t, i) =>
            {
                float k = Mathf.Lerp(0.02f, 0.35f, t / 0.8f);
                lp2 += (Noise() - lp2) * k;
                return lp2 * 1.4f * Env(t, 0.8f, 0.3f, 0.3f);
            });
            Boom = Make("boom", 1.2f, (t, i) => (Mathf.Sin(2 * Mathf.PI * (55 - 30 * t) * t) * 0.9f + Noise() * 0.35f * Mathf.Exp(-t * 6)) * Mathf.Exp(-t * 3.5f));
            Chime = Make("chime", 0.9f, (t, i) => (Mathf.Sin(2 * Mathf.PI * 880 * t) + 0.6f * Mathf.Sin(2 * Mathf.PI * 1320 * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * 1760 * t)) * 0.18f * Mathf.Exp(-t * 4));
            float lp3 = 0, lp4 = 0;
            Crowd = Make("crowd", 6f, (t, i) =>
            {
                lp3 += (Noise() - lp3) * 0.05f;
                lp4 += (lp3 - lp4) * 0.2f;
                float swell = 0.8f + 0.2f * Mathf.Sin(t / 6f * 2 * Mathf.PI * 2);
                return lp4 * 3f * swell;
            });
            crowd.clip = Crowd;
            crowd.Play();
        }

        static float Env(float t, float len, float attack, float release)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = Mathf.Clamp01((len - t) / release);
            return a * r;
        }

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, i), -1, 1);
            var c = AudioClip.Create(name, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        public static void Play(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (!oneShots || !c) return;
            oneShots.pitch = pitch;
            oneShots.PlayOneShot(c, vol);
        }

        public static void CrowdLevel(float v) { if (crowd) crowd.volume = Mathf.MoveTowards(crowd.volume, v, Time.unscaledDeltaTime * 0.5f); }
    }
}
