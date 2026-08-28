param(
    [Parameter(Mandatory = $true)] [ValidateSet('Compile', 'EditMode', 'PlayMode', 'ValidateResults')] [string] $Mode,
    [int] $TimeoutSeconds = 900,
    [string] $ResultsPath,
    [switch] $UseGraphics,
    [string] $TestFilter
)

$ErrorActionPreference = 'Stop'

function Get-ResultAttributeValue {
    param(
        [Parameter(Mandatory = $true)] [System.Xml.XmlElement] $Element,
        [Parameter(Mandatory = $true)] [string[]] $Names
    )

    foreach ($name in $Names) {
        $attribute = $Element.GetAttribute($name)
        if (-not [string]::IsNullOrWhiteSpace($attribute)) {
            return $attribute
        }
    }

    return $null
}

function Test-NUnitResults {
    param([Parameter(Mandatory = $true)] [string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        [Console]::Error.WriteLine("Test result gate failed: XML was not produced: $Path")
        return 2
    }

    if ((Get-Item -LiteralPath $Path).Length -eq 0) {
        [Console]::Error.WriteLine("Test result gate failed: XML is empty: $Path")
        return 2
    }

    [System.Xml.XmlDocument] $document = $null
    $parseFailure = $null
    for ($attempt = 0; $attempt -lt 5 -and $null -eq $document; $attempt++) {
        try {
            # Unity closes the process before returning, but on Windows the test runner's final
            # XML write can still be observed for a very short interval.  Load from a shared
            # stream and retry a bounded number of times instead of misreporting a transient
            # partial write as permanent malformed evidence.
            $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
            try {
                $candidate = New-Object System.Xml.XmlDocument
                $candidate.Load($stream)
                $document = $candidate
            } finally {
                $stream.Dispose()
            }
        } catch {
            $parseFailure = $_.Exception
            if ($attempt -lt 4) { Start-Sleep -Milliseconds 100 }
        }
    }
    if ($null -eq $document) {
        [Console]::Error.WriteLine("Test result gate failed: XML cannot be parsed: $Path. $($parseFailure.Message)")
        return 3
    }

    $run = $document.SelectSingleNode('/test-run')
    if ($null -eq $run) {
        $run = $document.SelectSingleNode('/test-results')
    }
    if ($null -eq $run -or $run -isnot [System.Xml.XmlElement]) {
        [Console]::Error.WriteLine("Test result gate failed: expected NUnit test-run or test-results root: $Path")
        return 3
    }

    # NUnit 3 emits total/failed; NUnit 2-compatible output uses total/failures.
    $totalText = Get-ResultAttributeValue -Element $run -Names @('total', 'testcasecount')
    $failedText = Get-ResultAttributeValue -Element $run -Names @('failed', 'failures')
    [int] $total = 0
    [int] $failed = 0
    if (-not [int]::TryParse($totalText, [ref] $total) -or -not [int]::TryParse($failedText, [ref] $failed)) {
        [Console]::Error.WriteLine("Test result gate failed: NUnit totals are missing or invalid. total='$totalText', failed='$failedText', file=$Path")
        return 3
    }

    $result = Get-ResultAttributeValue -Element $run -Names @('result')
    Write-Host "NUnit result summary: result=$result total=$total failed=$failed file=$Path"
    if ($total -le 0 -or $failed -ne 0) {
        [Console]::Error.WriteLine("Test result gate failed: expected total>0 and failed=0; got total=$total failed=$failed result=$result. File: $Path")
        return 3
    }

    return 0
}

if ($Mode -eq 'ValidateResults') {
    if ([string]::IsNullOrWhiteSpace($ResultsPath)) {
        [Console]::Error.WriteLine('ValidateResults requires -ResultsPath <NUnit XML path>.')
        exit 64
    }

    exit (Test-NUnitResults -Path $ResultsPath)
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'project'
$unityPath = 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe'
$evidencePath = Join-Path $repoRoot 'test-results'
New-Item -ItemType Directory -Force -Path $evidencePath | Out-Null

if (-not (Test-Path -LiteralPath $unityPath)) {
    [Console]::Error.WriteLine("Unity 2022.3.62f3c1 was not found: $unityPath")
    exit 127
}

$lockCandidates = @(
    (Join-Path $projectPath 'Temp\UnityLockfile'),
    (Join-Path $projectPath 'Library\EditorInstance.json'),
    (Join-Path $projectPath 'Library\SourceAssetDB-lock')
) | Where-Object { Test-Path -LiteralPath $_ }
$projectUnityProcesses = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($projectPath, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 })
if ($projectUnityProcesses.Count -gt 0) {
    $processDetails = $projectUnityProcesses | ForEach-Object { "PID $($_.ProcessId): $($_.CommandLine)" }
    [Console]::Error.WriteLine("Unity project lock detected. Close the graphical Editor and retry. Active project process(es): " + ($processDetails -join '; ') + ". Lock diagnostics: " + ($lockCandidates -join '; '))
    exit 73
}
if ($lockCandidates.Count -gt 0) {
    Write-Warning ("No active Unity process matched this project. Ignoring stale lock diagnostic(s): " + ($lockCandidates -join '; '))
}

$logPath = Join-Path $evidencePath ("unity-{0}.log" -f $Mode.ToLowerInvariant())
$arguments = @('-batchmode', '-projectPath', $projectPath, '-logFile', $logPath)
if (-not $UseGraphics) {
    $arguments += '-nographics'
}
if ($Mode -eq 'Compile') {
    $arguments += '-quit'
} else {
    $resultsPath = if ([string]::IsNullOrWhiteSpace($ResultsPath)) { Join-Path $evidencePath ("{0}.xml" -f $Mode) } else { $ResultsPath }
    # The Unity Test Framework owns shutdown after -runTests. Supplying -quit
    # here causes Unity 2022.3 to exit during startup before it writes XML.
    $arguments += @('-runTests', '-testPlatform', $Mode, '-testResults', $resultsPath)
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        $arguments += @('-testFilter', $TestFilter)
    }
    if (Test-Path -LiteralPath $resultsPath) {
        Remove-Item -LiteralPath $resultsPath -Force
    }
}

