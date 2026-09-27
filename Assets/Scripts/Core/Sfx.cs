using System.Collections.Generic;
using UnityEngine;

namespace Taiyaki
{
    /// <summary>
    /// 외부 오디오 파일 없이 효과음과 BGM을 코드로 합성한다 (칩튠 풍).
    /// 나중에 Resources/Audio/{이름} 에 실제 파일을 넣으면 그것을 우선 사용한다.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource sfxSource, bgmSource;
        static string currentBgm;
        public static float SfxVolume = 0.55f;
        public static float BgmVolume = 0.32f;

        public static void Init(GameObject host)
        {
            sfxSource = host.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            bgmSource = host.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.volume = BgmVolume;
        }

        public static AudioSource BgmSource { get { return bgmSource; } }

        public static void Play(string name, float vol = 1f, float pitch = 1f)
        {
            if (sfxSource == null) return;
            var c = Get(name);
            if (c == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(c, vol * SfxVolume);
        }

        public static void Bgm(string name, bool restart = false)
        {
            if (bgmSource == null) return;
            if (currentBgm == name && bgmSource.isPlaying && !restart) return;
            currentBgm = name;
            if (string.IsNullOrEmpty(name)) { bgmSource.Stop(); return; }
            bgmSource.clip = Get(name);
            bgmSource.loop = name != "song";
            bgmSource.volume = BgmVolume;
            bgmSource.time = 0;
            bgmSource.Play();
        }

        public static AudioClip Get(string name)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) c = Make(name);
            clips[name] = c;
            return c;
        }

        // ───────────────────────── 합성 ─────────────────────────

        static float Freq(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

        static float Osc(int type, float phase)
        {
            phase -= Mathf.Floor(phase);
            switch (type)
            {
                case 0: return Mathf.Sin(phase * Mathf.PI * 2f);
                case 1: return phase < 0.5f ? 0.6f : -0.6f;                       // square
                case 2: return 1f - 4f * Mathf.Abs(phase - 0.5f);                  // triangle
                default: return (phase * 2f - 1f) * 0.7f;                          // saw
            }
        }

        static System.Random noise = new System.Random(1);
        static float Noise() { return (float)noise.NextDouble() * 2f - 1f; }

        static void Tone(float[] buf, float start, float dur, float f0, float f1, int type, float vol, float attack = 0.005f, float release = 0.05f)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            float ph = 0;
            for (int i = 0; i < n && s + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float k = (float)i / n;
                float f = Mathf.Lerp(f0, f1, k);
                ph += f / Rate;
                float env = Mathf.Min(1f, t / attack) * Mathf.Min(1f, (dur - t) / release);
                if (s + i >= 0) buf[s + i] += Osc(type, ph) * vol * env;
            }
        }

        static void NoiseBurst(float[] buf, float start, float dur, float vol, float lp = 0.3f, float decay = 8f)
        {
            int s = (int)(start * Rate), n = (int)(dur * Rate);
            float y = 0;
            for (int i = 0; i < n && s + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                y += (Noise() - y) * lp;
                if (s + i >= 0) buf[s + i] += y * vol * Mathf.Exp(-t * decay);
            }
        }

        static AudioClip Clip(string name, float[] data, bool normalize = true)
        {
            if (normalize)
            {
                float peak = 0.0001f;
                for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
                float g = Mathf.Min(1f, 0.9f / peak);
                for (int i = 0; i < data.Length; i++) data[i] *= g;
            }
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Make(string name)
        {
            float[] b;
            switch (name)
            {
                case "click":
                    b = new float[(int)(Rate * 0.06f)];
                    Tone(b, 0, 0.05f, 1400, 1100, 1, 0.4f);
                    return Clip(name, b);
                case "pour":
                    b = new float[(int)(Rate * 0.35f)];
                    NoiseBurst(b, 0, 0.35f, 0.6f, 0.08f, 5f);
                    Tone(b, 0, 0.3f, 300, 180, 0, 0.3f);
                    return Clip(name, b);
                case "fill":
                    b = new float[(int)(Rate * 0.2f)];
                    Tone(b, 0, 0.18f, 500, 260, 2, 0.5f);
                    NoiseBurst(b, 0, 0.12f, 0.3f, 0.2f, 20f);
                    return Clip(name, b);
                case "flip":
                    b = new float[(int)(Rate * 0.25f)];
                    Tone(b, 0, 0.12f, 300, 900, 1, 0.35f);
                    NoiseBurst(b, 0.05f, 0.2f, 0.5f, 0.4f, 12f);
                    return Clip(name, b);
                case "sizzle":
                    b = new float[(int)(Rate * 0.5f)];
                    NoiseBurst(b, 0, 0.5f, 0.35f, 0.9f, 3f);
                    return Clip(name, b);
                case "perfect":
                    b = new float[(int)(Rate * 0.5f)];
                    Tone(b, 0, 0.12f, Freq(84), Freq(84), 1, 0.4f);
                    Tone(b, 0.08f, 0.12f, Freq(88), Freq(88), 1, 0.4f);
                    Tone(b, 0.16f, 0.3f, Freq(91), Freq(91), 1, 0.4f, 0.005f, 0.25f);
                    return Clip(name, b);
                case "good":
                    b = new float[(int)(Rate * 0.25f)];
                    Tone(b, 0, 0.1f, Freq(76), Freq(76), 2, 0.5f);
                    Tone(b, 0.08f, 0.15f, Freq(79), Freq(79), 2, 0.5f);
                    return Clip(name, b);
                case "burn":
                    b = new float[(int)(Rate * 0.5f)];
                    Tone(b, 0, 0.45f, 180, 90, 3, 0.4f);
                    NoiseBurst(b, 0, 0.5f, 0.3f, 0.5f, 4f);
                    return Clip(name, b);
                case "coin":
                    b = new float[(int)(Rate * 0.3f)];
                    Tone(b, 0, 0.07f, Freq(83), Freq(83), 1, 0.35f);
                    Tone(b, 0.06f, 0.22f, Freq(88), Freq(88), 1, 0.35f, 0.003f, 0.2f);
                    return Clip(name, b);
                case "fail":
                    b = new float[(int)(Rate * 0.4f)];
                    Tone(b, 0, 0.15f, Freq(67), Freq(67), 1, 0.35f);
                    Tone(b, 0.14f, 0.25f, Freq(61), Freq(58), 1, 0.35f);
                    return Clip(name, b);
                case "notify":
                    b = new float[(int)(Rate * 0.4f)];
                    Tone(b, 0, 0.12f, Freq(81), Freq(81), 0, 0.5f);
                    Tone(b, 0.13f, 0.25f, Freq(86), Freq(86), 0, 0.5f, 0.005f, 0.2f);
                    return Clip(name, b);
                case "heart":
                    b = new float[(int)(Rate * 0.6f)];
                    for (int i = 0; i < 4; i++) Tone(b, i * 0.08f, 0.2f, Freq(72 + new[] { 0, 4, 7, 12 }[i]), Freq(72 + new[] { 0, 4, 7, 12 }[i]), 2, 0.4f, 0.005f, 0.15f);
                    return Clip(name, b);
                case "clink":
                    b = new float[(int)(Rate * 0.5f)];
                    Tone(b, 0, 0.45f, 2600, 2580, 0, 0.35f, 0.001f, 0.4f);
                    Tone(b, 0.02f, 0.4f, 3900, 3880, 0, 0.2f, 0.001f, 0.35f);
                    return Clip(name, b);
                case "door":
                    b = new float[(int)(Rate * 0.4f)];
                    NoiseBurst(b, 0, 0.3f, 0.5f, 0.1f, 10f);
                    Tone(b, 0.05f, 0.3f, Freq(88), Freq(88), 0, 0.25f, 0.002f, 0.25f);
                    Tone(b, 0.12f, 0.25f, Freq(84), Freq(84), 0, 0.2f, 0.002f, 0.2f);
                    return Clip(name, b);
                case "hit":
                    b = new float[(int)(Rate * 0.12f)];
                    Tone(b, 0, 0.1f, Freq(84), Freq(84), 1, 0.3f);
                    NoiseBurst(b, 0, 0.08f, 0.3f, 0.6f, 30f);
                    return Clip(name, b);
                case "neon":
                    b = new float[(int)(Rate * 0.25f)];
                    Tone(b, 0, 0.2f, 120, 120, 1, 0.25f);
                    NoiseBurst(b, 0, 0.1f, 0.3f, 0.9f, 25f);
                    return Clip(name, b);
                case "weekday": return Song(name, WeekdaySong());
                case "night": return Song(name, NightSong());
                case "weekend": return Song(name, WeekendSong());
                case "song": return Song(name, KaraokeSong());
                case "date": return Song(name, DateSong());
            }
            return null;
        }

        // ───────────────────────── 간단한 시퀀서 ─────────────────────────

        public class SongDef
        {
            public float bpm;
            public int bars;
            public string[] melody;   // 한 마디 = 8칸(8분음표). "." 쉼, "-" 지속, 숫자 = MIDI 노트
            public int[] chords;      // 마디별 루트 노트
            public bool minor;
            public int melodyWave = 2;
            public int bassWave = 2;
            public bool drums;
            public bool hat;
            public float swing;
        }

        public static float BeatSeconds(string song)
        {
            return 60f / (song == "song" ? KaraokeSong().bpm : 100f);
        }

        static SongDef WeekdaySong()
        {
            return new SongDef
            {
                bpm = 96, bars = 8, melodyWave = 2, bassWave = 2, hat = true,
                chords = new[] { 48, 45, 41, 43, 48, 45, 41, 43 },
                melody = new[]
                {
                    "72 - 76 - 79 - 76 -", "81 - 79 - 76 - . .", "77 - 76 - 74 - 72 -", "74 - - - 79 - . .",
                    "72 - 76 - 79 - 81 -", "84 - 81 - 79 - 76 -", "77 - 79 - 81 - 76 -", "72 - - - - - . .",
                }
            };
        }

        static SongDef NightSong()
        {
            return new SongDef
            {
                bpm = 72, bars = 8, melodyWave = 0, bassWave = 2,
                chords = new[] { 45, 41, 48, 43, 45, 41, 43, 43 },
                melody = new[]
                {
                    "76 - - - 72 - - -", "74 - - - 69 - - -", "72 - 74 - 76 - - -", "74 - - - - - . .",
                    "76 - - - 79 - - -", "77 - 76 - 74 - - -", "72 - 71 - 72 - 74 -", "69 - - - - - . .",
                }
            };
        }

        static SongDef WeekendSong()
        {
            return new SongDef
            {
                bpm = 118, bars = 8, melodyWave = 1, bassWave = 3, drums = true, hat = true, minor = true,
                chords = new[] { 45, 45, 41, 41, 43, 43, 40, 40 },
                melody = new[]
                {
                    "81 . 81 84 - 81 79 .", "76 - . . 76 79 81 -", "77 . 77 81 - 77 76 .", "72 - . . 74 76 77 -",
                    "79 . 79 83 - 79 77 .", "74 - . . 74 77 79 -", "76 . 80 - 83 - 80 -", "81 - - - . . . .",
                }
            };
        }

        static SongDef KaraokeSong()
        {
            return new SongDef
            {
                bpm = 120, bars = 20, melodyWave = 2, bassWave = 3, drums = true, hat = true,
                chords = new[] { 48, 48, 45, 45, 41, 43, 48, 43,  48, 45, 41, 43, 48, 45, 41, 43,  41, 43, 48, 48 },
                melody = new[]
                {
                    ". . . . . . . .", ". . . . . . . .", ". . . . . . . .", ". . . . . . . .",
                    "72 - 72 74 76 - 76 -", "77 - 76 74 72 - . .", "72 - 74 76 79 - 76 -", "74 - - - . . . .",
                    "76 - 76 77 79 - 79 -", "81 - 79 77 76 - . .", "77 - 77 79 81 - 79 77", "76 - 74 - 72 - . .",
                    "79 - 81 - 84 - 81 -", "79 - 76 - 77 - 76 74", "72 - 74 - 76 - 77 -", "79 - - - 74 - . .",
                    "77 - 76 - 74 - 72 -", "74 - 76 - 79 - . .", "84 - - - 79 - - -", "72 - - - - - . .",
                }
            };
        }

        static SongDef DateSong()
        {
            return new SongDef
            {
                bpm = 84, bars = 8, melodyWave = 0, bassWave = 2, hat = false,
                chords = new[] { 41, 43, 48, 45, 41, 43, 48, 48 },
                melody = new[]
                {
                    "77 - 81 - 84 - 81 -", "79 - 83 - 86 - 83 -", "84 - - - 79 - 76 -", "81 - - - - - . .",
                    "77 - 81 - 84 - 86 -", "83 - 81 - 79 - 81 -", "84 - - - 88 - - -", "84 - - - - - . .",
                }
            };
        }

        public static SongDef KaraokeChart { get { return KaraokeSong(); } }

        static AudioClip Song(string name, SongDef s)
        {
            float step = 60f / s.bpm / 2f;          // 8분음표
            float barLen = step * 8f;
            int total = (int)(barLen * s.bars * Rate) + Rate / 4;
            var b = new float[total];
            for (int bar = 0; bar < s.bars; bar++)
            {
                float bt = bar * barLen;
                int root = s.chords[bar % s.chords.Length];
                // 베이스: 반박마다
                for (int i = 0; i < 8; i += 2)
                    Tone(b, bt + i * step, step * 1.6f, Freq(root - 12 + (i == 4 ? 7 : 0)), Freq(root - 12 + (i == 4 ? 7 : 0)), s.bassWave, 0.22f, 0.005f, 0.08f);
                // 패드 화음(아주 약하게)
                int third = s.minor && (root % 12 == 9 || root % 12 == 4) ? 3 : (root % 12 == 9 || root % 12 == 4 || root % 12 == 2 ? 3 : 4);
                Tone(b, bt, barLen, Freq(root + 12), Freq(root + 12), 0, 0.05f, 0.08f, 0.2f);
                Tone(b, bt, barLen, Freq(root + 12 + third), Freq(root + 12 + third), 0, 0.04f, 0.08f, 0.2f);
                Tone(b, bt, barLen, Freq(root + 19), Freq(root + 19), 0, 0.04f, 0.08f, 0.2f);
                // 드럼
                for (int i = 0; i < 8; i++)
                {
                    float t = bt + i * step;
                    if (s.drums && (i == 0 || i == 4 || i == 3 && bar % 2 == 1)) Tone(b, t, 0.12f, 150, 45, 0, 0.55f, 0.001f, 0.1f);
                    if (s.drums && (i == 2 || i == 6)) NoiseBurst(b, t, 0.15f, 0.35f, 0.6f, 18f);
                    if (s.hat) NoiseBurst(b, t, 0.04f, i % 2 == 0 ? 0.08f : 0.05f, 0.95f, 60f);
                }
                // 멜로디
                var m = s.melody[bar % s.melody.Length].Split(' ');
                for (int i = 0; i < m.Length && i < 8; i++)
                {
                    int note;
                    if (!int.TryParse(m[i], out note)) continue;
                    int len = 1;
                    while (i + len < m.Length && m[i + len] == "-") len++;
                    Tone(b, bt + i * step, step * len * 0.95f, Freq(note), Freq(note), s.melodyWave, 0.2f, 0.01f, 0.06f);
                }
            }
            return Clip(name, b);
        }
    }
}
