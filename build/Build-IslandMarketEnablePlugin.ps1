$ErrorActionPreference = "Stop"

$modPackRoot = Split-Path -Parent $PSScriptRoot
$pluginsDir = Join-Path $modPackRoot "plugins"
$SourcePath = Join-Path $pluginsDir "IslandMarketEnablePlugin\IslandMarketEnablePlugin.cs"
$pluginDir = Join-Path $modPackRoot "build-output"
if (-not (Test-Path $pluginDir)) { New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null }
$outDll = Join-Path $pluginDir "IslandMarketEnablePlugin.dll"
$csc = "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"

foreach ($path in @($pluginDir, $SourcePath, $csc)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required path not found: $path"
    }
}

$refsDir = Join-Path $modPackRoot "refs"
$refs = @(
    (Join-Path $refsDir "BepInEx.dll"),
    (Join-Path $refsDir "0Harmony.dll"),
    (Join-Path $refsDir "Assembly-CSharp.dll"),
    (Join-Path $refsDir "UnityEngine.dll"),
    (Join-Path $refsDir "UnityEngine.CoreModule.dll")
)

foreach ($ref in $refs) {
    if (-not (Test-Path -LiteralPath $ref)) {
        throw "Required reference not found: $ref"
    }
}

$args = @(
    "/target:library",
    "/optimize+",
    "/nologo",
    "/out:$outDll"
)

foreach ($ref in $refs) {
    $args += "/reference:$ref"
}

$args += $SourcePath

& $csc @args
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE"
}
Get-Item -LiteralPath $outDll | Select-Object FullName, Length, LastWriteTime