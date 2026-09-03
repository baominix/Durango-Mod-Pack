using System;
using System.Collections.Generic;
using Durango.Logic.Item;
using HarmonyLib;
using Messages;
using Yaml;

namespace Baominix.DurangoOriginal.CombatSystem.EquipmentPerformance
{
    internal static class WeaponItemPerformance
    {
        internal static void Enrich(ItemData item)
        {
            if (item == null)
            {
                return;
            }

            WeaponPerformanceValues values;
            int previewLevel = EquipmentLevelRules.GetPerformanceLevel(item);
            if (WeaponPerformanceDatabase.TryGet(item.PrototypeId, previewLevel, out values))
            {
                Merge(item, "weapon", values);
            }
            if (WeaponPerformanceDatabase.TryGetArmor(item.PrototypeId, previewLevel, out values))
            {
                Merge(item, "armor", values);
            }
            if (WeaponPerformanceDatabase.TryGetModifiers(item.PrototypeId, previewLevel, out values))
            {
                Merge(item, "modifiers", values);
            }
        }

        private static void Merge(ItemData item, string performanceId, WeaponPerformanceValues values)
        {
            List<Performance> performances = item.Performances;
            int index = -1;
            for (int i = 0; i < performances.Count; i++)
            {
                if (performances[i].Id == performanceId)
                {
                    index = i;
                    break;
                }
            }

            Performance performance = index >= 0 ? performances[index] : new Performance { Id = performanceId };
            if (performance.Nums == null)
            {
                performance.Nums = new Dictionary<string, float>();
            }
            if (performance.Strs == null)
            {
                performance.Strs = new Dictionary<string, string>();
            }

            foreach (KeyValuePair<string, float> pair in values.Nums)
            {
                performance.Nums[pair.Key] = pair.Value;
            }
            foreach (KeyValuePair<string, string> pair in values.Strs)
            {
                performance.Strs[pair.Key] = pair.Value;
            }

            if (index >= 0) performances[index] = performance;
            else performances.Add(performance);
        }
    }

    [HarmonyPatch(typeof(ItemData), "Set", new Type[] { typeof(Item) })]
    internal static class ItemDataMessageSetWeaponPerformancePatch
    {
        private static void Postfix(ItemData __instance)
        {
            WeaponItemPerformance.Enrich(__instance);
        }
    }

    [HarmonyPatch(typeof(ItemData), "Set", new Type[] { typeof(PrototypePreset) })]
    internal static class ItemDataPresetSetWeaponPerformancePatch
    {
        private static void Postfix(ItemData __instance)
        {
            WeaponItemPerformance.Enrich(__instance);
        }
    }
}
