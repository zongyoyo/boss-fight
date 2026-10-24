using System.Collections.Generic;
using UnityEngine;

namespace BossFight.Progression
{
    /// <summary>所有角色的名冊；第一個是預設角色（新存檔、存檔裡的角色找不到時）。GameDatabase 引用它。</summary>
    [CreateAssetMenu(menuName = "BossFight/Progression/Character Roster", fileName = "CharacterRoster")]
    public class CharacterRoster : ScriptableObject
    {
        [Tooltip("所有角色；第一個是預設角色")]
        public List<CharacterDefinition> characters = new List<CharacterDefinition>();

        /// <summary>預設角色（清單裡第一個不是空的）；清單是空的時 null。</summary>
        public CharacterDefinition Default
        {
            get
            {
                foreach (var character in characters)
                {
                    if (character != null) return character;
                }
                return null;
            }
        }

        /// <summary>依 id 找角色；找不到（或 id 是空的）時 null。</summary>
        public CharacterDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var character in characters)
            {
                if (character != null && character.id == id) return character;
            }
            return null;
        }
    }
}
