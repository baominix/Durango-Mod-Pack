using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Shared.Battle;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal static class OriginalAnimalDataLoader
    {
        internal static Dictionary<int, AnimalCombatProfile> Load(
            string json,
            int[] entityTypeIds,
            CombatDataLoadReport report)
        {
            Dictionary<int, AnimalCombatProfile> result =
                new Dictionary<int, AnimalCombatProfile>();

            if (string.IsNullOrEmpty(json))
            {
                report.Errors.Add("Embedded animal combat data is empty.");
                return result;
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    "Embedded animal combat data could not be parsed: " +
                    exception.Message);
                return result;
            }

            int i;
            for (i = 0; i < entityTypeIds.Length; i++)
            {
                int entityTypeId = entityTypeIds[i];
                JObject source = root[entityTypeId.ToString(
                    CultureInfo.InvariantCulture)] as JObject;
                if (source == null)
                {
                    report.Errors.Add(
                        "Animal entity type is missing from animal.json: " +
                        entityTypeId);
                    continue;
                }

                AnimalCombatProfile profile = ParseProfile(
                    entityTypeId,
                    source,
                    report);
                if (profile != null)
                {
                    result[entityTypeId] = profile;
                }
            }
            return result;
        }

        private static AnimalCombatProfile ParseProfile(
            int entityTypeId,
            JObject source,
            CombatDataLoadReport report)
        {
            AnimalCombatProfile profile = new AnimalCombatProfile();
            profile.EntityTypeId = entityTypeId;
            profile.InternalName = RequiredString(
                source, "__name__", entityTypeId, report);
            profile.AnimalType = RequiredString(
                source, "type", entityTypeId, report);
            profile.Framework = RequiredString(
                source, "framework", entityTypeId, report);
            profile.ModelPath = RequiredString(
                source, "model_path", entityTypeId, report);
            profile.RootMotions = RequiredString(
                source, "root_motions", entityTypeId, report);
            profile.AiFactorId = RequiredString(
                source, "ai_factor_id", entityTypeId, report);

            profile.AttackFormula = RequiredString(
                source, "attack", entityTypeId, report);
            profile.DefenseFormula = RequiredString(
                source, "defense", entityTypeId, report);
            profile.AttackRatingFormula = RequiredString(
                source, "attack_rating", entityTypeId, report);
            profile.AccuracyFormula = RequiredString(
                source, "accuracy", entityTypeId, report);
            profile.DodgeFormula = RequiredString(
                source, "dodge", entityTypeId, report);
            profile.EvadeFormula = RequiredString(
                source, "evade", entityTypeId, report);
            profile.LifeMaxFormula = RequiredString(
                source, "life_max", entityTypeId, report);
            profile.GroggyMaxFormula = RequiredString(
                source, "groggy_max", entityTypeId, report);
            profile.GroggyVelocityFormula = RequiredString(
                source, "groggy_velocity", entityTypeId, report);
            profile.BlowResistanceFormula = RequiredString(
                source, "blow_resistance", entityTypeId, report);
            profile.KnockBackResistanceFormula = RequiredString(
                source, "knock_back_resistance", entityTypeId, report);
            profile.GroggyDurationFormula = RequiredString(
                source, "groggy_duration", entityTypeId, report);
            profile.KnockDownDurationFormula = RequiredString(
                source, "knock_down_duration", entityTypeId, report);
            profile.GroggySectionFormulas = RequiredStringArray(
                source, "groggy_section", 4, entityTypeId, report);

            profile.AttackCooltime = RequiredSingle(
                source, "attack_cooltime", entityTypeId, report);
            profile.BoundRadius = RequiredSingle(
                source, "bound_radius", entityTypeId, report);
            profile.RepresentScale = RequiredSingle(
                source, "represent_scale", entityTypeId, report);
            profile.Difficulty = RequiredSingle(
                source, "difficulty", entityTypeId, report);
            profile.SizeLevel = RequiredInt32(
                source, "size_level", entityTypeId, report);
            profile.DamageRatios = RequiredDirections(
                source, "damage_ratio_table", entityTypeId, report);
            profile.GroggyDamageRatios = RequiredDirections(
                source, "groggy_damage_ratio_table", entityTypeId, report);
            profile.BodyParts = RequiredBodyParts(
                source, entityTypeId, report);
            profile.PartProbabilities = RequiredPartProbabilities(
                source, entityTypeId, report);

            ValidateProfile(profile, report);
            return profile;
        }

        private static void ValidateProfile(
            AnimalCombatProfile profile,
            CombatDataLoadReport report)
        {
            if (profile.AttackCooltime <= 0f)
            {
                report.Errors.Add(
                    "Animal " + profile.EntityTypeId +
                    " has invalid attack_cooltime: " +
                    profile.AttackCooltime.ToString(
                        CultureInfo.InvariantCulture));
            }
            if (profile.BoundRadius <= 0f)
            {
                report.Errors.Add(
                    "Animal " + profile.EntityTypeId +
                    " has invalid bound_radius: " +
                    profile.BoundRadius.ToString(
                        CultureInfo.InvariantCulture));
            }
            if (profile.RepresentScale <= 0f)
            {
                report.Errors.Add(
                    "Animal " + profile.EntityTypeId +
                    " has invalid represent_scale: " +
                    profile.RepresentScale.ToString(
                        CultureInfo.InvariantCulture));
            }
        }

        private static string RequiredString(
            JObject source,
            string name,
            int entityTypeId,
            CombatDataLoadReport report)
        {
            JToken token = source[name];
            string value = token == null ? null : token.ToString();
            if (string.IsNullOrEmpty(value))
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " is missing required field '" + name + "'.");
            }
            return value;
        }

        private static float RequiredSingle(
            JObject source,
            string name,
            int entityTypeId,
            CombatDataLoadReport report)
        {
            JToken token = source[name];
            float value;
            if (token == null || !float.TryParse(
                token.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " has invalid numeric field '" + name + "'.");
                return 0f;
            }
            return value;
        }

        private static string[] RequiredStringArray(
            JObject source,
            string name,
            int expectedCount,
            int entityTypeId,
            CombatDataLoadReport report)
        {
            JArray values = source[name] as JArray;
            if (values == null || values.Count != expectedCount)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " has invalid formula array '" + name + "'.");
                return new string[0];
            }

            string[] result = new string[values.Count];
            int i;
            for (i = 0; i < values.Count; i++)
            {
                result[i] = values[i] == null
                    ? null
                    : values[i].ToString();
                if (string.IsNullOrEmpty(result[i]))
                {
                    report.Errors.Add(
                        "Animal " + entityTypeId +
                        " has an empty formula in '" + name +
                        "' at index " + i + ".");
                }
            }
            return result;
        }

        private static int RequiredInt32(
            JObject source,
            string name,
            int entityTypeId,
            CombatDataLoadReport report)
        {
            JToken token = source[name];
            int value;
            if (token == null || !int.TryParse(
                token.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " has invalid integer field '" + name + "'.");
                return 0;
            }
            return value;
        }

        private static DirectionalValues RequiredDirections(
            JObject source,
            string name,
            int entityTypeId,
            CombatDataLoadReport report)
        {
            JObject table = source[name] as JObject;
            if (table == null)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " is missing direction table '" + name + "'.");
                return new DirectionalValues();
            }

            return new DirectionalValues
            {
                Front = RequiredSingle(
                    table, "front", entityTypeId, report),
                Right = RequiredSingle(
                    table, "right", entityTypeId, report),
                Back = RequiredSingle(
                    table, "back", entityTypeId, report),
                Left = RequiredSingle(
                    table, "left", entityTypeId, report)
            };
        }

        private static Dictionary<BodyPart, AnimalBodyPartProfile>
            RequiredBodyParts(
                JObject source,
                int entityTypeId,
                CombatDataLoadReport report)
        {
            Dictionary<BodyPart, AnimalBodyPartProfile> result =
                new Dictionary<BodyPart, AnimalBodyPartProfile>();
            JObject table = source["body_parts"] as JObject;
            if (table == null)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " is missing body_parts.");
                return result;
            }

            foreach (JProperty property in table.Properties())
            {
                BodyPart part;
                if (!TryParseBodyPart(property.Name, out part))
                {
                    report.Errors.Add(
                        "Animal " + entityTypeId +
                        " has unsupported body part '" +
                        property.Name + "'.");
                    continue;
                }
                JObject bodyPart = property.Value as JObject;
                if (bodyPart == null)
                {
                    report.Errors.Add(
                        "Animal " + entityTypeId +
                        " has invalid body part '" +
                        property.Name + "'.");
                    continue;
                }

                AnimalBodyPartProfile profile =
                    new AnimalBodyPartProfile();
                profile.Part = part;
                profile.HpRatio = RequiredSingle(
                    bodyPart, "hp_ratio", entityTypeId, report);
                profile.DodgeRatio = RequiredSingle(
                    bodyPart, "dodge_ratio", entityTypeId, report);
                profile.DefenseRatios = RequiredDefenseRatios(
                    bodyPart, entityTypeId, property.Name, report);
                ReadBreakStatuses(
                    bodyPart,
                    entityTypeId,
                    property.Name,
                    profile.StatusEffects,
                    report);
                result[part] = profile;
            }
            if (result.Count == 0)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " has no usable body_parts.");
            }
            return result;
        }

        private static AnimalDefenseRatios RequiredDefenseRatios(
            JObject bodyPart,
            int entityTypeId,
            string partName,
            CombatDataLoadReport report)
        {
            JObject ratios = bodyPart["defense_ratio"] as JObject;
            if (ratios == null)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId + " body part '" +
                    partName + "' is missing defense_ratio.");
                return new AnimalDefenseRatios();
            }
            return new AnimalDefenseRatios
            {
                Impact = RequiredSingle(
                    ratios, "impact", entityTypeId, report),
                Pierce = RequiredSingle(
                    ratios, "pierce", entityTypeId, report),
                Cut = RequiredSingle(
                    ratios, "cut", entityTypeId, report)
            };
        }

        private static void ReadBreakStatuses(
            JObject bodyPart,
            int entityTypeId,
            string partName,
            List<AnimalBreakStatus> result,
            CombatDataLoadReport report)
        {
            JArray statuses =
                bodyPart["status_effects_on_break"] as JArray;
            if (statuses == null)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId + " body part '" +
                    partName +
                    "' is missing status_effects_on_break.");
                return;
            }
            int i;
            for (i = 0; i < statuses.Count; i++)
            {
                JObject entry = statuses[i] as JObject;
                string id = entry == null || entry["id"] == null
                    ? null
                    : entry["id"].ToString();
                JObject options = entry == null
                    ? null
                    : entry["options"] as JObject;
                int level;
                if (string.IsNullOrEmpty(id) || options == null ||
                    options["level"] == null ||
                    !int.TryParse(
                        options["level"].ToString(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out level))
                {
                    report.Errors.Add(
                        "Animal " + entityTypeId + " body part '" +
                        partName + "' has an invalid break status.");
                    continue;
                }
                result.Add(new AnimalBreakStatus
                {
                    Id = id,
                    Level = level
                });
            }
        }

        private static DirectionalBodyPartWeights
            RequiredPartProbabilities(
                JObject source,
                int entityTypeId,
                CombatDataLoadReport report)
        {
            JObject table = source["part_probability"] as JObject;
            if (table == null)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " is missing part_probability.");
                return new DirectionalBodyPartWeights();
            }
            return new DirectionalBodyPartWeights
            {
                Front = RequiredPartWeights(
                    table, "front", entityTypeId, report),
                Right = RequiredPartWeights(
                    table, "right", entityTypeId, report),
                Back = RequiredPartWeights(
                    table, "back", entityTypeId, report),
                Left = RequiredPartWeights(
                    table, "left", entityTypeId, report)
            };
        }

        private static BodyPartWeights RequiredPartWeights(
            JObject table,
            string direction,
            int entityTypeId,
            CombatDataLoadReport report)
        {
            BodyPartWeights result = new BodyPartWeights();
            JObject weights = table[direction] as JObject;
            if (weights == null)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " is missing part_probability." + direction + ".");
                return result;
            }
            foreach (JProperty property in weights.Properties())
            {
                BodyPart part;
                float value;
                if (!TryParseBodyPart(property.Name, out part) ||
                    !float.TryParse(
                        property.Value.ToString(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out value) || value < 0f)
                {
                    report.Errors.Add(
                        "Animal " + entityTypeId +
                        " has invalid part probability '" + direction +
                        "." + property.Name + "'.");
                    continue;
                }
                SetPartWeight(result, part, value);
            }
            if (Math.Abs(result.Total - 1f) > 0.001f)
            {
                report.Errors.Add(
                    "Animal " + entityTypeId +
                    " part_probability." + direction +
                    " must total 1.0 but was " +
                    result.Total.ToString(CultureInfo.InvariantCulture) +
                    ".");
            }
            return result;
        }

        private static void SetPartWeight(
            BodyPartWeights weights,
            BodyPart part,
            float value)
        {
            switch (part)
            {
                case BodyPart.Head: weights.Head = value; break;
                case BodyPart.Body: weights.Body = value; break;
                case BodyPart.Arm: weights.Arm = value; break;
                case BodyPart.Leg: weights.Leg = value; break;
                case BodyPart.Tail: weights.Tail = value; break;
                case BodyPart.Back: weights.Back = value; break;
            }
        }

        private static bool TryParseBodyPart(
            string name,
            out BodyPart part)
        {
            switch ((name ?? string.Empty).ToLowerInvariant())
            {
                case "head": part = BodyPart.Head; return true;
                case "body": part = BodyPart.Body; return true;
                case "arm": part = BodyPart.Arm; return true;
                case "leg": part = BodyPart.Leg; return true;
                case "tail": part = BodyPart.Tail; return true;
                case "back": part = BodyPart.Back; return true;
                default: part = BodyPart.Invalid; return false;
            }
        }
    }
}
