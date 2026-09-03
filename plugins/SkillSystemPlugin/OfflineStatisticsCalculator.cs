using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Logic;
using Durango.Logic.Combat;
using Durango.Logic.Item;
using Durango.Logic.Skill;
using Durango.Offline;
using Messages;
using Shared.Ability;
using Shared.Skill;
using Yaml;
using Yaml.Util;
using HarmonyLib;
using OfflinePlayer = Durango.Offline.Player;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    internal static class OfflineStatisticsCalculator
    {
        private const float LevelOneHp = 300f;
        private const float HpPerLevel = 9f;
        private const float LevelOneStamina = 100f;
        private static readonly string[] ActiveCombatModifiers = new string[]
        {
            "damage_bonus",
            "critical_rate_plus",
            "critical_damage_bonus",
            "armor_penetration_plus",
            "hit_rate_plus",
            "accuracy_plus"
        };
        private static readonly HashSet<string> LoggedRewards = new HashSet<string>(StringComparer.Ordinal);

        private sealed class CombatModifierContext
        {
            internal readonly HashSet<string> ActionIds = new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> Tags = new HashSet<string>(StringComparer.Ordinal);
        }

        internal static void Send(OfflinePlayer player, OfflineSkillState state)
        {
            if (player == null || state == null || state.Context == null)
            {
                return;
            }

            Statistics statistics = Build(state);
            ApplySurvival(state.Context, statistics);
            player.Send<Statistics>(statistics, 0U);
            Dictionary<string, Gauge> gauges = new Dictionary<string, Gauge>();
            gauges["life"] = state.Context.AppearPlayer.Survival.Life;
            gauges["stamina"] = state.Context.AppearPlayer.Survival.Gauges["stamina"];
            player.Send<SurvivalUpdated>(new SurvivalUpdated
            {
                EntityId = player.EntityId,
                Updated = gauges,
                Removed = new string[0]
            }, 0U);
        }

        private static Statistics Build(OfflineSkillState state)
        {
            int level = state.Context.PlayerInfo == null ? 1 : Math.Max(1, state.Context.PlayerInfo.PlayerLevel);
            int experience = GameSystem<StatisticsSystem>.HasInstance()
                ? Math.Max(0, GameSystem<StatisticsSystem>.Instance().Exp)
                : 0;
            TryResolvePlayerProgression(
                state.Context,
                ref level,
                ref experience);

            Statistics result = default(Statistics);
            result.BasicAbilities = new Dictionary<Basic, int>();
            result.DerivedsAbilities = new Dictionary<Derived, float>();
            result.ResistanceLevels = new Dictionary<Derived, int>();
            result.ResistanceExps = new Dictionary<Derived, int>();
            result.Modifiers = new Dictionary<string, float>(StringComparer.Ordinal);
            result.RepresentPowers = new Dictionary<RepresentType, float>();
            result.Level = level;
            result.Exp = experience;

            InitializeBasicAbilities(result, level);
            ApplyCategoryBasicAbilities(result, state);

            result.DerivedsAbilities[Derived.MaxHealth] = LevelOneHp;
            result.DerivedsAbilities[Derived.LifeMax] = LevelOneHp;
            result.DerivedsAbilities[Derived.MaxEnergy] = LevelOneStamina;
            result.DerivedsAbilities[Derived.StaminaMax] = LevelOneStamina;
            result.DerivedsAbilities[Derived.Swimming] = 100f;

            CombatModifierContext combatContext = CollectCombatModifierContext();
            foreach (Node node in state.EnumerateLearnedNodes())
            {
                ApplyNode(result, node, combatContext);
            }

            ApplyStatDerivedAbilities(result);

            float maxHealth = LevelOneHp + Math.Max(0, level - 1) * HpPerLevel;
            float maxEnergy = result.DerivedsAbilities[Derived.MaxEnergy] + Math.Max(0, level - 1);
            result.DerivedsAbilities[Derived.MaxHealth] = maxHealth;
            result.DerivedsAbilities[Derived.LifeMax] = maxHealth;
            result.DerivedsAbilities[Derived.MaxEnergy] = maxEnergy;
            result.DerivedsAbilities[Derived.StaminaMax] = maxEnergy;

            ApplyRepresentPowers(result);

            return result;
        }

        private static bool TryResolvePlayerProgression(
            PlayerContext context,
            ref int level,
            ref int experience)
        {
            try
            {
                Type api = AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.PlayerProgressionMod.PlayerProgressionApi");
                MethodInfo method = api == null
                    ? null
                    : api.GetMethod(
                        "TryGetProgression",
                        BindingFlags.Static | BindingFlags.Public);
                if (method == null)
                {
                    return false;
                }

                object[] args = new object[]
                {
                    context,
                    level,
                    experience
                };
                bool resolved = (bool)method.Invoke(null, args);
                if (!resolved)
                {
                    return false;
                }

                level = Math.Max(1, (int)args[1]);
                experience = Math.Max(0, (int)args[2]);
                return true;
            }
            catch (Exception exception)
            {
                if (SkillSystemPlugin.Log != null)
                {
                    SkillSystemPlugin.Log.LogWarning(
                        "Player progression statistics lookup failed: " +
                        exception.Message);
                }
                return false;
            }
        }

        private static void InitializeBasicAbilities(Statistics statistics, int level)
        {
            foreach (Basic basic in Enum.GetValues(typeof(Basic)))
            {
                if (basic != Basic.Invalid)
                {
                    statistics.BasicAbilities[basic] = Math.Max(1, level) * 2;
                }
            }
        }

        private static void ApplyCategoryBasicAbilities(Statistics statistics, OfflineSkillState state)
        {
            AddBasic(statistics, Basic.Strength, CategoryBonus(state,
                Shared.Skill.Category.Weaponcrafting,
                Shared.Skill.Category.MeleeCombat,
                Shared.Skill.Category.Defense));

            AddBasic(statistics, Basic.Agility, CategoryBonus(state,
                Shared.Skill.Category.Survival));

            AddBasic(statistics, Basic.Endurance, CategoryBonus(state,
                Shared.Skill.Category.Butchery,
                Shared.Skill.Category.Constructing));

            AddBasic(statistics, Basic.Charisma, CategoryBonus(state,
                Shared.Skill.Category.Cooking,
                Shared.Skill.Category.Armorcrafting,
                Shared.Skill.Category.Farming));

            AddBasic(statistics, Basic.Intelligence, CategoryBonus(state,
                Shared.Skill.Category.Process,
                Shared.Skill.Category.Cooking,
                Shared.Skill.Category.Constructing));

            AddBasic(statistics, Basic.Dexterity, CategoryBonus(state,
                Shared.Skill.Category.Weaponcrafting,
                Shared.Skill.Category.Gathering,
                Shared.Skill.Category.Armorcrafting));

            AddBasic(statistics, Basic.Will, CategoryBonus(state,
                Shared.Skill.Category.Process,
                Shared.Skill.Category.Butchery,
                Shared.Skill.Category.Farming));

            AddBasic(statistics, Basic.Perception, CategoryBonus(state,
                Shared.Skill.Category.Survival,
                Shared.Skill.Category.Gathering,
                Shared.Skill.Category.Butchery));
        }

        private static int CategoryBonus(OfflineSkillState state, params Shared.Skill.Category[] categories)
        {
            int bonus = 0;
            for (int i = 0; i < categories.Length; i++)
            {
                bonus += Math.Max(0, state.GetCategoryLevel(categories[i])) / 3;
            }
            return bonus;
        }

        private static void AddBasic(Statistics statistics, Basic basic, int value)
        {
            statistics.BasicAbilities[basic] = statistics.BasicAbilities[basic] + value;
        }

        private static void ApplyStatDerivedAbilities(Statistics statistics)
        {
            int strength = statistics.BasicAbilities[Basic.Strength];
            int agility = statistics.BasicAbilities[Basic.Agility];
            int dexterity = statistics.BasicAbilities[Basic.Dexterity];
            int perception = statistics.BasicAbilities[Basic.Perception];
            AddDerived(statistics, Derived.Attack, CombatFormula.AttackFromStrength(strength));
            AddDerived(statistics, Derived.AttackRating, CombatFormula.DefensePenetrationFromStrength(strength));
            AddDerived(statistics, Derived.Accuracy, CombatFormula.AccuracyFromAgility(agility));
            AddDerived(statistics, Derived.Dodge, CombatFormula.EvasionFromAgility(agility));
            AddDerived(statistics, Derived.Critical, CombatFormula.CriticalFromDexterity(dexterity));
            AddDerived(statistics, Derived.HidingPower, perception / 3);
            AddDerived(statistics, Derived.TamingPower, 1f);
            AddDerived(statistics, Derived.MaxTamingPet, 8f);

            AddDerived(statistics, Derived.Gathering, StatAbility(statistics, Basic.Intelligence, Basic.Dexterity));
            AddDerived(statistics, Derived.Mining, StatAbility(statistics, Basic.Intelligence, Basic.Dexterity));
            AddDerived(statistics, Derived.Butchering, StatAbility(statistics, Basic.Will, Basic.Perception));
            AddDerived(statistics, Derived.Disassembling, StatAbility(statistics, Basic.Intelligence, Basic.Perception));

            AddDerived(statistics, Derived.Weaponcraft, StatAbility(statistics, Basic.Strength, Basic.Dexterity));
            AddDerived(statistics, Derived.Armorcraft, StatAbility(statistics, Basic.Charisma, Basic.Dexterity));
            AddDerived(statistics, Derived.Tailor, StatAbility(statistics, Basic.Charisma, Basic.Dexterity));
            AddDerived(statistics, Derived.Smith, StatAbility(statistics, Basic.Intelligence, Basic.Will));
            AddDerived(statistics, Derived.Cook, StatAbility(statistics, Basic.Charisma, Basic.Intelligence));
            AddDerived(statistics, Derived.Furnishing, StatAbility(statistics, Basic.Endurance, Basic.Intelligence));
            AddDerived(statistics, Derived.Construction, StatAbility(statistics, Basic.Endurance, Basic.Intelligence));
            AddDerived(statistics, Derived.Farming, StatAbility(statistics, Basic.Charisma, Basic.Will));
            AddDerived(statistics, Derived.Handicraft, StatAbility(statistics, Basic.Intelligence, Basic.Will));
        }

        private static float StatAbility(Statistics statistics, Basic first, Basic second)
        {
            return 5f + statistics.BasicAbilities[first] / 6 + statistics.BasicAbilities[second] / 6;
        }

        private static void AddDerived(Statistics statistics, Derived derived, float value)
        {
            float current;
            statistics.DerivedsAbilities.TryGetValue(derived, out current);
            statistics.DerivedsAbilities[derived] = current + value;
        }

        private static void ApplyRepresentPowers(Statistics statistics)
        {
            Constants constants = Yaml.Util.Singleton<Constants>.Instance;
            if (constants == null || constants.RepresentAbilities == null)
            {
                return;
            }

            foreach (KeyValuePair<RepresentType, Dictionary<Derived, float>> represent in constants.RepresentAbilities)
            {
                float power = 0f;
                if (represent.Value != null)
                {
                    foreach (KeyValuePair<Derived, float> ability in represent.Value)
                    {
                        float value;
                        if (statistics.DerivedsAbilities.TryGetValue(ability.Key, out value))
                        {
                            power += value * ability.Value;
                        }
                    }
                }
                statistics.RepresentPowers[represent.Key] = power;
            }
        }

        private static void ApplyNode(Statistics statistics, Node node, CombatModifierContext combatContext)
        {
            if (node.Rewards == null)
            {
                return;
            }

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
                        ApplyModifier(statistics, modifier.Key, modifier.Value, node, reward, combatContext);
                    }
                }

                if (!string.IsNullOrEmpty(reward.Modifier))
                {
                    ApplyModifier(statistics, reward.Modifier, reward.Value, node, reward, combatContext);
                }
            }
        }

        private static void ApplyModifier(Statistics statistics, string id, float value, Node node, Durango.Logic.Skill.Reward reward, CombatModifierContext combatContext)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            if (IsActiveCombatModifier(id) && !RewardMatchesCombatContext(node, reward, combatContext))
            {
                return;
            }

            float current;
            statistics.Modifiers.TryGetValue(id, out current);
            statistics.Modifiers[id] = current + value;

            Basic basic;
            if (TryMapBasic(id, out basic))
            {
                int basicValue;
                statistics.BasicAbilities.TryGetValue(basic, out basicValue);
                statistics.BasicAbilities[basic] = basicValue + (int)Math.Round(value);
                LogModifier(node, id, value, null);
                return;
            }

            Derived derived;
            if (!TryMapDerived(id, out derived))
            {
                LogModifier(node, id, value, null);
                return;
            }

            SkillModifier definition = SingletonDict<string, SkillModifier>.Get(id, null);
            float baseValue;
            statistics.DerivedsAbilities.TryGetValue(derived, out baseValue);
            float updated = value;
            if (definition != null && definition.IncreaseType == IncreaseType.Ratio)
            {
                updated = GetBaseDerived(statistics.Level, derived) * value;
            }
            if (definition != null && definition.ApplyType == ApplyType.Replace)
            {
                statistics.DerivedsAbilities[derived] = value;
            }
            else
            {
                statistics.DerivedsAbilities[derived] = baseValue + updated;
            }

            MirrorGaugeDerived(statistics.DerivedsAbilities, derived);
            LogModifier(node, id, value, derived);
        }

        private static bool IsActiveCombatModifier(string id)
        {
            for (int i = 0; i < ActiveCombatModifiers.Length; i++)
            {
                if (string.Equals(id, ActiveCombatModifiers[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
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
            if (reward == null || reward.ActionIds == null || reward.ActionIds.Length == 0)
            {
                return true;
            }
            if (context == null || context.ActionIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < reward.ActionIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(reward.ActionIds[i]) && context.ActionIds.Contains(reward.ActionIds[i]))
                {
                    return true;
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
            if (ContainsAny(key, "blunt", "hammer", "mace")) return ContextHasAny(context, "blunt", "hammer", "mace");
            if (ContainsAny(key, "axe")) return ContextHasAny(context, "axe");
            if (ContainsAny(key, "sword")) return ContextHasAny(context, "sword");
            if (ContainsAny(key, "lance", "spear")) return ContextHasAny(context, "lance", "spear");
            if (ContainsAny(key, "barehand", "barehands", "bare")) return ContextHasAny(context, "barehand", "barehands", "bare");
            return true;
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

        private static float GetBaseDerived(int level, Derived derived)
        {
            if (derived == Derived.MaxHealth || derived == Derived.LifeMax)
            {
                return LevelOneHp;
            }
            if (derived == Derived.MaxEnergy || derived == Derived.StaminaMax)
            {
                return LevelOneStamina;
            }
            return 0f;
        }

        private static bool TryMapBasic(string id, out Basic basic)
        {
            switch (Normalize(id))
            {
                case "strengthplus": basic = Basic.Strength; return true;
                case "charismaplus": basic = Basic.Charisma; return true;
                case "dexterityplus": basic = Basic.Dexterity; return true;
                case "agilityplus": basic = Basic.Agility; return true;
                case "enduranceplus": basic = Basic.Endurance; return true;
                case "willplus": basic = Basic.Will; return true;
                case "intelligenceplus": basic = Basic.Intelligence; return true;
                case "perceptionplus": basic = Basic.Perception; return true;
                default: basic = Basic.Invalid; return false;
            }
        }

        private static void MirrorGaugeDerived(Dictionary<Derived, float> values, Derived changed)
        {
            if (changed == Derived.MaxHealth || changed == Derived.LifeMax)
            {
                float value = values[changed];
                values[Derived.MaxHealth] = value;
                values[Derived.LifeMax] = value;
            }
            else if (changed == Derived.MaxEnergy || changed == Derived.StaminaMax)
            {
                float value = values[changed];
                values[Derived.MaxEnergy] = value;
                values[Derived.StaminaMax] = value;
            }
        }

        private static bool TryMapDerived(string id, out Derived derived)
        {
            string key = Normalize(id);
            foreach (Derived value in Enum.GetValues(typeof(Derived)))
            {
                if (value != Derived.Invalid && Normalize(value.ToString()) == key)
                {
                    derived = value;
                    return true;
                }
            }

            Derived? combatDerived = CombatFormula.MapFlatCombatModifier(key);
            if (combatDerived != null)
            {
                derived = combatDerived.Value;
                return true;
            }

            if (ContainsAny(key, "maxhealth", "healthmax", "lifemax", "maxhp", "hpmax")) derived = Derived.MaxHealth;
            else if (ContainsAny(key, "maxenergy", "energymax", "staminamax", "maxstamina")) derived = Derived.MaxEnergy;
            else if (ContainsAny(key, "liferegen", "healthregen", "lifevelocity")) derived = Derived.LifeVelocity;
            else if (ContainsAny(key, "staminaregen", "energyregen", "staminavelocity")) derived = Derived.StaminaVelocity;
            else if (ContainsAny(key, "basedefense", "defense")) derived = Derived.Defense;
            else if (ContainsAny(key, "accuracy", "hitrate")) derived = Derived.Accuracy;
            else if (ContainsAny(key, "dodge", "evade")) derived = Derived.Dodge;
            else if (ContainsAny(key, "attackrating")) derived = Derived.AttackRating;
            else if (ContainsAny(key, "attackpower")) derived = Derived.Attack;
            else if (ContainsAny(key, "gathering")) derived = Derived.Gathering;
            else if (ContainsAny(key, "mining")) derived = Derived.Mining;
            else if (ContainsAny(key, "weaponcraft")) derived = Derived.Weaponcraft;
            else if (ContainsAny(key, "armorcraft")) derived = Derived.Armorcraft;
            else if (ContainsAny(key, "tailor", "sewing")) derived = Derived.Tailor;
            else if (ContainsAny(key, "smith", "metalprocess")) derived = Derived.Smith;
            else if (ContainsAny(key, "cook")) derived = Derived.Cook;
            else if (ContainsAny(key, "furnishing", "furniturecraft")) derived = Derived.Furnishing;
            else if (ContainsAny(key, "construction", "building")) derived = Derived.Construction;
            else if (ContainsAny(key, "farming")) derived = Derived.Farming;
            else if (ContainsAny(key, "hiding", "stealth")) derived = Derived.HidingPower;
            else if (ContainsAny(key, "maxtamingpet")) derived = Derived.MaxTamingPet;
            else if (ContainsAny(key, "taming")) derived = Derived.TamingPower;
            else if (ContainsAny(key, "butcher", "slaughter")) derived = Derived.Butchering;
            else if (ContainsAny(key, "handicraft")) derived = Derived.Handicraft;
            else if (ContainsAny(key, "disassembl")) derived = Derived.Disassembling;
            else
            {
                derived = Derived.Invalid;
                return false;
            }
            return true;
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

        private static void ApplySurvival(PlayerContext context, Statistics statistics)
        {
            float maxHealth = statistics.DerivedsAbilities[Derived.MaxHealth];
            float maxStamina = statistics.DerivedsAbilities[Derived.MaxEnergy];
            float hp = Math.Max(0f, Math.Min(maxHealth, context.AppearPlayer.Survival.Life.Get()));

            if (context.AppearPlayer.Survival.Gauges == null)
            {
                context.AppearPlayer.Survival.Gauges = new Dictionary<string, Gauge>();
            }

            Gauge stamina;
            context.AppearPlayer.Survival.Gauges.TryGetValue("stamina", out stamina);
            float energy = stamina == null ? maxStamina : Math.Max(0f, Math.Min(maxStamina, stamina.Get()));
            context.AppearPlayer.Survival.Life = MakeGauge(maxHealth, hp);
            context.AppearPlayer.Survival.Gauges["stamina"] = MakeGauge(maxStamina, energy);
        }

        private static Gauge MakeGauge(float max, float current)
        {
            return new Gauge(max, 0f, new GaugeNode[]
            {
                new GaugeNode { Time = 0.0, Value = current }
            });
        }

        private static void LogModifier(Node node, string id, float value, Derived? derived)
        {
            if (SkillSystemPlugin.Log != null)
            {
                string key = node.Id + "/" + node.Sub + "/" + node.Level + "/" + id;
                if (LoggedRewards.Add(key))
                {
                    SkillSystemPlugin.Log.LogInfo("Skill stat reward: " + node.Id + "/" + node.Sub + " Lv." + node.Level + " modifier=" + id + " value=" + value + " derived=" + (derived == null ? "unmapped" : derived.Value.ToString()));
                }
            }
        }
    }
}
