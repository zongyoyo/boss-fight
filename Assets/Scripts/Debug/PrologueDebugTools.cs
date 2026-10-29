#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using BossFight.Battle;
using BossFight.Boss;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.DebugTools
{
    /// <summary>
    /// 序章專屬的 Debug 工具（F1–F7 與 F12 跟 OIIA 共用 BossDebugTools／BalanceDebugPanel）：
    /// F8 切換小樹精模式（待機 → 白色攻擊 → 紅色攻擊 → 標靶）、F9 菊花抽打玩家、F10 菊花甩小樹精巴掌、F11 小樹精無敵。
    /// F4（跟 Boss 面板同一顆）打開左邊玩家狀態下面的序章面板：跳到教學第 N 步、直接完成教學（接登場演出）、略過登場直接開戰、從守衛戰開始重來。
    /// 「從守衛戰開始重來」重新載入序章後自動略過教學與登場；這個要求放在 static 欄位（Debug 例外，進 Play 模式時清空）。
    /// 按鍵直接讀 Keyboard.current，不放進 Input Actions。
    /// </summary>
    [DisallowMultipleComponent]
    public class PrologueDebugTools : MonoBehaviour, IDebugCheats
    {
        [SerializeField] PrologueCutsceneFlow flow;
        [SerializeField] TutorialDirector tutorial;
        [SerializeField] PrologueActorBinding binding;
        [SerializeField] DaisyMentor mentor;
        [SerializeField] TrainingSapling sapling;
        [Tooltip("一開始就打開序章面板（F4 切換）")]
        [SerializeField] bool panelVisible;
        [Tooltip("按鍵提示停留時間（秒，真實時間）")]
        [Min(0.1f)] [SerializeField] float toastTime = 2f;

        // 跨重新載入：重新載入後自動開戰（只在 Debug）
        static bool restartAtBattle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => restartAtBattle = false;

        bool invulnerableOverridden, invulnerableBefore;
        bool started;
        string toast;
        float toastUntil;
        GUIStyle style, toastStyle;
        const float PanelTop = 350f; // 左上 PlayerDebugOverlay（10–340）的下面
        const float PanelWidth = 620f;

        void Update()
        {
            if (!started)
            {
                // 第一幀（所有 Start 跑完之後）才接「從守衛戰開始重來」
                started = true;
                if (restartAtBattle)
                {
                    restartAtBattle = false;
                    SkipToBattle();
                }
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f4Key.wasPressedThisFrame) panelVisible = !panelVisible;
            if (Time.deltaTime <= 0f) return; // 暫停（F12 面板）時不動角色
            if (kb.f8Key.wasPressedThisFrame) CycleSaplingMode();
            if (kb.f9Key.wasPressedThisFrame) Notify(binding.WhipPlayer() ? "F9：菊花抽打玩家" : "F9：菊花現在不能抽打（不在待機）");
            if (kb.f10Key.wasPressedThisFrame) Notify(mentor.Slap(sapling) ? "F10：菊花甩小樹精巴掌" : "F10：現在不能甩巴掌（菊花不在待機或小樹精不在）");
            if (kb.f11Key.wasPressedThisFrame) ToggleSaplingInvulnerable();
        }

        /// <summary>還原 F11 改過的小樹精無敵（關掉開發者模式時由 DeveloperMode 呼叫）。</summary>
        public void ClearCheats()
        {
            if (!invulnerableOverridden) return;
            invulnerableOverridden = false;
            if (sapling != null) sapling.SetInvulnerable(invulnerableBefore);
        }

        void CycleSaplingMode()
        {
            if (!sapling.IsAlive) sapling.Appear();
            var next = sapling.Mode == SaplingMode.Idle ? SaplingMode.Attack :
                       sapling.Mode == SaplingMode.Attack ? SaplingMode.DodgeAttack :
                       sapling.Mode == SaplingMode.DodgeAttack ? SaplingMode.Target : SaplingMode.Idle;
            sapling.SetMode(next);
            sapling.SetInvulnerable(next != SaplingMode.Target);
            sapling.SetStaggerAccumulation(false);
            Notify($"F8：小樹精模式 {ModeLabel(next)}（無敵 {(sapling.Invulnerable ? "開" : "關")}）");
        }

        void ToggleSaplingInvulnerable()
        {
            if (!invulnerableOverridden)
            {
                invulnerableOverridden = true;
                invulnerableBefore = sapling.Invulnerable;
            }
            sapling.SetInvulnerable(!sapling.Invulnerable);
            Notify($"F11：小樹精無敵 {(sapling.Invulnerable ? "開" : "關")}");
        }

        void JumpToStep(int index)
        {
            var step = tutorial.Data.steps[index];
            Notify(tutorial.DebugJumpToStep(index) ? $"跳到教學第 {index + 1} 步（{step.id}）" : "教學已經結束，不能跳步（按 F5 重新載入）");
        }

        void CompleteTutorial()
        {
            Notify(tutorial.DebugComplete() ? "直接完成教學，接登場演出" : "教學已經結束");
        }

        void SkipToBattle()
        {
            Notify(flow.DebugSkipEntrance() ? "略過教學與登場，直接開戰" : "守衛戰已經開始（或玩家倒下）");
        }

        void RestartAtBattle()
        {
            restartAtBattle = true;
            Notify("重新載入序章，從守衛戰開始");
            ((IBattleDebugTarget)flow).DebugRetry();
        }

        void Notify(string text)
        {
            Debug.Log($"[PrologueDebug] {text}");
            toast = text;
            toastUntil = Time.realtimeSinceStartup + toastTime;
        }

        static string ModeLabel(SaplingMode value) =>
            value == SaplingMode.Attack ? "白色攻擊（格擋訓練）" :
            value == SaplingMode.DodgeAttack ? "紅色攻擊（閃避訓練）" :
            value == SaplingMode.Target ? "標靶" : "待機";

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
                toastStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter };
                toastStyle.normal.textColor = Color.white;
            }

            // 跟 Boss 面板的提示錯開，放在畫面上方中央偏下
            if (toast != null && Time.realtimeSinceStartup < toastUntil)
            {
                var size = toastStyle.CalcSize(new GUIContent(toast));
                GUI.Box(new Rect((Screen.width - size.x) * 0.5f - 12f, 56f, size.x + 24f, size.y + 10f), toast, toastStyle);
            }

            string status = sapling != null
                ? $"小樹精 {ModeLabel(sapling.Mode)}　HP {sapling.Health:0}　軀幹 {sapling.Stagger:0}　無敵 {(sapling.Invulnerable ? "開" : "關")}{(sapling.IsAlive ? "" : "　（不在場上）")}"
                : "";
            if (!panelVisible)
            {
                GUI.Label(new Rect(10f, PanelTop, PanelWidth, 44f), $"<b>序章</b>　F4 序章面板　F8 切模式　F9 抽打　F10 巴掌　F11 樹精無敵\n{status}", style);
                return;
            }

            GUILayout.BeginArea(new Rect(10f, PanelTop, PanelWidth, Mathf.Min(320f, Screen.height - PanelTop - 10f)), GUI.skin.box);
            GUILayout.Label("<b>序章面板（F4）</b>　F8 切模式　F9 抽打　F10 巴掌　F11 樹精無敵", style);
            GUILayout.Label(status, style);
            int steps = tutorial.StepCount;
            string stepText = !tutorial.IsRunning ? "教學已結束" : tutorial.StepIndex < 0 ? "還沒開始（開場前）" :
                tutorial.StepIndex >= steps ? "結尾" : $"第 {tutorial.StepIndex + 1} / {steps} 步";
            GUILayout.Label($"教學  {stepText}", style);

            GUI.enabled = tutorial.IsRunning;
            GUILayout.BeginHorizontal();
            GUILayout.Label("跳到", style, GUILayout.Width(36f));
            for (int i = 0; i < steps; i++)
            {
                var step = tutorial.Data.steps[i];
                if (GUILayout.Button($"{i + 1} {StepName(step)}")) JumpToStep(i);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("直接完成教學（接登場演出）")) CompleteTutorial();
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("略過登場直接開戰（F1）")) SkipToBattle();
            if (GUILayout.Button("從守衛戰開始重來")) RestartAtBattle();
            if (GUILayout.Button("重新載入序章（F5）")) ((IBattleDebugTarget)flow).DebugRetry();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        static string StepName(TutorialStepData step)
        {
            switch (step.condition)
            {
                case TutorialCondition.Move: return "移動";
                case TutorialCondition.Heal: return "啃骨頭";
                case TutorialCondition.Dodge: return "閃避";
                case TutorialCondition.Block: return "格擋";
                case TutorialCondition.Hit: return "攻擊";
                case TutorialCondition.Execute: return "處決";
                default: return step.id;
            }
        }
    }
}
#endif
