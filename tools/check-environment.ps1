param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath,
    [string]$SmapiPath,
    [string]$ModsPath
)

$ErrorActionPreference = 'Stop'
if (-not $SmapiPath) { $SmapiPath = $GamePath }
if (-not $ModsPath) { $ModsPath = Join-Path $GamePath 'Mods' }

function Get-AssemblyProductVersion([string]$Directory, [string]$Name) {
    $path = Join-Path $Directory $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { return $null }
    $value = (Get-Item -LiteralPath $path).VersionInfo.ProductVersion
    if ($value -match '^\d+\.\d+\.\d+(?:\.\d+)?') { return $Matches[0] }
    return $null
}

$gameVersion = Get-AssemblyProductVersion $GamePath 'Stardew Valley.dll'
$smapiVersion = Get-AssemblyProductVersion $SmapiPath 'StardewModdingAPI.dll'
$contentPatcher = @()
if (Test-Path -LiteralPath $ModsPath) {
    foreach ($file in Get-ChildItem -LiteralPath $ModsPath -Filter manifest.json -Recurse -File) {
        try {
            $manifest = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            if ($manifest.UniqueID -eq 'Pathoschild.ContentPatcher') {
                $contentPatcher += [PSCustomObject]@{ Version = $manifest.Version; Manifest = $file.FullName }
            }
        }
        catch { Write-Warning "Could not parse manifest: $($file.FullName)" }
    }
}
$issues = @()
if (-not $gameVersion -or [version]$gameVersion -lt [version]'1.6.15') { $issues += 'Game 1.6.15+ is required for the selected baseline.' }
if (-not $smapiVersion -or [version]$smapiVersion -lt [version]'4.4.0') { $issues += 'SMAPI 4.4.0+ is required.' }
if ($contentPatcher.Count -ne 1) { $issues += 'Expected exactly one installed Content Patcher.' }
elseif ($contentPatcher[0].Version -notmatch '^\d+\.\d+\.\d+$' -or [version]$contentPatcher[0].Version -lt [version]'2.9.0') {
    $issues += 'Content Patcher 2.9.0+ stable is required.'
}

[PSCustomObject]@{
    OS = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    Architecture = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
    GameVersion = $gameVersion
    SmapiReferenceVersion = $smapiVersion
    ContentPatcher = $contentPatcher
    GamePath = $GamePath
    SmapiPath = $SmapiPath
    ModsPath = $ModsPath
    Issues = $issues
    Note = 'Read-only version check; not proof of successful game loading or compatibility.'
} | ConvertTo-Json -Depth 4
if ($issues.Count -gt 0) { exit 1 }
