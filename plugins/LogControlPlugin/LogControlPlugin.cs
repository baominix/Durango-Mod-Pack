using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DurangoOriginal.LogControl
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class LogControlPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.logcontrol";
        public const string PluginName = "Durango Plugin Log Control";
        public const string PluginVersion = "1.0.0";
        internal const string ManagedGuidPrefix = "com.baominix.durango.original.";

        private void Awake()
        {
            LogControl.Initialize(Config);
            new Harmony(PluginGuid).PatchAll(typeof(LogControlPlugin).Assembly);
        }
    }

    internal static class LogControl
    {
        private static ConfigEntry<bool> logEnabled;
        private static ConfigFile configFile;
        private static int knownPluginCount = -1;
        private static readonly Dictionary<string, ConfigEntry<bool>> PluginSettings =
            new Dictionary<string, ConfigEntry<bool>>(StringComparer.OrdinalIgnoreCase);

        internal static void Initialize(ConfigFile config)
        {
            configFile = config;
            logEnabled = config.Bind(
                "General",
                "Log_Enabled",
                false,
                "Enable log output from Durango mod plugins. Individual plugins can be controlled below.");

            RefreshPluginSettings();
        }

        private static void RefreshPluginSettings()
        {
            if (configFile == null || knownPluginCount == Chainloader.PluginInfos.Count)
            {
                return;
            }

            knownPluginCount = Chainloader.PluginInfos.Count;
            List<PluginInfo> plugins = new List<PluginInfo>(Chainloader.PluginInfos.Values);
            plugins.Sort(delegate(PluginInfo left, PluginInfo right)
            {
                return StringComparer.OrdinalIgnoreCase.Compare(left.Metadata.Name, right.Metadata.Name);
            });

            foreach (PluginInfo plugin in plugins)
            {
                string guid = plugin.Metadata.GUID;
                if (!guid.StartsWith(LogControlPlugin.ManagedGuidPrefix, StringComparison.OrdinalIgnoreCase) ||
                    guid.Equals(LogControlPlugin.PluginGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string sourceName = plugin.Metadata.Name;
                if (PluginSettings.ContainsKey(sourceName))
                {
                    continue;
                }

                ConfigEntry<bool> setting = configFile.Bind(
                    "Plugins",
                    sourceName,
                    false,
                    "Write this plugin's messages to BepInEx/LogOutput.log.");
                PluginSettings[sourceName] = setting;
            }
        }

        internal static bool ShouldWrite(LogEventArgs eventArgs)
        {
            if (eventArgs == null || eventArgs.Source == null)
            {
                return true;
            }

            RefreshPluginSettings();

            ConfigEntry<bool> pluginSetting;
            if (!PluginSettings.TryGetValue(eventArgs.Source.SourceName, out pluginSetting))
            {
                return true;
            }

            return logEnabled != null && logEnabled.Value && pluginSetting.Value;
        }
    }

    [HarmonyPatch(typeof(DiskLogListener), "LogEvent")]
    internal static class DiskLogListenerLogEventPatch
    {
        private static bool Prefix(LogEventArgs eventArgs)
        {
            return LogControl.ShouldWrite(eventArgs);
        }
    }
}
