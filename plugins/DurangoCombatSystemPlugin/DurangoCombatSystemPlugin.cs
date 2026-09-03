using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Baominix.DurangoOriginal.CombatSystem.Runtime;
using Baominix.DurangoOriginal.CombatSystem.Presentation;
using Baominix.DurangoOriginal.CombatSystem.EquipmentPerformance;
using Durango.Utils;
using HarmonyLib;

namespace Baominix.DurangoOriginal.CombatSystem
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "com.baominix.durango.original.logcontrol",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DurangoCombatSystemPlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "com.baominix.durango.original.combatsystem";
        public const string PluginName =
            "Durango Combat System Plugin (Original)";
        public const string PluginVersion = "0.3.12";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> ValidateEmbeddedData;
        internal static ConfigEntry<bool> SaurusAiEnabled;

        private Harmony _harmony;
        private int _lastEquipmentRuleLevel = -1;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Enable the reconstructed single-player combat system.");
            ValidateEmbeddedData = Config.Bind(
                "Development",
                "ValidateEmbeddedData",
                true,
                "Validate combat data embedded inside the plugin DLL at startup.");
            SaurusAiEnabled = Config.Bind(
                "SaurusAI",
                "Enabled",
                true,
                "Enable the shared single-player Saurus AI state machine for supported wild animals.");
            CombatDataRegistry.Reset();
            CombatDataLoadReport report =
                CombatDataRegistry.LoadEmbeddedData();
            if (ValidateEmbeddedData.Value)
            {
                WriteReport(report);
            }

            if (!Enabled.Value)
            {
                Log.LogInfo(PluginName + " is disabled by config.");
                return;
            }

            if (report != null && !report.IsValid)
            {
                Log.LogError(
                    "Combat protocol bridge was not installed because " +
                    "embedded combat-data validation failed.");
                return;
            }

            CombatRuntime.Reset();
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo(
                PluginName + " " + PluginVersion +
                " initialized (player combat and Saurus AI core active)."
            );
        }

        private void Update()
        {
            if (Enabled == null || !Enabled.Value)
            {
                return;
            }

            EquipmentLevelRules.Tick();
            if (GameSystem<StatisticsSystem>.HasInstance())
            {
                int playerLevel = GameSystem<StatisticsSystem>.Instance().Level;
                if (playerLevel != _lastEquipmentRuleLevel)
                {
                    _lastEquipmentRuleLevel = playerLevel;
                    EquipmentLevelRules.RefreshForPlayerLevel();
                }
            }

            CombatRuntime.Process(Times.UnixTimeNow());
        }

        private static void WriteReport(CombatDataLoadReport report)
        {
            if (report == null)
            {
                Log.LogError("Combat embedded-data validation returned no report.");
                return;
            }

            int i;
            for (i = 0; i < report.Warnings.Count; i++)
            {
                Log.LogWarning(report.Warnings[i]);
            }
            for (i = 0; i < report.Errors.Count; i++)
            {
                Log.LogError(report.Errors[i]);
            }

            if (report.IsValid)
            {
                Log.LogInfo(
                    "Combat reference data validated: animals=" +
                    report.ProfileCount + ", frameworks=" +
                    report.FrameworkCount + ".");
            }
        }

        private void OnDestroy()
        {
            SaurusDebugBubble.SetEnabled(false);
            CombatRuntime.Reset();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
            CombatDataRegistry.Reset();
            Log = null;
        }
    }
}
