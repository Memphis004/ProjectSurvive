<#
.SYNOPSIS
    ProjectSurvive test harness: the Fast and Full suites over dotnet, Unity and the browser.

.DESCRIPTION
    One entry point for every automated check the project owns, split into two suites so the loop
    between two edits is seconds-to-minutes rather than tens of minutes. Which test lives in which
    suite, and the measured cost of each step, are in docs/testing/test-suites.md; the rule that
    nothing is dropped is kept by that table listing every test, not by this script being short.

    Fast (the default, and what you run while working):
      1. dotnet SDK present
      2. `dotnet build` of every layer, the test project and the Sim
      3. `dotnet test` (xUnit, 425 tests) with a .trx in artifacts/dotnet/
      4. the Sim, at process level: the same seed repeats a fingerprint, a different seed does not,
         the three streams stay separate, and the bot plays two floors
      5. the static data gates: validate-data, verify-generated, check-fixtures
      6. Unity EditMode tests, through the Editor that is already open

    Full (before a Stage's commit, or at a Stage gate) = Fast, then:
      7. Unity PlayMode tests, through the same Editor
      8. a WebGL build (Development; Release only with -Release)
      9. the browser suite: smoke, battle and camp in a real Chrome

    Unity steps need an Editor with this project open (the `unity` CLI's own connection, the same one
    `run_tests` uses). With no Editor connected they report NOT RUN with that reason - they never
    quietly pass. The WebGL build needs the opposite: an Editor holding the project lock cannot build,
    so with the Editor open step 8 reports NOT RUN and says so. Running Fast with the Editor open and
    Full with it closed therefore produces a full verdict across two runs; both are appended to
    artifacts/test-summary.md.

    -Release moves the dotnet build and the WebGL build to Release. It is for a Stage gate or a
    measurement, not for the working loop: a Development build's numbers may not be quoted (ADR 0003).

.PARAMETER Suite
    Fast (default) or Full.

.PARAMETER Release
    Build Release instead of Debug/Development. Off by default.

.PARAMETER Seed
    Seed used by the Sim checks. Default 20261013.

.PARAMETER UnityTimeoutSeconds
    How long one Unity test run may take before it is reported NOT RUN. Default 900.

.PARAMETER WebBuildTimeoutSeconds
    How long the WebGL build may take before it is reported NOT RUN. Default 3600.

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.PARAMETER NoWrite
    Do not write report files under artifacts/.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Suite Full -Release

.NOTES
    Exit codes (same contract as tools/build-web.ps1 and tools/doctor.ps1):
        0 = every check that ran passed (NOT RUN steps are reported, not hidden)
        1 = at least one check FAILED
        2 = the harness itself could not run
#>
[CmdletBinding()]
param(
    [ValidateSet('Fast', 'Full')]
    [string]$Suite = 'Fast',
    [switch]$Release,
    [uint32]$Seed = 20261013,
    [int]$UnityTimeoutSeconds = 900,
    [int]$WebBuildTimeoutSeconds = 3600,
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:SlnDir = Join-Path $script:Root 'lib\Tests~'
$script:Sln = Join-Path $script:SlnDir 'ProjectSurvive.Core.sln'
$script:Configuration = if ($Release) { 'Release' } else { 'Debug' }
$script:WebConfiguration = if ($Release) { 'Release' } else { 'Development' }
$script:SimDll = Join-Path $script:SlnDir ("Sim\bin\{0}\net9.0\ProjectSurvive.Sim.dll" -f $script:Configuration)
$script:ArtifactDir = Join-Path $script:Root 'artifacts'
$script:DotnetDir = Join-Path $script:ArtifactDir 'dotnet'
$script:WebRoot = Join-Path $script:Root 'UnityProject\Builds\Web'
$script:WebPort = 8080
$script:Results = New-Object System.Collections.ArrayList
$script:HarnessError = ''
$script:WebBuildOk = $false

# The steps write logs under artifacts/ as they run, so both directories exist before the first one.
foreach ($dir in @($script:ArtifactDir, $script:DotnetDir)) {
    if (-not (Test-Path -LiteralPath $dir)) {
        [void](New-Item -ItemType Directory -Path $dir -Force)
    }
}

function Add-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$StepSuite,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'NOT RUN', 'WARN')][string]$Status,
        [string]$Detail = '',
        [double]$Seconds = 0
    )
    [void]$script:Results.Add([pscustomobject]@{
            Name    = $Name
            Suite   = $StepSuite
            Status  = $Status
            Detail  = $Detail
            Seconds = [math]::Round($Seconds, 1)
        })

    # Printed as it happens: a Full run is minutes long and a log that only fills at the end cannot
    # tell a slow build from a stuck one.
    $color = 'Gray'
    switch ($Status) {
        'PASS' { $color = 'Green' }
        'FAIL' { $color = 'Red' }
        'WARN' { $color = 'Yellow' }
        'NOT RUN' { $color = 'DarkGray' }
    }
    Write-Host ('  [{0,-7}] {1,-38} {2,7:N1}s  {3}' -f $Status, $Name, [math]::Round($Seconds, 1), $Detail) -ForegroundColor $color
}

