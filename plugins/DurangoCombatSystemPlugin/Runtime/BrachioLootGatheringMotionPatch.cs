using System;
using Durango.MotionInfo;
using HarmonyLib;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    [HarmonyPatch(
        typeof(MotionMap),
        "GetGatheringMotion",
        new Type[]
        {
            typeof(string),
            typeof(string),
            typeof(int),
            typeof(string)
        })]
    internal static class BrachioLootGatheringMotionPatch
    {
        private static void Postfix(
            string resource,
            int animalType,
            ref string __result)
        {
            if (BrachioLootRuntime.UsesButcherMotion(resource, animalType))
            {
                __result = "Barehand_Butcher";
            }
        }
    }
}
