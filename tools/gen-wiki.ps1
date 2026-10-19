#Requires -Version 5.1
<#
.SYNOPSIS
    Generate llmwiki/content/ from data/ (Stage 10e).

.DESCRIPTION
    The wiki is a SYNTHESISED layer, never a source of truth: every balance
    number on a generated page is copied out of data/tables/*.csv (and every
    Thai string out of data/localization/localization.csv) by this script, in
    file order. Nothing here carries a balance number of its own.

    Output contract (the same one Stage 02 set for generated C#):
      - deterministic: file order, no timestamp, no absolute path, no machine
        name in a generated page, LF line endings, UTF-8 without BOM
      - running twice from the same data/ produces byte-identical files, so
        `tools/verify-wiki.ps1` can diff a fresh run against the committed
        llmwiki/content/ and `git diff` stays empty
      - a missing localization key prints "(missing: <key>)" rather than a
        guessed string

    Pages written (all under -OutDir, default llmwiki/content):
      index.md  items.md  skills.md  synergies.md  upgrades.md
      enemies.md  floors.md  economy.md

    Stale *.md files in the output directory are removed, so a page that is no
    longer generated cannot linger and pass a naive diff.

.PARAMETER DataDir
    The source tables. Default: <repo>/data.

.PARAMETER OutDir
    Where the pages are written. Default: <repo>/llmwiki/content. The verifier
    passes a temporary directory so it can diff without touching the repo.

.PARAMETER Quiet
    Do not print the human summary.

.PARAMETER Json
    Emit the machine-readable report to stdout instead of the human summary.

.PARAMETER NoWrite
    Do not write the optional report under artifacts/.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1
    powershell -NoProfile -ExecutionPolicy Bypass -File tools/gen-wiki.ps1 -DataDir data -OutDir $env:TEMP\wiki

.NOTES
    Exit codes (the repository contract):
      0 = every page was generated
      1 = the data could not be rendered (a missing table or a malformed row)
      2 = the script itself could not run (no data directory, no output path)
#>
[CmdletBinding()]
param(
    [string]$DataDir = '',
    [string]$OutDir = '',
    [switch]$Quiet,
    [switch]$Json,
    [switch]$NoWrite
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$script:Root = Split-Path -Parent $PSScriptRoot
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:EmDash = [string][char]0x2014
$script:Pages = New-Object System.Collections.Generic.List[object]
$script:Findings = New-Object System.Collections.Generic.List[object]
$script:ExitCode = 0

if (-not $DataDir) { $DataDir = Join-Path $script:Root 'data' }
if (-not $OutDir) { $OutDir = Join-Path $script:Root 'llmwiki\content' }

trap {
    Write-Host ('ERROR: {0}' -f $_.Exception.Message) -ForegroundColor Red
    Write-Host ($_.InvocationInfo.PositionMessage) -ForegroundColor DarkGray
    exit 1
}

if (-not (Test-Path -LiteralPath $DataDir)) {
    Write-Host ('ERROR: data directory not found: {0}' -f $DataDir) -ForegroundColor Red
    exit 2
}

function Add-Finding {
    param([string]$Code, [string]$Detail)
    [void]$script:Findings.Add([pscustomobject]@{ code = $Code; detail = $Detail })
}

function Read-Table {
    param([Parameter(Mandatory)][string]$Name)

    $path = Join-Path $DataDir ('tables\' + $Name + '.csv')
    if (-not (Test-Path -LiteralPath $path)) {
        throw ("missing table: tables/{0}.csv" -f $Name)
    }

    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    # ConvertFrom-Csv treats a line starting with '#' as a comment, so the
    # documented '##' marker column would swallow the header row. Rename that
    # one cell, then drop the marker column from the object below.
    $text = $text -replace '^##', '__row'
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($row in @(ConvertFrom-Csv -InputObject $text)) {
        $hasValue = $false
        foreach ($property in $row.PSObject.Properties) {
            if ($property.Name -eq '__row') { continue }
            if (-not [string]::IsNullOrWhiteSpace([string]$property.Value)) { $hasValue = $true; break }
        }
        if ($hasValue) { [void]$rows.Add($row) }
    }
    return $rows.ToArray()
}

function Read-Localization {
    $path = Join-Path $DataDir 'localization\localization.csv'
    if (-not (Test-Path -LiteralPath $path)) {
        throw 'missing table: localization/localization.csv'
    }

    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $text = $text -replace '^##', '__row'
    $map = @{}
    foreach ($row in @(ConvertFrom-Csv -InputObject $text)) {
        if ($row.key) { $map[[string]$row.key] = $row }
    }

    return $map
}

function Get-Loc {
    param([string]$Key, [string]$Lang)

    if ([string]::IsNullOrWhiteSpace($Key)) { return '' }
    if ($script:Localization.ContainsKey($Key)) {
        $value = [string]$script:Localization[$Key].$Lang
        if (-not [string]::IsNullOrWhiteSpace($value)) { return $value }
    }

    return ('(missing: {0})' -f $Key)
}

function Get-Thai { param([string]$Key) return (Get-Loc $Key 'th') }
function Get-English { param([string]$Key) return (Get-Loc $Key 'en') }

function Get-Code { param([string]$Value) return ('`{0}`' -f $Value) }

function Get-Link {
    param([string]$Page, [string]$Id, [string]$Text)
    return ('[{0}]({1}#{2})' -f $Text, $Page, $Id)
}

function Get-RowLink {
    param([string]$Page, [string]$Id, [string]$NameKey)
    $th = Get-Thai $NameKey
    if ([string]::IsNullOrWhiteSpace($th)) { return (Get-Link $Page $Id $Id) }
    return (Get-Link $Page $Id $th)
}

function Get-Tags {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return @() }
    return @($Value.Split('|') | Where-Object { $_ -ne '' })
}

function Get-FootprintInfo {
    param([string]$Picture)

    $rows = @($Picture.Split('|'))
    $cols = 0
    $cells = 0
    foreach ($row in $rows) {
        if ($row.Length -gt $cols) { $cols = $row.Length }
        foreach ($ch in $row.ToCharArray()) {
            if ($ch -eq 'X') { $cells++ }
        }
    }

    return [pscustomobject]@{ Rows = $rows.Count; Cols = $cols; Cells = $cells }
}

function Get-MagnitudeText {
    param([string]$Method, [string]$Magnitude)

    $value = 0.0
    $isNumber = [double]::TryParse($Magnitude, [ref]$value)
    if ($Method -eq 'Percent') {
        if ($isNumber -and $value -ge 0) { return ('+{0}%' -f $Magnitude) }
        return ('{0}%' -f $Magnitude)
    }

    if ($isNumber -and $value -ge 0) { return ('+{0}' -f $Magnitude) }
    return ([string]$Magnitude)
}

function Get-Lines {
    param([string[]]$Lines)
    return @($Lines | ForEach-Object { if ($null -eq $_) { '' } else { [string]$_ } })
}

function Add-Page {
    param([string]$Name, [string[]]$Lines)
    [void]$script:Pages.Add([pscustomobject]@{ Name = $Name; Lines = (Get-Lines $Lines) })
}

function Get-Banner {
    return ('> **GENERATED FROM data/ ' + $script:EmDash + ' DO NOT EDIT.** Regenerate with `tools/gen-wiki.ps1`; the wiki is not the source of truth for balance numbers.')
}

function Get-SeeAlso {
    param([string]$Self)

    $links = New-Object System.Collections.Generic.List[string]
    $order = @(
        @{ Page = 'items.md'; Label = 'items' },
        @{ Page = 'skills.md'; Label = 'skills' },
        @{ Page = 'synergies.md'; Label = 'synergies' },
        @{ Page = 'upgrades.md'; Label = 'upgrades' },
        @{ Page = 'enemies.md'; Label = 'enemies' },
        @{ Page = 'floors.md'; Label = 'floors' },
        @{ Page = 'economy.md'; Label = 'economy' }
    )

    foreach ($entry in $order) {
        if ($entry.Page -ne $Self) { [void]$links.Add(('[{0}]({1})' -f $entry.Label, $entry.Page)) }
    }

    [void]$links.Add('[content index](index.md)')
    return ('See also: ' + ($links -join ' | '))
}

# ---------------------------------------------------------------------------
# Input
# ---------------------------------------------------------------------------
$script:Items = @(Read-Table 'item')
$script:Bags = @(Read-Table 'bag')
$script:Skills = @(Read-Table 'skill')
$script:Synergies = @(Read-Table 'synergy')
$script:Enemies = @(Read-Table 'enemy')
$script:Floors = @(Read-Table 'floor')
$script:Waves = @(Read-Table 'wave')
$script:Variants = @(Read-Table 'upgrade_variant')
$script:Modifiers = @(Read-Table 'upgrade_variant_modifier')
$script:Merges = @(Read-Table 'merge_recipe')
$script:ShopPool = @(Read-Table 'shop_pool')
$script:EconomyRows = @(Read-Table 'economy')
$script:ConstantRows = @(Read-Table 'constant')

if ($script:EconomyRows.Count -lt 1) { throw 'economy.csv has no row' }
if ($script:ConstantRows.Count -lt 1) { throw 'constant.csv has no row' }
$script:Economy = $script:EconomyRows[0]
$script:Constant = $script:ConstantRows[0]
$script:Localization = Read-Localization
$script:MaxTier = [string]$script:Constant.max_tier

$script:ItemById = @{}
foreach ($row in @($script:Items + $script:Bags)) { $script:ItemById[[string]$row.id] = $row }

function Get-MergeSteps {
    param([string]$Id)
    $steps = New-Object System.Collections.Generic.List[string]
    foreach ($merge in $script:Merges) {
        if (([string]$merge.item_id -eq $Id) -or ([string]$merge.bag_id -eq $Id)) {
            [void]$steps.Add(('{0}->{1}' -f $merge.from_tier, $merge.to_tier))
        }
    }
    return $steps
}

function Get-ShopRows {
    param([string]$Id)
    return @($script:ShopPool | Where-Object { ([string]$_.item_id -eq $Id) -or ([string]$_.bag_id -eq $Id) })
}

function Test-VariantApplies {
    param($Variant, $Item)

    if (-not [string]::IsNullOrEmpty([string]$Variant.category)) {
        return ([string]$Variant.category -eq [string]$Item.category)
    }

    if ([string]::IsNullOrEmpty([string]$Variant.tag)) { return $false }

    foreach ($tag in (Get-Tags ([string]$Item.tags))) {
        if ($tag -eq [string]$Variant.tag) { return $true }
    }

    return $false
}

function Get-ApplicableVariants {
    param($Item)
    return @($script:Variants | Where-Object { Test-VariantApplies $_ $Item })
}

# ---------------------------------------------------------------------------
# index.md
# ---------------------------------------------------------------------------
$categoryOrder = New-Object System.Collections.Generic.List[string]
$countByCategory = @{}
foreach ($item in $script:Items) {
    $key = [string]$item.category
    if (-not $countByCategory.ContainsKey($key)) {
        $countByCategory[$key] = 0
        [void]$categoryOrder.Add($key)
    }
    $countByCategory[$key] = $countByCategory[$key] + 1
}

$index = New-Object System.Collections.Generic.List[string]
[void]$index.Add((Get-Banner))
[void]$index.Add('')
[void]$index.Add('# Content index (generated)')
[void]$index.Add('')
[void]$index.Add(('This directory is the **synthesised content layer** of the wiki: one page per kind of table, ' +
    'generated from `data/` by `tools/gen-wiki.ps1`. Every number here is a copy of a table value; when this ' +
    'page and `data/` disagree, `data/` is right. Read `../SCHEMA.md` first for how to use the wiki, `../INGEST.md` ' +
    'for how a stage report becomes a system note, and `../index.md` for the hand-written side.'))
[void]$index.Add('')
[void]$index.Add('## Pages')
[void]$index.Add('')
[void]$index.Add('| Page | Answers questions about | Source tables |')
[void]$index.Add('| --- | --- | --- |')
[void]$index.Add('| [items.md](items.md) | what a piece is, what it costs, what it weighs, what it does, which combos and upgrades it joins | `item`, `bag`, `merge_recipe`, `shop_pool` |')
[void]$index.Add('| [skills.md](skills.md) | a cast behaviour and its numbers, and which pieces use it | `skill` |')
[void]$index.Add('| [synergies.md](synergies.md) | the five combo rules, and the actual pieces on each side of every rule | `synergy`, `item` |')
[void]$index.Add('| [upgrades.md](upgrades.md) | the merge-upgrade choices per tier and which pieces each choice can be offered for | `upgrade_variant`, `upgrade_variant_modifier` |')
[void]$index.Add('| [enemies.md](enemies.md) | a ghost''s role and numbers, and the floors it appears on | `enemy`, `wave` |')
[void]$index.Add('| [floors.md](floors.md) | a floor''s duration, boss and wave timeline (what spawns, when, how many) | `floor`, `wave` |')
[void]$index.Add('| [economy.md](economy.md) | the run''s starting purse, shop prices, sell-back, heal and the tuning constants | `economy`, `constant` |')
[void]$index.Add('')
[void]$index.Add('## What is in data/ right now')
[void]$index.Add('')
[void]$index.Add('| Kind | Count | Where |')
[void]$index.Add('| --- | --- | --- |')
[void]$index.Add(('| items | {0} | [items.md](items.md) |' -f $script:Items.Count))
[void]$index.Add(('| bags | {0} | [items.md](items.md) |' -f $script:Bags.Count))
[void]$index.Add(('| skills | {0} | [skills.md](skills.md) |' -f $script:Skills.Count))
[void]$index.Add(('| synergy rules | {0} | [synergies.md](synergies.md) |' -f $script:Synergies.Count))
[void]$index.Add(('| upgrade variants | {0} | [upgrades.md](upgrades.md) |' -f $script:Variants.Count))
[void]$index.Add(('| enemies | {0} | [enemies.md](enemies.md) |' -f $script:Enemies.Count))
[void]$index.Add(('| floors | {0} | [floors.md](floors.md) |' -f $script:Floors.Count))
[void]$index.Add('')
[void]$index.Add('Items per category: ' + (($categoryOrder | ForEach-Object { '{0} {1}' -f $countByCategory[$_], $_ }) -join ', ') + '.')
[void]$index.Add('')
[void]$index.Add('## Rules for reading this layer')
[void]$index.Add('')
[void]$index.Add('- Every page starts with the banner `GENERATED FROM data/ ' + $script:EmDash + ' DO NOT EDIT`.')
[void]$index.Add('- Edit `data/`, then run `tools/gen-wiki.ps1`; never edit a page under `content/` by hand.')
[void]$index.Add('- `tools/verify-wiki.ps1` fails when the committed pages differ from a fresh generation, when a link or anchor is broken, when a data id has no section, or when a `systems/` or `gotchas/` page is not linked from the wiki index.')
[void]$index.Add('')
Add-Page 'index.md' $index.ToArray()

# ---------------------------------------------------------------------------
# items.md
# ---------------------------------------------------------------------------
$itemsPage = New-Object System.Collections.Generic.List[string]
[void]$itemsPage.Add((Get-Banner))
[void]$itemsPage.Add('')
[void]$itemsPage.Add('# Items')
[void]$itemsPage.Add('')
[void]$itemsPage.Add((('Every piece in `data/tables/item.csv` ({0} items) and `data/tables/bag.csv` ({1} bags), in file order. ' +
        'A piece is authored once at the tier its row carries; `data/tables/merge_recipe.csv` takes it to `constant.max_tier` ({2}). ' +
        'Floors are the `shop_pool` band for the piece, not a promise it appears in every run.') -f $script:Items.Count, $script:Bags.Count, $script:MaxTier))
[void]$itemsPage.Add('')
[void]$itemsPage.Add('## Contents')
[void]$itemsPage.Add('')
foreach ($item in $script:Items) {
    [void]$itemsPage.Add(('- [{0} ({1})](#{1})' -f (Get-Thai $item.name_key), $item.id))
}
foreach ($bag in $script:Bags) {
    [void]$itemsPage.Add(('- [{0} ({1})](#{1})' -f (Get-Thai $bag.name_key), $bag.id))
}
[void]$itemsPage.Add('')
[void]$itemsPage.Add('## Items')
[void]$itemsPage.Add('')

foreach ($item in $script:Items) {
    $id = [string]$item.id
    $th = Get-Thai $item.name_key
    $en = Get-English $item.name_key
    $foot = Get-FootprintInfo ([string]$item.footprint)
    $tags = Get-Tags ([string]$item.tags)
    $tagText = if ($tags.Count -eq 0) { '(none)' } else { (($tags | ForEach-Object { '`{0}`' -f $_ }) -join ', ') }

    [void]$itemsPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$itemsPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$itemsPage.Add('')
    [void]$itemsPage.Add('| Field | Value |')
    [void]$itemsPage.Add('| --- | --- |')
    [void]$itemsPage.Add(('| id | `{0}` |' -f $id))
    [void]$itemsPage.Add(('| category | `{0}` |' -f $item.category))
    [void]$itemsPage.Add(('| tier | {0} (of {1}) |' -f $item.tier, $script:MaxTier))
    [void]$itemsPage.Add(('| element | `{0}` |' -f $item.element))
    [void]$itemsPage.Add(('| price | {0} bia |' -f $item.price))
    [void]$itemsPage.Add(('| weight | {0} |' -f $item.weight))
    [void]$itemsPage.Add(('| footprint | `{0}` ({1}x{2}, {3} cell(s)) |' -f $item.footprint, $foot.Rows, $foot.Cols, $foot.Cells))
    [void]$itemsPage.Add(('| tags | {0} |' -f $tagText))

    if ([string]::IsNullOrEmpty([string]$item.skill_id)) {
        [void]$itemsPage.Add('| skill | (none - this piece does not cast) |')
    }
    else {
        $skill = $script:Skills | Where-Object { [string]$_.id -eq [string]$item.skill_id } | Select-Object -First 1
        if ($null -eq $skill) {
            [void]$itemsPage.Add(('| skill | `{0}` (no such skill row) |' -f $item.skill_id))
        }
        else {
            [void]$itemsPage.Add(('| skill | {0} |' -f (Get-RowLink 'skills.md' ([string]$skill.id) ([string]$skill.name_key))))
        }
    }

    $mergeSteps = Get-MergeSteps $id
    if ($mergeSteps.Count -eq 0) {
        [void]$itemsPage.Add('| merges | (no merge recipe) |')
    }
    else {
        [void]$itemsPage.Add(('| merges | {0} |' -f ($mergeSteps -join ', ')))
    }

    $shopRows = Get-ShopRows $id
    if ($shopRows.Count -eq 0) {
        [void]$itemsPage.Add('| shop | not in the shop pool |')
    }
    else {
        $bands = @($shopRows | ForEach-Object { 'floor {0}-{1} (weight {2})' -f $_.floor_min, $_.floor_max, $_.weight })
        [void]$itemsPage.Add(('| shop | {0} |' -f ($bands -join '; ')))
    }

    [void]$itemsPage.Add('')
    [void]$itemsPage.Add(('**{0}.** {1}' -f $en, (Get-Thai $item.description_key)))
    [void]$itemsPage.Add('')

    $lines = New-Object System.Collections.Generic.List[string]
    foreach ($rule in $script:Synergies) {
        if ([string]$rule.kind -eq 'BagArea') { continue }

        $roles = New-Object System.Collections.Generic.List[string]
        if ($tags -contains [string]$rule.tag_a) { [void]$roles.Add('tag_a') }
        if ($tags -contains [string]$rule.tag_b) { [void]$roles.Add('tag_b') }
        if ($roles.Count -eq 0) { continue }

        $partners = New-Object System.Collections.Generic.List[string]
        foreach ($other in $script:Items) {
            if ([string]$other.id -eq $id) { continue }
            $otherTags = Get-Tags ([string]$other.tags)
            if (($roles -contains 'tag_a') -and ($otherTags -contains [string]$rule.tag_b)) {
                [void]$partners.Add((Get-RowLink 'items.md' ([string]$other.id) ([string]$other.name_key)))
            }
            elseif (($roles -contains 'tag_b') -and ($otherTags -contains [string]$rule.tag_a)) {
                [void]$partners.Add((Get-RowLink 'items.md' ([string]$other.id) ([string]$other.name_key)))
            }
        }

        $roleText = $roles -join ' + '
        $valueText = Get-MagnitudeText 'Percent' ([string]$rule.value_percent)
        $partnerText = if ($partners.Count -eq 0) { 'no shipped partner for this side' } else { ($partners -join ', ') }
        [void]$lines.Add(('- {0} - holds `{1}` as **{2}**: `{3}` {4}, max {5} stack(s). Partner side: {6}.' -f
                (Get-RowLink 'synergies.md' ([string]$rule.id) ([string]$rule.name_key)),
                $(if ($roles -contains 'tag_a') { $rule.tag_a } else { $rule.tag_b }),
                $roleText, $rule.stat, $valueText, $rule.max_stacks, $partnerText))
    }

    if ($lines.Count -eq 0) {
        [void]$itemsPage.Add('**Combos this piece joins:** none. Its tags match neither side of a shipped `synergy` rule (the bag-area rule is added below).')
    }
    else {
        [void]$itemsPage.Add('**Combos this piece joins (computed from the tags in `data/tables/synergy.csv`):**')
        [void]$itemsPage.Add('')
        foreach ($line in $lines) { [void]$itemsPage.Add($line) }
    }

    [void]$itemsPage.Add('')
    $bagArea = $script:Synergies | Where-Object { [string]$_.kind -eq 'BagArea' } | Select-Object -First 1
    if ($null -ne $bagArea) {
        [void]$itemsPage.Add(('- {0} - applies while this item stands on **any** bag: `{1}` {2}, max {3} stack(s). The bag itself gets no bonus.' -f
                (Get-RowLink 'synergies.md' ([string]$bagArea.id) ([string]$bagArea.name_key)),
                $bagArea.stat, (Get-MagnitudeText 'Percent' ([string]$bagArea.value_percent)), $bagArea.max_stacks))
        [void]$itemsPage.Add('')
    }

    $applicableVariants = Get-ApplicableVariants $item
    if ($applicableVariants.Count -eq 0) {
        [void]$itemsPage.Add('**Upgrades:** no `upgrade_variant` row applies to this piece; a merge folds the pair without a choice.')
    }
    else {
        [void]$itemsPage.Add('**Upgrades that can be offered for this piece:**')
        [void]$itemsPage.Add('')
        foreach ($variant in $applicableVariants) {
            [void]$itemsPage.Add(('- {0} - tier {1}->{2}' -f
                    (Get-RowLink 'upgrades.md' ([string]$variant.id) ([string]$variant.name_key)),
                    $variant.from_tier, $variant.to_tier))
        }
    }

    [void]$itemsPage.Add('')
}

[void]$itemsPage.Add('## Bags')
[void]$itemsPage.Add('')
[void]$itemsPage.Add('Bags open usable slots; they carry no combo tags and no upgrade variants. They are placed on the board first, and the items above must be fully covered by a bag.')
[void]$itemsPage.Add('')

foreach ($bag in $script:Bags) {
    $id = [string]$bag.id
    $th = Get-Thai $bag.name_key
    $en = Get-English $bag.name_key
    $foot = Get-FootprintInfo ([string]$bag.footprint)

    [void]$itemsPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$itemsPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$itemsPage.Add('')
    [void]$itemsPage.Add('| Field | Value |')
    [void]$itemsPage.Add('| --- | --- |')
    [void]$itemsPage.Add(('| id | `{0}` |' -f $id))
    [void]$itemsPage.Add('| category | `Bag` |')
    [void]$itemsPage.Add(('| tier | {0} (of {1}) |' -f $bag.tier, $script:MaxTier))
    [void]$itemsPage.Add('| element | `None` |')
    [void]$itemsPage.Add(('| price | {0} bia |' -f $bag.price))
    [void]$itemsPage.Add(('| weight | {0} |' -f $bag.weight))
    [void]$itemsPage.Add(('| footprint | `{0}` ({1}x{2}, {3} cell(s)) |' -f $bag.footprint, $foot.Rows, $foot.Cols, $foot.Cells))
    [void]$itemsPage.Add('| tags | (none - bags are not matched by a synergy tag) |')
    [void]$itemsPage.Add('| skill | (none - a bag does not cast) |')

    $mergeSteps = Get-MergeSteps $id
    if ($mergeSteps.Count -eq 0) {
        [void]$itemsPage.Add('| merges | (no merge recipe) |')
    }
    else {
        [void]$itemsPage.Add(('| merges | {0} |' -f ($mergeSteps -join ', ')))
    }

    $shopRows = Get-ShopRows $id
    if ($shopRows.Count -eq 0) {
        [void]$itemsPage.Add('| shop | not in the shop pool |')
    }
    else {
        $bands = @($shopRows | ForEach-Object { 'floor {0}-{1} (weight {2})' -f $_.floor_min, $_.floor_max, $_.weight })
        [void]$itemsPage.Add(('| shop | {0} |' -f ($bands -join '; ')))
    }

    [void]$itemsPage.Add('')
    # bag.csv has no description_key column; the localization table names a
    # bag's description '<id>.desc' (the same shape the item rows spell out).
    # A missing row still prints '(missing: <key>)' rather than a guess.
    $bagDescriptionKey = [string]$bag.description_key
    if ([string]::IsNullOrWhiteSpace($bagDescriptionKey)) { $bagDescriptionKey = ($id + '.desc') }
    [void]$itemsPage.Add(('**{0}.** {1}' -f $en, (Get-Thai $bagDescriptionKey)))
    [void]$itemsPage.Add('')
    [void]$itemsPage.Add('**Combos this piece joins:** none - the `synergy` tags live on items, and a bag is not a tag carrier. The bag-area rule is resolved for the items standing on it, not for the bag.')
    [void]$itemsPage.Add('')
    [void]$itemsPage.Add('**Upgrades:** no `upgrade_variant` row names `Bag`, so a bag merge has an empty choice pool at both tiers and folds immediately.')
    [void]$itemsPage.Add('')
}

[void]$itemsPage.Add((Get-SeeAlso 'items.md'))
Add-Page 'items.md' $itemsPage.ToArray()

# ---------------------------------------------------------------------------
# skills.md
# ---------------------------------------------------------------------------
$skillsPage = New-Object System.Collections.Generic.List[string]
[void]$skillsPage.Add((Get-Banner))
[void]$skillsPage.Add('')
[void]$skillsPage.Add('# Skills')
[void]$skillsPage.Add('')
[void]$skillsPage.Add(('One section per row of `data/tables/skill.csv` ({0} skills), in file order. A piece casts the skill named by `item.skill_id`; the battle auto-casts it when ready and affordable.' -f $script:Skills.Count))
[void]$skillsPage.Add('')

foreach ($skill in $script:Skills) {
    $id = [string]$skill.id
    $th = Get-Thai $skill.name_key
    $en = Get-English $skill.name_key

    [void]$skillsPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$skillsPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$skillsPage.Add('')
    [void]$skillsPage.Add('| Field | Value |')
    [void]$skillsPage.Add('| --- | --- |')
    [void]$skillsPage.Add(('| behavior | `{0}` |' -f $skill.behavior))
    [void]$skillsPage.Add(('| element | `{0}` |' -f $skill.element))
    [void]$skillsPage.Add(('| cooldown_seconds | {0} |' -f $skill.cooldown_seconds))
    [void]$skillsPage.Add(('| mp_cost | {0} |' -f $skill.mp_cost))
    [void]$skillsPage.Add(('| damage | {0} |' -f $skill.damage))
    [void]$skillsPage.Add(('| range | {0} |' -f $skill.range))
    [void]$skillsPage.Add(('| pierce_count | {0} |' -f $skill.pierce_count))
    [void]$skillsPage.Add(('| projectile_count | {0} |' -f $skill.projectile_count))
    [void]$skillsPage.Add(('| projectile_speed | {0} |' -f $skill.projectile_speed))
    [void]$skillsPage.Add('')

    $users = @($script:Items | Where-Object { [string]$_.skill_id -eq $id })
    if ($users.Count -eq 0) {
        [void]$skillsPage.Add('**Used by:** no item row names this skill.')
    }
    else {
        [void]$skillsPage.Add('**Used by:** ' + (($users | ForEach-Object { Get-RowLink 'items.md' ([string]$_.id) ([string]$_.name_key) }) -join ', ') + '.')
    }

    [void]$skillsPage.Add('')
    [void]$skillsPage.Add('See also: [synergy rules](synergies.md) that change this skill''s damage or cooldown through its owner, and the [upgrade variants](upgrades.md) offered when its owner merges.')
    [void]$skillsPage.Add('')
}

[void]$skillsPage.Add((Get-SeeAlso 'skills.md'))
Add-Page 'skills.md' $skillsPage.ToArray()

# ---------------------------------------------------------------------------
# synergies.md
# ---------------------------------------------------------------------------
$synergiesPage = New-Object System.Collections.Generic.List[string]
[void]$synergiesPage.Add((Get-Banner))
[void]$synergiesPage.Add('')
[void]$synergiesPage.Add('# Synergy (combo) rules')
[void]$synergiesPage.Add('')
[void]$synergiesPage.Add((('All {0} rows of `data/tables/synergy.csv`, in file order. The rules are resolved into the loadout before a battle starts; the battle does not re-evaluate them. ' +
        '`AdjacentPair` counts orthogonal neighbours only; `SameRow` and `SameColumn` are different rules, not one "same line" rule.') -f $script:Synergies.Count))
[void]$synergiesPage.Add('')
[void]$synergiesPage.Add('| Rule | kind | tag_a | tag_b | stat | value_percent | max_stacks |')
[void]$synergiesPage.Add('| --- | --- | --- | --- | --- | --- | --- |')
foreach ($rule in $script:Synergies) {
    $tagA = if ([string]::IsNullOrEmpty([string]$rule.tag_a)) { '-' } else { '`{0}`' -f $rule.tag_a }
    $tagB = if ([string]::IsNullOrEmpty([string]$rule.tag_b)) { '-' } else { '`{0}`' -f $rule.tag_b }
    [void]$synergiesPage.Add(('| {0} | `{1}` | {2} | {3} | `{4}` | {5} | {6} |' -f
            (Get-RowLink 'synergies.md' ([string]$rule.id) ([string]$rule.name_key)),
            $rule.kind, $tagA, $tagB, $rule.stat, $rule.value_percent, $rule.max_stacks))
}
[void]$synergiesPage.Add('')

foreach ($rule in $script:Synergies) {
    $id = [string]$rule.id
    $th = Get-Thai $rule.name_key
    $en = Get-English $rule.name_key
    $kind = [string]$rule.kind

    [void]$synergiesPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$synergiesPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$synergiesPage.Add('')
    [void]$synergiesPage.Add(('**Kind:** `{0}`. **Stat:** `{1}` {2}. **Max stacks:** {3}.' -f
            $kind, $rule.stat, (Get-MagnitudeText 'Percent' ([string]$rule.value_percent)), $rule.max_stacks))
    [void]$synergiesPage.Add('')

    if ($kind -eq 'BagArea') {
        [void]$synergiesPage.Add('**tag_a / tag_b:** none - this rule is not tag-scoped. It applies to every **item** standing on a bag (not to the bag itself).')
        [void]$synergiesPage.Add('')
        [void]$synergiesPage.Add('Affected pieces: every item in [items.md](items.md) while it is fully covered by a bag; a piece in the chest gets nothing.')
        [void]$synergiesPage.Add('')
        continue
    }

    $sideA = @($script:Items | Where-Object { (Get-Tags ([string]$_.tags)) -contains [string]$rule.tag_a })
    $sideB = @($script:Items | Where-Object { (Get-Tags ([string]$_.tags)) -contains [string]$rule.tag_b })

    $aText = if ($sideA.Count -eq 0) { '(no item carries this tag)' } else { (($sideA | ForEach-Object { Get-RowLink 'items.md' ([string]$_.id) ([string]$_.name_key) }) -join ', ') }
    $bText = if ($sideB.Count -eq 0) { '(no item carries this tag)' } else { (($sideB | ForEach-Object { Get-RowLink 'items.md' ([string]$_.id) ([string]$_.name_key) }) -join ', ') }

    [void]$synergiesPage.Add(('**tag_a `{0}` pieces ({1}):** {2}' -f $rule.tag_a, $sideA.Count, $aText))
    [void]$synergiesPage.Add('')
    [void]$synergiesPage.Add(('**tag_b `{0}` pieces ({1}):** {2}' -f $rule.tag_b, $sideB.Count, $bText))
    [void]$synergiesPage.Add('')

    if ($kind -eq 'AdjacentPair') {
        [void]$synergiesPage.Add(('Pairs: {0} x {1} = {2} possible side-by-side combinations (orthogonal neighbours only).' -f $sideA.Count, $sideB.Count, ($sideA.Count * $sideB.Count)))
    }
    elseif ($kind -eq 'SameRow') {
        [void]$synergiesPage.Add(('Pairs: {0} x {1} = {2} possible same-row combinations (a shared column does not fire this rule).' -f $sideA.Count, $sideB.Count, ($sideA.Count * $sideB.Count)))
    }
    elseif ($kind -eq 'SameColumn') {
        [void]$synergiesPage.Add(('Pairs: {0} x {1} = {2} possible same-column combinations (a shared row does not fire this rule).' -f $sideA.Count, $sideB.Count, ($sideA.Count * $sideB.Count)))
    }
    [void]$synergiesPage.Add('')
    [void]$synergiesPage.Add(('See the effect on each piece in [items.md](items.md); the stat is applied to both sides where the rule says so (`{0}`).' -f $rule.stat))
    [void]$synergiesPage.Add('')
}

[void]$synergiesPage.Add((Get-SeeAlso 'synergies.md'))
Add-Page 'synergies.md' $synergiesPage.ToArray()

# ---------------------------------------------------------------------------
# upgrades.md
# ---------------------------------------------------------------------------
$upgradesPage = New-Object System.Collections.Generic.List[string]
[void]$upgradesPage.Add((Get-Banner))
[void]$upgradesPage.Add('')
[void]$upgradesPage.Add('# Upgrade variants')
[void]$upgradesPage.Add('')
[void]$upgradesPage.Add((('All {0} rows of `data/tables/upgrade_variant.csv` and their {1} modifier rows, in file order. A merge offers at most `constant.upgrade_choice_count` ({2}) weighted choices from the variants that apply at that tier. ' +
        'Eligibility follows `UpgradePool.Applies`: a non-empty `category` matches the item''s category; otherwise a non-empty `tag` matches one of the item''s tags. Only items have variants; `bag` rows have no category and no tag, so a bag merge has an empty pool.') -f
        $script:Variants.Count, $script:Modifiers.Count, $script:Constant.upgrade_choice_count))
[void]$upgradesPage.Add('')

$tierOrder = New-Object System.Collections.Generic.List[string]
foreach ($variant in $script:Variants) {
    $key = [string]$variant.from_tier
    if (-not $tierOrder.Contains($key)) { [void]$tierOrder.Add($key) }
}
foreach ($tier in $tierOrder) {
    $group = @($script:Variants | Where-Object { [string]$_.from_tier -eq $tier })
    $toTier = [string]$group[0].to_tier
    [void]$upgradesPage.Add(('## Tier {0} -> {1}' -f $tier, $toTier))
    [void]$upgradesPage.Add('')
    [void]$upgradesPage.Add('| Variant | Applies by | Weight | Modifiers |')
    [void]$upgradesPage.Add('| --- | --- | --- | --- |')
    foreach ($variant in $group) {
        $scope = if (-not [string]::IsNullOrEmpty([string]$variant.category)) { 'category `{0}`' -f $variant.category } else { 'tag `{0}`' -f $variant.tag }
        $mods = @($script:Modifiers | Where-Object { [string]$_.variant_id -eq [string]$variant.id } | ForEach-Object {
                '`{0}` {1}' -f $_.stat, (Get-MagnitudeText ([string]$_.method) ([string]$_.magnitude))
            })
        [void]$upgradesPage.Add(('| {0} | {1} | {2} | {3} |' -f
                (Get-RowLink 'upgrades.md' ([string]$variant.id) ([string]$variant.name_key)),
                $scope, $variant.weight, ($mods -join '; ')))
    }
    [void]$upgradesPage.Add('')
}

foreach ($variant in $script:Variants) {
    $id = [string]$variant.id
    $th = Get-Thai $variant.name_key
    $en = Get-English $variant.name_key

    [void]$upgradesPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$upgradesPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$upgradesPage.Add('')
    [void]$upgradesPage.Add(('**Tier:** {0} -> {1}. **Weight:** {2}.' -f $variant.from_tier, $variant.to_tier, $variant.weight))
    [void]$upgradesPage.Add('')

    if (-not [string]::IsNullOrEmpty([string]$variant.category)) {
        [void]$upgradesPage.Add(('**Applies by:** category `{0}` (the category wins when it is set).' -f $variant.category))
    }
    elseif (-not [string]::IsNullOrEmpty([string]$variant.tag)) {
        [void]$upgradesPage.Add(('**Applies by:** tag `{0}` (checked only when `category` is empty).' -f $variant.tag))
    }
    else {
        [void]$upgradesPage.Add('**Applies by:** neither category nor tag is set, so the rule cannot match a piece.')
    }
    [void]$upgradesPage.Add('')

    $mods = @($script:Modifiers | Where-Object { [string]$_.variant_id -eq $id })
    if ($mods.Count -eq 0) {
        [void]$upgradesPage.Add('**Modifiers:** none.')
    }
    else {
        [void]$upgradesPage.Add('**Modifiers:**')
        [void]$upgradesPage.Add('')
        [void]$upgradesPage.Add('| stat | method | magnitude | displayed |')
        [void]$upgradesPage.Add('| --- | --- | --- | --- |')
        foreach ($mod in $mods) {
            [void]$upgradesPage.Add(('| `{0}` | `{1}` | {2} | {3} |' -f $mod.stat, $mod.method, $mod.magnitude, (Get-MagnitudeText ([string]$mod.method) ([string]$mod.magnitude))))
        }
    }
    [void]$upgradesPage.Add('')

    $eligible = @($script:Items | Where-Object { Test-VariantApplies $variant $_ })
    if ($eligible.Count -eq 0) {
        [void]$upgradesPage.Add('**Eligible pieces:** none in the current data.')
    }
    else {
        [void]$upgradesPage.Add(('**Eligible pieces ({0}):** {1}.' -f $eligible.Count, (($eligible | ForEach-Object { Get-RowLink 'items.md' ([string]$_.id) ([string]$_.name_key) }) -join ', ')))
    }
    [void]$upgradesPage.Add('')
}

[void]$upgradesPage.Add((Get-SeeAlso 'upgrades.md'))
Add-Page 'upgrades.md' $upgradesPage.ToArray()

# ---------------------------------------------------------------------------
# enemies.md
# ---------------------------------------------------------------------------
$enemiesPage = New-Object System.Collections.Generic.List[string]
[void]$enemiesPage.Add((Get-Banner))
[void]$enemiesPage.Add('')
[void]$enemiesPage.Add('# Enemies')
[void]$enemiesPage.Add('')
[void]$enemiesPage.Add(('All {0} rows of `data/tables/enemy.csv`, in file order. "Appears on" is computed from `data/tables/wave.csv`; the full timed timeline is in [floors.md](floors.md).' -f $script:Enemies.Count))
[void]$enemiesPage.Add('')

foreach ($enemy in $script:Enemies) {
    $id = [string]$enemy.id
    $th = Get-Thai $enemy.name_key
    $en = Get-English $enemy.name_key

    [void]$enemiesPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$enemiesPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$enemiesPage.Add('')
    [void]$enemiesPage.Add('| Field | Value |')
    [void]$enemiesPage.Add('| --- | --- |')
    [void]$enemiesPage.Add(('| role | `{0}` |' -f $enemy.role))
    [void]$enemiesPage.Add(('| max_hp | {0} |' -f $enemy.max_hp))
    [void]$enemiesPage.Add(('| move_speed | {0} |' -f $enemy.move_speed))
    [void]$enemiesPage.Add(('| contact_damage | {0} |' -f $enemy.contact_damage))
    [void]$enemiesPage.Add(('| attack_cooldown_seconds | {0} |' -f $enemy.attack_cooldown_seconds))
    [void]$enemiesPage.Add(('| move_pattern | `{0}` |' -f $enemy.move_pattern))
    [void]$enemiesPage.Add(('| is_elite | {0} |' -f $enemy.is_elite))
    [void]$enemiesPage.Add(('| is_boss | {0} |' -f $enemy.is_boss))
    [void]$enemiesPage.Add(('| boss_phase2_hp_percent | {0} |' -f $enemy.boss_phase2_hp_percent))
    [void]$enemiesPage.Add(('| drop_value | {0} bia per kill |' -f $enemy.drop_value))
    [void]$enemiesPage.Add('')

    $appearances = @($script:Waves | Where-Object { [string]$_.enemy_id -eq $id })
    if ($appearances.Count -eq 0) {
        [void]$enemiesPage.Add('**Appears on:** no wave row names this enemy.')
    }
    else {
        $floorLines = New-Object System.Collections.Generic.List[string]
        foreach ($wave in $appearances) {
            $floor = $script:Floors | Where-Object { [string]$_.id -eq [string]$wave.floor_id } | Select-Object -First 1
            if ($null -eq $floor) {
                [void]$floorLines.Add(('`{0}` (no such floor)' -f $wave.floor_id))
            }
            else {
                [void]$floorLines.Add(('{0} - wave `{1}`: {2} spawn(s) from {3}s to {4}s every {5}s' -f
                        (Get-RowLink 'floors.md' ([string]$floor.id) ([string]$floor.name_key)),
                        $wave.id, $wave.spawn_count, $wave.start_time_seconds, $wave.end_time_seconds, $wave.spawn_interval_seconds))
            }
        }
        [void]$enemiesPage.Add('**Appears on:**')
        [void]$enemiesPage.Add('')
        foreach ($line in $floorLines) { [void]$enemiesPage.Add('- ' + $line) }
    }

    [void]$enemiesPage.Add('')
}

[void]$enemiesPage.Add((Get-SeeAlso 'enemies.md'))
Add-Page 'enemies.md' $enemiesPage.ToArray()

# ---------------------------------------------------------------------------
# floors.md
# ---------------------------------------------------------------------------
$floorPage = New-Object System.Collections.Generic.List[string]
[void]$floorPage.Add((Get-Banner))
[void]$floorPage.Add('')
[void]$floorPage.Add('# Floors and wave timelines')
[void]$floorPage.Add('')
[void]$floorPage.Add(('All {0} rows of `data/tables/floor.csv` and the {1} `wave` rows, in file order. A floor ends when its wave timeline is survived, or (boss floors) when the boss dies.' -f $script:Floors.Count, $script:Waves.Count))
[void]$floorPage.Add('')
[void]$floorPage.Add('| Floor | index | duration (s) | boss | waves | total spawns |')
[void]$floorPage.Add('| --- | --- | --- | --- | --- | --- |')
foreach ($floor in $script:Floors) {
    $floorWaves = @($script:Waves | Where-Object { [string]$_.floor_id -eq [string]$floor.id })
    $total = 0
    foreach ($wave in $floorWaves) { $total = $total + [int]$wave.spawn_count }
    $bossText = if ([string]$floor.is_boss -eq 'true') { 'yes (`{0}`)' -f $floor.boss_enemy_id } else { 'no' }
    [void]$floorPage.Add(('| {0} | {1} | {2} | {3} | {4} | {5} |' -f
            (Get-RowLink 'floors.md' ([string]$floor.id) ([string]$floor.name_key)),
            $floor.index, $floor.duration_seconds, $bossText, $floorWaves.Count, $total))
}
[void]$floorPage.Add('')

foreach ($floor in $script:Floors) {
    $id = [string]$floor.id
    $th = Get-Thai $floor.name_key
    $en = Get-English $floor.name_key
    $floorWaves = @($script:Waves | Where-Object { [string]$_.floor_id -eq $id })

    [void]$floorPage.Add(('<a id="{0}"></a>' -f $id))
    [void]$floorPage.Add(('### {0} - {1} ({2})' -f $id, $th, $en))
    [void]$floorPage.Add('')
    [void]$floorPage.Add(('**Index:** {0}. **Duration:** {1}s. **Boss floor:** {2}.' -f $floor.index, $floor.duration_seconds, $floor.is_boss))
    [void]$floorPage.Add('')

    if ([string]$floor.is_boss -eq 'true') {
        $boss = $script:Enemies | Where-Object { [string]$_.id -eq [string]$floor.boss_enemy_id } | Select-Object -First 1
        if ($null -eq $boss) {
            [void]$floorPage.Add(('**Boss:** `{0}` (no such enemy row).' -f $floor.boss_enemy_id))
        }
        else {
            [void]$floorPage.Add(('**Boss:** {0} - phase 2 at {1}% HP.' -f (Get-RowLink 'enemies.md' ([string]$boss.id) ([string]$boss.name_key)), $boss.boss_phase2_hp_percent))
        }
        [void]$floorPage.Add('')
    }

    if ($floorWaves.Count -eq 0) {
        [void]$floorPage.Add('**Waves:** none.')
    }
    else {
        [void]$floorPage.Add('**Wave timeline (data order):**')
        [void]$floorPage.Add('')
        [void]$floorPage.Add('| Wave | Enemy | start (s) | end (s) | spawn_count | interval (s) | elite |')
        [void]$floorPage.Add('| --- | --- | --- | --- | --- | --- | --- |')
        foreach ($wave in $floorWaves) {
            $enemy = $script:Enemies | Where-Object { [string]$_.id -eq [string]$wave.enemy_id } | Select-Object -First 1
            $enemyText = if ($null -eq $enemy) { '`{0}` (no such enemy)' -f $wave.enemy_id } else { Get-RowLink 'enemies.md' ([string]$enemy.id) ([string]$enemy.name_key) }
            [void]$floorPage.Add(('| `{0}` | {1} | {2} | {3} | {4} | {5} | {6} |' -f
                    $wave.id, $enemyText, $wave.start_time_seconds, $wave.end_time_seconds, $wave.spawn_count, $wave.spawn_interval_seconds, $wave.is_elite))
        }
        [void]$floorPage.Add('')
        $total = 0
        foreach ($wave in $floorWaves) { $total = $total + [int]$wave.spawn_count }
        [void]$floorPage.Add(('Total spawns in the data: {0}. The interval is the gap between spawns inside that wave''s window.' -f $total))
    }

    [void]$floorPage.Add('')
}

[void]$floorPage.Add((Get-SeeAlso 'floors.md'))
Add-Page 'floors.md' $floorPage.ToArray()

# ---------------------------------------------------------------------------
# economy.md
# ---------------------------------------------------------------------------
$economyPage = New-Object System.Collections.Generic.List[string]
[void]$economyPage.Add((Get-Banner))
[void]$economyPage.Add('')
[void]$economyPage.Add('# Economy and constants')
[void]$economyPage.Add('')
[void]$economyPage.Add('The single row of `data/tables/economy.csv` and the single row of `data/tables/constant.csv`. These are the run-wide numbers; item prices and weights are on [items.md](items.md), and enemy drop values on [enemies.md](enemies.md).')
[void]$economyPage.Add('')
[void]$economyPage.Add('## economy.csv')
[void]$economyPage.Add('')
[void]$economyPage.Add('| Field | Value |')
[void]$economyPage.Add('| --- | --- |')
foreach ($property in $script:Economy.PSObject.Properties) {
    if ($property.Name -eq '__row') { continue }
    [void]$economyPage.Add(('| `{0}` | {1} |' -f $property.Name, $property.Value))
}
[void]$economyPage.Add('')
[void]$economyPage.Add('How the run consumes these: starting purse, a shop of `shop_slot_count` slots per sala, a re-roll priced `refresh_base_cost + refresh_cost_increment x refreshes this camp`, sell-back at `sell_back_percent`, a clear heal of `heal_percent_on_clear` (win only), a chest of `chest_slots`, and `clear_reward_bia` on a win. The rules live in [`../systems/run-shop.md`](../systems/run-shop.md).')
[void]$economyPage.Add('')
[void]$economyPage.Add('## constant.csv')
[void]$economyPage.Add('')
[void]$economyPage.Add('| Field | Value |')
[void]$economyPage.Add('| --- | --- |')
foreach ($property in $script:Constant.PSObject.Properties) {
    if ($property.Name -eq '__row') { continue }
    [void]$economyPage.Add(('| `{0}` | {1} |' -f $property.Name, $property.Value))
}
[void]$economyPage.Add('')
[void]$economyPage.Add('Reading notes: `board_width` x `board_height` is the board the bag is placed on; `max_tier` is the merge ceiling; `min_cooldown_seconds` is a floor the resolver clamps a cooldown to, so an upgrade can be visibly capped; `weight_speed_percent_per_unit` and `max_speed_penalty_percent` are the weight penalty; `magnet_speed` and `sweep_timeout_seconds` belong to the end-of-floor sweep. The consumers are [`../systems/backpack.md`](../systems/backpack.md) and [`../systems/battle-simulation.md`](../systems/battle-simulation.md).')
[void]$economyPage.Add('')
[void]$economyPage.Add((Get-SeeAlso 'economy.md'))
Add-Page 'economy.md' $economyPage.ToArray()

# ---------------------------------------------------------------------------
# Write
# ---------------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $OutDir)) {
    [void](New-Item -ItemType Directory -Path $OutDir -Force)
}

$written = New-Object System.Collections.Generic.List[object]
foreach ($page in $script:Pages) {
    $path = Join-Path $OutDir $page.Name
    $content = (($page.Lines -join "`n") + "`n")
    [System.IO.File]::WriteAllText($path, $content, $script:Utf8NoBom)
    [void]$written.Add([pscustomobject]@{
            name  = $page.Name
            lines = $page.Lines.Count
            bytes = $script:Utf8NoBom.GetByteCount($content)
        })
}

$expected = @($script:Pages | ForEach-Object { $_.Name })
foreach ($file in @(Get-ChildItem -LiteralPath $OutDir -Filter '*.md' -File)) {
    if ($expected -notcontains $file.Name) {
        Remove-Item -LiteralPath $file.FullName -Force
        Add-Finding 'stale_removed' ('removed stale page: {0}' -f $file.Name)
    }
}

$report = [pscustomobject]@{
    tool     = 'gen-wiki.ps1'
    stage    = '10e'
    exitCode = 0
    summary  = [pscustomobject]@{
        pages = $script:Pages.Count
        items = $script:Items.Count
        bags  = $script:Bags.Count
    }
    files    = $written.ToArray()
    findings = $script:Findings.ToArray()
}

if (-not $Quiet) {
    if ($Json) {
        $report | ConvertTo-Json -Depth 6
    }
    else {
        Write-Host ''
        Write-Host 'ProjectSurvive - generate llmwiki/content (Stage 10e)' -ForegroundColor Cyan
        Write-Host ('=' * 72)
        foreach ($file in $written) {
            Write-Host ('  {0,-16} {1,4} lines' -f $file.name, $file.lines) -ForegroundColor Gray
        }
        Write-Host ('=' * 72)
        Write-Host ('  {0} page(s) from {1} item(s) and {2} bag(s)' -f $script:Pages.Count, $script:Items.Count, $script:Bags.Count) -ForegroundColor Green
        Write-Host ''
    }
}
elseif ($Json) {
    $report | ConvertTo-Json -Depth 6
}

if (-not $NoWrite) {
    try {
        $artifactDir = Join-Path $script:Root 'artifacts\wiki'
        if (-not (Test-Path -LiteralPath $artifactDir)) {
            [void](New-Item -ItemType Directory -Path $artifactDir -Force)
        }
        $report | ConvertTo-Json -Depth 6 |
            Set-Content -LiteralPath (Join-Path $artifactDir 'gen-wiki-report.json') -Encoding UTF8
    }
    catch {
        Write-Host ('  (warning) could not write gen-wiki-report.json: {0}' -f $_.Exception.Message) -ForegroundColor Yellow
    }
}

exit 0
