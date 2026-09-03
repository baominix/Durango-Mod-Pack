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
    internal sealed class ResolvedAnimalHit
    {
        internal DamageResult Result;
        internal int Value;
        internal BodyPart Part;
        internal DamageDirection Direction;
        internal AttackType AttackType;
        internal DamageEffects Effects;
        internal bool UsedExactAttackFormula;
        internal bool UsedExactAccuracyFormula;
        internal bool UsedExactAttackRatingFormula;
    }

    internal static class AnimalHitResolver
    {
        internal static ResolvedAnimalHit Resolve(
            AnimalAttackSnapshot attack,
            Vector2 playerPosition,
            float playerYaw,
            AnimalInjuryModifiers injuryModifiers)
        {
            ResolvedAnimalHit result = new ResolvedAnimalHit();
            result.Value = 0;
            result.Part = BodyPart.Body;
            result.Direction = ResolveDirection(
                attack.ActorOriginAtHit,
                playerPosition,
                playerYaw);
            result.AttackType = ResolveAttackType(attack.Profile);
            result.Effects = (DamageEffects)0;

            float playerDefense;
            float playerDodge;
            float playerEvade;
            ReadPlayerStatistics(
                out playerDefense,
                out playerDodge,
                out playerEvade);

            float animalAttack = AnimalFormulaEvaluator.Evaluate(
                attack.Profile.AttackFormula,
                attack.Level,
                1f,
                Mathf.Max(1f, attack.Level * 0.5f),
                out result.UsedExactAttackFormula);
            float animalAccuracy = AnimalFormulaEvaluator.Evaluate(
                attack.Profile.AccuracyFormula,
                attack.Level,
                1f,
                attack.Level * 5f,
                out result.UsedExactAccuracyFormula);
            float animalAttackRating = AnimalFormulaEvaluator.Evaluate(
                attack.Profile.AttackRatingFormula,
                attack.Level,
                1f,
                attack.Level * 6f,
                out result.UsedExactAttackRatingFormula);
            if (injuryModifiers != null)
            {
                animalAttack *= Mathf.Max(
                    0f,
                    1f + injuryModifiers.DamageBonus);
                animalAccuracy *= Mathf.Max(
                    0f,
                    1f + injuryModifiers.HitRatePlus);
            }

            float hitChance = animalAccuracy /
                Mathf.Max(1f, animalAccuracy + playerDodge);
            hitChance = Mathf.Clamp(hitChance, 0.05f, 0.99f);
            if (StableRoll(attack, 17) >= hitChance)
            {
                result.Result = DamageResult.Missed;
                return result;
            }

            // Animal JSON stores evade as a direct 0..1 probability. Player
            // derived evade is commonly exposed on a 0..1000 scale, so keep
            // that normalization isolated until the original server formula
            // is recovered.
            float evadeChance = playerEvade <= 1f
                ? playerEvade
                : playerEvade / 1000f;
            evadeChance = Mathf.Clamp(evadeChance, 0f, 0.35f);
            if (StableRoll(attack, 29) < evadeChance)
            {
                result.Result = DamageResult.Dodged;
                return result;
            }

            float penetration = animalAttackRating /
                Mathf.Max(1f, animalAttackRating + playerDefense);
            penetration = Mathf.Clamp(penetration, 0.15f, 1f);
            result.Result = DamageResult.Hit;
            result.Value = Math.Max(
                1,
                Mathf.RoundToInt(Mathf.Max(1f, animalAttack) * penetration));
            return result;
        }

        private static void ReadPlayerStatistics(
            out float defense,
            out float dodge,
            out float evade)
        {
            int level = PlayerBehavior.LocalPlayer == null
                ? 1
                : Math.Max(1, PlayerBehavior.LocalPlayer.Level);
            defense = level * 5f;
            dodge = level * 5f;
            evade = 0f;

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
            defense = GetDerived(
                statistics,
                Derived.Defense,
                defense);
            dodge = GetDerived(statistics, Derived.Dodge, dodge);
            evade = GetDerived(statistics, Derived.Evade, evade);
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
            Vector2 player,
            float playerYaw)
        {
            Vector2 delta = attacker - player;
            if (delta.sqrMagnitude <= 0.001f)
            {
                return DamageDirection.Front;
            }
            float attackerYaw =
                Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float relative = Mathf.DeltaAngle(playerYaw, attackerYaw);
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

        private static AttackType ResolveAttackType(
            AnimalCombatProfile profile)
        {
            bool tear = profile != null &&
                (string.Equals(
                    profile.AnimalType,
                    "Carnivore",
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                    profile.AnimalType,
                    "Scavenger",
                    StringComparison.OrdinalIgnoreCase));
            bool large = profile != null && profile.SizeLevel >= 3;
            if (tear)
            {
                return large ? AttackType.LargeTear : AttackType.SmallTear;
            }
            return large ? AttackType.LargeBody : AttackType.SmallBody;
        }

        private static float StableRoll(
            AnimalAttackSnapshot attack,
            int salt)
        {
            unchecked
            {
                uint hash = 2166136261U;
                hash = Mix(hash, (uint)attack.AttackInstanceId);
                hash = Mix(hash, (uint)(attack.AttackInstanceId >> 32));
                hash = Mix(hash, (uint)attack.HitIndex);
                hash = Mix(hash, (uint)salt);
                string text = attack.ActorEntityId ?? string.Empty;
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
