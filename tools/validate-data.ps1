#Requires -Version 5.1
<#
.SYNOPSIS
    Lint the hand-authored Balancesheet in data/ against the config rules.

.DESCRIPTION
    Runs lib/Tests~/Data.Validator (a plain console app in the .NET test
    solution) over data/ and fails on any rule violation.

    Why a separate process: the rules need the generated C# table classes to
    read rows back through the same code path the game uses. That code lives in
    the dotnet test solution, not in PowerShell.

    The rules are split by what can enforce them, and this script only drives
    the second half:
      * data/ -> Luban (--strict, via tools/gen.ps1) owns the SCHEMA: column
        names, types, unique primary keys, `#ref` targets and `#(range=)` bounds.
      * the generated .bytes -> ProjectSurvive.Data.Validator owns the GAME
        rules that span tables, notably everything Luban cannot check because an
        empty cell is legitimate there: duplicate ids across item/bag, missing
        optional references (skill_id, description_key, boss_enemy_id), empty or
        ragged footprint pictures, cooldown below constant.min_cooldown_seconds,
        non-sequential or over-max merge tiers, a piece whose merge ladder is
        incomplete, a shop-pool weight band that leaves a floor with no usable
        entry, a piece no shop-pool band offers, a floor with no wave, a wave
        whose spawns cannot fit its window, a synergy tag no item carries,
        price <= 0, and localization rows missing a language.

    Exit code 0 = clean, 1 = violations, 2 = could not run (missing toolchain).

.EXAMPLE
    powershell -NoProfile -File tools/validate-data.ps1
#>
[CmdletBinding()]
param(
    [string]$DataDir = '',
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Resolve the repository root from this script's own location so the script is
# cwd-independent (it is called from repo root, from tools/, and from CI).
# ---------------------------------------------------------------------------
$script:Root = Split-Path -Parent $PSScriptRoot
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
# Inputs
# ---------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($DataDir)) {
    $DataDir = Join-Path $script:Root 'data'
}
elseif (-not [System.IO.Path]::IsPathRooted($DataDir)) {
    # A relative -DataDir is resolved against the repository root, not the
    # caller's cwd, so `-DataDir data/broken` behaves the same from anywhere.
    $DataDir = Join-Path $script:Root $DataDir
}
$DataDir = [System.IO.Path]::GetFullPath($DataDir)

$project = Join-Path $script:Root 'lib\Tests~\Data.Validator\ProjectSurvive.Data.Validator.csproj'

# ---------------------------------------------------------------------------
# 1. Prerequisites
# ---------------------------------------------------------------------------
$dotnet = $null
try {
    $c = Get-Command 'dotnet' -ErrorAction SilentlyContinue
    if ($null -ne $c) { $dotnet = $c.Source }
}
catch { }

if (-not $dotnet) {
    Add-Result '.NET SDK' 'Required' 'FAIL' 'dotnet not found on PATH'
}
else {
    $r = & $dotnet '--list-sdks' 2>&1
    if ($LASTEXITCODE -eq 0 -and $r) {
        # One line per SDK: "<version> [<path>]". Report every version, newest
        # last, because the validator targets net9.0 and needs an SDK >= 9.
        $versions = @($r | ForEach-Object { if ($_ -match '^\s*(\d+\.\d+\.\d+)') { $Matches[1] } })
        $major = 0
        foreach ($v in $versions) { if ([int]($v -split '\.')[0] -gt $major) { $major = [int]($v -split '\.')[0] } }
        if ($major -ge 9) {
            Add-Result '.NET SDK' 'Required' 'PASS' ($versions -join '; ')
        }
        else {
            Add-Result '.NET SDK' 'Required' 'FAIL' ("the validator targets net9.0; need an SDK >= 9, found: {0}" -f ($versions -join '; '))
        }
    }
    else {
        Add-Result '.NET SDK' 'Required' 'FAIL' 'dotnet --list-sdks failed'
    }
}

if (Test-Path -LiteralPath $project) {
    Add-Result 'validator project' 'Required' 'PASS' 'lib/Tests~/Data.Validator'
}
else {
    Add-Result 'validator project' 'Required' 'FAIL' ("missing {0}" -f $project)
}

if (Test-Path -LiteralPath $DataDir) {
    Add-Result 'data directory' 'Required' 'PASS' $DataDir
}
else {
    Add-Result 'data directory' 'Required' 'FAIL' ("missing {0}" -f $DataDir)
}

# ---------------------------------------------------------------------------
# 2. Run the validator
# ---------------------------------------------------------------------------
$validatorOutput = ''
$validatorFailed = $false

