using System;
using System.Collections.Generic;
using Messages;

namespace BaoX.DurangoOriginal.GatheringMod
{
    internal static class LostPackageModernDatabase
    {
        internal const string CollectibleId = "paperbox_normal_mixed";
        internal const string GeneratorPrefix = "lost_package_modern:";
        internal const int MaximumTypes = 8;
        internal const int MaximumAmountPerType = 5;

        private static readonly object RandomGate = new object();
        private static readonly Random Random = new Random();

        // Curated from tools/island-market-dump/island-market-dump.log.
        // Clothing and Accessories are separate market categories. Selection
        // chooses one of the five groups first, then an item in that group, so
        // a larger category cannot overwhelm the smaller categories.
        private static readonly string[] ClothingPrototypeIds =
        {
            "clothes_cargopants",
            "clothes_northernface",
            "clothes_officeworker",
            "clothes_officeworker_torn",
            "clothes_waiter",
            "clothes_waiter_torn",
            "clothes_gmpress",
            "clothes_school_event_cardigan"
        };

        private static readonly string[] AccessoryPrototypeIds =
        {
            "digitalwatch",
            "event_tablet",
            "bag_birkin",
            "hat_sunglasses",
            "hat_bike",
            "hat_safety",
            "hat_trafficcone",
            "necklace_tag_starting"
        };

        private static readonly string[] FurniturePrototypeIds =
        {
            "capsulated_refrigerator_01",
            "capsulated_refrigerator_02",
            "capsulated_fan_01",
            "capsulated_cabinet_warp_01",
            "capsulated_cabinet_warp_02",
            "capsulated_classroom_chair",
            "capsulated_classroom_desk",
            "capsulated_classroom_locker_01",
            "capsulated_bed_02_steel_hospital",
            "capsulated_curtain_01_steel_hospital",
            "capsulated_wheelchair_01",
            "capsulated_camp_radio_station_02",
            "capsulated_worktable_warp_02",
            "capsulated_worktable_warp_03",
            "capsulated_sofa_warp_01",
            "capsulated_s02_car"
        };

        private static readonly string[] WeaponToolPrototypeIds =
        {
            "axe_onehand_emergency",
            "bat_onehand",
            "broom_twohand",
            "chainsaw_twohand",
            "chair_twohand",
            "gmpress_camera",
            "golf_twohand",
            "hammer_officer",
            "hoe_twohand_farmer",
            "keyboard_onehand",
            "knife_onehand_survival",
            "needle_waiter",
            "signpole_twohand_01",
            "signpole_twohand_02",
            "saxophone_onehand",
            "urban_axe_twohand"
        };

        private static readonly string[] FoodMedicinePrototypeIds =
        {
            "food_k",
            "ramen",
            "coffee_lessbe",
            "event_fatigue_drug_01",
            "medicine_modern_01",
            "medicine_pillet_01",
            "ration_survival",
            "survival_food_the_firm",
            "hamberger_01",
            "bread_03_pizza",
            "sandwich_01",
            "milk",
            "hardtack_store",
            "hp_medicine_store",
            "fatigue_drug",
            "immune_gas"
        };

        private static readonly string[][] PrototypeGroups =
        {
            ClothingPrototypeIds,
            AccessoryPrototypeIds,
            FurniturePrototypeIds,
            WeaponToolPrototypeIds,
            FoodMedicinePrototypeIds
        };

        private static readonly string[] PrototypeIds =
            CombinePrototypeGroups();

        private static readonly HashSet<string> PrototypeSet =
            new HashSet<string>(PrototypeIds, StringComparer.Ordinal);

