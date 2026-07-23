using System;

namespace BaoX.DurangoOriginal.CombatSystemMod.Geometry
{
    internal struct WeaponSkillAoEProfile
    {
        public string Name;
        public string Shape;
        public float Length;
        public float HalfWidth;
        public float ForwardOffset;
        public float SideOffset;

        public WeaponSkillAoEProfile(string name, float length, float halfWidth, float forwardOffset)
            : this(name, "rect", length, halfWidth, forwardOffset, 0f)
        {
        }

        public WeaponSkillAoEProfile(string name, string shape, float length, float halfWidth, float forwardOffset, float sideOffset)
        {
            Name = name;
            Shape = shape;
            Length = length;
            HalfWidth = halfWidth;
            ForwardOffset = forwardOffset;
            SideOffset = sideOffset;
        }
    }

    internal static class WeaponSkillTuning
    {
        public const float LanceHitRange = 1200f;
        public const float BowHitRange = 1000f;
        public const float CrossbowHitRange = 850f;
        public const float TwoHandHitRange = 500f;
        public const float OneHandHitRange = 450f;
        public const float BareHandHitRange = 400f;

        public const float LanceDefaultAoELength = 450f;
        public const float LanceDefaultAoEHalfWidth = 80f;
        public const float LanceDefaultAoEForwardOffset = 0f;
        public const float LanceStrikeAoELength = 650f;
        public const float LanceStrikeAoEHalfWidth = 80f;
        public const float LanceStrikeAoEForwardOffset = 0f;
        public const float LanceDashAoELength = 450f;
        public const float LanceDashAoEHalfWidth = 80f;
        public const float LanceDashAoEForwardOffset = -50f;
        public const float TwoHandDefaultAoELength = 300f;
        public const float TwoHandDefaultAoEHalfWidth = 100f;
        public const float TwoHandDefaultAoEForwardOffset = 0f;
        public const float TwoHandSmashAoEForwardRadius = 400f;
        public const float TwoHandSmashAoEHalfWidth = 300f;
        public const float TwoHandSmashAoEForwardOffset = 0f;
        public const float TwoHandSmashAoESideOffset = 0f;
        public const float TwoHandSweepingHit1AoERadius = 300f;
        public const float TwoHandSweepingHit2AoEForwardRadius = 400f;
        public const float TwoHandSweepingHit2AoEHalfWidth = 300f;
        public const float TwoHandStrikeAoELength = 650f;
        public const float TwoHandStrikeAoEHalfWidth = 120f;
        public const float TwoHandStrikeAoEForwardOffset = 0f;
        public const float OneHandDefaultAoELength = 240f;
        public const float OneHandDefaultAoEHalfWidth = 70f;
        public const float OneHandDefaultAoEForwardOffset = 0f;
        public const float OneHandSmashAoEForwardRadius = 260f;
        public const float OneHandSmashAoEHalfWidth = 180f;
        public const float OneHandSmashAoEForwardOffset = 0f;
        public const float OneHandSmashAoESideOffset = 0f;
        public const float OneHandFlurryHit12AoEForwardRadius = 260f;
        public const float OneHandFlurryHit12AoEHalfWidth = 180f;
        public const float OneHandFlurryHit3AoELength = 400f;
        public const float OneHandFlurryHit3AoEHalfWidth = 70f;
        public const float OneHandStabAoELength = 400f;
        public const float OneHandStabAoEHalfWidth = 70f;
        public const float OneHandStabAoEForwardOffset = 0f;

