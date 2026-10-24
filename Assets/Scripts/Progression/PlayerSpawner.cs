using System;
using System.Collections.Generic;
using BossFight.Boss;
using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>場景裡的一個出生點：id（SceneFlowData 的 startArrival／portalArrival）與位置、朝向。</summary>
    [Serializable]
    public struct SceneSpawnPoint
    {
        [Tooltip("出生點 id")]
        public string id;
        [Tooltip("出生的位置與朝向（看 forward）")]
        public Transform point;
    }

    /// <summary>
    /// 生成玩家角色（T18）：場景裡不再放玩家，改放這個。Awake（最早執行）時：
    /// 1. 關掉底下的預覽模型（previewCharacter，EditorOnly：編輯模式下給 Timeline 綁軌道、預覽鏡頭用，正式 build 不會有）。
    /// 2. 看這次的出生點（ProfileStore.PeekArrival，換場景時 SceneFlow 設定）：spawnPoints 裡有同 id 的就生成在那裡，否則生成在自己的位置與朝向。
    /// 3. 生成 characterPrefab，把場景物件（Main Camera、鎖定與處決清單、AudioManager）交給 PlayerRig.SetSceneReferences，Boss 呼叫 SetTarget。
    /// 4. 對 receivers（Inspector 接好的 IPlayerBinding）呼叫 BindPlayer，最後送出 OnPlayerSpawned。
    /// 接收者的 Awake／OnEnable 比這裡晚，執行時已經拿到玩家；Inspector 接線不算編譯期依賴，所以可以把玩家交給 UI、Debug 這些上層元件。
    /// ReplaceCharacter 換角色（之後休息區的營火用）：在同一位置生成新角色、銷毀舊的，再對所有接收者 BindPlayer 一次。
    /// 這一版 characterPrefab 固定是刀盾狗；T19 起改從存檔的目前角色與 CharacterRoster 找。
    /// 接線與預覽模型由 PlayerSpawnerBuilder 建立（Builder 會自動收集場景裡的接收者與目標）。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class PlayerSpawner : MonoBehaviour
    {
        [Tooltip("要生成的角色 prefab")]
        [SerializeField] PlayerRig characterPrefab;
        [Tooltip("編輯模式的預覽模型（EditorOnly）；進 Play 時關掉")]
        [SerializeField] GameObject previewCharacter;
        [Tooltip("換場景進來時依 id 找出生點；沒有對應的（直接進 Play、重試）生成在這個物件的位置")]
        [SerializeField] SceneSpawnPoint[] spawnPoints = new SceneSpawnPoint[0];

        [Header("填進角色的場景物件")]
        [Tooltip("移動方向以這個鏡頭為準（Main Camera）")]
        [SerializeField] Transform cameraTransform;
        [Tooltip("可以被鎖定的目標（Boss、木樁）")]
        [SerializeField] List<LockOnTarget> lockTargets = new List<LockOnTarget>();
        [Tooltip("可以處決的目標（Boss）")]
        [SerializeField] List<ExecutionTarget> executionTargets = new List<ExecutionTarget>();
        [Tooltip("打擊回饋播音效的 AudioManager")]
        [SerializeField] AudioManager audioManager;
        [Tooltip("以玩家為目標的 Boss（SetTarget）")]
        [SerializeField] List<BossController> bosses = new List<BossController>();

        [Header("接收者")]
        [Tooltip("要拿到玩家的元件（必須實作 IPlayerBinding）")]
        [SerializeField] List<MonoBehaviour> receivers = new List<MonoBehaviour>();

        /// <summary>生成（或換）了角色、所有接收者都 BindPlayer 之後送出。</summary>
        public event Action<PlayerRig> OnPlayerSpawned;

        /// <summary>目前的角色。</summary>
        public PlayerRig Current { get; private set; }

        void Awake()
        {
            if (previewCharacter != null) previewCharacter.SetActive(false);
            if (characterPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] 沒有指定角色 prefab。", this);
                return;
            }
            GetSpawnPose(out Vector3 position, out Quaternion rotation);
            Spawn(characterPrefab, position, rotation);
        }

        /// <summary>換角色：在目前角色的位置與朝向生成 prefab，銷毀舊的，再對所有接收者 BindPlayer。</summary>
        public void ReplaceCharacter(PlayerRig prefab)
        {
            if (prefab == null) return;
            var old = Current;
            Vector3 position = old != null ? old.transform.position : transform.position;
            Quaternion rotation = old != null ? old.transform.rotation : transform.rotation;
            if (old != null)
            {
                old.gameObject.SetActive(false); // 這一幀剩下的時間不再動、不再被打到；Destroy 要等到幀末
                Destroy(old.gameObject);
            }
            Spawn(prefab, position, rotation);
        }

        void Spawn(PlayerRig prefab, Vector3 position, Quaternion rotation)
        {
            var rig = Instantiate(prefab, position, rotation);
            rig.name = prefab.name;
            rig.SetSceneReferences(cameraTransform, lockTargets, executionTargets, audioManager);
            foreach (var boss in bosses)
            {
                if (boss != null) boss.SetTarget(rig.transform);
            }
            Current = rig;
            foreach (var receiver in receivers)
            {
                if (receiver is IPlayerBinding binding) binding.BindPlayer(rig);
                else if (receiver != null) Debug.LogWarning($"[PlayerSpawner] {receiver.name} 的 {receiver.GetType().Name} 沒有實作 IPlayerBinding。", this);
            }
            OnPlayerSpawned?.Invoke(rig);
        }

        void GetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = Flatten(transform.forward, Quaternion.identity);
            if (!ProfileStore.PeekArrival(out string arrival) || string.IsNullOrEmpty(arrival)) return;
            foreach (var spawn in spawnPoints)
            {
                if (spawn.point == null || spawn.id != arrival) continue;
                position = spawn.point.position;
                rotation = Flatten(spawn.point.forward, rotation);
                return;
            }
            Debug.LogWarning($"[PlayerSpawner] 這個場景沒有出生點「{arrival}」，生成在預設位置。", this);
        }

        static Quaternion Flatten(Vector3 forward, Quaternion fallback)
        {
            forward = Vector3.ProjectOnPlane(forward, Vector3.up);
            return forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward) : fallback;
        }

        void OnValidate()
        {
            foreach (var receiver in receivers)
            {
                if (receiver != null && !(receiver is IPlayerBinding))
                    Debug.LogWarning($"[PlayerSpawner] 接收者 {receiver.name} 的 {receiver.GetType().Name} 沒有實作 IPlayerBinding。", this);
            }
        }
    }
}
