using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Logic;
using Durango.System;
using Durango.System.Config;
using Durango.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BaoX.DurangoOriginal.MobilePCUISwitch
{
    public enum DurangoUIMode
    {
        Mobile,
        PC
    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.baominix.durango.original.uisizeoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.baominix.durango.original.keybindsettings", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.baominix.durango.original.keybind2", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class MobilePCUISwitchPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.mobilepcuiswitch";
        public const string PluginName = "Durango Mobile / PC UI Switch";
        public const string PluginVersion = "1.2.17";

        internal static MobilePCUISwitchPlugin Instance;
        internal static ManualLogSource Log;

        private ConfigEntry<string> _mode;
        private ConfigEntry<bool> _reloadSceneOnChange;
        private ConfigEntry<bool> _enableHotkeys;
        private ConfigEntry<KeyCode> _panelKey;
        private ConfigEntry<KeyCode> _toggleKey;

        private Harmony _harmony;
        private DurangoUIMode _lastConfiguredMode;
        private bool _showPanel;
        private bool _reloadInProgress;
        private bool _modeConfirmationOpen;
        private Rect _windowRect = new Rect(24f, 90f, 390f, 250f);
        private string _statusText = string.Empty;
        private float _statusUntil;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            _mode = Config.Bind(
                "UI Mode",
                "Mode",
                "pc",
                "UI prefab set. Valid values are pc and mobile; the default is pc.");
            _reloadSceneOnChange = Config.Bind(
                "UI Mode",
                "ReloadSceneOnChange",
                true,
                "Reload the native settings model and active Unity scene after UI mode changes.");
            _enableHotkeys = Config.Bind(
                "Hotkeys",
                "EnableHotkeys",
                false,
                "Enable the optional F6/F7 fallback controls. Disabled by default to avoid conflicts with Keybind plugins.");
            _panelKey = Config.Bind(
                "Hotkeys",
                "PanelKey",
                KeyCode.F6,
                "Open or close the fallback runtime panel when EnableHotkeys is true.");
            _toggleKey = Config.Bind(
                "Hotkeys",
                "ToggleModeKey",
                KeyCode.F7,
                "Toggle directly between Mobile and PC UI when EnableHotkeys is true.");

            DurangoUIMode storedMode;
            if (NativeSettingsBridge.TryReadStoredMode(out storedMode))
            {
                _mode.Value = NativeSettingsBridge.ToSettingValue(storedMode);
            }
            else if (!NativeSettingsBridge.TryParseMode(_mode.Value, out storedMode))
            {
                storedMode = DurangoUIMode.PC;
                _mode.Value = NativeSettingsBridge.ToSettingValue(storedMode);
            }
            Config.Save();

            RuntimeUIPatches.RequestedMode = storedMode;
            _lastConfiguredMode = storedMode;

            _harmony = new Harmony(PluginGuid);
            RuntimeUIPatches.Apply(_harmony);
            NativeSettingsBridge.ApplyPatches(_harmony);
            NativeSettingsBridge.InstallSettings();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            ShowStatus(MobileUiLocalization.Get("status_ready", MobileUiLocalization.ModeName(RuntimeUIPatches.RequestedMode)));
            Logger.LogInfo(
                PluginName + " loaded. Mode=" + storedMode +
                ", effective UI=" + RuntimeUIPatches.EffectiveModeLabel +
                ", native setting=ui_platform, hotkeys=" + _enableHotkeys.Value);
        }

        private void Update()
        {
            if (_enableHotkeys.Value && Input.GetKeyDown(_panelKey.Value))
            {
                _showPanel = !_showPanel;
            }

            if (_enableHotkeys.Value && Input.GetKeyDown(_toggleKey.Value))
            {
                DurangoUIMode next = RuntimeUIPatches.RequestedMode == DurangoUIMode.PC
                    ? DurangoUIMode.Mobile
                    : DurangoUIMode.PC;
                RequestModeChange(next, _reloadSceneOnChange.Value, false);
            }

            DurangoUIMode configuredMode;
            if (!NativeSettingsBridge.TryParseMode(_mode.Value, out configuredMode))
            {
                configuredMode = DurangoUIMode.PC;
                _mode.Value = NativeSettingsBridge.ToSettingValue(configuredMode);
                Config.Save();
            }

            if (configuredMode != _lastConfiguredMode)
            {
                DurangoUIMode mode = configuredMode;
                _lastConfiguredMode = mode;
                ApplyMode(mode, false);
                NativeSettingsBridge.SaveMode(mode);
                ShowStatus(MobileUiLocalization.Get("status_changed", MobileUiLocalization.ModeName(RuntimeUIPatches.RequestedMode)));
                Logger.LogInfo("UI mode changed through BepInEx config: " + mode);
            }
        }

        private void OnGUI()
        {
            if (!_enableHotkeys.Value)
            {
                return;
            }

            if (_showPanel)
            {
                GUI.depth = -10000;
                _windowRect = GUILayout.Window(814207, _windowRect, DrawWindow, MobileUiLocalization.Get("panel_title"));
            }

            if (!string.IsNullOrEmpty(_statusText) && Time.realtimeSinceStartup < _statusUntil)
            {
                GUI.depth = -9999;
                GUI.Box(new Rect(20f, 20f, 470f, 42f), _statusText);
            }
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label(MobileUiLocalization.Get("requested_mode", MobileUiLocalization.ModeName(RuntimeUIPatches.RequestedMode)));
            GUILayout.Label(MobileUiLocalization.Get("active_scene", SceneManager.GetActiveScene().name));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(MobileUiLocalization.Get("mobile"), GUILayout.Height(34f))) RequestModeChange(DurangoUIMode.Mobile, false, false);
            if (GUILayout.Button(MobileUiLocalization.Get("pc"), GUILayout.Height(34f))) RequestModeChange(DurangoUIMode.PC, false, false);
            GUILayout.EndHorizontal();

            GUILayout.Space(10f);
            if (GUILayout.Button(MobileUiLocalization.Get("apply_rebuild"), GUILayout.Height(40f)))
            {
                ReloadCurrentScene();
            }

            bool reload = GUILayout.Toggle(_reloadSceneOnChange.Value, MobileUiLocalization.Get("rebuild_after_setting"));
            if (reload != _reloadSceneOnChange.Value)
            {
                _reloadSceneOnChange.Value = reload;
            }

            GUILayout.Space(6f);
            GUILayout.Label(MobileUiLocalization.Get("pc_size_only"));
            GUILayout.Label(MobileUiLocalization.Get("hotkey_hint"));
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 24f));
        }

        internal void OnNativeModeChanged(string value, bool save)
        {
            DurangoUIMode mode;
            if (!NativeSettingsBridge.TryParseMode(value, out mode))
            {
                return;
            }

            // LoadConfigValue reports every persisted value through
            // ChangeValue. Do not re-apply an already active mode during
            // startup; doing so is redundant and can re-enter settings work.
            if (!save && mode == RuntimeUIPatches.RequestedMode)
            {
                NativeSettingsBridge.SyncLiveSetting(mode);
                return;
            }

            if (save)
            {
                RequestModeChange(mode, _reloadSceneOnChange.Value, true);
            }
            else
            {
                ChangeMode(mode, false, true);
            }
        }

        internal void RequestNativeModeChange(DurangoUIMode mode)
        {
            RequestModeChange(mode, _reloadSceneOnChange.Value, true);
        }

        private void RequestModeChange(DurangoUIMode mode, bool reloadScene, bool fromNativeSetting)
        {
            if (mode == RuntimeUIPatches.RequestedMode || _modeConfirmationOpen)
            {
                NativeSettingsBridge.SyncLiveSetting(RuntimeUIPatches.RequestedMode);
                return;
            }

            MessageBox messageBox = UIManager.MessageBox;
            if (messageBox == null)
            {
                Logger.LogWarning("Cannot show UI mode confirmation because MessageBox is not ready.");
                NativeSettingsBridge.SyncLiveSetting(RuntimeUIPatches.RequestedMode);
                return;
            }

            _modeConfirmationOpen = true;
            string target = GetModeDisplayName(mode);
            messageBox.Show(
                GetConfirmTitle(target),
                GetConfirmDetail(),
                delegate(bool confirmed)
                {
                    _modeConfirmationOpen = false;
                    if (confirmed)
                    {
                        ChangeMode(mode, reloadScene, fromNativeSetting);
                    }
                    else
                    {
                        NativeSettingsBridge.SyncLiveSetting(RuntimeUIPatches.RequestedMode);
                    }
                },
                GetConfirmButtonText(),
                GetCancelButtonText());
        }

        private static string GetModeDisplayName(DurangoUIMode mode)
        {
            return MobileUiLocalization.ModeName(mode);
        }

        private static string GetConfirmTitle(string target)
        {
            return MobileUiLocalization.Get("confirm_title", target);
        }

        private static string GetConfirmDetail()
        {
            return MobileUiLocalization.Get("confirm_detail");
        }

        private static string GetConfirmButtonText()
        {
            return MobileUiLocalization.Get("confirm");
        }

        private static string GetCancelButtonText()
        {
            return MobileUiLocalization.Get("cancel");
        }

        private void ChangeMode(DurangoUIMode mode, bool reloadScene, bool fromNativeSetting)
        {
            ApplyMode(mode, true);

            if (!fromNativeSetting)
            {
                NativeSettingsBridge.ChangeNativeValue(mode, true);
            }

            ShowStatus(MobileUiLocalization.Get(reloadScene ? "status_mode_rebuild" : "status_mode_wait", MobileUiLocalization.ModeName(mode)));
            Logger.LogInfo("Requested UI mode: " + mode + (fromNativeSetting ? " (Settings)" : string.Empty));

            if (reloadScene)
            {
                ReloadCurrentScene();
            }
        }

        private void ApplyMode(DurangoUIMode mode, bool saveConfig)
        {
            bool modeChanged = mode != RuntimeUIPatches.RequestedMode;
            RuntimeUIPatches.RequestedMode = mode;
            if (modeChanged)
            {
                RuntimeUIPatches.MarkSpriteManagerReloadRequired();
            }
            _lastConfiguredMode = mode;
            string configValue = NativeSettingsBridge.ToSettingValue(mode);
            if (!string.Equals(_mode.Value, configValue, StringComparison.OrdinalIgnoreCase))
            {
                _mode.Value = configValue;
            }
            if (saveConfig)
            {
                Config.Save();
            }
            NativeSettingsBridge.SaveMode(mode);
            NativeSettingsBridge.SyncLiveSetting(mode);
        }

        private void ReloadCurrentScene()
        {
            if (_reloadInProgress)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.name))
            {
                ShowStatus(MobileUiLocalization.Get("cannot_rebuild"));
                Logger.LogWarning("Cannot reload UI because the active scene is invalid.");
                return;
            }

            StartCoroutine(ReloadSceneAtEndOfFrame(scene.name));
        }

        private IEnumerator ReloadSceneAtEndOfFrame(string sceneName)
        {
            _reloadInProgress = true;
            yield return new WaitForEndOfFrame();

            // Prepare the target schema and virtual size without notifying the
            // UIManager that is about to be destroyed. The new UIManager will
            // perform its normal first layout once, with the final values.
            RuntimeUIPatches.PrepareForUpcomingSceneName(sceneName);

            // UISpriteManager keeps a platform-filtered atlas dictionary for
            // the lifetime of the process. A PC -> Mobile switch therefore
            // needs a fresh dictionary before the new Mobile prefabs Awake;
            // otherwise only sprites already present in the PC cache render.
            float spriteReloadDeadline = Time.realtimeSinceStartup + 20f;
            while (!RuntimeUIPatches.AdvanceSpriteManagerReload(sceneName) &&
                   Time.realtimeSinceStartup < spriteReloadDeadline)
            {
                yield return null;
            }
            RuntimeUIPatches.ReportSpriteManagerReloadWaitResult(sceneName);

            Logger.LogInfo(
                "Reloading scene '" + sceneName + "' for " + RuntimeUIPatches.ModeLabel +
                " UI prefabs after preparing native settings.");
            SceneManager.LoadScene(sceneName);
            _reloadInProgress = false;
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene currentScene)
        {
            bool previousPCUI = RuntimeUIPatches.UsePCUIForScene(previousScene);
            bool currentPCUI = RuntimeUIPatches.UsePCUIForScene(currentScene);

            Logger.LogInfo(
                "Scene UI policy: '" + previousScene.name + "' -> '" + currentScene.name +
                "', requested=" + RuntimeUIPatches.ModeLabel +
                ", effective=" + (currentPCUI ? "PC" : "Mobile"));

            // Do not rebuild ConfigInstance or resize here. Unity may already
            // be constructing ConfigMainWidget/UIManager at this point, which
            // caused an intermittent mixed schema and a visible loading resize.
        }

        private void ShowStatus(string text)
        {
            _statusText = text;
            _statusUntil = Time.realtimeSinceStartup + 4f;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }
    }

    internal static class NativeSettingsBridge
    {
        internal const string SettingKey = "ui_platform";
        private const string ScreenCategory = "screen";
        private const string UISizeKey = "ui_size";
        private const string SaveKey = "option:" + SettingKey;
        private const string MobileUISizeSaveKey = "option:ui_size_mobile";
        private const string MobileUISizeProfileKey = "option:ui_size_mobile_profile";
        private const string ReverseMobileUISizeProfile = "reverse_600_v1";
        private const string PreviousMobileUISizeProfile = "ascending_1024_v2";
        private const string MobileUISizeProfile = "ascending_1400_v3";

        private static readonly string[] ModeOptions = new string[] { "pc", "mobile" };
        private static readonly string[] MobileUISizeOptions = new string[]
        {
            "1400", "1600", "1800", "2000", "2200"
        };
        private static bool _reloadingSettings;
        private static bool _loadingConfigValues;
        private static bool _interceptedModeChange;

        internal static void ApplyPatches(Harmony harmony)
        {
            Patch(harmony, typeof(ConfigInstance).GetMethod(
                "LoadFromJson", BindingFlags.Static | BindingFlags.NonPublic), null, "AfterSettingsLoaded");
            Patch(harmony, typeof(ConfigInstance).GetMethod(
                "LoadConfigValue", BindingFlags.Static | BindingFlags.NonPublic), "BeforeValuesLoaded", "AfterValuesLoaded");
            Patch(harmony, typeof(ConfigInstance).GetMethod(
                "ChangeValue",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new Type[] { typeof(string), typeof(string), typeof(bool) },
                null), "BeforeStringValueChanged", "AfterStringValueChanged");
            Patch(harmony, typeof(LocalizeSystem).GetMethod(
                "Get", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(string) }, null), null, "AfterLocalizedTextGet");
            Patch(harmony, typeof(LocalizeSystem).GetMethod(
                "SetLocale", BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(string) }, null), null, "AfterLocaleChanged");
        }

        private static void Patch(Harmony harmony, MethodInfo target, string prefixName, string postfixName)
        {
            if (target == null)
            {
                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogWarning("Native settings patch target was not found.");
                }
                return;
            }

            HarmonyMethod prefix = null;
            HarmonyMethod postfix = null;
            if (!string.IsNullOrEmpty(prefixName))
            {
                prefix = new HarmonyMethod(typeof(NativeSettingsBridge).GetMethod(
                    prefixName, BindingFlags.Static | BindingFlags.NonPublic));
            }
            if (!string.IsNullOrEmpty(postfixName))
            {
                postfix = new HarmonyMethod(typeof(NativeSettingsBridge).GetMethod(
                    postfixName, BindingFlags.Static | BindingFlags.NonPublic));
            }
            harmony.Patch(target, prefix, postfix, null, null, null);
        }

        internal static void InstallSettings()
        {
            if (ConfigInstance.Settings == null)
            {
                return;
            }

            ShowSettingsInEveryClusterMode();

            bool pcLayout = RuntimeUIPatches.UsePCUI;
            string targetCategory = pcLayout ? ScreenCategory : "default";
            List<Setting> targetSettings;
            if (!ConfigInstance.Settings.TryGetValue(targetCategory, out targetSettings))
            {
                targetSettings = new List<Setting>();
                ConfigInstance.Settings[targetCategory] = targetSettings;
            }

            DurangoUIMode storedMode;
            if (!TryReadStoredMode(out storedMode))
            {
                storedMode = RuntimeUIPatches.RequestedMode;
            }
            string value = ToSettingValue(storedMode);

            Setting detached = DetachSetting(SettingKey);
            Setting setting = pcLayout
                ? (Setting)CreatePCModeSetting(detached as DropdownSetting, value)
                : (Setting)CreateMobileModeSetting(detached as ToggleSetting, value);

            if (pcLayout)
            {
                Setting uiSizeSetting = FindSetting(UISizeKey);
                int uiSizeIndex = (uiSizeSetting == null) ? -1 : targetSettings.IndexOf(uiSizeSetting);
                targetSettings.Insert((uiSizeIndex < 0) ? targetSettings.Count : uiSizeIndex, setting);
            }
            else
            {
                // The stock Mobile ui_size is DebugBuild-only and is removed
                // by ConfigInstance.LoadFromJson in release builds. Recreate
                // it when absent, using the original Mobile Toggle widget.
                ToggleSetting uiSizeSetting = CreateMobileUISizeSetting(
                    DetachSetting(UISizeKey) as ToggleSetting);
                targetSettings.Insert(0, setting);
                targetSettings.Insert(1, uiSizeSetting);
            }
        }

        private static void ShowSettingsInEveryClusterMode()
        {
            foreach (KeyValuePair<string, List<Setting>> pair in ConfigInstance.Settings)
            {
                List<Setting> settings = pair.Value;
                if (settings == null)
                {
                    continue;
                }

                for (int i = 0; i < settings.Count; i++)
                {
                    Setting setting = settings[i];
                    if (setting != null && setting.HideOnOffline)
                    {
                        // Keep release, prologue, platform, country and locale
                        // visibility rules intact. Only remove the cluster-mode
                        // filter so the same setting exists Online/Offline/Editable.
                        setting.HideOnOffline = false;
                    }
                }
            }
        }

        private static DropdownSetting CreatePCModeSetting(DropdownSetting setting, string value)
        {
            if (setting == null)
            {
                setting = new DropdownSetting();
            }
            setting.Key = SettingKey;
            setting.Type = SettingType.Dropdown;
            setting.Default = "pc";
            setting.Value = value;
            setting.Options = ModeOptions;
            setting.ButtonClickClose = true;
            setting.Custom = false;
            ConfigureVisibleSetting(setting);
            return setting;
        }

        private static ToggleSetting CreateMobileModeSetting(ToggleSetting setting, string value)
        {
            if (setting == null)
            {
                setting = new ToggleSetting();
            }
            setting.Key = SettingKey;
            setting.Type = SettingType.Toggle;
            setting.Default = "pc";
            setting.Value = value;
            setting.Options = ModeOptions;
            ConfigureVisibleSetting(setting);
            return setting;
        }

        private static ToggleSetting CreateMobileUISizeSetting(ToggleSetting setting)
        {
            if (setting == null)
            {
                setting = new ToggleSetting();
            }

            string value = GetStoredMobileUISizeValue();
            bool valid = false;
            for (int i = 0; i < MobileUISizeOptions.Length; i++)
            {
                if (string.Equals(MobileUISizeOptions[i], value, StringComparison.Ordinal))
                {
                    valid = true;
                    break;
                }
            }
            if (!valid)
            {
                value = "1800";
            }

            setting.Key = UISizeKey;
            setting.Type = SettingType.Toggle;
            setting.Default = "1800";
            setting.Value = value;
            setting.Options = MobileUISizeOptions;
            ConfigureVisibleSetting(setting);
            return setting;
        }

        internal static string GetStoredMobileUISizeValue()
        {
            string value = PlayerPrefs.GetString(MobileUISizeSaveKey, "1280");
            string profile = PlayerPrefs.GetString(MobileUISizeProfileKey, string.Empty);
            if (!string.Equals(
                profile,
                MobileUISizeProfile,
                StringComparison.Ordinal))
            {
                if (string.Equals(profile, PreviousMobileUISizeProfile, StringComparison.Ordinal))
                {
                    if (string.Equals(value, "1024", StringComparison.Ordinal))
                    {
                        value = "1400";
                    }
                    else if (string.Equals(value, "1280", StringComparison.Ordinal))
                    {
                        value = "1600";
                    }
                    else if (string.Equals(value, "1420", StringComparison.Ordinal))
                    {
                        value = "1800";
                    }
                    else if (string.Equals(value, "1600", StringComparison.Ordinal))
                    {
                        value = "2000";
                    }
                    else if (string.Equals(value, "1920", StringComparison.Ordinal))
                    {
                        value = "2200";
                    }
                    else
                    {
                        value = "1800";
                    }
                }
                else if (string.Equals(profile, ReverseMobileUISizeProfile, StringComparison.Ordinal))
                {
                    if (string.Equals(value, "1420", StringComparison.Ordinal))
                    {
                        value = "1400";
                    }
                    else if (string.Equals(value, "1280", StringComparison.Ordinal))
                    {
                        value = "1600";
                    }
                    else if (string.Equals(value, "1024", StringComparison.Ordinal))
                    {
                        value = "1800";
                    }
                    else if (string.Equals(value, "800", StringComparison.Ordinal))
                    {
                        value = "2000";
                    }
                    else if (string.Equals(value, "600", StringComparison.Ordinal))
                    {
                        value = "2200";
                    }
                    else
                    {
                        value = "1800";
                    }
                }
                else if (string.Equals(value, "800", StringComparison.Ordinal))
                {
                    value = "1400";
                }
                else if (string.Equals(value, "1024", StringComparison.Ordinal))
                {
                    value = "1600";
                }
                else if (string.Equals(value, "1280", StringComparison.Ordinal))
                {
                    value = "1800";
                }
                else if (string.Equals(value, "1420", StringComparison.Ordinal))
                {
                    value = "2000";
                }
                else if (string.Equals(value, "1600", StringComparison.Ordinal) ||
                         string.Equals(value, "1920", StringComparison.Ordinal))
                {
                    value = "2200";
                }
                else
                {
                    value = "1800";
                }

                PlayerPrefs.SetString(MobileUISizeSaveKey, value);
                PlayerPrefs.SetString(MobileUISizeProfileKey, MobileUISizeProfile);
                PlayerPrefs.Save();
            }
            return value;
        }

        private static void ConfigureVisibleSetting(Setting setting)
        {
            ValueSetting valueSetting = setting as ValueSetting;
            if (valueSetting != null)
            {
                valueSetting.PrepareLabelText = string.Empty;
            }
            setting.HideOnPrologue = false;
            setting.HideOnOffline = false;
            setting.HideOnRelease = false;
            setting.DebugBuild = false;
        }

        private static Setting DetachSetting(string key)
        {
            if (ConfigInstance.Settings == null)
            {
                return null;
            }

            Setting found = null;
            foreach (KeyValuePair<string, List<Setting>> pair in ConfigInstance.Settings)
            {
                List<Setting> list = pair.Value;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i] != null && list[i].Key == key)
                    {
                        if (found == null)
                        {
                            found = list[i];
                        }
                        list.RemoveAt(i);
                    }
                }
            }
            return found;
        }

        private static Setting FindSetting(string key)
        {
            if (ConfigInstance.Settings == null)
            {
                return null;
            }
            foreach (KeyValuePair<string, List<Setting>> pair in ConfigInstance.Settings)
            {
                List<Setting> list = pair.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].Key == key)
                    {
                        return list[i];
                    }
                }
            }
            return null;
        }

        internal static void ChangeNativeValue(DurangoUIMode mode, bool save)
        {
            string value = ToSettingValue(mode);
            if (ConfigInstance.Settings != null && FindSetting(SettingKey) != null)
            {
                ConfigInstance.ChangeValue(SettingKey, value, save);
            }
            else if (save)
            {
                PlayerPrefs.SetString(SaveKey, value);
                PlayerPrefs.Save();
            }
        }

        internal static void SyncLiveSetting(DurangoUIMode mode)
        {
            ValueSetting setting = FindSetting(SettingKey) as ValueSetting;
            if (setting != null)
            {
                setting.Value = ToSettingValue(mode);
            }
        }

        internal static void SaveMode(DurangoUIMode mode)
        {
            PlayerPrefs.SetString(SaveKey, ToSettingValue(mode));
            PlayerPrefs.Save();
        }

        internal static bool TryReadStoredMode(out DurangoUIMode mode)
        {
            mode = DurangoUIMode.PC;
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return false;
            }
            return TryParseMode(PlayerPrefs.GetString(SaveKey, null), out mode);
        }

        internal static bool TryParseMode(string value, out DurangoUIMode mode)
        {
            mode = DurangoUIMode.PC;
            if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
            {
                // Migrate values saved by v1.1.1 and earlier to the new PC default.
                return true;
            }
            if (string.Equals(value, "mobile", StringComparison.OrdinalIgnoreCase))
            {
                mode = DurangoUIMode.Mobile;
                return true;
            }
            if (string.Equals(value, "pc", StringComparison.OrdinalIgnoreCase))
            {
                mode = DurangoUIMode.PC;
                return true;
            }
            return false;
        }

        internal static string ToSettingValue(DurangoUIMode mode)
        {
            if (mode == DurangoUIMode.Mobile) return "mobile";
            return "pc";
        }

        internal static void ReloadAllSettings()
        {
            if (_reloadingSettings)
            {
                return;
            }

            MethodInfo loadFromJson = typeof(ConfigInstance).GetMethod(
                "LoadFromJson", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo loadConfigValue = typeof(ConfigInstance).GetMethod(
                "LoadConfigValue", BindingFlags.Static | BindingFlags.NonPublic);
            if (loadFromJson == null || loadConfigValue == null)
            {
                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogWarning("Could not rebuild ConfigInstance settings for the selected UI mode.");
                }
                return;
            }

            try
            {
                _reloadingSettings = true;
                loadFromJson.Invoke(null, null);
                loadConfigValue.Invoke(null, null);
                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogInfo(
                        "Native settings rebuilt for " + RuntimeUIPatches.ModeLabel +
                        " UI; UISize/Keybind plugin patch chains were retained.");
                }
            }
            catch (Exception exception)
            {
                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogError("Failed to rebuild native settings: " + exception);
                }
            }
            finally
            {
                _reloadingSettings = false;
            }
        }

        private static void BeforeValuesLoaded()
        {
            _loadingConfigValues = true;
            InstallSettings();
        }

        private static void AfterSettingsLoaded()
        {
            InstallSettings();
        }

        private static void AfterValuesLoaded()
        {
            // The original method has finished enumerating every category and
            // setting. Structural changes are safe again from this point.
            _loadingConfigValues = false;
            InstallSettings();
            DurangoUIMode mode;
            if (TryReadStoredMode(out mode) && MobilePCUISwitchPlugin.Instance != null)
            {
                MobilePCUISwitchPlugin.Instance.OnNativeModeChanged(ToSettingValue(mode), false);
            }
        }

        private static bool BeforeStringValueChanged(
            string key,
            string value,
            bool save,
            ref string __result)
        {
            _interceptedModeChange = false;
            if (key != SettingKey || !save || _reloadingSettings || MobilePCUISwitchPlugin.Instance == null)
            {
                return true;
            }

            DurangoUIMode requested;
            if (!TryParseMode(value, out requested) || requested == RuntimeUIPatches.RequestedMode)
            {
                return true;
            }

            _interceptedModeChange = true;
            __result = ToSettingValue(RuntimeUIPatches.RequestedMode);
            MobilePCUISwitchPlugin.Instance.RequestNativeModeChange(requested);
            return false;
        }

        private static void AfterStringValueChanged(string key, string value, bool save)
        {
            if (_interceptedModeChange)
            {
                _interceptedModeChange = false;
                return;
            }
            if (key != SettingKey || MobilePCUISwitchPlugin.Instance == null)
            {
                return;
            }
            MobilePCUISwitchPlugin.Instance.OnNativeModeChanged(value, save && !_reloadingSettings);
        }

        private static void AfterLocalizedTextGet(string __0, ref string __result)
        {
            string text;
            if (TryGetLocalizedText(__0, out text))
            {
                __result = text;
            }
        }

        private static void AfterLocaleChanged()
        {
            // LoadConfigValue changes locale while iterating a List<Setting>.
            // Detaching/inserting UI Mode or UI Size from this callback would
            // invalidate that enumerator and abort GameManager.OnAwake.
            if (_loadingConfigValues)
            {
                return;
            }
            InstallSettings();
        }

        private static bool TryGetLocalizedText(string key, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(key)) return false;

            if (key == "#config_" + SettingKey)
            {
                text = MobileUiLocalization.Get("ui_mode");
                return true;
            }
            if (key == "#config_" + SettingKey + "_mobile")
            {
                text = MobileUiLocalization.Get("mobile");
                return true;
            }
            if (key == "#config_" + SettingKey + "_pc")
            {
                text = MobileUiLocalization.Get("pc");
                return true;
            }
            return false;
        }
    }

    internal static class RuntimeUIPatches
    {
        internal static DurangoUIMode RequestedMode = DurangoUIMode.PC;
        private static readonly FieldInfo UIManagerSizeField = typeof(UIManager).GetField(
            "_uiSize", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo UIAnchorPolicyField = typeof(UIAnchorPolicy).GetField(
            "_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo IconMapEnumCacheField = typeof(IconMap).GetField(
            "CachedEnumIcon", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MenuContainerFirstDepthField = typeof(MenuContainer).GetField(
            "FirstDepth", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo MenuContainerUIMenuField = typeof(MenuContainer).GetField(
            "UIMenu", BindingFlags.Static | BindingFlags.NonPublic);
        private static bool _preparingScene;
        private static string _sceneNameOverride;
        private static bool? _forcedPCUIOverride;
        private static string _preparedForNextAwake;
        private static bool _spriteManagerReloadRequired;
        private static bool _spriteManagerReloadInProgress;
        private static bool _spriteManagerReloadPCUI;
        private static bool _platformMenuCacheRefreshRequired;

        internal static bool UsePCUI
        {
            get
            {
                if (_forcedPCUIOverride.HasValue)
                {
                    return _forcedPCUIOverride.Value;
                }
                if (!string.IsNullOrEmpty(_sceneNameOverride))
                {
                    return UsePCUIForSceneName(_sceneNameOverride);
                }
                return UsePCUIForScene(SceneManager.GetActiveScene());
            }
        }

        internal static bool UsePCUIForScene(Scene scene)
        {
            return UsePCUIForSceneName(scene.IsValid() ? scene.name : string.Empty);
        }

        private static bool UsePCUIForSceneName(string sceneName)
        {
            // UI mode now applies to the complete native UI lifecycle. In
            // Mobile mode this includes the real Mobile Title controller and
            // prefab set, not only the gameplay scene.
            return RequestedMode == DurangoUIMode.PC;
        }

        internal static string ModeLabel
        {
            get
            {
                return RequestedMode.ToString();
            }
        }

        internal static string EffectiveModeLabel
        {
            get
            {
                return UsePCUI ? "PC" : "Mobile";
            }
        }

        internal static void MarkSpriteManagerReloadRequired()
        {
            _spriteManagerReloadRequired = true;
            _platformMenuCacheRefreshRequired = true;
            if (MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "UI mode changed; UISpriteManager atlas cache will be rebuilt for " +
                    ModeLabel + ".");
            }
        }

        private static void RefreshPlatformMenuCaches(bool pcUI)
        {
            if (!_platformMenuCacheRefreshRequired)
            {
                return;
            }

            int clearedIconTypes = 0;
            IDictionary iconCache = (IconMapEnumCacheField == null)
                ? null
                : IconMapEnumCacheField.GetValue(null) as IDictionary;
            if (iconCache != null)
            {
                clearedIconTypes = iconCache.Count;
                iconCache.Clear();
            }

            // MenuContainer is initialized once. If its first access happened
            // under PC UI, Screenshot remains in the normal first-depth list
            // forever and becomes a duplicate text entry in Mobile UI. Keep
            // both static lists aligned with the selected platform.
            IList firstDepth = (MenuContainerFirstDepthField == null)
                ? null
                : MenuContainerFirstDepthField.GetValue(null) as IList;
            IList uiMenu = (MenuContainerUIMenuField == null)
                ? null
                : MenuContainerUIMenuField.GetValue(null) as IList;
            SetScreenshotMenuMembership(firstDepth, pcUI);
            SetScreenshotMenuMembership(uiMenu, pcUI);

            _platformMenuCacheRefreshRequired = false;
            if (MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "Refreshed platform menu caches for " + (pcUI ? "PC" : "Mobile") +
                    " UI: cleared IconMap enum types=" + clearedIconTypes +
                    ", Screenshot first-depth=" + pcUI + ".");
            }
        }

        private static void SetScreenshotMenuMembership(IList list, bool include)
        {
            if (list == null)
            {
                return;
            }

            while (list.Contains(MenuType.Screenshot))
            {
                list.Remove(MenuType.Screenshot);
            }
            if (include)
            {
                list.Add(MenuType.Screenshot);
            }
        }

        private static void TryStartSpriteManagerReload(bool pcUI)
        {
            if (!_spriteManagerReloadRequired)
            {
                return;
            }

            UISpriteManager spriteManager = ResourceSingleton<UISpriteManager>.Instance();
            if (spriteManager == null)
            {
                _spriteManagerReloadRequired = false;
                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogWarning(
                        "Cannot rebuild UISpriteManager atlas cache because the resource is unavailable.");
                }
                return;
            }

            // Do not mix callbacks from two platform loads. Load() itself
            // ignores calls while Loading, so retry after the current load has
            // completed instead of incorrectly treating its atlas set as ours.
            if (spriteManager.LoadingStatus == UISpriteManager.Status.Loading)
            {
                return;
            }

            _spriteManagerReloadRequired = false;
            _spriteManagerReloadInProgress = true;
            _spriteManagerReloadPCUI = pcUI;
            spriteManager.Load();

            if (MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "Rebuilding UISpriteManager atlas cache for " +
                    (pcUI ? "PC" : "Mobile") + " UI.");
            }
        }

        internal static bool AdvanceSpriteManagerReload(string sceneName)
        {
            UISpriteManager spriteManager = ResourceSingleton<UISpriteManager>.Instance();
            if (spriteManager == null)
            {
                _spriteManagerReloadRequired = false;
                _spriteManagerReloadInProgress = false;
                return true;
            }

            if (_spriteManagerReloadInProgress)
            {
                if (spriteManager.LoadingStatus == UISpriteManager.Status.Loading)
                {
                    return false;
                }

                if (MobilePCUISwitchPlugin.Log != null)
                {
                    if (spriteManager.LoadingStatus == UISpriteManager.Status.Ready)
                    {
                        MobilePCUISwitchPlugin.Log.LogInfo(
                            "UISpriteManager atlas cache ready for " +
                            (_spriteManagerReloadPCUI ? "PC" : "Mobile") + " UI.");
                    }
                    else
                    {
                        MobilePCUISwitchPlugin.Log.LogWarning(
                            "UISpriteManager atlas cache rebuild ended with status " +
                            spriteManager.LoadingStatus + ".");
                    }
                }
                _spriteManagerReloadInProgress = false;
            }

            if (_spriteManagerReloadRequired)
            {
                TryStartSpriteManagerReload(UsePCUIForSceneName(sceneName));
            }

            return !_spriteManagerReloadRequired && !_spriteManagerReloadInProgress;
        }

        internal static void ReportSpriteManagerReloadWaitResult(string sceneName)
        {
            if ((_spriteManagerReloadRequired || _spriteManagerReloadInProgress) &&
                MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogWarning(
                    "Timed out waiting for UISpriteManager atlas cache before reloading scene '" +
                    sceneName + "'.");
            }
        }

        internal static void PrepareForSceneName(string sceneName)
        {
            if (_preparingScene || string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            bool pcUI = UsePCUIForSceneName(sceneName);
            int targetSize = pcUI ? GetStoredPCUISize() : GetStoredMobileUISize();
            try
            {
                _preparingScene = true;
                _sceneNameOverride = sceneName;
                SetUIManagerSizeField(targetSize);
                SetAnchorPolicy(pcUI);
                RefreshPlatformMenuCaches(pcUI);
                TryStartSpriteManagerReload(pcUI);

                // Reload before the target UIManager creates Config widgets.
                // SetUISize is suppressed during this window, so the outgoing
                // scene never receives a second resize notification.
                NativeSettingsBridge.ReloadAllSettings();
                SetUIManagerSizeField(targetSize);
                SetAnchorPolicy(pcUI);

                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogInfo(
                        "Prepared scene '" + sceneName + "': effective=" +
                        (pcUI ? "PC" : "Mobile") + ", UI size=" + targetSize);
                }
            }
            finally
            {
                _sceneNameOverride = null;
                _preparingScene = false;
            }
        }

        internal static void PrepareForUpcomingSceneName(string sceneName)
        {
            PrepareForSceneName(sceneName);
            _preparedForNextAwake = sceneName;
        }

        private static void EnsureWidgetSchema(ConfigMainWidget widget)
        {
            if (widget == null || ConfigInstance.Settings == null)
            {
                return;
            }

            bool pcWidget = widget is ConfigMainWidget_PC;
            bool pcSchema = IsPCSettingsSchema();
            if (pcWidget == pcSchema)
            {
                return;
            }

            int targetSize = pcWidget ? GetStoredPCUISize() : GetStoredMobileUISize();
            try
            {
                _preparingScene = true;
                _forcedPCUIOverride = pcWidget;
                SetUIManagerSizeField(targetSize);
                SetAnchorPolicy(pcWidget);
                NativeSettingsBridge.ReloadAllSettings();
                SetUIManagerSizeField(targetSize);
                SetAnchorPolicy(pcWidget);

                if (MobilePCUISwitchPlugin.Log != null)
                {
                    MobilePCUISwitchPlugin.Log.LogWarning(
                        "Corrected mismatched Settings schema before widget Awake: widget=" +
                        (pcWidget ? "PC" : "Mobile") + ", previous schema=" +
                        (pcSchema ? "PC" : "Mobile"));
                }
            }
            finally
            {
                _forcedPCUIOverride = null;
                _preparingScene = false;
            }
        }

        private static bool IsPCSettingsSchema()
        {
            foreach (KeyValuePair<string, List<Setting>> pair in ConfigInstance.Settings)
            {
                List<Setting> settings = pair.Value;
                for (int i = 0; i < settings.Count; i++)
                {
                    Setting setting = settings[i];
                    if (setting != null && string.Equals(setting.Key, "ui_size", StringComparison.Ordinal))
                    {
                        // The original PC schema owns a DropdownSetting while
                        // the original Mobile schema owns a ToggleSetting.
                        // This remains reliable even when legacy plugins add a
                        // synthetic Screen category to Mobile settings.
                        return setting is DropdownSetting;
                    }
                }
            }

            return ConfigInstance.Settings.ContainsKey("screen");
        }

        private static int GetStoredPCUISize()
        {
            int size;
            string stored = PlayerPrefs.GetString("option:ui_size", "1280");
            if (!int.TryParse(stored, out size) || size < 640 || size > 2560)
            {
                size = 1280;
            }
            return size;
        }

        private static int GetStoredMobileUISize()
        {
            int size;
            string stored = NativeSettingsBridge.GetStoredMobileUISizeValue();
            if (!int.TryParse(stored, out size) ||
                (size != 1400 && size != 1600 && size != 1800 && size != 2000 && size != 2200))
            {
                size = 1800;
            }
            return size;
        }

        private static void SetUIManagerSizeField(int size)
        {
            if (UIManagerSizeField != null)
            {
                UIManagerSizeField.SetValue(null, size);
            }
        }

        private static void SetAnchorPolicy(bool pcUI)
        {
            if (UIAnchorPolicyField != null)
            {
                UIAnchorPolicyBase policy = pcUI
                    ? (UIAnchorPolicyBase)new UIAnchorPolicy_PC()
                    : (UIAnchorPolicyBase)new UIAnchorPolicy_Mobile();
                UIAnchorPolicyField.SetValue(null, policy);
            }
        }

        internal static void Apply(Harmony harmony)
        {
            HashSet<MethodBase> patchedUsePCUI = new HashSet<MethodBase>();
            HashSet<MethodBase> patchedPortrait = new HashSet<MethodBase>();
            Type platformType = typeof(Platform);
            Type[] types = GetLoadableTypes(platformType.Assembly);

            HarmonyMethod usePCUIPostfix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                "UsePCUIPostfix", BindingFlags.Static | BindingFlags.NonPublic));
            HarmonyMethod supportPortraitPostfix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                "SupportPortraitPostfix", BindingFlags.Static | BindingFlags.NonPublic));

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || !platformType.IsAssignableFrom(type)) continue;
                PatchDeclaredGetter(harmony, type, "UsePCUI", usePCUIPostfix, patchedUsePCUI);
                PatchDeclaredGetter(harmony, type, "SupportPortrait", supportPortraitPostfix, patchedPortrait);
            }

            MethodInfo pcResolution = typeof(Platform_PC).GetMethod(
                "GetScreenResolution",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new Type[] { typeof(bool), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() },
                null);
            if (pcResolution != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "PCScreenResolutionPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(pcResolution, prefix, null, null, null, null);
            }

            MethodInfo uiManagerAwake = typeof(UIManager).GetMethod(
                "OnAwake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (uiManagerAwake != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "UIManagerAwakePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(uiManagerAwake, prefix, null, null, null, null);
            }

            MethodInfo setUISize = typeof(UIManager).GetMethod(
                "SetUISize", BindingFlags.Static | BindingFlags.Public,
                null, new Type[] { typeof(int) }, null);
            if (setUISize != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "UIManagerSetUISizePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(setUISize, prefix, null, null, null, null);
            }

            MethodInfo configWidgetAwake = typeof(ConfigMainWidget).GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            if (configWidgetAwake != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "ConfigMainWidgetAwakePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(configWidgetAwake, prefix, null, null, null, null);
            }

            MethodInfo titleLoading = typeof(TitleMenuGroup).GetMethod(
                "CoLoadingLevel", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new Type[] { typeof(string) }, null);
            if (titleLoading != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "TitleLoadingPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(titleLoading, prefix, null, null, null, null);
            }

            MethodInfo getMainPrefabs = typeof(UIPrefabMap).GetMethod(
                "GetMain", BindingFlags.Instance | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            if (getMainPrefabs != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "UIPrefabMapGetMainPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(getMainPrefabs, prefix, null, null, null, null);
            }

            MethodInfo getTitlePrefabs = typeof(UIPrefabMap).GetMethod(
                "GetTitle", BindingFlags.Instance | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            if (getTitlePrefabs != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "UIPrefabMapGetTitlePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(getTitlePrefabs, prefix, null, null, null, null);
            }

            MethodInfo titleStateSetter = typeof(TitleMenuGroup).GetProperty(
                "CurState", BindingFlags.Instance | BindingFlags.NonPublic).GetSetMethod(true);
            if (titleStateSetter != null)
            {
                HarmonyMethod postfix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "TitleStateChangedPostfix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(titleStateSetter, null, postfix, null, null, null);
            }

            MethodInfo titleRequest = typeof(TitleMenuGroup).GetMethod(
                "RequestHttpUrl", BindingFlags.Instance | BindingFlags.NonPublic);
            if (titleRequest != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "TitleRequestHttpUrlPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(titleRequest, prefix, null, null, null, null);
            }

            MethodInfo titleRequestSucceeded = typeof(TitleMenuGroup).GetMethod(
                "OnRequestSucceed", BindingFlags.Instance | BindingFlags.NonPublic);
            if (titleRequestSucceeded != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "TitleRequestSucceededPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(titleRequestSucceeded, prefix, null, null, null, null);
            }

            MethodInfo titleApplyEmigrationMode = typeof(TitleMenuGroup).GetMethod(
                "ApplyEmigrationMode", BindingFlags.Instance | BindingFlags.NonPublic);
            if (titleApplyEmigrationMode != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "TitleApplyEmigrationModePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(titleApplyEmigrationMode, prefix, null, null, null, null);
            }

            MethodInfo loadConfigJson = typeof(ConfigInstance).GetMethod(
                "LoadFromJson", BindingFlags.Static | BindingFlags.NonPublic);
            if (loadConfigJson != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "ConfigLoadFromJsonPrefix", BindingFlags.Static | BindingFlags.NonPublic));
                HarmonyMethod postfix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "ConfigLoadFromJsonPostfix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(loadConfigJson, prefix, postfix, null, null, null);
            }

            MethodInfo changeLocale = typeof(ConfigInstance).GetMethod(
                "ChangeLocale", BindingFlags.Static | BindingFlags.NonPublic,
                null, new Type[] { typeof(string) }, null);
            if (changeLocale != null)
            {
                HarmonyMethod prefix = new HarmonyMethod(typeof(RuntimeUIPatches).GetMethod(
                    "ChangeLocalePrefix", BindingFlags.Static | BindingFlags.NonPublic));
                harmony.Patch(changeLocale, prefix, null, null, null, null);
            }

            if (MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "Patched UI platform selectors: UsePCUI=" + patchedUsePCUI.Count +
                    ", SupportPortrait=" + patchedPortrait.Count +
                    ", PC resolution=" + (pcResolution != null) +
                    ", scene preparation=" + (uiManagerAwake != null && setUISize != null) +
                    ", schema guard=" + (configWidgetAwake != null) +
                    ", direct Main prefabs=" + (getMainPrefabs != null) +
                    ", direct Title prefabs=" + (getTitlePrefabs != null) +
                    ", Title diagnostics=" +
                    (titleStateSetter != null && titleRequest != null && titleRequestSucceeded != null) +
                    ", Mobile Title movie fallback=" + (titleApplyEmigrationMode != null) +
                    ", forced config source=" + (loadConfigJson != null) +
                    ", safe locale load=" + (changeLocale != null));
            }
        }

        private static void UIManagerAwakePrefix()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                if (string.Equals(
                    _preparedForNextAwake, scene.name, StringComparison.OrdinalIgnoreCase))
                {
                    _preparedForNextAwake = null;
                    if (MobilePCUISwitchPlugin.Log != null)
                    {
                        MobilePCUISwitchPlugin.Log.LogInfo(
                            "Using settings prepared before UIManager.Awake for scene '" +
                            scene.name + "'.");
                    }
                    return;
                }

                _preparedForNextAwake = null;
                PrepareForSceneName(scene.name);
            }
        }

        private static bool UIManagerSetUISizePrefix(int __0)
        {
            if (!_preparingScene)
            {
                return true;
            }

            SetUIManagerSizeField(__0);
            return false;
        }

        private static void ConfigMainWidgetAwakePrefix(ConfigMainWidget __instance)
        {
            EnsureWidgetSchema(__instance);
        }

        private static bool UIPrefabMapGetMainPrefix(
            UIPrefabMap __instance,
            ref GameObject[] __result)
        {
            UIPrefabMap.Type type = RequestedMode == DurangoUIMode.PC
                ? UIPrefabMap.Type.PC
                : UIPrefabMap.Type.Mobile;
            __result = __instance.GetUIList(type, UIPrefabMap.Category.Main);
            return false;
        }

        private static bool UIPrefabMapGetTitlePrefix(
            UIPrefabMap __instance,
            ref GameObject[] __result)
        {
            UIPrefabMap.Type type = RequestedMode == DurangoUIMode.PC
                ? UIPrefabMap.Type.PC
                : UIPrefabMap.Type.Mobile;
            __result = __instance.GetUIList(type, UIPrefabMap.Category.Title);
            if (MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "Title prefab set selected: " + type +
                    ", count=" + ((__result == null) ? 0 : __result.Length));
            }
            return false;
        }

        private static void TitleStateChangedPostfix(TitleMenuGroup.State __0)
        {
            if (RequestedMode != DurangoUIMode.Mobile || MobilePCUISwitchPlugin.Log == null)
            {
                return;
            }

            MobilePCUISwitchPlugin.Log.LogInfo(
                "Mobile Title state -> " + __0 +
                "; cluster=" + (GameManager.ClusterKey ?? "<null>") +
                "; gateway=" + (GameManager.GatewayUrl ?? "<null>") +
                "; playerSelected=" + GameManager.IsPlayerIdSelected +
                "; connectCluster=" + (GameManager.ConnectCluster != null));
        }

        private static void TitleRequestHttpUrlPrefix(string __0)
        {
            if (RequestedMode == DurangoUIMode.Mobile && MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo("Mobile Title HTTP request: " + (__0 ?? "<null>"));
            }
        }

        private static void TitleRequestSucceededPrefix(string __0)
        {
            if (RequestedMode == DurangoUIMode.Mobile && MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "Mobile Title HTTP response received; length=" +
                    (string.IsNullOrEmpty(__0) ? 0 : __0.Length));
            }
        }

        private static void TitleApplyEmigrationModePrefix(TitleMenuGroup __instance)
        {
            if (RequestedMode != DurangoUIMode.Mobile || __instance == null)
            {
                return;
            }

            FieldInfo titleListField = typeof(TitleMenuGroup).GetField(
                "_titleList", BindingFlags.Instance | BindingFlags.NonPublic);
            IList titleList = (titleListField == null)
                ? null
                : titleListField.GetValue(__instance) as IList;
            if (titleList == null)
            {
                return;
            }

            int remapped = 0;
            for (int i = 0; i < titleList.Count; i++)
            {
                object option = titleList[i];
                if (option == null)
                {
                    continue;
                }

                FieldInfo videoNameField = option.GetType().GetField(
                    "VideoName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                string mobilePath = (videoNameField == null)
                    ? null
                    : videoNameField.GetValue(option) as string;
                if (string.IsNullOrEmpty(mobilePath) ||
                    !mobilePath.StartsWith("Movie/Mobile/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string pcPath = "Movie/PC/" + mobilePath.Substring("Movie/Mobile/".Length);
                string localPath = System.IO.Path.Combine(
                    Application.streamingAssetsPath,
                    pcPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(localPath))
                {
                    if (MobilePCUISwitchPlugin.Log != null)
                    {
                        MobilePCUISwitchPlugin.Log.LogWarning(
                            "Mobile Title movie fallback not found: " + localPath);
                    }
                    continue;
                }

                // TitleOptions is a private value type, so write the modified
                // boxed value back into IList after changing VideoName.
                videoNameField.SetValue(option, pcPath);
                titleList[i] = option;
                remapped++;
            }

            if (MobilePCUISwitchPlugin.Log != null)
            {
                MobilePCUISwitchPlugin.Log.LogInfo(
                    "Mobile Title movies remapped to Movie/PC: " + remapped +
                    "/" + titleList.Count);
            }
        }

        private static void ConfigLoadFromJsonPrefix(ref bool? __state)
        {
            __state = _forcedPCUIOverride;
            bool selectedPCUI = UsePCUI;
            _forcedPCUIOverride = selectedPCUI;
        }

        private static void ConfigLoadFromJsonPostfix(bool? __state)
        {
            _forcedPCUIOverride = __state;
        }

        private static void TitleLoadingPrefix(string __0)
        {
            PrepareForUpcomingSceneName(__0);
        }

        private static bool ChangeLocalePrefix(string __0, ref string __result)
        {
            if (!_preparingScene)
            {
                return true;
            }

            if (string.IsNullOrEmpty(__0) ||
                string.Equals(LocalizeSystem.Locale, __0, StringComparison.OrdinalIgnoreCase))
            {
                __result = LocalizeSystem.Locale;
                return false;
            }

            // Automatic config loading must not open a confirmation MessageBox
            // before PopupGroup/MessageBoxInfoWidget exists in the new scene.
            __result = LocalizeSystem.SetLocale(__0);
            return false;
        }

        private static Type[] GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        private static void PatchDeclaredGetter(
            Harmony harmony,
            Type type,
            string propertyName,
            HarmonyMethod postfix,
            HashSet<MethodBase> patched)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            MethodInfo getter = (property == null) ? null : property.GetGetMethod(true);
            if (getter == null || patched.Contains(getter)) return;
            harmony.Patch(getter, null, postfix, null, null, null);
            patched.Add(getter);
        }

        private static void UsePCUIPostfix(ref bool __result)
        {
            __result = UsePCUI;
        }

        private static void SupportPortraitPostfix(ref bool __result)
        {
            __result = !UsePCUI;
        }

        private static bool PCScreenResolutionPrefix(bool __0, ref int __1, ref int __2, ref bool __result)
        {
            if (UsePCUI)
            {
                return true;
            }

            int uiSize = UIManager.UISize;
            int shortSide = Mathf.RoundToInt((float)uiSize * UIAnchorPolicy.DefaultAspectRatio);
            if (__0)
            {
                __1 = shortSide;
                __2 = uiSize;
            }
            else
            {
                __1 = uiSize;
                __2 = shortSide;
            }
            __result = true;
            return false;
        }
    }
}
