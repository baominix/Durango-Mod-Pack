param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$PluginName,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

$modPackRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$pluginsRoot = Join-Path $modPackRoot "plugins"
$refsRoot = Join-Path $modPackRoot "refs"
$outputRoot = Join-Path $modPackRoot "build-output"
$sourceDir = Join-Path $pluginsRoot $PluginName

$csc = $env:DURANGO_CSC
if ([string]::IsNullOrWhiteSpace($csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe"
}

if (-not (Test-Path -LiteralPath $sourceDir -PathType Container)) {
    throw "Plugin source folder not found: $sourceDir"
}
if (-not (Test-Path -LiteralPath $refsRoot -PathType Container)) {
    throw "Reference folder not found: $refsRoot"
}
if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) {
    throw ("C# compiler not found: " + $csc + [Environment]::NewLine +
        "Set DURANGO_CSC to a compatible csc.exe if needed.")
}
if (-not (Test-Path -LiteralPath $outputRoot)) {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
}

$outputNames = @{
    "SelectGameMode" = "SelectGameMode.dll"
}

$localDependencies = @{
    "TamedIslandRestorationPlugin" = @("HarborSailingMapPlugin")
}

$outputName = if ($outputNames.ContainsKey($PluginName)) {
    $outputNames[$PluginName]
}
else {
    "$PluginName.dll"
}
$outDll = Join-Path $outputRoot $outputName

if ($Clean -and (Test-Path -LiteralPath $outDll)) {
    Remove-Item -LiteralPath $outDll -Force
}

if ($localDependencies.ContainsKey($PluginName)) {
    foreach ($dependency in $localDependencies[$PluginName]) {
        $dependencyName = if ($outputNames.ContainsKey($dependency)) {
            $outputNames[$dependency]
        }
        else {
            "$dependency.dll"
        }

        $dependencyDll = Join-Path $outputRoot $dependencyName
        if (-not (Test-Path -LiteralPath $dependencyDll -PathType Leaf)) {
            Write-Host "Building dependency: $dependency" -ForegroundColor DarkCyan
            & $PSCommandPath -PluginName $dependency
            if ($LASTEXITCODE -ne 0) {
                throw "Dependency build failed: $dependency"
            }
        }
    }
}

$excludedDirs = @(
    "backup",
    "backups",
    "_backup",
    "_backups",
    "test",
    "tests",
    "disabled"
)

