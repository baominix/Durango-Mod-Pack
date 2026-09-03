using System;
using System.Reflection;
using BepInEx;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.System;
using Durango.UI;
using HarmonyLib;

namespace BaoX.DurangoOriginal.WeatherModeMod
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class WeatherModePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.weathermode";
        public const string PluginName = "Weather Mode Plugin";
        public const string PluginVersion = "0.1.0";

        private Harmony _harmony;

        private void Awake()
        {
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(WeatherModePlugin).Assembly);
            Logger.LogInfo("WeatherModePlugin loaded");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }

    internal static class WeatherModeRules
    {
        private const string PreferenceKey = "baox_select_game_mode";
        private const string CreativeKey = "free_offline";
        private const string SurvivalKey = "single_multi_offline";

        internal static bool IsCreative()
        {
            string selected = Preferences.GetString(PreferenceKey, string.Empty, Preferences.Level.Device);
            if (string.Equals(selected, CreativeKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(selected, SurvivalKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string clusterKey = GameManager.ClusterKey;
            if (string.Equals(clusterKey, CreativeKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(clusterKey, SurvivalKey, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return GameManager.ClusterMode == Mode.Editable;
        }
    }

    [HarmonyPatch(typeof(CircularTimeGauge), "OnClick")]
    internal static class CircularTimeGaugeOnClickPatch
    {
        private static readonly MethodInfo ShowTooltipMethod = typeof(CircularTimeGauge).GetMethod(
            "ShowTooltip",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(float) },
            null);

        private static bool Prefix(CircularTimeGauge __instance)
        {
            if (WeatherModeRules.IsCreative())
            {
                return true;
            }

            if (ShowTooltipMethod != null)
            {
                ShowTooltipMethod.Invoke(__instance, new object[] { 60f });
            }

            return false;
        }
    }
}


