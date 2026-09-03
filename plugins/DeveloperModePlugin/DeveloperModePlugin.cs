using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace Baominix.DurangoOriginal.DeveloperMode
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(
        "com.baominix.durango.original.logcontrol",
        BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(
        "com.baominix.durango.original.combatsystem",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class DeveloperModePlugin : BaseUnityPlugin
    {
        public const string PluginGuid =
            "com.baominix.durango.original.developermode";
        public const string PluginName = "Developer Mode Plugin";
        public const string PluginVersion = "0.1.5";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> AttackAlert;
        internal static ConfigEntry<bool> AnimalBubble;

        private static DeveloperModePlugin _instance;
        private static bool _originalAttackAlert;
        private static bool _animalBubbleRuntimeActive;
        private static bool _animalBubbleRuntimeAvailable;
        private Harmony _harmony;

        private void Awake()
        {
            _instance = this;
            Log = Logger;
            Enabled = Config.Bind(
                "General",
                "Enabled",
                false,
                "Enable developer commands and configured developer toggles.");
            AttackAlert = Config.Bind(
                "DeveloperToggles",
                "AttackAlert",
                false,
                "Enable the original CombatSystem.AttackAlertEnabled developer visualization.");
            AnimalBubble = Config.Bind(
                "DeveloperToggles",
                "AnimalBubble",
                false,
                "Show live Saurus AI state and animation diagnostics in chat bubbles above animals.");

            _originalAttackAlert = global::CombatSystem.AttackAlertEnabled;
            ApplyRuntimeState();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(
                PluginName + " " + PluginVersion +
                " loaded (Enabled=" + Enabled.Value + ").");
        }

        internal static bool IsEnabled
        {
            get { return Enabled != null && Enabled.Value; }
        }

        internal static void SetEnabled(bool value)
        {
            if (Enabled == null)
            {
                return;
            }
            Enabled.Value = value;
            ApplyRuntimeState();
            SaveConfig();
        }

        internal static void SetAttackAlert(bool value)
        {
            if (AttackAlert == null)
            {
                return;
            }
            AttackAlert.Value = value;
            ApplyRuntimeState();
            SaveConfig();
        }

        internal static void SetAnimalBubble(bool value)
        {
            if (AnimalBubble == null)
            {
                return;
            }
            AnimalBubble.Value = value;
            ApplyRuntimeState();
            SaveConfig();
        }

        internal static bool AnimalBubbleRuntimeActive
        {
            get { return _animalBubbleRuntimeActive; }
        }

        internal static bool AnimalBubbleRuntimeAvailable
        {
            get { return _animalBubbleRuntimeAvailable; }
        }

        internal static void ResetToggles()
        {
            if (AttackAlert != null)
            {
                AttackAlert.Value = false;
            }
            if (AnimalBubble != null)
            {
                AnimalBubble.Value = false;
            }
            ApplyRuntimeState();
            SaveConfig();
        }

        private static void ApplyRuntimeState()
        {
            global::CombatSystem.AttackAlertEnabled =
                IsEnabled && AttackAlert != null && AttackAlert.Value
                    ? true
                    : _originalAttackAlert;
            ApplyAnimalBubbleRuntime(
                IsEnabled && AnimalBubble != null &&
                AnimalBubble.Value);
        }

        private static void ApplyAnimalBubbleRuntime(bool enabled)
        {
            _animalBubbleRuntimeActive = false;
            _animalBubbleRuntimeAvailable = false;
            try
            {
                Type runtime = FindType(
                    "Baominix.DurangoOriginal.CombatSystem.Runtime.CombatRuntime");
                MethodInfo method = runtime == null
                    ? null
                    : runtime.GetMethod(
                        "SetSaurusBubbleDebug",
                        BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic);
                if (method == null)
                {
                    return;
                }
                _animalBubbleRuntimeAvailable = true;
                object[] parameters = new object[] { enabled, null };
                bool success = (bool)method.Invoke(null, parameters);
                _animalBubbleRuntimeActive = success && enabled;
            }
            catch (Exception exception)
            {
                if (Log != null)
                {
                    Log.LogWarning(
                        "Unable to apply animal bubble debug: " +
                        exception.Message);
                }
            }
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            int i;
            for (i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }

        private static void SaveConfig()
        {
            if (_instance != null)
            {
                _instance.Config.Save();
            }
        }

        private void OnDestroy()
        {
            ApplyAnimalBubbleRuntime(false);
            global::CombatSystem.AttackAlertEnabled = _originalAttackAlert;
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
            _instance = null;
            Log = null;
        }
    }
}
