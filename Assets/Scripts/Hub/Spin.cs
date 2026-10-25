using UnityEngine;

namespace BossFight.Hub
{
    /// <summary>讓物件繞自己的軸一直轉（傳送門的紫色光面）。純視覺。</summary>
    [DisallowMultipleComponent]
    public class Spin : MonoBehaviour
    {
        [Tooltip("每秒轉幾度（本地座標的各軸）")]
        [SerializeField] Vector3 degreesPerSecond = new Vector3(0f, 0f, 40f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
