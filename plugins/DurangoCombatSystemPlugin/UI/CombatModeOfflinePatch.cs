using System;
using Durango.Logic.Clusters;
using Durango.UI;
using HarmonyLib;

namespace BaoX.DurangoOriginal.CombatMode
{
    // Kyllox removes the offline early return at the start of CombatGroup.Start().
    [HarmonyPatch(typeof(CombatGroup), "Start")]
    internal static class CombatGroupOfflinePatch
    {
        private static void Prefix(ref ClusterModePatchState __state)
        {
            __state = new ClusterModePatchState();
            if (!GameManager.IsMainScene || GameManager.ClusterMode == Mode.Online)
            {
                return;
            }

            __state.OriginalMode = GameManager.ClusterMode;
            __state.Changed = CombatModeRuntime.SetClusterMode(Mode.Online);
        }

        private static Exception Finalizer(Exception __exception, ClusterModePatchState __state)
        {
            if (__state.Changed)
            {
                CombatModeRuntime.SetClusterMode(__state.OriginalMode);
                if (CombatModePlugin.Log != null)
                {
                    CombatModePlugin.Log.LogInfo("Combat UI enabled for offline mode");
                }
            }
            return __exception;
        }
    }
}
