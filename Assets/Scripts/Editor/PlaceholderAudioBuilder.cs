using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 程序合成演出用的佔位音效（44.1 kHz 單聲道 16-bit WAV），放在 Assets/Audio/Placeholder，每次重建覆寫：
    /// 腳步（沙地 4 步，1.3 秒）、風聲（8 秒無縫循環）、法陣嗡鳴（4 秒，前 2.5 秒漸強）、雷聲（1.5 秒，炸裂聲接低頻悶雷）、
    /// 唱片刮擦（0.55 秒，轉階段音樂中斷）。
    /// 之後有正式音效時，直接在 Timeline 的 Audio Track 換掉片段即可。選單 Tools > Audio > Build Placeholder Sounds；IntroCutsceneBuilder 也會呼叫。
    /// 戰鬥的打擊回饋音效（T11，BuildCombat）：揮刀、命中、格擋、盾反、受擊、預兆、Boss 的撞牆與落地…，檔名 Sfx_Combat_*；
    /// 由 CombatFeedbackBuilder 填進 CombatFeedbackData（只填空的或還是佔位音效的欄位，換成正式音效後重建不會蓋掉）。
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
            public AudioClip scratch;
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
                scratch = Save("Sfx_RecordScratch", Scratch()),
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

        // 唱片刮擦：音高快速來回晃、整體往下滑的鋸齒波，加一點沙沙的雜訊，0.55 秒內收掉
        static float[] Scratch()
        {
            var rnd = new System.Random(31);
            const float length = 0.55f;
            var s = new float[Seconds(length)];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float wobble = Mathf.Sin(2f * Mathf.PI * 9f * t);
                float f = Mathf.Lerp(900f, 140f, t / length) * (1f + 0.6f * wobble);
                phase += f / Rate;
                float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
                lp += (Noise(rnd) - lp) * 0.5f;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 3f) * Mathf.Clamp01((length - t) / 0.05f);
                s[i] = (saw * 0.6f + lp * 0.45f) * env;
            }
            return Normalize(s, 0.8f);
        }

        // ---------- 戰鬥音效（T11） ----------

        /// <summary>合成戰鬥音效，回傳「CombatFeedbackData 的欄位名稱 → 音效（變化版）」；落雷沿用 Sfx_Thunder。</summary>
        public static Dictionary<string, AudioClip[]> BuildCombat()
        {
            DaoDunDogBuilder.EnsureFolder(Dir);
            var thunder = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/Sfx_Thunder.wav");
            if (thunder == null) thunder = Save("Sfx_Thunder", Thunder());

            var r = new Dictionary<string, AudioClip[]>
            {
                ["swing"] = Variants("Swing", 3, v => Whoosh(101 + v, 0.18f + 0.02f * v, 500f * (1f + 0.08f * v), 2600f * (1f - 0.06f * v), 0.45f, 0.6f)),
                ["heavySwing"] = One("SwingHeavy", Whoosh(111, 0.38f, 250f, 1600f, 0.55f, 0.7f)),
                ["hit"] = Variants("Hit", 3, v => Thud(121 + v, 0.16f, 180f * (1f + 0.07f * v), 70f, 0.8f, 22f, 0.85f)),
                ["comboFinisherHit"] = One("HitFinisher", Thud(131, 0.26f, 160f, 55f, 1f, 14f, 0.9f)),
                ["heavyHit"] = One("HitHeavy", Mix(0.95f, (Thud(141, 0.45f, 140f, 40f, 1.2f, 8f, 1f), 0f, 1f),
                                                         (Metal(142, 0.3f, new[] { 900f, 1340f }, 18f, 0f, 1f), 0f, 0.25f))),
                ["executionHit"] = One("Execution", Mix(0.98f, (Thud(151, 0.8f, 120f, 30f, 1.4f, 5f, 1f), 0f, 1f),
                                                              (Metal(152, 0.9f, new[] { 660f, 990f, 1485f }, 4f, 0f, 1f), 0.02f, 0.35f))),
                ["hitDeflected"] = One("Deflect", Metal(161, 0.25f, new[] { 2200f, 3300f, 4700f }, 25f, 0.4f, 0.5f)),
                ["hurt"] = One("Hurt", Mix(0.9f, (Thud(171, 0.3f, 220f, 60f, 1.3f, 12f, 1f), 0f, 1f), (Tone(0.14f, 900f, 480f, 0f, 1f), 0.03f, 0.3f))),
                ["block"] = One("Block", Mix(0.85f, (Metal(181, 0.4f, new[] { 520f, 1230f, 2110f, 3400f }, 12f, 0.8f, 1f), 0f, 1f),
                                                   (Thud(182, 0.1f, 150f, 80f, 0.5f, 30f, 1f), 0f, 0.5f))),
                ["guardBreak"] = One("GuardBreak", Mix(0.95f, (Metal(191, 0.7f, new[] { 400f, 950f, 1600f }, 6f, 1f, 1f), 0f, 0.8f),
                                                             (Thud(192, 0.6f, 120f, 35f, 1.5f, 6f, 1f), 0f, 1f),
                                                             (Whoosh(193, 0.4f, 3000f, 300f, 0.1f, 1f), 0.05f, 0.4f))),
                ["parry"] = One("Parry", Mix(0.9f, (Metal(201, 1f, new[] { 1568f, 2349f, 3136f, 4699f }, 3.5f, 0.6f, 1f), 0f, 1f),
                                                  (Whoosh(202, 0.15f, 2000f, 6000f, 0.2f, 1f), 0f, 0.3f))),
                ["evade"] = One("Evade", Whoosh(211, 0.22f, 1200f, 4000f, 0.3f, 0.45f)),
                ["roll"] = One("Roll", Mix(0.5f, (Whoosh(221, 0.32f, 200f, 900f, 0.4f, 1f), 0f, 1f), (Thud(222, 0.08f, 120f, 70f, 0.6f, 40f, 1f), 0.25f, 0.4f))),
                ["jump"] = One("Jump", Thud(231, 0.12f, 140f, 260f, 0.6f, 25f, 0.45f)),
                ["heal"] = One("Heal", Mix(0.6f, (Thud(241, 0.12f, 300f, 120f, 1.5f, 30f, 1f), 0.05f, 1f),
                                                 (Thud(242, 0.12f, 280f, 110f, 1.5f, 30f, 1f), 0.4f, 0.9f),
                                                 (Thud(243, 0.12f, 320f, 120f, 1.5f, 30f, 1f), 0.75f, 1f))),
                ["boneEarned"] = One("BoneEarned", Mix(0.55f, (Metal(244, 0.25f, new[] { 1319f, 2637f }, 14f, 0f, 1f), 0f, 0.8f),
                                                             (Metal(245, 0.45f, new[] { 1976f, 3951f }, 8f, 0f, 1f), 0.09f, 1f))),
                ["telegraphBlockable"] = One("TelegraphWhite", Metal(251, 0.5f, new[] { 1760f, 2640f, 3520f }, 7f, 0f, 0.6f)),
                ["telegraphUnblockable"] = One("TelegraphRed", Buzz(0.45f, 200f, 0.18f, 0.05f, 0.7f)),
                ["pawSwipe"] = One("PawSwipe", Mix(0.6f, (Whoosh(261, 0.25f, 300f, 1800f, 0.5f, 1f), 0f, 1f), (Thud(262, 0.2f, 120f, 50f, 0.8f, 18f, 1f), 0.12f, 0.8f))),
                ["pawFinisher"] = One("PawFinisher", Mix(0.85f, (Whoosh(271, 0.3f, 300f, 1800f, 0.5f, 1f), 0f, 0.8f), (Zap(272, 0.5f, 1f), 0.12f, 0.8f),
                                                               (Thud(273, 0.35f, 110f, 40f, 1f, 10f, 1f), 0.12f, 1f))),
                ["wallSlam"] = One("WallSlam", Mix(1f, (Thud(281, 1f, 90f, 28f, 1.6f, 4f, 1f), 0f, 1f), (Whoosh(282, 0.8f, 1200f, 150f, 0.05f, 1f), 0.02f, 0.5f))),
                ["pounceLanding"] = One("PounceLanding", Thud(291, 0.5f, 110f, 35f, 1.2f, 8f, 0.85f)),
                ["phantomLanding"] = One("PhantomLanding", Mix(0.55f, (Thud(301, 0.35f, 160f, 60f, 0.6f, 12f, 1f), 0f, 1f),
                                                                    (Metal(302, 0.5f, new[] { 1200f, 1800f }, 6f, 0f, 1f), 0f, 0.3f))),
                ["shockwave"] = One("Shockwave", Mix(0.85f, (Thud(311, 0.4f, 100f, 40f, 1f, 8f, 1f), 0f, 1f), (Whoosh(312, 0.8f, 1500f, 200f, 0.15f, 1f), 0f, 0.7f))),
                ["lightning"] = new[] { thunder },
                ["tornado"] = One("Tornado", Whoosh(321, 1.4f, 200f, 700f, 0.6f, 0.75f)),
                ["grandSpinLanding"] = One("GrandSpinLanding", Mix(1f, (Thud(331, 1.6f, 70f, 22f, 2f, 2.5f, 1f), 0f, 1f),
                                                                     (Whoosh(332, 1.2f, 2000f, 150f, 0.05f, 1f), 0f, 0.6f),
                                                                     (Metal(333, 1.5f, new[] { 180f, 270f }, 2f, 0f, 1f), 0f, 0.2f))),
                ["stunned"] = One("Stunned", Mix(0.8f, (Tone(0.8f, 900f, 400f, 8f, 1f), 0f, 1f), (Tone(0.08f, 2400f, 3000f, 0f, 1f), 0.15f, 0.4f),
                                                      (Tone(0.08f, 2600f, 3200f, 0f, 1f), 0.45f, 0.4f))),
                ["defeated"] = One("BossDefeated", Mix(1f, (Thud(341, 1.4f, 80f, 25f, 1.5f, 3f, 1f), 0f, 1f), (Tone(1.2f, 500f, 100f, 5f, 1f), 0.1f, 0.3f))),
            };
            Debug.Log($"[Audio] 戰鬥佔位音效已建立：{Dir}/Sfx_Combat_*");
            return r;
        }

        static AudioClip[] One(string name, float[] samples) => new[] { Save("Sfx_Combat_" + name, samples) };

        static AudioClip[] Variants(string name, int count, System.Func<int, float[]> make)
        {
            var clips = new AudioClip[count];
            for (int v = 0; v < count; v++) clips[v] = Save($"Sfx_Combat_{name}_{v + 1}", make(v));
            return clips;
        }

        // 一階低通的係數（截止頻率 f）
        static float K(float f) => 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(1f, f) / Rate);

        // 收尾 20 ms 淡出，避免爆音
        static float Tail(float t, float dur) => Mathf.Clamp01((dur - t) / 0.02f);

        // 呼的一聲：帶通雜訊，中心頻率 f0 → f1 滑動；在 peakAt（0..1）最大聲
        static float[] Whoosh(int seed, float dur, float f0, float f1, float peakAt, float peak)
        {
            var rnd = new System.Random(seed);
            var s = new float[Seconds(dur)];
            float a = 0f, b = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, u = t / dur;
                float f = Mathf.Lerp(f0, f1, u);
                float n = Noise(rnd);
                a += (n - a) * K(f * 1.5f);
                b += (a - b) * K(f * 0.5f);
                float env = u < peakAt ? Mathf.SmoothStep(0f, 1f, u / peakAt) : Mathf.Pow(1f - (u - peakAt) / (1f - peakAt), 2f);
                s[i] = (a - b) * env * Tail(t, dur);
            }
            return Normalize(s, peak);
        }

        // 悶響：音高 f0 → f1 指數滑動的正弦，加一段低通雜訊的「啪」；decay 越大越短
        static float[] Thud(int seed, float dur, float f0, float f1, float noise, float decay, float peak)
        {
            var rnd = new System.Random(seed);
            var s = new float[Seconds(dur)];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, u = t / dur;
                float f = f0 * Mathf.Pow(f1 / f0, u);
                phase += f / Rate;
                float tone = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * decay);
                lp += (Noise(rnd) - lp) * K(2500f);
                float crack = lp * noise * Mathf.Exp(-t * decay * 3f);
                s[i] = (tone + crack) * Mathf.Clamp01(t / 0.002f) * Tail(t, dur);
            }
            return Normalize(s, peak);
        }

        // 金屬聲：幾個不成倍數的泛音各自衰減（高的衰減快），開頭一點雜訊的敲擊
        static float[] Metal(int seed, float dur, float[] freqs, float decay, float noise, float peak)
        {
            var rnd = new System.Random(seed);
            var s = new float[Seconds(dur)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, v = 0f;
                for (int k = 0; k < freqs.Length; k++)
                    v += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * Mathf.Exp(-t * decay * (1f + 0.5f * k)) / (1f + 0.3f * k);
                v += Noise(rnd) * noise * Mathf.Exp(-t * 200f);
                s[i] = v * Mathf.Clamp01(t / 0.001f) * Tail(t, dur);
            }
            return Normalize(s, peak);
        }

        // 滑音：f0 → f1 的正弦，可加顫音（vibrato Hz）
        static float[] Tone(float dur, float f0, float f1, float vibrato, float peak)
        {
            var s = new float[Seconds(dur)];
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate, u = t / dur;
                float f = Mathf.Lerp(f0, f1, u) * (1f + 0.06f * Mathf.Sin(2f * Mathf.PI * vibrato * t));
                phase += f / Rate;
                s[i] = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Clamp01(t / 0.01f) * (1f - u * 0.5f) * Tail(t, dur);
            }
            return Normalize(s, peak);
        }

        // 警告嗡嗡聲：兩個略為走音的方波脈衝（pulse 秒、間隔 gap 秒），低通後比較不刺耳
        static float[] Buzz(float dur, float f, float pulse, float gap, float peak)
        {
            var s = new float[Seconds(dur)];
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float local = t % (pulse + gap);
                float gate = local < pulse ? Mathf.Clamp01(local / 0.005f) * Mathf.Clamp01((pulse - local) / 0.01f) : 0f;
                float sq = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t)) + 0.7f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * 1.51f * t));
                lp += (sq - lp) * K(1800f);
                s[i] = lp * gate * Tail(t, dur);
            }
            return Normalize(s, peak);
        }

        // 電流聲：雜訊被 60 Hz 方波斷斷續續地開關，加一點 120 Hz 嗡聲
        static float[] Zap(int seed, float dur, float peak)
        {
            var rnd = new System.Random(seed);
            var s = new float[Seconds(dur)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)Rate;
                float gate = Mathf.Sin(2f * Mathf.PI * 60f * t) > 0.2f ? 1f : 0.15f;
                float v = Noise(rnd) * gate + 0.4f * Mathf.Sin(2f * Mathf.PI * 120f * t);
                s[i] = v * Mathf.Exp(-t * 6f) * Tail(t, dur);
            }
            return Normalize(s, peak);
        }

        // 疊加幾段（樣本, 開始秒數, 音量），整體正規化到 peak
        static float[] Mix(float peak, params (float[] samples, float at, float gain)[] parts)
        {
            int length = 0;
            foreach (var p in parts) length = Mathf.Max(length, Seconds(p.at) + p.samples.Length);
            var s = new float[length];
            foreach (var p in parts)
            {
                int offset = Seconds(p.at);
                for (int i = 0; i < p.samples.Length; i++) s[offset + i] += p.samples[i] * p.gain;
            }
            return Normalize(s, peak);
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
