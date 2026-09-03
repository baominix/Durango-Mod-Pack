using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Durango.Offline;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Yaml;
using Yaml.Util;

namespace BaoX.DurangoOriginal.PlayerProgressionMod
{
    internal static class ProgressionPersistence
    {
        private const string ExpKey = "character_exp";
        private const string VersionKey = "character_progression_version";
        private static readonly Dictionary<PlayerContext, PlayerProgressionState> Attached = new Dictionary<PlayerContext, PlayerProgressionState>();
        private static readonly HashSet<PlayerContext> Dirty = new HashSet<PlayerContext>();
        private static readonly object Gate = new object();

        internal static bool IsProgressionMode(PlayerContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.Path))
            {
                return false;
            }

            string normalized = context.Path.Replace('/', '\\').ToLowerInvariant();
            return normalized.Contains("\\single_multi\\")
                || normalized.Contains("\\free\\");
        }

        internal static PlayerProgressionState Load(PlayerContext context, out bool isNew)
        {
            isNew = true;
            int exp = 0;

            if (!string.IsNullOrEmpty(context.Path) && File.Exists(context.Path))
            {
                try
                {
                    JObject root = JObject.Parse(File.ReadAllText(context.Path));
                    JToken token = root[ExpKey];
                    if (token != null && token.Type != JTokenType.Null)
                    {
                        exp = Math.Max(0, token.Value<int>());
                        isNew = false;
                    }
                    else
                    {
                        exp = InferExperienceFromContext(context);
                        if (exp > 0)
                        {
                            isNew = false;
                        }
                    }
                }
                catch (Exception exception)
                {
                    PlayerProgressionPlugin.Log.LogWarning("Progression load failed: " + exception.Message);
                }
            }

            PlayerProgressionState state = new PlayerProgressionState(context, exp);
            Attached[context] = state;
            return state;
        }

        private static int InferExperienceFromContext(PlayerContext context)
        {
            int level = 1;
            if (context != null && context.PlayerInfo != null)
            {
                level = Math.Max(level, context.PlayerInfo.PlayerLevel);
            }
            if (context != null)
            {
                level = Math.Max(level, context.AppearPlayer.Level);
            }
            if (level <= 1)
            {
                return 0;
            }

            try
            {
                PlayerStatistics statistics = Singleton<PlayerStatistics>.Instance;
                int[] thresholds = statistics == null ? null : statistics.level_thresholds;
                if (thresholds == null || thresholds.Length == 0)
                {
                    return 0;
                }
                int index = Math.Max(0, Math.Min(level - 2, thresholds.Length - 1));
                return thresholds[index];
            }
            catch
            {
                return 0;
            }
        }

        internal static PlayerProgressionState Get(PlayerContext context)
        {
            PlayerProgressionState state;
            if (Attached.TryGetValue(context, out state))
            {
                return state;
            }

            bool isNew;
            return Load(context, out isNew);
        }

        internal static void MarkDirty(PlayerContext context)
        {
            if (context == null)
            {
                return;
            }
            lock (Gate)
            {
                Dirty.Add(context);
            }
        }

        internal static void MarkSaved(PlayerContext context)
        {
            if (context == null)
            {
                return;
            }
            lock (Gate)
            {
                Dirty.Remove(context);
            }
        }

        internal static void Flush(PlayerContext context)
        {
            if (context == null)
            {
                return;
            }

            lock (Gate)
            {
                if (!Dirty.Contains(context))
                {
                    return;
                }
            }

            context.Save();
            SaveAttached(context);
            lock (Gate)
            {
                Dirty.Remove(context);
            }
            PlayerProgressionPlugin.Log.LogInfo(
                "Deferred character XP flushed to disk.");
        }

        internal static void FlushAll()
        {
            PlayerContext[] contexts;
            lock (Gate)
            {
                contexts = new PlayerContext[Dirty.Count];
                Dirty.CopyTo(contexts);
            }
            for (int i = 0; i < contexts.Length; i++)
            {
                Flush(contexts[i]);
            }
        }

        internal static void Detach(PlayerContext context)
        {
            if (context == null)
            {
                return;
            }
            lock (Gate)
            {
                Dirty.Remove(context);
                Attached.Remove(context);
            }
        }

        internal static void SaveAttached(PlayerContext context)
        {
            PlayerProgressionState state;
            if (!Attached.TryGetValue(context, out state) || string.IsNullOrEmpty(context.Path) || !File.Exists(context.Path))
            {
                return;
            }

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(context.Path));
                root[ExpKey] = state.Experience;
                root[VersionKey] = 1;

                JObject playerInfo = root["player_info"] as JObject;
                if (playerInfo != null)
                {
                    playerInfo["player_level"] = state.Level;
                }

                JObject appearPlayer = root["appear_player"] as JObject;
                if (appearPlayer != null)
                {
                    appearPlayer["Level"] = state.Level;
                }

                File.WriteAllText(context.Path, root.ToString(Formatting.Indented), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                PlayerProgressionPlugin.Log.LogWarning("Progression save failed: " + exception.Message);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerContext), "Save")]
    internal static class PlayerContextProgressionSavePatch
    {
        private static void Postfix(PlayerContext __instance)
        {
            ProgressionPersistence.SaveAttached(__instance);
            ProgressionPersistence.MarkSaved(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerContext), "Initialize")]
    internal static class PlayerContextProgressionInitializePatch
    {
        private static void Postfix(PlayerContext __instance)
        {
            if (!ProgressionPersistence.IsProgressionMode(__instance))
            {
                return;
            }

            bool isNew;
            PlayerProgressionState state = ProgressionPersistence.Load(__instance, out isNew);
            state.ApplyToContext(isNew);
            __instance.Save();
            ProgressionPersistence.SaveAttached(__instance);

            PlayerProgressionPlugin.Log.LogInfo("Progression initialized: level=" + state.Level + " exp=" + state.Experience + " new=" + isNew);
        }
    }

    [HarmonyPatch(typeof(Server), "EndServer")]
    internal static class ServerEndProgressionSavePatch
    {
        private static void Prefix()
        {
            ProgressionPersistence.FlushAll();
        }
    }
}
