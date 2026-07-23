using System;
using System.Reflection;
using Durango.UI;
using Durango.UI.Control;
using HarmonyLib;
using L10N;
using Shared.Ability;
using UnityEngine;

namespace BaoX.DurangoOriginal.WeaponStatisticsMod
{
    [HarmonyPatch(typeof(CharacterAbilityWidget), "AddResistance")]
    internal static class PhysicalResistanceWidgetFix
    {
        private static readonly FieldInfo IsInitField = AccessTools.Field(typeof(CharacterAbilityWidget), "_isInit");
        private static readonly FieldInfo ScrollViewField = AccessTools.Field(typeof(CharacterAbilityWidget), "_scrollView");
        private static readonly FieldInfo TypesField = AccessTools.Field(typeof(CharacterAbilityWidget), "_types");

        private static void Postfix(CharacterAbilityWidget __instance)
        {
            if (__instance == null || IsInitField == null || ScrollViewField == null || TypesField == null)
            {
                return;
            }
            if (!(bool)IsInitField.GetValue(__instance))
            {
                return;
            }

            KScrollView scrollView = ScrollViewField.GetValue(__instance) as KScrollView;
            RepresentType[] types = TypesField.GetValue(__instance) as RepresentType[];
            if (scrollView == null || types == null || scrollView.Nodes.Count > types.Length)
            {
                return;
            }

            GameObject node = scrollView.Nodes.Add();
            node.FindComponent<UILabel>("Key").text = string.Format(
                "[icon=icon_skin_hardness:1.25]  {0}",
                T._("\uc2e0\uccb4 \uc800\ud56d \ub808\ubca8"));
            node.GetComponent<RectLayoutComponent>().UpdateOnSizeChange();
            UIEventListener listener = UIEventListener.Get(node);
            listener.onClick = (UIEventListener.VoidDelegate)Delegate.Combine(
                listener.onClick,
                new UIEventListener.VoidDelegate(delegate(GameObject go)
                {
                    CharacterInfoGroup.ShowResistanceInfoPopup();
                }));
        }
    }
}
