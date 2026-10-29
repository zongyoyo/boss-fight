using System;
using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 角色 prefab 的根物件（T18）：公開這隻角色身上的玩家元件，以及鏡頭跟隨點與演出用的錨點。引用由 DaoDunDogBuilder 接好（編輯模式的預覽模型也讀得到）。
    /// PlayerSpawner 生成角色後呼叫 SetSceneReferences，把場景物件（Main Camera、鎖定與處決清單、AudioManager）填進角色；
    /// 再把這個物件交給所有 IPlayerBinding。
    /// 數值（T19）：spawner 依角色定義與修正建出 PlayerStats 複本後呼叫 ApplyStats，換進這隻角色的玩家元件；
    /// 角色以外要讀同一份數值的元件（CameraController 的視角靈敏度）每次從 Stats 讀：重新套用時會換成新的複本、舊的被銷毀，不要自己留著。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerRig : MonoBehaviour
    {
        [SerializeField] PlayerController controller;
        [SerializeField] PlayerHealth health;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] PlayerAttack attack;
        [SerializeField] PlayerFeedback feedback;
        [SerializeField] FeedbackEmitter feedbackEmitter;
        [SerializeField] DaoDunDogAnimator animator;
        [Tooltip("鏡頭跟隨與鎖定鏡頭群組裡的玩家節點")]
        [SerializeField] Transform cameraTarget;
        [Tooltip("演出特寫的臉部錨點（頭）")]
        [SerializeField] Transform faceAnchor;

        /// <summary>ApplyStats 之後送出；參數是新的數值。</summary>
        public event Action<PlayerStats> OnStatsApplied;

        public PlayerController Controller => controller;
        public PlayerHealth Health => health;
        public PlayerStamina Stamina => stamina;
        public PlayerAttack Attack => attack;
        public PlayerFeedback Feedback => feedback;
        public DaoDunDogAnimator Animator => animator;
        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;
        public Transform FaceAnchor => faceAnchor;
        /// <summary>目前套用的數值（還沒 ApplyStats 時是 prefab 上接的資產）。</summary>
        public PlayerStats Stats => controller != null ? controller.Stats : null;
        /// <summary>受擊範圍的 Collider（Hurtbox 那一個）；Boss 判定「玩家 Hurtbox 中心在圈內」、翻滾閃避用。</summary>
        public Collider HurtCollider
        {
            get
            {
                if (hurtCollider == null)
                {
                    var hurtbox = GetComponentInChildren<Hurtbox>(true);
                    hurtCollider = hurtbox != null ? hurtbox.GetComponent<Collider>() : null;
                }
                return hurtCollider;
            }
        }
        /// <summary>身體半徑（CharacterController 的 radius，m）。</summary>
        public float BodyRadius
        {
            get
            {
                if (body == null) body = GetComponent<CharacterController>();
                return body != null ? body.radius : 0f;
            }
        }

        Collider hurtCollider;
        CharacterController body;

        /// <summary>
        /// 換進 PlayerController／Health／Stamina／Attack 的數值。refill 時補滿血量、耐力與骨頭（生成時、休息區換裝或點技能後），
        /// 否則只把目前值夾到新上限以內（F12 改數值時）。最後送出 OnStatsApplied。
        /// </summary>
        public void ApplyStats(PlayerStats stats, bool refill)
        {
            if (stats == null) return;
            if (controller != null) controller.SetStats(stats);
            if (attack != null) attack.SetStats(stats);
            if (stamina != null) stamina.SetStats(stats, refill);
            if (health != null) health.SetStats(stats, refill);
            OnStatsApplied?.Invoke(stats);
        }

        /// <summary>生成後由 PlayerSpawner 呼叫：移動方向的鏡頭、可鎖定與可處決的目標、打擊回饋的 AudioManager。</summary>
        public void SetSceneReferences(Transform cameraTransform, IEnumerable<LockOnTarget> lockTargets,
                                       IEnumerable<ExecutionTarget> executionTargets, AudioManager audioManager)
        {
            if (controller != null)
            {
                controller.SetCameraTransform(cameraTransform);
                controller.SetLockTargets(lockTargets);
                controller.SetExecutionTargets(executionTargets);
            }
            if (feedbackEmitter != null) feedbackEmitter.SetAudioManager(audioManager);
        }
    }
}