# One step: timed, and it says what it found. The block returns @{ Status = ...; Detail = ... }.
function Invoke-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('Fast', 'Full')][string]$StepSuite,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $outcome = $null
    try {
        $outcome = & $Action
    }
    catch {
        $outcome = @{ Status = 'FAIL'; Detail = ('harness error: {0}' -f $_.Exception.Message) }
    }
    $watch.Stop()

    if ($null -eq $outcome) {
        $outcome = @{ Status = 'FAIL'; Detail = 'the step returned nothing' }
    }

    Add-Step -Name $Name -StepSuite $StepSuite -Status $outcome.Status -Detail $outcome.Detail -Seconds $watch.Elapsed.TotalSeconds
    return $outcome
}

# Run a native command, return its exit code and combined output, never throw.
function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [string]$WorkingDirectory = ''
    )
    try {
        if ($WorkingDirectory -and (Test-Path -LiteralPath $WorkingDirectory)) {
            Push-Location -LiteralPath $WorkingDirectory
        }
        $out = & $FilePath @Arguments 2>&1
        $code = $LASTEXITCODE
        return [pscustomobject]@{
            Ok      = ($code -eq 0)
            Code    = $code
            Output  = (($out | Out-String).Trim())
            Missing = $false
        }
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Code = -1; Output = $_.Exception.Message; Missing = $true }
    }
    finally {
        if ($WorkingDirectory -and (Test-Path -LiteralPath $WorkingDirectory)) {
            Pop-Location
        }
    }
}

function Find-CommandPath {
    param([Parameter(Mandatory)][string]$Name)
    try {
        $c = Get-Command $Name -ErrorAction SilentlyContinue
        if ($null -ne $c) { return $c.Source }
    }
    catch { }
    return $null
}

function Write-Artifact {
    param([string]$Directory, [string]$Name, [string]$Content)
    if ($NoWrite) { return }
    try {
        if (-not (Test-Path -LiteralPath $Directory)) {
            [void](New-Item -ItemType Directory -Path $Directory -Force)
        }
        Set-Content -LiteralPath (Join-Path $Directory $Name) -Value $Content -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write {0}: {1}" -f $Name, $_.Exception.Message) -ForegroundColor Yellow
    }
}

$dotnet = Find-CommandPath 'dotnet'
$unity = Find-CommandPath 'unity'
$node = Find-CommandPath 'node'

Write-Host ''
Write-Host ("ProjectSurvive - test suite: {0} ({1})" -f $Suite, $script:Configuration) -ForegroundColor Cyan
Write-Host ('=' * 78)

# ---------------------------------------------------------------------------
# 1. Toolchain
# ---------------------------------------------------------------------------
Invoke-Step 'toolchain: .NET SDK' 'Fast' {
    if (-not $dotnet) {
        return @{ Status = 'FAIL'; Detail = 'dotnet not found on PATH' }
    }

    $r = Invoke-Native $dotnet @('--list-sdks')
    if (-not ($r.Ok -and $r.Output)) {
        return @{ Status = 'FAIL'; Detail = ('dotnet --list-sdks failed: {0}' -f $r.Output) }
    }

    $sdks = @($r.Output -split "`r?`n" | Where-Object { $_ -match '\S' })
    $major = 0
    foreach ($s in $sdks) {
        if ($s -match '^(\d+)\.') { if ([int]$Matches[1] -gt $major) { $major = [int]$Matches[1] } }
    }

    if ($major -lt 8) {
        return @{ Status = 'FAIL'; Detail = ('need an SDK >= 8 for net9.0 tests; found: {0}' -f ($sdks -join '; ')) }
    }

    return @{ Status = 'PASS'; Detail = ($sdks -join '; ') }
} | Out-Null