$sources = @(
    Get-ChildItem -LiteralPath $sourceDir -Recurse -File -Filter "*.cs" |
        Where-Object {
            $relative = $_.FullName.Substring($sourceDir.Length).TrimStart('\', '/')
            $parts = @($relative -split '[\\/]')

            $parentParts = @()
            if ($parts.Count -gt 1) {
                $parentParts = @($parts[0..($parts.Count - 2)])
            }

            $excludedByDir = $false
            foreach ($part in $parentParts) {
                if ($excludedDirs -contains $part.ToLowerInvariant()) {
                    $excludedByDir = $true
                    break
                }
            }

            $isBackupFile =
                $_.Name -match '(?i)\.backup(?:[._-]|$)' -or
                $_.Name -match '(?i)\.bak(?:[._-]|$)' -or
                $_.Name -match '(?i)_backup(?:[._-]|$)'

            -not $excludedByDir -and -not $isBackupFile
        } |
        Sort-Object FullName |
        Select-Object -ExpandProperty FullName
)

if ($sources.Count -eq 0) {
    throw "No buildable C# source files found below: $sourceDir"
}

$references = New-Object System.Collections.Generic.List[string]
Get-ChildItem -LiteralPath $refsRoot -File -Filter "*.dll" |
    Sort-Object Name |
    ForEach-Object {
        [void]$references.Add($_.FullName)
    }

if ($localDependencies.ContainsKey($PluginName)) {
    foreach ($dependency in $localDependencies[$PluginName]) {
        $dependencyName = if ($outputNames.ContainsKey($dependency)) {
            $outputNames[$dependency]
        }
        else {
            "$dependency.dll"
        }

        $dependencyDll = Join-Path $outputRoot $dependencyName
        if (-not $references.Contains($dependencyDll)) {
            [void]$references.Add($dependencyDll)
        }
    }
}

if ($references.Count -eq 0) {
    throw "No reference DLLs found in: $refsRoot"
}

$resourceArgs = @()
$embeddedTemp = $null

if ($PluginName -eq "DurangoCombatSystemPlugin") {
    # ReferenceData is a development/build-time snapshot only.
    # The compiled plugin never reads this folder at runtime.
    $referenceData = Join-Path $sourceDir "ReferenceData"

    $animalSourcePath =
        Join-Path $referenceData "entity_types\animal.json"
    $rootMotionSourcePath =
        Join-Path $referenceData "saurus_root_motion.json"
    $playerActionsPath =
        Join-Path $referenceData "player\player_battle_actions.json"
    $triceraFramework =
        Join-Path $referenceData "models\animals\tricera\tricera_framework.asset"
    $phenacodusFramework =
        Join-Path $referenceData "models\animals\phenacodus\Phenacodus_framework.asset"
    $raptorFramework =
        Join-Path $referenceData "models\animals\raptor\Raptor_framework.asset"

    foreach ($requiredPath in @(
        $animalSourcePath,
        $rootMotionSourcePath,
        $playerActionsPath,
        $triceraFramework,
        $phenacodusFramework,
        $raptorFramework
    )) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Combat build-time source data is missing: $requiredPath"
        }
    }

    $legacyReferenceOutput = Join-Path $outputRoot "ReferenceData"
    if (Test-Path -LiteralPath $legacyReferenceOutput) {
        Remove-Item -LiteralPath $legacyReferenceOutput -Recurse -Force
    }

    $embeddedTemp = Join-Path $outputRoot ".combat-embedded-temp"
    if (Test-Path -LiteralPath $embeddedTemp) {
        Remove-Item -LiteralPath $embeddedTemp -Recurse -Force
    }
    New-Item -ItemType Directory -Path $embeddedTemp -Force | Out-Null

    $animalSource =
        Get-Content -LiteralPath $animalSourcePath -Raw |
        ConvertFrom-Json

    $animalSubset = [ordered]@{}
    foreach ($entityTypeId in @("2001", "2027", "2037", "2039")) {
        $property = $animalSource.PSObject.Properties[$entityTypeId]
        if ($null -eq $property) {
            throw "animal.json does not contain required entity type: $entityTypeId"
        }

        $animalSubset[$entityTypeId] = $property.Value
    }

    $animalEmbeddedPath =
        Join-Path $embeddedTemp "animal.embedded.json"

    $animalSubset |
        ConvertTo-Json -Depth 100 -Compress |
        Set-Content -LiteralPath $animalEmbeddedPath -Encoding UTF8

    $motionNames =
        New-Object 'System.Collections.Generic.HashSet[string]' (
            [System.StringComparer]::Ordinal
        )

    foreach ($frameworkPath in @(
        $triceraFramework,
        $phenacodusFramework,
        $raptorFramework
    )) {
        foreach ($line in Get-Content -LiteralPath $frameworkPath) {
            $trimmed = $line.Trim()
            if ($trimmed -match '^(?:motion|front|back|left|right|begin|during|end):\s*(.+)$') {
                [void]$motionNames.Add($Matches[1].Trim())
            }
        }
    }

    $rootSource =
        Get-Content -LiteralPath $rootMotionSourcePath -Raw |
        ConvertFrom-Json

    $rootSubsetClips = [ordered]@{}
    foreach ($property in $rootSource.clips.PSObject.Properties) {
        if ($motionNames.Contains($property.Name)) {
            $rootSubsetClips[$property.Name] = $property.Value
        }
    }

    if ($rootSubsetClips.Count -eq 0) {
        throw "No required Saurus root-motion clips were selected."
    }

    $rootSubset = [ordered]@{
        format = $rootSource.format
        source = $rootSource.source
        clips = $rootSubsetClips
    }

    $rootEmbeddedPath =
        Join-Path $embeddedTemp "saurus_root_motion.embedded.json"

    $rootSubset |
        ConvertTo-Json -Depth 100 -Compress |
        Set-Content -LiteralPath $rootEmbeddedPath -Encoding UTF8

    $resourceArgs +=
        "/resource:$animalEmbeddedPath,DurangoCombat.animal.json"
    $resourceArgs +=
        "/resource:$rootEmbeddedPath,DurangoCombat.saurus_root_motion.json"
    $resourceArgs +=
        "/resource:$playerActionsPath,DurangoCombat.player_battle_actions.json"
    $resourceArgs +=
        "/resource:$triceraFramework,DurangoCombat.framework.tricera"
    $resourceArgs +=
        "/resource:$phenacodusFramework,DurangoCombat.framework.phenacodus"
    $resourceArgs +=
        "/resource:$raptorFramework,DurangoCombat.framework.raptor"

    Write-Host "Embedded animal profiles : 4 (2001, 2027, 2037, 2039)"
    Write-Host "Embedded root clips      : $($rootSubsetClips.Count)"
    Write-Host "Embedded frameworks      : 3 (Tricera, Phenacodus, Raptor)"
}

Write-Host "============================================================" -ForegroundColor DarkGray
Write-Host "Building : $PluginName" -ForegroundColor Cyan
Write-Host "Source   : $sourceDir"
Write-Host "Output   : $outDll"
Write-Host "Sources  : $($sources.Count)"
Write-Host "Refs     : $($references.Count)"
Write-Host "Resources: $($resourceArgs.Count)"
Write-Host "============================================================" -ForegroundColor DarkGray

$args = @(
    "/target:library",
    "/optimize+",
    "/nologo",
    "/out:$outDll"
)

foreach ($ref in $references) {
    $args += "/reference:$ref"
}

$args += $resourceArgs
$args += $sources

& $csc @args
$compileExitCode = $LASTEXITCODE

if ($embeddedTemp -and (Test-Path -LiteralPath $embeddedTemp)) {
    Remove-Item -LiteralPath $embeddedTemp -Recurse -Force
}

if ($compileExitCode -ne 0) {
    throw "Plugin build failed with exit code $compileExitCode : $PluginName"
}

if (-not (Test-Path -LiteralPath $outDll -PathType Leaf)) {
    throw "Compiler reported success but output DLL was not created: $outDll"
}

if ($PluginName -eq "MapEditorPlugin") {
    $catalog = Join-Path $sourceDir "model_catalog.tsv"
    if (Test-Path -LiteralPath $catalog -PathType Leaf) {
        $dataOutput = Join-Path $outputRoot "MapEditorPlugin"
        New-Item -ItemType Directory -Path $dataOutput -Force | Out-Null
        Copy-Item -LiteralPath $catalog -Destination (Join-Path $dataOutput "model_catalog.tsv") -Force
    }
}

$item = Get-Item -LiteralPath $outDll
Write-Host "BUILD SUCCEEDED: $($item.Name)" -ForegroundColor Green
$item | Select-Object FullName, Length, LastWriteTime
