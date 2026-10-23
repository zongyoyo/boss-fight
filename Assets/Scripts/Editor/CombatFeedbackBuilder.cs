using System.Linq;
using BossFight.Boss;
using BossFight.Cameras;
using BossFight.Core;
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
    /// 2. 玩家（DaoDunDog.prefab）與 Boss（OiiaCat.prefab）加上 CinemachineImpulseSource、FeedbackEmitter 與 PlayerFeedback／BossImpactFeedback；
    ///    場景裡的實例另外接 AudioManager（場景物件，prefab 不能引用）。
    /// 3. 自由視角與鎖定視角兩台 CinemachineCamera 加上 CinemachineImpulseListener（鏡頭震動）。
    /// CameraRigBuilder（DaoDunDogBuilder 也會跑）、BossBuilder、IntroCutsceneBuilder（重建 AudioManager）結束時呼叫 Wire 重接，
    /// 重建角色、Boss、鏡頭或 AudioManager 後引用不會斷。
    /// </summary>
    public static class CombatFeedbackBuilder
    {
        const string DataDir = "Assets/ScriptableObjects/Feedback";
        const string DataPath = DataDir + "/CombatFeedback.asset";
        const string PlayerPrefabPath = "Assets/Prefabs/DaoDunDog.prefab";
        const string BossPrefabPath = "Assets/Prefabs/OiiaCat.prefab";

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

        static void Wire(CombatFeedbackData data)
        {
            SetupPrefab<PlayerFeedback>(PlayerPrefabPath, data);
            SetupPrefab<BossImpactFeedback>(BossPrefabPath, data);

            var audio = Object.FindAnyObjectByType<AudioManager>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var boss = Object.FindAnyObjectByType<BossController>();
            if (player != null) WireInstance<PlayerFeedback>(player.gameObject, data, audio);
            if (boss != null) WireInstance<BossImpactFeedback>(boss.gameObject, data, audio);
            int listeners = AddListeners();

            string missing = (player == null ? " 玩家" : "") + (boss == null ? " Boss" : "") + (audio == null ? " AudioManager（先跑 Build Intro）" : "") +
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
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
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
