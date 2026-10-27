#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using BossFight.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
namespace BossFight.DebugTools
{
    public class GuardianDebugTools : MonoBehaviour
    {
        [SerializeField] GuardianPhaseController guardian;
        [SerializeField] GuardianPhaseTwo phaseTwo;
        [SerializeField] PrologueOutcome outcome;
        InputAction start, barrage, nextMove, phase2;
        int selected = -1;
        void Awake()
        {
            start = InputSystem.actions?.FindAction("Player/DebugGuardianBattle", true);
            barrage = InputSystem.actions?.FindAction("Player/DebugGuardianBarrage", true);
            nextMove = InputSystem.actions?.FindAction("Player/DebugGuardianNextMove", true);
            phase2 = InputSystem.actions?.FindAction("Player/DebugGuardianPhase2", true);
        }
        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (start != null && start.WasPressedThisFrame()) guardian.StartBattle();
            if (barrage != null && barrage.WasPressedThisFrame()) guardian.RequestRootBarrage();
            if (phase2 != null && phase2.WasPressedThisFrame()) phaseTwo.DebugEnterPhase2();
            if (nextMove != null && nextMove.WasPressedThisFrame() && (guardian.Stage == GuardianStage.Phase1 || guardian.Stage == GuardianStage.Phase2))
            {
                var moves = guardian.Boss.Moves;
                for (int i = 0; i < moves.Count; i++)
                {
                    selected = (selected + 1) % moves.Count;
                    if (moves[selected].Data.weight <= 0f || moves[selected].Data.phase != guardian.Boss.Phase.movePool || !moves[selected].CanChoose) continue;
                    guardian.Boss.ForceNextMove(moves[selected]); break;
                }
            }
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 270f, 20f, 250f, 550f), GUI.skin.box);
            GUILayout.Label("大樹守衛：" + guardian.Stage);
            GUILayout.Label("戰鬥時間：" + guardian.Elapsed.ToString("F1"));
            GUILayout.Label("F4 依序指定目前階段招式");
            if (GUILayout.Button("F1 直接開戰")) guardian.StartBattle();
            if (GUILayout.Button("F3 萬根（先站 " + guardian.Data.guidanceRadius.ToString("0.#") + "m 內）")) guardian.RequestRootBarrage();
            if (GUILayout.Button("F5 直入二階段")) phaseTwo.DebugEnterPhase2();
            if (outcome.IsVictoryPending && GUILayout.Button("暫代勝利演出完成／重試保存")) outcome.CompleteVictoryPresentation(outcome.PresentationToken);
            GUI.enabled = guardian.Stage == GuardianStage.Phase1 || guardian.Stage == GuardianStage.Phase2;
            foreach (var move in guardian.Boss.Moves)
                if (move.Data.weight > 0f && move.Data.phase == guardian.Boss.Phase.movePool && move.CanChoose && GUILayout.Button("下一招：" + move.DisplayName)) guardian.Boss.ForceNextMove(move);
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
#endif
