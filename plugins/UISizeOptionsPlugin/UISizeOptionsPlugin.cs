using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.System;
using HarmonyLib;
using UnityEngine;

namespace UISizeOptionsPlugin
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class UISizeOptionsPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.uisizeoptions";
        public const string PluginName = "Durango UI Size Options Plugin";
        public const string PluginVersion = "0.4.6";

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
        private const string MobileUiSizeSaveKey = "option:ui_size_mobile";
        private const string MobileUiSizeProfileKey = "option:ui_size_mobile_profile";
        private const string ReverseMobileUiSizeProfile = "reverse_600_v1";
        private const string PreviousMobileUiSizeProfile = "ascending_1024_v2";
        private const string MobileUiSizeProfile = "ascending_1400_v3";
        private const string PCVeryLargeValue = "800";
        private const string PCLargeValue = "1024";
        private const string PCNormalValue = "1280";
        private const string PCSmallValue = "1600";
        private const string PCVerySmallValue = "1920";
        private const string MobileVeryLargeValue = "1400";
        private const string MobileLargeValue = "1600";
        private const string MobileNormalValue = "1800";
        private const string MobileSmallValue = "2000";
        private const string MobileVerySmallValue = "2200";

        private static readonly string[] PCDesiredOptions = new string[]
        {
            PCVeryLargeValue,
            PCLargeValue,
            PCNormalValue,
            PCSmallValue,
            PCVerySmallValue
        };
        private static readonly string[] MobileDesiredOptions = new string[]
        {
            MobileVeryLargeValue,
            MobileLargeValue,
            MobileNormalValue,
            MobileSmallValue,
            MobileVerySmallValue
        };
        private static readonly Dictionary<string, string[]> LabelsByLocale =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "en_US",
                    new string[] { "Very large", "Large", "Normal", "Small", "Very small" }
                },
                {
                    "ko_KR",
                    new string[]
                    {
                        "\uB9E4\uC6B0 \uD06C\uAC8C",
                        "\uD06C\uAC8C",
                        "\uBCF4\uD1B5",
                        "\uC791\uAC8C",
                        "\uB9E4\uC6B0 \uC791\uAC8C"
                    }
                },
                {
                    "es_MX",
                    new string[] { "Muy grande", "Grande", "Normal", "Peque\u00F1o", "Muy peque\u00F1o" }
                },
                {
                    "pt_BR",
                    new string[] { "Muito grande", "Grande", "Normal", "Pequeno", "Muito pequeno" }
                },
                {
                    "id_ID",
                    new string[] { "Sangat besar", "Besar", "Normal", "Kecil", "Sangat kecil" }
                },
                {
                    "ru_RU",
                    new string[]
                    {
                        "\u041E\u0447\u0435\u043D\u044C \u0431\u043E\u043B\u044C\u0448\u043E\u0439",
                        "\u0411\u043E\u043B\u044C\u0448\u043E\u0439",
                        "\u041E\u0431\u044B\u0447\u043D\u044B\u0439",
                        "\u041C\u0430\u043B\u0435\u043D\u044C\u043A\u0438\u0439",
                        "\u041E\u0447\u0435\u043D\u044C \u043C\u0430\u043B\u0435\u043D\u044C\u043A\u0438\u0439"
                    }
                },
                {
                    "th_TH",
                    new string[]
                    {
                        "\u0E43\u0E2B\u0E0D\u0E48\u0E21\u0E32\u0E01",
                        "\u0E43\u0E2B\u0E0D\u0E48",
                        "\u0E1B\u0E01\u0E15\u0E34",
                        "\u0E40\u0E25\u0E47\u0E01",
                        "\u0E40\u0E25\u0E47\u0E01\u0E21\u0E32\u0E01"
                    }
                },
                {
                    "de_DE",
                    new string[] { "Sehr gro\u00DF", "Gro\u00DF", "Normal", "Klein", "Sehr klein" }
                },
                {
                    "fr_FR",
                    new string[]
                    {
                        "Tr\u00E8s grande",
                        "Grande",
                        "Normale",
                        "Petite",
                        "Tr\u00E8s petite"
                    }
                },
                {
                    "zh_TW",
                    new string[]
                    {
                        "\u7279\u5927",
                        "\u5927",
                        "\u6A19\u6E96",
                        "\u5C0F",
                        "\u7279\u5C0F"
                    }
                }
            };
        private static readonly object LockObj = new object();
        private static bool _expanded;
        private static bool _loadingConfigValues;

        public static bool Expanded
        {
            get { return _expanded; }
        }

        public static void ApplyPatches(Harmony harmony)
        {
            Patch(harmony, "Durango.System.Config.ConfigInstance:LoadFromJson", null, "AfterConfigSettingsChanged");
            Patch(harmony, "Durango.System.Config.ConfigInstance:LoadConfigValue", "BeforeConfigValueLoad", "AfterConfigValueLoad");
            Patch(harmony, "Durango.System.Config.ConfigInstance:ChangeUISize", "ChangeUISizePrefix", null);
            PatchStringChangeValue(harmony);
            Patch(harmony, "LocalizeSystem:Get", null, "AfterLocalizedTextGet");
            Patch(harmony, "LocalizeSystem:SetLocale", null, "AfterLocaleChanged");
            Patch(harmony, "Durango.UI.DropdownWidget:Localize", "DropdownLocalizePrefix", null);
            Patch(harmony, "Durango.UI.ToggleWidget:OnLocalize", null, "AfterToggleLocalized");
            Patch(harmony, "Durango.UI.ToggleWidget:MoveIndex", "BeforeToggleMoveIndex", null);
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

        private static void PatchStringChangeValue(Harmony harmony)
        {
            Type configType = AccessTools.TypeByName("Durango.System.Config.ConfigInstance");
            MethodInfo target = configType == null ? null : configType.GetMethod(
                "ChangeValue",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(string), typeof(string), typeof(bool) },
                null);
            MethodInfo prefix = typeof(UISizeOptions).GetMethod(
                "BeforeStringValueChanged", BindingFlags.Static | BindingFlags.NonPublic);
            if (target == null || prefix == null)
            {
                WriteLog("Patch target not found: ConfigInstance.ChangeValue(string,string,bool)");
                return;
            }

            harmony.Patch(target, new HarmonyMethod(prefix), null, null, null, null);
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
                        bool mobileSetting = string.Equals(
                            setting.GetType().Name, "ToggleSetting", StringComparison.Ordinal);
                        string[] expanded = AppendOptions(options, mobileSetting);
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

        private static string[] AppendOptions(string[] options, bool mobileSetting)
        {
            string[] desired = mobileSetting ? MobileDesiredOptions : PCDesiredOptions;
            if (IsSameOptions(options, desired))
            {
                return options;
            }

            string[] copy = new string[desired.Length];
            Array.Copy(desired, copy, desired.Length);
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
            _loadingConfigValues = true;
            ExtendSettings();
        }

        private static void AfterConfigValueLoad()
        {
            _loadingConfigValues = false;
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

        private static void AfterLocalizedTextGet(string __0, ref string __result)
        {
            string label;
            if (string.IsNullOrEmpty(__0) ||
                !__0.StartsWith("#config_ui_size_", StringComparison.Ordinal) ||
                !TryGetLabel(
                    __0.Substring("#config_ui_size_".Length),
                    IsMobileUISizeSchema(),
                    out label))
            {
                return;
            }

            __result = label;
        }

        private static bool DropdownLocalizePrefix(
            object __instance,
            string text,
            ref string __result)
        {
            string label;
            if (!IsDropdownUiSize(__instance) ||
                !TryGetLabel(text, false, out label))
            {
                return true;
            }

            // The original client owns #config_ui_size_1600 and translates it
            // as "Very small". This plugin intentionally uses 1600 for Small,
            // so resolve UI-size labels at the dropdown itself and leave the
            // actual numeric setting values untouched.
            __result = label;
            return false;
        }

        private static void AfterToggleLocalized(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            PropertyInfo parentProperty = __instance.GetType().GetProperty(
                "Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object parent = parentProperty == null
                ? null
                : parentProperty.GetValue(__instance, null);
            if (parent == null)
            {
                return;
            }

            PropertyInfo keyProperty = parent.GetType().GetProperty(
                "Key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo valueField = FindField(parent.GetType(), "Value");
            string key = keyProperty == null
                ? null
                : keyProperty.GetValue(parent, null) as string;
            string value = valueField == null
                ? null
                : valueField.GetValue(parent) as string;
            if (!string.Equals(key, UiSizeKey, StringComparison.Ordinal))
            {
                return;
            }

            string label;
            if (!TryGetLabel(value, true, out label))
            {
                return;
            }

            FieldInfo textField = FindField(__instance.GetType(), "Text");
            object textWidget = textField == null ? null : textField.GetValue(__instance);
            PropertyInfo textProperty = textWidget == null
                ? null
                : textWidget.GetType().GetProperty(
                    "text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (textProperty != null)
            {
                textProperty.SetValue(textWidget, label, null);
            }
        }

        private static void BeforeToggleMoveIndex(object __instance, ref int __0)
        {
            if (__0 == 0 || !IsToggleUiSize(__instance))
            {
                return;
            }

            // ToggleWidget binds Left=-1 and Right=+1. Mobile UI size uses a
            // visual-size scale whose expected arrow direction is opposite,
            // so invert only this setting and leave UI Mode/toggles untouched.
            __0 = -__0;
        }

        private static bool IsToggleUiSize(object instance)
        {
            if (instance == null)
            {
                return false;
            }

            PropertyInfo parentProperty = instance.GetType().GetProperty(
                "Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object parent = parentProperty == null
                ? null
                : parentProperty.GetValue(instance, null);
            if (parent == null)
            {
                return false;
            }

            PropertyInfo keyProperty = parent.GetType().GetProperty(
                "Key", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            string key = keyProperty == null
                ? null
                : keyProperty.GetValue(parent, null) as string;
            return string.Equals(key, UiSizeKey, StringComparison.Ordinal);
        }

        private static void BeforeStringValueChanged(
            string key,
            ref string value,
            ref bool save)
        {
            if (!string.Equals(key, UiSizeKey, StringComparison.Ordinal) || !IsMobileUISizeSchema())
            {
                return;
            }

            if (_loadingConfigValues)
            {
                value = GetStoredMobileSize();
                return;
            }

            if (!save)
            {
                return;
            }

            if (!IsDesiredValue(value))
            {
                value = MobileNormalValue;
            }
            PlayerPrefs.SetString(MobileUiSizeSaveKey, value);
            PlayerPrefs.Save();

            // Let ConfigInstance update the live setting and UIManager, but do
            // not overwrite the PC value stored under option:ui_size.
            save = false;
        }

        private static string GetStoredMobileSize()
        {
            string value = PlayerPrefs.GetString(MobileUiSizeSaveKey, PCNormalValue);
            string profile = PlayerPrefs.GetString(MobileUiSizeProfileKey, string.Empty);
            if (!string.Equals(
                profile,
                MobileUiSizeProfile,
                StringComparison.Ordinal))
            {
                value = MigrateMobileValue(value, profile);
                PlayerPrefs.SetString(MobileUiSizeSaveKey, value);
                PlayerPrefs.SetString(MobileUiSizeProfileKey, MobileUiSizeProfile);
                PlayerPrefs.Save();
            }
            return IsDesiredValue(value) ? value : MobileNormalValue;
        }

        private static string MigrateMobileValue(string value, string profile)
        {
            if (string.Equals(profile, PreviousMobileUiSizeProfile, StringComparison.Ordinal))
            {
                if (string.Equals(value, "1024", StringComparison.Ordinal))
                {
                    return MobileVeryLargeValue;
                }
                if (string.Equals(value, "1280", StringComparison.Ordinal))
                {
                    return MobileLargeValue;
                }
                if (string.Equals(value, "1420", StringComparison.Ordinal))
                {
                    return MobileNormalValue;
                }
                if (string.Equals(value, "1600", StringComparison.Ordinal))
                {
                    return MobileSmallValue;
                }
                if (string.Equals(value, "1920", StringComparison.Ordinal))
                {
                    return MobileVerySmallValue;
                }
            }

            if (string.Equals(profile, ReverseMobileUiSizeProfile, StringComparison.Ordinal))
            {
                if (string.Equals(value, "1420", StringComparison.Ordinal))
                {
                    return MobileVeryLargeValue;
                }
                if (string.Equals(value, "1280", StringComparison.Ordinal))
                {
                    return MobileLargeValue;
                }
                if (string.Equals(value, "1024", StringComparison.Ordinal))
                {
                    return MobileNormalValue;
                }
                if (string.Equals(value, "800", StringComparison.Ordinal))
                {
                    return MobileSmallValue;
                }
                if (string.Equals(value, "600", StringComparison.Ordinal))
                {
                    return MobileVerySmallValue;
                }
            }

            if (string.Equals(value, "800", StringComparison.Ordinal))
            {
                return MobileVeryLargeValue;
            }
            if (string.Equals(value, "1024", StringComparison.Ordinal))
            {
                return MobileLargeValue;
            }
            if (string.Equals(value, "1280", StringComparison.Ordinal))
            {
                return MobileNormalValue;
            }
            if (string.Equals(value, "1420", StringComparison.Ordinal))
            {
                return MobileSmallValue;
            }
            return string.Equals(value, "1600", StringComparison.Ordinal) ||
                string.Equals(value, "1920", StringComparison.Ordinal)
                ? MobileVerySmallValue
                : MobileNormalValue;
        }

        private static bool IsDesiredValue(string value)
        {
            for (int i = 0; i < MobileDesiredOptions.Length; i++)
            {
                if (string.Equals(MobileDesiredOptions[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsMobileUISizeSchema()
        {
            Type configType = AccessTools.TypeByName("Durango.System.Config.ConfigInstance");
            PropertyInfo settingsProperty = configType == null ? null : configType.GetProperty(
                "Settings", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerable settings = settingsProperty == null
                ? null
                : settingsProperty.GetValue(null, null) as IEnumerable;
            if (settings != null)
            {
                foreach (object pair in settings)
                {
                    PropertyInfo valueProperty = pair.GetType().GetProperty("Value");
                    IEnumerable list = valueProperty == null
                        ? null
                        : valueProperty.GetValue(pair, null) as IEnumerable;
                    if (list == null)
                    {
                        continue;
                    }
                    foreach (object setting in list)
                    {
                        if (IsUiSizeSetting(setting))
                        {
                            return string.Equals(
                                setting.GetType().Name, "ToggleSetting", StringComparison.Ordinal);
                        }
                    }
                }
            }

            // During the very first config load the setting collection can be
            // unavailable, so retain the platform selector only as fallback.
            return !IsPCUILayout();
        }

        private static void AfterLocaleChanged()
        {
            RefreshActiveUiSizeDropdowns();
        }

        private static void RefreshActiveUiSizeDropdowns()
        {
            if (!IsPCUILayout())
            {
                return;
            }

            Type dropdownType = AccessTools.TypeByName("Durango.UI.DropdownWidget");
            if (dropdownType == null)
            {
                return;
            }

            UnityEngine.Object[] widgets = UnityEngine.Object.FindObjectsOfType(dropdownType);
            for (int i = 0; i < widgets.Length; i++)
            {
                object widget = widgets[i];
                if (!IsDropdownUiSize(widget))
                {
                    continue;
                }

                PropertyInfo settingProperty = widget.GetType().GetProperty(
                    "Setting",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo optionsField = FindField(widget.GetType(), "Options");
                FieldInfo closeOnClickField = FindField(widget.GetType(), "IsCloseOnClick");
                MethodInfo initMethod = FindMethod(widget.GetType(), "Init");
                if (settingProperty == null ||
                    optionsField == null ||
                    closeOnClickField == null ||
                    initMethod == null)
                {
                    continue;
                }

                object setting = settingProperty.GetValue(widget, null);
                string[] options = optionsField.GetValue(widget) as string[];
                bool closeOnClick = (bool)closeOnClickField.GetValue(widget);
                initMethod.Invoke(widget, new object[] { setting, options, closeOnClick });
            }
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

        private static bool IsPCUILayout()
        {
            return Platform.Instance == null || Platform.Instance.UsePCUI;
        }

        private static bool TryGetLabel(string value, bool mobileSetting, out string label)
        {
            label = null;
            int index = -1;
            string[] values = mobileSetting ? MobileDesiredOptions : PCDesiredOptions;
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(value, values[i], StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return false;
            }

            string locale = LocalizeSystem.Locale;
            string[] labels;
            if (string.IsNullOrEmpty(locale) || !LabelsByLocale.TryGetValue(locale, out labels))
            {
                labels = LabelsByLocale["en_US"];
            }

            label = labels[index];
            return true;
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