if (-not (Test-Path -LiteralPath $script:Sln)) {
    Add-Step 'dotnet solution' 'Fast' 'FAIL' ("missing {0}" -f $script:Sln)
    $script:HarnessError = 'the dotnet solution is missing'
}

# ---------------------------------------------------------------------------
# 2-5. dotnet: build, tests, the Sim and the data gates (the previous harness, step for step)
# ---------------------------------------------------------------------------
if ($dotnet -and (Test-Path -LiteralPath $script:Sln)) {
    Invoke-Step 'dotnet build' 'Fast' {
        $build = Invoke-Native $dotnet @('build', $script:Sln, '-c', $script:Configuration, '--nologo', '-v', 'minimal')
        Write-Artifact $script:DotnetDir 'build.log' $build.Output

        $warningLines = @($build.Output -split "`r?`n" | Where-Object { $_ -match ':\s+warning\s' })
        if ($build.Ok -and $warningLines.Count -gt 0) {
            Add-Step -Name 'build warnings' -StepSuite 'Fast' -Status 'WARN' -Detail ("{0} warning line(s); first: {1}" -f $warningLines.Count, $warningLines[0].Trim())
        }

        if (-not $build.Ok) {
            return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/dotnet/build.log" -f $build.Code) }
        }

        return @{ Status = 'PASS'; Detail = ("{0} configuration" -f $script:Configuration) }
    } | Out-Null

    Invoke-Step 'dotnet test (xUnit)' 'Fast' {
        $trxName = 'core-tests.trx'
        if (-not (Test-Path -LiteralPath $script:DotnetDir)) {
            [void](New-Item -ItemType Directory -Path $script:DotnetDir -Force)
        }

        $test = Invoke-Native $dotnet @(
            'test', $script:Sln, '-c', $script:Configuration, '--no-build', '--nologo',
            '--results-directory', $script:DotnetDir,
            '--logger', ('trx;LogFileName={0}' -f $trxName)
        )
        Write-Artifact $script:DotnetDir 'test.log' $test.Output

        $summaryLine = @($test.Output -split "`r?`n" | Where-Object { $_ -match 'Failed:\s+\d+' }) | Select-Object -Last 1
        $failed = 0
        $passed = 0
        if ($summaryLine) {
            if ($summaryLine -match 'Failed:\s+(\d+)') { $failed = [int]$Matches[1] }
            if ($summaryLine -match 'Passed:\s+(\d+)') { $passed = [int]$Matches[1] }
        }

        if (-not $test.Ok -or $failed -gt 0) {
            $detail = if ($summaryLine) { $summaryLine.Trim() } else { 'no summary line' }
            return @{ Status = 'FAIL'; Detail = ("exit {0}; {1}" -f $test.Code, $detail) }
        }

        return @{ Status = 'PASS'; Detail = ("{0} passed" -f $passed) }
    } | Out-Null

    # Sim determinism (process level) - unchanged from the Stage 01 harness.
    Invoke-Step 'sim: determinism' 'Fast' {
        if (-not (Test-Path -LiteralPath $script:SimDll)) {
            return @{ Status = 'FAIL'; Detail = 'the Sim assembly was not built' }
        }

        $seedA = $Seed
        $seedB = $Seed + 1
        $run1 = Invoke-Native $dotnet @('exec', $script:SimDll, '--seed', "$seedA", '--quiet')
        $run2 = Invoke-Native $dotnet @('exec', $script:SimDll, '--seed', "$seedA", '--quiet')
        $run3 = Invoke-Native $dotnet @('exec', $script:SimDll, '--seed', "$seedB", '--quiet')

        if (-not ($run1.Ok -and $run2.Ok -and $run3.Ok)) {
            return @{ Status = 'FAIL'; Detail = ("sim exited with {0}/{1}/{2}" -f $run1.Code, $run2.Code, $run3.Code) }
        }
        if ($run1.Output -ne $run2.Output) {
            return @{ Status = 'FAIL'; Detail = ("seed {0} printed '{1}' then '{2}'" -f $seedA, $run1.Output, $run2.Output) }
        }
        if ($run1.Output -eq $run3.Output) {
            return @{ Status = 'FAIL'; Detail = ("seeds {0} and {1} produced the same fingerprint" -f $seedA, $seedB) }
        }

        return @{ Status = 'PASS'; Detail = ("seed {0} -> {1}; seed {2} -> {3}" -f $seedA, $run1.Output, $seedB, $run3.Output) }
    } | Out-Null

    # The three streams must not be one stream wearing three names.
    Invoke-Step 'sim: stream separation' 'Fast' {
        if (-not (Test-Path -LiteralPath $script:SimDll)) {
            return @{ Status = 'FAIL'; Detail = 'the Sim assembly was not built' }
        }

        $full = Invoke-Native $dotnet @('exec', $script:SimDll, '--seed', "$Seed")
        Write-Artifact $script:DotnetDir 'sim-full.txt' $full.Output

        $battle = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^battleDigest=' } | Select-Object -First 1)
        $visual = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^visualDigest=' } | Select-Object -First 1)
        $shop = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^shopDigest=' } | Select-Object -First 1)
        $produced = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^ticksProduced=' } | Select-Object -First 1)

        if (-not $full.Ok) { return @{ Status = 'FAIL'; Detail = ("sim exited with {0}" -f $full.Code) } }
        if (-not $battle -or -not $visual -or -not $shop) {
            return @{ Status = 'FAIL'; Detail = 'could not read the per-stream digests from the Sim output' }
        }
        if ($battle -eq $visual -or $battle -eq $shop -or $visual -eq $shop) {
            return @{ Status = 'FAIL'; Detail = ("{0} / {1} / {2}" -f $shop, $battle, $visual) }
        }
        if ($produced -ne 'ticksProduced=300') {
            return @{ Status = 'FAIL'; Detail = ("expected ticksProduced=300, got '{0}'" -f $produced) }
        }

        return @{ Status = 'PASS'; Detail = ("{0} {1} {2}" -f $shop, $battle, $visual) }
    } | Out-Null

    # The bot plays two floors headlessly; full runs are `tools/sim.ps1 --seeds 200`.
    Invoke-Step 'sim: bot smoke run' 'Fast' {
        if (-not (Test-Path -LiteralPath $script:SimDll)) {
            return @{ Status = 'FAIL'; Detail = 'the Sim assembly was not built' }
        }

        $botCsv = Join-Path $script:DotnetDir 'balance-smoke.csv'
        $bot = Invoke-Native $dotnet @('exec', $script:SimDll, '--mode', 'bot', '--seeds', '2', '--out', $botCsv, '--quiet')
        if (-not $bot.Ok) {
            return @{ Status = 'FAIL'; Detail = ("bot exited with {0}" -f $bot.Code) }
        }

        $rows = 0
        if (Test-Path -LiteralPath $botCsv) {
            $rows = @(
                Get-Content -LiteralPath $botCsv | Where-Object { $_ -notmatch '^##' -and $_ -notmatch '^seed,' }
            ).Count
        }

        if ($rows -lt 1) {
            return @{ Status = 'FAIL'; Detail = "the bot wrote no balance rows to $botCsv" }
        }

        return @{ Status = 'PASS'; Detail = ("2 seeds -> {0} floor rows" -f $rows) }
    } | Out-Null
}
else {
    Add-Step 'dotnet build' 'Fast' 'NOT RUN' 'dotnet is unavailable'
    Add-Step 'dotnet test (xUnit)' 'Fast' 'NOT RUN' 'dotnet is unavailable'
    Add-Step 'sim: determinism' 'Fast' 'NOT RUN' 'dotnet is unavailable'
    Add-Step 'sim: stream separation' 'Fast' 'NOT RUN' 'dotnet is unavailable'
    Add-Step 'sim: bot smoke run' 'Fast' 'NOT RUN' 'dotnet is unavailable'
}

