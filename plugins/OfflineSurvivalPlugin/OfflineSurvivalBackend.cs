using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Network;
using Durango.Offline;
using HarmonyLib;
using Messages;

namespace BaoX.DurangoOriginal.OfflineSurvivalMod
{
    /// <summary>
    /// Server-side handlers for survival / revive messages. Registered from
    /// <see cref="ConstructorPostfix"/> on every new <c>Durango.Offline.Player</c>.
    /// </summary>
    internal static class OfflineSurvivalBackend
    {
        // -------- tunables (mirror the real production server's timing) --------
        private const float DrinkWaterDuration = 3f;
        private const float WashBodyDuration   = 4f;
        private const float DrawWaterDuration  = 5f;
        private const float ReviveDuration     = 5f;
        private const float ResurrectDuration  = 8f;  // CPR / rescue takes longer
        private const float ResurrectPetDuration = 5f;
        // ReviveImmediately intentionally has no timer (instant revive).

        // -------- reflection cache (private fields we need to reach) --------
        private static readonly Type PlayerType          = typeof(Durango.Offline.Player);
        private static readonly Type PlayerContextType   = typeof(Durango.Offline.PlayerContext);
        private static readonly Type WorldType           = typeof(Durango.Offline.World);

        private static readonly FieldInfo WorldField    = AccessTools.Field(PlayerType, "_world");
        private static readonly FieldInfo ContextField  = AccessTools.Field(PlayerType, "_context");
        private static readonly MethodInfo ContextChangedMethod = PlayerType.GetMethod(
            "OnContextChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        // -------- entry point: constructor postfix --------
        public static void ConstructorPostfix(Durango.Offline.Player __instance, Durango.Offline.Connection connection)
        {
            // DrinkWater (TypeCode 3492) — empty struct, no payload.
            connection.Recv<DrinkWater>(delegate(DrinkWater message, PacketHeader header)
            {
                try
                {
                    SendTimer(__instance, DrinkWaterDuration, header.Seq);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("DrinkWater failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            // WashBody (TypeCode 3494) — empty struct, no payload.
            connection.Recv<WashBody>(delegate(WashBody message, PacketHeader header)
            {
                try
                {
                    SendTimer(__instance, WashBodyDuration, header.Seq);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("WashBody failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            // DrawWater (TypeCode 3493) — ToolItemId, player must own a container.
            connection.Recv<DrawWater>(delegate(DrawWater message, PacketHeader header)
            {
                try
                {
                    PlayerContext ctx = GetContext(__instance);
                    if (ctx == null || ctx.InventoryItems == null)
                    {
                        __instance.Send<Abort>(default(Abort), header.Seq);
                        return;
                    }

                    int index = ctx.InventoryItems.FindIndex(delegate(Item candidate) { return candidate.Id == message.ToolItemId; });
                    if (index < 0)
                    {
                        OfflineSurvivalPlugin.Log.LogInfo("DrawWater rejected: ToolItemId '" + (message.ToolItemId ?? "<null>") + "' not in inventory.");
                        __instance.Send<Abort>(default(Abort), header.Seq);
                        return;
                    }

                    SendTimer(__instance, DrawWaterDuration, header.Seq);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("DrawWater failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            // Revive (TypeCode 2101) — WarpholeTile? optional. After timer, broadcast EntityRevived.
            connection.Recv<Revive>(delegate(Revive message, PacketHeader header)
            {
                try
                {
                    SendTimer(__instance, ReviveDuration, header.Seq);
                    ScheduleRevive(__instance, __instance.EntityId, ReviveDuration);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("Revive failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            // Resurrect (TypeCode 132) — rescue a different player (CPR / food / water).
            connection.Recv<Resurrect>(delegate(Resurrect message, PacketHeader header)
            {
                try
                {
                    string targetId = string.IsNullOrEmpty(message.EntityId) ? __instance.EntityId : message.EntityId;
                    SendTimer(__instance, ResurrectDuration, header.Seq);
                    ScheduleRevive(__instance, targetId, ResurrectDuration);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("Resurrect failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            // ResurrectPet (TypeCode 239187) — revive a dead pet / vehicle.
            connection.Recv<ResurrectPet>(delegate(ResurrectPet message, PacketHeader header)
            {
                try
                {
                    string petId = message.PetId;
                    if (string.IsNullOrEmpty(petId))
                    {
                        __instance.Send<Abort>(default(Abort), header.Seq);
                        return;
                    }
                    SendTimer(__instance, ResurrectPetDuration, header.Seq);
                    ScheduleRevive(__instance, petId, ResurrectPetDuration);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("ResurrectPet failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });

            // ReviveImmediately (TypeCode 210201) — skip timer, broadcast immediately.
            connection.Recv<ReviveImmediately>(delegate(ReviveImmediately message, PacketHeader header)
            {
                try
                {
                    BroadcastEntityRevived(__instance, __instance.EntityId);
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogError("ReviveImmediately failed: " + ex);
                    __instance.Send<Abort>(default(Abort), header.Seq);
                }
            });
        }

        // -------- helpers --------

        private static void SendTimer(Durango.Offline.Player player, float duration, uint replyOf)
        {
            // client: InteractionSystem plays the PredictTimer using msg.Duration, then
            // *On*() handler chains keep the bar accurate.
            player.Send<Messages.Timer>(new Messages.Timer { Duration = duration }, replyOf);
        }

        private static void ScheduleRevive(Durango.Offline.Player player, string entityId, float delay)
        {
            if (string.IsNullOrEmpty(entityId)) entityId = player.EntityId;
            Durango.Offline.World world = GetWorld(player);

            OfflineSurvivalPlugin.Schedule(delay, delegate
            {
                if (world == null)
                {
                    // World already gone — just notify the local player.
                    double at = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    player.Send<EntityRevived>(new EntityRevived
                    {
                        EntityId = entityId,
                        At = at
                    }, 0u);
                    return;
                }

                BroadcastEntityRevived(player, entityId);
                OfflineSurvivalPlugin.Log.LogInfo("EntityRevived broadcast for " + entityId);
            });
        }

        private static void BroadcastEntityRevived(Durango.Offline.Player player, string entityId)
        {
            // .NET 3.5 doesn't have DateTimeOffset.ToUnixTimeMilliseconds — compute
            // seconds-since-epoch manually. Matches the wall-clock the client uses
            // in ObjectManager.SetEntityAlive.
            double at = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

            EntityRevived msg = new EntityRevived
            {
                EntityId = entityId,
                At = at
            };

            // Best-effort: try world.BroadCast (multi-player offline), fall back to
            // just sending to the local player. World.BroadCast iterates all players
            // and forwards to each Player.Send, which is exactly what we want.
            Durango.Offline.World world = GetWorld(player);
            if (world != null)
            {
                try
                {
                    MethodInfo broadCast = WorldType.GetMethod("BroadCast");
                    if (broadCast != null)
                    {
                        broadCast.MakeGenericMethod(typeof(EntityRevived)).Invoke(world, new object[] { msg });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    OfflineSurvivalPlugin.Log.LogWarning("World.BroadCast failed, falling back to Player.Send: " + ex.Message);
                }
            }

            // Note: the Send<T>(T, uint) default value for `replyOf` is not visible
            // to csc 3.5 in the Assembly-CSharp metadata, so pass 0u explicitly.
            player.Send<EntityRevived>(msg, 0u);
        }

        private static Durango.Offline.World GetWorld(Durango.Offline.Player player)
        {
            return WorldField == null ? null : WorldField.GetValue(player) as Durango.Offline.World;
        }

        private static PlayerContext GetContext(Durango.Offline.Player player)
        {
            return ContextField == null ? null : ContextField.GetValue(player) as PlayerContext;
        }
    }
}
