using UnityEngine;

namespace BossFight.Title
{
    /// <summary>
    /// 標題畫面的背景構圖（T32）：TitleBuilder 讀，照休息區營火的站點鏡頭擺標題鏡頭，再照這裡的值移動，讓刀盾狗與火堆落在畫面右側。
    /// 改完重跑 Tools > Title > Build Title。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Title Layout", fileName = "TitleLayout")]
    public class TitleLayoutData : ScriptableObject
    {
        [Header("鏡頭（以營火站點鏡頭為準）")]
        [Tooltip("鏡頭位置的位移，鏡頭自己的座標（x = 畫面右、y = 上、z = 前）；x 負 = 鏡頭往左移，狗和火往畫面右邊跑")]
        public Vector3 cameraLocalOffset = new Vector3(-1.4f, 0.15f, -0.3f);
        [Tooltip("鏡頭在位移後再往左右轉幾度（正 = 往右轉，畫面裡的東西往左跑）")]
        [Range(-45f, 45f)] public float cameraYawOffset = -15f;
        [Tooltip("鏡頭上下再轉幾度（正 = 往下看）")]
        [Range(-30f, 30f)] public float cameraPitchOffset = 0f;
        [Tooltip("視野角度；0 = 沿用站點鏡頭")]
        [Range(0f, 90f)] public float fieldOfView = 0f;

        [Header("佈景")]
        [Tooltip("複製休息區佈景時，包圍盒跟標題鏡頭視野相交才留下（往外多留幾公尺，避免陰影與邊緣穿幫）")]
        [Min(0f)] public float cullMargin = 4f;
        [Tooltip("視野的最遠距離：更遠的佈景不複製（霧裡看不到）")]
        [Min(10f)] public float cullDistance = 70f;
    }
}
