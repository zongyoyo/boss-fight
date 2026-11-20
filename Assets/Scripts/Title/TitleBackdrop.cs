using BossFight.Player;
using UnityEngine;

namespace BossFight.Title
{
    /// <summary>
    /// 標題畫面的背景（T32）：刀盾狗坐在營火前的圓木上。狗是 TitleBuilder 從角色 prefab 拆出來、只剩外觀與 DaoDunDogAnimator 的複本，
    /// 這裡只在 Start 讓它直接坐好（程序化姿勢照常呼吸、搖尾巴）。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleBackdrop : MonoBehaviour
    {
        [Tooltip("坐在圓木上的刀盾狗（只有外觀的複本）")]
        [SerializeField] DaoDunDogAnimator dog;

        void Start()
        {
            if (dog != null) dog.SetSitting(true, true);
        }
    }
}
