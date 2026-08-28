param(
    [int] $TimeoutSeconds = 4200
)

$ErrorActionPreference = 'Stop'

$unity = 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe'
$shadowRoot = 'D:\WorkWork\Assist\image_to_smart\.codex_tmp\w24-s0a-full110-20260825-1448'
$projectPath = Join-Path $shadowRoot 'project'
$captureRoot = Join-Path $projectPath 'Library\VFXComposer\W24S0aCalibration'
$tempRoot = Join-Path $shadowRoot '.codex_tmp\unity-temp-full110'
$upmCacheRoot = Join-Path $shadowRoot '.codex_tmp\upm-cache-full110'
$resultRoot = Join-Path $shadowRoot 'test-results'
$logRoot = Join-Path $shadowRoot 'Logs'
$xml = Join-Path $resultRoot 's0a-full110.xml'
$log = Join-Path $logRoot 's0a-full110.log'
$filter = 'VFXComposer.Tests.PlayMode.W24S0aFormalRuntime.W24S0aFormalPlayModeProxyTests.Capture_Full110_OperatorOnlyMutants_WhenTheFutureCohortExists'

$required = @(
    'Assets/VFX/Preview/VFXPREVIEW_SustainedFlame.unity',
    'Assets/VFX/Effects/Aura/sustained_flame_3d/VFX_sustained_flame_3d.prefab',
    'Assets/VFX/Effects/Aura/sustained_flame_3d/VFX_sustained_flame_3d.prefab.meta',
    'Assets/VFX/Recipes/Aura/sustained_flame_3d.default.json',
    'ProjectSettings/VFXComposer/BuildManifests/sustained_flame_3d.manifest.json',
    'Assets/Settings/VFXPreviewUniversalRenderer.asset',
    'ProjectSettings/GraphicsSettings.asset',
    'Packages/com.vfxcomposer.unity/Runtime/Components/SustainedEffectController.cs',
    'Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24CaptureProfile.cs',
    'Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs',
    'Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24EvidenceStore.cs',
    'Packages/com.vfxcomposer.unity/Editor/W24/S0a/W24S0aFixtureAdapter.cs',
    'Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormal/W24S0aFormalCalibrationCaptureTests.cs',
    'Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormal/VFXComposer.Tests.PlayMode.W24S0aFormal.asmdef',
    'Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormalRuntime/W24S0aFormalPlayModeProxyTests.cs',
    'Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormalRuntime/VFXComposer.Tests.PlayMode.W24S0aFormalRuntime.asmdef'
)
$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $projectPath $_) -PathType Leaf) })
$contract = Join-Path $shadowRoot 'docs\vfx-contracts\sustained_flame_3d.contract.json'
$commandSet = Join-Path $shadowRoot 'docs\vfx-calibration\full\operator\command-set.json'
if (-not (Test-Path -LiteralPath $contract -PathType Leaf)) { $missing += $contract }
if (-not (Test-Path -LiteralPath $commandSet -PathType Leaf)) { $missing += $commandSet }
if ($missing.Count -gt 0) { throw 'Missing full110 preconditions: ' + ($missing -join '; ') }

if (Test-Path -LiteralPath $captureRoot) {
    throw "Full110 capture root must be absent before first launch: $captureRoot"
}
if ((Test-Path -LiteralPath $xml) -or (Test-Path -LiteralPath $log)) {
    throw 'Refusing to overwrite an existing full110 XML or log.'
}

$active = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.IndexOf($projectPath, [StringComparison]::OrdinalIgnoreCase) -ge 0
})
if ($active.Count -ne 0) { throw "The full110 shadow already has $($active.Count) Unity process(es)." }

$license = 'C:\ProgramData\Unity\Unity_lic.ulf'
if (-not (Test-Path -LiteralPath $license -PathType Leaf) -or (Get-Item -LiteralPath $license).Length -lt 1000) {
    throw 'Unity license file is missing or unexpectedly short.'
}

New-Item -ItemType Directory -Force -Path $tempRoot, $upmCacheRoot, $resultRoot, $logRoot | Out-Null
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
$env:UPM_CACHE_ROOT = $upmCacheRoot

$arguments = @(
    '-batchmode',
    '-projectPath', $projectPath,
    '-runTests',
    '-testPlatform', 'PlayMode',
    '-assemblyNames', 'VFXComposer.Tests.PlayMode.W24S0aFormalRuntime',
    '-testFilter', $filter,
    '-testResults', $xml,
    '-logFile', $log
)

