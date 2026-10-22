using System.Collections.Generic;
using BossFight.Hazards;
using UnityEngine;

namespace BossFight.Boss
{
    /// <summary>
    /// 強化階段的外觀（佔位）：BossPhaseData.electrified 的階段（第二階段、狂暴）身上纏繞紅色電光（ElectricArcs），移動時拖出殘影。
    /// 只訂閱 BossController.OnPhaseChanged。轉階段演出第 4 步會先由 Timeline 把電光打開，套用第二階段時這裡接手維持。
    /// 殘影：移動速度超過 minSpeed 時每 interval 秒記下模型每個網格的姿勢，用半透明材質直接畫（Graphics.RenderMesh，不產生物件），lifetime 秒淡出。
    /// 這裡的數字全是視覺參數。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossPhaseVisual : MonoBehaviour
    {
        [SerializeField] BossController boss;
        [Tooltip("身上的電光")]
        [SerializeField] ElectricArcs aura;
        [Tooltip("殘影複製這個模型底下所有的 MeshRenderer")]
        [SerializeField] Transform model;
        [Tooltip("殘影材質（半透明、吃 _BaseColor）")]
        [SerializeField] Material ghostMaterial;
        [SerializeField] [ColorUsage(true, true)] Color ghostColor = new Color(1.6f, 0.15f, 0.12f, 0.45f);
        [Tooltip("每隔幾秒留一個殘影")]
        [Min(0.01f)] [SerializeField] float interval = 0.06f;
        [Tooltip("殘影淡出的時間（秒）")]
        [Min(0.01f)] [SerializeField] float lifetime = 0.3f;
        [Tooltip("移動速度 (m/s) 超過這個值才留殘影")]
        [Min(0f)] [SerializeField] float minSpeed = 2.5f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        struct Part
        {
            public Mesh mesh;
            public Renderer renderer;
            public Transform transform;
        }

        class Ghost
        {
            public Matrix4x4[] matrices;
            public bool[] visible;
            public float age;
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        }

        readonly List<Part> parts = new List<Part>();
        readonly List<Ghost> ghosts = new List<Ghost>();
        readonly Stack<Ghost> pool = new Stack<Ghost>();
        bool active;
        float timer;
        Vector3 lastPosition;

        void Awake()
        {
            if (model == null) return;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = filter.GetComponent<MeshRenderer>();
                if (r == null || filter.sharedMesh == null) continue;
                parts.Add(new Part { mesh = filter.sharedMesh, renderer = r, transform = filter.transform });
            }
        }

        void OnEnable() => boss.OnPhaseChanged += HandlePhaseChanged;

        void OnDisable() => boss.OnPhaseChanged -= HandlePhaseChanged;

        void HandlePhaseChanged(BossPhaseData phase)
        {
            active = phase.electrified;
            if (aura != null) aura.SetIntensity(active ? 1f : 0f);
            lastPosition = transform.position;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (active && ghostMaterial != null)
            {
                float speed = (transform.position - lastPosition).magnitude / dt;
                timer += dt;
                if (speed >= minSpeed && timer >= interval)
                {
                    timer = 0f;
                    Capture();
                }
            }
            lastPosition = transform.position;

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                g.age += dt;
                if (g.age >= lifetime)
                {
                    ghosts.RemoveAt(i);
                    pool.Push(g);
                    continue;
                }
                Draw(g);
            }
        }

        void Capture()
        {
            var g = pool.Count > 0 ? pool.Pop() : new Ghost();
            if (g.matrices == null || g.matrices.Length != parts.Count)
            {
                g.matrices = new Matrix4x4[parts.Count];
                g.visible = new bool[parts.Count];
            }
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                g.visible[i] = p.renderer.enabled && p.renderer.gameObject.activeInHierarchy;
                g.matrices[i] = p.transform.localToWorldMatrix;
            }
            g.age = 0f;
            ghosts.Add(g);
        }

        void Draw(Ghost g)
        {
            float k = 1f - g.age / lifetime;
            Color c = ghostColor * k;
            c.a = ghostColor.a * k;
            g.block.SetColor(BaseColorId, c);
            var rp = new RenderParams(ghostMaterial)
            {
                matProps = g.block,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
            };
            for (int i = 0; i < parts.Count; i++)
            {
                if (g.visible[i]) Graphics.RenderMesh(rp, parts[i].mesh, 0, g.matrices[i]);
            }
        }
    }
}
