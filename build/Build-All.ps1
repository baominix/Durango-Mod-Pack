param(
    [string]$GameRoot = "D:\ProgramData\Durango_Ver_PC_Final\Durango_Original",
    [string]$ProjectRoot = $null
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$pluginsRoot = Join-Path $ProjectRoot "plugins"
if (-not (Test-Path -LiteralPath $pluginsRoot)) {
    throw "Plugins folder not found: $pluginsRoot"
}

$plugins = Get-ChildItem -LiteralPath $pluginsRoot -Directory
foreach ($plugin in $plugins) {
    & (Join-Path $PSScriptRoot "Build-Plugin.ps1") -PluginName $plugin.Name -GameRoot $GameRoot -ProjectRoot $ProjectRoot
}
