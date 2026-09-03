using System;
using System.Collections.Generic;
using Messages;

namespace BaoX.DurangoOriginal.GatheringMod
{
    internal sealed class GatheringGeneratorDefinition
    {
        internal readonly string Id;
        internal readonly string PrototypeId;
        internal readonly string Name;
        internal readonly string Icon;
        internal readonly int Amount;
        internal readonly string[] ToolTags;

        internal GatheringGeneratorDefinition(
            string id,
            string prototypeId,
            string name,
            string icon,
            int amount,
            params string[] toolTags)
        {
            Id = id;
            PrototypeId = prototypeId;
            Name = name;
            Icon = icon;
            Amount = amount;
            ToolTags = toolTags ?? new string[0];
        }
    }

    internal sealed class GatheringResourceDefinition
    {
        internal readonly string CollectibleId;
        internal readonly string ResourceName;
        internal readonly int Level;
        internal readonly int Yield;
        internal readonly bool UsesSharedYield;
        internal readonly GatheringGeneratorDefinition[] Generators;

        internal GatheringResourceDefinition(
            string collectibleId,
            string resourceName,
            int level,
            bool usesSharedYield,
            params GatheringGeneratorDefinition[] generators)
        {
            CollectibleId = collectibleId;
            ResourceName = resourceName;
            Level = level;
            UsesSharedYield = usesSharedYield;
            Generators = generators ?? new GatheringGeneratorDefinition[0];

            if (UsesSharedYield)
            {
                Yield = Generators.Length == 0
                    ? 0
                    : Math.Max(0, Generators[0].Amount);
            }
            else
            {
                int total = 0;
                for (int i = 0; i < Generators.Length; i++)
                {
                    total += Math.Max(0, Generators[i].Amount);
                }
                Yield = total;
            }
        }
    }

    // Runtime gathering data restored from the archived Lv.15 Unstable Savanna
    // Sea table. Generator IDs/icons come from the PC client's own
    // generator_client_data; PrototypeId is the actual inventory item created.
    internal static class UnstableSavannaSeaDatabase
    {
        internal const string SourceUrl =
            "https://durango-archive.fandom.com/wiki/Unstable_Savanna_Sea";
        internal const string RegionTemplateId = "ri15sa190710";
        private static readonly object YieldRandomGate = new object();
        private static readonly Random YieldRandom = new Random();

        private static readonly Dictionary<string, GatheringResourceDefinition> Resources =
            CreateResources();

        internal static bool IsSupported(string collectibleId)
        {
            return !string.IsNullOrEmpty(collectibleId) &&
                Resources.ContainsKey(collectibleId);
        }

        internal static bool IsActive(string collectibleId)
        {
            if (!IsSupported(collectibleId))
            {
                return false;
            }

            // Date Palm is the legacy restoration that predates this database
            // and remains available on every region where the game places it.
            if (string.Equals(
                collectibleId,
                "tree_date",
                StringComparison.Ordinal))
            {
                return true;
            }

            // Several natural-object IDs are reused by higher-level islands.
            // Apply the Lv.15 yields/items only to the archived Savanna sea.
            Durango.Logic.Explore.Region region =
                global::GameManager.Region;
            return region != null &&
                string.Equals(
                    region.TemplateId,
                    RegionTemplateId,
                    StringComparison.Ordinal);
        }

        internal static bool IsTamedVegetation(string collectibleId)
        {
            return IsVegetation(collectibleId) &&
                IsTamedRegion();
        }

        internal static bool IsHandled(string collectibleId)
        {
            return IsActive(collectibleId) ||
                IsTamedVegetation(collectibleId);
        }

        private static bool IsVegetation(string collectibleId)
        {
            return !string.IsNullOrEmpty(collectibleId) &&
                (collectibleId.StartsWith(
                    "tree_",
                    StringComparison.Ordinal) ||
                 collectibleId.StartsWith(
                    "bush_",
                    StringComparison.Ordinal) ||
                 collectibleId.StartsWith(
                    "grass_",
                    StringComparison.Ordinal));
        }

