# Island Map Restoration Plugin (Original)

Restores the physical terrain aliases missing from the Original PC client and
attaches the correct region simulation template to each route.

The plugin covers the early/mid-level maps, all normal Lv.60 biome families,
and the Temperate/Tropical Savage Island maps. Existing packaged terrain data is
reused only for geometry; each restored route keeps its own terrain id, region
template, Harbor snapshot and save identity.

This plugin is the terrain layer used by `HarborSailingMapPlugin`, which supplies
the 24 sailing destinations shown by the Original route map.

Build and install while `Durango.exe` is closed:

```powershell
.\tools\durango-mod-original\Build-IslandMapRestorationPlugin.ps1
.\tools\durango-mod-original\Build-HarborSailingMapPlugin.ps1
```

Configuration is created at
`BepInEx/config/com.baox.durango.original.islandmaprestoration.cfg`.
