using System;
using System.Collections.Generic;
using Durango.Utils;
using Messages;
using NCalc;
using Newtonsoft.Json.Linq;
using Shared.StatusEffect;

namespace BaoX.DurangoOriginal.FoodConsumptionMod
{
    internal sealed class FoodEffect
    {
        internal string Prototype;
        internal int Level;
        internal JObject Definition;
        internal readonly Dictionary<string, float> Numbers = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        internal readonly Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal float Number(string key, float fallback)
        {
            float value;
            if (Numbers.TryGetValue(key, out value)) return value;
            if (Definition == null) return fallback;
            JToken token = Definition[key];
            if (token == null || token.Type == JTokenType.Null) return fallback;
            try
            {
                if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                    return Convert.ToSingle(((JValue)token).Value);
                string formula = token.ToString();
                if (string.IsNullOrEmpty(formula)) return fallback;
                Expression expression = ExpressionParser.Parse(formula);
                expression.Parameters["level"] = Math.Max(1, Level);
                object result = expression.Evaluate();
                return result == null ? fallback : Convert.ToSingle(result);
            }
            catch
            {
                float parsed;
                return float.TryParse(token.ToString(), out parsed) ? parsed : fallback;
            }
        }

        internal string Text(string key, string fallback)
        {
            string value;
            if (Strings.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)) return value;
            JToken token = Definition == null ? null : Definition[key];
            return token == null || token.Type == JTokenType.Null || string.IsNullOrEmpty(token.ToString())
                ? fallback
                : token.ToString();
        }