        private static bool IsTamedRegion()
        {
            Durango.Logic.Explore.Region region =
                global::GameManager.Region;
            if (region == null ||
                string.IsNullOrEmpty(region.TemplateId))
            {
                return false;
            }

            try
            {
                Type harborApi = HarmonyLib.AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.HarborSailingMap.HarborIslandApi");
                System.Reflection.MethodInfo isTamed =
                    harborApi == null
                        ? null
                        : harborApi.GetMethod(
                            "IsTamedTerrain",
                            System.Reflection.BindingFlags.Static |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic);
                if (isTamed != null)
                {
                    object result = isTamed.Invoke(
                        null,
                        new object[] { region.TemplateId });
                    if (result is bool)
                    {
                        return (bool)result;
                    }
                }
            }
            catch
            {
                // Fall through to the stock Tamed terrain prefix.
            }

            return region.TemplateId.StartsWith(
                "pe10",
                StringComparison.Ordinal);
        }

        internal static int LevelOf(string collectibleId)
        {
            GatheringResourceDefinition resource;
            return Resources.TryGetValue(collectibleId, out resource)
                ? resource.Level
                : 1;
        }

        internal static List<Generator> CreateGenerators(string collectibleId)
        {
            GatheringResourceDefinition resource;
            List<Generator> result = new List<Generator>();
            if (!Resources.TryGetValue(collectibleId, out resource))
            {
                return result;
            }

            if (string.Equals(
                collectibleId,
                LostPackageModernDatabase.CollectibleId,
                StringComparison.Ordinal))
            {
                return LostPackageModernDatabase.CreateGenerators(
                    resource.Level);
            }

            int[] amounts = AllocateGeneratorAmounts(resource);
            for (int i = 0; i < resource.Generators.Length; i++)
            {
                GatheringGeneratorDefinition definition = resource.Generators[i];
                if (amounts[i] <= 0)
                {
                    continue;
                }

                Dictionary<string, int> tools = new Dictionary<string, int>();
                if (definition.ToolTags.Length == 0)
                {
                    tools.Add("bare_hands", 1);
                }
                else
                {
                    for (int j = 0; j < definition.ToolTags.Length; j++)
                    {
                        tools[definition.ToolTags[j]] = 1;
                    }
                }

                result.Add(new Generator
                {
                    Id = definition.Id,
                    Name = definition.Name,
                    Icon = definition.Icon,
                    Amount = amounts[i],
                    Level = resource.Level,
                    // The live service used the standard gathering effort
                    // curve: 2.5 + (level - 1) * 0.25.
                    Effort = 2.5f + (resource.Level - 1) * 0.25f,
                    Duration = 3f,
                    Enabled = true,
                    ToolRequirements = tools
                });
            }
            return result;
        }

        private static int[] AllocateGeneratorAmounts(
            GatheringResourceDefinition resource)
        {
            int count = resource.Generators.Length;
            int[] amounts = new int[count];
            if (!resource.UsesSharedYield)
            {
                for (int i = 0; i < count; i++)
                {
                    amounts[i] = Math.Max(
                        0,
                        resource.Generators[i].Amount);
                }
                return amounts;
            }

            int totalYield = Math.Max(0, resource.Yield);
            if (count == 0 || totalYield == 0)
            {
                return amounts;
            }

            // Yield belongs to the whole natural object, not to every item
            // generator. Give each possible result one unit when the yield
            // permits, then randomly distribute the remainder. When there
            // are more result types than yield (for example a 2-yield fishing
            // point with three possible catches), choose a random subset.
            lock (YieldRandomGate)
            {
                int[] shuffled = new int[count];
                for (int i = 0; i < count; i++)
                {
                    shuffled[i] = i;
                }
                for (int i = count - 1; i > 0; i--)
                {
                    int swapIndex = YieldRandom.Next(0, i + 1);
                    int value = shuffled[i];
                    shuffled[i] = shuffled[swapIndex];
                    shuffled[swapIndex] = value;
                }

                int participating = Math.Min(count, totalYield);
                for (int i = 0; i < participating; i++)
                {
                    amounts[shuffled[i]] = 1;
                }

                int remaining = totalYield - participating;
                while (remaining > 0)
                {
                    int selected = YieldRandom.Next(0, participating);
                    amounts[shuffled[selected]]++;
                    remaining--;
                }
            }

            return amounts;
        }

