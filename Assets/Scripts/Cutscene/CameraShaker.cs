using Unity.Cinemachine;
using UnityEngine;

namespace BossFight.Cutscene
{
    /// <summary>
    /// 鏡頭震動：CurveTrack 的值就是同一台 CinemachineCamera 上 Perlin 雜訊的 AmplitudeGain（0 = 不震）。
    /// 震動的樣式（雜訊設定檔、頻率）在 CinemachineBasicMultiChannelPerlin 上調。
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraShaker : CurveTarget
    {
        [SerializeField] CinemachineBasicMultiChannelPerlin noise;

        void Awake() => Apply(0f);

        public override void Apply(float value)
        {
            noise.AmplitudeGain = Mathf.Max(0f, value);
        }
    }
}
