using System.Reflection;
using BaoX.DurangoOriginal.HarborSailingMap;
using Durango.Offline;
using HarmonyLib;
using Messages;
using Shared.Region;

namespace BaoX.DurangoOriginal.TamedIslandRestoration
{
    // The stock offline server identifies every world as Region "1" / Rural.
    // Personal-island UI and building rules rely on the Welcome region instead
    // of the loaded terrain, so Tamed terrain needs a truthful Welcome packet.
    [HarmonyPatch(typeof(GameServer), "SendWelcome")]
    internal static class TamedWelcomePatch
    {
        private static readonly MethodInfo GetPlayerContextMethod = typeof(GameServer).GetMethod(
            "GetPlayerContext", BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool Prefix(GameServer __instance, Connection connection,
            string entityId, string name, uint seq)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value || __instance == null ||
                __instance.World == null) return true;

            string terrainId = __instance.World.TerrainInfo.region_template;
            if (!HarborIslandApi.IsTamedTerrain(terrainId)) return true;

            PlayerContext playerContext = GetPlayerContextMethod == null
                ? null
                : GetPlayerContextMethod.Invoke(__instance, new object[] { entityId }) as PlayerContext;
            if (playerContext == null) return true;

            string regionId = HarborIslandApi.GetTamedRegionId(terrainId);
            Welcome welcome = default(Welcome);
            welcome.UserId = entityId;
            welcome.Name = name;
            welcome.Storage.Data = playerContext.Storage;
            welcome.Region.CreatedAt = 0.0;
            welcome.Region.Id = regionId;
            welcome.Region.Name = TamedIslandRestorationPlugin.IslandName.Value;
            welcome.Region.TemplateId = terrainId;
            // Offline Gateway only serves /terrains/1, so this must remain "1".
            welcome.Region.TerrainId = "1";
            welcome.Region.Role = Role.Personal;
            welcome.PersonalRegionId = regionId;
            welcome.Options.Bool = new[]
            {
                new BoolOption { Key = "market.ui_enabled", Value = true }
            };
            welcome.Options.Int = new[]
            {
                new IntegerOption { Key = "market.search.limit", Value = 20L }
            };

            connection.Send<Welcome>(welcome, seq);
            TamedIslandRestorationPlugin.Log.LogInfo(
                "Sent Personal Welcome region: id=" + regionId + ", terrain=" + terrainId);
            return false;
        }
    }
}