$process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
$startedAtValue = $process.StartTime.ToUniversalTime()
$deadline = $startedAtValue.AddSeconds($TimeoutSeconds)
$lastProgress = [datetime]::MinValue

while (-not $process.WaitForExit(15000)) {
    $now = [datetime]::UtcNow
    if ($now -ge $deadline) {
        $live = Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)"
        $liveStartedAt = if ($null -ne $live) { ([datetime] $live.CreationDate).ToUniversalTime() } else { $null }
        $sameStart = $null -ne $liveStartedAt -and [math]::Abs(($liveStartedAt - $startedAtValue).TotalSeconds) -le 2
        if ($null -eq $live -or $live.Name -ne 'Unity.exe' -or $live.CommandLine.IndexOf($projectPath, [StringComparison]::OrdinalIgnoreCase) -lt 0 -or -not $sameStart) {
            throw "Timed out, but PID $($process.Id) is not the exact full110 Unity process; refusing to terminate it."
        }
        Stop-Process -Id $process.Id -Force
        throw "Full110 capture timed out after $TimeoutSeconds seconds; the exact shadow PID was stopped and the partial shadow is preserved unchanged."
    }

    if (($now - $lastProgress).TotalSeconds -ge 60) {
        $candidateDirectories = if (Test-Path -LiteralPath $captureRoot) { @(Get-ChildItem -LiteralPath $captureRoot -Directory -Force).Count } else { 0 }
        $completed = if (Test-Path -LiteralPath $captureRoot) {
            @(Get-ChildItem -LiteralPath $captureRoot -Directory -Force | Where-Object {
                Test-Path -LiteralPath (Join-Path $_.FullName 'candidate-completion.json') -PathType Leaf
            }).Count
        } else { 0 }
        $logBytes = if (Test-Path -LiteralPath $log -PathType Leaf) { (Get-Item -LiteralPath $log).Length } else { 0 }
        [pscustomobject]@{
            Status = 'RUNNING'
            Pid = $process.Id
            ElapsedMinutes = [math]::Round(($now - $startedAtValue).TotalMinutes, 2)
            CandidateDirectories = $candidateDirectories
            CompletedCandidates = $completed
            LogBytes = $logBytes
            DFreeGiB = [math]::Round((Get-PSDrive -Name D).Free / 1GB, 2)
        } | ConvertTo-Json -Compress
        $lastProgress = $now
    }
}

$process.Refresh()
if (-not (Test-Path -LiteralPath $xml -PathType Leaf)) {
    throw "Unity exited without a full110 XML result: exit=$($process.ExitCode) log=$log"
}

[xml] $document = Get-Content -LiteralPath $xml -Raw
$run = $document.'test-run'
$candidateCount = if (Test-Path -LiteralPath $captureRoot) { @(Get-ChildItem -LiteralPath $captureRoot -Directory -Force).Count } else { 0 }
$completedCount = if (Test-Path -LiteralPath $captureRoot) {
    @(Get-ChildItem -LiteralPath $captureRoot -Directory -Force | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'candidate-completion.json') -PathType Leaf
    }).Count
} else { 0 }
$result = [pscustomobject]@{
    Status = 'COMPLETED'
    Pid = $process.Id
    StartedAtUtc = $startedAtValue.ToString('o')
    ExitCode = $process.ExitCode
    Total = [int] $run.total
    Passed = [int] $run.passed
    Failed = [int] $run.failed
    Skipped = [int] $run.skipped
    Inconclusive = [int] $run.inconclusive
    Result = [string] $run.result
    CandidateDirectories = $candidateCount
    CompletedCandidates = $completedCount
    Xml = $xml
    Log = $log
}
$result | ConvertTo-Json -Depth 3

if ($process.ExitCode -ne 0 -or $result.Total -ne 1 -or $result.Passed -ne 1 -or $result.Failed -ne 0 -or $result.Inconclusive -ne 0 -or $candidateCount -ne 110 -or $completedCount -ne 110) {
    throw "Full110 gate failed: exit=$($process.ExitCode) total=$($result.Total) passed=$($result.Passed) failed=$($result.Failed) candidates=$candidateCount completed=$completedCount"
}
