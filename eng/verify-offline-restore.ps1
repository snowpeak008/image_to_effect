[CmdletBinding()]
param(
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repoRoot 'eng\approved-packages.json'
$nugetConfigPath = Join-Path $repoRoot 'NuGet.config'
$solutionPath = Join-Path $repoRoot 'VFXComposer.sln'
$feedPath = Join-Path $repoRoot '.codex_tmp\w24-phase1-approved-feed'

foreach ($requiredPath in @($manifestPath, $nugetConfigPath, $solutionPath, $feedPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "W24P1-OFFLINE-001: required offline input is absent."
    }
}

[xml]$nugetConfig = Get-Content -Raw -LiteralPath $nugetConfigPath
$sourceNodes = @($nugetConfig.configuration.packageSources.ChildNodes)
if ($sourceNodes.Count -ne 2 -or
    $sourceNodes[0].Name -cne 'clear' -or
    $sourceNodes[1].Name -cne 'add') {
    throw 'W24P1-OFFLINE-002: NuGet source declaration is not exact.'
}

$sources = @($nugetConfig.configuration.packageSources.add)
if ($sources.Count -ne 1 -or
    $sources[0].key -cne 'w24-approved-local' -or
    $sources[0].value -cne '.codex_tmp/w24-phase1-approved-feed') {
    throw 'W24P1-OFFLINE-003: NuGet source allow-list is not exact.'
}

$sourceValue = [string]$sources[0].value
if ([Uri]::IsWellFormedUriString($sourceValue, [UriKind]::Absolute)) {
    throw 'W24P1-OFFLINE-004: a network NuGet source is configured.'
}

$resolvedSource = [IO.Path]::GetFullPath((Join-Path $repoRoot $sourceValue))
if ($resolvedSource -cne [IO.Path]::GetFullPath($feedPath)) {
    throw 'W24P1-OFFLINE-005: NuGet source does not resolve to the approved feed.'
}

$mappingNodes = @($nugetConfig.configuration.packageSourceMapping.ChildNodes)
$mappingSources = @($nugetConfig.configuration.packageSourceMapping.packageSource)
if ($mappingNodes.Count -ne 2 -or
    $mappingNodes[0].Name -cne 'clear' -or
    $mappingNodes[1].Name -cne 'packageSource' -or
    $mappingSources.Count -ne 1 -or
    $mappingSources[0].key -cne 'w24-approved-local' -or
    @($mappingSources[0].package).Count -ne 1 -or
    $mappingSources[0].package.pattern -cne '*') {
    throw 'W24P1-OFFLINE-006: package source mapping is not exact.'
}

$configEntries = @($nugetConfig.configuration.config.add)
if ($configEntries.Count -ne 1 -or
    @($configEntries | Where-Object {
        $_.key -ceq 'globalPackagesFolder' -and
        $_.value -ceq '.codex_tmp/w24-phase1-packages'
    }).Count -ne 1) {
    throw 'W24P1-OFFLINE-007: NuGet cache configuration is not exact.'
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.schema -ne 'w24-phase1-approved-packages/1') {
    throw 'W24P1-OFFLINE-008: package manifest schema is not accepted.'
}

$approved = @($manifest.packages)
if ($approved.Count -ne [int]$manifest.packageCount -or $approved.Count -eq 0) {
    throw 'W24P1-OFFLINE-009: package manifest count is inconsistent.'
}

$approvedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$approvedById = @{}
foreach ($package in $approved) {
    $fileName = [string]$package.nupkgFile
    if ([string]::IsNullOrWhiteSpace($fileName) -or [IO.Path]::GetFileName($fileName) -ne $fileName) {
        throw 'W24P1-OFFLINE-010: package file identity is invalid.'
    }
    if (-not $approvedNames.Add($fileName)) {
        throw 'W24P1-OFFLINE-011: duplicate approved package file.'
    }

    $packageKey = ([string]$package.id).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($packageKey) -or $approvedById.ContainsKey($packageKey)) {
        throw 'W24P1-OFFLINE-012: duplicate or empty approved package id.'
    }
    $approvedById[$packageKey] = $package

    $packagePath = Join-Path $feedPath $fileName
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw 'W24P1-OFFLINE-013: approved package file is absent.'
    }
    $actualHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $packagePath).Hash.ToLowerInvariant()
    if ($actualHash -cne [string]$package.sha512) {
        throw 'W24P1-OFFLINE-014: approved package hash drifted.'
    }
}

