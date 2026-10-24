using System;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BossFight.Progression
{
    /// <summary>場景裡的一個出生點：id（SceneFlowData 的 startArrival／portalArrival）與位置、朝向。</summary>
    [Serializable]
    public struct SceneSpawnPoint
    {
        [Tooltip("出生點 id")]
        public string id;
        [Tooltip("出生的位置與朝向（看 forward）")]
        public Transform point;
    }

    /// <summary>
    /// 換場景（T17）：休息區與 Boss 戰都放一個。GoToBattle（休息區的傳送門）、ReturnFromBattle（勝敗畫面與 ESC 選單的「返回休息區」）、
    /// ResetProfile（開發者模式的「重置存檔」：清空存檔、回到休息區的啟動出生點）。
    /// 換場景：黑幕用真實時間淡出（暫停中也會動），全黑時解除暫停、timeScale 回 1，把出生點 id 交給 ProfileStore，再載入新場景；換場景中的其他請求不理。
    /// 新場景的 SceneFlow 在 Start 取走出生點：有對應的出生點就把玩家瞬移過去、鏡頭轉到背後，再從全黑淡入；
    /// 直接進 Play 或重試（BattleManager.Retry）不是換場景進來的，不擺玩家也不淡入。T18 起擺玩家交給 PlayerSpawner。
    /// 黑幕是全螢幕黑色 Image 的 CanvasGroup（SceneFlowBuilder 建立，sortingOrder 1000），淡出時擋住滑鼠。
    /// 淡入淡出累加每幀的真實時間，但一幀最多算 MaxFadeStep 秒：載入新場景後的頭幾幀常卡上半秒，直接用時間差的話淡入會整段被吃掉。
    /// Battle 與 Hub 互不引用，兩邊都只靠這裡換場景。
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneFlow : MonoBehaviour
    {
        const float MaxFadeStep = 1f / 30f; // 純視覺：一幀最多推進的淡入淡出秒數

        [SerializeField] SceneFlowData data;
        [Tooltip("黑幕：全螢幕黑色 Image 的 CanvasGroup")]
        [SerializeField] CanvasGroup fade;
        [Tooltip("換場景前解除暫停（ESC 選單開著時）；可留空")]
        [SerializeField] GamePause pause;

        [Header("出生點")]
        [Tooltip("換場景進來時擺到出生點的玩家；留空時不擺")]
        [SerializeField] PlayerController player;
        [Tooltip("擺好玩家後把自由視角轉到背後；可留空")]
        [SerializeField] CameraController cameraController;
        [Tooltip("這個場景的出生點；換場景進來時依 id 找")]
        [SerializeField] SceneSpawnPoint[] spawnPoints = new SceneSpawnPoint[0];

        /// <summary>換場景中（淡出到載入新場景之間）。</summary>
        public bool IsLoading { get; private set; }

        string targetScene, targetArrival;
        float fadeElapsed, fadeFrom, fadeTo;
        float fadeDuration = -1f; // < 0 = 沒有在淡入淡出

        void Awake()
        {
            SetFade(0f);
        }

        void Start()
        {
            if (!ProfileStore.TakeArrival(out string arrival)) return;
            PlacePlayer(arrival);
            // 從全黑淡入
            SetFade(1f);
            BeginFade(0f, data != null ? data.fadeInTime : 0f);
        }

        void Update()
        {
            if (fadeDuration < 0f) return;
            fadeElapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFadeStep);
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            SetFade(Mathf.Lerp(fadeFrom, fadeTo, t));
            if (t < 1f) return;
            fadeDuration = -1f;
            if (IsLoading) LoadTarget();
        }

        /// <summary>從休息區進 Boss 戰（傳送門）。</summary>
        public void GoToBattle()
        {
            if (data != null) Load(data.battleScene, string.Empty);
        }

        /// <summary>從 Boss 戰回休息區，出生在傳送門前。</summary>
        public void ReturnFromBattle()
        {
            if (data != null) Load(data.hubScene, data.portalArrival);
        }

        /// <summary>清空存檔，回到休息區的啟動出生點（開發者模式的「重置存檔」）。</summary>
        public void ResetProfile()
        {
            if (data == null || IsLoading) return;
            ProfileStore.ResetProfile();
            Load(data.hubScene, data.startArrival);
        }

        void Load(string scene, string arrival)
        {
            if (IsLoading || string.IsNullOrEmpty(scene)) return;
            IsLoading = true;
            targetScene = scene;
            targetArrival = arrival;
            BeginFade(1f, data.fadeOutTime);
        }

        void LoadTarget()
        {
            if (pause != null) pause.SetPaused(false);
            Time.timeScale = 1f; // 慢動作、畫面停頓中離開時不要把 timeScale 帶到新場景
            ProfileStore.SetArrival(targetArrival);
            SceneManager.LoadScene(targetScene);
        }

        void PlacePlayer(string arrival)
        {
            if (player == null || string.IsNullOrEmpty(arrival)) return;
            foreach (var spawn in spawnPoints)
            {
                if (spawn.point == null || spawn.id != arrival) continue;
                Vector3 forward = Vector3.ProjectOnPlane(spawn.point.forward, Vector3.up);
                var rotation = forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : player.transform.rotation;
                player.Teleport(spawn.point.position, rotation);
                if (cameraController != null) cameraController.ResetBehind(spawn.point.position + rotation * Vector3.forward);
                return;
            }
            Debug.LogWarning($"[SceneFlow] 這個場景沒有出生點「{arrival}」，玩家留在原地。", this);
        }

        void BeginFade(float to, float duration)
        {
            fadeElapsed = 0f;
            fadeFrom = fade != null ? fade.alpha : to;
            fadeTo = to;
            fadeDuration = Mathf.Max(0f, duration);
            if (fade != null) fade.blocksRaycasts = to > 0f || IsLoading;
        }

        void SetFade(float alpha)
        {
            if (fade == null) return;
            fade.alpha = alpha;
            fade.blocksRaycasts = alpha > 0f || IsLoading;
        }
    }
}
