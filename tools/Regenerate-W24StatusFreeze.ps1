param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$projectRoot = Join-Path $RepositoryRoot 'project'
$generatedRoot = Join-Path $projectRoot 'Assets\VFX\Generated'
$manifestRoot = Join-Path $projectRoot 'ProjectSettings\VFXComposer\BuildManifests'
$output = Join-Path $RepositoryRoot 'docs\vfx-status\s0a-provisional-status.json'

function IsCanonicalHash([string]$value) { return $value -match '^sha256:[0-9a-f]{64}$' }
function NormalizeHash([string]$value) { if ([string]::IsNullOrEmpty($value)) { return $null }; $normalized = if ($value.StartsWith('sha256:')) { $value } else { 'sha256:' + $value }; if (IsCanonicalHash $normalized) { return $normalized }; return $null }
function FileHash([string]$path) { return 'sha256:' + ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()) }
function IsGuid([string]$value) { return $value -match '^[0-9a-f]{32}$' }
function StringValue($value) { if ($null -eq $value) { return '' }; return [string]$value }
function ProjectPath([string]$path) {
    if ([string]::IsNullOrEmpty($path) -or [IO.Path]::IsPathRooted($path) -or -not $path.StartsWith('Assets/')) { return $null }
    $root = ([IO.Path]::GetFullPath($projectRoot)).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $assets = [IO.Path]::Combine($root, 'Assets') + [IO.Path]::DirectorySeparatorChar
    $candidate = [IO.Path]::GetFullPath([IO.Path]::Combine($root, $path.Replace('/', [IO.Path]::DirectorySeparatorChar)))
    if (-not $candidate.StartsWith($assets, [StringComparison]::OrdinalIgnoreCase)) { return $null }
    return $candidate
}

$entries = foreach ($directory in Get-ChildItem -LiteralPath $generatedRoot -Directory | Sort-Object Name) {
    $effectId = $directory.Name
    $entry = [ordered]@{ EffectId=$effectId; PrefabPath=$null; HasMachineReport=$false; HasRuntimeEntry=$false; RuntimeEntryPathIsValid=$false; RuntimeEntryExists=$false; RuntimeEntryGuidIsVerifiable=$false; RuntimeEntryHashIsVerifiable=$false; RuntimeEntryGuid=$null; RuntimeEntryHash=$null; BuildHash=$null; Maturity='L0_InvalidOrMissing'; WorkingStatus='None' }
    $manifestPath = Join-Path $manifestRoot ($effectId + '.manifest.json')
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        $entry.HasMachineReport = $true
        try { $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -Depth 12 } catch { $manifest = $null }
        if ($null -ne $manifest -and $manifest.effectId -eq $effectId -and $null -ne $manifest.runtimeEntry -and $manifest.runtimeEntry.kind -eq 'prefab' -and -not [string]::IsNullOrEmpty($manifest.runtimeEntry.path)) {
            $entry.HasRuntimeEntry = $true
            $entry.PrefabPath = $manifest.runtimeEntry.path.Replace('\', '/')
            $entry.BuildHash = NormalizeHash ([string]$manifest.buildHash)
            $entry.RuntimeEntryGuid = if ($null -eq $manifest.runtimeEntry.guid) { $null } else { ([string]$manifest.runtimeEntry.guid).ToLowerInvariant() }
            $owned = @($manifest.ownedOutputs | Where-Object { $null -ne $_ -and $_.path -eq $manifest.runtimeEntry.path }) | Select-Object -First 1
            if ($null -ne $owned) { $entry.RuntimeEntryHash = NormalizeHash ([string]$owned.sha256) }
            $absolute = ProjectPath ([string]$manifest.runtimeEntry.path)
            if ($null -ne $absolute) {
                $entry.RuntimeEntryPathIsValid = $true
                $entry.RuntimeEntryExists = Test-Path -LiteralPath $absolute -PathType Leaf
                if ($entry.RuntimeEntryExists) {
                    $metaGuid = $null
                    try { $metaGuid = (Get-Content -LiteralPath ($absolute + '.meta') | Where-Object { $_.StartsWith('guid:') } | Select-Object -First 1).Substring(5).Trim().ToLowerInvariant() } catch { }
                    $entry.RuntimeEntryGuidIsVerifiable = (IsGuid $entry.RuntimeEntryGuid) -and ($null -ne $owned) -and ($entry.RuntimeEntryGuid -eq ([string]$owned.guid).ToLowerInvariant()) -and ($entry.RuntimeEntryGuid -eq $metaGuid)
                    $entry.RuntimeEntryHashIsVerifiable = ($null -ne $owned) -and (IsCanonicalHash $entry.RuntimeEntryHash) -and ($entry.RuntimeEntryHash -eq (FileHash $absolute))
                }
            }
        }
    }
    $verified = $entry.HasMachineReport -and $entry.HasRuntimeEntry -and $entry.RuntimeEntryPathIsValid -and $entry.RuntimeEntryExists -and $entry.RuntimeEntryGuidIsVerifiable -and $entry.RuntimeEntryHashIsVerifiable -and (IsCanonicalHash $entry.BuildHash)
    if ($verified) { $entry.Maturity='L2_VisualPlaceholder'; $entry.WorkingStatus='VISUAL_PENDING' }
    [pscustomobject]$entry
}

