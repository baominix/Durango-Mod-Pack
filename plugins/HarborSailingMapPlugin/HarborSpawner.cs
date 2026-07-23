using System;
using System.Collections.Generic;
using Durango.Offline;
using Durango.Terrain;
using HarmonyLib;
using InteractionData;
using Messages;
using Shared.Building;
using Shared.Etc;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    internal static class HarborSpawner
    {
        internal const ushort DockEntityType = 7001;
        private const string AutoHarborPrefix = "harbor:auto:";

        public static void EnsureHarbor(World world)
        {
            if (world == null || !HarborSailingMapPlugin.Enabled.Value || !HarborSailingMapPlugin.SpawnHarborEveryMap.Value)
            {
                return;
            }

            WorldContext context = HarborRuntime.GetWorldContext(world);
            if (context == null || context.Artifacts == null)
            {
                return;
            }

            Point2 entry = world.EntryPoint;
            int reuseRadius = Math.Max(0, HarborSailingMapPlugin.ExistingHarborReuseRadius.Value);
            foreach (AppearArtifact artifact in context.Artifacts.Values)
            {
                if (artifact.EntityId != null && artifact.EntityId.StartsWith(AutoHarborPrefix, StringComparison.Ordinal))
                {
                    SetFullDurability(artifact);
                    HarborSailingMapPlugin.Log.LogInfo("Harbor already present on " + context.TerrainId + " at " + artifact.Tile);
                    return;
                }
                if (artifact.EntityType == DockEntityType && DistanceSquared(entry, artifact.Tile) <= reuseRadius * reuseRadius)
                {
                    HarborSailingMapPlugin.Log.LogInfo("Reusing existing harbor on " + context.TerrainId + " at " + artifact.Tile);
                    return;
                }
            }

            Point2 tile;
            bool fullWaterFootprint;
            if (!TryFindWater(world, entry, out tile, out fullWaterFootprint))
            {
                HarborSailingMapPlugin.Log.LogWarning("No water tile found for harbor on " + context.TerrainId + ". Harbor was not spawned.");
                return;
            }

            string entityId = AutoHarborPrefix + (string.IsNullOrEmpty(context.TerrainId) ? "unknown" : context.TerrainId);
            AppearArtifact harbor = MakeHarbor(entityId, tile);
            context.Artifacts[entityId] = harbor;
            HarborSailingMapPlugin.Log.LogInfo("Spawned harbor on " + context.TerrainId + " at " + tile + " near entry " + entry + " fullWaterFootprint=" + fullWaterFootprint);
        }

        private static AppearArtifact MakeHarbor(string entityId, Point2 tile)
        {
            Dictionary<string, string> parts = new Dictionary<string, string>();
            parts["common"] = HarborSailingMapPlugin.DockModel.Value;
            return new AppearArtifact
            {
                EntityId = entityId,
                EntityType = DockEntityType,
                IsAlive = false,
                Tile = tile,
                Size = new Point2(3, 3),
                Rotation = Rotation.Quarter,
                Display = new ArtifactDisplay { EntityId = entityId, Parts = parts },
                States = new ArtifactState
                {
                    EntityId = entityId,
                    Durability = CreateFullDurability(),
                    BuildingState = BuildingState.Completed
                }
            };
        }

        private static void SetFullDurability(AppearArtifact harbor)
        {
            ArtifactState states = harbor.States;
            if (string.IsNullOrEmpty(states.EntityId))
            {
                states.EntityId = harbor.EntityId;
            }
            if (states.Durability == null)
            {
                states.Durability = CreateFullDurability();
            }
            states.BuildingState = BuildingState.Completed;
            harbor.States = states;
        }

        private static Gauge CreateFullDurability()
        {
            return new Gauge(1f, 0f, new[] { new GaugeNode(0.0, 1f) });
        }

        private static bool TryFindWater(World world, Point2 entry, out Point2 result, out bool fullWaterFootprint)
        {
            int preferred = Math.Max(1, HarborSailingMapPlugin.PreferredWaterSearchRadius.Value);
            int maxMapRadius = Math.Max(world.NumTilesX, world.NumTilesY) * 2;
            int firstLimit = Math.Min(preferred, maxMapRadius);

            if (TryFindWaterInRadius(world, entry, firstLimit, true, out result))
            {
                fullWaterFootprint = true;
                return true;
            }
            if (TryFindWaterInRadius(world, entry, firstLimit, false, out result))
            {
                fullWaterFootprint = false;
                return true;
            }
            if (HarborSailingMapPlugin.SearchWholeMapFallback.Value && firstLimit < maxMapRadius)
            {
                if (TryFindWaterInRadius(world, entry, maxMapRadius, true, out result))
                {
                    fullWaterFootprint = true;
                    return true;
                }
                if (TryFindWaterInRadius(world, entry, maxMapRadius, false, out result))
                {
                    fullWaterFootprint = false;
                    return true;
                }
            }
            result = entry;
            fullWaterFootprint = false;
            return false;
        }

        private static bool TryFindWaterInRadius(World world, Point2 center, int maxRadius, bool requireFootprint, out Point2 result)
        {
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                int minX = center.x - radius;
                int maxX = center.x + radius;
                int minY = center.y - radius;
                int maxY = center.y + radius;

                for (int x = minX; x <= maxX; x++)
                {
                    if (IsValidWaterCandidate(world, x, minY, requireFootprint)) { result = new Point2(x, minY); return true; }
                    if (maxY != minY && IsValidWaterCandidate(world, x, maxY, requireFootprint)) { result = new Point2(x, maxY); return true; }
                }
                for (int y = minY + 1; y < maxY; y++)
                {
                    if (IsValidWaterCandidate(world, minX, y, requireFootprint)) { result = new Point2(minX, y); return true; }
                    if (maxX != minX && IsValidWaterCandidate(world, maxX, y, requireFootprint)) { result = new Point2(maxX, y); return true; }
                }
            }
            result = center;
            return false;
        }

        private static bool IsValidWaterCandidate(World world, int x, int y, bool requireFootprint)
        {
            if (!IsWater(world, x, y))
            {
                return false;
            }
            if (!requireFootprint)
            {
                return true;
            }
            for (int offsetY = 0; offsetY < 3; offsetY++)
            {
                for (int offsetX = 0; offsetX < 3; offsetX++)
                {
                    if (!IsWater(world, x + offsetX, y + offsetY))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsWater(World world, int x, int y)
        {
            if (x < 0 || y < 0 || x >= world.NumTilesX || y >= world.NumTilesY)
            {
                return false;
            }
            int index = x + y * world.NumTilesX;
            byte[] biomes = world.Biomes;
            if (biomes == null || index < 0 || index >= biomes.Length)
            {
                return false;
            }
            return Durango.Terrain.Util.IsWater(Durango.Terrain.Util.GetUnmaskedBiome(biomes[index]));
        }

        private static int DistanceSquared(Point2 a, Point2 b)
        {
            int x = a.x - b.x;
            int y = a.y - b.y;
            return x * x + y * y;
        }
    }

    [HarmonyPatch(typeof(World), MethodType.Constructor, new Type[] { typeof(WorldContext) })]
    internal static class WorldHarborSpawnPatch
    {
        private static void Postfix(World __instance)
        {
            try
            {
                HarborSpawner.EnsureHarbor(__instance);
            }
            catch (Exception ex)
            {
                HarborSailingMapPlugin.Log.LogError("Harbor spawn failed: " + ex);
            }
        }
    }

    // Original's offline backend never adds the Port interaction. Handle dock touches here.
    [HarmonyPatch(typeof(Durango.Offline.Player), "HandleTouchMsg")]
    internal static class HarborTouchPatch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Durango.Offline.Player __instance, Messages.Touch touch, uint seq)
        {
            if (!HarborSailingMapPlugin.Enabled.Value || touch.EntityType != HarborSpawner.DockEntityType)
            {
                return true;
            }
            Touched touched = new Touched
            {
                EntityId = touch.EntityId,
                EntityName = "Harbor",
                Interactions = new int[] { (int)Interaction.SailingExplore, (int)Interaction.SailingRoutes },
                DisabledInteractions = new int[0],
                AccessDeniedInteractions = new int[0]
            };
            __instance.Send<Touched>(touched, seq);
            return false;
        }
    }
}
