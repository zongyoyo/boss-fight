using System.Collections.Generic;
using BossFight.Core;
using UnityEngine;

namespace BossFight.Player
{
    /// <summary>
    /// 角色 prefab 的根物件（T18）：公開這隻角色身上的玩家元件，以及鏡頭跟隨點與演出用的錨點。引用由 DaoDunDogBuilder 接好（編輯模式的預覽模型也讀得到）。
    /// PlayerSpawner 生成角色後呼叫 SetSceneReferences，把場景物件（Main Camera、鎖定與處決清單、AudioManager）填進角色；
    /// 再把這個物件交給所有 IPlayerBinding。
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

        public PlayerController Controller => controller;
        public PlayerHealth Health => health;
        public PlayerStamina Stamina => stamina;
        public PlayerAttack Attack => attack;
        public PlayerFeedback Feedback => feedback;
        public DaoDunDogAnimator Animator => animator;
        public Transform CameraTarget => cameraTarget != null ? cameraTarget : transform;
        public Transform FaceAnchor => faceAnchor;

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
