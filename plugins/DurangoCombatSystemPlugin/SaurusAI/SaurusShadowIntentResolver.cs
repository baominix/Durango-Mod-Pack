using System;
using System.Collections.Generic;
using System.Globalization;
using Baominix.DurangoOriginal.CombatSystem.Data;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal sealed class SaurusShadowCandidateResult
    {
        internal SaurusShadowCandidateResult(
            SaurusShadowIntentRule rule,
            bool eligible,
            string reason)
        {
            Rule = rule;
            Eligible = eligible;
            Reason = reason;
        }

        internal SaurusShadowIntentRule Rule { get; private set; }
        internal bool Eligible { get; private set; }
        internal string Reason { get; private set; }
    }

    internal sealed class SaurusShadowIntentDecision
    {
        internal SaurusShadowIntentDecision(
            long sequence,
            long contextSequence,
            int generation,
            long engagementId,
            double decidedAt,
            SaurusCombatIntent intent,
            string actionKey,
            SaurusAlignmentPolicy alignment,
            float deterministicRoll,
            string reason,
            SaurusShadowCandidateResult[] candidates,
            bool legacySelectionObserved,
            string legacyActionKey)
        {
            Sequence = sequence;
            ContextSequence = contextSequence;
            Generation = generation;
            EngagementId = engagementId;
            DecidedAt = decidedAt;
            Intent = intent;
            ActionKey = actionKey;
            Alignment = alignment;
            DeterministicRoll = deterministicRoll;
            Reason = reason;
            Candidates = candidates ?? new SaurusShadowCandidateResult[0];
            LegacySelectionObserved = legacySelectionObserved;
            LegacyActionKey = legacyActionKey;
        }

        internal long Sequence { get; private set; }
        internal long ContextSequence { get; private set; }
        internal int Generation { get; private set; }
        internal long EngagementId { get; private set; }
        internal double DecidedAt { get; private set; }
        internal SaurusCombatIntent Intent { get; private set; }
        internal string ActionKey { get; private set; }
        internal SaurusAlignmentPolicy Alignment { get; private set; }
        internal float DeterministicRoll { get; private set; }
        internal string Reason { get; private set; }
        internal SaurusShadowCandidateResult[] Candidates { get; private set; }
        internal bool LegacySelectionObserved { get; private set; }
        internal string LegacyActionKey { get; private set; }

        internal SaurusShadowIntentDecision WithLegacySelection(
            string legacyActionKey)
        {
            return new SaurusShadowIntentDecision(
                Sequence,
                ContextSequence,
                Generation,
                EngagementId,
                DecidedAt,
                Intent,
                ActionKey,
                Alignment,
                DeterministicRoll,
                Reason,
                Candidates,
                true,
                legacyActionKey);
        }

        internal string ToSummaryLine(string entityId, int entityTypeId)
        {
            return "entity=" + entityId + " type=" + entityTypeId +
                " shadow=" + Intent + "/" + (ActionKey ?? "none") +
                " legacy=" + (LegacySelectionObserved
                    ? (LegacyActionKey ?? "none")
                    : "pending") +
                " agreement=" + AgreementText() + ".";
        }

        internal string[] ToDiagnosticLines(string entityId, int entityTypeId)
        {
            List<string> lines = new List<string>();
            lines.Add(
                "ShadowIntent seq=" + Sequence + " context=" +
                ContextSequence + " gen=" + Generation +
                " engagement=" + EngagementId + " entity=" +
                entityId + " type=" + entityTypeId + ".");
            lines.Add(
                "Decision intent=" + Intent + " action=" +
                (ActionKey ?? "none") + " alignment=" + Alignment +
                " roll=" + F(DeterministicRoll) + " reason=" +
                Reason + ".");
            lines.Add(
                "Legacy observed=" + LegacySelectionObserved +
                " action=" + (LegacyActionKey ?? "none") +
                " agreement=" + AgreementText() + ".");
            int i;
            for (i = 0; i < Candidates.Length; i++)
            {
                SaurusShadowCandidateResult candidate = Candidates[i];
                lines.Add(
                    "candidate " + candidate.Rule.Intent + "/" +
                    candidate.Rule.AttackKey + " " +
                    (candidate.Eligible ? "eligible" : "rejected") +
                    " reason=" + candidate.Reason + " provenance=" +
                    candidate.Rule.Provenance + ".");
            }
            return lines.ToArray();
        }

        private string AgreementText()
        {
            if (!LegacySelectionObserved)
            {
                return "pending";
            }
            return string.Equals(
                ActionKey,
                LegacyActionKey,
                StringComparison.OrdinalIgnoreCase)
                    ? "same-action"
                    : "different";
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }

    internal static class SaurusShadowIntentResolver
    {
        private const int CounterPriority = 100;

        internal static SaurusShadowIntentDecision Resolve(
            long decisionSequence,
            SaurusCombatContext context,
            SaurusCombatMemory memory,
            AnimalCombatProfile profile)
        {
            if (context == null || memory == null || profile == null)
            {
                return Empty(decisionSequence, context, "missing-input");
            }

            SaurusShadowIntentRule[] rules;
            if (!SaurusShadowIntentProfiles.TryGet(
                context.ActorEntityTypeId,
                out rules))
            {
                return Empty(
                    decisionSequence,
                    context,
                    "missing-species-intent-profile");
            }

            List<SaurusShadowCandidateResult> results =
                new List<SaurusShadowCandidateResult>();
            List<SaurusShadowIntentRule> eligible =
                new List<SaurusShadowIntentRule>();
            int highestPriority = int.MinValue;
            float roll = DeterministicRoll(context, decisionSequence);
            int i;
            for (i = 0; i < rules.Length; i++)
            {
                SaurusShadowIntentRule rule = rules[i];
                string reason;
                bool accepted = Evaluate(
                    rule,
                    context,
                    memory,
                    profile,
                    DeterministicRuleRoll(
                        context,
                        decisionSequence,
                        rule.AttackKey),
                    out reason);
                results.Add(new SaurusShadowCandidateResult(
                    rule,
                    accepted,
                    reason));
                if (!accepted)
                {
                    continue;
                }
                if (rule.Priority > highestPriority)
                {
                    highestPriority = rule.Priority;
                    eligible.Clear();
                }
                if (rule.Priority == highestPriority)
                {
                    eligible.Add(rule);
                }
            }

            if (!context.HasTarget)
            {
                return Decision(
                    decisionSequence,
                    context,
                    SaurusCombatIntent.None,
                    null,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    roll,
                    "no-valid-target",
                    results);
            }
            if (!context.Engaged)
            {
                return Decision(
                    decisionSequence,
                    context,
                    SaurusCombatIntent.None,
                    null,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    roll,
                    "not-engaged",
                    results);
            }
            if (context.ActorState == SaurusAiState.Dead)
            {
                return Decision(
                    decisionSequence,
                    context,
                    SaurusCombatIntent.None,
                    null,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    roll,
                    "actor-dead",
                    results);
            }
            if (context.ActionLocked)
            {
                return Decision(
                    decisionSequence,
                    context,
                    SaurusCombatIntent.Stand,
                    null,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    roll,
                    "active-action-or-reaction-lock",
                    results);
            }
            if (context.CooldownRemaining > 0.001f &&
                (eligible.Count == 0 ||
                    highestPriority < CounterPriority))
            {
                return Decision(
                    decisionSequence,
                    context,
                    SaurusCombatIntent.Stand,
                    null,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    roll,
                    "attack-cooldown",
                    results);
            }
            if (eligible.Count == 0)
            {
                SaurusCombatIntent fallback = ResolveFallback(context, rules);
                return Decision(
                    decisionSequence,
                    context,
                    fallback,
                    null,
                    SaurusAlignmentPolicy.KeepCurrentFacing,
                    roll,
                    fallback == SaurusCombatIntent.Approach
                        ? "no-eligible-action-outside-reach"
                        : fallback == SaurusCombatIntent.Reposition
                            ? "no-eligible-action-for-sector-or-path"
                            : "no-eligible-action",
                    results);
            }

            SaurusShadowIntentRule selected = SelectWeighted(eligible, roll);
            return Decision(
                decisionSequence,
                context,
                selected.Intent,
                selected.AttackKey,
                selected.Alignment,
                roll,
                "highest-priority-eligible-intent",
                results);
        }

        private static bool Evaluate(
            SaurusShadowIntentRule rule,
            SaurusCombatContext context,
            SaurusCombatMemory memory,
            AnimalCombatProfile profile,
            float activationRoll,
            out string reason)
        {
            AnimalAttackDefinition definition =
                SaurusAttackSelector.FindDefinition(
                    profile,
                    rule.AttackKey);
            if (definition == null)
            {
                reason = "attack-definition-missing";
                return false;
            }
            if (definition.Hits.Length == 0 ||
                string.IsNullOrEmpty(definition.Motion))
            {
                reason = "motion-or-hit-data-missing";
                return false;
            }
            if (!string.IsNullOrEmpty(rule.AuditBlockReason))
            {
                reason = "audit-block:" + rule.AuditBlockReason;
                return false;
            }
            if (!SectorAllowed(rule.Sectors, context.TargetSector))
            {
                reason = "sector=" + context.TargetSector +
                    " allowed=" + rule.Sectors;
                return false;
            }
            if (context.SurfaceDistance < rule.MinimumDistance ||
                context.SurfaceDistance > rule.MaximumDistance)
            {
                reason = "surface-distance=" + F(context.SurfaceDistance) +
                    " range=" + F(rule.MinimumDistance) + ".." +
                    F(rule.MaximumDistance);
                return false;
            }
            if (rule.RequiresUnblockedPath &&
                context.PathState == SaurusObservationState.Blocked)
            {
                reason = "path-blocked";
                return false;
            }
            if (rule.TriggerEvent != SaurusCombatEventType.None &&
                !HasTrigger(rule, context, memory))
            {
                reason = "missing-event=" + rule.TriggerEvent +
                    " window=" + F(rule.TriggerWindowSeconds) + "s";
                return false;
            }
            if (activationRoll >= rule.ActivationChance)
            {
                reason = "activation-roll=" + F(activationRoll) +
                    " chance=" + F(rule.ActivationChance);
                return false;
            }
            reason = "sector=" + context.TargetSector +
                " distance=" + F(context.SurfaceDistance) +
                (rule.TriggerEvent == SaurusCombatEventType.None
                    ? string.Empty
                    : " event-window=open");
            return true;
        }

        private static bool HasTrigger(
            SaurusShadowIntentRule rule,
            SaurusCombatContext context,
            SaurusCombatMemory memory)
        {
            if (memory.HasRecent(
                rule.TriggerEvent,
                context.CapturedAt,
                rule.TriggerWindowSeconds,
                context.EngagementId))
            {
                return true;
            }
            return rule.Intent == SaurusCombatIntent.CounterAttack &&
                memory.HasRecent(
                    SaurusCombatEventType.AnimalDodgedPlayerAttack,
                    context.CapturedAt,
                    rule.TriggerWindowSeconds,
                    context.EngagementId);
        }

        private static bool SectorAllowed(
            SaurusSectorMask mask,
            SaurusTargetSector sector)
        {
            SaurusSectorMask actual;
            switch (sector)
            {
                case SaurusTargetSector.Front:
                    actual = SaurusSectorMask.Front;
                    break;
                case SaurusTargetSector.RightFlank:
                    actual = SaurusSectorMask.RightFlank;
                    break;
                case SaurusTargetSector.Rear:
                    actual = SaurusSectorMask.Rear;
                    break;
                case SaurusTargetSector.LeftFlank:
                    actual = SaurusSectorMask.LeftFlank;
                    break;
                default:
                    actual = SaurusSectorMask.None;
                    break;
            }
            return (mask & actual) != 0;
        }

        private static SaurusCombatIntent ResolveFallback(
            SaurusCombatContext context,
            SaurusShadowIntentRule[] rules)
        {
            if (context.PathState == SaurusObservationState.Blocked ||
                context.TargetSector == SaurusTargetSector.LeftFlank ||
                context.TargetSector == SaurusTargetSector.RightFlank ||
                context.TargetSector == SaurusTargetSector.Rear)
            {
                return SaurusCombatIntent.Reposition;
            }
            float maximum = 0f;
            int i;
            for (i = 0; i < rules.Length; i++)
            {
                if (string.IsNullOrEmpty(rules[i].AuditBlockReason))
                {
                    maximum = Mathf.Max(maximum, rules[i].MaximumDistance);
                }
            }
            return context.SurfaceDistance > maximum
                ? SaurusCombatIntent.Approach
                : SaurusCombatIntent.Stand;
        }

        private static SaurusShadowIntentRule SelectWeighted(
            List<SaurusShadowIntentRule> candidates,
            float roll)
        {
            float total = 0f;
            int i;
            for (i = 0; i < candidates.Count; i++)
            {
                total += candidates[i].Weight;
            }
            if (total <= 0f)
            {
                return candidates[0];
            }
            float cursor = Mathf.Clamp01(roll) * total;
            for (i = 0; i < candidates.Count; i++)
            {
                cursor -= candidates[i].Weight;
                if (cursor <= 0f)
                {
                    return candidates[i];
                }
            }
            return candidates[candidates.Count - 1];
        }

        private static float DeterministicRoll(
            SaurusCombatContext context,
            long decisionSequence)
        {
            uint hash = 2166136261u;
            string entityId = context.ActorEntityId ?? string.Empty;
            int i;
            for (i = 0; i < entityId.Length; i++)
            {
                hash ^= entityId[i];
                hash *= 16777619u;
            }
            hash = Mix(hash, (uint)context.Generation);
            hash = Mix(hash, (uint)context.EngagementId);
            hash = Mix(hash, (uint)decisionSequence);
            return (hash & 0x00ffffffu) / 16777216f;
        }

        private static float DeterministicRuleRoll(
            SaurusCombatContext context,
            long decisionSequence,
            string actionKey)
        {
            uint hash = 2166136261u;
            string entityId = context.ActorEntityId ?? string.Empty;
            int i;
            for (i = 0; i < entityId.Length; i++)
            {
                hash ^= entityId[i];
                hash *= 16777619u;
            }
            hash = Mix(hash, (uint)context.Generation);
            hash = Mix(hash, (uint)context.EngagementId);
            hash = Mix(hash, (uint)decisionSequence);
            string key = actionKey ?? string.Empty;
            for (i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619u;
            }
            return (hash & 0x00ffffffu) / 16777216f;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            hash ^= value >> 16;
            hash *= 16777619u;
            return hash;
        }

        private static SaurusShadowIntentDecision Empty(
            long sequence,
            SaurusCombatContext context,
            string reason)
        {
            return new SaurusShadowIntentDecision(
                sequence,
                context == null ? 0L : context.Sequence,
                context == null ? 0 : context.Generation,
                context == null ? 0L : context.EngagementId,
                context == null ? 0.0 : context.CapturedAt,
                SaurusCombatIntent.None,
                null,
                SaurusAlignmentPolicy.KeepCurrentFacing,
                0f,
                reason,
                new SaurusShadowCandidateResult[0],
                false,
                null);
        }

        private static SaurusShadowIntentDecision Decision(
            long sequence,
            SaurusCombatContext context,
            SaurusCombatIntent intent,
            string actionKey,
            SaurusAlignmentPolicy alignment,
            float roll,
            string reason,
            List<SaurusShadowCandidateResult> candidates)
        {
            return new SaurusShadowIntentDecision(
                sequence,
                context.Sequence,
                context.Generation,
                context.EngagementId,
                context.CapturedAt,
                intent,
                actionKey,
                alignment,
                roll,
                reason,
                candidates.ToArray(),
                false,
                null);
        }

        private static string F(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
