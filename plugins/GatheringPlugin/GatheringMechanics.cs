using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Durango.Offline;
using Durango.Logic;
using HarmonyLib;
using Messages;
using Shared.Ability;
using Shared.Building;
using Shared.Etc;
using Shared.Item;
using Shared.Skill;

namespace BaoX.DurangoOriginal.GatheringMod
{
    internal sealed class GatheringAttempt
    {
        internal bool ToolValid;
        internal bool SkillValid;
        internal string RequiredSkillId;
        internal int RequiredSkillLevel;
        internal Dictionary<string, int> RequiredTools;
        internal Item? Tool;
        internal int ToolPerformance;
        internal int CategoryLevel;
        internal float Ability;
        internal float GatheringAbility;
        internal float Duration;
        internal float SuccessRatio;
        internal float GreatSuccessRatio;
        internal float RandomAttributeRatio;
        internal Result Result;
        internal ActionInfo ActionInfo;
    }

    internal static class GatheringMechanics
    {
        private static readonly object RandomGate = new object();
        private static readonly Random Random = new Random();
        private static readonly Dictionary<Durango.Offline.Player, PlayerContext> Contexts =
            new Dictionary<Durango.Offline.Player, PlayerContext>();

        private static readonly string[][] WoodAttributes =
        {
            new string[] { "weight_light", "weight_heavy" },
            new string[] { "hardness_hard", "hardness_soft" },
            new string[] { "inside_full", "inside_empty" },
            new string[] { "surface_smooth", "surface_rough" }
        };

        private static readonly string[][] FiberAttributes =
        {
            new string[] { "fiber_tough", "elasticity_low" },
            new string[] { "elasticity_high", "elasticity_low" },
            new string[] { "surface_smooth", "surface_rough" },
            new string[] { "weight_light", "weight_heavy" }
        };

        private static readonly string[][] MineralAttributes =
        {
            new string[] { "density_high", "density_low" },
            new string[] { "hardness_hard", "hardness_soft" },
            new string[] { "surface_smooth", "surface_rough" }
        };

        private static readonly string[][] FoodAttributes =
        {
            new string[] { "fresh", "rotten" },
            new string[] { "inside_full", "inside_empty" },
            new string[] { "surface_softness", "surface_rough" }
        };

        internal static void RegisterPlayer(
            Durango.Offline.Player player,
            PlayerContext context)
        {
            if (player != null && context != null)
            {
                Contexts[player] = context;
            }
        }

        internal static void SavePlayerAfterGathering(
            Durango.Offline.Player player)
        {
            PlayerContext context;
            if (player == null ||
                !Contexts.TryGetValue(player, out context) ||
                context == null)
            {
                GatheringPlugin.Log.LogWarning(
                    "Immediate gathering player save skipped: context unavailable.");
                return;
            }

            try
            {
                context.Save();
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError(
                    "Immediate gathering player save failed: " + ex);
            }
        }

