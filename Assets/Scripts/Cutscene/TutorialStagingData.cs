using System;
using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>教學對白的鏡頭（H2）；新的加在最後，保留資產的編號。</summary>
    public enum TutorialShot { None, TwoShot, Mentor, Summon, Whip, Demo, SaplingBars, Slap, Sapling, Reveal }

    /// <summary>鏡頭位置的參考點：角色本身，或兩個角色的中點。</summary>
    public enum ShotAnchor { Player, Mentor, Sapling, PlayerMentor, MentorSapling, PlayerSapling }

    /// <summary>
    /// 教學對白的鏡頭擺法（H2，TutorialStage 讀）：每種鏡頭的位置與看的點都寫在一個「框架」裡——原點是 anchor，
    /// 前方 = 從 frameFrom 指向 frameTo（水平），右手邊 = x、上 = y。所以角色站在哪裡都是同樣的構圖。
    /// 資產在 Assets/ScriptableObjects/Tutorial/TutorialStaging.asset（TutorialBuilder 沒有時建立）。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Tutorial/Staging", fileName = "TutorialStaging")]
    public class TutorialStagingData : ScriptableObject
    {
        [Serializable]
        public class ShotSetting
        {
            public TutorialShot shot;
            [Tooltip("框架的原點")] public ShotAnchor anchor = ShotAnchor.Player;
            [Tooltip("框架的前方：從這裡")] public ShotAnchor frameFrom = ShotAnchor.Player;
            [Tooltip("框架的前方：指向這裡")] public ShotAnchor frameTo = ShotAnchor.Mentor;
            [Tooltip("鏡頭位置（框架座標，m）")] public Vector3 offset = new Vector3(0.9f, 1.4f, -2.2f);
            [Tooltip("鏡頭看的參考點")] public ShotAnchor lookAt = ShotAnchor.Mentor;
            [Tooltip("看的點相對參考點的位移（框架座標，m）")] public Vector3 lookOffset = new Vector3(0f, 0.4f, 0f);
            [Tooltip("視角（度）")] [Range(10f, 90f)] public float fieldOfView = 45f;
        }

        [Tooltip("鏡頭的優先度：要比自由視角與鎖定鏡頭高")]
        public int priority = 30;
        [Tooltip("各種鏡頭的擺法；找不到的鏡頭用第一個")]
        public ShotSetting[] shots =
        {
            new ShotSetting { shot = TutorialShot.TwoShot },
            new ShotSetting { shot = TutorialShot.Mentor, anchor = ShotAnchor.Mentor, offset = new Vector3(0.35f, 1f, -2f), lookOffset = new Vector3(0f, 0.85f, 0f), fieldOfView = 40f },
            new ShotSetting { shot = TutorialShot.Summon, anchor = ShotAnchor.Player, offset = new Vector3(-1.4f, 1.9f, -2.6f), lookAt = ShotAnchor.MentorSapling, lookOffset = new Vector3(0f, 0.4f, 0f), fieldOfView = 50f },
            new ShotSetting { shot = TutorialShot.Whip, anchor = ShotAnchor.PlayerMentor, offset = new Vector3(3.2f, 1.3f, -0.6f), lookAt = ShotAnchor.PlayerMentor, lookOffset = new Vector3(0f, 0.6f, 0f), fieldOfView = 50f },
            new ShotSetting { shot = TutorialShot.Demo, anchor = ShotAnchor.Player, frameTo = ShotAnchor.Sapling, offset = new Vector3(1.6f, 3.6f, -2.2f), lookAt = ShotAnchor.Sapling, lookOffset = new Vector3(0f, 0f, 1.4f), fieldOfView = 55f },
            new ShotSetting { shot = TutorialShot.SaplingBars, anchor = ShotAnchor.Sapling, frameTo = ShotAnchor.Sapling, offset = new Vector3(0.4f, 1.3f, -2.4f), lookAt = ShotAnchor.Sapling, lookOffset = new Vector3(0f, 1.6f, 0f), fieldOfView = 45f },
            new ShotSetting { shot = TutorialShot.Slap, anchor = ShotAnchor.MentorSapling, frameTo = ShotAnchor.MentorSapling, offset = new Vector3(1.8f, 1.1f, -2.4f), lookAt = ShotAnchor.MentorSapling, lookOffset = new Vector3(0f, 0.5f, 0f), fieldOfView = 45f },
            new ShotSetting { shot = TutorialShot.Sapling, anchor = ShotAnchor.Sapling, frameTo = ShotAnchor.Sapling, offset = new Vector3(0.9f, 1.4f, -2.4f), lookAt = ShotAnchor.Sapling, lookOffset = new Vector3(0f, 0.8f, 0f), fieldOfView = 45f },
            new ShotSetting { shot = TutorialShot.Reveal, anchor = ShotAnchor.Mentor, offset = new Vector3(0.15f, 0.95f, -1.5f), lookOffset = new Vector3(0f, 0.92f, 0f), fieldOfView = 35f },
        };

        public ShotSetting Get(TutorialShot shot)
        {
            if (shots == null || shots.Length == 0) return null;
            foreach (var s in shots)
                if (s != null && s.shot == shot) return s;
            return shots[0];
        }
    }
}
