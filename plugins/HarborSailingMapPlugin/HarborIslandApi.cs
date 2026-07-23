using Durango.Offline;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    // Stable bridge used by the separate Tamed Island restoration plugin.
    public static class HarborIslandApi
    {
        public static bool SailToTamedIsland(Player player)
        {
            SailTarget target = HarborRoutes.FindFirstTamedTarget();
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

        public static bool IsTamedTerrain(string terrainId)
        {
            return HarborRoutes.FindTamedTarget(terrainId) != null;
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
    }
}
