using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Logic;
using Durango.Logic.Item;
using Durango.Network;
using HarmonyLib;
using Messages;
using Shared.Ability;
using Yaml;
using Yaml.Util;

namespace BaoX.DurangoOriginal.WeaponStatisticsMod
{
    internal struct WeaponDerivedContribution
    {
        internal float Attack;
        internal float Accuracy;
        internal float AttackRating;
        internal float Critical;
        internal float Defense;
        internal float InventoryCapacity;
        internal float HealthFromEndurance;
        internal float EnergyFromStats;
        internal bool HasWeapon;
        internal Dictionary<Basic, int> Basics;
        internal Dictionary<Derived, float> Deriveds;
    }

    internal static class EquippedWeaponStatistics
    {
        private static readonly MethodInfo StatisticsReceived = typeof(StatisticsSystem).GetMethod(
            "StatisticsReceived",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(Statistics), typeof(PacketHeader) },
            null);
        private static WeaponDerivedContribution _lastContribution;
        private static bool _refreshing;

        internal static void Augment(ref Statistics statistics)
        {
            if (statistics.DerivedsAbilities == null)
            {
                statistics.DerivedsAbilities = new Dictionary<Derived, float>();
            }
            if (statistics.BasicAbilities == null)
            {
                statistics.BasicAbilities = new Dictionary<Basic, int>();
            }

            WeaponDerivedContribution contribution = GetCurrentContribution(statistics.BasicAbilities);
            Add(statistics.DerivedsAbilities, Derived.Attack, contribution.Attack);
            Add(statistics.DerivedsAbilities, Derived.Accuracy, contribution.Accuracy);
            Add(statistics.DerivedsAbilities, Derived.AttackRating, contribution.AttackRating);
            Add(statistics.DerivedsAbilities, Derived.Critical, contribution.Critical);
            Add(statistics.DerivedsAbilities, Derived.Defense, contribution.Defense);
            Add(statistics.DerivedsAbilities, Derived.InventoryCapacity, contribution.InventoryCapacity);
            int baseEndurance = GetBasic(statistics.BasicAbilities, Basic.Endurance);
            int baseWill = GetBasic(statistics.BasicAbilities, Basic.Will);
            int enduranceBonus = GetBasic(contribution.Basics, Basic.Endurance);
            int willBonus = GetBasic(contribution.Basics, Basic.Will);
            contribution.HealthFromEndurance = enduranceBonus * 4.5f;
            contribution.EnergyFromStats =
                (baseEndurance + enduranceBonus) / 3 - baseEndurance / 3
                + (baseWill + willBonus) / 3 - baseWill / 3;
            Add(statistics.DerivedsAbilities, Derived.MaxHealth, contribution.HealthFromEndurance);
            Add(statistics.DerivedsAbilities, Derived.LifeMax, contribution.HealthFromEndurance);
            Add(statistics.DerivedsAbilities, Derived.MaxEnergy, contribution.EnergyFromStats);
            Add(statistics.DerivedsAbilities, Derived.StaminaMax, contribution.EnergyFromStats);
            ApplyBasics(statistics.BasicAbilities, contribution.Basics, 1);
            ApplyDeriveds(statistics.DerivedsAbilities, contribution.Deriveds, 1f);
            _lastContribution = contribution;
        }

        internal static void RefreshCurrent()
        {
            if (_refreshing || !GameSystem<StatisticsSystem>.HasInstance())
            {
                return;
            }

            StatisticsSystem system = GameSystem<StatisticsSystem>.Instance();
            if (system.Statistics == null || StatisticsReceived == null)
            {
                return;
            }

            Statistics statistics = system.Statistics.Value;
            if (statistics.DerivedsAbilities == null)
            {
                statistics.DerivedsAbilities = new Dictionary<Derived, float>();
            }
            if (statistics.BasicAbilities == null)
            {
                statistics.BasicAbilities = new Dictionary<Basic, int>();
            }

            Add(statistics.DerivedsAbilities, Derived.Attack, -_lastContribution.Attack);
            Add(statistics.DerivedsAbilities, Derived.Accuracy, -_lastContribution.Accuracy);
            Add(statistics.DerivedsAbilities, Derived.AttackRating, -_lastContribution.AttackRating);
            Add(statistics.DerivedsAbilities, Derived.Critical, -_lastContribution.Critical);
            Add(statistics.DerivedsAbilities, Derived.Defense, -_lastContribution.Defense);
            Add(statistics.DerivedsAbilities, Derived.InventoryCapacity, -_lastContribution.InventoryCapacity);
            Add(statistics.DerivedsAbilities, Derived.MaxHealth, -_lastContribution.HealthFromEndurance);
            Add(statistics.DerivedsAbilities, Derived.LifeMax, -_lastContribution.HealthFromEndurance);
            Add(statistics.DerivedsAbilities, Derived.MaxEnergy, -_lastContribution.EnergyFromStats);
            Add(statistics.DerivedsAbilities, Derived.StaminaMax, -_lastContribution.EnergyFromStats);
            ApplyBasics(statistics.BasicAbilities, _lastContribution.Basics, -1);
            ApplyDeriveds(statistics.DerivedsAbilities, _lastContribution.Deriveds, -1f);
            _lastContribution = default(WeaponDerivedContribution);

            try
            {
                _refreshing = true;
                StatisticsReceived.Invoke(system, new object[] { statistics, default(PacketHeader) });
            }
            catch (Exception exception)
            {
                WeaponStatisticsPlugin.Log.LogWarning("Weapon statistics refresh failed: " + exception.Message);
            }
            finally
            {
                _refreshing = false;
            }
        }

