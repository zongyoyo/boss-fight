#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using BossFight.Battle;
using BossFight.Boss;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.DebugTools
{
    /// <summary>F8–F11 只讀 Player map 的開發動作；DeveloperMode 與暫停共用既有門控。</summary>
    public class PrologueActorDebugTools : MonoBehaviour
    {
        [SerializeField] PrologueActorBinding binding;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] TrainingSapling sapling;
        InputAction mode, whip, slap, invulnerable;

        void Awake()
        {
            var actions = InputSystem.actions;
            mode = actions.FindAction("Player/DebugSaplingMode", true);
            whip = actions.FindAction("Player/DebugMentorWhip", true);
            slap = actions.FindAction("Player/DebugMentorSlap", true);
            invulnerable = actions.FindAction("Player/DebugSaplingInvulnerability", true);
        }
        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (mode.WasPressedThisFrame())
            {
                if (!sapling.IsAlive) sapling.Appear();
                var next = sapling.Mode == SaplingMode.Idle ? SaplingMode.Attack :
                           sapling.Mode == SaplingMode.Attack ? SaplingMode.DodgeAttack :
                           sapling.Mode == SaplingMode.DodgeAttack ? SaplingMode.Target : SaplingMode.Idle;
                sapling.SetMode(next);
                sapling.SetInvulnerable(next != SaplingMode.Target);
                sapling.SetStaggerAccumulation(false);
                Debug.Log($"[T26] 小樹精模式 {ModeLabel(next)}；無敵 {sapling.Invulnerable}");
            }
            if (whip.WasPressedThisFrame()) binding.WhipPlayer();
            if (slap.WasPressedThisFrame()) mentor.Slap(sapling);
            if (invulnerable.WasPressedThisFrame())
            {
                sapling.SetInvulnerable(!sapling.Invulnerable);
                Debug.Log($"[T26] 小樹精無敵 {sapling.Invulnerable}");
            }
        }
        void OnGUI()
        {
            GUI.Label(new Rect(20f, 160f, 800f, 55f),
                $"F8 切模式　F9 抽打　F10 巴掌　F11 樹精無敵\n{ModeLabel(sapling.Mode)}　HP {sapling.Health:0}　軀幹 {sapling.Stagger:0}　無敵 {sapling.Invulnerable}");
        }
        static string ModeLabel(SaplingMode value) => value == SaplingMode.Attack ? "白色攻擊（格擋訓練）" :
            value == SaplingMode.DodgeAttack ? "紅色攻擊（閃避訓練）" : value == SaplingMode.Target ? "標靶" : "待機";
    }
}
#endif
