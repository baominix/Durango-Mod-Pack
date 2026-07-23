using System;
using Durango.Logic;
using UnityEngine;

namespace BaoX.DurangoOriginal.CombatSystemMod.Geometry
{
    internal struct PlayerAttackArea
    {
        internal Vector3 Origin;
        internal Vector3 Forward;
        internal WeaponSkillAoEProfile Profile;
    }

    internal static class AttackGeometry
    {
        internal static bool UsesKylloxProfile(string actionId)
        {
            return WeaponSkillTuning.IsSmallAoEAction(actionId);
        }

        internal static float GetMaximumRange(string actionId, float configuredRange)
        {
            return Mathf.Max(configuredRange, WeaponSkillTuning.GetHitRange(actionId));
        }

        internal static bool TryCreatePlayerArea(
            string actionId,
            int hitIndex,
            Vector3 primaryPosition,
            out PlayerAttackArea area)
        {
            area = default(PlayerAttackArea);
            if (!UsesKylloxProfile(actionId) || PlayerBehavior.LocalPlayer == null)
            {
                return false;
            }

            WeaponSkillAoEProfile profile =
                WeaponSkillTuning.GetPlayerAttackAoEProfile(actionId, hitIndex);
            Vector3 origin = PlayerBehavior.LocalPlayer.CurrentPosition;
            Vector3 forward = WeaponSkillTuning.UsePlayerForwardForAoE(profile.Name)
                ? PlayerBehavior.LocalPlayer.transform.forward
                : primaryPosition - origin;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = PlayerBehavior.LocalPlayer.transform.forward;
                forward.y = 0f;
            }
            if (forward.sqrMagnitude <= 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            origin += forward * profile.ForwardOffset + right * profile.SideOffset;
            area.Origin = origin;
            area.Forward = forward;
            area.Profile = profile;
            return true;
        }

        internal static bool Contains(PlayerAttackArea area, Vector3 point)
        {
            Vector3 delta = point - area.Origin;
            delta.y = 0f;
            Vector3 right = new Vector3(area.Forward.z, 0f, -area.Forward.x);
            float forwardDistance = Vector3.Dot(delta, area.Forward);
            float sideDistance = Mathf.Abs(Vector3.Dot(delta, right));
            WeaponSkillAoEProfile profile = area.Profile;

            if (string.Equals(profile.Shape, "circle", StringComparison.OrdinalIgnoreCase))
            {
                return delta.sqrMagnitude <= profile.Length * profile.Length;
            }
            if (string.Equals(profile.Shape, "half-circle", StringComparison.OrdinalIgnoreCase))
            {
                return forwardDistance >= 0f &&
                    delta.sqrMagnitude <= profile.Length * profile.Length;
            }
            if (string.Equals(profile.Shape, "half-ellipse", StringComparison.OrdinalIgnoreCase))
            {
                float forwardRadius = Mathf.Max(1f, profile.Length);
                float sideRadius = Mathf.Max(1f, profile.HalfWidth);
                float forwardRatio = forwardDistance / forwardRadius;
                float sideRatio = sideDistance / sideRadius;
                return forwardDistance >= 0f &&
                    forwardRatio * forwardRatio + sideRatio * sideRatio <= 1f;
            }

            return forwardDistance >= 0f &&
                forwardDistance <= profile.Length &&
                sideDistance <= profile.HalfWidth;
        }
    }
}
