using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace BossFight.EditorTools
{
    /// <summary>只在檔案不存在時生成序章專用合成音；不覆寫已調音檔或 OIIA。</summary>
    public static class PrologueAudioBuilder
    {
        public const string DirectoryPath = "Assets/Audio/Placeholder/Prologue";
        const int Rate = 22050;
        public static Dictionary<string, AudioClip> Build()
        {
            DaoDunDogBuilder.EnsureFolder(DirectoryPath);
            var result = new Dictionary<string, AudioClip>();
            foreach (var row in new[] { ("Giggle", 1f), ("Rumble", 4f), ("Crack", 1.5f), ("Eruption", 1.5f), ("Heartbeat", 0.65f),
                ("Roar", 2f), ("Wave", 1f), ("Wither", 2.5f), ("Sink", 2.5f), ("Surprise", 0.6f), ("Fire", 4f), ("Birds", 4f),
                ("Victory", 3f), ("Phase1", 8f), ("Phase2", 8f), ("Tutorial", 8f), ("Wind", 6f) })
            {
                string path = DirectoryPath + "/" + row.Item1 + ".wav";
                if (!File.Exists(path)) { Write(path, Synthesize(row.Item1, row.Item2)); AssetDatabase.ImportAsset(path); }
                else if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null) AssetDatabase.ImportAsset(path);
                result.Add(row.Item1, AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            return result;
        }
        static float[] Synthesize(string name, float duration)
        {
            var samples = new float[Mathf.RoundToInt(duration * Rate)];
            var random = new System.Random(name.Length * 97); float low = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate, u = t / duration;
                float noise = (float)random.NextDouble() * 2f - 1f; low += (noise - low) * 0.018f;
                float envelope = Mathf.Min(1f, t * 30f) * Mathf.Min(1f, (duration - t) * 12f);
                float value;
                if (name == "Tutorial")
                {
                    float beat = (t * 2f) % 1f; float[] melody = { 261.63f, 329.63f, 392f, 329.63f, 293.66f, 349.23f, 440f, 392f };
                    value = Mathf.Sin(t * melody[((int)(t * 2f)) % melody.Length] * 2f * Mathf.PI) * Mathf.Exp(-beat * 7f) * 0.2f;
                }
                else if (name == "Wind") value = low * 0.7f;
                else if (name == "Phase1" || name == "Phase2")
                {
                    float bpm = name == "Phase2" ? 132f : 108f, beat = (t * bpm / 60f) % 1f;
                    float bass = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-beat * 11f);
                    float hat = noise * Mathf.Exp(-((beat * 2f) % 1f) * 35f) * 0.12f;
                    value = bass * 0.45f + hat + Mathf.Sin(2f * Mathf.PI * 82.4f * t) * 0.08f;
                }
                else if (name == "Heartbeat") value = Mathf.Sin(2f * Mathf.PI * (48f - u * 15f) * t) * Mathf.Exp(-t * 9f) * 0.8f;
                else if (name == "Birds" || name == "Giggle" || name == "Surprise")
                    value = Mathf.Sin(2f * Mathf.PI * (name == "Birds" ? 1500f : 320f) * t + Mathf.Sin(t * 32f) * 10f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (name == "Birds" ? 8f : 25f))), 8f) * 0.25f;
                else if (name == "Victory")
                {
                    int note = Mathf.Min(3, (int)(t * 2f)); float[] notes = { 261.63f, 329.63f, 392f, 523.25f };
                    value = Mathf.Sin(t * notes[note] * 2f * Mathf.PI) * (0.25f + 0.1f * Mathf.Sin(t * notes[note] * 4f * Mathf.PI)) * Mathf.Exp(-u * 2f);
                }
                else if (name == "Fire") value = low * 0.5f + (noise > 0.992f ? noise * 0.35f : 0f);
                else if (name == "Rumble") value = (low * 2f + Mathf.Sin(t * 2f * Mathf.PI * 35f) * 0.28f) * u;
                else if (name == "Roar") value = (low * 2f + Mathf.Sin(t * 2f * Mathf.PI * (60f - u * 20f)) * 0.3f) * Mathf.Sin(u * Mathf.PI);
                else value = (low * 2f + noise * 0.25f) * Mathf.Exp(-u * 3f);
                samples[i] = Mathf.Clamp(value * envelope, -0.85f, 0.85f);
            }
            return samples;
        }
        static void Write(string path, float[] samples)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (float sample in samples) w.Write((short)(sample * short.MaxValue));
            }
        }
    }
}
