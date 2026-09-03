using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Item;
using Durango.Logic.Skill;
using Durango.Utils;
using Messages;
using UnityEngine;
using Yaml;
using Yaml.Util;

namespace Baominix.DurangoOriginal.CombatSystem.Actions
{
    internal static class AvailableActionProvider
    {
        private const string BareHandsTag = "bare_hands";

        internal static CombatActionSnapshot Build()
        {
            HashSet<string> learnedActionIds = GetLearnedActionIds();
            bool skillDataReady = IsSkillDataReady();
            List<TagAllowAction> allowedSets = new List<TagAllowAction>();
            List<string> equipmentTags = new List<string>();
            bool equipmentDataReady;

            CollectEquipmentActionSets(
                allowedSets,
                equipmentTags,
                out equipmentDataReady);
            if (equipmentDataReady && allowedSets.Count == 0)
            {
                TagAllowAction bareHands =
                    SingletonDict<string, TagAllowAction>.Get(
                        BareHandsTag,
                        null);
                if (bareHands != null)
                {
                    allowedSets.Add(bareHands);
                    equipmentTags.Add(BareHandsTag);
                }
            }

            List<string> orderedIds = new List<string>();
            HashSet<string> uniqueIds =
                new HashSet<string>(StringComparer.Ordinal);

            int i;
            for (i = 0; i < allowedSets.Count; i++)
            {
                TagAllowAction allowed = allowedSets[i];
                AddActions(allowed.DefaultActions, null, orderedIds, uniqueIds);
                AddActions(
                    allowed.SkillActions,
                    learnedActionIds,
                    orderedIds,
                    uniqueIds);
            }

            List<ActionStatus> statuses = new List<ActionStatus>();
            HashSet<string> validatedIds =
                new HashSet<string>(StringComparer.Ordinal);
            for (i = 0; i < orderedIds.Count; i++)
            {
                string id = orderedIds[i];
                PlayerAction action =
                    SingletonDict<string, PlayerAction>.Get(id, null);
                if (action == null || action.Meta == null)
                {
                    DurangoCombatSystemPlugin.Log.LogWarning(
                        "Action data not found for allowed action: " + id);
                    continue;
                }

                ActionStatus status = default(ActionStatus);
                status.Id = id;
                status.Stamina = Mathf.RoundToInt(action.Meta.Stamina);
                status.Cooltime = action.Meta.Cooldown;
                statuses.Add(status);
                validatedIds.Add(id);
            }

            return new CombatActionSnapshot(
                statuses.ToArray(),
                validatedIds,
                equipmentDataReady
                    ? Join(equipmentTags)
                    : "loading",
                equipmentDataReady,
                skillDataReady);
        }

        private static void CollectEquipmentActionSets(
            List<TagAllowAction> target,
            List<string> sourceTags,
            out bool dataReady)
        {
            dataReady = false;
            if (!GameSystem<EquipSystem>.HasInstance() ||
                !GameSystem<InventorySystem>.HasInstance())
            {
                return;
            }

            EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
            InventorySystem inventorySystem =
                GameSystem<InventorySystem>.Instance();
            if (equipSystem == null || inventorySystem == null)
            {
                return;
            }

            EquipSystem.EquipPreset preset =
                equipSystem.GetEquipPreset(equipSystem.CurrentEquipPreset);
            if (preset == null || preset.SlotItems == null)
            {
                return;
            }

            dataReady = true;

            HashSet<string> seenTags =
                new HashSet<string>(StringComparer.Ordinal);
            foreach (string itemId in preset.SlotItems.Values)
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                ItemData item = inventorySystem.FindItem(itemId);
                if (item == null)
                {
                    // EquipPreset arrives before InventorySystem on initial map
                    // entry. This is a loading state, not an empty-hand loadout.
                    dataReady = false;
                    continue;
                }
                if (item.Tags == null)
                {
                    continue;
                }

                int i;
                for (i = 0; i < item.Tags.Count; i++)
                {
                    TagData tag = item.Tags[i];
                    if (tag == null || string.IsNullOrEmpty(tag.Id) ||
                        !seenTags.Add(tag.Id))
                    {
                        continue;
                    }

                    TagAllowAction allowed =
                        SingletonDict<string, TagAllowAction>.Get(
                            tag.Id,
                            null);
                    if (allowed != null)
                    {
                        target.Add(allowed);
                        sourceTags.Add(tag.Id);
                    }
                }
            }
        }

