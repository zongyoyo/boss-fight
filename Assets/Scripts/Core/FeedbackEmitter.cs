using Unity.Cinemachine;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 播一個 FeedbackCue：音效經 AudioManager（2D），鏡頭震動用同一物件上的 CinemachineImpulseSource 在事件位置發出
    /// （鏡頭上要有 CinemachineImpulseListener，CameraRigBuilder 會加；開場與轉階段的演出鏡頭沒有，所以演出中不震）。
    /// 震動只動鏡頭，不動角色；離鏡頭越遠震得越小（CombatFeedbackData 的 shakeDistance／shakeSpread）。
    /// 玩家（PlayerFeedback）與 Boss（BossImpactFeedback）各掛一個；CombatFeedbackBuilder 接線。
    /// </summary>
    [DisallowMultipleComponent]
    public class FeedbackEmitter : MonoBehaviour
    {
        [SerializeField] CombatFeedbackData data;
        [Tooltip("播音效的 AudioManager（場景物件）；沒接時只有震動")]
        [SerializeField] AudioManager audioManager;
        [Tooltip("發出鏡頭震動；沒接時只有聲音")]
        [SerializeField] CinemachineImpulseSource impulse;

        public CombatFeedbackData Data => data;

        /// <summary>播音效的 AudioManager；生成的角色由 PlayerSpawner 設定（prefab 不能引用場景物件）。</summary>
        public void SetAudioManager(AudioManager value) => audioManager = value;

        // 同一幀同一組回饋只播一次：一波落雷 3–5 道同時劈下，聲音與震動不疊成好幾倍
        AudioClip[] lastClips;
        int lastFrame = -1;

        void Awake()
        {
            if (impulse == null || data == null) return;
            var def = impulse.ImpulseDefinition;
            def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
            def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            def.ImpulseDuration = data.shakeDuration;
            def.DissipationDistance = data.shakeDistance;
            def.DissipationRate = data.shakeSpread;
        }

        /// <summary>播一次：隨機挑一個音效，並在 position 發出 cue.shake × shakeScale 的震動。同一幀重複的同一組只算第一次。</summary>
        public void Play(in FeedbackCue cue, Vector3 position)
        {
            var clips = cue.clips;
            if (clips != null && clips == lastClips && Time.frameCount == lastFrame) return;
            lastClips = clips;
            lastFrame = Time.frameCount;

            if (audioManager != null && clips != null && clips.Length > 0)
                audioManager.PlaySfx(clips[Random.Range(0, clips.Length)], cue.volume);

            float strength = data != null ? cue.shake * data.shakeScale : 0f;
            if (impulse == null || strength <= 0f) return;
            // 往下為主、帶一點隨機的水平方向，每次晃的方向才不會一樣
            Vector2 side = Random.insideUnitCircle * 0.5f;
            Vector3 direction = new Vector3(side.x, -1f, side.y).normalized;
            impulse.GenerateImpulseAtPositionWithVelocity(position, direction * strength);
        }
    }
}
