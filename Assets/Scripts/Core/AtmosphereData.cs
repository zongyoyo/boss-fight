using System;
using UnityEngine;

namespace BossFight.Core
{
    /// <summary>
    /// 競技場天色的兩組外觀：開場前的「暴風雨前」與開場第 3 鏡之後一直維持的「暴風雨」。ArenaAtmosphere 依進度在兩組之間插值。
    /// 兩組的 Skybox 要用同一個 shader（Skybox/Procedural），才能逐格插值。全是視覺數值；資產在 Assets/ScriptableObjects/Arena/Atmosphere.asset。
    /// </summary>
    [CreateAssetMenu(fileName = "Atmosphere", menuName = "BossFight/Arena/Atmosphere Data")]
    public class AtmosphereData : ScriptableObject
    {
        [Serializable]
        public struct Look
        {
            [Tooltip("天空盒（Skybox/Procedural）")]
            public Material skybox;
            [Tooltip("主光源顏色")]
            public Color sunColor;
            [Tooltip("主光源強度")]
            [Min(0f)] public float sunIntensity;
            [Tooltip("環境光（Trilight）：天空、地平線、地面")]
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
        }

        [Tooltip("開場前：暴風雨前的黃昏")]
        public Look calm;
        [Tooltip("暴風雨：開場第 3 鏡之後與戰鬥中")]
        public Look storm;

        [Header("雷雲")]
        [Tooltip("還沒聚集時，雷雲離場地中心的距離是聚集後的幾倍")]
        [Min(1f)] public float cloudScatter = 3f;
        [Tooltip("還沒聚集時的大小比例")]
        [Range(0f, 1f)] public float cloudStartScale = 0.35f;
    }
}
