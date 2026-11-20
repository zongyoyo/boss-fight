using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BossFight.UI
{
    /// <summary>
    /// 傳送門挑戰清單一列上的鎖（T34）：中央一個金色鎖、四條金色鎖鏈拉到這一列的四個角。HubUIBuilder 建在樣板列底下（列複製時一起複製）。
    /// SetLocked 只管顯示；PlayUnlock 播解鎖演出：等 portalUnlockDelay 秒 → 鎖與鎖鏈越震越大 portalUnlockShakeTime 秒 →
    /// 藏起鎖與鎖鏈、在鎖與鎖鏈上撒碎片（shardTemplate 複製），碎片往外噴、受重力往下掉出畫面 → 全部碎片掉完（或 portalShardLifetime 秒）後呼叫 done。
    /// 用真實時間（暫停時介面不會開著，但傳送門開著時沒有暫停也照樣一致）。Stop 立刻收尾（藏起鎖、清掉碎片，不呼叫 done）。
    /// 數值都在 HubUIData 的「傳送門解鎖（T34）」。
    /// </summary>
    [DisallowMultipleComponent]
    public class PortalLockUI : MonoBehaviour
    {
        [SerializeField] HubUIData data;
        [Tooltip("震動時一起移動的容器（鎖與鎖鏈的父物件）")]
        [SerializeField] RectTransform shakeRoot;
        [Tooltip("中央的鎖")]
        [SerializeField] RectTransform padlock;
        [Tooltip("四條鎖鏈（從中央拉到四個角）")]
        [SerializeField] RectTransform[] chains;
        [Tooltip("一片碎片的樣板（停用的 Image）")]
        [SerializeField] Image shardTemplate;

        struct Shard
        {
            public RectTransform rect;
            public Vector2 velocity;
            public float spin;
        }

        readonly List<Shard> shards = new List<Shard>();
        readonly List<Image> pool = new List<Image>();
        Action done;
        float elapsed;
        bool playing, shattered;
        Vector2 rootPosition;
        Quaternion padlockRotation;

        public bool IsPlaying => playing;

        void Awake()
        {
            if (shakeRoot != null) rootPosition = shakeRoot.anchoredPosition;
            if (padlock != null) padlockRotation = padlock.localRotation;
            if (shardTemplate != null) shardTemplate.gameObject.SetActive(false);
        }

        /// <summary>顯示或藏起鎖（不播演出）；播到一半時先收掉。</summary>
        public void SetLocked(bool locked)
        {
            Stop();
            SetLockVisible(locked);
        }

        /// <summary>播解鎖演出，播完呼叫 done。</summary>
        public void PlayUnlock(Action done)
        {
            Stop();
            SetLockVisible(true);
            this.done = done;
            elapsed = 0f;
            shattered = false;
            playing = true;
        }

        /// <summary>立刻收尾：藏起鎖、清掉碎片，不呼叫 done。</summary>
        public void Stop()
        {
            playing = false;
            done = null;
            ClearShards();
            ResetShake();
            SetLockVisible(false);
        }

        void Update()
        {
            if (!playing || data == null) return;
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            float shakeStart = data.portalUnlockDelay;
            float shatterAt = shakeStart + data.portalUnlockShakeTime;
            if (!shattered)
            {
                if (elapsed >= shatterAt) Shatter();
                else if (elapsed >= shakeStart) Shake((elapsed - shakeStart) / Mathf.Max(data.portalUnlockShakeTime, 1e-3f));
                return;
            }
            UpdateShards(dt);
            if (shards.Count == 0 || elapsed >= shatterAt + data.portalShardLifetime) Finish();
        }

        // 越震越大：位置隨機抖、鎖本身左右搖
        void Shake(float t)
        {
            float envelope = Mathf.Lerp(data.portalUnlockShakeStart, 1f, t * t);
            float amplitude = data.portalUnlockShakeAmplitude * envelope;
            float phase = elapsed * data.portalUnlockShakeFrequency;
            var offset = new Vector2(Mathf.PerlinNoise(phase, 0.17f) - 0.5f, Mathf.PerlinNoise(0.53f, phase) - 0.5f) * (2f * amplitude);
            if (shakeRoot != null) shakeRoot.anchoredPosition = rootPosition + offset;
            if (padlock != null)
                padlock.localRotation = padlockRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * Mathf.PI * 2f) * data.portalUnlockShakeTilt * envelope);
        }

        void Shatter()
        {
            shattered = true;
            ResetShake();
            if (padlock != null) Burst(padlock, data.portalLockShards, data.lockColor, padlock.rect.size);
            if (chains != null)
            {
                foreach (var chain in chains)
                {
                    if (chain == null) continue;
                    // 碎片沿著鏈子分布
                    var size = chain.rect.size;
                    for (int i = 0; i < data.portalChainShards; i++)
                    {
                        float along = (i + UnityEngine.Random.value) / Mathf.Max(1, data.portalChainShards) * size.x;
                        var local = new Vector2(along - chain.pivot.x * size.x, 0f);
                        Spawn(ToOwnSpace(chain, local), data.chainColor);
                    }
                }
            }
            SetLockVisible(false);
        }

        void Burst(RectTransform source, int count, Color color, Vector2 size)
        {
            for (int i = 0; i < count; i++)
            {
                var local = new Vector2((UnityEngine.Random.value - 0.5f) * size.x, (UnityEngine.Random.value - 0.5f) * size.y);
                Spawn(ToOwnSpace(source, local), color);
            }
        }

        // source 的區域座標 → 這個物件（碎片的父物件）的區域座標
        Vector2 ToOwnSpace(RectTransform source, Vector2 local)
        {
            Vector3 world = source.TransformPoint(local);
            return transform.InverseTransformPoint(world);
        }

        void Spawn(Vector2 position, Color color)
        {
            if (shardTemplate == null) return;
            Image image = null;
            foreach (var pooled in pool)
            {
                if (!pooled.gameObject.activeSelf) { image = pooled; break; }
            }
            if (image == null)
            {
                image = Instantiate(shardTemplate, transform);
                pool.Add(image);
            }
            image.gameObject.SetActive(true);
            image.color = color;
            var rect = image.rectTransform;
            rect.anchoredPosition = position;
            float scale = Mathf.Lerp(data.portalShardScale.x, data.portalShardScale.y, UnityEngine.Random.value);
            rect.localScale = Vector3.one * scale;
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.value * 360f);
            // 從這一列的中心往外噴，再往上加一點
            Vector2 outward = position.sqrMagnitude > 1e-4f ? position.normalized : UnityEngine.Random.insideUnitCircle.normalized;
            float speed = Mathf.Lerp(data.portalShardSpeed.x, data.portalShardSpeed.y, UnityEngine.Random.value);
            var velocity = outward * speed + Vector2.up * data.portalShardLift * UnityEngine.Random.value;
            float spin = Mathf.Lerp(-data.portalShardSpin, data.portalShardSpin, UnityEngine.Random.value);
            shards.Add(new Shard { rect = rect, velocity = velocity, spin = spin });
        }

        void UpdateShards(float dt)
        {
            // 掉到畫面下緣再往下一段就收掉（Canvas 單位，參考解析度 1080 高）
            float floor = -data.portalShardFallDistance;
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                var s = shards[i];
                s.velocity += Vector2.down * data.portalShardGravity * dt;
                s.rect.anchoredPosition += s.velocity * dt;
                s.rect.localRotation *= Quaternion.Euler(0f, 0f, s.spin * dt);
                shards[i] = s;
                if (s.rect.anchoredPosition.y < floor)
                {
                    s.rect.gameObject.SetActive(false);
                    shards.RemoveAt(i);
                }
            }
        }

        void Finish()
        {
            var callback = done;
            playing = false;
            done = null;
            ClearShards();
            callback?.Invoke();
        }

        void ClearShards()
        {
            foreach (var s in shards)
            {
                if (s.rect != null) s.rect.gameObject.SetActive(false);
            }
            shards.Clear();
        }

        void ResetShake()
        {
            if (shakeRoot != null) shakeRoot.anchoredPosition = rootPosition;
            if (padlock != null) padlock.localRotation = padlockRotation;
        }

        void SetLockVisible(bool visible)
        {
            if (shakeRoot != null) shakeRoot.gameObject.SetActive(visible);
        }
    }
}
