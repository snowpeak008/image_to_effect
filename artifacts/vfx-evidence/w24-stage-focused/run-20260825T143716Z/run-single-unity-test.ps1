param(
    [Parameter(Mandatory = $true)]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [string] $Filter,

    [Parameter(Mandatory = $true)]
    [string] $RunId,

    [int] $TimeoutSeconds = 240,

    [switch] $UseCanonicalHubAuth
)

$ErrorActionPreference = 'Stop'

$unity = 'E:\workwork\steamgamework\unit\2022.3.62f3c1\Editor\Unity.exe'
$projectPath = 'D:\WorkWork\Assist\image_to_smart\.codex_tmp\w24-fresh-20260825-0628\project'
$workspaceTemp = 'D:\WorkWork\Assist\image_to_smart\.codex_tmp\unity-temp-w24-stages'
$outputRoot = Join-Path 'D:\WorkWork\Assist\image_to_smart\.codex_tmp\w24-stage-regression-results' $RunId

New-Item -ItemType Directory -Force -Path $workspaceTemp, $outputRoot | Out-Null
$env:TEMP = $workspaceTemp
$env:TMP = $workspaceTemp

$xml = Join-Path $outputRoot ($Name + '.xml')
$log = Join-Path $outputRoot ($Name + '.log')
if ((Test-Path -LiteralPath $xml) -or (Test-Path -LiteralPath $log)) {
    throw "Refusing to overwrite an existing result for $Name in $outputRoot."
}

$arguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $projectPath,
    '-runTests',
    '-testPlatform', 'EditMode',
    '-testFilter', $Filter,
    '-testResults', $xml,
    '-logFile', $log
)

if ($UseCanonicalHubAuth) {
    $canonicalProject = 'D:\WorkWork\Assist\image_to_smart\project'
    $canonical = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -eq 'Unity.exe' -and
        $_.CommandLine -like "*$canonicalProject*" -and
        $_.CommandLine -notlike '*AssetImportWorker*' -and
        $_.CommandLine -match '(?i)-accessToken\s+(\S+)' -and
        $_.CommandLine -match '(?i)-hubSessionId\s+(\S+)'
    })
    if ($canonical.Count -ne 1) {
        throw "Expected exactly one authenticated canonical Unity process, found $($canonical.Count)."
    }

    $accessTokenMatch = [regex]::Match($canonical[0].CommandLine, '(?i)-accessToken\s+(\S+)')
    $hubSessionMatch = [regex]::Match($canonical[0].CommandLine, '(?i)-hubSessionId\s+(\S+)')
    if (-not $accessTokenMatch.Success -or -not $hubSessionMatch.Success) {
        throw 'Canonical Unity authentication arguments could not be parsed.'
    }

    $arguments += @(
        '-useHub',
        '-hubIPC',
        '-cloudEnvironment', 'production',
        '-licensingIpc', 'LicenseClient-admin',
        '-hubSessionId', $hubSessionMatch.Groups[1].Value,
        '-accessToken', $accessTokenMatch.Groups[1].Value
    )
}

$process = Start-Process -FilePath $unity -ArgumentList $arguments -PassThru -WindowStyle Hidden
$startedAtValue = $process.StartTime.ToUniversalTime()
$startedAt = $startedAtValue.ToString('o')
$exited = $process.WaitForExit($TimeoutSeconds * 1000)

if (-not $exited) {
    $live = Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)"
    $liveStartedAt = if ($null -ne $live) { ([datetime] $live.CreationDate).ToUniversalTime() } else { $null }
    $sameStart = $null -ne $liveStartedAt -and [math]::Abs(($liveStartedAt - $startedAtValue).TotalSeconds) -le 2
    if ($null -eq $live -or $live.Name -ne 'Unity.exe' -or $live.CommandLine -notlike "*$projectPath*" -or -not $sameStart) {
        throw "Timed out, but PID $($process.Id) no longer resolves to the exact shadow Unity process; refusing to terminate it."
    }

    Stop-Process -Id $process.Id -Force
    throw "Timed out after $TimeoutSeconds seconds: name=$Name pid=$($process.Id) started=$startedAt xmlExists=$(Test-Path -LiteralPath $xml)."
}

$process.Refresh()
if (-not (Test-Path -LiteralPath $xml -PathType Leaf)) {
    throw "Unity exited without an XML result: name=$Name exit=$($process.ExitCode) log=$log."
}

[xml] $document = Get-Content -LiteralPath $xml -Raw
$run = $document.'test-run'
$result = [pscustomobject] @{
    Name = $Name
    Filter = $Filter
    Pid = $process.Id
    StartedAtUtc = $startedAt
    ExitCode = $process.ExitCode
    Total = [int] $run.total
    Passed = [int] $run.passed
    Failed = [int] $run.failed
    Skipped = [int] $run.skipped
    Inconclusive = [int] $run.inconclusive
    Result = [string] $run.result
    Xml = $xml
    Log = $log
}

$result | ConvertTo-Json -Depth 3
if ($process.ExitCode -ne 0 -or $result.Total -le 0 -or $result.Failed -ne 0 -or $result.Inconclusive -ne 0) {
    throw "Gate failed: name=$Name exit=$($process.ExitCode) total=$($result.Total) failed=$($result.Failed) inconclusive=$($result.Inconclusive)."
}
