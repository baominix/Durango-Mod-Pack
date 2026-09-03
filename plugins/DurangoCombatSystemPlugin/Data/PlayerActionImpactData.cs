using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Baominix.DurangoOriginal.CombatSystem.Data
{
    internal sealed class PlayerActionHitImpact
    {
        internal int Frame;
        internal float GroggyRatio;
        internal float BlowPower;
        internal float KnockBackForce;
        internal bool HitForce;
        internal bool StrongAttack;
    }

    internal sealed class PlayerActionImpactProfile
    {
        internal string ActionId;
        internal PlayerActionHitImpact[] Hits;
    }

    internal static class PlayerActionImpactDataLoader
    {
        internal static Dictionary<string, PlayerActionImpactProfile> Load(
            string json,
            CombatDataLoadReport report)
        {
            Dictionary<string, PlayerActionImpactProfile> result =
                new Dictionary<string, PlayerActionImpactProfile>(
                    StringComparer.Ordinal);
            if (string.IsNullOrEmpty(json))
            {
                report.Errors.Add(
                    "Embedded player battle-action data is empty.");
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
                    "Embedded player battle-action data could not be parsed: " +
                    exception.Message);
                return result;
            }

            foreach (JProperty property in root.Properties())
            {
                JObject action = property.Value as JObject;
                JArray hits = action == null
                    ? null
                    : action["attack_info"] as JArray;
                if (hits == null || hits.Count == 0)
                {
                    continue;
                }

                PlayerActionHitImpact[] parsed =
                    new PlayerActionHitImpact[hits.Count];
                int i;
                for (i = 0; i < hits.Count; i++)
                {
                    JObject hit = hits[i] as JObject;
                    if (hit == null)
                    {
                        report.Errors.Add(
                            "Player action '" + property.Name +
                            "' has invalid attack_info index " + i + ".");
                        continue;
                    }
                    parsed[i] = new PlayerActionHitImpact
                    {
                        Frame = RequiredInt32(
                            hit, "frame", property.Name, i, report),
                        GroggyRatio = RequiredSingle(
                            hit, "groggy", property.Name, i, report),
                        BlowPower = RequiredSingle(
                            hit, "blow_power", property.Name, i, report),
                        KnockBackForce = RequiredSingle(
                            hit, "knock_back_force", property.Name, i, report),
                        HitForce = RequiredBoolean(
                            hit, "hit_force", property.Name, i, report),
                        StrongAttack = RequiredBoolean(
                            hit, "strong_attack", property.Name, i, report)
                    };
                }
                result[property.Name] = new PlayerActionImpactProfile
                {
                    ActionId = property.Name,
                    Hits = parsed
                };
            }

            if (result.Count == 0)
            {
                report.Errors.Add(
                    "player_battle_actions.json has no attack impact data.");
            }
            return result;
        }

        private static float RequiredSingle(
            JObject source,
            string name,
            string actionId,
            int hitIndex,
            CombatDataLoadReport report)
        {
            float value;
            JToken token = source[name];
            if (token == null || !float.TryParse(
                token.ToString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            {
                report.Errors.Add(Describe(actionId, hitIndex, name));
                return 0f;
            }
            return value;
        }

        private static int RequiredInt32(
            JObject source,
            string name,
            string actionId,
            int hitIndex,
            CombatDataLoadReport report)
        {
            int value;
            JToken token = source[name];
            if (token == null || !int.TryParse(
                token.ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value))
            {
                report.Errors.Add(Describe(actionId, hitIndex, name));
                return 0;
            }
            return value;
        }

        private static bool RequiredBoolean(
            JObject source,
            string name,
            string actionId,
            int hitIndex,
            CombatDataLoadReport report)
        {
            bool value;
            JToken token = source[name];
            if (token == null || !bool.TryParse(
                token.ToString(), out value))
            {
                report.Errors.Add(Describe(actionId, hitIndex, name));
                return false;
            }
            return value;
        }

        private static string Describe(
            string actionId,
            int hitIndex,
            string name)
        {
            return "Player action '" + actionId + "' hit " + hitIndex +
                " has invalid field '" + name + "'.";
        }
    }
}
