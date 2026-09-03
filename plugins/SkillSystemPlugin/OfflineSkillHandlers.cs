using System;
using System.Collections.Generic;
using Durango.Logic;
using Durango.Network;
using Durango.Offline;
using HarmonyLib;
using Messages;
using Shared.Skill;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    internal static class OfflineSkillHandlers
    {
        private static readonly Dictionary<OfflinePlayer, OfflineSkillState> States = new Dictionary<OfflinePlayer, OfflineSkillState>();
        private static OfflinePlayer _localPlayer;

        internal static void Register(OfflinePlayer player, OfflineConnection connection, PlayerContext context, bool isLocalPlayer)
        {
            OfflineSkillState state = new OfflineSkillState(context);
            States[player] = state;
            if (isLocalPlayer)
            {
                _localPlayer = player;
            }

            connection.Recv<GetSkills>(delegate(GetSkills request, PacketHeader header)
            {
                SendSkills(player, state, header.Seq);
            });

            connection.Recv<LearnSkill>(delegate(LearnSkill request, PacketHeader header)
            {
                string error;
                SkillSystemPlugin.Log.LogInfo("LearnSkill RX: " + request.SkillId + "/" + request.SubId + " level=" + request.Level);
                if (state.Learn(request, out error))
                {
                    SendSkills(player, state, 0U);
                    RefreshCraftBuildAvailability(player);
                    player.Send<OK>(default(OK), header.Seq);
                    SkillSystemPlugin.Log.LogInfo("LearnSkill saved");
                }
                else
                {
                    SkillSystemPlugin.Log.LogWarning("LearnSkill rejected: " + error);
                    player.Send<Abort>(default(Abort), header.Seq);
                }
            });

            connection.Recv<UntrainSkill>(delegate(UntrainSkill request, PacketHeader header)
            {
                if (state.Untrain(request))
                {
                    SendSkills(player, state, 0U);
                    RefreshCraftBuildAvailability(player);
                    player.Send<OK>(default(OK), header.Seq);
                }
                else
                {
                    player.Send<Abort>(default(Abort), header.Seq);
                }
            });

            player.Closed += delegate()
            {
                SkillPlayerDataPersistence.Flush(state.Context);
                States.Remove(player);
                if (_localPlayer == player)
                {
                    _localPlayer = null;
                }
                SkillPlayerDataPersistence.Detach(state.Context);
            };

            if (SkillSystemPlugin.Log != null)
            {
                SkillSystemPlugin.Log.LogInfo("Offline skill handlers registered for " + player.EntityId);
            }
        }

        private static void RefreshCraftBuildAvailability(OfflinePlayer player)
        {
            try
            {
                Type backendType = AccessTools.TypeByName("BaoX.DurangoOriginal.CraftBuildMod.CraftBuildBackend");
                if (backendType == null)
                {
                    return;
                }

                System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                System.Reflection.MethodInfo sendRecipes = backendType.GetMethod("SendRecipeAvailability", flags);
                System.Reflection.MethodInfo sendBlueprints = backendType.GetMethod("SendBlueprintAvailability", flags);
                if (sendRecipes != null)
                {
                    sendRecipes.Invoke(null, new object[] { player, 0U });
                }
                if (sendBlueprints != null)
                {
                    sendBlueprints.Invoke(null, new object[] { player, 0U });
                }
            }
            catch (Exception ex)
            {
                SkillSystemPlugin.Log.LogWarning("Craft/build availability refresh after LearnSkill failed: " + ex.Message);
            }
        }

        internal static bool TryGetCraftBuildUnlockState(
            out string[] allRecipeIds,
            out string[] unlockedRecipeIds,
            out string[] allBlueprintIds,
            out string[] unlockedBlueprintIds)
        {
            HashSet<string> allRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> learnedRecipes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> allBlueprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> learnedBlueprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            allRecipeIds = new string[0];
            unlockedRecipeIds = new string[0];
            allBlueprintIds = new string[0];
            unlockedBlueprintIds = new string[0];

            OfflineSkillState state;
            Durango.Logic.SkillSystem skillSystem = GameSystem<Durango.Logic.SkillSystem>.Instance();
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state) ||
                skillSystem == null || skillSystem.Skills == null || skillSystem.Skills.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < skillSystem.Skills.Count; i++)
            {
                Durango.Logic.Skill.Bundle bundle = skillSystem.Skills[i];
                if (!CollectSkillRewards(bundle.Base, allRecipes, allBlueprints))
                {
                    return false;
                }
                if (bundle.Sub == null)
                {
                    continue;
                }
                for (int j = 0; j < bundle.Sub.Length; j++)
                {
                    if (!CollectSkillRewards(bundle.Sub[j], allRecipes, allBlueprints))
                    {
                        return false;
                    }
                }
            }

            foreach (Durango.Logic.Skill.Node node in state.EnumerateLearnedNodes())
            {
                if (node == null || node.Rewards == null)
                {
                    return false;
                }
                CollectNodeRewards(node, learnedRecipes, learnedBlueprints);
            }

            allRecipeIds = ToSortedArray(allRecipes);
            unlockedRecipeIds = ToSortedArray(learnedRecipes);
            allBlueprintIds = ToSortedArray(allBlueprints);
            unlockedBlueprintIds = ToSortedArray(learnedBlueprints);
            return true;
        }

        private static bool CollectSkillRewards(
            Durango.Logic.Skill.Skill skill,
            HashSet<string> recipeIds,
            HashSet<string> blueprintIds)
        {
            if (skill == null)
            {
                return true;
            }
            if (skill.Category == Category.S02OilPoison)
            {
                return true;
            }
            for (int level = 1; level <= skill.MaxLevel; level++)
            {
                Durango.Logic.Skill.Node node = skill.Get(level);
                if (node == null || node.Rewards == null)
                {
                    return false;
                }
                CollectNodeRewards(node, recipeIds, blueprintIds);
            }
            return true;
        }

        private static void CollectNodeRewards(
            Durango.Logic.Skill.Node node,
            HashSet<string> recipeIds,
            HashSet<string> blueprintIds)
        {
            if (node == null || node.Category == Category.S02OilPoison)
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
                AddIds(recipeIds, reward.RecipeIds);
                AddIds(blueprintIds, reward.BlueprintIds);
            }
        }

        private static void AddIds(HashSet<string> destination, string[] ids)
        {
            if (ids == null)
            {
                return;
            }
            for (int i = 0; i < ids.Length; i++)
            {
                if (!string.IsNullOrEmpty(ids[i]))
                {
                    destination.Add(ids[i]);
                }
            }
        }

        private static string[] ToSortedArray(HashSet<string> ids)
        {
            string[] result = new string[ids.Count];
            ids.CopyTo(result);
            Array.Sort(result, StringComparer.OrdinalIgnoreCase);
            return result;
        }

        internal static bool TryModifyCategoryExperience(string categoryName, string operation, int amount, out string response)
        {
            response = null;
            OfflineSkillState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                response = "SkillSystemPlugin is not active.";
                return false;
            }

            Category category;
            if (!TryParseCategory(categoryName, out category))
            {
                response = "Unknown skill category: " + categoryName;
                return false;
            }

            int previousLevel;
            int currentLevel;
            int currentExp;
            if (!state.ModifyCategoryExperience(category, operation, amount, out previousLevel, out currentLevel, out currentExp))
            {
                response = "Usage: /xp <category> <add|set> <amount>";
                return false;
            }

            _localPlayer.Send<SkillCategoryExperienced>(new SkillCategoryExperienced
            {
                Category = category,
                Exp = string.Equals(operation, "add", System.StringComparison.OrdinalIgnoreCase) ? amount : 0,
                ResearchReducedTime = 0.0
            }, 0U);

            bool set = string.Equals(
                operation,
                "set",
                System.StringComparison.OrdinalIgnoreCase);
            if (set || currentLevel != previousLevel)
            {
                SendSkills(_localPlayer, state, 0U);
            }
            if (currentLevel > previousLevel)
            {
                System.Collections.Generic.Dictionary<Category, int> changed = new System.Collections.Generic.Dictionary<Category, int>();
                changed[category] = currentLevel;
                _localPlayer.Send<Rewarded>(new Rewarded
                {
                    Effect = new CategoryLevelUpRewardEffect
                    {
                        Type = Shared.System.RewardEffect.CategoryLevelUp,
                        ChangedLevels = changed
                    },
                    Reward = default(RewardInfo)
                }, 0U);
                SkillPlayerDataPersistence.Flush(state.Context);
            }

            response = category + " XP " + operation + " " + amount + " | Lv." + currentLevel + " | XP " + currentExp;
            return true;
        }

        internal static bool TryAddGameplayCategoryExperience(
            string categoryName,
            double amount,
            out string response)
        {
            response = null;
            OfflineSkillState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                response = "SkillSystemPlugin is not active.";
                return false;
            }

            Category category;
            if (!TryParseCategory(categoryName, out category))
            {
                response = "Unknown skill category: " + categoryName;
                return false;
            }

            int previousLevel;
            int currentLevel;
            int currentExp;
            int appliedExp;
            double remainder;
            if (!state.AddCategoryExperienceFromGameplay(
                category,
                amount,
                out previousLevel,
                out currentLevel,
                out currentExp,
                out appliedExp,
                out remainder))
            {
                response = "Gameplay category XP must be greater than zero.";
                return false;
            }

            if (appliedExp > 0)
            {
                _localPlayer.Send<SkillCategoryExperienced>(new SkillCategoryExperienced
                {
                    Category = category,
                    Exp = appliedExp,
                    ResearchReducedTime = 0.0
                }, 0U);
                if (currentLevel != previousLevel)
                {
                    SendSkills(_localPlayer, state, 0U);
                }
            }

            if (currentLevel > previousLevel)
            {
                Dictionary<Category, int> changed = new Dictionary<Category, int>();
                changed[category] = currentLevel;
                _localPlayer.Send<Rewarded>(new Rewarded
                {
                    Effect = new CategoryLevelUpRewardEffect
                    {
                        Type = Shared.System.RewardEffect.CategoryLevelUp,
                        ChangedLevels = changed
                    },
                    Reward = default(RewardInfo)
                }, 0U);
                SkillPlayerDataPersistence.Flush(state.Context);
            }

            response = category + " gameplay XP +" + amount.ToString("0.###") +
                " | applied " + appliedExp +
                " | remainder " + remainder.ToString("0.###") +
                " | Lv." + currentLevel +
                " | XP " + currentExp;
            return true;
        }

        internal static bool TryModifyAllCategoryExperience(string operation, int amount, out string response)
        {
            response = null;
            OfflineSkillState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                response = "SkillSystemPlugin is not active.";
                return false;
            }

            if ((!string.Equals(operation, "add", System.StringComparison.OrdinalIgnoreCase)
                && !string.Equals(operation, "set", System.StringComparison.OrdinalIgnoreCase)) || amount < 0)
            {
                response = "Usage: /xp category all <add|set> <amount>";
                return false;
            }

            Category[] categories = new Category[]
            {
                Category.Survival,
                Category.MeleeCombat,
                Category.RangedCombat,
                Category.Defense,
                Category.Butchery,
                Category.Gathering,
                Category.Cooking,
                Category.Weaponcrafting,
                Category.Armorcrafting,
                Category.Constructing,
                Category.Farming,
                Category.Process
            };
            Dictionary<Category, int> changed = new Dictionary<Category, int>();
            int updated = 0;
            for (int i = 0; i < categories.Length; i++)
            {
                int previousLevel;
                int currentLevel;
                int currentExp;
                if (!state.ModifyCategoryExperience(categories[i], operation, amount, out previousLevel, out currentLevel, out currentExp))
                {
                    continue;
                }

                updated++;
                _localPlayer.Send<SkillCategoryExperienced>(new SkillCategoryExperienced
                {
                    Category = categories[i],
                    Exp = string.Equals(operation, "add", System.StringComparison.OrdinalIgnoreCase) ? amount : 0,
                    ResearchReducedTime = 0.0
                }, 0U);
                if (currentLevel > previousLevel)
                {
                    changed[categories[i]] = currentLevel;
                }
            }

            SendSkills(_localPlayer, state, 0U);
            if (changed.Count > 0)
            {
                _localPlayer.Send<Rewarded>(new Rewarded
                {
                    Effect = new CategoryLevelUpRewardEffect
                    {
                        Type = Shared.System.RewardEffect.CategoryLevelUp,
                        ChangedLevels = changed
                    },
                    Reward = default(RewardInfo)
                }, 0U);
                SkillPlayerDataPersistence.Flush(state.Context);
            }

            response = "All category XP " + operation + " " + amount + " | Updated " + updated;
            return updated > 0;
        }

        internal static bool RefreshForCharacterLevel(out string response)
        {
            OfflineSkillState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                response = "SkillSystemPlugin is not active.";
                return false;
            }

            state.EnsureSkillPoints();
            SendSkills(_localPlayer, state, 0U);
            response = "SP updated: " + state.SkillPoints;
            return true;
        }

        internal static bool TrySetCategoryLevel(PlayerContext context, string categoryName, int level, out string response)
        {
            response = null;
            if (context == null)
            {
                response = "Player context is not available.";
                return false;
            }

            Category category;
            if (!TryParseCategory(categoryName, out category))
            {
                response = "Unknown skill category: " + categoryName;
                return false;
            }

            OfflineSkillState state = new OfflineSkillState(context);
            int currentLevel;
            if (!state.SetCategoryLevel(category, level, out currentLevel))
            {
                response = "Unable to set category level.";
                return false;
            }

            response = category + " Lv." + currentLevel;
            return true;
        }

        internal static bool HasLocalDependentBranch(string skillId, string subId, int parentLevelAfterUntrain)
        {
            OfflineSkillState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                return false;
            }

            return state.HasDependentBranch(skillId, subId, parentLevelAfterUntrain);
        }

        internal static void RefreshLocalStatistics()
        {
            OfflineSkillState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                return;
            }

            SendStatistics(_localPlayer, state);
        }

        private static bool TryParseCategory(string value, out Category category)
        {
            string normalized = (value ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
            switch (normalized)
            {
                case "survival": category = Category.Survival; return true;
                case "melee": case "meleecombat": category = Category.MeleeCombat; return true;
                case "ranged": case "rangedcombat": category = Category.RangedCombat; return true;
                case "defense": category = Category.Defense; return true;
                case "butchery": case "butchering": category = Category.Butchery; return true;
                case "gathering": case "gather": category = Category.Gathering; return true;
                case "cooking": case "cook": category = Category.Cooking; return true;
                case "weapon": case "weapontool": case "weaponcrafting": category = Category.Weaponcrafting; return true;
                case "tailoring": case "armor": case "armorcrafting": category = Category.Armorcrafting; return true;
                case "construction": case "constructing": case "build": category = Category.Constructing; return true;
                case "farming": case "farm": category = Category.Farming; return true;
                case "processing": case "process": category = Category.Process; return true;
                default: category = Category.Invalid; return false;
            }
        }

        private static void SendSkills(OfflinePlayer player, OfflineSkillState state, uint replyOf)
        {
            player.Send<Skills>(state.CreateMessage(), replyOf);
            SendStatistics(player, state);
        }

        private static void SendStatistics(OfflinePlayer player, OfflineSkillState state)
        {
            OfflineStatisticsCalculator.Send(player, state);
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new System.Type[]
    {
        typeof(string),
        typeof(OfflineConnection),
        typeof(World),
        typeof(PlayerContext),
        typeof(bool)
    })]
    internal static class OfflinePlayerConstructorSkillPatch
    {
        private static void Postfix(OfflinePlayer __instance, OfflineConnection connection, PlayerContext context, bool isLocalPlayer)
        {
            OfflineSkillHandlers.Register(__instance, connection, context, isLocalPlayer);
        }
    }

    [HarmonyPatch(typeof(EquipSystem), "EquipmentsReceived")]
    internal static class EquipmentsReceivedSkillStatisticsPatch
    {
        private static void Postfix()
        {
            OfflineSkillHandlers.RefreshLocalStatistics();
        }
    }
}
