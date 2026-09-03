using Baominix.DurangoOriginal.CombatSystem.Data;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal enum SaurusActionAlignmentPolicy
    {
        CommitFacingThenFollowOriginalRootYaw = 0,
        LockCommitFacing = 1
    }

    // Immutable transform contract created once at the selection boundary.
    // Animation, logical root movement, telegraph and hit geometry all sample
    // this object instead of independently rebuilding axes and root curves.
    internal sealed class SaurusActionPlan
    {
        private readonly SaurusRootMotionCurve _rootCurve;
        private readonly Vector2 _commitForward;
        private readonly Vector2 _commitRight;

        private SaurusActionPlan(
            int generation,
            long engagementId,
            long actionInstanceId,
            AnimalAttackDefinition attack,
            double committedAt,
            Vector3 actorPosition,
            float actorYaw,
            Vector3 targetPosition,
            float spatialScale,
            SaurusActionAlignmentPolicy alignmentPolicy,
            SaurusRootMotionCurve rootCurve)
        {
            Generation = generation;
            EngagementId = engagementId;
            ActionInstanceId = actionInstanceId;
            Attack = attack;
            CommittedAt = committedAt;
            ActorPositionAtCommit = actorPosition;
            ActorYawAtCommit = actorYaw;
            TargetPositionAtCommit = targetPosition;
            SpatialScale = Mathf.Max(0.01f, spatialScale);
            AlignmentPolicy = alignmentPolicy;
            _rootCurve = rootCurve;
            float radians = actorYaw * Mathf.Deg2Rad;
            _commitForward = new Vector2(
                Mathf.Sin(radians),
                Mathf.Cos(radians));
            _commitRight = new Vector2(
                _commitForward.y,
                -_commitForward.x);
        }

        internal int Generation { get; private set; }
        internal long EngagementId { get; private set; }
        internal long ActionInstanceId { get; private set; }
        internal AnimalAttackDefinition Attack { get; private set; }
        internal double CommittedAt { get; private set; }
        internal Vector3 ActorPositionAtCommit { get; private set; }
        internal float ActorYawAtCommit { get; private set; }
        internal Vector3 TargetPositionAtCommit { get; private set; }
        internal float SpatialScale { get; private set; }
        internal SaurusActionAlignmentPolicy AlignmentPolicy
        {
            get;
            private set;
        }

        internal float Duration
        {
            get { return _rootCurve == null ? 0f : _rootCurve.Duration; }
        }

        internal static SaurusActionPlan Create(
            int generation,
            long engagementId,
            long actionInstanceId,
            AnimalAttackDefinition attack,
            double committedAt,
            Vector3 actorPosition,
            float actorYaw,
            Vector3 targetPosition,
            float spatialScale)
        {
            SaurusRootMotionCurve rootCurve;
            SaurusRootMotionData.TryGet(
                attack == null ? null : attack.Motion,
                out rootCurve);
            return new SaurusActionPlan(
                generation,
                engagementId,
                actionInstanceId,
                attack,
                committedAt,
                actorPosition,
                actorYaw,
                targetPosition,
                spatialScale,
                SaurusActionAlignmentPolicy.
                    CommitFacingThenFollowOriginalRootYaw,
                rootCurve);
        }

        internal Vector2 GetLocalPositionDelta(float elapsed)
        {
            return _rootCurve == null
                ? Vector2.zero
                : _rootCurve.GetLocalDelta(elapsed) * SpatialScale;
        }

        internal Vector2 GetLocalPositionDeltaAtFrame(
            int frame,
            float frameRate)
        {
            return frameRate <= 0f
                ? Vector2.zero
                : GetLocalPositionDelta(frame / frameRate);
        }

        internal Vector3 GetPlannedActorPosition(float elapsed)
        {
            Vector2 local = GetLocalPositionDelta(elapsed);
            Vector2 world = _commitRight * local.x +
                _commitForward * local.y;
            return ActorPositionAtCommit +
                new Vector3(world.x, 0f, world.y);
        }

        internal Vector3 GetPlannedActorPositionAtFrame(
            int frame,
            float frameRate)
        {
            return frameRate <= 0f
                ? ActorPositionAtCommit
                : GetPlannedActorPosition(frame / frameRate);
        }

        internal float GetPlannedActorYaw(float elapsed)
        {
            if (_rootCurve == null ||
                AlignmentPolicy ==
                    SaurusActionAlignmentPolicy.LockCommitFacing)
            {
                return ActorYawAtCommit;
            }
            return ActorYawAtCommit +
                _rootCurve.GetLocalYawDelta(elapsed);
        }

        internal float GetPlannedActorYawAtFrame(
            int frame,
            float frameRate)
        {
            return frameRate <= 0f
                ? ActorYawAtCommit
                : GetPlannedActorYaw(frame / frameRate);
        }
    }
}
