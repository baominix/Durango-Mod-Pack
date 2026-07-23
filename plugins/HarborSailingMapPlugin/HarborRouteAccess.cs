using HarmonyLib;
using Messages;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    // Harbor routes are restored offline routes and do not use the live-service
    // Tamed Island progression. Bypass only the Pioneer-grade part of the
    // condition check; resistance, unstable-factor and quest checks stay intact.
    [HarmonyPatch(typeof(ArchipelagoRouteExtension), "IsPioneerGradeSatisfied")]
    internal static class HarborPioneerRequirementPatch
    {
        private static void Postfix(ArchipelagoRoute archipelagoRoute, ref bool __result)
        {
            if (!string.IsNullOrEmpty(archipelagoRoute.ArchipelagoId) &&
                archipelagoRoute.ArchipelagoId.StartsWith("harbor_arch|"))
            {
                __result = true;
            }
        }
    }
}