$freezeLines = [Text.StringBuilder]::new("W24-S0A-PROVISIONAL-STATUS-V2`n")
foreach ($entry in $entries | Sort-Object EffectId) {
    [void]$freezeLines.Append($entry.EffectId).Append('|').Append((StringValue $entry.PrefabPath)).Append('|').Append($(if($entry.HasMachineReport){'1'}else{'0'})).Append('|').Append($(if($entry.HasRuntimeEntry){'1'}else{'0'})).Append('|').Append($(if($entry.RuntimeEntryPathIsValid){'1'}else{'0'})).Append('|').Append($(if($entry.RuntimeEntryExists){'1'}else{'0'})).Append('|').Append($(if($entry.RuntimeEntryGuidIsVerifiable){'1'}else{'0'})).Append('|').Append($(if($entry.RuntimeEntryHashIsVerifiable){'1'}else{'0'})).Append('|').Append((StringValue $entry.RuntimeEntryGuid)).Append('|').Append((StringValue $entry.RuntimeEntryHash)).Append('|').Append((StringValue $entry.BuildHash)).Append('|').Append($entry.Maturity).Append('|').Append($entry.WorkingStatus).Append("`n")
}
$hashBytes = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($freezeLines.ToString()))
$freezeHash = 'sha256:' + [BitConverter]::ToString($hashBytes).Replace('-', '').ToLowerInvariant()
$document = [ordered]@{
    statusSchema='W24-S0A-PROVISIONAL-STATUS-V2'; scanDate=(Get-Date -Format 'yyyy-MM-dd'); generatedDirectory='Assets/VFX/Generated'; machineReportDirectory='ProjectSettings/VFXComposer/BuildManifests'
    entryRules=[ordered]@{ prefabPath='BuildManifest.runtimeEntry.path'; machineReportPath='ProjectSettings/VFXComposer/BuildManifests/{effectId}.manifest.json'; whenManifestRuntimeEntryIsVerified=[ordered]@{ maturity='L2_VisualPlaceholder'; workingStatus='VISUAL_PENDING'; hasW24VisualQa=$false; basis='BuildManifest runtimeEntry path, Prefab, .meta GUID, owned-output SHA-256, and build SHA-256 verified; no W24 visual-QA evidence was scanned.' }; whenAnyRequiredEntryMissing=[ordered]@{ maturity='L0_InvalidOrMissing'; workingStatus='None'; basis='Missing or unverifiable BuildManifest runtime entry, path, GUID, or SHA-256; no visual conclusion is asserted.' } }
    entryCount=@($entries).Count; freezeHash=$freezeHash; effectIds=@($entries | Sort-Object EffectId | ForEach-Object EffectId)
}
[IO.File]::WriteAllText($output, (($document | ConvertTo-Json -Depth 8) + "`n"), [Text.UTF8Encoding]::new($false))
Write-Output "Regenerated $output with $($document.entryCount) entries and $freezeHash"
