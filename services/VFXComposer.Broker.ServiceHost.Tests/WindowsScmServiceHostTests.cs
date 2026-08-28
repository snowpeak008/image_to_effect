using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Broker.ServiceHost;

namespace VFXComposer.Broker.ServiceHost.Tests;

[TestClass]
public sealed class WindowsScmServiceHostTests
{
    [TestMethod]
    public void UnavailablePolicyReportsPendingThenStoppedWithoutRunning()
    {
        var native = new RecordingScmApi { InvokeServiceMainDuringDispatch = true };
        using var host = new WindowsScmServiceHost(native);

        var exitCode = host.Run();

        Assert.AreEqual(ServiceHostDiagnostics.FailClosedExitCode, exitCode);
        Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
        CollectionAssert.AreEqual(
            new uint[]
            {
                (uint)ServiceLifecycleState.StartPending,
                (uint)ServiceLifecycleState.StopPending,
                (uint)ServiceLifecycleState.Stopped,
            },
            native.Statuses.Select(status => status.CurrentState).ToArray());
        Assert.IsFalse(native.Statuses.Any(status => status.CurrentState == (uint)ServiceLifecycleState.Running));
        Assert.AreEqual(ServiceStatusSnapshot.PendingCheckpoint, native.Statuses[0].Checkpoint);
        Assert.AreEqual(ServiceStatusSnapshot.PendingWaitHintMilliseconds, native.Statuses[0].WaitHint);
        Assert.AreEqual(ServiceStatusSnapshot.PendingCheckpoint, native.Statuses[1].Checkpoint);
        Assert.AreEqual(ServiceStatusSnapshot.PendingWaitHintMilliseconds, native.Statuses[1].WaitHint);
        Assert.AreEqual(0U, native.Statuses[2].Checkpoint);
        Assert.AreEqual(0U, native.Statuses[2].WaitHint);
        Assert.AreEqual(ServiceHostDiagnostics.ErrorServiceSpecificError, native.Statuses[2].Win32ExitCode);
        Assert.AreEqual(ServiceHostDiagnostics.ServiceSpecificExitCode, native.Statuses[2].ServiceSpecificExitCode);
    }

    [TestMethod]
    public void CallbackDelegatesRemainRootedThroughDispatchAndLateControlsAreSafe()
    {
        var native = new RecordingScmApi
        {
            InvokeServiceMainDuringDispatch = true,
            ForceCollectionBeforeServiceMain = true,
        };
        using var host = new WindowsScmServiceHost(native);

        _ = host.Run();

        Assert.IsNotNull(native.ServiceMain);
        Assert.IsNotNull(native.ControlHandler);
        Assert.AreEqual(0U, native.InvokeControl(1));
        Assert.AreEqual(ServiceHostDiagnostics.ErrorCallNotImplemented, native.InvokeControl(128));
        Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
    }

    [TestMethod]
    public void DirectDispatcherFailureReturnsTheFixedCodeWithoutInvokingTheService()
    {
        var native = new RecordingScmApi { DispatcherResult = false };
        using var host = new WindowsScmServiceHost(native);

        Assert.AreEqual(ServiceHostDiagnostics.FailClosedExitCode, host.Run());
        Assert.IsNotNull(native.ServiceMain);
        Assert.IsNull(native.ControlHandler);
        Assert.AreEqual(0, native.Statuses.Count);
        Assert.IsTrue(native.LastErrorCallCount > 0);
        Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
    }

    [TestMethod]
    public void DisposeWinsOverALateCallbackWithoutStartingTheService()
    {
        var native = new RecordingScmApi();
        var host = new WindowsScmServiceHost(native);
        try
        {
            Assert.AreEqual(ServiceHostDiagnostics.FailClosedExitCode, host.Run());
            host.Dispose();

            native.InvokeServiceMain();

            CollectionAssert.AreEqual(
                new uint[] { (uint)ServiceLifecycleState.Stopped },
                native.Statuses.Select(status => status.CurrentState).ToArray());
            Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
        }
        finally
        {
            host.Dispose();
        }
    }

    [TestMethod]
    public void ReentrantStopAndDisposeAreLinearized()
    {
        var native = new RecordingScmApi { InvokeServiceMainDuringDispatch = true };
        WindowsScmServiceHost? host = null;
        native.OnStatus = status =>
        {
            if (status.CurrentState == (uint)ServiceLifecycleState.StartPending)
            {
                Assert.AreEqual(0U, native.InvokeControl(1));
                host!.Dispose();
            }
        };

        host = new WindowsScmServiceHost(native);
        using (host)
        {
            _ = host.Run();
            Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
        }

        CollectionAssert.AreEqual(
            new uint[]
            {
                (uint)ServiceLifecycleState.StartPending,
                (uint)ServiceLifecycleState.StopPending,
                (uint)ServiceLifecycleState.Stopped,
            },
            native.Statuses.Select(status => status.CurrentState).ToArray());
    }

