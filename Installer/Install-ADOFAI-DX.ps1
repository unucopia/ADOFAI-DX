param(
    [string]$GamePath = ''
)

$ErrorActionPreference = 'Stop'
$installerDir = $PSScriptRoot
$packageDir = Join-Path $installerDir 'ADOFAI_DX'
$packageInfoPath = Join-Path $packageDir 'Info.json'
$packageDll = Join-Path $packageDir 'ADOFAIDX.dll'

if (-not (Test-Path -LiteralPath $packageInfoPath)) {
    throw 'ADOFAI_DX\Info.json was not found beside this installer.'
}
if (-not (Test-Path -LiteralPath $packageDll)) {
    throw 'ADOFAI_DX\ADOFAIDX.dll was not found beside this installer.'
}

$packageInfo = Get-Content -LiteralPath $packageInfoPath -Raw |
    ConvertFrom-Json
$packageVersion = [string]$packageInfo.Version

function Test-AdofaiGamePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    $exePath = Join-Path $Path 'A Dance of Fire and Ice.exe'
    $managedPath = Join-Path $Path 'A Dance of Fire and Ice_Data\Managed\Assembly-CSharp.dll'
    return (Test-Path -LiteralPath $exePath -PathType Leaf) -and
           (Test-Path -LiteralPath $managedPath -PathType Leaf)
}

function Add-GameCandidate {
    param(
        [System.Collections.Generic.List[string]]$Candidates,
        [string]$LibraryRoot
    )

    if ([string]::IsNullOrWhiteSpace($LibraryRoot)) {
        return
    }

    $candidate = Join-Path $LibraryRoot 'steamapps\common\A Dance of Fire and Ice'
    if (-not $Candidates.Contains($candidate)) {
        $Candidates.Add($candidate)
    }
}

if (Get-Process -Name 'A Dance of Fire and Ice' -ErrorAction SilentlyContinue) {
    Write-Host 'Close A Dance of Fire and Ice completely, then run this installer again.' -ForegroundColor Yellow
    exit 1
}

