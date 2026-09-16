<#
.SYNOPSIS
    Downloads every PixelLab-generated asset listed in assets.json into the Unity
    project's Art folders.

.DESCRIPTION
    The manifest stores permanent PixelLab UUIDs, so this script is reproducible:
    a fresh clone plus one run gets you the same art the project was built with.
    Nothing here generates art — regenerating is a deliberate act done through the
    PixelLab MCP tools, after which you update the id in assets.json.

    Downloads use curl.exe rather than Invoke-WebRequest on purpose. The CDN in
    front of PixelLab rejects some default user agents, and Invoke-WebRequest
    tries to open an interactive credential prompt on a 4xx, which hangs
    non-interactive shells.

.PARAMETER Force
    Re-download assets whose destination folder already exists.

.PARAMETER Only
    Slugs to fetch, e.g. -Only warden,vulcanor. Default is everything.

.EXAMPLE
    .\fetch_assets.ps1
    .\fetch_assets.ps1 -Only vulcanor -Force
#>
[CmdletBinding()]
param(
    # Resolved in the body: $PSScriptRoot is not yet populated while parameter
    # defaults are evaluated under Windows PowerShell 5.1.
    [string]   $Manifest,
    [switch]   $Force,
    [string[]] $Only,
    [switch]   $SkipCharacters,
    [switch]   $SkipTiles,
    [switch]   $SkipItems,
    [int]      $MaxWaitSeconds = 600,

    # Terrain tiles are snapped to the environment palette after download; see
    # Invoke-PaletteSnap below for why. Pass -NoPaletteSnap to keep the raw art.
    [switch]   $NoPaletteSnap,
    [string]   $PythonExe
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $Manifest) { $Manifest = Join-Path $PSScriptRoot 'assets.json' }

# tools/pixellab/ -> tools/ -> project root
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$cfg         = Get-Content $Manifest -Raw | ConvertFrom-Json
$artRoot     = Join-Path $projectRoot $cfg.art_root
$tempRoot    = Join-Path $env:TEMP ("emberdepths_fetch_" + [guid]::NewGuid().ToString('N').Substring(0, 8))

New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

$results = New-Object System.Collections.ArrayList

function Test-Wanted {
    param([string]$Slug)
    if (-not $Only) { return $true }
    return $Only -contains $Slug
}

<#
    Fetches a URL to disk, tolerating PixelLab's 423 "asset is locked while a job
    runs against it" response. That happens whenever an animation is still
    generating for a character, so polling is the normal path, not an error path.
#>
function Invoke-Download {
    param([string]$Url, [string]$OutFile, [string]$Label)

    $deadline = (Get-Date).AddSeconds($MaxWaitSeconds)
    $attempt  = 0

    while ($true) {
        $attempt++
        $code = & curl.exe -sSL --retry 2 --retry-delay 2 -w '%{http_code}' -o $OutFile $Url

        if ($code -eq '200') { return $true }

        if ($code -eq '423') {
            if ((Get-Date) -gt $deadline) {
                Write-Warning "$Label still generating after $MaxWaitSeconds s - skipped."
                return $false
            }
            Write-Host ("  {0}: still generating, waiting 15s (attempt {1})" -f $Label, $attempt) -ForegroundColor DarkYellow
            Start-Sleep -Seconds 15
            continue
        }

        Write-Warning "$Label failed with HTTP $code"
        return $false
    }
}

function Get-CharacterAsset {
    param($Entry)

    $dest = Join-Path $artRoot $Entry.dest

    if ((Test-Path $dest) -and -not $Force) {
        Write-Host ("  {0,-18} already present (use -Force to refresh)" -f $Entry.slug) -ForegroundColor DarkGray
        [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'skipped'; Files = 0 })
        return
    }

    $zip = Join-Path $tempRoot ($Entry.slug + '.zip')
    $url = "{0}/characters/{1}/download" -f $cfg.api_base, $Entry.id

    Write-Host ("  {0,-18} downloading..." -f $Entry.slug) -ForegroundColor Cyan
    if (-not (Invoke-Download -Url $url -OutFile $zip -Label $Entry.slug)) {
        [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'FAILED'; Files = 0 })
        return
    }

    # The endpoint returns JSON on error with a 200 in some edge cases, so verify
    # the payload really is a zip before trashing an existing folder with it.
    $isZip = $false
    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
        $isZip = $true
        $archive.Dispose()
    } catch { $isZip = $false }

    if (-not $isZip) {
        Write-Warning ("{0}: response was not a zip archive - left untouched." -f $Entry.slug)
        [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'FAILED'; Files = 0 })
        return
    }

    $staging = Join-Path $tempRoot ($Entry.slug + '_x')
    Expand-Archive -Path $zip -DestinationPath $staging -Force

    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $dest | Out-Null

    # Flatten one level if the zip wrapped everything in a single root folder.
    $top = Get-ChildItem $staging
    $source = $staging
    if ($top.Count -eq 1 -and $top[0].PSIsContainer) { $source = $top[0].FullName }

    Copy-Item (Join-Path $source '*') -Destination $dest -Recurse -Force

    $count = (Get-ChildItem $dest -Recurse -File -Filter *.png | Measure-Object).Count
    Write-Host ("  {0,-18} {1} png files -> {2}" -f $Entry.slug, $count, $Entry.dest) -ForegroundColor Green
    [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'ok'; Files = $count })
}

<#
    Tiles and map objects are both single PNGs behind different endpoints, so
    one function handles both and the caller supplies the route.
