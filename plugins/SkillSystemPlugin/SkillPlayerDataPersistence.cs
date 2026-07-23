using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Durango.Offline;
using Durango.Utils;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Skill;

namespace BaoX.DurangoOriginal.SkillSystemMod
{
    internal static class SkillPlayerDataPersistence
    {
        private const string StorageKey = "baox_offline_skills_v3";
        private const string LegacyStorageKey = "baox_offline_skills_v2";
        private const string ProgressionVersionKey = "skill_progression_version";
        private static readonly Dictionary<PlayerContext, SkillSaveData> Attached = new Dictionary<PlayerContext, SkillSaveData>();

        internal static SkillSaveData Load(PlayerContext context)
        {
            SkillSaveData data = LoadStorage(context, StorageKey);
            if (data == null)
            {
                data = LoadDirect(context.Path);
            }
            if (data == null)
            {
                data = LoadLegacy(context);
            }
            if (data == null)
            {
                data = new SkillSaveData();
            }

            data.Normalize();
            MergeDuplicateBundles(data);
            Attach(context, data);
            return data;
        }

        internal static void Attach(PlayerContext context, SkillSaveData data)
        {
            Attached[context] = data;
            SaveStorage(context, data);
        }

        internal static void SaveAttached(PlayerContext context)
        {
            SkillSaveData data;
            if (!Attached.TryGetValue(context, out data) || data == null || string.IsNullOrEmpty(context.Path) || !File.Exists(context.Path))
            {
                return;
            }

            try
            {
                data.Normalize();
                MergeDuplicateBundles(data);

                JObject root = JObject.Parse(File.ReadAllText(context.Path));
                root["skills"] = BuildCategories(data);
                root["known_skills"] = BuildKnownSkills(data);
                root["skill_points"] = data.SkillPoints;
                root["skill_list"] = BuildSkillList(data);
                root[ProgressionVersionKey] = 3;

                File.WriteAllText(context.Path, root.ToString(Formatting.Indented), new UTF8Encoding(false));
            }
            catch (Exception exception)
            {
                if (SkillSystemPlugin.Log != null)
                {
                    SkillSystemPlugin.Log.LogWarning("Direct skill save failed: " + exception.Message);
                }
            }
        }

        private static SkillSaveData LoadDirect(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path));
                JArray knownSkills = root["known_skills"] as JArray;
                if (knownSkills == null)
                {
                    return null;
                }

                SkillSaveData data = new SkillSaveData();
                data.Categories = new List<SkillCategorySaveData>();
                JToken version = root[ProgressionVersionKey];
                int savedVersion = version == null ? 0 : version.Value<int>();
                JToken points = root["skill_points"];
                data.SkillPoints = points == null ? 0 : points.Value<int>();

                if (savedVersion >= 3)
                {
                    JObject categories = root["skills"] as JObject;
                    if (categories != null)
                    {
                        foreach (JProperty property in categories.Properties())
                        {
                            Category category;
                            try
                            {
                                category = (Category)Enum.Parse(typeof(Category), property.Name, true);
                            }
                            catch
                            {
                                continue;
                            }
                            if (category == Category.Invalid)
                            {
                                continue;
                            }
                            JObject value = property.Value as JObject;
                            if (value == null)
                            {
                                continue;
                            }
                            SkillCategorySaveData saved = new SkillCategorySaveData();
                            saved.Category = (int)category;
                            saved.Level = value["Level"] == null ? 1 : Math.Max(1, value["Level"].Value<int>());
                            saved.Exp = value["Exp"] == null ? 0 : Math.Max(0, value["Exp"].Value<int>());
                            data.Categories.Add(saved);
                        }
                    }
                }

                data.Normalize();

                foreach (JToken token in knownSkills)
                {
                    JObject savedBundle = token as JObject;
                    if (savedBundle == null)
                    {
                        continue;
                    }

                    string skillId = ReadString(savedBundle["SkillId"]);
                    if (string.IsNullOrEmpty(skillId))
                    {
                        continue;
                    }

                    SkillBundleSaveData bundle = GetOrCreateBundle(data, skillId);
                    JToken category = savedBundle["Category"];
                    if (category != null)
                    {
                        bundle.Category = category.Value<int>();
                    }

                    JObject levels = savedBundle["Levels"] as JObject;
                    if (levels == null)
                    {
                        continue;
                    }

                    foreach (JProperty level in levels.Properties())
                    {
                        int value = level.Value.Value<int>();
                        if (value > 0)
                        {
                            SetLevel(bundle, level.Name, value);
                        }
                    }
                }

