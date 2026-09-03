using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using Durango.Network;
using Durango.Offline;
using HarmonyLib;
using Messages;

namespace BaoX.DurangoOriginal.InventoryLockMod
{
    [BepInPlugin(
        "com.baominix.durango.original.inventorylock",
        "InventoryLockPlugin",
        "0.1.1")]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class InventoryLockPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(
                "com.baominix.durango.original.inventorylock");

            Type playerType = AccessTools.TypeByName(
                "Durango.Offline.Player");
            if (playerType == null)
            {
                Logger.LogError("Durango.Offline.Player was not found.");
                return;
            }

            ConstructorInfo constructor = playerType.GetConstructor(
                new Type[]
                {
                    typeof(string),
                    typeof(Durango.Offline.Connection),
                    typeof(Durango.Offline.World),
                    typeof(Durango.Offline.PlayerContext),
                    typeof(bool)
                });
            if (constructor == null)
            {
                Logger.LogError("Offline Player constructor was not found.");
                return;
            }

            _harmony.Patch(
                constructor,
                null,
                new HarmonyMethod(
                    typeof(InventoryLockPatches).GetMethod(
                        "PlayerConstructorPostfix")),
                null,
                null,
                null);

            MethodInfo sendInventory = playerType.GetMethod(
                "SendInventory",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (sendInventory != null)
            {
                _harmony.Patch(
                    sendInventory,
                    null,
                    new HarmonyMethod(
                        typeof(InventoryLockPatches).GetMethod(
                            "SendInventoryPostfix")),
                    null,
                    null,
                    null);
            }
            else
            {
                Logger.LogWarning("Offline Player.SendInventory was not found.");
            }

            MethodInfo process = playerType.GetMethod(
                "Process",
                BindingFlags.Instance | BindingFlags.Public);
            if (process != null)
            {
                _harmony.Patch(
                    process,
                    null,
                    new HarmonyMethod(
                        typeof(InventoryLockPatches).GetMethod(
                            "PlayerProcessPostfix")),
                    null,
                    null,
                    null);
            }
            else
            {
                Logger.LogWarning("Offline Player.Process was not found.");
            }

            Logger.LogInfo(
                "Inventory lock persistence, offline message handling, and " +
                "delayed inventory resync enabled.");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }

    internal static class InventoryLockPatches
    {
        private const string StorageKey =
            "inventory_lock_plugin.locked_item_ids";

        private const int ResyncDelayTicks = 45;
        private const int ResyncBatchSize = 8;

        private sealed class InventoryResyncState
        {
            public int DelayTicks;
            public Item[] Items;
            public int NextIndex;
            public bool ClearedClientInventory;
        }

        private static readonly object ResyncLock = new object();

        private static readonly Dictionary<Durango.Offline.Player,
            InventoryResyncState> PendingResyncs =
                new Dictionary<Durango.Offline.Player,
                    InventoryResyncState>();

        private static readonly FieldInfo ContextField =
            AccessTools.Field(typeof(Durango.Offline.Player), "_context");

        public static void PlayerConstructorPostfix(
            Durango.Offline.Player __instance,
            Durango.Offline.Connection connection)
        {
            try
            {
                connection.Recv<LockOrUnlockItems>(
                    delegate(LockOrUnlockItems message, PacketHeader header)
                    {
                        HandleLockOrUnlock(
                            __instance,
                            message,
                            header.Seq);
                    });

                if (__instance.IsLocalPlayer)
                {
                    ScheduleInventoryResync(__instance);
                    __instance.Closed += delegate
                    {
                        CancelInventoryResync(__instance);
                    };
                }
            }
            catch (Exception ex)
            {
                InventoryLockPlugin.Log.LogError(
                    "Failed to register LockOrUnlockItems handler: " + ex);
            }
        }

        public static void PlayerProcessPostfix(
            Durango.Offline.Player __instance)
        {
            if (__instance == null || !__instance.IsLocalPlayer)
            {
                return;
            }

            try
            {
                ProcessInventoryResync(__instance);
            }
            catch (Exception ex)
            {
                CancelInventoryResync(__instance);
                InventoryLockPlugin.Log.LogWarning(
                    "Delayed inventory resync failed: " + ex);
            }
        }

