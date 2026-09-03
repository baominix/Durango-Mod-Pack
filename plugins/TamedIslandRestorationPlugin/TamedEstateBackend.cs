using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using BaoX.DurangoOriginal.HarborSailingMap;
using Durango.Logic.Estate;
using Durango.Offline;
using Durango.UI;
using Durango.UI.InGame;
using Durango.Utils;
using HarmonyLib;
using Messages;
using Shared.Estate;
using OfflinePlayer = Durango.Offline.Player;
using PacketHeader = Durango.Network.PacketHeader;

namespace BaoX.DurangoOriginal.TamedIslandRestoration
{
    internal static class TamedEstateState
    {
        private static readonly Dictionary<string, ConfigEntry<string>> UnitEntries =
            new Dictionary<string, ConfigEntry<string>>();
        private static readonly Dictionary<string, ConfigEntry<bool>> DeclaredEntries =
            new Dictionary<string, ConfigEntry<bool>>();
        private static readonly HashSet<string> LoggedGridKeys = new HashSet<string>();
        private static readonly FieldInfo CenterXField = AccessTools.Field(typeof(OfflinePlayer), "_centerX");
        private static readonly FieldInfo CenterYField = AccessTools.Field(typeof(OfflinePlayer), "_centerY");

        public static List<Point2> GetUnits(string ownerId, string terrainId)
        {
            if (!IsDeclared(ownerId, terrainId)) return new List<Point2>();
            ConfigEntry<string> entry = GetUnitEntry(ownerId, terrainId);
            List<Point2> units = ParseUnits(entry.Value);
            if (units.Count == 0)
            {
                SetDeclared(ownerId, terrainId, false);
            }
            return units;
        }

        public static bool IsDeclared(string ownerId, string terrainId)
        {
            return GetDeclaredEntry(ownerId, terrainId).Value;
        }

        public static Point2 GetAnchorTile(string ownerId, string terrainId)
        {
            List<Point2> units = GetUnits(ownerId, terrainId);
            return units[0] * 4;
        }

        public static bool IsTamedWorld(OfflinePlayer player, out World world, out string terrainId)
        {
            world = HarborIslandApi.GetCurrentWorld(player);
            terrainId = world == null
                ? null
                : HarborIslandApi.GetCurrentTamedTerrainId(world);
            return world != null && !string.IsNullOrEmpty(terrainId);
        }

        public static void SendGrid(OfflinePlayer player, string ownerId)
        {
            World world;
            string terrainId;
            if (!IsTamedWorld(player, out world, out terrainId)) return;

            int centerX = CenterXField == null ? 0 : (int)CenterXField.GetValue(player);
            int centerY = CenterYField == null ? 0 : (int)CenterYField.GetValue(player);
            List<Point2> chunks = new List<Point2>();
            HashSet<Point2> visibleChunks = new HashSet<Point2>();
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                for (int y = centerY - 1; y <= centerY + 1; y++)
                {
                    if (x < 0 || y < 0 || x >= world.NumChunksX || y >= world.NumChunksY) continue;
                    Point2 chunk = new Point2(x, y);
                    chunks.Add(chunk);
                    visibleChunks.Add(chunk);
                }
            }

            string estateId = EstateId(ownerId);
            Dictionary<Point2, string> cells = new Dictionary<Point2, string>();
            List<Point2> units = GetUnits(ownerId, terrainId);
            for (int i = 0; i < units.Count; i++)
            {
                Point2 tile = units[i] * 4;
                Point2 chunk = tile / 16;
                if (visibleChunks.Contains(chunk)) cells[units[i]] = estateId;
            }

            bool declared = IsDeclared(ownerId, terrainId);
            EstateGrids grids = new EstateGrids
            {
                Chunks = chunks.ToArray(),
                Cells = cells,
                EstateLicenses = declared
                    ? new[] { TamedIslandData.MakeLicense(ownerId) }
                    : new EstateLicense[0]
            };
            player.Send<EstateGrids>(grids, 0U);

            string logKey = ownerId + "|" + terrainId;
            if (LoggedGridKeys.Add(logKey))
            {
                TamedIslandRestorationPlugin.Log.LogInfo(
                    "Synced Tamed estate grid: terrain=" + terrainId +
                    ", declared=" + declared +
                    ", units=" + units.Count + ", visible=" + cells.Count);
            }
        }

