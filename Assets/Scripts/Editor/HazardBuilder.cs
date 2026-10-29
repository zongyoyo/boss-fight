using System.Collections.Generic;
using BossFight.Core;
using BossFight.Hazards;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立 Boss 招式用的地面提示、Hazard 與特效（T4）：
    /// 1. 確認 PC_Renderer 有 Decal Renderer Feature（地面提示用 URP Decal Projector）。
    /// 2. 材質（只在不存在時建立）：TelegraphDecal（Shaders/TelegraphDecal.shadergraph）、Fx_Additive（Particles/Unlit 加算、雙面）。
    /// 3. 特效網格（每次重建覆寫）：環牆、地面衝擊環、爪痕、閃電柱。
    /// 4. Prefab（每次重建覆寫）：GroundTelegraph、ShockwaveRing、LightningStrike、Fx_ClawSlash、Fx_ImpactRing、Fx_Bolt、Fx_HitSpark、
    ///    Fx_DamageNumber（Boss 受擊跳的數字，TextMesh + 內建字型）；T8 加上 TornadoVortex（龍捲風）、SafeZone 與 GrandSpinField（大迴旋）；
    ///    序章修正加上刀盾狗的刀光 Fx_DogSlash／Fx_DogSlashHeavy 與種子爆炸 Fx_SeedBlast（BuildExtraFx 可單獨建）。
    /// 選單 Tools > Boss > Build Hazards；BossBuilder 建 Boss 時也會先呼叫這裡。尺寸、顏色、時間都是純視覺參數。
    /// </summary>
    public static class HazardBuilder
    {
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string ShaderPath = "Assets/Shaders/TelegraphDecal.shadergraph";
        const string Dir = "Assets/Models/Hazards";
        const string MatDir = Dir + "/Materials";
        const string MeshDir = Dir + "/Meshes";
        const string PrefabDir = "Assets/Prefabs/Hazards";

        /// <summary>建好的 prefab，給 BossBuilder 接到招式上。</summary>
        public struct Result
        {
            public GroundTelegraph telegraph;
            public ShockwaveRing shockwave;
            public LightningStrike lightning;
            public TimedFx clawSlash;
            public TimedFx impactRing;
            public TimedFx bolt;
            public TimedFx hitSpark;
            public DamageNumber damageNumber;
            public TornadoVortex tornado;
            public GrandSpinField grandSpin;
        }

        /// <summary>序章修正（A1、C8）加的特效：刀盾狗的刀光（輕攻擊、重擊）、種子炸彈的爆炸。</summary>
        public struct ExtraFx
        {
            public TimedFx dogSlash;
            public TimedFx dogSlashHeavy;
            public TimedFx seedBlast;
        }

        /// <summary>只建 ExtraFx 那幾個（CombatFeedbackBuilder、GuardianBuilder 用），不重建其他 Hazard。網格與 prefab 每次覆寫。</summary>
        public static ExtraFx BuildExtraFx()
        {
            var result = new ExtraFx();
            var fxMat = GetFxMaterial();
            if (fxMat == null) return result;
            DaoDunDogBuilder.EnsureFolder(MeshDir);
            DaoDunDogBuilder.EnsureFolder(PrefabDir);
            var fade = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.9f), new Keyframe(1f, 0f));
            result.dogSlash = BuildFx("Fx_DogSlash", BuildSlashMesh("Fx_DogSlash", 150f, 1.05f, 0.26f), fxMat, 0.18f,
                                      new Vector3(0.75f, 1f, 0.75f), new Vector3(1.1f, 1f, 1.1f), fade, 0f);
            result.dogSlashHeavy = BuildFx("Fx_DogSlashHeavy", BuildSlashMesh("Fx_DogSlashHeavy", 200f, 1.2f, 0.42f), fxMat, 0.26f,
                                           new Vector3(0.7f, 1f, 0.7f), new Vector3(1.15f, 1f, 1.15f), fade, 0f);
            result.seedBlast = BuildFx("Fx_SeedBlast", GetBuiltinSphere(), fxMat, 0.4f, Vector3.one * 0.5f, Vector3.one * 2f,
                                       new AnimationCurve(new Keyframe(0f, 1.4f), new Keyframe(0.3f, 0.8f), new Keyframe(1f, 0f)), 6f);
            return result;
        }

        [MenuItem("Tools/Boss/Build Hazards")]
        public static void BuildFromMenu()
        {
            var r = Build();
            if (r.telegraph != null) Selection.activeObject = r.telegraph.gameObject;
        }

        public static Result Build()
        {
            var result = new Result();
            int hurtLayer = LayerMask.NameToLayer(DaoDunDogBuilder.HurtboxLayer);
            if (hurtLayer < 0)
            {
                Debug.LogError($"[Hazard] 找不到 Layer「{DaoDunDogBuilder.HurtboxLayer}」，請先在 Project Settings > Tags and Layers 加上。");
                return result;
            }
            EnsureDecalFeature();

            var decalMat = GetDecalMaterial();
            var fxMat = GetFxMaterial();
            if (decalMat == null || fxMat == null) return result;

            DaoDunDogBuilder.EnsureFolder(MeshDir);
            DaoDunDogBuilder.EnsureFolder(PrefabDir);
            var ringWall = BuildRingWallMesh();
            var flatRing = BuildFlatRingMesh();
            var claw = BuildClawMesh();
            var bolt = BuildBoltMesh();
            var sphere = GetBuiltinSphere();

            result.telegraph = BuildTelegraph(decalMat);
            result.hitSpark = BuildFx("Fx_HitSpark", sphere, fxMat, 0.3f, Vector3.one * 0.6f, Vector3.one * 1.4f, null, 0f);
            result.impactRing = BuildFx("Fx_ImpactRing", flatRing, fxMat, 0.35f, new Vector3(0.3f, 1f, 0.3f), Vector3.one, null, 0f);
            result.clawSlash = BuildFx("Fx_ClawSlash", claw, fxMat, 0.25f, new Vector3(1.1f, 1f, 1f), new Vector3(1.5f, 1.5f, 1f), null, 0f);
            result.bolt = BuildFx("Fx_Bolt", bolt, fxMat, 0.3f, new Vector3(2.2f, 14f, 2.2f), new Vector3(1f, 14f, 1f),
                                  new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.35f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f)),
                                  10f);
            result.shockwave = BuildShockwave(result.telegraph, ringWall, fxMat, hurtLayer);
            BuildExtraFx();
            result.lightning = BuildLightning(result.telegraph, result.bolt, result.impactRing, hurtLayer);
            result.damageNumber = BuildDamageNumber();
            result.tornado = BuildTornado(result.telegraph, ringWall, fxMat, hurtLayer);
            result.grandSpin = BuildGrandSpin(result.telegraph, BuildSafeZone(result.telegraph), hurtLayer);

            AssetDatabase.SaveAssets();
            Debug.Log($"[Hazard] 地面提示、Hazard 與特效已建立：{PrefabDir}");
            return result;
        }

        // ---------- Renderer ----------

        /// 地面提示用 Decal Projector，PC_Renderer 要有 Decal Renderer Feature。
        public static void EnsureDecalFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null)
            {
                Debug.LogWarning($"[Hazard] 找不到 {RendererPath}，請手動加上 Decal Renderer Feature。");
                return;
            }
            foreach (var f in data.rendererFeatures)
            {
                if (f is DecalRendererFeature) return;
            }

            var feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
            feature.name = "Decal";
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
            var so = new SerializedObject(data);
            var list = so.FindProperty("m_RendererFeatures");
            var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            Debug.Log($"[Hazard] 已在 {RendererPath} 加上 Decal Renderer Feature。");
        }

        // ---------- 材質 ----------

        static Material GetDecalMaterial()
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/TelegraphDecal.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                Debug.LogError($"[Hazard] 找不到 {ShaderPath}。");
                return null;
            }
            mat = new Material(shader) { name = "TelegraphDecal" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// 加算、雙面、吃頂點色的 Particles/Unlit；顏色由 TimedFx／ShockwaveRing 用 MaterialPropertyBlock 改 _BaseColor。
        static Material GetFxMaterial()
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/Fx_Additive.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[Hazard] 找不到 Universal Render Pipeline/Particles/Unlit。");
                return null;
            }
            mat = new Material(shader) { name = "Fx_Additive" };
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 2f);   // Additive
            mat.SetFloat("_Cull", 0f);    // 雙面
            mat.SetColor("_BaseColor", Color.white);
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            UnityEditor.Rendering.Universal.ShaderGUI.ParticleGUI.SetMaterialKeywords(mat);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------- Prefab ----------

        static GroundTelegraph BuildTelegraph(Material decalMat)
        {
            var go = new GameObject("GroundTelegraph");
            var projector = go.AddComponent<DecalProjector>();
            projector.material = decalMat;
            projector.size = new Vector3(4f, 4f, 0.3f);
            projector.pivot = new Vector3(0f, 0f, 0.15f);
            // 只投在朝上的面：角色身體的側面不會被染色
            projector.startAngleFade = 40f;
            projector.endAngleFade = 70f;
            var telegraph = go.AddComponent<GroundTelegraph>();
            DaoDunDogBuilder.SetReference(telegraph, "projector", projector);
            DaoDunDogBuilder.SetReference(telegraph, "palette", CombatFeedbackBuilder.EnsureTelegraphPalette());
            return SavePrefab(go, "GroundTelegraph").GetComponent<GroundTelegraph>();
        }

        static TimedFx BuildFx(string name, Mesh mesh, Material mat, float duration, Vector3 startScale, Vector3 endScale,
                               AnimationCurve intensity, float lightIntensity)
        {
            var go = new GameObject(name);
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Light light = null;
            if (lightIntensity > 0f)
            {
                var lightGo = new GameObject("Flash");
                lightGo.transform.SetParent(go.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.15f, 0f); // 縮放後在離地約 2 m 處
                light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 12f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }

            var fx = go.AddComponent<TimedFx>();
            var so = new SerializedObject(fx);
            var renderers = so.FindProperty("renderers");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.FindProperty("flashLight").objectReferenceValue = light;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("startScale").vector3Value = startScale;
            so.FindProperty("endScale").vector3Value = endScale;
            if (intensity != null) so.FindProperty("intensityCurve").animationCurveValue = intensity;
            so.FindProperty("lightIntensity").floatValue = lightIntensity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, name).GetComponent<TimedFx>();
        }

        static ShockwaveRing BuildShockwave(GroundTelegraph telegraphPrefab, Mesh ringWall, Material fxMat, int hurtLayer)
        {
            var go = new GameObject("ShockwaveRing");
            var hitbox = AddHitbox(go, hurtLayer);

            var band = (GroundTelegraph)PrefabUtility.InstantiatePrefab(telegraphPrefab);
            band.name = "Band";
            band.transform.SetParent(go.transform, false);

            var wall = new GameObject("Wall");
            wall.transform.SetParent(go.transform, false);
            wall.AddComponent<MeshFilter>().sharedMesh = ringWall;
            var renderer = wall.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fxMat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var ring = go.AddComponent<ShockwaveRing>();
            var so = new SerializedObject(ring);
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("band").objectReferenceValue = band;
            so.FindProperty("wall").objectReferenceValue = wall.transform;
            so.FindProperty("wallRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "ShockwaveRing").GetComponent<ShockwaveRing>();
        }

        static LightningStrike BuildLightning(GroundTelegraph telegraphPrefab, TimedFx bolt, TimedFx impact, int hurtLayer)
        {
            var go = new GameObject("LightningStrike");
            var hitbox = AddHitbox(go, hurtLayer);

            var circle = (GroundTelegraph)PrefabUtility.InstantiatePrefab(telegraphPrefab);
            circle.name = "Circle";
            circle.transform.SetParent(go.transform, false);

            var strike = go.AddComponent<LightningStrike>();
            var so = new SerializedObject(strike);
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("circle").objectReferenceValue = circle;
            so.FindProperty("boltPrefab").objectReferenceValue = bolt;
            so.FindProperty("impactPrefab").objectReferenceValue = impact;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "LightningStrike").GetComponent<LightningStrike>();
        }

        /// 龍捲風：地上的紅圈，加上幾層由下往上越來越大的環牆（和衝擊波同一個網格、同一個加算材質），TornadoVortex 讓它們各自旋轉。
        static TornadoVortex BuildTornado(GroundTelegraph telegraphPrefab, Mesh ringWall, Material fxMat, int hurtLayer)
        {
            const int layerCount = 5;
            var go = new GameObject("TornadoVortex");
            var hitbox = AddHitbox(go, hurtLayer);

            var circle = (GroundTelegraph)PrefabUtility.InstantiatePrefab(telegraphPrefab);
            circle.name = "Circle";
            circle.transform.SetParent(go.transform, false);

            var layers = new Transform[layerCount];
            var renderers = new Renderer[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                var layer = new GameObject($"Layer{i + 1}");
                layer.transform.SetParent(go.transform, false);
                layer.AddComponent<MeshFilter>().sharedMesh = ringWall;
                var renderer = layer.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = fxMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                layers[i] = layer.transform;
                renderers[i] = renderer;
            }

            var vortex = go.AddComponent<TornadoVortex>();
            var so = new SerializedObject(vortex);
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("circle").objectReferenceValue = circle;
            SetArray(so.FindProperty("layers"), layers);
            SetArray(so.FindProperty("layerRenderers"), renderers);
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "TornadoVortex").GetComponent<TornadoVortex>();
        }

        /// 大迴旋的安全區：地上的圓圈（顏色在 SafeZone 上指定）與圈中央的漂浮盾牌（T14 佔位標記）。
        static SafeZone BuildSafeZone(GroundTelegraph telegraphPrefab)
        {
            var go = new GameObject("SafeZone");
            var circle = (GroundTelegraph)PrefabUtility.InstantiatePrefab(telegraphPrefab);
            circle.name = "Circle";
            circle.transform.SetParent(go.transform, false);
            var shield = BuildShieldMarker(go.transform);
            var zone = go.AddComponent<SafeZone>();
            DaoDunDogBuilder.SetReference(zone, "circle", circle);
            DaoDunDogBuilder.SetReference(zone, "shield", shield);
            return SavePrefab(go, "SafeZone").GetComponent<SafeZone>();
        }

        /// 佔位的發光圓盾（直立、約 0.9 m）：外框、盾面、中間的十字紋與盾心，正反兩面都有（會自轉），都是基本形體、沒有碰撞。高度與動作由 SafeZone 執行時擺。
        static Transform BuildShieldMarker(Transform parent)
        {
            var rimMat = GetGlowMat("Fx_ShieldRim", new Color(0.75f, 0.85f, 0.95f), new Color(0.45f, 0.75f, 1.1f));
            var faceMat = GetGlowMat("Fx_ShieldFace", new Color(0.1f, 0.3f, 0.75f), new Color(0.03f, 0.18f, 0.6f));
            var emblemMat = GetGlowMat("Fx_ShieldEmblem", Color.white, new Color(1.1f, 1.2f, 1.3f));

            var root = DaoDunDogBuilder.Node(parent, "ShieldMarker", Vector3.zero);
            Part(root, "Rim", PrimitiveType.Cylinder, rimMat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(0.9f, 0.03f, 0.9f));
            foreach (float side in new[] { 1f, -1f })
            {
                string suffix = side > 0f ? "Front" : "Back";
                Part(root, "Face" + suffix, PrimitiveType.Cylinder, faceMat, new Vector3(0f, 0f, 0.035f * side), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.78f, 0.03f, 0.78f));
                Part(root, "CrossV" + suffix, PrimitiveType.Cube, emblemMat, new Vector3(0f, 0f, 0.08f * side), Quaternion.identity, new Vector3(0.12f, 0.6f, 0.04f));
                Part(root, "CrossH" + suffix, PrimitiveType.Cube, emblemMat, new Vector3(0f, 0.05f, 0.08f * side), Quaternion.identity, new Vector3(0.5f, 0.12f, 0.04f));
                Part(root, "Center" + suffix, PrimitiveType.Sphere, rimMat, new Vector3(0f, 0.05f, 0.1f * side), Quaternion.identity, new Vector3(0.16f, 0.16f, 0.08f));
            }
            return root;
        }

        static void Part(Transform parent, string name, PrimitiveType type, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// 自己發光的 URP Lit 材質（HDR Emission，配合 Bloom 看起來發亮）；只在不存在時建立。
        static Material GetGlowMat(string name, Color color, Color emission)
        {
            DaoDunDogBuilder.EnsureFolder(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.6f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// 大迴旋的全場判定：Hitbox、落地時整片閃紅光的地面提示；安全區執行時生成在底下。
        static GrandSpinField BuildGrandSpin(GroundTelegraph telegraphPrefab, SafeZone zonePrefab, int hurtLayer)
        {
            var go = new GameObject("GrandSpinField");
            var hitbox = AddHitbox(go, hurtLayer);

            var flash = (GroundTelegraph)PrefabUtility.InstantiatePrefab(telegraphPrefab);
            flash.name = "Flash";
            flash.transform.SetParent(go.transform, false);

            var field = go.AddComponent<GrandSpinField>();
            var so = new SerializedObject(field);
            so.FindProperty("hitbox").objectReferenceValue = hitbox;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("zonePrefab").objectReferenceValue = zonePrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "GrandSpinField").GetComponent<GrandSpinField>();
        }

        static void SetArray<T>(SerializedProperty list, T[] values) where T : Object
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// 傷害數字：粗體白字，加一層往右下、往後偏一點的黑色陰影；字型用 Unity 內建的 LegacyRuntime.ttf。
        static DamageNumber BuildDamageNumber()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Fx_DamageNumber");
            var shadow = AddText(go.transform, "Shadow", font, new Color(0f, 0f, 0f, 0.85f), new Vector3(0.025f, -0.025f, 0.01f));
            var text = AddText(go.transform, "Text", font, Color.white, Vector3.zero);

            var number = go.AddComponent<DamageNumber>();
            var so = new SerializedObject(number);
            so.FindProperty("text").objectReferenceValue = text;
            so.FindProperty("shadow").objectReferenceValue = shadow;
            so.ApplyModifiedPropertiesWithoutUndo();
            return SavePrefab(go, "Fx_DamageNumber").GetComponent<DamageNumber>();
        }

        static TextMesh AddText(Transform parent, string name, Font font, Color color, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.06f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.text = "0";
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        /// 掃 Hurtbox Layer 的 Hitbox；攻擊方在生成時由招式指定（Hazard.SetAttacker）。
        internal static Hitbox AddHitbox(GameObject go, int hurtLayer)
        {
            var hitbox = go.AddComponent<Hitbox>();
            var so = new SerializedObject(hitbox);
            so.FindProperty("targetMask").intValue = 1 << hurtLayer;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hitbox;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        // ---------- 網格 ----------

        /// 半徑 1、高 1 的開口圓柱（底部不透明、往上淡出），衝擊波的環牆。
        static Mesh BuildRingWallMesh()
        {
            const int segments = 64;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                verts.Add(d);
                verts.Add(d + Vector3.up);
                colors.Add(Color.white);
                colors.Add(new Color(1f, 1f, 1f, 0f));
                if (i == segments) continue;
                int b = i * 2;
                tris.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
            }
            return SaveMesh("Fx_RingWall", verts, colors, tris);
        }

        /// 平放的環（內外緣透明、中間最亮），落地與撞牆的衝擊環；半徑 1。
        static Mesh BuildFlatRingMesh()
        {
            const int segments = 64;
            float[] radii = { 0.7f, 0.9f, 1f };
            float[] alpha = { 0f, 1f, 0f };
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int r = 0; r < radii.Length; r++)
                {
                    verts.Add(d * radii[r] + Vector3.up * 0.05f);
                    colors.Add(new Color(1f, 1f, 1f, alpha[r]));
                }
                if (i == segments) continue;
                int b = i * radii.Length;
                for (int r = 0; r < radii.Length - 1; r++)
                {
                    int p = b + r, q = b + r + radii.Length;
                    tris.AddRange(new[] { p, q, p + 1, p + 1, q, q + 1 });
                }
            }
            return SaveMesh("Fx_FlatRing", verts, colors, tris);
        }

        /// 三道微彎的爪痕，立在 XY 平面（面向 ±Z），兩端尖、中間粗；高約 1.8 m。
        /// 水平的新月形刀光：在 XZ 平面、往 +Z 張開 arc 度、外半徑 radius，中間最寬 thickness、兩端收尖；外緣不透明、往內淡出。
        static Mesh BuildSlashMesh(string assetName, float arc, float radius, float thickness)
        {
            const int steps = 24;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                float angle = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t) * Mathf.Deg2Rad;
                float width = thickness * Mathf.Sin(t * Mathf.PI) + 0.01f;
                var dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                verts.Add(dir * radius);
                verts.Add(dir * (radius - width));
                float a = Mathf.Sin(t * Mathf.PI);
                colors.Add(new Color(1f, 1f, 1f, a));
                colors.Add(new Color(1f, 1f, 1f, 0f));
                if (i == steps) continue;
                int b = i * 2;
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            return SaveMesh(assetName, verts, colors, tris);
        }

        static Mesh BuildClawMesh()
        {
            const int steps = 12;
            float[] offsets = { -0.38f, 0f, 0.38f };
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            foreach (float ox in offsets)
            {
                int start = verts.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float t = (float)i / steps;
                    float y = Mathf.Lerp(0.9f, -0.9f, t);
                    float x = ox + Mathf.Sin(t * Mathf.PI) * 0.18f;
                    float half = Mathf.Sin(t * Mathf.PI) * 0.1f + 0.005f;
                    verts.Add(new Vector3(x - half, y, 0f));
                    verts.Add(new Vector3(x + half, y, 0f));
                    var c = new Color(1f, 1f, 1f, Mathf.Sin(t * Mathf.PI));
                    colors.Add(c);
                    colors.Add(c);
                    if (i == steps) continue;
                    int b = start + i * 2;
                    tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            return SaveMesh("Fx_ClawSlash", verts, colors, tris);
        }

        /// 鋸齒狀的閃電柱，兩片十字交叉的面，高 1（底部在 0）、寬約 0.15；由 TimedFx 拉長到天上。
        static Mesh BuildBoltMesh()
        {
            const int steps = 10;
            const float half = 0.075f;
            var random = new System.Random(4);
            var path = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float jitter = i == 0 ? 0f : (float)(random.NextDouble() * 2.0 - 1.0) * 0.16f;
                path[i] = new Vector2(jitter, (float)i / steps);
            }

            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int plane = 0; plane < 2; plane++)
            {
                int start = verts.Count;
                for (int i = 0; i <= steps; i++)
                {
                    float x = path[i].x, y = path[i].y;
                    var a = plane == 0 ? new Vector3(x - half, y, 0f) : new Vector3(0f, y, x - half);
                    var b = plane == 0 ? new Vector3(x + half, y, 0f) : new Vector3(0f, y, x + half);
                    verts.Add(a);
                    verts.Add(b);
                    var c = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.4f, y));
                    colors.Add(c);
                    colors.Add(c);
                    if (i == steps) continue;
                    int s = start + i * 2;
                    tris.AddRange(new[] { s, s + 2, s + 1, s + 1, s + 2, s + 3 });
                }
            }
            return SaveMesh("Fx_Bolt", verts, colors, tris);
        }

        static Mesh SaveMesh(string assetName, List<Vector3> verts, List<Color> colors, List<int> tris)
        {
            string path = $"{MeshDir}/{assetName}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = assetName };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Mesh GetBuiltinSphere()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            return mesh;
        }
    }
}
