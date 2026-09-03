using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic.Clusters;
using Durango.Logic.InputSystem;
using Durango.System.Config;
using Durango.UI;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.KeybindSettings
{
    [BepInPlugin("baox.durango.original.keybindsettings", "Keybind Settings Plugin", "0.1.0")]
    public sealed class KeybindSettingsPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = base.Logger;
            new Harmony("baox.durango.original.keybindsettings").PatchAll();
            KeybindRuntime.ExtendSettings();
            Log.LogInfo("KeybindSettingsPlugin loaded");
        }

        private void Update()
        {
            KeybindRuntime.ExtendSettings();
            KeybindRuntime.ApplyToLiveKeyboard();
        }
    }

    internal static class KeybindRuntime
    {
        internal const string CategoryKey = "keybind";
        private static bool _settingsReady;
        private static bool _keyboardApplied;

        private static readonly string[] KeyOptions = new string[]
        {
            "Tab", "Return", "Escape", "Space",
            "P", "K", "M", "C", "I", "J", "T", "N", "B", "F", "G", "H", "L", "O", "Q", "R", "U", "Y", "Z",
            "Alpha1", "Alpha2", "Alpha3", "Alpha4", "Alpha5", "Alpha6", "Alpha7", "Alpha8", "Alpha9", "Alpha0",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
        };

        private static readonly BindDef[] Binds = new BindDef[]
        {
            new BindDef("keybind_game_menu", "Game Menu", InputCommand.ShowMenuList, "Tab", Layer.Menu),
            new BindDef("keybind_character", "Character", InputCommand.Character, "P", Layer.Menu),
            new BindDef("keybind_skill", "Skill", InputCommand.Skill, "K", Layer.Menu),
            new BindDef("keybind_craft", "Craft / Build", InputCommand.Recipe, "C", Layer.Menu),
            new BindDef("keybind_bag", "Bag", InputCommand.Inventory, "I", Layer.Menu),
            new BindDef("keybind_map", "Map", InputCommand.WorldMap, "M", Layer.Menu),
            new BindDef("keybind_quest", "Quest", InputCommand.Quest, "J", Layer.Menu),
            new BindDef("keybind_chat", "Chat", InputCommand.PopChatImmediately, "Return", Layer.Default),
            new BindDef("keybind_emoticons", "Emoticons", InputCommand.CommunicationMenuButtonAction, "T", Layer.Default)
        };

        internal static void ExtendSettings()
        {
            if (ConfigInstance.Settings == null)
            {
                return;
            }

            List<Setting> list;
            if (!ConfigInstance.Settings.TryGetValue(CategoryKey, out list))
            {
                list = new List<Setting>();
                ConfigInstance.Settings[CategoryKey] = list;
            }

            EnsureCategory(list);
            for (int i = 0; i < Binds.Length; i++)
            {
                EnsureBind(list, Binds[i]);
            }

            _settingsReady = true;
        }

        internal static IEnumerable<string> EnumerateSettingsPatched()
        {
            if (ConfigInstance.Settings == null)
            {
                yield break;
            }

            foreach (KeyValuePair<string, List<Setting>> kv in ConfigInstance.Settings)
            {
                if (GameManager.ClusterMode != Mode.Online && kv.Key != "default" && kv.Key != "screen" && kv.Key != CategoryKey)
                {
                    continue;
                }

                bool hidden = true;
                List<Setting> settings = kv.Value;
                for (int i = 0; i < settings.Count; i++)
                {
                    if (!Setting.IsHidden(settings[i]))
                    {
                        hidden = false;
                        break;
                    }
                }

                if (!hidden)
                {
                    yield return kv.Key;
                }
            }
        }

        internal static void MarkKeyboardDirty()
        {
            _keyboardApplied = false;
        }

        internal static void ApplyToLiveKeyboard()
        {
            if (!_settingsReady || _keyboardApplied || !GameSystem<InputSystem>.HasInstance())
            {
                return;
            }

            InputSystem inputSystem = GameSystem<InputSystem>.Instance();
            if (inputSystem == null || inputSystem.Keyboard == null)
            {
                return;
            }

            ApplyToKeyboard(inputSystem.Keyboard);
        }

        internal static void ApplyToKeyboard(InputKeyboard keyboard)
        {
            if (keyboard == null)
            {
                return;
            }

            KeyCodeDictionary map = GetKeyMap(keyboard);
            if (map == null)
            {
                return;
            }

            RemoveManagedBindings(map);

            for (int i = 0; i < Binds.Length; i++)
            {
                BindDef bind = Binds[i];
                KeyCode code;
                if (TryParseKeyCode(GetValue(bind), out code))
                {
                    map[code, Modifier.None, bind.Layer, Trigger.Down] = bind.Command;
                }
            }

            RebuildReverseMap(map);
            _keyboardApplied = true;
        }

        internal static bool TryGetCaption(InputCommand command, out string caption)
        {
            for (int i = 0; i < Binds.Length; i++)
            {
                if (Binds[i].Command == command)
                {
                    KeyCode code;
                    if (TryParseKeyCode(GetValue(Binds[i]), out code))
                    {
                        caption = InputKeyboard.KeyToCaption(code);
                        return true;
                    }
                }
            }

            caption = null;
            return false;
        }

        internal static bool IsKeybindSetting(string key)
        {
            return FindBind(key) != null;
        }

        internal static string GetOptionCaption(string key, string value)
        {
            if (!IsKeybindSetting(key))
            {
                return null;
            }

            KeyCode code;
            return TryParseKeyCode(value, out code) ? InputKeyboard.KeyToCaption(code) : value;
        }

        private static void EnsureCategory(List<Setting> list)
        {
            if (FindSetting(list, "keybind_category") != null)
            {
                return;
            }

            list.Insert(0, new ValueSetting
            {
                Key = "keybind_category",
                Type = SettingType.Category,
                Default = "Keybind",
                Value = "Keybind",
                PrepareLabelText = "Keybind"
            });
        }

        private static void EnsureBind(List<Setting> list, BindDef bind)
        {
            DropdownSetting setting = FindSetting(list, bind.Key) as DropdownSetting;
            string value = PlayerPrefs.GetString("option:" + bind.Key, bind.DefaultKey);
            if (!Contains(KeyOptions, value))
            {
                value = bind.DefaultKey;
            }

            if (setting == null)
            {
                setting = new DropdownSetting
                {
                    Key = bind.Key,
                    Type = SettingType.Dropdown,
                    Default = bind.DefaultKey,
                    Value = value,
                    PrepareLabelText = bind.Label,
                    Options = KeyOptions,
                    ButtonClickClose = true,
                    Custom = false
                };
                list.Add(setting);
            }
            else
            {
                setting.Default = bind.DefaultKey;
                setting.PrepareLabelText = bind.Label;
                setting.Options = KeyOptions;
                setting.ButtonClickClose = true;
                setting.Value = value;
            }
        }

        private static Setting FindSetting(List<Setting> list, string key)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Key == key)
                {
                    return list[i];
                }
            }

            return null;
        }

        private static BindDef FindBind(string key)
        {
            for (int i = 0; i < Binds.Length; i++)
            {
                if (Binds[i].Key == key)
                {
                    return Binds[i];
                }
            }

            return null;
        }

        private static string GetValue(BindDef bind)
        {
            string value = ConfigInstance.GetValue<string>(bind.Key, null);
            if (string.IsNullOrEmpty(value))
            {
                value = PlayerPrefs.GetString("option:" + bind.Key, bind.DefaultKey);
            }
            if (!Contains(KeyOptions, value))
            {
                value = bind.DefaultKey;
            }
            return value;
        }

        private static bool Contains(string[] values, string value)
        {
            if (values == null || value == null)
            {
                return false;
            }
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseKeyCode(string value, out KeyCode code)
        {
            code = KeyCode.None;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            try
            {
                code = (KeyCode)Enum.Parse(typeof(KeyCode), value, true);
                return code != KeyCode.None;
            }
            catch
            {
                return false;
            }
        }

        private static KeyCodeDictionary GetKeyMap(InputKeyboard keyboard)
        {
            FieldInfo field = typeof(InputKeyboard).GetField("_keyMap", BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(keyboard) as KeyCodeDictionary;
        }

        private static void RemoveManagedBindings(KeyCodeDictionary map)
        {
            List<KeySet> remove = new List<KeySet>();
            foreach (KeyValuePair<KeySet, InputCommand> pair in map)
            {
                if (IsManagedCommand(pair.Value) && pair.Key.Modifiers == Modifier.None && pair.Key.Trigger == Trigger.Down)
                {
                    remove.Add(pair.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                map.Remove(remove[i]);
            }
        }

        private static bool IsManagedCommand(InputCommand command)
        {
            for (int i = 0; i < Binds.Length; i++)
            {
                if (Binds[i].Command == command)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RebuildReverseMap(KeyCodeDictionary map)
        {
            Dictionary<InputCommand, List<KeySet>> reverse = new Dictionary<InputCommand, List<KeySet>>();
            foreach (KeyValuePair<KeySet, InputCommand> pair in map)
            {
                List<KeySet> list;
                if (!reverse.TryGetValue(pair.Value, out list))
                {
                    list = new List<KeySet>();
                    reverse[pair.Value] = list;
                }
                list.Add(pair.Key);
            }

            FieldInfo field = typeof(KeyCodeDictionary).GetField("_reverseMap", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(map, reverse);
            }
        }

        private sealed class BindDef
        {
            internal readonly string Key;
            internal readonly string Label;
            internal readonly InputCommand Command;
            internal readonly string DefaultKey;
            internal readonly Layer Layer;

            internal BindDef(string key, string label, InputCommand command, string defaultKey, Layer layer)
            {
                Key = key;
                Label = label;
                Command = command;
                DefaultKey = defaultKey;
                Layer = layer;
            }
        }
    }

    [HarmonyPatch(typeof(ConfigInstance), "LoadFromJson")]
    internal static class ConfigInstanceLoadFromJsonPatch
    {
        private static void Postfix()
        {
            KeybindRuntime.ExtendSettings();
        }
    }

    [HarmonyPatch(typeof(ConfigInstance), "LoadConfigValue")]
    internal static class ConfigInstanceLoadConfigValuePatch
    {
        private static void Prefix()
        {
            KeybindRuntime.ExtendSettings();
        }

        private static void Postfix()
        {
            KeybindRuntime.ExtendSettings();
            KeybindRuntime.MarkKeyboardDirty();
        }
    }

    [HarmonyPatch(typeof(ConfigInstance), "ChangeValue", new Type[] { typeof(string), typeof(string), typeof(bool) })]
    internal static class ConfigInstanceChangeValueStringPatch
    {
        private static void Postfix(string key)
        {
            if (KeybindRuntime.IsKeybindSetting(key))
            {
                KeybindRuntime.MarkKeyboardDirty();
            }
        }
    }

    [HarmonyPatch(typeof(ConfigTabWidget), "EnumerateSettings")]
    internal static class ConfigTabWidgetEnumerateSettingsPatch
    {
        private static bool Prefix(ref IEnumerable<string> __result)
        {
            __result = KeybindRuntime.EnumerateSettingsPatched();
            return false;
        }
    }

    [HarmonyPatch(typeof(ConfigTabItem), "Set")]
    internal static class ConfigTabItemSetPatch
    {
        private static void Postfix(ConfigTabItem __instance, string category)
        {
            if (category != KeybindRuntime.CategoryKey)
            {
                return;
            }

            FieldInfo field = typeof(ConfigTabItem).GetField("_nameLabel", BindingFlags.Instance | BindingFlags.NonPublic);
            UILabel label = field == null ? null : field.GetValue(__instance) as UILabel;
            if (label != null)
            {
                label.text = "Keybind";
            }
        }
    }

    [HarmonyPatch(typeof(DropdownWidget), "Localize")]
    internal static class DropdownWidgetLocalizePatch
    {
        private static bool Prefix(DropdownWidget __instance, string text, ref string __result)
        {
            if (__instance == null || __instance.Setting == null)
            {
                return true;
            }

            string caption = KeybindRuntime.GetOptionCaption(__instance.Setting.Key, text);
            if (caption == null)
            {
                return true;
            }

            __result = caption;
            return false;
        }
    }

    [HarmonyPatch(typeof(InputKeyboard), "InitShortcut")]
    internal static class InputKeyboardInitShortcutPatch
    {
        private static void Postfix(InputKeyboard __instance)
        {
            KeybindRuntime.ApplyToKeyboard(__instance);
        }
    }

    [HarmonyPatch(typeof(InputKeyboard), "GetKeyCaption")]
    internal static class InputKeyboardGetKeyCaptionPatch
    {
        private static bool Prefix(InputCommand command, ref string __result)
        {
            string caption;
            if (KeybindRuntime.TryGetCaption(command, out caption))
            {
                __result = caption;
                return false;
            }

            return true;
        }
    }
}
