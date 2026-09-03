param(
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$builder = Join-Path $PSScriptRoot "Build-Plugin.ps1"
& $builder -PluginName "TitleBarMenuDisablePlugin" -Clean:$Clean
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
