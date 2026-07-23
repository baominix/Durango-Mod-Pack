using System;
using Durango.Logic;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatSystemMod.Geometry
{
    internal enum AnimalAttackShape
    {
        Circle,
        Sector,
        HalfCircle,
        Rectangle
    }

    internal struct AnimalAttackArea
    {
        internal AnimalAttackShape Shape;
        internal Vector3 Origin;
        internal Vector3 Forward;
        internal float Radius;
        internal float Length;
        internal float HalfWidth;
        internal float ArcStart;
        internal float ArcEnd;
        internal string AttackId;
    }

    internal static class AnimalAttackGeometry
    {
        internal static AnimalAttackArea Create(
            AnimalBehavior animal,
            float attackRange,
            float arcStart,
            float arcEnd,
            string attackId,
            Vector3 attackForward)
        {
            Vector3 origin = animal == null ? Vector3.zero : animal.CurrentPosition;
            Vector3 forward = attackForward;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = animal == null ? Vector3.forward : animal.transform.forward;
            }
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            AnimalAttackArea area = new AnimalAttackArea
            {
                Shape = AnimalAttackShape.Circle,
                Origin = origin,
                Forward = forward,
                Radius = Mathf.Max(1f, attackRange),
                Length = Mathf.Max(1f, attackRange),
                HalfWidth = Mathf.Max(1f, attackRange),
                ArcStart = NormalizeAngle(arcStart),
                ArcEnd = NormalizeAngle(arcEnd),
                AttackId = attackId ?? string.Empty
            };

            if (string.Equals(
                attackId,
                BrachioAttackProfiles.FrontAttackId,
                StringComparison.OrdinalIgnoreCase))
            {
                area.Shape = AnimalAttackShape.Circle;
                area.Origin = origin +
                    forward * BrachioAttackProfiles.AreaAttackForwardOffset;
                area.Radius = BrachioAttackProfiles.AreaAttackDistance;
                return area;
            }

            if (string.Equals(
                attackId,
                BrachioAttackProfiles.TailAttackId,
                StringComparison.OrdinalIgnoreCase))
            {
                area.Shape = AnimalAttackShape.HalfCircle;
                // Brachio faces away for a tail attack, but attackForward is the
                // locked direction from the animal toward its target.
                area.Forward = forward;
                area.Radius = BrachioAttackProfiles.TailAreaDistance;
                return area;
            }

            if (string.Equals(
                attackId,
                BrachioAttackProfiles.WoundedTailAttackId,
                StringComparison.OrdinalIgnoreCase))
            {
                area.Shape = AnimalAttackShape.Rectangle;
                area.Forward = forward;
                area.Length = BrachioAttackProfiles.TailAreaDistance;
                area.HalfWidth = BrachioAttackProfiles.TailAreaHalfWidth;
                return area;
            }

            if (Mathf.Abs(area.ArcStart - area.ArcEnd) >= 0.01f)
            {
                area.Shape = AnimalAttackShape.Sector;
            }
            return area;
        }

        internal static bool Contains(AnimalAttackArea area, Vector3 point)
        {
            Vector3 delta = point - area.Origin;
            delta.y = 0f;

            if (area.Shape == AnimalAttackShape.Circle)
            {
                return delta.sqrMagnitude <= area.Radius * area.Radius;
            }

            Vector3 forward = area.Forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            if (area.Shape == AnimalAttackShape.HalfCircle)
            {
                return delta.sqrMagnitude <= area.Radius * area.Radius &&
                    (delta.sqrMagnitude <= 0.001f ||
                    Vector3.Dot(delta.normalized, forward) >= 0f);
            }

            if (area.Shape == AnimalAttackShape.Rectangle)
            {
                float along = Vector3.Dot(delta, forward);
                float side = Mathf.Abs(Vector3.Dot(delta, right));
                return along >= 0f && along <= area.Length &&
                    side <= area.HalfWidth;
            }

            if (delta.sqrMagnitude > area.Radius * area.Radius)
            {
                return false;
            }
            if (delta.sqrMagnitude <= 0.001f)
            {
                return true;
            }

            float angle = Vector3.Angle(forward, delta.normalized);
            if (Vector3.Dot(right, delta.normalized) < 0f)
            {
                angle = 360f - angle;
            }
            angle = NormalizeAngle(angle);
            return IsAngleInside(angle, area.ArcStart, area.ArcEnd);
        }

        internal static bool IsAngleInside(float angle, float start, float end)
        {
            angle = NormalizeAngle(angle);
            start = NormalizeAngle(start);
            end = NormalizeAngle(end);
            if (start <= end)
            {
                return angle >= start && angle <= end;
            }
            return angle >= start || angle <= end;
        }

        internal static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }
    }
}
