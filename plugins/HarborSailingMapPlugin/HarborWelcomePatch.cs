using System.Reflection;
using Durango.Offline;
using Durango.UI;
using HarmonyLib;
using L10N;
using Messages;
using Shared.Region;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    // The stock offline server reports every world as region "1" / Rural and
    // leaves its name empty. Supply truthful metadata for restored unstable
    // routes so the world-map header can distinguish them from Tamed Islands.
    [HarmonyPatch(typeof(GameServer), "SendWelcome")]
    internal static class HarborUnstableWelcomePatch
    {
        private static readonly MethodInfo GetPlayerContextMethod =
            typeof(GameServer).GetMethod(
                "GetPlayerContext",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static bool Prefix(
            GameServer __instance,
            Connection connection,
            string entityId,
            string name,
            uint seq)
        {
            if (!HarborSailingMapPlugin.Enabled.Value ||
                __instance == null ||
                __instance.World == null)
            {
                return true;
            }

            SailTarget target = HarborRoutes.FindForWorld(__instance.World);
            if (target == null || target.Kind != HarborIslandKind.Unstable)
            {
                return true;
            }

            PlayerContext playerContext = GetPlayerContextMethod == null
                ? null
                : GetPlayerContextMethod.Invoke(
                    __instance,
                    new object[] { entityId }) as PlayerContext;
            if (playerContext == null)
            {
                return true;
            }

            Welcome welcome = default(Welcome);
            welcome.UserId = entityId;
            welcome.Name = name;
            welcome.Storage.Data = playerContext.Storage;
            welcome.Region.CreatedAt = 0.0;
            welcome.Region.Id = target.RegionId;
            welcome.Region.Name = target.Name;
            welcome.Region.TemplateId = target.RegionTemplateId;
            // Offline Gateway only serves /terrains/1.
            welcome.Region.TerrainId = "1";
            welcome.Region.Role = target.Role;
            welcome.Options.Bool = new[]
            {
                new BoolOption { Key = "market.ui_enabled", Value = true }
            };
            welcome.Options.Int = new[]
            {
                new IntegerOption { Key = "market.search.limit", Value = 20L }
            };

            connection.Send<Welcome>(welcome, seq);
            HarborSailingMapPlugin.Log.LogInfo(
                "Sent Unstable Welcome region: id=" + target.RegionId +
                ", template=" + target.RegionTemplateId +
                ", name=" + target.Name);
            return false;
        }
    }

    [HarmonyPatch(typeof(WorldMapEnvWidget), "UpdateRegion")]
    internal static class HarborWorldMapRegionHeaderPatch
    {
        private static readonly FieldInfo LevelLabelField =
            AccessTools.Field(typeof(WorldMapEnvWidget), "_levelLabel");
        private static readonly FieldInfo NameLabelField =
            AccessTools.Field(typeof(WorldMapEnvWidget), "_nameLabel");

        private static void Postfix(WorldMapEnvWidget __instance)
        {
            if (__instance == null || GameManager.Region == null)
            {
                return;
            }

            SailTarget target = HarborRoutes.FindByRegionId(
                GameManager.Region.Id);
            if (target == null || target.Kind != HarborIslandKind.Unstable)
            {
                return;
            }

            UILabel levelLabel = LevelLabelField == null
                ? null
                : LevelLabelField.GetValue(__instance) as UILabel;
            UILabel nameLabel = NameLabelField == null
                ? null
                : NameLabelField.GetValue(__instance) as UILabel;

            if (levelLabel != null)
            {
                string apparentClimate = GameManager.Region.Template == null
                    ? target.SeaName
                    : (string)GameManager.Region.Template.ApparentClimate;
                string localizedSeaAndLevel = T._(
                    "불안정 {0} 해역\n[size=24]{1:lv:}[/size]",
                    new object[]
                    {
                        apparentClimate,
                        target.Level
                    });
                int lineBreak = localizedSeaAndLevel.IndexOf('\n');
                string regionType = lineBreak >= 0
                    ? localizedSeaAndLevel.Substring(0, lineBreak)
                    : localizedSeaAndLevel;
                int sizeTag = regionType.IndexOf("[size=", System.StringComparison.Ordinal);
                if (sizeTag >= 0)
                {
                    regionType = regionType.Substring(0, sizeTag).TrimEnd();
                }
                levelLabel.text = T._("{0:lv:} {1}", new object[]
                {
                    target.Level,
                    regionType
                });
            }
            if (nameLabel != null)
            {
                nameLabel.text = target.Name;
            }
        }
    }
}
