using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Baominix.DurangoOriginal.CombatSystem.EquipmentPerformance
{
    internal sealed class WeaponPerformanceValues
    {
        internal readonly Dictionary<string, float> Nums = new Dictionary<string, float>(StringComparer.Ordinal);
        internal readonly Dictionary<string, string> Strs = new Dictionary<string, string>(StringComparer.Ordinal);

        internal float Get(string key)
        {
            float value;
            return Nums.TryGetValue(key, out value) ? value : 0f;
        }
    }

    internal static class WeaponPerformanceDatabase
    {
        private static readonly string[] WeaponNumericKeys =
        {
            "attack",
            "accuracy",
            "attack_rating",
            "critical",
            "attack_cooltime",
            "battle_speed",
            "range_bonus",
            "aim_time"
        };

        private static readonly string[] WeaponStringKeys =
        {
            "weapon_framework",
            "model",
            "slot",
            "attack_type",
            "accuracy_type",
            "visual_effect",
            "icon"
        };

        private static readonly string[] ArmorNumericKeys =
        {
            "defense",
            "bag_size"
        };

        private static readonly string[] ArmorStringKeys =
        {
            "female_model",
            "male_model",
            "slot",
            "visual_effect",
            "icon"
        };

        private static JObject _weapons;
        private static JObject _armor;
        private static JObject _modifiers;
        private static bool _loadAttempted;

        internal static bool TryGet(string prototypeId, int level, out WeaponPerformanceValues values)
        {
            EnsureLoaded();
            return TryGet(_weapons, prototypeId, level, WeaponNumericKeys, WeaponStringKeys, out values, false);
        }

        internal static bool TryGetArmor(string prototypeId, int level, out WeaponPerformanceValues values)
        {
            EnsureLoaded();
            return TryGet(_armor, prototypeId, level, ArmorNumericKeys, ArmorStringKeys, out values, false);
        }

        internal static bool TryGetModifiers(string prototypeId, int level, out WeaponPerformanceValues values)
        {
            EnsureLoaded();
            return TryGet(_modifiers, prototypeId, level, null, null, out values, true);
        }

        private static bool TryGet(
            JObject group,
            string prototypeId,
            int level,
            string[] numericKeys,
            string[] stringKeys,
            out WeaponPerformanceValues values,
            bool includeAllNumbers)
        {
            values = null;
            if (group == null || string.IsNullOrEmpty(prototypeId))
            {
                return false;
            }

            JObject ranges = group[prototypeId] as JObject;
            if (ranges == null)
            {
                return false;
            }

            JObject data = SelectLevelRange(ranges, level);
            if (data == null)
            {
                return false;
            }

            WeaponPerformanceValues result = new WeaponPerformanceValues();
            if (includeAllNumbers)
            {
                foreach (JProperty property in data.Properties())
                {
                    float number;
                    if (TryEvaluate(property.Value, level, out number))
                    {
                        result.Nums[property.Name] = number;
                    }
                }
            }
            else if (numericKeys != null)
            {
                for (int i = 0; i < numericKeys.Length; i++)
                {
                    string key = numericKeys[i];
                    JToken token = data[key];
                    float number;
                    if (TryEvaluate(token, level, out number))
                    {
                        result.Nums[key] = number;
                    }
                }
            }

            if (stringKeys != null)
            {
                for (int i = 0; i < stringKeys.Length; i++)
                {
                    string key = stringKeys[i];
                    JToken token = data[key];
                    if (token != null && token.Type == JTokenType.String)
                    {
                        result.Strs[key] = token.ToString();
                    }
                }
            }

            values = result;
            return true;
        }

        private static void EnsureLoaded()
        {
            if (_loadAttempted)
            {
                return;
            }

            _loadAttempted = true;
            try
            {
                TextAsset asset = Resources.Load("offline/assets/performance") as TextAsset;
                if (asset == null)
                {
                    DurangoCombatSystemPlugin.Log.LogWarning("Cannot load offline/assets/performance");
                    return;
                }

                JObject root = JObject.Parse(asset.text);
                _weapons = root["weapon"] as JObject;
                _armor = root["armor"] as JObject;
                _modifiers = root["modifiers"] as JObject;
                DurangoCombatSystemPlugin.Log.LogInfo("Loaded original equipment performance data");
            }
            catch (Exception exception)
            {
                DurangoCombatSystemPlugin.Log.LogError("Weapon performance load failed: " + exception);
            }
        }

        private static JObject SelectLevelRange(JObject ranges, int level)
        {
            JObject fallback = null;
            foreach (JProperty property in ranges.Properties())
            {
                JObject data = property.Value as JObject;
                if (data == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = data;
                }

                int min;
                int max;
                if (TryParseRange(property.Name, out min, out max) && level >= min && level <= max)
                {
                    return data;
                }
            }
            return fallback;
        }

        private static bool TryParseRange(string text, out int min, out int max)
        {
            min = 0;
            max = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            string[] parts = text.Trim('[', ']', ' ').Split(',');
            return parts.Length == 2
                && int.TryParse(parts[0].Trim(), out min)
                && int.TryParse(parts[1].Trim(), out max);
        }

        private static bool TryEvaluate(JToken token, int level, out float value)
        {
            value = 0f;
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
            {
                value = token.Value<float>();
                return true;
            }

            string expression = token.ToString().Replace("level", level.ToString(CultureInfo.InvariantCulture));
            if (TryEvaluateRangeLookup(expression, level, out value))
            {
                return true;
            }
            try
            {
                value = new ArithmeticExpression(expression).Evaluate();
                return true;
            }
            catch
            {
                return float.TryParse(expression, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            }
        }

        private static bool TryEvaluateRangeLookup(string expression, int level, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(expression)
                || !expression.TrimStart().StartsWith("range_lookup", StringComparison.Ordinal))
            {
                return false;
            }

            int pairStart = expression.IndexOf("[(", StringComparison.Ordinal);
            int pairEnd = expression.IndexOf(")]", StringComparison.Ordinal);
            if (pairStart < 0 || pairEnd <= pairStart)
            {
                return false;
            }

            string pairs = expression.Substring(pairStart + 2, pairEnd - pairStart - 2);
            string[] entries = pairs.Split(new string[] { "),", "), (" }, StringSplitOptions.RemoveEmptyEntries);
            bool found = false;
            int bestLevel = int.MinValue;
            float firstValue = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim(' ', '(', ')');
                string[] parts = entry.Split(',');
                int threshold;
                float candidate;
                if (parts.Length != 2
                    || !int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold)
                    || !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out candidate))
                {
                    continue;
                }

                if (!found)
                {
                    firstValue = candidate;
                    found = true;
                }
                if (threshold <= level && threshold >= bestLevel)
                {
                    bestLevel = threshold;
                    value = candidate;
                }
            }

            if (bestLevel == int.MinValue && found)
            {
                value = firstValue;
            }
            return found;
        }

        private sealed class ArithmeticExpression
        {
            private readonly string _text;
            private int _index;

            internal ArithmeticExpression(string text)
            {
                _text = text ?? string.Empty;
            }

            internal float Evaluate()
            {
                float value = ParseExpression();
                SkipSpaces();
                if (_index != _text.Length)
                {
                    throw new FormatException();
                }
                return value;
            }

            private float ParseExpression()
            {
                float value = ParseTerm();
                while (true)
                {
                    SkipSpaces();
                    if (Take('+')) value += ParseTerm();
                    else if (Take('-')) value -= ParseTerm();
                    else return value;
                }
            }

            private float ParseTerm()
            {
                float value = ParseFactor();
                while (true)
                {
                    SkipSpaces();
                    if (Take('*')) value *= ParseFactor();
                    else if (Take('/')) value /= ParseFactor();
                    else return value;
                }
            }

            private float ParseFactor()
            {
                SkipSpaces();
                if (Take('+')) return ParseFactor();
                if (Take('-')) return -ParseFactor();
                if (Take('('))
                {
                    float value = ParseExpression();
                    SkipSpaces();
                    if (!Take(')')) throw new FormatException();
                    return value;
                }

                int start = _index;
                while (_index < _text.Length)
                {
                    char c = _text[_index];
                    if (!char.IsDigit(c) && c != '.') break;
                    _index++;
                }
                if (start == _index) throw new FormatException();
                return float.Parse(_text.Substring(start, _index - start), CultureInfo.InvariantCulture);
            }

            private bool Take(char value)
            {
                if (_index < _text.Length && _text[_index] == value)
                {
                    _index++;
                    return true;
                }
                return false;
            }

            private void SkipSpaces()
            {
                while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
            }
        }
    }
}
