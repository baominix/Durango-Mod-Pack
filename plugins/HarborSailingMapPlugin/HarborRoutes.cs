using System;
using System.Collections.Generic;
using Messages;
using Shared.Region;

namespace BaoX.DurangoOriginal.HarborSailingMap
{
    public enum HarborIslandKind
    {
        Unstable,
        Tamed
    }

    internal sealed class SailTarget
    {
        public readonly string TerrainId;
        public readonly string RegionTemplateId;
        public readonly string ArchipelagoTemplateId;
        public readonly int Level;
        public readonly Biome Biome;
        public readonly string SeaName;
        public readonly string Name;
        public readonly int UnstableFactor;
        public readonly Role Role;
        public readonly HarborIslandKind Kind;
        public readonly bool IsEpic;

        public SailTarget(string terrainId, int level, Biome biome, string seaName, string name, int unstableFactor)
            : this(terrainId, terrainId, terrainId, level, biome, seaName, name, unstableFactor)
        {
        }

        public SailTarget(string terrainId, string regionTemplateId, string archipelagoTemplateId, int level, Biome biome, string seaName, string name, int unstableFactor)
            : this(terrainId, regionTemplateId, archipelagoTemplateId, level, biome, seaName, name, unstableFactor, Role.Risky, HarborIslandKind.Unstable)
        {
        }

        public SailTarget(string terrainId, string regionTemplateId, string archipelagoTemplateId, int level, Biome biome, string seaName, string name, int unstableFactor, Role role, HarborIslandKind kind)
            : this(terrainId, regionTemplateId, archipelagoTemplateId, level, biome, seaName, name, unstableFactor, role, kind, false)
        {
        }

        public SailTarget(string terrainId, string regionTemplateId, string archipelagoTemplateId, int level, Biome biome, string seaName, string name, int unstableFactor, Role role, HarborIslandKind kind, bool isEpic)
        {
            TerrainId = terrainId;
            RegionTemplateId = regionTemplateId;
            ArchipelagoTemplateId = archipelagoTemplateId;
            Level = level;
            Biome = biome;
            SeaName = seaName;
            Name = kind == HarborIslandKind.Unstable
                ? UnstableIslandDatabase.IslandName(terrainId, name)
                : name;
            UnstableFactor = unstableFactor;
            Role = role;
            Kind = kind;
            IsEpic = isEpic;
        }

        public string RegionId { get { return (Kind == HarborIslandKind.Tamed ? "tamed|" : "harbor|") + TerrainId + "|" + Level; } }
        public string ArchipelagoId { get { return (Kind == HarborIslandKind.Tamed ? "tamed_arch|" : "harbor_arch|") + TerrainId + "|" + Level; } }
        public string SaveKey { get { return (Kind == HarborIslandKind.Tamed ? "tamed." : string.Empty) + TerrainId + "." + Level; } }
    }

