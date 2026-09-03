using System;
using System.Collections.Generic;
using System.Globalization;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal static class FrameworkReferenceReader
    {
        private sealed class AttackBuilder
        {
            internal string Key;
            internal string Motion;
            internal bool BoundEnemy;
            internal float RotationSpeed;
            internal readonly List<HitBuilder> Hits = new List<HitBuilder>();
        }

        private sealed class HitBuilder
        {
            internal int Frame;
            internal string SubActionId;
            internal int DamageType;
            internal float Radius;
            internal float RadiusMin;
            internal float AngleStart;
            internal float AngleEnd;
            internal float RectangleHalfWidth;
            internal float RectangleHalfHeight;
            internal float OffsetX;
            internal float OffsetY;
            internal float DamageAngle;
            internal bool UseTargetOrigin;
        }

        internal static FrameworkSnapshot Read(
            string text,
            string sourceName,
            CombatDataLoadReport report)
        {
            if (string.IsNullOrEmpty(text))
            {
                report.Errors.Add(
                    "Embedded animal framework is empty: " + sourceName);
                return null;
            }

            string[] lines = text.Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Split(new char[] { '\n' });

            FrameworkSnapshot snapshot = new FrameworkSnapshot();
            snapshot.SourcePath = sourceName;
            bool inCombatDirections = false;
            bool inCombatAttacks = false;
            bool inCombat3States = false;
            string currentSimpleKey = null;
            AttackBuilder currentAttack = null;
            HitBuilder currentHit = null;

            int i;
            for (i = 0; i < lines.Length; i++)
            {
                string raw = lines[i];
                string trimmed = raw.Trim();

                if (trimmed.StartsWith("m_Name: "))
                {
                    snapshot.Name = ValueAfterColon(trimmed);
                }

                if (trimmed == "combat_directions:")
                {
                    inCombatDirections = true;
                    inCombatAttacks = false;
                    inCombat3States = false;
                    currentSimpleKey = null;
                    continue;
                }
                if (trimmed == "combat_attacks:")
                {
                    inCombatDirections = false;
                    inCombatAttacks = true;
                    inCombat3States = false;
                    currentSimpleKey = null;
                    currentAttack = null;
                    currentHit = null;
                    continue;
                }
                if (trimmed == "active_skills:" ||
                    trimmed == "etc_motions:" ||
                    trimmed == "combat_3states:")
                {
                    inCombatDirections = false;
                    inCombat3States = trimmed == "combat_3states:";
                    if (trimmed != "combat_3states:")
                    {
                        AddAttack(snapshot, currentAttack);
                        currentAttack = null;
                        currentHit = null;
                        inCombatAttacks = false;
                    }
                }

                if (inCombatAttacks && raw.StartsWith("  - key: "))
                {
                    AddAttack(snapshot, currentAttack);
                    currentAttack = new AttackBuilder();
                    currentAttack.Key = trimmed.Substring(7).Trim();
                    currentHit = null;
                    snapshot.AttackKeys.Add(currentAttack.Key);
                    currentSimpleKey = currentAttack.Key;
                    continue;
                }

                if (trimmed.StartsWith("- key: "))
                {
                    string key = trimmed.Substring(7).Trim();
                    currentSimpleKey = key;
                    continue;
                }

                if (inCombatAttacks &&
                    currentAttack != null &&
                    raw.StartsWith("    - frame: "))
                {
                    currentHit = new HitBuilder();
                    currentHit.Frame = ParseInt32(ValueAfterColon(trimmed));
                    currentAttack.Hits.Add(currentHit);
                    continue;
                }

                if (inCombatAttacks && currentAttack != null)
                {
                    if (currentHit == null)
                    {
                        if (trimmed.StartsWith("motion: "))
                        {
                            currentAttack.Motion = ValueAfterColon(trimmed);
                            continue;
                        }
                        if (trimmed.StartsWith("bound_enemy: "))
                        {
                            currentAttack.BoundEnemy =
                                ParseBoolean(ValueAfterColon(trimmed));
                            continue;
                        }
                        if (trimmed.StartsWith("rot_speed: "))
                        {
                            currentAttack.RotationSpeed =
                                ParseSingle(ValueAfterColon(trimmed));
                            continue;
                        }
                    }
                    else
                    {
                        if (trimmed.StartsWith("sub_action_id: "))
                            currentHit.SubActionId = ValueAfterColon(trimmed);
                        else if (trimmed.StartsWith("damage_type: "))
                            currentHit.DamageType = ParseInt32(ValueAfterColon(trimmed));
                        else if (trimmed.StartsWith("radius: "))
                            currentHit.Radius = ParseSingle(ValueAfterColon(trimmed));
                        else if (trimmed.StartsWith("radius_min: "))
                            currentHit.RadiusMin = ParseSingle(ValueAfterColon(trimmed));
                        else if (trimmed.StartsWith("angles: "))
                            ParseVector2(ValueAfterColon(trimmed), out currentHit.AngleStart, out currentHit.AngleEnd);
                        else if (trimmed.StartsWith("rect_half_size: "))
                            ParseVector2(ValueAfterColon(trimmed), out currentHit.RectangleHalfWidth, out currentHit.RectangleHalfHeight);
                        else if (trimmed.StartsWith("offset: "))
                            ParseVector2(ValueAfterColon(trimmed), out currentHit.OffsetX, out currentHit.OffsetY);
                        else if (trimmed.StartsWith("damage_angle: "))
                            currentHit.DamageAngle = ParseSingle(ValueAfterColon(trimmed));
                        else if (trimmed.StartsWith("use_target_origin: "))
                            currentHit.UseTargetOrigin = ParseBoolean(ValueAfterColon(trimmed));
                        continue;
                    }
                }

                if (trimmed.StartsWith("motion: "))
                {
                    string motion = ValueAfterColon(trimmed);
                    if (currentSimpleKey == "stand")
                    {
                        snapshot.StandMotion = motion;
                    }
                    else if (currentSimpleKey == "battle_idle")
                    {
                        snapshot.BattleIdleMotion = motion;
                    }
                    else if (currentSimpleKey == "battle_stand")
                    {
                        snapshot.BattleStandMotion = motion;
                    }
                    else if (currentSimpleKey == "evade")
                    {
                        snapshot.EvadeMotion = motion;
                    }
                    else if (currentSimpleKey == "groggy")
                    {
                        snapshot.GroggyMotion = motion;
                    }
                    else if (currentSimpleKey == "blow")
                    {
                        snapshot.BlowMotion = motion;
                    }
                    continue;
                }

                if (inCombat3States &&
                    currentSimpleKey == "knock_down_motions")
                {
                    if (trimmed.StartsWith("begin: "))
                    {
                        snapshot.KnockDownBeginMotion =
                            ValueAfterColon(trimmed);
                    }
                    else if (trimmed.StartsWith("during: "))
                    {
                        snapshot.KnockDownDuringMotion =
                            ValueAfterColon(trimmed);
                    }
                    else if (trimmed.StartsWith("end: "))
                    {
                        snapshot.KnockDownEndMotion =
                            ValueAfterColon(trimmed);
                    }
                    continue;
                }

                if (inCombatDirections)
                {
                    if (trimmed.StartsWith("front: "))
                    {
                        snapshot.DamageFrontMotion = ValueAfterColon(trimmed);
                    }
                    else if (trimmed.StartsWith("back: "))
                    {
                        snapshot.DamageBackMotion = ValueAfterColon(trimmed);
                    }
                    else if (trimmed.StartsWith("left: "))
                    {
                        snapshot.DamageLeftMotion = ValueAfterColon(trimmed);
                    }
                    else if (trimmed.StartsWith("right: "))
                    {
                        snapshot.DamageRightMotion = ValueAfterColon(trimmed);
                    }
                }
            }

            AddAttack(snapshot, currentAttack);

            ValidateSnapshot(snapshot, report);
            return snapshot;
        }

        private static string ValueAfterColon(string text)
        {
            int index = text.IndexOf(':');
            return index < 0 ? string.Empty : text.Substring(index + 1).Trim();
        }

        private static void AddAttack(
            FrameworkSnapshot snapshot,
            AttackBuilder builder)
        {
            if (builder == null || string.IsNullOrEmpty(builder.Key))
            {
                return;
            }

            AttackHitDefinition[] hits =
                new AttackHitDefinition[builder.Hits.Count];
            int i;
            for (i = 0; i < builder.Hits.Count; i++)
            {
                HitBuilder hit = builder.Hits[i];
                hits[i] = new AttackHitDefinition(
                    hit.Frame,
                    hit.SubActionId,
                    hit.DamageType,
                    hit.Radius,
                    hit.RadiusMin,
                    hit.AngleStart,
                    hit.AngleEnd,
                    hit.RectangleHalfWidth,
                    hit.RectangleHalfHeight,
                    hit.OffsetX,
                    hit.OffsetY,
                    hit.DamageAngle,
                    hit.UseTargetOrigin);
            }
            snapshot.Attacks.Add(new AnimalAttackDefinition(
                builder.Key,
                builder.Motion,
                builder.BoundEnemy,
                builder.RotationSpeed,
                hits));
        }

        private static int ParseInt32(string value)
        {
            int result;
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result) ? result : 0;
        }

        private static float ParseSingle(string value)
        {
            float result;
            return float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result) ? result : 0f;
        }

        private static bool ParseBoolean(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void ParseVector2(
            string value,
            out float x,
            out float y)
        {
            x = 0f;
            y = 0f;
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            string cleaned = value.Replace("{", string.Empty)
                .Replace("}", string.Empty);
            string[] parts = cleaned.Split(',');
            int i;
            for (i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.StartsWith("x:"))
                    x = ParseSingle(part.Substring(2).Trim());
                else if (part.StartsWith("y:"))
                    y = ParseSingle(part.Substring(2).Trim());
            }
        }

        private static void ValidateSnapshot(
            FrameworkSnapshot snapshot,
            CombatDataLoadReport report)
        {
            if (string.IsNullOrEmpty(snapshot.Name))
            {
                report.Errors.Add(
                    "Framework has no m_Name: " + snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.StandMotion))
            {
                report.Errors.Add(
                    "Framework has no stand motion: " + snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.BattleStandMotion))
            {
                report.Errors.Add(
                    "Framework has no battle_stand motion: " +
                    snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.BattleIdleMotion))
            {
                report.Errors.Add(
                    "Framework has no battle_idle motion: " +
                    snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.EvadeMotion))
            {
                report.Errors.Add(
                    "Framework has no evade motion: " + snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.GroggyMotion))
            {
                report.Errors.Add(
                    "Framework has no groggy motion: " + snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.BlowMotion))
            {
                report.Errors.Add(
                    "Framework has no blow motion: " + snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.KnockDownBeginMotion) ||
                string.IsNullOrEmpty(snapshot.KnockDownDuringMotion) ||
                string.IsNullOrEmpty(snapshot.KnockDownEndMotion))
            {
                report.Errors.Add(
                    "Framework knock_down_motions is incomplete: " +
                    snapshot.SourcePath);
            }
            if (string.IsNullOrEmpty(snapshot.DamageFrontMotion) ||
                string.IsNullOrEmpty(snapshot.DamageBackMotion) ||
                string.IsNullOrEmpty(snapshot.DamageLeftMotion) ||
                string.IsNullOrEmpty(snapshot.DamageRightMotion))
            {
                report.Errors.Add(
                    "Framework directional damage map is incomplete: " +
                    snapshot.SourcePath);
            }
            if (snapshot.AttackKeys.Count == 0)
            {
                report.Errors.Add(
                    "Framework has no combat attacks: " + snapshot.SourcePath);
            }
            if (snapshot.Attacks.Count != snapshot.AttackKeys.Count)
            {
                report.Errors.Add(
                    "Framework attack snapshot count does not match its keys: " +
                    snapshot.SourcePath);
            }
            int i;
            for (i = 0; i < snapshot.Attacks.Count; i++)
            {
                AnimalAttackDefinition attack = snapshot.Attacks[i];
                if (string.IsNullOrEmpty(attack.Motion))
                {
                    report.Errors.Add(
                        "Framework attack has no motion: " + attack.Key +
                        " in " + snapshot.SourcePath);
                }
                if (attack.Hits.Length == 0)
                {
                    report.Warnings.Add(
                        "Framework attack has no hit geometry: " + attack.Key +
                        " in " + snapshot.SourcePath);
                }
            }
        }
    }
}