        public const float PlayerAttackAoEDamageScale = 1f;
        public const double LanceDashHitInterval = 0.12;
        public const int LanceDashHitCount = 4;
        public const int TwoHandSweepingHitCount = 2;
        public const int OneHandFlurryHitCount = 3;
        public const float PlayerAnimalHitDelayDefault = 0.75f;
        public const float PlayerAnimalHitDelayLanceDefault = 0.75f;
        public const float PlayerAnimalHitDelayLanceStrike = 0.75f;
        public const float PlayerAnimalHitDelayLanceDash = 0.75f;
        public const float PlayerAnimalHitDelayTwoHandDefault = 0.75f;
        public const float PlayerAnimalHitDelayTwoHandSmash = 1.4f;
        public const float PlayerAnimalHitDelayTwoHandSweeping = 1.0f;
        public const float PlayerAnimalHitDelayTwoHandStrike = 1.5f;
        public const float PlayerAnimalHitDelayOneHandDefault = 0.5f;
        public const float PlayerAnimalHitDelayOneHandSmash = 0.75f;
        public const float PlayerAnimalHitDelayOneHandFlurry = 0.5f;
        public const float PlayerAnimalHitDelayOneHandStab = 0.85f;
        private static readonly float[] LanceDashHitDelayOffsets = new float[] { 0.8f, 1.15f, 1.5f, 1.85f };
        private static readonly float[] TwoHandSweepingHitDelayOffsets = new float[] { 0.55f, 1.25f };
        private static readonly float[] OneHandFlurryHitDelayOffsets = new float[] { 0f, 0.7f, 1.6f };
        private static readonly float[] TwoHandSweepingForwardOffsets = new float[] { 0f, 0f };
        private static readonly float[] TwoHandSweepingSideOffsets = new float[] { 0f, 0f };
        private static readonly float[] OneHandFlurryForwardOffsets = new float[] { 0f, 0f, 0f };
        private static readonly float[] OneHandFlurrySideOffsets = new float[] { 0f, 0f, 0f };

