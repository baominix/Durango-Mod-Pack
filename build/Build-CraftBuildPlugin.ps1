param(
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$builder = Join-Path $PSScriptRoot "Build-Plugin.ps1"
& $builder -PluginName "CraftBuildPlugin" -Clean:$Clean
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