        internal bool HasModifier
        {
            get
            {
                if (Definition != null)
                {
                    foreach (JProperty property in Definition.Properties())
                        if (property.Name.EndsWith("_plus", StringComparison.OrdinalIgnoreCase)) return true;
                }
                foreach (string key in Numbers.Keys)
                    if (key.EndsWith("_plus", StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        internal Messages.EffectDetail[] ModifierDetails()
        {
            Dictionary<string, float> values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            if (Definition != null)
            {
                foreach (JProperty property in Definition.Properties())
                    if (property.Name.EndsWith("_plus", StringComparison.OrdinalIgnoreCase))
                        values[property.Name] = Number(property.Name, 0f);
            }
            foreach (KeyValuePair<string, float> pair in Numbers)
                if (pair.Key.EndsWith("_plus", StringComparison.OrdinalIgnoreCase)) values[pair.Key] = pair.Value;

            List<Messages.EffectDetail> details = new List<Messages.EffectDetail>();
            foreach (KeyValuePair<string, float> pair in values)
            {
                if (Math.Abs(pair.Value) < 0.0001f) continue;
                details.Add(new Messages.EffectDetail
                {
                    Type = EffectType.Modifier,
                    Key = pair.Key,
                    Value = pair.Value
                });
            }
            return details.ToArray();
        }
    }

    internal static class FoodDatabase
    {
        private static JObject _foods;
        private static JObject _statusEffects;

        internal static void Load()
        {
            try
            {
                JObject root = Json.ReadFromFile<JObject>("offline/assets/performance");
                _foods = root == null ? null : root["food"] as JObject;
                _statusEffects = Json.ReadFromFile<JObject>("offline/assets/survival/status_effects");
                FoodConsumptionPlugin.Log.LogInfo("Loaded original food definitions: " + (_foods == null ? 0 : _foods.Count));
            }
            catch (Exception ex)
            {
                FoodConsumptionPlugin.Log.LogError("Unable to load original food/status data: " + ex);
            }
        }

        internal static bool IsEdible(Item item)
        {
            return HasTag(item, "eatable") || HasTag(item, "drinkable") || FindFoodDefinition(item.Prototype, item.Level) != null;
        }

        internal static bool HasTag(Item item, string id)
        {
            if (item.Tags != null)
                for (int i = 0; i < item.Tags.Length; i++)
                    if (string.Equals(item.Tags[i].Id, id, StringComparison.OrdinalIgnoreCase)) return true;
            if (item.TagModifications != null)
                for (int i = 0; i < item.TagModifications.Length; i++)
                    if (string.Equals(item.TagModifications[i].Id, id, StringComparison.OrdinalIgnoreCase) && item.TagModifications[i].Level > 0) return true;
            return false;
        }

        internal static FoodEffect Resolve(Item item)
        {
            FoodEffect effect = new FoodEffect
            {
                Prototype = item.Prototype,
                Level = Math.Max(1, item.Level),
                Definition = FindFoodDefinition(item.Prototype, item.Level)
            };

            if (item.Performance != null)
            {
                for (int i = 0; i < item.Performance.Length; i++)
                {
                    Performance performance = item.Performance[i];
                    if (!string.Equals(performance.Id, "food", StringComparison.OrdinalIgnoreCase)) continue;
                    if (performance.Nums != null)
                        foreach (KeyValuePair<string, float> pair in performance.Nums) effect.Numbers[pair.Key] = pair.Value;
                    if (performance.Strs != null)
                        foreach (KeyValuePair<string, string> pair in performance.Strs) effect.Strings[pair.Key] = pair.Value;
                }
            }
            return effect;
        }

        private static JObject FindFoodDefinition(string prototype, int level)
        {
            if (_foods == null || string.IsNullOrEmpty(prototype)) return null;
            JToken ranges = _foods[prototype];
            if (ranges == null && prototype.StartsWith("skewer_", StringComparison.OrdinalIgnoreCase))
                ranges = _foods["skewer_meat_mart"] ?? _foods["meat_skewers"];
            JObject rangeObject = ranges as JObject;
            if (rangeObject == null) return null;

            int wanted = Math.Max(1, level);
            JObject first = null;
            foreach (JProperty property in rangeObject.Properties())
            {
                JObject definition = property.Value as JObject;
                if (definition == null) continue;
                if (first == null) first = definition;
                string text = property.Name.Trim('[', ']', ' ');
                string[] parts = text.Split(',');
                int min;
                int max;
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out min) && int.TryParse(parts[1].Trim(), out max) && wanted >= min && wanted <= max)
                    return definition;
            }
            return first;
        }

        internal static JObject StatusTemplate(string id)
        {
            return _statusEffects == null || string.IsNullOrEmpty(id) ? null : _statusEffects[id] as JObject;
        }

        internal static Messages.EffectDetail[] TemplateDetails(string id, int level)
        {
            JObject template = StatusTemplate(id);
            JArray effects = template == null ? null : template["effects"] as JArray;
            if (effects == null) return new Messages.EffectDetail[0];
            List<Messages.EffectDetail> details = new List<Messages.EffectDetail>();
            foreach (JToken token in effects)
            {
                JObject obj = token as JObject;
                if (obj == null) continue;
                int type = obj.Value<int?>("type") ?? 0;
                string key = obj.Value<string>("key") ?? string.Empty;
                string formula = obj.Value<string>("value") ?? "0";
                float value = 0f;
                try
                {
                    Expression expression = ExpressionParser.Parse(formula);
                    expression.Parameters["level"] = Math.Max(1, level);
                    value = Convert.ToSingle(expression.Evaluate());
                }
                catch { float.TryParse(formula, out value); }
                details.Add(new Messages.EffectDetail { Type = (EffectType)type, Key = key, Value = value });
            }
            return details.ToArray();
        }

        internal static float StatusDuration(string id, int level, float fallback)
        {
            JObject template = StatusTemplate(id);
            string formula = template == null ? null : template.Value<string>("duration");
            if (string.IsNullOrEmpty(formula)) return fallback;
            try
            {
                Expression expression = ExpressionParser.Parse(formula);
                expression.Parameters["level"] = Math.Max(1, level);
                return Convert.ToSingle(expression.Evaluate());
            }
            catch { return fallback; }
        }
    }
}