        public static void SendInventoryPostfix(
            Durango.Offline.Player __instance)
        {
            try
            {
                PlayerContext context = GetContext(__instance);
                if (context == null)
                {
                    return;
                }

                HashSet<string> lockedIds = LoadLockedIds(context);
                bool changed = PruneMissingItems(context, lockedIds);
                if (changed)
                {
                    SaveLockedIds(context, lockedIds);
                }

                SendInventoryInfo(__instance, lockedIds);
            }
            catch (Exception ex)
            {
                InventoryLockPlugin.Log.LogWarning(
                    "Failed to restore inventory locks: " + ex);
            }
        }

        private static void HandleLockOrUnlock(
            Durango.Offline.Player player,
            LockOrUnlockItems message,
            uint sequence)
        {
            try
            {
                PlayerContext context = GetContext(player);
                if (context == null)
                {
                    player.Send<Abort>(default(Abort), sequence);
                    return;
                }

                HashSet<string> ownedItemIds = GetOwnedItemIds(context);
                HashSet<string> lockedIds = LoadLockedIds(context);
                PruneMissingItems(context, lockedIds);

                int changedCount = 0;
                if (message.ItemIds != null)
                {
                    for (int i = 0; i < message.ItemIds.Length; i++)
                    {
                        string itemId = message.ItemIds[i];
                        if (string.IsNullOrEmpty(itemId) ||
                            !ownedItemIds.Contains(itemId))
                        {
                            continue;
                        }

                        bool changed = message.Lock
                            ? lockedIds.Add(itemId)
                            : lockedIds.Remove(itemId);
                        if (changed)
                        {
                            changedCount++;
                        }
                    }
                }

                SaveLockedIds(context, lockedIds);
                player.Send<OK>(default(OK), sequence);
                SendInventoryInfo(player, lockedIds);

                InventoryLockPlugin.Log.LogInfo(
                    (message.Lock ? "Locked " : "Unlocked ") +
                    changedCount + " inventory item(s). Total locked: " +
                    lockedIds.Count);
            }
            catch (Exception ex)
            {
                InventoryLockPlugin.Log.LogError(
                    "LockOrUnlockItems failed: " + ex);
                try
                {
                    player.Send<Abort>(default(Abort), sequence);
                }
                catch
                {
                }
            }
        }

        private static PlayerContext GetContext(
            Durango.Offline.Player player)
        {
            if (player == null || ContextField == null)
            {
                return null;
            }

            return ContextField.GetValue(player) as PlayerContext;
        }

        private static void ScheduleInventoryResync(
            Durango.Offline.Player player)
        {
            lock (ResyncLock)
            {
                PendingResyncs[player] = new InventoryResyncState
                {
                    DelayTicks = ResyncDelayTicks,
                    Items = null,
                    NextIndex = 0,
                    ClearedClientInventory = false
                };
            }
        }

        private static void CancelInventoryResync(
            Durango.Offline.Player player)
        {
            lock (ResyncLock)
            {
                PendingResyncs.Remove(player);
            }
        }