        private static WeaponDerivedContribution GetCurrentContribution(Dictionary<Basic, int> baseBasics)
        {
            WeaponDerivedContribution result = default(WeaponDerivedContribution);
            result.Basics = new Dictionary<Basic, int>();
            result.Deriveds = new Dictionary<Derived, float>();
            if (!GameSystem<EquipSystem>.HasInstance() || !GameSystem<InventorySystem>.HasInstance())
            {
                return result;
            }

            EquipSystem equipSystem = GameSystem<EquipSystem>.Instance();
            EquipSystem.EquipPreset preset = equipSystem.GetEquipPreset(equipSystem.CurrentEquipPreset);
            if (preset == null)
            {
                return result;
            }

            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            bool hasRangedWeapon = false;
            foreach (KeyValuePair<string, string> slot in preset.SlotItems)
            {
                if (string.IsNullOrEmpty(slot.Value) || !visited.Add(slot.Value))
                {
                    continue;
                }

                ItemData item = GameSystem<InventorySystem>.Instance().FindItem(slot.Value);
                if (item == null)
                {
                    continue;
                }
                if (IsRangedWeapon(item))
                {
                    hasRangedWeapon = true;
                }

                WeaponPerformanceValues values;
                if (WeaponPerformanceDatabase.TryGet(item.PrototypeId, item.Level, out values))
                {
                    result.HasWeapon = true;
                    result.Attack += values.Get("attack");
                    result.Accuracy += values.Get("accuracy");
                    result.AttackRating += values.Get("attack_rating");
                    result.Critical += values.Get("critical");
                }
                if (WeaponPerformanceDatabase.TryGetArmor(item.PrototypeId, item.Level, out values))
                {
                    result.Defense += values.Get("defense");
                    result.InventoryCapacity += values.Get("bag_size");
                }
                if (WeaponPerformanceDatabase.TryGetModifiers(item.PrototypeId, item.Level, out values))
                {
                    ApplyModifierValues(ref result, values);
                }
            }
            if (result.HasWeapon)
            {
                result.Accuracy -= 100f;
            }
            ApplyCombatCategoryContext(ref result, baseBasics, hasRangedWeapon);
            return result;
        }

        private static void ApplyCombatCategoryContext(ref WeaponDerivedContribution result, Dictionary<Basic, int> baseBasics, bool hasRangedWeapon)
        {
            if (!hasRangedWeapon || !GameSystem<SkillSystem>.HasInstance())
            {
                return;
            }

            int meleeBonus = CategoryBonus(Shared.Skill.Category.MeleeCombat);
            int rangedBonus = CategoryBonus(Shared.Skill.Category.RangedCombat);
            int delta = rangedBonus - meleeBonus;
            if (delta == 0)
            {
                return;
            }

            AddBasic(result.Basics, Basic.Strength, delta);
            int baseStrength = GetBasic(baseBasics, Basic.Strength);
            result.Attack += AttackFromStrength(baseStrength + delta) - AttackFromStrength(baseStrength);
            result.AttackRating += DefensePenetrationFromStrength(baseStrength + delta) - DefensePenetrationFromStrength(baseStrength);
        }

        private static int CategoryBonus(Shared.Skill.Category category)
        {
            return Math.Max(0, GameSystem<SkillSystem>.Instance().GetCategoryLevel(category)) / 3;
        }

        private static float AttackFromStrength(int strength)
        {
            return 40f + Math.Max(0, strength) / 5;
        }

        private static float DefensePenetrationFromStrength(int strength)
        {
            return Math.Max(0, strength) / 5;
        }

        private static void AddBasic(Dictionary<Basic, int> values, Basic key, int amount)
        {
            int current;
            values.TryGetValue(key, out current);
            values[key] = current + amount;
        }

