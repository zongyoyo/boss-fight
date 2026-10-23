using System;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 遊戲暫停（ESC 選單）：Time.timeScale 0、音效暫停（AudioListener.pause；AudioManager 讓音樂不受影響，選單裡調音量聽得到）。
    /// LateUpdate 每幀壓住 timeScale：畫面停頓（HitStop）結束、勝利慢動作、F12 平衡面板關閉都會改 timeScale，放在 LateUpdate 才壓得住。
    /// 解除時 timeScale 回 1：勝利慢動作由 BattleManager 下一幀自己再設；F12 面板還開著的話它自己會再壓回 0。
    /// 玩家與鏡頭的輸入鎖定由開選單的一方（PauseMenuUI）處理；Debug 面板訂閱 OnPausedChanged，暫停時藏起來。
    /// </summary>
    [DisallowMultipleComponent]
    public class GamePause : MonoBehaviour
    {
        public bool IsPaused { get; private set; }

        /// <summary>暫停或解除；參數 true = 暫停。</summary>
        public event Action<bool> OnPausedChanged;

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            OnPausedChanged?.Invoke(paused);
        }

        void LateUpdate()
        {
            if (IsPaused) Time.timeScale = 0f;
        }

        // 暫停中重新載入場景或離開 Play：timeScale 與 AudioListener.pause 是全域的，不要帶走
        void OnDestroy()
        {
            if (!IsPaused) return;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
