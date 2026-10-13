<#
.SYNOPSIS
    ProjectSurvive environment / dependency health check (Stage 00).

.DESCRIPTION
    Verifies that this machine can build and test the project, and reports a
    RELIABLE exit code:

        0  = every REQUIRED check passed
        1  = at least one REQUIRED check failed
        2  = the doctor itself could not run (unexpected internal error)

    Optional checks (Git LFS, Firefox, Playwright, npm) may fail without
    changing the exit code; they are reported as WARN so they are not forgotten.

    NO SECRETS ARE PRINTED. The Unity batchmode probe reads the Editor log only
    to extract the license *product name* (e.g. "Unity Personal"); the license
    id, serial, and the raw log are never echoed - the log is written to
    artifacts/doctor/ for local inspection and is git-ignored.

.PARAMETER Unity
    Also run the slow Unity batchmode license probe (launches Unity, ~30-90s).
    Off by default so doctor stays fast.

.PARAMETER UnityPath
    Explicit path to Unity.exe. Overrides auto-detection.

.PARAMETER Json
    Emit the machine-readable report to stdout as well as the summary.

.PARAMETER NoWrite
    Do not write report files under artifacts/.

.EXAMPLE
    pwsh -NoProfile -File tools/doctor.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/doctor.ps1 -Unity
