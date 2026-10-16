using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 貓掌連拍（第一階段，近距離）：預兆舉起右爪並轉向玩家，地上 Boss 前方的長方形範圍慢慢填滿；
    /// 填滿那一刻右爪拍下（第一下），接著左爪舉起、範圍重新填色，strikeInterval 秒後拍第二下。方向在第一下鎖定。
    /// 每一下都會讓範圍閃亮並在前方出現爪痕。數值讀 PawSwipeData；可擋、可盾反（白光）。
    /// </summary>
    [DisallowMultipleComponent]
    public class PawSwipeMove : BossMove
    {
        [Tooltip("攻擊判定，attacker 指向 Boss 根物件")]
        [SerializeField] Hitbox hitbox;
        [SerializeField] OiiaCatPoser poser;
        [Tooltip("地面範圍提示（執行時生成一份）")]
        [SerializeField] GroundTelegraph telegraphPrefab;
        [Tooltip("每一下的爪痕")]
        [SerializeField] TimedFx slashPrefab;
        [SerializeField] [ColorUsage(true, true)] Color slashColor = new Color(3f, 3f, 3f, 1f);
        [Tooltip("爪痕中心離地高度 (m)；比刀盾狗高，從玩家背後的鏡頭才看得到")]
        [SerializeField] float slashHeight = 1.7f;
        [Tooltip("爪痕在 Boss 前方多遠，以判定深度的比例計")]
        [Range(0f, 1f)] [SerializeField] float slashForward = 0.45f;
        [Tooltip("爪痕左右傾斜（度），左右爪相反")]
        [SerializeField] float slashTilt = 30f;

        PawSwipeData paw;
        GroundTelegraph area;
        float timer;
        int strikeIndex;
        bool hitOpen;

        void Awake()
        {
            paw = Data as PawSwipeData;
            if (paw == null) Debug.LogError($"[Boss] {name} 的資料不是 PawSwipeData。", this);
            area = Instantiate(telegraphPrefab);
            area.name = $"{name}_Telegraph";
        }

        void OnDestroy()
        {
            if (area != null) Destroy(area.gameObject);
        }

        void OnEnable() => hitbox.OnHit += ReportHit;

        void OnDisable() => hitbox.OnHit -= ReportHit;

        public override void BeginTelegraph()
        {
            timer = 0f;
            poser.SetPaw(true, PawPose.Raised);
            area.ShowRect(OnGround(Boss.transform.position), Boss.transform.forward, paw.hitReach, paw.hitWidth, Data.canBlock);
        }

        public override void TickTelegraph(float dt)
        {
            base.TickTelegraph(dt);
            timer += dt;
            area.MoveRect(OnGround(Boss.transform.position), Boss.transform.forward, paw.hitReach, paw.hitWidth);
            area.SetFill(TelegraphTime > 0f ? timer / TelegraphTime : 1f);
        }

        public override void BeginActive()
        {
            strikeIndex = 0;
            timer = 0f;
            Strike();
        }

        public override bool TickActive(float dt)
        {
            timer += dt;
            if (hitOpen && timer >= paw.hitWindow)
            {
                hitOpen = false;
                hitbox.Deactivate();
            }

            if (strikeIndex < paw.strikeCount - 1)
            {
                // 下一下之前範圍重新填色，填滿就拍
                area.SetFill(timer / paw.strikeInterval);
                if (timer >= paw.strikeInterval)
                {
                    timer -= paw.strikeInterval;
                    strikeIndex++;
                    Strike();
                }
                return false;
            }
            return !hitOpen;
        }

        void Strike()
        {
            bool right = strikeIndex % 2 == 0;
            bool last = strikeIndex >= paw.strikeCount - 1;
            poser.SetPaw(right, PawPose.Strike);
            poser.SetPaw(!right, last ? PawPose.Rest : PawPose.Raised);

            hitbox.SetBox(new Vector3(0f, paw.hitHeight * 0.5f, paw.hitReach * 0.5f),
                          new Vector3(paw.hitWidth, paw.hitHeight, paw.hitReach));
            hitbox.Activate(CreateDamage());
            hitOpen = true;

            area.Strike();
            if (last) area.Hide();
            else area.SetFill(0f);

            Vector3 forward = Flat(Boss.transform.forward).normalized;
            Vector3 pos = OnGround(Boss.transform.position) + forward * (paw.hitReach * slashForward) + Vector3.up * slashHeight;
            var rot = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, right ? -slashTilt : slashTilt);
            TimedFx.Spawn(slashPrefab, pos, rot, slashColor);
        }

        public override void BeginRecovery()
        {
            hitOpen = false;
            hitbox.Deactivate();
            poser.SetPaw(true, PawPose.Rest);
            poser.SetPaw(false, PawPose.Rest);
        }

        public override void End()
        {
            hitOpen = false;
            hitbox.Deactivate();
            area.HideImmediate();
            poser.ResetPose();
        }
    }
}
