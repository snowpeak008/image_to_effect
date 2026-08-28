using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WindowsProductionNamedPipeHostTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string AlternateServiceSidText = "S-1-5-80-801-802-803-804-805";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";

    [TestMethod]
    public void NativeCreationContractAndSecurityAttributesMatchWindowsAbi()
    {
        var first = WindowsProductionNamedPipeHost.GetNativeCreationContract(bootstrap: true);
        var later = WindowsProductionNamedPipeHost.GetNativeCreationContract(bootstrap: false);

        Assert.AreEqual(0x01080003U, first.OpenMode);
        Assert.AreEqual(0x01000003U, later.OpenMode);
        Assert.AreEqual(0x00000008U, first.PipeMode);
        Assert.AreEqual(2U, first.MaximumInstances);
        Assert.AreEqual(4096U, first.InBufferBytes);
        Assert.AreEqual(4096U, first.OutBufferBytes);
        Assert.IsTrue(first.FirstPipeInstance);
        Assert.IsFalse(later.FirstPipeInstance);
        Assert.IsFalse(first.Inheritable);

        var nativeCreate = typeof(WindowsProductionNamedPipeHost).GetMethod(
            "CreateNamedPipeW",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(nativeCreate);
        Assert.AreEqual(typeof(SafePipeHandle), nativeCreate!.ReturnType);
        Assert.AreEqual(typeof(IntPtr), nativeCreate.GetParameters()[^1].ParameterType);
        Assert.AreEqual(8, nativeCreate.GetParameters().Length);
        var createImport = nativeCreate.GetCustomAttribute<DllImportAttribute>();
        Assert.IsNotNull(createImport);
        Assert.AreEqual("kernel32.dll", createImport!.Value);
        Assert.AreEqual(CharSet.Unicode, createImport.CharSet);
        Assert.AreEqual(CallingConvention.Winapi, createImport.CallingConvention);
        Assert.IsTrue(createImport.SetLastError);
        Assert.IsTrue(createImport.ExactSpelling);

        var nativeRead = typeof(WindowsNamedPipeAclReadback).GetMethod(
            "GetSecurityInfo",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(nativeRead);
        Assert.AreEqual(typeof(uint), nativeRead!.ReturnType);
        Assert.AreEqual(typeof(SafePipeHandle), nativeRead.GetParameters()[0].ParameterType);
        Assert.AreEqual(8, nativeRead.GetParameters().Length);
        var readImport = nativeRead.GetCustomAttribute<DllImportAttribute>();
        Assert.IsNotNull(readImport);
        Assert.AreEqual("advapi32.dll", readImport!.Value);
        Assert.AreEqual(CallingConvention.Winapi, readImport.CallingConvention);
        Assert.IsTrue(readImport.SetLastError);
        Assert.IsTrue(readImport.ExactSpelling);

        var attributesOwner = typeof(WindowsProductionNamedPipeHost).GetNestedType(
            "NativeSecurityAttributes",
            BindingFlags.NonPublic);
        Assert.IsNotNull(attributesOwner);
        var layoutType = attributesOwner!.GetNestedType(
            "SecurityAttributes",
            BindingFlags.NonPublic);
        Assert.IsNotNull(layoutType);
        Assert.AreEqual(LayoutKind.Sequential, layoutType!.StructLayoutAttribute!.Value);
        Assert.AreEqual(0L, Marshal.OffsetOf(layoutType, "Length").ToInt64());
        Assert.AreEqual((long)(IntPtr.Size == 4 ? 4 : 8), Marshal.OffsetOf(
            layoutType,
            "SecurityDescriptor").ToInt64());
        Assert.AreEqual((long)(IntPtr.Size == 4 ? 8 : 16), Marshal.OffsetOf(
            layoutType,
            "InheritHandle").ToInt64());
        Assert.AreEqual(IntPtr.Size == 4 ? 12 : 24, Marshal.SizeOf(layoutType));
        var inheritField = layoutType.GetField(
            "InheritHandle",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(inheritField);
        var boolMarshal = inheritField!.GetCustomAttribute<MarshalAsAttribute>();
        Assert.IsNotNull(boolMarshal);
        Assert.AreEqual(UnmanagedType.Bool, boolMarshal!.Value);

        var namespaceStateType = typeof(WindowsProductionNamedPipeNamespaceState);
        var hostGateLeaseType = namespaceStateType.GetNestedType(
            "HostGateLease",
            BindingFlags.NonPublic);
        Assert.IsNotNull(hostGateLeaseType);
        Assert.IsTrue(hostGateLeaseType!.IsValueType);
        var enterHostGate = namespaceStateType.GetMethod(
            "EnterHostGate",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(enterHostGate);
        Assert.AreEqual(hostGateLeaseType, enterHostGate!.ReturnType);

        // Repeated pattern-based using calls exercise the real value-type
        // lease. A class allocation after Monitor.Enter would be able to
        // strand this gate on an allocation failure; HostGateLease stores the
        // gate before entering it and has no post-acquisition work.
        var gateState = new WindowsProductionNamedPipeNamespaceState();
        for (var index = 0; index < 32; index++)
        {
            using var hostGateLease = gateState.EnterHostGate();
            Assert.IsTrue(gateState.TryPlanNextCreation(out var plan));
            Assert.IsTrue(plan.FirstPipeInstance);
        }
    }

    [TestMethod]
    public void NamespaceStatePlansFirstAndLocksPessimisticallyAfterFirstFailure()
    {
        var state = new WindowsProductionNamedPipeNamespaceState();

        Assert.IsTrue(state.TryPlanNextCreation(out var firstPlan));
        Assert.IsTrue(firstPlan.FirstPipeInstance);
        Assert.AreEqual(0, state.LiveVerifiedCandidateLeases);
        Assert.IsFalse(state.IsPermanentlyRejected);
        Assert.IsFalse(state.IsFirstAttemptPending);
        Assert.IsFalse(state.IsHostDisposed);

        Assert.IsTrue(state.TryLatchFirstPhysicalHandle());
        Assert.IsTrue(state.IsPermanentlyRejected);
        Assert.IsTrue(state.IsFirstAttemptPending);

        // This is the host finally transition after any post-handle first-path
        // failure, including a candidate-owner allocation failure.
        state.RejectFirstAttemptPermanently();
        Assert.IsTrue(state.IsPermanentlyRejected);
        Assert.IsFalse(state.IsFirstAttemptPending);
        Assert.IsFalse(state.TryPlanNextCreation(out _));
    }

    [TestMethod]
    public void PreparedOwnerAbandonmentLeavesFirstPessimisticAndLaterCountUnchanged()
    {
        var inputs = CreateBoundInputs();

        // Prepare allocates the real production owner but makes no state
        // mutation. Abandoning it deterministically covers the pre-commit
        // allocation/failure seam without a delegate, native factory, or test
        // hook: a genuine allocation failure cannot reach commit either.
        var rejectedFirstState = new WindowsProductionNamedPipeNamespaceState();
        using var rejectedFirstHandle = CreateSyntheticHandle(0x1011);
        WindowsProductionNamedPipeCandidate? uncommittedFirst;
        using (rejectedFirstState.EnterHostGate())
        {
            Assert.IsTrue(rejectedFirstState.TryPlanNextCreation(out var firstPlan));
            Assert.IsTrue(firstPlan.FirstPipeInstance);
            Assert.IsTrue(rejectedFirstState.TryLatchFirstPhysicalHandle());
            Assert.IsTrue(rejectedFirstState.TryPrepareCandidateLease(
                firstPlan,
                rejectedFirstHandle,
                CreateSyntheticReadback(rejectedFirstHandle, inputs),
                out uncommittedFirst));
            Assert.IsNotNull(uncommittedFirst);
            Assert.AreEqual(0, rejectedFirstState.LiveVerifiedCandidateLeases);
            Assert.IsTrue(rejectedFirstState.IsPermanentlyRejected);
            Assert.IsTrue(rejectedFirstState.IsFirstAttemptPending);
        }

        uncommittedFirst!.Dispose();
        Assert.IsTrue(rejectedFirstHandle.IsClosed);
        Assert.AreEqual(0, rejectedFirstState.LiveVerifiedCandidateLeases);
        Assert.IsTrue(rejectedFirstState.IsPermanentlyRejected);
        Assert.IsFalse(rejectedFirstState.IsFirstAttemptPending);
        Assert.IsFalse(rejectedFirstState.TryPlanNextCreation(out _));

        var laterState = new WindowsProductionNamedPipeNamespaceState();
        using var firstHandle = CreateSyntheticHandle(0x1021);
        var first = CommitSyntheticCandidate(laterState, inputs, firstHandle);
        try
        {
            using var laterHandle = CreateSyntheticHandle(0x1031);
            WindowsProductionNamedPipeCandidate? uncommittedLater;
            using (laterState.EnterHostGate())
            {
                Assert.IsTrue(laterState.TryPlanNextCreation(out var laterPlan));
                Assert.IsFalse(laterPlan.FirstPipeInstance);
                Assert.IsTrue(laterState.TryPrepareCandidateLease(
                    laterPlan,
                    laterHandle,
                    CreateSyntheticReadback(laterHandle, inputs),
                    out uncommittedLater));
                Assert.IsNotNull(uncommittedLater);
                Assert.AreEqual(1, laterState.LiveVerifiedCandidateLeases);
            }

            uncommittedLater!.Dispose();
            Assert.IsTrue(laterHandle.IsClosed);
            Assert.AreEqual(1, laterState.LiveVerifiedCandidateLeases);
            Assert.IsTrue(laterState.TryPlanNextCreation(out var nextLaterPlan));
            Assert.IsFalse(nextLaterPlan.FirstPipeInstance);
        }
        finally
        {
            first.Dispose();
        }

        Assert.IsTrue(firstHandle.IsClosed);
        Assert.AreEqual(0, laterState.LiveVerifiedCandidateLeases);
        Assert.IsTrue(laterState.TryPlanNextCreation(out var reacquiredFirstPlan));
        Assert.IsTrue(reacquiredFirstPlan.FirstPipeInstance);
    }

    [TestMethod]
    public void NamespaceStateCommitsFirstCandidateWithImmutableProofMetadata()
    {
        var inputs = CreateBoundInputs();
        var state = new WindowsProductionNamedPipeNamespaceState();
        using var handle = CreateSyntheticHandle(0x1041);
        var candidate = CommitSyntheticCandidate(state, inputs, handle);
        try
        {
            Assert.IsTrue(candidate.DurableProfileDigest.FixedTimeEquals(
                inputs.DurableProfile.ProfileDigest));
            Assert.IsTrue(candidate.ServiceSid.FixedEquals(inputs.DurableProfile.ServiceSid));
            Assert.IsTrue(candidate.UserSid.FixedEquals(inputs.ProvisioningIntent.UserSid));
            Assert.IsFalse(state.IsPermanentlyRejected);
            Assert.IsFalse(state.IsFirstAttemptPending);
            Assert.AreEqual(1, state.LiveVerifiedCandidateLeases);
        }
        finally
        {
            candidate.Dispose();
        }

        Assert.IsTrue(handle.IsClosed);
        Assert.AreEqual(0, state.LiveVerifiedCandidateLeases);
    }

    [TestMethod]
    public void CandidateDoesNotExposeHandleAndExplicitDisposalIsExactOnce()
    {
        const BindingFlags CandidateFlags =
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.Instance |
            BindingFlags.DeclaredOnly;
        var candidateType = typeof(WindowsProductionNamedPipeCandidate);
        Assert.IsNull(candidateType.GetProperty("Handle", CandidateFlags));
        Assert.IsFalse(candidateType.GetProperties(CandidateFlags).Any(property =>
            property.PropertyType == typeof(SafePipeHandle) ||
            property.PropertyType == typeof(IntPtr) ||
            property.PropertyType == typeof(WindowsNamedPipeAclReadback)));
        Assert.IsFalse(candidateType.GetMethods(CandidateFlags).Any(method =>
            method.ReturnType == typeof(SafePipeHandle) ||
            method.ReturnType == typeof(IntPtr)));
        Assert.IsNotNull(candidateType.GetMethod("Finalize", CandidateFlags));
        Assert.IsFalse(candidateType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any());

        var lifetimeOwnerType = typeof(PipeInstanceLifetimeOwner);
        Assert.IsFalse(lifetimeOwnerType.IsVisible);
        Assert.IsTrue(lifetimeOwnerType.IsSealed);
        Assert.IsFalse(lifetimeOwnerType.GetProperties(CandidateFlags).Any(property =>
            property.PropertyType == typeof(SafeHandle) ||
            property.PropertyType == typeof(SafePipeHandle) ||
            property.PropertyType == typeof(IntPtr)));

        var inputs = CreateBoundInputs();
        var barrierState = new WindowsProductionNamedPipeNamespaceState();
        using var barrierHandle = new BarrierSafePipeHandle(new IntPtr(0x1051));
        var lifetimeOwner = CommitSyntheticLifetimeOwner(barrierState, barrierHandle);
        Task? winningDispose = null;
        Thread? duplicateThread = null;
        Thread? plannerThread = null;
        Exception? duplicateFailure = null;
        Exception? plannerFailure = null;
        var plannerSucceeded = false;
        WindowsProductionNamedPipeNamespaceState.NamespaceCreationPlan plannerPlan = default;
        using var duplicateEnteredDispose = new ManualResetEventSlim(false);
        using var duplicateReturned = new ManualResetEventSlim(false);
        using var plannerEntered = new ManualResetEventSlim(false);
        using var plannerReturned = new ManualResetEventSlim(false);
        try
        {
            winningDispose = Task.Run(lifetimeOwner.Dispose);
            Assert.IsTrue(barrierHandle.WaitForReleaseHandle(TimeSpan.FromSeconds(5)));

            // ReleaseHandle is now physically blocked while the winning owner
            // holds the production namespace gate. A real plan call must not
            // observe either the old count or a new FIRST plan until close and
            // the matching transition have completed.
            plannerThread = new Thread(() =>
            {
                try
                {
                    plannerEntered.Set();
                    plannerSucceeded = barrierState.TryPlanNextCreation(out plannerPlan);
                }
                catch (Exception exception)
                {
                    plannerFailure = exception;
                }
                finally
                {
                    plannerReturned.Set();
                }
            })
            {
                IsBackground = true,
            };
            plannerThread.Start();
            Assert.IsTrue(plannerEntered.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(SpinWait.SpinUntil(
                () => (plannerThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(5)));
            Assert.IsFalse(plannerReturned.Wait(TimeSpan.FromMilliseconds(100)));

            duplicateThread = new Thread(() =>
            {
                try
                {
                    duplicateEnteredDispose.Set();
                    lifetimeOwner.Dispose();
                }
                catch (Exception exception)
                {
                    duplicateFailure = exception;
                }
                finally
                {
                    duplicateReturned.Set();
                }
            })
            {
                IsBackground = true,
            };
            duplicateThread.Start();
            Assert.IsTrue(duplicateEnteredDispose.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(SpinWait.SpinUntil(
                () => (duplicateThread.ThreadState & ThreadState.WaitSleepJoin) != 0,
                TimeSpan.FromSeconds(5)));
            Assert.IsFalse(duplicateReturned.Wait(TimeSpan.FromMilliseconds(100)));

            barrierHandle.AllowRelease();
            Assert.IsTrue(winningDispose.Wait(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(plannerThread.Join(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(duplicateThread.Join(TimeSpan.FromSeconds(5)));
            Assert.IsNull(duplicateFailure);
            Assert.IsNull(plannerFailure);
            Assert.IsTrue(barrierHandle.ReleaseHandleReturned);
            Assert.IsTrue(barrierHandle.IsClosed);
            Assert.IsTrue(plannerSucceeded);
            Assert.IsTrue(plannerPlan.FirstPipeInstance);
            Assert.AreEqual(0, barrierState.LiveVerifiedCandidateLeases);
        }
        finally
        {
            barrierHandle.AllowRelease();
            if (winningDispose is not null)
            {
                _ = winningDispose.Wait(TimeSpan.FromSeconds(5));
            }

            if (duplicateThread is not null)
            {
                _ = duplicateThread.Join(TimeSpan.FromSeconds(5));
            }

            if (plannerThread is not null)
            {
                _ = plannerThread.Join(TimeSpan.FromSeconds(5));
            }

            lifetimeOwner.Dispose();
        }

        // The verified candidate remains exact-SafePipeHandle-only and
        // delegates its own duplicate disposal to the same serialized owner.
        var candidateState = new WindowsProductionNamedPipeNamespaceState();
        using var candidateHandle = CreateSyntheticHandle(0x1052);
        var candidate = CommitSyntheticCandidate(candidateState, inputs, candidateHandle);
        var disposeTasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(candidate.Dispose))
            .ToArray();
        Task.WaitAll(disposeTasks);

        Assert.IsTrue(candidateHandle.IsClosed);
        Assert.AreEqual(0, candidateState.LiveVerifiedCandidateLeases);
        Assert.IsTrue(candidateState.TryPlanNextCreation(out var nextPlan));
        Assert.IsTrue(nextPlan.FirstPipeInstance);
    }

    [TestMethod]
    public void CandidateFinalizerAbandonmentClosesAndReleasesLastLeaseToFirst()
    {
        var inputs = CreateBoundInputs();
        var state = new WindowsProductionNamedPipeNamespaceState();
        using var handle = CreateSyntheticHandle(0x1061);
        var weakCandidate = CreateAbandonedCommittedCandidate(state, inputs, handle);

        for (var attempt = 0; weakCandidate.IsAlive && attempt < 12; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.IsFalse(weakCandidate.IsAlive);
        Assert.IsTrue(handle.IsClosed);
        Assert.AreEqual(0, state.LiveVerifiedCandidateLeases);
        Assert.IsTrue(state.TryPlanNextCreation(out var nextPlan));
        Assert.IsTrue(nextPlan.FirstPipeInstance);
    }

    [TestMethod]
    public void NamespaceStateRejectsMismatchedReadbackAndSerializesCandidateAndHostDisposal()
    {
        var inputs = CreateBoundInputs();
        var state = new WindowsProductionNamedPipeNamespaceState();
        using var firstHandle = CreateSyntheticHandle(0x1071);
        var first = CommitSyntheticCandidate(state, inputs, firstHandle);
        WindowsProductionNamedPipeCandidate? laterA = null;
        WindowsProductionNamedPipeCandidate? laterB = null;
        try
        {
            using var verifiedHandle = CreateSyntheticHandle(0x1079);
            using var mismatchedHandle = CreateSyntheticHandle(0x107A);
            using (state.EnterHostGate())
            {
                Assert.IsTrue(state.TryPlanNextCreation(out var laterPlan));
                Assert.IsFalse(laterPlan.FirstPipeInstance);
                Assert.IsFalse(state.TryPrepareCandidateLease(
                    laterPlan,
                    mismatchedHandle,
                    CreateSyntheticReadback(verifiedHandle, inputs),
                    out var rejectedCandidate));
                Assert.IsNull(rejectedCandidate);
            }

            Assert.IsFalse(mismatchedHandle.IsClosed);
            Assert.AreEqual(1, state.LiveVerifiedCandidateLeases);

            using var laterAHandle = CreateSyntheticHandle(0x1081);
            using var laterBHandle = CreateSyntheticHandle(0x1091);
            laterA = CommitSyntheticCandidate(state, inputs, laterAHandle);
            laterB = CommitSyntheticCandidate(state, inputs, laterBHandle);
            Assert.AreEqual(3, state.LiveVerifiedCandidateLeases);
            Assert.IsTrue(laterA.DurableProfileDigest.FixedTimeEquals(
                inputs.DurableProfile.ProfileDigest));
            Assert.IsTrue(laterB.ServiceSid.FixedEquals(inputs.DurableProfile.ServiceSid));

            var candidates = new[] { first, laterA, laterB };
            var tasks = Enumerable.Range(0, 18)
                .Select(index => Task.Run(candidates[index % candidates.Length].Dispose))
                .Append(Task.Run(() =>
                {
                    for (var index = 0; index < 18; index++)
                    {
                        state.DisposeHost();
                    }
                }))
                .ToArray();
            Task.WaitAll(tasks);

            Assert.IsTrue(firstHandle.IsClosed);
            Assert.IsTrue(laterAHandle.IsClosed);
            Assert.IsTrue(laterBHandle.IsClosed);
            Assert.AreEqual(0, state.LiveVerifiedCandidateLeases);
            Assert.IsTrue(state.IsHostDisposed);
            Assert.IsFalse(state.TryPlanNextCreation(out _));
        }
        finally
        {
            laterB?.Dispose();
            laterA?.Dispose();
            first.Dispose();
        }
    }

    [TestMethod]
    public void HostBindingSurfaceAndOrdinaryTokenAttemptRemainFailClosed()
    {
        var inputs = CreateBoundInputs();
        using var host = new WindowsProductionNamedPipeHost(
            inputs.DurableProfile,
            inputs.ProvisioningIntent);
        Assert.IsTrue(host.DurableProfileDigest.FixedTimeEquals(inputs.DurableProfile.ProfileDigest));
        Assert.IsTrue(host.ServiceSid.FixedEquals(inputs.DurableProfile.ServiceSid));
        Assert.IsTrue(host.UserSid.FixedEquals(inputs.ProvisioningIntent.UserSid));

        var durableProfile = new DurableProductionProfile(
            "p1-named-pipe-profile",
            WindowsSid.ParseService(ServiceSidText));
        var mismatch = CreateBoundInputs(serviceSidText: AlternateServiceSidText);
        Assert.ThrowsExactly<ArgumentException>(() => new WindowsProductionNamedPipeHost(
            durableProfile,
            mismatch.ProvisioningIntent));

        Assert.IsFalse(typeof(WindowsProductionNamedPipeHost).IsVisible);
        Assert.IsFalse(typeof(WindowsProductionNamedPipeCandidate).IsVisible);
        Assert.IsFalse(typeof(WindowsProductionNamedPipeHost)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(method => method.Name.Contains("Accept", StringComparison.Ordinal) ||
                           method.Name.Contains("Connect", StringComparison.Ordinal)));
        Assert.IsFalse(typeof(WindowsProductionNamedPipeCandidate)
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(method => method.Name.Contains("Accept", StringComparison.Ordinal) ||
                           method.Name.Contains("Connect", StringComparison.Ordinal)));

        var created = host.TryCreateDormantCandidate(CancellationToken.None, out var candidate);
        Assert.AreEqual(created, candidate is not null);
        candidate?.Dispose();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var creators = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() =>
            {
                var cancelledCreate = host.TryCreateDormantCandidate(
                    cancellation.Token,
                    out var cancelledCandidate);
                if (cancelledCreate || cancelledCandidate is not null)
                {
                    throw new InvalidOperationException("Cancelled creation produced a candidate.");
                }
            }))
            .Append(Task.Run(() =>
            {
                for (var index = 0; index < 16; index++)
                {
                    host.Dispose();
                }
            }))
            .ToArray();
        Task.WaitAll(creators);

        host.Dispose();
        Assert.IsFalse(host.TryCreateDormantCandidate(CancellationToken.None, out var afterDispose));
        Assert.IsNull(afterDispose);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAbandonedCommittedCandidate(
        WindowsProductionNamedPipeNamespaceState state,
        BoundInputs inputs,
        SafePipeHandle handle)
    {
        var candidate = CommitSyntheticCandidate(state, inputs, handle);
        return new WeakReference(candidate);
    }

    private static WindowsProductionNamedPipeCandidate CommitSyntheticCandidate(
        WindowsProductionNamedPipeNamespaceState state,
        BoundInputs inputs,
        SafePipeHandle handle)
    {
        using var hostGate = state.EnterHostGate();
        if (!state.TryPlanNextCreation(out var creationPlan))
        {
            throw new InvalidOperationException("Expected a namespace creation plan.");
        }

        if (creationPlan.FirstPipeInstance && !state.TryLatchFirstPhysicalHandle())
        {
            throw new InvalidOperationException("Expected a pessimistic first-handle latch.");
        }

        if (!state.TryPrepareCandidateLease(
                creationPlan,
                handle,
                CreateSyntheticReadback(handle, inputs),
                out var candidate) ||
            candidate is null)
        {
            throw new InvalidOperationException("Expected a prepared candidate owner.");
        }

        if (!state.TryCommitPreparedCandidateLease(candidate))
        {
            candidate.Dispose();
            throw new InvalidOperationException("Expected a nonthrowing prepared candidate commit.");
        }

        return candidate;
    }

    private static PipeInstanceLifetimeOwner CommitSyntheticLifetimeOwner(
        WindowsProductionNamedPipeNamespaceState state,
        SafeHandle handle)
    {
        using var hostGate = state.EnterHostGate();
        if (!state.TryPlanNextCreation(out var creationPlan))
        {
            throw new InvalidOperationException("Expected a namespace creation plan.");
        }

        if (creationPlan.FirstPipeInstance && !state.TryLatchFirstPhysicalHandle())
        {
            throw new InvalidOperationException("Expected a pessimistic first-handle latch.");
        }

        var owner = new PipeInstanceLifetimeOwner(
            handle,
            state,
            creationPlan.FirstPipeInstance,
            checked(state.LiveVerifiedCandidateLeases + 1));
        if (!state.TryCommitPreparedLifetimeOwner(owner))
        {
            owner.Dispose();
            throw new InvalidOperationException("Expected a committed lifetime owner.");
        }

        return owner;
    }

    private static SafePipeHandle CreateSyntheticHandle(long rawHandle) =>
        new(new IntPtr(rawHandle), ownsHandle: false);

    // SafePipeHandle is sealed in .NET 8, so a literal subclass is impossible.
    // This test-only safe-pipe lifetime handle derives from its runtime base
    // and exercises the same SafeHandle.ReleaseHandle close protocol without
    // adding a production callback, factory seam, or test hook.
    private sealed class BarrierSafePipeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private readonly ManualResetEventSlim _releaseEntered = new(false);
        private readonly ManualResetEventSlim _allowRelease = new(false);
        private readonly ManualResetEventSlim _releaseReturned = new(false);

        internal BarrierSafePipeHandle(IntPtr rawHandle)
            : base(ownsHandle: true)
        {
            SetHandle(rawHandle);
        }

        internal bool ReleaseHandleReturned => _releaseReturned.IsSet;

        internal bool WaitForReleaseHandle(TimeSpan timeout) => _releaseEntered.Wait(timeout);

        internal void AllowRelease() => _allowRelease.Set();

        protected override bool ReleaseHandle()
        {
            _releaseEntered.Set();
            _allowRelease.Wait();
            _releaseReturned.Set();
            return true;
        }
    }

    private static WindowsNamedPipeAclReadback CreateSyntheticReadback(
        SafePipeHandle handle,
        BoundInputs inputs)
    {
        var constructor = typeof(WindowsNamedPipeAclReadback)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        var facts = new WindowsNamedPipeAclReadback.DescriptorFacts(
            0x9004,
            20,
            32,
            52,
            32,
            84,
            64,
            92,
            116,
            148);
        return (WindowsNamedPipeAclReadback)constructor.Invoke(new object?[]
        {
            handle,
            inputs.DurableProfile.ProfileDigest,
            inputs.DurableProfile.ServiceSid,
            inputs.ProvisioningIntent.UserSid,
            facts,
        });
    }

    private static BoundInputs CreateBoundInputs(
        string serviceSidText = ServiceSidText,
        string userSidText = UserSidText)
    {
        var serviceSid = WindowsSid.ParseService(serviceSidText);
        var userSid = WindowsSid.ParseUser(userSidText);
        var pipeName = string.Concat("vfxcomposer-p1-", Guid.NewGuid().ToString("N"));
        var profile = new ProductionTrustProfile(
            pipeName,
            "broker-production",
            17,
            serviceSid,
            userSid,
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash>
                {
                    TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "desktop-image"),
                },
                [PeerRoles.Worker] = new HashSet<TypedHash>
                {
                    TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "worker-image"),
                },
            });
        var intendedService = new WindowsServiceProcessIdentity(
            serviceSid,
            TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "broker-service-image"),
            processId: 81,
            processEpoch: "winproc-81-0000000000000054",
            generation: 17,
            sessionId: "service-17-1");
        return new BoundInputs(
            new DurableProductionProfile("p1-named-pipe-profile", serviceSid),
            new WindowsNamedPipeAclProvisioningIntent(profile, intendedService));
    }

    private sealed record BoundInputs(
        DurableProductionProfile DurableProfile,
        WindowsNamedPipeAclProvisioningIntent ProvisioningIntent);
}
