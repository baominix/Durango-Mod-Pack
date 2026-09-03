using System;
using Baominix.DurangoOriginal.CombatSystem.Data;
using Baominix.DurangoOriginal.CombatSystem.SaurusAI;
using Messages;
using Shared.Battle;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Geometry
{
    internal sealed class AnimalAttackSnapshot
    {
        private const float Epsilon = 0.001f;
        private AttackAlerted _alert;
        private readonly AttackHitDefinition _hit;
        private readonly Vector2 _targetOriginAtCommit;
        private readonly float _spatialScale;

        internal readonly long AttackInstanceId;
        internal readonly int Generation;
        internal readonly int HitIndex;
        internal readonly AnimalBehavior Animal;
        internal readonly int AnimalObjectInstanceId;
        internal readonly AnimalCombatProfile Profile;
        internal readonly string ActorEntityId;
        internal readonly string AttackId;
        internal readonly string SubActionId;
        internal readonly int Level;
        internal readonly Vector2 ActorOriginAtCommit;
        internal Vector2 ActorOriginAtHit { get; private set; }
        internal readonly float ActorYawAtCommit;
        internal float ActorYawAtHit { get; private set; }
        internal readonly float RadiusMin;
        internal readonly SaurusActionPlan ActionPlan;

        internal DamageType DamageType
        {
            get { return _alert.DamageType; }
        }

        internal double EventAt
        {
            get { return _alert.EventAt; }
        }

        internal double HitAt
        {
            get { return _alert.AttackTime; }
        }

        internal WorldPosition Center
        {
            get { return _alert.Center.Value; }
        }

        internal float Yaw
        {
            get { return _alert.Yaw ?? ActorYawAtCommit; }
        }

        internal float? Radius
        {
            get { return _alert.Radius; }
        }

        internal Vector2? Angles
        {
            get { return _alert.Angles; }
        }

        internal Vector2? RectSizeHalves
        {
            get { return _alert.RectSizeHalves; }
        }

        private AnimalAttackSnapshot(
            long attackInstanceId,
            int generation,
            int hitIndex,
            AnimalBehavior animal,
            int animalObjectInstanceId,
            AnimalCombatProfile profile,
            string attackId,
            string subActionId,
            int level,
            Vector2 actorOriginAtCommit,
            Vector2 actorOriginAtHit,
            float actorYawAtCommit,
            float actorYawAtHit,
            float radiusMin,
            AttackHitDefinition hit,
            Vector2 targetOriginAtCommit,
            float spatialScale,
            SaurusActionPlan actionPlan,
            AttackAlerted alert)
        {
            AttackInstanceId = attackInstanceId;
            Generation = generation;
            HitIndex = hitIndex;
            Animal = animal;
            AnimalObjectInstanceId = animalObjectInstanceId;
            Profile = profile;
            ActorEntityId = animal == null ? null : animal.EntityId;
            AttackId = attackId;
            SubActionId = subActionId;
            Level = level;
            ActorOriginAtCommit = actorOriginAtCommit;
            ActorOriginAtHit = actorOriginAtHit;
            ActorYawAtCommit = actorYawAtCommit;
            ActorYawAtHit = actorYawAtHit;
            RadiusMin = Mathf.Max(0f, radiusMin);
            _hit = hit;
            _targetOriginAtCommit = targetOriginAtCommit;
            _spatialScale = Mathf.Max(0.01f, spatialScale);
            ActionPlan = actionPlan;
            _alert = alert;
        }

        internal static AnimalAttackSnapshot Create(
            long attackInstanceId,
            int generation,
            int hitIndex,
            AnimalBehavior animal,
            AnimalCombatProfile profile,
            string attackId,
            AttackHitDefinition hit,
            double committedAt,
            float frameRate,
            Vector2 actorOriginAtCommit,
            Vector2 actorOriginAtHit,
            float actorYaw,
            float actorYawAtHit,
            Vector2 targetOrigin,
            SaurusActionPlan actionPlan)
        {
            if (animal == null || profile == null || hit == null ||
                frameRate <= 0f || hit.Frame < 0 ||
                hit.DamageType < (int)DamageType.Melee ||
                hit.DamageType > (int)DamageType.Ranged)
            {
                return null;
            }

            DamageType damageType = (DamageType)hit.DamageType;
            float spatialScale = actionPlan == null
                ? 1f
                : actionPlan.SpatialScale;
            Vector2 geometryOrigin = hit.UseTargetOrigin
                ? targetOrigin
                : actorOriginAtHit;
            Vector2 forward = AttackGeometry.Forward(actorYawAtHit);
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 center = geometryOrigin +
                forward * hit.OffsetY * spatialScale +
                right * hit.OffsetX * spatialScale;

            AttackAlerted alert = default(AttackAlerted);
            alert.EntityId = animal.EntityId;
            alert.EventAt = committedAt;
            alert.AttackTime = committedAt + hit.Frame / frameRate;
            alert.DamageType = damageType;
            alert.Center = new WorldPosition(center.x, center.y);
            alert.Yaw = actorYawAtHit + hit.DamageAngle;

            if (damageType == DamageType.RectangularArea)
            {
                if (hit.RectangleHalfWidth <= 0f ||
                    hit.RectangleHalfHeight <= 0f)
                {
                    return null;
                }
                alert.RectSizeHalves = new Vector2(
                    hit.RectangleHalfWidth * spatialScale,
                    hit.RectangleHalfHeight * spatialScale);
            }
            else
            {
                if (hit.Radius <= 0f)
                {
                    return null;
                }
                alert.Radius = hit.Radius * spatialScale;
                if (Mathf.Abs(hit.AngleStart - hit.AngleEnd) > Epsilon)
                {
                    alert.Angles = new Vector2(
                        hit.AngleStart,
                        hit.AngleEnd);
                }
            }

            return new AnimalAttackSnapshot(
                attackInstanceId,
                generation,
                hitIndex,
                animal,
                animal.gameObject.GetInstanceID(),
                profile,
                attackId,
                hit.SubActionId,
                Math.Max(1, animal.Level),
                actorOriginAtCommit,
                actorOriginAtHit,
                actorYaw,
                actorYawAtHit,
                hit.RadiusMin * spatialScale,
                hit,
                targetOrigin,
                spatialScale,
                actionPlan,
                alert);
        }

        internal bool RefreshGeometry(
            Vector2 actorOriginAtHit,
            float actorYawAtHit)
        {
            if (_hit == null)
            {
                return false;
            }
            Vector2 geometryOrigin = _hit.UseTargetOrigin
                ? _targetOriginAtCommit
                : actorOriginAtHit;
            Vector2 forward = AttackGeometry.Forward(actorYawAtHit);
            Vector2 right = new Vector2(forward.y, -forward.x);
            Vector2 center = geometryOrigin +
                forward * _hit.OffsetY * _spatialScale +
                right * _hit.OffsetX * _spatialScale;
            float yaw = actorYawAtHit + _hit.DamageAngle;
            bool changed =
                (center - _alert.Center.Value.ToVector2()).sqrMagnitude >
                    Epsilon * Epsilon ||
                Mathf.Abs(Mathf.DeltaAngle(
                    _alert.Yaw ?? ActorYawAtHit,
                    yaw)) > Epsilon;
            ActorOriginAtHit = actorOriginAtHit;
            ActorYawAtHit = actorYawAtHit;
            _alert.Center = new WorldPosition(center.x, center.y);
            _alert.Yaw = yaw;
            return changed;
        }

        internal AttackAlerted ToMessage()
        {
            return _alert;
        }

        internal bool Contains(Vector2 targetPosition, float targetRadius)
        {
            targetRadius = Mathf.Max(0f, targetRadius);
            Vector2 delta = targetPosition - Center.ToVector2();

            if (DamageType == DamageType.RectangularArea)
            {
                if (!RectSizeHalves.HasValue)
                {
                    return false;
                }
                Vector2 forward = AttackGeometry.Forward(Yaw);
                Vector2 right = new Vector2(forward.y, -forward.x);
                float localForward = Vector2.Dot(delta, forward);
                float localRight = Vector2.Dot(delta, right);
                return Mathf.Abs(localForward) <=
                        RectSizeHalves.Value.x + targetRadius &&
                    Mathf.Abs(localRight) <=
                        RectSizeHalves.Value.y + targetRadius;
            }

            if (!Radius.HasValue || Radius.Value <= 0f)
            {
                return false;
            }
            float distance = delta.magnitude;
            if (distance > Radius.Value + targetRadius ||
                distance + targetRadius < RadiusMin)
            {
                return false;
            }
            if (!Angles.HasValue || distance <= targetRadius + Epsilon)
            {
                return true;
            }

            float pointYaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float relative = Normalize360(pointYaw - Yaw);
            float start = Normalize360(Angles.Value.x);
            float end = Normalize360(Angles.Value.y);
            if (IsAngleInArc(relative, start, end))
            {
                return true;
            }

            float angularRadius = 0f;
            if (targetRadius > 0f && distance > Epsilon)
            {
                angularRadius = Mathf.Asin(
                    Mathf.Clamp01(targetRadius / distance)) * Mathf.Rad2Deg;
            }
            return AngularDistance(relative, start) <= angularRadius ||
                AngularDistance(relative, end) <= angularRadius;
        }

        private static bool IsAngleInArc(
            float angle,
            float start,
            float end)
        {
            if (start <= end)
            {
                return angle >= start && angle <= end;
            }
            return angle >= start || angle <= end;
        }

        private static float AngularDistance(float first, float second)
        {
            float difference = Mathf.Abs(
                Normalize360(first) - Normalize360(second));
            return Mathf.Min(difference, 360f - difference);
        }

        private static float Normalize360(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }
    }
}
