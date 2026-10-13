<#
.SYNOPSIS
    Install the pinned static-data toolchain (Luban + protoc) under tools/vendor.

.DESCRIPTION
    Stage 02 needs two external CLIs that are NOT committed to the repository
    (they are large and binary, but fully reproducible from a pinned URL +
    SHA-256):

      * Luban 5.1.0  - CSV/XML schema -> generated C# readers + .bin/.bytes
      * protoc 33.6  - proto/app.proto -> generated C# protobuf (App.cs)

    This script downloads each archive to artifacts/tools/, verifies its
    SHA-256 against tools/versions.lock.json ("vendoredTools"), and extracts it
    into tools/vendor/<name>/<version>/. It is idempotent: when the expected
    executable is already present and reports the pinned version nothing is
    downloaded, so a second run works offline.

    Everything this script writes (artifacts/tools/ and tools/vendor/) is
    git-ignored. tools/gen.ps1, tools/validate-data.ps1 and
    tools/verify-generated.ps1 call this script automatically, so a fresh clone
    bootstraps with exactly one network round-trip.

    Portability note: this must run on the machine's Windows PowerShell 5.1,
    whose Microsoft.PowerShell.Utility module may be older than 4.0 and lack
    Get-FileHash. Hashing therefore goes through
    System.Security.Cryptography.SHA256 and archives are unpacked with 7-Zip
    (System.IO.Compression is the .zip fallback).

    Requirements:
      * PowerShell 5.1+ (Windows PowerShell or pwsh)
      * .NET 8 (or newer) runtime  - Luban ships as a framework-dependent
        net8.0 app; `dotnet --list-runtimes` must list Microsoft.NETCore.App 8.x+
      * 7-Zip to unpack the Luban .7z release archive.

.PARAMETER Force
    Re-download and re-extract even when the installed version already matches.

.PARAMETER SkipLuban
    Install protoc only.

.PARAMETER SkipProtoc
    Install Luban only.

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.PARAMETER NoWrite
    Do not write report files under artifacts/.

.EXAMPLE
    pwsh -NoProfile -File tools/setup-tools.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/setup-tools.ps1 -Force

.NOTES
    Exit codes (same contract as tools/doctor.ps1 and tools/test.ps1):
      0 = every requested tool is installed
      1 = a tool could not be installed
      2 = the script itself could not run (missing lock file, bad JSON)
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [switch]$SkipLuban,
    [switch]$SkipProtoc,
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:LockFile = Join-Path $PSScriptRoot 'versions.lock.json'
$script:VendorDir = Join-Path $PSScriptRoot 'vendor'
$script:DownloadDir = Join-Path $script:Root 'artifacts\tools'
$script:Results = New-Object System.Collections.ArrayList

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Status,   # PASS | FAIL | SKIP
        [string]$Detail = ''
    )
    [void]$script:Results.Add([pscustomobject]@{ Name = $Name; Status = $Status; Detail = $Detail })
}

function Write-Artifact {
    param([string]$Name, [string]$Content)
    if ($NoWrite) { return }
    try {
        if (-not (Test-Path -LiteralPath $script:ArtifactDir)) {
            [void](New-Item -ItemType Directory -Path $script:ArtifactDir -Force)
        }
        Set-Content -LiteralPath (Join-Path $script:ArtifactDir $Name) -Value $Content -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write {0}: {1}" -f $Name, $_.Exception.Message) -ForegroundColor Yellow
    }
}

