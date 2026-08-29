<#
.SYNOPSIS
Lightweight per-task acceptance gate: locked restore + Release build + tests
(+ optional schema verification).

.DESCRIPTION
Day-to-day acceptance runner for atomic tasks dispatched under
docs/plans/OPTIMIZATION_MASTER_PLAN.md. It enforces the per-task acceptance
baseline from docs/plans/CODING_STANDARDS.md section 1:

  Step 1  restore  dotnet restore in locked mode (-p:RestoreLockedMode=true).
                   Locked mode fails (NU1004) when any packages.lock.json has
                   drifted from the project reference graph, so a PASS here
                   proves the lock files did not drift.
  Step 2  build    dotnet build VFXComposer.sln -c <Configuration> --no-restore.
                   TreatWarningsAsErrors is enabled repo-wide, so a PASS here
                   proves 0 warnings / 0 errors.
  Step 3  test     dotnet test. Whole solution by default; pass -TestProject
                   to run only the named test projects. Skipped by -SkipTests.
  Step 4  schemas  optional (-IncludeSchemas): runs eng/verify-phase2-schemas.py
                   when a python interpreter is on PATH; otherwise the step is
                   reported as SKIP with an explicit notice.

Every step prints an explicit [PASS]/[FAIL]/[SKIP] line, a summary table is
printed at the end, and the script exits non-zero when any step fails. Steps
after a failed step are not executed (fail fast).

WHEN TO USE THIS LIGHT GATE VS. THE HEAVY GATE (eng/run-phase2-gate.ps1)
  Use this script for routine per-task acceptance: bug fixes, feature
  increments, refactors - anything accepted under the master plan's per-task
  baseline (locked restore, Release 0 warning / 0 error, relevant tests green).
  You MUST use the heavy gate eng/run-phase2-gate.ps1 (left untouched by this
  script) instead of, or in addition to, this one whenever:
    - a milestone/phase is being closed out (milestone acceptance),
    - protocol surfaces or docs/schemas/desktop schemas changed,
    - preparing a release/merge that must produce audit receipts
      (frozen-root replay, assembly-binding checks, residue checks).

KNOWN TRANSITIONAL ISSUE (tracked by task O3)
  services/VFXComposer.Broker.HandleProbe and services/VFXComposer.Broker.Tests
  carry baseline packages.lock.json drift inherited from the phase2 baseline.
  Until O3 lands, step 1 can fail through no fault of the change under test.
  Pass -SkipLockedRestore to fall back to a plain (unlocked) restore in that
  case; the switch is transitional and must not be used once O3 is merged,
  because it disables lock-file drift verification.

.PARAMETER Configuration
Build configuration for build and test steps. Default: Release.

.PARAMETER TestProject
One or more test projects to run instead of the whole solution. Each value is
either a path to a .csproj or a project name (e.g. VFXComposer.Protocol.Tests)
that resolves to exactly one <name>.csproj in the repository.

.PARAMETER IncludeSchemas
Also run eng/verify-phase2-schemas.py. Requires a python interpreter (with the
jsonschema and referencing packages) on PATH; when python is missing the step
is skipped with an explicit notice instead of failing.

.PARAMETER SkipTests
Skip step 3 entirely (restore + build only).

.PARAMETER SkipLockedRestore
Transitional: restore WITHOUT -p:RestoreLockedMode=true, so lock-file drift is
NOT verified. Only intended to bypass the known O3 baseline drift above.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File eng\run-task-acceptance.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File eng\run-task-acceptance.ps1 -TestProject VFXComposer.Protocol.Tests -IncludeSchemas

.EXAMPLE
powershell -ExecutionPolicy Bypass -File eng\run-task-acceptance.ps1 -SkipTests -SkipLockedRestore
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Configuration = "Release",

    [Parameter()]
    [string[]]$TestProject = @(),

    [Parameter()]
    [switch]$IncludeSchemas,

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [switch]$SkipLockedRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$script:SolutionName = "VFXComposer.sln"
$script:StepRecords = @()

