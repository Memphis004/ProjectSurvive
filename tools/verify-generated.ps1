<#
.SYNOPSIS
    Prove lib/Runtime/Generated is reproducible from data/ and proto/ (Stage 02).

.DESCRIPTION
    Acceptance criterion: "generate twice from the same input and `git diff` is
    empty". This script is the machine check for it, and it is stronger than
    `git diff` alone because it also covers the git-ignored .bytes tables:

      1. run tools/gen.ps1 into a throwaway directory, from the SAME data/ and
         proto/ the working tree was generated from
      2. compare every generated .cs against lib/Runtime/Generated/Luban
         (content, plus "no file left over on either side")
      3. compare every .bytes against lib/Tests~/data AND
         UnityProject/Assets/Main/Resources/Config
      4. compare the protoc output (App.cs) and confirm App.cs exists exactly once
      5. recompute the config hash from the freshly generated tables and confirm
         the tracked ConfigBuildMetadata.g.cs already carries it
      6. report the tracked generated paths that are not committed (a WARN, not a
         failure: that is a repository-hygiene signal, not a reproducibility one)

    Run this after touching data/ or the proto, before committing.

.PARAMETER Configuration
    Build configuration used to run the data validator. Debug (default) / Release.

.PARAMETER SkipGit
    Skip check 6 (useful outside a git checkout).

.PARAMETER Json
    Also emit the machine-readable report to stdout.

.PARAMETER NoWrite
    Do not write the report under artifacts/.

.EXAMPLE
    pwsh -NoProfile -File tools/verify-generated.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-generated.ps1 -Json

.NOTES
    Exit codes (same contract as tools/doctor.ps1 and tools/test.ps1):
      0 = generated output is reproducible and current
      1 = a required check failed
      2 = the script itself could not run
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$SkipGit,
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:ArtifactDir = Join-Path $script:Root 'artifacts\config'
$script:Results = New-Object System.Collections.ArrayList

$script:CodeDir = Join-Path $script:Root 'lib\Runtime\Generated\Luban'
$script:TestDataDir = Join-Path $script:Root 'lib\Tests~\data'
$script:UnityDataDir = Join-Path $script:Root 'UnityProject\Assets\Main\Resources\Config'
$script:ProtoDir = Join-Path $script:Root 'lib\Runtime\Generated\Proto'
$script:DataDir = Join-Path $script:Root 'data'

function Add-Result {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][ValidateSet('Required', 'Optional')][string]$Level,
        [Parameter(Mandatory)][ValidateSet('PASS', 'FAIL', 'WARN', 'SKIP')][string]$Status,
        [string]$Detail = ''
    )
    [void]$script:Results.Add([pscustomobject]@{ Name = $Name; Level = $Level; Status = $Status; Detail = $Detail })
}

function Invoke-Native {
    param([Parameter(Mandatory)][string]$FilePath, [string[]]$Arguments = @())
    $out = & $FilePath @Arguments 2>&1
    return [pscustomobject]@{ Code = $LASTEXITCODE; Output = (($out | Out-String).Trim()) }
}

function Get-Sha256File {
    param([Parameter(Mandatory)][string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try { $hash = $sha.ComputeHash($stream) } finally { $stream.Dispose() }
        return ([System.BitConverter]::ToString($hash)).Replace('-', '')
    }
    finally { $sha.Dispose() }
}

# Compares two directories of same-named artifacts; returns a list of problems.
function Compare-Directory {
    param(
        [Parameter(Mandatory)][string]$Expected,   # freshly generated
        [Parameter(Mandatory)][string]$Actual,     # committed / tracked
        [Parameter(Mandatory)][string]$Filter,
        [string[]]$IgnoreNames = @()
    )

    $problems = New-Object System.Collections.ArrayList
    if (-not (Test-Path -LiteralPath $Actual)) {
        [void]$problems.Add("$Actual does not exist")
        return $problems
    }

    $expectedFiles = @(Get-ChildItem -LiteralPath $Expected -Filter $Filter -File |
            Where-Object { $IgnoreNames -notcontains $_.Name })
    $actualFiles = @(Get-ChildItem -LiteralPath $Actual -Filter $Filter -File |
            Where-Object { $IgnoreNames -notcontains $_.Name })

    foreach ($file in $expectedFiles) {
        $target = Join-Path $Actual $file.Name
        if (-not (Test-Path -LiteralPath $target)) {
            [void]$problems.Add("missing: $($file.Name)")
            continue
        }
        if ((Get-Sha256File -Path $file.FullName) -ne (Get-Sha256File -Path $target)) {
            [void]$problems.Add("differs: $($file.Name)")
        }
    }

    foreach ($file in $actualFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $Expected $file.Name))) {
            [void]$problems.Add("stale (no longer generated): $($file.Name)")
        }
    }

    return $problems
}

