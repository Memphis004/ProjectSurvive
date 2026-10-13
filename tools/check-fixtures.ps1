#Requires -Version 5.1
<#
.SYNOPSIS
    Prove that broken data/ is rejected - and rejected for the right reason.

.DESCRIPTION
    Each directory under lib/Tests~/fixtures/ is a copy of data/ with ONE table
    overlaid by a deliberately broken version, plus a fixture.json saying which
    stage must catch it:

      "luban"     the schema rejects it (column type, unique key, #ref, range).
                  Expected: tools/gen.ps1 exits non-zero and writes nothing.
      "semantic"  Luban accepts the schema, but a GAME rule is violated - the
                  kind of rule that only spans tables (a floor with no wave, a
                  cooldown below the configured minimum). Expected: gen.ps1
                  succeeds, then tools/validate-data.ps1 exits non-zero and the
                  Data.Validator reports the expected finding codes.

    The unmodified data/ is run first as the control, so a fixture can never
    "pass" because the whole toolchain is broken.

    Why both halves matter: a rule that is only tested with a hand-built object
    proves the C# rule, not the pipeline. ConfigRulesTests.cs covers the rules at
    unit speed; this script covers data/ -> Luban -> .bytes -> findings.

.PARAMETER Only
    Run a single fixture by directory name.

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.PARAMETER NoWrite
    Do not write the report under artifacts/.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-fixtures.ps1 -Only price-zero

.NOTES
    Exit codes (same contract as tools/doctor.ps1 and tools/test.ps1):
      0 = every fixture failed exactly as expected
      1 = a fixture was accepted, or failed with the wrong reason
      2 = the script itself could not run (toolchain or temp directory missing)
#>
[CmdletBinding()]
param(
    [string]$Only = '',
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:ArtifactDir = Join-Path $script:Root 'artifacts\config'
$script:Results = New-Object System.Collections.ArrayList

$fixturesDir = Join-Path $script:Root 'lib\Tests~\fixtures'
$dataDir = Join-Path $script:Root 'data'
$genScript = Join-Path $PSScriptRoot 'gen.ps1'
$validateScript = Join-Path $PSScriptRoot 'validate-data.ps1'
$validatorProject = Join-Path $script:Root 'lib\Tests~\Data.Validator\ProjectSurvive.Data.Validator.csproj'
$validatorDll = Join-Path $script:Root 'lib\Tests~\Data.Validator\bin\Release\net9.0\ProjectSurvive.Data.Validator.dll'

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('Required', 'Optional')][string]$Level,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'WARN', 'SKIP')][string]$Status,
        [string]$Detail = ''
    )
    [void]$script:Results.Add([pscustomobject]@{ Name = $Name; Level = $Level; Status = $Status; Detail = $Detail })
}

# Runs a native command and returns its exit code plus merged output. stderr goes
# to a temp file: PowerShell 5.1 turns a redirected native stderr stream into a
# terminating error when $ErrorActionPreference is Stop, and we want the code.
function Invoke-Native {
    param([Parameter(Mandatory)][string]$FilePath, [string[]]$Arguments = @())

    $out = Join-Path ([System.IO.Path]::GetTempPath()) ('ps-out-' + [guid]::NewGuid().ToString('N') + '.txt')
    $err = Join-Path ([System.IO.Path]::GetTempPath()) ('ps-err-' + [guid]::NewGuid().ToString('N') + '.txt')
    try {
        & $FilePath @Arguments > $out 2> $err
        $code = $LASTEXITCODE
        $text = (@(
                Get-Content -LiteralPath $out -ErrorAction SilentlyContinue
                Get-Content -LiteralPath $err -ErrorAction SilentlyContinue
            ) | Out-String).Trim()
        return [pscustomobject]@{ Code = $code; Output = $text }
    }
    finally {
        Remove-Item -LiteralPath $out, $err -Force -ErrorAction SilentlyContinue
    }
}