function Add-StepRecord {
    param(
        [Parameter(Mandatory = $true)] [string]$Name,
        [Parameter(Mandatory = $true)] [ValidateSet("PASS", "FAIL", "SKIP")] [string]$Status,
        [Parameter()] [string]$Detail = ""
    )

    $script:StepRecords += [pscustomobject]@{
        Name   = $Name
        Status = $Status
        Detail = $Detail
    }

    $line = "[{0}] {1}" -f $Status, $Name
    if ($Detail -ne "") {
        $line = $line + " -- " + $Detail
    }
    $color = "Green"
    if ($Status -eq "FAIL") { $color = "Red" }
    if ($Status -eq "SKIP") { $color = "Yellow" }
    Write-Host $line -ForegroundColor $color
}

function Invoke-AcceptanceStep {
    param(
        [Parameter(Mandatory = $true)] [string]$Name,
        [Parameter(Mandatory = $true)] [string]$FileName,
        [Parameter(Mandatory = $true)] [string[]]$ArgumentList
    )

    Write-Host ""
    Write-Host ("=== STEP {0} ===" -f $Name) -ForegroundColor Cyan
    Write-Host ("    command: {0} {1}" -f $FileName, ($ArgumentList -join " "))

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $previousPreference = $ErrorActionPreference
    # Windows PowerShell 5.1 can surface native stderr as errors under
    # ErrorActionPreference=Stop when streams are redirected; relax it around
    # the native call and judge strictly by exit code.
    $ErrorActionPreference = "Continue"
    Push-Location $script:RepositoryRoot
    try {
        # Out-Host keeps native stdout visible without leaking it into this
        # function's return value (which must stay a single boolean).
        & $FileName @ArgumentList | Out-Host
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
        $ErrorActionPreference = $previousPreference
        $stopwatch.Stop()
    }

    $elapsed = "{0:0.0}s" -f $stopwatch.Elapsed.TotalSeconds
    if ($exitCode -eq 0) {
        Add-StepRecord -Name $Name -Status "PASS" -Detail $elapsed
        return $true
    }
    Add-StepRecord -Name $Name -Status "FAIL" -Detail ("exit code {0}, {1}" -f $exitCode, $elapsed)
    return $false
}

function Resolve-TestProjectPath {
    param([Parameter(Mandatory = $true)] [string]$NameOrPath)

    $candidate = $NameOrPath
    if (-not [System.IO.Path]::IsPathRooted($candidate)) {
        $candidate = Join-Path $script:RepositoryRoot $NameOrPath
    }
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }

    $fileName = $NameOrPath
    if (-not $fileName.EndsWith(".csproj", [System.StringComparison]::OrdinalIgnoreCase)) {
        $fileName = $fileName + ".csproj"
    }
    $found = @(Get-ChildItem -Path $script:RepositoryRoot -Recurse -Filter $fileName -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.codex_tmp)\\' })
    if ($found.Count -eq 1) {
        return $found[0].FullName
    }
    if ($found.Count -eq 0) {
        throw ("Test project '{0}' was not found (searched for {1} under {2})." -f $NameOrPath, $fileName, $script:RepositoryRoot)
    }
    throw ("Test project '{0}' is ambiguous; {1} matches found. Pass an explicit .csproj path." -f $NameOrPath, $found.Count)
}

Write-Host ("Task acceptance gate (light) -- repository: {0}" -f $script:RepositoryRoot)
Write-Host ("Configuration: {0}" -f $Configuration)
$failed = $false

# --- Step 1: restore -------------------------------------------------------
if ($SkipLockedRestore) {
    Write-Warning "-SkipLockedRestore: lock-file drift is NOT verified in this run (transitional switch for the O3 baseline drift)."
    Add-StepRecord -Name "restore: locked-mode drift verification" -Status "SKIP" -Detail "-SkipLockedRestore was passed"
    if (-not (Invoke-AcceptanceStep -Name "restore (unlocked)" -FileName "dotnet" -ArgumentList @("restore", $script:SolutionName))) {
        $failed = $true
    }
}
else {
    if (-not (Invoke-AcceptanceStep -Name "restore (locked mode)" -FileName "dotnet" -ArgumentList @("restore", $script:SolutionName, "-p:RestoreLockedMode=true"))) {
        $failed = $true
        Write-Host "Hint: an NU1004 failure limited to services/VFXComposer.Broker.HandleProbe or services/VFXComposer.Broker.Tests is the known O3 baseline drift; re-run with -SkipLockedRestore until O3 is merged." -ForegroundColor Yellow
    }
}