    internal static class HarborRoutes
    {
        // All terrain packages present in the Original client's Resources/offline/terrains directory.
        internal static readonly SailTarget[] Targets = new SailTarget[]
        {
            new SailTarget("pe10gr_1", "pe10gr_1", "pe10gr_1", 10, Biome.Grassland, "Tamed Islands", "Tamed Grassland Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("pe10gr_2", "pe10gr_2", "pe10gr_2", 10, Biome.Grassland, "Tamed Islands", "Tamed Grassland Island 2", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("pe10gr_3", "pe10gr_3", "pe10gr_3", 10, Biome.Grassland, "Tamed Islands", "Tamed Grassland Island 3", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("pe10gr_4", "pe10gr_4", "pe10gr_4", 10, Biome.Grassland, "Tamed Islands", "Tamed Grassland Island 4", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("pe10gr_5", "pe10gr_5", "pe10gr_5", 10, Biome.Grassland, "Tamed Islands", "Tamed Grassland Island 5", 0, Role.Personal, HarborIslandKind.Tamed),
            // The 24 sailing-map entries from the Original route catalog.
            new SailTarget("ri15sa", "ri15sa190710", "15Sa", 15, Biome.Grassland, "Savanna Sea", "Savanna Island Lv. 15", 1),
            new SailTarget("ri18tr", "ri18tr_01_01", "18Tr_epic_01", 18, Biome.TropicalForest, "Tropical Sea", "Tropical Island Lv. 18", 1),
            new SailTarget("ri20te", "ri20te190710", "20Te", 20, Biome.TemperateForest, "Temperate Sea", "Temperate Island Lv. 20", 1),
            new SailTarget("ri25tr", "ri25tr190710", "25Tr", 25, Biome.TropicalForest, "Tropical Sea", "Tropical Island Lv. 25", 1),
            new SailTarget("ri30tu", "ri30tu171228", "30TuT01", 30, Biome.Tundra, "Tundra Sea", "Tundra Island Lv. 30", 1),
            new SailTarget("ri35te", "ri35te171228", "35TeT01", 35, Biome.TemperateForest, "Temperate Sea", "Temperate Island Lv. 35", 1),
            new SailTarget("ri35de", "ri35de171228", "35DeT01", 35, Biome.Desert, "Desert Sea", "Desert Island Lv. 35", 1),
            new SailTarget("ri40tu", "ri40tu171228", "40TuT01", 40, Biome.Tundra, "Tundra Sea", "Tundra Island Lv. 40", 1),
            new SailTarget("ri40tr", "ri40tr171228", "40TrT01", 40, Biome.TropicalForest, "Tropical Sea", "Tropical Island Lv. 40", 1),
            new SailTarget("ri45sa", "ri45sa171228", "45SaT01", 45, Biome.Grassland, "Savanna Sea", "Savanna Island Lv. 45", 1),
            new SailTarget("ri45sw", "ri45sw171228", "45SwT01", 45, Biome.SwampMud, "Swamp Sea", "Swamp Island Lv. 45", 1),
            new SailTarget("ri50sn", "ri50sn171228", "50SnT01", 50, Biome.SnowField, "Snowfield Sea", "Snowfield Island Lv. 50", 1),
            new SailTarget("ri50de", "ri50de171228", "50DeT01", 50, Biome.Desert, "Desert Sea", "Desert Island Lv. 50", 1),
            new SailTarget("ri55tu", "ri55tu171228", "55TuT01", 55, Biome.Tundra, "Tundra Sea", "Tundra Island Lv. 55", 1),
            new SailTarget("ri55tr", "ri55tb171228", "55TrT01", 55, Biome.TropicalForest, "Tropical Blue Sea", "Blue Tropical Island Lv. 55", 1),
            new SailTarget("ri55sw", "ri55sw171228", "55SwT01", 55, Biome.SwampMud, "Swamp Sea", "Swamp Island Lv. 55", 1),
            new SailTarget("ua60tu", "ua60tuMain01", "60TuT01", 60, Biome.Tundra, "Tundra Sea", "Tundra Island Lv. 60", 1),
            new SailTarget("ua60sn", "ua60snMain03", "60SnT01", 60, Biome.SnowField, "Snowfield Sea", "Snowfield Island Lv. 60", 1),
            new SailTarget("ua60vol", "ua60vol_01_01", "60VoS01", 60, Biome.Volcanic, "Volcano Sea", "Volcanic Island Lv. 60", 1),
            new SailTarget("ua60sw", "ua60swMain05", "60SwT01", 60, Biome.SwampMud, "Swamp Sea", "Swamp Island Lv. 60", 1),
            new SailTarget("ua60de", "ua60deMain01", "60DeT01", 60, Biome.Desert, "Desert Sea", "Desert Island Lv. 60", 1),
            new SailTarget("ua60tr", "ua60trMain01", "60TrT01", 60, Biome.TropicalForest, "Tropical Sea", "Tropical Island Lv. 60", 1),
            new SailTarget("op60te", "op60te171228", "op60te171228", 60, Biome.TemperateForest, "Temperate Savage Island Sea", "Temperate Savage Island Lv. 60", 1, Role.Outpost, HarborIslandKind.Unstable, false),
            new SailTarget("op60tr", "op60tr171228", "op60tr171228", 60, Biome.TropicalForest, "Tropical Savage Island Sea", "Tropical Savage Island Lv. 60", 1, Role.Outpost, HarborIslandKind.Unstable, false),

            // The offline personal-region selector also exposes every remaining
            // packaged terrain. Keep these as separate Personal destinations even
            // when the same physical terrain is available through Unstable sailing.
            // These terrain package ids are not RegionTemplate keys. Keep the
            // Personal destination identity, but use the template declared by
            // each packaged terrain so UI systems can resolve its metadata.
            new SailTarget("ra60sw", "ra60sw180226", "ra60sw", 10, Biome.SwampMud, "Tamed Islands", "Tamed Swamp Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ri35de", "ri35deSub01", "ri35de", 10, Biome.Desert, "Tamed Islands", "Tamed Desert Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ri35te", "ri35teSub01", "ri35te", 10, Biome.TemperateForest, "Tamed Islands", "Tamed Temperate Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ri40tr", "ri40trSub01", "ri40tr", 10, Biome.TropicalForest, "Tamed Islands", "Tamed Tropical Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ri45sa", "ri45saSub01", "ri45sa", 10, Biome.Grassland, "Tamed Islands", "Tamed Savanna Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ri50sn", "ri50snSub01", "ri50sn", 10, Biome.SnowField, "Tamed Islands", "Tamed Snowfield Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ri55tu", "ri55tuSub01", "ri55tu", 10, Biome.Tundra, "Tamed Islands", "Tamed Tundra Island", 0, Role.Personal, HarborIslandKind.Tamed),
            new SailTarget("ua60vol", "ua60vol_06_03", "ua60vol", 10, Biome.Volcanic, "Tamed Islands", "Tamed Volcanic Island", 0, Role.Personal, HarborIslandKind.Tamed)
        };

        public static Routes MakeRoutes()
        {
            Dictionary<Role, Dictionary<string, List<Route>>> groupedByRole = new Dictionary<Role, Dictionary<string, List<Route>>>();
            List<ArchipelagoRoute> archipelagoRoutes = new List<ArchipelagoRoute>();

            for (int i = 0; i < Targets.Length; i++)
            {
                SailTarget target = Targets[i];
                if (target.Kind != HarborIslandKind.Unstable)
                {
                    continue;
                }
                Route route = new Route { RegionId = target.RegionId, Price = null };
                Dictionary<string, List<Route>> grouped;
                if (!groupedByRole.TryGetValue(target.Role, out grouped))
                {
                    grouped = new Dictionary<string, List<Route>>();
                    groupedByRole[target.Role] = grouped;
                }
                List<Route> list;
                if (!grouped.TryGetValue(target.RegionTemplateId, out list))
                {
                    list = new List<Route>();
                    grouped[target.RegionTemplateId] = list;
                }
                list.Add(route);
                if (target.Role == Role.Outpost)
                {
                    continue;
                }
                archipelagoRoutes.Add(new ArchipelagoRoute
                {
                    Level = target.Level,
                    Biome = target.Biome,
                    UnstableFactor = target.UnstableFactor,
                    ArchipelagoId = target.ArchipelagoId,
                    IncludedRoutes = new Route[] { route },
                    PrerequisiteQuest = null,
                    IsEpic = target.IsEpic,
                    EpicRegionId = target.IsEpic ? target.RegionId : null,
                    EpicWarpSiloRegionId = null
                });
            }

            Dictionary<Role, Dictionary<string, Route[]>> routesByRole = new Dictionary<Role, Dictionary<string, Route[]>>();
            foreach (KeyValuePair<Role, Dictionary<string, List<Route>>> rolePair in groupedByRole)
            {
                Dictionary<string, Route[]> routes = new Dictionary<string, Route[]>();
                foreach (KeyValuePair<string, List<Route>> pair in rolePair.Value)
                {
                    routes[pair.Key] = pair.Value.ToArray();
                }
                routesByRole[rolePair.Key] = routes;
            }
            return new Routes { _Routes = routesByRole, ArchipelagoRoutes = archipelagoRoutes.ToArray() };
        }

        public static bool TryMakeRegion(string regionId, out Messages.Region region)
        {
            SailTarget target = FindByRegionId(regionId);
            if (target == null)
            {
                region = default(Messages.Region);
                return false;
            }
            region = new Messages.Region
            {
                Id = target.RegionId,
                TerrainId = target.TerrainId,
                TemplateId = target.RegionTemplateId,
                Role = target.Role,
                Name = target.Name,
                CreatedAt = Now()
            };
            return true;
        }

        public static bool TryMakeArchipelago(string id, out Messages.Archipelago archipelago)
        {
            SailTarget target = FindByArchipelagoId(id);
            if (target == null)
            {
                archipelago = default(Messages.Archipelago);
                return false;
            }
            archipelago = new Messages.Archipelago
            {
                Id = target.ArchipelagoId,
                TemplateId = target.ArchipelagoTemplateId,
                UnstableFactor = target.UnstableFactor,
                Name = target.Name + " Route",
                ExpiresAt = Now() + 31536000.0,
                IncludedRegions = new ArchipelagoRegionInfo[]
                {
                    new ArchipelagoRegionInfo { Id = target.RegionId, Progess = 0, CoOpList = new RegionCoOpTodo[0] }
                }
            };
            return true;
        }

        public static bool TryGetTravelTarget(string regionId, out SailTarget target)
        {
            target = FindByRegionId(regionId);
            return target != null;
        }

        public static bool IsKnownRegionTemplate(string templateId)
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].RegionTemplateId == templateId) return true;
            }
            return false;
        }

        public static SailTarget[] GetTargetsForSea(string seaName)
        {
            List<SailTarget> result = new List<SailTarget>();
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].SeaName == seaName)
                {
                    result.Add(Targets[i]);
                }
            }
            return result.ToArray();
        }

        public static SailTarget[] GetTargetsForKind(HarborIslandKind kind)
        {
            List<SailTarget> result = new List<SailTarget>();
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].Kind == kind) result.Add(Targets[i]);
            }
            return result.ToArray();
        }

        public static SailTarget FindFirstTamedTarget()
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].Kind == HarborIslandKind.Tamed) return Targets[i];
            }
            return null;
        }

        public static SailTarget FindTamedTarget(string terrainId)
        {
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].Kind == HarborIslandKind.Tamed && Targets[i].TerrainId == terrainId) return Targets[i];
            }
            return null;
        }

        public static SailTarget FindBySaveKey(string saveKey)
        {
            if (string.IsNullOrEmpty(saveKey)) return null;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].SaveKey == saveKey) return Targets[i];
            }
            return null;
        }

        public static string[] GetSeaNames()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].Kind == HarborIslandKind.Unstable && !names.Contains(Targets[i].SeaName))
                {
                    names.Add(Targets[i].SeaName);
                }
            }
            return names.ToArray();
        }

        public static SailTarget FindByRegionId(string id)
        {
            for (int i = 0; i < Targets.Length; i++) if (Targets[i].RegionId == id) return Targets[i];
            return null;
        }

        public static SailTarget FindForWorld(Durango.Offline.World world)
        {
            if (world == null) return null;

            // Several physical terrain packages can now be either Personal or
            // Unstable. The active Harbor save key is authoritative when present.
            SailTarget activeTarget = HarborRuntime.GetCurrentTarget(world);
            if (activeTarget != null) return activeTarget;

            string templateId = world.TerrainInfo == null
                ? null
                : world.TerrainInfo.region_template;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].RegionTemplateId == templateId ||
                    Targets[i].TerrainId == templateId)
                {
                    return Targets[i];
                }
            }

            Durango.Offline.WorldContext context =
                HarborRuntime.GetWorldContext(world);
            string terrainId = context == null ? null : context.TerrainId;
            for (int i = 0; i < Targets.Length; i++)
            {
                if (Targets[i].TerrainId == terrainId) return Targets[i];
            }
            return null;
        }

        private static SailTarget FindByArchipelagoId(string id)
        {
            for (int i = 0; i < Targets.Length; i++) if (Targets[i].ArchipelagoId == id) return Targets[i];
            return null;
        }

        private static double Now()
        {
            return (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1)).TotalSeconds;
        }
    }
}
