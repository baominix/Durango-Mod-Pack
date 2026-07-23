# Harbor Sailing Map Plugin (Original)

Restores the Harbor/Port interaction and offline map travel backend that are absent from the Original PC assembly.

## Features

- Adds `Set Sail` and `Map` actions when touching a dock (`EntityType 7001`).
- Shows every dock as an explored Harbor marker on the mini map and world map.
- Supplies route, region, archipelago, and travel packets to the offline client.
- Restores the standard Sail detail flow with valid region templates and discovery-info replies.
- Bypasses the live-service Tamed Island Pioneer requirement for restored Harbor routes only.
- Shows the restored Savanna Lv.15 crater and its three real indigenous animal entries immediately.
- Restores all 24 Original sailing-map destinations from Lv.15 through Lv.60,
  including missing tundra, swamp, desert, snowfield and Savage Island routes.
- Uses Island Map Restoration aliases when a route's physical terrain package is
  absent from the Original PC client.
- Keeps the home map and each destination in separate snapshot files beside the normal offline world save.
- Spawns one wooden dock in the nearest suitable water to the terrain entry point on every map.
- Creates and restores automatic docks at full durability (`min=0`, `max=1`, `cur=1`).
- Reuses a nearby existing dock instead of creating a duplicate.

## Build

Run `tools\durango-mod-original\Build-HarborSailingMapPlugin.ps1` while `Durango.exe` is closed.

Runtime settings are written to `BepInEx\config\com.baox.durango.original.harborsailingmap.cfg`.

Harbor snapshots use names such as `0.harbor.home` and `0.harbor.ri45sa.45`. They intentionally do not end in `.world`, so the Original offline server does not list them as additional characters.
