<#
.SYNOPSIS
    ProjectSurvive dotnet harness (Stage 01): build + unit-test the Core rule
    engine and check that the headless Sim is deterministic.

.DESCRIPTION
    Runs entirely outside Unity, against lib/Tests~/ProjectSurvive.Core.sln, which
    compiles the SAME source files as the lib/ .asmdefs (see
    docs/adr/0004-dotnet-layer-enforcement.md).

    Checks:
      1. dotnet SDK present (>= 8)
      2. solution present
      3. `dotnet build` of every layer, the test project and the Sim
      4. `dotnet test` (xUnit) with a .trx result file written to artifacts/
      5. Sim determinism, at process level: the same seed must print the same
         fingerprint twice, and a different seed must print a different one
      6. Static data pipeline (Stage 02): the generated tables validate, the
         generated code is reproducible from data/ + proto/, and every broken
         fixture under lib/Tests~/fixtures is rejected for the right reason

    Exit codes (same contract as tools/doctor.ps1):
        0 = every REQUIRED check passed
        1 = at least one REQUIRED check failed
        2 = the harness itself could not run

.PARAMETER Configuration
    Build configuration. Debug (default) or Release.

.PARAMETER Seed
    Seed used by the Sim determinism checks. Default 20261013.

.PARAMETER NoSim
    Run build + tests only, skipping the Sim checks (useful when iterating).

.PARAMETER NoPipeline
    Skip the Stage 02 static-data gates (validate-data, verify-generated,
    check-fixtures). They are the slow part of this harness: verify-generated
    regenerates the whole data set and check-fixtures runs twelve of them.

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.PARAMETER NoWrite
    Do not write report files under artifacts/.

.EXAMPLE
    pwsh -NoProfile -File tools/test.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [uint32]$Seed = 20261013,
    [switch]$NoSim,
    [switch]$NoPipeline,
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:SlnDir = Join-Path $script:Root 'lib\Tests~'
$script:Sln = Join-Path $script:SlnDir 'ProjectSurvive.Core.sln'
$script:SimDll = Join-Path $script:SlnDir ("Sim\bin\{0}\net9.0\ProjectSurvive.Sim.dll" -f $Configuration)
$script:ArtifactDir = Join-Path $script:Root 'artifacts\dotnet'
$script:Results = New-Object System.Collections.ArrayList

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('Required', 'Optional')][string]$Level,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'WARN', 'SKIP')][string]$Status,
        [string]$Detail = ''
    )
    [void]$script:Results.Add([pscustomobject]@{
            Name   = $Name
            Level  = $Level
            Status = $Status
            Detail = $Detail
        })
}

