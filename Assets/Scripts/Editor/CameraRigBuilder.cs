using BossFight.Cameras;
using BossFight.Player;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 建立 Cinemachine 3 鏡頭：Main Camera 掛 CinemachineBrain（並拿掉舊的手寫鏡頭），
    /// 加上自由視角與鎖定視角兩台 CinemachineCamera、鎖定用的 TargetGroup 與 CameraController，
    /// 接好 PlayerStats；Follow 與鎖定群組先綁 PlayerSpawner 底下的預覽模型（編輯模式預覽用，進 Play 時 CameraController.BindPlayer 改成生成的角色），
    /// 最後跑 PlayerSpawnerBuilder.Wire（Main Camera、鎖定清單與 CameraController 接到 spawner）。
    /// 選單 Tools > Camera > Build Camera Rig；DaoDunDogBuilder 重建角色後也會呼叫。重建時刪掉舊的 CameraRig 重做。
    /// 最後呼叫 CombatFeedbackBuilder.Wire：兩台鏡頭加上鏡頭震動的 ImpulseListener、玩家的打擊回饋重接（T11）。
    /// 距離、高度、阻尼、混合時間都是純視覺參數。
    /// </summary>
    public static class CameraRigBuilder
    {
        const string RigName = "CameraRig";
        const string StatsPath = "Assets/ScriptableObjects/Player/PlayerStats.asset";

        const float PivotHeight = 1.1f;     // 鏡頭繞著角色這個高度轉
        const float FreeLookRadius = 5.5f;
        const float LockOnRadius = 6f;
        const float StartPitch = 15f;
        const float LockOnPitch = 12f;
        static readonly Vector2 PitchRange = new Vector2(-10f, 60f);
        const float FollowDamping = 0.1f;
        const float BlendTime = 0.3f;
        const int FreeLookPriority = 10;

        [MenuItem("Tools/Camera/Build Camera Rig")]
        public static void BuildFromMenu()
        {
            var preview = PlayerSpawnerBuilder.Preview();
            if (preview == null)
            {
                Debug.LogError("[Camera] 場景裡沒有 PlayerSpawner（或它沒有預覽模型），請先用 Tools > Player > Build DaoDun Dog 建立。");
                return;
            }
            Selection.activeGameObject = Build(preview.Controller).gameObject;
        }

        /// <summary>player 是 PlayerSpawner 底下的預覽模型。</summary>
        public static CameraController Build(PlayerController player)
        {
            Undo.SetCurrentGroupName("Build Camera Rig");
            int undoGroup = Undo.GetCurrentGroup();

            var stats = AssetDatabase.LoadAssetAtPath<PlayerStats>(StatsPath);
            if (stats == null) Debug.LogWarning($"[Camera] 找不到 {StatsPath}，CameraController 的 stats 要手動指定。");

            var cam = SetupMainCamera();

            var old = GameObject.Find(RigName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var rig = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rig, "Build Camera Rig");

            float yaw = player.transform.eulerAngles.y;
            var free = CreateCamera(rig, "CM FreeLook", player.transform, FreeLookRadius, StartPitch, yaw, cam, out var freeOrbit);
            free.Priority = FreeLookPriority;

            // 鎖定鏡頭看玩家與目標的中間；目標由 CameraController 在鎖定時加入
            var groupGo = new GameObject("LockOnGroup");
            groupGo.transform.SetParent(rig.transform, false);
            var group = groupGo.AddComponent<CinemachineTargetGroup>();
            group.AddMember(player.transform, 1f, 0.5f);

            var lockOn = CreateCamera(rig, "CM LockOn", player.transform, LockOnRadius, LockOnPitch, yaw, cam, out var lockOnOrbit);
            lockOn.Priority = FreeLookPriority - 1;
            lockOn.Target.CustomLookAtTarget = true;
            lockOn.Target.LookAtTarget = group.transform;
            lockOn.GetComponent<CinemachineRotationComposer>().TargetOffset = Vector3.zero;

            var controller = rig.AddComponent<CameraController>();
            var so = new SerializedObject(controller);
            so.FindProperty("stats").objectReferenceValue = stats;
            so.FindProperty("freeLookCamera").objectReferenceValue = free;
            so.FindProperty("freeLookOrbit").objectReferenceValue = freeOrbit;
            so.FindProperty("lockOnCamera").objectReferenceValue = lockOn;
            so.FindProperty("lockOnOrbit").objectReferenceValue = lockOnOrbit;
            so.FindProperty("lockOnGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 玩家的 Main Camera、鎖定清單（場景裡所有的 LockOnTarget）與 CameraController 由 spawner 交給生成的角色
            PlayerSpawnerBuilder.Wire();

            // 先把 Main Camera 擺到自由視角的位置，編輯模式下的 Game 視圖也看得到角色
            Undo.RecordObject(cam.transform, "Build Camera Rig");
            var rot = Quaternion.Euler(StartPitch, yaw, 0f);
            var pivot = player.transform.position + Vector3.up * PivotHeight;
            cam.transform.SetPositionAndRotation(pivot + rot * Vector3.back * FreeLookRadius, rot);

            // 鏡頭震動的 ImpulseListener 與玩家的打擊回饋（DaoDunDogBuilder 重建角色後也會走到這裡）
            CombatFeedbackBuilder.Wire();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(rig.scene);
            Debug.Log($"[Camera] 鏡頭已建立：{RigName}");
            return controller;
        }

        static Camera SetupMainCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            // 舊的 ThirdPersonCamera 已刪除，留在場景上會是 Missing Script
            Undo.RegisterCompleteObjectUndo(cam.gameObject, "Remove Missing Scripts");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cam.gameObject);

            var brain = cam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = Undo.AddComponent<CinemachineBrain>(cam.gameObject);
            Undo.RecordObject(brain, "Setup Brain");
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, BlendTime);
            EditorUtility.SetDirty(brain);
            return cam;
        }

        static CinemachineCamera CreateCamera(GameObject rig, string name, Transform follow, float radius, float pitch, float yaw,
                                              Camera lensFrom, out CinemachineOrbitalFollow orbit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig.transform, false);

            var cam = go.AddComponent<CinemachineCamera>();
            cam.Target.TrackingTarget = follow;
            var lens = LensSettings.FromCamera(lensFrom);
            lens.NearClipPlane = 0.1f; // 鏡頭會被牆推到很近
            cam.Lens = lens;

            orbit = go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = radius;
            orbit.TargetOffset = new Vector3(0f, PivotHeight, 0f);
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = Vector3.one * FollowDamping;
            orbit.HorizontalAxis.Value = yaw;
            orbit.HorizontalAxis.Center = yaw;
            orbit.VerticalAxis.Range = PitchRange;
            orbit.VerticalAxis.Value = pitch;
            orbit.VerticalAxis.Center = pitch;

            var composer = go.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0f, PivotHeight, 0f);
            composer.Damping = Vector2.zero;

            // 被外牆或觀眾席擋住時把鏡頭往前拉；從角色身上（不是 LookAt）檢查視線
            var deoccluder = go.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = LayerMask.GetMask("Default", "ArenaWall");
            deoccluder.IgnoreTag = "Player";
            deoccluder.MinimumDistanceFromTarget = 0.3f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.CameraRadius = 0.2f;
            deoccluder.AvoidObstacles.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            deoccluder.AvoidObstacles.UseFollowTarget.Enabled = true;
            deoccluder.AvoidObstacles.UseFollowTarget.YOffset = PivotHeight;
            deoccluder.AvoidObstacles.Damping = 0.2f;
            deoccluder.AvoidObstacles.DampingWhenOccluded = 0f;
            deoccluder.ShotQualityEvaluation.Enabled = false;

            return cam;
        }
    }
}
