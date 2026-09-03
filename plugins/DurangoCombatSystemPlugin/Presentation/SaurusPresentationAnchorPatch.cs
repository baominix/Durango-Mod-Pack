using Baominix.DurangoOriginal.CombatSystem.SaurusAI;
using HarmonyLib;

namespace Baominix.DurangoOriginal.CombatSystem.Presentation
{
    // AnimalBehavior invokes RootMotionMovable from LateUpdate. Run after
    // that exact boundary so presentation-only children remain anchored to
    // the logical actor without changing the action plan or hit trajectory.
    [HarmonyPatch(typeof(AnimalBehavior), "LateUpdate")]
    internal static class SaurusPresentationAnchorPatch
    {
        [HarmonyPostfix]
        private static void Postfix(AnimalBehavior __instance)
        {
            SaurusMotionAdapter.StabilizePresentationBase(__instance);
        }
    }
}
