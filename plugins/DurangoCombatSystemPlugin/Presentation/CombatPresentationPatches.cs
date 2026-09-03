using System;
using System.Collections.Generic;
using Baominix.DurangoOriginal.CombatSystem.Geometry;
using Durango.Logic.Combat;
using Durango.Terrain;
using Durango.UI.InGame;
using HarmonyLib;
using Messages;

namespace Baominix.DurangoOriginal.CombatSystem.Presentation
{
    internal static class PlayerAttackTelegraph
    {
        private static readonly Dictionary<string, int> Active =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal static bool IsLocalPlayer(AttackAlerted alert)
        {
            string entityId = alert.EntityId;
            return !string.IsNullOrEmpty(entityId) &&
                (string.Equals(
                    entityId,
                    GameManager.PlayerId,
                    StringComparison.Ordinal) ||
                 (PlayerBehavior.LocalPlayer != null &&
                  string.Equals(
                    entityId,
                    PlayerBehavior.LocalPlayer.EntityId,
                    StringComparison.Ordinal)));
        }

        internal static bool IsPluginOwned(AttackAlerted alert)
        {
            return IsLocalPlayer(alert);
        }

        internal static void Show(AttackSnapshot attack)
        {
            if (attack == null ||
                attack.DamageType == Shared.Battle.DamageType.Melee ||
                attack.DamageType == Shared.Battle.DamageType.Ranged ||
                !global::CombatSystem.AttackAlertEnabled)
            {
                return;
            }

            string key = MakeKey(attack);
            int previous;
            if (Active.TryGetValue(key, out previous))
            {
                AreaOfEffectVisualizer.Stop(previous, 0f);
                Active.Remove(key);
            }

            int id = AreaOfEffectVisualizer.ShowAttackAlerted(
                AreaOfEffectVisualizer.Type.Player,
                attack.ToMessage());
            if (id != -1)
            {
                Active[key] = id;
            }
        }

        internal static void Move(AttackSnapshot attack)
        {
            if (attack == null)
            {
                return;
            }

            if (!global::CombatSystem.AttackAlertEnabled)
            {
                Stop(attack);
                return;
            }

            int id;
            if (!Active.TryGetValue(MakeKey(attack), out id))
            {
                // Allows /dev attackalert on while an action is already in
                // progress to begin showing its still-pending hit areas.
                Show(attack);
                return;
            }

            AreaOfEffectVisualizer.Move(
                id,
                attack.Center.ToClientPosition());
        }

        private static void Stop(AttackSnapshot attack)
        {
            int id;
            string key = MakeKey(attack);
            if (Active.TryGetValue(key, out id))
            {
                AreaOfEffectVisualizer.Stop(id, 0f);
                Active.Remove(key);
            }
        }

        internal static void Release(AttackSnapshot attack)
        {
            if (attack != null)
            {
                Active.Remove(MakeKey(attack));
            }
        }

        internal static void Cancel(AttackSnapshot attack)
        {
            if (attack != null)
            {
                Stop(attack);
            }
        }

        internal static void Clear()
        {
            foreach (KeyValuePair<string, int> item in Active)
            {
                AreaOfEffectVisualizer.Stop(item.Value, 0f);
            }
            Active.Clear();
        }

        private static string MakeKey(AttackSnapshot attack)
        {
            return attack.Generation + ":" +
                attack.ActionInstanceId + ":" + attack.HitIndex;
        }
    }

    internal static class AnimalAttackTelegraph
    {
        private static readonly Dictionary<string, int> Active =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal static void Show(AnimalAttackSnapshot attack)
        {
            if (attack == null ||
                !global::CombatSystem.AttackAlertEnabled)
            {
                return;
            }
            string key = MakeKey(attack);
            int previous;
            if (Active.TryGetValue(key, out previous))
            {
                AreaOfEffectVisualizer.Stop(previous, 0f);
                Active.Remove(key);
            }
            int id = AreaOfEffectVisualizer.ShowAttackAlerted(
                AreaOfEffectVisualizer.Type.Alert,
                attack.ToMessage());
            if (id != -1)
            {
                Active[key] = id;
            }
        }

        internal static void Move(AnimalAttackSnapshot attack)
        {
            if (attack == null)
            {
                return;
            }
            string key = MakeKey(attack);
            int id;
            if (!global::CombatSystem.AttackAlertEnabled)
            {
                if (Active.TryGetValue(key, out id))
                {
                    AreaOfEffectVisualizer.Stop(id, 0f);
                    Active.Remove(key);
                }
                return;
            }
            if (!Active.TryGetValue(key, out id))
            {
                Show(attack);
                return;
            }
            AreaOfEffectVisualizer.Move(
                id,
                attack.Center.ToClientPosition());
        }

        internal static void Release(AnimalAttackSnapshot attack)
        {
            if (attack != null)
            {
                Active.Remove(MakeKey(attack));
            }
        }

        internal static void Cancel(AnimalAttackSnapshot attack)
        {
            if (attack == null)
            {
                return;
            }
            string key = MakeKey(attack);
            int id;
            if (Active.TryGetValue(key, out id))
            {
                AreaOfEffectVisualizer.Stop(id, 0f);
                Active.Remove(key);
            }
        }

        internal static void Clear()
        {
            foreach (KeyValuePair<string, int> item in Active)
            {
                AreaOfEffectVisualizer.Stop(item.Value, 0f);
            }
            Active.Clear();
        }

        private static string MakeKey(AnimalAttackSnapshot attack)
        {
            return attack.Generation + ":" +
                attack.ActorEntityId + ":" +
                attack.AttackInstanceId + ":" + attack.HitIndex;
        }
    }

    [HarmonyPatch(
        typeof(UsingActionAlert),
        "Set",
        new Type[] { typeof(BattleAction) })]
    internal static class OriginalPlayerAttackAlertPatch
    {
        private static bool Prefix()
        {
            return DurangoCombatSystemPlugin.Enabled == null ||
                !DurangoCombatSystemPlugin.Enabled.Value;
        }
    }

    [HarmonyPatch(
        typeof(AreaOfEffectVisualizer),
        "ShowAttackAlerted",
        new Type[] { typeof(AttackAlerted) })]
    internal static class SuppressDefaultPluginAttackAlertPatch
    {
        private static bool Prefix(
            AttackAlerted alert,
            ref int __result)
        {
            if (DurangoCombatSystemPlugin.Enabled == null ||
                !DurangoCombatSystemPlugin.Enabled.Value ||
                !PlayerAttackTelegraph.IsPluginOwned(alert))
            {
                return true;
            }

            // Plugin-owned alerts are drawn and moved by the authoritative
            // offline runtime.  Do not let the original developer renderer
            // create a second, static copy from the same message.
            __result = -1;
            return false;
        }
    }
}
