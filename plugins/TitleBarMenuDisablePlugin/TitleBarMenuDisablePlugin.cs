using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.UI;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.TitleBarMenuDisable
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TitleBarMenuDisablePlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "com.baox.durango.original.titlebarmenudisable";
        public const string PluginName = "Title Bar Menu Disable Plugin";
        public const string PluginVersion = "1.0.0";

        internal static ConfigEntry<bool> Enabled;
        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Disable the PC scrollable menu strip in fullscreen title bars.");
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

    // TitleBarMenuGroup owns the horizontal scrollable icon strip shown in
    // fullscreen PC title bars. Disable its root before the first rendered
    // frame, leaving the normal title, Back and Close buttons untouched.
    [HarmonyPatch(typeof(TitleBarMenuGroup), "Start")]
    internal static class DisableTitleBarMenuStartPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(TitleBarMenuGroup __instance)
        {
            if (!TitleBarMenuDisablePlugin.Enabled.Value) return true;
            __instance.gameObject.SetActive(false);
            return false;
        }
    }

    // UITitleWidget_PC normally shortens the title against the strip's Previous
    // button. Return no anchor after disabling the strip so the full title width
    // becomes available to the Shop currency widgets.
    [HarmonyPatch(typeof(TitleBarMenuGroup), "TitleBarRightAnchor",
        MethodType.Getter)]
    internal static class DisableTitleBarRightAnchorPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref Transform __result)
        {
            if (TitleBarMenuDisablePlugin.Enabled.Value) __result = null;
        }
    }
}
