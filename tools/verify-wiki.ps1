#Requires -Version 5.1
<#
.SYNOPSIS
    Lint the LLM wiki (Stage 10e): reproducible content, links, ids, orphans.

.DESCRIPTION
    A structural check only - no LLM, no network, no writes inside the
    repository. The five checks are:

      a. content reproduces   run tools/gen-wiki.ps1 into a temporary directory
                              and compare every llmwiki/content/*.md byte for
                              byte (missing, differing and stale files all fail)
      b. links resolve        every relative markdown link under llmwiki/ must
                              point at a file that exists, and a '#fragment'
                              must name an explicit <a id="..."> or a heading
                              whose GitHub slug matches
      c. ids are covered      every id in item, bag, skill, synergy, enemy,
                              floor and upgrade_variant must have an <a id>
                              section in the matching content page; an anchor
                              that no data row names is a failure too
      d. notes are reachable  every .md under systems/ and gotchas/ must be
                              linked from llmwiki/index.md (no orphan page)
      e. balance numbers      WARN only: a hand-written page that restates a
                              skill's data number next to its name is flagged
                              as a heuristic, because it is not a hard error

    -Fixtures proves the lint can fail: each directory under
    tools/wiki-fixtures/ is a copy of the wiki with one deliberate defect and a
    fixture.json naming the finding codes that must appear. The check runs in a
    temporary copy, so nothing in the repository is written.

.PARAMETER WikiDir
    The wiki to lint. Default: <repo>/llmwiki.

.PARAMETER DataDir
    The source tables. Default: <repo>/data.

.PARAMETER Fixtures
    Also run tools/wiki-fixtures/ (proof that a broken wiki is rejected).

.PARAMETER Json
    Emit the machine-readable report to stdout instead of the human summary.

.PARAMETER WriteReport
    Write artifacts/wiki/verify-wiki-report.json. OFF by default: the lint is
    read-only and writes no file inside the repository.

.PARAMETER NoWrite
    Accepted for compatibility with the other tools; the report is already off
    unless -WriteReport is given.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify-wiki.ps1 -Fixtures

.NOTES
    Exit codes (the repository contract):
      0 = every required check passed (WARN findings do not fail the run)
      1 = at least one required check failed
      2 = the script itself could not run (wiki or data directory missing)
#>
[CmdletBinding()]
param(
    [string]$WikiDir = '',
    [string]$DataDir = '',
    [switch]$Fixtures,
    [switch]$Json,
    [switch]$WriteReport,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:Checks = New-Object System.Collections.Generic.List[object]
$script:Findings = New-Object System.Collections.Generic.List[object]
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not $WikiDir) { $WikiDir = Join-Path $script:Root 'llmwiki' }
if (-not $DataDir) { $DataDir = Join-Path $script:Root 'data' }

function Add-Check {
    param([string]$Name, [string]$Status, [string]$Detail)
    [void]$script:Checks.Add([pscustomobject]@{ name = $Name; status = $Status; detail = $Detail })
}

function Add-Finding {
    param([string]$Code, [string]$Detail, [string]$Severity = 'FAIL')
    [void]$script:Findings.Add([pscustomobject]@{ code = $Code; severity = $Severity; detail = $Detail })
}

function Get-RelPath {
    param([string]$Path)
    try {
        $full = [System.IO.Path]::GetFullPath($Path)
        if ($full.StartsWith($script:Root, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $full.Substring($script:Root.Length).TrimStart('\', '/')
        }
        return $full
    }
    catch {
        return $Path
    }
}

function Read-CsvRows {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw ("missing table: {0}" -f (Get-RelPath $Path))
    }
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $text = $text -replace '^##', '__row'
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($row in @(ConvertFrom-Csv -InputObject $text)) {
        foreach ($property in $row.PSObject.Properties) {
            if ($property.Name -eq '__row') { continue }
            if (-not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
                [void]$rows.Add($row)
                break
            }
        }
    }
    return $rows.ToArray()
}

function Get-Sha256 {
    param([string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $stream = [System.IO.File]::OpenRead($Path)
        try { $hash = $sha.ComputeHash($stream) } finally { $stream.Dispose() }
        return ([System.BitConverter]::ToString($hash)).Replace('-', '')
    }
    finally { $sha.Dispose() }
}

function Get-MarkdownLinks {
    param([string]$Path)
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    # An inline code span may show a markdown link as an example;
    # it is text, not a link.
    $text = [regex]::Replace($text, '`[^`]*`', '')
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($match in [regex]::Matches($text, '\[[^\]]*\]\(([^)\s]+)(?:\s+"[^"]*")?\)')) {
        [void]$found.Add($match.Groups[1].Value)
    }
    return $found.ToArray()
}

function Get-Slug {
    param([string]$Text)
    $value = $Text.Trim()
    $value = [regex]::Replace($value, '\[[^\]]*\]\([^)]*\)', '')
    $value = $value.ToLowerInvariant()
    $builder = New-Object System.Text.StringBuilder
    foreach ($ch in $value.ToCharArray()) {
        if ([char]::IsLetterOrDigit($ch) -or $ch -eq ' ' -or $ch -eq '-' -or $ch -eq '_') {
            [void]$builder.Append($ch)
        }
    }
    return ($builder.ToString().Trim(' ') -replace ' ', '-')
}

function Test-Fragment {
    param([string]$Path, [string]$Fragment)
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    if ($text -match [regex]::Escape('<a id="' + $Fragment + '">')) { return $true }
    foreach ($heading in [regex]::Matches($text, '(?m)^#{1,6}\s+(.+)$')) {
        if ((Get-Slug $heading.Groups[1].Value) -eq $Fragment) { return $true }
    }
    return $false
}

function Get-ContentAnchors {
    param([string]$Path)
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $found = New-Object System.Collections.Generic.List[string]
    foreach ($match in [regex]::Matches($text, '<a id="([^"]+)">')) {
        [void]$found.Add($match.Groups[1].Value)
    }
    return $found.ToArray()
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('ps-wiki-' + [guid]::NewGuid().ToString('N'))

try {
    if (-not (Test-Path -LiteralPath $WikiDir)) {
        Write-Host ('ERROR: wiki directory not found: {0}' -f $WikiDir) -ForegroundColor Red
        exit 2
    }
    if (-not (Test-Path -LiteralPath $DataDir)) {
        Write-Host ('ERROR: data directory not found: {0}' -f $DataDir) -ForegroundColor Red
        exit 2
    }

    $contentDir = Join-Path $WikiDir 'content'
    if (-not (Test-Path -LiteralPath $contentDir)) {
        Add-Finding 'content_missing' 'llmwiki/content/ does not exist; run tools/gen-wiki.ps1'
    }

    # ------------------------------------------------------------------
    # a. content reproduces from data/
    # ------------------------------------------------------------------
    [void](New-Item -ItemType Directory -Path $temp -Force)
    $generated = Join-Path $temp 'content'
    $genScript = Join-Path $PSScriptRoot 'gen-wiki.ps1'
    $genOutput = ''
    $genCode = 0
    if (-not (Test-Path -LiteralPath $genScript)) {
        Add-Finding 'generator_missing' 'tools/gen-wiki.ps1 is missing'
        Add-Check 'content reproduces' 'FAIL' 'tools/gen-wiki.ps1 is missing'
    }
    else {
        $genOutput = (& powershell -NoProfile -ExecutionPolicy Bypass -File $genScript -DataDir $DataDir -OutDir $generated -Quiet -NoWrite 2>&1 | Out-String).Trim()
        $genCode = $LASTEXITCODE
        if ($genCode -ne 0) {
            Add-Finding 'generate_failed' ('gen-wiki.ps1 exited {0}: {1}' -f $genCode, $genOutput)
            Add-Check 'content reproduces' 'FAIL' ('generator exited {0}' -f $genCode)
        }
        elseif (-not (Test-Path -LiteralPath $contentDir)) {
            Add-Check 'content reproduces' 'FAIL' 'llmwiki/content/ does not exist'
        }
        else {
            $expectedFiles = @(Get-ChildItem -LiteralPath $generated -Filter '*.md' -File)
            $actualFiles = @(Get-ChildItem -LiteralPath $contentDir -Filter '*.md' -File)
            $problems = New-Object System.Collections.Generic.List[string]

            foreach ($file in $expectedFiles) {
                $target = Join-Path $contentDir $file.Name
                if (-not (Test-Path -LiteralPath $target)) {
                    [void]$problems.Add(('missing: {0}' -f $file.Name))
                    Add-Finding 'content_differs' ('content/{0} is missing (generate it)' -f $file.Name)
                    continue
                }
                if ((Get-Sha256 $file.FullName) -ne (Get-Sha256 $target)) {
                    [void]$problems.Add(('differs: {0}' -f $file.Name))
                    Add-Finding 'content_differs' ('content/{0} differs from a fresh generation' -f $file.Name)
                }
            }

            foreach ($file in $actualFiles) {
                if (-not (Test-Path -LiteralPath (Join-Path $generated $file.Name))) {
                    [void]$problems.Add(('stale: {0}' -f $file.Name))
                    Add-Finding 'content_differs' ('content/{0} is not generated any more (stale)' -f $file.Name)
                }
            }

            if ($problems.Count -eq 0) {
                Add-Check 'content reproduces' 'PASS' ('{0} file(s) identical to a fresh run' -f $expectedFiles.Count)
            }
            else {
                Add-Check 'content reproduces' 'FAIL' ($problems -join '; ')
            }
        }
    }

    # ------------------------------------------------------------------
    # b. links and anchors
    # ------------------------------------------------------------------
    $wikiFiles = @(Get-ChildItem -LiteralPath $WikiDir -Recurse -Filter '*.md' -File |
            Sort-Object -Property FullName)
    $linkProblems = New-Object System.Collections.Generic.List[string]
    $linkCount = 0

    foreach ($file in $wikiFiles) {
        foreach ($target in (Get-MarkdownLinks $file.FullName)) {
            if ([string]::IsNullOrWhiteSpace($target)) { continue }
            if ($target -match '^(https?|mailto|tel|data):') { continue }

            $linkCount++
            $linkTarget = $target.Trim()
            if ($linkTarget.StartsWith('<') -and $linkTarget.EndsWith('>')) {
                $linkTarget = $linkTarget.Substring(1, $linkTarget.Length - 2)
            }

            $pathPart = $linkTarget
            $fragment = ''
            $hash = $linkTarget.IndexOf('#')
            if ($hash -ge 0) {
                $pathPart = $linkTarget.Substring(0, $hash)
                $fragment = $linkTarget.Substring($hash + 1)
            }

            if ([string]::IsNullOrWhiteSpace($pathPart)) {
                $resolved = $file.FullName
            }
            else {
                $resolved = Join-Path (Split-Path -Parent $file.FullName) $pathPart
            }

            try {
                $resolved = [System.IO.Path]::GetFullPath($resolved)
            }
            catch {
                [void]$linkProblems.Add(('{0} -> {1} (unresolvable)' -f (Get-RelPath $file.FullName), $target))
                Add-Finding 'link_missing' ('{0} links to an unresolvable target: {1}' -f (Get-RelPath $file.FullName), $target)
                continue
            }

            # artifacts/ is untracked output (screenshots, logs, reports run on demand),
            # so a wiki note may point at a file that only exists after a capture. Its
            # absence is not a documentation defect.
            $artifactsRoot = Join-Path $script:Root 'artifacts'
            if ($resolved.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) { continue }

            if (-not (Test-Path -LiteralPath $resolved)) {
                [void]$linkProblems.Add(('{0} -> {1}' -f (Get-RelPath $file.FullName), $target))
                Add-Finding 'link_missing' ('{0} links to a file that does not exist: {1}' -f (Get-RelPath $file.FullName), $target)
                continue
            }

            if (-not [string]::IsNullOrWhiteSpace($fragment) -and (Test-Path -LiteralPath $resolved -PathType Leaf)) {
                if ($resolved -like '*.md' -and -not (Test-Fragment $resolved $fragment)) {
                    [void]$linkProblems.Add(('{0} -> {1} (no such anchor)' -f (Get-RelPath $file.FullName), $target))
                    Add-Finding 'anchor_missing' ('{0} links to a missing anchor: {1}' -f (Get-RelPath $file.FullName), $target)
                }
            }
        }
    }

    if ($linkProblems.Count -eq 0) {
        Add-Check 'links resolve' 'PASS' ('{0} relative link(s) and their anchors resolve' -f $linkCount)
    }
    else {
        Add-Check 'links resolve' 'FAIL' ('{0} broken of {1}' -f $linkProblems.Count, $linkCount)
    }

    # ------------------------------------------------------------------
    # c. every data id has a section; every anchor names a data id
    # ------------------------------------------------------------------
    $idTables = @(
        @{ Table = 'item'; Page = 'items.md' },
        @{ Table = 'bag'; Page = 'items.md' },
        @{ Table = 'skill'; Page = 'skills.md' },
        @{ Table = 'synergy'; Page = 'synergies.md' },
        @{ Table = 'enemy'; Page = 'enemies.md' },
        @{ Table = 'floor'; Page = 'floors.md' },
        @{ Table = 'upgrade_variant'; Page = 'upgrades.md' }
    )

    $knownIds = New-Object System.Collections.Generic.List[string]
    $missingIds = New-Object System.Collections.Generic.List[string]
    foreach ($entry in $idTables) {
        $rows = @(Read-CsvRows (Join-Path $DataDir ('tables\' + $entry.Table + '.csv')))
        foreach ($row in $rows) {
            if ([string]::IsNullOrWhiteSpace([string]$row.id)) { continue }
            $knownIds.Add([string]$row.id)
            $pagePath = Join-Path $contentDir $entry.Page
            if (-not (Test-Path -LiteralPath $pagePath)) {
                $missingIds.Add(('{0} (no {1})' -f $row.id, $entry.Page))
                Add-Finding 'id_missing' ('{0} has no section: {1} is missing' -f $row.id, $entry.Page)
                continue
            }
            $anchors = @(Get-ContentAnchors $pagePath)
            if ($anchors -notcontains [string]$row.id) {
                $missingIds.Add([string]$row.id)
                Add-Finding 'id_missing' ('{0} has no <a id> section in content/{1}' -f $row.id, $entry.Page)
            }
        }
    }

    $unknownAnchors = New-Object System.Collections.Generic.List[string]
    if (Test-Path -LiteralPath $contentDir) {
        foreach ($file in @(Get-ChildItem -LiteralPath $contentDir -Filter '*.md' -File | Sort-Object -Property Name)) {
            foreach ($anchor in (Get-ContentAnchors $file.FullName)) {
                if ($knownIds -notcontains $anchor) {
                    $unknownAnchors.Add($anchor)
                    Add-Finding 'id_unknown' ('content/{0} declares anchor {1}, which no data row names' -f $file.Name, $anchor)
                }
            }
        }
    }

    if ($missingIds.Count -eq 0 -and $unknownAnchors.Count -eq 0) {
        Add-Check 'ids covered' 'PASS' ('{0} data id(s) have sections; no unknown anchor' -f $knownIds.Count)
    }
    else {
        Add-Check 'ids covered' 'FAIL' ('{0} missing, {1} unknown' -f $missingIds.Count, $unknownAnchors.Count)
    }

    # ------------------------------------------------------------------
    # d. every systems/ and gotchas/ note is reachable from index.md
    # ------------------------------------------------------------------
    $indexPath = Join-Path $WikiDir 'index.md'
    $reachable = New-Object System.Collections.Generic.List[string]
    if (Test-Path -LiteralPath $indexPath) {
        foreach ($target in (Get-MarkdownLinks $indexPath)) {
            if ($target -match '^(https?|mailto|tel|data):') { continue }
            $pathPart = $target.Split([char]'#')[0]
            if ([string]::IsNullOrWhiteSpace($pathPart)) { continue }
            try {
                $resolved = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $indexPath) $pathPart))
                [void]$reachable.Add($resolved.ToLowerInvariant())
            }
            catch { }
        }
    }

    $orphans = New-Object System.Collections.Generic.List[string]
    foreach ($folderName in @('systems', 'gotchas')) {
        $folder = Join-Path $WikiDir $folderName
        if (-not (Test-Path -LiteralPath $folder)) { continue }
        foreach ($file in @(Get-ChildItem -LiteralPath $folder -Recurse -Filter '*.md' -File | Sort-Object -Property FullName)) {
            if ($reachable -notcontains $file.FullName.ToLowerInvariant()) {
                $orphans.Add((Get-RelPath $file.FullName))
                Add-Finding 'orphan_page' ('{0} is not linked from llmwiki/index.md' -f (Get-RelPath $file.FullName))
            }
        }
    }

    if ($orphans.Count -eq 0) {
        Add-Check 'notes reachable' 'PASS' 'every systems/ and gotchas/ note is linked from index.md'
    }
    else {
        Add-Check 'notes reachable' 'FAIL' ($orphans -join '; ')
    }

    # ------------------------------------------------------------------
    # e. WARN: a hand-written page restating a skill's data numbers
    # ------------------------------------------------------------------
    $warnings = New-Object System.Collections.Generic.List[string]
    $skillRows = @(Read-CsvRows (Join-Path $DataDir 'tables\skill.csv'))
    $locPath = Join-Path $DataDir 'localization\localization.csv'
    $loc = @{}
    if (Test-Path -LiteralPath $locPath) {
        $locText = [System.IO.File]::ReadAllText($locPath, [System.Text.Encoding]::UTF8) -replace '^##', '__row'
        foreach ($row in @(ConvertFrom-Csv -InputObject $locText)) {
            if ($row.key) { $loc[[string]$row.key] = $row }
        }
    }

    if (Test-Path -LiteralPath $contentDir) {
        $handWritten = @(Get-ChildItem -LiteralPath $WikiDir -Recurse -Filter '*.md' -File |
                Where-Object { $_.FullName -notlike (Join-Path $contentDir '*') } |
                Sort-Object -Property FullName)

        foreach ($file in $handWritten) {
            $text = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
            foreach ($skill in $skillRows) {
                $id = [string]$skill.id
                $thai = ''
                $english = ''
                if ($loc.ContainsKey([string]$skill.name_key)) {
                    $thai = [string]$loc[[string]$skill.name_key].th
                    $english = [string]$loc[[string]$skill.name_key].en
                }

                $mentions = $text.Contains($id)
                if (-not $mentions -and $thai) { $mentions = $text.Contains($thai) }
                if (-not $mentions -and $english) { $mentions = $text.Contains($english) }
                if (-not $mentions) { continue }

                $fields = @(
                    @{ Name = 'cooldown'; Value = [string]$skill.cooldown_seconds },
                    @{ Name = 'damage'; Value = [string]$skill.damage },
                    @{ Name = 'mp cost'; Value = [string]$skill.mp_cost },
                    @{ Name = 'range'; Value = [string]$skill.range }
                )

                foreach ($field in $fields) {
                    $pattern = '(?i)' + [regex]::Escape($field.Name) + '[^\n]{0,18}?(\d+(?:\.\d+)?)'
                    foreach ($match in [regex]::Matches($text, $pattern)) {
                        $a = 0.0
                        $b = 0.0
                        $okA = [double]::TryParse($match.Groups[1].Value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$a)
                        $okB = [double]::TryParse($field.Value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$b)
                        if ($okA -and $okB -and [math]::Abs($a - $b) -lt 0.0001) {
                            $line = ('{0}: {1} {2} (data says {3})' -f (Get-RelPath $file.FullName), $id, $field.Name, $field.Value)
                            $warnings.Add($line)
                            Add-Finding 'balance_numbers' $line 'WARN'
                        }
                    }
                }
            }
        }
    }

    if ($warnings.Count -eq 0) {
        Add-Check 'balance numbers' 'PASS' 'no hand-written page restates a skill number'
    }
    else {
        Add-Check 'balance numbers' 'WARN' ('{0} heuristic hit(s): {1}' -f $warnings.Count, ($warnings -join '; '))
    }

    # ------------------------------------------------------------------
    # fixtures: prove the lint fails for the right reasons
    # ------------------------------------------------------------------
    if ($Fixtures) {
        $fixturesDir = Join-Path $script:Root 'tools\wiki-fixtures'
        $cases = @()
        if (Test-Path -LiteralPath $fixturesDir) {
            $cases = @(Get-ChildItem -LiteralPath $fixturesDir -Directory | Sort-Object -Property Name)
        }

        if ($cases.Count -eq 0) {
            Add-Finding 'fixtures_missing' ('no fixture directories under {0}' -f (Get-RelPath $fixturesDir))
            Add-Check 'fixtures rejected' 'FAIL' 'no fixtures found'
        }
        else {
            $fixtureFailures = New-Object System.Collections.Generic.List[string]
            foreach ($case in $cases) {
                $manifestPath = Join-Path $case.FullName 'fixture.json'
                if (-not (Test-Path -LiteralPath $manifestPath)) {
                    $fixtureFailures.Add(('{0}: fixture.json is missing' -f $case.Name))
                    continue
                }

                $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
                $expected = @($manifest.expectedCodes | Where-Object { $_ })
                $caseWiki = Join-Path $temp ('fixture-' + $case.Name)
                Copy-Item -LiteralPath $WikiDir -Destination $caseWiki -Recurse -Force

                foreach ($file in @(Get-ChildItem -LiteralPath $case.FullName -Recurse -File)) {
                    $relative = $file.FullName.Substring($case.FullName.Length).TrimStart('\', '/')
                    if ($relative -eq 'fixture.json') { continue }
                    $target = Join-Path $caseWiki $relative
                    $parent = Split-Path -Parent $target
                    if (-not (Test-Path -LiteralPath $parent)) {
                        [void](New-Item -ItemType Directory -Path $parent -Force)
                    }
                    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
                }

                $output = (& powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
                        -WikiDir $caseWiki -DataDir $DataDir -NoWrite -Json 2>&1 | Out-String).Trim()
                $code = $LASTEXITCODE
                $codes = @()
                try {
                    $payload = $output | ConvertFrom-Json
                    $codes = @($payload.findings | Where-Object { $_.severity -eq 'FAIL' } | ForEach-Object { $_.code })
                }
                catch {
                    $fixtureFailures.Add(('{0}: could not read the verification report' -f $case.Name))
                    continue
                }

                $missing = @($expected | Where-Object { $codes -notcontains $_ })
                if ($code -ne 1) {
                    $fixtureFailures.Add(('{0}: expected exit 1, got {1}' -f $case.Name, $code))
                    Add-Finding 'fixture_not_rejected' ('{0}: exited {1}' -f $case.Name, $code)
                }
                elseif ($missing.Count -gt 0) {
                    $fixtureFailures.Add(('{0}: expected [{1}], got [{2}]' -f $case.Name, ($expected -join ', '), ($codes -join ', ')))
                    Add-Finding 'fixture_wrong_reason' ('{0}: missing codes {1}' -f $case.Name, ($missing -join ', '))
                }
                else {
                    Add-Check ('fixture: ' + $case.Name) 'PASS' ('rejected with {0}' -f ($expected -join ', '))
                }
            }

            if ($fixtureFailures.Count -eq 0) {
                Add-Check 'fixtures rejected' 'PASS' ('{0} broken fixture(s) rejected for the right reason' -f $cases.Count)
            }
            else {
                Add-Check 'fixtures rejected' 'FAIL' ($fixtureFailures -join '; ')
            }
        }
    }
}
catch {
    Add-Finding 'harness_error' $_.Exception.Message
    Add-Check 'harness' 'FAIL' $_.Exception.Message
}
finally {
    if (Test-Path -LiteralPath $temp) {
        Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$stopwatch.Stop()
$elapsed = [math]::Round($stopwatch.Elapsed.TotalSeconds, 2)

$failed = @($script:Findings | Where-Object { $_.severity -eq 'FAIL' })
$warned = @($script:Findings | Where-Object { $_.severity -eq 'WARN' })
$passedChecks = @($script:Checks | Where-Object { $_.status -eq 'PASS' })
$exitCode = 0
if ($failed.Count -gt 0) { $exitCode = 1 }

$report = [pscustomobject]@{
    tool     = 'verify-wiki.ps1'
    stage    = '10e'
    exitCode = $exitCode
    elapsedSeconds = $elapsed
    summary  = [pscustomobject]@{
        pass = $passedChecks.Count
        fail = $failed.Count
        warn = $warned.Count
    }
    checks   = $script:Checks.ToArray()
    findings = $script:Findings.ToArray()
}

if ($Json) {
    $report | ConvertTo-Json -Depth 6
}
else {
    Write-Host ''
    Write-Host 'ProjectSurvive - verify the LLM wiki (Stage 10e)' -ForegroundColor Cyan
    Write-Host ('=' * 72)
    foreach ($check in $script:Checks) {
        $color = 'Gray'
        switch ($check.status) {
            'PASS' { $color = 'Green' }
            'FAIL' { $color = 'Red' }
            'WARN' { $color = 'Yellow' }
        }
        Write-Host ('  [{0,-4}] {1,-22} {2}' -f $check.status, $check.name, $check.detail) -ForegroundColor $color
    }
    Write-Host ('=' * 72)
    Write-Host ('  PASS {0}  |  FAIL {1}  |  WARN {2}' -f $passedChecks.Count, $failed.Count, $warned.Count)
    Write-Host ('  elapsed: {0:0.00} s' -f $elapsed)
    if ($exitCode -eq 0) {
        Write-Host '  RESULT: OK (wiki content, links, ids and notes are consistent)' -ForegroundColor Green
    }
    else {
        Write-Host '  RESULT: FAILED (fix the findings above)' -ForegroundColor Red
    }
    Write-Host ''
}

if ($WriteReport) {
    try {
        $artifactDir = Join-Path $script:Root 'artifacts\wiki'
        if (-not (Test-Path -LiteralPath $artifactDir)) {
            [void](New-Item -ItemType Directory -Path $artifactDir -Force)
        }
        $report | ConvertTo-Json -Depth 6 |
            Set-Content -LiteralPath (Join-Path $artifactDir 'verify-wiki-report.json') -Encoding UTF8
    }
    catch {
        Write-Host ('  (warning) could not write verify-wiki-report.json: {0}' -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit $exitCode
