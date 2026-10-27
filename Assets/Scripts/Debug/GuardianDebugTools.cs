#if UNITY_EDITOR || DEVELOPMENT_BUILD || BOSSFIGHT_DEV_TOOLS
using BossFight.Battle;
using UnityEngine;
using UnityEngine.InputSystem;
namespace BossFight.DebugTools
{
    public class GuardianDebugTools : MonoBehaviour
    {
        [SerializeField] GuardianPhaseController guardian;
        InputAction start, barrage, nextMove;
        int selected = -1;
        void Awake()
        {
            start = InputSystem.actions?.FindAction("Player/DebugGuardianBattle", true);
            barrage = InputSystem.actions?.FindAction("Player/DebugGuardianBarrage", true);
            nextMove = InputSystem.actions?.FindAction("Player/DebugGuardianNextMove", true);
        }
        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (start != null && start.WasPressedThisFrame()) guardian.StartBattle();
            if (barrage != null && barrage.WasPressedThisFrame()) guardian.RequestRootBarrage();
            if (nextMove != null && nextMove.WasPressedThisFrame() && guardian.Stage == GuardianStage.Phase1)
            {
                var moves = guardian.Boss.Moves;
                for (int i = 0; i < moves.Count; i++)
                {
                    selected = (selected + 1) % moves.Count;
                    if (moves[selected].Data.weight <= 0f) continue;
                    guardian.Boss.ForceNextMove(moves[selected]); break;
                }
            }
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 250f, 20f, 230f, 330f), GUI.skin.box);
            GUILayout.Label("大樹守衛：" + guardian.Stage);
            GUILayout.Label("戰鬥時間：" + guardian.Elapsed.ToString("F1"));
            GUILayout.Label("F4 依序指定六招，免操作游標");
            if (GUILayout.Button("F1 直接開戰")) guardian.StartBattle();
            if (GUILayout.Button("F3 萬根（先站 " + guardian.Data.guidanceRadius.ToString("0.#") + "m 內）")) guardian.RequestRootBarrage();
            GUI.enabled = guardian.Stage == GuardianStage.Phase1;
            foreach (var move in guardian.Boss.Moves)
                if (move.Data.weight > 0f && GUILayout.Button("下一招：" + move.DisplayName)) guardian.Boss.ForceNextMove(move);
            GUI.enabled = true;
            GUILayout.EndArea();
        }
    }
}
#endif
