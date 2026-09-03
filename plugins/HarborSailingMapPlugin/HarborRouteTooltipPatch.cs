using System.Reflection;
using Durango.Logic.Explore;
using Durango.UI.Popup;
using HarmonyLib;
using L10N;
using UnityEngine;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    // MajorBiome intentionally groups Savanna under Grassland for simulation.
    // The sailing tooltip should show the route's apparent climate instead.
    [HarmonyPatch(typeof(RouteInfoTooltip), "Refresh")]
    internal static class HarborRouteTooltipClimatePatch
    {
        private static readonly FieldInfo RouteField =
            AccessTools.Field(typeof(RouteInfoTooltip), "_route");
        private static readonly FieldInfo TooltipField =
            AccessTools.Field(typeof(RouteInfoTooltip), "_tooltip");

        private static void Postfix(RouteInfoTooltip __instance)
        {
            if (!HarborSailingMapPlugin.Enabled.Value || __instance == null ||
                RouteField == null || TooltipField == null)
            {
                return;
            }

            Messages.Route route = (Messages.Route)RouteField.GetValue(__instance);
            if (HarborRoutes.FindByRegionId(route.RegionId) == null) return;

            Durango.Logic.Explore.Region region = route.Region();
            if (region == null || region.Template == null ||
                Gettext.IsEmpty(region.Template.ApparentClimate))
            {
                return;
            }

            InfoTooltip tooltip = TooltipField.GetValue(__instance) as InfoTooltip;
            if (tooltip == null)
            {
                tooltip = ((Component)__instance).GetComponent<InfoTooltip>();
            }
            if (tooltip != null)
            {
                tooltip.SetSubtitle(T._((string)region.Template.ApparentClimate));
            }
        }
    }
}
