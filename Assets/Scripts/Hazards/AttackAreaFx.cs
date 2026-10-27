using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Hazards
{
    /// <summary>純視覺範圍效果：藤蔓掃痕、下降葉片與破土根刺。沒有 Collider，也不決定傷害或安全區。</summary>
    public class AttackAreaFx : MonoBehaviour
    {
        [SerializeField] MeshFilter filter;
        [SerializeField] MeshRenderer surface;
        [SerializeField] LineRenderer vine;
        [SerializeField] Material rootMaterial;
        [SerializeField] Material leafMaterial;
        [Header("純視覺")]
        [Min(0.01f), SerializeField] float fadeTime = 0.35f;
        [Min(0.01f), SerializeField] float riseTime = 0.08f;
        [Min(0f), SerializeField] float fallHeight = 4f;
        [Min(0.01f), SerializeField] float rootWidth = 0.2f;
        [Min(0.01f), SerializeField] float vineWidth = 0.45f;
        [Min(8), SerializeField] int ringSegments = 48;
        [Min(4), SerializeField] int fieldSamples = 17;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        Mesh mesh;
        MaterialPropertyBlock block;
        AttackArea area;
        AreaStrikeStyle style;
        Color tint;
        float progress, impactAge, fadeAge;
        bool struck, released;

        void Awake()
        {
            mesh = new Mesh { name = "AttackAreaFx_Runtime" }; mesh.MarkDynamic(); filter.sharedMesh = mesh;
            block = new MaterialPropertyBlock();
        }
        public void Show(AttackArea value, AreaStrikeStyle look, Color color, bool prewarning)
        {
            area = value; style = look; tint = color; struck = !prewarning;
            surface.sharedMaterial = style == AreaStrikeStyle.Leaf ? leafMaterial : rootMaterial;
            surface.enabled = style != AreaStrikeStyle.Vine; vine.enabled = style == AreaStrikeStyle.Vine;
            Draw();
        }
        public void SetArea(AttackArea value, float fill) { area = value; progress = fill; }
        public void Strike() { struck = true; progress = 1f; impactAge = 0f; }
        public void Release() { released = true; fadeAge = 0f; }
        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            if (struck) impactAge += Time.deltaTime;
            if (released)
            {
                fadeAge += Time.deltaTime;
                if (fadeAge >= fadeTime) { Destroy(gameObject); return; }
            }
            Draw();
        }
        void Draw()
        {
            float fade = released ? 1f - Mathf.Clamp01(fadeAge / fadeTime) : 1f;
            if (style == AreaStrikeStyle.Vine) { DrawVine(fade); return; }
            vertices.Clear(); triangles.Clear();
            if (style == AreaStrikeStyle.Leaf) DrawLeaves(fade);
            else DrawRoots(fade);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            block.SetColor("_EmissionColor", tint * (0.4f * fade));
            surface.SetPropertyBlock(block);
        }
        void DrawVine(float fade)
        {
            const int points = 49;
            vine.positionCount = points; vine.widthMultiplier = vineWidth * fade;
            Color c = tint; c.a = fade; vine.startColor = vine.endColor = c;
            Vector3 f = area.forward.sqrMagnitude > 0f ? area.forward.normalized : Vector3.forward;
            for (int i = 0; i < points; i++)
            {
                float t = (float)i / (points - 1);
                Vector3 p;
                if (area.shape == AttackAreaShape.Sector)
                    p = area.center + Quaternion.Euler(0f, Mathf.Lerp(-area.angle * 0.5f, area.angle * 0.5f, t), 0f) * f * area.radius;
                else p = area.center + f * area.length * t;
                p.y = area.center.y + 0.25f + Mathf.Sin(t * Mathf.PI) * 0.3f * fade;
                vine.SetPosition(i, p);
            }
        }
        void DrawLeaves(float fade)
        {
            const int count = 9;
            for (int i = 0; i < count; i++)
            {
                float angle = i * 137.508f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (area.radius * 0.7f * Mathf.Sqrt((float)i / count));
                Vector3 p = area.center + offset;
                float h = struck ? Mathf.Sin(Mathf.Min(impactAge * 8f, Mathf.PI)) * 0.3f : fallHeight * (1f - progress * progress);
                p.y += h + 0.08f;
                Vector3 side = Quaternion.Euler(0f, angle + impactAge * 240f, 0f) * Vector3.right * (0.35f * fade);
                Vector3 forward = Vector3.Cross(Vector3.up, side) * 1.5f;
                int start = vertices.Count;
                vertices.Add(p - side); vertices.Add(p + forward); vertices.Add(p + side); vertices.Add(p - forward);
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }
        void DrawRoots(float fade)
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(impactAge / riseTime));
            float height = area.height * 0.9f * rise * fade;
            if (area.shape == AttackAreaShape.Ring)
            {
                float r = (area.radius + area.innerRadius) * 0.5f;
                float width = Mathf.Min(rootWidth, (area.radius - area.innerRadius) * 0.35f);
                for (int i = 0; i < ringSegments; i++)
                {
                    Vector3 p = area.center + Quaternion.Euler(0f, i * 360f / ringSegments, 0f) * Vector3.forward * r;
                    if (Inside(p, width)) Spike(p, width, height);
                }
            }
            else if (area.shape == AttackAreaShape.Circle)
            {
                for (int i = 0; i < 9; i++)
                {
                    Vector3 p = area.center + Quaternion.Euler(0f, i * 137.508f, 0f) * Vector3.forward * (area.radius * 0.65f * Mathf.Sqrt(i / 9f));
                    if (Inside(p, rootWidth)) Spike(p, rootWidth, height * (0.8f + 0.2f * Mathf.Sin(i)));
                }
            }
            else
            {
                for (int x = 0; x < fieldSamples; x++) for (int z = 0; z < fieldSamples; z++)
                {
                    Vector3 p = area.fieldCenter + new Vector3(Mathf.Lerp(-area.fieldRadius, area.fieldRadius, x / (float)(fieldSamples - 1)), 0f,
                        Mathf.Lerp(-area.fieldRadius, area.fieldRadius, z / (float)(fieldSamples - 1)));
                    if (Inside(p, rootWidth)) Spike(p, rootWidth, height);
                }
            }
        }
        bool Inside(Vector3 p, float width)
        {
            if (!area.Contains(p, -width)) return false;
            if (area.safeCenters != null)
                foreach (var center in area.safeCenters)
                    if (new Vector2(p.x - center.x, p.z - center.z).magnitude < area.safeRadius + width) return false;
            return true;
        }
        void Spike(Vector3 p, float width, float height)
        {
            const int sides = 6;
            int start = vertices.Count;
            vertices.Add(p + new Vector3(width * 0.3f, height, width * 0.3f));
            for (int i = 0; i < sides; i++) vertices.Add(p + Quaternion.Euler(0f, i * 360f / sides, 0f) * Vector3.forward * width);
            for (int i = 0; i < sides; i++) { triangles.Add(start); triangles.Add(start + 1 + i); triangles.Add(start + 1 + (i + 1) % sides); }
        }
        void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
