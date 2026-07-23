using Shared.Ability;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    internal static class CombatFormula
    {
        internal static float AttackFromStrength(int strength)
        {
            return 40f + strength / 5;
        }

        internal static float AccuracyFromAgility(int agility)
        {
            return 100f + agility / 5;
        }

        internal static float DefensePenetrationFromStrength(int strength)
        {
            return System.Math.Max(0, strength) / 5;
        }

        internal static float EvasionFromAgility(int agility)
        {
            return agility;
        }

        internal static float CriticalFromDexterity(int dexterity)
        {
            return dexterity / 6;
        }

        internal static Derived? MapFlatCombatModifier(string normalizedId)
        {
            switch (normalizedId)
            {
                case "attackplus":
                case "attackpower":
                    return Derived.Attack;
                case "accuracyplus":
                case "hitrateplus":
                    return Derived.Accuracy;
                case "criticalplus":
                    return Derived.Critical;
                case "attackratingplus":
                    return Derived.AttackRating;
                case "dodgeplus":
                case "evadeplus":
                    return Derived.Dodge;
                default:
                    return null;
            }
        }
    }
}
