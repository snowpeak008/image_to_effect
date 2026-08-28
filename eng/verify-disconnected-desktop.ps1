[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateRange(5, 60)]
    [int]$StartupTimeoutSeconds = 20
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$executablePath = Join-Path $repoRoot "apps\VFXComposer.Desktop\bin\$Configuration\net8.0\VFXComposer.Desktop.exe"
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw 'W24P1-SMOKE-001: Desktop executable is absent; build the frozen solution first.'
}
$executablePath = (Resolve-Path -LiteralPath $executablePath).Path

$watchedRelativeRoots = @(
    'project\Assets',
    'project\Packages',
    'project\ProjectSettings',
    'docs',
    'artifacts'
)
$watchedRoots = @($watchedRelativeRoots | ForEach-Object {
    $candidate = Join-Path $repoRoot $_
    if (-not (Test-Path -LiteralPath $candidate -PathType Container)) {
        throw 'W24P1-SMOKE-002: a watched project root is absent.'
    }
    (Resolve-Path -LiteralPath $candidate).Path
})

$existingDesktop = @(Get-CimInstance Win32_Process | Where-Object {
    $_.ExecutablePath -and [IO.Path]::GetFullPath($_.ExecutablePath) -ceq $executablePath
})
if ($existingDesktop.Count -ne 0) {
    throw 'W24P1-SMOKE-003: a Desktop instance is already running.'
}

function Get-ForbiddenProcessCounts {
    $processes = @(Get-CimInstance Win32_Process -ErrorAction Stop)
    [pscustomobject]@{
        Unity = @($processes | Where-Object { $_.Name -ceq 'Unity.exe' }).Count
        Broker = @($processes | Where-Object { $_.Name -ceq 'VFXComposer.Broker.exe' }).Count
    }
}

function Assert-ForbiddenProcessesAbsent {
    $counts = Get-ForbiddenProcessCounts
    if ($counts.Unity -ne 0) {
        throw 'W24P1-SMOKE-004: Unity Editor must remain absent for the disconnected gate.'
    }
    if ($counts.Broker -ne 0) {
        throw 'W24P1-SMOKE-005: Broker must remain absent for the disconnected gate.'
    }
    $counts
}

$initialForbiddenProcessCounts = Assert-ForbiddenProcessesAbsent

$events = [System.Collections.Concurrent.ConcurrentQueue[string]]::new()
$watchers = [System.Collections.Generic.List[IO.FileSystemWatcher]]::new()
$subscriptionIdentifiers = [System.Collections.Generic.List[string]]::new()
$process = $null
$forcedTermination = $false
$runId = [Guid]::NewGuid().ToString('N')

