using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Combat;
using Durango.Logic.Item;
using Durango.Logic.Skill;
using Durango.Utils;
using Shared.Skill;
using Yaml;
using Yaml.Util;

namespace BaoX.DurangoOriginal.SkillCombatBridge
{
    internal static class SkillCombatActionBuilder
    {
        internal static bool IsDirty { get; private set; }

        internal static void MarkDirty()
        {
            IsDirty = true;
        }

        internal static bool TryRefresh()
        {
            if (!GameSystem<SkillSystem>.HasInstance() ||
                !GameSystem<CombatSystem>.HasInstance() ||
                !GameSystem<EquipSystem>.HasInstance() ||
                !GameSystem<InventorySystem>.HasInstance())
            {
                return false;
            }

            HashSet<string> learnedActions = CollectLearnedActionIds();
            HashSet<string> defaultActions = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> equipmentSkillActions = new HashSet<string>(StringComparer.Ordinal);
            CollectEquipmentActions(defaultActions, equipmentSkillActions);

            if (defaultActions.Count == 0)
            {
                AddTagActions("bare_hands", defaultActions, equipmentSkillActions);
            }

            List<BattleAction> actions = new List<BattleAction>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
            AddBattleActions(defaultActions, added, actions);

            foreach (string actionId in equipmentSkillActions)
            {
                if (learnedActions.Contains(actionId))
                {
                    AddBattleAction(actionId, added, actions);
                }
            }

            GameSystem<CombatSystem>.Instance().SetCurrentBattleActions(actions);
            IsDirty = false;

            if (SkillCombatBridgePlugin.Log != null)
            {
                SkillCombatBridgePlugin.Log.LogInfo(
                    "Combat actions refreshed: active=" + actions.Count +
                    " learned=" + learnedActions.Count +
                    " equipmentSkills=" + equipmentSkillActions.Count);
            }
            return true;
        }

        private static HashSet<string> CollectLearnedActionIds()
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            List<Bundle> bundles = GameSystem<SkillSystem>.Instance().Skills;
            for (int i = 0; i < bundles.Count; i++)
            {
                Bundle bundle = bundles[i];
                CollectSkillActions(bundle.Base, result);
                if (bundle.Sub == null)
                {
                    continue;
                }
                for (int j = 0; j < bundle.Sub.Length; j++)
                {
                    CollectSkillActions(bundle.Sub[j], result);
                }
            }
            return result;
        }

        private static void CollectSkillActions(Durango.Logic.Skill.Skill skill, HashSet<string> result)
        {
            if (skill == null || skill.Level <= 0)
            {
                return;
            }

            for (int level = 1; level <= skill.Level; level++)
            {
                Node node = skill.Get(level);
                if (node == null || node.Rewards == null)
                {
                    continue;
                }

                for (int i = 0; i < node.Rewards.Length; i++)
                {
                    Durango.Logic.Skill.Reward reward = node.Rewards[i];
                    if (reward == null || reward.Type != RewardType.Action || reward.ActionIds == null)
                    {
                        continue;
                    }
                    for (int j = 0; j < reward.ActionIds.Length; j++)
                    {
                        if (!string.IsNullOrEmpty(reward.ActionIds[j]))
                        {
                            result.Add(reward.ActionIds[j]);
                        }
                    }
                }
            }
        }

        private static void CollectEquipmentActions(HashSet<string> defaults, HashSet<string> skills)
        {
            EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
            EquipSystem.EquipPreset preset = equipSystem.GetEquipPreset(equipSystem.CurrentEquipPreset);
            if (preset == null || preset.SlotItems == null)
            {
                return;
            }

            foreach (string itemId in preset.SlotItems.Values)
            {
                ItemData item = GameSystem<InventorySystem>.Instance().FindItem(itemId);
                if (item == null || item.Tags == null)
                {
                    continue;
                }

                foreach (TagData tag in item.Tags)
                {
                    if (tag != null && !string.IsNullOrEmpty(tag.Id))
                    {
                        AddTagActions(tag.Id, defaults, skills);
                    }
                }
            }
        }

        private static void AddTagActions(string tagId, HashSet<string> defaults, HashSet<string> skills)
        {
            TagAllowAction allowed = SingletonDict<string, TagAllowAction>.Get(tagId, null);
            if (allowed == null)
            {
                return;
            }

            AddIds(allowed.DefaultActions, defaults);
            AddIds(allowed.SkillActions, skills);
        }

        private static void AddIds(string[] ids, HashSet<string> target)
        {
            if (ids == null)
            {
                return;
            }
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                {
                    target.Add(ids[i]);
                }
            }
        }

        private static void AddBattleActions(IEnumerable<string> ids, HashSet<string> added, List<BattleAction> actions)
        {
            foreach (string id in ids)
            {
                AddBattleAction(id, added, actions);
            }
        }

        private static void AddBattleAction(string id, HashSet<string> added, List<BattleAction> actions)
        {
            if (!added.Add(id))
            {
                return;
            }

            PlayerAction data = SingletonDict<string, PlayerAction>.Get(id, null);
            if (data != null)
            {
                actions.Add(new BattleAction(data));
            }
        }
    }
}
