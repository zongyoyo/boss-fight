using System;
using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>
    /// 休息區的版面（T20）：邊界折線、樹與隱形牆、四個站點、小路、出生點。HubBuilder（Tools > Hub > Build Hub）照這份重建場景；
    /// 執行時 Interactable 讀互動半徑，HubManager 讀走到站位點的速度。
    /// 座標以休息區中心為原點，(x, z) = (東, 北)，公尺；朝向是度，0 = 北（+Z）、順時針。
    /// 輪廓照企劃書的暫定尺寸（南北約 50 m、東西約 36 m，北邊主區、往南收窄成寬 6 m 的小路通往傳送門）；照草圖調整時改 boundary 的點。
    /// 資產在 Assets/ScriptableObjects/Hub/HubLayout.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "HubLayout", menuName = "BossFight/Hub/Hub Layout Data")]
    public class HubLayoutData : ScriptableObject
    {
        [Serializable]
        public class Station
        {
            public HubStationKind kind;
            [Tooltip("互動點（村長、鐵砧、營火、傳送門）的位置 (x, z)")]
            public Vector2 position;
            [Tooltip("站點正面的朝向（度）：玩家站在正面、面向站點")]
            public float facing;
            [Tooltip("玩家離互動點多近（水平距離）時可以互動 (m)")]
            [Min(0f)] public float interactRadius = 3f;
            [Tooltip("打開介面時玩家走到的站位點：沿正面離互動點多遠 (m)")]
            [Min(0f)] public float standDistance = 2f;
            [Tooltip("站點鏡頭的位置：以站位點為原點，(右, 上, 前)，「前」= 從站位點看向互動點")]
            public Vector3 cameraOffset;
            [Tooltip("站點鏡頭看向的點：以站位點為原點，(右, 上, 前)；站點面板在畫面右邊，所以主體擺在畫面左半")]
            public Vector3 cameraLookOffset;
            [Tooltip("站點鏡頭的垂直視角（度）")]
            [Range(10f, 90f)] public float cameraFov = 40f;
        }

        [Header("邊界")]
        [Tooltip("休息區可走範圍的輪廓 (x, z)，依序連成封閉折線；第一排樹種在線上，往外再種幾排，隱形牆在第一排後面")]
        public Vector2[] boundary =
        {
            new Vector2(-18f, 24f), new Vector2(18f, 24f), new Vector2(18f, -2f), new Vector2(12f, -10f),
            new Vector2(1f, -14f), new Vector2(1f, -27f), new Vector2(-5f, -27f), new Vector2(-5f, -14f),
            new Vector2(-16f, -10f), new Vector2(-18f, -2f),
        };
        [Tooltip("地面比邊界往外多鋪多少 (m)")]
        [Min(0f)] public float groundMargin = 14f;

        [Header("樹")]
        [Tooltip("沿邊界種幾排")]
        [Range(1, 3)] public int treeRows = 3;
        [Tooltip("同一排樹的間距 (m)")]
        [Min(0.5f)] public float treeSpacing = 2.6f;
        [Tooltip("排與排的間距（往外）(m)")]
        [Min(0.5f)] public float rowSpacing = 2.4f;
        [Tooltip("每棵樹位置的隨機偏移上限 (m)")]
        [Min(0f)] public float treeJitter = 0.5f;
        [Tooltip("樹高的範圍 (m)")]
        public Vector2 treeHeight = new Vector2(4.5f, 7.5f);
        [Tooltip("樹幹碰撞的半徑 (m)")]
        [Min(0.05f)] public float trunkRadius = 0.3f;
        [Tooltip("隨機種子：一樣的種子重建出一樣的樹")]
        public int treeSeed = 20;

        [Header("隱形牆")]
        [Tooltip("牆在邊界外多遠（第一排樹的後面）(m)")]
        [Min(0f)] public float wallOffset = 1.2f;
        [Min(0.1f)] public float wallHeight = 5f;
        [Min(0.1f)] public float wallThickness = 1f;

        [Header("站點")]
        [Tooltip("中心小屋的位置 (x, z)；村長站在小屋前（stations 的 Chief）")]
        public Vector2 hutPosition = new Vector2(0f, 14f);
        [Tooltip("小屋門口的朝向（度）")]
        public float hutFacing = 180f;
        public Station[] stations =
        {
            new Station
            {
                kind = HubStationKind.Chief, position = new Vector2(0f, 10f), facing = 180f, interactRadius = 3f, standDistance = 2.2f,
                cameraOffset = new Vector3(4.5f, 1.6f, 1.1f), cameraLookOffset = new Vector3(0f, 1.1f, 1.9f), cameraFov = 40f,
            },
            new Station
            {
                kind = HubStationKind.Blacksmith, position = new Vector2(12f, 6f), facing = 270f, interactRadius = 2.8f, standDistance = 1.6f,
                cameraOffset = new Vector3(1f, 2f, -3.4f), cameraLookOffset = new Vector3(0.9f, 0.8f, 2f), cameraFov = 45f,
            },
            new Station
            {
                kind = HubStationKind.Campfire, position = new Vector2(-10f, -4f), facing = 90f, interactRadius = 3f, standDistance = 1.9f,
                cameraOffset = new Vector3(-2f, 1.7f, 5.2f), cameraLookOffset = new Vector3(-1f, 0.7f, 0.8f), cameraFov = 40f,
            },
            new Station
            {
                kind = HubStationKind.Portal, position = new Vector2(-2f, -24f), facing = 0f, interactRadius = 3.5f, standDistance = 3f,
                cameraOffset = new Vector3(1f, 2.2f, -4.2f), cameraLookOffset = new Vector3(1f, 2f, 3f), cameraFov = 45f,
            },
        };
        [Tooltip("打開介面時玩家走到站位點的速度 (m/s)")]
        [Min(0.1f)] public float approachSpeed = 3f;

        [Header("小路")]
        [Tooltip("小路從這裡（休息區中心的空地）連到各站點的站位點 (x, z)")]
        public Vector2 plazaCenter = Vector2.zero;
        [Tooltip("中心空地的半徑 (m)")]
        [Min(0f)] public float plazaRadius = 2.5f;
        [Tooltip("小路寬 (m)")]
        [Min(0.1f)] public float pathWidth = 2f;

        [Header("出生點")]
        [Tooltip("啟動遊戲時的出生點（營火旁）(x, z)")]
        public Vector2 startSpawn = new Vector2(-7f, -6.5f);
        [Tooltip("啟動時的朝向（度）")]
        public float startFacing = 45f;
        [Tooltip("從 Boss 戰回來的出生點：傳送門往正面多遠 (m)，面向正面（背對傳送門）")]
        [Min(0f)] public float portalSpawnDistance = 5f;

        /// <summary>這個種類的站點；沒有時 null。</summary>
        public Station Get(HubStationKind kind)
        {
            if (stations == null) return null;
            foreach (var s in stations)
            {
                if (s != null && s.kind == kind) return s;
            }
            return null;
        }

        /// <summary>(x, z) 轉成地面上的位置。</summary>
        public static Vector3 Ground(Vector2 p) => new Vector3(p.x, 0f, p.y);

        /// <summary>朝向（度）的水平方向。</summary>
        public static Vector3 Direction(float facing)
        {
            float rad = facing * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
        }
    }
}