if ($dotnet -and (Test-Path -LiteralPath $project) -and (Test-Path -LiteralPath $DataDir)) {
    # No --no-restore: a clean checkout has no obj/ yet, so restore must run.
    # The validator's own contract: 0 = clean, 1 = findings, 2 = could not run.
    #
    # stdout/stderr go to temp files rather than `2>&1`: under
    # $ErrorActionPreference = 'Stop', PowerShell 5.1 turns a native command's
    # redirected stderr into a terminating error, which would abort the run
    # before its exit code is even read.
    $tmpOut = [System.IO.Path]::GetTempFileName()
    $tmpErr = [System.IO.Path]::GetTempFileName()
    try {
        & $dotnet 'run' '--project' $project '-c' 'Release' '--' '--data' $DataDir > $tmpOut 2> $tmpErr
        $validatorCode = $LASTEXITCODE

        $validatorOutput = (@(
                Get-Content -LiteralPath $tmpOut -ErrorAction SilentlyContinue
                Get-Content -LiteralPath $tmpErr -ErrorAction SilentlyContinue
            ) | Out-String).Trim()
    }
    finally {
        Remove-Item -LiteralPath $tmpOut, $tmpErr -Force -ErrorAction SilentlyContinue
    }
    Write-Artifact 'validate-data.log' $validatorOutput

    $lines = @($validatorOutput -split "`r?`n")
    # Each finding prints as '  [FAIL] <code>' then '         at <target>'.
    $findingLines = @($lines | Where-Object { $_ -match '^\s*\[FAIL\]\s*(\S+)' })
    $findingCount = $findingLines.Count
    $firstCode = if ($findingCount -gt 0) { $Matches[1] } else { '' }

    if ($validatorCode -eq 2) {
        $validatorFailed = $true
        Add-Result 'data rules' 'Required' 'FAIL' ("validator could not run (exit 2): {0}" -f (($lines | Where-Object { $_ -match '\S' } | Select-Object -First 1)))
    }
    elseif ($validatorCode -eq 0 -and $findingCount -eq 0) {
        Add-Result 'data rules' 'Required' 'PASS' (($lines | Where-Object { $_ -match 'RESULT:\s*OK' } | Select-Object -First 1) -replace '^\s+', '')
    }
    else {
        $validatorFailed = $true
        $firstMessage = ($lines | Where-Object { $_ -match '^\s+at\s' } | Select-Object -First 1) -replace '^\s+', ''
        Add-Result 'data rules' 'Required' 'FAIL' ("{0} finding(s); first {1} {2}" -f $findingCount, $firstCode, $firstMessage)
    }

    if ($validatorCode -eq 0 -and $findingCount -eq 0) {
        Add-Result 'violations' 'Required' 'PASS' '0'
    }
    elseif ($validatorCode -eq 0) {
        Add-Result 'violations' 'Required' 'WARN' ("validator exited 0 but printed {0} finding(s)" -f $findingCount)
    }
    else {
        Add-Result 'violations' 'Required' 'FAIL' ("{0} finding(s)" -f $findingCount)
    }
}
else {
    Add-Result 'data rules' 'Required' 'SKIP' 'prerequisites missing'
    Add-Result 'violations' 'Required' 'SKIP' 'prerequisites missing'
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
    Write-Host 'ProjectSurvive - validate data (Stage 02)' -ForegroundColor Cyan
    Write-Host ('=' * 72)
    foreach ($x in $script:Results) {
        $color = 'Gray'
        switch ($x.Status) {
            'PASS' { $color = 'Green' }
            'FAIL' { $color = 'Red' }
            'WARN' { $color = 'Yellow' }
            'SKIP' { $color = 'DarkGray' }
        }
        Write-Host ('  [{0}] {1,-20} {2}' -f $x.Status, $x.Name, $x.Detail) -ForegroundColor $color
    }
    Write-Host ('=' * 72)
    Write-Host ('  PASS {0}  |  FAIL {1}  |  WARN {2}  |  SKIP {3}' -f `
        (@($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count), $failedChecks.Count, $warned.Count, $skipped.Count)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (all required checks passed)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (a required check failed)' -ForegroundColor Red
    }
    if ($validatorOutput -and $validatorFailed) {
        Write-Host ''
        Write-Host '  validator output:' -ForegroundColor DarkGray
        foreach ($line in ($validatorOutput -split "`r?`n")) {
            Write-Host ('    ' + $line) -ForegroundColor DarkGray
        }
    }
    Write-Host ''
}

$report = [pscustomobject]@{
    tool        = 'validate-data.ps1'
    stage       = '02'
    generatedAt = (Get-Date).ToString('o')
    machine     = $env:COMPUTERNAME
    root        = $script:Root
    dataDir     = $DataDir
    exitCode    = $exitCode
    summary     = [pscustomobject]@{
        pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count
        fail = $failedChecks.Count
        warn = $warned.Count
        skip = $skipped.Count
    }
    checks      = @($script:Results)
}

if ($Json) {
    $report | ConvertTo-Json -Depth 6
}

if (-not $NoWrite) {
    try {
        if (-not (Test-Path -LiteralPath $script:ArtifactDir)) {
            [void](New-Item -ItemType Directory -Path $script:ArtifactDir -Force)
        }
        $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'validate-data.json') -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write report: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
