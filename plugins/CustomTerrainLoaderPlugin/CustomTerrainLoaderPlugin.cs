using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Offline;
using HarmonyLib;
using ICSharpCode.SharpZipLib.Zip;

namespace BaoX.DurangoOriginal.CustomTerrainLoader
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class CustomTerrainLoaderPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.customterrainloader";
        public const string PluginName = "Custom Terrain Loader Plugin";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<string> ForceTerrainId;
        internal static ConfigEntry<bool> ForceExistingWorlds;
        internal static string TerrainDir;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            TerrainDir = Path.Combine(Paths.BepInExRootPath, "custom-terrains");
            Directory.CreateDirectory(TerrainDir);

            Enabled = Config.Bind("General", "Enabled", true, "Load custom terrain files from BepInEx/custom-terrains.");
            ForceTerrainId = Config.Bind("General", "ForceTerrainId", "baox_test_1", "Terrain id loaded from <ForceTerrainId>.bytes.");
            ForceExistingWorlds = Config.Bind("General", "ForceExistingWorlds", true, "Also override saved offline worlds for quick testing.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo(PluginName + " loaded. TerrainDir=" + TerrainDir + " ForceTerrainId=" + ForceTerrainId.Value);
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }

        internal static string GetTerrainPath(string terrainId)
        {
            if (string.IsNullOrEmpty(terrainId))
            {
                return null;
            }

            string clean = terrainId.Replace("/", string.Empty).Replace("\\", string.Empty).Replace("..", string.Empty);
            return Path.Combine(TerrainDir, clean + ".bytes");
        }

        internal static bool HasTerrain(string terrainId)
        {
            string path = GetTerrainPath(terrainId);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }
    }

    [HarmonyPatch(typeof(World), MethodType.Constructor, new Type[] { typeof(WorldContext) })]
    internal static class WorldCtorPatch
    {
        private static void Prefix(WorldContext context)
        {
            if (context == null || !CustomTerrainLoaderPlugin.Enabled.Value || !CustomTerrainLoaderPlugin.ForceExistingWorlds.Value)
            {
                return;
            }

            string terrainId = CustomTerrainLoaderPlugin.ForceTerrainId.Value;
            if (!CustomTerrainLoaderPlugin.HasTerrain(terrainId))
            {
                CustomTerrainLoaderPlugin.Log.LogWarning("Custom terrain not found: " + CustomTerrainLoaderPlugin.GetTerrainPath(terrainId));
                return;
            }

            string oldTerrainId = context.TerrainId;
            context.TerrainId = terrainId;
            CustomTerrainLoaderPlugin.Log.LogInfo("World terrain forced: " + oldTerrainId + " -> " + terrainId);
        }
    }

    [HarmonyPatch(typeof(TerrainLoader), "OpenZipStreamForRead")]
    internal static class TerrainLoaderOpenZipStreamForReadPatch
    {
        private static bool Prefix(string terrainId, ref ZipInputStream __result)
        {
            if (!CustomTerrainLoaderPlugin.Enabled.Value)
            {
                return true;
            }

            string path = CustomTerrainLoaderPlugin.GetTerrainPath(terrainId);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return true;
            }

            __result = new ZipInputStream(File.OpenRead(path));
            CustomTerrainLoaderPlugin.Log.LogInfo("Loading custom terrain: " + terrainId + " from " + path);
            return false;
        }
    }
}
