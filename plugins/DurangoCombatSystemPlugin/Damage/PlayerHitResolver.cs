using System;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Baominix.DurangoOriginal.CombatSystem.Geometry;
using Baominix.DurangoOriginal.CombatSystem.Runtime;
using Durango.Logic;
using Messages;
using Shared.Ability;
using Shared.Battle;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Damage
{
    internal sealed class ResolvedPlayerHit
    {
        internal DamageResult Result;
        internal int Value;
        internal BodyPart Part;
        internal DamageDirection Direction;
        internal AttackType AttackType;
        internal DamageEffects Effects;
        internal float GroggyDamage;
        internal float BlowPower;
        internal float KnockBackForce;
        internal bool IsBowOrCrossbow;
        internal bool UsedExactImpactData;
        internal bool UsedExactBlowResistance;
        internal bool UsedExactKnockBackResistance;
        internal bool UsedExactDefenseFormula;
        internal bool UsedExactDodgeFormula;
        internal bool UsedExactEvadeFormula;
        internal bool UsedExactPartProbability;
    }

    internal static class PlayerHitResolver
    {
        internal static ResolvedPlayerHit Resolve(
            AttackSnapshot attack,
            AnimalCombatTarget target,
            string actionId,
            AnimalInjuryModifiers injuryModifiers)
        {
            ResolvedPlayerHit result = new ResolvedPlayerHit();
            result.Value = 0;
            result.Part = BodyPart.Body;
            result.Direction = ResolveDirection(
                attack.ActorOriginAtHit,
                target.Position,
                target.Yaw);
            result.AttackType = ResolveAttackType(actionId);
            result.Effects = (DamageEffects)0;
            result.GroggyDamage = 0f;
            result.BlowPower = 0f;
            result.KnockBackForce = 0f;
            result.IsBowOrCrossbow =
                attack.DamageType == DamageType.Ranged &&
                IsBowOrCrossbowAction(actionId);

            float playerAttack;
            float playerAccuracy;
            float playerAttackRating;
            ReadPlayerStatistics(
                out playerAttack,
                out playerAccuracy,
                out playerAttackRating);
            PlayerActionHitImpact impact;
            result.UsedExactImpactData =
                CombatDataRegistry.TryGetPlayerActionImpact(
                    attack.ActionId,
                    attack.HitIndex,
                    out impact);

            float animalDodge = AnimalFormulaEvaluator.Evaluate(
                target.Profile == null
                    ? null
                    : target.Profile.DodgeFormula,
                target.Level,
                1f,
                target.Level * 5f,
                out result.UsedExactDodgeFormula);
            if (injuryModifiers != null)
            {
                animalDodge = Mathf.Max(
                    0f,
                    animalDodge + injuryModifiers.DodgePlus);
            }
            float animalEvade = AnimalFormulaEvaluator.Evaluate(
                target.Profile == null
                    ? null
                    : target.Profile.EvadeFormula,
                target.Level,
                1f,
                0.20f,
                out result.UsedExactEvadeFormula);
            float animalDefense = AnimalFormulaEvaluator.Evaluate(
                target.Profile == null
                    ? null
                    : target.Profile.DefenseFormula,
                target.Level,
                1f,
                target.Level * 5f,
                out result.UsedExactDefenseFormula);
            float accuracyChance = playerAccuracy /
                Mathf.Max(1f, playerAccuracy + animalDodge);
            accuracyChance = Mathf.Clamp(accuracyChance, 0.05f, 0.99f);
            if (StableRoll(attack, target.EntityId, 17) >= accuracyChance)
            {
                result.Result = DamageResult.Missed;
                return result;
            }

            // animal.json exposes evade directly as a 0..1 probability for
            // the current Saurus profiles (0.2 at every combat level).
            // Keep the accuracy miss and evade dodge rolls independent: the
            // client presents them as different combat results.
            float evadeChance = Mathf.Clamp(animalEvade, 0f, 0.95f);
            if (StableRoll(attack, target.EntityId, 29) < evadeChance)
            {
                result.Result = DamageResult.Dodged;
                return result;
            }

            result.Part = SelectBodyPart(
                target.Profile,
                result.Direction,
                StableRoll(attack, target.EntityId, 41),
                out result.UsedExactPartProbability);
            float penetration = playerAttackRating /
                Mathf.Max(1f, playerAttackRating + animalDefense);
            penetration = Mathf.Clamp(penetration, 0.15f, 1f);
            float directionRatio = GetDirectionRatio(
                target.Profile,
                result.Direction);

            result.Result = DamageResult.Hit;
            result.Value = Math.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Max(1f, playerAttack) *
                    penetration * directionRatio));

            if (impact != null)
            {
                float groggyPlus;
                float blowPowerPlus;
                ReadPlayerImpactModifiers(
                    out groggyPlus,
                    out blowPowerPlus);
                result.GroggyDamage = Mathf.Max(
                    0f,
                    Mathf.Max(1f, playerAttack) *
                    Mathf.Max(0f, impact.GroggyRatio + groggyPlus) *
                    GetGroggyDirectionRatio(
                        target.Profile,
                        result.Direction));
                result.BlowPower = Mathf.Max(
                    0f,
                    impact.BlowPower + blowPowerPlus);
                result.KnockBackForce = Mathf.Max(
                    0f,
                    impact.KnockBackForce);

                bool exactKnockBack;
                float knockBackResistance = AnimalFormulaEvaluator.Evaluate(
                    target.Profile == null
                        ? null
                        : target.Profile.KnockBackResistanceFormula,
                    target.Level,
                    1f,
                    float.MaxValue,
                    out exactKnockBack);
                result.UsedExactKnockBackResistance = exactKnockBack;
                bool exactBlow;
                float blowResistance = AnimalFormulaEvaluator.Evaluate(
                    target.Profile == null
                        ? null
                        : target.Profile.BlowResistanceFormula,
                    target.Level,
                    1f,
                    float.MaxValue,
                    out exactBlow);
                result.UsedExactBlowResistance = exactBlow;

                if (exactKnockBack && result.KnockBackForce > 0f &&
                    result.KnockBackForce >= knockBackResistance)
                {
                    result.Effects |= DamageEffects.KnockBack;
                }
                else if (exactBlow && result.BlowPower > 0f &&
                    result.BlowPower >= blowResistance)
                {
                    result.Effects |= DamageEffects.Blow;
                }
            }
            return result;
        }

        private static void ReadPlayerImpactModifiers(
            out float groggyPlus,
            out float blowPowerPlus)
        {
            groggyPlus = 0f;
            blowPowerPlus = 0f;
            if (!GameSystem<StatisticsSystem>.HasInstance())
            {
                return;
            }
            StatisticsSystem system =
                GameSystem<StatisticsSystem>.Instance();
            if (system == null || !system.Statistics.HasValue)
            {
                return;
            }
            Statistics statistics = system.Statistics.Value;
            if (statistics.Modifiers == null)
            {
                return;
            }
            statistics.Modifiers.TryGetValue(
                "groggy_plus", out groggyPlus);
            statistics.Modifiers.TryGetValue(
                "blow_power_plus", out blowPowerPlus);
        }

        private static BodyPart SelectBodyPart(
            AnimalCombatProfile profile,
            DamageDirection direction,
            float roll,
            out bool exact)
        {
            exact = false;
            if (profile == null || profile.PartProbabilities == null)
            {
                return BodyPart.Body;
            }
            BodyPartWeights weights =
                profile.PartProbabilities.Get(direction);
            if (weights == null || weights.Total <= 0f)
            {
                return BodyPart.Body;
            }

            exact = true;
            float cursor = Mathf.Clamp01(roll) * weights.Total;
            BodyPart[] order = new BodyPart[]
            {
                BodyPart.Body,
                BodyPart.Head,
                BodyPart.Arm,
                BodyPart.Leg,
                BodyPart.Tail,
                BodyPart.Back
            };
            int i;
            for (i = 0; i < order.Length; i++)
            {
                float weight = weights.Get(order[i]);
                if (weight <= 0f)
                {
                    continue;
                }
                if (cursor < weight)
                {
                    return order[i];
                }
                cursor -= weight;
            }

            // Floating-point accumulation can leave only the upper endpoint.
            // Choose the last present part rather than introduce a new Body
            // bias that is not in the source table.
            for (i = order.Length - 1; i >= 0; i--)
            {
                if (weights.Get(order[i]) > 0f)
                {
                    return order[i];
                }
            }
            exact = false;
            return BodyPart.Body;
        }

        private static void ReadPlayerStatistics(
            out float attack,
            out float accuracy,
            out float attackRating)
        {
            attack = 10f;
            accuracy = 100f;
            attackRating = 10f;
            if (!GameSystem<StatisticsSystem>.HasInstance())
            {
                return;
            }

            StatisticsSystem system =
                GameSystem<StatisticsSystem>.Instance();
            if (system == null || !system.Statistics.HasValue)
            {
                return;
            }

            Statistics statistics = system.Statistics.Value;
            attack = GetDerived(statistics, Derived.Attack, attack);
            accuracy = GetDerived(statistics, Derived.Accuracy, accuracy);
            attackRating = GetDerived(
                statistics,
                Derived.AttackRating,
                attackRating);
        }

        private static float GetDerived(
            Statistics statistics,
            Derived key,
            float fallback)
        {
            float value;
            return statistics.DerivedsAbilities != null &&
                statistics.DerivedsAbilities.TryGetValue(key, out value)
                ? Mathf.Max(0f, value)
                : fallback;
        }

        private static DamageDirection ResolveDirection(
            Vector2 attacker,
            Vector2 target,
            float targetYaw)
        {
            Vector2 delta = attacker - target;
            if (delta.sqrMagnitude <= 0.001f)
            {
                return DamageDirection.Front;
            }

            float attackerYaw =
                Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float relative = Mathf.DeltaAngle(targetYaw, attackerYaw);
            float absolute = Mathf.Abs(relative);
            if (absolute <= 45f)
            {
                return DamageDirection.Front;
            }
            if (absolute >= 135f)
            {
                return DamageDirection.Back;
            }
            return relative > 0f
                ? DamageDirection.Right
                : DamageDirection.Left;
        }

        private static float GetDirectionRatio(
            AnimalCombatProfile profile,
            DamageDirection direction)
        {
            if (profile == null || profile.DamageRatios == null)
            {
                return 1f;
            }
            switch (direction)
            {
                case DamageDirection.Front:
                    return profile.DamageRatios.Front;
                case DamageDirection.Back:
                    return profile.DamageRatios.Back;
                case DamageDirection.Left:
                    return profile.DamageRatios.Left;
                case DamageDirection.Right:
                    return profile.DamageRatios.Right;
                default:
                    return 1f;
            }
        }

        private static float GetGroggyDirectionRatio(
            AnimalCombatProfile profile,
            DamageDirection direction)
        {
            if (profile == null || profile.GroggyDamageRatios == null)
            {
                return 1f;
            }
            switch (direction)
            {
                case DamageDirection.Front:
                    return profile.GroggyDamageRatios.Front;
                case DamageDirection.Back:
                    return profile.GroggyDamageRatios.Back;
                case DamageDirection.Left:
                    return profile.GroggyDamageRatios.Left;
                case DamageDirection.Right:
                    return profile.GroggyDamageRatios.Right;
                default:
                    return 1f;
            }
        }

        private static AttackType ResolveAttackType(string actionId)
        {
            string id = (actionId ?? string.Empty).ToLowerInvariant();
            if (id.Contains("axe")) return AttackType.Axe;
            if (id.Contains("blunt") || id.Contains("hammer"))
                return AttackType.Blunt;
            if (id.Contains("spear")) return AttackType.Spear;
            if (id.Contains("bow") || id.Contains("arrow"))
                return AttackType.Arrow;
            if (id.Contains("dagger") || id.Contains("knife"))
                return AttackType.Dagger;
            if (id.Contains("bomb")) return AttackType.Bomb;
            if (id.Contains("onehand") || id.Contains("twohand"))
                return AttackType.Sword;
            return AttackType.BareHands;
        }

        private static bool IsBowOrCrossbowAction(string actionId)
        {
            string id = (actionId ?? string.Empty).ToLowerInvariant();
            return id.StartsWith(
                    "ranged_bow_",
                    StringComparison.Ordinal) ||
                id.StartsWith(
                    "ranged_crossbow_",
                    StringComparison.Ordinal);
        }

        private static float StableRoll(
            AttackSnapshot attack,
            string targetEntityId,
            int salt)
        {
            unchecked
            {
                uint hash = 2166136261U;
                hash = Mix(hash, (uint)attack.ActionInstanceId);
                hash = Mix(hash, (uint)(attack.ActionInstanceId >> 32));
                hash = Mix(hash, (uint)attack.HitIndex);
                hash = Mix(hash, (uint)salt);
                string text = targetEntityId ?? string.Empty;
                int i;
                for (i = 0; i < text.Length; i++)
                {
                    hash = Mix(hash, text[i]);
                }
                return (hash & 0x00FFFFFFU) / 16777216f;
            }
        }

        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619U;
        }
    }
}
