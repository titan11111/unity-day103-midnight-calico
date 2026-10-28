using UnityEngine;

namespace MidnightCalico
{
    /// <summary>
    /// 効果音・環境音をすべてコードで合成する（外部の音声ファイル不要）。
    /// </summary>
    public static class Sfx
    {
        const int SR = 22050;
        public static AudioClip StepWood, StepSoft, StepTile, Meow, MeowShort, MeowLong, Hiss, Purr, Crash, Clatter,
            Pickup, Cursor, Locked, Scratch, Ambient, ClockTick, Catch, Thud, Shake, Lick, Type;

        static System.Random rng = new System.Random(12345);
        static float Rnd() { return (float)(rng.NextDouble() * 2.0 - 1.0); }

        static AudioClip Make(string name, float[] d, bool loop = false)
        {
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        public static void Init()
        {
            if (Meow != null) return;
            StepWood = Step(90f, 0.5f, 0.10f);
            StepSoft = Step(70f, 0.15f, 0.12f);
            StepTile = Step(140f, 0.8f, 0.07f);
            Meow = MeowClip(0.62f, 520f, 820f, 560f);
            MeowShort = MeowClip(0.32f, 650f, 900f, 700f);
            MeowLong = MeowClip(1.1f, 480f, 760f, 420f);
            Hiss = HissClip();
            Purr = PurrClip();
            Crash = CrashClip(0.8f, true);
            Clatter = CrashClip(0.25f, false);
            Pickup = Jingle(new float[] { 660f, 880f, 1320f }, 0.12f);
            Catch = Jingle(new float[] { 523f, 659f, 784f, 1046f, 1318f }, 0.11f);
            Cursor = Tone(1200f, 0.04f, 0.25f);
            Type = Tone(1800f, 0.015f, 0.12f);
            Locked = LockedClip();
            Scratch = ScratchClip();
            Ambient = AmbientClip();
            ClockTick = TickClip();
            Thud = Step(55f, 0.3f, 0.25f);
            Shake = ShakeClip();
            Lick = LickClip();
        }

        static float[] Buf(float sec) { return new float[Mathf.Max(1, (int)(sec * SR))]; }

        static AudioClip Step(float thump, float noiseAmt, float len)
        {
            var d = Buf(len);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Exp(-t * 45f);
                lp += (Rnd() - lp) * 0.25f;
                d[i] = (Mathf.Sin(2 * Mathf.PI * thump * t) * 0.6f + lp * noiseAmt) * env * 0.7f;
            }
            return Make("step", d);
        }