        internal static List<Generator> CreateGenerators(int level)
        {
            List<List<string>> availableGroups =
                new List<List<string>>();
            int availableCount = 0;
            for (int groupIndex = 0;
                groupIndex < PrototypeGroups.Length;
                groupIndex++)
            {
                List<string> available = new List<string>();
                string[] group = PrototypeGroups[groupIndex];
                for (int i = 0; i < group.Length; i++)
                {
                    Yaml.Prototype prototype = GetPrototype(
                        group[i],
                        level);
                    if (prototype != null &&
                        IsAllowedCategory(prototype.Category))
                    {
                        available.Add(group[i]);
                        availableCount++;
                    }
                }
                availableGroups.Add(available);
            }

            List<Generator> result = new List<Generator>();
            if (availableCount == 0)
            {
                return result;
            }

            lock (RandomGate)
            {
                int typeCount = Random.Next(
                    1,
                    Math.Min(MaximumTypes, availableCount) + 1);
                for (int i = 0; i < typeCount; i++)
                {
                    List<int> populatedGroupIndexes = new List<int>();
                    for (int groupIndex = 0;
                        groupIndex < availableGroups.Count;
                        groupIndex++)
                    {
                        if (availableGroups[groupIndex].Count > 0)
                        {
                            populatedGroupIndexes.Add(groupIndex);
                        }
                    }

                    int selectedGroupIndex = populatedGroupIndexes[
                        Random.Next(0, populatedGroupIndexes.Count)];
                    List<string> selectedGroup =
                        availableGroups[selectedGroupIndex];
                    int selectedItemIndex = Random.Next(
                        0,
                        selectedGroup.Count);
                    string prototypeId = selectedGroup[selectedItemIndex];
                    selectedGroup.RemoveAt(selectedItemIndex);

                    Yaml.Prototype prototype = GetPrototype(
                        prototypeId,
                        level);
                    if (prototype == null)
                    {
                        continue;
                    }

                    result.Add(new Generator
                    {
                        Id = GeneratorPrefix + prototypeId,
                        Name = prototype.Name,
                        Icon = prototype.Icon,
                        Amount = RandomAmount(),
                        Level = level,
                        Effort = 2.5f + (level - 1) * 0.25f,
                        Duration = 3f,
                        Enabled = true,
                        ToolRequirements = new Dictionary<string, int>
                        {
                            { "bare_hands", 1 }
                        }
                    });
                }
            }
            return result;
        }

        private static string[] CombinePrototypeGroups()
        {
            List<string> result = new List<string>();
            for (int i = 0; i < PrototypeGroups.Length; i++)
            {
                result.AddRange(PrototypeGroups[i]);
            }
            return result.ToArray();
        }

        internal static bool HasOnlyExpectedGenerators(
            int level,
            Generator[] generators)
        {
            if (generators == null || generators.Length == 0)
            {
                return true;
            }
            if (generators.Length > MaximumTypes)
            {
                return false;
            }

            HashSet<string> seen = new HashSet<string>(
                StringComparer.Ordinal);
            for (int i = 0; i < generators.Length; i++)
            {
                Generator generator = generators[i];
                string prototypeId;
                int bareHands;
                if (!TryResolvePrototypeId(generator.Id, out prototypeId) ||
                    !seen.Add(prototypeId) ||
                    generator.Amount < 1 ||
                    generator.Amount > MaximumAmountPerType ||
                    generator.Level != level ||
                    generator.Duration <= 0f ||
                    generator.Effort <= 0f ||
                    string.IsNullOrEmpty(generator.Icon) ||
                    generator.ToolRequirements == null ||
                    generator.ToolRequirements.Count != 1 ||
                    !generator.ToolRequirements.TryGetValue(
                        "bare_hands",
                        out bareHands) ||
                    bareHands < 1)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool IsGenerator(string generatorId)
        {
            string prototypeId;
            return TryResolvePrototypeId(generatorId, out prototypeId);
        }

        internal static bool TryResolvePrototypeId(
            string generatorId,
            out string prototypeId)
        {
            prototypeId = null;
            if (string.IsNullOrEmpty(generatorId) ||
                !generatorId.StartsWith(
                    GeneratorPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string candidate = generatorId.Substring(
                GeneratorPrefix.Length);
            if (!PrototypeSet.Contains(candidate))
            {
                return false;
            }
            prototypeId = candidate;
            return true;
        }

        internal static string GeneratorIcon(
            string generatorId,
            int level)
        {
            string prototypeId;
            if (!TryResolvePrototypeId(generatorId, out prototypeId))
            {
                return null;
            }
            Yaml.Prototype prototype = GetPrototype(prototypeId, level);
            return prototype == null ? null : prototype.Icon;
        }

        private static int RandomAmount()
        {
            // 85% of result types contain only one item. Larger stacks remain
            // possible but become progressively rarer, capped at five.
            int roll = Random.Next(0, 1000);
            if (roll < 850) return 1;
            if (roll < 940) return 2;
            if (roll < 980) return 3;
            if (roll < 995) return 4;
            return 5;
        }

        private static Yaml.Prototype GetPrototype(
            string prototypeId,
            int level)
        {
            return Yaml.PrototypeYaml.GetItemPrototype(
                    prototypeId,
                    Math.Max(1, level)) ??
                Yaml.PrototypeYaml.GetItemPrototype(prototypeId);
        }

        private static bool IsAllowedCategory(string category)
        {
            return string.Equals(
                    category,
                    "clothing",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "accessory",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "building/furniture",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "weapon/tool",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "food/medicine",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
