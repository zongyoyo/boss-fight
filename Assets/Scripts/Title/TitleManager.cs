using System;
using BossFight.Core;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Title
{
    /// <summary>
    /// 標題畫面的流程（T32）：退出遊戲；T33 起加上存檔欄位（新遊戲、載入、刪除）與換場景。
    /// 介面（UI/TitleMenuUI）只呼叫這裡的方法、訂閱這裡的事件。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleManager : MonoBehaviour
    {
        [Tooltip("換場景（黑幕淡出）")]
        [SerializeField] SceneFlow sceneFlow;

        /// <summary>開始換場景（之後的按鍵不理）。</summary>
        public event Action OnLeaving;

        /// <summary>已經在換場景。</summary>
        public bool IsLeaving { get; private set; }

        /// <summary>退出遊戲（編輯器裡停止 Play）。</summary>
        public void Quit()
        {
            if (IsLeaving) return;
            IsLeaving = true;
            OnLeaving?.Invoke();
            ProfileStore.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
