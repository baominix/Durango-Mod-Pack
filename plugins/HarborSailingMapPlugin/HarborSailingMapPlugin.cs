using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baox.durango.original.islandmaprestoration", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class HarborSailingMapPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.harborsailingmap";
        public const string PluginName = "Harbor Sailing Map Plugin (Original)";
        public const string PluginVersion = "1.5.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> SpawnHarborEveryMap;
        internal static ConfigEntry<int> PreferredWaterSearchRadius;
        internal static ConfigEntry<bool> SearchWholeMapFallback;
        internal static ConfigEntry<int> ExistingHarborReuseRadius;
        internal static ConfigEntry<string> DockModel;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true, "Enable the restored offline Harbor sailing system.");
            SpawnHarborEveryMap = Config.Bind("Harbor Spawn", "SpawnHarborEveryMap", true, "Spawn a dock in water near the player entry point on every map.");
            PreferredWaterSearchRadius = Config.Bind("Harbor Spawn", "PreferredWaterSearchRadius", 96, "Preferred maximum distance from the entry point. The whole map can be searched as a fallback.");
            SearchWholeMapFallback = Config.Bind("Harbor Spawn", "SearchWholeMapFallback", true, "Search the whole map when no water is found inside PreferredWaterSearchRadius.");
            ExistingHarborReuseRadius = Config.Bind("Harbor Spawn", "ExistingHarborReuseRadius", 24, "Reuse an existing dock this close to the entry point instead of spawning another one.");
            DockModel = Config.Bind("Harbor Spawn", "DockModel", "dock_01_wood", "Artifact model used by the automatically spawned dock.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(PluginName + " loaded. Routes=" + HarborRoutes.Targets.Length);
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
}
