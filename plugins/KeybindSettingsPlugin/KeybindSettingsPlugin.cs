using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.InputSystem;
using Durango.System.Config;
using Durango.UI;
using Durango.UI.Control;
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
            KeybindRuntime.UpdateKeyCapture();
            KeybindRuntime.HandleCustomMenuHotkeys();
        }

        private void LateUpdate()
        {
            KeybindRuntime.UpdateQuickChatCaret();
        }
    }

    internal static class KeybindRuntime
    {
        internal const string CategoryKey = "keybind";
        private static bool _settingsReady;
        private static bool _keyboardApplied;
        private static bool _oldDefaultMigrationChecked;
        private static IconTileDef _editingTile;
        private static MenuWidget_PC _editingWidget;
        private static string _editingValue;
        private static bool _editingDirty;
        private static float _captureStartAt;
        private static UIInput _quickChatInput;
        private static bool _quickChatOriginalSelectAll;
        private static int _quickChatFocusFrame = -1;
        private const string IconStripName = "BaoX_KeybindIconStrip";
        private const int IconGridHeight = 660;
        private const int IconGridColumns = 6;
        private const int IconTileWidth = 118;
        private const int IconTileHeight = 132;
        private const float IconGridOffsetX = 100f;
        private const float IconGridOffsetY = -130f;

        private static readonly string[] KeyOptions = new string[]
        {
            "None", "Tab", "Return", "Escape", "Space", "Slash",
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
            new BindDef("keybind_quick_chat", "Quick Chat", InputCommand.None, "Slash", Layer.Default),
            new BindDef("keybind_emoticons", "Emoticons", InputCommand.CommunicationMenuButtonAction, "T", Layer.Default),
            new BindDef("keybind_career_guide", "Career Guide", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_change_character", "Change Character", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_play", "Play", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_visit_friend", "Visit Friend's Island", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_animals", "Animals", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_island_market", "Island Market", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_domain", "Domain", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_shop", "Shop", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_support_organization", "Support Organization", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_brawl_island", "Brawl Island", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_daily_log", "Daily Log", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_clan", "Clan", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_friend", "Friend", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_party", "Party", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_encyclopedia", "Encyclopedia", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_warp_remnants", "Warp Remnants", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_screenshot", "Screenshot", InputCommand.None, string.Empty, Layer.Menu),
            new BindDef("keybind_quick_screenshot", "Quick Screenshot", InputCommand.ScreenCapture, "F12", Layer.Default)
        };

        private static readonly MethodInfo MenuClickMethod = typeof(MenuListGroupBase).GetMethod(
            "OnMenuClick",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly IconTileDef[] IconTiles = new IconTileDef[]
        {
            new IconTileDef(MenuType.Character, "Character", "keybind_character", "P"),
            new IconTileDef(MenuType.LearningGuide, "Career\nGuide", "keybind_career_guide", string.Empty),
            new IconTileDef(MenuType.PlayerSelection, "Change\nCharacter", "keybind_change_character", string.Empty),
            new IconTileDef(MenuType.Music, "Play", "keybind_play", string.Empty),
            new IconTileDef(MenuType.Skill, "Skill", "keybind_skill", "K"),
            new IconTileDef(MenuType.Craft, "Craft / Build", "keybind_craft", "C"),
            new IconTileDef(MenuType.Inventory, "Bag", "keybind_bag", "I"),
            new IconTileDef(MenuType.Connect, "Visit a\nFriend's\nIsland", "keybind_visit_friend", string.Empty),
            new IconTileDef(MenuType.Pet, "Animals", "keybind_animals", string.Empty),
            new IconTileDef(MenuType.Market, "Island\nMarket", "keybind_island_market", string.Empty),
            new IconTileDef(MenuType.Estate, "Domain", "keybind_domain", string.Empty),
            new IconTileDef(MenuType.Shop, "Shop", "keybind_shop", string.Empty),
            new IconTileDef(MenuType.Quest, "Task", "keybind_quest", "J"),
            new IconTileDef(MenuType.Faction, "Support\nOrganization", "keybind_support_organization", string.Empty),
            new IconTileDef(MenuType.PvpIsland, "Brawl Island", "keybind_brawl_island", string.Empty),
            new IconTileDef(MenuType.Story, "Daily Log", "keybind_daily_log", string.Empty),
            new IconTileDef(MenuType.Clan, "Clan", "keybind_clan", string.Empty),
            new IconTileDef(MenuType.Social, "Friend", "keybind_friend", string.Empty),
            new IconTileDef(MenuType.Party, "Party", "keybind_party", string.Empty),
            new IconTileDef(MenuType.Encyclopedia, "Encyclopedia", "keybind_encyclopedia", string.Empty),
            new IconTileDef(MenuType.WarpShop, "Warp\nRemnants", "keybind_warp_remnants", string.Empty),
            new IconTileDef(MenuType.Screenshot, "Screenshot", "keybind_screenshot", string.Empty),
            new IconTileDef(MenuType.Screenshot, "Quick\nScreenshot", "keybind_quick_screenshot", "F12"),
            new IconTileDef(MenuType.WorldMap, "Map", "keybind_map", "M"),
            new IconTileDef(MenuType.Social, "Chat", "keybind_chat", "Return", "button_hud_chat"),
            new IconTileDef(MenuType.Social, "Quick Chat", "keybind_quick_chat", "Slash", "button_hud_chat", true)
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

            EnsureIconOnlySettings(list);
            MigrateOldIconDefaults();
            MigrateScreenshotShortcut();

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
                if (bind.Command == InputCommand.None)
                {
                    continue;
                }

                string value = GetValue(bind);
                if (IsNoneValue(value))
                {
                    continue;
                }

                KeyCode code;
                if (TryParseKeyCode(value, out code))
                {
                    map[code, Modifier.None, bind.Layer, Trigger.Down] = bind.Command;
                }
            }

            RebuildReverseMap(map);
            _keyboardApplied = true;
        }

        internal static bool TryGetCaption(InputCommand command, out string caption)
        {
            if (command == InputCommand.None)
            {
                caption = null;
                return false;
            }

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

        internal static void UpdateKeyCapture()
        {
            if (_editingTile == null)
            {
                return;
            }

            MessageBox messageBox = UIManager.MessageBox;
            if (messageBox == null || !messageBox.IsShow)
            {
                SavePendingCapturedKey();
                ClearKeyCapture();
                return;
            }

            if (Time.time < _captureStartAt || !Input.anyKeyDown)
            {
                return;
            }

            KeyCode code;
            if (!TryReadPressedKey(out code))
            {
                return;
            }

            _editingValue = code.ToString();
            _editingDirty = true;
            SetShortcutText(_editingWidget, InputKeyboard.KeyToCaption(code));
            UpdateCurrentKeyButton(InputKeyboard.KeyToCaption(code));
        }

        internal static void HandleCustomMenuHotkeys()
        {
            if (_editingTile != null || !Input.anyKeyDown || IsTextInputFocused())
            {
                return;
            }

            MessageBox messageBox = UIManager.MessageBox;
            if (messageBox != null && messageBox.IsShow)
            {
                return;
            }

            for (int i = 0; i < IconTiles.Length; i++)
            {
                IconTileDef tile = IconTiles[i];
                BindDef bind = FindBind(tile.BindKey);
                if (bind == null || bind.Command != InputCommand.None)
                {
                    continue;
                }

                string value = GetValue(bind);
                if (string.IsNullOrEmpty(value) || IsNoneValue(value))
                {
                    continue;
                }

                KeyCode code;
                if (TryParseKeyCode(value, out code) && Input.GetKeyDown(code))
                {
                    if (tile.IsQuickChat)
                    {
                        OpenQuickChat();
                    }
                    else
                    {
                        ToggleMenu(tile.Menu);
                    }
                    return;
                }
            }
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

        internal static string GetBindCaption(string key, string fallback)
        {
            BindDef bind = FindBind(key);
            if (bind == null)
            {
                return fallback;
            }

            string value = GetValue(bind);
            if (IsNoneValue(value))
            {
                return "None";
            }

            KeyCode code;
            return TryParseKeyCode(value, out code) ? InputKeyboard.KeyToCaption(code) : fallback;
        }

        private static string GetConfiguredBindCaption(string key)
        {
            BindDef bind = FindBind(key);
            if (bind == null)
            {
                return null;
            }

            string value = ConfigInstance.GetValue<string>(bind.Key, null);
            if (string.IsNullOrEmpty(value) && PlayerPrefs.HasKey("option:" + bind.Key))
            {
                value = PlayerPrefs.GetString("option:" + bind.Key);
            }

            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            if (IsNoneValue(value))
            {
                return "None";
            }

            KeyCode code;
            return TryParseKeyCode(value, out code) ? InputKeyboard.KeyToCaption(code) : null;
        }

        private static string GetTileCaption(IconTileDef tile)
        {
            if (tile == null)
            {
                return string.Empty;
            }

            string configured = GetConfiguredBindCaption(tile.BindKey);
            if (IsNoneValue(configured))
            {
                return string.Empty;
            }
            if (!string.IsNullOrEmpty(configured))
            {
                return configured;
            }

            string live = GetLiveMenuCaption(tile.Menu);
            if (!string.IsNullOrEmpty(live))
            {
                return live;
            }

            return tile.DefaultKey ?? string.Empty;
        }

        internal static void InstallKeybindIconStrip(ConfigMainWidget widget, string category)
        {
            if (widget == null)
            {
                return;
            }

            Transform old = widget.transform.Find(IconStripName);
            if (old != null)
            {
                UnityEngine.Object.Destroy(old.gameObject);
            }

            if (category != CategoryKey)
            {
                return;
            }

            UIScrollView scrollView = GetField<UIScrollView>(widget, "_scrollView");
            UIPanel panel = scrollView == null ? null : scrollView.panel;
            int width = panel == null ? 760 : (int)panel.width;
            List<SettingItem> settingItems = GetField<List<SettingItem>>(widget, "_settingItems");
            if (settingItems != null)
            {
                for (int i = 0; i < settingItems.Count; i++)
                {
                    if (settingItems[i] != null && settingItems[i].GameObj != null)
                    {
                        settingItems[i].GameObj.SetActive(false);
                    }
                }
            }

            UIWidget emptyWidget = GetField<UIWidget>(widget, "_emptyWidget");
            if (emptyWidget != null)
            {
                emptyWidget.transform.localPosition = new Vector3(0f, -IconGridHeight, 0f);
            }

            UILabel template = FindLabelTemplate(widget);
            GameObject strip = new GameObject(IconStripName);
            strip.transform.SetParent(widget.transform, false);
            strip.transform.localPosition = new Vector3(IconGridOffsetX, IconGridOffsetY, 0f);
            UIWidget stripWidget = strip.AddComponent<UIWidget>();
            stripWidget.pivot = UIWidget.Pivot.TopLeft;
            stripWidget.width = width;
            stripWidget.height = IconGridHeight;
            stripWidget.depth = 30;
            UIDragScrollView stripDrag = strip.AddComponent<UIDragScrollView>();
            stripDrag.scrollView = scrollView;

            for (int i = 0; i < IconTiles.Length; i++)
            {
                CreateIconTile(strip.transform, scrollView, template, IconTiles[i], i);
            }

            UIWidget widgetField = GetField<UIWidget>(widget, "_widget");
            RectLayout rectLayout = GetField<RectLayout>(widget, "_rectLayout");
            if (rectLayout != null)
            {
                rectLayout.UpdateLayout();
            }
            if (widgetField != null)
            {
                UIUtility.UpdateAnchors(widgetField.transform);
            }
        }

        private static void CreateIconTile(Transform parent, UIScrollView scrollView, UILabel template, IconTileDef tile, int index)
        {
            int row = index / IconGridColumns;
            int col = index % IconGridColumns;
            float x = 34f + col * 120f;
            float y = -4f - row * IconTileHeight;

            GameObject clone = CreateMenuWidgetClone(parent, scrollView, tile);
            if (clone != null)
            {
                clone.transform.localPosition = new Vector3(x, y, 0f);
                return;
            }

            GameObject root = new GameObject("KeybindIcon_" + tile.Label.Replace("\n", "_"));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, y, 0f);
            UIWidget rootWidget = root.AddComponent<UIWidget>();
            rootWidget.pivot = UIWidget.Pivot.TopLeft;
            rootWidget.width = IconTileWidth;
            rootWidget.height = IconTileHeight;
            rootWidget.depth = 31;
            UIDragScrollView drag = root.AddComponent<UIDragScrollView>();
            drag.scrollView = scrollView;

            UILabel keyLabel = AddLabel(root.transform, template, "Key", GetTileCaption(tile), 20, new Color(0.95f, 0.95f, 0.95f, 1f));
            keyLabel.width = 42;
            keyLabel.height = 26;
            keyLabel.transform.localPosition = new Vector3(10f, -4f, 0f);

            UISprite iconSprite = AddIconSprite(root.transform, tile);
            if (iconSprite != null)
            {
                iconSprite.transform.localPosition = new Vector3(28f, -24f, 0f);
            }

            UILabel textLabel = AddLabel(root.transform, template, "Text", tile.Label, 18, new Color(0.9f, 0.87f, 0.78f, 1f));
            textLabel.width = IconTileWidth;
            textLabel.height = 44;
            textLabel.transform.localPosition = new Vector3(0f, -78f, 0f);
        }

        private static GameObject CreateMenuWidgetClone(Transform parent, UIScrollView scrollView, IconTileDef tile)
        {
            MenuWidget_PC template = FindMenuWidgetTemplate();
            if (template == null)
            {
                return null;
            }

            GameObject clone = UnityEngine.Object.Instantiate(template.gameObject) as GameObject;
            if (clone == null)
            {
                return null;
            }

            clone.name = "KeybindMenuWidget_" + tile.BindKey;
            clone.transform.SetParent(parent, false);
            clone.transform.localScale = Vector3.one;
            clone.SetActive(true);

            MenuWidget_PC widget = clone.GetComponent<MenuWidget_PC>();
            if (widget != null)
            {
                widget.Set(tile.Menu);
                if (!string.IsNullOrEmpty(tile.IconName))
                {
                    UILabel menuLabel = GetField<UILabel>(widget, "_menuLabel");
                    if (menuLabel != null)
                    {
                        menuLabel.text = tile.Label.Replace("\n", " ");
                    }
                }
                else if (tile.BindKey == "keybind_quick_screenshot")
                {
                    UILabel menuLabel = GetField<UILabel>(widget, "_menuLabel");
                    if (menuLabel != null)
                    {
                        menuLabel.text = "Quick " + menuLabel.text;
                    }
                }
                SetShortcutText(widget, GetTileCaption(tile));
                widget.Clicked = null;
            }

            UIDragScrollView drag = clone.GetComponent<UIDragScrollView>();
            if (drag == null)
            {
                drag = clone.AddComponent<UIDragScrollView>();
            }
            drag.scrollView = scrollView;

            PrepareClickComponents(clone, widget, tile);
            RaiseWidgetDepth(clone, 30);
            if (widget != null)
            {
                ForceMenuIcon(widget, tile, 90);
            }
            return clone;
        }

        private static MenuWidget_PC FindMenuWidgetTemplate()
        {
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(MenuWidget_PC));
            if (objects == null)
            {
                return null;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                MenuWidget_PC widget = objects[i] as MenuWidget_PC;
                if (widget == null || widget.gameObject == null)
                {
                    continue;
                }
                if (widget.gameObject.name.StartsWith("KeybindMenuWidget_", StringComparison.Ordinal))
                {
                    continue;
                }
                if (GetField<UISprite>(widget, "_menuIcon") == null || GetField<UILabel>(widget, "_menuLabel") == null)
                {
                    continue;
                }
                return widget;
            }

            return null;
        }

        private static void SetShortcutText(MenuWidget_PC widget, string text)
        {
            UILabel label = GetField<UILabel>(widget, "_shortcutLabel");
            UISprite bg = GetField<UISprite>(widget, "_shortcutBg");
            if (label == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                label.gameObject.SetActive(false);
                label.text = string.Empty;
                if (bg != null)
                {
                    bg.gameObject.SetActive(false);
                }
                return;
            }

            label.gameObject.SetActive(true);
            label.text = text;
            if (bg != null)
            {
                bg.gameObject.SetActive(true);
                bg.width = Mathf.Max(28, label.width + 12);
            }
        }

        private static void PrepareClickComponents(GameObject root, MenuWidget_PC widget, IconTileDef tile)
        {
            NGUITools.AddWidgetCollider(root);
            SetColliderEnabled(root, true);
            UIEventListener listener = UIEventListener.Get(root);
            listener.onClick = delegate(GameObject go)
            {
                ShowKeybindPopup(tile, widget);
            };
            listener.onTooltip = null;
            listener.onDoubleClick = null;
        }

        private static void SetColliderEnabled(GameObject root, bool enabledValue)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;
                if (typeName == "BoxCollider" || typeName == "SphereCollider" || typeName == "CapsuleCollider")
                {
                    PropertyInfo enabled = component.GetType().GetProperty("enabled", BindingFlags.Instance | BindingFlags.Public);
                    if (enabled != null)
                    {
                        enabled.SetValue(component, enabledValue, null);
                    }
                }
            }
        }

        private static void ShowKeybindPopup(IconTileDef tile, MenuWidget_PC widget)
        {
            SavePendingCapturedKey();
            ClearKeyCapture();

            string label = tile.Label.Replace("\n", " ");
            string currentKey = GetTileCaption(tile);
            if (string.IsNullOrEmpty(currentKey))
            {
                currentKey = "None";
            }

            MessageBox.Button currentButton = new MessageBox.Button("Current Key: " + currentKey, PresetButton.Style.Border, null, false, PresetButton.Effect.None);
            MessageBox.Button noneButton = new MessageBox.Button((!string.IsNullOrEmpty(tile.BindKey)) ? "Set to None" : "No editable keybind", PresetButton.Style.Border, null, string.IsNullOrEmpty(tile.BindKey), PresetButton.Effect.None);
            MessageBox.Button cancelButton = new MessageBox.Button("Close", PresetButton.Style.Border, null, false, PresetButton.Effect.None);

            _editingTile = tile;
            _editingWidget = widget;
            _editingValue = null;
            _editingDirty = false;
            _captureStartAt = Time.time + 0.15f;

            UIManager.MessageBox.Show(
                "Keybind: <em>" + label + "</em>",
                "Press any key to change this shortcut.",
                delegate(int index)
                {
                    if (index == 1)
                    {
                        SetBindToNone(tile, widget);
                        ClearKeyCapture();
                    }
                    else
                    {
                        SavePendingCapturedKey();
                        ClearKeyCapture();
                    }
                },
                currentButton,
                noneButton,
                cancelButton);
        }

        private static void SetBindToNone(IconTileDef tile, MenuWidget_PC widget)
        {
            if (string.IsNullOrEmpty(tile.BindKey))
            {
                UIManager.SystemMsg("No editable keybind data.", 2f);
                return;
            }

            SaveBindValue(tile, widget, "None");
            UIManager.SystemMsg(tile.Label.Replace("\n", " ") + " keybind: None", 2f);
        }

        private static void SavePendingCapturedKey()
        {
            if (!_editingDirty || _editingTile == null || string.IsNullOrEmpty(_editingValue))
            {
                return;
            }

            SaveBindValue(_editingTile, _editingWidget, _editingValue);
            _editingDirty = false;
        }

        private static void SaveBindValue(IconTileDef tile, MenuWidget_PC widget, string value)
        {
            if (tile == null || string.IsNullOrEmpty(tile.BindKey))
            {
                return;
            }

            ConfigInstance.ChangeValue(tile.BindKey, value, true);
            PlayerPrefs.SetString("option:" + tile.BindKey, value);
            PlayerPrefs.Save();
            MarkKeyboardDirty();
            ApplyToLiveKeyboard();
            if (widget != null)
            {
                SetShortcutText(widget, IsNoneValue(value) ? string.Empty : GetTileCaption(tile));
            }
            RefreshLiveMenuShortcutLabels();
        }

        private static void ClearKeyCapture()
        {
            _editingTile = null;
            _editingWidget = null;
            _editingValue = null;
            _editingDirty = false;
            _captureStartAt = 0f;
        }

        private static bool TryReadPressedKey(out KeyCode code)
        {
            Array values = Enum.GetValues(typeof(KeyCode));
            for (int i = 0; i < values.Length; i++)
            {
                KeyCode candidate = (KeyCode)values.GetValue(i);
                if (!IsCapturableKey(candidate))
                {
                    continue;
                }
                if (Input.GetKeyDown(candidate))
                {
                    code = candidate;
                    return true;
                }
            }

            code = KeyCode.None;
            return false;
        }

        private static bool IsCapturableKey(KeyCode code)
        {
            if (code == KeyCode.None || code == KeyCode.Escape)
            {
                return false;
            }

            string name = code.ToString();
            return !name.StartsWith("Mouse", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Joystick", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTextInputFocused()
        {
            if (UICamera.inputHasFocus)
            {
                return true;
            }

            return UIInput.selection != null && UIInput.selection.gameObject != null && UIInput.selection.gameObject.activeInHierarchy;
        }

        private static void ToggleMenu(MenuType menu)
        {
            MenuListGroupBase menuList = UIManager.FindScript<MenuListGroupBase>();
            if (menuList != null && MenuClickMethod != null)
            {
                try
                {
                    MenuClickMethod.Invoke(menuList, new object[] { menu });
                }
                catch (Exception ex)
                {
                    if (KeybindSettingsPlugin.Log != null)
                    {
                        KeybindSettingsPlugin.Log.LogError("Keybind menu action failed: " + menu + "\n" + ex);
                    }
                }
                return;
            }

            UIBase script = MenuHelper.GetScript(menu);
            if (script == null)
            {
                return;
            }

            if (UIBase.CurrentUI != null && script != UIBase.CurrentUI && UIBase.CurrentUI.IsOpened)
            {
                UIBase.CloseUI();
            }

            MenuHelper.Toggle(menu, false);
            MenuHelper.SetLastOpendUI(menu, script);
        }

        private static void OpenQuickChat()
        {
            try
            {
                MenuListGroupBase menuList = UIManager.FindScript<MenuListGroupBase>();
                if (menuList != null && menuList.IsOpened)
                {
                    menuList.Close();
                }

                ChattingGroup_PC chat = UIManager.FindScript<ChattingGroup_PC>();
                if (chat == null)
                {
                    return;
                }

                chat.Show(true);
                ChattingInputControl_PC inputControl = GetField<ChattingInputControl_PC>(chat, "_chatInputCtrl");
                if (inputControl == null || !inputControl.IsAvailable)
                {
                    return;
                }

                UIInput input = GetField<UIInput>(inputControl, "_inputLabel");
                if (input == null)
                {
                    return;
                }

                inputControl.SetFocus(true, false);
                _quickChatInput = input;
                _quickChatOriginalSelectAll = input.selectAllTextOnFocus;
                _quickChatFocusFrame = Time.frameCount;
                input.selectAllTextOnFocus = false;
                input.value = "/";
                MoveCaretToEnd(input);
            }
            catch (Exception ex)
            {
                if (KeybindSettingsPlugin.Log != null)
                {
                    KeybindSettingsPlugin.Log.LogError("Quick Chat action failed\n" + ex);
                }
            }
        }

        internal static void UpdateQuickChatCaret()
        {
            if (_quickChatInput == null || Time.frameCount <= _quickChatFocusFrame)
            {
                return;
            }

            if (_quickChatInput.isSelected)
            {
                MoveCaretToEnd(_quickChatInput);
            }

            _quickChatInput.selectAllTextOnFocus = _quickChatOriginalSelectAll;
            _quickChatInput = null;
            _quickChatFocusFrame = -1;
        }

        private static void MoveCaretToEnd(UIInput input)
        {
            if (input == null || !input.isSelected)
            {
                return;
            }

            int end = input.value.Length;
            input.selectionStart = end;
            input.selectionEnd = end;
            input.cursorPosition = end;
        }

        private static void UpdateCurrentKeyButton(string caption)
        {
            MessageBox messageBox = UIManager.MessageBox;
            if (messageBox == null || string.IsNullOrEmpty(caption))
            {
                return;
            }

            ListObjectPool<SelectableButton> buttons = GetField<ListObjectPool<SelectableButton>>(messageBox, "_buttons");
            if (buttons == null || buttons.Count <= 0 || buttons[0] == null)
            {
                return;
            }

            buttons[0].Text = "Current Key: " + caption;
        }

        internal static bool ShouldBlockKeybindButton(MessageBox messageBox)
        {
            if (_editingTile == null || messageBox == null)
            {
                return false;
            }

            SelectableButton current = Selectable.Current as SelectableButton;
            if (current == null)
            {
                return false;
            }

            ListObjectPool<SelectableButton> buttons = GetField<ListObjectPool<SelectableButton>>(messageBox, "_buttons");
            if (buttons == null || buttons.Count == 0)
            {
                return false;
            }

            int index = buttons.IndexOf(current);
            if (index == 0)
            {
                return true;
            }
            if (index != 1)
            {
                return false;
            }

            SetBindToNone(_editingTile, _editingWidget);
            _editingValue = null;
            _editingDirty = false;
            _captureStartAt = Time.time + 0.15f;
            UpdateCurrentKeyButton("None");
            return true;
        }

        internal static bool TryApplyMenuShortcutLabel(MenuWidget_PC widget, MenuType menu)
        {
            if (widget == null)
            {
                return false;
            }

            IconTileDef tile = FindIconTile(menu);
            if (tile == null)
            {
                return false;
            }

            SetShortcutText(widget, GetTileCaption(tile));
            return true;
        }

        private static void RefreshLiveMenuShortcutLabels()
        {
            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(typeof(MenuWidget_PC));
            if (objects == null)
            {
                return;
            }

            for (int i = 0; i < objects.Length; i++)
            {
                MenuWidget_PC widget = objects[i] as MenuWidget_PC;
                if (widget == null || widget.gameObject == null)
                {
                    continue;
                }
                if (widget.gameObject.name.StartsWith("KeybindMenuWidget_", StringComparison.Ordinal))
                {
                    continue;
                }

                TryApplyMenuShortcutLabel(widget, widget.Type);
            }
        }

        private static IconTileDef FindIconTile(MenuType menu)
        {
            for (int i = 0; i < IconTiles.Length; i++)
            {
                if (IconTiles[i].Menu == menu)
                {
                    return IconTiles[i];
                }
            }

            return null;
        }

        private static string GetLiveMenuCaption(MenuType menu)
        {
            if (!GameSystem<InputSystem>.HasInstance())
            {
                return null;
            }

            InputSystem inputSystem = GameSystem<InputSystem>.Instance();
            if (inputSystem == null || inputSystem.Keyboard == null)
            {
                return null;
            }

            InputCommand command = inputSystem.Keyboard.GetMenuCommand(menu);
            if (command == InputCommand.None)
            {
                return null;
            }

            string caption = inputSystem.Keyboard.GetKeyCaption(command, Layer.None);
            if (string.IsNullOrEmpty(caption))
            {
                caption = inputSystem.Keyboard.GetKeyCaption(command, Layer.Menu);
            }
            return caption;
        }

        private static void RaiseWidgetDepth(GameObject root, int minDepth)
        {
            UIWidget[] widgets = root.GetComponentsInChildren<UIWidget>(true);
            if (widgets == null)
            {
                return;
            }

            int lowest = int.MaxValue;
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] != null && widgets[i].depth < lowest)
                {
                    lowest = widgets[i].depth;
                }
            }
            if (lowest == int.MaxValue || lowest >= minDepth)
            {
                return;
            }

            int offset = minDepth - lowest;
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] != null)
                {
                    widgets[i].depth += offset;
                }
            }
        }

        private static void ForceMenuIcon(MenuWidget_PC widget, IconTileDef tile, int depth)
        {
            UISprite icon = GetField<UISprite>(widget, "_menuIcon");
            if (icon == null)
            {
                return;
            }

            icon.gameObject.SetActive(true);
            icon.enabled = true;
            icon.depth = depth;
            string iconName = string.IsNullOrEmpty(tile.IconName)
                ? IconMap.Get(tile.Menu, "icon_question")
                : tile.IconName;
            icon.SetSprite(iconName, "icon_question");
        }

        private static UISprite AddIconSprite(Transform parent, IconTileDef tile)
        {
            string iconName = string.IsNullOrEmpty(tile.IconName)
                ? IconMap.Get(tile.Menu, "icon_question")
                : tile.IconName;
            GameObject go = new GameObject("Icon");
            go.transform.SetParent(parent, false);
            UISprite sprite = go.AddComponent<UISprite>();
            sprite.spriteName = iconName;
            sprite.pivot = UIWidget.Pivot.TopLeft;
            sprite.width = 58;
            sprite.height = 58;
            sprite.depth = 38;
            return sprite;
        }

        private static UILabel AddLabel(Transform parent, UILabel template, string name, string text, int fontSize, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            UILabel label = go.AddComponent<UILabel>();
            if (template != null)
            {
                label.bitmapFont = template.bitmapFont;
                label.effectStyle = template.effectStyle;
                label.effectColor = template.effectColor;
                label.effectDistance = template.effectDistance;
                label.spacingX = template.spacingX;
                label.spacingY = template.spacingY;
            }
            label.pivot = UIWidget.Pivot.TopLeft;
            label.alignment = NGUIText.Alignment.Center;
            label.supportEncoding = true;
            label.fontSize = fontSize;
            label.color = color;
            label.text = text;
            label.depth = 40;
            return label;
        }

        private static UILabel FindLabelTemplate(ConfigMainWidget widget)
        {
            UILabel[] labels = widget.GetComponentsInChildren<UILabel>(true);
            if (labels == null || labels.Length == 0)
            {
                return null;
            }
            return labels[0];
        }

        private static T GetField<T>(object instance, string name) where T : class
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(instance) as T;
                }
                type = type.BaseType;
            }

            return null;
        }

        private static void EnsureIconOnlySettings(List<Setting> list)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null || list[i].Key != "keybind_category")
                {
                    list.RemoveAt(i);
                }
            }

            EnsureCategory(list);
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

        private static void MigrateOldIconDefaults()
        {
            if (_oldDefaultMigrationChecked)
            {
                return;
            }
            _oldDefaultMigrationChecked = true;

            const string migrationKey = "baox.keybindsettings.oldIconDefaultsMigrated.v2";
            if (PlayerPrefs.GetInt(migrationKey, 0) == 1)
            {
                return;
            }

            ClearOldDefaultValue("keybind_change_character", "Y");
            ClearOldDefaultValue("keybind_play", "U");
            ClearOldDefaultValue("keybind_visit_friend", "N");
            ClearOldDefaultValue("keybind_animals", "F");
            ClearOldDefaultValue("keybind_island_market", "K");
            ClearOldDefaultValue("keybind_encyclopedia", "G");
            ClearOldDefaultValue("keybind_friend", "O");
            ClearOldDefaultValue("keybind_warp_remnants", "B");

            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
            MarkKeyboardDirty();
        }

        private static void MigrateScreenshotShortcut()
        {
            const string migrationKey = "baox.keybindsettings.screenshotShortcutMigrated.v1";
            if (PlayerPrefs.GetInt(migrationKey, 0) == 1)
            {
                return;
            }

            ClearOldDefaultValue("keybind_screenshot", "F12");
            PlayerPrefs.SetInt(migrationKey, 1);
            PlayerPrefs.Save();
            MarkKeyboardDirty();
        }

        private static void ClearOldDefaultValue(string key, string oldValue)
        {
            string value = ConfigInstance.GetValue<string>(key, null);
            if (string.IsNullOrEmpty(value) && PlayerPrefs.HasKey("option:" + key))
            {
                value = PlayerPrefs.GetString("option:" + key);
            }

            if (!string.Equals(value, oldValue, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ConfigInstance.ChangeValue(key, "None", true);
            PlayerPrefs.SetString("option:" + key, "None");
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

            KeyCode code;
            if (!IsNoneValue(value) && !TryParseKeyCode(value, out code))
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

        private static bool IsNoneValue(string value)
        {
            return string.Equals(value, "None", StringComparison.OrdinalIgnoreCase);
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
            if (command == InputCommand.None)
            {
                return false;
            }

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

        private sealed class IconTileDef
        {
            internal readonly MenuType Menu;
            internal readonly string Label;
            internal readonly string BindKey;
            internal readonly string DefaultKey;
            internal readonly string IconName;
            internal readonly bool IsQuickChat;

            internal IconTileDef(MenuType menu, string label, string bindKey, string defaultKey)
                : this(menu, label, bindKey, defaultKey, null, false)
            {
            }

            internal IconTileDef(MenuType menu, string label, string bindKey, string defaultKey, string iconName)
                : this(menu, label, bindKey, defaultKey, iconName, false)
            {
            }

            internal IconTileDef(MenuType menu, string label, string bindKey, string defaultKey, string iconName, bool isQuickChat)
            {
                Menu = menu;
                Label = label;
                BindKey = bindKey;
                DefaultKey = defaultKey;
                IconName = iconName;
                IsQuickChat = isQuickChat;
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

    [HarmonyPatch(typeof(ConfigMainWidget), "SetConfigLayout")]
    internal static class ConfigMainWidgetSetConfigLayoutPatch
    {
        private static void Postfix(ConfigMainWidget __instance, string category)
        {
            KeybindRuntime.InstallKeybindIconStrip(__instance, category);
        }
    }

    [HarmonyPatch(typeof(MessageBox), "OnButtonClick")]
    internal static class MessageBoxOnButtonClickPatch
    {
        private static bool Prefix(MessageBox __instance)
        {
            return !KeybindRuntime.ShouldBlockKeybindButton(__instance);
        }
    }

    [HarmonyPatch(typeof(MenuWidget_PC), "SetShortcutLabel")]
    internal static class MenuWidgetPCSetShortcutLabelPatch
    {
        private static void Postfix(MenuWidget_PC __instance, MenuType menuType)
        {
            KeybindRuntime.TryApplyMenuShortcutLabel(__instance, menuType);
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
