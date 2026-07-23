using System;
using System.Collections.Generic;
using BaoX.DurangoOriginal.CombatSystemMod.Geometry;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BaoX.DurangoOriginal.OfflineCombat
{
    internal sealed class CombatHitProfile
    {
        internal float DamageBonus = 1f;
        internal float Groggy;
        internal float ArmorPenetration;
        internal float BlowPower;
        internal float AccuracyRatio = 1f;
        internal bool Blockable = true;
        internal bool StrongAttack;
    }

    internal sealed class CombatDefenseProfile
    {
        internal float StandByTime;
        internal float ActiveTime;
        internal float DodgeForce;
    }

    internal static class CombatActionProfiles
    {
        private static readonly Dictionary<string, CombatHitProfile[]> HitProfiles =
            new Dictionary<string, CombatHitProfile[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string, CombatDefenseProfile> DefenseProfiles =
            new Dictionary<string, CombatDefenseProfile>(StringComparer.Ordinal);
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
                TextAsset asset = Resources.Load("offline/assets/player/player_battle_actions") as TextAsset;
                if (asset == null)
                {
                    OfflineCombatBackendPlugin.Log.LogWarning(
                        "Cannot load original player_battle_actions; using neutral damage bonuses");
                    return;
                }

                JObject root = JObject.Parse(asset.text);
                foreach (JProperty action in root.Properties())
                {
                    JArray hits = action.Value["attack_info"] as JArray;
                    if (hits != null && hits.Count > 0)
                    {
                        CombatHitProfile[] profiles = new CombatHitProfile[hits.Count];
                        for (int i = 0; i < hits.Count; i++)
                        {
                            JToken hit = hits[i];
                            profiles[i] = new CombatHitProfile
                            {
                                DamageBonus = Mathf.Max(0f, Float(hit["damage_bonus"], 1f)),
                                Groggy = Mathf.Max(0f, Float(hit["groggy"], 0f)),
                                ArmorPenetration = Mathf.Clamp01(Float(hit["armor_penetration"], 0f)),
                                BlowPower = Mathf.Max(0f, Float(hit["blow_power"], 0f)),
                                AccuracyRatio = Mathf.Max(0f, Float(hit["accuracy_ratio"], 1f)),
                                Blockable = Bool(hit["blockable"], true),
                                StrongAttack = Bool(hit["strong_attack"], false)
                            };
                        }
                        HitProfiles[action.Name] = profiles;
                    }

                    JToken defense = action.Value["defense_info"];
                    if (defense != null && defense.Type != JTokenType.Null)
                    {
                        DefenseProfiles[action.Name] = new CombatDefenseProfile
                        {
                            StandByTime = Mathf.Max(0f, Float(defense["stand_by_time"], 0f)),
                            ActiveTime = Mathf.Max(0f, Float(defense["active_time"], 0f)),
                            DodgeForce = Mathf.Max(0f, Float(defense["dodge_force"], 0f))
                        };
                    }
                }

                OfflineCombatBackendPlugin.Log.LogInfo(
                    "Loaded original combat profiles: " + HitProfiles.Count +
                    " attacks, " + DefenseProfiles.Count + " defenses");
            }
            catch (Exception exception)
            {
                OfflineCombatBackendPlugin.Log.LogError(
                    "Combat profile load failed: " + exception);
            }
        }

        internal static CombatHitProfile GetHitProfile(string actionId, int hitIndex)
        {
            EnsureLoaded();
            CombatHitProfile[] profiles;
            if (!string.IsNullOrEmpty(actionId) &&
                HitProfiles.TryGetValue(actionId, out profiles) &&
                profiles != null && profiles.Length > 0)
            {
                int index = Mathf.Clamp(hitIndex, 0, profiles.Length - 1);
                return profiles[index];
            }
            return new CombatHitProfile();
        }

        internal static bool TryGetDefenseProfile(
            string actionId,
            out CombatDefenseProfile profile)
        {
            EnsureLoaded();
            profile = null;
            return !string.IsNullOrEmpty(actionId) &&
                DefenseProfiles.TryGetValue(actionId, out profile);
        }

        private static float Float(JToken token, float fallback)
        {
            return token == null || token.Type == JTokenType.Null
                ? fallback
                : token.Value<float>();
        }

        private static bool Bool(JToken token, bool fallback)
        {
            return token == null || token.Type == JTokenType.Null
                ? fallback
                : token.Value<bool>();
        }

        internal static float GetMaximumRange(string actionId, float configuredRange)
        {
            return AttackGeometry.GetMaximumRange(actionId, configuredRange);
        }
    }
}