# SHA-256 of a file, as an upper-case hex string. Avoids Get-FileHash, which is
# missing from some Windows PowerShell installations (see the header note).
function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try {
            $hash = $sha.ComputeHash($stream)
        }
        finally {
            $stream.Dispose()
        }
        return ([System.BitConverter]::ToString($hash)).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Get-SevenZip {
    $candidates = @(
        (Join-Path ${env:ProgramFiles} '7-Zip\7z.exe'),
        (Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe'),
        'C:\Program Files\7-Zip\7z.exe'
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
    foreach ($name in @('7z.exe', '7z')) {
        try {
            $c = Get-Command $name -ErrorAction SilentlyContinue
            if ($null -ne $c) { return $c.Source }
        }
        catch { }
    }
    return $null
}

function Expand-ArchiveFile {
    param(
        [Parameter(Mandatory)][string]$ArchivePath,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][string]$ArchiveType
    )

    if (Test-Path -LiteralPath $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }
    [void](New-Item -ItemType Directory -Path $Destination -Force)

    $sevenZip = Get-SevenZip
    if ($sevenZip) {
        & $sevenZip 'x' '-y' ("-o" + $Destination) $ArchivePath | Out-Null
        if ($LASTEXITCODE -ne 0) { throw ("7-Zip failed (exit {0}) extracting {1}" -f $LASTEXITCODE, $ArchivePath) }
        return
    }

    if ($ArchiveType -eq 'zip') {
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        [System.IO.Compression.ZipFile]::ExtractToDirectory($ArchivePath, $Destination)
        return
    }

    throw ("Cannot extract '{0}': it is a .7z archive and 7-Zip was not found. " +
        "Install 7-Zip (https://www.7-zip.org/) and re-run." -f $ArchivePath)
}

function Test-Installed {
    param($Tool)
    $probe = Join-Path $script:VendorDir ($Tool.name + '\' + $Tool.version + '\' + $Tool.probeFile)
    if (-not (Test-Path -LiteralPath $probe)) { return $false }
    if (-not $Tool.probeArgument) { return $true }
    # The exit code is deliberately NOT part of the probe: Luban prints its
    # banner (including the version) on stderr and still exits non-zero for
    # `--version`. Matching the reported version string in the combined output
    # is the reliable check; a different installed build reports a different one.
    $output = & $probe $Tool.probeArgument 2>&1 | Out-String
    return $output.Contains([string]$Tool.probeExpect)
}

function Install-Tool {
    param($Tool)

    if (-not $Force -and (Test-Installed -Tool $Tool)) {
        Add-Result ("{0} {1}" -f $Tool.name, $Tool.version) 'PASS' 'already installed'
        return
    }

    if (-not (Test-Path -LiteralPath $script:DownloadDir)) {
        [void](New-Item -ItemType Directory -Path $script:DownloadDir -Force)
    }

    $archiveName = [System.IO.Path]::GetFileName(([Uri]$Tool.url).AbsolutePath)
    $archivePath = Join-Path $script:DownloadDir $archiveName
    $expected = ([string]$Tool.sha256).ToUpperInvariant()

    $cached = $false
    if (Test-Path -LiteralPath $archivePath) {
        $cached = ((Get-Sha256 -Path $archivePath) -eq $expected)
    }

    if (-not $cached) {
        Write-Host ("  downloading {0} {1} <- {2}" -f $Tool.name, $Tool.version, $Tool.url)
        try {
            Invoke-WebRequest -Uri $Tool.url -OutFile $archivePath -UseBasicParsing
        }
        catch {
            throw ("download failed: {0}" -f $_.Exception.Message)
        }
    }

    $actual = Get-Sha256 -Path $archivePath
    if ($actual -ne $expected) {
        throw ("checksum mismatch for {0}: expected {1}, got {2}. Delete {3} and retry." -f
            $Tool.name, $expected, $actual, $archivePath)
    }

    $installDir = Join-Path $script:VendorDir ($Tool.name + '\' + $Tool.version)
    Expand-ArchiveFile -ArchivePath $archivePath -Destination $installDir -ArchiveType ([string]$Tool.archiveType)

    if (-not (Test-Installed -Tool $Tool)) {
        throw ("{0} {1} extracted to {2} but '{3} {4}' did not report '{5}'." -f
            $Tool.name, $Tool.version, $installDir, $Tool.probeFile, $Tool.probeArgument, $Tool.probeExpect)
    }

    $shaShort = $expected.Substring(0, 12)
    Add-Result ("{0} {1}" -f $Tool.name, $Tool.version) 'PASS' ("installed; sha256 {0}..." -f $shaShort)
}

# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------
$script:ArtifactDir = Join-Path $script:Root 'artifacts\tools'

if (-not (Test-Path -LiteralPath $script:LockFile)) {
    Write-Host ("ERROR: {0} not found" -f $script:LockFile) -ForegroundColor Red
    exit 2
}

try {
    $lock = Get-Content -LiteralPath $script:LockFile -Raw | ConvertFrom-Json
}
catch {
    Write-Host ("ERROR: tools/versions.lock.json is not valid JSON: {0}" -f $_.Exception.Message) -ForegroundColor Red
    exit 2
}

$selected = @()
foreach ($tool in @($lock.vendoredTools)) {
    if (-not $tool) { continue }
    if ($SkipLuban -and $tool.name -eq 'luban') { continue }
    if ($SkipProtoc -and $tool.name -eq 'protoc') { continue }
    $selected += $tool
}

if ($selected.Count -eq 0) {
    Write-Host 'ERROR: no vendoredTools entries selected in tools/versions.lock.json' -ForegroundColor Red
    exit 2
}

foreach ($tool in $selected) {
    try {
        Install-Tool -Tool $tool
    }
    catch {
        Add-Result ("{0} {1}" -f $tool.name, $tool.version) 'FAIL' $_.Exception.Message
    }
}

$failed = @($script:Results | Where-Object { $_.Status -eq 'FAIL' })
$exitCode = 0
if ($failed.Count -gt 0) { $exitCode = 1 }

if (-not $Json) {
    Write-Host ''
    Write-Host 'ProjectSurvive - static data toolchain (Stage 02)' -ForegroundColor Cyan
    Write-Host ('=' * 72)
    foreach ($r in $script:Results) {
        $color = if ($r.Status -eq 'PASS') { 'Green' } else { 'Red' }
        Write-Host ('  [{0}] {1,-16} {2}' -f $r.Status, $r.Name, $r.Detail) -ForegroundColor $color
    }
    Write-Host ('=' * 72)
    Write-Host ("  PASS {0}  |  FAIL {1}" -f @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count, $failed.Count)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (toolchain installed)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (a tool could not be installed)' -ForegroundColor Red
    }
    Write-Host ''
}

$report = [pscustomobject]@{
    tool      = 'setup-tools.ps1'
    stage     = '02'
    root      = $script:Root
    vendorDir = $script:VendorDir
    exitCode  = $exitCode
    summary   = [pscustomobject]@{
        pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count
        fail = $failed.Count
    }
    checks    = @($script:Results)
}

if ($Json) { $report | ConvertTo-Json -Depth 6 }
if (-not $NoWrite) {
    Write-Artifact 'setup-tools-report.json' ($report | ConvertTo-Json -Depth 6)
}

exit $exitCode
