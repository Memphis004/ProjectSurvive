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
    Development (default) or Release.

.PARAMETER OutputDirectory
    Where the player is written; relative paths resolve against the repo root.
    Default: UnityProject/Builds/Web

.PARAMETER NoWrite
    Do not write the report under artifacts/.

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-web.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Development', 'Release')]
    [string]$Configuration = 'Development',
    [string]$OutputDirectory = 'UnityProject/Builds/Web',
    [switch]$Json,
    [switch]$NoWrite
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
$method = if ($Configuration -eq 'Release') {
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

Write-Host ("  running: Unity.exe {0}" -f ($arguments -join ' ')) -ForegroundColor DarkGray
$process = Start-Process -FilePath $editorPath -ArgumentList $arguments -Wait -PassThru -NoNewWindow
$exitCode = $process.ExitCode

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
$wasm = if (Test-Path -LiteralPath $OutputDirectory) {
    Get-ChildItem -LiteralPath $OutputDirectory -Recurse -Filter '*.wasm' -ErrorAction SilentlyContinue
} else { @() }

$artifactOk = (Test-Path -LiteralPath $indexHtml) -and ($wasm.Count -gt 0)
Add-Check 'player output' $(if ($artifactOk) { 'PASS' } else { 'FAIL' }) "index.html + $($wasm.Count) wasm file(s) in $OutputDirectory"

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
