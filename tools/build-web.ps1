<#
.SYNOPSIS
    ProjectSurvive WebGL build (Stage 03): produce the browser build the
    Playwright smoke test runs against.

.DESCRIPTION
    Runs the locked Unity Editor in batch mode and calls the project's own build
    pipeline, so the artifact built here is the same one a developer gets from
    "ProjectSurvive > Stage 03 > 3. Build Web (Development)":

        Unity.exe -batchmode -quit -projectPath UnityProject
                  -executeMethod ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment

    Why batch mode: a build that only works from a running Editor GUI is not
    reproducible, and a second Editor cannot open a project while one already has
    it. Close the Editor before running this script (the script says so instead of
    hanging).

    Checks:
      1. the locked Editor exists at engine.unity.installPath
      2. the project is not already open (no Unity lock on UnityProject/Temp)
      3. the build exits 0 and wrote index.html plus at least one .wasm
      4. build-info.json (config hash, size, scene list) is present
      5. no `error CS` in the build log

    The build is Development by default. Use -Release for the configuration whose
    timing numbers may be quoted (ADR 0003); a Development build's performance is
    not evidence.

    Exit codes (same contract as tools/doctor.ps1 and tools/test.ps1):
      0 = build succeeded and the output was verified
      1 = the build ran and failed, or the output is incomplete
      2 = the script could not run (missing Editor, project locked, no project)

.PARAMETER Configuration
    Development (default) or Release. The parameter is also reachable as
    -Profile (Stage 03b), because a "build profile" is what the size work calls
    it; both names set the same thing.

    Release is the configuration whose SIZE and TIMING numbers may be quoted
    (ADR 0003). A Development build is bigger (debug symbols, no optimisation)
    and is kept only as the comparison point for the size baseline.

.PARAMETER SizeProfile
    Stage 03b size measurement: baseline | strip-high | optimize-size | minimal.
    Selecting one switches the script to the profile-driven Release entry point
    (WebBuildPipeline.BuildWebForSize), which applies that profile in the same
    process as the build and writes build-report.json next to the player.

    A profile is a value, not Editor state, so the same command reproduces the
    same configuration: the numbers in docs/build/size-baseline.md were produced
    by these four commands, and re-running one is how they are checked.

.PARAMETER OutputDirectory
    Where the player is written; relative paths resolve against the repo root.
    Default: UnityProject/Builds/Web

.PARAMETER ReleaseCandidate
    The release candidate: `-SizeProfile optimize-size` plus `-RestoreProjectSettings`, in one
    command. A profile build persists its settings into ProjectSettings.asset (see
    llmwiki/gotchas/webgl-size-profile-settings-residue.md); restoring them afterwards means the
    RC is reproducible with one command and the working tree is left clean, so the next ordinary
    build cannot silently inherit a profile it never asked for.

.PARAMETER RestoreProjectSettings
    Back up ProjectSettings.asset and GraphicsSettings.asset before the build and put them back
    after it. Only meaningful with -SizeProfile; the artifact still records the settings it was
    built with in build-report.json.

.PARAMETER NoWrite
    Do not write the build report under artifacts/.

.PARAMETER Force
    Skip the confirmation prompt before the build. Required for an unattended
    run: without it the script asks, and a run with no console answers "no".

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Configuration Release
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Profile Release
#>
[CmdletBinding()]
param(
    [Alias('Profile')]
    [ValidateSet('Development', 'Release')]
    [string]$Configuration = 'Development',
    [ValidateSet('', 'baseline', 'strip-high', 'optimize-size', 'minimal')]
    [string]$SizeProfile = '',
    [string]$OutputDirectory = 'UnityProject/Builds/Web',
    [switch]$Json,
    [switch]$NoWrite,
    [switch]$ReleaseCandidate,
    [switch]$RestoreProjectSettings,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$LockFile = Join-Path $PSScriptRoot 'versions.lock.json'
$ProjectPath = Join-Path $RepoRoot 'UnityProject'
$ArtifactsDir = Join-Path $RepoRoot 'artifacts'

if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $RepoRoot $OutputDirectory
}

if ($ReleaseCandidate) {
    if ($SizeProfile -and $SizeProfile -ne 'optimize-size') {
        Write-Host "RESULT: ERROR - -ReleaseCandidate is the optimize-size profile; do not also pass -SizeProfile $SizeProfile" -ForegroundColor Red
        exit 2
    }
    $SizeProfile = 'optimize-size'
    $RestoreProjectSettings = $true
}

$script:Checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param([string]$Name, [string]$Status, [string]$Detail)
    $script:Checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
    $colour = switch ($Status) { 'PASS' { 'Green' } 'FAIL' { 'Red' } 'WARN' { 'Yellow' } default { 'Gray' } }
    Write-Host ("  [{0}] {1,-34} {2}" -f $Status, $Name, $Detail) -ForegroundColor $colour
}

function Fail-Hard {
    param([string]$Message)
    Write-Host ""
    Write-Host "RESULT: ERROR - $Message" -ForegroundColor Red
    exit 2
}