        /// <summary>「ニャー」：基本周波数の上がり下がり＋口の開き（フォルマント）変化</summary>
        static AudioClip MeowClip(float len, float f0, float fPeak, float fEnd)
        {
            var d = Buf(len);
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float f = t < 0.35f ? Mathf.Lerp(f0, fPeak, t / 0.35f) : Mathf.Lerp(fPeak, fEnd, (t - 0.35f) / 0.65f);
                f *= 1f + 0.015f * Mathf.Sin(i / (float)SR * 2 * Mathf.PI * 6f);
                phase += 2 * Mathf.PI * f / SR;
                // m → i → a → o の口の形
                float formant = t < 0.15f ? 500f : (t < 0.45f ? Mathf.Lerp(500f, 1600f, (t - 0.15f) / 0.3f) : Mathf.Lerp(1600f, 700f, (t - 0.45f) / 0.55f));
                float s = 0f;
                for (int k = 1; k <= 8; k++)
                {
                    float hf = f * k;
                    float w = 1f / (1f + Mathf.Pow((hf - formant) / 450f, 2f)) + 0.25f / k;
                    s += Mathf.Sin((float)(phase * k)) * w;
                }
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1f - t) / 0.25f);
                d[i] = s * env * 0.16f + Rnd() * 0.01f * env;
            }
            return Make("meow", d);
        }

        static AudioClip HissClip()
        {
            var d = Buf(0.9f);
            float prev = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float n = Rnd();
                float hp = n - prev; prev = n;
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Pow(1f - t, 1.5f);
                d[i] = hp * env * 0.35f;
            }
            return Make("hiss", d);
        }

        static AudioClip PurrClip()
        {
            var d = Buf(1.0f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += (Rnd() - lp) * 0.05f;
                float am = Mathf.Pow(Mathf.Abs(Mathf.Sin(2 * Mathf.PI * 13f * t)), 3f);
                float breath = 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 1f * t);
                d[i] = (lp * 2.2f + Mathf.Sin(2 * Mathf.PI * 26f * t) * 0.3f) * am * breath * 0.5f;
            }
            return Make("purr", d, true);
        }

        static AudioClip CrashClip(float len, bool big)
        {
            var d = Buf(len);
            float[] pings = big ? new float[] { 2100f, 3370f, 5230f, 1480f, 4410f } : new float[] { 2600f, 4100f };
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float s = Rnd() * Mathf.Exp(-t * (big ? 18f : 40f)) * 0.5f;
                for (int k = 0; k < pings.Length; k++)
                {
                    float st = k * 0.035f;
                    if (t > st) s += Mathf.Sin(2 * Mathf.PI * pings[k] * (t - st)) * Mathf.Exp(-(t - st) * 14f) * 0.18f;
                }
                d[i] = s * 0.8f;
            }
            return Make("crash", d);
        }

        static AudioClip Jingle(float[] notes, float noteLen)
        {
            var d = Buf(noteLen * notes.Length + 0.5f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * noteLen * SR);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)SR;
                    float env = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t / 0.005f);
                    d[i] += (Mathf.Sin(2 * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * notes[n] * 2f * t)) * env * 0.18f;
                }
            }
            return Make("jingle", d);
        }

        static AudioClip Tone(float f, float len, float vol)
        {
            var d = Buf(len);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                d[i] = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t / len * 3f) * vol;
            }
            return Make("tone", d);
        }

        static AudioClip LockedClip()
        {
            var d = Buf(0.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float burst = (t % 0.12f) < 0.03f ? 1f : 0f;
                d[i] = (Rnd() * 0.4f + Mathf.Sin(2 * Mathf.PI * 180f * t) * 0.5f) * burst * Mathf.Exp(-t * 3f) * 0.6f;
            }
            return Make("locked", d);
        }

        static AudioClip ScratchClip()
        {
            var d = Buf(0.35f);
            float prev = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float n = Rnd(); float hp = n - prev; prev = n;
                float sweep = Mathf.Sin(t * Mathf.PI * 3f) > 0 ? 1f : 0.4f;
                d[i] = hp * sweep * (1f - t) * 0.5f;
            }
            return Make("scratch", d);
        }

        static AudioClip ShakeClip()
        {
            // カリカリの袋を振る音
            var d = Buf(0.6f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float grains = Rnd() > 0.92f ? Rnd() : 0f;
                float env = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 5f));
                d[i] = grains * env * 0.7f;
            }
            return Make("shake", d);
        }

        static AudioClip LickClip()
        {
            var d = Buf(0.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float p = (t % 0.16f) / 0.16f;
                float env = p < 0.3f ? Mathf.Sin(p / 0.3f * Mathf.PI) : 0f;
                d[i] = Rnd() * env * 0.25f;
            }
            return Make("lick", d);
        }

        /// <summary>深夜の家の空気（低いうなり＋すきま風）</summary>
        static AudioClip AmbientClip()
        {
            float len = 8f;
            var d = Buf(len);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                lp += (Rnd() - lp) * 0.01f;
                lp2 += (lp - lp2) * 0.02f;
                float wind = lp2 * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * t / len * 2f));
                float hum = Mathf.Sin(2 * Mathf.PI * 50f * t) * 0.05f + Mathf.Sin(2 * Mathf.PI * 75f * t) * 0.025f * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t / len));
                float drone = Mathf.Sin(2 * Mathf.PI * 41.2f * t) * 0.06f * (0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t / len * 1f));
                // ループのつなぎ目をなめらかに
                float edge = Mathf.Clamp01(t / 0.2f) * Mathf.Clamp01((len - t) / 0.2f);
                d[i] = (wind * 3.5f + hum + drone) * (0.85f + 0.15f * edge);
            }
            return Make("ambient", d, true);
        }

        static AudioClip TickClip()
        {
            var d = Buf(1.0f);
            for (int tick = 0; tick < 2; tick++)
            {
                int s = tick * SR / 2;
                float f = tick == 0 ? 2400f : 2000f;
                for (int i = 0; i < 600 && s + i < d.Length; i++)
                {
                    float t = i / (float)SR;
                    d[s + i] += Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 300f) * 0.4f;
                }
            }
            return Make("tick", d, true);
        }
    }

    public class AudioMan : MonoBehaviour
    {
        public static AudioMan I;
        AudioSource[] pool;
        int next;
        AudioSource ambient;

        void Awake()
        {
            I = this;
            Sfx.Init();
            pool = new AudioSource[6];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
            ambient = gameObject.AddComponent<AudioSource>();
            ambient.clip = Sfx.Ambient;
            ambient.loop = true;
            ambient.volume = 0.55f;
            ambient.playOnAwake = false;
        }

        public void StartAmbient() { if (!ambient.isPlaying) ambient.Play(); }

        public void Play2D(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (c == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.pitch = pitch;
            s.PlayOneShot(c, vol);
        }

        public void PlayAt(AudioClip c, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            if (c == null) return;
            var g = new GameObject("sfx");
            g.transform.position = pos;
            var s = g.AddComponent<AudioSource>();
            s.clip = c;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = 18f;
            s.volume = vol;
            s.pitch = pitch;
            s.Play();
            Destroy(g, c.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        /// <summary>3D のループ音（猫のゴロゴロ、柱時計）</summary>
        public static AudioSource Loop3D(GameObject host, AudioClip c, float vol, float maxDist)
        {
            var s = host.AddComponent<AudioSource>();
            s.clip = c;
            s.loop = true;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.5f;
            s.maxDistance = maxDist;
            s.volume = vol;
            s.playOnAwake = false;
            return s;
        }
    }
}
