[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string]$Milestone = "phase2",

    [Parameter()]
    [string]$ReceiptRoot,

    [Parameter()]
    [string]$BaselineRootManifest = (Join-Path $PSScriptRoot "phase2-baseline-roots.json"),

    [Parameter()]
    [string[]]$MutableRoot = @(),

    [Parameter()]
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$script:RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$script:DefaultRoots = @(
    "src/VFXComposer.Protocol",
    "src/VFXComposer.Protocol.Tests",
    "src/VFXComposer.AI.Contracts",
    "src/VFXComposer.AI.Providers",
    "src/VFXComposer.AI.Tests",
    "src/VFXComposer.Client",
    "src/VFXComposer.Client.Tests",
    "docs/schemas/desktop",
    "apps/VFXComposer.Desktop",
    "apps/VFXComposer.Desktop.Tests",
    "services/VFXComposer.Broker",
    "services/VFXComposer.Broker.Tests",
    "services/VFXComposer.Broker.ServiceHost",
    "services/VFXComposer.Broker.ServiceHost.Tests",
    "services/VFXComposer.UnityWorker",
    "tests/VFXComposer.LocalE2E.Tests",
    "project/Packages/com.vfxcomposer.unity",
    "VFXComposer.sln"
)

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [AllowEmptyString()] [string]$Value
    )

    $parent = Split-Path -Parent $Path
    if ($parent) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Value, $script:Utf8NoBom)
}

function Write-Json {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [object]$Value
    )

    Write-Utf8NoBom -Path $Path -Value (($Value | ConvertTo-Json -Depth 12) + "`n")
}