        private static void ProcessInventoryResync(
            Durango.Offline.Player player)
        {
            InventoryResyncState state;
            lock (ResyncLock)
            {
                if (!PendingResyncs.TryGetValue(player, out state))
                {
                    return;
                }

                if (state.DelayTicks > 0)
                {
                    state.DelayTicks--;
                    return;
                }
            }

            PlayerContext context = GetContext(player);
            if (context == null)
            {
                CancelInventoryResync(player);
                return;
            }

            if (state.Items == null)
            {
                state.Items = context.InventoryItems == null
                    ? new Item[0]
                    : context.InventoryItems.ToArray();
            }

            if (!state.ClearedClientInventory)
            {
                Messages.InventoryItems reset =
                    default(Messages.InventoryItems);
                reset.EntityId = player.EntityId;
                reset.Items = new Item[0];
                player.Send<Messages.InventoryItems>(reset, 0U);
                state.ClearedClientInventory = true;
                return;
            }

            if (state.NextIndex < state.Items.Length)
            {
                int count = Math.Min(
                    ResyncBatchSize,
                    state.Items.Length - state.NextIndex);
                Item[] batch = new Item[count];
                Array.Copy(state.Items, state.NextIndex, batch, 0, count);

                InventoryUpdated update = default(InventoryUpdated);
                update.EntityId = player.EntityId;
                update.Items = batch;
                update.RemovedItemIds = new string[0];
                player.Send<InventoryUpdated>(update, 0U);
                state.NextIndex += count;
                return;
            }

            HashSet<string> lockedIds = LoadLockedIds(context);
            PruneMissingItems(context, lockedIds);
            SendInventoryInfo(player, lockedIds);
            CancelInventoryResync(player);

            InventoryLockPlugin.Log.LogInfo(
                "Resynced " + state.Items.Length +
                " inventory item(s) after entering the world.");
        }

        private static HashSet<string> GetOwnedItemIds(
            PlayerContext context)
        {
            HashSet<string> result = new HashSet<string>(
                StringComparer.Ordinal);
            if (context.InventoryItems == null)
            {
                return result;
            }

            for (int i = 0; i < context.InventoryItems.Count; i++)
            {
                string itemId = context.InventoryItems[i].Id;
                if (!string.IsNullOrEmpty(itemId))
                {
                    result.Add(itemId);
                }
            }

            return result;
        }

        private static HashSet<string> LoadLockedIds(
            PlayerContext context)
        {
            HashSet<string> result = new HashSet<string>(
                StringComparer.Ordinal);
            if (context.Storage == null)
            {
                context.Storage = new Dictionary<string, byte[]>();
                return result;
            }

            byte[] data;
            if (!context.Storage.TryGetValue(StorageKey, out data) ||
                data == null ||
                data.Length == 0)
            {
                return result;
            }

            string value = Encoding.UTF8.GetString(data);
            string[] itemIds = value.Split(
                new char[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < itemIds.Length; i++)
            {
                string itemId = itemIds[i].Trim();
                if (!string.IsNullOrEmpty(itemId))
                {
                    result.Add(itemId);
                }
            }

            return result;
        }

        private static void SaveLockedIds(
            PlayerContext context,
            HashSet<string> lockedIds)
        {
            if (context.Storage == null)
            {
                context.Storage = new Dictionary<string, byte[]>();
            }

            string[] values = new string[lockedIds.Count];
            lockedIds.CopyTo(values);
            Array.Sort(values, StringComparer.Ordinal);
            context.Storage[StorageKey] = Encoding.UTF8.GetBytes(
                string.Join("\n", values));
            context.Save();
        }

        private static bool PruneMissingItems(
            PlayerContext context,
            HashSet<string> lockedIds)
        {
            HashSet<string> ownedItemIds = GetOwnedItemIds(context);
            List<string> missing = new List<string>();
            foreach (string lockedId in lockedIds)
            {
                if (!ownedItemIds.Contains(lockedId))
                {
                    missing.Add(lockedId);
                }
            }

            for (int i = 0; i < missing.Count; i++)
            {
                lockedIds.Remove(missing[i]);
            }

            return missing.Count > 0;
        }

        private static void SendInventoryInfo(
            Durango.Offline.Player player,
            HashSet<string> lockedIds)
        {
            string[] values = new string[lockedIds.Count];
            lockedIds.CopyTo(values);
            Array.Sort(values, StringComparer.Ordinal);

            InventoryInfos info = default(InventoryInfos);
            info.EntityId = player.EntityId;
            info.MaxSize = 200;
            info.LockedItemIds = values;
            info.ItemOrder = new string[0];
            info.ProtectedItems.ItemIds = new string[0];
            player.Send<InventoryInfos>(info, 0U);
        }
    }
}