#>
[CmdletBinding()]
param(
    [switch]$Unity,
    [string]$UnityPath,
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
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

# Run a native command, return its trimmed stdout+stderr, never throw.
function Invoke-Native {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @()
    )
    try {
        $out = & $FilePath @Arguments 2>&1
        $code = $LASTEXITCODE
        return [pscustomobject]@{
            Ok     = ($code -eq 0)
            Code   = $code
            Output = (($out | Out-String).Trim())
            Missing = $false
        }
    }
    catch {
        return [pscustomobject]@{ Ok = $false; Code = -1; Output = ''; Missing = $true }
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

# ---------------------------------------------------------------------------
# 1. Operating system
# ---------------------------------------------------------------------------
try {
    $os = Get-CimInstance Win32_OperatingSystem -ErrorAction Stop
    if ($os.Caption -match 'Windows') {
        Add-Result 'OS' 'Required' 'PASS' ("{0} {1} (build {2}) {3}" -f $os.Caption, $os.Version, $os.BuildNumber, $os.OSArchitecture)
    }
    else {
        Add-Result 'OS' 'Required' 'FAIL' ("expected Windows, found '{0}'" -f $os.Caption)
    }
}
catch {
    Add-Result 'OS' 'Required' 'FAIL' ("could not read OS info: {0}" -f $_.Exception.Message)
}

# ---------------------------------------------------------------------------
# 2. PowerShell
# ---------------------------------------------------------------------------
$psVer = $PSVersionTable.PSVersion
if ($psVer.Major -ge 5) {
    Add-Result 'PowerShell' 'Required' 'PASS' ("{0} {1} ({2})" -f $PSVersionTable.PSEdition, $psVer, $PSVersionTable.Platform)
}
else {
    Add-Result 'PowerShell' 'Required' 'FAIL' ("need >= 5.1, found {0}" -f $psVer)
}

# ---------------------------------------------------------------------------
# 3. Git + Git LFS
# ---------------------------------------------------------------------------
$git = Find-CommandPath 'git'
if ($git) {
    $r = Invoke-Native $git @('--version')
    if ($r.Ok) { Add-Result 'Git' 'Required' 'PASS' $r.Output }
    else { Add-Result 'Git' 'Required' 'FAIL' ("git --version failed: {0}" -f $r.Output) }
}
else {
    Add-Result 'Git' 'Required' 'FAIL' 'git not found on PATH'
}

# Git LFS: optional for the toolchain, required before large binary assets land.
$lfs = Find-CommandPath 'git-lfs'
$lfsOut = $null
if ($lfs) {
    $r = Invoke-Native $lfs @('version')
    if ($r.Ok) { $lfsOut = $r.Output }
}
if (-not $lfsOut -and $git) {
    $r = Invoke-Native $git @('lfs', 'version')
    if ($r.Ok) { $lfsOut = $r.Output }
}
if ($lfsOut) {
    Add-Result 'Git LFS' 'Optional' 'PASS' $lfsOut
}
else {
    Add-Result 'Git LFS' 'Optional' 'WARN' 'not installed; required before png/psd/fbx/wav/ogg assets are committed'
}

# ---------------------------------------------------------------------------
# 4. .NET SDK
# ---------------------------------------------------------------------------
$dotnet = Find-CommandPath 'dotnet'
if ($dotnet) {
    $r = Invoke-Native $dotnet @('--list-sdks')
    if ($r.Ok -and $r.Output) {
        $sdks = @($r.Output -split "`r?`n" | Where-Object { $_ -match '\S' })
        $major = 0
        foreach ($s in $sdks) {
            if ($s -match '^(\d+)\.') { if ([int]$Matches[1] -gt $major) { $major = [int]$Matches[1] } }
        }
        if ($major -ge 8) {
            Add-Result '.NET SDK' 'Required' 'PASS' (($sdks -join '; '))
        }
        else {
            Add-Result '.NET SDK' 'Required' 'FAIL' ("need an SDK >= 8 for net8.0/net9.0 tests; found: {0}" -f ($sdks -join '; '))
        }
    }
    else {
        Add-Result '.NET SDK' 'Required' 'FAIL' 'dotnet --list-sdks produced no output'
    }
}
else {
    Add-Result '.NET SDK' 'Required' 'FAIL' 'dotnet not found on PATH'
}

# ---------------------------------------------------------------------------
# 5. Node.js + npm
# ---------------------------------------------------------------------------
$node = Find-CommandPath 'node'
if ($node) {
    $r = Invoke-Native $node @('--version')
    if ($r.Ok) { Add-Result 'Node.js' 'Required' 'PASS' $r.Output }
    else { Add-Result 'Node.js' 'Required' 'FAIL' ("node --version failed: {0}" -f $r.Output) }
}
else {
    Add-Result 'Node.js' 'Required' 'FAIL' 'node not found on PATH'
}

$npm = Find-CommandPath 'npm'
if ($npm) {
    $r = Invoke-Native $npm @('--version')
    if ($r.Ok) { Add-Result 'npm' 'Optional' 'PASS' $r.Output }
    else { Add-Result 'npm' 'Optional' 'WARN' 'npm present but --version failed' }
}
else {
    Add-Result 'npm' 'Optional' 'WARN' 'npm not found; needed for the Playwright suite (Stage 03/14)'
}

# ---------------------------------------------------------------------------
# 6. Unity Editor (6000.3.x) + WebGL module
# ---------------------------------------------------------------------------
function Get-UnityEditors {
    $found = New-Object System.Collections.ArrayList
    $roots = New-Object System.Collections.ArrayList
    if ($env:ProgramFiles) { [void]$roots.Add((Join-Path $env:ProgramFiles 'Unity\Hub\Editor')) }
    [void]$roots.Add('C:\Program Files\Unity\Hub\Editor')
    foreach ($root in ($roots | Select-Object -Unique)) {
        if (Test-Path -LiteralPath $root) {
            Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                $exe = Join-Path $_.FullName 'Editor\Unity.exe'
                if (Test-Path -LiteralPath $exe) {
                    [void]$found.Add([pscustomobject]@{ Version = $_.Name; Exe = $exe })
                }
            }
        }
    }
    return $found
}

$unityExe = $null
$unityVersionLabel = $null

if ($UnityPath) {
    if (Test-Path -LiteralPath $UnityPath) {
        $unityExe = (Resolve-Path -LiteralPath $UnityPath).Path
        $unityVersionLabel = (Get-Item -LiteralPath $unityExe).VersionInfo.ProductVersion
        Add-Result 'Unity Editor' 'Required' 'PASS' ("explicit path: {0} ({1})" -f $unityExe, $unityVersionLabel)
    }
    else {
        Add-Result 'Unity Editor' 'Required' 'FAIL' ("-UnityPath not found: {0}" -f $UnityPath)
    }
}
else {
    $editors = @(Get-UnityEditors)
    $candidates = @($editors | Where-Object { $_.Version -match '^6000\.3\.' })
    if ($candidates.Count -eq 0) {
        if ($editors.Count -gt 0) {
            Add-Result 'Unity Editor' 'Required' 'FAIL' ("no 6000.3.x editor found; present: {0}" -f (($editors | ForEach-Object { $_.Version }) -join ', '))
        }
        else {
            Add-Result 'Unity Editor' 'Required' 'FAIL' 'no Unity Editor detected under Program Files\Unity\Hub\Editor'
        }
    }
    else {
        $sorted = $candidates | Sort-Object { [version](($_.Version -replace '^(\d+\.\d+\.\d+).*', '$1')) }
        $chosen = $sorted | Select-Object -Last 1
        $unityExe = $chosen.Exe
        $vi = (Get-Item -LiteralPath $unityExe).VersionInfo
        $unityVersionLabel = $vi.ProductVersion
        if ($chosen.Version -match '^6000\.3\.') {
            Add-Result 'Unity Editor' 'Required' 'PASS' ("{0} ({1})" -f $chosen.Version, $vi.ProductVersion)
        }
        else {
            Add-Result 'Unity Editor' 'Required' 'FAIL' $chosen.Version
        }
    }
}

# WebGL build support module (required for the Web target)
if ($unityExe) {
    $enginesRoot = Join-Path (Split-Path -Parent $unityExe) 'Data\PlaybackEngines'
    $webgl = Join-Path $enginesRoot 'WebGLSupport'
    $webglProg = Join-Path $webgl 'WebGLPlayerBuildProgram.exe'
    if ((Test-Path -LiteralPath $webgl) -and (Test-Path -LiteralPath $webglProg)) {
        Add-Result 'WebGL Build Support' 'Required' 'PASS' 'WebGLSupport + WebGLPlayerBuildProgram.exe present'
    }
    elseif (Test-Path -LiteralPath $webgl) {
        Add-Result 'WebGL Build Support' 'Required' 'WARN' 'WebGLSupport folder present but WebGLPlayerBuildProgram.exe missing'
    }
    else {
        Add-Result 'WebGL Build Support' 'Required' 'FAIL' ("WebGLSupport module missing under {0}" -f $enginesRoot)
    }
}
else {
    Add-Result 'WebGL Build Support' 'Required' 'SKIP' 'Unity Editor not located'
}

# ---------------------------------------------------------------------------
# 7. Repository structure sanity
# ---------------------------------------------------------------------------
$expectedDirs = @('lib', 'lib\Runtime', 'data', 'docs', 'docs\gdd', 'docs\architecture', 'docs\adr', 'docs\stages', 'llmwiki', 'tools', 'proto', 'tests\web', 'UnityProject')
$missingDirs = @()
foreach ($d in $expectedDirs) {
    if (-not (Test-Path -LiteralPath (Join-Path $script:Root $d))) { $missingDirs += $d }
}
if ($missingDirs.Count -eq 0) {
    Add-Result 'Repo structure' 'Required' 'PASS' ("root = {0}" -f $script:Root)
}
else {
    Add-Result 'Repo structure' 'Required' 'FAIL' ("missing: {0}" -f ($missingDirs -join ', '))
}

$expectedFiles = @('AGENTS.md', 'knowledge.md', 'tools\versions.lock.json', 'tools\doctor.ps1', 'docs\gdd\prototype-v0.3.md', 'docs\architecture\overview.md', 'docs\architecture\dependencies.md', 'llmwiki\glossary.md')
$missingFiles = @()
foreach ($f in $expectedFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $script:Root $f))) { $missingFiles += $f }
}
if ($missingFiles.Count -eq 0) {
    Add-Result 'Core documents' 'Required' 'PASS' ($expectedFiles.Count.ToString() + ' files present')
}
else {
    Add-Result 'Core documents' 'Required' 'FAIL' ("missing: {0}" -f ($missingFiles -join ', '))
}

