using BossFight.Core;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss.Moves
{
    /// <summary>
    /// 貓掌連拍（第一階段，近距離）／貓掌三連拍（第二階段）：預兆舉起右爪並轉向玩家，地上 Boss 前方的長方形範圍慢慢填滿；
    /// 填滿那一刻右爪拍下（第一下），接著另一隻爪舉起、範圍重新填色，strikeInterval 秒後拍下一下，共 strikeCount 下。方向在第一下鎖定。
    /// 每一下都會讓範圍閃亮並在前方出現爪痕，同時沿攻擊方向往前踏（PawSwipeData.lungeDistance，踏到玩家身前就停）。數值讀 PawSwipeData；可擋、可盾反（白光）。
    /// enhancedFinisher：最後一下前改送一次預兆（finisherCanBlock 決定白光或紅光，BossMove.Telegraph），地上換成放大 finisherRangeScale 倍的範圍，
    /// finisherInterval 秒後以 finisher 的傷害、可擋、可盾反拍下，帶電時在落點劈一道閃電。
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
        [Tooltip("強化最後一下的爪痕顏色")]
        [SerializeField] [ColorUsage(true, true)] Color finisherSlashColor = new Color(4f, 0.6f, 0.4f, 1f);
        [Tooltip("帶電的最後一下劈在落點的閃電柱（可留空）")]
        [SerializeField] TimedFx boltPrefab;
        [SerializeField] [ColorUsage(true, true)] Color boltColor = new Color(4f, 0.5f, 0.35f, 1f);
        [Tooltip("閃電柱大小（倍率）")]
        [Min(0f)] [SerializeField] float boltSize = 0.3f;
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
        // 地上範圍目前的倍率（強化的最後一下會放大）；0 = 已經收掉
        float areaScale;
        // 這一下的踏步：方向、總距離（0 = 沒在踏）、經過時間、已走距離
        Vector3 lungeDir;
        float lungeTotal, lungeTime, lungeTravelled;

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

        /// <summary>第一下的蓄力（預兆）：難度修正用 pawSwipeFirstStrikeBonus 取代通用的 telegraphBonus；第二下之後的間隔不變。</summary>
        public override float TelegraphTime => Data.telegraphTime + Boss.Difficulty.pawSwipeFirstStrikeBonus;

        public override void BeginTelegraph()
        {
            timer = 0f;
            lungeTotal = 0f;
            areaScale = 1f;
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
            LungeStep(dt);
            if (hitOpen && timer >= paw.hitWindow)
            {
                hitOpen = false;
                hitbox.Deactivate();
            }

            if (strikeIndex < paw.strikeCount - 1)
            {
                // 下一下之前範圍重新填色，填滿就拍
                float interval = IntervalBefore(strikeIndex + 1);
                area.SetFill(timer / interval);
                if (timer >= interval)
                {
                    timer -= interval;
                    strikeIndex++;
                    Strike();
                }
                return false;
            }
            return !hitOpen;
        }

        // 拍第 index 下之前要等幾秒（從上一下算）
        float IntervalBefore(int index) => paw.IsFinisher(index) ? paw.finisherInterval : paw.strikeInterval;

        void Strike()
        {
            bool right = strikeIndex % 2 == 0;
            bool last = strikeIndex >= paw.strikeCount - 1;
            bool finisher = paw.IsFinisher(strikeIndex);
            float scale = finisher ? paw.finisherRangeScale : 1f;
            float reach = paw.hitReach * scale, width = paw.hitWidth * scale;
            poser.SetPaw(right, PawPose.Strike);
            poser.SetPaw(!right, last ? PawPose.Rest : PawPose.Raised);

            hitbox.SetBox(new Vector3(0f, paw.hitHeight * 0.5f, reach * 0.5f), new Vector3(width, paw.hitHeight, reach));
            hitbox.Activate(finisher ? new DamageInfo(paw.finisherDamage, paw.finisherCanBlock, paw.finisherCanParry, 0f) : CreateDamage());
            hitOpen = true;

            Vector3 forward = Flat(Boss.transform.forward).normalized;
            Vector3 origin = OnGround(Boss.transform.position);
            area.Strike();
            if (last)
            {
                area.Hide();
                areaScale = 0f;
            }
            else if (paw.IsFinisher(strikeIndex + 1))
            {
                // 下一下是強化的最後一下：換顏色重新預兆，地上換成放大的範圍
                areaScale = paw.finisherRangeScale;
                area.ShowRect(origin, forward, paw.hitReach * areaScale, paw.hitWidth * areaScale, paw.finisherCanBlock);
                Telegraph(paw.finisherCanBlock, paw.finisherInterval);
            }
            else area.SetFill(0f);

            // 拍下的同時往攻擊方向踏一步（帶電的最後一下預設站定）
            lungeDir = forward;
            lungeTotal = paw.LungeDistance(strikeIndex);
            lungeTime = lungeTravelled = 0f;

            Vector3 slashPos = origin + forward * (reach * slashForward);
            var rot = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, right ? -slashTilt : slashTilt);
            TimedFx.Spawn(slashPrefab, slashPos + Vector3.up * slashHeight, rot, finisher ? finisherSlashColor : slashColor, scale);
            if (finisher && paw.finisherElectric)
                TimedFx.Spawn(boltPrefab, slashPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), boltColor, boltSize);
            Impact(finisher ? BossImpactKind.PawFinisher : BossImpactKind.PawSwipe, slashPos);
        }

        // 依踏步曲線往前滑這一幀的量，踏到玩家身前就停；不出場地。地上下一下的範圍跟著 Boss 走
        void LungeStep(float dt)
        {
            if (lungeTotal <= 0f) return;
            lungeTime += dt;
            float t = Mathf.Clamp01(lungeTime / paw.lungeDuration);
            float travelled = lungeTotal * (1f - (1f - t) * (1f - t));
            float step = Mathf.Min(travelled - lungeTravelled, RoomToTarget());
            lungeTravelled = travelled;
            if (t >= 1f) lungeTotal = 0f;
            if (step <= 0f) return;

            Boss.transform.position = Boss.ClampToArena(Boss.transform.position + lungeDir * step);
            if (areaScale > 0f)
                area.MoveRect(OnGround(Boss.transform.position), lungeDir, paw.hitReach * areaScale, paw.hitWidth * areaScale);
        }

        // 沿踏步方向還能走多遠才碰到玩家（兩邊的身體半徑加上 lungeStopGap）；玩家不在前進路線上時不限制
        float RoomToTarget()
        {
            var target = Boss.Target;
            if (target == null) return float.PositiveInfinity;
            Vector3 to = Flat(target.position - Boss.transform.position);
            float along = Vector3.Dot(to, lungeDir);
            if (along <= 0f) return float.PositiveInfinity;
            float r = Boss.BodyRadius + Boss.TargetBodyRadius + paw.lungeStopGap;
            float lateralSqr = Mathf.Max(0f, to.sqrMagnitude - along * along);
            if (lateralSqr >= r * r) return float.PositiveInfinity;
            return Mathf.Max(0f, along - Mathf.Sqrt(r * r - lateralSqr));
        }

        public override void BeginRecovery()
        {
            hitOpen = false;
            hitbox.Deactivate();
            poser.SetPaw(true, PawPose.Rest);
            poser.SetPaw(false, PawPose.Rest);
        }

        // 最後一下的判定比踏步短，踏完剩下的部分
        public override void TickRecovery(float dt) => LungeStep(dt);

        public override void End()
        {
            lungeTotal = 0f;
            hitOpen = false;
            hitbox.Deactivate();
            area.HideImmediate();
            poser.ResetPose();
        }
    }
}
