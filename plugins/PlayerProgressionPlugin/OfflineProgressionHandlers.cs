using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Durango.Network;
using Durango.Offline;
using HarmonyLib;
using Messages;
using Shared.Ability;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;

namespace BaoX.DurangoOriginal.PlayerProgressionMod
{
    internal static class OfflineProgressionHandlers
    {
        private static readonly Dictionary<OfflinePlayer, PlayerProgressionState> States = new Dictionary<OfflinePlayer, PlayerProgressionState>();
        private static OfflinePlayer _localPlayer;

        internal static void Register(OfflinePlayer player, PlayerContext context, bool isLocalPlayer)
        {
            if (!ProgressionPersistence.IsProgressionMode(context))
            {
                return;
            }

            PlayerProgressionState state = ProgressionPersistence.Get(context);
            States[player] = state;
            if (isLocalPlayer)
            {
                _localPlayer = player;
            }
            SendStatistics(player, state);
            SendSurvival(player, context);

            player.Closed += delegate()
            {
                ProgressionPersistence.Flush(state.Context);
                States.Remove(player);
                if (_localPlayer == player)
                {
                    _localPlayer = null;
                }
                ProgressionPersistence.Detach(state.Context);
            };
        }

        internal static bool TryAddExperience(int amount, out string response)
        {
            return TryModifyExperience("add", amount, out response);
        }

        internal static bool TryModifyExperience(string operation, int amount, out string response)
        {
            PlayerProgressionState state;
            if (_localPlayer == null || !States.TryGetValue(_localPlayer, out state))
            {
                response = "Player progression is not active in this game mode.";
                return false;
            }

            bool set = string.Equals(operation, "set", StringComparison.OrdinalIgnoreCase);
            bool add = string.Equals(operation, "add", StringComparison.OrdinalIgnoreCase);
            if ((!set && !add) || amount < 0 || (add && amount == 0))
            {
                response = "Usage: /xp level <add|set> <amount>";
                return false;
            }

            if (add && state.Level >= PlayerProgressionState.MaximumLevel)
            {
                response = "Character is already at maximum level (Lv." + PlayerProgressionState.MaximumLevel + "). XP unchanged.";
                PlayerProgressionPlugin.Log.LogInfo("XP add skipped: maximum level reached level=" + state.Level + " exp=" + state.Experience);
                return true;
            }

            int previousLevel = state.Level;
            int changedLevels = set ? state.SetExperience(amount) : state.AddExperience(amount);

            if (add)
            {
                _localPlayer.Send<ExpGained>(new ExpGained
                {
                    EntityId = _localPlayer.EntityId,
                    Exp = amount,
                    BonusExp = 0,
                    ResistanceType = null,
                    ResistanceExp = 0
                }, 0U);
            }

            SendStatistics(_localPlayer, state);
            if (changedLevels != 0)
            {
                SendSurvival(_localPlayer, null);
                RefreshSkillSystem();
            }

            for (int level = previousLevel + 1; level <= previousLevel + Math.Max(0, changedLevels); level++)
            {
                Dictionary<Basic, int> abilities = new Dictionary<Basic, int>();
                foreach (Basic basic in Enum.GetValues(typeof(Basic)))
                {
                    if (basic != Basic.Invalid)
                    {
                        abilities[basic] = 2;
                    }
                }

                RewardInfo reward = default(RewardInfo);
                reward.SkillPoints = PlayerProgressionState.GetSkillPointReward(level);
                reward.UsableSkillPoints = reward.SkillPoints;
                reward.Abilities = abilities;
                _localPlayer.Send<Rewarded>(new Rewarded
                {
                    Effect = new LevelUpEffect
                    {
                        Type = Shared.System.RewardEffect.LevelUp,
                        Level = level
                    },
                    Reward = reward
                }, 0U);
            }

            if (changedLevels > 0)
            {
                ProgressionPersistence.Flush(state.Context);
            }

            PlayerProgressionPlugin.Log.LogInfo("XP command: " + operation + " " + amount + " level=" + state.Level + " exp=" + state.Experience);
            if (changedLevels > 0)
            {
                int levelCount = Math.Max(0, changedLevels);
                int gainedSkillPoints = 0;
                for (int level = previousLevel + 1; level <= state.Level; level++)
                {
                    gainedSkillPoints += PlayerProgressionState.GetSkillPointReward(level);
                }

                int abilityGain = levelCount * 2;
                StringBuilder summary = new StringBuilder();
                summary.AppendLine("Level Up");
                summary.AppendLine("You are now Lv. " + state.Level);
                summary.AppendLine("Skill Point +" + gainedSkillPoints);
                summary.AppendLine("Strength +" + abilityGain);
                summary.AppendLine("Charisma +" + abilityGain);
                summary.AppendLine("Dexterity +" + abilityGain);
                summary.AppendLine("Agility +" + abilityGain);
                summary.AppendLine("Endurance +" + abilityGain);
                summary.AppendLine("Will +" + abilityGain);
                summary.AppendLine("Intelligence +" + abilityGain);
                summary.Append("Perception +" + abilityGain);
                response = summary.ToString();
            }
            else
            {
                response = "Character XP " + operation + " " + amount + " | Lv." + state.Level + " | XP " + state.Experience + " | HP " + PlayerProgressionState.GetMaxHp(state.Level) + " | Stamina " + PlayerProgressionState.GetMaxStamina(state.Level);
            }
            return true;
        }

