using System;
using System.Reflection;
using Durango.Logic.Clusters;
using Durango.UI;
using HarmonyLib;

namespace Baominix.DurangoOriginal.CombatSystem.Presentation
{
    internal struct ClusterModePatchState
    {
        internal bool Changed;
        internal Mode OriginalMode;
    }

    internal static class OfflineCombatUiRuntime
    {
        private static readonly PropertyInfo ClusterModeProperty =
            typeof(GameManager).GetProperty(
                "ClusterMode",
                BindingFlags.Static | BindingFlags.Public);

        internal static bool SetClusterMode(Mode mode)
        {
            if (ClusterModeProperty == null)
            {
                return false;
            }

            MethodInfo setter = ClusterModeProperty.GetSetMethod(true);
            if (setter == null)
            {
                return false;
            }

            setter.Invoke(null, new object[] { mode });
            return true;
        }
    }

    // CombatGroup.Start disables its complete button/event setup in a main-scene
    // offline cluster. Temporarily expose the original online UI path while Start
    // runs, then immediately restore the real cluster mode.
    [HarmonyPatch(typeof(CombatGroup), "Start")]
    internal static class OfflineCombatGroupStartPatch
    {
        private static void Prefix(ref ClusterModePatchState __state)
        {
            __state = new ClusterModePatchState();
            if (DurangoCombatSystemPlugin.Enabled == null ||
                !DurangoCombatSystemPlugin.Enabled.Value ||
                !GameManager.IsMainScene ||
                GameManager.ClusterMode == Mode.Online)
            {
                return;
            }

            __state.OriginalMode = GameManager.ClusterMode;
            __state.Changed =
                OfflineCombatUiRuntime.SetClusterMode(Mode.Online);
        }

        private static Exception Finalizer(
            Exception __exception,
            ClusterModePatchState __state)
        {
            if (__state.Changed)
            {
                OfflineCombatUiRuntime.SetClusterMode(
                    __state.OriginalMode);
            }
            return __exception;
        }
    }

    // The original interaction callback immediately consumes slot 1 after
    // selecting a target. Offline combat first opens the combat controls and
    // lets the player choose an action explicitly.
    [HarmonyPatch]
    internal static class OfflineCombatTargetSelectionPatch
    {
        private static MethodBase TargetMethod()
        {
            return typeof(CombatGroup).GetMethod(
                "<Start>m__4",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static bool Prefix(
            CombatGroup __instance,
            InteractionObject target)
        {
            if (DurangoCombatSystemPlugin.Enabled == null ||
                !DurangoCombatSystemPlugin.Enabled.Value)
            {
                return true;
            }

            if (__instance == null || target == null)
            {
                return false;
            }

            __instance.SetBattleView(CombatGroup.BattleViewMode.Battle);
            GameSystem<global::CombatSystem>.Instance().SelectTarget(
                target.EntityId);
            return false;
        }
    }
}
