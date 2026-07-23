namespace BaoX.DurangoOriginal.CombatSystemMod.Geometry
{
    internal static class BrachioAttackProfiles
    {
        internal const string FrontAttackId = "brachio_attack";
        internal const string TailAttackId = "brachio_tail";
        internal const string WoundedTailAttackId = "brachio_wounded_tail";

        internal const float EngageDistance = 650f;
        internal const float AttackDistance = 580f;
        internal const float CombatExitDistance = 5000f;
        // Preserve the wider SaurusAICore front telegraph requested for Brachio.
        internal const float AreaAttackDistance = 700f;
        internal const float AreaAttackForwardOffset = 700f;
        internal const float FrontAdvanceDistance = 0f;
        internal const float TailAreaDistanceScale = 2f;
        // Preserve the SaurusAICore tail areas independently from front tuning.
        internal const float TailAreaDistance = 1400f;
        internal const float TailAreaHalfWidth = 700f;

        internal const float AttackHitBeforeEnd = 0.8f;
        internal const float TailAttackHitBeforeEnd = 2f;
        internal const float WoundedTailAttackHitBeforeEnd = 2f;
        internal const float TailAttackPrepSeconds = 0.4f;
        internal const float WoundedTailAttackPrepSeconds = 1.6f;
        internal const float TailAttackChance = 0.10f;
        internal const float WoundedTailAttackChance = 0.10f;
        internal const float AttackEndDelay = 4.5f;
        internal const float AttackCooldown = 8f;
    }
}
