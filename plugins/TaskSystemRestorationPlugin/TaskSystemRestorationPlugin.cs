using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Durango;
using Durango.Logic;
using Durango.Network;
using Durango.Offline;
using HarmonyLib;
using Messages;
using Shared.Quest;
using Yaml;
using Yaml.Util;
using OfflineConnection = Durango.Offline.Connection;
using OfflinePlayer = Durango.Offline.Player;
using MessageQuestState = Messages.QuestState;
using SharedQuestState = Shared.Quest.QuestState;

namespace BaoX.DurangoOriginal.TaskSystemRestoration
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class TaskSystemRestorationPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.tasksystem";
        public const string PluginName = "Task System Restoration Plugin";
        public const string PluginVersion = "0.1.1";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<int> MaxTasksPerCategory;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            Enabled = Config.Bind("General", "Enabled", true,
                "Restore the Quest/Task menu and offline task catalog backend.");
            MaxTasksPerCategory = Config.Bind("Catalog", "MaxTasksPerCategory", 120,
                "Maximum client definitions exposed in each task category (10-600).");
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            Logger.LogInfo("Task system restoration loaded: Permanent, Daily and Weekly client catalogs.");
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
            _harmony = null;
        }
    }

    internal static class OfflineTaskBackend
    {
        private static readonly string[] VisibleCategoryKeys = new string[]
        {
            "permanent", "daily", "weekly"
        };

        internal static void Register(OfflinePlayer player, OfflineConnection connection)
        {
            connection.Recv<GetQuestCategories>(delegate(GetQuestCategories msg, PacketHeader header)
            {
                player.Send<QuestCategories>(CreateCategories(), header.Seq);
            });
            // Replaces the original offline handler, which always returned the
            // Epic "sunset" list regardless of the requested category.
            connection.Recv<GetQuests>(delegate(GetQuests msg, PacketHeader header)
            {
                player.Send<Quests>(CreateQuests(msg.Category), header.Seq);
            });
            connection.Recv<GetQuestScoreInfos>(delegate(GetQuestScoreInfos msg, PacketHeader header)
            {
                player.Send<QuestScoreInfos>(CreateScoreInfo(msg.Category), header.Seq);
            });
            connection.Recv<GetQuestState>(delegate(GetQuestState msg, PacketHeader header)
            {
                MessageQuestState state = default(MessageQuestState);
                state.States = new Dictionary<string, SharedQuestState>();
                if (msg.QuestIds != null)
                {
                    for (int i = 0; i < msg.QuestIds.Length; i++)
                    {
                        string id = msg.QuestIds[i];
                        if (!string.IsNullOrEmpty(id))
                            state.States[id] = SharedQuestState.WorkInProgress;
                    }
                }
                player.Send<MessageQuestState>(state, header.Seq);
            });

            // The original constructor already sent its Epic-only snapshot.
            // Send the corrected complete category snapshot afterward.
            player.Send<QuestCategories>(CreateCategories(), 0U);
            TaskSystemRestorationPlugin.Log.LogInfo(
                "Offline Task backend registered; Epic story remains separate from visible Task tabs.");
        }

        private static QuestCategories CreateCategories()
        {
            List<QuestCategory> categories = new List<QuestCategory>();
            Dictionary<string, QuestYml> catalog = SingletonDict<string, QuestYml>.Instance;
            for (int i = 0; i < VisibleCategoryKeys.Length; i++)
            {
                string key = VisibleCategoryKeys[i];
                if (!HasCategory(catalog, key)) continue;
                QuestCategory category = default(QuestCategory);
                category.Category = key;
                category.Name = DisplayName(key);
                category.Faction = null;
                category.Season = null;
                category.UnreceivedCount = 0;
                categories.Add(category);
            }

            QuestCategory epic = default(QuestCategory);
            epic.Category = "sunset";
            epic.Name = TaskLocalization.Get("story");
            epic.Faction = null;
            epic.Season = null;
            epic.UnreceivedCount = 0;
            QuestCategories result = default(QuestCategories);
            result.Categories = categories.ToArray();
            result.Epic = new QuestCategory?(epic);
            return result;
        }

        private static Quests CreateQuests(string category)
        {
            string key = string.IsNullOrEmpty(category) ? "permanent" : category;
            int limit = Math.Max(10, Math.Min(600,
                TaskSystemRestorationPlugin.MaxTasksPerCategory.Value));
            List<KeyValuePair<string, QuestYml>> matches =
                new List<KeyValuePair<string, QuestYml>>();
            Dictionary<string, QuestYml> catalog = SingletonDict<string, QuestYml>.Instance;
            if (catalog != null)
            {
                foreach (KeyValuePair<string, QuestYml> pair in catalog)
                {
                    if (pair.Value != null && pair.Value.Category == key)
                        matches.Add(pair);
                }
            }
            matches.Sort(delegate(KeyValuePair<string, QuestYml> left,
                KeyValuePair<string, QuestYml> right)
            {
                int order = left.Value.Order.CompareTo(right.Value.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Key, right.Key);
            });

            int count = Math.Min(limit, matches.Count);
            QuestToDo[] todos = new QuestToDo[count];
            for (int i = 0; i < count; i++)
            {
                QuestToDo todo = default(QuestToDo);
                todo.Id = matches[i].Key;
                todo.Progress = 0;
                todo.GoalCount = 1;
                todo.Finished = false;
                todo.EndAt = 0.0;
                todo.Reward = null;
                todos[i] = todo;
            }
            Quests result = default(Quests);
            result.Category = key;
            result.Todos = todos;
            TaskSystemRestorationPlugin.Log.LogInfo(
                "Task catalog response: category=" + key + ", definitions=" +
                matches.Count + ", exposed=" + count);
            return result;
        }

        private static QuestScoreInfos CreateScoreInfo(string category)
        {
            QuestScoreInfos info = default(QuestScoreInfos);
            info.Category = category ?? string.Empty;
            info.CurQuestScore = 0;
            info.QuestScoreRewards = new QuestScoreReward[0];
            return info;
        }

        private static bool HasCategory(Dictionary<string, QuestYml> catalog, string category)
        {
            if (catalog == null) return false;
            foreach (KeyValuePair<string, QuestYml> pair in catalog)
            {
                if (pair.Value != null && pair.Value.Category == category) return true;
            }
            return false;
        }

        private static string DisplayName(string category)
        {
            if (category == "permanent") return TaskLocalization.Get("permanent");
            if (category == "daily") return TaskLocalization.Get("daily");
            if (category == "weekly") return TaskLocalization.Get("weekly");
            return category;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "IsHiddenMenu")]
    internal static class TaskHiddenMenuPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(MenuType type, ref bool __result)
        {
            if (TaskSystemRestorationPlugin.Enabled.Value && type == MenuType.Quest)
                __result = false;
        }
    }

    [HarmonyPatch(typeof(MenuSystem), "GameManager_MainSceneLoaded")]
    internal static class TaskMainScenePatch
    {
        private static void Postfix()
        {
            if (!TaskSystemRestorationPlugin.Enabled.Value) return;
            MenuSystem menu = GameSystem<MenuSystem>.Instance();
            if (menu != null) menu.EnableMenu(MenuType.Quest, true, true);
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), MethodType.Constructor, new Type[]
    {
        typeof(string), typeof(OfflineConnection), typeof(World), typeof(PlayerContext), typeof(bool)
    })]
    internal static class TaskBackendPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(OfflinePlayer __instance, string entityId,
            OfflineConnection connection, World world, PlayerContext context,
            bool isLocalPlayer)
        {
            if (!TaskSystemRestorationPlugin.Enabled.Value || !isLocalPlayer ||
                connection == null) return;
            OfflineTaskBackend.Register(__instance, connection);
        }
    }
}