try {
    foreach ($root in $watchedRoots) {
        $watcher = [IO.FileSystemWatcher]::new($root)
        $watcher.IncludeSubdirectories = $true
        $watcher.NotifyFilter = [IO.NotifyFilters]'FileName, DirectoryName, LastWrite, Size, CreationTime'
        foreach ($eventName in @('Changed', 'Created', 'Deleted', 'Renamed')) {
            $sourceIdentifier = "W24P1Smoke-$runId-$($subscriptionIdentifiers.Count)"
            Register-ObjectEvent -InputObject $watcher -EventName $eventName -SourceIdentifier $sourceIdentifier -MessageData $events -Action {
                [System.Collections.Concurrent.ConcurrentQueue[string]]$queue = $event.MessageData
                $queue.Enqueue($event.SourceEventArgs.ChangeType.ToString())
            } | Out-Null
            $subscriptionIdentifiers.Add($sourceIdentifier)
        }
        $errorSourceIdentifier = "W24P1Smoke-$runId-$($subscriptionIdentifiers.Count)"
        Register-ObjectEvent -InputObject $watcher -EventName Error -SourceIdentifier $errorSourceIdentifier -MessageData $events -Action {
            [System.Collections.Concurrent.ConcurrentQueue[string]]$queue = $event.MessageData
            $queue.Enqueue('WatcherError')
        } | Out-Null
        $subscriptionIdentifiers.Add($errorSourceIdentifier)
        $watcher.EnableRaisingEvents = $true
        $watchers.Add($watcher)
    }

    $process = Start-Process -FilePath $executablePath -PassThru -WindowStyle Hidden
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
    } while (-not $process.HasExited -and
             $process.MainWindowHandle -eq [IntPtr]::Zero -and
             [DateTime]::UtcNow -lt $deadline)

    if ($process.HasExited) {
        throw 'W24P1-SMOKE-006: Desktop exited before its main window loaded.'
    }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
        throw 'W24P1-SMOKE-007: Desktop main window did not load before the timeout.'
    }
    $runningForbiddenProcessCounts = Assert-ForbiddenProcessesAbsent
    $windowTitle = $process.MainWindowTitle
    if ($windowTitle -cne 'VFX Composer') {
        throw 'W24P1-SMOKE-008: Desktop main window title is not exact.'
    }

    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $rootElement = [System.Windows.Automation.AutomationElement]::RootElement
    $processCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
        $process.Id)
    $elements = $rootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        $processCondition)
    $automationNames = @($elements | ForEach-Object { $_.Current.Name } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if (-not $automationNames.Contains('Disconnected') -or
        -not $automationNames.Contains('No registered project')) {
        throw 'W24P1-SMOKE-009: accessibility state does not expose the exact disconnected presentation.'
    }

    $tcpListenerCount = @(Get-NetTCPConnection -State Listen -ErrorAction Stop |
        Where-Object { $_.OwningProcess -eq $process.Id }).Count
    $udpListenerCount = @(Get-NetUDPEndpoint -ErrorAction Stop |
        Where-Object { $_.OwningProcess -eq $process.Id }).Count
    if ($tcpListenerCount -ne 0 -or $udpListenerCount -ne 0) {
        throw 'W24P1-SMOKE-010: Desktop opened a network listener.'
    }

    Start-Sleep -Milliseconds 500
    $settledForbiddenProcessCounts = Assert-ForbiddenProcessesAbsent
    if ($events.Count -ne 0) {
        throw 'W24P1-SMOKE-011: Desktop touched a watched project or evidence root.'
    }

    if (-not $process.CloseMainWindow()) {
        throw 'W24P1-SMOKE-012: Desktop main window did not accept normal close.'
    }
    if (-not $process.WaitForExit(10000)) {
        $forcedTermination = $true
        Stop-Process -Id $process.Id -Force
        throw 'W24P1-SMOKE-013: Desktop required forced termination.'
    }
    if ($process.ExitCode -ne 0) {
        throw 'W24P1-SMOKE-014: Desktop returned a non-zero exit code.'
    }
    if ($events.Count -ne 0) {
        throw 'W24P1-SMOKE-015: watched roots changed during Desktop shutdown.'
    }
    $finalForbiddenProcessCounts = Assert-ForbiddenProcessesAbsent

    [ordered]@{
        schema = 'w24-phase1-disconnected-desktop-smoke/1'
        status = 'PASS'
        pid = $process.Id
        executableSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $executablePath).Hash.ToLowerInvariant()
        windowTitle = $windowTitle
        connectionDisplay = 'Disconnected'
        projectDisplay = 'No registered project'
        normalClose = $true
        forcedTermination = $forcedTermination
        exitCode = $process.ExitCode
        tcpListenerCount = 0
        udpListenerCount = 0
        watchedRootCount = $watchedRoots.Count
        watchedFilesystemEventCount = 0
        watcherErrorCount = 0
        forbiddenProcessCheckpointCount = 4
        unityEditorProcessCount = $finalForbiddenProcessCounts.Unity
        brokerProcessCount = $finalForbiddenProcessCounts.Broker
        verifierSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $PSCommandPath).Hash.ToLowerInvariant()
    } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $forcedTermination = $true
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(5000) | Out-Null
    }
    foreach ($sourceIdentifier in $subscriptionIdentifiers) {
        Unregister-Event -SourceIdentifier $sourceIdentifier -ErrorAction SilentlyContinue
        Get-Job -Name $sourceIdentifier -ErrorAction SilentlyContinue | Remove-Job -Force -ErrorAction SilentlyContinue
    }
    foreach ($watcher in $watchers) {
        $watcher.EnableRaisingEvents = $false
        $watcher.Dispose()
    }
}
