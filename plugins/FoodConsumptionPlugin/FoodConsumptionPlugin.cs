using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.FoodConsumptionMod
{
    [BepInPlugin("com.baominix.durango.original.foodconsumption", "FoodConsumptionPlugin", "0.2.1")]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class FoodConsumptionPlugin : BaseUnityPlugin
    {
        private sealed class ScheduledAction
        {
            internal float DueAt;
            internal Action Action;
            internal bool Cancelled;
        }

        internal static ManualLogSource Log;
        private static readonly object ScheduledSync = new object();
        private static readonly List<ScheduledAction> ScheduledActions = new List<ScheduledAction>();
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            FoodDatabase.Load();

            _harmony = new Harmony("com.baominix.durango.original.foodconsumption");
            Type playerType = AccessTools.TypeByName("Durango.Offline.Player");
            if (playerType == null)
            {
                Logger.LogError("Durango.Offline.Player class not found.");
                return;
            }

            ConstructorInfo ctor = playerType.GetConstructor(new Type[]
            {
                typeof(string),
                typeof(Durango.Offline.Connection),
                typeof(Durango.Offline.World),
                typeof(Durango.Offline.PlayerContext),
                typeof(bool)
            });
            if (ctor == null)
            {
                Logger.LogError("Durango.Offline.Player constructor not found.");
                return;
            }

            _harmony.Patch(ctor, null,
                new HarmonyMethod(typeof(FoodBackend).GetMethod("ConstructorPostfix")),
                null, null, null);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("FoodConsumptionPlugin 0.2.1 enabled (timed/cancellable consumption).");
        }

        internal static object Schedule(float delaySeconds, Action action)
        {
            if (action == null) return null;
            ScheduledAction scheduled = new ScheduledAction
            {
                DueAt = Time.realtimeSinceStartup + Math.Max(0f, delaySeconds),
                Action = action
            };
            lock (ScheduledSync) ScheduledActions.Add(scheduled);
            return scheduled;
        }

        internal static void CancelScheduled(object token)
        {
            ScheduledAction scheduled = token as ScheduledAction;
            if (scheduled == null) return;
            lock (ScheduledSync)
            {
                scheduled.Cancelled = true;
                ScheduledActions.Remove(scheduled);
            }
        }

        private void Update()
        {
            List<Action> due = null;
            float now = Time.realtimeSinceStartup;
            lock (ScheduledSync)
            {
                for (int i = ScheduledActions.Count - 1; i >= 0; i--)
                {
                    ScheduledAction scheduled = ScheduledActions[i];
                    if (scheduled.Cancelled)
                    {
                        ScheduledActions.RemoveAt(i);
                    }
                    else if (scheduled.DueAt <= now)
                    {
                        if (due == null) due = new List<Action>();
                        due.Add(scheduled.Action);
                        ScheduledActions.RemoveAt(i);
                    }
                }
            }

            if (due == null) return;
            for (int i = due.Count - 1; i >= 0; i--)
            {
                try { due[i](); }
                catch (Exception ex) { Log.LogError("Food completion failed: " + ex); }
            }
        }

        private void OnDestroy()
        {
            FoodBackend.CancelAll();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }
}
