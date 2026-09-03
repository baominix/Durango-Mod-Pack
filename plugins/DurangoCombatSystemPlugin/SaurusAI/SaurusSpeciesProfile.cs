using System;
using System.Collections.Generic;
using Baominix.DurangoOriginal.CombatSystem.Data;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal sealed class SaurusAttackProfile
    {
        internal SaurusAttackProfile(
            string attackKey,
            float minimumDistance,
            float maximumDistance,
            float weight)
            : this(
                attackKey,
                minimumDistance,
                maximumDistance,
                weight,
                0f,
                0f,
                0f,
                1f)
        {
        }

        internal SaurusAttackProfile(
            string attackKey,
            float minimumDistance,
            float maximumDistance,
            float weight,
            float repositionBelowDistance,
            float preferredCommitDistance,
            float repositionSeconds,
            float repositionSpeedMultiplier)
        {
            AttackKey = attackKey;
            MinimumDistance = Math.Max(0f, minimumDistance);
            MaximumDistance = Math.Max(MinimumDistance, maximumDistance);
            Weight = Math.Max(0f, weight);
            RepositionBelowDistance = Math.Max(
                0f,
                repositionBelowDistance);
            PreferredCommitDistance = Math.Max(
                RepositionBelowDistance,
                preferredCommitDistance);
            RepositionSeconds = Math.Max(0f, repositionSeconds);
            RepositionSpeedMultiplier = Math.Max(
                0.1f,
                repositionSpeedMultiplier);
        }

        internal string AttackKey { get; private set; }
        internal float MinimumDistance { get; private set; }
        internal float MaximumDistance { get; private set; }
        internal float Weight { get; private set; }
        internal float RepositionBelowDistance { get; private set; }
        internal float PreferredCommitDistance { get; private set; }
        internal float RepositionSeconds { get; private set; }
        internal float RepositionSpeedMultiplier { get; private set; }

        internal bool IsInRange(float distance, float combinedBounds)
        {
            float adjusted = Math.Max(0f, distance - combinedBounds);
            return adjusted >= MinimumDistance &&
                adjusted <= MaximumDistance;
        }

        internal bool NeedsReposition(
            float distance,
            float combinedBounds)
        {
            float adjusted = Math.Max(0f, distance - combinedBounds);
            return RepositionSeconds > 0f &&
                PreferredCommitDistance > RepositionBelowDistance &&
                adjusted < RepositionBelowDistance;
        }
    }

    internal sealed class SaurusSpeciesProfile
    {
        private readonly Dictionary<string, SaurusAttackProfile> _byKey =
            new Dictionary<string, SaurusAttackProfile>(
                StringComparer.OrdinalIgnoreCase);

        internal SaurusSpeciesProfile(
            int entityTypeId,
            string name,
            SaurusAttackProfile[] attacks,
            float cooldownBonusSeconds,
            float retreatLifeRatio,
            float retreatChance,
            float retreatSeconds,
            float retreatSpeedMultiplier,
            string escapeAttackKey)
        {
            EntityTypeId = entityTypeId;
            Name = name;
            Attacks = attacks ?? new SaurusAttackProfile[0];
            CooldownBonusSeconds = Math.Max(0f, cooldownBonusSeconds);
            RetreatLifeRatio = Math.Max(0f, retreatLifeRatio);
            RetreatChance = Math.Max(0f, Math.Min(1f, retreatChance));
            RetreatSeconds = Math.Max(0f, retreatSeconds);
            RetreatSpeedMultiplier = Math.Max(1f, retreatSpeedMultiplier);
            EscapeAttackKey = escapeAttackKey;
            int i;
            for (i = 0; i < Attacks.Length; i++)
            {
                SaurusAttackProfile attack = Attacks[i];
                if (attack != null && !string.IsNullOrEmpty(attack.AttackKey))
                {
                    _byKey[attack.AttackKey] = attack;
                }
            }
        }

        internal int EntityTypeId { get; private set; }
        internal string Name { get; private set; }
        internal SaurusAttackProfile[] Attacks { get; private set; }
        internal float CooldownBonusSeconds { get; private set; }
        internal float RetreatLifeRatio { get; private set; }
        internal float RetreatChance { get; private set; }
        internal float RetreatSeconds { get; private set; }
        internal float RetreatSpeedMultiplier { get; private set; }
        internal string EscapeAttackKey { get; private set; }

        internal bool TryGetAttack(
            string attackKey,
            out SaurusAttackProfile attack)
        {
            attack = null;
            return !string.IsNullOrEmpty(attackKey) &&
                _byKey.TryGetValue(attackKey, out attack);
        }
    }

    internal static class SaurusSpeciesProfiles
    {
        private static readonly Dictionary<int, SaurusSpeciesProfile> Profiles =
            CreateProfiles();

        internal static bool TryGet(
            int entityTypeId,
            out SaurusSpeciesProfile profile)
        {
            return Profiles.TryGetValue(entityTypeId, out profile);
        }

        private static Dictionary<int, SaurusSpeciesProfile> CreateProfiles()
        {
            Dictionary<int, SaurusSpeciesProfile> result =
                new Dictionary<int, SaurusSpeciesProfile>();

            // Distance bands and weights are reconstructed from the original
            // Framework geometry plus the observed combat flows.  Motion,
            // hit frames, hit geometry and root curves remain original data.
            result[2027] = new SaurusSpeciesProfile(
                2027,
                "Zebraceratops",
                new SaurusAttackProfile[]
                {
                    new SaurusAttackProfile("tricera_counter", 0f, 190f, 1f),
                    new SaurusAttackProfile("tricera_turn", 0f, 420f, 4f),
                    new SaurusAttackProfile("tricera_head", 0f, 330f, 3f),
                    new SaurusAttackProfile("tricera_once", 0f, 380f, 4f),
                    new SaurusAttackProfile("tricera_dash", 300f, 1050f, 4f)
                },
                0f,
                0.20f,
                0.15f,
                6f,
                1.25f,
                null);

            result[2037] = new SaurusSpeciesProfile(
                2037,
                "Elephantulus",
                new SaurusAttackProfile[]
                {
                    new SaurusAttackProfile("phenaco_bite", 0f, 230f, 5f),
                    new SaurusAttackProfile("phenaco_jump", 150f, 520f, 4f),
                    // At frame 42 the original gas clip has already moved
                    // roughly 151 units forward and turned about 178 degrees.
                    // Create a little room before committing the rear sector,
                    // so the target stays near the middle of its original
                    // radius instead of being passed or collision-blocked.
                    new SaurusAttackProfile(
                        "phenaco_gas",
                        0f,
                        300f,
                        1f,
                        200f,
                        250f,
                        1.25f,
                        0.65f)
                },
                0f,
                0.20f,
                0.15f,
                6f,
                1.20f,
                "phenaco_attack_escape");

            result[2039] = new SaurusSpeciesProfile(
                2039,
                "Deinonychus",
                new SaurusAttackProfile[]
                {
                    new SaurusAttackProfile("raptor_counter", 0f, 150f, 1f),
                    new SaurusAttackProfile("raptor_attack", 0f, 220f, 5f),
                    new SaurusAttackProfile("raptor_jump", 120f, 430f, 5f),
                    new SaurusAttackProfile("raptor_dash", 180f, 340f, 3f)
                },
                0f,
                0.20f,
                0.15f,
                6f,
                1.30f,
                null);

            // Raptor 2001 shares the Raptor Framework with Deinonychus but
            // uses RaptorPrefab at represent_scale 2.2. Keep an independent
            // intent/range/weight profile and scale the reconstructed 2039
            // distance bands instead of inheriting its policy object.
            result[2001] = new SaurusSpeciesProfile(
                2001,
                "Raptor",
                new SaurusAttackProfile[]
                {
                    new SaurusAttackProfile("raptor_counter", 0f, 330f, 1f),
                    new SaurusAttackProfile("raptor_attack", 0f, 484f, 6f),
                    new SaurusAttackProfile("raptor_jump", 264f, 946f, 4f),
                    new SaurusAttackProfile("raptor_dash", 396f, 748f, 2f)
                },
                0f,
                0.20f,
                0.15f,
                6f,
                1.30f,
                null);

            return result;
        }
    }
}
