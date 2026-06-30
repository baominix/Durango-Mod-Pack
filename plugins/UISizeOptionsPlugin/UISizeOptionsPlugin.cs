using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace UISizeOptionsPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UISizeOptionsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "local.durango.uisizeoptions";
        public const string PluginName = "Durango UI Size Options Plugin";
        public const string PluginVersion = "0.2.0";

        internal static ManualLogSource Log;
        private Harmony _harmony;
        private float _nextRetry;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            UISizeOptions.ApplyPatches(_harmony);
            UISizeOptions.ExtendSettings();
            Logger.LogInfo("UI size options enabled.");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < _nextRetry || UISizeOptions.Expanded)
            {
                return;
            }

            _nextRetry = Time.realtimeSinceStartup + 1f;
            UISizeOptions.ExtendSettings();
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

    internal static class UISizeOptions
    {
        private const string UiSizeKey = "ui_size";
        private const string VeryLargeValue = "800";
        private const string LargeValue = "1024";
        private const string NormalValue = "1280";
        private const string SmallValue = "1600";
        private const string VerySmallValue = "1920";

        // Dropdown order: Very large, Large, Normal, Small, Very small
        private static readonly string[] DesiredOptions = new string[]
        {
            VeryLargeValue,
            LargeValue,
            NormalValue,
            SmallValue,
            VerySmallValue
        };
        private static readonly object LockObj = new object();
        private static bool _expanded;

        public static bool Expanded
        {
            get { return _expanded; }
        }

        public static void ApplyPatches(Harmony harmony)
        {
            Patch(harmony, "Durango.System.Config.ConfigInstance:LoadFromJson", null, "AfterConfigSettingsChanged");
            Patch(harmony, "Durango.System.Config.ConfigInstance:LoadConfigValue", "BeforeConfigValueLoad", null);
            Patch(harmony, "Durango.System.Config.ConfigInstance:ChangeUISize", "ChangeUISizePrefix", null);
            Patch(harmony, "Durango.UI.DropdownWidget:Localize", "DropdownLocalizePrefix", null);
        }

        private static void Patch(Harmony harmony, string targetName, string prefixName, string postfixName)
        {
            MethodInfo target = ResolveMethod(targetName);
            if (target == null)
            {
                WriteLog("Patch target not found: " + targetName);
                return;
            }

            HarmonyMethod prefix = null;
            HarmonyMethod postfix = null;

            if (!string.IsNullOrEmpty(prefixName))
            {
                MethodInfo prefixMethod = typeof(UISizeOptions).GetMethod(prefixName, BindingFlags.Static | BindingFlags.NonPublic);
                if (prefixMethod != null)
                {
                    prefix = new HarmonyMethod(prefixMethod);
                }
            }

            if (!string.IsNullOrEmpty(postfixName))
            {
                MethodInfo postfixMethod = typeof(UISizeOptions).GetMethod(postfixName, BindingFlags.Static | BindingFlags.NonPublic);
                if (postfixMethod != null)
                {
                    postfix = new HarmonyMethod(postfixMethod);
                }
            }

            harmony.Patch(target, prefix, postfix, null, null, null);
        }

        private static MethodInfo ResolveMethod(string targetName)
        {
            int split = targetName.LastIndexOf(':');
            if (split <= 0 || split >= targetName.Length - 1)
            {
                return null;
            }

            string typeName = targetName.Substring(0, split);
            string methodName = targetName.Substring(split + 1);
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return null;
            }

            return FindMethod(type, methodName);
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < methods.Length; i++)
                {
                    if (string.Equals(methods[i].Name, name, StringComparison.Ordinal))
                    {
                        return methods[i];
                    }
                }

                type = type.BaseType;
            }

            return null;
        }

        public static void ExtendSettings()
        {
            lock (LockObj)
            {
                Type configType = AccessTools.TypeByName("Durango.System.Config.ConfigInstance");
                if (configType == null)
                {
                    return;
                }

                PropertyInfo settingsProperty = configType.GetProperty("Settings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (settingsProperty == null)
                {
                    return;
                }

                object settings = settingsProperty.GetValue(null, null);
                IEnumerable enumerable = settings as IEnumerable;
                if (enumerable == null)
                {
                    return;
                }

                bool changed = false;
                foreach (object pair in enumerable)
                {
                    PropertyInfo valueProperty = pair.GetType().GetProperty("Value");
                    if (valueProperty == null)
                    {
                        continue;
                    }

                    IEnumerable list = valueProperty.GetValue(pair, null) as IEnumerable;
                    if (list == null)
                    {
                        continue;
                    }

                    foreach (object setting in list)
                    {
                        if (!IsUiSizeSetting(setting))
                        {
                            continue;
                        }

                        FieldInfo optionsField = FindField(setting.GetType(), "Options");
                        if (optionsField == null)
                        {
                            continue;
                        }

                        string[] options = optionsField.GetValue(setting) as string[];
                        string[] expanded = AppendOptions(options);
                        if (!ReferenceEquals(options, expanded))
                        {
                            optionsField.SetValue(setting, expanded);
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    _expanded = true;
                    WriteLog("Normalized UI size options order: Very large, Large, Normal, Small, Very small");
                }
            }
        }

        private static bool IsUiSizeSetting(object setting)
        {
            if (setting == null)
            {
                return false;
            }

            FieldInfo keyField = FindField(setting.GetType(), "Key");
            if (keyField == null)
            {
                return false;
            }

            string key = keyField.GetValue(setting) as string;
            return string.Equals(key, UiSizeKey, StringComparison.Ordinal);
        }

        private static string[] AppendOptions(string[] options)
        {
            // Force the UI size dropdown to a fixed order instead of appending.
            // Required order:
            // Very large, Large, Normal, Small, Very small
            if (IsSameOptions(options, DesiredOptions))
            {
                return options;
            }

            string[] copy = new string[DesiredOptions.Length];
            Array.Copy(DesiredOptions, copy, DesiredOptions.Length);
            return copy;
        }

        private static bool IsSameOptions(string[] left, string[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static void BeforeConfigValueLoad()
        {
            ExtendSettings();
        }

        private static void AfterConfigSettingsChanged()
        {
            ExtendSettings();
        }

        private static bool ChangeUISizePrefix(string __0)
        {
            int size;
            if (!TryParseSize(__0, out size))
            {
                return true;
            }

            Type uiManagerType = AccessTools.TypeByName("UIManager");
            if (uiManagerType == null)
            {
                uiManagerType = AccessTools.TypeByName("Durango.UI.UIManager");
            }

            MethodInfo setUiSize = uiManagerType == null ? null : FindMethod(uiManagerType, "SetUISize");
            if (setUiSize == null)
            {
                return true;
            }

            setUiSize.Invoke(null, new object[] { size });
            return false;
        }

        private static bool DropdownLocalizePrefix(object __instance, string __0, ref string __result)
        {
            string label;
            if (!IsDropdownUiSize(__instance) || !TryGetLabel(__0, out label))
            {
                return true;
            }

            __result = label;
            return false;
        }

        private static bool IsDropdownUiSize(object widget)
        {
            if (widget == null)
            {
                return false;
            }

            PropertyInfo settingProperty = widget.GetType().GetProperty("Setting", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (settingProperty == null)
            {
                return false;
            }

            object setting = settingProperty.GetValue(widget, null);
            return IsUiSizeSetting(setting);
        }

        private static bool TryParseSize(string value, out int size)
        {
            size = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string normalized = value.Trim().Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
            if (normalized == "large")
            {
                size = 1024;
                return true;
            }

            if (normalized == "very_large")
            {
                size = 800;
                return true;
            }

            return int.TryParse(value, out size);
        }

        private static bool TryGetLabel(string value, out string label)
        {
            label = null;
            if (string.Equals(value, VeryLargeValue, StringComparison.Ordinal))
            {
                label = "Very large";
                return true;
            }

            if (string.Equals(value, LargeValue, StringComparison.Ordinal))
            {
                label = "Large";
                return true;
            }

            if (string.Equals(value, NormalValue, StringComparison.Ordinal))
            {
                label = "Normal";
                return true;
            }

            if (string.Equals(value, SmallValue, StringComparison.Ordinal))
            {
                label = "Small";
                return true;
            }

            if (string.Equals(value, VerySmallValue, StringComparison.Ordinal))
            {
                label = "Very small";
                return true;
            }

            return false;
        }

        private static void WriteLog(string message)
        {
            if (UISizeOptionsPlugin.Log != null)
            {
                UISizeOptionsPlugin.Log.LogInfo(message);
            }
        }
    }
}