# ---------------------------------------------------------------------------
# 5. The static data gates, each a tool in its own right
# ---------------------------------------------------------------------------
function Invoke-Tool {
    param([Parameter(Mandatory)][string]$Script, [string[]]$Arguments = @())
    $path = Join-Path $PSScriptRoot $Script
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{ Missing = $true; Ok = $false; Code = -1; Output = "$Script not found" }
    }

    $run = Invoke-Native 'powershell' (@(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $path
        ) + $Arguments + @('-NoWrite'))

    return [pscustomobject]@{ Missing = $false; Ok = $run.Ok; Code = $run.Code; Output = $run.Output }
}

Invoke-Step 'data: generated tables validate' 'Fast' {
    $r = Invoke-Tool 'validate-data.ps1' @('-DataDir', (Join-Path $script:SlnDir 'data'))
    Write-Artifact $script:DotnetDir 'validate-data.log' $r.Output
    if ($r.Missing) { return @{ Status = 'FAIL'; Detail = 'tools/validate-data.ps1 is missing' } }
    if ($r.Ok) { return @{ Status = 'PASS'; Detail = 'no findings' } }
    return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/dotnet/validate-data.log" -f $r.Code) }
} | Out-Null

Invoke-Step 'data: generated code reproducible' 'Fast' {
    $r = Invoke-Tool 'verify-generated.ps1'
    Write-Artifact $script:DotnetDir 'verify-generated.log' $r.Output
    if ($r.Missing) { return @{ Status = 'FAIL'; Detail = 'tools/verify-generated.ps1 is missing' } }
    if ($r.Ok) { return @{ Status = 'PASS'; Detail = 'regeneration reproduces the tracked output' } }
    return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/dotnet/verify-generated.log" -f $r.Code) }
} | Out-Null

