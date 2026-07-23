using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic.Clusters;
using Durango.Offline;
using Durango.System;
using Durango.UI;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.SelectGameMode
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class SelectGameModePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.selectgamemode";
        public const string PluginName = "Select Game Mode";
        public const string PluginVersion = "0.1.0";

        private Harmony _harmony;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Logger.LogInfo("SelectGameMode loaded");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }

    internal static class SelectGameModeRuntime
    {
        public const string CreativeKey = "free_offline";
        public const string SingleMultiKey = "single_multi_offline";
        public const string PrefKey = "baox_select_game_mode";

        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo TitleClustersField = typeof(TitleMenuUserControlBase).GetField("Clusters", Flags);
        private static readonly FieldInfo ClustersField = typeof(Clusters).GetField("_clusters", Flags);

        public static bool IsModeKey(string key)
        {
            return key == CreativeKey || key == SingleMultiKey;
        }

        public static string GetModeName(string key)
        {
            if (key == SingleMultiKey)
            {
                return "Single/Multi Player Mode";
            }
            return "Creative Island Mode";
        }

        public static void SaveMode(string key)
        {
            if (!IsModeKey(key))
            {
                return;
            }
            Preferences.SetString(PrefKey, key, Preferences.Level.Device);
            if (SelectGameModePlugin.Log != null)
            {
                SelectGameModePlugin.Log.LogInfo("Selected game mode: " + GetModeName(key) + " (" + key + ")");
            }
        }

        public static void Install(TitleMenuUserControlBase control)
        {
            if (control == null || TitleClustersField == null)
            {
                return;
            }
            Clusters clusters = TitleClustersField.GetValue(control) as Clusters;
            Install(clusters);
        }

        public static void Install(Clusters clusters)
        {
            if (clusters == null || ClustersField == null)
            {
                return;
            }

            Dictionary<string, Cluster> map = ClustersField.GetValue(clusters) as Dictionary<string, Cluster>;
            if (map == null)
            {
                return;
            }

            if (!HasModeClusters(map))
            {
                map.Clear();

                Server creative = new Server("free", MakeNames("Creative Island Mode"));
                creative.Cluster.IsRecommendable = true;
                creative.Cluster.Mode = Mode.Editable;
                map[CreativeKey] = creative.Cluster;

                Server singleMulti = new Server("single_multi", MakeNames("Single/Multi Player Mode"));
                singleMulti.Cluster.IsRecommendable = false;
                singleMulti.Cluster.Mode = Mode.Offline;
                map[SingleMultiKey] = singleMulti.Cluster;
            }

            string selected = Preferences.GetString("last_selected_cluster_key", string.Empty, Preferences.Level.Device);
            if (!IsModeKey(selected))
            {
                Preferences.SetString("last_selected_cluster_key", CreativeKey, Preferences.Level.Device);
            }
        }

        private static bool HasModeClusters(Dictionary<string, Cluster> map)
        {
            Cluster creative;
            Cluster singleMulti;
            return map.TryGetValue(CreativeKey, out creative)
                && creative != null
                && !string.IsNullOrEmpty(creative.GatewayUrlRoot)
                && map.TryGetValue(SingleMultiKey, out singleMulti)
                && singleMulti != null
                && !string.IsNullOrEmpty(singleMulti.GatewayUrlRoot);
        }

        public static void SetLabel(object instance, string fieldName, string text)
        {
            if (instance == null)
            {
                return;
            }
            FieldInfo field = instance.GetType().GetField(fieldName, Flags);
            UILabel label = field == null ? null : field.GetValue(instance) as UILabel;
            if (label != null)
            {
                label.text = text;
            }
        }

        private static Dictionary<string, string> MakeNames(string english)
        {
            Dictionary<string, string> names = new Dictionary<string, string>();
            names["en_US"] = english;
            names["ko_KR"] = english;
            names["th_TH"] = english;
            return names;
        }
    }

    [HarmonyPatch(typeof(TitleMenuUserControlBase), "TryUpdateClusters")]
    internal static class TitleMenuUserControlBaseTryUpdateClustersPatch
    {
        private static bool Prefix(TitleMenuUserControlBase __instance, ref bool __result)
        {
            if (GameManager.ConnectCluster != null)
            {
                return true;
            }
            SelectGameModeRuntime.Install(__instance);
            GameManager.SetArenaAuthServer(string.Empty);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(TitleMenuUserControlBase), "ShowCluster")]
    internal static class TitleMenuUserControlBaseShowClusterPatch
    {
        private static void Prefix(TitleMenuUserControlBase __instance)
        {
            if (GameManager.ConnectCluster == null)
            {
                SelectGameModeRuntime.Install(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "SetCluster")]
    internal static class GameManagerSetClusterPatch
    {
        private static void Postfix(string clusterKey)
        {
            SelectGameModeRuntime.SaveMode(clusterKey);
        }
    }

    [HarmonyPatch(typeof(TitleMenuUserControl_PC), "Start")]
    internal static class TitleMenuUserControlPCStartPatch
    {
        private static void Postfix(TitleMenuUserControl_PC __instance)
        {
            SelectGameModeRuntime.SetLabel(__instance, "_serverSelectionLabel", "Select Game Mode");
        }
    }

    [HarmonyPatch(typeof(TitleClusterSelection), "Awake")]
    internal static class TitleClusterSelectionAwakePatch
    {
        private static void Postfix(TitleClusterSelection __instance)
        {
            SelectGameModeRuntime.SetLabel(__instance, "_titleLabel", "Select Game Mode");
        }
    }
}
