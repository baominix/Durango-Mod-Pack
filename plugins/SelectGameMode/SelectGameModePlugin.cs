using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Durango.Logic.Clusters;
using Durango.Offline;
using Durango.System;
using Durango.UI;
using HarmonyLib;
using UnityEngine;

namespace BaoX.DurangoOriginal.SelectGameMode
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.baominix.durango.original.logcontrol", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SelectGameModePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.baominix.durango.original.selectgamemode";
        public const string PluginName = "Select Game Mode";
        public const string PluginVersion = "0.2.1";

        private Harmony _harmony;
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            Logger.LogInfo("SelectGameMode loaded");
        }

        private void OnDestroy()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }

    internal static class SelectGameModeRuntime
    {
        public const string CreativeKey = "free_offline";
        public const string SingleMultiKey = "single_multi_offline";
        public const string PrefKey = "baox_select_game_mode";
        public const string LastClusterPrefKey = "last_selected_cluster_key";
        private const string TitleKey = "title";

        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo TitleClustersField = typeof(TitleMenuUserControlBase).GetField("Clusters", Flags);
        private static readonly FieldInfo ClustersField = typeof(Clusters).GetField("_clusters", Flags);
        private static readonly Dictionary<string, string[]> LabelsByLocale =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "en_US",
                    new string[]
                    {
                        "Select Game Mode",
                        "Creative Island Mode",
                        "Single/Multi Player Mode"
                    }
                },
                {
                    "ko_KR",
                    new string[]
                    {
                        "\uAC8C\uC784 \uBAA8\uB4DC \uC120\uD0DD",
                        "\uD06C\uB9AC\uC5D0\uC774\uD2F0\uBE0C \uC12C \uBAA8\uB4DC",
                        "\uC2F1\uAE00/\uBA40\uD2F0\uD50C\uB808\uC774\uC5B4 \uBAA8\uB4DC"
                    }
                },
                {
                    "es_MX",
                    new string[]
                    {
                        "Seleccionar modo de juego",
                        "Modo Isla Creativa",
                        "Modo individual/multijugador"
                    }
                },
                {
                    "pt_BR",
                    new string[]
                    {
                        "Selecionar modo de jogo",
                        "Modo Ilha Criativa",
                        "Modo individual/multijogador"
                    }
                },
                {
                    "id_ID",
                    new string[]
                    {
                        "Pilih Mode Game",
                        "Mode Pulau Kreatif",
                        "Mode Pemain Tunggal/Multipemain"
                    }
                },
                {
                    "ru_RU",
                    new string[]
                    {
                        "\u0412\u044B\u0431\u0435\u0440\u0438\u0442\u0435 \u0440\u0435\u0436\u0438\u043C \u0438\u0433\u0440\u044B",
                        "\u0420\u0435\u0436\u0438\u043C \u0442\u0432\u043E\u0440\u0447\u0435\u0441\u043A\u043E\u0433\u043E \u043E\u0441\u0442\u0440\u043E\u0432\u0430",
                        "\u041E\u0434\u0438\u043D\u043E\u0447\u043D\u044B\u0439/\u0441\u0435\u0442\u0435\u0432\u043E\u0439 \u0440\u0435\u0436\u0438\u043C"
                    }
                },
                {
                    "th_TH",
                    new string[]
                    {
                        "\u0E40\u0E25\u0E37\u0E2D\u0E01\u0E42\u0E2B\u0E21\u0E14\u0E40\u0E01\u0E21",
                        "\u0E42\u0E2B\u0E21\u0E14\u0E40\u0E01\u0E32\u0E30\u0E2A\u0E23\u0E49\u0E32\u0E07\u0E2A\u0E23\u0E23\u0E04\u0E4C",
                        "\u0E42\u0E2B\u0E21\u0E14\u0E1C\u0E39\u0E49\u0E40\u0E25\u0E48\u0E19\u0E40\u0E14\u0E35\u0E48\u0E22\u0E27/\u0E2B\u0E25\u0E32\u0E22\u0E04\u0E19"
                    }
                },
                {
                    "de_DE",
                    new string[]
                    {
                        "Spielmodus ausw\u00E4hlen",
                        "Kreativinsel-Modus",
                        "Einzel-/Mehrspielermodus"
                    }
                },
                {
                    "fr_FR",
                    new string[]
                    {
                        "S\u00E9lectionner le mode de jeu",
                        "Mode \u00CEle cr\u00E9ative",
                        "Mode solo/multijoueur"
                    }
                },
                {
                    "zh_TW",
                    new string[]
                    {
                        "\u9078\u64C7\u904A\u6232\u6A21\u5F0F",
                        "\u5275\u610F\u5CF6\u6A21\u5F0F",
                        "\u55AE\u4EBA/\u591A\u4EBA\u6A21\u5F0F"
                    }
                }
            };

        public static bool IsModeKey(string key)
        {
            return key == CreativeKey || key == SingleMultiKey;
        }

        public static string GetModeName(string key)
        {
            if (key == SingleMultiKey)
            {
                return GetLocalizedText(SingleMultiKey);
            }
            return GetLocalizedText(CreativeKey);
        }

        public static string GetTitle()
        {
            return GetLocalizedText(TitleKey);
        }

        private static string GetLocalizedText(string key)
        {
            int index = key == TitleKey ? 0 : (key == CreativeKey ? 1 : 2);
            string[] labels;
            string locale = LocalizeSystem.Locale;
            if (string.IsNullOrEmpty(locale) || !LabelsByLocale.TryGetValue(locale, out labels))
            {
                labels = LabelsByLocale["en_US"];
            }
            return labels[index];
        }

        public static void SaveMode(string key)
        {
            if (!IsModeKey(key))
            {
                return;
            }
            Preferences.SetString(PrefKey, key, Preferences.Level.Device);
            Preferences.SetString(LastClusterPrefKey, key, Preferences.Level.Device);
            if (SelectGameModePlugin.Log != null)
            {
                SelectGameModePlugin.Log.LogInfo("Selected game mode: " + GetModeName(key) + " (" + key + ")");
            }
        }

        public static void Install(TitleMenuUserControlBase control)
        {
            if (control == null || TitleClustersField == null)
            {
                return;
            }
            Clusters clusters = TitleClustersField.GetValue(control) as Clusters;
            Install(clusters);
        }

        public static void Install(Clusters clusters)
        {
            if (clusters == null || ClustersField == null)
            {
                return;
            }

            Dictionary<string, Cluster> map = ClustersField.GetValue(clusters) as Dictionary<string, Cluster>;
            if (map == null)
            {
                return;
            }

            if (!HasModeClusters(map))
            {
                map.Clear();

                Server creative = new Server("free", MakeNames(CreativeKey));
                creative.Cluster.IsRecommendable = true;
                creative.Cluster.Mode = Mode.Editable;
                map[CreativeKey] = creative.Cluster;

                Server singleMulti = new Server("single_multi", MakeNames(SingleMultiKey));
                singleMulti.Cluster.IsRecommendable = false;
                singleMulti.Cluster.Mode = Mode.Offline;
                map[SingleMultiKey] = singleMulti.Cluster;
            }
            else
            {
                map[CreativeKey].Names = MakeNames(CreativeKey);
                map[SingleMultiKey].Names = MakeNames(SingleMultiKey);
            }

            string selected = Preferences.GetString(LastClusterPrefKey, string.Empty, Preferences.Level.Device);
            string savedMode = Preferences.GetString(PrefKey, string.Empty, Preferences.Level.Device);
            if (!IsModeKey(selected))
            {
                selected = IsModeKey(savedMode) ? savedMode : CreativeKey;
            }
            Preferences.SetString(LastClusterPrefKey, selected, Preferences.Level.Device);
            Preferences.SetString(PrefKey, selected, Preferences.Level.Device);
        }

        private static bool HasModeClusters(Dictionary<string, Cluster> map)
        {
            Cluster creative;
            Cluster singleMulti;
            return map.TryGetValue(CreativeKey, out creative)
                && creative != null
                && !string.IsNullOrEmpty(creative.GatewayUrlRoot)
                && map.TryGetValue(SingleMultiKey, out singleMulti)
                && singleMulti != null
                && !string.IsNullOrEmpty(singleMulti.GatewayUrlRoot);
        }

        public static void SetLabel(object instance, string fieldName, string text)
        {
            if (instance == null)
            {
                return;
            }
            FieldInfo field = instance.GetType().GetField(fieldName, Flags);
            UILabel label = field == null ? null : field.GetValue(instance) as UILabel;
            if (label != null)
            {
                label.text = text;
            }
        }

        public static void RefreshLocalizedUi()
        {
            UnityEngine.Object[] controls = Resources.FindObjectsOfTypeAll(typeof(TitleMenuUserControlBase));
            if (controls != null)
            {
                for (int i = 0; i < controls.Length; i++)
                {
                    TitleMenuUserControlBase control = controls[i] as TitleMenuUserControlBase;
                    if (control != null)
                    {
                        Install(control);
                        SetLabel(control, "_serverSelectionLabel", GetTitle());
                    }
                }
            }

            UnityEngine.Object[] selections = Resources.FindObjectsOfTypeAll(typeof(TitleClusterSelection));
            if (selections == null)
            {
                return;
            }
            for (int i = 0; i < selections.Length; i++)
            {
                SetLabel(selections[i], "_titleLabel", GetTitle());
            }
        }

        private static Dictionary<string, string> MakeNames(string key)
        {
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string[]> pair in LabelsByLocale)
            {
                names[pair.Key] = pair.Value[key == CreativeKey ? 1 : 2];
            }
            return names;
        }
    }

    [HarmonyPatch(typeof(TitleMenuUserControlBase), "TryUpdateClusters")]
    internal static class TitleMenuUserControlBaseTryUpdateClustersPatch
    {
        private static bool Prefix(TitleMenuUserControlBase __instance, ref bool __result)
        {
            if (GameManager.ConnectCluster != null)
            {
                return true;
            }
            SelectGameModeRuntime.Install(__instance);
            GameManager.SetArenaAuthServer(string.Empty);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(TitleMenuUserControlBase), "ShowCluster")]
    internal static class TitleMenuUserControlBaseShowClusterPatch
    {
        private static void Prefix(TitleMenuUserControlBase __instance)
        {
            if (GameManager.ConnectCluster == null)
            {
                SelectGameModeRuntime.Install(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "SetCluster")]
    internal static class GameManagerSetClusterPatch
    {
        private static void Postfix(string clusterKey)
        {
            SelectGameModeRuntime.SaveMode(clusterKey);
        }
    }

    [HarmonyPatch(typeof(TitleMenuUserControl_PC), "Start")]
    internal static class TitleMenuUserControlPCStartPatch
    {
        private static void Postfix(TitleMenuUserControl_PC __instance)
        {
            SelectGameModeRuntime.SetLabel(__instance, "_serverSelectionLabel", SelectGameModeRuntime.GetTitle());
        }
    }

    [HarmonyPatch(typeof(TitleClusterSelection), "Awake")]
    internal static class TitleClusterSelectionAwakePatch
    {
        private static void Postfix(TitleClusterSelection __instance)
        {
            SelectGameModeRuntime.SetLabel(__instance, "_titleLabel", SelectGameModeRuntime.GetTitle());
        }
    }

    [HarmonyPatch(typeof(LocalizeSystem), "SetLocale")]
    internal static class LocalizeSystemSetLocalePatch
    {
        private static void Postfix()
        {
            SelectGameModeRuntime.RefreshLocalizedUi();
        }
    }
}
