using System;
using Baominix.DurangoOriginal.CombatSystem.Data;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal static class SaurusAttackSelector
    {
        internal static float GetMaximumReach(
            AnimalCombatProfile profile,
            SaurusSpeciesProfile species)
        {
            if (profile == null || profile.FrameworkData == null ||
                species == null)
            {
                return 0f;
            }

            float maximum = 0f;
            int i;
            for (i = 0; i < species.Attacks.Length; i++)
            {
                SaurusAttackProfile speciesAttack = species.Attacks[i];
                AnimalAttackDefinition definition = FindDefinition(
                    profile,
                    speciesAttack.AttackKey);
                if (definition == null)
                {
                    continue;
                }
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Max(
                        speciesAttack.MaximumDistance,
                        GetReach(definition)));
            }
            return maximum;
        }

        internal static AnimalAttackDefinition Select(
            AnimalCombatProfile profile,
            SaurusSpeciesProfile species,
            float distance,
            float combinedBounds,
            float roll)
        {
            if (profile == null || profile.FrameworkData == null ||
                species == null || species.Attacks.Length == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            int i;
            for (i = 0; i < species.Attacks.Length; i++)
            {
                SaurusAttackProfile candidate = species.Attacks[i];
                AnimalAttackDefinition definition = FindDefinition(
                    profile,
                    candidate.AttackKey);
                if (definition == null || definition.Hits.Length == 0 ||
                    string.IsNullOrEmpty(definition.Motion) ||
                    !candidate.IsInRange(distance, combinedBounds))
                {
                    continue;
                }
                totalWeight += candidate.Weight;
            }
            if (totalWeight <= 0f)
            {
                return null;
            }

            float cursor = Mathf.Clamp01(roll) * totalWeight;
            AnimalAttackDefinition fallback = null;
            for (i = 0; i < species.Attacks.Length; i++)
            {
                SaurusAttackProfile candidate = species.Attacks[i];
                AnimalAttackDefinition definition = FindDefinition(
                    profile,
                    candidate.AttackKey);
                if (definition == null || definition.Hits.Length == 0 ||
                    string.IsNullOrEmpty(definition.Motion) ||
                    !candidate.IsInRange(distance, combinedBounds))
                {
                    continue;
                }
                fallback = definition;
                cursor -= candidate.Weight;
                if (cursor <= 0f)
                {
                    return definition;
                }
            }
            return fallback;
        }

        internal static AnimalAttackDefinition FindDefinition(
            AnimalCombatProfile profile,
            string attackKey)
        {
            if (profile == null || profile.FrameworkData == null ||
                string.IsNullOrEmpty(attackKey))
            {
                return null;
            }
            int i;
            for (i = 0; i < profile.FrameworkData.Attacks.Count; i++)
            {
                AnimalAttackDefinition attack =
                    profile.FrameworkData.Attacks[i];
                if (attack != null && string.Equals(
                    attack.Key,
                    attackKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return attack;
                }
            }
            return null;
        }

        private static float GetReach(AnimalAttackDefinition attack)
        {
            if (attack == null)
            {
                return 0f;
            }
            float maximum = 0f;
            int i;
            for (i = 0; i < attack.Hits.Length; i++)
            {
                AttackHitDefinition hit = attack.Hits[i];
                float forwardSize = hit.RectangleHalfWidth > 0f
                    ? hit.RectangleHalfWidth
                    : hit.Radius;
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Abs(hit.OffsetY) + forwardSize);
            }
            return maximum;
        }
    }
}