#>
function Get-SingleImageAsset {
    param($Entry, [string]$Route)

    $destDir = Join-Path $artRoot $Entry.dest
    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    $destFile = Join-Path $destDir ($Entry.slug + '.png')

    if ((Test-Path $destFile) -and -not $Force) {
        Write-Host ("  {0,-18} already present" -f $Entry.slug) -ForegroundColor DarkGray
        [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'skipped'; Files = 0 })
        return
    }

    $tmp = Join-Path $tempRoot ($Entry.slug + '.png')
    $url = "{0}/{1}/{2}/download" -f $cfg.api_base, $Route, $Entry.id

    if (-not (Invoke-Download -Url $url -OutFile $tmp -Label $Entry.slug)) {
        [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'FAILED'; Files = 0 })
        return
    }

    # PNG magic number. Guards against saving an error page as a .png.
    $head = [System.IO.File]::ReadAllBytes($tmp)[0..3]
    if (-not ($head[0] -eq 0x89 -and $head[1] -eq 0x50 -and $head[2] -eq 0x4E -and $head[3] -eq 0x47)) {
        Write-Warning ("{0}: response was not a PNG - left untouched." -f $Entry.slug)
        [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'FAILED'; Files = 0 })
        return
    }

    Move-Item $tmp $destFile -Force
    Write-Host ("  {0,-18} -> {1}/{0}.png" -f $Entry.slug, $Entry.dest) -ForegroundColor Green
    [void]$results.Add([pscustomobject]@{ Asset = $Entry.slug; Status = 'ok'; Files = 1 })
}

<#
    Snaps the downloaded terrain tiles onto the game's environment palette.

    Two things this fixes, neither of which prompt engineering solved reliably:

      * Generated "volcanic rock" comes back with a cool blue-grey cast. The
        environment palette omits the cool STEEL and PLAYER ramps entirely, so
        terrain is forced warm and the cool half stays reserved for the party.
      * The tiles and the Blender-rendered props otherwise sit next to each
        other in visibly different colour spaces.

    Runs on any Python 3; Blender ships one, so no separate install is needed.
#>
function Invoke-PaletteSnap {
    $script = Join-Path $projectRoot 'tools\blender\quantise.py'
    $tiles = Join-Path $artRoot 'Tiles'

    if (-not (Test-Path $script)) {
        Write-Warning "quantise.py not found; tiles left un-snapped."
        return
    }

    $python = $PythonExe
    if (-not $python) {
        # Blender's bundled interpreter is checked first and on purpose. A bare
        # "python" on PATH is very often the Windows Store alias stub, which
        # exists as a command, prints an advert, and exits non-zero — so every
        # candidate is actually run before it is trusted.
        $candidates = @()
        $candidates += Get-ChildItem 'C:\Program Files\Blender Foundation' -Recurse -Depth 4 `
            -Filter 'python.exe' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
        $candidates += @('python3', 'python')

        foreach ($candidate in $candidates) {
            if (-not $candidate) { continue }
            try {
                $version = & $candidate --version 2>$null
                if ($LASTEXITCODE -eq 0 -and "$version" -match 'Python 3') {
                    $python = $candidate
                    break
                }
            } catch { }
        }
    }

    if (-not $python) {
        Write-Warning "No working Python 3 found; tiles left un-snapped. Pass -PythonExe or use -NoPaletteSnap."
        return
    }

    Write-Host "Palette snap" -ForegroundColor White
    & $python $script $tiles
    Write-Host ''
}

try {
    Write-Host "EmberDepths asset fetch" -ForegroundColor White
    Write-Host ("project : {0}" -f $projectRoot)
    Write-Host ("art root: {0}" -f $artRoot)
    Write-Host ''

    if (-not $SkipCharacters) {
        Write-Host 'Characters' -ForegroundColor White
        foreach ($c in $cfg.characters) {
            if (Test-Wanted $c.slug) { Get-CharacterAsset $c }
        }
        Write-Host ''
    }

    if (-not $SkipTiles) {
        Write-Host 'Isometric tiles' -ForegroundColor White
        foreach ($t in $cfg.isometric_tiles) {
            if (Test-Wanted $t.slug) { Get-SingleImageAsset $t 'isometric-tile' }
        }
        Write-Host ''

        # Only terrain is snapped. Item icons keep their own colours; see the
        # note in assets.json.
        if (-not $NoPaletteSnap) { Invoke-PaletteSnap }
    }

    if (-not $SkipItems -and $cfg.map_objects) {
        Write-Host 'Item icons' -ForegroundColor White
        foreach ($o in $cfg.map_objects) {
            if (Test-Wanted $o.slug) { Get-SingleImageAsset $o 'map-objects' }
        }
        Write-Host ''
    }

    Write-Host 'Summary' -ForegroundColor White
    $results | Format-Table -AutoSize

    $failed = @($results | Where-Object { $_.Status -eq 'FAILED' })
    if ($failed.Count -gt 0) {
        Write-Warning ("{0} asset(s) failed. Re-run to retry; 423 means a PixelLab job is still running." -f $failed.Count)
        exit 1
    }

    Write-Host 'Done. Switch to Unity and let it import, then run EmberDepths/Art/Rebuild Visual Sets.' -ForegroundColor Green
}
finally {
    if (Test-Path $tempRoot) { Remove-Item $tempRoot -Recurse -Force -ErrorAction SilentlyContinue }
}
