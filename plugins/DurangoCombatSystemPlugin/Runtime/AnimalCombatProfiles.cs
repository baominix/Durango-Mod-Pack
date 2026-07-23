using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Yaml;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal sealed class AnimalCombatProfile
    {
        internal int EntityTypeId;
        internal string Name;
        internal string AnimalType;
        internal string AiFactorId;
        internal int SizeLevel = 2;
        internal float BoundRadius = 100f;
        internal float AttackCooldown = 2.2f;
        internal float AttackBase = 15f;
        internal float AttackPerLevel = 0.75f;
        internal float AccuracyBase;
        internal float AccuracyPerLevel = 5f;
        internal float CriticalBase;
        internal float CriticalPerLevel;
        internal float GroggyDuration = 8f;
        internal float LifeMaxCoefficient;
        internal float BlowResistance = 560f;
        internal float KnockBackResistance;

        internal bool IsProactive
        {
            get
            {
                return string.Equals(AnimalType, "Carnivore", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(AnimalType, "Scavenger", StringComparison.OrdinalIgnoreCase);
            }
        }

        internal float AggroRange
        {
            get
            {
                if (string.Equals(AnimalType, "Carnivore", StringComparison.OrdinalIgnoreCase))
                {
                    return 900f + SizeLevel * 140f;
                }
                if (string.Equals(AnimalType, "Scavenger", StringComparison.OrdinalIgnoreCase))
                {
                    return 650f + SizeLevel * 110f;
                }
                return 0f;
            }
        }

        internal float LeashRange
        {
            get { return 2200f + SizeLevel * 250f; }
        }

        internal float MoveSpeedMultiplier
        {
            get
            {
                if (string.Equals(AnimalType, "Carnivore", StringComparison.OrdinalIgnoreCase))
                {
                    return 1.12f;
                }
                if (string.Equals(AnimalType, "Scavenger", StringComparison.OrdinalIgnoreCase))
                {
                    return 1.05f;
                }
                return 0.95f;
            }
        }

        internal float AttackAt(int level)
        {
            return Mathf.Max(1f, AttackBase + Mathf.Max(1, level) * AttackPerLevel);
        }

        internal float AccuracyAt(int level)
        {
            return Mathf.Max(0f, AccuracyBase + Mathf.Max(1, level) * AccuracyPerLevel);
        }

        internal float CriticalAt(int level)
        {
            return Mathf.Max(0f, CriticalBase + Mathf.Max(1, level) * CriticalPerLevel);
        }

        internal float LifeMaxAt(int level, float unstableFactor)
        {
            int combatLevel = Mathf.Max(1, level);
            float factor = Mathf.Max(0.01f, unstableFactor);
            if (LifeMaxCoefficient <= 0f)
            {
                return Mathf.Max(500f, combatLevel * 200f) * factor;
            }

            float adjustedLevel = combatLevel + 24f;
            return Mathf.Max(
                1f,
                LifeMaxCoefficient * adjustedLevel * adjustedLevel * factor);
        }
    }

    internal static class AnimalCombatProfiles
    {
        private static readonly Dictionary<int, AnimalCombatProfile> Profiles =
            new Dictionary<int, AnimalCombatProfile>();
        private static readonly Regex NumberPattern = new Regex(
            @"-?\d+(?:\.\d+)?");
        private static readonly AnimalCombatProfile Fallback = new AnimalCombatProfile
        {
            Name = "unknown",
            AnimalType = "Herbivore",
            AiFactorId = "unknown_ai"
        };
        private static bool _loaded;

        internal static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;

            try
            {
                TextAsset asset = Resources.Load("offline/assets/entity_types/animal") as TextAsset;
                if (asset == null)
                {
                    OfflineCombatBackendPlugin.Log.LogWarning(
                        "Cannot load original animal profiles; using fallback AI tuning");
                    return;
                }

                JObject root = JObject.Parse(asset.text);
                foreach (JProperty entry in root.Properties())
                {
                    int entityTypeId;
                    if (!int.TryParse(entry.Name, out entityTypeId))
                    {
                        continue;
                    }

                    JToken data = entry.Value;
                    AnimalCombatProfile profile = new AnimalCombatProfile();
                    profile.EntityTypeId = entityTypeId;
                    profile.Name = Text(data["__name__"], "animal_" + entityTypeId);
                    profile.AnimalType = Text(data["type"], "Herbivore");
                    profile.AiFactorId = Text(data["ai_factor_id"], "unknown_ai");
                    profile.SizeLevel = Mathf.Max(1, Integer(data["size_level"], 2));
                    profile.BoundRadius = Mathf.Max(20f, Float(data["bound_radius"], 100f));
                    profile.AttackCooldown = Mathf.Max(0.5f, Float(data["attack_cooltime"], 2.2f));
                    profile.GroggyDuration = Mathf.Max(0.5f, FirstNumber(data["groggy_duration"], 8f));
                    profile.LifeMaxCoefficient = Mathf.Max(
                        0f, FirstNumber(data["life_max"], 0f));
                    profile.BlowResistance = Mathf.Max(
                        1f, FirstNumber(data["blow_resistance"], 560f));
                    profile.KnockBackResistance = Mathf.Max(
                        0f, FirstNumber(data["knock_back_resistance"], 0f));
                    ReadLinear(data["attack"], 15f, 0.75f,
                        out profile.AttackBase, out profile.AttackPerLevel);
                    ReadLinear(data["accuracy"], 0f, 5f,
                        out profile.AccuracyBase, out profile.AccuracyPerLevel);
                    ReadLinear(data["critical"], 0f, 0f,
                        out profile.CriticalBase, out profile.CriticalPerLevel);
                    Profiles[entityTypeId] = profile;
                }

                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Loaded original animal combat profiles: " + Profiles.Count);
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogError(
                    "Animal combat profile load failed: " + exception);
            }
        }

        internal static AnimalCombatProfile Get(int entityTypeId)
        {
            EnsureLoaded();
            AnimalCombatProfile profile;
            return Profiles.TryGetValue(entityTypeId, out profile) ? profile : Fallback;
        }

        internal static List<AnimalCombatProfile> GetSpawnCandidates(bool proactive)
        {
            EnsureLoaded();
            List<AnimalCombatProfile> result = new List<AnimalCombatProfile>();
            foreach (AnimalCombatProfile profile in Profiles.Values)
            {
                if (profile == null ||
                    profile.EntityTypeId <= 0 ||
                    profile.IsProactive != proactive ||
                    !IsWildSpawnName(profile.Name))
                {
                    continue;
                }

                string prefabPath = AnimalYaml.GetPrefabPath(profile.EntityTypeId);
                if (string.IsNullOrEmpty(prefabPath))
                {
                    continue;
                }

                result.Add(profile);
            }
            return result;
        }

        private static bool IsWildSpawnName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.IndexOf("for_pet") < 0 &&
                lower.IndexOf("store") < 0 &&
                lower.IndexOf("event") < 0 &&
                lower.IndexOf("hanbok") < 0 &&
                lower.IndexOf("witch") < 0 &&
                lower.IndexOf("xmas") < 0 &&
                lower.IndexOf("dummy") < 0 &&
                lower.IndexOf("costume") < 0;
        }

        private static void ReadLinear(
            JToken token,
            float fallbackBase,
            float fallbackPerLevel,
            out float baseValue,
            out float perLevel)
        {
            baseValue = fallbackBase;
            perLevel = fallbackPerLevel;
            if (token == null || token.Type == JTokenType.Null)
            {
                return;
            }

            MatchCollection matches = NumberPattern.Matches(token.ToString());
            if (matches.Count > 0)
            {
                float.TryParse(matches[0].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out baseValue);
            }
            if (matches.Count > 1)
            {
                float.TryParse(matches[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out perLevel);
            }
        }

        private static float FirstNumber(JToken token, float fallback)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return fallback;
            }
            Match match = NumberPattern.Match(token.ToString());
            float value;
            return match.Success && float.TryParse(
                match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static string Text(JToken token, string fallback)
        {
            return token == null || token.Type == JTokenType.Null
                ? fallback
                : token.ToString();
        }

        private static float Float(JToken token, float fallback)
        {
            return token == null || token.Type == JTokenType.Null
                ? fallback
                : token.Value<float>();
        }

        private static int Integer(JToken token, int fallback)
        {
            return token == null || token.Type == JTokenType.Null
                ? fallback
                : token.Value<int>();
        }
    }
}
