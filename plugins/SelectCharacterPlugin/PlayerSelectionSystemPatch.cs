using System;
using System.Collections.Generic;
using System.Reflection;
using Durango.Logic.Clusters;
using HarmonyLib;

namespace BaoX.DurangoOriginal.SelectCharacterMod
{
    internal static class LastCharacterSelection
    {
        private const string LastClusterKey = "last_selected_cluster_key";
        private const string CharacterKeyPrefix = "baox_last_selected_character_";
        private const string SlotKeyPrefix = "baox_last_selected_character_slot_";
        private static readonly HashSet<Account> RestoredAccounts = new HashSet<Account>();

        internal static string GetModeKey()
        {
            string mode = Preferences.GetString(LastClusterKey, "free_offline", Preferences.Level.Device);
            return mode == "single_multi_offline" ? "single_multi_offline" : "free_offline";
        }

        internal static void Save(string playerEntityId, int slotIndex)
        {
            if (string.IsNullOrEmpty(playerEntityId))
            {
                return;
            }
            string mode = GetModeKey();
            Preferences.SetString(CharacterKeyPrefix + mode, playerEntityId, Preferences.Level.Device);
            Preferences.SetInt(SlotKeyPrefix + mode, Math.Max(0, slotIndex), Preferences.Level.Device);
            SelectCharacterPlugin.Log.LogInfo("Remembered character " + playerEntityId + " for " + mode + ".");
        }

        internal static void Restore(Account account)
        {
            if (account == null || account.Players == null || account.Players.Count == 0)
            {
                return;
            }
            // Restore a saved character only when this account is first loaded.
            // The title UI deliberately caches an empty player id when the user
            // selects an empty slot. Restoring on every GetRecommendedPlayer()
            // call overwrote that empty id and prevented new-character creation.
            if (!RestoredAccounts.Add(account))
            {
                return;
            }
            string mode = GetModeKey();
            string playerEntityId = Preferences.GetString(CharacterKeyPrefix + mode, string.Empty, Preferences.Level.Device);
            if (string.IsNullOrEmpty(playerEntityId))
            {
                return;
            }
            for (int i = 0; i < account.Players.Count; i++)
            {
                if (account.Players[i] != null && account.Players[i].PlayerEntityId == playerEntityId)
                {
                    account.ApplyRecommendedPlayer(new Pair<string, int>(playerEntityId, i));
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Account), "ApplyRecommendedPlayer")]
    internal static class Account_ApplyRecommendedPlayer_Patch
    {
        private static void Postfix(Pair<string, int> idToSlot)
        {
            LastCharacterSelection.Save(idToSlot.Item1, idToSlot.Item2);
        }
    }

    [HarmonyPatch(typeof(Account), "GetRecommendedPlayer")]
    internal static class Account_GetRecommendedPlayer_Patch
    {
        private static void Prefix(Account __instance)
        {
            LastCharacterSelection.Restore(__instance);
        }
    }

    [HarmonyPatch(typeof(PlayerSelectionSystem), "ChangePlayer")]
    internal static class PlayerSelectionSystem_ChangePlayer_Patch
    {
        private static void Prefix(PlayerSelectionSystem __instance, string playerEntityId)
        {
            LastCharacterSelection.Save(playerEntityId, GetPlayerIndex(__instance, playerEntityId));
        }

