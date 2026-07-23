# Durango Mod Pack

BepInEx source plugin pack for the Original PC client of Durango: Wild Lands.

## Layout

- `plugins/<PluginName>` - C# source for each plugin.
- `refs` - the local .NET/BepInEx/Unity references required by the compiler.
- `build-output` - compiled plugin DLLs.
- `build` - repository-local build scripts.

The repository intentionally does not include the source workspace's `disable`
or `_combat_system_backup` folders.

## Build

Build one plugin entirely inside this repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-GatheringPlugin.ps1"
```

Build every plugin:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1"
```

All DLLs are written to `build-output`. The compiler remains:

```text
C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
```

To additionally copy a build into a local game installation, pass `-Deploy`.
`-GameRoot` can override the default installation path.

## Included restoration groups

- `GatheringPlugin` currently restores Date Palm gathering only and is ready for
  additional gathering resources later.
- `IslandMapRestorationPlugin` supplies missing physical terrain aliases and the
  correct simulation templates for restored islands.
- `HarborSailingMapPlugin` exposes all 24 Original sailing-map destinations and
  uses Island Map Restoration for terrain packages absent from the PC client.