$staging = Join-Path ([System.IO.Path]::GetTempPath()) ('ps-verify-' + [System.Guid]::NewGuid().ToString('N'))
$exitCode = 0

if (-not $Json) {
    Write-Host ''
    Write-Host 'ProjectSurvive - verify generated output (Stage 02)' -ForegroundColor Cyan
    Write-Host ('=' * 72)
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $script:DataDir 'luban.conf'))) {
        Add-Result 'data directory' 'Required' 'FAIL' ("missing {0}" -f $script:DataDir)
        throw 'data/ is not usable'
    }

    # -----------------------------------------------------------------------
    # 1. regenerate into a throwaway directory
    # -----------------------------------------------------------------------
    $genScript = Join-Path $PSScriptRoot 'gen.ps1'
    $gen = Invoke-Native -FilePath 'powershell' -Arguments @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $genScript,
        '-DataDir', $script:DataDir, '-OutDir', $staging, '-SkipBootstrap', '-Quiet', '-NoWrite'
    )

    if ($gen.Code -ne 0) {
        Add-Result 'regenerate' 'Required' 'FAIL' ("gen.ps1 exited {0}: {1}" -f $gen.Code, $gen.Output)
        throw 'regeneration failed'
    }

    Add-Result 'regenerate' 'Required' 'PASS' 'into a temporary directory'

    # -----------------------------------------------------------------------
    # 2. generated C# (tracked)
    # -----------------------------------------------------------------------
    $problems = Compare-Directory -Expected (Join-Path $staging 'Luban') -Actual $script:CodeDir -Filter '*.cs'
    if ($problems.Count -eq 0) {
        Add-Result 'generated C# is current' 'Required' 'PASS' (
            "{0} file(s) identical" -f @(Get-ChildItem -LiteralPath (Join-Path $staging 'Luban') -Filter '*.cs').Count)
    }
    else {
        Add-Result 'generated C# is current' 'Required' 'FAIL' ($problems -join '; ')
    }

    # -----------------------------------------------------------------------
    # 3. .bytes tables (git-ignored) - against BOTH publish targets
    # -----------------------------------------------------------------------
    foreach ($target in @(
            @{ Name = 'lib/Tests~/data'; Path = $script:TestDataDir; Level = 'Required' },
            @{ Name = 'Resources/Config'; Path = $script:UnityDataDir; Level = 'Required' }
        )) {
        if (-not (Test-Path -LiteralPath $target.Path)) {
            Add-Result ("tables: " + $target.Name) $target.Level 'SKIP' 'directory not present yet'
            continue
        }

        $tables = Compare-Directory -Expected (Join-Path $staging 'data') -Actual $target.Path -Filter '*.bytes'
        if ($tables.Count -eq 0) {
            Add-Result ("tables: " + $target.Name) $target.Level 'PASS' (
                "{0} file(s) identical" -f @(Get-ChildItem -LiteralPath (Join-Path $staging 'data') -Filter '*.bytes').Count)
        }
        else {
            Add-Result ("tables: " + $target.Name) $target.Level 'FAIL' ($tables -join '; ')
        }
    }

    # -----------------------------------------------------------------------
    # 4. protoc output, and "App.cs exists exactly once"
    # -----------------------------------------------------------------------
    $expectedProto = Join-Path $staging 'Proto\App.cs'
    if (-not (Test-Path -LiteralPath $expectedProto)) {
        Add-Result 'protoc output' 'Required' 'FAIL' 'regeneration produced no Proto/App.cs'
    }
    else {
        $protoProblems = Compare-Directory -Expected (Join-Path $staging 'Proto') -Actual $script:ProtoDir -Filter '*.cs'
        if ($protoProblems.Count -eq 0) {
            Add-Result 'protoc output is current' 'Required' 'PASS' 'App.cs identical'
        }
        else {
            Add-Result 'protoc output is current' 'Required' 'FAIL' ($protoProblems -join '; ')
        }

        $appCopies = @(Get-ChildItem -LiteralPath $script:Root -Recurse -Filter 'App.cs' -File |
                Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' })
        if ($appCopies.Count -eq 1) {
            Add-Result 'App.cs has exactly one home' 'Required' 'PASS' (
                $appCopies[0].FullName.Replace($script:Root + '\', ''))
        }
        else {
            Add-Result 'App.cs has exactly one home' 'Required' 'FAIL' (
                "found {0}: {1}" -f $appCopies.Count, (($appCopies | ForEach-Object { $_.FullName }) -join '; '))
        }
    }

    # -----------------------------------------------------------------------
    # 5. config hash in build metadata matches the generated tables
    # -----------------------------------------------------------------------
    $metadataPath = Join-Path $script:CodeDir 'ConfigBuildMetadata.g.cs'
    if (-not (Test-Path -LiteralPath $metadataPath)) {
        Add-Result 'config hash metadata' 'Required' 'FAIL' 'ConfigBuildMetadata.g.cs is missing'
    }
    else {
        $validatorDll = Join-Path $script:Root (
            "lib\Tests~\Data.Validator\bin\{0}\net9.0\ProjectSurvive.Data.Validator.dll" -f $Configuration)
        if (-not (Test-Path -LiteralPath $validatorDll)) {
            $build = Invoke-Native -FilePath 'dotnet' -Arguments @(
                'build', (Join-Path $script:Root 'lib\Tests~\Data.Validator\ProjectSurvive.Data.Validator.csproj'),
                '-c', $Configuration, '--nologo', '-v', 'quiet')
            if ($build.Code -ne 0) {
                Add-Result 'config hash metadata' 'Required' 'FAIL' ('validator build failed: ' + $build.Output)
                $validatorDll = $null
            }
        }

        if ($validatorDll -and (Test-Path -LiteralPath $validatorDll)) {
            $metadata = Get-Content -LiteralPath $metadataPath -Raw
            $match = [regex]::Match($metadata, 'ConfigHash\s*=\s*"([0-9A-Fa-f]{64})"')
            $validation = Invoke-Native -FilePath 'dotnet' -Arguments @(
                'exec', $validatorDll, '--data', (Join-Path $staging 'data')
            )
            $hashMatch = [regex]::Match($validation.Output, 'configHash:\s*([0-9A-Fa-f]{64})')

            if (-not $match.Success) {
                Add-Result 'config hash metadata' 'Required' 'FAIL' 'no 64-hex ConfigHash in the generated metadata'
            }
            elseif (-not $hashMatch.Success) {
                Add-Result 'config hash metadata' 'Required' 'FAIL' ('could not read the hash of the generated tables: ' + $validation.Output)
            }
            elseif ($match.Groups[1].Value.ToUpperInvariant() -ne $hashMatch.Groups[1].Value.ToUpperInvariant()) {
                Add-Result 'config hash metadata' 'Required' 'FAIL' (
                    "metadata {0} != tables {1}" -f $match.Groups[1].Value, $hashMatch.Groups[1].Value)
            }
            else {
                Add-Result 'config hash metadata' 'Required' 'PASS' ("{0}..." -f $match.Groups[1].Value.Substring(0, 16))

                if ($validation.Code -eq 0) {
                    Add-Result 'freshly generated tables validate' 'Required' 'PASS' 'no findings'
                }
                else {
                    Add-Result 'freshly generated tables validate' 'Required' 'FAIL' $validation.Output
                }
            }
        }
    }

    # -----------------------------------------------------------------------
    # 6. are the tracked generated paths committed? (hygiene, not reproducibility)
    # -----------------------------------------------------------------------
    if ($SkipGit) {
        Add-Result 'tracked output committed' 'Optional' 'SKIP' '-SkipGit'
    }
    else {
        $git = Invoke-Native -FilePath 'git' -Arguments @(
            '-C', $script:Root, 'status', '--porcelain', '--',
            'lib/Runtime/Generated', 'lib/Tests~/data', 'UnityProject/Assets/Main/Resources/Config'
        )
        if ($git.Code -ne 0) {
            Add-Result 'tracked output committed' 'Optional' 'SKIP' 'git unavailable'
        }
        elseif ([string]::IsNullOrWhiteSpace($git.Output)) {
            Add-Result 'tracked output committed' 'Optional' 'PASS' 'git status is clean for generated paths'
        }
        else {
            # Only a hygiene signal, so keep the detail readable: a fresh clone
            # with an un-populated data/ can legitimately list the whole tree.
            $changed = @($git.Output -split "`r?`n" | Where-Object { $_ -match '\S' })
            $shown = @($changed | Select-Object -First 6)
            if ($changed.Count -gt $shown.Count) {
                $shown += ("... and {0} more" -f ($changed.Count - $shown.Count))
            }
            Add-Result 'tracked output committed' 'Optional' 'WARN' (
                "{0} uncommitted generated path(s): {1}" -f $changed.Count, ($shown -join ', '))
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
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue }
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
        Write-Host ('  [{0}] {1,-32} {2}' -f $r.Status, $r.Name, $r.Detail) -ForegroundColor $color
    }
    Write-Host ('=' * 72)
    Write-Host ("  PASS {0}  |  FAIL {1}  |  WARN {2}  |  SKIP {3}" -f `
        @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count, $failed.Count, $warned.Count, $skipped.Count)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (generated output is reproducible)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (regeneration does not reproduce the tracked output)' -ForegroundColor Red
    }
    Write-Host ''
}

$report = [pscustomobject]@{
    tool        = 'verify-generated.ps1'
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
            Set-Content -LiteralPath (Join-Path $script:ArtifactDir 'verify-generated-report.json') -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write verify-generated-report.json: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
