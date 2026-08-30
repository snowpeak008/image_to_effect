<#
.SYNOPSIS
F6 end-to-end acceptance: the automated machine gate plus the manual, human-judged
steps the machine cannot stand in for (real Unity batchmode, and a real provider).

.DESCRIPTION
F6 accepts the two product flows end to end (docs/plans/OPTIMIZATION_MASTER_PLAN.md,
task card F6):

  Flow one  Desktop chat -> recipe draft -> confirm -> restricted build -> the
            three-file write surface (prefab + build manifest + recipe provenance).
            The build step is a real short-lived Unity batchmode process, so it is
            NOT run here: it needs the pinned editor (2022.3.62f3c1) and is judged
            by a human against the produced assets. This script prints the steps.

  Flow two  CLI manifest batch -> serial queue -> report / exit code, and the MCP
            submit / status / cancel roundtrip. The orchestration, report shape and
            exit codes are fully covered by the automated suites this script runs
            (VFXComposer.Cli.Tests, VFXComposer.Mcp.Tests), driving the real vfxc /
            vfxc-mcp entry points over a temp store and a mock generation channel —
            no provider, no network, no Unity project touched. The checked-in
            batches/sample-batch.manifest.json is exercised end to end by
            CliRunnerTests.TheCheckedInSampleManifestRunsGreenThroughTheRealRunner.

WHAT THIS SCRIPT DOES
  1. Release build (0 warning / 0 error under repo-wide TreatWarningsAsErrors).
  2. Runs the F6 flow-two acceptance suites (or -Full for the whole solution).
  3. Prints the manual flow-one and provider-backed flow-two steps for a human.

The manual steps are documentation, not assertions: a machine PASS here is
necessary but not sufficient for F6 closure. The visual / asset judgement of
flow one belongs to the user, exactly as the master plan reserves it.

.PARAMETER Configuration
Build/test configuration. Defaults to Release (the acceptance baseline; Debug
makes Broker/LocalE2E fail closed on U4FS001 by design).

.PARAMETER Full
Run the whole solution's tests instead of only the F6 flow-two suites.

.PARAMETER SkipTests
Build only; skip the automated suites.

.EXAMPLE
pwsh eng/run-f6-e2e-acceptance.ps1

.EXAMPLE
pwsh eng/run-f6-e2e-acceptance.ps1 -Full
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$Full,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'VFXComposer.sln'
$sampleManifest = Join-Path $repoRoot 'batches/sample-batch.manifest.json'

$results = [System.Collections.Generic.List[object]]::new()
function Add-Result([string]$step, [string]$status, [string]$detail) {
    $results.Add([pscustomobject]@{ Step = $step; Status = $status; Detail = $detail })
    Write-Host ("[{0}] {1} {2}" -f $status, $step, $detail)
}

# --- Step 1: Release build -------------------------------------------------
Write-Host "== F6 E2E acceptance ($Configuration) ==" -ForegroundColor Cyan
& dotnet build $solution -c $Configuration | Out-Host
if ($LASTEXITCODE -ne 0) {
    Add-Result 'build' 'FAIL' "dotnet build exit $LASTEXITCODE"
    $results | Format-Table -AutoSize | Out-Host
    exit 1
}
Add-Result 'build' 'PASS' '0 warning / 0 error'

# --- Step 2: automated flow-two acceptance suites --------------------------
if ($SkipTests) {
    Add-Result 'tests' 'SKIP' '-SkipTests'
}
else {
    if ($Full) {
        & dotnet test $solution -c $Configuration --no-build | Out-Host
        $testDetail = 'whole solution'
    }
    else {
        $flowTwoSuites = @(
            (Join-Path $repoRoot 'apps/VFXComposer.Cli.Tests/VFXComposer.Cli.Tests.csproj'),
            (Join-Path $repoRoot 'apps/VFXComposer.Mcp.Tests/VFXComposer.Mcp.Tests.csproj')
        )
        foreach ($suite in $flowTwoSuites) {
            & dotnet test $suite -c $Configuration --no-build | Out-Host
            if ($LASTEXITCODE -ne 0) { break }
        }
        $testDetail = 'Cli.Tests + Mcp.Tests (flow two)'
    }
    if ($LASTEXITCODE -ne 0) {
        Add-Result 'tests' 'FAIL' "dotnet test exit $LASTEXITCODE ($testDetail)"
        $results | Format-Table -AutoSize | Out-Host
        exit 1
    }
    Add-Result 'tests' 'PASS' $testDetail
}

# --- Step 3: manual steps (documentation, not assertions) ------------------
Add-Result 'manual-flow-one' 'MANUAL' 'real Unity batchmode; human-judged'
Add-Result 'manual-flow-two' 'MANUAL' 'provider-backed CLI/MCP roundtrip'

Write-Host ""
Write-Host "-- Manual flow one (real Unity batchmode; needs editor 2022.3.62f3c1) --" -ForegroundColor Yellow
Write-Host @"
  1. Open the Desktop app, chat a prompt, and confirm the generated recipe draft.
  2. Build the confirmed draft. The restricted build starts one short-lived Unity
     batchmode process (Invoke-Unity ... RecipeBuild).
  3. Verify the three-file write surface for the effect id, and nothing else:
       - Assets/VFX/Generated/<effectId>/VFX_<effectId>.prefab
       - ProjectSettings/VFXComposer/BuildManifests/<effectId>.manifest.json
       - Assets/VFX/Recipes/<effectId>.json (build provenance)
  4. Confirm the effect looks correct (user's visual sign-off; not a machine gate).
  5. dependencyHash / build drift under project/ is restored, never committed
     (git checkout -- project; git clean -fd project).
"@ | Out-Host

Write-Host "-- Manual flow two (provider-backed; the machine suites cover the mock path) --" -ForegroundColor Yellow
Write-Host @"
  With a real AI provider bound and the queue store available:
    vfxc batch run "$sampleManifest"
    vfxc batch status sample-fire-pack
    vfxc job status <jobId>
    vfxc batch cancel sample-fire-pack
  And over MCP (stdio):
    vfx_submit_batch { manifest: <sample> } -> vfx_get_batch_report -> vfx_cancel_batch
"@ | Out-Host

# --- Summary ---------------------------------------------------------------
Write-Host ""
$results | Format-Table -AutoSize | Out-Host
if ($results | Where-Object { $_.Status -eq 'FAIL' }) { exit 1 }
Write-Host "F6 machine acceptance PASS. Manual flow-one / flow-two remain for human sign-off." -ForegroundColor Green
exit 0
