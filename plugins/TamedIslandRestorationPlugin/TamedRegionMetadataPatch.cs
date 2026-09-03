using System.Reflection;
using Durango.UI;
using HarmonyLib;
using L10N;
using Messages;
using Shared.Region;

namespace BaoX.DurangoOriginal.TamedIslandRestoration
{
    // Personal-island level is a property of the region role, not of whichever
    // physical terrain layout was selected. Keep it at Lv.10 for every
    // personal layout without replacing the terrain's simulation template.
    [HarmonyPatch(typeof(Durango.Logic.Explore.Region), "get_Level")]
    internal static class TamedRegionLevelPatch
    {
        private static readonly FieldInfo RoleField =
            AccessTools.Field(typeof(Durango.Logic.Explore.Region), "_role");

        private static void Postfix(
            Durango.Logic.Explore.Region __instance,
            ref int __result)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value ||
                __instance == null ||
                RoleField == null)
            {
                return;
            }

            Role role = (Role)RoleField.GetValue(__instance);
            if (role == Role.Personal)
            {
                __result = 10;
            }
        }
    }

    // Reused exploration templates carry a finite unstable-island lifetime.
    // A Personal region never expires, even when it reuses that terrain data.
    [HarmonyPatch(typeof(Durango.Logic.Explore.Region), "get_DestroyAt")]
    internal static class TamedRegionDestroyAtPatch
    {
        private static readonly FieldInfo RoleField =
            AccessTools.Field(typeof(Durango.Logic.Explore.Region), "_role");

        private static void Postfix(
            Durango.Logic.Explore.Region __instance,
            ref double __result)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value ||
                __instance == null || RoleField == null)
            {
                return;
            }

            if ((Role)RoleField.GetValue(__instance) == Role.Personal)
            {
                __result = 0.0;
            }
        }
    }

    // ExplorePersonalNode reads RegionTemplate.Level directly instead of the
    // logical Region.Level getter. Keep every restored Personal layout at Lv.10.
    [HarmonyPatch(typeof(ExplorePersonalNode), "Set")]
    internal static class TamedPersonalNodeLevelPatch
    {
        private static readonly FieldInfo NameLabelField =
            AccessTools.Field(typeof(ExploreNode<ExplorePersonalNode>), "_nameLabel");

        private static void Postfix(ExplorePersonalNode __instance, Messages.Region region)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value ||
                __instance == null || region.Role != Role.Personal ||
                NameLabelField == null)
            {
                return;
            }

            UILabel label = NameLabelField.GetValue(__instance) as UILabel;
            if (label != null)
            {
                label.text = T._("{0}의 섬", region.Name) + "\n" +
                    LocalizeUtil.FormatLevel(10);
            }
        }
    }
}