function Get-Sha256 {
    param([Parameter(Mandatory)] [string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Format-LiteralCommand {
    param(
        [Parameter(Mandatory)] [string]$FileName,
        [Parameter(Mandatory)] [string[]]$Arguments
    )

    $parts = @($FileName)
    foreach ($argument in $Arguments) {
        if ($argument -match '[\s";]') {
            $parts += '"{0}"' -f ($argument.Replace('"', '\"'))
        }
        else {
            $parts += $argument
        }
    }
    return $parts -join " "
}

function Convert-RawBytesToText {
    param([Parameter(Mandatory)] [AllowEmptyCollection()] [byte[]]$Bytes)

    try {
        $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
        return $strictUtf8.GetString($Bytes).Replace("`r`n", "`n").Replace("`r", "`n")
    }
    catch {
        return "<non-UTF8; see raw .bin and .hex receipts>`n"
    }
}

function Invoke-CapturedProcess {
    param(
        [Parameter(Mandatory)] [string]$Id,
        [Parameter(Mandatory)] [string]$FileName,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [int[]]$ExpectedExitCodes,
        [Parameter(Mandatory)] [string]$OutputRoot
    )

    $commandRoot = Join-Path $OutputRoot $Id
    [System.IO.Directory]::CreateDirectory($commandRoot) | Out-Null
    $literal = Format-LiteralCommand -FileName $FileName -Arguments $Arguments
    Write-Utf8NoBom -Path (Join-Path $commandRoot "command.txt") -Value ($literal + "`n")

    $start = [DateTimeOffset]::UtcNow
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.WorkingDirectory = $script:RepositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $stdout = [System.IO.MemoryStream]::new()
    $stderr = [System.IO.MemoryStream]::new()
    try {
        if (-not $process.Start()) {
            throw "Process '$Id' did not start."
        }
        $pidValue = $process.Id
        $stdoutTask = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
        $stderrTask = $process.StandardError.BaseStream.CopyToAsync($stderr)
        $process.WaitForExit()
        [System.Threading.Tasks.Task]::WaitAll(@($stdoutTask, $stderrTask))
        $exitCode = $process.ExitCode
    }
    finally {
        $end = [DateTimeOffset]::UtcNow
        $process.Dispose()
    }

    $stdoutBytes = $stdout.ToArray()
    $stderrBytes = $stderr.ToArray()
    $stdout.Dispose()
    $stderr.Dispose()

    [System.IO.File]::WriteAllBytes((Join-Path $commandRoot "stdout.bin"), $stdoutBytes)
    [System.IO.File]::WriteAllBytes((Join-Path $commandRoot "stderr.bin"), $stderrBytes)
    Write-Utf8NoBom -Path (Join-Path $commandRoot "stdout.txt") -Value (Convert-RawBytesToText -Bytes $stdoutBytes)
    Write-Utf8NoBom -Path (Join-Path $commandRoot "stderr.txt") -Value (Convert-RawBytesToText -Bytes $stderrBytes)
    Write-Utf8NoBom -Path (Join-Path $commandRoot "stdout.hex") -Value (([Convert]::ToHexString($stdoutBytes)) + "`n")
    Write-Utf8NoBom -Path (Join-Path $commandRoot "stderr.hex") -Value (([Convert]::ToHexString($stderrBytes)) + "`n")
    Write-Utf8NoBom -Path (Join-Path $commandRoot "exit.txt") -Value ("{0}`n" -f $exitCode)
    Write-Json -Path (Join-Path $commandRoot "metadata.json") -Value ([ordered]@{
        id = $Id
        command = $literal
        executable = $FileName
        arguments = $Arguments
        workingDirectory = $script:RepositoryRoot.Replace('\', '/')
        pid = $pidValue
        startUtc = $start.ToString("O")
        endUtc = $end.ToString("O")
        exitCode = $exitCode
        expectedExitCodes = $ExpectedExitCodes
        stdoutBytes = $stdoutBytes.Length
        stderrBytes = $stderrBytes.Length
        passed = $ExpectedExitCodes -contains $exitCode
    })

    return [pscustomobject]@{
        Id = $Id
        ExitCode = $exitCode
        Passed = $ExpectedExitCodes -contains $exitCode
        Stdout = $stdoutBytes
        Stderr = $stderrBytes
        StartUtc = $start
        EndUtc = $end
        Pid = $pidValue
    }
}

function Get-RepositoryFiles {
    $git = Get-Command git -ErrorAction Stop
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $git.Source
    $startInfo.WorkingDirectory = $script:RepositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    [void]$startInfo.ArgumentList.Add("ls-files")
    [void]$startInfo.ArgumentList.Add("-co")
    [void]$startInfo.ArgumentList.Add("--exclude-standard")
    [void]$startInfo.ArgumentList.Add("-z")
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $raw = $process.StandardOutput.ReadToEnd()
    $errorText = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) {
        throw "git ls-files failed: $errorText"
    }
    $process.Dispose()

    $paths = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $raw.Split([char]0, [System.StringSplitOptions]::RemoveEmptyEntries)) {
        $normalized = $path.Replace('\', '/')
        $segments = $normalized.Split('/')
        if ($segments -contains "bin" -or $segments -contains "obj") {
            continue
        }
        if ([System.IO.File]::Exists((Join-Path $script:RepositoryRoot $path))) {
            $paths.Add($normalized)
        }
    }
    $paths.Sort([System.StringComparer]::Ordinal)
    return $paths
}

function Write-FileManifest {
    param(
        [Parameter(Mandatory)] [System.Collections.Generic.List[string]]$Paths,
        [Parameter(Mandatory)] [string]$OutputPath
    )

    $builder = [System.Text.StringBuilder]::new()
    foreach ($relativePath in $Paths) {
        $absolutePath = Join-Path $script:RepositoryRoot $relativePath
        [void]$builder.Append((Get-Sha256 -Path $absolutePath))
        [void]$builder.Append("  ")
        [void]$builder.Append($relativePath)
        [void]$builder.Append("`n")
    }
    Write-Utf8NoBom -Path $OutputPath -Value $builder.ToString()
    return [pscustomobject]@{
        count = $Paths.Count
        sha256 = Get-Sha256 -Path $OutputPath
    }
}

function Get-RootAggregate {
    param([Parameter(Mandatory)] [string]$RelativeRoot)

    $absoluteRoot = Join-Path $script:RepositoryRoot $RelativeRoot
    if ([System.IO.File]::Exists($absoluteRoot)) {
        return [ordered]@{ path = $RelativeRoot; kind = "file"; count = 1; sha256 = Get-Sha256 -Path $absoluteRoot }
    }
    if (-not [System.IO.Directory]::Exists($absoluteRoot)) {
        return [ordered]@{ path = $RelativeRoot; kind = "absent"; count = 0; sha256 = $null }
    }

    $paths = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $absoluteRoot -Recurse -File -Force) {
        $relative = [System.IO.Path]::GetRelativePath($script:RepositoryRoot, $file.FullName).Replace('\', '/')
        $segments = $relative.Split('/')
        if ($segments -contains "bin" -or $segments -contains "obj") {
            continue
        }
        $paths.Add($relative)
    }
    $paths.Sort([System.StringComparer]::Ordinal)
    $builder = [System.Text.StringBuilder]::new()
    foreach ($relative in $paths) {
        [void]$builder.Append((Get-Sha256 -Path (Join-Path $script:RepositoryRoot $relative)))
        [void]$builder.Append("  ")
        [void]$builder.Append($relative)
        [void]$builder.Append("`n")
    }
    $bytes = $script:Utf8NoBom.GetBytes($builder.ToString())
    $hash = [System.Security.Cryptography.SHA256]::HashData($bytes)
    return [ordered]@{
        path = $RelativeRoot
        kind = "directory"
        count = $paths.Count
        sha256 = [Convert]::ToHexString($hash).ToLowerInvariant()
    }
}

function Get-RootSnapshot {
    $rows = @()
    foreach ($root in $script:DefaultRoots) {
        $rows += Get-RootAggregate -RelativeRoot $root
    }
    return $rows
}

function Compare-RootSnapshot {
    param(
        [Parameter(Mandatory)] [object[]]$Current,
        [Parameter(Mandatory)] [string]$ManifestPath,
        [Parameter(Mandatory)] [AllowEmptyCollection()] [string[]]$Mutable
    )

    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "Baseline root manifest is missing: $ManifestPath"
    }
    $baseline = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $mutableSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $Mutable) { [void]$mutableSet.Add($entry.Replace('\', '/')) }
    $mismatches = @()
    foreach ($expected in $baseline.roots) {
        $actual = $Current | Where-Object { $_.path -ceq $expected.path } | Select-Object -First 1
        if ($null -eq $actual) {
            $mismatches += [ordered]@{ path = $expected.path; reason = "missing-current-root" }
            continue
        }
        if ($mutableSet.Contains([string]$expected.path)) {
            continue
        }
        if ([int64]$actual.count -ne [int64]$expected.count -or [string]$actual.sha256 -cne [string]$expected.sha256) {
            $mismatches += [ordered]@{
                path = $expected.path
                expectedCount = $expected.count
                actualCount = $actual.count
                expectedSha256 = $expected.sha256
                actualSha256 = $actual.sha256
            }
        }
    }
    return $mismatches
}

function Get-AssemblyIdentity {
    param([Parameter(Mandatory)] [string]$RelativePath)

    $absolutePath = Join-Path $script:RepositoryRoot $RelativePath
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        return [ordered]@{ path = $RelativePath; present = $false }
    }
    $stream = [System.IO.File]::Open($absolutePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, ([System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete))
    try {
        $reader = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $metadata = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($reader)
            $module = $metadata.GetModuleDefinition()
            $mvid = $metadata.GetGuid($module.Mvid)
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
    return [ordered]@{
        path = $RelativePath
        present = $true
        sha256 = Get-Sha256 -Path $absolutePath
        mvid = $mvid.ToString("D")
        length = (Get-Item -LiteralPath $absolutePath).Length
    }
}

function Get-ResidueSnapshot {
    $runtimeProcesses = @()
    try {
        $runtimeProcesses = @(Get-CimInstance Win32_Process | Where-Object {
            $_.CommandLine -and $_.CommandLine -match 'VFXComposer\.(?:Broker|UnityWorker)(?:\.dll|\.exe)'
        } | ForEach-Object {
            [ordered]@{ processId = $_.ProcessId; name = $_.Name; commandLine = $_.CommandLine }
        })
    }
    catch {
        $runtimeProcesses = @([ordered]@{ queryError = $_.Exception.Message })
    }

    $pipeNames = @()
    try {
        $pipeNames = @(Get-ChildItem -LiteralPath '\\.\pipe\' -ErrorAction Stop | Where-Object {
            $_.Name -match '(?i)vfxcomposer'
        } | ForEach-Object { $_.Name })
    }
    catch {
        $pipeNames = @("<query-error: $($_.Exception.Message)>")
    }

    $localE2ETemporaryRoots = @()
    try {
        $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $localE2ETemporaryRoots = @(Get-ChildItem -LiteralPath $temporaryRoot -Directory -Force -ErrorAction Stop |
            Where-Object { $_.Name.StartsWith("vfxcomposer-u5-", [System.StringComparison]::Ordinal) } |
            ForEach-Object { $_.FullName })
    }
    catch {
        $localE2ETemporaryRoots = @("<query-error: $($_.Exception.Message)>")
    }

    return [ordered]@{
        runtimeProcesses = $runtimeProcesses
        vfxComposerNamedPipes = $pipeNames
        localE2ETemporaryRoots = $localE2ETemporaryRoots
        claim = "Point-in-time staged runtime, named-pipe, and owned LocalE2E temporary-root enumeration only; not proof of historical absence."
    }
}

function Write-ReceiptManifest {
    param([Parameter(Mandatory)] [string]$Root)

    $manifestPath = Join-Path $Root "receipt-manifest.sha256"
    $paths = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Force) {
        if ($file.FullName -ceq $manifestPath) { continue }
        $paths.Add([System.IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/'))
    }
    $paths.Sort([System.StringComparer]::Ordinal)
    $builder = [System.Text.StringBuilder]::new()
    foreach ($relative in $paths) {
        [void]$builder.Append((Get-Sha256 -Path (Join-Path $Root $relative)))
        [void]$builder.Append("  ")
        [void]$builder.Append($relative)
        [void]$builder.Append("`n")
    }
    Write-Utf8NoBom -Path $manifestPath -Value $builder.ToString()
    return [ordered]@{ path = $manifestPath; count = $paths.Count; sha256 = Get-Sha256 -Path $manifestPath }
}

$timestamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMddTHHmmssZ")
if ([string]::IsNullOrWhiteSpace($ReceiptRoot)) {
    $ReceiptRoot = Join-Path $script:RepositoryRoot (".codex_tmp/phase2-gate/{0}-{1}" -f $Milestone, $timestamp)
}
elseif (-not [System.IO.Path]::IsPathFullyQualified($ReceiptRoot)) {
    $ReceiptRoot = Join-Path $script:RepositoryRoot $ReceiptRoot
}
$ReceiptRoot = [System.IO.Path]::GetFullPath($ReceiptRoot)
if (-not $ReceiptRoot.StartsWith((Join-Path $script:RepositoryRoot ".codex_tmp"), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "ReceiptRoot must remain below the repository .codex_tmp directory."
}

$testResults = Join-Path $ReceiptRoot "test-results"
$commands = @(
    [ordered]@{ phase = "build"; id = "ai-contracts-build"; file = "dotnet"; args = @("build", "src/VFXComposer.AI.Contracts/VFXComposer.AI.Contracts.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "ai-providers-build"; file = "dotnet"; args = @("build", "src/VFXComposer.AI.Providers/VFXComposer.AI.Providers.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "ai-test-build"; file = "dotnet"; args = @("build", "src/VFXComposer.AI.Tests/VFXComposer.AI.Tests.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "protocol-build"; file = "dotnet"; args = @("build", "src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "client-build"; file = "dotnet"; args = @("build", "src/VFXComposer.Client.Tests/VFXComposer.Client.Tests.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "broker-build"; file = "dotnet"; args = @("build", "services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "worker-build"; file = "dotnet"; args = @("build", "services/VFXComposer.UnityWorker/VFXComposer.UnityWorker.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "local-e2e-build"; file = "dotnet"; args = @("build", "tests/VFXComposer.LocalE2E.Tests/VFXComposer.LocalE2E.Tests.csproj", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "build"; id = "solution-build"; file = "dotnet"; args = @("build", "VFXComposer.sln", "--configuration", "Release", "--no-restore", "-p:RestoreLockedMode=true"); expected = @(0) },
    [ordered]@{ phase = "run"; id = "protocol-test"; file = "dotnet"; args = @("test", "src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj", "--configuration", "Release", "--no-build", "--no-restore", "-p:RestoreLockedMode=true", "--logger", "trx;LogFileName=protocol.trx", "--results-directory", (Join-Path $testResults "protocol")); expected = @(0) },
    [ordered]@{ phase = "run"; id = "ai-test"; file = "dotnet"; args = @("test", "src/VFXComposer.AI.Tests/VFXComposer.AI.Tests.csproj", "--configuration", "Release", "--no-build", "--no-restore", "-p:RestoreLockedMode=true", "--logger", "trx;LogFileName=ai.trx", "--results-directory", (Join-Path $testResults "ai")); expected = @(0) },
    [ordered]@{ phase = "run"; id = "client-test"; file = "dotnet"; args = @("test", "src/VFXComposer.Client.Tests/VFXComposer.Client.Tests.csproj", "--configuration", "Release", "--no-build", "--no-restore", "-p:RestoreLockedMode=true", "--logger", "trx;LogFileName=client.trx", "--results-directory", (Join-Path $testResults "client")); expected = @(0) },
    [ordered]@{ phase = "run"; id = "broker-test"; file = "dotnet"; args = @("test", "services/VFXComposer.Broker.Tests/VFXComposer.Broker.Tests.csproj", "--configuration", "Release", "--no-build", "--no-restore", "-p:RestoreLockedMode=true", "--logger", "trx;LogFileName=broker.trx", "--results-directory", (Join-Path $testResults "broker")); expected = @(0) },
    [ordered]@{ phase = "run"; id = "local-e2e-test"; file = "dotnet"; args = @("test", "tests/VFXComposer.LocalE2E.Tests/VFXComposer.LocalE2E.Tests.csproj", "--configuration", "Release", "--no-build", "--no-restore", "-p:RestoreLockedMode=true", "--logger", "trx;LogFileName=local-e2e.trx", "--results-directory", (Join-Path $testResults "local-e2e")); expected = @(0) },
    [ordered]@{ phase = "run"; id = "schema"; file = "python"; args = @("eng/verify-phase2-schemas.py"); expected = @(0) },
    [ordered]@{ phase = "run"; id = "broker-smoke"; file = "dotnet"; args = @("services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll"); expected = @(23) }
)

if ($DryRun) {
    [pscustomobject]@{
        repositoryRoot = $script:RepositoryRoot
        milestone = $Milestone
        receiptRoot = $ReceiptRoot
        baselineRootManifest = [System.IO.Path]::GetFullPath($BaselineRootManifest)
        mutableRoots = $MutableRoot
        commands = @($commands | ForEach-Object { Format-LiteralCommand -FileName $_.file -Arguments $_.args })
    } | ConvertTo-Json -Depth 6
    return
}

[System.IO.Directory]::CreateDirectory($ReceiptRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($testResults) | Out-Null
$gateStart = [DateTimeOffset]::UtcNow
$failures = [System.Collections.Generic.List[string]]::new()

$preFiles = Get-RepositoryFiles
$preManifest = Write-FileManifest -Paths $preFiles -OutputPath (Join-Path $ReceiptRoot "workspace-source-pre.sha256")
$preRoots = @(Get-RootSnapshot)
Write-Json -Path (Join-Path $ReceiptRoot "roots-pre.json") -Value ([ordered]@{ roots = $preRoots })

$buildResults = @()
foreach ($command in $commands | Where-Object { $_.phase -eq "build" }) {
    $result = Invoke-CapturedProcess -Id $command.id -FileName $command.file -Arguments $command.args -ExpectedExitCodes $command.expected -OutputRoot (Join-Path $ReceiptRoot "commands")
    $buildResults += $result
    if (-not $result.Passed) { $failures.Add("$($command.id) exited $($result.ExitCode)") }
}

$bindingBeforeTests = @(
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.Protocol.Tests/bin/Release/net8.0/VFXComposer.Protocol.Tests.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.AI.Contracts/bin/Release/net8.0/VFXComposer.AI.Contracts.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.AI.Providers/bin/Release/net8.0/VFXComposer.AI.Providers.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.AI.Tests/bin/Release/net8.0/VFXComposer.AI.Tests.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.Client.Tests/bin/Release/net8.0/VFXComposer.Client.Tests.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.Client/bin/Release/net8.0/VFXComposer.Client.dll"
    Get-AssemblyIdentity -RelativePath "services/VFXComposer.Broker.Tests/bin/Release/net8.0/VFXComposer.Broker.Tests.dll"
    Get-AssemblyIdentity -RelativePath "services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll"
    Get-AssemblyIdentity -RelativePath "services/VFXComposer.UnityWorker/bin/Release/net8.0-windows/VFXComposer.UnityWorker.dll"
    Get-AssemblyIdentity -RelativePath "tests/VFXComposer.LocalE2E.Tests/bin/Release/net8.0-windows/VFXComposer.LocalE2E.Tests.dll"
    Get-AssemblyIdentity -RelativePath "tests/VFXComposer.LocalE2E.Tests/bin/Release/net8.0-windows/VFXComposer.Broker.dll"
    Get-AssemblyIdentity -RelativePath "tests/VFXComposer.LocalE2E.Tests/bin/Release/net8.0-windows/VFXComposer.UnityWorker.dll"
)
Write-Json -Path (Join-Path $ReceiptRoot "assembly-binding-before-tests.json") -Value ([ordered]@{ assemblies = $bindingBeforeTests })

$runResults = @()
foreach ($command in $commands | Where-Object { $_.phase -eq "run" }) {
    $result = Invoke-CapturedProcess -Id $command.id -FileName $command.file -Arguments $command.args -ExpectedExitCodes $command.expected -OutputRoot (Join-Path $ReceiptRoot "commands")
    $runResults += $result
    if (-not $result.Passed) { $failures.Add("$($command.id) exited $($result.ExitCode)") }
    if ($command.id -eq "broker-smoke") {
        $expectedSmokeStderr = [System.Text.Encoding]::ASCII.GetBytes("W24FS001`r`n")
        if ($result.Stdout.Length -ne 0) { $failures.Add("broker-smoke stdout was not empty") }
        if (-not [System.Security.Cryptography.CryptographicOperations]::FixedTimeEquals($result.Stderr, $expectedSmokeStderr)) {
            $failures.Add("broker-smoke stderr was not exact W24FS001 CRLF")
        }
    }
}

$bindingAfterTests = @(
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.Protocol.Tests/bin/Release/net8.0/VFXComposer.Protocol.Tests.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.AI.Contracts/bin/Release/net8.0/VFXComposer.AI.Contracts.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.AI.Providers/bin/Release/net8.0/VFXComposer.AI.Providers.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.AI.Tests/bin/Release/net8.0/VFXComposer.AI.Tests.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.Client.Tests/bin/Release/net8.0/VFXComposer.Client.Tests.dll"
    Get-AssemblyIdentity -RelativePath "src/VFXComposer.Client/bin/Release/net8.0/VFXComposer.Client.dll"
    Get-AssemblyIdentity -RelativePath "services/VFXComposer.Broker.Tests/bin/Release/net8.0/VFXComposer.Broker.Tests.dll"
    Get-AssemblyIdentity -RelativePath "services/VFXComposer.Broker/bin/Release/net8.0/VFXComposer.Broker.dll"
    Get-AssemblyIdentity -RelativePath "services/VFXComposer.UnityWorker/bin/Release/net8.0-windows/VFXComposer.UnityWorker.dll"
    Get-AssemblyIdentity -RelativePath "tests/VFXComposer.LocalE2E.Tests/bin/Release/net8.0-windows/VFXComposer.LocalE2E.Tests.dll"
    Get-AssemblyIdentity -RelativePath "tests/VFXComposer.LocalE2E.Tests/bin/Release/net8.0-windows/VFXComposer.Broker.dll"
    Get-AssemblyIdentity -RelativePath "tests/VFXComposer.LocalE2E.Tests/bin/Release/net8.0-windows/VFXComposer.UnityWorker.dll"
)
Write-Json -Path (Join-Path $ReceiptRoot "assembly-binding-after-tests.json") -Value ([ordered]@{ assemblies = $bindingAfterTests })
if (($bindingBeforeTests | ConvertTo-Json -Depth 6 -Compress) -cne ($bindingAfterTests | ConvertTo-Json -Depth 6 -Compress)) {
    $failures.Add("tested assembly SHA/MVID binding changed during the test phase")
}

$postFiles = Get-RepositoryFiles
$postManifest = Write-FileManifest -Paths $postFiles -OutputPath (Join-Path $ReceiptRoot "workspace-source-post.sha256")
if ($preManifest.count -ne $postManifest.count -or $preManifest.sha256 -cne $postManifest.sha256) {
    $failures.Add("workspace source manifest changed while the gate was running")
}

$postRoots = @(Get-RootSnapshot)
Write-Json -Path (Join-Path $ReceiptRoot "roots-post.json") -Value ([ordered]@{ roots = $postRoots })
$rootMismatches = @(Compare-RootSnapshot -Current $postRoots -ManifestPath $BaselineRootManifest -Mutable $MutableRoot)
Write-Json -Path (Join-Path $ReceiptRoot "frozen-root-replay.json") -Value ([ordered]@{
    baselineManifest = [System.IO.Path]::GetFullPath($BaselineRootManifest).Replace('\', '/')
    mutableRoots = $MutableRoot
    mismatchCount = $rootMismatches.Count
    mismatches = $rootMismatches
})
if ($rootMismatches.Count -ne 0) {
    $failures.Add("frozen root replay found $($rootMismatches.Count) mismatch(es)")
}

$residue = Get-ResidueSnapshot
Write-Json -Path (Join-Path $ReceiptRoot "residue.json") -Value $residue
if (@($residue.runtimeProcesses | Where-Object { -not $_.Contains('queryError') }).Count -ne 0) {
    $failures.Add("Broker or Worker process residue was present after the gate")
}
if (@($residue.vfxComposerNamedPipes | Where-Object { $_ -notlike '<query-error:*' }).Count -ne 0) {
    $failures.Add("VFX Composer named-pipe residue was present after the gate")
}
if (@($residue.localE2ETemporaryRoots | Where-Object { $_ -notlike '<query-error:*' }).Count -ne 0) {
    $failures.Add("LocalE2E temporary-project residue was present after the gate")
}

$gateEnd = [DateTimeOffset]::UtcNow
Write-Json -Path (Join-Path $ReceiptRoot "summary.json") -Value ([ordered]@{
    formatVersion = 1
    milestone = $Milestone
    repositoryRoot = $script:RepositoryRoot.Replace('\', '/')
    startUtc = $gateStart.ToString("O")
    endUtc = $gateEnd.ToString("O")
    sourceManifest = [ordered]@{ count = $postManifest.count; sha256 = $postManifest.sha256 }
    failures = $failures
    passed = $failures.Count -eq 0
    limitation = "This runner does not install/start SCM services, enable privileges, run Unity Editor, or claim production activation."
})
$receiptManifest = Write-ReceiptManifest -Root $ReceiptRoot

Write-Host ("Phase 2 gate receipts: {0}" -f $ReceiptRoot)
Write-Host ("Receipt manifest: {0} rows / {1}" -f $receiptManifest.count, $receiptManifest.sha256)
if ($failures.Count -ne 0) {
    throw ("Phase 2 gate failed: " + ($failures -join "; "))
}
Write-Host "Phase 2 gate passed."
