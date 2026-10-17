using System.IO;
using UnityEditor;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 程序合成開場用的佔位音效（44.1 kHz 單聲道 16-bit WAV），放在 Assets/Audio/Placeholder，每次重建覆寫：
    /// 腳步（沙地 4 步，1.3 秒）、風聲（8 秒無縫循環）、法陣嗡鳴（4 秒，前 2.5 秒漸強）、雷聲（1.5 秒，炸裂聲接低頻悶雷）。
    /// 之後有正式音效時，直接在 Timeline 的 Audio Track 換掉片段即可。選單 Tools > Audio > Build Placeholder Sounds；IntroCutsceneBuilder 也會呼叫。
    /// </summary>
    public static class PlaceholderAudioBuilder
    {
        public const string Dir = "Assets/Audio/Placeholder";
        const int Rate = 44100;

        public struct Result
        {
            public AudioClip footsteps;
            public AudioClip wind;
            public AudioClip hum;
            public AudioClip thunder;
        }

        [MenuItem("Tools/Audio/Build Placeholder Sounds")]
        public static void BuildFromMenu() => Build();

        public static Result Build()
        {
            DaoDunDogBuilder.EnsureFolder(Dir);
            var r = new Result
            {
                footsteps = Save("Sfx_Footsteps", Footsteps()),
                wind = Save("Amb_Wind", Wind()),
                hum = Save("Sfx_CircleHum", Hum()),
                thunder = Save("Sfx_Thunder", Thunder()),
            };
            Debug.Log($"[Audio] 佔位音效已建立：{Dir}");
            return r;
        }

        // ---------- 合成 ----------

        // 沙地腳步：每步是一小段低通雜訊加一點低頻悶響
        static float[] Footsteps()
        {
            var rnd = new System.Random(11);
            var s = new float[Seconds(1.3f)];
            float[] steps = { 0.04f, 0.37f, 0.70f, 1.02f };
            foreach (float start in steps)
            {
                int i0 = Seconds(start);
                float lp = 0f;
                for (int i = 0; i < Seconds(0.16f) && i0 + i < s.Length; i++)
                {
                    float t = i / (float)Rate;
                    lp += (Noise(rnd) - lp) * 0.25f;
                    float env = Mathf.Exp(-t * 30f);
                    float thump = Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t * 45f);
                    s[i0 + i] += (lp * 0.9f + thump * 0.6f) * env;
                }
            }
            return Normalize(s, 0.6f);
        }

        // 風：低通雜訊，截止頻率與音量慢慢起伏；多做 1 秒和開頭交叉淡化，循環時接得起來
        static float[] Wind()
        {
            var rnd = new System.Random(23);
            int loop = Seconds(8f), fade = Seconds(1f);
            var raw = new float[loop + fade];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)Rate;
                float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.125f * t) + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.375f * t + 1.3f);
                float k = 0.015f + 0.02f * gust;
                lp += (Noise(rnd) - lp) * k;
                lp2 += (lp - lp2) * k;
                raw[i] = lp2 * gust;
            }
            var s = new float[loop];
            for (int i = 0; i < loop; i++)
            {
                float w = i < fade ? i / (float)fade : 1f;
                s[i] = raw[i] * w + (i < fade ? raw[loop + i] * (1f - w) : 0f);
            }
            return Normalize(s, 0.5f);
        }

        // 嗡鳴：55 Hz 加泛音、微微走音產生拍頻，前 2.5 秒漸強，最後 30 ms 收掉避免爆音
        static float[] Hum()
        {
            var s = new float[Seconds(4f)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float v = Mathf.Sin(2f * Mathf.PI * 55f * t)
                          + 0.6f * Mathf.Sin(2f * Mathf.PI * 55.7f * t)
                          + 0.45f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                          + 0.25f * Mathf.Sin(2f * Mathf.PI * 165.4f * t)
                          + 0.15f * Mathf.Sin(2f * Mathf.PI * 220f * t);
                float swell = Mathf.Clamp01(t / 2.5f);
                float env = swell * swell * (1f + 0.08f * Mathf.Sin(2f * Mathf.PI * 5f * t));
                float tail = Mathf.Clamp01((4f - t) / 0.03f);
                s[i] = v * env * tail;
            }
            return Normalize(s, 0.7f);
        }

        // 雷：開頭 0.12 秒的白雜訊炸裂，接著低通悶雷忽大忽小地衰減，1.5 秒內收完
        static float[] Thunder()
        {
            var rnd = new System.Random(37);
            var s = new float[Seconds(1.5f)];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise(rnd);
                float crack = n * Mathf.Exp(-t * 28f);
                lp += (n - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.02f;
                float flicker = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 7f * t) * Mathf.Sin(2f * Mathf.PI * 2.3f * t);
                float rumble = lp2 * 14f * flicker * Mathf.Exp(-t * 2.4f) * Mathf.Clamp01(t / 0.05f);
                float tail = Mathf.Clamp01((1.5f - t) / 0.3f);
                s[i] = (crack * 0.8f + rumble) * tail;
            }
            return Normalize(s, 0.95f);
        }

        // ---------- 工具 ----------

        static int Seconds(float sec) => Mathf.RoundToInt(sec * Rate);

        static float Noise(System.Random rnd) => (float)(rnd.NextDouble() * 2.0 - 1.0);

        static float[] Normalize(float[] s, float peak)
        {
            float max = 1e-6f;
            foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
            return s;
        }

        static AudioClip Save(string name, float[] samples)
        {
            string path = $"{Dir}/{name}.wav";
            string full = Path.Combine(Directory.GetCurrentDirectory(), path);
            using (var fs = new FileStream(full, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);      // PCM
                w.Write((short)1);      // 單聲道
                w.Write(Rate);
                w.Write(Rate * 2);      // byte rate
                w.Write((short)2);      // block align
                w.Write((short)16);     // bits
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                foreach (float v in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * short.MaxValue));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
