using System;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>一種事件的回饋：音效（多個時隨機挑一個）、音量與鏡頭震動強度。</summary>
    [Serializable]
    public struct FeedbackCue
    {
        [Tooltip("音效；放多個時每次隨機挑一個，連續觸發才不會一模一樣。留空 = 沒有聲音")]
        public AudioClip[] clips;
        [Tooltip("音量")]
        [Range(0f, 1f)] public float volume;
        [Tooltip("鏡頭震動強度（0 = 不震）；實際幅度再乘上 shakeScale")]
        [Min(0f)] public float shake;

        public FeedbackCue(float volume, float shake)
        {
            clips = null;
            this.volume = volume;
            this.shake = shake;
        }
    }

    /// <summary>
    /// 戰鬥的打擊回饋（T11）：每個事件一組音效與鏡頭震動，依重要程度分級（揮空、輕攻擊 &lt; 第三段、格擋 &lt; 重擊、被打中 &lt; 處決、Boss 撞牆、大迴旋落地）。
    /// 畫面停頓不在這裡：企劃書只有盾反停頓（PlayerStats.parryHitStop），其他事件不加。
    /// 玩家身上的 PlayerFeedback、Boss 身上的 BossImpactFeedback 讀，經同一物件上的 FeedbackEmitter 播放。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Combat Feedback", fileName = "CombatFeedback")]
    public class CombatFeedbackData : ScriptableObject
    {
        [Header("鏡頭震動")]
        [Tooltip("所有震動的總倍率；0 = 關掉鏡頭震動（不喜歡晃動的玩家用）")]
        [Min(0f)] public float shakeScale = 1f;
        [Tooltip("一次震動的長度（秒）")]
        [Min(0.01f)] public float shakeDuration = 0.25f;
        [Tooltip("震動傳得到的距離 (m)：鏡頭離事件越遠震得越小，超過就不震")]
        [Min(0.1f)] public float shakeDistance = 30f;
        [Tooltip("距離衰減的寬度（0 = 一離開事件點就開始變小，1 = 到很遠才變小）")]
        [Range(0f, 1f)] public float shakeSpread = 0.6f;

        [Header("玩家攻擊")]
        [Tooltip("輕攻擊揮刀（判定開始那一刻）")]
        public FeedbackCue swing = new FeedbackCue(0.45f, 0f);
        [Tooltip("重擊、處決揮刀")]
        public FeedbackCue heavySwing = new FeedbackCue(0.6f, 0f);
        [Tooltip("輕攻擊第 1、2 段命中")]
        public FeedbackCue hit = new FeedbackCue(0.7f, 0.12f);
        [Tooltip("輕攻擊第 3 段（最後一段）命中")]
        public FeedbackCue comboFinisherHit = new FeedbackCue(0.85f, 0.22f);
        [Tooltip("重擊命中")]
        public FeedbackCue heavyHit = new FeedbackCue(1f, 0.4f);
        [Tooltip("處決命中")]
        public FeedbackCue executionHit = new FeedbackCue(1f, 0.75f);
        [Tooltip("打到但沒有效果（Boss 無敵：轉階段、大迴旋浮空）")]
        public FeedbackCue hitDeflected = new FeedbackCue(0.5f, 0f);

        [Header("玩家防守")]
        [Tooltip("被直接打中")]
        public FeedbackCue hurt = new FeedbackCue(0.9f, 0.45f);
        [Tooltip("舉盾擋下")]
        public FeedbackCue block = new FeedbackCue(0.8f, 0.15f);
        [Tooltip("舉盾擋下但耐力歸零破防")]
        public FeedbackCue guardBreak = new FeedbackCue(1f, 0.45f);
        [Tooltip("盾反成功（畫面停頓照 PlayerStats.parryHitStop）")]
        public FeedbackCue parry = new FeedbackCue(1f, 0.3f);
        [Tooltip("無敵中被攻擊打到（翻滾閃過、受擊後無敵）")]
        public FeedbackCue evade = new FeedbackCue(0.5f, 0f);

        [Header("玩家動作")]
        public FeedbackCue roll = new FeedbackCue(0.4f, 0f);
        public FeedbackCue jump = new FeedbackCue(0.35f, 0f);
        [Tooltip("開始啃骨頭")]
        public FeedbackCue heal = new FeedbackCue(0.6f, 0f);
        [Tooltip("累積湊滿、多了一根骨頭")]
        public FeedbackCue boneEarned = new FeedbackCue(0.55f, 0f);

        [Header("Boss 預兆（白光可擋、紅光不可擋，用聽的也分得出來）")]
        public FeedbackCue telegraphBlockable = new FeedbackCue(0.6f, 0f);
        public FeedbackCue telegraphUnblockable = new FeedbackCue(0.75f, 0f);

        [Header("Boss 招式")]
        [Tooltip("貓掌每一下拍下去")]
        public FeedbackCue pawSwipe = new FeedbackCue(0.6f, 0.08f);
        [Tooltip("三連拍第三下（帶電）")]
        public FeedbackCue pawFinisher = new FeedbackCue(0.85f, 0.3f);
        [Tooltip("迴旋衝撞撞牆")]
        public FeedbackCue wallSlam = new FeedbackCue(1f, 0.65f);
        [Tooltip("扭屁股飛撲落地")]
        public FeedbackCue pounceLanding = new FeedbackCue(0.85f, 0.3f);
        [Tooltip("幻影飛撲落地")]
        public FeedbackCue phantomLanding = new FeedbackCue(0.55f, 0.12f);
        [Tooltip("衝擊波放出（每一圈）")]
        public FeedbackCue shockwave = new FeedbackCue(0.85f, 0.25f);
        [Tooltip("一道落雷劈下")]
        public FeedbackCue lightning = new FeedbackCue(0.55f, 0.2f);
        [Tooltip("龍捲風成形")]
        public FeedbackCue tornado = new FeedbackCue(0.75f, 0.15f);
        [Tooltip("大迴旋落地")]
        public FeedbackCue grandSpinLanding = new FeedbackCue(1f, 1f);

        [Header("大樹守衛招式（序章）")]
        [Tooltip("藤蔓橫掃、砸地每一段開判定")]
        public FeedbackCue vineStrike = new FeedbackCue(0.75f, 0.15f);
        [Tooltip("葉刃、落葉每一波出手")]
        public FeedbackCue leafBlade = new FeedbackCue(0.55f, 0f);
        [Tooltip("樹根破土（突刺、環波、萬根齊發每一段）")]
        public FeedbackCue rootBurst = new FeedbackCue(0.85f, 0.3f);
        [Tooltip("種子炸彈爆炸")]
        public FeedbackCue seedBlast = new FeedbackCue(0.8f, 0.25f);
        [Tooltip("呼喚孩子（守衛怒吼）")]
        public FeedbackCue summon = new FeedbackCue(0.8f, 0.2f);

        [Header("序章角色")]
        [Tooltip("小樹精被砍中（木頭聲）")]
        public FeedbackCue saplingHurt = new FeedbackCue(0.7f, 0f);
        [Tooltip("菊花說一句話（嘰嘰的咕噥，不配音）")]
        public FeedbackCue daisyChatter = new FeedbackCue(0.5f, 0f);

        [Header("Boss 狀態")]
        [Tooltip("失衡滿、頭暈（大迴旋落地的頭暈也算）")]
        public FeedbackCue stunned = new FeedbackCue(0.8f, 0f);
        [Tooltip("Boss 倒下")]
        public FeedbackCue defeated = new FeedbackCue(1f, 0.8f);
    }
}
