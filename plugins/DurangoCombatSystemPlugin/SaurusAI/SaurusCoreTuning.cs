using System;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    // These values fill gaps that are not present in animal.json or the
    // exported AnimalFrameworkResource.  Keep them in one profile-neutral
    // object so Phase 6 can replace them per species without changing the
    // state machine.
    internal sealed class SaurusCoreTuning
    {
        internal float AlertSeconds = 0.25f;
        internal float FaceSettleSeconds = 0.12f;
        internal float RecoverStandSeconds = 1f;
        internal float FaceToleranceDegrees = 8f;
        internal float ApproachHysteresis = 80f;
        internal float RoamRadius = 450f;
        internal float RoamStopDistance = 45f;
        internal float ReturnStopDistance = 60f;
        internal float MaximumPursuitDistance = 2800f;
        internal float LeashDistance = 2400f;
        internal float IdleSecondsMin = 2f;
        internal float IdleSecondsMax = 5f;
        internal float RoamSecondsMin = 2f;
        internal float RoamSecondsMax = 5f;
        internal float BlockedSecondsBeforeReturn = 1.5f;

        internal static SaurusCoreTuning CreateDefault()
        {
            return new SaurusCoreTuning();
        }

        internal string Validate()
        {
            if (AlertSeconds < 0f || FaceSettleSeconds < 0f ||
                RecoverStandSeconds < 0f)
            {
                return "Saurus timing values cannot be negative.";
            }
            if (FaceToleranceDegrees <= 0f ||
                FaceToleranceDegrees >= 180f)
            {
                return "Saurus face tolerance must be between 0 and 180 degrees.";
            }
            if (ApproachHysteresis < 0f || RoamRadius <= 0f ||
                MaximumPursuitDistance <= 0f || LeashDistance <= 0f)
            {
                return "Saurus range values are invalid.";
            }
            if (IdleSecondsMin < 0f || IdleSecondsMax < IdleSecondsMin ||
                RoamSecondsMin < 0f || RoamSecondsMax < RoamSecondsMin)
            {
                return "Saurus idle/roam duration ranges are invalid.";
            }
            return null;
        }
    }

    internal sealed class SaurusRandom
    {
        private uint _state;

        internal SaurusRandom(string seed)
        {
            unchecked
            {
                _state = 2166136261U;
                string value = seed ?? string.Empty;
                int i;
                for (i = 0; i < value.Length; i++)
                {
                    _state = (_state ^ value[i]) * 16777619U;
                }
                if (_state == 0U)
                {
                    _state = 1U;
                }
            }
        }

        internal float Range(float minimum, float maximum)
        {
            unchecked
            {
                _state = _state * 1664525U + 1013904223U;
            }
            float ratio = (_state & 0x00FFFFFFU) / 16777216f;
            return minimum + (maximum - minimum) * ratio;
        }
    }
}