Write-Host "Starting Unity $Mode check. Graphics=$UseGraphics Filter='$TestFilter' Log: $logPath"
$process = Start-Process -FilePath $unityPath -ArgumentList $arguments -PassThru -WindowStyle Hidden
$processStartTime = $process.StartTime
try {
    Wait-Process -Id $process.Id -Timeout $TimeoutSeconds -ErrorAction Stop
} catch {
    $waitException = $_
    $exactProcessStillRunning = $false
    try {
        $sameProcess = Get-Process -Id $process.Id -ErrorAction Stop
        $exactProcessStillRunning = -not $sameProcess.HasExited -and $sameProcess.StartTime -eq $processStartTime
    } catch {
        $exactProcessStillRunning = $false
    }

    if ($exactProcessStillRunning) {
        [Console]::Error.WriteLine("Unity $Mode wait failed for live exact PID $($process.Id) ($($waitException.Exception.Message)); terminating that PID only. See $logPath")
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        exit 124
    }

    [Console]::Error.WriteLine("Unity $Mode wait failed, but PID $($process.Id) is no longer the started process. No process was terminated. $($waitException.Exception.Message)")
    throw $waitException
}

$process.Refresh()
$exitCode = $process.ExitCode
Write-Host "Unity $Mode check finished with exit code $exitCode (PID $($process.Id))."
if ($Mode -ne 'Compile') {
    $resultGateExitCode = Test-NUnitResults -Path $resultsPath
    if ($resultGateExitCode -ne 0) {
        [Console]::Error.WriteLine("Unity $Mode check failed its test-result gate. Unity exit code=$exitCode. See $logPath")
        exit $resultGateExitCode
    }
}
exit $exitCode
