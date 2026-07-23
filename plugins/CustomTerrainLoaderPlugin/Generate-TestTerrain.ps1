param(
    [string]$OutPath = "D:\ProgramData\Durango_Ver_PC_Final\Durango_Original\BepInEx\custom-terrains\baox_test_1.bytes",
    [string]$PreviewPath = "D:\ProgramData\Durango_Ver_PC_Final\Durango_Original\BepInEx\custom-terrains\baox_test_1_preview.png",
    [int]$Width = 256,
    [int]$Height = 256
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Add-ZipEntryBytes {
    param(
        [System.IO.Compression.ZipArchive]$Zip,
        [string]$Name,
        [byte[]]$Bytes
    )

    $entry = $Zip.CreateEntry($Name, [System.IO.Compression.CompressionLevel]::Optimal)
    $stream = $entry.Open()
    try {
        $stream.Write($Bytes, 0, $Bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
}

function New-ByteArray {
    param([int]$Length)
    return [byte[]]::new($Length)
}

$terrainDir = Split-Path -Parent $OutPath
New-Item -ItemType Directory -Force -Path $terrainDir | Out-Null

if (Test-Path -LiteralPath $OutPath) {
    $backupPath = "$OutPath.bak_$(Get-Date -Format yyyyMMdd_HHmmss)"
    Copy-Item -LiteralPath $OutPath -Destination $backupPath
    Write-Host "Backup: $backupPath"
}

$biomeGrassland = [byte]5
$biomeSandBeach = [byte]10
$biomeWarmOcean = [byte]12

$biomes = New-ByteArray ($Width * $Height)
$oceanWidth = $Width + 1
$oceanHeight = $Height + 1
$ocean = New-ByteArray ($oceanWidth * $oceanHeight)
$rivers = New-ByteArray ($oceanWidth * $oceanHeight * 3)

$centerX = $Width / 2.0
$centerY = $Height / 2.0
$radiusX = $Width * 0.36
$radiusY = $Height * 0.30
$grassEdge = 0.70
$beachEdge = 0.86
$waterStart = 0.72
$deepWaterAt = 1.02

for ($y = 0; $y -lt $Height; $y++) {
    for ($x = 0; $x -lt $Width; $x++) {
        $nx = ($x - $centerX) / $radiusX
        $ny = ($y - $centerY) / $radiusY
        $edgeNoise = 0.08 * [Math]::Sin($x * 0.13) + 0.05 * [Math]::Cos($y * 0.17) + 0.04 * [Math]::Sin(($x + $y) * 0.08)
        $d = ($nx * $nx) + ($ny * $ny) + $edgeNoise

        $idx = ($y * $Width) + $x
        if ($d -lt $grassEdge) {
            $biomes[$idx] = $biomeGrassland
        }
        elseif ($d -lt $beachEdge) {
            $biomes[$idx] = $biomeSandBeach
        }
        else {
            $biomes[$idx] = $biomeWarmOcean
        }
    }
}

for ($y = 0; $y -lt $oceanHeight; $y++) {
    for ($x = 0; $x -lt $oceanWidth; $x++) {
        $nx = ($x - $centerX) / $radiusX
        $ny = ($y - $centerY) / $radiusY
        $edgeNoise = 0.08 * [Math]::Sin($x * 0.13) + 0.05 * [Math]::Cos($y * 0.17) + 0.04 * [Math]::Sin(($x + $y) * 0.08)
        $d = ($nx * $nx) + ($ny * $ny) + $edgeNoise

        $idx = ($y * $oceanWidth) + $x
        if ($d -lt $waterStart) {
            $ocean[$idx] = 0
        }
        else {
            $t = ($d - $waterStart) / ($deepWaterAt - $waterStart)
            if ($t -lt 0) {
                $t = 0
            }
            elseif ($t -gt 1) {
                $t = 1
            }

            # Push shallow water into medium depth faster, otherwise it becomes a dark green strip in-game.
            $depth = [int](18 + (109 * [Math]::Pow($t, 0.55)))
            if ($depth -gt 127) {
                $depth = 127
            }
            $ocean[$idx] = [byte]$depth
        }
    }
}

$info = @"
{
   "tile_count": [
      $Width,
      $Height
   ],
   "lake_biome": "grassland",
   "ocean_biome": "warm_ocean",
   "river_biome": "temperate_forest",
   "color_set": "grassland",
   "region_template": "pe10gr_1",
   "tile_set": "grassland",
   "entry_points": [
      [128,128]
   ],
   "indicators": []
}
"@

$utf8 = [System.Text.UTF8Encoding]::new($false)
$infoBytes = $utf8.GetBytes($info)

$fileStream = [System.IO.File]::Open($OutPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
try {
    $zip = [System.IO.Compression.ZipArchive]::new($fileStream, [System.IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        Add-ZipEntryBytes -Zip $zip -Name "whole.biomes" -Bytes $biomes
        Add-ZipEntryBytes -Zip $zip -Name "whole.ocean" -Bytes $ocean
        Add-ZipEntryBytes -Zip $zip -Name "whole.rivers" -Bytes $rivers
        Add-ZipEntryBytes -Zip $zip -Name "info.yml" -Bytes $infoBytes
    }
    finally {
        $zip.Dispose()
    }
}
finally {
    $fileStream.Dispose()
}

try {
    Add-Type -AssemblyName System.Drawing
    $bmp = [System.Drawing.Bitmap]::new($Width, $Height)
    for ($y = 0; $y -lt $Height; $y++) {
        for ($x = 0; $x -lt $Width; $x++) {
            $b = [int]$biomes[($y * $Width) + $x]
            $depth = [int]$ocean[($y * $oceanWidth) + $x]
            if ($depth -gt 0) {
                $t = $depth / 127.0
                $r = [int](82 - (45 * $t))
                $g = [int](174 - (70 * $t))
                $bl = [int](190 + (42 * $t))
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($r, $g, $bl))
            }
            elseif ($b -eq 10) {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(228, 207, 132))
            }
            else {
                $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(83, 165, 83))
            }
        }
    }
    $bmp.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Preview: $PreviewPath"
}
catch {
    Write-Warning "Preview skipped: $($_.Exception.Message)"
}

Write-Host "Generated: $OutPath"
Write-Host "Entries: whole.biomes=$($biomes.Length), whole.ocean=$($ocean.Length), whole.rivers=$($rivers.Length), info.yml=$($infoBytes.Length)"