function Confirm-Build {
    if ($Force) { return }
    $yes = Read-Host "Build WebGL player now? (y/N)"
    if ($yes -ne 'y') { exit 2 }
}

# The call sits below the function it names on purpose: PowerShell resolves a function only once
# its definition has run, so a call above the definition fails with "not recognized as the name of
# a cmdlet" - which is how this script broke before Stage 09's build, and why the first build of
# the stage produced no player at all.
Confirm-Build

Write-Host ""
Write-Host "ProjectSurvive - WebGL build (Stage 03)" -ForegroundColor Cyan
Write-Host ("=" * 72)

# ---------------------------------------------------------------------------
# 1. locked Editor
# ---------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $LockFile)) { Fail-Hard "version lock not found at $LockFile" }
$lock = Get-Content -LiteralPath $LockFile -Raw | ConvertFrom-Json
$unityVersion = $lock.engine.unity.version
$editorPath = Join-Path $lock.engine.unity.installPath 'Editor/Unity.exe'
if (-not (Test-Path -LiteralPath $editorPath)) {
    Fail-Hard "Unity $unityVersion not found at $editorPath - install it with Unity Hub, or fix tools/versions.lock.json"
}
Add-Check 'editor' 'PASS' "$unityVersion at $($lock.engine.unity.installPath)"

# ---------------------------------------------------------------------------
# 2. project must not be open elsewhere
# ---------------------------------------------------------------------------
$lockDir = Join-Path $ProjectPath 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lockDir) {
    Fail-Hard "UnityProject is open in another Unity instance (Temp/UnityLockfile exists). Close the Editor and run again - batch mode cannot share a project."
}

if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'))) {
    Fail-Hard "no Unity project at $ProjectPath"
}
Add-Check 'project' 'PASS' $ProjectPath

# ---------------------------------------------------------------------------
# 3. build
# ---------------------------------------------------------------------------
$method = if ($SizeProfile) {
    # A size build is always Release, whatever -Profile says: only a Release build's
    # size is a number about the game rather than about debug symbols.
    $Configuration = 'Release'
    'ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebForSize'
} elseif ($Configuration -eq 'Release') {
    'ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebRelease'
} else {
    'ProjectSurvive.Game.Editor.WebBuildPipeline.BuildWebDevelopment'
}

if (-not (Test-Path -LiteralPath $ArtifactsDir)) { New-Item -ItemType Directory -Path $ArtifactsDir | Out-Null }
$logPath = Join-Path $ArtifactsDir 'unity-build-web.log'
if (Test-Path -LiteralPath $logPath) { Remove-Item -LiteralPath $logPath -Force }

$arguments = @(
    '-batchmode'
    '-quit'
    '-projectPath', $ProjectPath
    '-buildTarget', 'WebGL'
    '-executeMethod', $method
    '-logFile', $logPath
)

if ($SizeProfile) {
    # The pipeline reads these back off the command line, so the profile and the
    # output directory are the build's inputs rather than ambient Editor state.
    $arguments += @('-sizeProfile', $SizeProfile, '-outputDir', $OutputDirectory)
}

Write-Host ("  running: Unity.exe {0}" -f ($arguments -join ' ')) -ForegroundColor DarkGray

# A profile build persists its settings (WebSizeProfiles.ApplyAndPersist runs inside the build
# process). Restoring them here, after Unity has exited and written ProjectSettings, is what
# closes the residue trap: the artifact keeps the configuration in build-report.json, the tree
# keeps the authored one.
$settingsToRestore = @('ProjectSettings/ProjectSettings.asset', 'ProjectSettings/GraphicsSettings.asset')
$settingsBackup = @{}
if ($RestoreProjectSettings -and $SizeProfile) {
    foreach ($relative in $settingsToRestore) {
        $source = Join-Path $ProjectPath $relative
        if (Test-Path -LiteralPath $source) {
            $settingsBackup[$relative] = [System.IO.File]::ReadAllBytes($source)
        }
    }
}

try {
    $process = Start-Process -FilePath $editorPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    $exitCode = $process.ExitCode
}
finally {
    if ($settingsBackup.Count -gt 0) {
        foreach ($relative in $settingsBackup.Keys) {
            [System.IO.File]::WriteAllBytes((Join-Path $ProjectPath $relative), $settingsBackup[$relative])
        }
        Add-Check 'player settings restored' 'PASS' ("{0} file(s) put back after the profile build" -f $settingsBackup.Count)
    }
}

if ($exitCode -ne 0) {
    Add-Check 'unity build' 'FAIL' "exit $exitCode (see $logPath)"
} else {
    Add-Check 'unity build' 'PASS' "exit 0"
}

$logText = if (Test-Path -LiteralPath $logPath) { Get-Content -LiteralPath $logPath -Raw } else { '' }
$compileErrors = ([regex]::Matches($logText, 'error CS\d+')).Count
Add-Check 'C# errors in log' $(if ($compileErrors -eq 0) { 'PASS' } else { 'FAIL' }) "$compileErrors occurrence(s)"