        private static void RefreshSkillSystem()
        {
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    Type apiType = assemblies[i].GetType("BaoX.DurangoOriginal.SkillSystemMod.SkillSystemApi", false);
                    if (apiType == null)
                    {
                        continue;
                    }

                    MethodInfo method = apiType.GetMethod("RefreshForCharacterLevel", BindingFlags.Public | BindingFlags.Static);
                    if (method == null)
                    {
                        return;
                    }

                    object[] parameters = new object[] { null };
                    method.Invoke(null, parameters);
                    return;
                }
            }
            catch (Exception exception)
            {
                PlayerProgressionPlugin.Log.LogWarning("Skill SP refresh failed: " + exception.Message);
            }
        }

        private static void SendStatistics(OfflinePlayer player, PlayerProgressionState state)
        {
            Statistics statistics = default(Statistics);
            statistics.BasicAbilities = new Dictionary<Basic, int>();
            foreach (Basic basic in Enum.GetValues(typeof(Basic)))
            {
                if (basic != Basic.Invalid)
                {
                    statistics.BasicAbilities[basic] = PlayerProgressionState.GetBasicAbility(state.Level);
                }
            }
            statistics.DerivedsAbilities = new Dictionary<Derived, float>();
            statistics.DerivedsAbilities[Derived.Swimming] = 100f;
            statistics.Level = state.Level;
            statistics.Exp = state.Experience;
            player.Send<Statistics>(statistics, 0U);
        }

        private static void SendSurvival(OfflinePlayer player, PlayerContext context)
        {
            if (context == null)
            {
                PlayerProgressionState state;
                if (!States.TryGetValue(player, out state))
                {
                    return;
                }
                context = state.Context;
            }

            if (context == null || context.AppearPlayer.Survival.Gauges == null)
            {
                return;
            }

            Dictionary<string, Gauge> updated = new Dictionary<string, Gauge>();
            updated["life"] = context.AppearPlayer.Survival.Life;
            updated["stamina"] = context.AppearPlayer.Survival.Gauges["stamina"];
            player.Send<SurvivalUpdated>(new SurvivalUpdated
            {
                EntityId = player.EntityId,
                Updated = updated,
                Removed = new string[0]
            }, 0U);
        }

    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string),
        typeof(OfflineConnection),
        typeof(World),
        typeof(PlayerContext),
        typeof(bool)
    })]
    internal static class OfflinePlayerProgressionConstructorPatch
    {
        private static void Postfix(OfflinePlayer __instance, PlayerContext context, bool isLocalPlayer)
        {
            OfflineProgressionHandlers.Register(__instance, context, isLocalPlayer);
        }
    }
}
