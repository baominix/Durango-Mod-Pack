using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Logic.Clan;
using Durango.Logic.Clusters;
using HarmonyLib;
using Messages;
using Shared.Clan;
using Shared.Economy;

namespace BaoX.DurangoOriginal.OfflineClanRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OfflineClanRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.offlineclanrestoration";
        public const string PluginName = "Offline Clan Restoration Plugin";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> HasClan;
        internal static ConfigEntry<string> ClanId;
        internal static ConfigEntry<string> ClanName;
        internal static ConfigEntry<int> ClanLevel;
        internal static ConfigEntry<long> ClanExp;
        internal static ConfigEntry<long> ClanFund;
        internal static ConfigEntry<string> ClanNotice;
        internal static ConfigEntry<string> ClanIntro;
        internal static BepInEx.Configuration.ConfigFile StoreConfig;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            StoreConfig = Config;
            Enabled = Config.Bind("General", "Enabled", true,
                "Restore the local offline Clan UI and clan state.");
            HasClan = Config.Bind("Clan", "HasClan", false,
                "Whether the current offline profile owns a local clan.");
            ClanId = Config.Bind("Clan", "ClanId", "offline-clan-local",
                "Stable id for the local offline clan.");
            ClanName = Config.Bind("Clan", "ClanName", "Durango Clan",
                "Name of the local offline clan.");
            ClanLevel = Config.Bind("Clan", "Level", 1, "Local clan level.");
            ClanExp = Config.Bind("Clan", "Exp", 0L, "Local clan experience.");
            ClanFund = Config.Bind("Clan", "Fund", 0L, "Local clan fund.");
            ClanNotice = Config.Bind("Clan", "Notice", "Offline clan restored.", "Clan notice.");
            ClanIntro = Config.Bind("Clan", "Intro", "A local offline clan.", "Clan introduction.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(PluginName + " loaded. HasClan=" + HasClan.Value);
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }

        internal static bool IsOfflineEnabled
        {
            get
            {
                return Enabled != null && Enabled.Value && GameManager.ClusterMode != Mode.Online;
            }
        }
    }

    internal static class OfflineClanStore
    {
        private static readonly MethodInfo OnReceivePlayerClan = typeof(ClanSystem).GetMethod(
            "OnReceivePlayerClan", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AlliesField = AccessTools.Field(typeof(ClanSystem), "<Allies>k__BackingField");
        private static readonly FieldInfo AlliesUpdatedField = AccessTools.Field(typeof(ClanSystem), "AlliesUpdated");

        public static Clan BuildClan()
        {
            if (!OfflineClanRestorationPlugin.HasClan.Value || PlayerBehavior.LocalPlayer == null)
            {
                return null;
            }

            string entityId = PlayerBehavior.LocalPlayer.EntityId;
            ClanJson json = new ClanJson
            {
                id = OfflineClanRestorationPlugin.ClanId.Value,
                name = OfflineClanRestorationPlugin.ClanName.Value,
                fund = OfflineClanRestorationPlugin.ClanFund.Value,
                level = Math.Max(1, OfflineClanRestorationPlugin.ClanLevel.Value),
                exp = Math.Max(0L, OfflineClanRestorationPlugin.ClanExp.Value),
                intro = OfflineClanRestorationPlugin.ClanIntro.Value,
                notice = OfflineClanRestorationPlugin.ClanNotice.Value,
                member_count = 1,
                capacity = 30,
                mainland = "Offline",
                members = new Pair<string, int>[] { new Pair<string, int>(entityId, 0) },
                appliers = new string[0],
                role_infos = new Dictionary<int, RoleInfo>
                {
                    {
                        0,
                        new RoleInfo
                        {
                            id = 0,
                            grade = 0,
                            permissions = Permissions.ApproveMember | Permissions.PromoteMember |
                                Permissions.EditClanInfo | Permissions.OccupyWarphole | Permissions.Research,
                            user_type = UserType.Root,
                            name = "Leader"
                        }
                    }
                }
            };
            Clan clan = new Clan();
            clan.Set(json, true);
            return clan;
        }

        public static void ApplyToSystem(ClanSystem system)
        {
            if (system == null || PlayerBehavior.LocalPlayer == null || OnReceivePlayerClan == null)
            {
                return;
            }

            Clan clan = BuildClan();
            if (clan == null)
            {
                PlayerBehavior.LocalPlayer.Clan = default(Messages.Member);
            }
            else
            {
                PlayerBehavior.LocalPlayer.Clan = new Messages.Member
                {
                    EntityId = PlayerBehavior.LocalPlayer.EntityId,
                    ClanId = clan.Id,
                    ClanName = clan.Name,
                    RoleId = 0,
                    ApplyingClanId = null
                };
            }
            OnReceivePlayerClan.Invoke(system, new object[] { clan });
        }

        public static void Create(string name)
        {
            OfflineClanRestorationPlugin.ClanName.Value = name.Trim();
            OfflineClanRestorationPlugin.ClanId.Value = "offline-clan-" + StableSlug(name);
            OfflineClanRestorationPlugin.HasClan.Value = true;
            OfflineClanRestorationPlugin.StoreConfig.Save();
            if (GameSystem<ClanSystem>.HasInstance())
            {
                ApplyToSystem(GameSystem<ClanSystem>.Instance());
            }
        }

        public static void Leave()
        {
            OfflineClanRestorationPlugin.HasClan.Value = false;
            OfflineClanRestorationPlugin.StoreConfig.Save();
            if (GameSystem<ClanSystem>.HasInstance())
            {
                ApplyToSystem(GameSystem<ClanSystem>.Instance());
            }
        }

        public static void SetAlliesEmpty(ClanSystem system)
        {
            if (AlliesField != null) AlliesField.SetValue(system, new AllySlot[0]);
            Action updated = AlliesUpdatedField == null ? null : AlliesUpdatedField.GetValue(system) as Action;
            if (updated != null) updated();
        }

        public static void SaveComments(string notice, string intro)
        {
            OfflineClanRestorationPlugin.ClanNotice.Value = notice ?? string.Empty;
            OfflineClanRestorationPlugin.ClanIntro.Value = intro ?? string.Empty;
            OfflineClanRestorationPlugin.StoreConfig.Save();
            if (GameSystem<ClanSystem>.HasInstance()) ApplyToSystem(GameSystem<ClanSystem>.Instance());
        }

        private static string StableSlug(string value)
        {
            if (string.IsNullOrEmpty(value)) return "local";
            int hash = 17;
            for (int i = 0; i < value.Length; i++) hash = unchecked(hash * 31 + value[i]);
            return Math.Abs((long)hash).ToString();
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "RequestPlayerClan")]
    internal static class RequestPlayerClanPatch
    {
        private static bool Prefix(ClanSystem __instance)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            OfflineClanStore.ApplyToSystem(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "GetClanMakeCost")]
    internal static class ClanMakeCostPatch
    {
        private static bool Prefix(Action<Costs> onResult)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            if (onResult != null)
            {
                onResult(new Costs
                {
                    _Costs = new Dictionary<Currency, long> { { Currency.TStone, 0L } }
                });
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "MakeClan")]
    internal static class MakeClanPatch
    {
        private static bool Prefix(Currency currency, string clanName, Action<bool> onResult)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            bool success = !string.IsNullOrEmpty(clanName);
            if (success)
            {
                OfflineClanStore.Create(clanName);
                OfflineClanRestorationPlugin.Log.LogInfo("Created offline clan: " + clanName);
            }
            if (onResult != null) onResult(success);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "GetClanInfo", new Type[]
    {
        typeof(string), typeof(Action<Clan>), typeof(bool), typeof(bool)
    })]
    internal static class GetClanInfoPatch
    {
        private static bool Prefix(string clanId, Action<Clan> callback)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            Clan clan = OfflineClanStore.BuildClan();
            if (callback != null) callback(clan != null && clan.Id == clanId ? clan : null);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "RequestClanInfo", new Type[]
    {
        typeof(string), typeof(Action<IList<Clan>>)
    })]
    internal static class SearchClanPatch
    {
        private static bool Prefix(string clanName, Action<IList<Clan>> callback)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            List<Clan> result = new List<Clan>();
            Clan clan = OfflineClanStore.BuildClan();
            if (clan != null && (string.IsNullOrEmpty(clanName) ||
                clan.Name.IndexOf(clanName, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                result.Add(clan);
            }
            if (callback != null) callback(result);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "GetAllySlots")]
    internal static class GetAllySlotsPatch
    {
        private static bool Prefix(ClanSystem __instance)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            OfflineClanStore.SetAlliesEmpty(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "LeaveClan")]
    internal static class LeaveClanPatch
    {
        private static bool Prefix(Action<bool> onResult)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            OfflineClanStore.Leave();
            if (onResult != null) onResult(true);
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "RenameClan")]
    internal static class RenameClanPatch
    {
        private static bool Prefix(string clanName)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            if (!string.IsNullOrEmpty(clanName))
            {
                OfflineClanRestorationPlugin.ClanName.Value = clanName.Trim();
                OfflineClanRestorationPlugin.StoreConfig.Save();
                if (GameSystem<ClanSystem>.HasInstance()) OfflineClanStore.ApplyToSystem(GameSystem<ClanSystem>.Instance());
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ClanSystem), "SetClanComment")]
    internal static class SetClanCommentPatch
    {
        private static bool Prefix(string notice, string intro, Action<bool> onResult)
        {
            if (!OfflineClanRestorationPlugin.IsOfflineEnabled) return true;
            OfflineClanStore.SaveComments(notice, intro);
            if (onResult != null) onResult(true);
            return false;
        }
    }
}
