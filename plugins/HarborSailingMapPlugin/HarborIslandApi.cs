using System;
using Durango.Offline;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    // Stable bridge used by the separate Tamed Island restoration plugin.
    public static class HarborIslandApi
    {
        private static Func<string> _personalTerrainProvider;

        public static void RegisterPersonalTerrainProvider(Func<string> provider)
        {
            _personalTerrainProvider = provider;
        }

        public static bool SailToTamedIsland(Player player)
        {
            if (HarborRuntime.IsAtTamedHome(player)) return false;
            if (HarborRuntime.CanReturnToTamedHome(player))
            {
                return HarborRuntime.ReturnHome(player);
            }
            SailTarget target = ResolvePersonalTarget();
            return target != null && HarborRuntime.Sail(player, target);
        }

        public static bool SailToTamedIsland(Player player, string terrainId)
        {
            SailTarget target = HarborRoutes.FindTamedTarget(terrainId);
            return target != null && HarborRuntime.Sail(player, target);
        }

        public static bool ReturnToHomeIsland(Player player)
        {
            return HarborRuntime.ReturnHome(player);
        }

        public static bool ReturnToExploring(Player player)
        {
            return HarborRuntime.ReturnToExploring(player);
        }

        public static bool CanReturnToExploring(Player player)
        {
            return HarborRuntime.CanReturnToExploring(player);
        }

        public static bool IsAtTamedHome(Player player)
        {
            return HarborRuntime.IsAtTamedHome(player);
        }

        public static bool CanReturnToTamedHome(Player player)
        {
            return HarborRuntime.CanReturnToTamedHome(player);
        }

        public static string GetHomeTamedTerrainId(Player player)
        {
            SailTarget target = HarborRuntime.GetHomeTamedTarget(player);
            return target == null ? string.Empty : target.TerrainId;
        }

        public static bool IsTamedTerrain(string terrainId)
        {
            return HarborRoutes.FindTamedTarget(terrainId) != null;
        }

        public static bool IsCurrentTamedWorld(World world)
        {
            SailTarget target = HarborRuntime.GetCurrentTarget(world);
            return target != null && target.Kind == HarborIslandKind.Tamed;
        }

        public static string GetCurrentTamedTerrainId(World world)
        {
            SailTarget target = HarborRuntime.GetCurrentTarget(world);
            return target != null && target.Kind == HarborIslandKind.Tamed
                ? target.TerrainId
                : string.Empty;
        }

        public static World GetCurrentWorld(Player player)
        {
            return HarborRuntime.GetWorld(player);
        }

        public static string TamedTerrainId
        {
            get
            {
                SailTarget target = HarborRoutes.FindFirstTamedTarget();
                return target == null ? string.Empty : target.TerrainId;
            }
        }

        public static string TamedRegionId
        {
            get
            {
                SailTarget target = HarborRoutes.FindFirstTamedTarget();
                return target == null ? string.Empty : target.RegionId;
            }
        }

        public static string GetTamedRegionId(string terrainId)
        {
            SailTarget target = HarborRoutes.FindTamedTarget(terrainId);
            return target == null ? string.Empty : target.RegionId;
        }

        public static string GetTamedRegionTemplateId(string terrainId)
        {
            SailTarget target = HarborRoutes.FindTamedTarget(terrainId);
            return target == null ? string.Empty : target.RegionTemplateId;
        }

        private static SailTarget ResolvePersonalTarget()
        {
            if (_personalTerrainProvider != null)
            {
                try
                {
                    SailTarget selected = HarborRoutes.FindTamedTarget(
                        _personalTerrainProvider());
                    if (selected != null) return selected;
                }
                catch (Exception ex)
                {
                    HarborSailingMapPlugin.Log.LogWarning(
                        "Personal terrain provider failed: " + ex.Message);
                }
            }
            return HarborRoutes.FindFirstTamedTarget();
        }
    }
}
