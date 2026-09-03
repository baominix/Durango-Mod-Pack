using System;
using Shared.Battle;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.Geometry
{
    internal static class AttackGeometry
    {
        private const float Epsilon = 0.001f;

        internal static bool Contains(
            AttackSnapshot attack,
            Vector2 targetPosition,
            float targetRadius)
        {
            if (attack == null)
            {
                return false;
            }

            targetRadius = Mathf.Max(0f, targetRadius);
            Vector2 center = attack.Center.ToVector2();
            Vector2 delta = targetPosition - center;

            switch (attack.DamageType)
            {
                case DamageType.Melee:
                case DamageType.Ranged:
                    // Melee and Ranged are selected-target damage types in the
                    // original player action data. Their radius is reach/range,
                    // not an area query.
                    return false;
                case DamageType.CircularArea:
                    return ContainsCircleOrArc(
                        delta,
                        attack.Radius,
                        attack.Angles,
                        attack.Yaw,
                        targetRadius);
                case DamageType.RectangularArea:
                    return ContainsRectangle(
                        delta,
                        attack.RectSizeHalves,
                        attack.Yaw,
                        targetRadius);
                default:
                    return false;
            }
        }

        private static bool ContainsCircleOrArc(
            Vector2 delta,
            float? radius,
            Vector2? angles,
            float yaw,
            float targetRadius)
        {
            if (!radius.HasValue || radius.Value <= 0f)
            {
                return false;
            }

            float distance = delta.magnitude;
            if (distance > radius.Value + targetRadius)
            {
                return false;
            }
            if (!angles.HasValue || distance <= targetRadius + Epsilon)
            {
                return true;
            }

            float pointYaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float relative = Normalize360(pointYaw - yaw);
            float start = Normalize360(angles.Value.x);
            float end = Normalize360(angles.Value.y);
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

        private static bool ContainsRectangle(
            Vector2 delta,
            Vector2? halfSize,
            float yaw,
            float targetRadius)
        {
            if (!halfSize.HasValue ||
                halfSize.Value.x <= 0f || halfSize.Value.y <= 0f)
            {
                return false;
            }

            Vector2 forward = Forward(yaw);
            Vector2 right = new Vector2(forward.y, -forward.x);
            float localRight = Vector2.Dot(delta, right);
            float localForward = Vector2.Dot(delta, forward);

            // FillBorderAlert.MakeRect interprets the first value as the
            // dimension along its yaw vector (forward), and the second value
            // as the perpendicular dimension (right).  Keep the damage query
            // in that exact order so the visible rectangle and hit area are
            // identical.  Sunder is [350,100]: long forward, narrow sideways.
            return Mathf.Abs(localForward) <=
                    halfSize.Value.x + targetRadius &&
                Mathf.Abs(localRight) <=
                    halfSize.Value.y + targetRadius;
        }

        internal static Vector2 Forward(float yaw)
        {
            float radians = (90f - yaw) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
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

        private static float AngularDistance(float a, float b)
        {
            float difference = Mathf.Abs(Normalize360(a) - Normalize360(b));
            return Mathf.Min(difference, 360f - difference);
        }

        private static float Normalize360(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }
    }
}
