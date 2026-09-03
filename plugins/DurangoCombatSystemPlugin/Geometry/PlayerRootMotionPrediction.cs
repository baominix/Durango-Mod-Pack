using System;
using Durango.Player.Animation;
using Durango.Utils;
using UnityEngine;
using Yaml;

namespace Baominix.DurangoOriginal.CombatSystem.Geometry
{
    /// <summary>
    /// Predicts the authoritative player base position at each attack_time
    /// from the original player animation root-motion path.
    /// </summary>
    internal static class PlayerRootMotionPrediction
    {
        // LocalMoveOperator uses a 20-unit player capsule before applying
        // animation root motion.  Keep the initial combat forecast outside
        // the selected animal's current bound as well.
        private const float PlayerCollisionRadius = 20f;
        private const float CollisionSkin = 2f;

        /// <summary>
        /// Immutable per-frame sample.  Multi-hit actions share this object so
        /// the animation clip, root path and current animation time are read
        /// once rather than once for every pending hit.
        /// </summary>
        internal sealed class Sample
        {
            private readonly PlayerRootMotionPath _path;
            private readonly float _elapsed;
            private readonly float _lastSampleTime;
            private readonly Vector2 _forward;
            private readonly Vector2 _right;

            internal Sample(
                PlayerRootMotionPath path,
                float elapsed,
                float lastSampleTime,
                float actorYaw)
            {
                _path = path;
                _elapsed = elapsed;
                _lastSampleTime = lastSampleTime;
                _forward = AttackGeometry.Forward(actorYaw);
                _right = new Vector2(_forward.y, -_forward.x);
            }

            internal Vector2 GetRemainingWorldDelta(float attackTime)
            {
                if (_path == null || attackTime <= 0f)
                {
                    return Vector2.zero;
                }

                float elapsed = _elapsed;
                if (_lastSampleTime > 0f)
                {
                    elapsed = Mathf.Min(elapsed, _lastSampleTime);
                    attackTime = Mathf.Min(
                        attackTime,
                        _lastSampleTime);
                }
                elapsed = Mathf.Min(elapsed, attackTime);

                Vector3 localDelta = _path.GetDelta(
                    elapsed,
                    attackTime);
                return _right * localDelta.x +
                    _forward * localDelta.z;
            }
        }

        internal static Sample CreateSample(
            PlayerAction action,
            bool isMale,
            float actorYaw,
            double actionStartedAt,
            double snapshotAt)
        {
            if (action == null || action.Meta == null ||
                string.IsNullOrEmpty(action.Meta.Motion))
            {
                return null;
            }

            try
            {
                PlayerAnimationClipInfo clip =
                    Singleton<PlayerAnimationClipManager>.Instance()
                        .GetPlayerAnimationClipInfo(action.Meta.Motion);
                if (clip == null)
                {
                    return null;
                }

                PlayerRootMotionPath path = clip.GetPath(isMale);
                if (path == null || path.DeltaTime <= 0f)
                {
                    return null;
                }

                float playbackRate = action.Meta.PlaybackRate ?? 1f;
                if (playbackRate <= 0f)
                {
                    playbackRate = 1f;
                }

                float elapsed;
                if (!TryGetCurrentAnimationTime(
                        action.Meta.Motion,
                        out elapsed))
                {
                    elapsed = Mathf.Max(
                        0f,
                        (float)(snapshotAt - actionStartedAt) *
                            playbackRate);
                }

                return new Sample(
                    path,
                    elapsed,
                    GetLastSampleTime(path),
                    actorYaw);
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogWarning(
                    "Player root-motion sample failed for action=" +
                    action.Id + " motion=" + action.Meta.Motion + ": " +
                    exception.Message);
                return null;
            }
        }

        internal static Vector2 GetRemainingWorldDelta(
            PlayerAction action,
            bool isMale,
            float actorYaw,
            double actionStartedAt,
            double snapshotAt,
            float attackTime)
        {
            Sample sample = CreateSample(
                action,
                isMale,
                actorYaw,
                actionStartedAt,
                snapshotAt);
            return sample == null
                ? Vector2.zero
                : sample.GetRemainingWorldDelta(attackTime);
        }

        internal static Vector2 ClampAgainstTarget(
            Vector2 actorOrigin,
            Vector2 rootMotionDelta,
            Vector2 targetOrigin,
            float targetRadius)
        {
            float distance = rootMotionDelta.magnitude;
            if (distance <= 0.001f)
            {
                return Vector2.zero;
            }

            float collisionRadius = Mathf.Max(0f, targetRadius) +
                PlayerCollisionRadius;
            Vector2 fromTarget = actorOrigin - targetOrigin;
            float radiusSquared = collisionRadius * collisionRadius;

            // If the sampled bounds already overlap, prevent additional
            // root motion toward the target while still allowing movement
            // that separates the two actors.
            if (fromTarget.sqrMagnitude <= radiusSquared)
            {
                return Vector2.Dot(rootMotionDelta, fromTarget) < 0f
                    ? Vector2.zero
                    : rootMotionDelta;
            }

            float a = Vector2.Dot(rootMotionDelta, rootMotionDelta);
            float b = 2f * Vector2.Dot(fromTarget, rootMotionDelta);
            float c = fromTarget.sqrMagnitude - radiusSquared;
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return rootMotionDelta;
            }

            float root = Mathf.Sqrt(discriminant);
            float first = (-b - root) / (2f * a);
            float second = (-b + root) / (2f * a);
            float impact = first >= 0f && first <= 1f
                ? first
                : second;
            if (impact < 0f || impact > 1f)
            {
                return rootMotionDelta;
            }

            float skinRatio = CollisionSkin / distance;
            return rootMotionDelta * Mathf.Clamp01(impact - skinRatio);
        }

        private static float GetLastSampleTime(PlayerRootMotionPath path)
        {
            int count = 0;
            if (path.X != null)
            {
                count = Math.Max(count, path.X.Length);
            }
            if (path.Z != null)
            {
                count = Math.Max(count, path.Z.Length);
            }
            return count <= 1 ? 0f : (count - 1) * path.DeltaTime;
        }

        private static bool TryGetCurrentAnimationTime(
            string motion,
            out float animationTime)
        {
            animationTime = 0f;
            PlayerBehavior player = PlayerBehavior.LocalPlayer;
            if (player == null || player.CurrentPlayerClipInfo == null ||
                !string.Equals(
                    player.CurrentPlayerClipInfo.Clip,
                    motion,
                    StringComparison.Ordinal))
            {
                return false;
            }

            AnimationState state =
                player.Anim[player.MotionPrefix + motion];
            if (state == null)
            {
                return false;
            }
            animationTime = Mathf.Max(0f, state.time);
            return true;
        }
    }
}