function Copy-Overlay {
    param(
        [Parameter(Mandatory)][string]$Source,
        [Parameter(Mandatory)][string]$Destination
    )

    Get-ChildItem -LiteralPath $Source -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($Source.Length).TrimStart('\', '/')
        $target = Join-Path $Destination $relative
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) {
            [void](New-Item -ItemType Directory -Path $parent -Force)
        }

        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

$work = Join-Path ([System.IO.Path]::GetTempPath()) ('ps-fixtures-' + [guid]::NewGuid().ToString('N'))
$exitCode = 0

if (-not $Json) {
    Write-Host ''
    Write-Host 'ProjectSurvive - broken-data fixtures (Stage 02)' -ForegroundColor Cyan
    Write-Host ('=' * 72)
}

try {
    if (-not (Test-Path -LiteralPath $fixturesDir)) {
        Add-Result 'fixtures directory' 'Required' 'FAIL' ("missing {0}" -f $fixturesDir)
        throw 'no fixtures'
    }

    [void](New-Item -ItemType Directory -Path $work -Force)

    # -------------------------------------------------------------------
    # toolchain
    # -------------------------------------------------------------------
    $lubanExe = Join-Path $PSScriptRoot 'vendor\luban\5.1.0\Luban\Luban.exe'
    if (-not (Test-Path -LiteralPath $lubanExe)) {
        $bootstrap = Invoke-Native -FilePath 'powershell' -Arguments @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $PSScriptRoot 'setup-tools.ps1')
        )
        if (-not (Test-Path -LiteralPath $lubanExe)) {
            Add-Result 'pinned toolchain' 'Required' 'FAIL' 'Luban is not installed and setup-tools.ps1 did not provide it'
            Write-Host $bootstrap.Output -ForegroundColor DarkGray
            throw 'no toolchain'
        }
    }

    Add-Result 'pinned toolchain' 'Required' 'PASS' 'Luban + protoc present under tools/vendor'

    # The rule codes are read from the validator itself, so build it once here
    # rather than paying a `dotnet run` per fixture.
    if (-not (Test-Path -LiteralPath $validatorDll)) {
        $build = Invoke-Native -FilePath 'dotnet' -Arguments @(
            'build', $validatorProject, '-c', 'Release', '--nologo', '-v', 'quiet')
        if ($build.Code -ne 0) {
            Add-Result 'validator binary' 'Required' 'FAIL' ('could not build the validator: ' + $build.Output)
            throw 'no validator'
        }
    }

    Add-Result 'validator binary' 'Required' 'PASS' 'lib/Tests~/Data.Validator (Release)'

    # -------------------------------------------------------------------
    # control: the shipped data/ must generate and validate cleanly
    # -------------------------------------------------------------------
    $controlData = Join-Path $work 'control'
    $controlOut = Join-Path $work 'control-out'
    Copy-Item -LiteralPath $dataDir -Destination $controlData -Recurse -Force

    $controlGen = Invoke-Native -FilePath 'powershell' -Arguments @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $genScript,
        '-DataDir', $controlData, '-OutDir', $controlOut, '-SkipBootstrap', '-Quiet', '-NoWrite'
    )
    if ($controlGen.Code -ne 0) {
        Add-Result 'control: data/ generates' 'Required' 'FAIL' ("gen.ps1 exited {0}: {1}" -f $controlGen.Code, $controlGen.Output)
    }
    else {
        Add-Result 'control: data/ generates' 'Required' 'PASS' 'unmodified data/ accepted'
    }

    $controlValidate = Invoke-Native -FilePath 'powershell' -Arguments @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $validateScript,
        '-DataDir', (Join-Path $controlOut 'data'), '-NoWrite'
    )
    if ($controlValidate.Code -ne 0) {
        Add-Result 'control: data/ validates' 'Required' 'FAIL' ("validate-data.ps1 exited {0}" -f $controlValidate.Code)
    }
    else {
        Add-Result 'control: data/ validates' 'Required' 'PASS' 'unmodified data/ has no findings'
    }

    # -------------------------------------------------------------------
    # the fixtures
    # -------------------------------------------------------------------
    $cases = @(Get-ChildItem -LiteralPath $fixturesDir -Directory | Sort-Object -Property Name)
    if ($Only) {
        $cases = @($cases | Where-Object { $_.Name -eq $Only })
        if ($cases.Count -eq 0) {
            Add-Result ('fixture: ' + $Only) 'Required' 'FAIL' 'no such fixture directory'
            throw 'unknown fixture'
        }
    }

    foreach ($case in $cases) {
        $name = $case.Name
        $manifestPath = Join-Path $case.FullName 'fixture.json'
        if (-not (Test-Path -LiteralPath $manifestPath)) {
            Add-Result ('fixture: ' + $name) 'Required' 'FAIL' 'fixture.json is missing'
            continue
        }

        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $stage = [string]$manifest.stage
        $expected = @($manifest.expectedCodes | Where-Object { $_ })

        $stageData = Join-Path $work ($name + '-data')
        $stageOut = Join-Path $work ($name + '-out')
        Copy-Item -LiteralPath $dataDir -Destination $stageData -Recurse -Force
        Copy-Overlay -Source $case.FullName -Destination $stageData
        Remove-Item -LiteralPath (Join-Path $stageData 'fixture.json') -Force -ErrorAction SilentlyContinue

        $gen = Invoke-Native -FilePath 'powershell' -Arguments @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $genScript,
            '-DataDir', $stageData, '-OutDir', $stageOut, '-SkipBootstrap', '-Quiet', '-NoWrite'
        )

        if ($stage -eq 'luban') {
            if ($gen.Code -eq 0) {
                Add-Result ('fixture: ' + $name) 'Required' 'FAIL' (
                    'Luban accepted data that violates the schema - ' + [string]$manifest.why)
            }
            else {
                Add-Result ('fixture: ' + $name) 'Required' 'PASS' (
                    ("rejected by Luban (exit {0}): {1}" -f $gen.Code, [string]$manifest.why))
            }

            continue
        }

        if ($stage -ne 'semantic') {
            Add-Result ('fixture: ' + $name) 'Required' 'FAIL' (
                "fixture.json stage must be 'luban' or 'semantic', found '" + $stage + "'")
            continue
        }

        if ($gen.Code -ne 0) {
            Add-Result ('fixture: ' + $name) 'Required' 'FAIL' (
                "Luban rejected a fixture that is meant to be schema-valid (exit $($gen.Code)): $($gen.Output)")
            continue
        }

        $generated = Join-Path $stageOut 'data'
        $validate = Invoke-Native -FilePath 'powershell' -Arguments @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $validateScript,
            '-DataDir', $generated, '-NoWrite'
        )
        if ($validate.Code -eq 0) {
            Add-Result ('fixture: ' + $name) 'Required' 'FAIL' (
                'validate-data.ps1 accepted data that breaks a game rule - ' + [string]$manifest.why)
            continue
        }

        $validator = Invoke-Native -FilePath 'dotnet' -Arguments @('exec', $validatorDll, '--data', $generated)
        $reported = @()
        foreach ($line in ($validator.Output -split "`r?`n")) {
            if ($line -match '^\s*\[FAIL\]\s*(\S+)') { $reported += $Matches[1] }
        }

        $seen = if ($reported.Count -gt 0) { $reported -join ', ' } else { 'no findings at all' }
        $missing = @($expected | Where-Object { $reported -notcontains $_ })
        if ($missing.Count -gt 0) {
            Add-Result ('fixture: ' + $name) 'Required' 'FAIL' (
                "expected [{0}] but the validator reported [{1}]" -f ($expected -join ', '), $seen)
        }
        else {
            Add-Result ('fixture: ' + $name) 'Required' 'PASS' (
                "rejected by validate-data.ps1: " + ($expected -join ', '))
        }
    }
}
catch {
    if ($script:Results.Count -eq 0) {
        Write-Host ("ERROR: {0}" -f $_.Exception.Message) -ForegroundColor Red
        $exitCode = 2
    }
}
finally {
    if (Test-Path -LiteralPath $work) {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$failed = @($script:Results | Where-Object { $_.Status -eq 'FAIL' -and $_.Level -eq 'Required' })
$warned = @($script:Results | Where-Object { $_.Status -eq 'WARN' })
$skipped = @($script:Results | Where-Object { $_.Status -eq 'SKIP' })
if ($failed.Count -gt 0) { $exitCode = 1 }

if (-not $Json) {
    Write-Host ('=' * 72)
    foreach ($r in $script:Results) {
        $color = 'Gray'
        switch ($r.Status) {
            'PASS' { $color = 'Green' }
            'FAIL' { $color = 'Red' }
            'WARN' { $color = 'Yellow' }
            'SKIP' { $color = 'DarkGray' }
        }
        Write-Host ('  [{0}] {1,-34} {2}' -f $r.Status, $r.Name, $r.Detail) -ForegroundColor $color
    }
    Write-Host ('=' * 72)
    Write-Host ('  PASS {0}  |  FAIL {1}  |  WARN {2}  |  SKIP {3}' -f `
        @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count, $failed.Count, $warned.Count, $skipped.Count)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (every broken fixture is rejected)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (a broken fixture was accepted, or the reason did not match)' -ForegroundColor Red
    }
    Write-Host ''
}

$report = [pscustomobject]@{
    tool        = 'check-fixtures.ps1'
    stage       = '02'
    generatedAt = (Get-Date).ToString('o')
    machine     = $env:COMPUTERNAME
    exitCode    = $exitCode
    summary     = [pscustomobject]@{
        pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count
        fail = $failed.Count
        warn = $warned.Count
        skip = $skipped.Count
    }
    checks      = @($script:Results)
}

if ($Json) { $report | ConvertTo-Json -Depth 6 }
if (-not $NoWrite) {
    try {
        if (-not (Test-Path -LiteralPath $script:ArtifactDir)) {
            [void](New-Item -ItemType Directory -Path $script:ArtifactDir -Force)
        }
        $report | ConvertTo-Json -Depth 6 |
            Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'check-fixtures-report.json') -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write the report: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
