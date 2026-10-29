using System.Linq;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
using BossFight.Hazards;
using BossFight.Player;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// Tools > Feedback > Build Combat Feedback（T11 打擊回饋）：
    /// 1. 合成戰鬥佔位音效（PlaceholderAudioBuilder.BuildCombat），建立或更新 Assets/ScriptableObjects/Feedback/CombatFeedback.asset；
    ///    資產已經存在時數值不動，音效只填空的或還是佔位音效（Assets/Audio/Placeholder）的欄位，換成正式音效後重建不會蓋掉。
    /// 2. 玩家（Characters/DaoDunDog.prefab）與 Boss（OiiaCat.prefab）加上 CinemachineImpulseSource、FeedbackEmitter 與 PlayerFeedback／BossImpactFeedback；
    ///    Boss 的場景實例另外接 AudioManager（場景物件，prefab 不能引用）；玩家是執行時生成的，AudioManager 由 PlayerSpawner 填（這裡最後跑 PlayerSpawnerBuilder.Wire）。
    ///    玩家的 PlayerFeedback 另外接刀光（HazardBuilder.BuildExtraFx）與成功閃避的火花；序章的大樹守衛、小樹精、菊花的佔位音由 PrologueAudioBuilder 合成，一起填進同一份資產。
    /// 3. 自由視角與鎖定視角兩台 CinemachineCamera 加上 CinemachineImpulseListener（鏡頭震動）。
    /// 4. 預兆分類色 TelegraphPalette.asset（沒有才建）接到 GroundTelegraph、OIIA 貓、大樹守衛、小樹精的 prefab（白可格擋、紅不可格擋、綠安全區只有這一份）。
    /// CameraRigBuilder（DaoDunDogBuilder 也會跑）、BossBuilder、IntroCutsceneBuilder（重建 AudioManager）結束時呼叫 Wire 重接，
    /// 重建角色、Boss、鏡頭或 AudioManager 後引用不會斷。
    /// </summary>
    public static class CombatFeedbackBuilder
    {
        const string DataDir = "Assets/ScriptableObjects/Feedback";
        const string DataPath = DataDir + "/CombatFeedback.asset";
        const string PlayerPrefabPath = DaoDunDogBuilder.PrefabPath;
        const string BossPrefabPath = "Assets/Prefabs/OiiaCat.prefab";
        const string PalettePath = DataDir + "/TelegraphPalette.asset";
        static readonly string[] PalettePrefabs =
        {
            "Assets/Prefabs/Hazards/GroundTelegraph.prefab", BossPrefabPath, "Assets/Prefabs/TreeGuardian.prefab", "Assets/Prefabs/Prologue/TrainingSapling.prefab",
        };

        [MenuItem("Tools/Feedback/Build Combat Feedback")]
        public static void BuildFromMenu()
        {
            var data = BuildData();
            Wire(data);
            Selection.activeObject = data;
        }

        /// <summary>重接玩家、Boss、鏡頭與 AudioManager；資產還不存在時先建立（含音效）。</summary>
        public static void Wire()
        {
            var data = AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(DataPath);
            if (data == null) data = BuildData();
            Wire(data);
        }

        /// <summary>合成音效，建立或更新資產。</summary>
        public static CombatFeedbackData BuildData()
        {
            var clips = PlaceholderAudioBuilder.BuildCombat();
            foreach (var pair in PrologueAudioBuilder.BuildCombatCues()) clips[pair.Key] = pair.Value; // 序章：大樹守衛招式、小樹精、菊花

            var data = AssetDatabase.LoadAssetAtPath<CombatFeedbackData>(DataPath);
            if (data == null)
            {
                DaoDunDogBuilder.EnsureFolder(DataDir);
                data = ScriptableObject.CreateInstance<CombatFeedbackData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            var so = new SerializedObject(data);
            int filled = 0;
            foreach (var pair in clips)
            {
                var cue = so.FindProperty(pair.Key);
                if (cue == null)
                {
                    Debug.LogWarning($"[Feedback] CombatFeedbackData 沒有欄位 {pair.Key}，這組音效沒有填進去。");
                    continue;
                }
                var list = cue.FindPropertyRelative("clips");
                if (!IsEmptyOrPlaceholder(list)) continue;
                list.arraySize = pair.Value.Length;
                for (int i = 0; i < pair.Value.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = pair.Value[i];
                filled++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Feedback] {DataPath}：填入 {filled} 組佔位音效");
            return data;
        }

        static bool IsEmptyOrPlaceholder(SerializedProperty list)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                var clip = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (clip != null && !AssetDatabase.GetAssetPath(clip).StartsWith(PlaceholderAudioBuilder.Dir)) return false;
            }
            return true;
        }

        /// <summary>預兆的紅、白、安全區綠（TelegraphPaletteData）：沒有就建，回傳資產。</summary>
        public static TelegraphPaletteData EnsureTelegraphPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<TelegraphPaletteData>(PalettePath);
            if (palette != null) return palette;
            DaoDunDogBuilder.EnsureFolder(DataDir);
            palette = ScriptableObject.CreateInstance<TelegraphPaletteData>();
            AssetDatabase.CreateAsset(palette, PalettePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Feedback] 已建立 {PalettePath}");
            return palette;
        }

        /// <summary>把分類色接到所有預兆 prefab：地面範圍（GroundTelegraph）、OIIA 貓與大樹守衛的預兆閃光、小樹精。沒有的 prefab 跳過。</summary>
        public static void WireTelegraphPalette()
        {
            var palette = EnsureTelegraphPalette();
            foreach (var path in PalettePrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var c in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (!(c is GroundTelegraph || c is BossTelegraphFlash || c is TrainingSaplingVisual)) continue;
                        var so = new SerializedObject(c);
                        var p = so.FindProperty("palette");
                        if (p == null || p.objectReferenceValue == palette) continue;
                        p.objectReferenceValue = palette;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        static void Wire(CombatFeedbackData data)
        {
            SetupPrefab<PlayerFeedback>(PlayerPrefabPath, data);
            SetupPrefab<BossImpactFeedback>(BossPrefabPath, data);
            WireTelegraphPalette();

            var audio = Object.FindAnyObjectByType<AudioManager>();
            var boss = Object.FindAnyObjectByType<BossController>();
            if (boss != null) WireInstance<BossImpactFeedback>(boss.gameObject, data, audio);
            int listeners = AddListeners();
            var spawner = PlayerSpawnerBuilder.Wire(); // 生成的角色的 AudioManager

            string missing = (spawner == null ? " PlayerSpawner" : "") + (boss == null ? " Boss" : "") + (audio == null ? " AudioManager（先跑 Build Intro）" : "") +
                             (listeners == 0 ? " 鏡頭（先跑 Build Camera Rig）" : "");
            Debug.Log($"[Feedback] 打擊回饋已接上：鏡頭震動 {listeners} 台" + (missing.Length > 0 ? $"；缺少：{missing}" : ""));
        }

        // prefab 上的部分：只引用 prefab 自己與資產
        static void SetupPrefab<TFeedback>(string path, CombatFeedbackData data) where TFeedback : Component
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[Feedback] 找不到 {path}，只接場景裡的物件。");
                return;
            }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Setup<TFeedback>(root, data, false);
                var playerFeedback = root.GetComponent<PlayerFeedback>();
                if (playerFeedback != null) WirePlayerVisuals(playerFeedback);
                if (root.GetComponent<PlayerController>() != null) DaoDunDogBuilder.WireRig(root); // PlayerRig 也接上剛加的回饋元件
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 玩家的可見回饋（序章修正 A1–A5）：刀光、成功閃避的火花；其他數值留在元件上，重建不會蓋掉
        static void WirePlayerVisuals(PlayerFeedback feedback)
        {
            var fx = HazardBuilder.BuildExtraFx();
            var spark = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Hazards/Fx_HitSpark.prefab");
            var so = new SerializedObject(feedback);
            so.FindProperty("slashPrefab").objectReferenceValue = fx.dogSlash;
            so.FindProperty("heavySlashPrefab").objectReferenceValue = fx.dogSlashHeavy;
            so.FindProperty("evadeSparkPrefab").objectReferenceValue = spark != null ? spark.GetComponent<BossFight.Hazards.TimedFx>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 場景實例：prefab 已經有的元件直接沿用；不是 prefab 實例時就地加上。AudioManager 是場景物件，只能接在實例上
        static void WireInstance<TFeedback>(GameObject go, CombatFeedbackData data, AudioManager audio) where TFeedback : Component
        {
            var emitter = Setup<TFeedback>(go, data, true);
            var so = new SerializedObject(emitter);
            so.FindProperty("audioManager").objectReferenceValue = audio;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        static FeedbackEmitter Setup<TFeedback>(GameObject go, CombatFeedbackData data, bool undo) where TFeedback : Component
        {
            var impulse = Add<CinemachineImpulseSource>(go, undo);
            var def = impulse.ImpulseDefinition;
            def.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Dissipating;
            def.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            def.ImpulseDuration = data.shakeDuration;
            def.DissipationDistance = data.shakeDistance;
            def.DissipationRate = data.shakeSpread;
            EditorUtility.SetDirty(impulse);

            var emitter = Add<FeedbackEmitter>(go, undo);
            Add<TFeedback>(go, undo);
            var so = new SerializedObject(emitter);
            so.FindProperty("data").objectReferenceValue = data;
            so.FindProperty("impulse").objectReferenceValue = impulse;
            so.ApplyModifiedPropertiesWithoutUndo();
            return emitter;
        }

        static T Add<T>(GameObject go, bool undo) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) return c;
            return undo ? Undo.AddComponent<T>(go) : go.AddComponent<T>();
        }

        // 戰鬥的兩台鏡頭（自由視角、鎖定）收震動；演出鏡頭不收
        static int AddListeners()
        {
            var cameraController = Object.FindAnyObjectByType<CameraController>();
            if (cameraController == null) return 0;
            var so = new SerializedObject(cameraController);
            var cams = new[] { "freeLookCamera", "lockOnCamera" }
                .Select(n => so.FindProperty(n).objectReferenceValue as CinemachineCamera)
                .Where(c => c != null).ToArray();
            foreach (var cam in cams)
            {
                var listener = Add<CinemachineImpulseListener>(cam.gameObject, true);
                Undo.RecordObject(listener, "Setup Impulse Listener");
                listener.ApplyAfter = CinemachineCore.Stage.Noise;
                listener.ChannelMask = 1;
                listener.Gain = 1f;
                listener.Use2DDistance = false;
                listener.UseCameraSpace = true;
                EditorUtility.SetDirty(listener);
            }
            if (cams.Length > 0) EditorSceneManager.MarkSceneDirty(cameraController.gameObject.scene);
            return cams.Length;
        }
    }
}