        internal static List<Generator> CreateTamedRootGenerators()
        {
            return new List<Generator>
            {
                new Generator
                {
                    Id = "root",
                    Name = "Root",
                    Icon = "icon_nat_root_winter",
                    Amount = 1,
                    Level = 10,
                    Effort = 4.75f,
                    Duration = 3f,
                    Enabled = true,
                    ToolRequirements = new Dictionary<string, int>
                    {
                        { "bare_hands", 1 }
                    }
                }
            };
        }

        internal static bool HasOnlyTamedRootGenerator(
            Generator[] generators)
        {
            if (generators == null || generators.Length != 1)
            {
                return false;
            }

            Generator generator = generators[0];
            int bareHands;
            return string.Equals(
                    generator.Id,
                    "root",
                    StringComparison.Ordinal) &&
                string.Equals(
                    generator.Name,
                    "Root",
                    StringComparison.Ordinal) &&
                string.Equals(
                    generator.Icon,
                    "icon_nat_root_winter",
                    StringComparison.Ordinal) &&
                generator.Amount > 0 &&
                generator.Level == 10 &&
                generator.Duration > 0f &&
                generator.Effort > 0f &&
                generator.ToolRequirements != null &&
                generator.ToolRequirements.Count == 1 &&
                generator.ToolRequirements.TryGetValue(
                    "bare_hands",
                    out bareHands) &&
                bareHands >= 1;
        }

        internal static bool HasOnlyExpectedGenerators(
            string collectibleId,
            Generator[] generators)
        {
            GatheringResourceDefinition resource;
            if (!Resources.TryGetValue(collectibleId, out resource))
            {
                return false;
            }
            if (string.Equals(
                collectibleId,
                LostPackageModernDatabase.CollectibleId,
                StringComparison.Ordinal))
            {
                return LostPackageModernDatabase.HasOnlyExpectedGenerators(
                    resource.Level,
                    generators);
            }
            if (generators == null || generators.Length == 0)
            {
                return true;
            }

            if (generators.Length > resource.Generators.Length)
            {
                return false;
            }

            int totalAmount = 0;
            HashSet<string> seenIds = new HashSet<string>(
                StringComparer.Ordinal);
            for (int i = 0; i < generators.Length; i++)
            {
                GatheringGeneratorDefinition definition = null;
                for (int j = 0;
                    j < resource.Generators.Length;
                    j++)
                {
                    if (string.Equals(
                        generators[i].Id,
                        resource.Generators[j].Id,
                        StringComparison.Ordinal))
                    {
                        definition = resource.Generators[j];
                        break;
                    }
                }
                if (definition == null ||
                    !seenIds.Add(generators[i].Id) ||
                    !HasExpectedRuntimeMetadata(
                        resource,
                        definition,
                        generators[i]))
                {
                    return false;
                }
                totalAmount += generators[i].Amount;
            }
            return totalAmount <= resource.Yield;
        }

