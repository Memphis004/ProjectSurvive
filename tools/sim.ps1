#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the headless determinism harness (the fixed tick over the three random streams).

.DESCRIPTION
    Builds lib/Tests~/Sim (Release by default) and runs it:

      pwsh -File tools/sim.ps1
          prints one fingerprint per stream plus the combined determinismDigest.

      pwsh -File tools/sim.ps1 --seed 7 --ticks 900 --trace 20
          the same, with the first 20 ticks echoed and a longer run.

    The output is what tools/test.ps1 compares at process level: the same seed must repeat
    byte-for-byte, a different seed must differ, and the three stream digests must differ from
    each other (AGENTS rule 6 - separate streams; one stream wearing three names would show up
    here as three equal fingerprints).

    The template keeps one mode. A game adds its own (a bot that plays, a load benchmark) to
    lib/Tests~/Sim next to this one, and a switch here.

.PARAMETER Seed
    Run seed (default 20261013).

.PARAMETER Ticks
    How many fixed ticks to produce (default 300).

.PARAMETER TickHz
    Fixed tick rate (default 30 - the battle simulation rate the architecture doc fixes).

.PARAMETER FrameHz
    Synthetic frame rate fed to the ticker (default 60).

.PARAMETER RunIntervalTicks
    How often the run stream draws, in ticks (default 10).

.PARAMETER Trace
    Print the first N ticks.

.PARAMETER Output
    Log path, relative to the repository root (default artifacts/sim-streams.txt).

.PARAMETER Configuration
    dotnet configuration to build and run (default Release).

.EXAMPLE
    pwsh -File tools/sim.ps1 --seed 7
    pwsh -File tools/sim.ps1 --ticks 900 --no-build
#>
[CmdletBinding()]
param(
    [uint32]$Seed = 20261013,

    [int]$Ticks = 300,

    [double]$TickHz = 30,

    [double]$FrameHz = 60,

    [int]$RunIntervalTicks = 10,

    [int]$Trace = 0,

    [string]$Output = '',

    [string]$Configuration = 'Release',

    [Alias('SkipBuild')]
    [switch]$NoBuild,

    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:SlnDir = Split-Path -Parent $PSScriptRoot
$script:SimProject = Join-Path $script:SlnDir 'lib\Tests~\Sim\ProjectSurvive.Sim.csproj'
$script:Artifacts = Join-Path $script:SlnDir 'artifacts'

if ($Output -eq '') { $Output = 'artifacts/sim-streams.txt' }

$simDll = Join-Path $script:SlnDir ("lib\Tests~\Sim\bin\{0}\net9.0\ProjectSurvive.Sim.dll" -f $Configuration)

if (-not $NoBuild) {
    Write-Host "[sim] building $Configuration"
    & dotnet build $script:SimProject -c $Configuration --nologo -v quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "[sim] dotnet build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

if (-not (Test-Path -LiteralPath $simDll)) {
    Write-Error "[sim] $simDll not found - run without --no-build, or check --configuration"
    exit 1
}

$logPath = Join-Path $script:SlnDir $Output
$logDirectory = Split-Path -Parent $logPath
if (-not (Test-Path -LiteralPath $logDirectory)) {
    New-Item -ItemType Directory -Path $logDirectory | Out-Null
}

$simArguments = @(
    'exec', $simDll,
    '--seed', "$Seed",
    '--ticks', "$Ticks",
    '--tick-hz', "$TickHz",
    '--frame-hz', "$FrameHz",
    '--run-interval-ticks', "$RunIntervalTicks")

if ($Trace -gt 0) {
    $simArguments += @('--trace', "$Trace")
}

if ($Quiet) {
    $simArguments += '--quiet'
}

Write-Host "[sim] dotnet $($simArguments -join ' ')"
& dotnet @simArguments 2>&1 | Tee-Object -FilePath $logPath
$code = $LASTEXITCODE

Write-Host "[sim] log=$logPath"
if ($code -eq 0) {
    Write-Host '[sim] PASS (exit 0)'
}
else {
    Write-Host "[sim] FAIL (exit $code)"
}

exit $code
