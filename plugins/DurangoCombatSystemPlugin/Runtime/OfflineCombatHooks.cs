using System;
using Durango.Logic.Combat;
using Durango.Network;
using HarmonyLib;
using Messages;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    [HarmonyPatch(
        typeof(Durango.Offline.Player),
        MethodType.Constructor,
        new Type[]
        {
            typeof(string),
            typeof(Durango.Offline.Connection),
            typeof(Durango.Offline.World),
            typeof(Durango.Offline.PlayerContext),
            typeof(bool)
        })]
    internal static class OfflinePlayerCombatRegistrationPatch
    {
        private static void Postfix(
            Durango.Offline.Player __instance,
            Durango.Offline.Connection connection,
            Durango.Offline.PlayerContext context,
            bool isLocalPlayer)
        {
            if (!isLocalPlayer || __instance == null || connection == null)
            {
                return;
            }

            OfflineCombatRuntime.Register(__instance, connection, context);
            BrachioLootRuntime.Register(__instance, connection);

            connection.Recv<GetActions>(delegate(GetActions message, PacketHeader header)
            {
                OfflineCombatRuntime.RequestActions(__instance, header.Seq);
            });

            connection.Recv<UseBattleAction>(delegate(UseBattleAction message, PacketHeader header)
            {
                OfflineCombatRuntime.UseAction(__instance, message);
            });

            connection.Recv<ExitBattle>(delegate(ExitBattle message, PacketHeader header)
            {
                OfflineCombatRuntime.EndCombat(__instance, "ExitBattle");
            });

            connection.Recv<ReviveImmediately>(delegate(ReviveImmediately message, PacketHeader header)
            {
                OfflineCombatRuntime.ReviveImmediately(__instance, message, header.Seq);
            });
        }
    }

    [HarmonyPatch(typeof(CombatSystem), "SelectTarget", new Type[] { typeof(DamageableEntity) })]
    internal static class CombatTargetBeginsOfflineBattlePatch
    {
        private static void Postfix(DamageableEntity target)
        {
            if (target == null || target.GameObject == null ||
                target.GameObject.GetComponent<AnimalBehavior>() == null)
            {
                return;
            }

            OfflineCombatRuntime.BeginCombat(target.GetEntityId());
        }
    }
}