$gameDir = ''
if (-not [string]::IsNullOrWhiteSpace($GamePath)) {
    if (-not (Test-AdofaiGamePath $GamePath)) {
        throw ('The supplied game path is not a valid ADOFAI installation: ' + $GamePath)
    }
    $gameDir = [System.IO.Path]::GetFullPath($GamePath)
}
else {
    $candidates = [System.Collections.Generic.List[string]]::new()
    Add-GameCandidate $candidates $env:ADOFAI_GAME_PATH

    $steamRegistrations = @(
        @{ Key = 'HKCU:\Software\Valve\Steam'; Value = 'SteamPath' },
        @{ Key = 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam'; Value = 'InstallPath' },
        @{ Key = 'HKLM:\SOFTWARE\Valve\Steam'; Value = 'InstallPath' }
    )
    foreach ($registration in $steamRegistrations) {
        try {
            $steamRoot = Get-ItemPropertyValue `
                -LiteralPath $registration.Key `
                -Name $registration.Value `
                -ErrorAction Stop
            Add-GameCandidate $candidates ([string]$steamRoot)
        }
        catch {
            # Steam may store its path in only one registry location.
        }
    }

    $steamLibraries = [System.Collections.Generic.List[string]]::new()
    foreach ($candidate in @($candidates)) {
        $steamRoot = Split-Path -Parent (Split-Path -Parent $candidate)
        if (-not $steamLibraries.Contains($steamRoot)) {
            $steamLibraries.Add($steamRoot)
        }
    }
    foreach ($registration in $steamRegistrations) {
        try {
            $steamRoot = Get-ItemPropertyValue `
                -LiteralPath $registration.Key `
                -Name $registration.Value `
                -ErrorAction Stop
            if (-not $steamLibraries.Contains([string]$steamRoot)) {
                $steamLibraries.Add([string]$steamRoot)
            }
        }
        catch {
            # Ignore absent registry entries.
        }
    }

    foreach ($steamRoot in $steamLibraries) {
        $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) {
            continue
        }

        $libraryText = Get-Content -LiteralPath $libraryFile -Raw
        $matches = [regex]::Matches(
            $libraryText,
            '"path"\s*"([^"]+)"')
        foreach ($match in $matches) {
            $libraryRoot = $match.Groups[1].Value.Replace('\\', '\')
            Add-GameCandidate $candidates $libraryRoot
        }
    }

    foreach ($drive in Get-PSDrive -PSProvider FileSystem) {
        if ($drive.Root -notmatch '^[A-Za-z]:\\$') {
            continue
        }
        Add-GameCandidate $candidates (Join-Path $drive.Root 'SteamLibrary')
        Add-GameCandidate $candidates (Join-Path $drive.Root 'Steam')
        Add-GameCandidate $candidates (Join-Path $drive.Root 'Program Files (x86)\Steam')
    }

    foreach ($candidate in $candidates) {
        if (Test-AdofaiGamePath $candidate) {
            $gameDir = [System.IO.Path]::GetFullPath($candidate)
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($gameDir)) {
        Write-Host 'ADOFAI was not found automatically.' -ForegroundColor Yellow
        $enteredPath = Read-Host 'Enter the ADOFAI game folder path'
        if (-not (Test-AdofaiGamePath $enteredPath)) {
            throw 'The entered path is not a valid ADOFAI installation.'
        }
        $gameDir = [System.IO.Path]::GetFullPath($enteredPath)
    }
}

$modsDir = Join-Path $gameDir 'Mods'
$targetDir = Join-Path $modsDir 'ADOFAI_DX'
New-Item -ItemType Directory -Path $modsDir -Force | Out-Null

$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$backupDir = Join-Path $gameDir ('_ADOFAI_DX_Backup_' + $stamp)
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

if (Test-Path -LiteralPath $targetDir) {
    Copy-Item `
        -LiteralPath $targetDir `
        -Destination (Join-Path $backupDir 'ADOFAI_DX') `
        -Recurse `
        -Force
}

$paramsPath = Join-Path $gameDir 'A Dance of Fire and Ice_Data\Managed\UnityModManager\Params.xml'
if (Test-Path -LiteralPath $paramsPath -PathType Leaf) {
    Copy-Item `
        -LiteralPath $paramsPath `
        -Destination (Join-Path $backupDir 'Params.xml') `
        -Force
}

$removedLegacy = [System.Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

$legacyExactFiles = @(
    'CPerfect.dll',
    'CPerfect.pdb',
    'ADOFAIDX.pdb'
)
foreach ($fileName in $legacyExactFiles) {
    $legacyPath = Join-Path $targetDir $fileName
    if (Test-Path -LiteralPath $legacyPath -PathType Leaf) {
        Remove-Item -LiteralPath $legacyPath -Force
        $removedLegacy.Add($fileName)
    }
}

$legacyCacheFiles = Get-ChildItem `
    -LiteralPath $targetDir `
    -File `
    -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Directory.FullName -eq $targetDir -and
        ($_.Name -like 'CPerfect.dll.*.cache' -or
         $_.Name -like 'ADOFAIDX.dll.*.cache')
    }
foreach ($cacheFile in $legacyCacheFiles) {
    Remove-Item -LiteralPath $cacheFile.FullName -Force
    $removedLegacy.Add($cacheFile.Name)
}

Copy-Item `
    -Path (Join-Path $packageDir '*') `
    -Destination $targetDir `
    -Recurse `
    -Force

Get-ChildItem -LiteralPath $targetDir -Recurse -File |
    Unblock-File

$targetDll = Join-Path $targetDir 'ADOFAIDX.dll'
$targetInfoPath = Join-Path $targetDir 'Info.json'
if (-not (Test-Path -LiteralPath $targetDll)) {
    throw 'ADOFAIDX.dll is missing after installation.'
}
if (-not (Test-Path -LiteralPath $targetInfoPath)) {
    throw 'Info.json is missing after installation.'
}

$sourceHash = (Get-FileHash -LiteralPath $packageDll -Algorithm SHA256).Hash
$targetHash = (Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash
if ($sourceHash -ne $targetHash) {
    throw 'ADOFAIDX.dll integrity check failed.'
}

$targetInfo = Get-Content -LiteralPath $targetInfoPath -Raw |
    ConvertFrom-Json
if ([string]$targetInfo.Version -ne $packageVersion) {
    throw 'Info.json version does not match the installer package.'
}

Write-Host ''
Write-Host ('ADOFAI DX ' + $packageVersion + ' installed.') -ForegroundColor Green
Write-Host ('Game: ' + $gameDir)
Write-Host ('ADOFAIDX.dll SHA256: ' + $targetHash)
if ($removedLegacy.Count -gt 0) {
    Write-Host ('Removed obsolete files: ' + ($removedLegacy -join ', '))
}
else {
    Write-Host 'No obsolete files needed cleanup.'
}

$ummPath = Join-Path $gameDir 'A Dance of Fire and Ice_Data\Managed\UnityModManager\UnityModManager.dll'
if (Test-Path -LiteralPath $ummPath) {
    Write-Host ('UMM version: ' + (Get-Item -LiteralPath $ummPath).VersionInfo.FileVersion)
}
Write-Host ('Backup: ' + $backupDir)
