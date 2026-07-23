using System;
using System.Reflection;
using Durango.UI;
using HarmonyLib;
using L10N;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatMode
{
    [HarmonyPatch]
    internal static class BattleLeaveButtonClickPatch
    {
        private static MethodBase TargetMethod()
        {
            Type closure = typeof(BattleActionButtons).GetNestedType(
                "<Start>c__AnonStorey0",
                BindingFlags.NonPublic);
            return closure == null ? null : closure.GetMethod(
                "<>m__10",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(GameObject) },
                null);
        }

        private static bool Prefix(object __instance)
        {
            FieldInfo ownerField = AccessTools.Field(__instance.GetType(), "$this");
            BattleActionButtons owner = ownerField == null ? null : ownerField.GetValue(__instance) as BattleActionButtons;

            UISound.PlayClick(UISound.ClickType.ActionButtonDefault);
            if (owner != null)
            {
                FieldInfo eventField = AccessTools.Field(typeof(BattleActionButtons), "BattleLeaveClicked");
                Action handler = eventField == null ? null : eventField.GetValue(owner) as Action;
                if (handler != null)
                {
                    handler();
                }
            }
            return false;
        }
    }

    [HarmonyPatch]
    internal static class CombatGroupBattleLeaveHandlerPatch
    {
        private static MethodBase TargetMethod()
        {
            return typeof(CombatGroup).GetMethod(
                "<Start>m__2",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static bool Prefix(CombatGroup __instance)
        {
            FieldInfo field = AccessTools.Field(typeof(CombatGroup), "_actionButtons");
            BattleActionButtons buttons = field == null ? null : field.GetValue(__instance) as BattleActionButtons;
            if (buttons == null || buttons.enabled)
            {
                return true;
            }

            UIManager.SystemMsg(
                "BattleAction",
                T._("일정 시간 피해를 받지 않으면 전투에서 벗어날 수 있습니다."),
                3f);
            return false;
        }
    }
}