# Run a native command, return its exit code and combined output, never throw.
function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @()
    )
    try {
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

# ---------------------------------------------------------------------------
# 1. Toolchain
# ---------------------------------------------------------------------------
$dotnet = Find-CommandPath 'dotnet'
if (-not $dotnet) {
    Add-Result '.NET SDK' 'Required' 'FAIL' 'dotnet not found on PATH'
}
else {
    $r = Invoke-Native $dotnet @('--list-sdks')
    if ($r.Ok -and $r.Output) {
        $sdks = @($r.Output -split "`r?`n" | Where-Object { $_ -match '\S' })
        $major = 0
        foreach ($s in $sdks) {
            if ($s -match '^(\d+)\.') { if ([int]$Matches[1] -gt $major) { $major = [int]$Matches[1] } }
        }
        if ($major -ge 8) {
            Add-Result '.NET SDK' 'Required' 'PASS' ($sdks -join '; ')
        }
        else {
            Add-Result '.NET SDK' 'Required' 'FAIL' ("need an SDK >= 8 for net9.0 tests; found: {0}" -f ($sdks -join '; '))
        }
    }
    else {
        Add-Result '.NET SDK' 'Required' 'FAIL' ('dotnet --list-sdks failed: {0}' -f $r.Output)
    }
}

# ---------------------------------------------------------------------------
# 2. Solution present
# ---------------------------------------------------------------------------
if (Test-Path -LiteralPath $script:Sln) {
    Add-Result 'dotnet solution' 'Required' 'PASS' $script:Sln
}
else {
    Add-Result 'dotnet solution' 'Required' 'FAIL' ("missing {0}" -f $script:Sln)
}

# ---------------------------------------------------------------------------
# 3. Build
# ---------------------------------------------------------------------------
if ($dotnet -and (Test-Path -LiteralPath $script:Sln)) {
    $build = Invoke-Native $dotnet @('build', $script:Sln, '-c', $Configuration, '--nologo', '-v', 'minimal')
    Write-Artifact 'build.log' $build.Output

    if (-not $build.Ok) {
        Add-Result 'dotnet build' 'Required' 'FAIL' ("exit {0}; see artifacts/dotnet/build.log" -f $build.Code)
    }
    else {
        Add-Result 'dotnet build' 'Required' 'PASS' ("{0} configuration" -f $Configuration)
    }

    # Warnings are reported, not fatal here: the vendored EcsLite project does
    # not treat warnings as errors (see lib/third_party/ecslite/VENDOR.md).
    $warningLines = @($build.Output -split "`r?`n" | Where-Object { $_ -match ':\s+warning\s' })
    if ($build.Ok -and $warningLines.Count -eq 0) {
        Add-Result 'build warnings' 'Required' 'PASS' 'none'
    }
    elseif ($build.Ok) {
        Add-Result 'build warnings' 'Required' 'WARN' ("{0} warning line(s); first: {1}" -f $warningLines.Count, $warningLines[0].Trim())
    }
    else {
        Add-Result 'build warnings' 'Required' 'SKIP' 'build failed'
    }
}
else {
    Add-Result 'dotnet build' 'Required' 'SKIP' 'prerequisites missing'
    Add-Result 'build warnings' 'Required' 'SKIP' 'prerequisites missing'
}

# ---------------------------------------------------------------------------
# 4. Tests
# ---------------------------------------------------------------------------
$testSummary = $null
if ($dotnet -and (Test-Path -LiteralPath $script:Sln)) {
    $trxName = 'core-tests.trx'
    $test = Invoke-Native $dotnet @(
        'test', $script:Sln, '-c', $Configuration, '--no-build', '--nologo',
        '--results-directory', $script:ArtifactDir,
        '--logger', ('trx;LogFileName={0}' -f $trxName)
    )
    Write-Artifact 'test.log' $test.Output

    $summaryLine = @($test.Output -split "`r?`n" | Where-Object { $_ -match 'Failed:\s+\d+' }) | Select-Object -Last 1
    if ($summaryLine) {
        $testSummary = $summaryLine.Trim()
    }

    $failed = 0
    $passed = 0
    if ($summaryLine -match 'Failed:\s+(\d+)') { $failed = [int]$Matches[1] }
    if ($summaryLine -match 'Passed:\s+(\d+)') { $passed = [int]$Matches[1] }

    if (-not $test.Ok) {
        Add-Result 'dotnet test' 'Required' 'FAIL' ("exit {0}; {1}" -f $test.Code, $testSummary)
    }
    elseif ($failed -gt 0) {
        Add-Result 'dotnet test' 'Required' 'FAIL' $testSummary
    }
    else {
        Add-Result 'dotnet test' 'Required' 'PASS' ("{0} passed" -f $passed)
    }
}
else {
    Add-Result 'dotnet test' 'Required' 'SKIP' 'prerequisites missing'
}

# ---------------------------------------------------------------------------
# 5. Sim determinism (process level)
# ---------------------------------------------------------------------------
function Invoke-Sim {
    param([Parameter(Mandatory)][string[]]$SimArguments)
    if (-not (Test-Path -LiteralPath $script:SimDll)) {
        return [pscustomobject]@{ Ok = $false; Code = -1; Output = 'Sim assembly not built' }
    }
    return Invoke-Native $dotnet (@('exec', $script:SimDll) + $SimArguments)
}

if ($NoSim) {
    Add-Result 'Sim determinism' 'Required' 'SKIP' 'disabled with -NoSim'
    Add-Result 'Sim stream separation' 'Required' 'SKIP' 'disabled with -NoSim'
}
elseif (-not $dotnet) {
    Add-Result 'Sim determinism' 'Required' 'SKIP' 'dotnet unavailable'
    Add-Result 'Sim stream separation' 'Required' 'SKIP' 'dotnet unavailable'
}
else {
    $seedA = $Seed
    $seedB = $Seed + 1

    $run1 = Invoke-Sim @('--seed', "$seedA", '--quiet')
    $run2 = Invoke-Sim @('--seed', "$seedA", '--quiet')
    $run3 = Invoke-Sim @('--seed', "$seedB", '--quiet')

    if (-not ($run1.Ok -and $run2.Ok -and $run3.Ok)) {
        Add-Result 'Sim determinism' 'Required' 'FAIL' ("sim exited with {0}/{1}/{2}" -f $run1.Code, $run2.Code, $run3.Code)
    }
    elseif ($run1.Output -ne $run2.Output) {
        Add-Result 'Sim determinism' 'Required' 'FAIL' ("seed {0} printed '{1}' then '{2}'" -f $seedA, $run1.Output, $run2.Output)
    }
    elseif ($run1.Output -eq $run3.Output) {
        Add-Result 'Sim determinism' 'Required' 'FAIL' ("seeds {0} and {1} produced the same fingerprint" -f $seedA, $seedB)
    }
    else {
        Add-Result 'Sim determinism' 'Required' 'PASS' ("seed {0} -> {1}; seed {2} -> {3}" -f $seedA, $run1.Output, $seedB, $run3.Output)
    }

    # The three streams must not be one stream wearing three names: their
    # fingerprints for the same seed have to differ.
    $full = Invoke-Sim @('--seed', "$seedA")
    Write-Artifact 'sim-full.txt' $full.Output

    $battle = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^battleDigest=' } | Select-Object -First 1)
    $visual = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^visualDigest=' } | Select-Object -First 1)
    $shop = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^shopDigest=' } | Select-Object -First 1)
    $produced = ($full.Output -split "`r?`n" | Where-Object { $_ -match '^ticksProduced=' } | Select-Object -First 1)

    if (-not $full.Ok) {
        Add-Result 'Sim stream separation' 'Required' 'FAIL' ("sim exited with {0}" -f $full.Code)
    }
    elseif (-not $battle -or -not $visual -or -not $shop) {
        Add-Result 'Sim stream separation' 'Required' 'FAIL' 'could not read the per-stream digests from the Sim output'
    }
    elseif ($battle -eq $visual -or $battle -eq $shop -or $visual -eq $shop) {
        Add-Result 'Sim stream separation' 'Required' 'FAIL' ("{0} / {1} / {2}" -f $shop, $battle, $visual)
    }
    elseif ($produced -ne 'ticksProduced=300') {
        Add-Result 'Sim stream separation' 'Required' 'FAIL' ("expected ticksProduced=300, got '{0}'" -f $produced)
    }
    else {
        Add-Result 'Sim stream separation' 'Required' 'PASS' ("{0} {1} {2}" -f $shop, $battle, $visual)
    }
}

# ---------------------------------------------------------------------------
# 6. Static data pipeline (Stage 02)
# ---------------------------------------------------------------------------
# Each of these is a tool in its own right (`tools/<name>.ps1`); the harness runs
# them because they are the stage's acceptance gates and nobody should have to
# remember three commands. Every one of them is a process with its own exit code,
# so a failure is reported with the tool's own report in artifacts/.
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

if ($NoPipeline) {
    Add-Result 'data: generated tables validate' 'Required' 'SKIP' 'disabled with -NoPipeline'
    Add-Result 'data: generated code is reproducible' 'Required' 'SKIP' 'disabled with -NoPipeline'
    Add-Result 'data: broken fixtures are rejected' 'Required' 'SKIP' 'disabled with -NoPipeline'
}
else {
    $validate = Invoke-Tool 'validate-data.ps1' @('-DataDir', (Join-Path $script:Root 'lib\Tests~\data'))
    Write-Artifact 'validate-data.log' $validate.Output
    if ($validate.Missing) {
        Add-Result 'data: generated tables validate' 'Required' 'FAIL' 'tools/validate-data.ps1 is missing'
    }
    elseif ($validate.Ok) {
        Add-Result 'data: generated tables validate' 'Required' 'PASS' 'no findings'
    }
    else {
        Add-Result 'data: generated tables validate' 'Required' 'FAIL' (
            "exit {0}; see artifacts/dotnet/validate-data.log" -f $validate.Code)
    }

    $verify = Invoke-Tool 'verify-generated.ps1'
    Write-Artifact 'verify-generated.log' $verify.Output
    if ($verify.Missing) {
        Add-Result 'data: generated code is reproducible' 'Required' 'FAIL' 'tools/verify-generated.ps1 is missing'
    }
    elseif ($verify.Ok) {
        Add-Result 'data: generated code is reproducible' 'Required' 'PASS' 'regeneration reproduces the tracked output'
    }
    else {
        Add-Result 'data: generated code is reproducible' 'Required' 'FAIL' (
            "exit {0}; see artifacts/dotnet/verify-generated.log" -f $verify.Code)
    }

    $fixtures = Invoke-Tool 'check-fixtures.ps1'
    Write-Artifact 'check-fixtures.log' $fixtures.Output
    $fixtureCount = @($fixtures.Output -split "`r?`n" | Where-Object { $_ -match '^\s*\[PASS\] fixture:' }).Count
    if ($fixtures.Missing) {
        Add-Result 'data: broken fixtures are rejected' 'Required' 'FAIL' 'tools/check-fixtures.ps1 is missing'
    }
    elseif ($fixtures.Ok) {
        Add-Result 'data: broken fixtures are rejected' 'Required' 'PASS' ("{0} fixture(s) rejected" -f $fixtureCount)
    }
    else {
        Add-Result 'data: broken fixtures are rejected' 'Required' 'FAIL' (
            "exit {0}; see artifacts/dotnet/check-fixtures.log" -f $fixtures.Code)
    }
}

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
$failedChecks = @($script:Results | Where-Object { $_.Status -eq 'FAIL' -and $_.Level -eq 'Required' })
$warned = @($script:Results | Where-Object { $_.Status -eq 'WARN' })
$skipped = @($script:Results | Where-Object { $_.Status -eq 'SKIP' })

$exitCode = 0
if ($failedChecks.Count -gt 0) { $exitCode = 1 }

if (-not $Json) {
    Write-Host ''
    Write-Host 'ProjectSurvive - dotnet harness (Stage 01-02b)' -ForegroundColor Cyan
    Write-Host ('=' * 72)
    foreach ($r in $script:Results) {
        $color = 'Gray'
        switch ($r.Status) {
            'PASS' { $color = 'Green' }
            'FAIL' { $color = 'Red' }
            'WARN' { $color = 'Yellow' }
            'SKIP' { $color = 'DarkGray' }
        }
        Write-Host ('  [{0}] {1,-24} {2}' -f $r.Status, $r.Name, $r.Detail) -ForegroundColor $color
    }
    Write-Host ('=' * 72)
    Write-Host ("  PASS {0}  |  FAIL {1}  |  WARN {2}  |  SKIP {3}" -f `
        (@($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count), $failedChecks.Count, $warned.Count, $skipped.Count)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (all required checks passed)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (a required check failed)' -ForegroundColor Red
    }
    Write-Host ''
}

$report = [pscustomobject]@{
    tool          = 'test.ps1'
    stage         = '01-02'
    generatedAt   = (Get-Date).ToString('o')
    machine       = $env:COMPUTERNAME
    root          = $script:Root
    configuration = $Configuration
    seed          = $Seed
    simChecked    = (-not $NoSim)
    pipelineChecked = (-not $NoPipeline)
    exitCode      = $exitCode
    testSummary   = $testSummary
    summary       = [pscustomobject]@{
        pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count
        fail = $failedChecks.Count
        warn = $warned.Count
        skip = $skipped.Count
    }
    checks        = @($script:Results)
}

if ($Json) {
    $report | ConvertTo-Json -Depth 6
}

if (-not $NoWrite) {
    try {
        if (-not (Test-Path -LiteralPath $script:ArtifactDir)) {
            [void](New-Item -ItemType Directory -Path $script:ArtifactDir -Force)
        }
        $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'test-report.json') -Encoding UTF8
        $lines = @(
            ('generatedAt: {0}' -f $report.generatedAt),
            ('configuration: {0}' -f $Configuration),
            ('seed: {0}' -f $Seed),
            ('exitCode: {0}' -f $exitCode),
            ('testSummary: {0}' -f $testSummary),
            ''
        ) + @($script:Results | ForEach-Object { ('[{0}] {1,-24} {2}' -f $_.Status, $_.Name, $_.Detail) })
        $lines | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'test-report.txt') -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write report files: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