Invoke-Step 'data: broken fixtures rejected' 'Fast' {
    $r = Invoke-Tool 'check-fixtures.ps1'
    Write-Artifact $script:DotnetDir 'check-fixtures.log' $r.Output
    $count = @($r.Output -split "`r?`n" | Where-Object { $_ -match '^\s*\[PASS\] fixture:' }).Count
    if ($r.Missing) { return @{ Status = 'FAIL'; Detail = 'tools/check-fixtures.ps1 is missing' } }
    if ($r.Ok) { return @{ Status = 'PASS'; Detail = ("{0} fixture(s) rejected" -f $count) } }
    return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/dotnet/check-fixtures.log" -f $r.Code) }
} | Out-Null

# ---------------------------------------------------------------------------
# 6-7. Unity: EditMode is Fast, PlayMode is Full. Both go through the open Editor.
# ---------------------------------------------------------------------------
# The Editor connection: `unity status` prints one row per connected Editor. A run is submitted with
# --async_tests and polled through test_status, because a synchronous run holds the main thread and a
# window that is not in the foreground does not tick (measured: a synchronous EditMode run timed out
# at 600 s while the asynchronous one finished in 12 s).
function Get-UnityEditor {
    if (-not $unity) { return $null }

    $status = Invoke-Native $unity @('status')
    if (-not $status.Ok) { return $null }

    $project = (Join-Path $script:Root 'UnityProject').TrimEnd('\')
    foreach ($line in @($status.Output -split "`r?`n")) {
        if ($line -match '^\s*(\d+)\s+(\S+)\s+(.+?)\s+(\S+)\s+\d+\s*$') {
            $path = $Matches[3].Trim()
            if ($path.TrimEnd('\') -ieq $project) {
                return [pscustomobject]@{ Port = $Matches[1]; State = $Matches[2]; Project = $path }
            }
        }
    }

    return $null
}

function Get-UnityTestStatus {
    $r = Invoke-Native $unity @('command', 'test_status', '--timeout', '30', '--result-only')
    if (-not $r.Ok -or -not $r.Output) { return $null }
    try {
        $payload = $r.Output | ConvertFrom-Json
    }
    catch {
        return $null
    }
    return [pscustomobject]@{ Payload = $payload; Raw = $r.Output }
}

function Invoke-UnityTests {
    param([Parameter(Mandatory)][ValidateSet('editor', 'playmode')][string]$Mode)

    $editor = Get-UnityEditor
    if ($null -eq $editor) {
        return @{ Status = 'NOT RUN'; Detail = 'no Unity Editor with this project is connected (open UnityProject in Unity 6.3 with the Pipeline package, then run again)' }
    }

    $before = Get-UnityTestStatus
    $beforeRaw = if ($before) { $before.Raw } else { '' }

    $submit = Invoke-Native $unity @('command', 'run_tests', '--mode', $Mode, '--async_tests', '--timeout', '120')
    if (-not $submit.Ok) {
        return @{ Status = 'FAIL'; Detail = ("run_tests could not be submitted: {0}" -f $submit.Output) }
    }

    $deadline = (Get-Date).AddSeconds($UnityTimeoutSeconds)
    $sawRunning = $false
    $last = $null
    $lastRaw = $beforeRaw

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 3
        $status = Get-UnityTestStatus
        if ($null -eq $status) { continue }
        $last = $status.Payload
        $lastRaw = $status.Raw

        if ($last.status -eq 'running') {
            $sawRunning = $true
            continue
        }

        if ($sawRunning -or $lastRaw -ne $beforeRaw) {
            break
        }
    }

    if ($null -eq $last -or $last.status -eq 'running' -or (-not $sawRunning -and $lastRaw -eq $beforeRaw)) {
        return @{ Status = 'NOT RUN'; Detail = ("the {0} run did not report a result within {1} s (the Editor may be busy or a modal dialog may be open)" -f $Mode, $UnityTimeoutSeconds) }
    }

    Write-Artifact $script:DotnetDir ("unity-{0}-status.json" -f $Mode) $lastRaw

    $results = @($last.results)
    $passed = @($results | Where-Object { $_.Status -eq 'Passed' }).Count
    $failed = @($results | Where-Object { $_.Status -ne 'Passed' -and $_.Status -ne 'Skipped' }).Count
    $total = $results.Count

    if ($total -eq 0) {
        return @{ Status = 'FAIL'; Detail = 'the run reported no tests at all' }
    }

    if ($failed -gt 0) {
        $names = @($results | Where-Object { $_.Status -ne 'Passed' -and $_.Status -ne 'Skipped' } | ForEach-Object { $_.FullName })
        $shown = @($names | Select-Object -First 4)
        $more = if ($names.Count -gt $shown.Count) { (" (+{0} more)" -f ($names.Count - $shown.Count)) } else { '' }
        return @{ Status = 'FAIL'; Detail = ("{0}/{1} passed; {2} not passed: {3}{4}; see artifacts/dotnet/unity-{5}-status.json" -f $passed, $total, $failed, ($shown -join ', '), $more, $Mode) }
    }

    return @{ Status = 'PASS'; Detail = ("{0}/{1} passed ({2} s)" -f $passed, $total, $last.duration) }
}

Invoke-Step 'unity: EditMode' 'Fast' { Invoke-UnityTests -Mode 'editor' } | Out-Null

if ($Suite -eq 'Full') {
    Invoke-Step 'unity: PlayMode' 'Full' { Invoke-UnityTests -Mode 'playmode' } | Out-Null
}
else {
    Add-Step 'unity: PlayMode' 'Full' 'NOT RUN' 'the Full suite was not requested (-Suite Full)'
}

# ---------------------------------------------------------------------------
# 8-9. Full: the WebGL build and the browser suite
# ---------------------------------------------------------------------------
if ($Suite -ne 'Full') {
    Add-Step 'web: WebGL build (Development)' 'Full' 'NOT RUN' 'the Full suite was not requested (-Suite Full)'
    Add-Step 'web: Playwright (smoke + battle + camp + result)' 'Full' 'NOT RUN' 'the Full suite was not requested (-Suite Full)'
}
else {
    $buildOk = $false

    Invoke-Step 'web: WebGL build (Development)' 'Full' {
        $editor = Get-UnityEditor
        if ($null -ne $editor) {
            return @{ Status = 'NOT RUN'; Detail = 'a Unity Editor holds the project lock; close it and run the Full suite again (tools/build-web.ps1 cannot share a project)' }
        }

        $build = Invoke-Native 'powershell' @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'build-web.ps1'),
            '-Configuration', $script:WebConfiguration, '-Force'
        )
        Write-Artifact $script:DotnetDir 'web-build.log' $build.Output

        if ($build.Code -eq 2) {
            return @{ Status = 'NOT RUN'; Detail = ('build-web.ps1 could not run: {0}' -f (@($build.Output -split "`r?`n" | Where-Object { $_ -match 'RESULT: ERROR' }) -join ' ')) }
        }
        if (-not $build.Ok) {
            return @{ Status = 'FAIL'; Detail = 'the WebGL build failed; see artifacts/dotnet/web-build.log' }
        }

        # The player's own files live in Build/ under the output directory (Web.wasm, Web.data,
        # Web.framework.js), so the wasm is looked for recursively, not at the top level.
        $index = Join-Path $script:WebRoot 'index.html'
        $wasm = @(Get-ChildItem -LiteralPath $script:WebRoot -Recurse -Filter *.wasm -File -ErrorAction SilentlyContinue)
        if (-not (Test-Path -LiteralPath $index) -or $wasm.Count -lt 1) {
            return @{ Status = 'FAIL'; Detail = ("the build produced no player at {0}" -f $script:WebRoot) }
        }

        $script:WebBuildOk = $true
        return @{ Status = 'PASS'; Detail = ("{0} configuration -> {1}" -f $script:WebConfiguration, $script:WebRoot) }
    } | Out-Null

    Invoke-Step 'web: Playwright (smoke + battle + camp + result)' 'Full' {
        if (-not $script:WebBuildOk) {
            return @{ Status = 'NOT RUN'; Detail = 'the WebGL build step did not produce a player to test' }
        }
        if (-not $node) {
            return @{ Status = 'NOT RUN'; Detail = 'node is not on PATH' }
        }

        $testsDir = Join-Path $script:Root 'tests\web'
        if (-not (Test-Path -LiteralPath (Join-Path $testsDir 'node_modules'))) {
            return @{ Status = 'NOT RUN'; Detail = 'tests/web/node_modules is missing; run npm install in tests/web first' }
        }

        $npx = Find-CommandPath 'npx'
        if (-not $npx) {
            return @{ Status = 'NOT RUN'; Detail = 'npx is not on PATH' }
        }

        $serveLog = Join-Path $script:DotnetDir 'serve-web.log'
        $serveErr = Join-Path $script:DotnetDir 'serve-web.err'
        $server = $null
        try {
            $server = Start-Process -FilePath $node -ArgumentList @(
                (Join-Path $PSScriptRoot 'serve-web.mjs'), '--root', $script:WebRoot, '--port', "$($script:WebPort)"
            ) -PassThru -NoNewWindow -RedirectStandardOutput $serveLog -RedirectStandardError $serveErr

            $up = $false
            for ($i = 0; $i -lt 30; $i++) {
                Start-Sleep -Milliseconds 500
                if ((Test-Path -LiteralPath $serveLog) -and (Get-Content -LiteralPath $serveLog -Raw) -match 'serving ') {
                    $up = $true
                    break
                }
                if ($server.HasExited) { break }
            }

            if (-not $up) {
                return @{ Status = 'NOT RUN'; Detail = ("tools/serve-web.mjs did not start on port {0}; see artifacts/dotnet/serve-web.log" -f $script:WebPort) }
            }

            $playwright = Invoke-Native $npx @(
                'playwright', 'test', 'smoke.spec.ts', 'battle.spec.ts', 'camp.spec.ts', 'result.spec.ts'
            ) $testsDir
            Write-Artifact $script:DotnetDir 'playwright.log' $playwright.Output

            $passedCount = 0
            $failedCount = 0
            foreach ($line in @($playwright.Output -split "`r?`n")) {
                if ($line -match '(\d+)\s+passed') { $passedCount = [int]$Matches[1] }
                if ($line -match '(\d+)\s+failed') { $failedCount = [int]$Matches[1] }
            }

            if ($failedCount -gt 0 -or -not $playwright.Ok) {
                return @{ Status = 'FAIL'; Detail = ("{0} passed, {1} failed; see artifacts/dotnet/playwright.log" -f $passedCount, $failedCount) }
            }

            return @{ Status = 'PASS'; Detail = ("{0} passed in Chrome ({1})" -f $passedCount, $script:WebPort) }
        }
        finally {
            if ($server -and -not $server.HasExited) {
                try { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue } catch { }
            }
        }
    } | Out-Null
}

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
$failed = @($script:Results | Where-Object { $_.Status -eq 'FAIL' })
$notRun = @($script:Results | Where-Object { $_.Status -eq 'NOT RUN' })
$warned = @($script:Results | Where-Object { $_.Status -eq 'WARN' })
$passed = @($script:Results | Where-Object { $_.Status -eq 'PASS' })
$totalSeconds = [math]::Round((@($script:Results | Measure-Object -Property Seconds -Sum).Sum), 1)