    [TestMethod]
    public async Task ConcurrentDisposeAndCallbackFailureLeaveTheHostStopped()
    {
        var native = new RecordingScmApi
        {
            InvokeServiceMainDuringDispatch = true,
            ThrowWhenSettingStatus = true,
        };
        var host = new WindowsScmServiceHost(native);
        try
        {
            var run = Task.Run(host.Run);
            var disposals = Enumerable.Range(0, 32).Select(_ => Task.Run(host.Dispose));

            await Task.WhenAll(disposals.Append(run));

            Assert.AreEqual(ServiceHostDiagnostics.FailClosedExitCode, run.Result);
            Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
        }
        finally
        {
            host.Dispose();
        }
    }

    [TestMethod]
    public void RegistrationFailureIsContainedAndDoesNotReachRunning()
    {
        var native = new RecordingScmApi
        {
            InvokeServiceMainDuringDispatch = true,
            RegistrationHandle = 0,
        };
        using var host = new WindowsScmServiceHost(native);

        Assert.AreEqual(ServiceHostDiagnostics.FailClosedExitCode, host.Run());
        Assert.AreEqual(ServiceLifecycleState.Stopped, host.State);
        Assert.AreEqual(0, native.Statuses.Count);
        Assert.IsTrue(native.LastErrorCallCount > 0);
    }

    [TestMethod]
    public void InteropLayoutsAndImportsMatchTheWindowsAbi()
    {
        Assert.AreEqual(28, Marshal.SizeOf<NativeServiceStatus>());
        Assert.AreEqual(0, Marshal.OffsetOf<NativeServiceStatus>(nameof(NativeServiceStatus.ServiceType)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<NativeServiceStatus>(nameof(NativeServiceStatus.CurrentState)).ToInt32());
        Assert.AreEqual(24, Marshal.OffsetOf<NativeServiceStatus>(nameof(NativeServiceStatus.WaitHint)).ToInt32());
        Assert.AreEqual(IntPtr.Size * 2, Marshal.SizeOf<ServiceTableEntry>());

        var imports = typeof(NativeScmMethods)
            .GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
            .Select(method => new
            {
                Method = method.Name,
                Import = method.GetCustomAttributes(typeof(DllImportAttribute), inherit: false)
                    .Cast<DllImportAttribute>()
                    .SingleOrDefault(),
            })
            .Where(item => item.Import is not null)
            .Select(item => $"{item.Import!.Value}|{item.Import.EntryPoint}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "advapi32.dll|RegisterServiceCtrlHandlerExW",
                "advapi32.dll|SetServiceStatus",
                "advapi32.dll|StartServiceCtrlDispatcherW",
                "kernel32.dll|GetLastError",
            },
            imports);
    }

    private sealed class RecordingScmApi : IWindowsScmApi
    {
        private readonly List<NativeServiceStatus> _statuses = new();

        internal bool InvokeServiceMainDuringDispatch { get; init; }

        internal bool ForceCollectionBeforeServiceMain { get; init; }

        internal bool ThrowWhenSettingStatus { get; init; }

        internal bool DispatcherResult { get; init; } = true;

        internal nint RegistrationHandle { get; init; } = 7;

        internal ServiceMainCallback? ServiceMain { get; private set; }

        internal ServiceControlHandlerExCallback? ControlHandler { get; private set; }

        internal Action<NativeServiceStatus>? OnStatus { get; set; }

        internal int LastErrorCallCount { get; private set; }

        internal IReadOnlyList<NativeServiceStatus> Statuses => _statuses;

        public bool StartServiceCtrlDispatcher(ServiceTableEntry[] dispatchTable)
        {
            ServiceMain = dispatchTable[0].ServiceMain;
            Assert.IsNotNull(ServiceMain);
            Assert.AreEqual(WindowsScmServiceHost.FixedServiceName, dispatchTable[0].ServiceName);
            Assert.IsNull(dispatchTable[1].ServiceName);
            Assert.IsNull(dispatchTable[1].ServiceMain);

            if (InvokeServiceMainDuringDispatch)
            {
                if (ForceCollectionBeforeServiceMain)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                InvokeServiceMain();
            }

            return DispatcherResult;
        }

        public nint RegisterServiceCtrlHandlerEx(string serviceName, ServiceControlHandlerExCallback controlHandler)
        {
            Assert.AreEqual(WindowsScmServiceHost.FixedServiceName, serviceName);
            ControlHandler = controlHandler;
            return RegistrationHandle;
        }

        public bool SetServiceStatus(nint serviceStatusHandle, NativeServiceStatus status)
        {
            Assert.AreEqual(RegistrationHandle, serviceStatusHandle);
            _statuses.Add(status);
            OnStatus?.Invoke(status);
            if (ThrowWhenSettingStatus)
            {
                throw new InvalidOperationException("test native failure");
            }

            return true;
        }

        public uint GetLastError()
        {
            LastErrorCallCount++;
            return 1063;
        }

        internal uint InvokeControl(uint control)
        {
            Assert.IsNotNull(ControlHandler);
            return ControlHandler!(control, 0, 0, 0);
        }

        internal void InvokeServiceMain()
        {
            Assert.IsNotNull(ServiceMain);
            ServiceMain!(0, 0);
        }
    }
}
