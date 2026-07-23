using Durango.Logic;
using HarmonyLib;

namespace BaoX.DurangoOriginal.SkillCombatBridge
{
    [HarmonyPatch(typeof(SkillSystem), "OnReceiveSkillMsg")]
    internal static class SkillSystemReceiveCombatRefreshPatch
    {
        private static void Postfix()
        {
            SkillCombatActionBuilder.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(CombatSystem), "CheckCurrentEquipments")]
    internal static class CombatEquipmentRefreshPatch
    {
        private static void Postfix()
        {
            SkillCombatActionBuilder.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(CombatSystem), "OnReady")]
    internal static class CombatReadyRefreshPatch
    {
        private static void Postfix()
        {
            SkillCombatActionBuilder.MarkDirty();
        }
    }
}