        public static bool IsLanceAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) &&
                actionId.IndexOf("lance", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsSmallAoEAction(string actionId)
        {
            return IsLanceAction(actionId) || IsTwoHandSwordAction(actionId) || IsOneHandSwordAction(actionId);
        }

        public static bool IsLanceDashAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) &&
                actionId.IndexOf("twohand_lance_dash", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsLanceStrikeAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId) &&
                actionId.IndexOf("twohand_lance_strike", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsLanceDefaultAction(string actionId)
        {
            return string.Equals(actionId, "twohand_lance_default_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_lance_default_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_lance_default_c", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTwoHandSmashAction(string actionId)
        {
            return string.Equals(actionId, "twohand_smash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_smash_axe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_smash_blunt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTwoHandDefaultAction(string actionId)
        {
            return string.Equals(actionId, "twohand_default_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_axe_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_axe_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_axe_c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_blunt_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_blunt_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_default_blunt_c", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTwoHandSweepingAction(string actionId)
        {
            return string.Equals(actionId, "twohand_sweeping", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_sweeping_axe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_sweeping_blunt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTwoHandStrikeAction(string actionId)
        {
            return string.Equals(actionId, "twohand_strike", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_strike_axe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "twohand_strike_blunt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTwoHandSwordSkillAction(string actionId)
        {
            return IsTwoHandSmashAction(actionId) ||
                IsTwoHandSweepingAction(actionId) ||
                IsTwoHandStrikeAction(actionId);
        }

        public static bool IsTwoHandSwordAction(string actionId)
        {
            return IsTwoHandDefaultAction(actionId) || IsTwoHandSwordSkillAction(actionId);
        }

        public static bool IsOneHandDefaultAction(string actionId)
        {
            return string.Equals(actionId, "onehand_default_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_axe_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_axe_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_axe_c", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_blunt_a", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_blunt_b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_default_blunt_c", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOneHandSmashAction(string actionId)
        {
            return string.Equals(actionId, "onehand_smash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_smash_axe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_smash_blunt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOneHandFlurryAction(string actionId)
        {
            return string.Equals(actionId, "onehand_flurry", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_flurry_axe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_flurry_blunt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOneHandStabAction(string actionId)
        {
            return string.Equals(actionId, "onehand_stab", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_stab_axe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(actionId, "onehand_stab_blunt", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOneHandSwordAction(string actionId)
        {
            return IsOneHandDefaultAction(actionId) ||
                IsOneHandSmashAction(actionId) ||
                IsOneHandFlurryAction(actionId) ||
                IsOneHandStabAction(actionId);
        }

        public static bool RequiresPrimaryAoEHit(string actionId)
        {
            return IsLanceDashAction(actionId);
        }

        public static bool ShouldRollEachHit(string actionId)
        {
            return IsLanceDashAction(actionId) || IsTwoHandSweepingAction(actionId) || IsOneHandFlurryAction(actionId);
        }

        public static bool UsePlayerForwardForAoE(string profileName)
        {
            return string.Equals(profileName, "dash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profileName, "twohand-smash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profileName, "twohand-sweeping", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profileName, "onehand-smash", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(profileName, "onehand-flurry", StringComparison.OrdinalIgnoreCase);
        }

        public static float GetPlayerAnimalHitDelay(string actionId)
        {
            return GetPlayerAnimalHitDelay(actionId, 0);
        }

        public static float GetPlayerAnimalHitDelay(string actionId, int hitIndex)
        {
            if (IsLanceDashAction(actionId))
            {
                return PlayerAnimalHitDelayLanceDash + GetLanceDashHitDelayOffset(hitIndex);
            }

            if (IsLanceStrikeAction(actionId))
            {
                return PlayerAnimalHitDelayLanceStrike;
            }

            if (IsLanceDefaultAction(actionId))
            {
                return PlayerAnimalHitDelayLanceDefault;
            }

            if (IsTwoHandDefaultAction(actionId))
            {
                return PlayerAnimalHitDelayTwoHandDefault;
            }

            if (IsTwoHandSmashAction(actionId))
            {
                return PlayerAnimalHitDelayTwoHandSmash;
            }

            if (IsTwoHandSweepingAction(actionId))
            {
                return PlayerAnimalHitDelayTwoHandSweeping + GetTwoHandSweepingHitDelayOffset(hitIndex);
            }

            if (IsTwoHandStrikeAction(actionId))
            {
                return PlayerAnimalHitDelayTwoHandStrike;
            }

            if (IsOneHandDefaultAction(actionId))
            {
                return PlayerAnimalHitDelayOneHandDefault;
            }

            if (IsOneHandSmashAction(actionId))
            {
                return PlayerAnimalHitDelayOneHandSmash;
            }

            if (IsOneHandFlurryAction(actionId))
            {
                return PlayerAnimalHitDelayOneHandFlurry + GetOneHandFlurryHitDelayOffset(hitIndex);
            }

            if (IsOneHandStabAction(actionId))
            {
                return PlayerAnimalHitDelayOneHandStab;
            }

            return PlayerAnimalHitDelayDefault;
        }

        public static float GetLanceDashHitDelayOffset(int hitIndex)
        {
            if (hitIndex < 0)
            {
                return 0f;
            }

            if (hitIndex < LanceDashHitDelayOffsets.Length)
            {
                return LanceDashHitDelayOffsets[hitIndex];
            }

            return LanceDashHitDelayOffsets[LanceDashHitDelayOffsets.Length - 1];
        }

        public static float GetTwoHandSweepingHitDelayOffset(int hitIndex)
        {
            if (hitIndex < 0)
            {
                return 0f;
            }

            if (hitIndex < TwoHandSweepingHitDelayOffsets.Length)
            {
                return TwoHandSweepingHitDelayOffsets[hitIndex];
            }

            return TwoHandSweepingHitDelayOffsets[TwoHandSweepingHitDelayOffsets.Length - 1];
        }

        public static float GetOneHandFlurryHitDelayOffset(int hitIndex)
        {
            if (hitIndex < 0)
            {
                return 0f;
            }

            if (hitIndex < OneHandFlurryHitDelayOffsets.Length)
            {
                return OneHandFlurryHitDelayOffsets[hitIndex];
            }

            return OneHandFlurryHitDelayOffsets[OneHandFlurryHitDelayOffsets.Length - 1];
        }

        public static double GetHitEventOffset(string actionId, int hitIndex)
        {
            if (IsLanceDashAction(actionId))
            {
                return GetLanceDashHitDelayOffset(hitIndex);
            }

            if (IsTwoHandSweepingAction(actionId))
            {
                return GetTwoHandSweepingHitDelayOffset(hitIndex);
            }

            if (IsOneHandFlurryAction(actionId))
            {
                return GetOneHandFlurryHitDelayOffset(hitIndex);
            }

            return LanceDashHitInterval * hitIndex;
        }

        public static WeaponSkillAoEProfile GetPlayerAttackAoEProfile(string actionId)
        {
            return GetPlayerAttackAoEProfile(actionId, 0);
        }

        public static WeaponSkillAoEProfile GetPlayerAttackAoEProfile(string actionId, int hitIndex)
        {
            if (!string.IsNullOrEmpty(actionId) &&
                actionId.IndexOf("twohand_lance_dash", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new WeaponSkillAoEProfile("dash", LanceDashAoELength, LanceDashAoEHalfWidth, LanceDashAoEForwardOffset);
            }

            if (IsTwoHandDefaultAction(actionId))
            {
                return new WeaponSkillAoEProfile("twohand-default", "rect", TwoHandDefaultAoELength, TwoHandDefaultAoEHalfWidth, TwoHandDefaultAoEForwardOffset, 0f);
            }

            if (IsOneHandDefaultAction(actionId))
            {
                return new WeaponSkillAoEProfile("onehand-default", "rect", OneHandDefaultAoELength, OneHandDefaultAoEHalfWidth, OneHandDefaultAoEForwardOffset, 0f);
            }

            if (IsTwoHandSmashAction(actionId))
            {
                return new WeaponSkillAoEProfile("twohand-smash", "half-ellipse", TwoHandSmashAoEForwardRadius, TwoHandSmashAoEHalfWidth, TwoHandSmashAoEForwardOffset, TwoHandSmashAoESideOffset);
            }

            if (IsOneHandSmashAction(actionId))
            {
                return new WeaponSkillAoEProfile("onehand-smash", "half-ellipse", OneHandSmashAoEForwardRadius, OneHandSmashAoEHalfWidth, OneHandSmashAoEForwardOffset, OneHandSmashAoESideOffset);
            }

            if (IsTwoHandSweepingAction(actionId))
            {
                if (hitIndex <= 0)
                {
                    return new WeaponSkillAoEProfile("twohand-sweeping", "circle", TwoHandSweepingHit1AoERadius, 0f, GetTwoHandSweepingForwardOffset(hitIndex), GetTwoHandSweepingSideOffset(hitIndex));
                }

                return new WeaponSkillAoEProfile("twohand-sweeping", "half-ellipse", TwoHandSweepingHit2AoEForwardRadius, TwoHandSweepingHit2AoEHalfWidth, GetTwoHandSweepingForwardOffset(hitIndex), GetTwoHandSweepingSideOffset(hitIndex));
            }

            if (IsTwoHandStrikeAction(actionId))
            {
                return new WeaponSkillAoEProfile("twohand-strike", TwoHandStrikeAoELength, TwoHandStrikeAoEHalfWidth, TwoHandStrikeAoEForwardOffset);
            }

            if (IsOneHandFlurryAction(actionId))
            {
                int sequenceIndex = NormalizeOneHandFlurryHitIndex(hitIndex);
                if (sequenceIndex < 2)
                {
                    return new WeaponSkillAoEProfile("onehand-flurry", "half-ellipse", OneHandFlurryHit12AoEForwardRadius, OneHandFlurryHit12AoEHalfWidth, GetOneHandFlurryForwardOffset(sequenceIndex), GetOneHandFlurrySideOffset(sequenceIndex));
                }

                return new WeaponSkillAoEProfile("onehand-flurry", "rect", OneHandFlurryHit3AoELength, OneHandFlurryHit3AoEHalfWidth, GetOneHandFlurryForwardOffset(sequenceIndex), GetOneHandFlurrySideOffset(sequenceIndex));
            }

            if (IsOneHandStabAction(actionId))
            {
                return new WeaponSkillAoEProfile("onehand-stab", "rect", OneHandStabAoELength, OneHandStabAoEHalfWidth, OneHandStabAoEForwardOffset, 0f);
            }

            if (!string.IsNullOrEmpty(actionId) &&
                actionId.IndexOf("twohand_lance_strike", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new WeaponSkillAoEProfile("strike", LanceStrikeAoELength, LanceStrikeAoEHalfWidth, LanceStrikeAoEForwardOffset);
            }

            return new WeaponSkillAoEProfile("default", LanceDefaultAoELength, LanceDefaultAoEHalfWidth, LanceDefaultAoEForwardOffset);
        }

        public static float GetTwoHandSweepingForwardOffset(int hitIndex)
        {
            if (hitIndex < 0)
            {
                return TwoHandSweepingForwardOffsets[0];
            }

            if (hitIndex < TwoHandSweepingForwardOffsets.Length)
            {
                return TwoHandSweepingForwardOffsets[hitIndex];
            }

            return TwoHandSweepingForwardOffsets[TwoHandSweepingForwardOffsets.Length - 1];
        }

        public static float GetTwoHandSweepingSideOffset(int hitIndex)
        {
            if (hitIndex < 0)
            {
                return TwoHandSweepingSideOffsets[0];
            }

            if (hitIndex < TwoHandSweepingSideOffsets.Length)
            {
                return TwoHandSweepingSideOffsets[hitIndex];
            }

            return TwoHandSweepingSideOffsets[TwoHandSweepingSideOffsets.Length - 1];
        }

        public static int NormalizeOneHandFlurryHitIndex(int hitIndex)
        {
            if (hitIndex < 0)
            {
                return 0;
            }

            return hitIndex % OneHandFlurryHitCount;
        }

        public static float GetOneHandFlurryForwardOffset(int hitIndex)
        {
            int index = NormalizeOneHandFlurryHitIndex(hitIndex);
            if (index < OneHandFlurryForwardOffsets.Length)
            {
                return OneHandFlurryForwardOffsets[index];
            }

            return OneHandFlurryForwardOffsets[OneHandFlurryForwardOffsets.Length - 1];
        }

        public static float GetOneHandFlurrySideOffset(int hitIndex)
        {
            int index = NormalizeOneHandFlurryHitIndex(hitIndex);
            if (index < OneHandFlurrySideOffsets.Length)
            {
                return OneHandFlurrySideOffsets[index];
            }

            return OneHandFlurrySideOffsets[OneHandFlurrySideOffsets.Length - 1];
        }

        public static int GetActionHitCount(string actionId, int damageValue, bool missed)
        {
            if (IsLanceDashAction(actionId))
            {
                return LanceDashHitCount;
            }

            if (IsTwoHandSweepingAction(actionId))
            {
                return TwoHandSweepingHitCount;
            }

            if (IsOneHandFlurryAction(actionId))
            {
                return OneHandFlurryHitCount;
            }

            if (damageValue <= 0 || missed)
            {
                return 1;
            }

            return 1;
        }

        public static float GetHitRange(string actionId)
        {
            if (string.IsNullOrEmpty(actionId))
            {
                return 650f;
            }

            if (actionId.IndexOf("bow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return BowHitRange;
            }

            if (actionId.IndexOf("crossbow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return CrossbowHitRange;
            }

            if (actionId.IndexOf("lance", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return LanceHitRange;
            }

            if (actionId.IndexOf("twohand", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return TwoHandHitRange;
            }

            if (actionId.IndexOf("onehand", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return OneHandHitRange;
            }

            return BareHandHitRange;
        }
    }

}
