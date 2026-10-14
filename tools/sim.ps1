#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the headless bot simulation (Stage 07) or the load benchmark.

.DESCRIPTION
    Builds lib/Tests~/Sim (Release by default) and runs it:

      pwsh -File tools/sim.ps1 --seeds 200
          plays 200 seeds through all five floors with the kiting bot and a greedy
          camp policy, writing one row per floor to artifacts/balance.csv.

      pwsh -File tools/sim.ps1 --mode benchmark
          measures the Stage 07 acceptance load (~300 ghosts + ~500 projectiles)
          per tick, writing artifacts/benchmark.csv with the machine it ran on.

    PowerShell binds --seeds to -Seeds (exact names in either dash spelling), so the
    stage prompt's command line works verbatim. The bot is not a test: it is the
    instrument the balance pass reads,
    and it exits non-zero when a run cannot be played to its end (a floor that
    never ends, a flow that cannot leave a state) - a partial CSV that looks
    complete is worse than no CSV.

.PARAMETER Seeds
    How many consecutive seeds to play (default 1).

.PARAMETER Mode
    bot | benchmark (default bot).

.PARAMETER Output
    Output CSV path, relative to the repository root.

.PARAMETER Configuration
    dotnet configuration to build and run (default Release).

.EXAMPLE
    pwsh -File tools/sim.ps1 --seeds 200 --output artifacts/balance.csv
    pwsh -File tools/sim.ps1 --mode benchmark -NoBuild
#>
[CmdletBinding()]
param(
    [string]$Mode = 'bot',

    [int]$Seeds = 1,

    [uint32]$Seed = 20261013,

    [string]$Output = '',

    [int]$Enemies = 300,

    [int]$Projectiles = 500,

    [int]$Ticks = 600,

    [int]$Warmup = 900,

    [int]$MaxSteps = 20000,

    [string]$Data = '',

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

if ($Mode -ne 'bot' -and $Mode -ne 'benchmark') {
    Write-Error "--mode must be bot or benchmark (got '$Mode')"
    exit 1
}

if ($Output -eq '') {
    $Output = if ($Mode -eq 'benchmark') { 'artifacts/benchmark.csv' } else { 'artifacts/balance.csv' }
}

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

if (-not (Test-Path -LiteralPath $script:Artifacts)) {
    New-Item -ItemType Directory -Path $script:Artifacts | Out-Null
}

$simArguments = @('exec', $simDll, '--mode', $Mode, '--seed', "$Seed", '--out', $Output)

if ($Mode -eq 'bot') {
    $simArguments += @('--seeds', "$Seeds", '--max-steps', "$MaxSteps")
}
else {
    $simArguments += @(
        '--enemies', "$Enemies",
        '--projectiles', "$Projectiles",
        '--ticks', "$Ticks",
        '--warmup', "$Warmup")
}

if ($Data -ne '') {
    $simArguments += @('--data', $Data)
}

if ($Quiet) {
    $simArguments += '--quiet'
}

$logPath = Join-Path $script:Artifacts ("sim-{0}.txt" -f $Mode)
Write-Host "[sim] mode=$Mode dotnet $($simArguments -join ' ')"
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
