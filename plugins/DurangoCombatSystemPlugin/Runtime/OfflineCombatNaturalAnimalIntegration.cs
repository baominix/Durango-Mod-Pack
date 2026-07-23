using Durango.Logic;
using Durango.UI;
using Durango.Utils;
using HarmonyLib;
using InteractionData;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal static class OfflineCombatAnimalTargets
    {
        internal static bool TryGetCombatAnimal(InteractionObject target, out AnimalBehavior animal)
        {
            animal = null;
            if (target == null)
            {
                return false;
            }

            animal = target.GetTargetComponent<AnimalBehavior>();
            if (animal == null && Singleton<AnimalManager>.HasInstance())
            {
                animal = Singleton<AnimalManager>.Instance().GetAnimal(target.EntityId);
            }

            return IsCombatAnimal(animal);
        }

        internal static bool IsCombatAnimal(AnimalBehavior animal)
        {
            if (animal == null || animal.gameObject == null ||
                string.IsNullOrEmpty(animal.EntityId) ||
                !animal.IsAlive)
            {
                return false;
            }

            if (!Singleton<AnimalManager>.HasInstance() ||
                Singleton<AnimalManager>.Instance().GetAnimal(animal.EntityId) != animal)
            {
                return false;
            }

            if (animal.GetComponent<PetAI>() != null ||
                animal.GetComponent<GrazingPetAI>() != null ||
                animal.GetComponent<VehicleProp>() != null ||
                animal.GetComponent<HumanBehavior>() != null ||
                animal.GetComponent<CostumeActorBehavior>() != null ||
                ObjectIdentifier.IsAlly(animal.gameObject))
            {
                return false;
            }

            string role = animal.Role;
            if (!string.IsNullOrEmpty(role))
            {
                string normalized = role.ToLowerInvariant();
                if (normalized == "warp_guard" ||
                    normalized.IndexOf("pet") >= 0 ||
                    normalized.IndexOf("vehicle") >= 0 ||
                    normalized.IndexOf("party") >= 0)
                {
                    return false;
                }
            }

            return animal.Life == null || animal.Life.Get() > 0f;
        }
    }

    internal static class OfflineCombatNaturalAnimalBridge
    {
        internal static bool TryStartFromInteraction(InteractionObject target)
        {
            AnimalBehavior animal;
            if (!OfflineCombatAnimalTargets.TryGetCombatAnimal(target, out animal))
            {
                return false;
            }

            LocalWildAnimalCombatAI ai = LocalWildAnimalCombatAI.Attach(animal);
            if (ai != null)
            {
                ai.ActivateCombat("interaction");
            }
            else
            {
                OfflineCombatRuntime.BeginCombat(animal.EntityId);
            }

            if (GameSystem<CombatSystem>.HasInstance())
            {
                GameSystem<CombatSystem>.Instance().SelectTarget(animal.EntityId);
            }

            CombatGroup combatGroup = UIManager.FindScript<CombatGroup>();
            if (combatGroup != null)
            {
                combatGroup.SetBattleView(CombatGroup.BattleViewMode.Battle);
            }

            if (GameSystem<InteractionSystem>.HasInstance())
            {
                GameSystem<InteractionSystem>.Instance().SetInteractionTarget(null);
            }

            OfflineCombatBackendPlugin.Log.LogInfo(
                "Natural animal attack interaction entity=" + animal.EntityId +
                " type=" + animal.EntityTypeId +
                " level=" + animal.Level);
            return true;
        }
    }

    [HarmonyPatch(
        typeof(InteractionSystem),
        "ExecuteInteraction",
        new System.Type[]
        {
            typeof(Interaction),
            typeof(InteractionObject),
            typeof(InteractionSystem.InteractionHandler)
        })]
    internal static class OfflineCombatAnimalAttackExecutionPatch
    {
        private static bool Prefix(Interaction action, InteractionObject target)
        {
            if (action != Interaction.Attack)
            {
                return true;
            }

            return !OfflineCombatNaturalAnimalBridge.TryStartFromInteraction(target);
        }
    }
}