$exitCode = 0
if ($failed.Count -gt 0) { $exitCode = 1 }
if ($script:HarnessError) { $exitCode = 2 }

if (-not $Json) {
    Write-Host ('=' * 78)
    # The table at the end carries the steps that need a decision; every step was already printed
    # with its timing as it finished. Full detail is always in artifacts/test-summary.md.
    foreach ($r in @($script:Results | Where-Object { $_.Status -eq 'FAIL' -or $_.Status -eq 'NOT RUN' -or $_.Status -eq 'WARN' })) {
        $color = 'Gray'
        switch ($r.Status) {
            'FAIL' { $color = 'Red' }
            'WARN' { $color = 'Yellow' }
            'NOT RUN' { $color = 'DarkGray' }
        }
        Write-Host ('  [{0,-7}] {1,-38} {2,7:N1}s  {3}' -f $r.Status, $r.Name, $r.Seconds, $r.Detail) -ForegroundColor $color
    }
    Write-Host ('=' * 78)
    Write-Host ("  PASS {0}  |  FAIL {1}  |  NOT RUN {2}  |  WARN {3}   ({4} s of steps)" -f `
            $passed.Count, $failed.Count, $notRun.Count, $warned.Count, $totalSeconds)
    if ($exitCode -eq 0) {
        Write-Host ("  RESULT: OK ({0} suite)" -f $Suite) -ForegroundColor Green
    }
    elseif ($exitCode -eq 1) {
        Write-Host ("  RESULT: FAILED ({0} suite)" -f $Suite) -ForegroundColor Red
    }
    else {
        Write-Host ("  RESULT: ERROR - {0}" -f $script:HarnessError) -ForegroundColor Red
    }
    Write-Host ''
}

$report = [pscustomobject]@{
    tool          = 'test.ps1'
    suite         = $Suite
    configuration = $script:Configuration
    webConfiguration = $script:WebConfiguration
    generatedAt   = (Get-Date).ToString('o')
    machine       = $env:COMPUTERNAME
    root          = $script:Root
    seed          = $Seed
    exitCode      = $exitCode
    totalSeconds  = $totalSeconds
    summary       = [pscustomobject]@{
        pass   = $passed.Count
        fail   = $failed.Count
        notRun = $notRun.Count
        warn   = $warned.Count
    }
    steps         = @($script:Results)
}

if ($Json) {
    $report | ConvertTo-Json -Depth 6
}

if (-not $NoWrite) {
    try {
        if (-not (Test-Path -LiteralPath $script:ArtifactDir)) {
            [void](New-Item -ItemType Directory -Path $script:ArtifactDir -Force)
        }

        $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'test-summary.json') -Encoding UTF8

        $lines = @(
            ('# Test summary - {0} suite' -f $Suite)
            ''
            ('- when: `{0}`' -f $report.generatedAt)
            ('- machine: `{0}`' -f $report.machine)
            ('- configuration: `{0}` (web: `{1}`)' -f $script:Configuration, $script:WebConfiguration)
            ('- result: **{0}** - PASS {1} | FAIL {2} | NOT RUN {3} | WARN {4} - {5} s of steps' -f `
                ($(if ($exitCode -eq 0) { 'OK' } elseif ($exitCode -eq 1) { 'FAILED' } else { 'ERROR' })), `
                $passed.Count, $failed.Count, $notRun.Count, $warned.Count, $totalSeconds)
            ''
            '| Step | Suite | Status | Seconds | Detail |'
            '| --- | --- | --- | --- | --- |'
        ) + @($script:Results | ForEach-Object {
                ('| {0} | {1} | **{2}** | {3:N1} | {4} |' -f $_.Name, $_.Suite, $_.Status, $_.Seconds, ($_.Detail -replace '\|', '\|'))
            })

        if ($notRun.Count -gt 0) {
            $lines += @('', 'NOT RUN is not a pass: each line above says what was missing.')
        }

        $lines | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'test-summary.md') -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write the summary: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
