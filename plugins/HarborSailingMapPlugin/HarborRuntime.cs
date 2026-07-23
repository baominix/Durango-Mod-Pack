using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Durango.Offline;
using Durango.Utils;
using HarmonyLib;
using Messages;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    internal sealed class HarborSaveState
    {
        public string CurrentSaveKey = string.Empty;
        public string CurrentTerrainId = string.Empty;
        public string HomeTerrainId = string.Empty;
    }

    internal static class HarborRuntime
    {
        private static readonly FieldInfo PlayerWorldField = AccessTools.Field(typeof(Durango.Offline.Player), "_world");
        private static readonly FieldInfo PlayerContextField = AccessTools.Field(typeof(Durango.Offline.Player), "_context");
        private static readonly FieldInfo WorldContextField = AccessTools.Field(typeof(World), "_context");

        public static World GetWorld(Durango.Offline.Player player)
        {
            return PlayerWorldField == null ? null : PlayerWorldField.GetValue(player) as World;
        }

        public static PlayerContext GetPlayerContext(Durango.Offline.Player player)
        {
            return PlayerContextField == null ? null : PlayerContextField.GetValue(player) as PlayerContext;
        }

        public static WorldContext GetWorldContext(Durango.Offline.Player player)
        {
            return GetWorldContext(GetWorld(player));
        }

        public static WorldContext GetWorldContext(World world)
        {
            return world == null || WorldContextField == null ? null : WorldContextField.GetValue(world) as WorldContext;
        }

        public static bool IsAwayFromHome(Durango.Offline.Player player)
        {
            WorldContext world = GetWorldContext(player);
            if (world == null || string.IsNullOrEmpty(world.Path)) return false;
            HarborSaveState state = LoadState(world);
            return !string.IsNullOrEmpty(state.CurrentSaveKey) && state.CurrentTerrainId == world.TerrainId;
        }

        public static bool Sail(Durango.Offline.Player player, SailTarget target)
        {
            if (player == null || target == null) return false;
            WorldContext current = GetWorldContext(player);
            PlayerContext playerContext = GetPlayerContext(player);
            if (current == null || playerContext == null || string.IsNullOrEmpty(current.Path))
            {
                HarborSailingMapPlugin.Log.LogWarning("Sail failed: offline contexts were not found.");
                return false;
            }

            try
            {
                HarborSaveState state = LoadState(current);
                PersistWorld(current);
                if (string.IsNullOrEmpty(state.CurrentSaveKey))
                {
                    File.Copy(current.Path, HomeSnapshotPath(current), true);
                    state.HomeTerrainId = current.TerrainId ?? string.Empty;
                }
                else
                {
                    File.Copy(current.Path, RouteSnapshotPath(current, state.CurrentSaveKey), true);
                }

                string targetSnapshot = RouteSnapshotPath(current, target.SaveKey);
                WorldContext loaded = File.Exists(targetSnapshot) ? LoadSnapshotIntoCurrent(targetSnapshot, current.Path) : CreateEmptyCurrent(current.Path, current.PlayerSlot, target.TerrainId);
                CopyWorld(loaded, current);
                state.CurrentSaveKey = target.SaveKey;
                state.CurrentTerrainId = target.TerrainId;
                SaveState(current, state);
                ResetPlayerToEntry(playerContext);
                PersistWorld(current);
                playerContext.Save();

                HarborSailingMapPlugin.Log.LogInfo("Sailing " + (state.HomeTerrainId ?? string.Empty) + " -> " + target.Name + " using snapshot " + targetSnapshot);
                RestartIntoCurrentWorld();
                return true;
            }
            catch (Exception ex)
            {
                HarborSailingMapPlugin.Log.LogError("Sail transition failed: " + ex);
                return false;
            }
        }

        public static bool ReturnHome(Durango.Offline.Player player)
        {
            if (player == null) return false;
            WorldContext current = GetWorldContext(player);
            PlayerContext playerContext = GetPlayerContext(player);
            if (current == null || playerContext == null || string.IsNullOrEmpty(current.Path)) return false;

            try
            {
                HarborSaveState state = LoadState(current);
                string home = HomeSnapshotPath(current);
                if (string.IsNullOrEmpty(state.CurrentSaveKey) || !File.Exists(home))
                {
                    HarborSailingMapPlugin.Log.LogWarning("Return home failed: no Harbor home snapshot exists.");
                    return false;
                }

                PersistWorld(current);
                File.Copy(current.Path, RouteSnapshotPath(current, state.CurrentSaveKey), true);
                WorldContext loaded = LoadSnapshotIntoCurrent(home, current.Path);
                CopyWorld(loaded, current);
                state.CurrentSaveKey = string.Empty;
                state.CurrentTerrainId = string.Empty;
                state.HomeTerrainId = current.TerrainId ?? state.HomeTerrainId;
                SaveState(current, state);
                ResetPlayerToEntry(playerContext);
                PersistWorld(current);
                playerContext.Save();

                HarborSailingMapPlugin.Log.LogInfo("Returning to home terrain " + current.TerrainId);
                RestartIntoCurrentWorld();
                return true;
            }
            catch (Exception ex)
            {
                HarborSailingMapPlugin.Log.LogError("Return home failed: " + ex);
                return false;
            }
        }

        private static WorldContext CreateEmptyCurrent(string currentPath, int slot, string terrainId)
        {
            WorldContext context = new WorldContext();
            context.PlayerSlot = slot;
            context.TerrainId = terrainId;
            context.Persistent = false;
            context.Initialize(currentPath);
            PersistWorld(context);
            return context;
        }

        private static WorldContext LoadSnapshotIntoCurrent(string snapshotPath, string currentPath)
        {
            File.Copy(snapshotPath, currentPath, true);
            WorldContext loaded = WorldContext.Load(currentPath);
            if (loaded == null) throw new InvalidDataException("Could not load Harbor snapshot: " + snapshotPath);
            loaded.Persistent = false;
            return loaded;
        }

        private static void CopyWorld(WorldContext source, WorldContext destination)
        {
            string path = destination.Path;
            int slot = destination.PlayerSlot;
            destination.PlayerSlot = slot;
            destination.TerrainId = source.TerrainId;
            destination.Artifacts = source.Artifacts;
            destination.ArtifactAddOns = source.ArtifactAddOns;
            destination.ArtifactMannequins = source.ArtifactMannequins;
            destination.AddedNatural = source.AddedNatural;
            destination.RemovedNatural = source.RemovedNatural;
            destination.GrazedPetList = source.GrazedPetList;
            destination.Garden = source.Garden;
            destination.Persistent = false;
            destination.Initialize(path);
        }

        private static void ResetPlayerToEntry(PlayerContext context)
        {
            AppearPlayer appear = context.AppearPlayer;
            Move move = appear.Move;
            move.Movements = null;
            appear.Move = move;
            context.AppearPlayer = appear;
        }

        private static void RestartIntoCurrentWorld()
        {
            GameManager.Emigrated = GameManager.EmigratedType.Explore;
            Durango.Utils.Singleton<GameManager>.Instance().MoveToTitle();
        }

        private static void PersistWorld(WorldContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.Path)) throw new InvalidOperationException("World path is missing.");
            byte[] bytes = Json.WriteToBytes<WorldContext>(context, true, null);
            File.WriteAllBytes(context.Path, bytes);
        }

        private static HarborSaveState LoadState(WorldContext context)
        {
            HarborSaveState state = new HarborSaveState();
            string path = StatePath(context);
            if (!File.Exists(path)) return state;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                int split = lines[i].IndexOf('=');
                if (split <= 0) continue;
                string key = lines[i].Substring(0, split);
                string value = lines[i].Substring(split + 1);
                if (key == "current_save_key") state.CurrentSaveKey = value;
                else if (key == "current_terrain_id") state.CurrentTerrainId = value;
                else if (key == "home_terrain_id") state.HomeTerrainId = value;
            }
            return state;
        }

        private static void SaveState(WorldContext context, HarborSaveState state)
        {
            File.WriteAllLines(StatePath(context), new string[]
            {
                "version=1",
                "current_save_key=" + (state.CurrentSaveKey ?? string.Empty),
                "current_terrain_id=" + (state.CurrentTerrainId ?? string.Empty),
                "home_terrain_id=" + (state.HomeTerrainId ?? string.Empty)
            });
        }

        private static string StatePath(WorldContext context)
        {
            return Path.Combine(Path.GetDirectoryName(context.Path), context.PlayerSlot + ".harbor.state");
        }

        private static string HomeSnapshotPath(WorldContext context)
        {
            return Path.Combine(Path.GetDirectoryName(context.Path), context.PlayerSlot + ".harbor.home");
        }

        private static string RouteSnapshotPath(WorldContext context, string saveKey)
        {
            string clean = (saveKey ?? "unknown").Replace("/", string.Empty).Replace("\\", string.Empty).Replace("..", string.Empty);
            return Path.Combine(Path.GetDirectoryName(context.Path), context.PlayerSlot + ".harbor." + clean);
        }
    }
}
