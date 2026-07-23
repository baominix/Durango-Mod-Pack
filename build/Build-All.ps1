$ErrorActionPreference = "Stop"

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$scripts = Get-ChildItem -Path $PSScriptRoot -Filter "Build-*.ps1" | Where-Object { $_.Name -ne "Build-All.ps1" }

$failed = 0
$success = 0

foreach ($script in $scripts) {
    Write-Host "Building $($script.Name)..." -ForegroundColor Cyan
    try {
        & $script.FullName
        $success++
    }
    catch {
        Write-Host "Failed to build $($script.Name): $_" -ForegroundColor Red
        $failed++
    }
}

Write-Host "--------------------------------"
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host "Success: $success" -ForegroundColor Green
if ($failed -gt 0) {
    Write-Host "Failed: $failed" -ForegroundColor Red
}