        private static bool HasExpectedRuntimeMetadata(
            GatheringResourceDefinition resource,
            GatheringGeneratorDefinition definition,
            Generator generator)
        {
            if (generator.Amount <= 0 ||
                (!resource.UsesSharedYield &&
                    generator.Amount > definition.Amount) ||
                generator.Level != resource.Level ||
                generator.Duration <= 0f ||
                generator.Effort <= 0f ||
                string.IsNullOrEmpty(generator.Icon) ||
                generator.ToolRequirements == null)
            {
                return false;
            }

            int expectedToolCount =
                definition.ToolTags.Length == 0
                    ? 1
                    : definition.ToolTags.Length;
            if (generator.ToolRequirements.Count !=
                expectedToolCount)
            {
                return false;
            }

            if (definition.ToolTags.Length == 0)
            {
                int bareHands;
                return generator.ToolRequirements.TryGetValue(
                    "bare_hands",
                    out bareHands) &&
                    bareHands >= 1;
            }

            for (int i = 0;
                i < definition.ToolTags.Length;
                i++)
            {
                int requiredLevel;
                if (!generator.ToolRequirements.TryGetValue(
                    definition.ToolTags[i],
                    out requiredLevel) ||
                    requiredLevel < 1)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool IsRewardGenerator(string generatorId)
        {
            if (string.IsNullOrEmpty(generatorId))
            {
                return false;
            }

            if (LostPackageModernDatabase.IsGenerator(generatorId))
            {
                return true;
            }

            foreach (GatheringResourceDefinition resource in Resources.Values)
            {
                for (int i = 0; i < resource.Generators.Length; i++)
                {
                    if (string.Equals(
                        resource.Generators[i].Id,
                        generatorId,
                        StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal static string ResolvePrototypeId(
            string collectibleId,
            string generatorId)
        {
            string lostPackagePrototypeId;
            if (string.Equals(
                    collectibleId,
                    LostPackageModernDatabase.CollectibleId,
                    StringComparison.Ordinal) &&
                LostPackageModernDatabase.TryResolvePrototypeId(
                    generatorId,
                    out lostPackagePrototypeId))
            {
                return lostPackagePrototypeId;
            }

            GatheringGeneratorDefinition definition =
                FindGenerator(collectibleId, generatorId);
            if (definition == null)
            {
                return generatorId;
            }
            return definition.PrototypeId;
        }

        internal static string CollectibleSizeOf(string collectibleId)
        {
            if (string.IsNullOrEmpty(collectibleId))
            {
                return "low";
            }

            // Collectible.Size is the character's gathering reach height, not
            // the inventory footprint of the generated item. Tall trees use
            // the high hand/knife animation, standing grass uses middle, and
            // ground-level or short resources use low.
            if (collectibleId.StartsWith(
                    "tree_",
                    StringComparison.Ordinal))
            {
                return "high";
            }

            if (string.Equals(
                    collectibleId,
                    "grass_elephant",
                    StringComparison.Ordinal) ||
                string.Equals(
                    collectibleId,
                    "grass_reed",
                    StringComparison.Ordinal) ||
                string.Equals(
                    collectibleId,
                    "grass_reed_lake",
                    StringComparison.Ordinal))
            {
                return "middle";
            }

            return "low";
        }

        internal static string GeneratorIcon(
            string collectibleId,
            string generatorId)
        {
            if (string.Equals(
                collectibleId,
                LostPackageModernDatabase.CollectibleId,
                StringComparison.Ordinal))
            {
                return LostPackageModernDatabase.GeneratorIcon(
                    generatorId,
                    LevelOf(collectibleId));
            }

            if (string.Equals(
                    generatorId,
                    "root",
                    StringComparison.Ordinal) &&
                IsTamedVegetation(collectibleId))
            {
                return "icon_nat_root_winter";
            }

            GatheringGeneratorDefinition definition =
                FindGenerator(collectibleId, generatorId);
            return definition == null ? null : definition.Icon;
        }

        internal static string CriticalGenerator(
            string collectibleId,
            Generator[] generators)
        {
            // Only the synthetic Tamed-Island Root generator is critical.
            // Tamed touch handling assigns "root" explicitly; every restored
            // Unstable resource must use the normal, non-critical presentation.
            return string.Empty;
        }

        private static GatheringGeneratorDefinition FindGenerator(
            string collectibleId,
            string generatorId)
        {
            GatheringResourceDefinition resource;
            if (!Resources.TryGetValue(collectibleId, out resource))
            {
                return null;
            }
            for (int i = 0; i < resource.Generators.Length; i++)
            {
                if (string.Equals(
                    resource.Generators[i].Id,
                    generatorId,
                    StringComparison.Ordinal))
                {
                    return resource.Generators[i];
                }
            }
            return null;
        }

        private static Dictionary<string, GatheringResourceDefinition> CreateResources()
        {
            GatheringResourceDefinition[] records =
            {
                Resource("egg", "Animal Egg", 15,
                    Gen("egg", "egg", "Egg", "icon_nat_egg_dino", 3)),

                Resource("tree_baobab", "Baobab Tree", 15,
                    Gen("wood_bough_broadleaved", "wood_bough_broadleaved",
                        "Broad-leaved Branch", "icon_nat_wood_branch", 20, "knife"),
                    Gen("leaf_small", "leaf_small",
                        "Leaf", "icon_nat_leaf", 20),
                    Gen("wood_log_baobab", "wood_log",
                        "Log", "icon_nat_wood_log", 20,
                        "axe_onehand_tool", "axe_twohand_tool", "saw")),

                Resource("rock", "Boulder", 15,
                    Gen("rock", "stone_big",
                        "Boulder", "icon_nat_mine_rock", 10,
                        "hammer_twohand", "pickaxe"),
                    Gen("stone", "stone",
                        "Pebble", "icon_nat_mine_stone", 10)),

                Resource("tree_cabbagepalm", "Cabbage Palm Tree", 15,
                    Gen("wood_bough", "wood_bough",
                        "Branch", "icon_nat_wood_branch", 12, "knife"),
                    Gen("coconut", "coconut",
                        "Coconut", "icon_nat_fruit_coconut", 12),
                    Gen("crab_coconut", "crab_coconut",
                        "Coconut Crab", "icon_nat_fish_cococrab", 12),
                    Gen("leaf_large", "leaf_large",
                        "Large Leaf", "icon_nat_leaf_big", 12, "knife"),
                    Gen("wood_log", "wood_log",
                        "Log", "icon_nat_wood_log", 12,
                        "axe_onehand_tool", "axe_twohand_tool", "saw")),

                Resource("seashell_1", "Clam", 15,
                    Gen("clam", "clam",
                        "Clam Meat", "icon_nat_fruit_mango", 6),
                    Gen("clam_shell", "clam_shell",
                        "Seashell", "icon_nat_clam", 6)),

                Resource("grass_wiregrass", "Crabgrass", 15,
                    Gen("reed", "stem",
                        "Stalk", "icon_nat_fiber_reed", 5, "knife")),

                Resource("grass_elephant", "Elephant Grass", 15,
                    Gen("reed", "stem",
                        "Stalk", "icon_nat_fiber_reed", 5, "knife")),

                Resource("grass_wiregrass_flowered", "Flowered Crabgrass", 15,
                    Gen("wiregrass_flower", "flower",
                        "Crabgrass Flower", "icon_nat_flower2", 5, "knife"),
                    Gen("reed", "stem",
                        "Stalk", "icon_nat_fiber_reed", 5, "knife")),

                Resource("bush_lavender", "Lavender", 15,
                    Gen("lavender_flower", "flower",
                        "Lavender", "icon_nat_farm_lavender", 7, "knife"),
                    Gen("leaf_small", "leaf_small",
                        "Leaf", "icon_nat_leaf", 7)),

                Resource("paperbox_normal_mixed", "Lost Package", 15,
                    Gen("random_modern_item", "$random_modern",
                        "Random Modern Item", "icon_map_poi_box", 1)),

                Resource("tree_morichepalm", "Moriche Palm Tree", 15,
                    Gen("wood_bough", "wood_bough",
                        "Branch", "icon_nat_wood_branch", 12, "knife"),
                    Gen("coconut", "coconut",
                        "Coconut", "icon_nat_fruit_coconut", 12),
                    Gen("leaf_large", "leaf_large",
                        "Large Leaf", "icon_nat_leaf_big", 12, "knife"),
                    Gen("wood_log", "wood_log",
                        "Log", "icon_nat_wood_log", 12,
                        "axe_onehand_tool", "axe_twohand_tool", "saw")),

                Resource("mud_swamp", "Mud Pit", 15,
                    Gen("clay", "clay",
                        "Mud", "icon_nat_clay", 3, "shovel")),

                Resource("stone", "Pebble", 15,
                    Gen("stone", "stone",
                        "Pebble", "icon_nat_mine_stone", 5)),

                Resource("grass_reed", "Reed", 15,
                    Gen("reed", "stem",
                        "Stalk", "icon_nat_fiber_reed", 5, "knife")),

                Resource("grass_reed_lake", "Reed", 15,
                    Gen("reed", "stem",
                        "Stalk", "icon_nat_fiber_reed", 5, "knife")),

                Resource("tree_screwpine", "Screw Pine Tree", 15,
                    Gen("wood_bough", "wood_bough",
                        "Branch", "icon_nat_wood_branch", 20, "knife"),
                    Gen("fruit", "fruit",
                        "Fruit", "tag_material_fruit", 20),
                    Gen("leaf_small", "leaf_small",
                        "Leaf", "icon_nat_leaf", 20),
                    Gen("wood_log", "wood_log",
                        "Log", "icon_nat_wood_log", 20,
                        "axe_onehand_tool", "axe_twohand_tool", "saw")),

                Resource("tree_birdnest", "Seedbed Tree", 15,
                    Gen("wood_bough", "wood_bough",
                        "Branch", "icon_nat_wood_branch", 12, "knife"),
                    Gen("worm", "worm",
                        "Caterpillar", "icon_nat_larva", 12)),

                Resource("harpoon_fishing_point_river", "Shoal of Fish", 15,
                    Gen("fish_big_harpoon", "fish_big",
                        "Big Fish", "icon_nat_fish_big", 2, "harpoon"),
                    Gen("fish_harpoon", "fish",
                        "Fish", "icon_nat_fish", 2, "harpoon"),
                    Gen("fish_vynil_harpoon", "fish_vynil",
                        "Plastic Bag", "warp_plasticbag_cookie", 2, "harpoon")),

                Resource("harpoon_fishing_point_ocean", "Shoal of Fish", 15,
                    Gen("fish_big_harpoon", "fish_big",
                        "Big Fish", "icon_nat_fish_big", 2, "harpoon"),
                    Gen("fish_harpoon", "fish",
                        "Fish", "icon_nat_fish", 2, "harpoon"),
                    Gen("fish_vynil_harpoon", "fish_vynil",
                        "Plastic Bag", "warp_plasticbag_cookie", 2, "harpoon")),

                Resource("harpoon_fishing_point_lake", "Shoal of Fish", 15,
                    Gen("fish_big_harpoon", "fish_big",
                        "Big Fish", "icon_nat_fish_big", 2, "harpoon"),
                    Gen("fish_harpoon", "fish",
                        "Fish", "icon_nat_fish", 2, "harpoon"),
                    Gen("fish_vynil_harpoon", "fish_vynil",
                        "Plastic Bag", "warp_plasticbag_cookie", 2, "harpoon")),

                Resource("dump_s", "Small Animal Dropping", 15,
                    Gen("dump", "dump",
                        "Animal Droppings", "icon_nat_poo", 2, "shovel")),

                // Preserve the existing Date Palm restoration outside the Lv.15
                // Savanna database.
                FixedResource("tree_date", "Date Palm", 1,
                    Gen("date", "fruit",
                        "Date", "icon_nat_fruit_date", 5),
                    Gen("wood_log", "wood_log",
                        "Log", "icon_nat_wood_log", 2,
                        "axe_onehand_tool", "axe_twohand_tool", "saw"),
                    Gen("leaf_large", "leaf_large",
                        "Large Leaf", "icon_nat_leaf_big", 2, "knife"))
            };

            Dictionary<string, GatheringResourceDefinition> result =
                new Dictionary<string, GatheringResourceDefinition>(
                    StringComparer.Ordinal);
            for (int i = 0; i < records.Length; i++)
            {
                result[records[i].CollectibleId] = records[i];
            }
            return result;
        }

        private static GatheringResourceDefinition Resource(
            string collectibleId,
            string resourceName,
            int level,
            params GatheringGeneratorDefinition[] generators)
        {
            return new GatheringResourceDefinition(
                collectibleId,
                resourceName,
                level,
                true,
                generators);
        }

        private static GatheringResourceDefinition FixedResource(
            string collectibleId,
            string resourceName,
            int level,
            params GatheringGeneratorDefinition[] generators)
        {
            return new GatheringResourceDefinition(
                collectibleId,
                resourceName,
                level,
                false,
                generators);
        }

        private static GatheringGeneratorDefinition Gen(
            string id,
            string prototypeId,
            string name,
            string icon,
            int amount,
            params string[] toolTags)
        {
            return new GatheringGeneratorDefinition(
                id,
                prototypeId,
                name,
                icon,
                amount,
                toolTags);
        }
    }
}
