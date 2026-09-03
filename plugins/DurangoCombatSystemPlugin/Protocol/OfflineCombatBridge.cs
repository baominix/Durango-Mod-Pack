using System;
using Baominix.DurangoOriginal.CombatSystem.Runtime;
using Durango.Network;
using Durango.Offline;
using HarmonyLib;
using Messages;
using OfflineConnection = Durango.Offline.Connection;

namespace Baominix.DurangoOriginal.CombatSystem.Protocol
{
    internal static class OfflineCombatBridge
    {
        internal static void Register(
            Player player,
            OfflineConnection connection,
            World world,
            PlayerContext context,
            bool isLocalPlayer)
        {
            if (!isLocalPlayer || player == null ||
                connection == null || world == null || context == null)
            {
                return;
            }

            if (ConnectionHandlerInspector.HasHandler(
                    connection, GetActions.TypeCode) ||
                ConnectionHandlerInspector.HasHandler(
                    connection, UseBattleAction.TypeCode))
            {
                DurangoCombatSystemPlugin.Log.LogError(
                    "Combat protocol ownership conflict detected. " +
                    "GetActions/UseBattleAction handlers were not installed.");
                return;
            }

            OfflineCombatSession session =
                CombatRuntime.Bind(player, connection, world, context);
            bool replacedGetActions = connection.Recv<GetActions>(
                delegate(GetActions message, PacketHeader header)
                {
                    session.HandleGetActions(message, header);
                });
            bool replacedUseAction = connection.Recv<UseBattleAction>(
                delegate(UseBattleAction message, PacketHeader header)
                {
                    session.HandleUseBattleAction(message, header);
                });

            if (replacedGetActions || replacedUseAction)
            {
                session.Dispose();
                DurangoCombatSystemPlugin.Log.LogError(
                    "Combat protocol ownership changed during registration. " +
                    "The new combat session was disabled.");
                return;
            }

            DurangoCombatSystemPlugin.Log.LogInfo(
                "Offline combat protocol bridge installed for player=" +
                player.EntityId + " generation=" + session.Generation + ".");
        }
    }

    [HarmonyPatch(
        typeof(Player),
        MethodType.Constructor,
        new Type[]
        {
            typeof(string),
            typeof(OfflineConnection),
            typeof(World),
            typeof(PlayerContext),
            typeof(bool)
        })]
    internal static class OfflinePlayerConstructorPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(
            Player __instance,
            OfflineConnection connection,
            World world,
            PlayerContext context,
            bool isLocalPlayer)
        {
            if (DurangoCombatSystemPlugin.Enabled == null ||
                !DurangoCombatSystemPlugin.Enabled.Value)
            {
                return;
            }

            try
            {
                OfflineCombatBridge.Register(
                    __instance,
                    connection,
                    world,
                    context,
                    isLocalPlayer);
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogError(
                    "Failed to install offline combat protocol bridge: " +
                    exception);
            }
        }
    }
}
