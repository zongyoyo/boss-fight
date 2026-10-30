using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BossFight.Core
{
    /// <summary>
    /// 用 GPU Instancing 畫一區散佈的小模型（草叢）：場景只存每一叢的位置、朝向與縮放，不建幾千個物件、也不另存合併網格。
    /// 由 Builder 填資料（序章是 PrologueSceneryBuilder，一區一個）；每台相機開始算圖時畫一次，編輯模式也看得到。
    /// 純視覺：沒有碰撞，不投影（材質要開 GPU Instancing）。
    /// </summary>
    [ExecuteAlways]
    public class InstancedScatter : MonoBehaviour
    {
        [Serializable]
        public struct Instance
        {
            [Tooltip("世界座標的位置（模型底部中心）")] public Vector3 position;
            [Tooltip("繞 Y 軸的朝向（度）")] public float yaw;
            [Tooltip("等比縮放")] public float scale;
        }

        [Serializable]
        public class Kind
        {
            [Tooltip("這種模型的網格（第 0 個子網格）")] public Mesh mesh;
            public Instance[] instances = Array.Empty<Instance>();
        }

        // RenderMeshInstanced 一次最多畫的數量
        const int BatchSize = 1023;

        [Tooltip("所有種類共用的材質（要開 GPU Instancing）")]
        [SerializeField] Material material;
        [Tooltip("要不要接收陰影")]
        [SerializeField] bool receiveShadows = true;
        [SerializeField] Kind[] kinds = Array.Empty<Kind>();

        readonly List<(Mesh mesh, Matrix4x4[] matrices)> batches = new List<(Mesh, Matrix4x4[])>();
        Bounds bounds;

        public void SetData(Material sharedMaterial, Kind[] newKinds)
        {
            material = sharedMaterial;
            kinds = newKinds ?? Array.Empty<Kind>();
            Rebuild();
        }

        void OnEnable()
        {
            Rebuild();
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        void OnDisable() => RenderPipelineManager.beginCameraRendering -= Draw;

        void OnValidate() => Rebuild();

        void Rebuild()
        {
            batches.Clear();
            bool any = false;
            foreach (var kind in kinds)
            {
                if (kind == null || kind.mesh == null || kind.instances == null || kind.instances.Length == 0) continue;
                var matrices = new Matrix4x4[kind.instances.Length];
                var meshBounds = kind.mesh.bounds;
                for (int i = 0; i < matrices.Length; i++)
                {
                    var it = kind.instances[i];
                    matrices[i] = Matrix4x4.TRS(it.position, Quaternion.Euler(0f, it.yaw, 0f), Vector3.one * it.scale);
                    var b = new Bounds(it.position + meshBounds.center * it.scale, meshBounds.size * it.scale * 1.5f);
                    if (any) bounds.Encapsulate(b);
                    else bounds = b;
                    any = true;
                }
                batches.Add((kind.mesh, matrices));
            }
        }

        void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (material == null || batches.Count == 0) return;
            var rp = new RenderParams(material)
            {
                camera = camera,
                layer = gameObject.layer,
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = receiveShadows,
                lightProbeUsage = LightProbeUsage.Off,
            };
            foreach (var (mesh, matrices) in batches)
                for (int start = 0; start < matrices.Length; start += BatchSize)
                    Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, Mathf.Min(BatchSize, matrices.Length - start), start);
        }
    }
}
