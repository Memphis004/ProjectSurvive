#Requires -Version 5.1
<#
.SYNOPSIS
    ProjectSurvive CI pipeline (Stage 14): the checks a commit has to pass, in one command.

.DESCRIPTION
    The pipeline the Stage 14 brief names, as one script with named jobs:

        generate -> validate -> verify-generated -> test -> sim -> unity -> build -> web

    Two kinds of job, kept apart because only one of them needs a Unity licence:

      * pure validation (no Editor, no licence): `generate`, `validate`, `verify-generated`,
        `test` (dotnet build + xUnit), `sim` (50 seeds and a win-rate band).
      * licence jobs (one Unity process at a time, and the project must not be open):
        `unity` (EditMode then PlayMode through `unity test`, each writing NUnit XML),
        `build` (the Release WebGL player) and `web` (Playwright against that player).

    `-Job Validate` runs only the first group, which is what a CI machine without a licence can
    do. `-Job All` runs everything in the order above.

    Unity cannot share a project directory: a second Editor on `UnityProject` is a corrupt
    project waiting to happen, so the licence jobs refuse to start while any Editor or batch
    build holds the project (and `tools/build-web.ps1` would refuse too). That refusal is
    reported as NOT RUN with the reason, and the script exits 2 - a blocked gate is never a pass.

    Exit codes (the same contract as tools/test.ps1, tools/build-web.ps1 and tools/doctor.ps1):
        0 = every required check passed
        1 = at least one required check FAILED
        2 = the pipeline could not run a required check (project locked, no Editor, no browser)
            or the harness itself broke

.PARAMETER Job
    All (default), Validate, Test, Sim, Unity, Build or Web. Several may be named.

.PARAMETER Configuration
    Release (default) or Development. Release is what the stage gate wants: ADR 0003 says only a
    Release build's numbers may be quoted, and `web` picks the harness or release-absence spec
    from this value.

.PARAMETER Seeds
    How many bot seeds the `sim` job plays (default 50).

.PARAMETER MinBossWinRate / MaxBossWinRate
    The band the boss-floor win rate has to stay inside (default 0.30 - 0.60, the band Stage 11d
    tuned to). Outside it, `sim` FAILS: a balance change that makes the last floor a formality or
    a coin flip is a regression the bot can see before a player does.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Validate
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1 -Job Build,Web
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/ci.ps1
#>
[CmdletBinding()]
param(
    # A comma-separated list, because `powershell -File` hands `-Job Build,Web` over as one
    # string: `-Job Build -Job Web` would bind the parameter twice and be refused.
    [string[]]$Job = @('All'),

    [ValidateSet('Development', 'Release')]
    [string]$Configuration = 'Release',

    [int]$Seeds = 50,

    [double]$MinBossWinRate = 0.30,

    [double]$MaxBossWinRate = 0.60,

    [int]$UnityTimeoutSeconds = 3600,

    [int]$WebPort = 8080,

    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:ProjectDir = Join-Path $script:Root 'UnityProject'
$script:Sln = Join-Path $script:Root 'lib\Tests~\ProjectSurvive.Core.sln'
$script:WebRoot = Join-Path $script:ProjectDir 'Builds\Web'
$script:ArtifactDir = Join-Path $script:Root 'artifacts\ci'
$script:Results = New-Object System.Collections.ArrayList
$script:HarnessError = ''
$script:Blocked = $false
$script:LockPath = Join-Path $script:ArtifactDir '.ci.lock'

if (-not (Test-Path -LiteralPath $script:ArtifactDir)) {
    [void](New-Item -ItemType Directory -Path $script:ArtifactDir -Force)
}

function Add-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$JobName,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'NOT RUN')][string]$Status,
        [string]$Detail = '',
        [double]$Seconds = 0,
        [string]$Log = ''
    )

    [void]$script:Results.Add([pscustomobject]@{
            Name    = $Name
            Job     = $JobName
            Status  = $Status
            Detail  = $Detail
            Seconds = [math]::Round($Seconds, 1)
            Log     = $Log
        })

    $color = switch ($Status) {
        'PASS' { 'Green' }
        'FAIL' { 'Red' }
        default { 'DarkGray' }
    }
    Write-Host ('  [{0,-7}] {1,-30} {2,7:N1}s  {3}' -f $Status, $Name, [math]::Round($Seconds, 1), $Detail) -ForegroundColor $color
}

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
        return [pscustomobject]@{ Ok = ($code -eq 0); Code = $code; Output = (($out | Out-String).Trim()) }
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Code = -1; Output = $_.Exception.Message }
    }
    finally {
        if ($WorkingDirectory -and (Test-Path -LiteralPath $WorkingDirectory)) {
            Pop-Location
        }
    }
}

function Write-Log {
    param([Parameter(Mandatory)][string]$Name, [string]$Content)
    if ($NoWrite) { return '' }
    $path = Join-Path $script:ArtifactDir $Name
    Set-Content -LiteralPath $path -Value $Content -Encoding UTF8
    return $path
}

