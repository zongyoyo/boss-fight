using System;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 可以被處決的目標（Boss）。什麼時候能被處決由目標自己決定（Boss 頭暈時 SetExecutable(true)）；
    /// 玩家在 Inspector 列出候選目標，按處決鍵時選範圍內最近、IsExecutable 的一個，呼叫 TryBeginExecution，處決動作結束時呼叫 EndExecution。
    /// 處決進行中（IsBeingExecuted）目標要保持可被處決的狀態，等那一刀打到（Boss 的頭暈時間到也先不結束）。
    /// 傷害本身照常走 Hitbox → Hurtbox → IDamageable，DamageInfo.isExecution 為 true。
    /// 放在 Core，玩家不用引用 Boss（跟 LockOnTarget 一樣）。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExecutionTarget : MonoBehaviour
    {
        /// <summary>能不能被處決改變（T9 的「F 處決」提示用）。</summary>
        public event Action<bool> OnExecutableChanged;
        /// <summary>玩家開始處決這個目標。</summary>
        public event Action OnExecutionStarted;
        /// <summary>玩家的處決動作結束（不管有沒有打到）。</summary>
        public event Action OnExecutionEnded;

        /// <summary>現在可以被處決（物件停用時視為不行）。</summary>
        public bool IsExecutable => executable && isActiveAndEnabled;
        /// <summary>處決動作進行中。</summary>
        public bool IsBeingExecuted { get; private set; }

        bool executable;

        /// <summary>由目標自己設定（Boss 頭暈開始、結束）。不影響已經開始的處決。</summary>
        public void SetExecutable(bool value)
        {
            if (executable == value) return;
            executable = value;
            OnExecutableChanged?.Invoke(value);
        }

        /// <summary>玩家開始處決；不能被處決或已經在被處決時回傳 false。</summary>
        public bool TryBeginExecution()
        {
            if (!IsExecutable || IsBeingExecuted) return false;
            IsBeingExecuted = true;
            OnExecutionStarted?.Invoke();
            return true;
        }

        /// <summary>玩家的處決動作結束（打完、或中途被取消）。</summary>
        public void EndExecution()
        {
            if (!IsBeingExecuted) return;
            IsBeingExecuted = false;
            OnExecutionEnded?.Invoke();
        }

        void OnDisable()
        {
            SetExecutable(false);
            EndExecution();
        }
    }
}
