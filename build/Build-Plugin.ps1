param(
    [Parameter(Mandatory = $true)]
    [string]$PluginName,
    [string]$GameRoot = "D:\ProgramData\Durango_Ver_PC_Final\Durango_Original",
    [string]$ProjectRoot = $null
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

$pluginSourceDir = Join-Path $ProjectRoot ("plugins\" + $PluginName)
$manifestPath = Join-Path $pluginSourceDir "plugin.json"
$artifactDir = Join-Path $ProjectRoot "artifacts"
$pluginDir = Join-Path $GameRoot "BepInEx\plugins"
$coreDir = Join-Path $GameRoot "BepInEx\core"
$managedDir = Join-Path $GameRoot "Durango_Data\Managed"
$csc = "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"

foreach ($path in @($ProjectRoot, $pluginSourceDir, $manifestPath, $GameRoot, $pluginDir, $coreDir, $managedDir, $csc)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required path not found: $path"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$dllName = $manifest.dll
if ([string]::IsNullOrEmpty($dllName)) {
    $dllName = $PluginName + ".dll"
}

New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

$sourceFiles = Get-ChildItem -LiteralPath $pluginSourceDir -Filter "*.cs" -Recurse | Select-Object -ExpandProperty FullName
if ($sourceFiles.Count -eq 0) {
    throw "No .cs files found in $pluginSourceDir"
}

$refs = @(
    (Join-Path $coreDir "BepInEx.dll"),
    (Join-Path $coreDir "0Harmony.dll"),
    (Join-Path $managedDir "Assembly-CSharp.dll"),
    (Join-Path $managedDir "UnityEngine.dll"),
    (Join-Path $managedDir "UnityEngine.CoreModule.dll")
)

foreach ($ref in $refs) {
    if (-not (Test-Path -LiteralPath $ref)) {
        throw "Required reference not found: $ref"
    }
}

$stagingDll = Join-Path $artifactDir ($PluginName + ".staging.dll")
$outDll = Join-Path $artifactDir $dllName
$targetDll = Join-Path $pluginDir $dllName

$args = @(
    "/target:library",
    "/optimize+",
    "/nologo",
    "/out:$stagingDll"
)

foreach ($ref in $refs) {
    $args += "/reference:$ref"
}

foreach ($source in $sourceFiles) {
    $args += $source
}

& $csc @args
if ($LASTEXITCODE -ne 0) {
    throw "Plugin build failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $stagingDll -Destination $outDll -Force

$lastError = $null
for ($i = 0; $i -lt 10; $i++) {
    try {
        Copy-Item -LiteralPath $outDll -Destination $targetDll -Force
        $lastError = $null
        break
    } catch {
        $lastError = $_
        Start-Sleep -Milliseconds 500
    }
}

if ($lastError -ne $null) {
    throw "Plugin built, but could not replace $targetDll. Close Durango and run this script again. $($lastError.Exception.Message)"
}

Get-Item -LiteralPath $targetDll | Select-Object FullName, Length, LastWriteTime