# One job step: timed, always recorded, and it can never throw out of the pipeline.
function Invoke-Step {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$JobName,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    $outcome = $null
    try {
        $outcome = & $Action
    }
    catch {
        $outcome = @{ Status = 'FAIL'; Detail = ('pipeline error: {0}' -f $_.Exception.Message) }
    }
    $watch.Stop()

    if ($null -eq $outcome) {
        $outcome = @{ Status = 'FAIL'; Detail = 'the step returned nothing' }
    }

    if ($outcome.Status -eq 'NOT RUN') {
        $script:Blocked = $true
    }

    Add-Step -Name $Name -JobName $JobName -Status $outcome.Status -Detail $outcome.Detail -Seconds $watch.Elapsed.TotalSeconds -Log ([string]$outcome.Log)
    return $outcome
}

function Find-CommandPath {
    param([Parameter(Mandatory)][string]$Name)
    try {
        $command = Get-Command $Name -ErrorAction SilentlyContinue
        if ($null -ne $command) { return $command.Source }
    }
    catch { }
    return $null
}

function Invoke-Tool {
    param([Parameter(Mandatory)][string]$Script, [string[]]$Arguments = @())
    $path = Join-Path $PSScriptRoot $Script
    if (-not (Test-Path -LiteralPath $path)) {
        return [pscustomobject]@{ Ok = $false; Code = -1; Output = ("{0} not found" -f $Script) }
    }

    return Invoke-Native 'powershell' (@('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $path) + $Arguments)
}

# --- Unity project ownership -------------------------------------------------
# The one rule the licence jobs share: this script may be the only thing touching UnityProject.
function Get-ConnectedEditor {
    $unity = Find-CommandPath 'unity'
    if (-not $unity) { return $null }

    $status = Invoke-Native $unity @('status')
    if (-not $status.Ok) { return $null }

    $project = $script:ProjectDir.TrimEnd('\')
    foreach ($line in @($status.Output -split "`r?`n")) {
        if ($line -match '^\s*(\d+)\s+(\S+)\s+(.+?)\s+(\S+)\s+\d+\s*$') {
            if ($Matches[3].Trim().TrimEnd('\') -ieq $project) {
                return [pscustomobject]@{ Port = $Matches[1]; State = $Matches[2] }
            }
        }
    }

    return $null
}

function Test-ProjectInUse {
    if ($null -ne (Get-ConnectedEditor)) { return 'a Unity Editor for UnityProject is connected' }
    if (Test-Path -LiteralPath (Join-Path $script:ProjectDir 'Temp\UnityLockfile')) {
        return 'UnityProject/Temp/UnityLockfile exists (an Editor or a batch build holds the project)'
    }
    return ''
}

# A build killed mid-run leaves Temp/UnityLockfile behind, and both this script and
# `tools/build-web.ps1` read that file as "the project is open" — so one interrupted run would
# block every later one until somebody deleted it by hand. A lock with no Unity process behind it
# is stale by definition; a live Editor or batch build holds a live `Unity` process, so this only
# ever removes the leftovers of a run that is already gone.
function Clear-StaleUnityLock {
    $lockFile = Join-Path $script:ProjectDir 'Temp\UnityLockfile'
    if (-not (Test-Path -LiteralPath $lockFile)) { return $false }
    if (@(Get-Process -Name 'Unity' -ErrorAction SilentlyContinue).Count -gt 0) { return $false }

    try {
        Remove-Item -LiteralPath $lockFile -Force -ErrorAction Stop
        Write-Host '  (stale UnityLockfile removed: no Unity process was running)' -ForegroundColor Yellow
        return $true
    }
    catch {
        return $false
    }
}

$dotnet = Find-CommandPath 'dotnet'
$unity = Find-CommandPath 'unity'
$node = Find-CommandPath 'node'
$npx = Find-CommandPath 'npx'

$requested = @($Job | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($requested.Count -eq 0) { $requested = @('All') }

$knownJobs = @('All', 'Validate', 'Test', 'Sim', 'Unity', 'Build', 'Web')
foreach ($name in $requested) {
    if ($knownJobs -notcontains $name) {
        Write-Host ("unknown job '{0}'; expected one of: {1}" -f $name, ($knownJobs -join ', ')) -ForegroundColor Red
        exit 2
    }
}

if ($requested -contains 'All') {
    $requested = @('Validate', 'Test', 'Sim', 'Unity', 'Build', 'Web')
}

# A second pipeline run in the same checkout would race the first over every artifact.
if (Test-Path -LiteralPath $script:LockPath) {
    $owner = (Get-Content -LiteralPath $script:LockPath -Raw).Trim()
    $alive = $false
    if ($owner -match '^(\d+)') {
        $alive = $null -ne (Get-Process -Id ([int]$Matches[1]) -ErrorAction SilentlyContinue)
    }
    if ($alive) {
        Write-Host ("another tools/ci.ps1 is running (pid {0}); refusing to share the checkout" -f $owner) -ForegroundColor Red
        exit 2
    }
}
if (-not $NoWrite) {
    Set-Content -LiteralPath $script:LockPath -Value ("{0} {1:o}" -f $PID, (Get-Date)) -Encoding UTF8
}

Write-Host ''
Write-Host ("ProjectSurvive CI - jobs: {0} ({1})" -f ($requested -join ', '), $Configuration) -ForegroundColor Cyan
Write-Host ('=' * 78)

try {
    # -----------------------------------------------------------------------
    # Pure validation: no Editor, no licence, safe to run anywhere.
    # -----------------------------------------------------------------------
    if ($requested -contains 'Validate') {
        Invoke-Step 'generate (gen.ps1)' 'Validate' {
            $run = Invoke-Tool 'gen.ps1'
            $log = Write-Log 'generate.log' $run.Output
            if (-not $run.Ok) {
                return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/ci/generate.log" -f $run.Code); Log = $log }
            }
            return @{ Status = 'PASS'; Detail = 'data/ and proto/ produced lib/Runtime/Generated'; Log = $log }
        } | Out-Null

        Invoke-Step 'validate data' 'Validate' {
            # The tables `gen.ps1` just published for the tests are the ones to validate; the
            # source CSVs in data/ are not what the game loads (tools/test.ps1 does the same).
            $run = Invoke-Tool 'validate-data.ps1' @('-DataDir', (Join-Path $script:Root 'lib\Tests~\data'))
            $log = Write-Log 'validate-data.log' $run.Output
            if (-not $run.Ok) {
                return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/ci/validate-data.log" -f $run.Code); Log = $log }
            }
            return @{ Status = 'PASS'; Detail = 'no findings'; Log = $log }
        } | Out-Null

        Invoke-Step 'verify generated code' 'Validate' {
            $run = Invoke-Tool 'verify-generated.ps1'
            $log = Write-Log 'verify-generated.log' $run.Output
            if (-not $run.Ok) {
                return @{ Status = 'FAIL'; Detail = ("exit {0}; generated code is not reproducible; see artifacts/ci/verify-generated.log" -f $run.Code); Log = $log }
            }
            return @{ Status = 'PASS'; Detail = 'regeneration reproduces the tracked output'; Log = $log }
        } | Out-Null

        Invoke-Step 'broken fixtures rejected' 'Validate' {
            $run = Invoke-Tool 'check-fixtures.ps1'
            $log = Write-Log 'check-fixtures.log' $run.Output
            if (-not $run.Ok) {
                return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/ci/check-fixtures.log" -f $run.Code); Log = $log }
            }
            return @{ Status = 'PASS'; Detail = 'every broken fixture was rejected for the right reason'; Log = $log }
        } | Out-Null

        Invoke-Step 'wiki reproducible' 'Validate' {
            $run = Invoke-Tool 'verify-wiki.ps1'
            $log = Write-Log 'verify-wiki.log' $run.Output
            if (-not $run.Ok) {
                return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/ci/verify-wiki.log" -f $run.Code); Log = $log }
            }
            return @{ Status = 'PASS'; Detail = 'llmwiki/content matches data/'; Log = $log }
        } | Out-Null
    }

    # -----------------------------------------------------------------------
    # The test job: dotnet build + the xUnit suites, with the contract suite
    # named twice (Fake and BattleSimulation) because that is the claim a green
    # run has to make visible.
    # -----------------------------------------------------------------------
    if ($requested -contains 'Test') {
        if (-not $dotnet) {
            Add-Step 'dotnet test' 'Test' 'NOT RUN' 'dotnet is not on PATH'
            $script:Blocked = $true
        }
        else {
            $buildOk = $false
            Invoke-Step 'dotnet build' 'Test' {
                $run = Invoke-Native $dotnet @('build', $script:Sln, '-c', $Configuration, '--nologo', '-v', 'minimal')
                $log = Write-Log 'dotnet-build.log' $run.Output
                if (-not $run.Ok) {
                    return @{ Status = 'FAIL'; Detail = ("exit {0}; see artifacts/ci/dotnet-build.log" -f $run.Code); Log = $log }
                }
                return @{ Status = 'PASS'; Detail = $Configuration; Log = $log }
            } | Out-Null
            $buildOk = @($script:Results | Where-Object { $_.Name -eq 'dotnet build' -and $_.Status -eq 'FAIL' }).Count -eq 0

            if ($buildOk) {
                Invoke-Step 'dotnet test (xUnit)' 'Test' {
                    $trx = 'ci-tests.trx'
                    $run = Invoke-Native $dotnet @(
                        'test', $script:Sln, '-c', $Configuration, '--no-build', '--nologo',
                        '--results-directory', $script:ArtifactDir,
                        '--logger', ('trx;LogFileName={0}' -f $trx)
                    )
                    $log = Write-Log 'dotnet-test.log' $run.Output

                    $summaryLine = @($run.Output -split "`r?`n" | Where-Object { $_ -match 'Failed:\s+\d+' }) | Select-Object -Last 1
                    if (-not $run.Ok) {
                        return @{ Status = 'FAIL'; Detail = ("exit {0}; {1}; see artifacts/ci/dotnet-test.log" -f $run.Code, $(if ($summaryLine) { $summaryLine.Trim() } else { 'no summary line' })); Log = $log }
                    }
                    if (-not $summaryLine) {
                        return @{ Status = 'FAIL'; Detail = 'the run printed no summary line'; Log = $log }
                    }
                    return @{ Status = 'PASS'; Detail = $summaryLine.Trim(); Log = $log }
                } | Out-Null

                $contracts = @(
                    @{ Name = 'contract: FakeBattleSession'; Class = 'FakeBattleSessionContractTests' },
                    @{ Name = 'contract: BattleSimulation'; Class = 'EcsBattleSessionContractTests' }
                )

                foreach ($contract in $contracts) {
                    $className = $contract.Class
                    $displayName = $contract.Name
                    Invoke-Step $displayName 'Test' {
                        $trxPath = Join-Path $script:ArtifactDir 'ci-tests.trx'
                        if (-not (Test-Path -LiteralPath $trxPath)) {
                            return @{ Status = 'FAIL'; Detail = 'the xUnit TRX is missing, so this suite cannot be named' }
                        }

                        try {
                            $doc = New-Object System.Xml.XmlDocument
                            $doc.Load($trxPath)
                        }
                        catch {
                            return @{ Status = 'FAIL'; Detail = ('the TRX could not be read: {0}' -f $_.Exception.Message) }
                        }

                        $pattern = '\.' + [regex]::Escape($className) + '\.'
                        $matched = @($doc.SelectNodes("//*[local-name()='UnitTestResult']") | Where-Object { ([string]$_.testName) -match $pattern })

                        if ($matched.Count -eq 0) {
                            return @{ Status = 'FAIL'; Detail = ("no test named {0} ran" -f $className) }
                        }

                        $failed = @($matched | Where-Object { $_.outcome -ne 'Passed' -and $_.outcome -ne 'NotExecuted' })
                        if ($failed.Count -gt 0) {
                            return @{ Status = 'FAIL'; Detail = ("{0}/{1} passed; first failure: {2}" -f ($matched.Count - $failed.Count), $matched.Count, [string]$failed[0].testName) }
                        }

                        return @{ Status = 'PASS'; Detail = ("{0}/{0} passed" -f $matched.Count) }
                    } | Out-Null
                }
            }
            else {
                Add-Step 'dotnet test (xUnit)' 'Test' 'NOT RUN' 'the build failed, so there is nothing to run'
            }
        }
    }

    # -----------------------------------------------------------------------
    # Sim: 50 seeds through the shipped session, and the boss-floor win rate
    # has to stay inside the band. This is the one check that can see a balance
    # regression, and it needs no licence.
    # -----------------------------------------------------------------------
    if ($requested -contains 'Sim') {
        if (-not $dotnet) {
            Add-Step 'sim: 50 seeds' 'Sim' 'NOT RUN' 'dotnet is not on PATH'
            $script:Blocked = $true
        }
        else {
            Invoke-Step ('sim: {0} seeds (boss win rate {1:P0}-{2:P0})' -f $Seeds, $MinBossWinRate, $MaxBossWinRate) 'Sim' {
                $csv = Join-Path $script:ArtifactDir 'balance-50.csv'
                if (Test-Path -LiteralPath $csv) { Remove-Item -LiteralPath $csv -Force }

                $run = Invoke-Tool 'sim.ps1' @('--seeds', "$Seeds", '--output', $csv, '--configuration', $Configuration)
                $log = Write-Log 'sim.log' $run.Output
                if (-not $run.Ok) {
                    return @{ Status = 'FAIL'; Detail = ("the bot exited {0}; see artifacts/ci/sim.log" -f $run.Code); Log = $log }
                }
                if (-not (Test-Path -LiteralPath $csv)) {
                    return @{ Status = 'FAIL'; Detail = 'the bot wrote no balance CSV'; Log = $log }
                }

                # The CSV starts with `##` comment lines, so the header is found by its first
                # column name rather than assumed to be line 1.
                $lines = @(Get-Content -LiteralPath $csv)
                $headerIndex = -1
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    if ($lines[$i] -match '^seed,') { $headerIndex = $i; break }
                }
                if ($headerIndex -lt 0) {
                    return @{ Status = 'FAIL'; Detail = 'the balance CSV has no header row'; Log = $log }
                }

                $rows = @(($lines[$headerIndex..($lines.Count - 1)] -join "`n") | ConvertFrom-Csv)
                $bossRows = @($rows | Where-Object { $_.is_boss -eq 'true' })
                if ($bossRows.Count -ne $Seeds) {
                    return @{ Status = 'FAIL'; Detail = ("{0} of {1} seeds reached the boss floor" -f $bossRows.Count, $Seeds); Log = $log }
                }

                $won = @($bossRows | Where-Object { $_.outcome -eq 'Won' }).Count
                $rate = [math]::Round($won / [double]$Seeds, 4)
                $floors = @($rows | Where-Object { $_.is_boss -ne 'true' })

                $summary = [pscustomobject]@{
                    seeds        = $Seeds
                    configuration = $Configuration
                    bossRows     = $bossRows.Count
                    bossWins     = $won
                    bossWinRate  = $rate
                    min          = $MinBossWinRate
                    max          = $MaxBossWinRate
                    floorRows    = $floors.Count
                    generatedAt  = (Get-Date).ToString('o')
                }
                if (-not $NoWrite) {
                    $summary | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'balance-summary.json') -Encoding UTF8
                }

                if ($rate -lt $MinBossWinRate -or $rate -gt $MaxBossWinRate) {
                    return @{ Status = 'FAIL'; Detail = ("boss win rate {0:P1} is outside {1:P0}-{2:P0}; see artifacts/ci/balance-50.csv" -f $rate, $MinBossWinRate, $MaxBossWinRate); Log = $log }
                }

                return @{ Status = 'PASS'; Detail = ("{0}/{1} boss wins = {2:P1} ({3} floor rows)" -f $won, $Seeds, $rate, $rows.Count); Log = $log }
            } | Out-Null
        }
    }

    # -----------------------------------------------------------------------
    # Licence jobs. From here on the project belongs to this script: every one of
    # `unity`, `build` and `web` refuses to start while another Editor holds it.
    # -----------------------------------------------------------------------
    $licenseJobs = @($requested | Where-Object { $_ -in @('Unity', 'Build', 'Web') })
    if ($licenseJobs.Count -gt 0) {
        [void](Clear-StaleUnityLock)
        $inUse = Test-ProjectInUse
        if ($inUse) {
            $why = ("{0}; close it and run again (Unity cannot share UnityProject)" -f $inUse)
            foreach ($jobName in $licenseJobs) {
                $name = switch ($jobName) {
                    'Unity' { 'unity: EditMode + PlayMode' }
                    'Build' { 'build: WebGL player' }
                    'Web' { 'web: Playwright' }
                }
                Add-Step $name $jobName 'NOT RUN' $why
            }
        }
        else {
            if ($requested -notcontains 'Unity') {
                # A licence job was asked for, but not the Unity tests: the build and the browser
                # suite do not need them, and running them here would make a `-Job Build` take ten
                # minutes of something it was not asked for.
            }
            elseif (-not $unity) {
                Add-Step 'unity: EditMode + PlayMode' 'Unity' 'NOT RUN' 'the unity CLI is not on PATH'
                $script:Blocked = $true
            }
            else {
                # EditMode runs as one suite: it is green as a suite.
                Invoke-Step 'unity: EditMode' 'Unity' {
                    $xml = Join-Path $script:ArtifactDir 'editmode.xml'
                    if (Test-Path -LiteralPath $xml) { Remove-Item -LiteralPath $xml -Force }

                    # `unity test` runs its own Editor, which is why the licence jobs require
                    # the project to be closed. It writes NUnit XML, which is the artifact a
                    # later reader (or a CI dashboard) can parse without this script.
                    $run = Invoke-Native $unity @(
                        'test', $script:ProjectDir, '--mode', 'EditMode', '--output', $xml
                    )
                    $log = Write-Log 'unity-editmode.log' $run.Output

                    if (-not (Test-Path -LiteralPath $xml)) {
                        return @{ Status = 'FAIL'; Detail = ("the EditMode run wrote no report (exit {0}); see artifacts/ci/unity-editmode.log" -f $run.Code); Log = $log }
                    }

                    try {
                        $doc = New-Object System.Xml.XmlDocument
                        $doc.Load($xml)
                    }
                    catch {
                        return @{ Status = 'FAIL'; Detail = ('the report could not be read: {0}' -f $_.Exception.Message); Log = $log }
                    }

                    $cases = @($doc.SelectNodes("//*[local-name()='test-case']"))
                    if ($cases.Count -eq 0) {
                        return @{ Status = 'FAIL'; Detail = 'the run reported no tests at all'; Log = $log }
                    }

                    $notPassed = @($cases | Where-Object { $_.result -ne 'Passed' -and $_.result -ne 'Skipped' -and $_.result -ne 'Inconclusive' })
                    if ($notPassed.Count -gt 0) {
                        $first = [string]$notPassed[0].name
                        return @{ Status = 'FAIL'; Detail = ("{0}/{1} passed; first failure: {2}; see artifacts/ci/editmode.xml" -f ($cases.Count - $notPassed.Count), $cases.Count, $first); Log = $log }
                    }

                    return @{ Status = 'PASS'; Detail = ("{0}/{1} passed; report artifacts/ci/editmode.xml" -f $cases.Count, $cases.Count); Log = $log }
                } | Out-Null

                # PlayMode runs **one fixture at a time**, which is the documented way this suite
                # is green (docs/testing/test-suites.md §7): a bulk run fails on Unity's own
                # ambient Input System / R3 log asserts — never on an assertion of ours — and the
                # failure cascades because a test that dies before teardown leaves the next
                # fixture a half-finished scene. Running each class in its own process is
                # therefore not a workaround for a flaky assertion; it is the only shape that
                # reports this suite's real verdict. It costs one Editor launch per fixture.
                $playModeDir = Join-Path $script:ProjectDir 'Assets\Main\Tests\PlayMode'
                $playModeClasses = @()
                if (Test-Path -LiteralPath $playModeDir) {
                    $playModeClasses = @(Get-ChildItem -LiteralPath $playModeDir -Filter '*PlayModeTests.cs' -File |
                        Sort-Object Name |
                        ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) })
                }

                if ($playModeClasses.Count -eq 0) {
                    Add-Step 'unity: PlayMode (per class)' 'Unity' 'NOT RUN' 'no *PlayModeTests.cs fixtures were found'
                    $script:Blocked = $true
                }
                else {
                    foreach ($className in $playModeClasses) {
                        $fixture = $className
                        Invoke-Step ("unity: PlayMode {0}" -f $fixture) 'Unity' {
                            $slug = $fixture.ToLowerInvariant()
                            $xml = Join-Path $script:ArtifactDir ("playmode-{0}.xml" -f $slug)
                            if (Test-Path -LiteralPath $xml) { Remove-Item -LiteralPath $xml -Force }

                            $run = Invoke-Native $unity @(
                                'test', $script:ProjectDir, '--mode', 'PlayMode', '--filter', $fixture, '--output', $xml
                            )
                            $log = Write-Log ("unity-playmode-{0}.log" -f $slug) $run.Output

                            if (-not (Test-Path -LiteralPath $xml)) {
                                return @{ Status = 'FAIL'; Detail = ("the run wrote no report (exit {0}); see artifacts/ci/unity-playmode-{1}.log" -f $run.Code, $slug); Log = $log }
                            }

                            try {
                                $doc = New-Object System.Xml.XmlDocument
                                $doc.Load($xml)
                            }
                            catch {
                                return @{ Status = 'FAIL'; Detail = ('the report could not be read: {0}' -f $_.Exception.Message); Log = $log }
                            }

                            $cases = @($doc.SelectNodes("//*[local-name()='test-case']"))
                            if ($cases.Count -eq 0) {
                                return @{ Status = 'FAIL'; Detail = ("no test matched {0}; see artifacts/ci/playmode-{1}.xml" -f $fixture, $slug); Log = $log }
                            }

                            $notPassed = @($cases | Where-Object { $_.result -ne 'Passed' -and $_.result -ne 'Skipped' -and $_.result -ne 'Inconclusive' })
                            if ($notPassed.Count -gt 0) {
                                $first = [string]$notPassed[0].name
                                return @{ Status = 'FAIL'; Detail = ("{0}/{1} passed; first failure: {2}; see artifacts/ci/playmode-{3}.xml" -f ($cases.Count - $notPassed.Count), $cases.Count, $first, $slug); Log = $log }
                            }

                            return @{ Status = 'PASS'; Detail = ("{0}/{0} passed; report artifacts/ci/playmode-{1}.xml" -f $cases.Count, $slug); Log = $log }
                        } | Out-Null
                    }
                }
            }

            if ($requested -contains 'Build') {
                Invoke-Step ('build: WebGL player ({0})' -f $Configuration) 'Build' {
                    $inUseAgain = Test-ProjectInUse
                    if ($inUseAgain) {
                        return @{ Status = 'NOT RUN'; Detail = ('{0}; the build needs the project closed' -f $inUseAgain) }
                    }

                    $run = Invoke-Tool 'build-web.ps1' @('-Configuration', $Configuration, '-Force')
                    $log = Write-Log 'build-web.log' $run.Output

                    if ($run.Code -eq 2) {
                        return @{ Status = 'NOT RUN'; Detail = ('build-web.ps1 could not run; see artifacts/ci/build-web.log'); Log = $log }
                    }
                    if (-not $run.Ok) {
                        return @{ Status = 'FAIL'; Detail = 'the WebGL build failed; see artifacts/ci/build-web.log'; Log = $log }
                    }

                    $index = Join-Path $script:WebRoot 'index.html'
                    $wasm = @(Get-ChildItem -LiteralPath $script:WebRoot -Recurse -Filter *.wasm -File -ErrorAction SilentlyContinue)
                    if (-not (Test-Path -LiteralPath $index) -or $wasm.Count -lt 1) {
                        return @{ Status = 'FAIL'; Detail = ('the build produced no player at {0}' -f $script:WebRoot); Log = $log }
                    }

                    # The configuration the pipeline asked for is the one that has to be on disk:
                    # the release-absence spec would pass vacuously against a Development build.
                    $info = Join-Path $script:WebRoot 'build-info.json'
                    if (Test-Path -LiteralPath $info) {
                        Copy-Item -LiteralPath $info -Destination (Join-Path $script:ArtifactDir 'build-info.json') -Force
                        $metadata = Get-Content -LiteralPath $info -Raw | ConvertFrom-Json
                        $expectedDevelopment = ($Configuration -eq 'Development')
                        if ([bool]$metadata.development -ne $expectedDevelopment) {
                            return @{ Status = 'FAIL'; Detail = ('the player on disk is development={0}, but {1} was requested' -f $metadata.development, $Configuration); Log = $log }
                        }
                    }

                    return @{ Status = 'PASS'; Detail = ('{0} -> {1}; metadata artifacts/ci/build-info.json' -f $Configuration, $script:WebRoot); Log = $log }
                } | Out-Null
            }

            if ($requested -contains 'Web') {
                Invoke-Step 'web: Playwright (built player)' 'Web' {
                    if (-not (Test-Path -LiteralPath (Join-Path $script:WebRoot 'index.html'))) {
                        return @{ Status = 'NOT RUN'; Detail = ('there is no player at {0}; run -Job Build first' -f $script:WebRoot) }
                    }
                    if (-not $node -or -not $npx) {
                        return @{ Status = 'NOT RUN'; Detail = 'node/npx is not on PATH' }
                    }

                    $testsDir = Join-Path $script:Root 'tests\web'
                    if (-not (Test-Path -LiteralPath (Join-Path $testsDir 'node_modules'))) {
                        return @{ Status = 'NOT RUN'; Detail = 'tests/web/node_modules is missing; run npm install in tests/web' }
                    }

                    $specs = @(
                        'smoke.spec.ts', 'title.spec.ts', 'battle.spec.ts', 'camp.spec.ts',
                        'result.spec.ts', 'persistence.spec.ts', 'audio.spec.ts', 'pause.spec.ts'
                    )
                    if ($Configuration -eq 'Release') {
                        $specs += 'release.spec.ts'
                    }
                    else {
                        $specs += 'harness.spec.ts'
                    }

                    $serveLog = Join-Path $script:ArtifactDir 'serve-web.log'
                    $server = $null
                    $previousJson = $env:PLAYWRIGHT_JSON_OUTPUT_NAME
                    $previousBuild = $env:PROJECTSURVIVE_BUILD
                    $env:PLAYWRIGHT_JSON_OUTPUT_NAME = Join-Path $script:ArtifactDir 'playwright.json'
                    $env:PROJECTSURVIVE_BUILD = if ($Configuration -eq 'Release') { 'release' } else { 'development' }
                    try {
                        $server = Start-Process -FilePath $node -ArgumentList @(
                            (Join-Path $PSScriptRoot 'serve-web.mjs'), '--root', $script:WebRoot, '--port', "$WebPort"
                        ) -PassThru -NoNewWindow -RedirectStandardOutput $serveLog -RedirectStandardError ($serveLog + '.err')

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
                            # The usual cause is a server somebody left holding the port (EADDRINUSE). That is
                            # reported, not worked around: a stale server would serve a different build, and a
                            # suite that passed against it would be evidence of nothing. stderr is its own
                            # file here, so the message names it - the stdout log is empty in this case.
                            $reason = ''
                            $errLog = $serveLog + '.err'
                            if ((Test-Path -LiteralPath $errLog) -and (Get-Content -LiteralPath $errLog -Raw) -match 'EADDRINUSE') {
                                $reason = ('; port {0} is already in use (stop that process, or pass -WebPort)' -f $WebPort)
                            }
                            return @{ Status = 'NOT RUN'; Detail = ('tools/serve-web.mjs did not start{0}; see artifacts/ci/serve-web.log.err' -f $reason) }
                        }

                        $playwright = Invoke-Native $npx (@('playwright', 'test', '--reporter', 'list,json') + $specs) $testsDir
                        $log = Write-Log 'playwright.log' $playwright.Output

                        $passedCount = 0
                        $failedCount = 0
                        foreach ($line in @($playwright.Output -split "`r?`n")) {
                            if ($line -match '(\d+)\s+passed') { $passedCount = [int]$Matches[1] }
                            if ($line -match '(\d+)\s+failed') { $failedCount = [int]$Matches[1] }
                        }

                        # The HTML report and the failure screenshots/traces travel with the
                        # result: a browser failure is read from these, not from the log.
                        foreach ($pair in @(
                                @{ Source = (Join-Path $testsDir 'test-results'); Name = 'web-test-results' },
                                @{ Source = (Join-Path $testsDir 'playwright-report'); Name = 'web-report' }
                            )) {
                            if (-not (Test-Path -LiteralPath $pair.Source)) { continue }
                            $destination = Join-Path $script:ArtifactDir $pair.Name
                            try {
                                if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
                                Copy-Item -LiteralPath $pair.Source -Destination $destination -Recurse -Force
                            }
                            catch {
                                Write-Host ('  (warning) could not copy {0}: {1}' -f $pair.Name, $_.Exception.Message) -ForegroundColor Yellow
                            }
                        }

                        if ($failedCount -gt 0 -or -not $playwright.Ok) {
                            return @{ Status = 'FAIL'; Detail = ('{0} passed, {1} failed; see artifacts/ci/playwright.log' -f $passedCount, $failedCount); Log = $log }
                        }

                        return @{ Status = 'PASS'; Detail = ('{0} passed in Chrome on port {1}' -f $passedCount, $WebPort); Log = $log }
                    }
                    finally {
                        $env:PLAYWRIGHT_JSON_OUTPUT_NAME = $previousJson
                        $env:PROJECTSURVIVE_BUILD = $previousBuild
                        if ($server -and -not $server.HasExited) {
                            try { Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue } catch { }
                        }
                    }
                } | Out-Null
            }
        }
    }
}
finally {
    if (-not $NoWrite -and (Test-Path -LiteralPath $script:LockPath)) {
        Remove-Item -LiteralPath $script:LockPath -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Report: PASS, FAIL and NOT RUN are counted and listed separately.
# ---------------------------------------------------------------------------
$failed = @($script:Results | Where-Object { $_.Status -eq 'FAIL' })
$notRun = @($script:Results | Where-Object { $_.Status -eq 'NOT RUN' })
$passed = @($script:Results | Where-Object { $_.Status -eq 'PASS' })
$totalSeconds = [math]::Round((@($script:Results | Measure-Object -Property Seconds -Sum).Sum), 1)

# A NOT RUN step is not a pass, but it is not a red test either: it is a gate that could not be
# evaluated, and the exit code says so (2) rather than pretending either way.
$exitCode = 0
if ($failed.Count -gt 0) { $exitCode = 1 }
elseif ($notRun.Count -gt 0 -or $script:HarnessError) { $exitCode = 2 }

$verdict = switch ($exitCode) {
    0 { 'OK' }
    1 { 'FAILED' }
    default { 'BLOCKED' }
}

Write-Host ('=' * 78)
Write-Host ("  PASS {0}  |  FAIL {1}  |  NOT RUN {2}   ({3} s of steps)" -f $passed.Count, $failed.Count, $notRun.Count, $totalSeconds)
foreach ($r in @($script:Results | Where-Object { $_.Status -ne 'PASS' })) {
    Write-Host ('  [{0,-7}] {1,-30} {2}' -f $r.Status, $r.Name, $r.Detail) -ForegroundColor $(if ($r.Status -eq 'FAIL') { 'Red' } else { 'DarkGray' })
}
Write-Host ("  RESULT: {0}" -f $verdict) -ForegroundColor $(if ($exitCode -eq 0) { 'Green' } else { 'Red' })
Write-Host ''

if (-not $NoWrite) {
    $report = [pscustomobject]@{
        tool          = 'ci.ps1'
        jobs          = @($requested)
        configuration = $Configuration
        generatedAt   = (Get-Date).ToString('o')
        machine       = $env:COMPUTERNAME
        exitCode      = $exitCode
        totalSeconds  = $totalSeconds
        summary       = [pscustomobject]@{
            pass   = $passed.Count
            fail   = $failed.Count
            notRun = $notRun.Count
        }
        steps         = @($script:Results)
    }

    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'summary.json') -Encoding UTF8

    $lines = @(
        ('# CI summary - {0}' -f $verdict)
        ''
        ('- when: `{0}`' -f $report.generatedAt)
        ('- machine: `{0}`' -f $report.machine)
        ('- jobs: `{0}`' -f ($requested -join ', '))
        ('- configuration: `{0}`' -f $Configuration)
        ('- result: **{0}** - PASS {1} | FAIL {2} | NOT RUN {3}' -f $verdict, $passed.Count, $failed.Count, $notRun.Count)
        ''
        '| Step | Job | Status | Seconds | Detail |'
        '| --- | --- | --- | --- | --- |'
    ) + @($script:Results | ForEach-Object {
            ('| {0} | {1} | **{2}** | {3:N1} | {4} |' -f $_.Name, $_.Job, $_.Status, $_.Seconds, ($_.Detail -replace '\|', '\|'))
        })

    if ($notRun.Count -gt 0) {
        $lines += @('', 'NOT RUN is not a pass: each line above says what was missing.')
    }

    $lines | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'summary.md') -Encoding UTF8
}

exit $exitCode