# ---------------------------------------------------------------------------
# 4. artifact
# ---------------------------------------------------------------------------
$indexHtml = Join-Path $OutputDirectory 'index.html'
# @(...) is load bearing: a build writes exactly one .wasm, and under
# Set-StrictMode a single FileInfo has no .Count, so the plain assignment made
# every successful single-wasm build fail its own verification.
$wasm = @()
if (Test-Path -LiteralPath $OutputDirectory) {
    $wasm = @(Get-ChildItem -LiteralPath $OutputDirectory -Recurse -Filter '*.wasm' -ErrorAction SilentlyContinue)
}

$artifactOk = (Test-Path -LiteralPath $indexHtml) -and ($wasm.Count -gt 0)
Add-Check 'player output' $(if ($artifactOk) { 'PASS' } else { 'FAIL' }) "index.html + $($wasm.Count) wasm file(s) in $OutputDirectory"

# --- the settings the build actually ran with (Stage 15) -------------------
# A size figure belongs to a configuration, and a build that ran with a profile's
# leftover settings is not the configuration anybody asked for. Read the report the
# build wrote and compare it with the requested profile; never infer it from the
# file name or from what was applied in memory.
$reportPath = Join-Path $OutputDirectory 'build-report.json'
if (Test-Path -LiteralPath $reportPath) {
    $buildReport = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if (-not $buildReport.PSObject.Properties['sizeSettings']) {
        Add-Check 'build settings match profile' 'FAIL' 'build-report.json records no sizeSettings'
        $buildReport = $null
    }
}

if ($buildReport) {
    $expected = @{
        managedStrippingLevel = if (-not $SizeProfile -or $SizeProfile -eq 'baseline') { 'Low' } else { 'High' }
        il2cppCodeGeneration = if (-not $SizeProfile -or $SizeProfile -in @('baseline', 'strip-high')) { 'OptimizeSpeed' } else { 'OptimizeSize' }
        exceptionSupport      = if ($SizeProfile -eq 'minimal') { 'None' } else { 'ExplicitlyThrownExceptionsOnly' }
    }

    $mismatches = @()
    foreach ($key in $expected.Keys) {
        $actual = $buildReport.sizeSettings.$key
        if ($actual -ne $expected[$key]) {
            $mismatches += ("{0}: expected {1}, built with {2}" -f $key, $expected[$key], $actual)
        }
    }

    $reportedProfile = if ($buildReport.sizeProfile) { $buildReport.sizeProfile } else { 'none' }
    $requestedProfile = if ($SizeProfile) { $SizeProfile } else { 'none' }
    if ($reportedProfile -ne $requestedProfile) {
        $mismatches += ("sizeProfile: requested {0}, report says {1}" -f $requestedProfile, $reportedProfile)
    }

    if ($mismatches.Count -eq 0) {
        Add-Check 'build settings match profile' 'PASS' ("{0}: stripping {1}, codegen {2}, exceptions {3}" -f $reportedProfile, $expected.managedStrippingLevel, $expected.il2cppCodeGeneration, $expected.exceptionSupport)
    } else {
        Add-Check 'build settings match profile' 'FAIL' ($mismatches -join '; ')
        Write-Host "  (a profile's settings were left in ProjectSettings.asset - see llmwiki/gotchas/webgl-size-profile-settings-residue.md)" -ForegroundColor Yellow
    }
} else {
    Add-Check 'build settings match profile' 'FAIL' 'the build wrote no build-report.json, so its configuration is unknown'
}

$buildInfoPath = Join-Path $OutputDirectory 'build-info.json'
$buildInfo = $null
if (Test-Path -LiteralPath $buildInfoPath) {
    $buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
    Add-Check 'build-info.json' 'PASS' "configHash $($buildInfo.configHash.Substring(0,8))... size $($buildInfo.totalSizeBytes) bytes"
} else {
    Add-Check 'build-info.json' 'WARN' 'not written by the pipeline'
}

$requiredFailures = @($script:Checks | Where-Object { $_.status -eq 'FAIL' })
$result = if ($requiredFailures.Count -eq 0 -and $artifactOk) { 'OK' } else { 'FAILED' }

Write-Host ("=" * 72)
$summary = [pscustomobject]@{
    timestamp       = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    unityVersion    = $unityVersion
    configuration   = $Configuration
    sizeProfile     = $SizeProfile
    outputDirectory = $OutputDirectory
    exitCode        = $exitCode
    checks          = $script:Checks
    result          = $result
}

if (-not $NoWrite) {
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ArtifactsDir 'web-build-report.json') -Encoding UTF8
}

Write-Host "  RESULT: $result" -ForegroundColor $(if ($result -eq 'OK') { 'Green' } else { 'Red' })
if ($Json) { $summary | ConvertTo-Json -Depth 5 }

if ($result -eq 'OK') { exit 0 }
exit 1
