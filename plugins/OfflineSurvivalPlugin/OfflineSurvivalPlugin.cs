using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.OfflineSurvivalMod
{
    /// <summary>
    /// BepInEx plugin that patches the local <c>Durango.Offline.Player</c> server so the
    /// survival-related interactions (drink water, wash body, draw water) and the
    /// revive / resurrect pipeline (Revive, Resurrect, ResurrectPet, ReviveImmediately)
    /// actually work in offline mode.
    ///
    /// Without this patch, the offline Player ctor only registers handlers for a subset
    /// of messages; the rest (DrinkWater / WashBody / DrawWater / Revive / ...) fall on
    /// the floor and the client side PredictTimer never receives a real duration, so
    /// the progress bar / motion never finishes.
    ///
    /// Architecture (mirrors FoodConsumptionPlugin):
    ///   * Constructor postfix on <c>Durango.Offline.Player</c> registers every
    ///     <c>connection.Recv&lt;T&gt;(handler)</c> we need.
    ///   * Each handler responds with a <c>Messages.Timer</c> so the client plays the
    ///     correct motion + progress bar.
    ///   * For Revive-style flows, a delayed callback (processed in <see cref="Update"/>)
    ///     broadcasts <c>Messages.EntityRevived</c> after the timer fires, which makes
    ///     <c>CharacterBehavior.OnRevive()</c> run on the client.
    /// </summary>
    [BepInPlugin("com.baominix.durango.original.offlinesurvival", "OfflineSurvivalPlugin", "0.2.0")]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class OfflineSurvivalPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private static readonly object ScheduledSync = new object();
        private static readonly List<ScheduledAction> ScheduledActions = new List<ScheduledAction>();

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            Type playerType = AccessTools.TypeByName("Durango.Offline.Player");
            if (playerType == null)
            {
                Logger.LogError("Durango.Offline.Player class not found — offline survival patch disabled.");
                return;
            }

            // Durango.Offline.Player ctor signature:
            //   Player(string entityId, Connection connection, World world, PlayerContext context, bool isLocalPlayer)
            ConstructorInfo ctor = playerType.GetConstructor(new Type[]
            {
                typeof(string),
                AccessTools.TypeByName("Durango.Offline.Connection"),
                AccessTools.TypeByName("Durango.Offline.World"),
                AccessTools.TypeByName("Durango.Offline.PlayerContext"),
                typeof(bool)
            });
            if (ctor == null)
            {
                Logger.LogError("Durango.Offline.Player ctor not found — offline survival patch disabled.");
                return;
            }

            _harmony = new Harmony("com.baominix.durango.original.offlinesurvival");
            _harmony.Patch(ctor, null,
                new HarmonyMethod(typeof(OfflineSurvivalBackend).GetMethod("ConstructorPostfix")),
                null, null, null);
            // Apply [HarmonyPatch]-attributed patches in this assembly
            // (e.g. OfflineSurvivalClientPatches.BiomeContextActionPatch).
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.LogInfo("OfflineSurvivalPlugin 0.2.0 enabled — server handlers: DrinkWater / WashBody / DrawWater / Revive / Resurrect / ResurrectPet / ReviveImmediately. Client patch: BiomeContextAction (offline icons).");
        }

        private void OnDestroy()
        {
            lock (ScheduledSync) ScheduledActions.Clear();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
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
                catch (Exception ex) { Log.LogError("Scheduled survival action failed: " + ex); }
            }
        }

        // -------- internal scheduling helpers (used by OfflineSurvivalBackend) --------

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

        private sealed class ScheduledAction
        {
            internal float DueAt;
            internal Action Action;
            internal bool Cancelled;
        }
    }
}
