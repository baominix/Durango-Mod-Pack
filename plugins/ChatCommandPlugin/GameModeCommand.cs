using System;
using System.Reflection;
using Durango.Logic.Clusters;
using HarmonyLib;
using Durango.System;

namespace BaoX.DurangoOriginal.ChatCommandMod
{
    internal static class GameModeCommand
    {
        private const string PreferenceKey = "baox_select_game_mode";
        private const string CreativeKey = "free_offline";
        private const string SurvivalKey = "single_multi_offline";

        internal static void Execute(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                ReplyCurrent();
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("usage_prefix", "/gamemode <survival|creative> (aliases: s/c, 0/1)"));
                return;
            }

            string value = args[0].Trim().ToLowerInvariant();
            bool creative;
            if (value == "creative" || value == "c" || value == "1")
            {
                creative = true;
            }
            else if (value == "survival" || value == "s" || value == "0")
            {
                creative = false;
            }
            else
            {
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("gamemode_unknown", args[0]));
                ChatCommandRegistry.Reply(ChatCommandLocalization.Get("gamemode_use"));
                return;
            }

            string key = creative ? CreativeKey : SurvivalKey;
            Mode mode = creative ? Mode.Editable : Mode.Offline;
            Preferences.SetString(PreferenceKey, key, Preferences.Level.Device);
            Preferences.SetString("last_selected_cluster_key", key, Preferences.Level.Device);

            // SetCluster is patched by SelectGameMode and persists the supplied
            // cluster key. Passing the previous key here immediately changed the
            // preference back to the previous mode (usually creative).
            GameManager.SetCluster(key, GameManager.GatewayUrl, mode);
            RefreshCraftBuildAvailability();

            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("gamemode_changed", ChatCommandLocalization.Get(creative ? "creative_name" : "survival_name")));
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get(
                creative ? "craft_free" : "craft_survival"));
            if (ChatCommandPlugin.Log != null)
            {
                ChatCommandPlugin.Log.LogInfo("Game mode command selected " + key + " (" + mode + ").");
            }
        }

        private static void RefreshCraftBuildAvailability()
        {
            try
            {
                Type backendType = AccessTools.TypeByName("BaoX.DurangoOriginal.CraftBuildMod.CraftBuildBackend");
                if (backendType == null)
                {
                    return;
                }

                MethodInfo refresh = backendType.GetMethod(
                    "RefreshLocalAvailability",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (refresh != null)
                {
                    refresh.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                if (ChatCommandPlugin.Log != null)
                {
                    ChatCommandPlugin.Log.LogWarning("Craft/build availability refresh after gamemode change failed: " + ex.Message);
                }
            }
        }

        private static void ReplyCurrent()
        {
            string selected = Preferences.GetString(PreferenceKey, string.Empty, Preferences.Level.Device);
            bool creative = string.Equals(selected, CreativeKey, StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(selected) && GameManager.ClusterMode == Mode.Editable);
            ChatCommandRegistry.Reply(ChatCommandLocalization.Get("gamemode_current", ChatCommandLocalization.Get(creative ? "creative_name" : "survival_name")));
        }
    }
}
