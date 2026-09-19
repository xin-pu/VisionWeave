[CmdletBinding()]
param(
    [string]$RepositoryRoot
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)

function Require-Path {
    param([string]$Path, [string]$Kind)

    if (-not (Test-Path -LiteralPath $Path -PathType $Kind)) {
        throw "Required project documentation path is missing: $Path"
    }
}

function Get-EntryBlocks {
    param([string]$Content, [string]$HeadingPattern)

    return [regex]::Matches($Content, "(?ms)^### $HeadingPattern.*?(?=^### |\z)")
}

function Require-Fields {
    param([string]$Entry, [string[]]$Fields, [string]$Description)

    foreach ($field in $Fields) {
        if ($Entry -notmatch [regex]::Escape("**${field}:**")) {
            throw "$Description is missing required field '$field'."
        }
    }
}

Require-Path (Join-Path $RepositoryRoot 'README.md') 'Leaf'
Require-Path (Join-Path $RepositoryRoot 'docs/standards-reference.md') 'Leaf'
foreach ($directory in @('docs/design', 'docs/ledger', 'docs/adr')) {
    Require-Path (Join-Path $RepositoryRoot $directory) 'Container'
}

$standardsReference = Get-Content -LiteralPath (Join-Path $RepositoryRoot 'docs/standards-reference.md') -Raw
if ($standardsReference -match 'REPLACE_WITH_COMMIT_SHA|YYYY-MM-DD') {
    throw 'docs/standards-reference.md still contains adoption placeholders.'
}
if ($standardsReference -notmatch '(?m)^- \*\*Knowledge base repository:\*\*\s+.+$') {
    throw 'docs/standards-reference.md is missing the knowledge-base repository.'
}
if ($standardsReference -notmatch '(?m)^- \*\*Adopted revision:\*\*[ \t]+`?[0-9a-fA-F]{7,64}`?[ \t]*$') {
    throw 'docs/standards-reference.md must contain a pinned commit SHA as the adopted revision.'
}
if ($standardsReference -notmatch '(?m)^- \*\*Last reviewed:\*\*[ \t]+`?\d{4}-\d{2}-\d{2}`?[ \t]*$') {
    throw 'docs/standards-reference.md must contain a YYYY-MM-DD review date.'
}

$deviationsPath = Join-Path $RepositoryRoot 'docs/ledger/standards-deviations.md'
if (Test-Path -LiteralPath $deviationsPath) {
    $deviations = Get-Content -LiteralPath $deviationsPath -Raw
    foreach ($entry in (Get-EntryBlocks -Content $deviations -HeadingPattern 'SD-\d{4}-\d{3}\s+')) {
        Require-Fields -Entry $entry.Value -Fields @('Shared rule', 'Deviation', 'Rationale', 'Risk and mitigation', 'Owner', 'Approved by', 'Approved on', 'Review again', 'Resolution') -Description 'Standards deviation entry'
        if (($entry.Value) -notmatch '\*\*Resolution:\*\*\s+(Open|Retired|Promoted to shared knowledge base)\.') {
            throw 'Standards deviation entry has an invalid Resolution value.'
        }
    }
}

Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'docs/ledger') -Filter '*.md' -File | Where-Object { $_.Name -ne 'standards-deviations.md' } | ForEach-Object {
    $ledger = Get-Content -LiteralPath $_.FullName -Raw
    foreach ($entry in (Get-EntryBlocks -Content $ledger -HeadingPattern 'PL-\d{4}-\d{3}\s+')) {
        Require-Fields -Entry $entry.Value -Fields @('Status', 'Recorded on', 'Scope', 'Observation', 'Decision or next step', 'Evidence', 'Owner', 'Review again') -Description 'Project ledger entry'
        if (($entry.Value) -notmatch '\*\*Status:\*\*\s+(Open|Monitoring|Implemented|Deferred|Closed)') {
            throw 'Project ledger entry has an invalid Status value.'
        }
    }
}

Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'docs/adr') -Filter '*.md' -File | Where-Object { $_.Name -ne '0000-template.md' } | ForEach-Object {
    $adr = Get-Content -LiteralPath $_.FullName -Raw
    if ($adr -notmatch '(?m)^# ADR-\d+\s+') {
        throw "ADR must begin with a numbered title: $($_.FullName)"
    }
    Require-Fields $adr @('Status', 'Date', 'Decision owners') "ADR '$($_.Name)'"
    foreach ($section in @('Context', 'Decision', 'Consequences', 'Alternatives considered', 'Standards impact')) {
        if ($adr -notmatch "(?m)^## $([regex]::Escape($section))\s*$") {
            throw "ADR '$($_.Name)' is missing section '$section'."
        }
    }
}

Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'docs') -Filter '*.md' -File -Recurse | ForEach-Object {
    $documentPath = $_.FullName
    $documentRoot = Split-Path -Parent $documentPath
    $content = Get-Content -LiteralPath $documentPath -Raw
    foreach ($match in [regex]::Matches($content, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Trim()
        if ($target -match '^(https?:|mailto:|#)' -or $target -match '^<') {
            continue
        }
        $target = ($target -split '#')[0]
        if ([string]::IsNullOrWhiteSpace($target)) {
            continue
        }
        $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $documentRoot $target))
        if (-not (Test-Path -LiteralPath $resolvedPath)) {
            throw "Markdown link target does not exist in '$documentPath': $target"
        }
    }
}

if (Test-Path -LiteralPath (Join-Path $RepositoryRoot '.git')) {
    $trackedToolArtifacts = @(git -C $RepositoryRoot ls-files -- docs/superpowers)
    if ($trackedToolArtifacts.Count -gt 0) {
        throw "Local coding-tool artifacts must not be tracked: $($trackedToolArtifacts -join ', ')"
    }
}

Write-Host "Validated project documentation: $RepositoryRoot"
