param(
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$modPackRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$pluginsRoot = Join-Path $modPackRoot "plugins"
$outputRoot = Join-Path $modPackRoot "build-output"
$builder = Join-Path $PSScriptRoot "Build-Plugin.ps1"

if (-not (Test-Path -LiteralPath $pluginsRoot -PathType Container)) {
    throw "Plugin folder not found: $pluginsRoot"
}
if (-not (Test-Path -LiteralPath $builder -PathType Leaf)) {
    throw "Build-Plugin.ps1 not found: $builder"
}
if (-not (Test-Path -LiteralPath $outputRoot)) {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

if ($Clean) {
    Write-Host "Cleaning generated plugin outputs..." -ForegroundColor Yellow
    Get-ChildItem -LiteralPath $outputRoot -File -Filter "*.dll" -ErrorAction SilentlyContinue |
        Remove-Item -Force

    foreach ($generatedData in @("ReferenceData", "MapEditorPlugin")) {
        $path = Join-Path $outputRoot $generatedData
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

$plugins = @(
    Get-ChildItem -LiteralPath $pluginsRoot -Directory |
        Sort-Object Name |
        Select-Object -ExpandProperty Name
)

if ($plugins.Count -eq 0) {
    throw "No plugin source folders found in: $pluginsRoot"
}

$success = New-Object System.Collections.Generic.List[string]
$failed = New-Object System.Collections.Generic.List[string]

Write-Host "Durango Mod Pack build" -ForegroundColor Cyan
Write-Host "Root    : $modPackRoot"
Write-Host "Plugins : $($plugins.Count)"
Write-Host "Output  : $outputRoot"
Write-Host ""

foreach ($plugin in $plugins) {
    try {
        & $builder -PluginName $plugin
        [void]$success.Add($plugin)
    }
    catch {
        [void]$failed.Add($plugin)
        Write-Host "FAILED: $plugin" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor DarkGray
Write-Host "Build complete" -ForegroundColor Cyan
Write-Host "Success: $($success.Count)" -ForegroundColor Green
Write-Host "Failed : $($failed.Count)" -ForegroundColor $(if ($failed.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Output : $outputRoot"

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed plugins:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    exit 1
}

exit 0
