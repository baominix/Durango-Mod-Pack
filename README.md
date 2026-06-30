# Durango Mod Pack

Durango Mod Pack is a BepInEx plugin pack for Durango: Wild Lands PC.

## Plugins

- `UISizeOptionsPlugin` - adds `Very large` and `Large` UI size options and keeps the game's original UI size options available.

## Build

Build one plugin:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-Plugin.ps1" -PluginName UISizeOptionsPlugin
```

Build every plugin:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ".\build\Build-All.ps1"
```

The default game path is:

```text
D:\ProgramData\Durango_Ver_PC_Final\Durango_Original
```

Use `-GameRoot` to build for another Durango install.

## Add A Plugin

1. Create a folder under `plugins\<PluginName>`.
2. Add one or more `.cs` files.
3. Add `plugin.json`.
4. Run `build\Build-Plugin.ps1 -PluginName <PluginName>`.

Example `plugin.json`:

```json
{
  "name": "MyPlugin",
  "displayName": "My Plugin",
  "version": "0.1.0",
  "dll": "MyPlugin.dll",
  "description": "Short plugin description."
}
```
