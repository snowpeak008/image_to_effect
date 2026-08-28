using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using VFXComposer.Editor.W24;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Builds one real Windows Development Player containing only the package fixture, then
    /// launches that same build through invalid-command, forced-probe-failure, success and
    /// write-once-conflict cases. The fixture is protocol evidence only: no visual/L3/L4
    /// authority can be produced here.
    /// </summary>
    public sealed class W24S2PlayerBuildEvidenceTests
    {
        private const string ScenePath = "Packages/com.vfxcomposer.unity/Tests/PlayerEvidence/W24S2PlayerEvidence.unity";
        private const string TemporaryPrefix = "vfxcomposer_w24_s2_player_";
        private const int PlayerExitTimeoutMilliseconds = 120000;
        private const int ForcedTerminationTimeoutMilliseconds = 15000;

        private static readonly string[] RootFields =
        {
            "schema", "passed", "exitCode", "unityVersion", "runtimePlatform",
            "graphicsDevice", "batchMode", "modules", "failure"
        };

        private static readonly string[] ModuleFields = { "moduleId", "passed", "detail" };

        private static readonly string[] ExpectedModules =
        {
            "moving_emitter_trail",
            "model_binding",
            "fragment_motion",
            "real_lighting",
            "semantic_state_machine",
            "semantic_telemetry"
        };

        private sealed class PlayerLaunchResult
        {
            public string Scenario;
            public string LogPath;
            public int ExitCode;
        }

        /// <summary>
        /// Windows-only handle-bound launcher. The primary process is created suspended, added
        /// to a kill-on-close Job Object, and only then resumed, so no child can escape between
        /// Process.Start and job assignment and no PID-reuse termination race exists.
        /// </summary>
        private sealed class SuspendedJobProcess : IDisposable
        {
            private const uint CreateSuspended = 0x00000004;
            private const uint CreateNoWindow = 0x08000000;
            private const uint JobObjectLimitKillOnJobClose = 0x00002000;
            private const int JobObjectBasicAccountingInformation = 1;
            private const int JobObjectExtendedLimitInformation = 9;
            private const uint WaitObject0 = 0x00000000;
            private const uint WaitTimeout = 0x00000102;
            private const uint WaitFailed = 0xFFFFFFFF;
            private const uint StillActive = 259;

            [StructLayout(LayoutKind.Sequential)]
            private struct IoCounters
            {
                public ulong ReadOperationCount;
                public ulong WriteOperationCount;
                public ulong OtherOperationCount;
                public ulong ReadTransferCount;
                public ulong WriteTransferCount;
                public ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JobObjectBasicLimitInformation
            {
                public long PerProcessUserTimeLimit;
                public long PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JobObjectExtendedLimitInformationValue
            {
                public JobObjectBasicLimitInformation BasicLimitInformation;
                public IoCounters IoInfo;
                public UIntPtr ProcessMemoryLimit;
                public UIntPtr JobMemoryLimit;
                public UIntPtr PeakProcessMemoryUsed;
                public UIntPtr PeakJobMemoryUsed;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JobObjectBasicAccountingInformationValue
            {
                public long TotalUserTime;
                public long TotalKernelTime;
                public long ThisPeriodTotalUserTime;
                public long ThisPeriodTotalKernelTime;
                public uint TotalPageFaultCount;
                public uint TotalProcesses;
                public uint ActiveProcesses;
                public uint TotalTerminatedProcesses;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct StartupInfo
            {
                public int Size;
                public IntPtr Reserved;
                public IntPtr Desktop;
                public IntPtr Title;
                public uint X;
                public uint Y;
                public uint XSize;
                public uint YSize;
                public uint XCountChars;
                public uint YCountChars;
                public uint FillAttribute;
                public uint Flags;
                public ushort ShowWindow;
                public ushort Reserved2Size;
                public IntPtr Reserved2;
                public IntPtr StandardInput;
                public IntPtr StandardOutput;
                public IntPtr StandardError;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct ProcessInformation
            {
                public IntPtr Process;
                public IntPtr Thread;
                public uint ProcessId;
                public uint ThreadId;
            }

            [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            private static extern IntPtr CreateJobObjectW(IntPtr jobAttributes, string name);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetInformationJobObject(IntPtr job, int informationClass, IntPtr information, uint informationLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool QueryInformationJobObject(IntPtr job, int informationClass, out JobObjectBasicAccountingInformationValue information, uint informationLength, IntPtr returnLength);

            [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CreateProcessW(string applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory, ref StartupInfo startupInfo, out ProcessInformation processInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern uint ResumeThread(IntPtr thread);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool TerminateProcess(IntPtr process, uint exitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CloseHandle(IntPtr handle);

            private IntPtr job;
            private IntPtr process;
            private IntPtr primaryThread;
            private bool assigned;
            private bool disposed;

            private SuspendedJobProcess() { }

            public static SuspendedJobProcess Start(string executable, string workingDirectory, string arguments)
            {
                var owned = new SuspendedJobProcess();
                try
                {
                    owned.CreateConfiguredJob();
                    var startup = new StartupInfo { Size = Marshal.SizeOf(typeof(StartupInfo)) };
                    ProcessInformation information;
                    var commandLine = new StringBuilder(Quote(executable) + " " + arguments);
                    if (!CreateProcessW(executable, commandLine, IntPtr.Zero, IntPtr.Zero, false, CreateSuspended | CreateNoWindow, IntPtr.Zero, workingDirectory, ref startup, out information))
                        throw Win32Failure("CreateProcessW(CREATE_SUSPENDED)");
                    owned.process = information.Process;
                    owned.primaryThread = information.Thread;

                    if (!AssignProcessToJobObject(owned.job, owned.process)) throw Win32Failure("AssignProcessToJobObject");
                    owned.assigned = true;
                    var previousSuspendCount = ResumeThread(owned.primaryThread);
                    if (previousSuspendCount == uint.MaxValue) throw Win32Failure("ResumeThread");
                    if (previousSuspendCount != 1u) throw new InvalidOperationException("CREATE_SUSPENDED primary thread had unexpected suspend count " + previousSuspendCount + ".");
                    owned.ClosePrimaryThread();
                    return owned;
                }
                catch (Exception startFailure)
                {
                    try { owned.AbortFailedStart(); }
                    catch (Exception cleanupFailure)
                    {
                        throw new AggregateException("S2 suspended Player startup and exact-handle cleanup both failed.", startFailure, cleanupFailure);
                    }
                    throw;
                }
            }

            public bool WaitForExit(int timeoutMilliseconds)
            {
                return WaitForHandle(process, timeoutMilliseconds, "Player process handle");
            }

            public int ExitCode
            {
                get
                {
                    uint value;
                    if (!GetExitCodeProcess(process, out value)) throw Win32Failure("GetExitCodeProcess");
                    if (value == StillActive) throw new InvalidOperationException("Player exit code was requested while the handle was still active.");
                    return unchecked((int)value);
                }
            }

            public void TerminateTreeAndWait(int timeoutMilliseconds, string scenario)
            {
                if (job == IntPtr.Zero || !assigned) throw new InvalidOperationException("No assigned S2 Job Object exists for " + scenario + ".");
                if (ReadActiveProcessCount() > 0 && !TerminateJobObject(job, unchecked((uint)W24S2PlayerEvidenceProtocol.ProbeFailureExitCode)))
                    throw Win32Failure("TerminateJobObject(" + scenario + ")");
                WaitForJobEmpty(timeoutMilliseconds, scenario);
                if (!WaitForExit(timeoutMilliseconds))
                    throw new TimeoutException("The handle-bound " + scenario + " Player did not exit within " + timeoutMilliseconds + " ms after Job Object termination.");
            }

            public void CompleteTreeAfterPrimaryExit(int timeoutMilliseconds, string scenario)
            {
                if (!WaitForExit(0)) throw new InvalidOperationException("Primary Player handle was not signaled before post-exit tree cleanup for " + scenario + ".");
                if (ReadActiveProcessCount() > 0 && !TerminateJobObject(job, unchecked((uint)W24S2PlayerEvidenceProtocol.ProbeFailureExitCode)))
                    throw Win32Failure("TerminateJobObject(post-exit " + scenario + ")");
                WaitForJobEmpty(timeoutMilliseconds, scenario + " post-exit tree cleanup");
                if (!WaitForExit(0)) throw new InvalidOperationException("Primary Player handle lost its signaled state after post-exit tree cleanup for " + scenario + ".");
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Exception failure = null;
                try
                {
                    if (assigned && job != IntPtr.Zero && ReadActiveProcessCount() > 0)
                    {
                        if (!TerminateJobObject(job, unchecked((uint)W24S2PlayerEvidenceProtocol.ProbeFailureExitCode))) throw Win32Failure("TerminateJobObject(dispose)");
                        WaitForJobEmpty(ForcedTerminationTimeoutMilliseconds, "dispose");
                    }
                }
                catch (Exception exception) { failure = exception; }
                finally
                {
                    try { ClosePrimaryThread(); }
                    catch (Exception exception) { if (failure == null) failure = exception; }
                    try { CloseOwnedHandle(ref process, "Player process"); }
                    catch (Exception exception) { if (failure == null) failure = exception; }
                    try { CloseOwnedHandle(ref job, "S2 Job Object"); }
                    catch (Exception exception) { if (failure == null) failure = exception; }
                }
                if (failure != null) throw new InvalidOperationException("S2 handle-bound Player cleanup failed.", failure);
            }

            private void CreateConfiguredJob()
            {
                job = CreateJobObjectW(IntPtr.Zero, null);
                if (job == IntPtr.Zero) throw Win32Failure("CreateJobObjectW");
                var information = new JobObjectExtendedLimitInformationValue();
                information.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
                var size = Marshal.SizeOf(typeof(JobObjectExtendedLimitInformationValue));
                var memory = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(information, memory, false);
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, memory, unchecked((uint)size)))
                        throw Win32Failure("SetInformationJobObject(KILL_ON_JOB_CLOSE)");
                }
                finally { Marshal.FreeHGlobal(memory); }
            }

            private uint ReadActiveProcessCount()
            {
                JobObjectBasicAccountingInformationValue information;
                var size = unchecked((uint)Marshal.SizeOf(typeof(JobObjectBasicAccountingInformationValue)));
                if (!QueryInformationJobObject(job, JobObjectBasicAccountingInformation, out information, size, IntPtr.Zero))
                    throw Win32Failure("QueryInformationJobObject");
                return information.ActiveProcesses;
            }

            private void WaitForJobEmpty(int timeoutMilliseconds, string scenario)
            {
                var timer = Stopwatch.StartNew();
                while (ReadActiveProcessCount() != 0)
                {
                    if (timer.ElapsedMilliseconds >= timeoutMilliseconds)
                        throw new TimeoutException("The " + scenario + " Job Object was not empty after the bounded " + timeoutMilliseconds + " ms wait.");
                    System.Threading.Thread.Sleep(25);
                }
            }

            private void AbortFailedStart()
            {
                try
                {
                    if (process != IntPtr.Zero)
                    {
                        var terminated = assigned && job != IntPtr.Zero
                            ? TerminateJobObject(job, unchecked((uint)W24S2PlayerEvidenceProtocol.ProbeFailureExitCode))
                            : TerminateProcess(process, unchecked((uint)W24S2PlayerEvidenceProtocol.ProbeFailureExitCode));
                        if (!terminated)
                        {
                            var error = Marshal.GetLastWin32Error();
                            if (!WaitForHandle(process, 0, "failed-start Player process handle"))
                                throw new Win32Exception(error, (assigned ? "TerminateJobObject" : "TerminateProcess") + " failed during suspended Player startup cleanup");
                        }
                        if (!WaitForHandle(process, ForcedTerminationTimeoutMilliseconds, "failed-start Player process handle"))
                            throw new TimeoutException("The exact failed-start Player handle did not signal within the bounded cleanup wait.");
                        if (assigned && job != IntPtr.Zero) WaitForJobEmpty(ForcedTerminationTimeoutMilliseconds, "failed-start");
                    }
                }
                finally
                {
                    try { ClosePrimaryThread(); }
                    finally
                    {
                        try { CloseOwnedHandle(ref process, "failed-start Player process"); }
                        finally { CloseOwnedHandle(ref job, "failed-start S2 Job Object"); }
                    }
                }
            }

            private void ClosePrimaryThread()
            {
                CloseOwnedHandle(ref primaryThread, "Player primary thread");
            }

            private static bool WaitForHandle(IntPtr handle, int timeoutMilliseconds, string label)
            {
                if (handle == IntPtr.Zero) throw new InvalidOperationException(label + " is not available.");
                var result = WaitForSingleObject(handle, unchecked((uint)timeoutMilliseconds));
                if (result == WaitObject0) return true;
                if (result == WaitTimeout) return false;
                if (result == WaitFailed) throw Win32Failure("WaitForSingleObject(" + label + ")");
                throw new InvalidOperationException("Unexpected wait result 0x" + result.ToString("X8") + " for " + label + ".");
            }

            private static void CloseOwnedHandle(ref IntPtr handle, string label)
            {
                if (handle == IntPtr.Zero) return;
                var value = handle;
                handle = IntPtr.Zero;
                if (!CloseHandle(value)) throw Win32Failure("CloseHandle(" + label + ")");
            }

            private static Win32Exception Win32Failure(string operation)
            {
                return new Win32Exception(Marshal.GetLastWin32Error(), operation + " failed");
            }
        }

        [Test]
        [Timeout(15 * 60 * 1000)]
        public void S2_DedicatedWindowsPlayer_BuildsLaunchesAndPassesAllSixRuntimeModules()
        {
            Assert.That(Application.platform, Is.EqualTo(RuntimePlatform.WindowsEditor), "W24 S2 fixes its Player evidence target to Windows; another Editor host must not substitute a different platform.");
            Assert.That(BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64), Is.True, "StandaloneWindows64 support is required; missing platform support is a fail-closed S2 result.");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null, "Dedicated package scene must be imported before the Player build starts.");

            var originalBuildSettingsScenes = EditorBuildSettings.scenes.Select(CloneBuildSettingsScene).ToArray();
            var originalBuildSettings = SnapshotBuildSettings();
            var folder = NewExternalTemporaryFolderPath();
            var executable = Path.Combine(folder, "W24S2RuntimeProbe.exe");
            var resultPath = FixedResultPath(folder);
            try
            {
                ValidateExternalTemporaryFolder(folder, false, false);
                Assert.That(Directory.Exists(folder) || File.Exists(folder), Is.False, "The exact random system-temp child must be absent before BuildPipeline owns it.");
                Directory.CreateDirectory(folder);
                ValidateExternalTemporaryFolder(folder, true, true);

                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = executable,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode | BuildOptions.Development
                });
                Assert.That(report, Is.Not.Null, "BuildPipeline returned no report.");
                Assert.That(report.summary.result, Is.EqualTo(BuildResult.Succeeded), DescribeBuild(report));
                Assert.That(File.Exists(executable), Is.True, "BuildPipeline reported success without producing the Player executable.");
                ValidateExternalTemporaryFolder(folder, true, true);
                Assert.That(SnapshotBuildSettings(), Is.EqualTo(originalBuildSettings), "The explicit package-scene build must not mutate EditorBuildSettings.");
                AssertFreshPublicationDestination(resultPath, folder);

                var invalid = LaunchPlayer(executable, folder, "invalid-command", W24S2PlayerEvidenceProtocol.ActivationArgument + " " + W24S2PlayerEvidenceProtocol.ActivationArgument);
                RecordNUnitEvidence(invalid, null, null);
                Assert.That(invalid.ExitCode, Is.EqualTo(W24S2PlayerEvidenceProtocol.InvalidCommandExitCode));
                AssertFreshPublicationDestination(resultPath, folder);

                var malformed = LaunchPlayer(executable, folder, "invalid-flag-value", W24S2PlayerEvidenceProtocol.ActivationArgument + " forbidden-value");
                RecordNUnitEvidence(malformed, null, null);
                Assert.That(malformed.ExitCode, Is.EqualTo(W24S2PlayerEvidenceProtocol.InvalidCommandExitCode));
                AssertFreshPublicationDestination(resultPath, folder);

                var forced = LaunchPlayer(executable, folder, "forced-probe-failure", W24S2PlayerEvidenceProtocol.ActivationArgument + " " + W24S2PlayerEvidenceProtocol.ForceProbeFailureArgument);
                var forcedJson = ParseStrictResult(resultPath);
                RecordNUnitEvidence(forced, forcedJson, resultPath);
                Assert.That(forced.ExitCode, Is.EqualTo(W24S2PlayerEvidenceProtocol.ProbeFailureExitCode));
                AssertCommonResultShape(forcedJson);
                Assert.That(ReadBoolean(forcedJson, "passed"), Is.False);
                Assert.That(ReadInteger(forcedJson, "exitCode"), Is.EqualTo(W24S2PlayerEvidenceProtocol.ProbeFailureExitCode));
                Assert.That(ReadString(forcedJson, "failure"), Does.Contain("stage=forced_probe_failure"));
                Assert.That(ReadString(forcedJson, "failure"), Does.Contain("System.InvalidOperationException"));
                Assert.That(RequireType(forcedJson, "modules", JTokenType.Array).Children().Count(), Is.Zero, "The frozen forced-failure shape contains no fabricated module rows.");
                DeleteOwnedFixedResultForNextLaunch(resultPath, folder);
                AssertFreshPublicationDestination(resultPath, folder);

                var success = LaunchPlayer(executable, folder, "success", W24S2PlayerEvidenceProtocol.ActivationArgument);
                var successJson = ParseStrictResult(resultPath);
                RecordNUnitEvidence(success, successJson, resultPath);
                Assert.That(success.ExitCode, Is.EqualTo(W24S2PlayerEvidenceProtocol.SuccessExitCode));
                var successBytes = File.ReadAllBytes(resultPath);
                AssertSuccessfulResult(successJson);
                AssertSingleFinalAndNoPending(resultPath, folder);

                var conflict = LaunchPlayer(executable, folder, "write-once-conflict", W24S2PlayerEvidenceProtocol.ActivationArgument);
                var conflictJson = ParseStrictResult(resultPath);
                RecordNUnitEvidence(conflict, conflictJson, resultPath);
                Assert.That(conflict.ExitCode, Is.EqualTo(W24S2PlayerEvidenceProtocol.ProbeFailureExitCode), "A pre-existing final result must fail closed with the probe/write failure exit code.");
                CollectionAssert.AreEqual(successBytes, File.ReadAllBytes(resultPath), "The Player must never delete, truncate or replace a pre-existing result.");
                AssertSingleFinalAndNoPending(resultPath, folder);
                AssertSuccessfulResult(conflictJson);
            }
            finally
            {
                // Temp cleanup and Build Settings restoration are deliberately independent.
                // If either throws, the outer finally still attempts the other authority boundary.
                try
                {
                    CleanupExternalTemporaryFolder(folder);
                }
                finally
                {
                    RestoreBuildSettings(originalBuildSettingsScenes, originalBuildSettings);
                }
            }
        }

        [Test]
        public void S2_PlayerEvidenceFixture_IsPackageOwnedAndDoesNotDependOnAssetsOrBuildSettings()
        {
            Assert.That(ScenePath, Does.StartWith("Packages/com.vfxcomposer.unity/"));
            Assert.That(ScenePath, Does.Not.StartWith("Assets/"));
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            Assert.That(EditorBuildSettings.scenes.Select(scene => scene.path), Does.Not.Contain(ScenePath), "The fixture is passed directly to BuildPlayer and must not be registered globally.");
            Assert.That(W24S2PlayerEvidenceProtocol.ActivationArgument, Is.EqualTo("-w24S2Evidence"));
            Assert.That(W24S2PlayerEvidenceProtocol.ForceProbeFailureArgument, Is.EqualTo("-w24S2ForceProbeFailure"));
            Assert.That(W24S2PlayerEvidenceProtocol.LegacyResultArgument, Is.EqualTo("-w24S2ResultPath"));
            Assert.That(W24S2PlayerEvidenceProtocol.ResultRelativePath, Is.EqualTo("evidence/player-result.json"));
            Assert.That(W24S2PlayerEvidenceProtocol.ResultSchema, Is.EqualTo("w24-s2-player-runtime-evidence/v1"));
        }

        private static PlayerLaunchResult LaunchPlayer(string executable, string folder, string scenario, string evidenceArguments)
        {
            ValidateExternalTemporaryFolder(folder, true, true);
            EnsureNoReparsePoints(executable, false);
            var logs = Path.Combine(folder, "logs");
            Directory.CreateDirectory(logs);
            EnsureNoReparsePoints(logs, false);
            var logPath = Path.Combine(logs, scenario + ".log");
            if (File.Exists(logPath) || Directory.Exists(logPath)) throw new IOException("Player scenario log is write-once: " + logPath);

            SuspendedJobProcess process = null;
            int? observedExitCode = null;
            try
            {
                process = SuspendedJobProcess.Start(executable, folder, "-batchmode -nographics -logFile " + Quote(logPath) + " " + evidenceArguments);
                if (!process.WaitForExit(PlayerExitTimeoutMilliseconds))
                {
                    process.TerminateTreeAndWait(ForcedTerminationTimeoutMilliseconds, scenario);
                    throw new TimeoutException("W24 S2 Player timed out after " + PlayerExitTimeoutMilliseconds + " ms in " + scenario + ". " + SafeTail(logPath));
                }
                process.CompleteTreeAfterPrimaryExit(ForcedTerminationTimeoutMilliseconds, scenario);
                if (!File.Exists(logPath)) throw new FileNotFoundException("The Player did not produce the required scenario log for " + scenario + ".", logPath);
                EnsureNoReparsePoints(logPath, false);
                observedExitCode = process.ExitCode;
                return new PlayerLaunchResult { Scenario = scenario, LogPath = logPath, ExitCode = observedExitCode.Value };
            }
            finally
            {
                if (!observedExitCode.HasValue && process != null)
                {
                    try { observedExitCode = process.ExitCode; }
                    catch { observedExitCode = null; }
                }
                // Job cleanup is bounded and independent, and the evidence snapshot runs even
                // when timeout termination, post-exit tree completion, missing-log validation,
                // exit-code retrieval, or Dispose itself fails.
                try
                {
                    if (process != null) process.Dispose();
                }
                finally
                {
                    RecordLaunchBaseline(scenario, folder, logPath, observedExitCode);
                }
            }
        }

        private static JObject ParseStrictResult(string path)
        {
            EnsureNoReparsePoints(path, false);
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length == 0) throw new InvalidDataException("Player result must not be empty.");
            if (HasUtf8Bom(bytes)) throw new InvalidDataException("Player result must be UTF-8 without BOM.");
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return W24StrictJsonText.ParseObject(text, "W24 S2 Player result");
        }

        private static void AssertCommonResultShape(JObject result)
        {
            RequireExactFields(result, RootFields, "Player result");
            RequireType(result, "schema", JTokenType.String);
            RequireType(result, "passed", JTokenType.Boolean);
            RequireType(result, "exitCode", JTokenType.Integer);
            RequireType(result, "unityVersion", JTokenType.String);
            RequireType(result, "runtimePlatform", JTokenType.String);
            RequireType(result, "graphicsDevice", JTokenType.String);
            RequireType(result, "batchMode", JTokenType.Boolean);
            var modules = RequireType(result, "modules", JTokenType.Array) as JArray;
            RequireType(result, "failure", JTokenType.String);

            Assert.That(ReadString(result, "schema"), Is.EqualTo(W24S2PlayerEvidenceProtocol.ResultSchema));
            Assert.That(ReadString(result, "unityVersion"), Is.Not.Empty);
            Assert.That(ReadString(result, "runtimePlatform"), Is.EqualTo(RuntimePlatform.WindowsPlayer.ToString()));
            Assert.That(ReadString(result, "graphicsDevice"), Is.Not.Empty, "-nographics may report Null, but the frozen string field is mandatory.");
            Assert.That(ReadBoolean(result, "batchMode"), Is.True);
            Assert.That(ReadInteger(result, "exitCode"), Is.InRange(int.MinValue, int.MaxValue));

            foreach (var token in modules)
            {
                Assert.That(token.Type, Is.EqualTo(JTokenType.Object), "Every module row must be an object.");
                var module = (JObject)token;
                RequireExactFields(module, ModuleFields, "module row");
                RequireType(module, "moduleId", JTokenType.String);
                RequireType(module, "passed", JTokenType.Boolean);
                RequireType(module, "detail", JTokenType.String);
                Assert.That(ReadString(module, "moduleId"), Is.Not.Empty);
                Assert.That(ReadString(module, "detail"), Is.Not.Empty);
            }
        }

        private static void AssertSuccessfulResult(JObject result)
        {
            AssertCommonResultShape(result);
            Assert.That(ReadBoolean(result, "passed"), Is.True);
            Assert.That(ReadInteger(result, "exitCode"), Is.EqualTo(W24S2PlayerEvidenceProtocol.SuccessExitCode));
            Assert.That(ReadString(result, "failure"), Is.Empty);
            var modules = (JArray)result["modules"];
            Assert.That(modules.Count, Is.EqualTo(ExpectedModules.Length));
            CollectionAssert.AreEqual(ExpectedModules, modules.Select(value => ReadString((JObject)value, "moduleId")).ToArray());
            foreach (var module in modules.Cast<JObject>()) Assert.That(ReadBoolean(module, "passed"), Is.True, ReadString(module, "moduleId") + ": " + ReadString(module, "detail"));
        }

        private static JToken RequireType(JObject value, string name, JTokenType type)
        {
            var token = value.Property(name, StringComparison.Ordinal) == null ? null : value.Property(name, StringComparison.Ordinal).Value;
            Assert.That(token, Is.Not.Null, "Missing exact field '" + name + "'.");
            Assert.That(token.Type, Is.EqualTo(type), "Field '" + name + "' must have exact JSON type " + type + ".");
            return token;
        }

        private static void RequireExactFields(JObject value, IEnumerable<string> expected, string label)
        {
            var actual = value.Properties().Select(property => property.Name).ToArray();
            CollectionAssert.AreEquivalent(expected.ToArray(), actual, label + " has extra, missing or renamed fields.");
            Assert.That(actual.Length, Is.EqualTo(expected.Count()), label + " field cardinality is not exact.");
        }

        private static string ReadString(JObject value, string name) { return (string)((JValue)RequireType(value, name, JTokenType.String)).Value; }
        private static bool ReadBoolean(JObject value, string name) { return (bool)((JValue)RequireType(value, name, JTokenType.Boolean)).Value; }
        private static long ReadInteger(JObject value, string name) { return Convert.ToInt64(((JValue)RequireType(value, name, JTokenType.Integer)).Value); }

        private static void RecordNUnitEvidence(PlayerLaunchResult launch, JObject result, string resultPath)
        {
            var resultHash = resultPath != null && File.Exists(resultPath) ? Sha256(File.ReadAllBytes(resultPath)) : "NONE";
            var logHash = File.Exists(launch.LogPath) ? Sha256(File.ReadAllBytes(launch.LogPath)) : "NONE";
            var unityVersion = ReadEvidenceString(result, "unityVersion");
            var normalized = result == null ? "NONE" : result.ToString(Formatting.None);
            TestContext.Out.WriteLine("W24S2 scenario=" + launch.Scenario + " osExit=" + launch.ExitCode + " unityVersion=" + unityVersion);
            TestContext.Out.WriteLine("W24S2 scenario=" + launch.Scenario + " resultSha256=" + resultHash + " playerLogSha256=" + logHash);
            TestContext.Out.WriteLine("W24S2 scenario=" + launch.Scenario + " normalizedJson=" + normalized);
            TestContext.Out.WriteLine("W24S2 scenario=" + launch.Scenario + " playerLogSummary=" + Tail(launch.LogPath));
        }

        private static string ReadEvidenceString(JObject result, string name)
        {
            if (result == null) return "NONE";
            var property = result.Property(name, StringComparison.Ordinal);
            if (property == null || property.Value.Type != JTokenType.String) return "INVALID_OR_MISSING";
            return (string)((JValue)property.Value).Value;
        }

        private static void RecordLaunchBaseline(string scenario, string folder, string logPath, int? exitCode)
        {
            var resultPath = FixedResultPath(folder);
            var resultHash = "NONE";
            var resultRawBase64 = "NONE";
            var resultReadFailure = "NONE";
            try
            {
                if (File.Exists(resultPath))
                {
                    EnsureNoReparsePoints(resultPath, false);
                    var resultBytes = File.ReadAllBytes(resultPath);
                    resultHash = Sha256(resultBytes);
                    resultRawBase64 = Convert.ToBase64String(resultBytes);
                }
            }
            catch (Exception exception)
            {
                resultReadFailure = SnapshotFailure(exception);
            }

            var logHash = "NONE";
            var logSummary = "Player log was not created.";
            var logReadFailure = "NONE";
            try
            {
                if (File.Exists(logPath))
                {
                    EnsureNoReparsePoints(logPath, false);
                    var logBytes = File.ReadAllBytes(logPath);
                    logHash = Sha256(logBytes);
                    logSummary = Tail(logBytes);
                }
            }
            catch (Exception exception)
            {
                logReadFailure = SnapshotFailure(exception);
                logSummary = "Player log snapshot failed: " + logReadFailure;
            }

            var osExit = exitCode.HasValue ? exitCode.Value.ToString() : "NONE";
            TestContext.Out.WriteLine("W24S2 launchBaseline scenario=" + scenario + " osExit=" + osExit + " resultSha256=" + resultHash + " playerLogSha256=" + logHash);
            TestContext.Out.WriteLine("W24S2 launchBaseline scenario=" + scenario + " resultRawBase64=" + resultRawBase64);
            TestContext.Out.WriteLine("W24S2 launchBaseline scenario=" + scenario + " playerLogSummary=" + logSummary);
            TestContext.Out.WriteLine("W24S2 launchBaseline scenario=" + scenario + " resultReadFailure=" + resultReadFailure + " playerLogReadFailure=" + logReadFailure);
        }

        private static string SnapshotFailure(Exception exception)
        {
            return exception.GetType().FullName + ": " + (exception.Message ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        }

        private static string NewExternalTemporaryFolderPath()
        {
            var tempRoot = CanonicalDirectory(Path.GetTempPath());
            EnsureNoReparsePoints(tempRoot, false);
            return Path.Combine(tempRoot, TemporaryPrefix + Guid.NewGuid().ToString("N"));
        }

        private static string FixedResultPath(string folder)
        {
            return Path.GetFullPath(Path.Combine(folder, W24S2PlayerEvidenceProtocol.ResultDirectoryName, W24S2PlayerEvidenceProtocol.ResultFileName));
        }

        private static void AssertFreshPublicationDestination(string resultPath, string folder)
        {
            ValidateExternalTemporaryFolder(folder, true, true);
            Assert.That(resultPath, Is.EqualTo(FixedResultPath(folder)).IgnoreCase);
            Assert.That(File.Exists(resultPath) || Directory.Exists(resultPath), Is.False, "Fresh write-once publication requires an absent final destination.");
            AssertNoPendingFiles(resultPath);
        }

        private static void AssertSingleFinalAndNoPending(string resultPath, string folder)
        {
            ValidateExternalTemporaryFolder(folder, true, true);
            Assert.That(File.Exists(resultPath), Is.True);
            EnsureNoReparsePoints(resultPath, false);
            var evidence = Path.GetDirectoryName(resultPath);
            Assert.That(Directory.GetFiles(evidence, W24S2PlayerEvidenceProtocol.ResultFileName, SearchOption.TopDirectoryOnly).Length, Is.EqualTo(1));
            AssertNoPendingFiles(resultPath);
        }

        private static void AssertNoPendingFiles(string resultPath)
        {
            var parent = Path.GetDirectoryName(resultPath);
            if (!Directory.Exists(parent)) return;
            EnsureNoReparsePoints(parent, false);
            Assert.That(Directory.GetFileSystemEntries(parent, W24S2PlayerEvidenceProtocol.ResultFileName + "*.pending", SearchOption.TopDirectoryOnly), Is.Empty, "No current or legacy Player pending file may remain after exit.");
        }

        private static void DeleteOwnedFixedResultForNextLaunch(string resultPath, string folder)
        {
            ValidateExternalTemporaryFolder(folder, true, true);
            if (!string.Equals(Path.GetFullPath(resultPath), FixedResultPath(folder), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to clear a non-fixed S2 Player result: " + resultPath);
            EnsureNoReparsePoints(resultPath, false);
            AssertNoPendingFiles(resultPath);
            File.Delete(resultPath);
            if (File.Exists(resultPath)) throw new IOException("Editor could not clear its owned fixed result before the next launch.");
        }

        private static void ValidateExternalTemporaryFolder(string folder, bool mustExist, bool inspectTree)
        {
            var absolute = CanonicalDirectory(folder);
            var tempRoot = CanonicalDirectory(Path.GetTempPath());
            var leaf = Path.GetFileName(absolute);
            if (!Regex.IsMatch(leaf, "^" + Regex.Escape(TemporaryPrefix) + "[0-9a-f]{32}$", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Unexpected S2 Player temp directory name: " + leaf);
            if (!string.Equals(Path.GetDirectoryName(absolute), tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("S2 Player build root must be a direct child of the system temp root: " + absolute);

            var projectRoot = CanonicalDirectory(Directory.GetParent(Application.dataPath).FullName);
            var workspaceRoot = CanonicalDirectory(Directory.GetParent(projectRoot).FullName);
            if (IsWithinOrEqual(absolute, projectRoot) || IsWithinOrEqual(absolute, workspaceRoot))
                throw new InvalidOperationException("S2 Player build root must be outside the Unity project/workspace: " + absolute);

            EnsureNoReparsePoints(tempRoot, false);
            EnsureNoReparsePoints(absolute, !mustExist);
            if (File.Exists(absolute) && !Directory.Exists(absolute)) throw new IOException("S2 Player build root is unexpectedly a file: " + absolute);
            if (mustExist && !Directory.Exists(absolute)) throw new DirectoryNotFoundException("S2 Player temp directory is missing: " + absolute);
            if (inspectTree && Directory.Exists(absolute)) EnsureTreeHasNoReparsePoints(absolute);
        }

        private static void CleanupExternalTemporaryFolder(string folder)
        {
            ValidateExternalTemporaryFolder(folder, false, Directory.Exists(folder));
            if (!Directory.Exists(folder)) return;
            ValidateExternalTemporaryFolder(folder, true, true);
            Directory.Delete(folder, true);
            if (Directory.Exists(folder)) throw new IOException("S2 external Player directory remained after cleanup: " + folder);
            TestContext.Out.WriteLine("W24S2 externalPlayerFixtureCleaned=" + Path.GetFullPath(folder));
        }

        private static void RestoreBuildSettings(EditorBuildSettingsScene[] originalScenes, string originalSnapshot)
        {
            try
            {
                if (!string.Equals(SnapshotBuildSettings(), originalSnapshot, StringComparison.Ordinal))
                    EditorBuildSettings.scenes = originalScenes.Select(CloneBuildSettingsScene).ToArray();
            }
            finally
            {
                Assert.That(SnapshotBuildSettings(), Is.EqualTo(originalSnapshot), "S2 build unexpectedly mutated Build Settings; restoration was attempted independently in the outer finally.");
            }
        }

        private static void EnsureNoReparsePoints(string path, bool allowMissingTail)
        {
            var absolute = Path.GetFullPath(path);
            var root = Path.GetPathRoot(absolute);
            if (string.IsNullOrEmpty(root)) throw new InvalidOperationException("Path has no rooted volume: " + absolute);
            var current = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point volume root is forbidden: " + current);
            var relative = absolute.Substring(root.Length);
            var segments = relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var missing = false;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                var exists = Directory.Exists(current) || File.Exists(current);
                if (!exists) { missing = true; continue; }
                if (missing) throw new IOException("Existing component follows a missing path component: " + current);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point path component is forbidden: " + current);
            }
            if (!allowMissingTail && missing) throw new DirectoryNotFoundException("Required path does not exist: " + absolute);
        }

        private static void EnsureTreeHasNoReparsePoints(string directory)
        {
            EnsureNoReparsePoints(directory, false);
            foreach (var entry in Directory.GetFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse point in S2 Player temp tree is forbidden: " + entry);
                if ((attributes & FileAttributes.Directory) != 0) EnsureTreeHasNoReparsePoints(entry);
            }
        }

        private static bool IsWithinOrEqual(string candidate, string root)
        {
            if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)) return true;
            return candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string CanonicalDirectory(string value) { return Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
        private static EditorBuildSettingsScene CloneBuildSettingsScene(EditorBuildSettingsScene scene) { return new EditorBuildSettingsScene(scene.path, scene.enabled); }
        private static string SnapshotBuildSettings() { return string.Join("\n", EditorBuildSettings.scenes.Select(scene => scene.path + "|" + (scene.enabled ? "1" : "0")).ToArray()); }
        private static string DescribeBuild(BuildReport report) { return report == null ? "BuildPipeline returned no report." : report.summary.totalErrors + " errors, " + report.summary.totalWarnings + " warnings, result=" + report.summary.result; }
        private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

        private static string Tail(string path)
        {
            if (!File.Exists(path)) return "Player log was not created.";
            return Tail(File.ReadAllBytes(path));
        }

        private static string SafeTail(string path)
        {
            try { return Tail(path); }
            catch (Exception exception) { return "Player log tail unavailable: " + SnapshotFailure(exception); }
        }

        private static string Tail(byte[] bytes)
        {
            var text = new UTF8Encoding(false, false).GetString(bytes ?? new byte[0]);
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            return string.Join(" | ", lines.Skip(Math.Max(0, lines.Length - 20)).Select(line => line.Trim()).Where(line => line.Length > 0).ToArray());
        }

        private static bool HasUtf8Bom(byte[] bytes) { return bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF; }

        private static string Sha256(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++) builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