        internal static Item? ReduceToolDurability(
            Durango.Offline.Player player,
            Item? selectedTool,
            float amount)
        {
            if (player == null ||
                selectedTool == null ||
                amount <= 0f)
            {
                return null;
            }

            PlayerContext context;
            if (!Contexts.TryGetValue(player, out context) ||
                context == null ||
                context.InventoryItems == null)
            {
                return null;
            }

            string toolId = selectedTool.Value.Id;
            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                Item tool = context.InventoryItems[i];
                if (!string.Equals(
                    tool.Id,
                    toolId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (!IsWeaponOrTool(tool) || tool.Durability == null)
                {
                    return null;
                }

                float maximum = tool.Durability.Max();
                float minimum = tool.Durability.Min();
                float current = tool.Durability.Get();
                float next = Math.Max(minimum, current - amount);
                if (next >= current)
                {
                    return null;
                }

                tool.Durability = new Gauge(
                    maximum,
                    minimum,
                    new GaugeNode[]
                    {
                        new GaugeNode(0.0, next)
                    });
                context.InventoryItems[i] = tool;
                context.Save();

                GatheringPlugin.Log.LogInfo(
                    "[Durability] tool=" + tool.Id +
                    " value=" + next.ToString("0.0") +
                    "/" + maximum.ToString("0.0"));
                return tool;
            }

            return null;
        }

        private static bool IsWeaponOrTool(Item item)
        {
            if (string.IsNullOrEmpty(item.Prototype))
            {
                return false;
            }

            Yaml.Prototype prototype =
                Yaml.PrototypeYaml.GetItemPrototype(
                    item.Prototype.Replace(".", "_"),
                    Math.Max(1, item.Level));
            return prototype != null &&
                string.Equals(
                    prototype.Category,
                    "weapon/tool",
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static GatheringAttempt CreateAttempt(
            Durango.Offline.Player player,
            string collectibleId,
            Generator generator,
            string toolItemId)
        {
            GatheringAttempt attempt = new GatheringAttempt();
            attempt.RequiredTools = generator.ToolRequirements ??
                new Dictionary<string, int>();

            PlayerContext context;
            Contexts.TryGetValue(player, out context);

            attempt.ToolValid = ValidateTool(
                context,
                toolItemId,
                attempt.RequiredTools,
                out attempt.Tool,
                out attempt.ToolPerformance);

            ResolveSkillRequirement(
                collectibleId,
                generator.Id,
                out attempt.RequiredSkillId,
                out attempt.RequiredSkillLevel);

            bool skillSystemActive;
            attempt.SkillValid = SkillBridge.HasSkill(
                player,
                attempt.RequiredSkillId,
                attempt.RequiredSkillLevel,
                out skillSystemActive);

            Statistics statistics;
            bool hasStatistics = SkillBridge.TryGetStatistics(
                player,
                out statistics);
            attempt.CategoryLevel = SkillBridge.GetCategoryLevel(
                player,
                context);

            bool mining = IsMining(generator.Id, collectibleId);
            Derived relatedAbility = mining
                ? Derived.Mining
                : Derived.Gathering;
            attempt.GatheringAbility = ReadAbility(
                statistics,
                hasStatistics,
                Derived.Gathering,
                context,
                attempt.CategoryLevel);
            attempt.Ability = relatedAbility == Derived.Gathering
                ? attempt.GatheringAbility
                : ReadAbility(
                    statistics,
                    hasStatistics,
                    relatedAbility,
                    context,
                    attempt.CategoryLevel);

            float timeReduction = ReadModifier(
                statistics,
                hasStatistics,
                "gathering_time_reduction");
            timeReduction += ReadEquippedTagModifier(
                context,
                "gathering_time_reduction",
                0.005f);

            float fatigueRatio = ReadGaugeRatio(context, "fatigue");
            float abilityAdvantage = attempt.Ability - generator.Level;
            float abilityReduction = Clamp(
                abilityAdvantage * 0.0075f,
                -0.20f,
                0.35f);
            float toolReduction = attempt.Tool == null
                ? 0f
                : Clamp(
                    (attempt.ToolPerformance - generator.Level) * 0.004f,
                    0f,
                    0.15f);
            float categoryReduction = Clamp(
                (attempt.CategoryLevel - generator.Level) * 0.0025f,
                -0.10f,
                0.12f);

            float baseDuration = generator.Duration > 0f
                ? generator.Duration
                : 3f;
            attempt.Duration = Clamp(
                baseDuration *
                (1f - abilityReduction) *
                (1f - toolReduction) *
                (1f - categoryReduction) *
                (1f - Clamp(timeReduction, 0f, 0.60f)) *
                (1f + Clamp(fatigueRatio, 0f, 1f) * 0.30f),
                0.75f,
                12f);

            // This is the success formula preserved in constants.json:
            // 1 - (max(0, difficulty - ability - correction) / 100)^2.
            float deficit = Math.Max(0f, generator.Effort - attempt.Ability);
            attempt.SuccessRatio = Clamp(
                1f - (deficit / 100f) * (deficit / 100f),
                0.05f,
                0.995f);

            float greatBonus =
                ReadModifier(
                    statistics,
                    hasStatistics,
                    "great_success_plus_collect") +
                ReadToolTagModifier(
                    attempt.Tool,
                    "collector_delicate",
                    0.05f);
            attempt.GreatSuccessRatio = Clamp(
                0.10f +
                Math.Max(0f, abilityAdvantage) * 0.002f +
                greatBonus,
                0.02f,
                0.35f);

            float randomTagBonus =
                ReadModifier(
                    statistics,
                    hasStatistics,
                    "random_tag_mul") +
                ReadToolTagModifier(
                    attempt.Tool,
                    "secret_collection",
                    1f);
            attempt.RandomAttributeRatio = Clamp(
                0.05f *
                (1f + Math.Max(0f, randomTagBonus)) +
                Math.Max(0f, abilityAdvantage) * 0.001f,
                0.02f,
                0.50f);

            float resultRoll = NextFloat();
            if (resultRoll > attempt.SuccessRatio)
            {
                attempt.Result = NextFloat() < 0.20f
                    ? Result.BigFailure
                    : Result.Failure;
            }
            else
            {
                attempt.Result = NextFloat() < attempt.GreatSuccessRatio
                    ? Result.GreatSuccess
                    : Result.Success;
            }

            attempt.ActionInfo = new ActionInfo
            {
                ActionLevel = generator.Level,
                PotentialLevel = Math.Max(
                    generator.Level,
                    (int)Math.Floor(attempt.Ability)),
                RelatedCategory = Category.Gathering,
                SuccessRatio = attempt.SuccessRatio,
                RelatedAbility = relatedAbility
            };

            return attempt;
        }

        internal static Item ApplyGatheredItemData(
            Item item,
            string collectibleId,
            string generatorId,
            Result result,
            float randomAttributeRatio,
            int categoryLevel)
        {
            item.CollectibleId = collectibleId;
            item.GeneratorId = generatorId;
            item.OriginalLevel = item.Level;
            item.ModifiableCount = Math.Max(1, 5 + categoryLevel / 10);

            string attribute = null;
            if (result == Result.Failure)
            {
                attribute = SelectAttribute(generatorId, false);
            }
            else if (result == Result.GreatSuccess ||
                (result == Result.Success &&
                    NextFloat() < randomAttributeRatio))
            {
                attribute = SelectAttribute(generatorId, true);
            }

            if (!string.IsNullOrEmpty(attribute))
            {
                AddAttribute(ref item, attribute);
            }
            return item;
        }

        internal static Item PrepareLostPackageItem(
            Item item,
            string collectibleId)
        {
            if (!string.Equals(
                collectibleId,
                LostPackageModernDatabase.CollectibleId,
                StringComparison.Ordinal))
            {
                return item;
            }

            Yaml.Prototype prototype =
                Yaml.PrototypeYaml.GetItemPrototype(
                    item.Prototype,
                    Math.Max(1, item.Level)) ??
                Yaml.PrototypeYaml.GetItemPrototype(item.Prototype);
            if (prototype != null &&
                string.Equals(
                    prototype.Category,
                    "weapon/tool",
                    StringComparison.OrdinalIgnoreCase))
            {
                float durability = Math.Max(1, item.Level) * 5f;
                item.Durability = new Gauge(
                    durability,
                    0f,
                    new GaugeNode[]
                    {
                        new GaugeNode(0.0, durability)
                    });
            }

            if (string.IsNullOrEmpty(item.Prototype) ||
                !item.Prototype.StartsWith(
                    "capsulated_",
                    StringComparison.Ordinal))
            {
                return item;
            }

            try
            {
                string blueprintId = item.Prototype.Substring(
                    "capsulated_".Length);
                Building.Blueprint blueprint =
                    GameSystem<RecipeSystem>.Instance().GetBlueprint(
                        blueprintId);
                if (blueprint == null)
                {
                    return item;
                }

                string artifactId = Guid.NewGuid().ToString();
                ArtifactDisplay display = blueprint.GetDefaultDisplay();
                display.EntityId = artifactId;

                ArtifactState state = default(ArtifactState);
                state.EntityId = artifactId;
                state.BuildingState = BuildingState.Completed;
                state.Durability = new Gauge(
                    1f,
                    0f,
                    new GaugeNode[]
                    {
                        new GaugeNode(0.0, 1f)
                    });
                state.Level = (byte)Math.Min(
                    255,
                    Math.Max(1, item.Level));
                state.MaxHealth = 1f;

                ArtifactCapsule capsule = default(ArtifactCapsule);
                capsule.EntityId = artifactId;
                capsule.BlueprintId = blueprintId;
                capsule.ArtifactLevel = Math.Max(1, item.Level);
                capsule.Tags = item.Tags ?? new Messages.Tag[0];
                capsule.Performance = item.Performance ??
                    new Performance[0];
                capsule.Display = display;
                capsule.State = state;
                capsule.LookNames = new Dictionary<string, string>();
                capsule.OccupySize = new Point2?(blueprint.Size);

                item.Ext = capsule;
                item.Icon = blueprint.ArtifactIcon;
                item.Name = blueprint.Name;
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Lost Package furniture capsule failed: " +
                    ex.Message);
            }
            return item;
        }

        internal static void AwardGatheringExperience(
            Result result,
            int gatheredItemLevel,
            float gatheringAbility)
        {
            if (result == Result.BigFailure || result == Result.Invalid)
            {
                return;
            }

            double categoryXp =
                Math.Max(1, gatheredItemLevel) / 100.0 +
                Math.Max(0.0f, gatheringAbility) / 100.0;
            SkillBridge.AddGatheringExperience(categoryXp);
        }

        internal static bool ClientHasRequiredSkill(
            string collectibleId,
            string generatorId,
            out SkillNeeded needed)
        {
            string skillId;
            int requiredLevel;
            ResolveSkillRequirement(
                collectibleId,
                generatorId,
                out skillId,
                out requiredLevel);

            needed = new SkillNeeded
            {
                SkillId = skillId,
                SubId = "__base__",
                Level = requiredLevel
            };

            if (string.IsNullOrEmpty(skillId) ||
                requiredLevel <= 0 ||
                !GameSystem<Durango.Logic.SkillSystem>.HasInstance())
            {
                return true;
            }

            Durango.Logic.Skill.Skill skill =
                GameSystem<Durango.Logic.SkillSystem>
                    .Instance()
                    .FindSkill(skillId, "__base__");
            return skill != null && skill.Level >= requiredLevel;
        }

        internal static void ApplyGeneratorSkillAvailability(
            Durango.Offline.Player player,
            string collectibleId,
            List<Generator> generators)
        {
            if (player == null || generators == null)
            {
                return;
            }

            for (int i = 0; i < generators.Count; i++)
            {
                Generator generator = generators[i];
                string skillId;
                int requiredLevel;
                ResolveSkillRequirement(
                    collectibleId,
                    generator.Id,
                    out skillId,
                    out requiredLevel);

                bool skillSystemActive;
                generator.Enabled = SkillBridge.HasSkill(
                    player,
                    skillId,
                    requiredLevel,
                    out skillSystemActive);
                generators[i] = generator;
            }
        }

        private static bool ValidateTool(
            PlayerContext context,
            string toolItemId,
            Dictionary<string, int> requirements,
            out Item? selectedTool,
            out int performance)
        {
            selectedTool = null;
            performance = 0;

            int bareHands = 1;
            bool acceptsBareHands = requirements == null ||
                requirements.Count == 0 ||
                requirements.TryGetValue("bare_hands", out bareHands);

            if (string.IsNullOrEmpty(toolItemId))
            {
                if (acceptsBareHands)
                {
                    performance = Math.Max(1, bareHands);
                    return true;
                }
                return false;
            }

            if (context == null || context.InventoryItems == null)
            {
                return false;
            }

            Item? found = null;
            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                if (string.Equals(
                    context.InventoryItems[i].Id,
                    toolItemId,
                    StringComparison.Ordinal))
                {
                    found = context.InventoryItems[i];
                    break;
                }
            }
            if (found == null)
            {
                return false;
            }

            Item tool = found.Value;
            if (tool.Tags == null)
            {
                return false;
            }

            for (int i = 0; i < tool.Tags.Length; i++)
            {
                int required;
                if (requirements.TryGetValue(
                    tool.Tags[i].Id,
                    out required) &&
                    tool.Tags[i].Level >= required)
                {
                    selectedTool = tool;
                    performance = tool.Tags[i].Level;
                    return true;
                }
            }
            return false;
        }

        private static void ResolveSkillRequirement(
            string collectibleId,
            string generatorId,
            out string skillId,
            out int level)
        {
            skillId = null;
            level = 0;
            string id = generatorId ?? string.Empty;

            if (id.StartsWith("wood_log", StringComparison.Ordinal))
            {
                skillId = "forester";
                level = string.Equals(
                    collectibleId,
                    "tree_baobab",
                    StringComparison.Ordinal) ? 2 : 1;
            }
            else if (id.StartsWith("wood_bough", StringComparison.Ordinal))
            {
                skillId = "bough";
                level = 1;
            }
            else if (id.StartsWith("leaf_", StringComparison.Ordinal))
            {
                skillId = "leaf";
                level = 1;
            }
            else if (id == "fruit" || id == "coconut" || id == "date")
            {
                skillId = "fruit";
                level = 1;
            }
            else if (id == "rock")
            {
                skillId = "stone";
                level = 2;
            }
            else if (id == "stone")
            {
                skillId = "stone";
                level = 1;
            }
            else if (id == "clay" || id == "dump")
            {
                skillId = "clay";
                level = 1;
            }
            else if (id == "reed")
            {
                skillId = "stem";
                level = 1;
            }
            else if (id.IndexOf(
                "flower",
                StringComparison.Ordinal) >= 0)
            {
                skillId = "herbalism";
                level = 1;
            }
        }

        private static bool IsMining(
            string generatorId,
            string collectibleId)
        {
            return generatorId == "rock" ||
                generatorId == "stone" ||
                collectibleId == "rock" ||
                collectibleId == "stone";
        }

        private static float ReadAbility(
            Statistics statistics,
            bool hasStatistics,
            Derived ability,
            PlayerContext context,
            int categoryLevel)
        {
            float value;
            if (hasStatistics &&
                statistics.DerivedsAbilities != null &&
                statistics.DerivedsAbilities.TryGetValue(
                    ability,
                    out value))
            {
                return Math.Max(1f, value);
            }

            int playerLevel = context == null ||
                context.PlayerInfo == null
                ? categoryLevel
                : Math.Max(1, context.PlayerInfo.PlayerLevel);
            return 5f + playerLevel * 0.66f;
        }

        private static float ReadModifier(
            Statistics statistics,
            bool hasStatistics,
            string id)
        {
            float value;
            return hasStatistics &&
                statistics.Modifiers != null &&
                statistics.Modifiers.TryGetValue(id, out value)
                ? value
                : 0f;
        }

        private static float ReadGaugeRatio(
            PlayerContext context,
            string id)
        {
            if (context == null ||
                context.AppearPlayer.Survival.Gauges == null)
            {
                return 0f;
            }

            Gauge gauge;
            if (!context.AppearPlayer.Survival.Gauges.TryGetValue(
                id,
                out gauge) ||
                gauge == null)
            {
                return 0f;
            }
            return gauge.Ratio();
        }

        private static float ReadEquippedTagModifier(
            PlayerContext context,
            string tagId,
            float perLevel)
        {
            if (context == null ||
                context.EquippedItems == null ||
                context.InventoryItems == null)
            {
                return 0f;
            }

            float result = 0f;
            foreach (string itemId in context.EquippedItems.Values)
            {
                for (int i = 0; i < context.InventoryItems.Count; i++)
                {
                    Item item = context.InventoryItems[i];
                    if (!string.Equals(
                        item.Id,
                        itemId,
                        StringComparison.Ordinal) ||
                        item.Tags == null)
                    {
                        continue;
                    }
                    for (int j = 0; j < item.Tags.Length; j++)
                    {
                        if (string.Equals(
                            item.Tags[j].Id,
                            tagId,
                            StringComparison.Ordinal))
                        {
                            result += item.Tags[j].Level * perLevel;
                        }
                    }
                }
            }
            return result;
        }

        private static float ReadToolTagModifier(
            Item? tool,
            string tagId,
            float value)
        {
            if (tool == null || tool.Value.Tags == null)
            {
                return 0f;
            }
            for (int i = 0; i < tool.Value.Tags.Length; i++)
            {
                if (string.Equals(
                    tool.Value.Tags[i].Id,
                    tagId,
                    StringComparison.Ordinal))
                {
                    return value;
                }
            }
            return 0f;
        }

        private static string SelectAttribute(
            string generatorId,
            bool good)
        {
            string id = generatorId ?? string.Empty;
            string[][] pool;
            if (id.StartsWith("wood_", StringComparison.Ordinal))
            {
                pool = WoodAttributes;
            }
            else if (id == "reed" ||
                id.StartsWith("leaf_", StringComparison.Ordinal) ||
                id.IndexOf("flower", StringComparison.Ordinal) >= 0)
            {
                pool = FiberAttributes;
            }
            else if (id == "rock" ||
                id == "stone" ||
                id == "clay")
            {
                pool = MineralAttributes;
            }
            else if (id == "random_modern_item" ||
                id.StartsWith(
                    LostPackageModernDatabase.GeneratorPrefix,
                    StringComparison.Ordinal))
            {
                return null;
            }
            else
            {
                pool = FoodAttributes;
            }

            int index;
            lock (RandomGate)
            {
                index = Random.Next(0, pool.Length);
            }
            return pool[index][good ? 0 : 1];
        }

        private static void AddAttribute(
            ref Item item,
            string attribute)
        {
            List<Messages.Tag> tags = item.Tags == null
                ? new List<Messages.Tag>()
                : new List<Messages.Tag>(item.Tags);
            for (int i = tags.Count - 1; i >= 0; i--)
            {
                if (string.Equals(
                    tags[i].Id,
                    attribute,
                    StringComparison.Ordinal))
                {
                    tags.RemoveAt(i);
                }
            }
            tags.Add(new Messages.Tag
            {
                Id = attribute,
                Level = Math.Max(1, item.Level)
            });
            item.Tags = tags.ToArray();

            List<Messages.Tag> modifications =
                item.TagModifications == null
                ? new List<Messages.Tag>()
                : new List<Messages.Tag>(item.TagModifications);
            modifications.Add(new Messages.Tag
            {
                Id = attribute,
                Level = 0
            });
            item.TagModifications = modifications.ToArray();
        }

        private static float NextFloat()
        {
            lock (RandomGate)
            {
                return (float)Random.NextDouble();
            }
        }

        private static float Clamp(
            float value,
            float min,
            float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static class SkillBridge
        {
            private static bool _warnedMissingApi;

            internal static int GetCategoryLevel(
                Durango.Offline.Player player,
                PlayerContext context)
            {
                object state = GetState(player);
                if (state != null)
                {
                    MethodInfo method = state.GetType().GetMethod(
                        "GetCategoryLevel",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                    if (method != null)
                    {
                        try
                        {
                            return Math.Max(
                                1,
                                (int)method.Invoke(
                                    state,
                                    new object[] { Category.Gathering }));
                        }
                        catch {}
                    }
                }

                return context == null ||
                    context.PlayerInfo == null
                    ? 1
                    : Math.Max(1, context.PlayerInfo.PlayerLevel);
            }

            internal static bool HasSkill(
                Durango.Offline.Player player,
                string skillId,
                int requiredLevel,
                out bool skillSystemActive)
            {
                skillSystemActive = false;
                if (string.IsNullOrEmpty(skillId) ||
                    requiredLevel <= 0)
                {
                    return true;
                }

                object state = GetState(player);
                if (state == null)
                {
                    // SkillSystemPlugin is optional; do not hard-lock gathering
                    // when it is not installed.
                    return true;
                }
                skillSystemActive = true;

                MethodInfo method = state.GetType().GetMethod(
                    "CreateMessage",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (method == null)
                {
                    return true;
                }

                try
                {
                    Skills skills = (Skills)method.Invoke(state, null);
                    if (skills.SkillList == null)
                    {
                        return false;
                    }
                    for (int i = 0; i < skills.SkillList.Length; i++)
                    {
                        SkillBundle bundle = skills.SkillList[i];
                        if (!string.Equals(
                            bundle.SkillId,
                            skillId,
                            StringComparison.Ordinal) ||
                            bundle.Levels == null)
                        {
                            continue;
                        }
                        int learned;
                        return bundle.Levels.TryGetValue(
                            "__base__",
                            out learned) &&
                            learned >= requiredLevel;
                    }
                    return false;
                }
                catch
                {
                    return true;
                }
            }

            internal static bool TryGetStatistics(
                Durango.Offline.Player player,
                out Statistics statistics)
            {
                statistics = default(Statistics);
                object state = GetState(player);
                if (state == null)
                {
                    return false;
                }

                Type calculator = AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.SkillSystemMod.OfflineStatisticsCalculator");
                MethodInfo build = calculator == null
                    ? null
                    : calculator.GetMethod(
                        "Build",
                        BindingFlags.Static |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                if (build == null)
                {
                    return false;
                }

                try
                {
                    statistics = (Statistics)build.Invoke(
                        null,
                        new object[] { state });
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            internal static void AddGatheringExperience(double amount)
            {
                try
                {
                    Type api = AccessTools.TypeByName(
                        "BaoX.DurangoOriginal.SkillSystemMod.SkillSystemApi");
                    MethodInfo method = api == null
                        ? null
                        : api.GetMethod(
                            "AddCategoryExperienceFromGameplay",
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                    if (method == null)
                    {
                        WarnMissingApi();
                        return;
                    }

                    object[] args = new object[]
                    {
                        "gathering",
                        Math.Max(0.000001, amount),
                        null
                    };
                    bool ok = (bool)method.Invoke(null, args);
                    if (!ok && args[2] != null)
                    {
                        GatheringPlugin.Log.LogWarning(
                            "Gathering skill XP rejected: " + args[2]);
                    }
                }
                catch (Exception ex)
                {
                    GatheringPlugin.Log.LogWarning(
                        "Gathering skill XP failed: " + ex.Message);
                }
            }

            private static object GetState(
                Durango.Offline.Player player)
            {
                Type handlers = AccessTools.TypeByName(
                    "BaoX.DurangoOriginal.SkillSystemMod.OfflineSkillHandlers");
                FieldInfo statesField = handlers == null
                    ? null
                    : AccessTools.Field(handlers, "States");
                IDictionary states = statesField == null
                    ? null
                    : statesField.GetValue(null) as IDictionary;
                return states == null || player == null
                    ? null
                    : states[player];
            }

            private static void WarnMissingApi()
            {
                if (_warnedMissingApi)
                {
                    return;
                }
                _warnedMissingApi = true;
                GatheringPlugin.Log.LogWarning(
                    "SkillSystemApi not found; gathering category XP disabled");
            }
        }
    }
}