        private static HashSet<string> GetLearnedActionIds()
        {
            HashSet<string> result =
                new HashSet<string>(StringComparer.Ordinal);
            if (!GameSystem<SkillSystem>.HasInstance())
            {
                return result;
            }

            SkillSystem skillSystem = GameSystem<SkillSystem>.Instance();
            if (skillSystem == null || skillSystem.Skills == null)
            {
                return result;
            }

            int i;
            for (i = 0; i < skillSystem.Skills.Count; i++)
            {
                Bundle bundle = skillSystem.Skills[i];
                if (bundle == null)
                {
                    continue;
                }

                AddLearnedSkillActions(bundle.Base, result);
                if (bundle.Sub == null)
                {
                    continue;
                }
                int j;
                for (j = 0; j < bundle.Sub.Length; j++)
                {
                    AddLearnedSkillActions(bundle.Sub[j], result);
                }
            }
            return result;
        }

        private static void AddLearnedSkillActions(
            Durango.Logic.Skill.Skill skill,
            HashSet<string> target)
        {
            if (skill == null || skill.Level <= 0)
            {
                return;
            }

            int level;
            for (level = 1; level <= skill.Level; level++)
            {
                Node node = skill.Get(level);
                if (node == null || node.Rewards == null)
                {
                    continue;
                }

                int i;
                for (i = 0; i < node.Rewards.Length; i++)
                {
                    Durango.Logic.Skill.Reward reward = node.Rewards[i];
                    if (reward == null ||
                        reward.Type != Shared.Skill.RewardType.Action ||
                        reward.ActionIds == null)
                    {
                        continue;
                    }

                    int j;
                    for (j = 0; j < reward.ActionIds.Length; j++)
                    {
                        string id = reward.ActionIds[j];
                        if (!string.IsNullOrEmpty(id))
                        {
                            target.Add(id);
                        }
                    }
                }
            }
        }

        private static bool IsSkillDataReady()
        {
            if (!GameSystem<SkillSystem>.HasInstance())
            {
                return false;
            }

            SkillSystem skillSystem = GameSystem<SkillSystem>.Instance();
            if (skillSystem == null || skillSystem.Skills == null ||
                skillSystem.Skills.Count == 0)
            {
                return false;
            }

            // Original SkillSystem does not raise SkillListUpdated for the first
            // Skills response. Bundle.Valid is therefore the authoritative
            // initial-load boundary that the combat backend can poll.
            int i;
            for (i = 0; i < skillSystem.Skills.Count; i++)
            {
                Bundle bundle = skillSystem.Skills[i];
                if (bundle == null || !bundle.Valid)
                {
                    return false;
                }

                if (!IsLearnedSkillRewardDataReady(bundle.Base))
                {
                    return false;
                }

                if (bundle.Sub == null)
                {
                    continue;
                }

                int j;
                for (j = 0; j < bundle.Sub.Length; j++)
                {
                    if (!IsLearnedSkillRewardDataReady(bundle.Sub[j]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsLearnedSkillRewardDataReady(
            Durango.Logic.Skill.Skill skill)
        {
            if (skill == null || skill.Level <= 0)
            {
                return true;
            }

            int level;
            for (level = 1; level <= skill.Level; level++)
            {
                Node node = skill.Get(level);
                if (node == null || node.Rewards == null)
                {
                    // InitSkillRewards runs as a coroutine in Original PC Final.
                    // Do not publish a default-only action list while learned
                    // Action rewards are still being attached to the nodes.
                    return false;
                }
            }
            return true;
        }

        private static void AddActions(
            string[] candidates,
            HashSet<string> learnedFilter,
            List<string> ordered,
            HashSet<string> unique)
        {
            if (candidates == null)
            {
                return;
            }

            int i;
            for (i = 0; i < candidates.Length; i++)
            {
                string id = candidates[i];
                if (string.IsNullOrEmpty(id) ||
                    (learnedFilter != null && !learnedFilter.Contains(id)) ||
                    !unique.Add(id))
                {
                    continue;
                }
                ordered.Add(id);
            }
        }

        private static string Join(List<string> values)
        {
            return values == null || values.Count == 0
                ? "none"
                : string.Join(",", values.ToArray());
        }
    }
}
