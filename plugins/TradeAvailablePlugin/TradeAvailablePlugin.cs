using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Offline;
using HarmonyLib;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;

namespace BaoX.DurangoOriginal.TradeAvailable
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class TradeAvailablePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.tradeavailable";
        public const string PluginName = "Trade Available Plugin";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Restore retail Tradable values for offline-created items.");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(PluginName + " loaded.");
        }

        private void OnDestroy()
        {
            if (_harmony == null) return;
            _harmony.UnpatchSelf();
            _harmony = null;
        }
    }

    public static class TradeAvailableApi
    {
        private static HashSet<string> _tradeLockedPrototypes;

        public static int NormalizeInventory(PlayerContext context)
        {
            if (context == null || context.InventoryItems == null) return 0;
            int changed = 0;
            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                Item item = context.InventoryItems[i];
                if (!Normalize(ref item)) continue;
                context.InventoryItems[i] = item;
                changed++;
            }
            if (changed > 0) context.Save();
            return changed;
        }

        public static bool Normalize(ref Item item)
        {
            bool tradable = !GetTradeLockedPrototypes().Contains(item.Prototype ?? string.Empty);
            if (item.Tradable == tradable) return false;
            item.Tradable = tradable;
            return true;
        }

        private static HashSet<string> GetTradeLockedPrototypes()
        {
            if (_tradeLockedPrototypes != null) return _tradeLockedPrototypes;
            HashSet<string> result = new HashSet<string>();
            result.Add("trade_locked_artifact_capsule");
            try
            {
                TextAsset asset = Resources.Load<TextAsset>("offline/assets/entity_types/artifact");
                if (asset != null)
                {
                    JObject root = JObject.Parse(asset.text);
                    foreach (JProperty property in root.Properties())
                    {
                        JObject definition = property.Value as JObject;
                        if (definition == null || definition["trade_locked"] == null ||
                            !definition["trade_locked"].Value<bool>()) continue;
                        JToken token = definition["capsule_prototype_id"];
                        string prototype = token == null ? null : token.Value<string>();
                        if (!string.IsNullOrEmpty(prototype)) result.Add(prototype);
                    }
                }
            }
            catch (Exception ex)
            {
                TradeAvailablePlugin.Log.LogWarning(
                    "Could not load original trade locks: " + ex.Message);
            }
            _tradeLockedPrototypes = result;
            TradeAvailablePlugin.Log.LogInfo(
                "Loaded original trade-locked prototypes: " + result.Count);
            return _tradeLockedPrototypes;
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class TradeAvailableInventoryPatch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(string entityId, PlayerContext context, bool isLocalPlayer)
        {
            if (!TradeAvailablePlugin.Enabled.Value || !isLocalPlayer) return;
            int changed = TradeAvailableApi.NormalizeInventory(context);
            if (changed > 0)
            {
                TradeAvailablePlugin.Log.LogInfo(
                    "Restored Tradable before inventory sync: owner=" + entityId +
                    ", items=" + changed);
            }
        }
    }

    [HarmonyPatch(typeof(Cheats), "MakeItem")]
    internal static class TradeAvailableMakeItemPatch
    {
        private static void Postfix(ref Item? __result)
        {
            if (!TradeAvailablePlugin.Enabled.Value || !__result.HasValue) return;
            Item item = __result.Value;
            if (TradeAvailableApi.Normalize(ref item)) __result = item;
        }
    }
}
