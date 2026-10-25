using BossFight.Core;
using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 一個可操作的角色（企劃書「角色系統」）：id、名稱、prefab、基礎數值。PlayerSpawner 依存檔的目前角色（id）從 CharacterRoster 找到這份，
    /// 生成 prefab，再由 PlayerLoadout 把 baseStats 複製一份、套上修正後交給角色（baseStats 本身不會被改）。
    /// 營火的角色清單（T21）：一開始就能用（unlockedByDefault）或存檔裡解鎖過的角色；developerOnly 的只在開發者模式開著時列出。
    /// 裝備欄（T22）：equipmentSlots 依序是這個角色的每一格（存檔照這個順序記穿戴，所以已有存檔後只往後加）。技能樹（T23）：skillTree，學過的節點存在 ProfileStore（每個角色分開記）。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Character Definition", fileName = "Character")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("識別")]
        [Tooltip("存檔用的 id：不能空、不能跟其他角色重複，存檔後不要改（Tools > Progression > Validate Game Database 檢查）")]
        public string id;
        [Tooltip("顯示名稱（營火的角色清單）")]
        public string displayName;
        [Tooltip("只有開發者模式開著時才出現在營火清單（測試角色）")]
        public bool developerOnly;
        [Tooltip("一開始就能用；沒勾的要在存檔裡解鎖過（ProfileStore.UnlockCharacter）才出現在營火清單")]
        public bool unlockedByDefault;

        [Header("角色")]
        [Tooltip("角色 prefab（根物件掛 PlayerRig）")]
        public PlayerRig prefab;
        [Tooltip("基礎數值；執行時複製一份再套用裝備與技能的修正，這份資產不會被改")]
        public PlayerStats baseStats;

        [Header("裝備")]
        [Tooltip("裝備欄：每一格的種類（刀盾狗：項圈、護符）。存檔依格子的順序記穿戴，已有存檔後只往後加")]
        public EquipmentSlotType[] equipmentSlots = new EquipmentSlotType[0];

        [Header("技能樹")]
        [Tooltip("這個角色的技能樹；留空時營火的技能樹頁顯示「沒有技能樹」")]
        public SkillTreeData skillTree;

        /// <summary>可以選這個角色了沒有：一開始就能用，或存檔裡解鎖過（不看 developerOnly）。</summary>
        public bool IsUnlocked => unlockedByDefault || ProfileStore.IsCharacterUnlocked(id);
    }
}
