using System;
using BossFight.Core;
using BossFight.Progression;
using UnityEngine;

namespace BossFight.Title
{
    /// <summary>
    /// 標題畫面的流程（T32、T33）：存檔欄位（ProfileStore 的四格）的摘要、新遊戲、載入、刪除，以及退出遊戲。
    /// 新遊戲：ProfileStore.StartNewProfile（清空那一格並寫檔）→ SceneFlow.EnterProfile(true) 進序章；
    /// 載入：ProfileStore.SelectSlot → SceneFlow.EnterProfile(false)（序章完成或開發者模式略過序章 → 休息區，否則序章，原本 BootRouter 的分流）。
    /// 介面（UI/TitleMenuUI）只呼叫這裡的方法、訂閱這裡的事件。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleManager : MonoBehaviour
    {
        [Tooltip("換場景（黑幕淡出）")]
        [SerializeField] SceneFlow sceneFlow;
        [Tooltip("存檔欄位顯示角色名稱用（角色 id 換回 CharacterDefinition）")]
        [SerializeField] GameDatabase database;

        /// <summary>某一格的存檔被刪掉或改變（介面重讀摘要）。</summary>
        public event Action<int> OnSlotChanged;
        /// <summary>開始換場景（之後的按鍵不理）。</summary>
        public event Action OnLeaving;

        /// <summary>存檔欄位的數量。</summary>
        public int SlotCount => ProfileStore.SlotCount;

        /// <summary>已經在換場景。</summary>
        public bool IsLeaving { get; private set; }

        /// <summary>某一格的摘要（不切換欄位、不改檔案）。</summary>
        public ProfileSlotInfo GetSlot(int slot) => ProfileStore.PeekSlot(slot);

        /// <summary>存檔裡目前角色的顯示名稱；空的或找不到時用名冊的預設角色。</summary>
        public string CharacterName(ProfileData profile)
        {
            if (database == null) return string.Empty;
            var character = profile != null ? database.FindCharacter(profile.currentCharacter) : null;
            if (character == null) character = database.DefaultCharacter;
            return character != null ? character.displayName : string.Empty;
        }

        /// <summary>空欄位開新遊戲，有存檔的欄位載入（在已選的欄位上再點一下）。</summary>
        public void Play(int slot)
        {
            if (GetSlot(slot).IsEmpty) NewGame(slot);
            else Load(slot);
        }

        /// <summary>在這一格開新存檔，進序章。</summary>
        public void NewGame(int slot)
        {
            if (!BeginLeaving(slot)) return;
            ProfileStore.StartNewProfile(slot);
            sceneFlow.EnterProfile(true);
        }

        /// <summary>載入這一格，照存檔進序章或休息區。</summary>
        public void Load(int slot)
        {
            if (!BeginLeaving(slot)) return;
            ProfileStore.SelectSlot(slot);
            sceneFlow.EnterProfile(false);
        }

        /// <summary>刪除這一格的存檔。</summary>
        public void Delete(int slot)
        {
            if (IsLeaving || slot < 0 || slot >= SlotCount) return;
            ProfileStore.DeleteSlot(slot);
            OnSlotChanged?.Invoke(slot);
        }

        /// <summary>退出遊戲（編輯器裡停止 Play）。</summary>
        public void Quit()
        {
            if (IsLeaving) return;
            IsLeaving = true;
            OnLeaving?.Invoke();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        bool BeginLeaving(int slot)
        {
            if (IsLeaving || slot < 0 || slot >= SlotCount) return false;
            if (sceneFlow == null)
            {
                Debug.LogError("[Title] 沒有接 SceneFlow，不能換場景。", this);
                return false;
            }
            IsLeaving = true;
            OnLeaving?.Invoke();
            return true;
        }
    }
}
