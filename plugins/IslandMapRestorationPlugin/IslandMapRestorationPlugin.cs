using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango.Offline;
using Durango.Terrain;
using HarmonyLib;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

namespace BaoX.DurangoOriginal.IslandMapRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class IslandMapRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baox.durango.original.islandmaprestoration";
        public const string PluginName = "Island Map Restoration Plugin (Original)";
        public const string PluginVersion = "2.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Restore missing island terrain packages used by the Harbor sailing map.");

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo(
                PluginName + " loaded. Restored terrain aliases=" +
                IslandMapCatalog.RestoredMapCount);
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }

        internal static bool IsEnabled
        {
            get { return Enabled != null && Enabled.Value; }
        }
    }

    internal sealed class RestoredIslandMap
    {
        internal readonly string TerrainId;
        internal readonly string SourceTerrainId;
        internal readonly string RegionTemplateId;

        internal RestoredIslandMap(
            string terrainId,
            string sourceTerrainId,
            string regionTemplateId)
        {
            TerrainId = terrainId;
            SourceTerrainId = sourceTerrainId;
            RegionTemplateId = regionTemplateId;
        }
    }

    internal static class IslandMapCatalog
    {
        private static readonly Dictionary<string, RestoredIslandMap> Maps =
            CreateMaps();

        internal static int RestoredMapCount
        {
            get { return Maps.Count; }
        }

        internal static bool TryGet(string terrainId, out RestoredIslandMap map)
        {
            map = null;
            return IslandMapRestorationPlugin.IsEnabled &&
                !string.IsNullOrEmpty(terrainId) &&
                Maps.TryGetValue(terrainId, out map);
        }

        private static Dictionary<string, RestoredIslandMap> CreateMaps()
        {
            RestoredIslandMap[] definitions = new RestoredIslandMap[]
            {
                // Early and mid-level island terrain packages absent from Original PC.
                Map("ri15sa", "ri45sa", "ri15sa190710"),
                Map("ri18tr", "ri40tr", "ri18tr_01_01"),
                Map("ri20te", "ri35te", "ri20te190710"),
                Map("ri25tr", "ri40tr", "ri25tr190710"),
                Map("ri30tu", "ri55tu", "ri30tu171228"),
                Map("ri40tu", "ri55tu", "ri40tu171228"),
                Map("ri45sw", "ra60sw", "ri45sw171228"),
                Map("ri50de", "ri35de", "ri50de171228"),
                Map("ri55tr", "ri40tr", "ri55tb171228"),
                Map("ri55sw", "ra60sw", "ri55sw171228"),

                // Lv.60 unstable-island families use the nearest packaged biome terrain.
                Map("ua60tu", "ri55tu", "ua60tuMain01"),
                Map("ua60sn", "ri50sn", "ua60snMain03"),
                Map("ua60sw", "ra60sw", "ua60swMain05"),
                Map("ua60de", "ri35de", "ua60deMain01"),
                Map("ua60tr", "ri40tr", "ua60trMain01"),

                // Savage-island routes shown separately from normal Lv.60 islands.
                Map("op60te", "ri35te", "op60te171228"),
                Map("op60tr", "ri40tr", "op60tr171228")
            };

            Dictionary<string, RestoredIslandMap> result =
                new Dictionary<string, RestoredIslandMap>();
            for (int i = 0; i < definitions.Length; i++)
            {
                result[definitions[i].TerrainId] = definitions[i];
            }
            return result;
        }

        private static RestoredIslandMap Map(
            string terrainId,
            string sourceTerrainId,
            string regionTemplateId)
        {
            return new RestoredIslandMap(
                terrainId,
                sourceTerrainId,
                regionTemplateId);
        }
    }

    // The Original PC client only packages one physical terrain for many biome
    // families. Missing route identities reuse that biome's packaged geometry,
    // while retaining a separate terrain/save id and the correct simulation data.
    [HarmonyPatch(typeof(TerrainLoader), "OpenZipStreamForRead")]
    internal static class RestoredTerrainStreamPatch
    {
        private static bool Prefix(string terrainId, ref ZipInputStream __result)
        {
            RestoredIslandMap map;
            if (!IslandMapCatalog.TryGet(terrainId, out map))
            {
                return true;
            }

            TextAsset source = Resources.Load(
                "offline/terrains/" + map.SourceTerrainId) as TextAsset;
            if (source == null)
            {
                IslandMapRestorationPlugin.Log.LogError(
                    "Cannot restore " + map.TerrainId +
                    ": source terrain not found: " + map.SourceTerrainId);
                __result = null;
                return false;
            }

            __result = new ZipInputStream(new MemoryStream(source.bytes));
            IslandMapRestorationPlugin.Log.LogInfo(
                "Loading restored terrain " + map.TerrainId +
                " from " + map.SourceTerrainId);
            return false;
        }
    }

    [HarmonyPatch(typeof(TerrainLoader), "Load")]
    internal static class RestoredTerrainInfoPatch
    {
        private static void Postfix(string terrainId, TerrainData __result)
        {
            RestoredIslandMap map;
            if (!IslandMapCatalog.TryGet(terrainId, out map) ||
                __result == null || __result.Info == null)
            {
                return;
            }

            string previousTemplate = __result.Info.region_template;
            __result.Info.region_template = map.RegionTemplateId;
            IslandMapRestorationPlugin.Log.LogInfo(
                "Restored " + map.TerrainId + " region template: " +
                previousTemplate + " -> " + map.RegionTemplateId);
        }
    }
}