# --- Step 2: build ---------------------------------------------------------
if ($failed) {
    Add-StepRecord -Name ("build {0} ({1})" -f $script:SolutionName, $Configuration) -Status "SKIP" -Detail "prior step failed"
}
else {
    if (-not (Invoke-AcceptanceStep -Name ("build {0} ({1}, warnings are errors)" -f $script:SolutionName, $Configuration) -FileName "dotnet" -ArgumentList @("build", $script:SolutionName, "--configuration", $Configuration, "--no-restore"))) {
        $failed = $true
    }
}

# --- Step 3: test ----------------------------------------------------------
if ($SkipTests) {
    Add-StepRecord -Name "test" -Status "SKIP" -Detail "-SkipTests was passed"
}
elseif ($failed) {
    Add-StepRecord -Name "test" -Status "SKIP" -Detail "prior step failed"
}
elseif ($TestProject.Count -eq 0) {
    if (-not (Invoke-AcceptanceStep -Name ("test {0} (full)" -f $script:SolutionName) -FileName "dotnet" -ArgumentList @("test", $script:SolutionName, "--configuration", $Configuration, "--no-build"))) {
        $failed = $true
    }
}
else {
    foreach ($requested in $TestProject) {
        if ($failed) {
            Add-StepRecord -Name ("test {0}" -f $requested) -Status "SKIP" -Detail "prior step failed"
            continue
        }
        $projectPath = $null
        try {
            $projectPath = Resolve-TestProjectPath -NameOrPath $requested
        }
        catch {
            Add-StepRecord -Name ("test {0}" -f $requested) -Status "FAIL" -Detail $_.Exception.Message
            $failed = $true
            continue
        }
        if (-not (Invoke-AcceptanceStep -Name ("test {0}" -f $requested) -FileName "dotnet" -ArgumentList @("test", $projectPath, "--configuration", $Configuration, "--no-build"))) {
            $failed = $true
        }
    }
}

# --- Step 4: schemas (optional) --------------------------------------------
if (-not $IncludeSchemas) {
    Add-StepRecord -Name "schemas (eng/verify-phase2-schemas.py)" -Status "SKIP" -Detail "not requested; pass -IncludeSchemas to run"
}
elseif ($failed) {
    Add-StepRecord -Name "schemas (eng/verify-phase2-schemas.py)" -Status "SKIP" -Detail "prior step failed"
}
else {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        Add-StepRecord -Name "schemas (eng/verify-phase2-schemas.py)" -Status "SKIP" -Detail "python was not found on PATH; install python with the jsonschema and referencing packages, or run the schema check on a machine that has them"
    }
    else {
        if (-not (Invoke-AcceptanceStep -Name "schemas (eng/verify-phase2-schemas.py)" -FileName "python" -ArgumentList @("eng/verify-phase2-schemas.py"))) {
            $failed = $true
        }
    }
}

# --- Summary ----------------------------------------------------------------
# Derive the overall verdict from the recorded steps rather than trusting the
# fail-fast flag alone, so a bookkeeping bug can never turn FAIL into PASS.
if (@($script:StepRecords | Where-Object { $_.Status -eq "FAIL" }).Count -ne 0) {
    $failed = $true
}
Write-Host ""
Write-Host "=== TASK ACCEPTANCE SUMMARY ===" -ForegroundColor Cyan
foreach ($record in $script:StepRecords) {
    $line = "  [{0}] {1}" -f $record.Status, $record.Name
    if ($record.Detail -ne "") {
        $line = $line + " -- " + $record.Detail
    }
    $color = "Green"
    if ($record.Status -eq "FAIL") { $color = "Red" }
    if ($record.Status -eq "SKIP") { $color = "Yellow" }
    Write-Host $line -ForegroundColor $color
}
Write-Host ""
if ($failed) {
    Write-Host "TASK ACCEPTANCE: FAIL" -ForegroundColor Red
    exit 1
}
Write-Host "TASK ACCEPTANCE: PASS" -ForegroundColor Green
exit 0
