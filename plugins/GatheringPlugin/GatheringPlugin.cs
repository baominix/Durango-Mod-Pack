using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Offline;
using Durango.Terrain;
using HarmonyLib;
using Messages;
using Shared.Item;
using Durango.Network;

namespace BaoX.DurangoOriginal.GatheringMod
{
    [BepInPlugin("com.baominix.durango.original.gathering", "Gathering Plugin (Original)", "0.4.38")]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class GatheringPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony("com.baominix.durango.original.gathering");

            Type playerType = AccessTools.TypeByName("Durango.Offline.Player");
            if (playerType == null)
            {
                Logger.LogError("Durango.Offline.Player class not found!");
                return;
            }

            MethodInfo interactionMenuSetMethod =
                typeof(Durango.UI.InteractionMenuWidgetBase).GetMethod(
                    "Set",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                        typeof(InteractionData.InteractionMenuData),
                        typeof(InteractionObject)
                    },
                    null);
            if (interactionMenuSetMethod != null)
            {
                _harmony.Patch(
                    interactionMenuSetMethod,
                    new HarmonyMethod(
                        typeof(ClientPatches).GetMethod(
                            "InteractionMenuWidgetSetPrefix")),
                    new HarmonyMethod(
                        typeof(ClientPatches).GetMethod(
                            "InteractionMenuWidgetSetPostfix")),
                    null,
                    null,
                    null);
                Logger.LogInfo(
                    "Interaction menu mobile-style no-tool warnings patched.");
            }
            else
            {
                Logger.LogError(
                    "InteractionMenuWidgetBase.Set not found.");
            }

            bool hasHandleTouchNatural = playerType.GetMethod("HandleTouchNatural", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
            if (hasHandleTouchNatural)
            {
                Logger.LogInfo("Detecting Kyllox client. Registering Kyllox patches.");

                var generatorMethod = playerType.GetMethod("Generator", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (generatorMethod != null)
                {
                    _harmony.Patch(generatorMethod, null, new HarmonyMethod(typeof(KylloxPatches).GetMethod("GeneratorPostfix")), null, null, null);
                }

                var handleTouchNaturalMethod = playerType.GetMethod("HandleTouchNatural", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (handleTouchNaturalMethod != null)
                {
                    _harmony.Patch(handleTouchNaturalMethod, null, new HarmonyMethod(typeof(KylloxPatches).GetMethod("HandleTouchNaturalPostfix")), null, null, null);
                }

                var collectNaturalMethod = playerType.GetMethod("CollectNatural", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (collectNaturalMethod != null)
                {
                    _harmony.Patch(collectNaturalMethod, new HarmonyMethod(typeof(KylloxPatches).GetMethod("CollectNaturalPrefix")), null, null, null, null);
                }

                var sendCollectedMethod = playerType.GetMethod("SendCollected", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (sendCollectedMethod != null)
                {
                    _harmony.Patch(sendCollectedMethod, null, new HarmonyMethod(typeof(KylloxPatches).GetMethod("SendCollectedPostfix")), null, null, null);
                }
            }
            else
            {
                Logger.LogInfo("Detecting Original client. Registering Original patches.");

                var ctor = playerType.GetConstructor(new Type[] {
                    typeof(string),
                    typeof(Durango.Offline.Connection),
                    typeof(Durango.Offline.World),
                    typeof(Durango.Offline.PlayerContext),
                    typeof(bool)
                });
                if (ctor != null)
                {
                    _harmony.Patch(ctor, null, new HarmonyMethod(typeof(OriginalPatches).GetMethod("ConstructorPostfix")), null, null, null);
                }
                else
                {
                    Logger.LogError("Player constructor not found!");
                }

                var processMethod = playerType.GetMethod(
                    "Process",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                if (processMethod != null)
                {
                    _harmony.Patch(
                        processMethod,
                        null,
                        new HarmonyMethod(
                            typeof(OriginalPatches).GetMethod(
                                "PlayerProcessPostfix")),
                        null,
                        null,
                        null);
                    Logger.LogInfo(
                        "Gathering outbound messages queued on Player.Process.");
                }
                else
                {
                    Logger.LogError("Player.Process method not found!");
                }

                var handleTouchMsgMethod = playerType.GetMethod("HandleTouchMsg", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (handleTouchMsgMethod != null)
                {
                    _harmony.Patch(handleTouchMsgMethod, new HarmonyMethod(typeof(OriginalPatches).GetMethod("HandleTouchMsgPrefix")), null, null, null, null);
                }
                else
                {
                    Logger.LogError("HandleTouchMsg method not found!");
                }

                MethodInfo sendTouchMsgMethod =
                    typeof(InteractionSystem).GetMethod(
                        "SendTouchMsg",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                if (sendTouchMsgMethod != null)
                {
                    _harmony.Patch(
                        sendTouchMsgMethod,
                        null,
                        new HarmonyMethod(
                            typeof(ClientPatches).GetMethod(
                                "SendTouchMsgPostfix")),
                        null,
                        null,
                        null);
                    Logger.LogInfo(
                        "InteractionSystem loading ring patched for delayed natural touches.");
                }
                else
                {
                    Logger.LogError(
                        "InteractionSystem.SendTouchMsg not found!");
                }

                Type gatheringSystemType = AccessTools.TypeByName("GatheringSystem");
                if (gatheringSystemType != null)
                {
                    var onCollectedMethod = gatheringSystemType.GetMethod("OnCollected", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (onCollectedMethod != null)
                    {
                        _harmony.Patch(onCollectedMethod,
                            new HarmonyMethod(typeof(ClientPatches).GetMethod("OnCollectedPrefix")),
                            new HarmonyMethod(typeof(ClientPatches).GetMethod("OnCollectedPostfix")),
                            null, null, null);
                        Logger.LogInfo("GatheringSystem.OnCollected patched.");
                    }
                    else
                    {
                        Logger.LogError("GatheringSystem.OnCollected not found!");
                    }

                    var onGatheringTimerMethod =
                        gatheringSystemType.GetMethod(
                            "OnGatheringTimer",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic |
                            BindingFlags.Public);
                    if (onGatheringTimerMethod != null)
                    {
                        _harmony.Patch(
                            onGatheringTimerMethod,
                            null,
                            new HarmonyMethod(
                                typeof(ClientPatches).GetMethod(
                                    "OnGatheringTimerPostfix")),
                            null,
                            null,
                            null);
                        Logger.LogInfo(
                            "Gathering actual-duration cache patched.");
                    }
                    else
                    {
                        Logger.LogError(
                            "GatheringSystem.OnGatheringTimer not found!");
                    }

                    MethodInfo findBestToolMethod =
                        typeof(InteractionData.GatheringData).GetMethod(
                            "FindBestTool",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic);
                    if (findBestToolMethod != null)
                    {
                        _harmony.Patch(
                            findBestToolMethod,
                            null,
                            new HarmonyMethod(
                                typeof(ClientPatches).GetMethod(
                                    "FindBestToolPostfix")),
                            null,
                            null,
                            null);
                    }
                    else
                    {
                        Logger.LogError(
                            "GatheringData.FindBestTool not found!");
                    }

                    var prop = gatheringSystemType.GetProperty("CurrentGatheringData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (prop != null)
                    {
                        var setter = prop.GetSetMethod(true);
                        if (setter != null)
                        {
                            _harmony.Patch(setter,
                                new HarmonyMethod(typeof(ClientPatches).GetMethod("set_CurrentGatheringDataPrefix")),
                                null, null, null, null);
                            Logger.LogInfo("GatheringSystem.CurrentGatheringData setter patched.");
                        }
                        else
                        {
                            Logger.LogError("GatheringSystem.CurrentGatheringData setter not found!");
                        }
                    }
                    else
                    {
                        Logger.LogError("GatheringSystem.CurrentGatheringData property not found!");
                    }

                    Type gatheringDataType =
                        AccessTools.TypeByName(
                            "InteractionData.GatheringData");
                    MethodInfo gatheringMethod =
                        gatheringDataType == null
                            ? null
                            : gatheringSystemType.GetMethod(
                                "Gathering",
                                BindingFlags.Instance |
                                BindingFlags.NonPublic,
                                null,
                                new Type[] { gatheringDataType },
                                null);
                    if (gatheringMethod != null)
                    {
                        _harmony.Patch(
                            gatheringMethod,
                            new HarmonyMethod(
                                typeof(ClientPatches).GetMethod(
                                    "GatheringPrefix")),
                            new HarmonyMethod(
                                typeof(ClientPatches).GetMethod(
                                    "GatheringPostfix")),
                            null,
                            null,
                            null);
                        Logger.LogInfo(
                            "GatheringSystem.Gathering skill precheck patched.");
                    }
                    else
                    {
                        Logger.LogError(
                            "GatheringSystem.Gathering(GatheringData) not found!");
                    }

                    MethodInfo lockConfirmMethod =
                        typeof(Durango.UI.MessageBox).GetMethod(
                            "ShowLockConfirm",
                            BindingFlags.Instance |
                            BindingFlags.Public |
                            BindingFlags.NonPublic,
                            null,
                            new Type[]
                            {
                                typeof(Durango.Logic.Item.ItemData),
                                typeof(Action)
                            },
                            null);
                    if (lockConfirmMethod != null)
                    {
                        _harmony.Patch(
                            lockConfirmMethod,
                            new HarmonyMethod(
                                typeof(ClientPatches).GetMethod(
                                    "ShowLockConfirmPrefix")),
                            null,
                            null,
                            null,
                            null);
                        Logger.LogInfo(
                            "Gathering locked-tool confirmation batching patched.");
                    }
                    else
                    {
                        Logger.LogError(
                            "MessageBox.ShowLockConfirm(ItemData, Action) not found!");
                    }
                }
                else
                {
                    Logger.LogError("GatheringSystem type not found.");
                }
            }

            MethodInfo worldSaveMethod = typeof(Durango.Offline.World).GetMethod(
                "Save",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (worldSaveMethod != null)
            {
                _harmony.Patch(
                    worldSaveMethod,
                    null,
                    new HarmonyMethod(
                        typeof(WorldPersistencePatches).GetMethod(
                            "WorldSavePostfix")),
                    null,
                    null,
                    null);
            }

            MethodInfo endServerMethod = typeof(Durango.Offline.Server).GetMethod(
                "EndServer",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (endServerMethod != null)
            {
                _harmony.Patch(
                    endServerMethod,
                    new HarmonyMethod(
                        typeof(WorldPersistencePatches).GetMethod(
                            "ServerEndServerPrefix")),
                    null,
                    null,
                    null,
                    null);
            }

            Logger.LogInfo("Gathering Plugin 0.4.38 loaded. Character/category/world and partial natural-resource data are persisted across map changes.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
        }
    }

    internal static class GatheringWorldPersistence
    {
        private static readonly object Gate = new object();
        private static readonly HashSet<Durango.Offline.World> DirtyWorlds =
            new HashSet<Durango.Offline.World>();

        internal static void MarkDirty(Durango.Offline.World world)
        {
            if (world == null)
            {
                return;
            }
            lock (Gate)
            {
                DirtyWorlds.Add(world);
            }
        }

        internal static void MarkSaved(Durango.Offline.World world)
        {
            if (world == null)
            {
                return;
            }
            lock (Gate)
            {
                DirtyWorlds.Remove(world);
            }
        }

        internal static void FlushAll()
        {
            Durango.Offline.World[] worlds;
            lock (Gate)
            {
                worlds = new Durango.Offline.World[DirtyWorlds.Count];
                DirtyWorlds.CopyTo(worlds);
                DirtyWorlds.Clear();
            }

            for (int i = 0; i < worlds.Length; i++)
            {
                try
                {
                    worlds[i].Save();
                }
                catch (Exception ex)
                {
                    MarkDirty(worlds[i]);
                    GatheringPlugin.Log.LogError(
                        "Deferred gathering world save failed: " + ex);
                }
            }

            if (worlds.Length > 0)
            {
                GatheringPlugin.Log.LogInfo(
                    "Deferred gathering world saved at map/logout transition. worlds=" +
                    worlds.Length);
            }
        }

        internal static void SaveNow(Durango.Offline.World world)
        {
            if (world == null)
            {
                return;
            }

            try
            {
                world.Save();
                MarkSaved(world);
            }
            catch (Exception ex)
            {
                MarkDirty(world);
                GatheringPlugin.Log.LogError(
                    "Immediate gathering world save failed: " + ex);
            }
        }
    }

    internal static class WorldPersistencePatches
    {
        public static void WorldSavePostfix(Durango.Offline.World __instance)
        {
            GatheringWorldPersistence.MarkSaved(__instance);
        }

        public static void ServerEndServerPrefix()
        {
            GatheringOutboundQueue.FlushAll();
            DatePalmReflection.FlushPersistentState();
            GatheringWorldPersistence.FlushAll();
        }
    }

#if false
    // Kept only as source history for the backed-up 0.4.20 experiment.
    internal static class OfflinePlayerScheduler
    {
        private sealed class ScheduledWork
        {
            internal object Player;
            internal DateTime DueAt;
            internal Action Action;
            internal bool Cancelled;
        }

        private static readonly object Gate = new object();
        private static readonly List<ScheduledWork> Pending =
            new List<ScheduledWork>();

        internal static object Schedule(
            object player,
            double delayMilliseconds,
            Action action)
        {
            ScheduledWork work = new ScheduledWork
            {
                Player = player,
                DueAt = DateTime.UtcNow.AddMilliseconds(
                    Math.Max(0.0, delayMilliseconds)),
                Action = action
            };
            lock (Gate)
            {
                Pending.Add(work);
            }
            return work;
        }

        internal static void Cancel(object token)
        {
            ScheduledWork work = token as ScheduledWork;
            if (work == null)
            {
                return;
            }
            lock (Gate)
            {
                work.Cancelled = true;
                Pending.Remove(work);
            }
        }

        internal static void Process(object player)
        {
            List<Action> dueActions = null;
            DateTime now = DateTime.UtcNow;
            lock (Gate)
            {
                for (int i = Pending.Count - 1; i >= 0; i--)
                {
                    ScheduledWork work = Pending[i];
                    if (work.Cancelled)
                    {
                        Pending.RemoveAt(i);
                        continue;
                    }
                    if (!object.ReferenceEquals(work.Player, player) ||
                        work.DueAt > now)
                    {
                        continue;
                    }

                    Pending.RemoveAt(i);
                    if (dueActions == null)
                    {
                        dueActions = new List<Action>();
                    }
                    dueActions.Add(work.Action);
                }
            }

            if (dueActions == null)
            {
                return;
            }
            dueActions.Reverse();
            for (int i = 0; i < dueActions.Count; i++)
            {
                try
                {
                    dueActions[i]();
                }
                catch (Exception ex)
                {
                    GatheringPlugin.Log.LogError(
                        "Offline scheduled gathering work failed: " + ex);
                }
            }
        }
    }

    internal static class GatheringPersistenceBatch
    {
        private static readonly object DirtyGate = new object();
        private static readonly HashSet<Durango.Offline.PlayerContext>
            DirtyPlayerContexts =
                new HashSet<Durango.Offline.PlayerContext>();
        private static readonly HashSet<Durango.Offline.WorldContext>
            DirtyWorldContexts =
                new HashSet<Durango.Offline.WorldContext>();

        [ThreadStatic]
        private static int _depth;

        [ThreadStatic]
        private static bool _flushing;

        [ThreadStatic]
        private static HashSet<Durango.Offline.PlayerContext>
            _playerContexts;

        [ThreadStatic]
        private static HashSet<Durango.Offline.WorldContext>
            _worldContexts;

        internal static void Begin()
        {
            _depth++;
            if (_depth != 1)
            {
                return;
            }
            _playerContexts =
                new HashSet<Durango.Offline.PlayerContext>();
            _worldContexts =
                new HashSet<Durango.Offline.WorldContext>();
        }

        internal static bool Defer(
            Durango.Offline.PlayerContext context)
        {
            if (_flushing || context == null)
            {
                return false;
            }

            if (_depth > 0)
            {
                _playerContexts.Add(context);
                return true;
            }

            lock (DirtyGate)
            {
                return DirtyPlayerContexts.Contains(context);
            }
        }

        internal static bool Defer(
            Durango.Offline.WorldContext context,
            bool persistent)
        {
            if (_flushing || context == null || persistent)
            {
                return false;
            }

            if (_depth > 0)
            {
                _worldContexts.Add(context);
                return true;
            }

            lock (DirtyGate)
            {
                return DirtyWorldContexts.Contains(context);
            }
        }

        internal static void End()
        {
            if (_depth <= 0)
            {
                return;
            }
            _depth--;
            if (_depth != 0)
            {
                return;
            }

            HashSet<Durango.Offline.PlayerContext> players =
                _playerContexts;
            HashSet<Durango.Offline.WorldContext> worlds =
                _worldContexts;
            _playerContexts = null;
            _worldContexts = null;

            lock (DirtyGate)
            {
                if (players != null)
                {
                    foreach (Durango.Offline.PlayerContext context in players)
                    {
                        DirtyPlayerContexts.Add(context);
                    }
                }
                if (worlds != null)
                {
                    foreach (Durango.Offline.WorldContext context in worlds)
                    {
                        DirtyWorldContexts.Add(context);
                    }
                }
            }
        }

        internal static void FlushAll()
        {
            Durango.Offline.PlayerContext[] players;
            Durango.Offline.WorldContext[] worlds;
            lock (DirtyGate)
            {
                players = new Durango.Offline.PlayerContext[
                    DirtyPlayerContexts.Count];
                DirtyPlayerContexts.CopyTo(players);
                worlds = new Durango.Offline.WorldContext[
                    DirtyWorldContexts.Count];
                DirtyWorldContexts.CopyTo(worlds);
                DirtyPlayerContexts.Clear();
                DirtyWorldContexts.Clear();
            }

            if (players.Length == 0 && worlds.Length == 0)
            {
                return;
            }

            _flushing = true;
            try
            {
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].Save();
                }
                for (int i = 0; i < worlds.Length; i++)
                {
                    worlds[i].Save(false);
                }
                GatheringPlugin.Log.LogInfo(
                    "Gathering dirty data saved at map/server transition. players=" +
                    players.Length +
                    ", worlds=" + worlds.Length);
            }
            finally
            {
                _flushing = false;
            }
        }
    }

    internal static class PersistencePatches
    {
        public static bool PlayerContextSavePrefix(
            Durango.Offline.PlayerContext __instance)
        {
            return !GatheringPersistenceBatch.Defer(__instance);
        }

        public static bool WorldContextSavePrefix(
            Durango.Offline.WorldContext __instance,
            bool persistent)
        {
            return !GatheringPersistenceBatch.Defer(
                __instance,
                persistent);
        }

        public static void ServerEndServerPrefix()
        {
            GatheringPersistenceBatch.FlushAll();
        }
    }
#endif

    internal static class GatheringOutboundQueue
    {
        private sealed class OutboundWork
        {
            internal string Label;
            internal Action Action;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<Durango.Offline.Player, Queue<OutboundWork>>
            Pending =
                new Dictionary<Durango.Offline.Player, Queue<OutboundWork>>();
        private static readonly HashSet<Durango.Offline.Player> Registered =
            new HashSet<Durango.Offline.Player>();

        internal static void Register(Durango.Offline.Player player)
        {
            if (player == null)
            {
                return;
            }

            bool subscribe = false;
            lock (Gate)
            {
                if (!Pending.ContainsKey(player))
                {
                    Pending[player] = new Queue<OutboundWork>();
                }
                if (Registered.Add(player))
                {
                    subscribe = true;
                }
            }

            if (subscribe)
            {
                player.Closed += delegate()
                {
                    DatePalmCollectState.CancelSession(player);
                    Clear(player);
                };
            }
        }

        internal static void Enqueue(
            Durango.Offline.Player player,
            Action action)
        {
            Enqueue(player, "Action", action);
        }

        internal static void Enqueue(
            Durango.Offline.Player player,
            string label,
            Action action)
        {
            if (player == null || action == null)
            {
                return;
            }

            lock (Gate)
            {
                Queue<OutboundWork> queue;
                if (!Pending.TryGetValue(player, out queue))
                {
                    queue = new Queue<OutboundWork>();
                    Pending[player] = queue;
                }
                queue.Enqueue(new OutboundWork
                {
                    Label = string.IsNullOrEmpty(label) ? "Action" : label,
                    Action = action
                });
            }
        }

        internal static void Send<T>(
            Durango.Offline.Player player,
            T message,
            uint replyOf)
        {
            Enqueue(player, typeof(T).Name, delegate()
            {
                player.Send<T>(message, replyOf);
            });
        }

        internal static void ProcessOne(Durango.Offline.Player player)
        {
            OutboundWork work = null;
            lock (Gate)
            {
                Queue<OutboundWork> queue;
                if (player != null &&
                    Pending.TryGetValue(player, out queue) &&
                    queue.Count > 0)
                {
                    work = queue.Dequeue();
                }
            }

            if (work == null || work.Action == null)
            {
                return;
            }

            try
            {
                work.Action();
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError(
                    "Gathering outbound queue action failed: " + ex);
            }
        }

        internal static void FlushAll()
        {
            List<OutboundWork> works = new List<OutboundWork>();
            lock (Gate)
            {
                foreach (KeyValuePair<Durango.Offline.Player,
                    Queue<OutboundWork>> pair in Pending)
                {
                    while (pair.Value.Count > 0)
                    {
                        works.Add(pair.Value.Dequeue());
                    }
                }
            }

            for (int i = 0; i < works.Count; i++)
            {
                try
                {
                    if (works[i] != null && works[i].Action != null)
                    {
                        works[i].Action();
                    }
                }
                catch (Exception ex)
                {
                    GatheringPlugin.Log.LogError(
                        "Gathering shutdown queue action failed (" +
                        (works[i] == null ? "unknown" : works[i].Label) +
                        "): " + ex);
                }
            }

            if (works.Count > 0)
            {
                GatheringPlugin.Log.LogInfo(
                    "Flushed " + works.Count +
                    " pending gathering action(s) before server shutdown.");
            }
        }

        private static void Clear(Durango.Offline.Player player)
        {
            lock (Gate)
            {
                Pending.Remove(player);
                Registered.Remove(player);
            }
        }
    }

    internal static class DatePalmCollectState
    {
        private class GatherSession
        {
            public System.Timers.Timer Timer;
            public uint Seq;
            public DateTime StartTime;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<object, string> PendingGenerators = new Dictionary<object, string>();
        private static readonly Dictionary<object, GatherSession> ActiveSessions = new Dictionary<object, GatherSession>();

        internal static void Mark(object player, string generatorId)
        {
            lock (Gate)
            {
                if (generatorId == null)
                {
                    PendingGenerators.Remove(player);
                }
                else
                {
                    PendingGenerators[player] = generatorId;
                }
            }
        }

        internal static string Consume(object player)
        {
            lock (Gate)
            {
                string generatorId;
                if (!PendingGenerators.TryGetValue(player, out generatorId))
                {
                    return null;
                }

                PendingGenerators.Remove(player);
                return generatorId;
            }
        }

        internal static void RegisterSession(object player, System.Timers.Timer timer, uint seq)
        {
            lock (Gate)
            {
                GatherSession oldSession;
                if (ActiveSessions.TryGetValue(player, out oldSession))
                {
                    if (oldSession != null && oldSession.Timer != null)
                    {
                        try { oldSession.Timer.Stop(); oldSession.Timer.Dispose(); } catch {}
                    }
                }
                ActiveSessions[player] = new GatherSession 
                { 
                    Timer = timer,
                    Seq = seq,
                    StartTime = DateTime.UtcNow
                };
            }
        }

        internal static bool IsActiveTimer(object player, System.Timers.Timer timer)
        {
            lock (Gate)
            {
                GatherSession session;
                if (ActiveSessions.TryGetValue(player, out session))
                {
                    return session != null && session.Timer == timer;
                }
                return false;
            }
        }

        internal static void RemoveSession(object player, System.Timers.Timer timer)
        {
            lock (Gate)
            {
                GatherSession session;
                if (ActiveSessions.TryGetValue(player, out session))
                {
                    if (session != null && session.Timer == timer)
                    {
                        ActiveSessions.Remove(player);
                    }
                }
            }
        }

        // Called by server when it receives Messages.Canceled from client (player moved mid-gather).
        // Stops and removes the active System.Timers.Timer for this player.
        internal static void CancelSession(object player)
        {
            lock (Gate)
            {
                GatherSession session;
                if (ActiveSessions.TryGetValue(player, out session))
                {
                    if (session != null && session.Timer != null)
                    {
                        try { session.Timer.Stop(); session.Timer.Dispose(); } catch {}
                    }
                    ActiveSessions.Remove(player);
                    PendingGenerators.Remove(player);
                    GatheringPlugin.Log.LogInfo("[Server] Harvest session cancelled by client cancel signal.");
                }
            }
        }

    }

    internal static class DatePalmReflection
    {
        private const BindingFlags InstanceFields =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        private static readonly FieldInfo PlayerWorldField =
            typeof(Durango.Offline.Player).GetField(
                "_world",
                InstanceFields);
        private static readonly FieldInfo WorldContextField =
            typeof(Durango.Offline.World).GetField(
                "_context",
                InstanceFields);
        private static readonly FieldInfo PlayerContextField =
            typeof(Durango.Offline.Player).GetField(
                "_context",
                InstanceFields);
        private static readonly FieldInfo NativeCollectedFromField =
            typeof(Durango.Offline.WorldContext).GetField(
                "CollectedFrom",
                InstanceFields);
        private static readonly FieldInfo PlayerGeneratorsField =
            typeof(Durango.Offline.Player).GetField(
                "_generators",
                InstanceFields);

        private static readonly object CollectedFromGate = new object();
        private static readonly Dictionary<object,
            Dictionary<string, Dictionary<string, Collectible>>>
            CustomCollectedFromByOwner =
                new Dictionary<object,
                    Dictionary<string, Dictionary<string, Collectible>>>();
        private static readonly HashSet<Durango.Offline.PlayerContext>
            DirtyPersistentContexts =
                new HashSet<Durango.Offline.PlayerContext>();

        private const string PersistentStoragePrefix =
            "gathering_plugin.collected_from.v1.";

        internal static Collectible? GetCollectedFrom(object player, string tileKey)
        {
            try
            {
                Durango.Offline.WorldContext context = GetWorldContext(player);
                IDictionary<string, Collectible> native =
                    GetNativeCollectedFrom(context);
                if (native != null)
                {
                    Collectible nativeCollectible;
                    return native.TryGetValue(tileKey, out nativeCollectible)
                        ? new Collectible?(nativeCollectible)
                        : null;
                }

                Durango.Offline.PlayerContext playerContext =
                    GetPlayerContext(player);
                object owner = (object)playerContext ?? context ?? player;
                if (owner == null)
                {
                    return null;
                }

                lock (CollectedFromGate)
                {
                    string worldKey = GetWorldKey(context);
                    Dictionary<string, Collectible> collectedFrom =
                        GetOrLoadCollectedFrom(
                            owner,
                            playerContext,
                            worldKey);
                    Collectible collectible;
                    return collectedFrom.TryGetValue(
                            tileKey,
                            out collectible)
                            ? new Collectible?(collectible)
                            : null;
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("GetCollectedFrom failed: " + ex.Message);
                return null;
            }
        }

        internal static void SetCollectedFrom(object player, string tileKey, Collectible collectible)
        {
            try
            {
                Durango.Offline.WorldContext context = GetWorldContext(player);
                IDictionary<string, Collectible> native =
                    GetNativeCollectedFrom(context);
                if (native != null)
                {
                    native[tileKey] = collectible;
                    return;
                }

                Durango.Offline.PlayerContext playerContext =
                    GetPlayerContext(player);
                object owner = (object)playerContext ?? context ?? player;
                if (owner == null)
                {
                    return;
                }

                lock (CollectedFromGate)
                {
                    string worldKey = GetWorldKey(context);
                    Dictionary<string, Collectible> collectedFrom =
                        GetOrLoadCollectedFrom(
                            owner,
                            playerContext,
                            worldKey);
                    collectedFrom[tileKey] = collectible;

                    if (playerContext != null)
                    {
                        if (playerContext.Storage == null)
                        {
                            playerContext.Storage =
                                new Dictionary<string, byte[]>();
                        }
                        playerContext.Storage[
                            GetPersistentStorageKey(worldKey)] =
                                SerializeCollectedFrom(collectedFrom);
                        DirtyPersistentContexts.Add(playerContext);
                    }
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("SetCollectedFrom failed: " + ex.Message);
            }
        }

        internal static void FlushPersistentState()
        {
            Durango.Offline.PlayerContext[] contexts;
            lock (CollectedFromGate)
            {
                contexts = new Durango.Offline.PlayerContext[
                    DirtyPersistentContexts.Count];
                DirtyPersistentContexts.CopyTo(contexts);
                DirtyPersistentContexts.Clear();
            }

            for (int i = 0; i < contexts.Length; i++)
            {
                try
                {
                    contexts[i].Save();
                }
                catch (Exception ex)
                {
                    lock (CollectedFromGate)
                    {
                        DirtyPersistentContexts.Add(contexts[i]);
                    }
                    GatheringPlugin.Log.LogError(
                        "Saving partial natural-resource state failed: " + ex);
                }
            }

            if (contexts.Length > 0)
            {
                GatheringPlugin.Log.LogInfo(
                    "Saved partial natural-resource state for " +
                    contexts.Length + " player context(s).");
            }
        }

        private static Dictionary<string, Collectible>
            GetOrLoadCollectedFrom(
                object owner,
                Durango.Offline.PlayerContext playerContext,
                string worldKey)
        {
            Dictionary<string, Dictionary<string, Collectible>> worlds;
            if (!CustomCollectedFromByOwner.TryGetValue(owner, out worlds))
            {
                worlds = new Dictionary<string,
                    Dictionary<string, Collectible>>(StringComparer.Ordinal);
                CustomCollectedFromByOwner[owner] = worlds;
            }

            Dictionary<string, Collectible> collectedFrom;
            if (worlds.TryGetValue(worldKey, out collectedFrom))
            {
                return collectedFrom;
            }

            collectedFrom = new Dictionary<string, Collectible>(
                StringComparer.Ordinal);
            if (playerContext != null && playerContext.Storage != null)
            {
                byte[] data;
                if (playerContext.Storage.TryGetValue(
                    GetPersistentStorageKey(worldKey),
                    out data))
                {
                    collectedFrom = DeserializeCollectedFrom(data);
                }
            }

            worlds[worldKey] = collectedFrom;
            return collectedFrom;
        }

        private static string GetWorldKey(
            Durango.Offline.WorldContext context)
        {
            if (context == null)
            {
                return "unknown";
            }

            string routeKey = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(context.Path))
                {
                    string directory = System.IO.Path.GetDirectoryName(
                        context.Path);
                    string slot = System.IO.Path.GetFileNameWithoutExtension(
                        context.Path);
                    string statePath = System.IO.Path.Combine(
                        directory,
                        slot + ".harbor.state");
                    if (File.Exists(statePath))
                    {
                        string[] lines = File.ReadAllLines(statePath);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            const string prefix = "current_save_key=";
                            if (lines[i].StartsWith(
                                prefix,
                                StringComparison.Ordinal))
                            {
                                routeKey = lines[i].Substring(prefix.Length);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Could not read Harbor route identity: " + ex.Message);
            }

            return "route=" +
                (string.IsNullOrEmpty(routeKey) ? "home" : routeKey) +
                "|terrain=" + (context.TerrainId ?? string.Empty);
        }

        private static string GetPersistentStorageKey(string worldKey)
        {
            return PersistentStoragePrefix + worldKey;
        }

        private static byte[] SerializeCollectedFrom(
            Dictionary<string, Collectible> collectedFrom)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(1);
                writer.Write(collectedFrom.Count);
                foreach (KeyValuePair<string, Collectible> pair in
                    collectedFrom)
                {
                    WriteString(writer, pair.Key);
                    Collectible collectible = pair.Value;
                    WriteString(writer, collectible.EntityId);
                    WriteString(writer, collectible.CollectibleId);
                    WriteString(writer, collectible.Size);
                    WriteString(writer, collectible.CriticalGenerator);

                    Generator[] generators = collectible.Generators ??
                        new Generator[0];
                    writer.Write(generators.Length);
                    for (int i = 0; i < generators.Length; i++)
                    {
                        Generator generator = generators[i];
                        WriteString(writer, generator.Id);
                        writer.Write(generator.Level);
                        WriteString(writer, generator.Name);
                        WriteString(writer, generator.Icon);
                        writer.Write(generator.Amount);
                        writer.Write(generator.Effort);
                        writer.Write(generator.Duration);
                        writer.Write(generator.Enabled);

                        Dictionary<string, int> requirements =
                            generator.ToolRequirements;
                        writer.Write(requirements == null
                            ? 0
                            : requirements.Count);
                        if (requirements != null)
                        {
                            foreach (KeyValuePair<string, int> requirement in
                                requirements)
                            {
                                WriteString(writer, requirement.Key);
                                writer.Write(requirement.Value);
                            }
                        }
                    }
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static Dictionary<string, Collectible>
            DeserializeCollectedFrom(byte[] data)
        {
            Dictionary<string, Collectible> result =
                new Dictionary<string, Collectible>(StringComparer.Ordinal);
            if (data == null || data.Length == 0)
            {
                return result;
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(data, false))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    int version = reader.ReadInt32();
                    int entryCount = reader.ReadInt32();
                    if (version != 1 || entryCount < 0 || entryCount > 100000)
                    {
                        return result;
                    }

                    for (int i = 0; i < entryCount; i++)
                    {
                        string tileKey = reader.ReadString();
                        Collectible collectible = default(Collectible);
                        collectible.EntityId = reader.ReadString();
                        collectible.CollectibleId = reader.ReadString();
                        collectible.Size = reader.ReadString();
                        collectible.CriticalGenerator = reader.ReadString();

                        int generatorCount = reader.ReadInt32();
                        if (generatorCount < 0 || generatorCount > 1000)
                        {
                            return new Dictionary<string, Collectible>(
                                StringComparer.Ordinal);
                        }
                        collectible.Generators =
                            new Generator[generatorCount];
                        for (int j = 0; j < generatorCount; j++)
                        {
                            Generator generator = default(Generator);
                            generator.Id = reader.ReadString();
                            generator.Level = reader.ReadInt32();
                            generator.Name = reader.ReadString();
                            generator.Icon = reader.ReadString();
                            generator.Amount = reader.ReadInt32();
                            generator.Effort = reader.ReadSingle();
                            generator.Duration = reader.ReadSingle();
                            generator.Enabled = reader.ReadBoolean();

                            int requirementCount = reader.ReadInt32();
                            if (requirementCount < 0 || requirementCount > 1000)
                            {
                                return new Dictionary<string, Collectible>(
                                    StringComparer.Ordinal);
                            }
                            generator.ToolRequirements =
                                new Dictionary<string, int>(
                                    requirementCount,
                                    StringComparer.Ordinal);
                            for (int k = 0; k < requirementCount; k++)
                            {
                                generator.ToolRequirements[
                                    reader.ReadString()] = reader.ReadInt32();
                            }
                            collectible.Generators[j] = generator;
                        }
                        result[tileKey] = collectible;
                    }
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Stored partial natural-resource state was invalid: " +
                    ex.Message);
                result.Clear();
            }
            return result;
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            writer.Write(value ?? string.Empty);
        }

        internal static void SetGenerators(object player, List<Generator> generators)
        {
            try
            {
                if (PlayerGeneratorsField != null &&
                    player is Durango.Offline.Player)
                {
                    PlayerGeneratorsField.SetValue(player, generators);
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("SetGenerators failed: " + ex.Message);
            }
        }

        private static Durango.Offline.WorldContext GetWorldContext(
            object player)
        {
            if (player == null ||
                PlayerWorldField == null ||
                !(player is Durango.Offline.Player))
            {
                return null;
            }

            object world = PlayerWorldField.GetValue(player);
            if (world == null || WorldContextField == null)
            {
                return null;
            }

            return WorldContextField.GetValue(world) as
                Durango.Offline.WorldContext;
        }

        private static Durango.Offline.PlayerContext GetPlayerContext(
            object player)
        {
            return player == null ||
                PlayerContextField == null ||
                !(player is Durango.Offline.Player)
                    ? null
                    : PlayerContextField.GetValue(player) as
                        Durango.Offline.PlayerContext;
        }

        private static IDictionary<string, Collectible> GetNativeCollectedFrom(
            object context)
        {
            return context == null || NativeCollectedFromField == null
                ? null
                : NativeCollectedFromField.GetValue(context) as
                    IDictionary<string, Collectible>;
        }

    }

    internal static class DatePalmXpBridge
    {
        private static bool _warnedMissingApi;

        internal static void AddLevelXp(int amount)
        {
            try
            {
                Type apiType = AccessTools.TypeByName("BaoX.DurangoOriginal.PlayerProgressionMod.PlayerProgressionApi");
                if (apiType == null)
                {
                    WarnMissingApi();
                    return;
                }

                MethodInfo method = apiType.GetMethod("AddExperience", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null)
                {
                    WarnMissingApi();
                    return;
                }

                object[] args = new object[] { amount, null };
                bool ok = (bool)method.Invoke(null, args);
                if (!ok && args.Length > 1 && args[1] != null)
                {
                    GatheringPlugin.Log.LogWarning("XP reward failed: " + args[1]);
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning("XP reward failed: " + ex.Message);
            }
        }

        private static void WarnMissingApi()
        {
            if (_warnedMissingApi)
            {
                return;
            }

            _warnedMissingApi = true;
            GatheringPlugin.Log.LogWarning("PlayerProgressionApi not found; gathering XP reward disabled");
        }
    }

    internal static class KylloxPatches
    {
        public static void GeneratorPostfix(BiomeSpriteInfo biomeSpriteInfo, ref List<Generator> __result)
        {
            if (biomeSpriteInfo == null)
            {
                return;
            }

            if (UnstableSavannaSeaDatabase.IsTamedVegetation(
                biomeSpriteInfo.CollectibleId))
            {
                __result =
                    UnstableSavannaSeaDatabase.CreateTamedRootGenerators();
            }
            else if (UnstableSavannaSeaDatabase.IsActive(
                biomeSpriteInfo.CollectibleId))
            {
                __result = UnstableSavannaSeaDatabase.CreateGenerators(
                    biomeSpriteInfo.CollectibleId);
            }
        }

        public static void HandleTouchNaturalPostfix(object __instance, Messages.Touch touch, BiomeSpriteInfo biomeSpriteInfo, ref Touched __result)
        {
            string collectibleId = biomeSpriteInfo == null
                ? __result.Collectible.CollectibleId
                : biomeSpriteInfo.CollectibleId;
            bool isTamedRoot =
                UnstableSavannaSeaDatabase.IsTamedVegetation(
                    collectibleId);
            if (!isTamedRoot &&
                !UnstableSavannaSeaDatabase.IsActive(collectibleId))
            {
                return;
            }

            string collectibleSize =
                UnstableSavannaSeaDatabase.CollectibleSizeOf(
                    collectibleId);
            bool hasExpectedGenerators =
                isTamedRoot
                    ? UnstableSavannaSeaDatabase
                        .HasOnlyTamedRootGenerator(
                            __result.Collectible.Generators)
                    : UnstableSavannaSeaDatabase
                        .HasOnlyExpectedGenerators(
                            collectibleId,
                            __result.Collectible.Generators);
            List<Generator> generators = hasExpectedGenerators
                ? new List<Generator>(
                    __result.Collectible.Generators ??
                    new Generator[0])
                : (isTamedRoot
                    ? UnstableSavannaSeaDatabase
                        .CreateTamedRootGenerators()
                    : UnstableSavannaSeaDatabase.CreateGenerators(
                        collectibleId));

            GatheringMechanics.ApplyGeneratorSkillAvailability(
                __instance as Durango.Offline.Player,
                collectibleId,
                generators);
            string criticalGenerator =
                isTamedRoot
                    ? "root"
                    : UnstableSavannaSeaDatabase.CriticalGenerator(
                        collectibleId,
                        generators.ToArray());

            Collectible collectible = __result.Collectible;
            collectible.CollectibleId = collectibleId;
            collectible.Size = collectibleSize;
            collectible.Generators = generators.ToArray();
            collectible.CriticalGenerator = criticalGenerator;
            __result.Collectible = collectible;

            DatePalmReflection.SetGenerators(__instance, generators);
            DatePalmReflection.SetCollectedFrom(__instance, touch.Tile.ToString(), collectible);
        }

        public static void CollectNaturalPrefix(object __instance, Collect msg)
        {
            Collectible? collectible = DatePalmReflection.GetCollectedFrom(__instance, msg.Tile.ToString());
            if (collectible == null ||
                !UnstableSavannaSeaDatabase.IsHandled(
                    collectible.Value.CollectibleId))
            {
                DatePalmCollectState.Mark(__instance, null);
                return;
            }

            DatePalmCollectState.Mark(__instance, msg.GeneratorId);
        }

        public static void SendCollectedPostfix(object __instance, List<Item> list, Result result)
        {
            string generatorId = DatePalmCollectState.Consume(__instance);
            if (!UnstableSavannaSeaDatabase.IsRewardGenerator(generatorId))
            {
                return;
            }

            if (result != Result.Success && result != Result.GreatSuccess)
            {
                return;
            }

            if (list != null && list.Count > 0)
            {
                DatePalmXpBridge.AddLevelXp(Math.Max(1, list[0].Level));
            }
        }
    }

    internal static class ClientPatches
    {
        private sealed class ActualGatheringDuration
        {
            internal string ToolId;
            internal float Duration;
        }

        private static bool _isNaturallyEnding = false;
        private static bool _gatheringLockInvocation;
        private static string _gatheringLockTargetId;
        private static string _gatheringLockGeneratorId;
        private static string _approvedLockTargetId;
        private static string _approvedLockGeneratorId;
        private static string _approvedLockToolId;
        private static string _actualDurationTargetId;
        private static string _lastGatheringTargetId;
        private static readonly Dictionary<string, ActualGatheringDuration>
            ActualDurations =
                new Dictionary<string, ActualGatheringDuration>();
        private const string GatheringStatusOverlayName =
            "GatheringStatusOverlay";
        private const string MobileNoToolInfoName =
            "GatheringMobileNoToolInfo";

        public static void InteractionMenuWidgetSetPrefix(
            InteractionData.InteractionMenuData data)
        {
            ApplyActualGatheringDuration(data.GatheringData);
        }

        public static void InteractionMenuWidgetSetPostfix(
            Durango.UI.InteractionMenuWidgetBase __instance,
            InteractionData.InteractionMenuData data)
        {
            try
            {
                InteractionData.GatheringData gatheringData =
                    data.GatheringData;
                if (gatheringData == null)
                {
                    return;
                }

                FieldInfo infoLabelField = AccessTools.Field(
                    typeof(Durango.UI.InteractionMenuWidgetBase),
                    "InfoLabel");
                UILabel infoLabel = infoLabelField == null
                    ? null
                    : infoLabelField.GetValue(__instance)
                        as UILabel;
                if (infoLabel == null ||
                    infoLabel.transform.parent == null)
                {
                    return;
                }

                // Remove the direct UISprite overlay used by v0.4.11.  The
                // mobile client renders this warning through an encoded
                // UILabel, just like the critical icon, so the complete
                // wrench + prohibited symbol is preserved and follows the
                // ring-menu position preset.
                UnityEngine.Transform overlayTransform =
                    infoLabel.transform.parent.Find(
                        GatheringStatusOverlayName);
                UISprite statusOverlay = overlayTransform == null
                    ? null
                    : overlayTransform.GetComponent<UISprite>();
                if (statusOverlay != null)
                {
                    statusOverlay.gameObject.SetActive(false);
                }

                UnityEngine.Transform warningInfoTransform =
                    infoLabel.transform.Find(MobileNoToolInfoName);
                UnityEngine.GameObject warningInfo =
                    warningInfoTransform == null
                        ? null
                        : warningInfoTransform.gameObject;
                Type hoverType = AccessTools.TypeByName(
                    "Durango.UI.Control.TargetActivatorOnHover");
                UnityEngine.Behaviour criticalHover = hoverType == null
                    ? null
                    : infoLabel.GetComponent(hoverType)
                        as UnityEngine.Behaviour;

                bool showMobileWarning =
                    !gatheringData.IsAvailableForGathering() &&
                    gatheringData.RequiredTools != null &&
                    gatheringData.RequiredTools.Count > 0;
                if (!showMobileWarning)
                {
                    if (warningInfo != null)
                    {
                        warningInfo.SetActive(false);
                    }
                    if (criticalHover != null)
                    {
                        criticalHover.enabled = true;
                    }
                    return;
                }

                string criticalText = null;
                if (gatheringData.IsCritical &&
                    infoLabel.gameObject.activeSelf &&
                    !string.IsNullOrEmpty(infoLabel.text))
                {
                    criticalText = infoLabel.text;
                }

                if (warningInfo == null)
                {
                    warningInfo = CreateMobileWarningInfo(infoLabel);
                }

                UILabel warningLabel =
                    warningInfo.GetComponent<UILabel>();
                warningLabel.supportEncoding = true;
                warningLabel.color = UnityEngine.Color.white;
                warningLabel.text = "[icon=img_notool]";
                warningLabel.width = 18;
                warningLabel.height = 24;
                warningLabel.MarkAsChanged();
                warningLabel.ResizeCollider();
                SetMobileWarningTooltip(
                    warningInfo,
                    BuildGatheringWarningTooltip(gatheringData));
                warningInfo.transform.localPosition =
                    string.IsNullOrEmpty(criticalText)
                        ? UnityEngine.Vector3.zero
                        : new UnityEngine.Vector3(-18f, 0f, 0f);
                warningInfo.transform.localScale = UnityEngine.Vector3.one;
                warningInfo.SetActive(true);

                // The clone owns hover handling while the warning is visible.
                // This prevents the original Critical tooltip from appearing
                // over the missing-skill/tool explanation.
                if (criticalHover != null)
                {
                    criticalHover.enabled = false;
                }
                infoLabel.text = criticalText ?? string.Empty;
                infoLabel.gameObject.SetActive(true);
                infoLabel.MarkAsChanged();

                // The original mobile widget dims unavailable generators to
                // 75 percent, regardless of whether the cause is a missing
                // skill (Enabled) or a missing tool (BestPerformance).
                __instance.Alpha = 0.75f;
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Failed to set gathering status sub-icon: " +
                    ex.Message);
            }
        }

        private static UnityEngine.GameObject CreateMobileWarningInfo(
            UILabel infoLabel)
        {
            UnityEngine.GameObject clone =
                UnityEngine.Object.Instantiate(
                    infoLabel.gameObject) as UnityEngine.GameObject;
            clone.name = MobileNoToolInfoName;
            clone.transform.SetParent(infoLabel.transform, false);
            clone.transform.localPosition = UnityEngine.Vector3.zero;
            clone.transform.localRotation = UnityEngine.Quaternion.identity;
            clone.transform.localScale = UnityEngine.Vector3.one;

            // The clone follows its Critical parent, so its own serialized
            // ring-position preset must not move it a second time.
            Type presetType = AccessTools.TypeByName(
                "Durango.UI.InteractionMenuPreset");
            UnityEngine.Behaviour preset = presetType == null
                ? null
                : clone.GetComponent(presetType)
                    as UnityEngine.Behaviour;
            if (preset != null)
            {
                preset.enabled = false;
            }

            UILabel cloneLabel = clone.GetComponent<UILabel>();
            cloneLabel.supportEncoding = true;
            cloneLabel.color = UnityEngine.Color.white;
            cloneLabel.text = "[icon=img_notool]";
            cloneLabel.width = 18;
            cloneLabel.height = 24;
            cloneLabel.MarkAsChanged();
            cloneLabel.ResizeCollider();
            return clone;
        }

        private static void SetMobileWarningTooltip(
            UnityEngine.GameObject warningInfo,
            string text)
        {
            Type hoverType = AccessTools.TypeByName(
                "Durango.UI.Control.TargetActivatorOnHover");
            if (hoverType == null)
            {
                return;
            }

            UnityEngine.Component hover =
                warningInfo.GetComponent(hoverType);
            FieldInfo targetField = AccessTools.Field(hoverType, "_target");
            UnityEngine.GameObject target = hover == null ||
                targetField == null
                    ? null
                    : targetField.GetValue(hover)
                        as UnityEngine.GameObject;
            UILabel tooltipLabel = target == null
                ? null
                : target.GetComponent<UILabel>();
            if (tooltipLabel != null)
            {
                tooltipLabel.text = text;
                tooltipLabel.MarkAsChanged();
            }
        }

        private static string BuildGatheringWarningTooltip(
            InteractionData.GatheringData gatheringData)
        {
            if (!gatheringData.Enabled)
            {
                try
                {
                    Touched touched =
                        GameSystem<InteractionSystem>.Instance().LastTouched;
                    string collectibleId =
                        touched.Collectible.CollectibleId;
                    SkillNeeded needed;
                    GatheringMechanics.ClientHasRequiredSkill(
                        collectibleId,
                        gatheringData.GeneratorId,
                        out needed);
                    Durango.Logic.Skill.Node skill =
                        GameSystem<Durango.Logic.SkillSystem>
                            .Instance()
                            .FindSkill(
                                needed.SkillId,
                                needed.SubId,
                                needed.Level);
                    if (skill != null)
                    {
                        string categoryName =
                            Durango.Logic.Skill.Util
                                .CategoryLocalizeName(skill.Category);
                        string categoryIcon =
                            Durango.Logic.Skill.Util
                                .CategoryIcon(skill.Category);
                        return skill.CategoryLevel <= 0
                            ? L10N.T._(
                                "<em>{0}</em> ([icon={1}]{2}) 스킬이 필요합니다.",
                                new object[]
                                {
                                    skill.Name,
                                    categoryIcon,
                                    categoryName
                                })
                            : L10N.T._(
                                "<em>{0}</em> ([icon={1}]{2} {3:lv:}) 스킬이 필요합니다.",
                                new object[]
                                {
                                    skill.Name,
                                    categoryIcon,
                                    categoryName,
                                    skill.CategoryLevel
                                });
                    }
                }
                catch
                {
                }

                return L10N.T._(
                    "<em>{0}</em> 스킬이 필요합니다.",
                    new object[] { gatheringData.Name });
            }

            List<KeyValuePair<string, int>> requiredTools =
                new List<KeyValuePair<string, int>>();
            int requiredLevel = 0;
            foreach (KeyValuePair<string, int> pair in
                gatheringData.RequiredTools)
            {
                if (string.Equals(
                    pair.Key,
                    "bare_hands",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                requiredTools.Add(pair);
                requiredLevel = Math.Max(requiredLevel, pair.Value);
            }

            string toolNames =
                Durango.Logic.Item.Util
                    .LocalizedTagNamesAndLevels(requiredTools);
            return requiredLevel <= 1
                ? L10N.T._(
                    "다음 중 하나의 도구가 필요합니다.\n<em>{0}</em>",
                    new object[] { toolNames })
                : L10N.T._(
                    "다음 중 하나의 도구가 필요합니다.\n{1:lv:}이상 <em>{0}</em>",
                    new object[] { toolNames, requiredLevel });
        }

        public static void SendTouchMsgPostfix(
            InteractionSystem __instance)
        {
            try
            {
                InteractionObject target = __instance.Target;
                if (target == null ||
                    !DataHelper.IsNaturalObject(target.EntityType))
                {
                    return;
                }

                if (!string.Equals(
                    _actualDurationTargetId,
                    InteractionTargetKey(target),
                    StringComparison.Ordinal))
                {
                    ActualDurations.Clear();
                    _actualDurationTargetId =
                        InteractionTargetKey(target);
                }
                _lastGatheringTargetId =
                    InteractionTargetKey(target);

                BiomeSpriteInfo biomeSpriteInfo =
                    DataHelper.GetBiomeSpriteInfo(target.EntityType);
                if (biomeSpriteInfo == null ||
                    !UnstableSavannaSeaDatabase.IsHandled(
                        biomeSpriteInfo.CollectibleId))
                {
                    return;
                }

                Durango.UI.Popup.LoadingRingWidget loadingRing =
                    UIManager.Popup.LoadingRing;
                if (loadingRing != null &&
                    loadingRing.AttachMode ==
                        Durango.UI.Popup.LoadingRingWidget.Mode
                            .InteractionTarget)
                {
                    loadingRing.ShowInstantly();
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Failed to show delayed touch loading ring: " +
                    ex.Message);
            }
        }

        public static void OnGatheringTimerPostfix(
            object __instance,
            Messages.Timer msg)
        {
            try
            {
                FieldInfo dataField = AccessTools.Field(
                    __instance.GetType(),
                    "_currentGatheringData");
                InteractionData.GatheringData data =
                    dataField == null
                        ? null
                        : dataField.GetValue(__instance)
                            as InteractionData.GatheringData;
                if (data == null ||
                    string.IsNullOrEmpty(data.GeneratorId) ||
                    msg.Duration <= 0f)
                {
                    return;
                }

                string targetId = CurrentInteractionTargetId();
                if (string.IsNullOrEmpty(targetId))
                {
                    return;
                }

                CacheActualGatheringDuration(
                    targetId,
                    data.GeneratorId,
                    data.BestTool == null
                        ? string.Empty
                        : data.BestTool.Id,
                    msg.Duration);
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Failed to cache actual gathering duration: " +
                    ex.Message);
            }
        }

        public static void FindBestToolPostfix(
            InteractionData.GatheringData __instance,
            IList<Durango.Logic.Item.ItemData> tools)
        {
            try
            {
                PreferUnequippedLockedTool(__instance, tools);
                ApplyActualGatheringDuration(__instance);
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Failed to restore actual gathering duration: " +
                    ex.Message);
            }
        }

        private static void PreferUnequippedLockedTool(
            InteractionData.GatheringData data,
            IList<Durango.Logic.Item.ItemData> tools)
        {
            if (data == null ||
                data.BestTool == null ||
                !data.BestTool.Locked ||
                !data.BestTool.IsEquipments ||
                data.RequiredTools == null ||
                tools == null)
            {
                return;
            }

            Durango.Logic.Item.ItemData replacement = null;
            int replacementPerformance = 0;
            for (int i = 0; i < tools.Count; i++)
            {
                Durango.Logic.Item.ItemData candidate = tools[i];
                if (candidate == null ||
                    candidate.IsDestroyed() ||
                    candidate.IsEquipments ||
                    !candidate.Locked)
                {
                    continue;
                }

                foreach (KeyValuePair<string, int> requirement in
                    data.RequiredTools)
                {
                    Durango.Logic.Item.TagData tag =
                        candidate.GetTagData(requirement.Key);
                    if (tag == null || tag.Level < requirement.Value)
                    {
                        continue;
                    }

                    if (replacement == null ||
                        tag.Level > replacementPerformance)
                    {
                        replacement = candidate;
                        replacementPerformance = tag.Level;
                    }
                }
            }

            if (replacement == null)
            {
                return;
            }

            string equippedToolId = data.BestTool.Id;
            data.BestTool = replacement;
            data.BestPerformance = replacementPerformance;
            GatheringPlugin.Log.LogInfo(
                "[Client] Preferred unequipped locked gathering tool: " +
                replacement.Id + " instead of equipped " +
                equippedToolId + ".");
        }

        internal static void CacheActualGatheringDuration(
            string targetId,
            string generatorId,
            string toolId,
            float duration)
        {
            if (string.IsNullOrEmpty(targetId) ||
                string.IsNullOrEmpty(generatorId) ||
                duration <= 0f)
            {
                return;
            }

            if (!string.Equals(
                _actualDurationTargetId,
                targetId,
                StringComparison.Ordinal))
            {
                ActualDurations.Clear();
                _actualDurationTargetId = targetId;
            }
            _lastGatheringTargetId = targetId;
            ActualDurations[generatorId] =
                new ActualGatheringDuration
                {
                    ToolId = toolId ?? string.Empty,
                    Duration = duration
                };
            GatheringPlugin.Log.LogInfo(
                "[Client] Cached actual gathering duration: target=" +
                targetId + " generator=" + generatorId + " duration=" +
                duration.ToString("0.0") + "s");
        }

        private static void ApplyActualGatheringDuration(
            InteractionData.GatheringData data)
        {
            if (data == null ||
                !string.Equals(
                    _actualDurationTargetId,
                    CurrentInteractionTargetId(),
                    StringComparison.Ordinal))
            {
                return;
            }

            ActualGatheringDuration cached;
            if (!ActualDurations.TryGetValue(
                data.GeneratorId,
                out cached))
            {
                return;
            }

            string toolId = data.BestTool == null
                ? string.Empty
                : data.BestTool.Id;
            if (!string.Equals(
                cached.ToolId,
                toolId,
                StringComparison.Ordinal))
            {
                return;
            }

            data.Duration = cached.Duration;
        }

        private static string CurrentInteractionTargetId()
        {
            InteractionSystem interactionSystem =
                GameSystem<InteractionSystem>.Instance();
            if (interactionSystem == null)
            {
                return null;
            }

            InteractionObject target = interactionSystem.Target ??
                interactionSystem.LastInteractionTarget;
            return target == null
                ? _lastGatheringTargetId
                : InteractionTargetKey(target);
        }

        private static string InteractionTargetKey(
            InteractionObject target)
        {
            return target == null
                ? null
                : MakeTileTargetKey(
                    (int)target.Tile.x,
                    (int)target.Tile.y);
        }

        internal static string MakeTileTargetKey(int x, int y)
        {
            return "tile:" + x + "," + y;
        }

        public static bool GatheringPrefix(object data)
        {
            try
            {
                if (data == null)
                {
                    return true;
                }

                Durango.Logic.Item.Inventory playerInventory =
                    GameSystem<InventorySystem>
                        .Instance()
                        .PlayerInventory;
                if (playerInventory != null &&
                    !playerInventory.CanPutIn(
                        new Durango.Logic.Item.ItemData[] { null }))
                {
                    GameSystem<InteractionSystem>
                        .Instance()
                        .ReservationQueue
                        .Clear();
                    UIManager.SystemMsg(
                        L10N.T._(
                            "\uAC00\uBC29\uC5D0 \uACF5\uAC04\uC774 \uC5C6\uC2B5\uB2C8\uB2E4."),
                        3f);
                    GatheringPlugin.Log.LogInfo(
                        "[Client] Gathering blocked: player inventory is full (" +
                        playerInventory.CurrentSize() + "/" +
                        playerInventory.Capacity + ").");
                    return false;
                }

                FieldInfo generatorField =
                    AccessTools.Field(data.GetType(), "GeneratorId");
                string generatorId = generatorField == null
                    ? null
                    : generatorField.GetValue(data) as string;

                Touched touched =
                    GameSystem<InteractionSystem>.Instance().LastTouched;
                string collectibleId =
                    touched.Collectible.CollectibleId;

                SkillNeeded needed;
                if (GatheringMechanics.ClientHasRequiredSkill(
                    collectibleId,
                    generatorId,
                    out needed))
                {
                    BeginGatheringLockInvocation(generatorId);
                    return true;
                }

                GameSystem<InteractionSystem>
                    .Instance()
                    .ReservationQueue
                    .Clear();
                GameSystem<Durango.Logic.SkillSystem>
                    .Instance()
                    .SkillNeeded(needed);
                GatheringPlugin.Log.LogInfo(
                    "[Client] Gathering blocked; showed required skill popup: " +
                    needed.SkillId + "/" + needed.SubId +
                    " level=" + needed.Level);
                return false;
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogWarning(
                    "Gathering skill precheck failed: " + ex.Message);
                return true;
            }
        }

        public static void GatheringPostfix()
        {
            _gatheringLockInvocation = false;
            _gatheringLockTargetId = null;
            _gatheringLockGeneratorId = null;
        }

        public static bool ShowLockConfirmPrefix(
            Durango.Logic.Item.ItemData item,
            ref Action onOk)
        {
            if (!_gatheringLockInvocation ||
                item == null ||
                item.SafeLevel == Durango.Logic.Item.SafeLevel.None)
            {
                return true;
            }

            string targetId = _gatheringLockTargetId;
            string generatorId = _gatheringLockGeneratorId;
            string toolId = item.Id;
            if (string.Equals(
                    _approvedLockTargetId,
                    targetId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _approvedLockGeneratorId,
                    generatorId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    _approvedLockToolId,
                    toolId,
                    StringComparison.Ordinal))
            {
                GatheringPlugin.Log.LogInfo(
                    "[Client] Reusing locked-tool confirmation for queued gather: " +
                    generatorId + " tool=" + toolId);
                if (onOk != null)
                {
                    onOk();
                }
                return false;
            }

            Action originalOnOk = onOk;
            onOk = delegate()
            {
                _approvedLockTargetId = targetId;
                _approvedLockGeneratorId = generatorId;
                _approvedLockToolId = toolId;
                GatheringPlugin.Log.LogInfo(
                    "[Client] Locked tool approved for current gathering queue: " +
                    generatorId + " tool=" + toolId);
                if (originalOnOk != null)
                {
                    originalOnOk();
                }
            };
            return true;
        }

        private static void BeginGatheringLockInvocation(
            string generatorId)
        {
            InteractionObject target =
                GameSystem<InteractionSystem>
                    .Instance()
                    .LastInteractionTarget;
            string targetId = InteractionTargetKey(target);
            if (!string.IsNullOrEmpty(targetId))
            {
                _lastGatheringTargetId = targetId;
            }

            if (!string.Equals(
                    _approvedLockTargetId,
                    targetId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    _approvedLockGeneratorId,
                    generatorId,
                    StringComparison.Ordinal))
            {
                ClearGatheringLockApproval();
            }

            _gatheringLockInvocation = true;
            _gatheringLockTargetId = targetId;
            _gatheringLockGeneratorId = generatorId;
        }

        private static void ClearGatheringLockApproval()
        {
            _approvedLockTargetId = null;
            _approvedLockGeneratorId = null;
            _approvedLockToolId = null;
        }

        public static void OnCollectedPrefix()
        {
            _isNaturallyEnding = true;
        }

        public static void OnCollectedPostfix()
        {
            _isNaturallyEnding = false;
            try
            {
                if (GameSystem<InteractionSystem>
                        .Instance()
                        .ReservationQueue
                        .Count == 0)
                {
                    ClearGatheringLockApproval();
                }
            }
            catch
            {
                ClearGatheringLockApproval();
            }
        }

        public static void set_CurrentGatheringDataPrefix(object __instance, object value)
        {
            try
            {
                if (_isNaturallyEnding)
                {
                    return;
                }

                if (value != null)
                {
                    return;
                }

                System.Reflection.FieldInfo dataField = AccessTools.Field(__instance.GetType(), "_currentGatheringData");
                if (dataField == null)
                {
                    return;
                }

                object currentData = dataField.GetValue(__instance);
                if (currentData == null)
                {
                    return;
                }

                GatheringPlugin.Log.LogInfo("[Client] Gathering cancelled by client. Sending Canceled to server.");
                ClearGatheringLockApproval();
                Connections.Frontend.Send<Messages.Canceled>(new Messages.Canceled(), false, 0U);
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in set_CurrentGatheringDataPrefix: " + ex);
            }
        }
    }

    internal static class OriginalPatches
    {
        private const double NaturalTouchResponseDelayMs = 300.0;

        public static void ConstructorPostfix(Durango.Offline.Player __instance, Durango.Offline.Connection connection, Durango.Offline.World world, Durango.Offline.PlayerContext context)
        {
            try
            {
                GatheringMechanics.RegisterPlayer(__instance, context);
                GatheringOutboundQueue.Register(__instance);

                connection.Recv<Messages.Collect>(delegate(Messages.Collect msg, Durango.Network.PacketHeader header)
                {
                    HandleCollectOriginal(__instance, world, msg, header);
                });

                connection.Recv<GetCollectible>(delegate(GetCollectible msg, Durango.Network.PacketHeader header)
                {
                    HandleGetCollectibleOriginal(__instance, msg, header);
                });

                // Server receives Canceled (TypeCode 2038) from client when player moves mid-gather.
                // Cancel the active System.Timers.Timer for this player.
                connection.Recv<Messages.Canceled>(delegate(Messages.Canceled msg, Durango.Network.PacketHeader header)
                {
                    try
                    {
                        GatheringPlugin.Log.LogInfo("[Server] Received Canceled from client — cancelling active harvest session.");
                        DatePalmCollectState.CancelSession(__instance);
                    }
                    catch (Exception ex)
                    {
                        GatheringPlugin.Log.LogError("Error handling Canceled on server: " + ex);
                    }
                });
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Failed to register Collect/GetCollectible handlers in ConstructorPostfix: " + ex);
            }
        }

        public static void PlayerProcessPostfix(
            Durango.Offline.Player __instance)
        {
            GatheringOutboundQueue.ProcessOne(__instance);
        }

        private static void HandleGetCollectibleOriginal(Durango.Offline.Player player, GetCollectible msg, Durango.Network.PacketHeader header)
        {
            try
            {
                Collectible? col = DatePalmReflection.GetCollectedFrom(player, msg.Tile.ToString());
                if (col != null &&
                    UnstableSavannaSeaDatabase.IsHandled(
                        col.Value.CollectibleId))
                {
                    player.Send<Collectible>(col.Value, header.Seq);
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in HandleGetCollectibleOriginal: " + ex);
            }
        }

        public static bool HandleTouchMsgPrefix(Durango.Offline.Player __instance, Messages.Touch touch, uint seq)
        {
            try
            {
                if (touch.EntityType == 0)
                {
                    return true;
                }

                if (DataHelper.IsNaturalObject((int)touch.EntityType))
                {
                    BiomeSpriteInfo biomeSpriteInfo = DataHelper.GetBiomeSpriteInfo((int)touch.EntityType);
                    if (biomeSpriteInfo != null &&
                        UnstableSavannaSeaDatabase.IsHandled(
                            biomeSpriteInfo.CollectibleId))
                    {
                        string collectibleId = biomeSpriteInfo.CollectibleId;
                        bool isTamedRoot =
                            UnstableSavannaSeaDatabase
                                .IsTamedVegetation(collectibleId);
                        Touched msg = default(Touched);
                        msg.EntityId = touch.EntityId;
                        msg.EntityName = biomeSpriteInfo.Name;
                        msg.Level = isTamedRoot
                            ? 10
                            : UnstableSavannaSeaDatabase.LevelOf(
                                collectibleId);
                        msg.PrototypeId = string.Empty;

                        bool flag = GameManager.ClusterMode == Durango.Logic.Clusters.Mode.Editable;
                        if (flag)
                        {
                            msg.Interactions = new int[]
                            {
                                (int)InteractionData.Interaction.Collect
                            };

                            Collectible collectible = default(Collectible);
                            collectible.EntityId = touch.EntityId;
                            collectible.CollectibleId = collectibleId;

                            Collectible? cachedCollectible = DatePalmReflection.GetCollectedFrom(__instance, touch.Tile.ToString());
                            bool cachedMatches =
                                cachedCollectible != null &&
                                string.Equals(
                                    cachedCollectible.Value.CollectibleId,
                                    collectibleId,
                                    StringComparison.Ordinal);
                            bool cachedGeneratorsValid =
                                cachedMatches &&
                                (isTamedRoot
                                    ? UnstableSavannaSeaDatabase
                                        .HasOnlyTamedRootGenerator(
                                            cachedCollectible.Value
                                                .Generators)
                                    : UnstableSavannaSeaDatabase
                                        .HasOnlyExpectedGenerators(
                                            collectibleId,
                                            cachedCollectible.Value
                                                .Generators));
                            List<Generator> generators =
                                cachedGeneratorsValid
                                    ? new List<Generator>(
                                        cachedCollectible.Value.Generators)
                                    : (isTamedRoot
                                        ? UnstableSavannaSeaDatabase
                                            .CreateTamedRootGenerators()
                                        : UnstableSavannaSeaDatabase
                                            .CreateGenerators(
                                                collectibleId));
                            GatheringMechanics
                                .ApplyGeneratorSkillAvailability(
                                    __instance,
                                    collectibleId,
                                    generators);
                            if (cachedMatches)
                            {
                                collectible = cachedCollectible.Value;
                            }
                            collectible.EntityId = touch.EntityId;
                            collectible.CollectibleId = collectibleId;
                            collectible.Generators =
                                generators.ToArray();

                            collectible.Size =
                                UnstableSavannaSeaDatabase.CollectibleSizeOf(
                                    collectibleId);
                            collectible.CriticalGenerator =
                                isTamedRoot
                                    ? "root"
                                    : UnstableSavannaSeaDatabase
                                        .CriticalGenerator(
                                            collectibleId,
                                            collectible.Generators);
                            DatePalmReflection.SetCollectedFrom(
                                __instance,
                                touch.Tile.ToString(),
                                collectible);
                            msg.Collectible = collectible;
                        }
                        else
                        {
                            msg.Interactions = new int[0];
                        }

                        msg.DisabledInteractions = new int[0];
                        msg.AccessDeniedInteractions = new int[0];

                        SendTouchedDelayed(
                            __instance,
                            msg,
                            seq);

                        return false; // Bypass stock offline touch for restored resources
                    }
                }
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in HandleTouchMsgPrefix: " + ex);
            }

            return true;
        }

        private static void SendTouchedDelayed(
            Durango.Offline.Player player,
            Touched touched,
            uint seq)
        {
            System.Timers.Timer responseTimer =
                new System.Timers.Timer
                {
                    Interval = NaturalTouchResponseDelayMs,
                    Enabled = false,
                    AutoReset = false
                };
            responseTimer.Elapsed += delegate(
                object sender,
                System.Timers.ElapsedEventArgs args)
            {
                try
                {
                    MethodInfo onContextChanged =
                        typeof(Durango.Offline.Player).GetMethod(
                            "OnContextChanged",
                            BindingFlags.Instance |
                            BindingFlags.NonPublic |
                            BindingFlags.Public);
                    if (onContextChanged != null)
                    {
                        onContextChanged.Invoke(player, null);
                    }

                    GatheringOutboundQueue.Send<Touched>(
                        player,
                        touched,
                        seq);
                }
                catch (Exception ex)
                {
                    GatheringPlugin.Log.LogError(
                        "Delayed natural touch response failed: " +
                        ex);
                }
                finally
                {
                    responseTimer.Dispose();
                }
            };
            responseTimer.Start();
        }

        private static void HandleCollectOriginal(Durango.Offline.Player player, Durango.Offline.World world, Messages.Collect msg, Durango.Network.PacketHeader header)
        {
            try
            {
                GatheringPlugin.Log.LogInfo("[Collect] start gen=" + msg.GeneratorId + " tile=" + msg.Tile);
                Collectible? col = DatePalmReflection.GetCollectedFrom(player, msg.Tile.ToString());
                if (col == null ||
                    !UnstableSavannaSeaDatabase.IsHandled(
                        col.Value.CollectibleId))
                {
                    return;
                }

                Generator generator = default(Generator);
                bool foundGenerator = false;
                if (col.Value.Generators != null)
                {
                    for (int i = 0; i < col.Value.Generators.Length; i++)
                    {
                        if (string.Equals(
                            col.Value.Generators[i].Id,
                            msg.GeneratorId,
                            StringComparison.Ordinal))
                        {
                            generator = col.Value.Generators[i];
                            foundGenerator = true;
                            break;
                        }
                    }
                }
                if (!foundGenerator || generator.Amount <= 0)
                {
                    return;
                }

                GatheringAttempt attempt =
                    GatheringMechanics.CreateAttempt(
                        player,
                        col.Value.CollectibleId,
                        generator,
                        msg.ToolItemId);

                if (!attempt.ToolValid)
                {
                    player.Send<ToolNeeded>(new ToolNeeded
                    {
                        RecipeIds = new string[0],
                        Skills = new Dictionary<string, Messages.Skill>(),
                        TagNames = string.Join(
                            ", ",
                            new List<string>(
                                attempt.RequiredTools.Keys).ToArray()),
                        Tags = attempt.RequiredTools
                    }, header.Seq);
                    return;
                }

                if (!attempt.SkillValid)
                {
                    player.Send<SkillNeeded>(new SkillNeeded
                    {
                        SkillId = attempt.RequiredSkillId,
                        SubId = "__base__",
                        Level = attempt.RequiredSkillLevel
                    }, header.Seq);
                    return;
                }

                DatePalmCollectState.Mark(player, msg.GeneratorId);

                List<Item> list = new List<Item>();
                Result result = attempt.Result;
                int gatheredItemLevel = Math.Max(
                    1,
                    Math.Min(generator.Level, attempt.CategoryLevel));
                if (result != Result.BigFailure)
                {
                    string actualProtoId =
                        UnstableSavannaSeaDatabase.ResolvePrototypeId(
                            col.Value.CollectibleId,
                            msg.GeneratorId);
                    Item? item = Durango.Offline.Cheats.MakeItem(
                        actualProtoId,
                        gatheredItemLevel);
                    if (item != null)
                    {
                        Item itemVal = item.Value;
                        itemVal = GatheringMechanics.PrepareLostPackageItem(
                            itemVal,
                            col.Value.CollectibleId);
                        string generatorIcon =
                            UnstableSavannaSeaDatabase.GeneratorIcon(
                                col.Value.CollectibleId,
                                msg.GeneratorId);
                        if (!string.IsNullOrEmpty(generatorIcon))
                        {
                            itemVal.Icon = generatorIcon;
                        }
                        if (col.Value.Generators != null)
                        {
                            int genIndex = Array.FindIndex(col.Value.Generators, delegate(Generator o) { return o.Id == msg.GeneratorId; });
                            if (genIndex != -1)
                            {
                                itemVal.Name = col.Value.Generators[genIndex].Name;
                            }
                        }
                        itemVal = GatheringMechanics.ApplyGatheredItemData(
                            itemVal,
                            col.Value.CollectibleId,
                            msg.GeneratorId,
                            result,
                            attempt.RandomAttributeRatio,
                            attempt.CategoryLevel);
                        list.Add(itemVal);
                    }
                }

                ClientPatches.CacheActualGatheringDuration(
                    ClientPatches.MakeTileTargetKey(
                        msg.Tile.x,
                        msg.Tile.y),
                    msg.GeneratorId,
                    attempt.Tool == null
                        ? string.Empty
                        : attempt.Tool.Value.Id,
                    attempt.Duration);

                player.Send<Messages.Timer>(new Messages.Timer
                {
                    Duration = attempt.Duration
                }, header.Seq);

                var delayTimer = new System.Timers.Timer
                {
                    Interval = Math.Max(
                        100.0,
                        attempt.Duration * 1000.0),
                    Enabled = true,
                    AutoReset = false
                };

                DatePalmCollectState.RegisterSession(player, delayTimer, header.Seq);

                delayTimer.Elapsed += delegate(object sender, System.Timers.ElapsedEventArgs args)
                {
                    bool completionQueued = false;
                    try
                    {
                        GatheringPlugin.Log.LogInfo("[Elapsed] fired gen=" + msg.GeneratorId + " tile=" + msg.Tile);
                        if (!DatePalmCollectState.IsActiveTimer(player, delayTimer))
                        {
                            GatheringPlugin.Log.LogInfo("Timer session inactive, aborting.");
                            return;
                        }

                        DatePalmCollectState.RemoveSession(player, delayTimer);
                        Collectible? currentCol = DatePalmReflection.GetCollectedFrom(player, msg.Tile.ToString());
                        if (currentCol == null)
                        {
                            GatheringPlugin.Log.LogError("Collectible state vanished.");
                            GatheringOutboundQueue.Send<Messages.Collected>(
                                player,
                                new Messages.Collected
                                {
                                    Items = new Item[0],
                                    Result = Result.BigFailure,
                                    ActionInfo = attempt.ActionInfo,
                                    RanOut = false
                                },
                                header.Seq);
                            completionQueued = true;
                            DatePalmCollectState.Consume(player);
                            return;
                        }

                        List<Generator> genList = new List<Generator>();
                        if (currentCol.Value.Generators != null)
                        {
                            genList.AddRange(currentCol.Value.Generators);
                        }

                        bool criticalRanOut = false;
                        int index = genList.FindIndex(delegate(Generator o) { return o.Id == msg.GeneratorId; });
                        if (index != -1)
                        {
                            Generator gen = genList[index];
                            gen.Amount = gen.Amount - 1;
                            if (gen.Amount <= 0)
                            {
                                criticalRanOut = string.Equals(
                                    gen.Id,
                                    currentCol.Value.CriticalGenerator,
                                    StringComparison.Ordinal);
                                genList.RemoveAt(index);
                            }
                            else
                            {
                                genList[index] = gen;
                            }
                        }

                        bool ranOut =
                            criticalRanOut ||
                            genList.Count == 0;
                        Collectible updatedCol = currentCol.Value;
                        updatedCol.Generators = genList.ToArray();
                        DatePalmReflection.SetCollectedFrom(player, msg.Tile.ToString(), updatedCol);
                        GatheringWorldPersistence.MarkDirty(world);

                        Item[] gatheredItems = list.ToArray();
                        Messages.Collected collected = default(Messages.Collected);
                        collected.Items = gatheredItems;
                        collected.Result = result;
                        collected.ActionInfo = attempt.ActionInfo;
                        collected.RanOut = ranOut;
                        GatheringOutboundQueue.Send<Messages.Collected>(
                            player,
                            collected,
                            header.Seq);
                        completionQueued = true;

                        if (gatheredItems.Length > 0)
                        {
                            GatheringOutboundQueue.Enqueue(
                                player,
                                "Inventory item commit/update",
                                delegate()
                                {
                                    player.AddItems(list);
                                    player.Send<InventoryUpdated>(
                                        new InventoryUpdated
                                        {
                                            EntityId = player.EntityId,
                                            Items = gatheredItems
                                        },
                                        0U);
                                });
                        }

                        // Finish the Collect request before refreshing the tool.
                        // Updating a locked BestTool while GatheringSystem is still
                        // active can make the client cancel/re-enter the request.
                        if (attempt.Tool != null)
                        {
                            GatheringOutboundQueue.Enqueue(
                                player,
                                "Tool durability/update",
                                delegate()
                                {
                                    Item? wornTool =
                                        GatheringMechanics.ReduceToolDurability(
                                            player,
                                            attempt.Tool,
                                            0.1f);
                                    if (wornTool != null)
                                    {
                                        player.Send<InventoryUpdated>(
                                            new InventoryUpdated
                                            {
                                                EntityId = player.EntityId,
                                                Items = new Item[]
                                                {
                                                    wornTool.Value
                                                }
                                            },
                                            0U);
                                    }
                                });
                        }

                        if (ranOut)
                        {
                            GatheringOutboundQueue.Enqueue(
                                player,
                                "Destroy natural object",
                                delegate()
                                {
                                    world.DestroyNatural(msg.Tile);
                                });
                        }

                        Messages.CollectibleChanged changed =
                            default(Messages.CollectibleChanged);
                        changed.EntityId = msg.EntityId;
                        GatheringOutboundQueue.Send<Messages.CollectibleChanged>(
                            player,
                            changed,
                            0U);

                        string consumedGenerator = DatePalmCollectState.Consume(player);
                        bool rewardExperience =
                            UnstableSavannaSeaDatabase.IsRewardGenerator(
                                consumedGenerator) &&
                            gatheredItems.Length > 0 &&
                            result != Result.BigFailure &&
                            result != Result.Invalid;
                        GatheringOutboundQueue.Enqueue(
                            player,
                            "Character and Gathering XP",
                            delegate()
                            {
                                if (rewardExperience)
                                {
                                    DatePalmXpBridge.AddLevelXp(
                                        gatheredItemLevel);
                                    GatheringMechanics.AwardGatheringExperience(
                                        result,
                                        gatheredItemLevel,
                                        attempt.GatheringAbility);
                                }

                                GatheringMechanics.SavePlayerAfterGathering(
                                    player);
                                GatheringWorldPersistence.SaveNow(world);
                            });
                    }
                    catch (Exception ex)
                    {
                        GatheringPlugin.Log.LogError("Error in HandleCollectOriginal timer: " + ex);
                        if (!completionQueued)
                        {
                            GatheringOutboundQueue.Send<Messages.Collected>(
                                player,
                                new Messages.Collected
                                {
                                    Items = new Item[0],
                                    Result = Result.BigFailure,
                                    ActionInfo = attempt.ActionInfo,
                                    RanOut = false
                                },
                                header.Seq);
                            DatePalmCollectState.Consume(player);
                        }
                    }
                    finally
                    {
                        delayTimer.Dispose();
                    }
                };
            }
            catch (Exception ex)
            {
                GatheringPlugin.Log.LogError("Error in HandleCollectOriginal: " + ex);
            }
        }

    }
}