$actualFeedFiles = @(Get-ChildItem -LiteralPath $feedPath -File -Filter '*.nupkg')
if ($actualFeedFiles.Count -ne $approved.Count) {
    throw 'W24P1-OFFLINE-015: local feed contains an undeclared or missing package.'
}
foreach ($file in $actualFeedFiles) {
    if (-not $approvedNames.Contains($file.Name)) {
        throw 'W24P1-OFFLINE-016: local feed contains an undeclared package.'
    }
}

$lockPaths = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'apps'), (Join-Path $repoRoot 'src') -Recurse -File -Filter 'packages.lock.json')
if ($lockPaths.Count -ne 6) {
    throw 'W24P1-OFFLINE-017: lock-file set is not exact.'
}

$relativeLockPaths = [System.Collections.Generic.List[string]]::new()
foreach ($lockPath in $lockPaths) {
    $relativeLockPaths.Add([IO.Path]::GetRelativePath($repoRoot, $lockPath.FullName).Replace('\', '/'))
}
$relativeLockPaths.Sort([StringComparer]::Ordinal)
$lockReceiptLines = @($relativeLockPaths |
    ForEach-Object {
        $relativeLockPath = $_
        $lockFileHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $repoRoot $relativeLockPath)).Hash.ToLowerInvariant()
        "$lockFileHash  $relativeLockPath`n"
    })
$lockSetSha256 = [Convert]::ToHexString(
    [Security.Cryptography.SHA256]::HashData(
        [Text.Encoding]::UTF8.GetBytes(($lockReceiptLines -join ''))
    )
).ToLowerInvariant()

$lockedById = @{}
foreach ($lockPath in $lockPaths) {
    $lock = Get-Content -Raw -LiteralPath $lockPath.FullName | ConvertFrom-Json
    if ([int]$lock.version -ne 2) {
        throw 'W24P1-OFFLINE-018: lock-file version is not accepted.'
    }

    foreach ($framework in $lock.dependencies.PSObject.Properties) {
        foreach ($dependency in $framework.Value.PSObject.Properties) {
            $dependencyType = [string]$dependency.Value.type
            if ($dependencyType -ceq 'Project') {
                continue
            }
            if ($dependencyType -cne 'Direct' -and
                $dependencyType -cne 'Transitive' -and
                $dependencyType -cne 'CentralTransitive') {
                throw 'W24P1-OFFLINE-019: lock-file dependency type is not accepted.'
            }

            $dependencyFields = @($dependency.Value.PSObject.Properties.Name)
            if ($dependencyFields -cnotcontains 'resolved' -or
                $dependencyFields -cnotcontains 'contentHash') {
                throw 'W24P1-OFFLINE-019: package identity is incomplete in a lock file.'
            }

            $dependencyId = $dependency.Name.ToLowerInvariant()
            $resolved = [string]$dependency.Value.resolved
            $contentHash = [string]$dependency.Value.contentHash
            if ($lockedById.ContainsKey($dependencyId)) {
                $prior = $lockedById[$dependencyId]
                if ($prior.resolved -cne $resolved -or $prior.contentHash -cne $contentHash) {
                    throw 'W24P1-OFFLINE-019: package identity differs across lock files.'
                }
            }
            else {
                $lockedById[$dependencyId] = [pscustomobject]@{
                    resolved = $resolved
                    contentHash = $contentHash
                }
            }
        }
    }
}

if ($lockedById.Count -ne $approvedById.Count) {
    throw 'W24P1-OFFLINE-020: manifest and lock-file package sets differ.'
}

foreach ($entry in $approvedById.GetEnumerator()) {
    if (-not $lockedById.ContainsKey($entry.Key)) {
        throw 'W24P1-OFFLINE-021: approved package is absent from the lock-file union.'
    }

    $locked = $lockedById[$entry.Key]
    $approvedPackage = $entry.Value
    try {
        $lockedContentHashBytes = [Convert]::FromBase64String($locked.contentHash)
    }
    catch {
        throw 'W24P1-OFFLINE-022: lock-file package content hash is malformed.'
    }
    if ($locked.resolved -cne [string]$approvedPackage.version -or
        $lockedContentHashBytes.Length -ne 64) {
        throw 'W24P1-OFFLINE-022: lock-file package version or content hash drifted.'
    }
}

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkVersion -cne [string]$manifest.sdkVersion) {
    throw 'W24P1-OFFLINE-023: .NET SDK identity does not match the frozen manifest.'
}

