using System;
using System.Collections.Generic;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal enum SaurusCombatIntent
    {
        None = 0,
        StandardFront = 1,
        GapCloser = 2,
        TurnAttack = 3,
        CounterAttack = 4,
        EscapeStrike = 5,
        AreaControl = 6,
        Approach = 7,
        Reposition = 8,
        Stand = 9
    }

    internal enum SaurusAlignmentPolicy
    {
        FaceTargetBeforeCommit = 0,
        KeepCurrentFacing = 1,
        BindTargetAtCommit = 2
    }

    [Flags]
    internal enum SaurusSectorMask
    {
        None = 0,
        Front = 1,
        RightFlank = 2,
        Rear = 4,
        LeftFlank = 8,
        Flanks = RightFlank | LeftFlank,
        Any = Front | RightFlank | Rear | LeftFlank
    }

    internal sealed class SaurusShadowIntentRule
    {
        internal SaurusShadowIntentRule(
            string attackKey,
            SaurusCombatIntent intent,
            SaurusAlignmentPolicy alignment,
            SaurusSectorMask sectors,
            float minimumDistance,
            float maximumDistance,
            float weight,
            int priority,
            SaurusCombatEventType triggerEvent,
            float triggerWindowSeconds,
            bool requiresUnblockedPath,
            string provenance,
            string auditBlockReason,
            float activationChance)
        {
            AttackKey = attackKey;
            Intent = intent;
            Alignment = alignment;
            Sectors = sectors;
            MinimumDistance = Math.Max(0f, minimumDistance);
            MaximumDistance = Math.Max(
                MinimumDistance,
                maximumDistance);
            Weight = Math.Max(0f, weight);
            Priority = priority;
            TriggerEvent = triggerEvent;
            TriggerWindowSeconds = Math.Max(0f, triggerWindowSeconds);
            RequiresUnblockedPath = requiresUnblockedPath;
            Provenance = provenance;
            AuditBlockReason = auditBlockReason;
            ActivationChance = Math.Max(
                0f,
                Math.Min(1f, activationChance));
        }

        internal string AttackKey { get; private set; }
        internal SaurusCombatIntent Intent { get; private set; }
        internal SaurusAlignmentPolicy Alignment { get; private set; }
        internal SaurusSectorMask Sectors { get; private set; }
        internal float MinimumDistance { get; private set; }
        internal float MaximumDistance { get; private set; }
        internal float Weight { get; private set; }
        internal int Priority { get; private set; }
        internal SaurusCombatEventType TriggerEvent { get; private set; }
        internal float TriggerWindowSeconds { get; private set; }
        internal bool RequiresUnblockedPath { get; private set; }
        internal string Provenance { get; private set; }
        internal string AuditBlockReason { get; private set; }
        internal float ActivationChance { get; private set; }
    }

    internal static class SaurusShadowIntentProfiles
    {
        private const string Reconstructed = "Reconstructed";
        private const string OriginalReconstructed =
            "Original geometry; Reconstructed trigger";

        private static readonly Dictionary<int, SaurusShadowIntentRule[]>
            Profiles = CreateProfiles();

        internal static bool TryGet(
            int entityTypeId,
            out SaurusShadowIntentRule[] rules)
        {
            return Profiles.TryGetValue(entityTypeId, out rules);
        }

        private static Dictionary<int, SaurusShadowIntentRule[]>
            CreateProfiles()
        {
            Dictionary<int, SaurusShadowIntentRule[]> result =
                new Dictionary<int, SaurusShadowIntentRule[]>();

            result[2027] = new SaurusShadowIntentRule[]
            {
                Rule(
                    "tricera_counter",
                    SaurusCombatIntent.CounterAttack,
                    SaurusAlignmentPolicy.BindTargetAtCommit,
                    SaurusSectorMask.Any,
                    0f,
                    190f,
                    1f,
                    100,
                    SaurusCombatEventType.PlayerAttackMissed,
                    1.25f,
                    false,
                    OriginalReconstructed,
                    "counter-trigger-unconfirmed"),
                Rule(
                    "tricera_turn",
                    SaurusCombatIntent.TurnAttack,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    SaurusSectorMask.Rear,
                    0f,
                    420f,
                    4f,
                    80,
                    SaurusCombatEventType.PlayerBowOrCrossbowAttack,
                    1.5f,
                    false,
                    OriginalReconstructed,
                    null,
                    0.80f),
                Rule(
                    "tricera_dash",
                    SaurusCombatIntent.GapCloser,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    300f,
                    1050f,
                    4f,
                    60,
                    SaurusCombatEventType.None,
                    0f,
                    true,
                    Reconstructed,
                    null),
                Rule(
                    "tricera_head",
                    SaurusCombatIntent.StandardFront,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    0f,
                    330f,
                    3f,
                    50,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    Reconstructed,
                    null),
                Rule(
                    "tricera_once",
                    SaurusCombatIntent.StandardFront,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    0f,
                    380f,
                    4f,
                    50,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    Reconstructed,
                    null)
            };

            result[2037] = new SaurusShadowIntentRule[]
            {
                Rule(
                    "phenaco_attack_escape",
                    SaurusCombatIntent.EscapeStrike,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    SaurusSectorMask.Any,
                    0f,
                    350f,
                    1f,
                    90,
                    SaurusCombatEventType.LowHealthThresholdCrossed,
                    8f,
                    false,
                    OriginalReconstructed,
                    "escape-trigger-unconfirmed"),
                Rule(
                    "phenaco_gas",
                    SaurusCombatIntent.AreaControl,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    0f,
                    300f,
                    1f,
                    50,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    OriginalReconstructed,
                    null),
                Rule(
                    "phenaco_jump",
                    SaurusCombatIntent.GapCloser,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    150f,
                    520f,
                    4f,
                    60,
                    SaurusCombatEventType.None,
                    0f,
                    true,
                    Reconstructed,
                    null),
                Rule(
                    "phenaco_bite",
                    SaurusCombatIntent.StandardFront,
                    SaurusAlignmentPolicy.BindTargetAtCommit,
                    SaurusSectorMask.Front,
                    0f,
                    230f,
                    5f,
                    50,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    Reconstructed,
                    null)
            };

            result[2039] = new SaurusShadowIntentRule[]
            {
                Rule(
                    "raptor_counter",
                    SaurusCombatIntent.CounterAttack,
                    SaurusAlignmentPolicy.BindTargetAtCommit,
                    SaurusSectorMask.Any,
                    0f,
                    150f,
                    1f,
                    100,
                    SaurusCombatEventType.PlayerAttackMissed,
                    1.25f,
                    false,
                    OriginalReconstructed,
                    null),
                Rule(
                    "dilopho_tail",
                    SaurusCombatIntent.TurnAttack,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    SaurusSectorMask.Flanks | SaurusSectorMask.Rear,
                    0f,
                    300f,
                    1f,
                    80,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    OriginalReconstructed,
                    "model-compatibility-unconfirmed"),
                Rule(
                    "raptor_dash",
                    SaurusCombatIntent.GapCloser,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    180f,
                    340f,
                    3f,
                    60,
                    SaurusCombatEventType.None,
                    0f,
                    true,
                    "Original geometry/root yaw; Reconstructed intent",
                    null),
                Rule(
                    "raptor_jump",
                    SaurusCombatIntent.GapCloser,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    120f,
                    430f,
                    5f,
                    60,
                    SaurusCombatEventType.None,
                    0f,
                    true,
                    Reconstructed,
                    null),
                Rule(
                    "raptor_attack",
                    SaurusCombatIntent.StandardFront,
                    SaurusAlignmentPolicy.BindTargetAtCommit,
                    SaurusSectorMask.Front,
                    0f,
                    220f,
                    5f,
                    50,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    Reconstructed,
                    null)
            };

            // R7 vertical slice. Raptor 2001 deliberately owns a separate
            // rule array even though the source Framework is shared with
            // 2039. Spatial ranges reflect its original represent_scale 2.2.
            result[2001] = new SaurusShadowIntentRule[]
            {
                Rule(
                    "raptor_counter",
                    SaurusCombatIntent.CounterAttack,
                    SaurusAlignmentPolicy.BindTargetAtCommit,
                    SaurusSectorMask.Any,
                    0f,
                    330f,
                    1f,
                    100,
                    SaurusCombatEventType.PlayerAttackMissed,
                    1.25f,
                    false,
                    OriginalReconstructed,
                    "counter-trigger-unconfirmed"),
                Rule(
                    "dilopho_tail",
                    SaurusCombatIntent.TurnAttack,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    SaurusSectorMask.Flanks | SaurusSectorMask.Rear,
                    0f,
                    660f,
                    1f,
                    80,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    OriginalReconstructed,
                    "model-compatibility-unconfirmed"),
                Rule(
                    "raptor_dash",
                    SaurusCombatIntent.GapCloser,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    396f,
                    748f,
                    2f,
                    60,
                    SaurusCombatEventType.None,
                    0f,
                    true,
                    "Original geometry/root yaw; Reconstructed range",
                    null),
                Rule(
                    "raptor_jump",
                    SaurusCombatIntent.GapCloser,
                    SaurusAlignmentPolicy.FaceTargetBeforeCommit,
                    SaurusSectorMask.Front,
                    264f,
                    946f,
                    4f,
                    60,
                    SaurusCombatEventType.None,
                    0f,
                    true,
                    Reconstructed,
                    null),
                Rule(
                    "raptor_attack",
                    SaurusCombatIntent.StandardFront,
                    SaurusAlignmentPolicy.BindTargetAtCommit,
                    SaurusSectorMask.Front,
                    0f,
                    484f,
                    6f,
                    50,
                    SaurusCombatEventType.None,
                    0f,
                    false,
                    Reconstructed,
                    null)
            };

            return result;
        }

        private static SaurusShadowIntentRule Rule(
            string attackKey,
            SaurusCombatIntent intent,
            SaurusAlignmentPolicy alignment,
            SaurusSectorMask sectors,
            float minimumDistance,
            float maximumDistance,
            float weight,
            int priority,
            SaurusCombatEventType triggerEvent,
            float triggerWindowSeconds,
            bool requiresUnblockedPath,
            string provenance,
            string auditBlockReason)
        {
            return Rule(
                attackKey,
                intent,
                alignment,
                sectors,
                minimumDistance,
                maximumDistance,
                weight,
                priority,
                triggerEvent,
                triggerWindowSeconds,
                requiresUnblockedPath,
                provenance,
                auditBlockReason,
                1f);
        }

        private static SaurusShadowIntentRule Rule(
            string attackKey,
            SaurusCombatIntent intent,
            SaurusAlignmentPolicy alignment,
            SaurusSectorMask sectors,
            float minimumDistance,
            float maximumDistance,
            float weight,
            int priority,
            SaurusCombatEventType triggerEvent,
            float triggerWindowSeconds,
            bool requiresUnblockedPath,
            string provenance,
            string auditBlockReason,
            float activationChance)
        {
            return new SaurusShadowIntentRule(
                attackKey,
                intent,
                alignment,
                sectors,
                minimumDistance,
                maximumDistance,
                weight,
                priority,
                triggerEvent,
                triggerWindowSeconds,
                requiresUnblockedPath,
                provenance,
                auditBlockReason,
                activationChance);
        }
    }
}
