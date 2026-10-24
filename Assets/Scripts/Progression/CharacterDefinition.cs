using BossFight.Player;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 一個可操作的角色（企劃書「角色系統」）：id、名稱、prefab、基礎數值。PlayerSpawner 依存檔的目前角色（id）從 CharacterRoster 找到這份，
    /// 生成 prefab，再由 PlayerLoadout 把 baseStats 複製一份、套上修正後交給角色（baseStats 本身不會被改）。
    /// 裝備欄（T22）與技能樹（T23）之後加在這裡。
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

        [Header("角色")]
        [Tooltip("角色 prefab（根物件掛 PlayerRig）")]
        public PlayerRig prefab;
        [Tooltip("基礎數值；執行時複製一份再套用裝備與技能的修正，這份資產不會被改")]
        public PlayerStats baseStats;
    }
}
