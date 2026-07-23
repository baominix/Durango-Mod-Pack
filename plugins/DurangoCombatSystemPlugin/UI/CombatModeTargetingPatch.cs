using System.Reflection;
using Durango.UI;
using HarmonyLib;

namespace BaoX.DurangoOriginal.CombatMode
{
    [HarmonyPatch]
    internal static class CombatGroupAttackInteractionPatch
    {
        private static MethodBase TargetMethod()
        {
            return typeof(CombatGroup).GetMethod(
                "<Start>m__4",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static bool Prefix(CombatGroup __instance, InteractionObject target)
        {
            if (__instance == null || target == null)
            {
                return false;
            }

            __instance.SetBattleView(CombatGroup.BattleViewMode.Battle);
            GameSystem<CombatSystem>.Instance().SelectTarget(target.EntityId);
            return false;
        }
    }
}
