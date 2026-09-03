using System;
using System.Globalization;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal enum SaurusTargetSector
    {
        None = 0,
        Front = 1,
        RightFlank = 2,
        Rear = 3,
        LeftFlank = 4
    }

    internal enum SaurusObservationState
    {
        Unknown = 0,
        Clear = 1,
        Blocked = 2
    }

    internal sealed class SaurusCombatContext
    {
        internal SaurusCombatContext(
            long sequence,
            int generation,
            long engagementId,
            double capturedAt,
            string actorEntityId,
            int actorEntityTypeId,
            int actorObjectInstanceId,
            Vector3 actorPosition,
            float actorYaw,
            float actorRadius,
            Vector3 actorVelocity,
            float actorLife,
            float actorLifeMaximum,
            SaurusAiState actorState,
            bool engaged,
            string targetEntityId,
            bool hasTarget,
            Vector3 targetPosition,
            float targetYaw,
            float targetRadius,
            Vector3 targetVelocity,
            float targetLife,
            float targetLifeMaximum,
            float centerDistance,
            float surfaceDistance,
            float targetBearingYaw,
            float signedRelativeAngle,
            SaurusTargetSector targetSector,
            SaurusObservationState lineOfSight,
            SaurusObservationState pathState,
            bool actionLocked,
            string activeActionKey,
            long activeActionInstanceId,
            float cooldownRemaining,
            float stateRemaining,
            SaurusCombatEventSnapshot latestEvent,
            int rememberedEventCount)
        {
            Sequence = sequence;
            Generation = generation;
            EngagementId = engagementId;
            CapturedAt = capturedAt;
            ActorEntityId = actorEntityId;
            ActorEntityTypeId = actorEntityTypeId;
            ActorObjectInstanceId = actorObjectInstanceId;
            ActorPosition = actorPosition;
            ActorYaw = actorYaw;
            ActorRadius = actorRadius;
            ActorVelocity = actorVelocity;
            ActorLife = actorLife;
            ActorLifeMaximum = actorLifeMaximum;
            ActorState = actorState;
            Engaged = engaged;
            TargetEntityId = targetEntityId;
            HasTarget = hasTarget;
            TargetPosition = targetPosition;
            TargetYaw = targetYaw;
            TargetRadius = targetRadius;
            TargetVelocity = targetVelocity;
            TargetLife = targetLife;
            TargetLifeMaximum = targetLifeMaximum;
            CenterDistance = centerDistance;
            SurfaceDistance = surfaceDistance;
            TargetBearingYaw = targetBearingYaw;
            SignedRelativeAngle = signedRelativeAngle;
            TargetSector = targetSector;
            LineOfSight = lineOfSight;
            PathState = pathState;
            ActionLocked = actionLocked;
            ActiveActionKey = activeActionKey;
            ActiveActionInstanceId = activeActionInstanceId;
            CooldownRemaining = cooldownRemaining;
            StateRemaining = stateRemaining;
            LatestEvent = latestEvent;
            RememberedEventCount = rememberedEventCount;
        }

        internal long Sequence { get; private set; }
        internal int Generation { get; private set; }
        internal long EngagementId { get; private set; }
        internal double CapturedAt { get; private set; }
        internal string ActorEntityId { get; private set; }
        internal int ActorEntityTypeId { get; private set; }
        internal int ActorObjectInstanceId { get; private set; }
        internal Vector3 ActorPosition { get; private set; }
        internal float ActorYaw { get; private set; }
        internal float ActorRadius { get; private set; }
        internal Vector3 ActorVelocity { get; private set; }
        internal float ActorLife { get; private set; }
        internal float ActorLifeMaximum { get; private set; }
        internal SaurusAiState ActorState { get; private set; }
        internal bool Engaged { get; private set; }
        internal string TargetEntityId { get; private set; }
        internal bool HasTarget { get; private set; }
        internal Vector3 TargetPosition { get; private set; }
        internal float TargetYaw { get; private set; }
        internal float TargetRadius { get; private set; }
        internal Vector3 TargetVelocity { get; private set; }
        internal float TargetLife { get; private set; }
        internal float TargetLifeMaximum { get; private set; }
        internal float CenterDistance { get; private set; }
        internal float SurfaceDistance { get; private set; }
        internal float TargetBearingYaw { get; private set; }
        internal float SignedRelativeAngle { get; private set; }
        internal SaurusTargetSector TargetSector { get; private set; }
        internal SaurusObservationState LineOfSight { get; private set; }
        internal SaurusObservationState PathState { get; private set; }
        internal bool ActionLocked { get; private set; }
        internal string ActiveActionKey { get; private set; }
        internal long ActiveActionInstanceId { get; private set; }
        internal float CooldownRemaining { get; private set; }
        internal float StateRemaining { get; private set; }
        internal SaurusCombatEventSnapshot LatestEvent { get; private set; }
        internal int RememberedEventCount { get; private set; }

        internal float ActorLifeRatio
        {
            get
            {
                return ActorLifeMaximum <= 0f
                    ? 0f
                    : Mathf.Clamp01(ActorLife / ActorLifeMaximum);
            }
        }

        internal static SaurusTargetSector ResolveSector(
            float signedRelativeAngle)
        {
            float absolute = Mathf.Abs(signedRelativeAngle);
            if (absolute <= 45f)
            {
                return SaurusTargetSector.Front;
            }
            if (absolute >= 135f)
            {
                return SaurusTargetSector.Rear;
            }
            return signedRelativeAngle > 0f
                ? SaurusTargetSector.RightFlank
                : SaurusTargetSector.LeftFlank;
        }

        internal string[] ToDiagnosticLines()
        {
            string action = string.IsNullOrEmpty(ActiveActionKey)
                ? "none"
                : ActiveActionKey;
            string lastEvent = LatestEvent == null
                ? "none"
                : LatestEvent.Type + "#" + LatestEvent.Sequence +
                    " age=" + F(CapturedAt - LatestEvent.At) + "s" +
                    (string.IsNullOrEmpty(LatestEvent.ActionKey)
                        ? string.Empty
                        : " action=" + LatestEvent.ActionKey +
                            "@" + LatestEvent.ActionInstanceId);

            return new string[]
            {
                "SaurusContext seq=" + Sequence + " gen=" + Generation +
                    " engagement=" + EngagementId +
                    " entity=" + ActorEntityId + " type=" +
                    ActorEntityTypeId + " state=" + ActorState +
                    " engaged=" + Engaged + ".",
                "Actor pos=" + V(ActorPosition) + " yaw=" + F(ActorYaw) +
                    " radius=" + F(ActorRadius) + " hp=" + F(ActorLife) +
                    "/" + F(ActorLifeMaximum) + " vel=" +
                    V(ActorVelocity) + ".",
                "Target id=" + (TargetEntityId ?? "none") + " valid=" +
                    HasTarget + " pos=" + V(TargetPosition) + " yaw=" +
                    F(TargetYaw) + " radius=" + F(TargetRadius) +
                    " hp=" + F(TargetLife) + "/" +
                    F(TargetLifeMaximum) + " vel=" +
                    V(TargetVelocity) + ".",
                "Spatial center=" + F(CenterDistance) + " surface=" +
                    F(SurfaceDistance) + " bearing=" +
                    F(TargetBearingYaw) + " relative=" +
                    F(SignedRelativeAngle) + " sector=" + TargetSector + ".",
                "Locks action=" + action + "@" + ActiveActionInstanceId +
                    " locked=" + ActionLocked + " cooldown=" +
                    F(CooldownRemaining) + "s stateRemaining=" +
                    F(StateRemaining) + "s.",
                "Observations lineOfSight=" + LineOfSight + " path=" +
                    PathState + " events=" + RememberedEventCount +
                    " latest=" + lastEvent + "."
            };
        }

        internal string ToSummaryLine()
        {
            return "entity=" + ActorEntityId + " type=" +
                ActorEntityTypeId + " state=" + ActorState +
                " distance=" + F(SurfaceDistance) + " angle=" +
                F(SignedRelativeAngle) + " sector=" + TargetSector +
                " action=" + (ActiveActionKey ?? "none") + ".";
        }

        private static string F(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 value)
        {
            return "(" + F(value.x) + "," + F(value.z) + ")";
        }
    }
}
