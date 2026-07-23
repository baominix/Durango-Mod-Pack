param(
    [switch]$Deploy
)

$ErrorActionPreference = "Stop"

$modPackRoot = Split-Path -Parent $PSScriptRoot
$pluginsDir = Join-Path $modPackRoot "plugins"
$SourceDir = Join-Path $pluginsDir "DurangoCombatSystemPlugin"
$pluginDir = Join-Path $modPackRoot "build-output"
if (-not (Test-Path $pluginDir)) { New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null }
$outDll = Join-Path $pluginDir "DurangoCombatSystemPlugin.dll"
$csc = "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"
$sources = @(Get-ChildItem -LiteralPath $SourceDir -Filter "*.cs" -File -Recurse |
    Select-Object -ExpandProperty FullName)

foreach ($path in @($pluginDir, $SourceDir, $csc)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required path not found: $path"
    }
}

if ($sources.Count -eq 0) {
    throw "No C# source files found below: $SourceDir"
}

$refsDir = Join-Path $modPackRoot "refs"
$refs = @(
    (Join-Path $refsDir "BepInEx.dll"),
    (Join-Path $refsDir "0Harmony.dll"),
    (Join-Path $refsDir "Assembly-CSharp.dll"),
    (Join-Path $refsDir "ExternalLibrary.dll"),
    (Join-Path $refsDir "UnityEngine.dll"),
    (Join-Path $refsDir "UnityEngine.CoreModule.dll")
)

foreach ($ref in $refs) {
    if (-not (Test-Path -LiteralPath $ref)) {
        throw "Required reference not found: $ref"
    }
}

$args = @("/target:library", "/optimize+", "/nologo", "/out:$outDll")
foreach ($ref in $refs) {
    $args += "/reference:$ref"
}
$args += $sources

& $csc @args
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE"
}

if ($Deploy) {
    Get-Item -LiteralPath $outDll | Select-Object FullName, Length, LastWriteTime
}
else {
    Get-Item -LiteralPath $outDll | Select-Object FullName, Length, LastWriteTime
}