$oldRevocationMode = $env:NUGET_CERT_REVOCATION_MODE
$env:NUGET_CERT_REVOCATION_MODE = 'offline'
try {
    foreach ($packageFile in $actualFeedFiles) {
        & dotnet nuget verify $packageFile.FullName --all --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            throw 'W24P1-OFFLINE-024: NuGet package signature verification failed.'
        }
    }
}
finally {
    $env:NUGET_CERT_REVOCATION_MODE = $oldRevocationMode
}

$runId = [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path $repoRoot ".codex_tmp\w24-phase1-offline-verify\$runId"
$packagesRoot = Join-Path $runRoot 'packages'
New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null

$oldTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$oldFirstRun = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$oldWorkloadNotify = $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE
try {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'

    & dotnet restore $solutionPath --locked-mode --configfile $nugetConfigPath --packages $packagesRoot --no-cache --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'W24P1-OFFLINE-025: locked offline restore failed.'
    }

    foreach ($entry in $approvedById.GetEnumerator()) {
        $approvedPackage = $entry.Value
        $locked = $lockedById[$entry.Key]
        $restoredPackageRoot = Join-Path $packagesRoot (Join-Path $entry.Key ([string]$approvedPackage.version))
        $metadataPath = Join-Path $restoredPackageRoot '.nupkg.metadata'
        $rawHashPath = Join-Path $restoredPackageRoot (([string]$approvedPackage.nupkgFile) + '.sha512')
        $restoredNupkgPath = Join-Path $restoredPackageRoot ([string]$approvedPackage.nupkgFile)
        foreach ($restoredPath in @($metadataPath, $rawHashPath, $restoredNupkgPath)) {
            if (-not (Test-Path -LiteralPath $restoredPath -PathType Leaf)) {
                throw 'W24P1-OFFLINE-025: restored package receipt is incomplete.'
            }
        }

        $metadata = Get-Content -Raw -LiteralPath $metadataPath | ConvertFrom-Json
        $metadataFields = @($metadata.PSObject.Properties.Name)
        if ($metadataFields -cnotcontains 'contentHash' -or
            $metadataFields -cnotcontains 'source' -or
            [string]$metadata.contentHash -cne [string]$locked.contentHash -or
            [IO.Path]::GetFullPath([string]$metadata.source) -cne [IO.Path]::GetFullPath($feedPath)) {
            throw 'W24P1-OFFLINE-025: restored package metadata does not bind the frozen lock and feed.'
        }

        try {
            $restoredRawHash = [Convert]::ToHexString(
                [Convert]::FromBase64String((Get-Content -Raw -LiteralPath $rawHashPath).Trim())
            ).ToLowerInvariant()
        }
        catch {
            throw 'W24P1-OFFLINE-025: restored package raw hash receipt is malformed.'
        }
        $restoredNupkgHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $restoredNupkgPath).Hash.ToLowerInvariant()
        if ($restoredRawHash -cne [string]$approvedPackage.sha512 -or
            $restoredNupkgHash -cne [string]$approvedPackage.sha512) {
            throw 'W24P1-OFFLINE-025: restored package bytes do not match the approved manifest.'
        }
    }

    & dotnet build $solutionPath --no-restore --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw 'W24P1-OFFLINE-026: release build failed.'
    }

    if (-not $SkipTests) {
        & dotnet test $solutionPath --no-restore --configuration Release --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            throw 'W24P1-OFFLINE-027: tests failed.'
        }
    }
}
finally {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $oldTelemetry
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $oldFirstRun
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = $oldWorkloadNotify
}

[ordered]@{
    schema = 'w24-phase1-offline-verification/1'
    status = 'PASS'
    sdkVersion = $sdkVersion
    packageCount = $approved.Count
    lockFileCount = $lockPaths.Count
    signaturesVerified = $actualFeedFiles.Count
    packageManifestSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifestPath).Hash.ToLowerInvariant()
    nugetConfigSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $nugetConfigPath).Hash.ToLowerInvariant()
    lockSetEncoding = 'ordinal-relative-path-sha256-lines-v1'
    lockSetSha256 = $lockSetSha256
    verifierSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PSCommandPath).Hash.ToLowerInvariant()
    solutionSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $solutionPath).Hash.ToLowerInvariant()
    runId = $runId
    testsExecuted = -not $SkipTests
} | ConvertTo-Json -Compress
