using System.Collections.Generic;
using Durango.Utils;
using HarmonyLib;
using InteractionData;
using Messages;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    [HarmonyPatch(typeof(InteractionSystem), "TouchedReceived")]
    internal static class AnimalCombatInteractionPatch
    {
        private static void Prefix(ref Touched msg)
        {
            if (BrachioLootRuntime.ApplyToTouched(ref msg))
            {
                return;
            }

            if (string.IsNullOrEmpty(msg.EntityId) ||
                !Singleton<AnimalManager>.HasInstance() ||
                !OfflineCombatAnimalTargets.IsCombatAnimal(
                    Singleton<AnimalManager>.Instance().GetAnimal(msg.EntityId)))
            {
                return;
            }

            List<int> interactions = new List<int>();
            bool hasAttack = false;
            if (msg.Interactions != null)
            {
                for (int i = 0; i < msg.Interactions.Length; i++)
                {
                    int interaction = msg.Interactions[i];
                    if (interaction == (int)Interaction.RemoveNatural ||
                        interaction == (int)Interaction.RemoveGrazingPet)
                    {
                        continue;
                    }
                    if (interaction == (int)Interaction.Attack)
                    {
                        hasAttack = true;
                    }
                    if (!interactions.Contains(interaction))
                    {
                        interactions.Add(interaction);
                    }
                }
            }

            if (!hasAttack)
            {
                interactions.Insert(0, (int)Interaction.Attack);
            }
            msg.Interactions = interactions.ToArray();
            if (msg.DisabledInteractions == null)
            {
                msg.DisabledInteractions = new int[0];
            }
            if (msg.AccessDeniedInteractions == null)
            {
                msg.AccessDeniedInteractions = new int[0];
            }
        }
    }
}
