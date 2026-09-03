using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Logic.Combat;
using Durango.Logic.Item;
using Durango.Logic.Skill;
using Messages;
using Shared.Ability;
using Shared.Skill;
using Yaml;
using Yaml.Util;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal static class CombatStatCommand
    {
        private static readonly string[] TrackedModifiers = new string[]
        {
            "damage_bonus",
            "critical_rate_plus",
            "critical_damage_bonus",
            "armor_penetration_plus",
            "hit_rate_plus",
            "accuracy_plus",
            "blow_power_plus",
            "attack_ratio_impact",
            "impact_plus",
            "impact_bonus",
            "impact_rate_plus",
            "impact_power_plus",
            "impact_damage_bonus",
            "impact_damage_plus",
            "mutilate_plus",
            "groggy_plus"
        };

        private sealed class CombatModifierContext
        {
            internal readonly HashSet<string> ActionIds = new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> Tags = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class ActionModifierBreakdown
        {
            internal readonly Dictionary<string, float> Totals = NewModifierMap();
            internal readonly List<string> Sources = new List<string>();
        }

        internal static void Execute(string[] args)
        {
            if (!GameSystem<StatisticsSystem>.HasInstance() || GameSystem<StatisticsSystem>.Instance().Statistics == null)
            {
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("stats_not_ready"));
                return;
            }

            Statistics statistics = GameSystem<StatisticsSystem>.Instance().Statistics.Value;
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("combat_stat"));
            ChatCommandRegistry.Reply("Lv." + statistics.Level + " XP " + statistics.Exp);
            ChatCommandRegistry.Reply("STR " + GetBasic(statistics, Basic.Strength)
                + " AGI " + GetBasic(statistics, Basic.Agility)
                + " DEX " + GetBasic(statistics, Basic.Dexterity));
            ChatCommandRegistry.Reply(
                ChatCommandLocalization.Get("attack") + " " +
                Number(GetDerived(statistics, Derived.Attack)) + " " +
                ChatCommandLocalization.Get("accuracy") + " " +
                Number(GetDerived(statistics, Derived.Accuracy)) + " " +
                ChatCommandLocalization.Get("evasion") + " " +
                Number(GetDerived(statistics, Derived.Dodge)));
            ChatCommandRegistry.Reply(
                ChatCommandLocalization.Get("crit_rate") + " " +
                Number(GetDerived(statistics, Derived.Critical)) + " " +
                ChatCommandLocalization.Get("attack_rating") + " " +
                Number(GetDerived(statistics, Derived.AttackRating)) + " " +
                ChatCommandLocalization.Get("defense") + " " +
                Number(GetDerived(statistics, Derived.Defense)));
            CombatModifierContext combatContext = CollectCombatModifierContext();
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("active_actions") + ": " + FormatActionSummary(combatContext.ActionIds));
            ReplyMeleeSkillGroupSummary(combatContext);
            ReplyRangedSkillGroupSummary(combatContext);
            ReplyActionModifierBreakdown(combatContext);
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("weapon") + ": " + WeaponText());
        }

        private static int GetBasic(Statistics statistics, Basic key)
        {
            int value;
            return statistics.BasicAbilities != null && statistics.BasicAbilities.TryGetValue(key, out value) ? value : 0;
        }

        private static float GetDerived(Statistics statistics, Derived key)
        {
            float value;
            return statistics.DerivedsAbilities != null && statistics.DerivedsAbilities.TryGetValue(key, out value) ? value : 0f;
        }

        private static float Modifier(Statistics statistics, string key)
        {
            float value;
            return statistics.Modifiers != null && statistics.Modifiers.TryGetValue(key, out value) ? value : 0f;
        }

        private static float Modifier(Dictionary<string, float> modifiers, string key)
        {
            float value;
            return modifiers != null && modifiers.TryGetValue(key, out value) ? value : 0f;
        }

        private static void AddNodeRewards(Dictionary<string, float> result, Node node)
        {
            for (int i = 0; i < node.Rewards.Length; i++)
            {
                Durango.Logic.Skill.Reward reward = node.Rewards[i];
                if (reward == null)
                {
                    continue;
                }

                if (reward.Modifiers != null)
                {
                    foreach (KeyValuePair<string, float> modifier in reward.Modifiers)
                    {
                        AddTrackedModifier(result, modifier.Key, modifier.Value);
                    }
                }

                if (!string.IsNullOrEmpty(reward.Modifier))
                {
                    AddTrackedModifier(result, reward.Modifier, reward.Value);
                }
            }
        }

        private static Dictionary<string, float> CollectActiveModifiers(CombatModifierContext combatContext)
        {
            Dictionary<string, float> result = NewModifierMap();
            if (!GameSystem<SkillSystem>.HasInstance())
            {
                return result;
            }

            List<Bundle> bundles = GameSystem<SkillSystem>.Instance().Skills;
            for (int i = 0; i < bundles.Count; i++)
            {
                Bundle bundle = bundles[i];
                CollectActiveModifiers(bundle.Base, combatContext, result);
                if (bundle.Sub == null)
                {
                    continue;
                }
                for (int j = 0; j < bundle.Sub.Length; j++)
                {
                    CollectActiveModifiers(bundle.Sub[j], combatContext, result);
                }
            }
            return result;
        }

        private static void CollectActiveModifiers(Durango.Logic.Skill.Skill skill, CombatModifierContext combatContext, Dictionary<string, float> result)
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
                    if (reward == null || !RewardMatchesCombatContext(node, reward, combatContext))
                    {
                        continue;
                    }

                    if (reward.Modifiers != null)
                    {
                        foreach (KeyValuePair<string, float> modifier in reward.Modifiers)
                        {
                            AddTrackedModifier(result, modifier.Key, modifier.Value);
                        }
                    }

                    if (!string.IsNullOrEmpty(reward.Modifier))
                    {
                        AddTrackedModifier(result, reward.Modifier, reward.Value);
                    }
                }
            }
        }

        private static void ReplyActionModifierBreakdown(CombatModifierContext combatContext)
        {
            if (combatContext == null || combatContext.ActionIds.Count == 0 || !GameSystem<SkillSystem>.HasInstance())
            {
                return;
            }

            Dictionary<string, ActionModifierBreakdown> breakdowns = new Dictionary<string, ActionModifierBreakdown>(StringComparer.Ordinal);
            foreach (string actionId in combatContext.ActionIds)
            {
                breakdowns[actionId] = new ActionModifierBreakdown();
            }

            List<Bundle> bundles = GameSystem<SkillSystem>.Instance().Skills;
            for (int i = 0; i < bundles.Count; i++)
            {
                Bundle bundle = bundles[i];
                CollectActionBreakdown(bundle.Base, breakdowns);
                if (bundle.Sub == null)
                {
                    continue;
                }
                for (int j = 0; j < bundle.Sub.Length; j++)
                {
                    CollectActionBreakdown(bundle.Sub[j], breakdowns);
                }
            }

            foreach (KeyValuePair<string, ActionModifierBreakdown> pair in breakdowns)
            {
                if (!HasAnyModifier(pair.Value.Totals))
                {
                    continue;
                }

                string displayActionId = ActionDisplayId(pair.Key);
                if (string.IsNullOrEmpty(displayActionId))
                {
                    continue;
                }

                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("action") + " " + displayActionId + ": " + FormatCompactModifiers(pair.Value.Totals));
            }
        }

        private static void ReplyMeleeSkillGroupSummary(CombatModifierContext combatContext)
        {
            if (!IsMeleeContext(combatContext))
            {
                return;
            }

            Dictionary<string, float> skill = CollectSkillIdModifiers("melee_weapon_mastery");
            if (!IsBarehandContext(combatContext) && HasAnyModifier(skill))
            {
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("melee_enhanced") + " " + ChatCommandLocalization.DisplayWeaponType(WeaponTypeText(combatContext)) + ": " + FormatCompactModifiers(skill));
            }

            string weaponSkillId = MeleeWeaponTypeSkillId(combatContext);
            if (!string.IsNullOrEmpty(weaponSkillId))
            {
                string weaponTypeText = WeaponTypeText(combatContext);
                Dictionary<string, float> weaponType = CollectSkillIdModifiers(weaponSkillId);
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("melee_type") + " " + ChatCommandLocalization.DisplayWeaponType(weaponTypeText) + ": " + FormatMeleeTypeModifiers(weaponTypeText, weaponType));
            }
        }

        private static void ReplyRangedSkillGroupSummary(CombatModifierContext combatContext)
        {
            string rangedTypeText = RangedWeaponTypeText(combatContext);
            if (rangedTypeText == "Unknown")
            {
                return;
            }

            Dictionary<string, float> enhanced = CollectSkillIdModifiers("ranged_weapon_proficiency");
            AddSkillIdModifiers(enhanced, "ranged_weapon_mastery");
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("ranged_enhanced") + " " + ChatCommandLocalization.DisplayWeaponType(rangedTypeText) + ": " + FormatRangedEnhancedModifiers(enhanced));

            string weaponSkillId = RangedWeaponTypeSkillId(combatContext);
            if (!string.IsNullOrEmpty(weaponSkillId))
            {
                Dictionary<string, float> weaponType = CollectSkillIdModifiers(weaponSkillId);
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("ranged_type") + " " + ChatCommandLocalization.DisplayWeaponType(rangedTypeText) + ": " + FormatRangedTypeModifiers(rangedTypeText, weaponType));
            }
        }

        private static Dictionary<string, float> CollectSkillIdModifiers(string skillId)
        {
            Dictionary<string, float> result = NewModifierMap();
            if (string.IsNullOrEmpty(skillId) || !GameSystem<SkillSystem>.HasInstance())
            {
                return result;
            }

            List<Bundle> bundles = GameSystem<SkillSystem>.Instance().Skills;
            for (int i = 0; i < bundles.Count; i++)
            {
                Bundle bundle = bundles[i];
                CollectSkillIdModifiers(bundle.Base, skillId, result);
                if (bundle.Sub == null)
                {
                    continue;
                }
                for (int j = 0; j < bundle.Sub.Length; j++)
                {
                    CollectSkillIdModifiers(bundle.Sub[j], skillId, result);
                }
            }
            return result;
        }

        private static void AddSkillIdModifiers(Dictionary<string, float> target, string skillId)
        {
            Dictionary<string, float> source = CollectSkillIdModifiers(skillId);
            foreach (KeyValuePair<string, float> pair in source)
            {
                AddModifier(target, pair.Key, pair.Value);
            }
        }

        private static void CollectSkillIdModifiers(Durango.Logic.Skill.Skill skill, string skillId, Dictionary<string, float> result)
        {
            if (skill == null || skill.Level <= 0)
            {
                return;
            }

            for (int level = 1; level <= skill.Level; level++)
            {
                Node node = skill.Get(level);
                if (node == null || node.Rewards == null || !string.Equals(node.Id, skillId, StringComparison.Ordinal))
                {
                    continue;
                }

                AddNodeRewards(result, node);
            }
        }

        private static string MeleeWeaponTypeSkillId(CombatModifierContext context)
        {
            string type = DetectMeleeWeaponType(context);
            if (type == "Sword") return "sword_mastery";
            if (type == "Axe") return "axe_mastery";
            if (type == "Blunt") return "blunt_mastery";
            if (type == "Lance") return "lance_mastery";
            return string.Empty;
        }

        private static string RangedWeaponTypeSkillId(CombatModifierContext context)
        {
            string type = DetectRangedWeaponType(context);
            if (type == "Bow") return "bow_mastery";
            if (type == "Crossbow") return "crossbow_mastery";
            return string.Empty;
        }

        private static void CollectActionBreakdown(Durango.Logic.Skill.Skill skill, Dictionary<string, ActionModifierBreakdown> breakdowns)
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
                    if (reward == null || reward.ActionIds == null || reward.ActionIds.Length == 0)
                    {
                        continue;
                    }

                    foreach (KeyValuePair<string, ActionModifierBreakdown> pair in breakdowns)
                    {
                        if (!SkillMatchesActionGroup(node, pair.Key))
                        {
                            continue;
                        }

                        if (RewardActionIdsMatch(reward, pair.Key))
                        {
                            AddRewardToActionBreakdown(node, reward, pair.Value);
                        }
                    }
                }
            }
        }

        private static bool RewardActionIdsMatch(Durango.Logic.Skill.Reward reward, string activeActionId)
        {
            if (reward == null || reward.ActionIds == null || reward.ActionIds.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < reward.ActionIds.Length; i++)
            {
                string rewardActionId = reward.ActionIds[i];
                if (!string.IsNullOrEmpty(rewardActionId) && ActionIdsMatch(rewardActionId, activeActionId))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddRewardToActionBreakdown(Node node, Durango.Logic.Skill.Reward reward, ActionModifierBreakdown breakdown)
        {
            Dictionary<string, float> added = NewModifierMap();
            if (reward.Modifiers != null)
            {
                foreach (KeyValuePair<string, float> modifier in reward.Modifiers)
                {
                    AddActionDisplayModifier(breakdown.Totals, modifier.Key, modifier.Value);
                    AddActionDisplayModifier(added, modifier.Key, modifier.Value);
                }
            }

            if (!string.IsNullOrEmpty(reward.Modifier))
            {
                AddActionDisplayModifier(breakdown.Totals, reward.Modifier, reward.Value);
                AddActionDisplayModifier(added, reward.Modifier, reward.Value);
            }

            if (HasAnyModifier(added))
            {
                breakdown.Sources.Add(ReadableNodeName(node) + " " + FormatCompactModifiers(added));
            }
        }

        private static bool SkillMatchesActionGroup(Node node, string actionId)
        {
            string nodeKey = Normalize(node == null ? string.Empty : node.Id);
            string actionKey = Normalize(actionId);
            if (nodeKey == "meleeweaponproficiency")
            {
                return IsDefaultComboAction(actionId);
            }
            if (nodeKey == "rangedweaponproficiency")
            {
                return IsRangedDefaultAction(actionId);
            }
            if (IsMeleeWeaponTypePassiveNode(nodeKey))
            {
                return false;
            }
            if (IsRangedPassiveNode(nodeKey))
            {
                return false;
            }

            if (ContainsAny(actionKey, "rangedbow", "rangedcrossbow", "quickshot", "aimedshot"))
            {
                if (ContainsAny(actionKey, "quickshot"))
                {
                    return nodeKey == "quickshot";
                }
                if (ContainsAny(actionKey, "aimedshot"))
                {
                    return nodeKey == "aimedshot";
                }
                return false;
            }

            if (ContainsAny(actionKey, "lance", "spear"))
            {
                return ContainsAny(nodeKey, "lance", "spear");
            }

            if (actionKey.StartsWith("onehand", StringComparison.Ordinal))
            {
                return nodeKey.StartsWith("onehanded", StringComparison.Ordinal) || nodeKey.StartsWith("onehand", StringComparison.Ordinal);
            }

            if (actionKey.StartsWith("twohand", StringComparison.Ordinal))
            {
                return nodeKey.StartsWith("twohanded", StringComparison.Ordinal) || nodeKey.StartsWith("twohand", StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsMeleeWeaponTypePassiveNode(string nodeKey)
        {
            return nodeKey == "swordmastery"
                || nodeKey == "axemastery"
                || nodeKey == "bluntmastery"
                || nodeKey == "lancemastery";
        }

        private static bool IsRangedPassiveNode(string nodeKey)
        {
            return nodeKey == "rangedweaponproficiency"
                || nodeKey == "rangedweaponmastery"
                || nodeKey == "bowmastery"
                || nodeKey == "crossbowmastery";
        }

        private static bool ActionIdsMatch(string rewardActionId, string activeActionId)
        {
            if (string.Equals(rewardActionId, activeActionId, StringComparison.Ordinal))
            {
                return true;
            }

            string rewardKey = NormalizeActionFamily(rewardActionId);
            string activeKey = NormalizeActionFamily(activeActionId);
            return rewardKey.Length > 0 && string.Equals(rewardKey, activeKey, StringComparison.Ordinal);
        }

        private static string ActionDisplayId(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return string.Empty;
            }

            if (IsDefaultComboAction(actionId))
            {
                if (!actionId.EndsWith("_a", StringComparison.Ordinal))
                {
                    return string.Empty;
                }
                return actionId.Substring(0, actionId.Length - 2);
            }

            return actionId;
        }

        private static bool IsDefaultComboAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return false;
            }

            return actionId.IndexOf("_default", StringComparison.Ordinal) >= 0
                && (actionId.EndsWith("_a", StringComparison.Ordinal)
                    || actionId.EndsWith("_b", StringComparison.Ordinal)
                    || actionId.EndsWith("_c", StringComparison.Ordinal));
        }

        private static bool IsRangedDefaultAction(string actionId)
        {
            string key = Normalize(actionId);
            return key == "rangedcrossbowdefault" || key.StartsWith("rangedbowdefault", StringComparison.Ordinal);
        }

        private static string NormalizeActionFamily(string actionId)
        {
            string key = Normalize(actionId);
            key = key.Replace("charge", "dash");
            key = key.Replace("crush", "smash");
            key = key.Replace("cleave", "sweeping");
            if (key.StartsWith("twohandlance", StringComparison.Ordinal))
            {
                key = key.Substring("twohand".Length);
            }

            if (key.EndsWith("axe", StringComparison.Ordinal))
            {
                return key.Substring(0, key.Length - 3);
            }
            if (key.EndsWith("blunt", StringComparison.Ordinal))
            {
                return key.Substring(0, key.Length - 5);
            }
            if (key.EndsWith("sword", StringComparison.Ordinal))
            {
                return key.Substring(0, key.Length - 5);
            }
            return key;
        }

        private static void AddActionDisplayModifier(Dictionary<string, float> values, string key, float value)
        {
            if (IsImpactModifier(key))
            {
                AddModifier(values, "attack_ratio_impact", value);
                return;
            }

            if (!IsTrackedModifier(key))
            {
                return;
            }

            AddTrackedModifier(values, key, value);
        }

        private static bool IsImpactModifier(string key)
        {
            return ContainsAny(Normalize(key), "impact", "blowpower");
        }

        private static bool HasAnyModifier(Dictionary<string, float> values)
        {
            if (values == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, float> pair in values)
            {
                if (Math.Abs(pair.Value) > 0.0001f)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ReadableNodeName(Node node)
        {
            if (node == null)
            {
                return ChatCommandLocalization.Get("unknown");
            }

            if (!string.IsNullOrEmpty(node.Name))
            {
                return node.Name;
            }

            return node.Id + "/" + node.Sub + " Lv." + node.Level;
        }

        private static string FormatCompactModifiers(Dictionary<string, float> modifiers)
        {
            string text = string.Empty;
            AppendModifierText(ref text, "dmg", Modifier(modifiers, "damage_bonus"));
            AppendModifierText(ref text, "def_pen", Modifier(modifiers, "armor_penetration_plus"));
            AppendModifierText(ref text, "cri_rate", Modifier(modifiers, "critical_rate_plus"));
            AppendModifierText(ref text, "cri_dmg", Modifier(modifiers, "critical_damage_bonus"));
            AppendModifierText(ref text, "hit_rate", Modifier(modifiers, "hit_rate_plus") + Modifier(modifiers, "accuracy_plus"));
            AppendFlatModifierText(ref text, "impact", ImpactModifier(modifiers));
            AppendModifierText(ref text, "mutilate", Modifier(modifiers, "mutilate_plus"));
            AppendModifierText(ref text, "groggy", Modifier(modifiers, "groggy_plus"));
            return text.Length == 0 ? "0" : text;
        }

        private static string FormatMeleeTypeModifiers(string weaponType, Dictionary<string, float> modifiers)
        {
            if (weaponType == "Sword")
            {
                return "cri_rate+" + Percent(Modifier(modifiers, "critical_rate_plus"))
                    + " def_pen+" + Percent(Modifier(modifiers, "armor_penetration_plus"));
            }
            if (weaponType == "Axe")
            {
                return "cri_dmg+" + Percent(Modifier(modifiers, "critical_damage_bonus"))
                    + " mutilate+" + Percent(Modifier(modifiers, "mutilate_plus"));
            }
            if (weaponType == "Blunt")
            {
                return "hit_rate+" + Percent(Modifier(modifiers, "hit_rate_plus") + Modifier(modifiers, "accuracy_plus"))
                    + " groggy+" + Percent(Modifier(modifiers, "groggy_plus"));
            }
            if (weaponType == "Lance")
            {
                return "def_pen+" + Percent(Modifier(modifiers, "armor_penetration_plus"))
                    + " cri_dmg+" + Percent(Modifier(modifiers, "critical_damage_bonus"));
            }
            return FormatCompactModifiers(modifiers);
        }

        private static string FormatRangedEnhancedModifiers(Dictionary<string, float> modifiers)
        {
            return "cri_dmg+" + Percent(Modifier(modifiers, "critical_damage_bonus"));
        }

        private static string FormatRangedTypeModifiers(string weaponType, Dictionary<string, float> modifiers)
        {
            if (weaponType == "Bow")
            {
                return "hit_rate+" + Percent(Modifier(modifiers, "hit_rate_plus") + Modifier(modifiers, "accuracy_plus"))
                    + " def_pen+" + Percent(Modifier(modifiers, "armor_penetration_plus"));
            }
            if (weaponType == "Crossbow")
            {
                return "cri_rate+" + Percent(Modifier(modifiers, "critical_rate_plus"))
                    + " groggy+" + Percent(Modifier(modifiers, "groggy_plus"));
            }
            return FormatCompactModifiers(modifiers);
        }

        private static float ImpactModifier(Dictionary<string, float> modifiers)
        {
            return Modifier(modifiers, "attack_ratio_impact")
                + Modifier(modifiers, "blow_power_plus")
                + Modifier(modifiers, "impact_plus")
                + Modifier(modifiers, "impact_bonus")
                + Modifier(modifiers, "impact_rate_plus")
                + Modifier(modifiers, "impact_power_plus")
                + Modifier(modifiers, "impact_damage_bonus")
                + Modifier(modifiers, "impact_damage_plus");
        }

        private static float ImpactModifier(Statistics statistics)
        {
            return Modifier(statistics, "attack_ratio_impact")
                + Modifier(statistics, "blow_power_plus")
                + Modifier(statistics, "impact_plus")
                + Modifier(statistics, "impact_bonus")
                + Modifier(statistics, "impact_rate_plus")
                + Modifier(statistics, "impact_power_plus")
                + Modifier(statistics, "impact_damage_bonus")
                + Modifier(statistics, "impact_damage_plus");
        }

        private static void AppendModifierText(ref string text, string label, float value)
        {
            if (Math.Abs(value) <= 0.0001f)
            {
                return;
            }

            if (text.Length > 0)
            {
                text += " ";
            }
            text += label + "+" + Percent(value);
        }

        private static void AppendFlatModifierText(ref string text, string label, float value)
        {
            if (Math.Abs(value) <= 0.0001f)
            {
                return;
            }

            if (text.Length > 0)
            {
                text += " ";
            }
            text += label + "+" + Number(value);
        }

        private static bool RewardMatchesCombatContext(Node node, Durango.Logic.Skill.Reward reward, CombatModifierContext context)
        {
            if (reward == null)
            {
                return true;
            }

            if (reward.ActionIds != null && reward.ActionIds.Length > 0)
            {
                return RewardMatchesActiveActions(reward, context);
            }

            if (reward.Type == RewardType.ActionEnhancement)
            {
                return false;
            }

            if (reward.Type == RewardType.WeaponEnhancement)
            {
                return RewardMatchesWeaponContext(node, reward, context);
            }

            return true;
        }

        private static bool RewardMatchesActiveActions(Durango.Logic.Skill.Reward reward, CombatModifierContext context)
        {
            if (reward.ActionIds == null || reward.ActionIds.Length == 0)
            {
                return true;
            }

            if (context == null || context.ActionIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < reward.ActionIds.Length; i++)
            {
                if (string.IsNullOrEmpty(reward.ActionIds[i]))
                {
                    continue;
                }

                foreach (string activeActionId in context.ActionIds)
                {
                    if (ActionIdsMatch(reward.ActionIds[i], activeActionId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool RewardMatchesWeaponContext(Node node, Durango.Logic.Skill.Reward reward, CombatModifierContext context)
        {
            string key = Normalize((node == null ? string.Empty : node.Id) + " "
                + (node == null ? string.Empty : node.Sub) + " "
                + (reward == null ? string.Empty : reward.Id) + " "
                + (reward == null ? string.Empty : reward.Category) + " "
                + (reward == null ? string.Empty : reward.Name));

            if (ContainsAny(key, "crossbow")) return ContextHasAny(context, "crossbow");
            if (ContainsAny(key, "bow")) return ContextHasAny(context, "bow");
            string meleeType = DetectMeleeWeaponType(context);
            if (ContainsAny(key, "blunt", "hammer", "mace")) return meleeType == "Blunt";
            if (ContainsAny(key, "axe")) return meleeType == "Axe";
            if (ContainsAny(key, "sword")) return meleeType == "Sword";
            if (ContainsAny(key, "lance", "spear")) return meleeType == "Lance";
            if (ContainsAny(key, "barehand", "barehands", "bare")) return ContextHasAny(context, "barehand", "barehands", "bare");
            return true;
        }

        private static string WeaponTypeText(CombatModifierContext context)
        {
            string type = DetectMeleeWeaponType(context);
            if (!string.IsNullOrEmpty(type)) return type;
            return "Unknown";
        }

        private static string RangedWeaponTypeText(CombatModifierContext context)
        {
            string type = DetectRangedWeaponType(context);
            if (!string.IsNullOrEmpty(type)) return type;
            return "Unknown";
        }

        private static string DetectMeleeWeaponType(CombatModifierContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            // Action ids are more reliable than broad item tags. Check specific types first.
            if (ContextActionHasAny(context, "lance", "spear")) return "Lance";
            if (ContextActionHasAny(context, "axe")) return "Axe";
            if (ContextActionHasAny(context, "blunt", "hammer", "mace")) return "Blunt";

            if (ContextTagHasAny(context, "lance", "spear")) return "Lance";
            if (ContextTagHasAny(context, "axe")) return "Axe";
            if (ContextTagHasAny(context, "blunt", "hammer", "mace")) return "Blunt";
            if (ContextTagHasAny(context, "sword")) return "Sword";

            if (ContextActionLooksLikeSword(context)) return "Sword";
            return string.Empty;
        }

        private static string DetectRangedWeaponType(CombatModifierContext context)
        {
            if (context == null)
            {
                return string.Empty;
            }

            if (ContextActionHasAny(context, "crossbow")) return "Crossbow";
            if (ContextActionHasAny(context, "bow")) return "Bow";
            if (ContextTagHasAny(context, "crossbow")) return "Crossbow";
            if (ContextTagHasAny(context, "bow")) return "Bow";
            return string.Empty;
        }

        private static bool IsMeleeContext(CombatModifierContext context)
        {
            return IsBarehandContext(context) || !string.IsNullOrEmpty(DetectMeleeWeaponType(context));
        }

        private static bool IsBarehandContext(CombatModifierContext context)
        {
            return ContextHasAny(context, "barehand", "barehands", "bare_hands");
        }

        private static bool ContextActionHasAny(CombatModifierContext context, params string[] keywords)
        {
            if (context == null)
            {
                return false;
            }

            foreach (string actionId in context.ActionIds)
            {
                if (ContainsAny(Normalize(actionId), keywords))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContextTagHasAny(CombatModifierContext context, params string[] keywords)
        {
            if (context == null)
            {
                return false;
            }

            foreach (string tag in context.Tags)
            {
                if (ContainsAny(Normalize(tag), keywords))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContextActionLooksLikeSword(CombatModifierContext context)
        {
            if (context == null)
            {
                return false;
            }

            foreach (string actionId in context.ActionIds)
            {
                string key = Normalize(actionId);
                if (ContainsAny(key, "axe", "blunt", "hammer", "mace", "lance", "spear"))
                {
                    continue;
                }

                if (ContainsAny(key,
                    "onehanddefault",
                    "onehandsmash",
                    "onehandflurry",
                    "onehandstab",
                    "twohanddefault",
                    "twohandsmash",
                    "twohandsweeping",
                    "twohandstrike"))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool ContextHasAny(CombatModifierContext context, params string[] keywords)
        {
            if (context == null)
            {
                return false;
            }

            foreach (string tag in context.Tags)
            {
                if (ContainsAny(Normalize(tag), keywords))
                {
                    return true;
                }
            }

            foreach (string actionId in context.ActionIds)
            {
                if (ContainsAny(Normalize(actionId), keywords))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, float> NewModifierMap()
        {
            Dictionary<string, float> result = new Dictionary<string, float>(StringComparer.Ordinal);
            for (int i = 0; i < TrackedModifiers.Length; i++)
            {
                result[TrackedModifiers[i]] = 0f;
            }
            return result;
        }

        private static void AddTrackedModifier(Dictionary<string, float> values, string key, float value)
        {
            if (IsImpactModifier(key))
            {
                AddModifier(values, "attack_ratio_impact", value);
                return;
            }

            if (!IsTrackedModifier(key))
            {
                return;
            }

            AddModifier(values, key, value);
        }

        private static void AddModifier(Dictionary<string, float> values, string key, float value)
        {
            float current;
            values.TryGetValue(key, out current);
            values[key] = current + value;
        }

        private static bool IsTrackedModifier(string key)
        {
            for (int i = 0; i < TrackedModifiers.Length; i++)
            {
                if (string.Equals(TrackedModifiers[i], key, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static CombatModifierContext CollectCombatModifierContext()
        {
            CombatModifierContext result = new CombatModifierContext();
            if (GameSystem<CombatSystem>.HasInstance())
            {
                foreach (BattleAction action in GameSystem<CombatSystem>.Instance().GetCurrentBattleActions())
                {
                    if (action != null && action.Data != null && !string.IsNullOrEmpty(action.Data.Id))
                    {
                        result.ActionIds.Add(action.Data.Id);
                    }
                }
            }

            CollectEquipmentActions(result);

            if (result.ActionIds.Count == 0 && result.Tags.Count == 0)
            {
                AddTagActions("bare_hands", result);
            }
            return result;
        }

        private static void CollectEquipmentActions(CombatModifierContext result)
        {
            if (!GameSystem<EquipSystem>.HasInstance() || !GameSystem<InventorySystem>.HasInstance())
            {
                return;
            }

            EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
            EquipSystem.EquipPreset preset = equipSystem.GetEquipPreset(equipSystem.CurrentEquipPreset);
            if (preset == null || preset.SlotItems == null)
            {
                return;
            }

            foreach (string itemId in preset.SlotItems.Values)
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                ItemData item = GameSystem<InventorySystem>.Instance().FindItem(itemId);
                if (item == null || item.Tags == null)
                {
                    continue;
                }

                foreach (TagData tag in item.Tags)
                {
                    if (tag != null && !string.IsNullOrEmpty(tag.Id))
                    {
                        AddTagActions(tag.Id, result);
                    }
                }
            }
        }

        private static void AddTagActions(string tagId, CombatModifierContext result)
        {
            if (!string.IsNullOrEmpty(tagId))
            {
                result.Tags.Add(tagId);
            }

            TagAllowAction allowed = SingletonDict<string, TagAllowAction>.Get(tagId, null);
            if (allowed == null)
            {
                return;
            }

            AddActionIds(allowed.DefaultActions, result.ActionIds);
            AddActionIds(allowed.SkillActions, result.ActionIds);
        }

        private static void AddActionIds(string[] ids, HashSet<string> result)
        {
            if (ids == null)
            {
                return;
            }

            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                {
                    result.Add(ids[i]);
                }
            }
        }

        private static string FormatActionSummary(HashSet<string> actionIds)
        {
            if (actionIds == null || actionIds.Count == 0)
            {
                return ChatCommandLocalization.Get("none");
            }

            string text = string.Empty;
            int count = 0;
            HashSet<string> displayed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string actionId in actionIds)
            {
                string displayActionId = ActionDisplayId(actionId);
                if (string.IsNullOrEmpty(displayActionId) || !displayed.Add(displayActionId))
                {
                    continue;
                }

                if (count > 0)
                {
                    text += ", ";
                }
                text += displayActionId;
                count++;
            }
            return text;
        }

        private static string Number(float value)
        {
            return value.ToString("0.##");
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (value.IndexOf(candidates[i], StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Percent(float value)
        {
            return (value * 100f).ToString("0.##") + "%";
        }

        private static string WeaponText()
        {
            if (!GameSystem<EquipSystem>.HasInstance() || !GameSystem<InventorySystem>.HasInstance())
            {
                return ChatCommandLocalization.Get("not_ready");
            }

            EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
            EquipSystem.EquipPreset preset = equipSystem.GetEquipPreset(equipSystem.CurrentEquipPreset);
            if (preset == null || preset.SlotItems == null)
            {
                return ChatCommandLocalization.Get("none");
            }

            foreach (string itemId in preset.SlotItems.Values)
            {
                if (string.IsNullOrEmpty(itemId))
                {
                    continue;
                }

                ItemData item = GameSystem<InventorySystem>.Instance().FindItem(itemId);
                if (item != null && HasAllowedActions(item))
                {
                    return item.PrototypeId + " Lv." + item.Level;
                }
            }
            return ChatCommandLocalization.Get("none");
        }

        private static bool HasAllowedActions(ItemData item)
        {
            if (item == null || item.Tags == null)
            {
                return false;
            }

            foreach (TagData tag in item.Tags)
            {
                if (tag != null && !string.IsNullOrEmpty(tag.Id))
                {
                    TagAllowAction allowed = SingletonDict<string, TagAllowAction>.Get(tag.Id, null);
                    if (allowed != null)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
