<#
.SYNOPSIS
    Rename the project identity across a copy of this template (T4).

.DESCRIPTION
    A game starts as a copy of this tree, not as a fork of its branch, so the first
    thing it needs is its own name. Doing that by hand misses one of the six spellings
    the name appears in (a namespace, a package id, a page-global prefix, an .asmdef
    file name, a UPM key and the Unity product name), and the miss only surfaces later
    as a compile error or a browser global that no longer matches.

    This tool replaces exactly the project token and nothing else: the word "survive"
    or "survivor" on its own is never touched, only the complete six forms:

        ProjectSurvive   projectSurvive   projectsurvive
        project-survive  PROJECT_SURVIVE  PROJECTSURVIVE

    It works in two places, because a name lives in two places:

      * the tree - every text file's content AND every file/folder name, so asmdefs,
        csproj, the .sln, package.json, manifest.json, packages-lock.json,
        luban.conf, the csharp_namespace in app.proto, link.xml,
        InternalsVisibleTo, ProjectSettings (productName/companyName),
        the .jslib and tests/web follow along;
      * a patch file - a `generic:` commit's diff, when upgrading a game (see
        docs/template/README.md). Paths and hunk text are rewritten together.

    Unity metadata: a file and its .meta are renamed as a pair by the same rule (the
    .meta shares the file's name), so the GUID inside the .meta - and therefore every
    scene and prefab reference - is preserved. Renamed .unity/.prefab/.asset files are
    reported separately at the end, because Unity YAML references assets by GUID but
    some strings (m_Name, menu paths) are plain text and worth a human look.

    Never renamed, because they are not identity:
      .git  Library  Temp  Logs  obj  bin  UserSettings  node_modules  artifacts
      Builds  lib/Runtime/Generated/**/*.cs  docs/template/examples/**
      docs/template/ORIGIN.md
      binary files (by extension and by a NUL byte in the first 8 KiB)
      tools/vendor/** (downloaded binaries; tools/setup-tools.ps1 recreates them)

    .git is excluded because rewriting history is not what starting a game means: the
    game's first commit is its own. The examples archive and ORIGIN.md keep the old name
    on purpose - they are where the provenance stays readable.

    Inside lib/Runtime/Generated only the *.cs files are skipped: tools/gen.ps1 owns
    exactly those (it deletes and republishes them). The two .asmdef files in that
    folder are hand-written and gen.ps1 never writes them, so skipping the whole folder
    would leave two tracked files - and every asmdef that references them by assembly
    name - pointing at the old name, and Unity would fail to resolve the assembly.

    Idempotent: a second run finds nothing to change. -DryRun prints the plan and
    touches nothing, so the plan is checkable before the tree moves.

.PARAMETER From
    The current project name in PascalCase, e.g. ProjectSurvive.

.PARAMETER To
    The new project name in PascalCase, e.g. JianghuAlmanac. It must not contain the
    old name (or vice versa): that would make a second run change the tree again.

.PARAMETER DryRun
    Report what would change (files, folders, replacement count) and write nothing.

.PARAMETER PatchFile
    Patch mode: rewrite a `git format-patch`/`git diff` file instead of the tree, and
    write the result next to it as <name>.renamed<ext>. Nothing in the tree is touched.

.EXAMPLE
    pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To JianghuAlmanac -DryRun
    pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To JianghuAlmanac
    pwsh -NoProfile -File tools/rename.ps1 -From ProjectSurvive -To JianghuAlmanac -PatchFile 0042.patch

.NOTES
    Exit codes (same contract as tools/doctor.ps1 and tools/test.ps1):
      0 = the tree (or patch) now carries the new name, or -DryRun found the plan
      1 = the rename could not be completed (collision, unreadable file, old name left)
      2 = the script itself could not run (bad name, missing patch file)

    Run this on a fresh copy, before the game's first commit. It does not commit.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$From,
    [Parameter(Mandatory)][string]$To,
    [switch]$DryRun,
    [string]$PatchFile = ''
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:MaxShown = 40

# Directories whose whole subtree is never touched. Matched by name at any depth
# (case-insensitively, which is how Windows compares them anyway).
$script:SkipDirNames = @(
    '.git', 'Library', 'Temp', 'Logs', 'obj', 'bin', 'UserSettings',
    'node_modules', 'artifacts', 'Builds'
)

# Paths (relative to the repo root, forward slashes) that are never touched, even
# though a name inside them matches. Each one has a reason in the header.
$script:SkipPaths = @(
    'docs/template/examples',
    'tools/vendor'
)

$script:SkipFiles = @(
    'docs/template/ORIGIN.md'
)

# Individual files that are never touched, by relative-path regex. lib/Runtime/Generated
# is machine output only where gen.ps1 writes (*.cs); its .asmdef files are hand-written
# and must follow the rename, or the assembly they declare keeps the old name.
$script:SkipFilePatterns = @(
    '^lib/Runtime/Generated/.*\.cs$'
)

# Extensions that are binary by definition. A file with one of these is skipped
# without reading it; the NUL-byte test below catches everything else.
$script:BinaryExtensions = @(
    '.png', '.jpg', '.jpeg', '.gif', '.bmp', '.psd', '.tga', '.exr', '.hdr', '.ico',
    '.ttf', '.otf', '.woff', '.woff2', '.eot',
    '.fbx', '.obj', '.blend', '.dae',
    '.wav', '.mp3', '.ogg', '.flac', '.m4a', '.mp4', '.webm', '.mov',
    '.zip', '.7z', '.gz', '.tar', '.jar', '.nupkg',
    '.dll', '.exe', '.so', '.dylib', '.pdb', '.mdb', '.class', '.pyc',
    '.bytes', '.bin', '.wasm', '.data', '.unitypackage'
)

# ---------------------------------------------------------------------------
# reporting
# ---------------------------------------------------------------------------
function Write-Row {
    param([string]$Label, [string]$Detail, [string]$Colour = 'Gray')
    Write-Host ('  {0,-26} {1}' -f $Label, $Detail) -ForegroundColor $Colour
}

function Show-Paths {
    param([string[]]$Lines, [string]$Indent = '      ')
    $shown = @($Lines | Select-Object -First $script:MaxShown)
    foreach ($line in $shown) { Write-Host ($Indent + $line) }
    if ($Lines.Count -gt $shown.Count) {
        Write-Host ($Indent + ('... and {0} more' -f ($Lines.Count - $shown.Count))) -ForegroundColor DarkGray
    }
}

# ---------------------------------------------------------------------------
# name forms
# ---------------------------------------------------------------------------
# "ProjectSurvive" -> Project | Survive. The pattern keeps acronym runs together
# (MyHTTPThing -> My, HTTP, Thing) so the kebab and snake forms read as words.
function Get-NameWords {
    param([Parameter(Mandatory)][string]$Pascal)
    $words = New-Object System.Collections.ArrayList
    foreach ($m in [regex]::Matches($Pascal, '[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+')) {
        [void]$words.Add($m.Value)
    }
    return @($words)
}

# The six spellings, keyed by KIND rather than by position, so a one-word name whose
# forms collapse (Foo / foo / FOO) still pairs with the right spelling of the new
# name instead of shifting every later form by one.
function Get-NameForms {
    param([Parameter(Mandatory)][string]$Pascal)
    $words = Get-NameWords $Pascal
    return [ordered]@{
        pascal = $Pascal
        camel  = $Pascal.Substring(0, 1).ToLowerInvariant() + $Pascal.Substring(1)
        lower  = $Pascal.ToLowerInvariant()
        kebab  = (($words | ForEach-Object { $_.ToLowerInvariant() }) -join '-')
        snake  = (($words | ForEach-Object { $_.ToUpperInvariant() }) -join '_')
        shout  = $Pascal.ToUpperInvariant()
    }
}

function Assert-Name {
    param([string]$Name, [string]$Option)
    # -cnotmatch, not -notmatch: PowerShell's -match is case-INSENSITIVE by default,
    # which would let 'projectsurvive' through a PascalCase check.
    if ($Name -cnotmatch '^[A-Z][A-Za-z0-9]*$') {
        Write-Host ("ERROR: {0} '{1}' is not a PascalCase name (letters and digits, starting upper-case)" -f $Option, $Name) -ForegroundColor Red
        exit 2
    }
}

# ---------------------------------------------------------------------------
# files
# ---------------------------------------------------------------------------
# UTF-8 / UTF-16 text, or $null for anything that is not text. Encoding and BOM are
# kept so a rewrite does not change every line ending or the encoding of a Thai doc.
function Read-TextFile {
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) {
        return [pscustomobject]@{ Text = ''; Encoding = (New-Object System.Text.UTF8Encoding($false)); BomLength = 0; IsText = $true }
    }

    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $enc = New-Object System.Text.UTF8Encoding($true)
        return [pscustomobject]@{ Text = $enc.GetString($bytes, 3, $bytes.Length - 3); Encoding = $enc; BomLength = 3; IsText = $true }
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        $enc = New-Object System.Text.UnicodeEncoding($false, $true)
        return [pscustomobject]@{ Text = $enc.GetString($bytes, 2, $bytes.Length - 2); Encoding = $enc; BomLength = 2; IsText = $true }
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        $enc = New-Object System.Text.UnicodeEncoding($true, $true)
        return [pscustomobject]@{ Text = $enc.GetString($bytes, 2, $bytes.Length - 2); Encoding = $enc; BomLength = 2; IsText = $true }
    }

    $probe = [Math]::Min($bytes.Length, 8192)
    for ($i = 0; $i -lt $probe; $i++) {
        if ($bytes[$i] -eq 0) { return $null }
    }

    # Strict UTF-8: a byte sequence that is not valid UTF-8 is not a text file this
    # tool can rewrite safely, and guessing an ANSI codepage is how a Thai string
    # turns into mojibake.
    $enc = New-Object System.Text.UTF8Encoding($false, $true)
    try {
        $text = $enc.GetString($bytes, 0, $bytes.Length)
    }
    catch [System.Text.DecoderFallbackException] {
        return $null
    }
    return [pscustomobject]@{ Text = $text; Encoding = $enc; BomLength = 0; IsText = $true }
}

function Write-TextFile {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)]$Encoding
    )
    [System.IO.File]::WriteAllText($Path, $Text, $Encoding)
}

function Test-BinaryExtension {
    param([string]$Path)
    $ext = [System.IO.Path]::GetExtension($Path).ToLowerInvariant()
    return ($script:BinaryExtensions -contains $ext)
}

# ---------------------------------------------------------------------------
# walking the tree
# ---------------------------------------------------------------------------
function Get-RelativePath {
    param([string]$FullPath)
    $prefix = $script:Root.TrimEnd('\') + '\'
    if ($FullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $FullPath.Substring($prefix.Length).Replace('\', '/')
    }
    return $FullPath.Replace('\', '/')
}

function Test-SkippedPath {
    param([string]$RelativePath)
    foreach ($skip in $script:SkipPaths) {
        if ($RelativePath -eq $skip -or $RelativePath.StartsWith($skip + '/', [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    foreach ($file in $script:SkipFiles) {
        if ($RelativePath -ieq $file) { return $true }
    }
    foreach ($pattern in $script:SkipFilePatterns) {
        if ($RelativePath -cmatch $pattern) { return $true }
    }
    return $false
}

# Iterative walk: a recursive Get-ChildItem would descend into Library (3 GB on a
# real project) before filtering, which is slower than the rename itself.
function Get-TreeInventory {
    param([Parameter(Mandatory)][string]$Root, [switch]$SkipIgnored)

    $files = New-Object System.Collections.ArrayList
    $dirs = New-Object System.Collections.ArrayList
    $skippedDirs = 0
    $skippedPaths = 0

    $stack = New-Object System.Collections.Stack
    $stack.Push($Root)

    while ($stack.Count -gt 0) {
        $dir = [string]$stack.Pop()
        [void]$dirs.Add($dir)

        foreach ($child in [System.IO.Directory]::GetDirectories($dir)) {
            $name = [System.IO.Path]::GetFileName($child)
            if ($SkipIgnored -and ($script:SkipDirNames -contains $name)) { $skippedDirs++; continue }
            if ($SkipIgnored -and (Test-SkippedPath (Get-RelativePath $child))) { $skippedPaths++; continue }
            $stack.Push($child)
        }

        foreach ($child in [System.IO.Directory]::GetFiles($dir)) {
            if ($SkipIgnored -and (Test-SkippedPath (Get-RelativePath $child))) { $skippedPaths++; continue }
            if ($SkipIgnored -and (Test-BinaryExtension $child)) { continue }
            [void]$files.Add($child)
        }
    }

    return [pscustomobject]@{
        Files        = @($files)
        Dirs         = @($dirs)
        SkippedDirs  = $skippedDirs
        SkippedPaths = $skippedPaths
    }
}

# New RELATIVE path with every one of the old forms replaced. Relative, not
# absolute: the checkout folder itself may be named after the project
# (".../ProjectSurvive-template"), and a rename must never move the tree's root.
# Names are never "cleaned": a leading dot or a space in a name must survive.
function Get-RenamedPath {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Map)
    $new = $Path
    foreach ($entry in $Map.GetEnumerator()) {
        if ($new.Contains($entry.Key)) { $new = $new.Replace($entry.Key, $entry.Value) }
    }
    return $new
}

function Count-Form {
    param([string]$Text, [string]$Form)
    $count = 0
    $index = 0
    while (($index = $Text.IndexOf($Form, $index, [System.StringComparison]::Ordinal)) -ge 0) {
        $count++
        $index += $Form.Length
    }
    return $count
}

# ---------------------------------------------------------------------------
# main - parameters
# ---------------------------------------------------------------------------
Assert-Name $From '-From'
Assert-Name $To '-To'

if ($From -eq $To) {
    Write-Host 'ERROR: -From and -To are the same name; nothing to do.' -ForegroundColor Red
    exit 2
}

$fromForms = Get-NameForms $From
$toForms = Get-NameForms $To
# Ordinal on purpose: the six spellings differ only by case, and PowerShell's
# [ordered]@{} literal is case-INSENSITIVE - under it 'projectSurvive' would be the
# same key as 'ProjectSurvive', and four of the six forms would silently vanish.
$map = New-Object System.Collections.Specialized.OrderedDictionary ([System.StringComparer]::Ordinal)
foreach ($kind in $fromForms.Keys) {
    $old = $fromForms[$kind]
    $new = $toForms[$kind]
    if ($old -eq $new -or $map.Contains($old)) { continue }
    $map[$old] = $new
}

# The two names must not be substrings of each other in any form: otherwise the
# replacement would re-match its own output and a second run would not be a no-op.
foreach ($old in $map.Keys) {
    foreach ($new in $map.Values) {
        if ($new.IndexOf($old, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Write-Host ("ERROR: '{0}' contains '{1}'; the rename could not be repeated safely." -f $new, $old) -ForegroundColor Red
            exit 2
        }
    }
}

Write-Host ''
Write-Host ("Rename {0} -> {1}" -f $From, $To) -ForegroundColor Cyan
Write-Host ('=' * 78)
Write-Host '  forms:'
foreach ($entry in $map.GetEnumerator()) {
    Write-Host ('    {0,-18} -> {1}' -f $entry.Key, $entry.Value)
}

# ---------------------------------------------------------------------------
# main - patch mode
# ---------------------------------------------------------------------------
if ($PatchFile) {
    if (-not (Test-Path -LiteralPath $PatchFile)) {
        Write-Host ("ERROR: patch file not found: {0}" -f $PatchFile) -ForegroundColor Red
        exit 2
    }

    $patchPath = (Resolve-Path -LiteralPath $PatchFile).Path
    $read = Read-TextFile -Path $patchPath
    if ($null -eq $read) {
        Write-Host ("ERROR: {0} is not a text file" -f $patchPath) -ForegroundColor Red
        exit 2
    }

    $text = $read.Text
    $total = 0
    foreach ($entry in $map.GetEnumerator()) {
        $count = Count-Form -Text $text -Form $entry.Key
        if ($count -gt 0) {
            Write-Host ('  {0,-18} {1} occurrence(s)' -f $entry.Key, $count)
            $total += $count
            $text = $text.Replace($entry.Key, $entry.Value)
        }
    }

    $directory = [System.IO.Path]::GetDirectoryName($patchPath)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($patchPath)
    $extension = [System.IO.Path]::GetExtension($patchPath)
    $outPath = Join-Path $directory ($stem + '.renamed' + $extension)

    Write-Host ('=' * 78)
    Write-Row 'replacements' ("{0} in {1}" -f $total, [System.IO.Path]::GetFileName($patchPath))
    if ($DryRun) {
        Write-Row 'dry run' ("would write {0}" -f $outPath) 'Yellow'
        Write-Host ''
        exit 0
    }
    if ($total -eq 0) {
        Write-Row 'result' 'nothing to rename; no file written' 'Yellow'
        Write-Host ''
        exit 0
    }

    Write-TextFile -Path $outPath -Text $text -Encoding $read.Encoding
    Write-Row 'result' ("wrote {0}" -f $outPath) 'Green'
    Write-Host ''
    Write-Host '  Apply the result with `git apply` (or `git am` for a mailbox patch).' -ForegroundColor DarkGray
    Write-Host ''
    exit 0
}

# ---------------------------------------------------------------------------
# main - tree mode
# ---------------------------------------------------------------------------
$inventory = Get-TreeInventory -Root $script:Root -SkipIgnored
Write-Row 'scanned' ("{0} file(s), {1} folder(s)" -f $inventory.Files.Count, ($inventory.Dirs.Count - 1))
Write-Row 'skipped' ("{0} excluded folder(s), {1} excluded path(s), binary files by extension" -f $inventory.SkippedDirs, $inventory.SkippedPaths)

$changes = New-Object System.Collections.ArrayList
$binaryFiles = 0
$unreadable = New-Object System.Collections.ArrayList

foreach ($path in $inventory.Files) {
    try {
        $read = Read-TextFile -Path $path
    }
    catch {
        # A locked or unreadable file could hold the name. Stop before writing:
        # half a rename is worse than a failed one.
        [void]$unreadable.Add(('{0}: {1}' -f (Get-RelativePath $path), $_.Exception.Message))
        continue
    }
    if ($null -eq $read) {
        $binaryFiles++
        continue
    }

    $count = 0
    foreach ($entry in $map.GetEnumerator()) {
        $count += Count-Form -Text $read.Text -Form $entry.Key
    }
    if ($count -eq 0) { continue }

    [void]$changes.Add([pscustomobject]@{
            Path  = $path
            Count = $count
            Read  = $read
        })
}

if ($unreadable.Count -gt 0) {
    Write-Host ''
    Write-Host 'ERROR: these files could not be read, so the old name may survive in them:' -ForegroundColor Red
    Show-Paths -Lines $unreadable
    Write-Host '       nothing was changed. Close whatever holds them and run again.' -ForegroundColor Yellow
    exit 1
}

$replacements = 0
foreach ($change in $changes) { $replacements += $change.Count }

# Planned renames. A file's .meta is covered by the same rule because its name is
# the file's name plus ".meta"; the pair keeps its GUIDs.
$fileRenames = New-Object System.Collections.ArrayList
foreach ($path in $inventory.Files) {
    $relative = Get-RelativePath $path
    $newRelative = Get-RenamedPath -Path $relative -Map $map
    if ($newRelative -ne $relative) {
        [void]$fileRenames.Add([pscustomobject]@{
                From = $path
                To   = (Join-Path $script:Root ($newRelative.Replace('/', '\')))
            })
    }
}

$dirRenames = New-Object System.Collections.ArrayList
foreach ($dir in $inventory.Dirs) {
    if ($dir -eq $script:Root) { continue }
    $relative = Get-RelativePath $dir
    $newRelative = Get-RenamedPath -Path $relative -Map $map
    if ($newRelative -ne $relative) {
        [void]$dirRenames.Add([pscustomobject]@{
                From = $dir
                To   = (Join-Path $script:Root ($newRelative.Replace('/', '\')))
            })
    }
}
# Deepest first: renaming a parent moves its children, so a child path is only
# valid until its parent moves.
$dirRenames = @($dirRenames | Sort-Object -Property { ($_.From -split '\\').Count } -Descending)

Write-Row 'content' ("{0} text file(s), {1} replacement(s)" -f $changes.Count, $replacements)
Write-Row 'names' ("{0} file(s), {1} folder(s)" -f $fileRenames.Count, $dirRenames.Count)
Write-Row 'binary' ("{0} file(s) skipped" -f $binaryFiles)

$unityAssets = @(
    $fileRenames |
        Where-Object { $_.From -match '\.(unity|prefab|asset)$' } |
        ForEach-Object { ('{0} -> {1}' -f (Get-RelativePath $_.From), (Get-RelativePath $_.To)) }
)

if ($fileRenames.Count -gt 0) {
    Write-Host ''
    Write-Host '  rename:' -ForegroundColor Cyan
    Show-Paths -Lines @($fileRenames | ForEach-Object { ('{0} -> {1}' -f (Get-RelativePath $_.From), (Get-RelativePath $_.To)) })
}
if ($dirRenames.Count -gt 0) {
    Write-Host ''
    Write-Host '  folders:' -ForegroundColor Cyan
    Show-Paths -Lines @($dirRenames | ForEach-Object { ('{0} -> {1}' -f (Get-RelativePath $_.From), (Get-RelativePath $_.To)) })
}
if ($changes.Count -gt 0) {
    Write-Host ''
    Write-Host '  content:' -ForegroundColor Cyan
    Show-Paths -Lines @($changes | ForEach-Object { ('{0} ({1})' -f (Get-RelativePath $_.Path), $_.Count) })
}

# A name collision means the target already exists: abort before writing anything,
# so a failed run leaves the tree exactly as it was.
$collisions = New-Object System.Collections.ArrayList
foreach ($rename in $fileRenames) {
    if ((Test-Path -LiteralPath $rename.To) -and ($rename.To -ne $rename.From)) {
        [void]$collisions.Add(('file {0} -> {1}' -f (Get-RelativePath $rename.From), (Get-RelativePath $rename.To)))
    }
}
foreach ($rename in $dirRenames) {
    if ((Test-Path -LiteralPath $rename.To) -and ($rename.To -ne $rename.From)) {
        [void]$collisions.Add(('folder {0} -> {1}' -f (Get-RelativePath $rename.From), (Get-RelativePath $rename.To)))
    }
}

if ($collisions.Count -gt 0) {
    Write-Host ''
    Write-Host 'ERROR: the new name already exists at these targets; nothing was changed:' -ForegroundColor Red
    Show-Paths -Lines $collisions
    exit 1
}

Write-Host ('=' * 78)
if ($DryRun) {
    Write-Row 'dry run' 'no file was written and no name was changed' 'Yellow'
    if ($changes.Count -eq 0 -and $fileRenames.Count -eq 0 -and $dirRenames.Count -eq 0) {
        Write-Row 'result' 'already renamed (idempotent)' 'Green'
    }
    Write-Host ''
    exit 0
}

$exitCode = 0
try {
    # 1. content. Written with the encoding the file arrived in, so a Thai document
    #    and a CRLF file both survive the pass unchanged.
    $written = 0
    foreach ($change in $changes) {
        $text = $change.Read.Text
        foreach ($entry in $map.GetEnumerator()) {
            if ($text.Contains($entry.Key)) { $text = $text.Replace($entry.Key, $entry.Value) }
        }
        Write-TextFile -Path $change.Path -Text $text -Encoding $change.Read.Encoding
        $written++
    }
    Write-Row 'content written' ("{0} file(s)" -f $written) 'Green'

    # 2. files, then folders (deepest first).
    foreach ($rename in $fileRenames) {
        [System.IO.File]::Move($rename.From, $rename.To)
    }
    foreach ($rename in $dirRenames) {
        [System.IO.Directory]::Move($rename.From, $rename.To)
    }
    Write-Row 'names changed' ("{0} file(s), {1} folder(s)" -f $fileRenames.Count, $dirRenames.Count) 'Green'
}
catch {
    Write-Host ("ERROR: {0}" -f $_.Exception.Message) -ForegroundColor Red
    Write-Host '       the tree is partly renamed; fix the cause and run again (the tool is idempotent).' -ForegroundColor Yellow
    exit 1
}

# ---------------------------------------------------------------------------
# residual check: the old name must be gone from everything this tool owns
# ---------------------------------------------------------------------------
$residual = New-Object System.Collections.ArrayList
$after = Get-TreeInventory -Root $script:Root -SkipIgnored
foreach ($path in $after.Files) {
    try {
        $read = Read-TextFile -Path $path
    }
    catch {
        [void]$residual.Add((Get-RelativePath $path) + ' (unreadable)')
        continue
    }
    if ($null -eq $read) { continue }
    foreach ($entry in $map.GetEnumerator()) {
        if ($read.Text.Contains($entry.Key)) {
            [void]$residual.Add((Get-RelativePath $path))
            break
        }
    }
}

Write-Host ('=' * 78)
if ($residual.Count -gt 0) {
    Write-Row 'residual name' ("{0} file(s) still contain an old form" -f $residual.Count) 'Red'
    Show-Paths -Lines $residual
    Write-Host '  (a file this tool must not edit - generated code, the examples archive - was skipped by design)' -ForegroundColor Yellow
    $exitCode = 1
}
else {
    Write-Row 'residual name' 'none outside the excluded paths' 'Green'
}

if ($unityAssets.Count -gt 0) {
    Write-Host ''
    Write-Host '  Unity assets whose name contained the old name (GUID references are intact;' -ForegroundColor Cyan
    Write-Host '  check m_Name / menu strings by eye):' -ForegroundColor Cyan
    Show-Paths -Lines $unityAssets
}
else {
    Write-Host ''
    Write-Host '  Unity assets: none were named after the project.' -ForegroundColor DarkGray
}

Write-Host ''
if ($exitCode -eq 0) {
    Write-Host ("  RESULT: OK ({0} -> {1})" -f $From, $To) -ForegroundColor Green
    Write-Host '  Next: tools/gen.ps1 (the generated tables and proto carry the new namespace),' -ForegroundColor DarkGray
    Write-Host '        then tools/verify-generated.ps1 and tools/test.ps1 -Suite Fast.' -ForegroundColor DarkGray
}
else {
    Write-Host '  RESULT: FAILED (the old name is still present)' -ForegroundColor Red
}
Write-Host ''

exit $exitCode
