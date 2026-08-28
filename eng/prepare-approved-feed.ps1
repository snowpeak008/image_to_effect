[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageBundleDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repoRoot 'eng\approved-packages.json'
$targetPath = Join-Path $repoRoot '.codex_tmp\w24-phase1-approved-feed'

if (-not (Test-Path -LiteralPath $PackageBundleDirectory -PathType Container)) {
    throw 'W24P1-FEED-001: approved package bundle directory is absent.'
}
$sourcePath = (Resolve-Path -LiteralPath $PackageBundleDirectory).Path

if (Test-Path -LiteralPath $targetPath) {
    throw 'W24P1-FEED-002: target feed already exists; verify it instead of overwriting it.'
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.schema -ne 'w24-phase1-approved-packages/1') {
    throw 'W24P1-FEED-003: package manifest schema is not accepted.'
}

$approved = @($manifest.packages)
if ($approved.Count -ne [int]$manifest.packageCount -or $approved.Count -eq 0) {
    throw 'W24P1-FEED-004: package manifest count is inconsistent.'
}

$expectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($package in $approved) {
    $fileName = [string]$package.nupkgFile
    if ([string]::IsNullOrWhiteSpace($fileName) -or [IO.Path]::GetFileName($fileName) -cne $fileName) {
        throw 'W24P1-FEED-005: package file identity is invalid.'
    }

    if (-not $expectedNames.Add($fileName)) {
        throw 'W24P1-FEED-006: package manifest contains a duplicate file identity.'
    }

    $sourceFile = Join-Path $sourcePath $fileName
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
        throw 'W24P1-FEED-007: approved package bundle is incomplete.'
    }

    $sourceHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $sourceFile).Hash.ToLowerInvariant()
    if ($sourceHash -cne [string]$package.sha512) {
        throw 'W24P1-FEED-008: approved package bundle hash does not match the manifest.'
    }
}

$sourcePackages = @(Get-ChildItem -LiteralPath $sourcePath -File -Filter '*.nupkg')
if ($sourcePackages.Count -ne $approved.Count -or
    $sourcePackages.Where({ -not $expectedNames.Contains($_.Name) }).Count -ne 0) {
    throw 'W24P1-FEED-009: approved package bundle file set is not exact.'
}

$targetParent = Split-Path -Parent $targetPath
New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
$pendingPath = Join-Path $targetParent ('w24-phase1-approved-feed.pending-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $pendingPath | Out-Null

try {
    foreach ($package in $approved) {
        $fileName = [string]$package.nupkgFile
        $sourceFile = Join-Path $sourcePath $fileName
        $destinationFile = Join-Path $pendingPath $fileName
        [IO.File]::Copy($sourceFile, $destinationFile, $false)

        $copiedHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $destinationFile).Hash.ToLowerInvariant()
        if ($copiedHash -cne [string]$package.sha512) {
            throw 'W24P1-FEED-010: copied package hash drifted.'
        }
    }

    if (Test-Path -LiteralPath $targetPath) {
        throw 'W24P1-FEED-011: target feed appeared during publication.'
    }

    [IO.Directory]::Move($pendingPath, $targetPath)
    $pendingPath = $null
}
catch {
    # Cleanup is intentionally non-destructive. A failed invocation keeps its
    # unique pending directory for inspection instead of recursively deleting a
    # path that another actor could have replaced with a reparse point.
    throw
}

[ordered]@{
    schema = 'w24-phase1-approved-feed-preparation/1'
    status = 'PASS'
    packageCount = $approved.Count
    manifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToLowerInvariant()
    sourceKind = 'caller-supplied-offline-bundle'
} | ConvertTo-Json -Compress