        private static int GetPlayerIndex(PlayerSelectionSystem system, string playerEntityId)
        {
            FieldInfo field = AccessTools.Field(typeof(PlayerSelectionSystem), "_players");
            List<PlayerInfo> players = field == null ? null : field.GetValue(system) as List<PlayerInfo>;
            if (players == null) return 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].PlayerEntityId == playerEntityId) return i;
            }
            return 0;
        }
    }

    [HarmonyPatch(typeof(PlayerSelectionSystem), "UpdateAccounts")]
    internal static class PlayerSelectionSystem_UpdateAccounts_Patch
    {
        private static bool Prefix(PlayerSelectionSystem __instance, Action updated)
        {
            SelectCharacterPlugin.Log.LogInfo("PlayerSelectionSystem.UpdateAccounts prefix triggered");

            Cluster cluster = null;
            if (GameManager.ConnectCluster != null && GameManager.ConnectCluster.OnRequestAccount != null)
            {
                cluster = GameManager.ConnectCluster;
                SelectCharacterPlugin.Log.LogInfo("Using GameManager.ConnectCluster");
            }
            else
            {
                string selectedCluster = Preferences.GetString("last_selected_cluster_key", "free_offline", Preferences.Level.Device);
                string serverKey = (selectedCluster == "single_multi_offline") ? "single_multi" : "free";
                SelectCharacterPlugin.Log.LogInfo("GameManager.ConnectCluster is null, instantiating Server for key: " + serverKey);
                Durango.Offline.Server offlineServer = new Durango.Offline.Server(serverKey, new Dictionary<string, string>());
                cluster = offlineServer.Cluster;
            }

            if (cluster == null || cluster.OnRequestAccount == null)
            {
                SelectCharacterPlugin.Log.LogWarning("Failed to resolve offline cluster or OnRequestAccount.");
                return true;
            }

            cluster.OnRequestAccount(delegate (Account account)
            {
                SelectCharacterPlugin.Log.LogInfo("OnRequestAccount callback invoked. account is null: " + (account == null));
                if (account == null)
                {
                    return;
                }

                LastCharacterSelection.Restore(account);

                // this._players = account.Players;
                AccessTools.Field(typeof(PlayerSelectionSystem), "_players").SetValue(__instance, account.Players);

                int size = account.Players == null ? 0 : account.Players.Count;

                // this.PlayerSlotExceeded = (account.PlayerSlotCount < size);
                PropertyInfo prop1 = AccessTools.Property(typeof(PlayerSelectionSystem), "PlayerSlotExceeded");
                if (prop1 != null) prop1.SetValue(__instance, account.PlayerSlotCount < size, null);

                // this.PlayerSlotCount = account.PlayerSlotCount;
                PropertyInfo prop2 = AccessTools.Property(typeof(PlayerSelectionSystem), "PlayerSlotCount");
                if (prop2 != null) prop2.SetValue(__instance, account.PlayerSlotCount, null);

                // this.EmptySlotCount = Mathf.Max(account.PlayerSlotCount - size, 0);
                int emptyCount = Math.Max(account.PlayerSlotCount - size, 0);
                PropertyInfo prop3 = AccessTools.Property(typeof(PlayerSelectionSystem), "EmptySlotCount");
                if (prop3 != null) prop3.SetValue(__instance, emptyCount, null);

                // int num = Mathf.Max(account.PlayerSlotCount, size);
                int num = Math.Max(account.PlayerSlotCount, size);
                
                // this.LockedSlotCount = Mathf.Max(account.MaxPlayerSlotCount - num, 0);
                int lockedCount = Math.Max(account.MaxPlayerSlotCount - num, 0);
                PropertyInfo prop4 = AccessTools.Property(typeof(PlayerSelectionSystem), "LockedSlotCount");
                if (prop4 != null) prop4.SetValue(__instance, lockedCount, null);

                // if (this.AccountsUpdated != null) { this.AccountsUpdated(account.Players); }
                FieldInfo eventField = AccessTools.Field(typeof(PlayerSelectionSystem), "AccountsUpdated");
                if (eventField != null)
                {
                    MulticastDelegate eventDelegate = eventField.GetValue(__instance) as MulticastDelegate;
                    if (eventDelegate != null)
                    {
                        eventDelegate.DynamicInvoke(account.Players);
                    }
                }

                if (updated != null)
                {
                    updated();
                }
            });

            // Skip the original HTTP request method
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerSelectionSystem), "RequestDeletePlayer", new Type[] { typeof(string), typeof(Action<double?>) })]
    internal static class PlayerSelectionSystem_RequestDeletePlayerStatic_Patch
    {
        private static bool Prefix(string playerEntityId, Action<double?> callback)
        {
            Cluster cluster = null;
            if (GameManager.ConnectCluster != null && GameManager.ConnectCluster.OnDeletePlayer != null)
            {
                cluster = GameManager.ConnectCluster;
            }
            else
            {
                string selectedCluster = Preferences.GetString("last_selected_cluster_key", "free_offline", Preferences.Level.Device);
                string serverKey = (selectedCluster == "single_multi_offline") ? "single_multi" : "free";
                Durango.Offline.Server offlineServer = new Durango.Offline.Server(serverKey, new Dictionary<string, string>());
                cluster = offlineServer.Cluster;
            }

            if (cluster != null && cluster.OnDeletePlayer != null)
            {
                SelectCharacterPlugin.Log.LogInfo("Executing offline OnDeletePlayer for: " + playerEntityId);
                // Execute the offline cluster deletion
                cluster.OnDeletePlayer(playerEntityId);

                // Call the callback with null to indicate immediate deletion (no timer)
                if (callback != null)
                {
                    callback(null);
                }

                return false;
            }

            return true;
        }
    }
    [HarmonyPatch(typeof(PlayerInfoManager), "RequestFunc")]
    internal static class PlayerInfoManager_RequestFunc_Patch
    {
        private static bool Prefix(string key, Durango.Player.PlayerInfo cachedInfo, Action<string, Durango.Player.PlayerInfo> onResult)
        {
            try
            {
                string selectedCluster = Preferences.GetString("last_selected_cluster_key", "free_offline", Preferences.Level.Device);
                string serverKey = (selectedCluster == "single_multi_offline") ? "single_multi" : "free";

                string basePath = Durango.Offline.WorldContext.GetBasePath(serverKey);
                string offlineDir = Durango.Utils.AppData.CombinePath(basePath);
                
                if (System.IO.Directory.Exists(offlineDir))
                {
                    string[] playerFiles = System.IO.Directory.GetFiles(offlineDir, "*.player", System.IO.SearchOption.TopDirectoryOnly);
                    foreach (string playerFilePath in playerFiles)
                    {
                        string json = System.IO.File.ReadAllText(playerFilePath);
                        if (string.IsNullOrEmpty(json)) continue;
                        
                        Durango.Offline.PlayerContext ctx = Durango.Utils.Json.Read<Durango.Offline.PlayerContext>(json, false);
                        if (ctx != null && ctx.AppearPlayer.EntityId == key)
                        {
                            Durango.Player.PlayerInfo pInfo = cachedInfo ?? new Durango.Player.PlayerInfo();
                            pInfo.Valid = true;
                            pInfo.EntityId = key;
                            pInfo.Name = ctx.AppearPlayer.Name;
                            pInfo.Level = ctx.AppearPlayer.Level;
                            pInfo.Freq = ctx.AppearPlayer.Freq;
                            pInfo.Display = ctx.AppearPlayer.Display;
                            
                            pInfo.ClanId = ctx.AppearPlayer.Member.ClanId ?? string.Empty;
                            pInfo.ClanName = ctx.AppearPlayer.Member.ClanName ?? string.Empty;

                            SelectCharacterPlugin.Log.LogInfo("Successfully loaded PlayerInfo display for " + key + " from offline file: " + playerFilePath);
                            
                            onResult(key, pInfo);
                            return false; // Skip original HTTP request
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SelectCharacterPlugin.Log.LogError("Error loading offline PlayerInfo for " + key + ": " + ex.Message);
            }
            
            return true;
        }
    }
}
