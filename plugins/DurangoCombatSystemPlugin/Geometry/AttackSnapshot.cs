using Messages;
using Baominix.DurangoOriginal.CombatSystem.Runtime;
using Shared.Battle;
using UnityEngine;
using Yaml;

namespace Baominix.DurangoOriginal.CombatSystem.Geometry
{
    internal sealed class AttackSnapshot
    {
        private AttackAlerted _alert;

        internal readonly long ActionInstanceId;
        internal readonly int Generation;
        internal readonly int HitIndex;
        internal readonly string ActorEntityId;
        internal readonly string ActionId;
        internal readonly string SelectedTargetEntityId;
        internal readonly Vector2 ActorOriginAtCommit;
        internal readonly Vector2? TargetOriginAtCommit;
        internal readonly float ActorYawAtCommit;
        internal readonly bool UseTargetOrigin;
        internal readonly PlayerActionAttackInfo Info;

        internal Vector2 ActorOriginAtHit { get; private set; }

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

        private AttackSnapshot(
            long actionInstanceId,
            int generation,
            int hitIndex,
            string actorEntityId,
            string actionId,
            string selectedTargetEntityId,
            Vector2 actorOriginAtCommit,
            Vector2? targetOriginAtCommit,
            float actorYawAtCommit,
            bool useTargetOrigin,
            PlayerActionAttackInfo info,
            AttackAlerted alert)
        {
            ActionInstanceId = actionInstanceId;
            Generation = generation;
            HitIndex = hitIndex;
            ActorEntityId = actorEntityId;
            ActionId = actionId;
            SelectedTargetEntityId = selectedTargetEntityId;
            ActorOriginAtCommit = actorOriginAtCommit;
            TargetOriginAtCommit = targetOriginAtCommit;
            ActorYawAtCommit = actorYawAtCommit;
            UseTargetOrigin = useTargetOrigin;
            Info = info;
            ActorOriginAtHit = actorOriginAtCommit;
            _alert = alert;
        }

        internal static AttackSnapshot Create(
            AcceptedPlayerAction accepted,
            int hitIndex,
            PlayerActionAttackInfo info,
            Vector2 actorOrigin,
            float actorYaw,
            Vector2? targetOrigin)
        {
            if (accepted == null || info == null)
            {
                return null;
            }

            Vector2 geometryOrigin = actorOrigin;
            if (info.UseTargetOrigin && targetOrigin.HasValue)
            {
                geometryOrigin = targetOrigin.Value;
            }

            AttackAlerted? made = info.MakeAlerted(
                geometryOrigin,
                actorYaw,
                accepted.ClientStartAt);
            if (!made.HasValue || !made.Value.Center.HasValue)
            {
                return null;
            }

            AttackAlerted alert = made.Value;
            alert.EntityId = accepted.ActorEntityId;
            return new AttackSnapshot(
                accepted.InstanceId,
                accepted.Generation,
                hitIndex,
                accepted.ActorEntityId,
                accepted.ActionId,
                accepted.TargetEntityId,
                actorOrigin,
                targetOrigin,
                actorYaw,
                info.UseTargetOrigin,
                info,
                alert);
        }

        internal bool RefreshGeometry(
            Vector2 actorOrigin,
            Vector2? targetOrigin)
        {
            Vector2 geometryOrigin = actorOrigin;
            if (UseTargetOrigin && targetOrigin.HasValue)
            {
                geometryOrigin = targetOrigin.Value;
            }

            AttackAlerted? made = Info.MakeAlerted(
                geometryOrigin,
                ActorYawAtCommit,
                EventAt);
            if (!made.HasValue || !made.Value.Center.HasValue)
            {
                return false;
            }

            AttackAlerted refreshed = made.Value;
            refreshed.EntityId = ActorEntityId;
            ActorOriginAtHit = actorOrigin;
            _alert = refreshed;
            return true;
        }

        internal AttackAlerted ToMessage()
        {
            return _alert;
        }
    }
}