        private static bool IsRangedWeapon(ItemData item)
        {
            if (item == null || item.Tags == null)
            {
                return false;
            }

            foreach (TagData tag in item.Tags)
            {
                if (tag == null || string.IsNullOrEmpty(tag.Id))
                {
                    continue;
                }
                if (tag.Id == "bow" || tag.Id == "crossbow")
                {
                    return true;
                }
                TagAllowAction allowed = SingletonDict<string, TagAllowAction>.Get(tag.Id, null);
                if (allowed != null && (HasRangedAction(allowed.DefaultActions) || HasRangedAction(allowed.SkillActions)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasRangedAction(string[] actions)
        {
            if (actions == null)
            {
                return false;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                if (!string.IsNullOrEmpty(actions[i]) && actions[i].StartsWith("ranged_", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void ApplyModifierValues(ref WeaponDerivedContribution result, WeaponPerformanceValues values)
        {
            foreach (KeyValuePair<string, float> pair in values.Nums)
            {
                Basic basic;
                Derived derived;
                if (TryMapBasic(pair.Key, out basic))
                {
                    int current;
                    result.Basics.TryGetValue(basic, out current);
                    result.Basics[basic] = current + (int)Math.Round(pair.Value);
                }
                else if (TryMapDerived(pair.Key, out derived))
                {
                    float current;
                    result.Deriveds.TryGetValue(derived, out current);
                    result.Deriveds[derived] = current + pair.Value;
                }
            }
        }

        private static bool TryMapBasic(string id, out Basic basic)
        {
            switch (id)
            {
                case "strength_plus": basic = Basic.Strength; return true;
                case "charisma_plus": basic = Basic.Charisma; return true;
                case "dexterity_plus": basic = Basic.Dexterity; return true;
                case "agility_plus": basic = Basic.Agility; return true;
                case "endurance_plus": basic = Basic.Endurance; return true;
                case "will_plus": basic = Basic.Will; return true;
                case "intelligence_plus": basic = Basic.Intelligence; return true;
                case "perception_plus": basic = Basic.Perception; return true;
                default: basic = Basic.Invalid; return false;
            }
        }

        private static bool TryMapDerived(string id, out Derived derived)
        {
            switch (id)
            {
                case "attack_plus": derived = Derived.Attack; return true;
                case "attack_rating_plus": derived = Derived.AttackRating; return true;
                case "accuracy_plus": derived = Derived.Accuracy; return true;
                case "critical_plus": derived = Derived.Critical; return true;
                case "dodge_plus": derived = Derived.Dodge; return true;
                case "carry_capacity": derived = Derived.InventoryCapacity; return true;
                case "max_health_plus": derived = Derived.MaxHealth; return true;
                case "max_energy_plus": derived = Derived.MaxEnergy; return true;
                case "life_recovery": derived = Derived.LifeVelocity; return true;
                case "normal_speed_bonus": derived = Derived.Speed; return true;
                case "gathering_plus": derived = Derived.Gathering; return true;
                case "mining_plus": derived = Derived.Mining; return true;
                case "weaponcraft_plus": derived = Derived.Weaponcraft; return true;
                case "armorcraft_plus": derived = Derived.Armorcraft; return true;
                case "tailor_plus": derived = Derived.Tailor; return true;
                case "smith_plus": derived = Derived.Smith; return true;
                case "cook_plus": derived = Derived.Cook; return true;
                case "furnishing_plus": derived = Derived.Furnishing; return true;
                case "construction_plus": derived = Derived.Construction; return true;
                case "farming_plus": derived = Derived.Farming; return true;
                case "hiding_power": derived = Derived.HidingPower; return true;
                case "butchering_plus": derived = Derived.Butchering; return true;
                case "handicraft_plus": derived = Derived.Handicraft; return true;
                case "volcanic_heat_resistant_plus": derived = Derived.VolcanicHeatResistant; return true;
                default: derived = Derived.Invalid; return false;
            }
        }

        private static void ApplyBasics(Dictionary<Basic, int> target, Dictionary<Basic, int> source, int sign)
        {
            if (target == null || source == null) return;
            foreach (KeyValuePair<Basic, int> pair in source)
            {
                int current;
                target.TryGetValue(pair.Key, out current);
                target[pair.Key] = Math.Max(0, current + pair.Value * sign);
            }
        }

        private static int GetBasic(Dictionary<Basic, int> values, Basic key)
        {
            int value;
            return values != null && values.TryGetValue(key, out value) ? value : 0;
        }

        private static void ApplyDeriveds(Dictionary<Derived, float> target, Dictionary<Derived, float> source, float sign)
        {
            if (target == null || source == null) return;
            foreach (KeyValuePair<Derived, float> pair in source)
            {
                Add(target, pair.Key, pair.Value * sign);
                if (pair.Key == Derived.MaxHealth) Add(target, Derived.LifeMax, pair.Value * sign);
                else if (pair.Key == Derived.MaxEnergy) Add(target, Derived.StaminaMax, pair.Value * sign);
            }
        }

        private static void Add(Dictionary<Derived, float> values, Derived key, float amount)
        {
            float current;
            values.TryGetValue(key, out current);
            values[key] = Math.Max(0f, current + amount);
        }
    }

    [HarmonyPatch(typeof(StatisticsSystem), "StatisticsReceived")]
    internal static class StatisticsReceivedWeaponPatch
    {
        private static void Prefix(ref Statistics msg)
        {
            EquippedWeaponStatistics.Augment(ref msg);
        }
    }

    [HarmonyPatch(typeof(EquipSystem), "EquipmentsReceived")]
    internal static class EquipmentsReceivedWeaponStatisticsPatch
    {
        private static void Postfix()
        {
            EquippedWeaponStatistics.RefreshCurrent();
        }
    }
}
