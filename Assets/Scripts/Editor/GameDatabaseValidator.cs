using System.Collections.Generic;
using BossFight.Progression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BossFight.EditorTools
{
    /// <summary>
    /// 檢查 GameDatabase 的 id（企劃書「存檔」：存檔只存 id，所以 id 不能空、同一類不能重複）。
    /// Tools > Progression > Validate Game Database 手動檢查；打包前自動檢查，有錯就停止打包。
    /// 角色另外檢查 prefab 與 baseStats 有沒有接、裝備欄有沒有空格；裝備檢查欄位種類（T22）。新增種類時在 GameDatabase.Entries 加，這裡不用改。
    /// 技能樹（T23，每個角色的 skillTree）：節點 id 不能空、同一棵樹裡不能重複；前置要是同一棵樹的節點、不能是自己、不能成環；格子座標不能重複。
    /// Boss 與任務（T24）：Boss 要有場景名稱；獎勵要接好（裝備、角色要在資料庫裡）；任務的目標 Boss 要在資料庫裡，前置任務要在資料庫裡、不能是自己、不能成環。
    /// </summary>
    public class GameDatabaseValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        [MenuItem("Tools/Progression/Validate Game Database")]
        static void ValidateFromMenu()
        {
            var database = ProgressionBuilder.LoadDatabase();
            if (database == null)
            {
                Debug.LogError($"[GameDatabase] 找不到 {ProgressionBuilder.DatabasePath}（Tools > Progression > Build Game Database 會建立）。");
                return;
            }
            Validate(database, true);
            Selection.activeObject = database;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var database = ProgressionBuilder.LoadDatabase();
            if (database == null) throw new BuildFailedException($"[GameDatabase] 找不到 {ProgressionBuilder.DatabasePath}。");
            var errors = Validate(database, true);
            if (errors.Count > 0) throw new BuildFailedException($"[GameDatabase] 有 {errors.Count} 個錯誤，停止打包（Console 有清單）。");
        }

        /// <summary>回傳錯誤清單；log 為 true 時每個錯誤寫一行 Console（點一下會選到那個資產），沒錯誤時寫一行通過。</summary>
        public static List<string> Validate(GameDatabase database, bool log)
        {
            var errors = new List<string>();
            void Error(string text, Object context)
            {
                errors.Add(text);
                if (log) Debug.LogError($"[GameDatabase] {text}", context);
            }

            var seen = new Dictionary<(string kind, string id), Object>();
            int count = 0;
            foreach (var (kind, id, asset) in database.Entries())
            {
                count++;
                if (string.IsNullOrWhiteSpace(id))
                {
                    Error($"{kind}「{asset.name}」沒有 id。", asset);
                    continue;
                }
                if (seen.TryGetValue((kind, id), out var other)) Error($"{kind} id「{id}」重複：{other.name}、{asset.name}。", asset);
                else seen.Add((kind, id), asset);
            }

            if (database.characters == null) Error("沒有接角色名冊（characters）。", database);
            else
            {
                if (database.characters.Default == null) Error("角色名冊是空的。", database.characters);
                foreach (var character in database.characters.characters)
                {
                    if (character == null)
                    {
                        Error("角色名冊裡有空的欄位。", database.characters);
                        continue;
                    }
                    if (character.prefab == null) Error($"角色「{character.name}」沒有 prefab。", character);
                    if (character.baseStats == null) Error($"角色「{character.name}」沒有 baseStats。", character);
                    if (character.equipmentSlots != null && System.Array.IndexOf(character.equipmentSlots, null) >= 0)
                        Error($"角色「{character.name}」的裝備欄有空的種類。", character);
                    if (character.skillTree != null) count += ValidateSkillTree(character.skillTree, Error);
                }
            }

            if (database.equipment != null)
            {
                foreach (var item in database.equipment)
                {
                    if (item == null) Error("裝備清單裡有空的欄位。", database);
                    else if (item.slotType == null) Error($"裝備「{item.name}」沒有欄位種類。", item);
                }
            }

            if (database.bosses != null)
            {
                foreach (var boss in database.bosses)
                {
                    if (boss == null)
                    {
                        Error("Boss 清單裡有空的欄位。", database);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(boss.sceneName)) Error($"Boss「{boss.name}」沒有場景名稱。", boss);
                    ValidateRewards(database, boss.firstClearRewards, $"Boss「{boss.name}」的首通獎勵", boss, Error);
                }
            }

            if (database.quests != null)
            {
                foreach (var quest in database.quests)
                {
                    if (quest == null)
                    {
                        Error("任務清單裡有空的欄位。", database);
                        continue;
                    }
                    var objective = quest.objective;
                    if (objective == null || (objective.kind == QuestObjectiveKind.DefeatBoss && objective.boss == null))
                        Error($"任務「{quest.name}」的目標沒有接 Boss。", quest);
                    else if (objective.kind == QuestObjectiveKind.DefeatBoss && (database.bosses == null || !database.bosses.Contains(objective.boss)))
                        Error($"任務「{quest.name}」的目標 Boss「{objective.boss.name}」不在 GameDatabase.bosses 裡。", quest);
                    ValidateRewards(database, quest.rewards, $"任務「{quest.name}」的獎勵", quest, Error);
                    if (quest.prerequisites == null) continue;
                    foreach (var prerequisite in quest.prerequisites)
                    {
                        if (prerequisite == null) Error($"任務「{quest.name}」的前置有空的欄位。", quest);
                        else if (prerequisite == quest) Error($"任務「{quest.name}」把自己當前置。", quest);
                        else if (!database.quests.Contains(prerequisite)) Error($"任務「{quest.name}」的前置「{prerequisite.name}」不在 GameDatabase.quests 裡。", quest);
                    }
                }
                // 前置不成環：深度優先，走到「正在走」的任務就是環
                var state = new Dictionary<QuestData, int>(); // 1 = 正在走、2 = 走完
                bool Visit(QuestData quest)
                {
                    if (state.TryGetValue(quest, out int s)) return s == 1;
                    state[quest] = 1;
                    if (quest.prerequisites != null)
                    {
                        foreach (var prerequisite in quest.prerequisites)
                        {
                            if (prerequisite != null && prerequisite != quest && Visit(prerequisite)) return true;
                        }
                    }
                    state[quest] = 2;
                    return false;
                }
                foreach (var quest in database.quests)
                {
                    if (quest != null && !state.ContainsKey(quest) && Visit(quest))
                    {
                        Error($"任務的前置成環（經過「{quest.name}」）。", quest);
                        break;
                    }
                }
            }

            if (log && errors.Count == 0) Debug.Log($"[GameDatabase] 檢查通過：{count} 個 id。", database);
            return errors;
        }

        // 一組獎勵：種類要接好引用，裝備、角色要在資料庫裡（不然存檔的 id 換不回資產）
        static void ValidateRewards(GameDatabase database, List<RewardData> rewards, string owner, Object context, System.Action<string, Object> error)
        {
            if (rewards == null) return;
            foreach (var reward in rewards)
            {
                if (reward == null || !reward.IsValid)
                {
                    error($"{owner}有沒接好的項目（{(reward != null ? reward.kind.ToString() : "null")}）。", context);
                    continue;
                }
                if (reward.kind == RewardKind.Equipment && (database.equipment == null || !database.equipment.Contains(reward.equipment)))
                    error($"{owner}的裝備「{reward.equipment.name}」不在 GameDatabase.equipment 裡。", context);
                if (reward.kind == RewardKind.Character && (database.characters == null || !database.characters.characters.Contains(reward.character)))
                    error($"{owner}的角色「{reward.character.name}」不在角色名冊裡。", context);
            }
        }

        // 一棵技能樹：回傳檢查了幾個節點 id
        static int ValidateSkillTree(SkillTreeData tree, System.Action<string, Object> error)
        {
            if (tree.nodes == null) return 0;
            var byId = new Dictionary<string, SkillNode>();
            var cells = new Dictionary<Vector2Int, SkillNode>();
            int count = 0;
            foreach (var node in tree.nodes)
            {
                if (node == null) continue;
                count++;
                if (string.IsNullOrWhiteSpace(node.id)) error($"技能樹「{tree.name}」有節點（{node.displayName}）沒有 id。", tree);
                else if (byId.ContainsKey(node.id)) error($"技能樹「{tree.name}」的節點 id「{node.id}」重複。", tree);
                else byId.Add(node.id, node);
                if (cells.TryGetValue(node.gridPosition, out var other))
                    error($"技能樹「{tree.name}」的「{other.displayName}」與「{node.displayName}」在同一格 {node.gridPosition}。", tree);
                else cells.Add(node.gridPosition, node);
            }
            foreach (var node in tree.nodes)
            {
                if (node == null || node.prerequisites == null) continue;
                foreach (var id in node.prerequisites)
                {
                    if (id == node.id) error($"技能樹「{tree.name}」的「{node.id}」把自己當前置。", tree);
                    else if (string.IsNullOrEmpty(id) || !byId.ContainsKey(id)) error($"技能樹「{tree.name}」的「{node.id}」的前置「{id}」不在這棵樹裡。", tree);
                }
            }
            // 前置不成環：深度優先，走到「正在走」的節點就是環
            var state = new Dictionary<string, int>(); // 1 = 正在走、2 = 走完
            bool Visit(SkillNode node)
            {
                if (state.TryGetValue(node.id, out int s)) return s == 1;
                state[node.id] = 1;
                foreach (var id in node.prerequisites)
                {
                    if (id != node.id && byId.TryGetValue(id, out var prerequisite) && Visit(prerequisite)) return true;
                }
                state[node.id] = 2;
                return false;
            }
            foreach (var node in byId.Values)
            {
                if (!state.ContainsKey(node.id) && Visit(node))
                {
                    error($"技能樹「{tree.name}」的前置成環（經過「{node.id}」）。", tree);
                    break;
                }
            }
            return count;
        }
    }
}
