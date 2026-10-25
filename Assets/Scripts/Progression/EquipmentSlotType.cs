using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 裝備欄的種類（企劃書「裝備系統」）：先有「項圈」「護符」兩種，之後加種類只要新增資產，不用改程式。
    /// 角色有哪幾格寫在 CharacterDefinition.equipmentSlots，裝備屬於哪一種寫在 EquipmentData.slotType。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Equipment Slot Type", fileName = "Slot")]
    public class EquipmentSlotType : ScriptableObject
    {
        [Tooltip("顯示名稱（鐵匠鋪的欄位、「沒有○○欄」提示）")]
        public string displayName;
    }
}