                return data;
            }
            catch (Exception exception)
            {
                if (SkillSystemPlugin.Log != null)
                {
                    SkillSystemPlugin.Log.LogWarning("Direct skill load failed: " + exception.Message);
                }
                return null;
            }
        }

        private static SkillSaveData LoadLegacy(PlayerContext context)
        {
            return LoadStorage(context, LegacyStorageKey);
        }

        private static SkillSaveData LoadStorage(PlayerContext context, string key)
        {
            if (context.Storage == null)
            {
                context.Storage = new Dictionary<string, byte[]>();
                return null;
            }

            byte[] bytes;
            if (!context.Storage.TryGetValue(key, out bytes) || bytes == null || bytes.Length == 0)
            {
                return null;
            }

            try
            {
                return Json.Read<SkillSaveData>(bytes, false);
            }
            catch (Exception exception)
            {
                if (SkillSystemPlugin.Log != null)
                {
                    SkillSystemPlugin.Log.LogWarning("Skill storage load failed: " + exception.Message);
                }
                return null;
            }
        }

        private static void SaveStorage(PlayerContext context, SkillSaveData data)
        {
            if (context == null || data == null)
            {
                return;
            }
            if (context.Storage == null)
            {
                context.Storage = new Dictionary<string, byte[]>();
            }

            try
            {
                data.Normalize();
                MergeDuplicateBundles(data);
                context.Storage[StorageKey] = Json.WriteToBytes<SkillSaveData>(data, false, null);
                context.Storage.Remove(LegacyStorageKey);
            }
            catch (Exception exception)
            {
                if (SkillSystemPlugin.Log != null)
                {
                    SkillSystemPlugin.Log.LogWarning("Skill storage save failed: " + exception.Message);
                }
            }
        }

        private static JObject BuildCategories(SkillSaveData data)
        {
            JObject result = new JObject();
            for (int i = 0; i < data.Categories.Count; i++)
            {
                SkillCategorySaveData saved = data.Categories[i];
                JObject category = new JObject();
                category["Level"] = saved.Level;
                category["Exp"] = saved.Exp;
                category["ResearchTime"] = new JValue((object)null);
                category["Researching"] = new JValue((object)null);
                result[((Category)saved.Category).ToString()] = category;
            }
            return result;
        }

        private static JArray BuildKnownSkills(SkillSaveData data)
        {
            JArray result = new JArray();
            for (int i = 0; i < data.Bundles.Count; i++)
            {
                SkillBundleSaveData saved = data.Bundles[i];
                JObject levels = new JObject();
                for (int j = 0; j < saved.Levels.Count; j++)
                {
                    if (!string.IsNullOrEmpty(saved.Levels[j].SubId) && saved.Levels[j].Level > 0)
                    {
                        levels[saved.Levels[j].SubId] = saved.Levels[j].Level;
                    }
                }
                if (!levels.HasValues)
                {
                    continue;
                }

                JObject bundle = new JObject();
                bundle["Category"] = saved.Category;
                bundle["SkillId"] = saved.SkillId;
                bundle["Levels"] = levels;
                result.Add(bundle);
            }
            return result;
        }

        private static JArray BuildSkillList(SkillSaveData data)
        {
            JArray result = new JArray();
            for (int i = 0; i < data.Bundles.Count; i++)
            {
                SkillBundleSaveData bundle = data.Bundles[i];
                for (int j = 0; j < bundle.Levels.Count; j++)
                {
                    SkillLevelSaveData level = bundle.Levels[j];
                    if (level.Level <= 0 || string.IsNullOrEmpty(level.SubId))
                    {
                        continue;
                    }

                    JObject skill = new JObject();
                    skill["SkillId"] = bundle.SkillId;
                    skill["Level"] = level.Level;
                    skill["SubId"] = level.SubId;
                    result.Add(skill);
                }
            }
            return result;
        }

        private static void MergeDuplicateBundles(SkillSaveData data)
        {
            Dictionary<string, SkillBundleSaveData> merged = new Dictionary<string, SkillBundleSaveData>(StringComparer.Ordinal);
            for (int i = 0; i < data.Bundles.Count; i++)
            {
                SkillBundleSaveData source = data.Bundles[i];
                if (source == null || string.IsNullOrEmpty(source.SkillId))
                {
                    continue;
                }

                SkillBundleSaveData target;
                if (!merged.TryGetValue(source.SkillId, out target))
                {
                    target = new SkillBundleSaveData();
                    target.SkillId = source.SkillId;
                    target.Category = source.Category;
                    target.Normalize();
                    merged.Add(source.SkillId, target);
                }
                else if (target.Category == (int)Category.Invalid && source.Category != (int)Category.Invalid)
                {
                    target.Category = source.Category;
                }

                source.Normalize();
                for (int j = 0; j < source.Levels.Count; j++)
                {
                    SetLevel(target, source.Levels[j].SubId, source.Levels[j].Level);
                }
            }

            data.Bundles = new List<SkillBundleSaveData>(merged.Values);
        }

        private static SkillBundleSaveData GetOrCreateBundle(SkillSaveData data, string skillId)
        {
            for (int i = 0; i < data.Bundles.Count; i++)
            {
                if (data.Bundles[i].SkillId == skillId)
                {
                    return data.Bundles[i];
                }
            }

            SkillBundleSaveData bundle = new SkillBundleSaveData();
            bundle.SkillId = skillId;
            bundle.Normalize();
            data.Bundles.Add(bundle);
            return bundle;
        }

        private static void SetLevel(SkillBundleSaveData bundle, string subId, int level)
        {
            if (string.IsNullOrEmpty(subId) || level <= 0)
            {
                return;
            }

            for (int i = 0; i < bundle.Levels.Count; i++)
            {
                if (bundle.Levels[i].SubId == subId)
                {
                    bundle.Levels[i].Level = Math.Max(bundle.Levels[i].Level, level);
                    return;
                }
            }

            SkillLevelSaveData saved = new SkillLevelSaveData();
            saved.SubId = subId;
            saved.Level = level;
            bundle.Levels.Add(saved);
        }

        private static string ReadString(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : token.Value<string>();
        }
    }

    [HarmonyPatch(typeof(PlayerContext), "Save")]
    internal static class PlayerContextDirectSkillSavePatch
    {
        private static void Postfix(PlayerContext __instance)
        {
            SkillPlayerDataPersistence.SaveAttached(__instance);
        }
    }
}
