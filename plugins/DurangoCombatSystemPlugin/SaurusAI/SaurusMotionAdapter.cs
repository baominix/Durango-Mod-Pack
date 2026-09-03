using System;
using System.Collections.Generic;
using System.Globalization;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Durango.Utils;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.SaurusAI
{
    internal sealed class SaurusMotionAdapter : IDisposable
    {
        private static readonly Dictionary<int, SaurusMotionAdapter>
            ActiveAdapters = new Dictionary<int, SaurusMotionAdapter>();

        private readonly AnimalBehavior _animal;
        private readonly int _objectInstanceId;
        private readonly Transform _meshObjectTransform;
        private readonly Transform _rootBoneTransform;
        private readonly Vector3 _initialMeshLocalPosition;
        private MoveMotionInfo _moveMotion;
        private bool _disposed;
        private string _playingMotion;
        private SaurusRootMotionCurve _rootMotionCurve;
        private SaurusActionPlan _actionPlan;
        private double _rootMotionStartedAt;
        private Vector2 _previousRootMotionDelta;
        private float _rootMotionYaw;
        private string _debugActivity = "Initialize";
        private string _debugRequestedMotion = "none";
        private float _debugSignedYawError;
        private float _debugCrossFadeLength;
        private string _activeTurnMotion;
        private string _activeTurnActivity;
        private float _turnMotionUntil;
        private const float ReverseTurnThresholdDegrees = 135f;

        internal SaurusMotionAdapter(AnimalBehavior animal)
        {
            _animal = animal;
            _objectInstanceId = animal == null
                ? 0
                : animal.gameObject.GetInstanceID();
            _meshObjectTransform = animal == null
                ? null
                : animal.MeshObjectTransform;
            _rootBoneTransform = animal == null
                ? null
                : animal.Bip001Transform;
            ResolveMoveMotion();
            TakeMovementOwnership();
            // TakeMovementOwnership calls ResetRootMotionOffset first. Read
            // the canonical prefab offset only after that reset; capturing an
            // offset left by the animation that happened to be active when
            // the controller attached would make the floor base drift again.
            _initialMeshLocalPosition = _meshObjectTransform == null
                ? Vector3.zero
                : _meshObjectTransform.localPosition;
            if (_objectInstanceId != 0)
            {
                ActiveAdapters[_objectInstanceId] = this;
            }
        }

        internal static void StabilizePresentationBase(
            AnimalBehavior animal)
        {
            if (animal == null)
            {
                return;
            }
            SaurusMotionAdapter adapter;
            int objectInstanceId = animal.gameObject.GetInstanceID();
            if (ActiveAdapters.TryGetValue(
                    objectInstanceId,
                    out adapter) &&
                adapter != null &&
                object.ReferenceEquals(adapter._animal, animal))
            {
                adapter.StabilizePresentationBase();
            }
        }

        internal float MoveSpeed
        {
            get
            {
                return _moveMotion == null
                    ? 100f
                    : Mathf.Max(1f, _moveMotion.base_move_speed);
            }
        }

        internal float RotateSpeed
        {
            get
            {
                return _moveMotion == null
                    ? 90f
                    : Mathf.Max(1f, _moveMotion.rot_speed);
            }
        }

        internal string GetDebugText()
        {
            string actualMotion = "none";
            if (_animal != null)
            {
                AnimationState state = _animal.GetCurAnimState();
                if (state != null && !string.IsNullOrEmpty(state.name))
                {
                    actualMotion = state.name;
                }
            }
            return _debugActivity +
                "\nreq " + _debugRequestedMotion +
                " | now " + actualMotion +
                "\nyaw " +
                _debugSignedYawError.ToString(
                    "+0.0;-0.0;0.0",
                    CultureInfo.InvariantCulture) +
                " | xfade " +
                _debugCrossFadeLength.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture);
        }

        internal void PlayStand(AnimalCombatProfile profile, bool battle)
        {
            if (_disposed || profile == null ||
                profile.FrameworkData == null)
            {
                return;
            }
            string motion = battle
                ? profile.FrameworkData.BattleStandMotion
                : profile.FrameworkData.StandMotion;
            CancelTurnMotion();
            _debugActivity = battle ? "Stand Battle" : "Stand";
            PlayLoop(motion, 0.12f, 1f);
        }

        internal void PlayMove()
        {
            if (_disposed || _moveMotion == null)
            {
                return;
            }
            float playback = _moveMotion.playback_rate > 0f
                ? _moveMotion.playback_rate
                : 1f;
            CancelTurnMotion();
            _debugActivity = "Move";
            PlayLoop(_moveMotion.motion, 0.12f, playback);
            AnimationState state = _animal.GetCurAnimState();
            if (state != null && string.Equals(
                state.name,
                _moveMotion.motion,
                StringComparison.Ordinal))
            {
                state.speed = Mathf.Abs(playback);
            }
        }

        internal float PlayAttack(
            SaurusActionPlan plan,
            double now)
        {
            AnimalAttackDefinition attack = plan == null
                ? null
                : plan.Attack;
            if (_disposed || attack == null ||
                string.IsNullOrEmpty(attack.Motion))
            {
                return 0f;
            }

            _playingMotion = attack.Motion;
            CancelTurnMotion();
            _debugActivity = "Attack " + attack.Key;
            _debugRequestedMotion = attack.Motion;
            float crossFadeLength = _animal.CrossFade(
                attack.Motion,
                0.08f,
                false,
                0f,
                1f);
            _debugCrossFadeLength = crossFadeLength;
            float length = crossFadeLength;
            if (length <= 0f &&
                _animal.AnimalFrameworkResource != null)
            {
                AnimationElemAttack runtimeAttack =
                    _animal.AnimalFrameworkResource.GetAnimationElements(
                        attack.Key) as AnimationElemAttack;
                if (runtimeAttack != null && runtimeAttack.meta != null &&
                    runtimeAttack.meta.Clip != null)
                {
                    length = runtimeAttack.meta.Clip.length;
                }
            }
            BeginActionPlan(plan, now);
            if (plan.Duration > 0f)
            {
                length = Mathf.Max(length, plan.Duration);
            }
            return Mathf.Max(0f, length);
        }

        internal float PlayEvade(
            AnimalCombatProfile profile,
            SaurusEvadeRoute route,
            double now)
        {
            _debugActivity = "Evade " + route;
            return profile == null || profile.FrameworkData == null
                ? 0f
                : PlayDirectionalEvade(
                    profile.FrameworkData.EvadeMotion,
                    route,
                    now);
        }

        internal float PlayDamage(
            AnimalCombatProfile profile,
            Shared.Battle.DamageDirection direction,
            double now)
        {
            if (profile == null || profile.FrameworkData == null)
            {
                return 0f;
            }
            string motion;
            switch (direction)
            {
                case Shared.Battle.DamageDirection.Back:
                    motion = profile.FrameworkData.DamageBackMotion;
                    break;
                case Shared.Battle.DamageDirection.Left:
                    motion = profile.FrameworkData.DamageLeftMotion;
                    break;
                case Shared.Battle.DamageDirection.Right:
                    motion = profile.FrameworkData.DamageRightMotion;
                    break;
                default:
                    motion = profile.FrameworkData.DamageFrontMotion;
                    break;
            }
            _debugActivity = "Damage " + direction;
            return PlayOneShot(motion, now);
        }

        internal float PlayBlow(
            AnimalCombatProfile profile,
            double now)
        {
            _debugActivity = "Blow";
            return profile == null || profile.FrameworkData == null
                ? 0f
                : PlayOneShot(profile.FrameworkData.BlowMotion, now);
        }

        internal float PlayGroggy(AnimalCombatProfile profile)
        {
            _debugActivity = "Groggy";
            if (profile == null || profile.FrameworkData == null)
            {
                return 0f;
            }
            return PlayLoop(
                profile.FrameworkData.GroggyMotion,
                0.08f,
                1f);
        }

        internal float PlayKnockDownBegin(
            AnimalCombatProfile profile,
            double now)
        {
            _debugActivity = "KnockDown Begin";
            return profile == null || profile.FrameworkData == null
                ? 0f
                : PlayOneShot(
                    profile.FrameworkData.KnockDownBeginMotion,
                    now);
        }

        internal float PlayKnockDownDuring(AnimalCombatProfile profile)
        {
            _debugActivity = "KnockDown During";
            if (profile == null || profile.FrameworkData == null)
            {
                return 0f;
            }
            return PlayLoop(
                profile.FrameworkData.KnockDownDuringMotion,
                0.08f,
                1f);
        }

        internal float PlayKnockDownEnd(
            AnimalCombatProfile profile,
            double now)
        {
            _debugActivity = "KnockDown End";
            return profile == null || profile.FrameworkData == null
                ? 0f
                : PlayOneShot(
                    profile.FrameworkData.KnockDownEndMotion,
                    now);
        }

        internal void ProcessRootMotion(double now)
        {
            if (_disposed ||
                _rootMotionCurve == null && _actionPlan == null)
            {
                return;
            }

            float elapsed = Mathf.Max(
                0f,
                (float)(now - _rootMotionStartedAt));
            Vector2 desired = _actionPlan == null
                ? _rootMotionCurve.GetLocalDelta(elapsed)
                : _actionPlan.GetLocalPositionDelta(elapsed);
            Vector2 localStep = desired - _previousRootMotionDelta;
            _previousRootMotionDelta = desired;
            if (_actionPlan != null)
            {
                _animal.TurnToYaw(
                    _actionPlan.GetPlannedActorYaw(elapsed),
                    true);
            }
            if (localStep.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 forward2 = new Vector2(
                Mathf.Sin(_rootMotionYaw * Mathf.Deg2Rad),
                Mathf.Cos(_rootMotionYaw * Mathf.Deg2Rad));
            Vector2 right2 = new Vector2(forward2.y, -forward2.x);
            Vector2 world2 = right2 * localStep.x +
                forward2 * localStep.y;
            Vector3 requested = new Vector3(world2.x, 0f, world2.y);
            Vector3 current = _animal.CurrentPosition;
            CollisionParam collision = Collisions.CreateCollisionParam(
                current,
                requested);
            Vector3 accepted = Collisions.ProcessSimpleSliding(collision);
            Vector3 next = current + accepted;
            next.y = current.y;
            _animal.CurrentPosition = next;
        }

        internal bool MoveToward(
            Vector3 destination,
            float stopDistance,
            float deltaSeconds,
            out float movedRatio)
        {
            movedRatio = 0f;
            if (_disposed || deltaSeconds <= 0f)
            {
                return false;
            }

            Vector3 current = _animal.CurrentPosition;
            Vector3 delta = destination - current;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= Mathf.Max(0f, stopDistance))
            {
                return true;
            }

            Vector3 direction = delta / Mathf.Max(0.001f, distance);
            FaceDirection(direction, false);
            PlayMove();
            float requestedDistance = Mathf.Min(
                MoveSpeed * deltaSeconds,
                Mathf.Max(0f, distance - stopDistance));
            Vector3 requested = direction * requestedDistance;
            CollisionParam collision = Collisions.CreateCollisionParam(
                current,
                requested);
            Vector3 accepted = Collisions.ProcessSimpleSliding(collision);
            if (requestedDistance > 0.001f)
            {
                movedRatio = accepted.magnitude / requestedDistance;
            }

            Vector3 next = current + accepted;
            next.y = current.y;
            _animal.CurrentPosition = next;
            return distance - accepted.magnitude <= stopDistance;
        }

        internal void MoveAwayFrom(
            Vector3 threat,
            float speedMultiplier,
            float deltaSeconds)
        {
            if (_disposed || deltaSeconds <= 0f)
            {
                return;
            }
            Vector3 current = _animal.CurrentPosition;
            Vector3 direction = current - threat;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                float yaw = _animal.CurrentYaw * Mathf.Deg2Rad;
                direction = new Vector3(
                    Mathf.Sin(yaw),
                    0f,
                    Mathf.Cos(yaw));
            }
            direction.Normalize();
            FaceDirection(direction, false);
            PlayMove();
            Vector3 requested = direction * MoveSpeed *
                Mathf.Max(1f, speedMultiplier) * deltaSeconds;
            CollisionParam collision = Collisions.CreateCollisionParam(
                current,
                requested);
            Vector3 accepted = Collisions.ProcessSimpleSliding(collision);
            Vector3 next = current + accepted;
            next.y = current.y;
            _animal.CurrentPosition = next;
        }

        internal bool MoveBackwardFromUntil(
            Vector3 threat,
            float stopDistance,
            float speedMultiplier,
            float deltaSeconds,
            out float movedRatio)
        {
            movedRatio = 0f;
            if (_disposed || deltaSeconds <= 0f)
            {
                return false;
            }

            Vector3 current = _animal.CurrentPosition;
            Vector3 away = current - threat;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance >= Mathf.Max(0f, stopDistance))
            {
                return true;
            }
            if (distance <= 0.001f)
            {
                float yaw = _animal.CurrentYaw * Mathf.Deg2Rad;
                away = new Vector3(
                    -Mathf.Sin(yaw),
                    0f,
                    -Mathf.Cos(yaw));
            }
            else
            {
                away /= distance;
            }

            // Reposition is a back-step, not a flee: keep the actor facing
            // the target, play the original move clip in reverse, and move
            // the logical base away through the same collision path used by
            // all other controller movement.
            FacePosition(threat, false);
            PlayMoveBackward();
            float requestedDistance = Mathf.Min(
                MoveSpeed * Mathf.Max(0.1f, speedMultiplier) * deltaSeconds,
                Mathf.Max(0f, stopDistance - distance));
            Vector3 requested = away * requestedDistance;
            CollisionParam collision = Collisions.CreateCollisionParam(
                current,
                requested);
            Vector3 accepted = Collisions.ProcessSimpleSliding(collision);
            if (requestedDistance > 0.001f)
            {
                movedRatio = accepted.magnitude / requestedDistance;
            }
            Vector3 next = current + accepted;
            next.y = current.y;
            _animal.CurrentPosition = next;
            Vector3 remaining = next - threat;
            remaining.y = 0f;
            return remaining.magnitude >= stopDistance - 0.5f;
        }

        internal float FacePosition(Vector3 target, bool snap)
        {
            return FacePositionInternal(
                target,
                snap,
                null,
                float.MaxValue);
        }

        internal float FacePositionWithTurnAnimation(
            Vector3 target,
            AnimalCombatProfile profile,
            float animationThresholdDegrees)
        {
            return FacePositionInternal(
                target,
                false,
                profile,
                Mathf.Max(0f, animationThresholdDegrees));
        }

        private float FacePositionInternal(
            Vector3 target,
            bool snap,
            AnimalCombatProfile profile,
            float animationThresholdDegrees)
        {
            Vector3 direction = target - _animal.CurrentPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return 0f;
            }
            float desiredYaw = Mathf.Atan2(direction.x, direction.z) *
                Mathf.Rad2Deg;
            float signedError = Mathf.DeltaAngle(
                _animal.CurrentYaw,
                desiredYaw);
            _debugSignedYawError = signedError;
            _animal.SetRotateSpeed(RotateSpeed);
            _animal.TurnToYaw(desiredYaw, snap);

            float absoluteError = Mathf.Abs(signedError);
            if (!snap && profile != null && _moveMotion != null)
            {
                if (!string.IsNullOrEmpty(_activeTurnMotion) &&
                    Time.time < _turnMotionUntil &&
                    _animal.IsAnimPlaying)
                {
                    _debugActivity =
                        (_activeTurnActivity ?? "Turn") + " finish";
                    // A movement turn is an atomic facing action. Keep the
                    // controller out of walk/attack until its one-shot pose or
                    // side-step loop has visibly completed.
                    return Mathf.Max(
                        absoluteError,
                        animationThresholdDegrees + 1f);
                }
                if (absoluteError > animationThresholdDegrees)
                {
                    // A target behind the animal uses the framework's authored
                    // 180-degree Turn once. Side corrections retain the small
                    // CW/CCW rotate-in-place cycles.
                    bool reverseTurn =
                        absoluteError >= ReverseTurnThresholdDegrees &&
                        !string.IsNullOrEmpty(
                            _moveMotion.turn_reverse_motion);
                    string motion;
                    string activity;
                    if (reverseTurn)
                    {
                        motion = _moveMotion.turn_reverse_motion;
                        activity = "Turn Reverse Once";
                    }
                    else
                    {
                        // Positive Unity yaw turns clockwise when viewed from
                        // above, so +DeltaAngle selects the CW animation.
                        motion = signedError > 0f
                            ? _moveMotion.rot_motion_cw
                            : _moveMotion.rot_motion_ccw;
                        activity = signedError > 0f
                            ? "Turn CW"
                            : "Turn CCW";
                    }
                    _debugActivity = activity;
                    float playback = _moveMotion.rot_playback_rate > 0f
                        ? _moveMotion.rot_playback_rate
                        : 1f;
                    if (!string.IsNullOrEmpty(motion))
                    {
                        bool changed = !string.Equals(
                            _activeTurnMotion,
                            motion,
                            StringComparison.Ordinal);
                        float length = reverseTurn
                            ? PlayTurnOneShot(
                                motion,
                                0.08f,
                                playback)
                            : PlayTurnLoop(
                                motion,
                                0.08f,
                                playback);
                        if (changed)
                        {
                            _activeTurnMotion = motion;
                            _activeTurnActivity = activity;
                            float visibleSeconds = length > 0f
                                ? length / Mathf.Max(0.01f, playback)
                                : 0.6f;
                            _turnMotionUntil = Time.time + Mathf.Clamp(
                                visibleSeconds,
                                0.35f,
                                reverseTurn ? 2.5f : 1f);
                        }
                    }
                    else
                    {
                        _debugActivity = "Turn clip missing";
                        PlayStand(profile, true);
                    }
                }
                else
                {
                    // Facing is already inside tolerance. Stand is an idle
                    // result here, never the animation used to perform a turn.
                    PlayStand(profile, true);
                }
            }
            else
            {
                _debugActivity = snap ? "Face SNAP" : "Face Smooth";
            }
            return snap ? 0f : absoluteError;
        }

        internal void Stop(AnimalCombatProfile profile, bool battle)
        {
            PlayStand(profile, battle);
        }

        private void FaceDirection(Vector3 direction, bool snap)
        {
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }
            float yaw = Mathf.Atan2(direction.x, direction.z) *
                Mathf.Rad2Deg;
            _animal.SetRotateSpeed(RotateSpeed);
            _animal.TurnToYaw(yaw, snap);
        }

        private float PlayLoop(
            string motion,
            float fadeSeconds,
            float playbackRate)
        {
            return PlayLoopInternal(
                motion,
                fadeSeconds,
                playbackRate,
                false);
        }

        private float PlayTurnLoop(
            string motion,
            float fadeSeconds,
            float playbackRate)
        {
            if (string.IsNullOrEmpty(motion))
            {
                return 0f;
            }
            if (string.Equals(
                    _playingMotion,
                    motion,
                    StringComparison.Ordinal) &&
                _animal.IsAnimPlaying)
            {
                AnimationState current = _animal.GetCurAnimState();
                return current == null ? 0f : current.length;
            }

            EndRootMotion();
            _animal.ResetRootMotionOffset();
            // Tricera_Rotate_CW/CCW contain a small cyclic Bip001 orbit that
            // returns to the origin. Compensating that orbit by moving the
            // mesh transform also drags the rendered target ring away from
            // the logical actor. Keep these two facing clips in-place and let
            // TurnToYaw remain the sole owner of the actor's real yaw.
            _animal.RootMotionMovable.SetInPlaceMotionMode(true);
            _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
            _animal.SetActivateRootMotion(true);
            _playingMotion = motion;
            _debugRequestedMotion = motion;
            _debugCrossFadeLength = _animal.CrossFade(
                motion,
                fadeSeconds,
                true,
                0f,
                playbackRate);
            return _debugCrossFadeLength;
        }

        private float PlayTurnOneShot(
            string motion,
            float fadeSeconds,
            float playbackRate)
        {
            if (string.IsNullOrEmpty(motion))
            {
                return 0f;
            }
            EndRootMotion();
            _animal.ResetRootMotionOffset();
            _animal.RootMotionMovable.SetInPlaceMotionMode(false);
            // TurnToYaw owns the logical yaw. Compensate the clip's authored
            // root yaw so the mesh, target ring and CurrentPosition remain one
            // actor while the feet/body perform the turn pose.
            _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
            _animal.SetActivateRootMotion(true);
            _playingMotion = motion;
            _debugRequestedMotion = motion;
            _debugCrossFadeLength = _animal.CrossFade(
                motion,
                fadeSeconds,
                false,
                0f,
                playbackRate);
            return _debugCrossFadeLength;
        }

        private float PlayLoopInternal(
            string motion,
            float fadeSeconds,
            float playbackRate,
            bool preserveLocalRootYaw)
        {
            if (string.IsNullOrEmpty(motion))
            {
                return 0f;
            }
            if (string.Equals(
                    _playingMotion,
                    motion,
                    StringComparison.Ordinal) &&
                _animal.IsAnimPlaying)
            {
                AnimationState current = _animal.GetCurAnimState();
                return current == null ? 0f : current.length;
            }
            EndRootMotion();
            // Looping locomotion/stand/rotate clips do not move the logical
            // actor. Always clear the outgoing visual offset on a clip handoff
            // so target rings and the rendered mesh return to CurrentPosition.
            _animal.ResetRootMotionOffset();
            _animal.RootMotionMovable.SetInPlaceMotionMode(false);
            // Root yaw and position are visual data. The logical actor is
            // already rotated through TurnToYaw, so compensate both here to
            // keep the rendered animal, target ring and CurrentPosition on
            // the same base point.
            _animal.RootMotionMovable.SetLocalRootMotionYawMode(
                preserveLocalRootYaw);
            _animal.SetActivateRootMotion(true);
            _playingMotion = motion;
            _debugRequestedMotion = motion;
            _debugCrossFadeLength = _animal.CrossFade(
                motion,
                fadeSeconds,
                true,
                0f,
                playbackRate);
            return _debugCrossFadeLength;
        }

        private void PlayMoveBackward()
        {
            if (_disposed || _moveMotion == null ||
                string.IsNullOrEmpty(_moveMotion.motion))
            {
                return;
            }
            float playback = _moveMotion.playback_rate > 0f
                ? _moveMotion.playback_rate
                : 1f;
            CancelTurnMotion();
            _debugActivity = "Reposition Back";
            PlayLoop(_moveMotion.motion, 0.12f, playback);
            AnimationState state = _animal.GetCurAnimState();
            if (state == null || !string.Equals(
                state.name,
                _moveMotion.motion,
                StringComparison.Ordinal))
            {
                return;
            }
            if (state.speed >= 0f || state.time <= 0f)
            {
                state.time = state.length;
            }
            state.speed = -Mathf.Abs(playback);
        }

        private float PlayOneShot(string motion, double now)
        {
            if (_disposed || string.IsNullOrEmpty(motion))
            {
                return 0f;
            }
            CancelTurnMotion();
            _playingMotion = motion;
            _debugRequestedMotion = motion;
            float length = _animal.CrossFade(
                motion,
                0.08f,
                false,
                0f,
                1f);
            _debugCrossFadeLength = length;
            BeginRootMotion(motion, now);
            if (_rootMotionCurve != null)
            {
                length = Mathf.Max(length, _rootMotionCurve.Duration);
            }
            return Mathf.Max(0f, length);
        }

        private float PlayDirectionalEvade(
            string motion,
            SaurusEvadeRoute route,
            double now)
        {
            if (_disposed || string.IsNullOrEmpty(motion))
            {
                return 0f;
            }
            CancelTurnMotion();
            _playingMotion = motion;
            _debugRequestedMotion = motion;
            float length = _animal.CrossFade(
                motion,
                0.08f,
                false,
                0f,
                1f);
            _debugCrossFadeLength = length;

            SaurusRootMotionCurve curve;
            float projectionYaw = _animal.CurrentYaw;
            if (SaurusRootMotionData.TryGet(motion, out curve))
            {
                Vector2 localTravel = curve.GetLocalDelta(curve.Duration);
                if (localTravel.sqrMagnitude > 0.0001f)
                {
                    float desiredWorldYaw = ResolveEvadeWorldYaw(route);
                    float localTravelYaw = Mathf.Atan2(
                        localTravel.x,
                        localTravel.y) * Mathf.Rad2Deg;
                    projectionYaw = desiredWorldYaw - localTravelYaw;
                }
            }

            BeginRootMotion(motion, now, projectionYaw);
            if (_rootMotionCurve != null)
            {
                length = Mathf.Max(length, _rootMotionCurve.Duration);
            }
            return Mathf.Max(0f, length);
        }

        private float ResolveEvadeWorldYaw(SaurusEvadeRoute route)
        {
            switch (route)
            {
                case SaurusEvadeRoute.Left:
                    return _animal.CurrentYaw - 90f;
                case SaurusEvadeRoute.Right:
                    return _animal.CurrentYaw + 90f;
                case SaurusEvadeRoute.Backward:
                    return _animal.CurrentYaw + 180f;
                default:
                    return _animal.CurrentYaw;
            }
        }

        private void BeginRootMotion(string motion, double now)
        {
            BeginRootMotion(motion, now, _animal.CurrentYaw);
        }

        private void BeginRootMotion(
            string motion,
            double now,
            float projectionYaw)
        {
            EndRootMotion();
            SaurusRootMotionCurve curve;
            if (!SaurusRootMotionData.TryGet(motion, out curve))
            {
                return;
            }
            _rootMotionCurve = curve;
            _actionPlan = null;
            _rootMotionStartedAt = now;
            _previousRootMotionDelta = Vector2.zero;
            _rootMotionYaw = projectionYaw;
            _animal.ResetRootMotionOffset();
            // A previous CW/CCW facing clip may have enabled in-place mode.
            // Attacks and reactions need visual root compensation while this
            // adapter replays their authored curve onto the logical actor.
            _animal.RootMotionMovable.SetInPlaceMotionMode(false);
            // RootMotionMovable removes the animated Bip001 displacement
            // from the mesh.  The adapter applies the same original curve to
            // AnimalBehavior.CurrentPosition through collision processing.
            // This runtime locks attack yaw at commit. Keep the original
            // RootMotionMovable yaw compensation enabled (local-yaw mode is
            // false), so baked Bip001 turns do not rotate only the rendered
            // animal away from the transform used by hit geometry.
            _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
            _animal.SetActivateRootMotion(true);
        }

        private void BeginActionPlan(SaurusActionPlan plan, double now)
        {
            EndRootMotion();
            if (plan == null)
            {
                return;
            }
            _actionPlan = plan;
            _rootMotionStartedAt = now;
            _previousRootMotionDelta = Vector2.zero;
            _rootMotionYaw = plan.ActorYawAtCommit;
            _animal.ResetRootMotionOffset();
            _animal.RootMotionMovable.SetInPlaceMotionMode(false);
            _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
            _animal.SetActivateRootMotion(true);
        }

        private void EndRootMotion()
        {
            if (_rootMotionCurve == null && _actionPlan == null)
            {
                return;
            }
            _rootMotionCurve = null;
            _actionPlan = null;
            _previousRootMotionDelta = Vector2.zero;
            _animal.ResetRootMotionOffset();
            // Do not disable visual compensation between an attack and its
            // recovery/stand cross-fade.  Exposing the outgoing clip's root
            // for one rendered frame produces the remote-position flash seen
            // at the end of attacks.
        }

        private void CancelTurnMotion()
        {
            bool hadTurnMotion = !string.IsNullOrEmpty(_activeTurnMotion);
            _activeTurnMotion = null;
            _activeTurnActivity = null;
            _turnMotionUntil = 0f;
            if (hadTurnMotion && _animal != null)
            {
                // Never leak the special CW/CCW in-place mode into an attack,
                // reaction, movement or recovery clip.
                _animal.ResetRootMotionOffset();
                _animal.RootMotionMovable.SetInPlaceMotionMode(false);
                _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
            }
        }

        private void ResolveMoveMotion()
        {
            if (_animal == null ||
                _animal.AnimalFrameworkResource == null)
            {
                return;
            }
            AnimationElemMoveSet element =
                _animal.AnimalFrameworkResource.GetAnimationElements(
                    "move_motion_sets") as AnimationElemMoveSet;
            if (element == null || element.elems == null ||
                element.elems.Count == 0 || element.elems[0] == null)
            {
                return;
            }
            _moveMotion = element.elems[0].GetMoveMotion(float.MaxValue);
        }

        private void TakeMovementOwnership()
        {
            if (_animal == null)
            {
                return;
            }
            _animal.PathMovable.Clear();
            // The shared controller owns logical movement for its whole
            // lifetime.  RootMotionMovable continuously cancels visual root
            // displacement, while attack/reaction curves are replayed onto
            // AnimalBehavior.CurrentPosition by this adapter.
            _animal.RootMotionMovable.SetInPlaceMotionMode(false);
            _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
            _animal.ResetRootMotionOffset();
            _animal.SetActivateRootMotion(true);
            _animal.SetRotateSpeed(RotateSpeed);
        }

        private void StabilizePresentationBase()
        {
            if (_disposed || _meshObjectTransform == null ||
                _rootBoneTransform == null ||
                object.ReferenceEquals(
                    _meshObjectTransform,
                    _rootBoneTransform))
            {
                return;
            }

            Vector3 parentOffset =
                _meshObjectTransform.localPosition -
                _initialMeshLocalPosition;
            if (parentOffset.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            // RootMotionMovable compensates an authored Bip001 curve by
            // translating the complete MeshObject parent. That keeps the
            // skinned animal at the controller-owned logical position, but
            // also moves non-bone floor renderers such as the red target
            // ring. Preserve the already-correct root-bone world position,
            // restore the model parent to its prefab base, then transfer the
            // compensation to Bip001 itself. The mesh pose is unchanged;
            // only presentation children that represent the logical base
            // stop inheriting the visual root offset.
            Vector3 rootWorldPosition = _rootBoneTransform.position;
            _meshObjectTransform.localPosition =
                _initialMeshLocalPosition;
            _rootBoneTransform.position = rootWorldPosition;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            SaurusMotionAdapter registered;
            if (_objectInstanceId != 0 &&
                ActiveAdapters.TryGetValue(
                    _objectInstanceId,
                    out registered) &&
                object.ReferenceEquals(registered, this))
            {
                ActiveAdapters.Remove(_objectInstanceId);
            }
            if (_animal != null)
            {
                EndRootMotion();
                _animal.PathMovable.Clear();
                _animal.RootMotionMovable.SetLocalRootMotionYawMode(false);
                _animal.ResetRootMotionOffset();
                _animal.SetActivateRootMotion(false);
            }
        }
    }
}
