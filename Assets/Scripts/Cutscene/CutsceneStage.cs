using BossFight.Player;
using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 演出鏡頭的擺位（轉階段）：玩家可能在場上任何地方觸發，鏡頭要跟著「狗 → 貓」的方向擺。CutsceneDirector 開始播之前呼叫 Align：
    /// - 自己放在場地中心（貓瞬移回去的位置），+Z 指向「玩家 → 場地中心」；子物件是以貓為準的鏡頭與目標（貓臉特寫、俯瞰、中景）。
    /// - 玩家轉向場地中心（再次對視）。
    /// - openingShot（第 1 步的全景鏡頭）擺在玩家與 Boss 連線的側面、場地內。
    /// 之後每幀把 playerAnchor 放在玩家腳下、朝向和自己一樣（狗臉特寫掛在它底下），midpoint 放在玩家與 Boss 中間（全景鏡頭看這裡）。
    /// 這裡的數字全是視覺參數。
    /// </summary>
    [DefaultExecutionOrder(-100)] // 在 Cinemachine 算鏡頭之前擺好
    [DisallowMultipleComponent]
    public class CutsceneStage : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [Tooltip("Boss（midpoint 與第 1 步的鏡頭用）")]
        [SerializeField] Transform boss;
        [Tooltip("場地中心")]
        [SerializeField] Transform arenaCenter;
        [Tooltip("跟著玩家的節點（+Z 朝向貓）；狗臉特寫的鏡頭與目標掛在底下")]
        [SerializeField] Transform playerAnchor;
        [Tooltip("玩家與 Boss 中間的點（全景鏡頭的 LookAt）")]
        [SerializeField] Transform midpoint;
        [Tooltip("第 1 步的全景鏡頭：Align 時擺在玩家與 Boss 連線的側面")]
        [SerializeField] Transform openingShot;
        [Tooltip("全景鏡頭離連線中點的水平距離 (m)")]
        [Min(0f)] [SerializeField] float openingShotDistance = 7f;
        [Tooltip("全景鏡頭的高度 (m)")]
        [SerializeField] float openingShotHeight = 4.5f;
        [Tooltip("全景鏡頭最多離場地中心多遠 (m)；要在外牆內")]
        [Min(0f)] [SerializeField] float openingShotMaxRadius = 10.5f;
        [Tooltip("midpoint 的高度 (m)")]
        [SerializeField] float midpointHeight = 1.1f;

        /// <summary>依目前玩家位置擺好舞台，並讓玩家面向場地中心。</summary>
        public void Align()
        {
            Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
            Vector3 p = player.transform.position;
            Vector3 fwd = Flat(center - p);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Flat(player.transform.forward);
            var rot = Quaternion.LookRotation(fwd.normalized);
            transform.SetPositionAndRotation(center, rot);
            player.Teleport(p, rot);

            if (openingShot != null && boss != null)
            {
                Vector3 mid = (Flat(p) + Flat(boss.position)) * 0.5f;
                Vector3 line = Flat(boss.position - p);
                if (line.sqrMagnitude < 1e-4f) line = fwd;
                Vector3 side = Vector3.Cross(Vector3.up, line.normalized);
                // 哪一側離場地中心近就擺哪一側，比較不會被外牆擋住
                Vector3 a = mid + side * openingShotDistance, b = mid - side * openingShotDistance;
                Vector3 pos = Flat(a - center).sqrMagnitude <= Flat(b - center).sqrMagnitude ? a : b;
                Vector3 offset = Flat(pos - center);
                if (offset.magnitude > openingShotMaxRadius) offset = offset.normalized * openingShotMaxRadius;
                openingShot.position = new Vector3(center.x + offset.x, center.y + openingShotHeight, center.z + offset.z);
            }
            LateUpdate();
        }

        void LateUpdate()
        {
            if (player == null) return;
            Vector3 p = player.transform.position;
            float ground = arenaCenter != null ? arenaCenter.position.y : 0f;
            if (playerAnchor != null) playerAnchor.SetPositionAndRotation(new Vector3(p.x, ground, p.z), transform.rotation);
            if (midpoint != null && boss != null)
                midpoint.position = new Vector3((p.x + boss.position.x) * 0.5f, ground + midpointHeight, (p.z + boss.position.z) * 0.5f);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
