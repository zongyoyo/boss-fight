#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BossFight.DebugTools
{
    /// <summary>
    /// Boss 戰的 Debug 快捷鍵與右上角的 Boss 面板。按鍵直接讀 Keyboard.current，不放進 Input Actions，正式 build 不會帶到。
    /// F1：Boss 血量設為「下一個階段的門檻 + 1」（第一階段時是 1001，再打一下就轉階段）。
    /// F2：切換玩家無敵。
    /// F3：依序切換下一招指定（不指定 → 各招 → 不指定），下一次選招直接出，不看距離、冷卻、不連出與開場序列。
    /// F4：開關 Boss 面板：狀態、階段、血量、距離、各招的招式池／距離／冷卻、開場序列進度、最近幾次選招與上次命中。
    /// 選招、換階段與按鍵都會在 Console 寫一行。狀態每幀讀 BossController／BossHealth，選招與命中訂閱事件。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossDebugTools : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [SerializeField] BossHealth bossHealth;
        [SerializeField] PlayerHealth playerHealth;
        [Tooltip("一開始就顯示 Boss 面板（F4 切換）")]
        [SerializeField] bool panelVisible;
        [Tooltip("每次選招都在 Console 寫一行")]
        [SerializeField] bool logMoveChoices = true;
        [Tooltip("面板保留最近幾次選招")]
        [Range(1, 20)] [SerializeField] int historySize = 8;
        [Tooltip("按鍵提示停留時間（秒，真實時間）")]
        [Min(0.1f)] [SerializeField] float toastTime = 2f;
        [Tooltip("面板寬度（像素）")]
        [Min(200f)] [SerializeField] float panelWidth = 470f;

        readonly List<string> history = new List<string>();
        int choiceCount;
        string lastHitText = "—";
        string toast;
        float toastUntil;
        GUIStyle style, toastStyle;
        readonly StringBuilder sb = new StringBuilder();

        void OnEnable()
        {
            if (boss == null) return;
            boss.OnMoveChosen += HandleMoveChosen;
            boss.OnMoveHit += HandleMoveHit;
            boss.OnPhaseChanged += HandlePhaseChanged;
        }

        void OnDisable()
        {
            if (boss == null) return;
            boss.OnMoveChosen -= HandleMoveChosen;
            boss.OnMoveHit -= HandleMoveHit;
            boss.OnPhaseChanged -= HandlePhaseChanged;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) SetHealthAboveNextThreshold();
            if (kb.f2Key.wasPressedThisFrame) TogglePlayerInvulnerable();
            if (kb.f3Key.wasPressedThisFrame) CycleForcedMove();
            if (kb.f4Key.wasPressedThisFrame) panelVisible = !panelVisible;
        }

        // ---------- 快捷鍵 ----------

        void SetHealthAboveNextThreshold()
        {
            if (boss == null || bossHealth == null) return;
            var next = boss.NextPhase;
            if (next == null)
            {
                Notify("F1：已經是最後一個階段，沒有下一個門檻");
                return;
            }
            float value = next.healthThreshold + 1f;
            bossHealth.DebugSetHealth(value);
            Notify($"F1：Boss 血量設為 {value:0}（{next.displayName}門檻 {next.healthThreshold:0} + 1）");
        }

        void TogglePlayerInvulnerable()
        {
            if (playerHealth == null) return;
            playerHealth.ForcedInvulnerable = !playerHealth.ForcedInvulnerable;
            Notify($"F2：玩家無敵 {(playerHealth.ForcedInvulnerable ? "開" : "關")}");
        }

        void CycleForcedMove()
        {
            if (boss == null || boss.Moves.Count == 0) return;
            var moves = boss.Moves;
            int index = -1;
            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] == boss.ForcedNextMove) index = i;
            }
            index++;
            var move = index < moves.Count ? moves[index] : null;
            boss.ForceNextMove(move);
            Notify(move != null ? $"F3：下一招指定 {move.DisplayName}" : "F3：不指定下一招");
        }

        void Notify(string text)
        {
            Debug.Log($"[BossDebug] {text}");
            toast = text;
            toastUntil = Time.realtimeSinceStartup + toastTime;
        }

        // ---------- 事件 ----------

        void HandleMoveChosen(BossMoveChoice choice)
        {
            choiceCount++;
            string source;
            switch (choice.source)
            {
                case MoveChoiceSource.Forced:
                    source = "F3 指定";
                    break;
                case MoveChoiceSource.Opening:
                    source = $"開場序列 {boss.OpeningIndex}/{boss.Phase.openingSequence.Length}";
                    break;
                default:
                    source = $"隨機（候選 {choice.candidateCount}）";
                    break;
            }
            string line = $"#{choiceCount} {choice.move.DisplayName}　{source}　距離 {choice.distance:0.0} m";
            history.Insert(0, line);
            if (history.Count > historySize) history.RemoveRange(historySize, history.Count - historySize);
            if (logMoveChoices) Debug.Log($"[Boss] 選招 {line}");
        }

        void HandleMoveHit(BossMove move, HitReport report)
        {
            string result;
            switch (report.result)
            {
                case DamageResult.Hit:
                    result = "<color=#ff7070>命中</color>";
                    break;
                case DamageResult.Blocked:
                    result = "<color=#9fd8ff>被格擋</color>";
                    break;
                case DamageResult.GuardBroken:
                    result = "<color=#ff9f40>破防</color>";
                    break;
                case DamageResult.Parried:
                    result = "<color=#ffe14d>被盾反</color>";
                    break;
                case DamageResult.Evaded:
                    result = "<color=#a0ffa0>被閃過（無敵）</color>";
                    break;
                default:
                    result = report.result.ToString();
                    break;
            }
            lastHitText = $"{move.DisplayName} {report.info.damage:0} → {result}";
        }

        void HandlePhaseChanged(BossPhaseData phase)
        {
            Debug.Log($"[Boss] 進入{phase.displayName}（招式池 {phase.movePool}）");
        }

        // ---------- 面板 ----------

        static string StateName(BossState s)
        {
            switch (s)
            {
                case BossState.Idle: return "待機";
                case BossState.ChooseMove: return "選招";
                case BossState.Telegraph: return "預兆";
                case BossState.Active: return "出招";
                case BossState.Recovery: return "破綻";
                case BossState.Stunned: return "頭暈";
                case BossState.Transition: return "轉階段";
                case BossState.Defeated: return "倒下";
                default: return s.ToString();
            }
        }

        static string RangeText(BossMoveData d) =>
            float.IsPositiveInfinity(d.maxDistance) ? $"{d.minDistance:0.#} m 以上" : $"{d.minDistance:0.#}–{d.maxDistance:0.#} m";

        string StateLine()
        {
            string s = $"<b>{StateName(boss.State)}</b>　{boss.StateTime:0.0}";
            switch (boss.State)
            {
                case BossState.Idle:
                    s += $" / {boss.IdleDuration:0.0} s";
                    break;
                case BossState.Telegraph:
                    s += $" / {boss.CurrentMove.TelegraphTime:0.0} s　{boss.CurrentMove.DisplayName}";
                    break;
                case BossState.Active:
                    s += $" s　{boss.CurrentMove.DisplayName}";
                    break;
                case BossState.Recovery:
                    s += $" / {boss.CurrentMove.RecoveryTime:0.0} s　{boss.CurrentMove.DisplayName}";
                    break;
                case BossState.Stunned:
                case BossState.Transition:
                    s += $" / {boss.TimedStateDuration:0.0} s";
                    break;
                case BossState.ChooseMove:
                    s += " s";
                    if (boss.RepositionGoal != null)
                        s += $"　走位 → {boss.RepositionGoal.DisplayName}（{RangeText(boss.RepositionGoal.Data)}）";
                    else
                        s += "　沒有能出的招，原地等";
                    break;
                default:
                    s += " s";
                    break;
            }
            return s;
        }

        string BuildPanelText()
        {
            sb.Clear();
            var phase = boss.Phase;
            float distance = boss.DistanceToTarget;

            sb.AppendLine($"<b>OIIA 貓</b>　{StateLine()}");
            sb.AppendLine($"階段  {phase.displayName}（招式池 {phase.movePool}）　間隔 {phase.moveIntervalMin:0.0#}–{phase.moveIntervalMax:0.0#} s　冷卻 ×{phase.cooldownMultiplier:0.##}");
            string invul = bossHealth.Invulnerable ? "　<color=#7fd4ff>無敵</color>" : "";
            sb.AppendLine($"HP  {bossHealth.Current:0} / {bossHealth.Max:0}{invul}");
            sb.AppendLine($"距離  {distance:0.00} m　上一招 {(boss.LastMove != null ? boss.LastMove.DisplayName : "—")}");

            var opening = phase.openingSequence;
            if (opening != null && opening.Length > 0)
            {
                sb.Append("開場序列  ");
                for (int i = 0; i < opening.Length; i++)
                {
                    if (i > 0) sb.Append(" → ");
                    string n = opening[i] != null ? opening[i].displayName : "?";
                    if (i < boss.OpeningIndex) sb.Append($"<color=#888888>{n} ✓</color>");
                    else if (i == boss.OpeningIndex) sb.Append($"<b>{n}</b>");
                    else sb.Append(n);
                }
                sb.AppendLine();
            }

            sb.AppendLine($"F3 指定  {(boss.ForcedNextMove != null ? boss.ForcedNextMove.DisplayName : "不指定")}　" +
                          $"F2 玩家無敵  {(playerHealth != null && playerHealth.ForcedInvulnerable ? "開" : "關")}");

            sb.AppendLine("招式（綠 = 現在會被隨機選到）");
            foreach (var move in boss.Moves)
            {
                var d = move.Data;
                bool inPool = d.phase == phase.movePool;
                bool inRange = d.InRange(distance);
                float cd = boss.CooldownRemaining(move);
                bool candidate = inPool && inRange && cd <= 0f && move != boss.LastMove && d.weight > 0f;
                string color = candidate ? "#7dff7d" : inPool ? "#ffffff" : "#777777";
                string cdText = cd > 0f ? $"冷卻 {cd:0.0} s" : "冷卻好";
                string rangeMark = inRange ? "距離內" : "距離外";
                string last = move == boss.LastMove ? "　上一招" : "";
                sb.AppendLine($"<color={color}>　{d.displayName}　池 {d.phase}　{RangeText(d)} {rangeMark}　{cdText}　權重 {d.weight:0.#}{last}</color>");
            }

            sb.AppendLine("最近選招");
            if (history.Count == 0) sb.AppendLine("　—");
            foreach (var line in history) sb.AppendLine($"　{line}");
            sb.Append($"上次命中  {lastHitText}");
            return sb.ToString();
        }

        void OnGUI()
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
                toastStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter };
                toastStyle.normal.textColor = Color.white;
            }

            if (toast != null && Time.realtimeSinceStartup < toastUntil)
            {
                var size = toastStyle.CalcSize(new GUIContent(toast));
                GUI.Box(new Rect((Screen.width - size.x) * 0.5f - 12f, 12f, size.x + 24f, size.y + 10f), toast, toastStyle);
            }

            if (boss == null || bossHealth == null) return;
            if (!panelVisible)
            {
                const string hint = "F1 Boss 血量→下一門檻+1　F2 玩家無敵　F3 指定下一招　F4 Boss 面板";
                var size = style.CalcSize(new GUIContent(hint));
                GUI.Label(new Rect(Screen.width - size.x - 12f, 10f, size.x + 4f, size.y), hint, style);
                return;
            }

            string text = BuildPanelText();
            float inner = panelWidth - 16f;
            float height = style.CalcHeight(new GUIContent(text), inner) + 16f;
            var rect = new Rect(Screen.width - panelWidth - 10f, 10f, panelWidth, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, inner, height - 16f), text, style);
        }
    }
}
#endif
