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
                    player.Send<OK>(default(OK), header.Seq);
                }
                else
                {
                    player.Send<Abort>(default(Abort), header.Seq);
                }
            });

            player.Closed += delegate()
            {
                States.Remove(player);
                if (_localPlayer == player)
                {
                    _localPlayer = null;
                }
            };

            if (SkillSystemPlugin.Log != null)
            {
                SkillSystemPlugin.Log.LogInfo("Offline skill handlers registered for " + player.EntityId);
            }
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

            SendSkills(_localPlayer, state, 0U);
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
            }

            response = category + " XP " + operation + " " + amount + " | Lv." + currentLevel + " | XP " + currentExp;
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