        public static void HandleDeclare(OfflinePlayer player, string ownerId,
            DeclareEstate request, PacketHeader header)
        {
            World world;
            string terrainId;
            if (!IsTamedWorld(player, out world, out terrainId) ||
                request.OwnerType != OwnerType.PersonalPlayer) return;

            Point2 tile = request.Cell * 4;
            if (tile.x < 0 || tile.y < 0 || tile.x + 3 >= world.NumChunksX * 16 ||
                tile.y + 3 >= world.NumChunksY * 16)
            {
                player.Send<Abort>(default(Abort), header.Seq);
                return;
            }

            List<Point2> units = new List<Point2> { request.Cell };
            SaveUnits(ownerId, terrainId, units);
            SendGrid(player, ownerId);
            player.Send<EstateLicense>(TamedIslandData.MakeLicense(ownerId), header.Seq);
            TamedIslandRestorationPlugin.Log.LogInfo(
                "Declared first Tamed estate cell at " + request.Cell + " on " + terrainId);
        }

        public static void HandleExpand(OfflinePlayer player, string ownerId,
            ExpandEstate request, PacketHeader header)
        {
            World world;
            string terrainId;
            if (!IsTamedWorld(player, out world, out terrainId) || request.EstateId != EstateId(ownerId)) return;

            if (!IsDeclared(ownerId, terrainId)) return;

            List<Point2> units = GetUnits(ownerId, terrainId);
            int maxSize = TamedPioneerState.GetMaximumEstateSize(ownerId);
            bool adjacent = false;
            for (int i = 0; i < units.Count; i++)
            {
                int distance = Math.Abs(units[i].x - request.Cell.x) + Math.Abs(units[i].y - request.Cell.y);
                if (distance == 1) adjacent = true;
            }
            if (!units.Contains(request.Cell) && adjacent && units.Count < maxSize)
            {
                units.Add(request.Cell);
                SaveUnits(ownerId, terrainId, units);
                TamedIslandRestorationPlugin.Log.LogInfo(
                    "Expanded Tamed estate at " + request.Cell + "; size=" + units.Count);
            }

            SendGrid(player, ownerId);
            player.Send<EstateLicense>(TamedIslandData.MakeLicense(ownerId), header.Seq);
        }

        public static void HandleShrink(OfflinePlayer player, string ownerId,
            ShrinkEstate request, PacketHeader header)
        {
            World world;
            string terrainId;
            if (!IsTamedWorld(player, out world, out terrainId) || request.EstateId != EstateId(ownerId)) return;

            if (!IsDeclared(ownerId, terrainId)) return;

            List<Point2> units = GetUnits(ownerId, terrainId);
            if (units.Count > 1 && units.Contains(request.Cell))
            {
                List<Point2> remaining = new List<Point2>(units);
                remaining.Remove(request.Cell);
                if (IsConnected(remaining))
                {
                    SaveUnits(ownerId, terrainId, remaining);
                    TamedIslandRestorationPlugin.Log.LogInfo(
                        "Reduced Tamed estate at " + request.Cell + "; size=" + remaining.Count);
                }
            }

            SendGrid(player, ownerId);
            player.Send<EstateLicense>(TamedIslandData.MakeLicense(ownerId), header.Seq);
        }

        public static void HandleRemove(OfflinePlayer player, string ownerId, RemoveEstate request)
        {
            World world;
            string terrainId;
            if (!IsTamedWorld(player, out world, out terrainId) || request.EstateId != EstateId(ownerId)) return;

            ConfigEntry<string> unitsEntry = GetUnitEntry(ownerId, terrainId);
            unitsEntry.Value = string.Empty;
            SetDeclared(ownerId, terrainId, false);
            TamedIslandRestorationPlugin.EstateSize.Value = 0;
            unitsEntry.ConfigFile.Save();
            SendGrid(player, ownerId);
            TamedIslandRestorationPlugin.Log.LogInfo(
                "Released Tamed estate cells; the player can select a new first cell.");
        }

        public static string EstateId(string ownerId)
        {
            return "offline:tamed:estate:" + ownerId;
        }

        private static ConfigEntry<string> GetUnitEntry(string ownerId, string terrainId)
        {
            string safeOwner = Sanitize(ownerId);
            string key = safeOwner + "|" + terrainId;
            ConfigEntry<string> entry;
            if (UnitEntries.TryGetValue(key, out entry)) return entry;

            entry = TamedIslandRestorationPlugin.PluginConfig.Bind(
                "Estate Layout " + safeOwner, terrainId,
                string.Empty,
                "Owned 4x4 estate cells for this offline Tamed Island terrain.");
            UnitEntries[key] = entry;
            return entry;
        }

