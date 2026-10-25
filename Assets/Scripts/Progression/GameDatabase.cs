using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>
    /// 存檔 id 換回資產的總表（企劃書「存檔」）：ProfileStore 只存 id 字串，Progression 用這裡找回角色、裝備（T22；之後加 Boss、任務）。
    /// id 不能空、同一類不能重複：Tools > Progression > Validate Game Database 檢查，打包前也會自動檢查（有錯就停止打包）。
    /// </summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        [Tooltip("角色名冊")]
        public CharacterRoster characters;
        [Tooltip("所有裝備（鐵匠鋪照這個順序列出）")]
        public List<EquipmentData> equipment = new List<EquipmentData>();

        /// <summary>預設角色（名冊第一個）。</summary>
        public CharacterDefinition DefaultCharacter => characters != null ? characters.Default : null;

        /// <summary>依 id 找角色；找不到時 null。</summary>
        public CharacterDefinition FindCharacter(string id) => characters != null ? characters.Find(id) : null;

        /// <summary>依 id 找裝備；找不到（或 id 是空的）時 null。</summary>
        public EquipmentData FindEquipment(string id)
        {
            if (string.IsNullOrEmpty(id) || equipment == null) return null;
            foreach (var item in equipment)
            {
                if (item != null && item.id == id) return item;
            }
            return null;
        }

        /// <summary>所有要檢查 id 的資產：(類別名稱, id, 資產)。新增種類（裝備、Boss、任務）時在這裡加。</summary>
        public IEnumerable<(string kind, string id, Object asset)> Entries()
        {
            if (characters != null)
            {
                foreach (var character in characters.characters)
                {
                    if (character != null) yield return ("角色", character.id, character);
                }
            }
            if (equipment != null)
            {
                foreach (var item in equipment)
                {
                    if (item != null) yield return ("裝備", item.id, item);
                }
            }
        }
    }
}
