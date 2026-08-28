using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Native;

namespace VFXComposer.Broker.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WindowsKillOnCloseJobTests
{
    [TestMethod]
    public void JobIsConfiguredBeforePublicationAndPhysicalCloseIsObserved()
    {
        using var job = WindowsKillOnCloseJob.CreateUniqueConfigured();
        Assert.IsTrue(job.IsActive);
        Assert.AreEqual(WindowsKillOnCloseJobCloseResult.Closed, job.Close());
        Assert.AreEqual(WindowsKillOnCloseJobCloseResult.AlreadyClosed, job.Close());
        Assert.IsFalse(job.IsActive);
    }

    [TestMethod]
    public void TwoConfiguredJobsAreIndependentOwners()
    {
        using var first = WindowsKillOnCloseJob.CreateUniqueConfigured();
        using var second = WindowsKillOnCloseJob.CreateUniqueConfigured();
        Assert.IsTrue(first.IsActive);
        Assert.IsTrue(second.IsActive);
        Assert.AreEqual(WindowsKillOnCloseJobCloseResult.Closed, first.Close());
        Assert.IsFalse(first.IsActive);
        Assert.IsTrue(second.IsActive);
    }

    [TestMethod]
    public void InvalidProcessHandleCannotBeAssignedOrTerminatedAfterClose()
    {
        using var job = WindowsKillOnCloseJob.CreateUniqueConfigured();
        using var invalid = new SafeProcessHandle(IntPtr.Zero, ownsHandle: false);
        Assert.IsFalse(job.TryAssign(null));
        Assert.IsFalse(job.TryAssign(invalid));
        Assert.AreEqual(WindowsKillOnCloseJobCloseResult.Closed, job.Close());
        Assert.IsFalse(job.TryTerminate(31));
    }

    [TestMethod]
    public async Task MandatoryOwnedJobPhysicalCloseTerminatesExactChild()
    {
        await using var child = LaunchProbe();
        Assert.IsTrue(child.HasActiveContainment);
        Assert.AreEqual(
            WindowsKillOnCloseJobCloseResult.Closed,
            child.CloseContainmentForTest());
        Assert.IsTrue(await child.WaitForExitAsync(TimeSpan.FromSeconds(5)));
    }

    [TestMethod]
    public async Task ClosingOneChildJobDoesNotTerminateOtherChild()
    {
        await using var first = LaunchProbe();
        await using var second = LaunchProbe();

        Assert.AreEqual(
            WindowsKillOnCloseJobCloseResult.Closed,
            first.CloseContainmentForTest());
        Assert.IsTrue(await first.WaitForExitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(second.IsExactProcessActive);
        Assert.IsTrue(second.HasActiveContainment);
    }

    [TestMethod]
    public void NativeAbiUsesSafeHandlesKillOnCloseAndExplicitTermination()
    {
        const BindingFlags Native = BindingFlags.Static | BindingFlags.NonPublic;
        var type = typeof(WindowsKillOnCloseJob);
        Assert.IsFalse(type.IsVisible);
        Assert.IsFalse(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any());

        var create = type.GetMethod("CreateJobObjectW", Native);
        var configure = type.GetMethod("SetInformationJobObject", Native);
        var assign = type.GetMethod("AssignProcessToJobObject", Native);
        var terminate = type.GetMethod("TerminateJobObject", Native);
        var close = type.GetMethod("CloseHandle", Native);
        Assert.AreEqual(typeof(SafeFileHandle), create!.ReturnType);
        Assert.AreEqual(typeof(bool), configure!.ReturnType);
        Assert.AreEqual(typeof(bool), assign!.ReturnType);
        Assert.AreEqual(typeof(bool), terminate!.ReturnType);
        Assert.AreEqual(typeof(bool), close!.ReturnType);
        Assert.AreEqual(typeof(IntPtr), close.GetParameters().Single().ParameterType);
        Assert.IsTrue(create.GetCustomAttribute<DllImportAttribute>()!.SetLastError);
        Assert.IsTrue(configure.GetCustomAttribute<DllImportAttribute>()!.SetLastError);
        Assert.IsTrue(assign.GetCustomAttribute<DllImportAttribute>()!.SetLastError);
        Assert.IsTrue(terminate.GetCustomAttribute<DllImportAttribute>()!.SetLastError);
        Assert.IsTrue(close.GetCustomAttribute<DllImportAttribute>()!.SetLastError);
    }

    [TestMethod]
    public void PublicSurfaceMakesNoSandboxOrPrivilegeClaim()
    {
        var names = typeof(WindowsKillOnCloseJob)
            .GetMembers(BindingFlags.Instance | BindingFlags.Static |
                        BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();
        Assert.IsFalse(names.Any(name =>
            name.Contains("Sandbox", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Privilege", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Integrity", StringComparison.OrdinalIgnoreCase)));
    }

    private static UserModeChildProcess LaunchProbe() =>
        UserModeChildProcess.Launch(
            UserModeSessionTestChild.ExpectedExecutablePath,
            UserModeSessionTestChild.Create("no-connect"));
}
