using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic;
using Durango.UI;
using HarmonyLib;

namespace BaoX.DurangoOriginal.CareerGuideEnable
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class CareerGuideEnablePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.careerguideenable";
        public const string PluginName = "Career Guide Enable Plugin";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("CareerGuideEnablePlugin loaded");
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

    internal static class CareerGuideRuntime
    {
        internal static void ForceEnableMenu()
        {
            try
            {
                if (GameSystem<MenuSystem>.HasInstance())
                {
                    GameSystem<MenuSystem>.Instance().EnableMenu(MenuType.LearningGuide, true, false);
                }
            }
            catch (Exception ex)
            {
                if (CareerGuideEnablePlugin.Log != null)
                {
                    CareerGuideEnablePlugin.Log.LogWarning("ForceEnableMenu failed: " + ex.Message);
                }
            }
        }

        internal static void ShowSkillGuideButton(SkillGroup skillGroup)
        {
            ForceEnableMenu();
            if (skillGroup == null || skillGroup.LearningGuideButton == null)
            {
                return;
            }
            skillGroup.LearningGuideButton.gameObject.SetActive(true);
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
    internal static class MenuSystemIsHiddenMenuPatch
    {
        private static bool Prefix(MenuType type, ref bool __result)
        {
            if (type == MenuType.LearningGuide)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsEnabled")]
    internal static class MenuSystemIsEnabledPatch
    {
        private static bool Prefix(MenuType type, ref bool __result)
        {
            if (type == MenuType.LearningGuide)
            {
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "GameManager_MainSceneLoaded")]
    internal static class MenuSystemMainSceneLoadedPatch
    {
        private static void Postfix(MenuSystem __instance)
        {
            if (__instance != null)
            {
                __instance.EnableMenu(MenuType.LearningGuide, true, false);
            }
        }
    }

    [HarmonyPatch(typeof(LearningGuideSystem), "CheckAvailable")]
    internal static class LearningGuideSystemCheckAvailablePatch
    {
        private static bool Prefix()
        {
            CareerGuideRuntime.ForceEnableMenu();
            return false;
        }
    }

    [HarmonyPatch(typeof(SkillGroup), "Start")]
    internal static class SkillGroupStartPatch
    {
        private static void Postfix(SkillGroup __instance)
        {
            CareerGuideRuntime.ShowSkillGuideButton(__instance);
        }
    }

    [HarmonyPatch(typeof(SkillGroup), "OnOpened")]
    internal static class SkillGroupOnOpenedPatch
    {
        private static void Postfix(SkillGroup __instance)
        {
            CareerGuideRuntime.ShowSkillGuideButton(__instance);
        }
    }

    [HarmonyPatch(typeof(SkillGroup), "MenuSystem_EnableMenuUpdated")]
    internal static class SkillGroupMenuUpdatedPatch
    {
        private static void Postfix(SkillGroup __instance)
        {
            CareerGuideRuntime.ShowSkillGuideButton(__instance);
        }
    }
}
