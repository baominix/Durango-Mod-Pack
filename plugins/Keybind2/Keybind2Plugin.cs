using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using Durango.System.Config;
using Durango.UI;
using HarmonyLib;

namespace BaoX.DurangoOriginal.Keybind2
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Keybind2Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.keybind2";
        public const string PluginName = "Keybind2";
        public const string PluginVersion = "0.1.0";

        private Harmony _harmony;

        private void Awake()
        {
            Keybind2Runtime.InstallPage();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Logger.LogInfo("Keybind2 loaded");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }

    internal static class Keybind2Runtime
    {
        internal const string CategoryKey = "keybind2";

        internal static void InstallPage()
        {
            if (ConfigInstance.Settings == null || ConfigInstance.Settings.ContainsKey(CategoryKey))
            {
                return;
            }

            ConfigInstance.Settings[CategoryKey] = new List<Setting>
            {
                new ValueSetting
                {
                    Key = "keybind2_placeholder",
                    Type = SettingType.Category,
                    Default = string.Empty,
                    Value = string.Empty,
                    PrepareLabelText = string.Empty
                }
            };
        }

        internal static IEnumerable<string> AppendPage(IEnumerable<string> source)
        {
            List<string> result = new List<string>();
            if (source != null)
            {
                foreach (string category in source)
                {
                    if (!result.Contains(category))
                    {
                        result.Add(category);
                    }
                }
            }

            if (!result.Contains(CategoryKey))
            {
                result.Add(CategoryKey);
            }
            return result;
        }
    }

    [HarmonyPatch(typeof(ConfigInstance), "LoadFromJson")]
    internal static class ConfigInstanceLoadFromJsonPatch
    {
        private static void Postfix()
        {
            Keybind2Runtime.InstallPage();
        }
    }

    [HarmonyPatch(typeof(ConfigInstance), "LoadConfigValue")]
    internal static class ConfigInstanceLoadConfigValuePatch
    {
        private static void Prefix()
        {
            Keybind2Runtime.InstallPage();
        }

        private static void Postfix()
        {
            Keybind2Runtime.InstallPage();
        }
    }

    [HarmonyPatch(typeof(ConfigTabWidget), "EnumerateSettings")]
    internal static class ConfigTabWidgetEnumerateSettingsPatch
    {
        private static void Postfix(ref IEnumerable<string> __result)
        {
            Keybind2Runtime.InstallPage();
            __result = Keybind2Runtime.AppendPage(__result);
        }
    }

    [HarmonyPatch(typeof(ConfigTabItem), "Set")]
    internal static class ConfigTabItemSetPatch
    {
        private static void Postfix(ConfigTabItem __instance, string category)
        {
            if (category != Keybind2Runtime.CategoryKey)
            {
                return;
            }

            FieldInfo field = typeof(ConfigTabItem).GetField("_nameLabel", BindingFlags.Instance | BindingFlags.NonPublic);
            UILabel label = field == null ? null : field.GetValue(__instance) as UILabel;
            if (label != null)
            {
                label.text = "Keybind2";
            }
        }
    }

    [HarmonyPatch(typeof(ConfigMainWidget), "SetConfigLayout")]
    internal static class ConfigMainWidgetSetConfigLayoutPatch
    {
        private static void Postfix(ConfigMainWidget __instance, string category)
        {
            if (category != Keybind2Runtime.CategoryKey)
            {
                return;
            }

            MethodInfo clear = typeof(ConfigMainWidget).GetMethod("ClearAllObjects", BindingFlags.Instance | BindingFlags.NonPublic);
            if (clear != null)
            {
                clear.Invoke(__instance, null);
            }
        }
    }
}