# ---------------------------------------------------------------------------
# 8. Browsers
# ---------------------------------------------------------------------------
function Get-BrowserInfo {
    $list = New-Object System.Collections.ArrayList
    $candidates = @(
        @{ Name = 'Google Chrome'; Path = (Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe') },
        @{ Name = 'Google Chrome'; Path = (Join-Path ${env:ProgramFiles(x86)} 'Google\Chrome\Application\chrome.exe') },
        @{ Name = 'Google Chrome'; Path = (Join-Path $env:LOCALAPPDATA 'Google\Chrome\Application\chrome.exe') },
        @{ Name = 'Microsoft Edge'; Path = (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe') },
        @{ Name = 'Microsoft Edge'; Path = (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe') },
        @{ Name = 'Mozilla Firefox'; Path = (Join-Path $env:ProgramFiles 'Mozilla Firefox\firefox.exe') },
        @{ Name = 'Mozilla Firefox'; Path = (Join-Path ${env:ProgramFiles(x86)} 'Mozilla Firefox\firefox.exe') }
    )
    foreach ($c in $candidates) {
        if ($c.Path -and (Test-Path -LiteralPath $c.Path)) {
            [void]$list.Add([pscustomobject]@{ Name = $c.Name; Path = $c.Path; Version = (Get-Item -LiteralPath $c.Path).VersionInfo.ProductVersion })
        }
    }
    return $list
}

$browsers = @(Get-BrowserInfo)
$chrome = $browsers | Where-Object { $_.Name -eq 'Google Chrome' } | Select-Object -First 1
$firefox = $browsers | Where-Object { $_.Name -eq 'Mozilla Firefox' } | Select-Object -First 1
if ($chrome) {
    Add-Result 'Browser (Chromium)' 'Required' 'PASS' ("{0} {1}" -f $chrome.Name, $chrome.Version)
}
else {
    $edge = $browsers | Where-Object { $_.Name -eq 'Microsoft Edge' } | Select-Object -First 1
    if ($edge) {
        Add-Result 'Browser (Chromium)' 'Required' 'WARN' ("Chrome not found; Edge {0} available as fallback" -f $edge.Version)
    }
    else {
        Add-Result 'Browser (Chromium)' 'Required' 'FAIL' 'no Chromium browser (Chrome/Edge) found'
    }
}
if ($firefox) {
    Add-Result 'Browser (Gecko)' 'Optional' 'PASS' ("Mozilla Firefox {0}" -f $firefox.Version)
}
else {
    Add-Result 'Browser (Gecko)' 'Optional' 'WARN' 'Firefox not installed; cross-browser results must be reported NOT RUN'
}

# ---------------------------------------------------------------------------
# 9. Playwright (optional until Stage 03/14)
# ---------------------------------------------------------------------------
if ($npm) {
    $r = Invoke-Native $npm @('ls', '--global', '--depth=0', '@playwright/test')
    if ($r.Ok -and $r.Output -match '@playwright') {
        Add-Result 'Playwright' 'Optional' 'PASS' 'installed globally'
    }
    else {
        Add-Result 'Playwright' 'Optional' 'SKIP' 'not installed yet (added at Stage 03/14)'
    }
}
else {
    Add-Result 'Playwright' 'Optional' 'SKIP' 'npm unavailable'
}

# ---------------------------------------------------------------------------
# 10. Unity batchmode license probe (slow, opt-in)
# ---------------------------------------------------------------------------
$unityLogPath = $null
if (-not $Unity) {
    Add-Result 'Unity batchmode' 'Required' 'SKIP' 'not run; pass -Unity to launch Unity and prove batchmode permission'
}
elseif (-not $unityExe) {
    Add-Result 'Unity batchmode' 'Required' 'FAIL' 'cannot probe: Unity Editor not located'
}
else {
    $artifactDir = Join-Path $script:Root 'artifacts\doctor'
    if (-not (Test-Path -LiteralPath $artifactDir)) { [void](New-Item -ItemType Directory -Path $artifactDir -Force) }
    $unityLogPath = [System.IO.Path]::GetFullPath((Join-Path $artifactDir 'unity-batchmode.log'))
    if (Test-Path -LiteralPath $unityLogPath) { Remove-Item -LiteralPath $unityLogPath -Force -ErrorAction SilentlyContinue }

    $argLine = "-batchmode -nographics -quit -logFile `"$unityLogPath`""
    $exitCode = $null
    $timedOut = $false
    try {
        $proc = Start-Process -FilePath $unityExe -ArgumentList $argLine -PassThru -WindowStyle Hidden
        $finished = $proc.WaitForExit(240000)
        if (-not $finished) {
            $timedOut = $true
            try { $proc.Kill() } catch { }
        }
        else {
            $exitCode = $proc.ExitCode
        }
    }
    catch {
        Add-Result 'Unity batchmode' 'Required' 'FAIL' ("could not launch Unity: {0}" -f $_.Exception.Message)
    }

    if ($null -ne $exitCode -or $timedOut) {
        if ($timedOut) {
            Add-Result 'Unity batchmode' 'Required' 'FAIL' 'Unity did not exit within 240s'
        }
        else {
            # Extract ONLY the non-secret license product name.
            $licenseProduct = $null
            if (Test-Path -LiteralPath $unityLogPath) {
                try {
                    $match = Select-String -LiteralPath $unityLogPath -Pattern '^\s*Product:\s*(.+)$' -ErrorAction SilentlyContinue |
                        Select-Object -First 1
                    if ($match) { $licenseProduct = $match.Matches[0].Groups[1].Value.Trim() }
                }
                catch { }
            }
            if ($exitCode -eq 0) {
                $detail = 'exit 0'
                if ($licenseProduct) { $detail += ("; license product: {0}" -f $licenseProduct) }
                Add-Result 'Unity batchmode' 'Required' 'PASS' $detail
            }
            else {
                $detail = ("exit {0}" -f $exitCode)
                if ($licenseProduct) { $detail += ("; license product: {0}" -f $licenseProduct) }
                Add-Result 'Unity batchmode' 'Required' 'FAIL' $detail
            }
        }
    }
}

# ---------------------------------------------------------------------------
# 11. artifacts/ writable
# ---------------------------------------------------------------------------
try {
    $artifactsDir = Join-Path $script:Root 'artifacts'
    if (-not (Test-Path -LiteralPath $artifactsDir)) { [void](New-Item -ItemType Directory -Path $artifactsDir -Force) }
    $probe = Join-Path $artifactsDir '.write-probe'
    Set-Content -LiteralPath $probe -Value 'ok' -Encoding ASCII -ErrorAction Stop
    Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
    Add-Result 'artifacts/ writable' 'Optional' 'PASS' $artifactsDir
}
catch {
    Add-Result 'artifacts/ writable' 'Optional' 'WARN' ("not writable: {0}" -f $_.Exception.Message)
}

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
$failed = @($script:Results | Where-Object { $_.Status -eq 'FAIL' -and $_.Level -eq 'Required' })
$warned = @($script:Results | Where-Object { $_.Status -eq 'WARN' })
$skipped = @($script:Results | Where-Object { $_.Status -eq 'SKIP' })

$exitCode = 0
if ($failed.Count -gt 0) { $exitCode = 1 }

if (-not $Json) {
    Write-Host ''
    Write-Host 'ProjectSurvive - Doctor (Stage 00)' -ForegroundColor Cyan
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
        (@($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count), $failed.Count, $warned.Count, $skipped.Count)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (all required checks passed)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (a required check failed)' -ForegroundColor Red
    }
    if (-not $Unity) {
        Write-Host '  Note: Unity batchmode probe was skipped. Re-run with -Unity to verify it.' -ForegroundColor DarkGray
    }
    Write-Host ''
}

# Machine-readable report
$report = [pscustomobject]@{
    tool        = 'doctor.ps1'
    stage       = '00'
    generatedAt = (Get-Date).ToString('o')
    machine     = $env:COMPUTERNAME
    root        = $script:Root
    unityProbe  = [bool]$Unity
    exitCode    = $exitCode
    summary     = [pscustomobject]@{
        pass = @($script:Results | Where-Object { $_.Status -eq 'PASS' }).Count
        fail = $failed.Count
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
        $artifactDir = Join-Path $script:Root 'artifacts\doctor'
        if (-not (Test-Path -LiteralPath $artifactDir)) { [void](New-Item -ItemType Directory -Path $artifactDir -Force) }
        $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $artifactDir 'doctor-report.json') -Encoding UTF8
        $lines = foreach ($r in $script:Results) { ('[{0}] {1,-24} {2}' -f $r.Status, $r.Name, $r.Detail) }
        $txt = @(
            ('generatedAt: {0}' -f $report.generatedAt),
            ('exitCode: {0}' -f $exitCode),
            ('unityProbe: {0}' -f $report.unityProbe),
            ''
        ) + $lines
        $txt | Set-Content -LiteralPath (Join-Path $artifactDir 'doctor-report.txt') -Encoding UTF8
    }
    catch {
        Write-Host ("  (warning) could not write report files: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
