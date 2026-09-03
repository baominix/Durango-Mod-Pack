using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango;
using Durango.Logic;
using Durango.Network;
using Durango.Offline;
using Durango.Utils.Extensions;
using HarmonyLib;
using L10N;
using Messages;
using Shared.Economy;
using Shared.Faction;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;

namespace BaoX.DurangoOriginal.SupportOrganizationRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SupportOrganizationRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.supportorganization";
        public const string PluginName = "Support Organization Restoration Plugin";
        public const string PluginVersion = "0.1.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> FriendshipReward;
        internal static ConfigEntry<int> RequestCooldownSeconds;
        internal static ConfigFile PluginConfig;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            PluginConfig = Config;
            Enabled = Config.Bind("General", "Enabled", true,
                "Restore the Support Organization/Faction UI and its offline message backend.");
            FriendshipReward = Config.Bind("Support Requests", "FriendshipPoints", 25,
                "Faction friendship points granted by each offline support request.");
            RequestCooldownSeconds = Config.Bind("Support Requests", "CooldownSeconds", 0,
                "Cooldown for each offline support request. Zero permits repeated testing.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("Support Organization restoration loaded: seven organizations, mission initialization and offline support requests.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
            _harmony = null;
        }
    }

    internal static class SupportOrganizationBackend
    {
        private static readonly FactionType[] Types = new FactionType[]
        {
            FactionType.ChlorophylForum,
            FactionType.ChamberOfPioneer,
            FactionType.TheFirm,
            FactionType.TheCommittee,
            FactionType.Lama,
            FactionType.RescueTf,
            FactionType.SubStory
        };

        private static readonly Dictionary<FactionType, ConfigEntry<int>> Points =
            new Dictionary<FactionType, ConfigEntry<int>>();
        private static readonly Dictionary<FactionType, double> Untils =
            new Dictionary<FactionType, double>();
        private static string _currentOwner;

        internal static void Register(OfflinePlayer player, OfflineConnection connection,
            string ownerId)
        {
            EnsureState(ownerId);

            connection.Recv<GetFactions>(delegate(GetFactions msg, PacketHeader header)
            {
                player.Send<Messages.Factions>(CreateFactions(), header.Seq);
            });
            connection.Recv<GetMissions>(delegate(GetMissions msg, PacketHeader header)
            {
                MissionInfos infos = default(MissionInfos);
                infos.Missions = new Mission[0];
                infos.MissionActivatesAt = new Dictionary<FactionType, double>();
                infos.RecommendFailReasons = null;
                infos.ShuffleCount = 0;
                infos.ShuffleAt = null;
                player.Send<MissionInfos>(infos, header.Seq);
            });
            connection.Recv<GetSupportRequests>(delegate(GetSupportRequests msg, PacketHeader header)
            {
                player.Send<SupportRequests>(CreateSupportRequests(msg.RequestedUntilsOnly), header.Seq);
            });
            connection.Recv<SendFactionSupportRequest>(delegate(SendFactionSupportRequest msg, PacketHeader header)
            {
                HandleSupportRequest(player, msg, header);
            });

            // FactionSystem may have completed OnReady before this late constructor patch.
            // Unsolicited snapshots are supported by its normal message listeners.
            player.Send<Messages.Factions>(CreateFactions(), 0U);
            player.Send<MissionInfos>(CreateEmptyMissionInfos(), 0U);
            SupportOrganizationRestorationPlugin.Log.LogInfo(
                "Offline Support Organization backend registered for " + ownerId + ".");
        }

        private static MissionInfos CreateEmptyMissionInfos()
        {
            MissionInfos infos = default(MissionInfos);
            infos.Missions = new Mission[0];
            infos.MissionActivatesAt = new Dictionary<FactionType, double>();
            infos.RecommendFailReasons = null;
            infos.ShuffleCount = 0;
            infos.ShuffleAt = null;
            return infos;
        }

        private static void EnsureState(string ownerId)
        {
            string owner = string.IsNullOrEmpty(ownerId) ? "local" : ownerId;
            if (Points.Count != 0 && _currentOwner == owner) return;
            Points.Clear();
            Untils.Clear();
            _currentOwner = owner;
            for (int i = 0; i < Types.Length; i++)
            {
                FactionType type = Types[i];
                Points[type] = SupportOrganizationRestorationPlugin.PluginConfig.Bind(
                    "Faction Points " + owner, type.ToString(), 0,
                    "Persisted offline Support Organization friendship points.");
                Untils[type] = 0.0;
            }
        }

        private static Messages.Factions CreateFactions()
        {
            Messages.Faction[] factions = new Messages.Faction[Types.Length];
            double now = UnixNow();
            for (int i = 0; i < Types.Length; i++)
            {
                FactionType type = Types[i];
                Messages.Faction faction = default(Messages.Faction);
                faction.Type = type;
                faction.Point = Points[type].Value;
                faction.Level = 1;
                faction.AvailableAt = Untils[type];
                faction.PointBefore = null;
                faction.StartsAt = 0.0;
                faction.EndsAt = now + 315360000.0;
                factions[i] = faction;
            }
            Messages.Factions result = default(Messages.Factions);
            result._Factions = factions;
            result.DailyMissionAvailableAt = 0.0;
            return result;
        }

        private static SupportRequests CreateSupportRequests(bool untilsOnly)
        {
            SupportRequests result = default(SupportRequests);
            result.Requests = untilsOnly ? new SupportRequest[0] : CreateRequestList();
            result.EndAt = UnixNow() + 315360000.0;
            result.Untils = new Dictionary<FactionType, double>(Untils);
            result.FactionTypes = (FactionType[])Types.Clone();
            result.Level = 1;
            return result;
        }

        private static SupportRequest[] CreateRequestList()
        {
            SupportRequest[] requests = new SupportRequest[Types.Length];
            for (int i = 0; i < Types.Length; i++)
            {
                FactionType type = Types[i];
                SupportRequest request = default(SupportRequest);
                request.RequestId = "offline-support-" + ((int)type).ToString();
                request.Name = SupportOrganizationLocalization.Get("offline_support", ((Enum)(object)type).GetName());
                request.FactionType = type;
                request.Fee = new Money(0, Currency.TStone);
                request.Level = 1;
                request.Rewards = EmptyRewards();
                request.RandomRewards = EmptyRewards();
                request.FriendshipPointReward = Math.Max(0,
                    SupportOrganizationRestorationPlugin.FriendshipReward.Value);
                request.Duration = Math.Max(0,
                    SupportOrganizationRestorationPlugin.RequestCooldownSeconds.Value);
                request.RequiredItem = null;
                request.MaxCount = 999;
                request.RemainCount = 999;
                requests[i] = request;
            }
            return requests;
        }

        private static void HandleSupportRequest(OfflinePlayer player,
            SendFactionSupportRequest message, PacketHeader header)
        {
            FactionType type;
            if (!TryParseRequest(message.RequestId, out type))
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            int reward = Math.Max(0, SupportOrganizationRestorationPlugin.FriendshipReward.Value);
            Points[type].Value += reward;
            double availableAt = UnixNow() + Math.Max(0,
                SupportOrganizationRestorationPlugin.RequestCooldownSeconds.Value);
            Untils[type] = availableAt;
            SupportOrganizationRestorationPlugin.PluginConfig.Save();

            SupportRequestUpdated update = default(SupportRequestUpdated);
            update.RequestId = message.RequestId;
            update.RemainCount = 999;
            update.FactionType = type;
            update.AvailableAt = availableAt;
            AcceptedSupportRewards accepted = default(AcceptedSupportRewards);
            accepted.Rewards = EmptyRewards();
            accepted.RandomRewards = EmptyRewards();
            accepted.UpdatedInfo = update;
            player.Send<AcceptedSupportRewards>(accepted, header.Seq);
            player.Send<Messages.Factions>(CreateFactions(), 0U);
            SupportOrganizationRestorationPlugin.Log.LogInfo(
                "Offline support request accepted: " + type + ", points=" + Points[type].Value);
        }

        private static bool TryParseRequest(string id, out FactionType type)
        {
            type = FactionType.Invalid;
            const string prefix = "offline-support-";
            if (string.IsNullOrEmpty(id) || !id.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            int value;
            if (!int.TryParse(id.Substring(prefix.Length), out value)) return false;
            type = (FactionType)value;
            return Array.IndexOf(Types, type) >= 0;
        }

        private static Messages.SupportRewards EmptyRewards()
        {
            Messages.SupportRewards rewards = default(Messages.SupportRewards);
            rewards.Items = new ItemSupportReward[0];
            rewards.Moneys = new Money[0];
            return rewards;
        }

        private static double UnixNow()
        {
            return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
    internal static class SupportOrganizationHiddenMenuPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MenuType type, ref bool __result)
        {
            if (SupportOrganizationRestorationPlugin.Enabled.Value && type == MenuType.Faction)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "GameManager_MainSceneLoaded")]
    internal static class SupportOrganizationMainScenePatch
    {
        private static void Postfix()
        {
            if (!SupportOrganizationRestorationPlugin.Enabled.Value) return;
            MenuSystem menu = GameSystem<MenuSystem>.Instance();
            if (menu != null) menu.EnableMenu(MenuType.Faction, true, true);
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class SupportOrganizationBackendPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(OfflinePlayer __instance, string entityId,
            OfflineConnection connection, World world, PlayerContext context,
            bool isLocalPlayer)
        {
            if (!SupportOrganizationRestorationPlugin.Enabled.Value || !isLocalPlayer ||
                connection == null) return;
            SupportOrganizationBackend.Register(__instance, connection, entityId);
        }
    }
}
