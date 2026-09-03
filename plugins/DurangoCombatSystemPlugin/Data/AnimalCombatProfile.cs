using System;
using System.Collections.Generic;
using Shared.Battle;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal sealed class DirectionalValues
    {
        internal float Front;
        internal float Right;
        internal float Back;
        internal float Left;
    }

    internal sealed class AnimalDefenseRatios
    {
        internal float Impact;
        internal float Pierce;
        internal float Cut;
    }

    internal sealed class AnimalBreakStatus
    {
        internal string Id;
        internal int Level;
    }

    internal sealed class AnimalBodyPartProfile
    {
        internal BodyPart Part;
        internal float HpRatio;
        internal float DodgeRatio;
        internal AnimalDefenseRatios DefenseRatios;
        internal readonly List<AnimalBreakStatus> StatusEffects =
            new List<AnimalBreakStatus>();
    }

    internal sealed class BodyPartWeights
    {
        internal float Head;
        internal float Body;
        internal float Arm;
        internal float Leg;
        internal float Tail;
        internal float Back;

        internal float Get(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return Head;
                case BodyPart.Body: return Body;
                case BodyPart.Arm: return Arm;
                case BodyPart.Leg: return Leg;
                case BodyPart.Tail: return Tail;
                case BodyPart.Back: return Back;
                default: return 0f;
            }
        }

        internal float Total
        {
            get { return Head + Body + Arm + Leg + Tail + Back; }
        }
    }

    internal sealed class DirectionalBodyPartWeights
    {
        internal BodyPartWeights Front;
        internal BodyPartWeights Right;
        internal BodyPartWeights Back;
        internal BodyPartWeights Left;

        internal BodyPartWeights Get(DamageDirection direction)
        {
            switch (direction)
            {
                case DamageDirection.Front: return Front;
                case DamageDirection.Right: return Right;
                case DamageDirection.Back: return Back;
                case DamageDirection.Left: return Left;
                default: return null;
            }
        }
    }

    internal sealed class AnimalCombatProfile
    {
        internal int EntityTypeId;
        internal string InternalName;
        internal string AnimalType;
        internal string Framework;
        internal string ModelPath;
        internal string RootMotions;
        internal string AiFactorId;

        internal string AttackFormula;
        internal string DefenseFormula;
        internal string AttackRatingFormula;
        internal string AccuracyFormula;
        internal string DodgeFormula;
        internal string EvadeFormula;
        internal string LifeMaxFormula;
        internal string GroggyMaxFormula;
        internal string GroggyVelocityFormula;
        internal string BlowResistanceFormula;
        internal string KnockBackResistanceFormula;
        internal string GroggyDurationFormula;
        internal string KnockDownDurationFormula;
        internal string[] GroggySectionFormulas;

        internal float AttackCooltime;
        internal float BoundRadius;
        internal float RepresentScale;
        internal float Difficulty;
        internal int SizeLevel;
        internal DirectionalValues DamageRatios;
        internal DirectionalValues GroggyDamageRatios;
        internal Dictionary<BodyPart, AnimalBodyPartProfile> BodyParts;
        internal DirectionalBodyPartWeights PartProbabilities;

        internal FrameworkSnapshot FrameworkData;
    }

    internal sealed class FrameworkSnapshot
    {
        internal string SourcePath;
        internal string Name;
        internal string StandMotion;
        internal string BattleIdleMotion;
        internal string BattleStandMotion;
        internal string EvadeMotion;
        internal string GroggyMotion;
        internal string BlowMotion;
        internal string KnockDownBeginMotion;
        internal string KnockDownDuringMotion;
        internal string KnockDownEndMotion;
        internal string DamageFrontMotion;
        internal string DamageBackMotion;
        internal string DamageLeftMotion;
        internal string DamageRightMotion;
        internal readonly List<string> AttackKeys = new List<string>();
        internal readonly List<AnimalAttackDefinition> Attacks =
            new List<AnimalAttackDefinition>();
    }

    internal sealed class AnimalAttackDefinition
    {
        internal AnimalAttackDefinition(
            string key,
            string motion,
            bool boundEnemy,
            float rotationSpeed,
            AttackHitDefinition[] hits)
        {
            Key = key;
            Motion = motion;
            BoundEnemy = boundEnemy;
            RotationSpeed = rotationSpeed;
            Hits = hits ?? new AttackHitDefinition[0];
        }

        internal string Key { get; private set; }
        internal string Motion { get; private set; }
        internal bool BoundEnemy { get; private set; }
        internal float RotationSpeed { get; private set; }
        internal AttackHitDefinition[] Hits { get; private set; }
    }

    internal sealed class AttackHitDefinition
    {
        internal AttackHitDefinition(
            int frame,
            string subActionId,
            int damageType,
            float radius,
            float radiusMin,
            float angleStart,
            float angleEnd,
            float rectangleHalfWidth,
            float rectangleHalfHeight,
            float offsetX,
            float offsetY,
            float damageAngle,
            bool useTargetOrigin)
        {
            Frame = frame;
            SubActionId = subActionId;
            DamageType = damageType;
            Radius = radius;
            RadiusMin = radiusMin;
            AngleStart = angleStart;
            AngleEnd = angleEnd;
            RectangleHalfWidth = rectangleHalfWidth;
            RectangleHalfHeight = rectangleHalfHeight;
            OffsetX = offsetX;
            OffsetY = offsetY;
            DamageAngle = damageAngle;
            UseTargetOrigin = useTargetOrigin;
        }

        internal int Frame { get; private set; }
        internal string SubActionId { get; private set; }
        internal int DamageType { get; private set; }
        internal float Radius { get; private set; }
        internal float RadiusMin { get; private set; }
        internal float AngleStart { get; private set; }
        internal float AngleEnd { get; private set; }
        internal float RectangleHalfWidth { get; private set; }
        internal float RectangleHalfHeight { get; private set; }
        internal float OffsetX { get; private set; }
        internal float OffsetY { get; private set; }
        internal float DamageAngle { get; private set; }
        internal bool UseTargetOrigin { get; private set; }
    }

    internal sealed class CombatDataLoadReport
    {
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal int ProfileCount;
        internal int FrameworkCount;

        internal bool IsValid
        {
            get { return Errors.Count == 0; }
        }
    }
}
