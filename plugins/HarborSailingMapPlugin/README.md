# Harbor Sailing Map Plugin (Original)

Restores the Harbor/Port interaction and offline map travel backend that are absent from the Original PC assembly.

## Features

- Restores the original Harbor actions when touching a dock (`EntityType 7001`):
  - `Set Sail` uses the game's original sailing route map and lists only
    unstable-island routes.
  - `To Tamed Island` opens the personal island selected by
    `TamedIslandRestorationPlugin`.
  - `Return to Exploring` appears when a previous unstable-island snapshot exists.
- Shows every dock as an explored Harbor marker on the mini map and world map.
- Supplies route, region, archipelago, and travel packets to the offline client.
- Restores the standard Sail detail flow with valid region templates and discovery-info replies.
- Bypasses the live-service Tamed Island Pioneer requirement for restored Harbor routes only.
- Shows the real indigenous-animal entries for every restored unstable route.
  The list is read from the route's original `region_templates.herds` data;
  its `closed_crater_herd_type` is shown as the Crater entry and is not counted
  as an indigenous animal.
- Restores all 24 Original sailing-map destinations from Lv.15 through Lv.60,
  including missing tundra, swamp, desert, snowfield and Savage Island routes.
- Uses Island Map Restoration aliases when a route's physical terrain package is
  absent from the Original PC client.
- Keeps the home map, personal island and each unstable destination in separate
  snapshot files beside the normal offline world save.
- Remembers the most recent unstable-island snapshot when traveling to the
  personal island, so `Return to Exploring` resumes that island.
- Reports restored unstable worlds with their real region role, route level,
  sea and generated island name in the world-map header.
- Keeps the archived Unstable Islands table (crater/resources and indigenous
  animals) in `data/unstable_islands_fandom.json`. The runtime route-name index
  is in `UnstableIslandDatabase.cs`.
- Spawns one wooden dock in the nearest suitable water to the terrain entry point on every map.
- Creates and restores automatic docks at full durability (`min=0`, `max=1`, `cur=1`).
- Reuses a nearby existing dock instead of creating a duplicate.

## Build

Run `tools\durango-mod-original\Build-HarborSailingMapPlugin.ps1` while `Durango.exe` is closed.

Runtime settings are written to `BepInEx\config\com.baominix.durango.original.harborsailingmap.cfg`.

Harbor snapshots use names such as `0.harbor.home` and `0.harbor.ri45sa.45`. They intentionally do not end in `.world`, so the Original offline server does not list them as additional characters.