        private static void SaveUnits(string ownerId, string terrainId, List<Point2> units)
        {
            ConfigEntry<string> entry = GetUnitEntry(ownerId, terrainId);
            entry.Value = SerializeUnits(units);
            SetDeclared(ownerId, terrainId, units.Count > 0);
            TamedIslandRestorationPlugin.EstateSize.Value = units.Count;
            entry.ConfigFile.Save();
        }

        private static ConfigEntry<bool> GetDeclaredEntry(string ownerId, string terrainId)
        {
            string safeOwner = Sanitize(ownerId);
            string key = safeOwner + "|" + terrainId;
            ConfigEntry<bool> entry;
            if (DeclaredEntries.TryGetValue(key, out entry)) return entry;

            entry = TamedIslandRestorationPlugin.PluginConfig.Bind(
                "Estate Declaration " + safeOwner, terrainId,
                false,
                "True after the player selects the first 4x4 estate cell on this terrain.");
            DeclaredEntries[key] = entry;
            return entry;
        }

        private static void SetDeclared(string ownerId, string terrainId, bool declared)
        {
            ConfigEntry<bool> entry = GetDeclaredEntry(ownerId, terrainId);
            entry.Value = declared;
            entry.ConfigFile.Save();
        }

        private static List<Point2> ParseUnits(string value)
        {
            List<Point2> units = new List<Point2>();
            if (string.IsNullOrEmpty(value)) return units;
            string[] cells = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < cells.Length; i++)
            {
                string[] pair = cells[i].Split(',');
                int x;
                int y;
                if (pair.Length == 2 && int.TryParse(pair[0], out x) && int.TryParse(pair[1], out y))
                {
                    Point2 unit = new Point2(x, y);
                    if (!units.Contains(unit)) units.Add(unit);
                }
            }
            return units;
        }

        private static string SerializeUnits(List<Point2> units)
        {
            string[] cells = new string[units.Count];
            for (int i = 0; i < units.Count; i++) cells[i] = units[i].x + "," + units[i].y;
            return string.Join(";", cells);
        }

        private static bool IsConnected(List<Point2> units)
        {
            if (units.Count <= 1) return true;
            HashSet<Point2> visited = new HashSet<Point2>();
            Queue<Point2> queue = new Queue<Point2>();
            queue.Enqueue(units[0]);
            visited.Add(units[0]);
            Point2[] directions =
            {
                new Point2(1, 0), new Point2(-1, 0), new Point2(0, 1), new Point2(0, -1)
            };
            while (queue.Count > 0)
            {
                Point2 current = queue.Dequeue();
                for (int i = 0; i < directions.Length; i++)
                {
                    Point2 next = current + directions[i];
                    if (units.Contains(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited.Count == units.Count;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "local-player";
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '-' && chars[i] != '_') chars[i] = '_';
            }
            return new string(chars);
        }
    }

    [HarmonyPatch(typeof(OfflinePlayer), "SetCenterChunks")]
    internal static class TamedEstateChunkSyncPatch
    {
        private static void Postfix(OfflinePlayer __instance)
        {
            if (!TamedIslandRestorationPlugin.Enabled.Value || !__instance.IsLocalPlayer) return;
            TamedEstateState.SendGrid(__instance, __instance.EntityId);
        }
    }

    [HarmonyPatch(typeof(EstateGridGroup), "RefreshExpandGrid")]
    internal static class TamedEstateReduceButtonsPatch
    {
        private static readonly FieldInfo ExpandEstateField = AccessTools.Field(typeof(EstateGridGroup), "_expandEstate");
        private static readonly FieldInfo AreaListField = AccessTools.Field(typeof(EstateGridGroup), "_areaList");
        private static readonly MethodInfo AddShrinkButtonsMethod = typeof(EstateGridGroup).GetMethod(
            "AddShrinkButtons", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Postfix(EstateGridGroup __instance)
        {
            EstateInfo estate = ExpandEstateField == null ? null : ExpandEstateField.GetValue(__instance) as EstateInfo;
            if (estate == null || estate.License.Type != OwnerType.PersonalPlayer ||
                string.IsNullOrEmpty(estate.License.RegionId) ||
                !estate.License.RegionId.StartsWith("tamed|", StringComparison.Ordinal) ||
                estate.Units.Count <= 1 || AddShrinkButtonsMethod == null) return;

            AddShrinkButtonsMethod.Invoke(__instance, null);
            List<GridAreaBase> areas = AreaListField == null
                ? null
                : AreaListField.GetValue(__instance) as List<GridAreaBase>;
            if (areas != null)
            {
                Singleton<GridAreaViewer>.Instance().Show(areas, GridAreaViewer.LayerType.Bottom, false);
            }
        }
    }
}
