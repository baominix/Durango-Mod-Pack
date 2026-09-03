using System;
using System.Collections.Generic;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    internal sealed class UnstableIslandRecord
    {
        public readonly string TerrainId;
        public readonly int Level;
        public readonly string SeaName;
        public readonly string IslandName;
        public readonly bool ListedInFandomArchive;

        public UnstableIslandRecord(
            string terrainId,
            int level,
            string seaName,
            string islandName,
            bool listedInFandomArchive)
        {
            TerrainId = terrainId;
            Level = level;
            SeaName = seaName;
            IslandName = islandName;
            ListedInFandomArchive = listedInFandomArchive;
        }
    }

    // Runtime index for the restored client routes. Crater/resource and animal
    // details from the archive are kept in data/unstable_islands_fandom.json.
    internal static class UnstableIslandDatabase
    {
        public const string SourceUrl =
            "https://durango-archive.fandom.com/wiki/Unstable_Islands";

        private static readonly Dictionary<string, UnstableIslandRecord> Records =
            CreateRecords();

        public static UnstableIslandRecord Get(string terrainId)
        {
            UnstableIslandRecord record;
            return !string.IsNullOrEmpty(terrainId) &&
                Records.TryGetValue(terrainId, out record)
                ? record
                : null;
        }

        public static string IslandName(string terrainId, string fallback)
        {
            UnstableIslandRecord record = Get(terrainId);
            return record == null ? fallback : record.IslandName;
        }

        private static Dictionary<string, UnstableIslandRecord> CreateRecords()
        {
            UnstableIslandRecord[] records = new[]
            {
                Record("ri15sa", 15, "Savanna Sea", "Ambergrass Island", true),
                // This Lv.18 route exists in the client catalog but is absent
                // from the archived Fandom table.
                Record("ri18tr", 18, "Tropical Sea", "Verdant Fang Island", false),
                Record("ri20te", 20, "Temperate Sea", "Silverwood Island", true),
                Record("ri25tr", 25, "Tropical Sea", "Emerald Canopy Island", true),
                Record("ri30tu", 30, "Tundra Sea", "Frostwind Island", true),
                Record("ri35te", 35, "Temperate Sea", "Willowmist Island", true),
                Record("ri35de", 35, "Desert Sea", "Sunscar Island", true),
                Record("ri40tu", 40, "Tundra Sea", "Whitehorn Island", true),
                Record("ri40tr", 40, "Tropical Sea", "Rainfang Island", true),
                Record("ri45sa", 45, "Savanna Sea", "Golden Baobab Island", true),
                Record("ri45sw", 45, "Swamp Sea", "Blackwater Island", true),
                Record("ri50sn", 50, "Snowfield Sea", "Ivory Frost Island", true),
                Record("ri50de", 50, "Desert Sea", "Ruby Dune Island", true),
                Record("ri55tu", 55, "Tundra Sea", "Red Fir Island", true),
                Record("ri55tr", 55, "Tropical Blue Sea", "Blue Canopy Island", true),
                Record("ri55sw", 55, "Swamp Sea", "Lotus Mire Island", true),
                Record("ua60tu", 60, "Tundra Sea", "Stormpine Island", true),
                Record("ua60sn", 60, "Snowfield Sea", "Mammoth Frost Island", true),
                Record("ua60vol", 60, "Volcano Sea", "Ashen Caldera Island", true),
                Record("ua60sw", 60, "Swamp Sea", "Ironroot Marsh Island", true),
                Record("ua60de", 60, "Desert Sea", "Crimson Mesa Island", true),
                Record("ua60tr", 60, "Tropical Sea", "Emerald Titan Island", true),
                Record("op60te", 60, "Temperate Savage Island Sea", "Savage Ironwood Island", true),
                Record("op60tr", 60, "Tropical Savage Island Sea", "Savage Heliconia Island", true)
            };

            Dictionary<string, UnstableIslandRecord> result =
                new Dictionary<string, UnstableIslandRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                result[records[i].TerrainId] = records[i];
            }
            return result;
        }

        private static UnstableIslandRecord Record(
            string terrainId,
            int level,
            string seaName,
            string islandName,
            bool listedInFandomArchive)
        {
            return new UnstableIslandRecord(
                terrainId,
                level,
                seaName,
                islandName,
                listedInFandomArchive);
        }
    }
}
