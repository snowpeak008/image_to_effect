using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Ipc;

/// <summary>
/// Windows-only, dormant factory for one unconnected production pipe instance.
/// This type intentionally contains no accept, connect, session, listener or
/// Running surface. A candidate exists only after same-handle ACL readback.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProductionNamedPipeHost : IDisposable
{
    private const uint PipeAccessDuplex = 0x00000003;
    private const uint AccessSystemSecurity = 0x01000000;
    private const uint FileFlagFirstPipeInstance = 0x00080000;
    private const uint PipeRejectRemoteClients = 0x00000008;
    private const uint MaximumInstances = 2;
    private const uint BufferBytes = 4096;
    private const string PipePathPrefix = "\\\\.\\pipe\\";

    private readonly DurableProductionProfile _durableProfile;
    private readonly WindowsNamedPipeAclProvisioningIntent _provisioningIntent;
    private readonly WindowsProductionNamedPipeNamespaceState _namespaceState = new();

    internal WindowsProductionNamedPipeHost(
        DurableProductionProfile durableProfile,
        WindowsNamedPipeAclProvisioningIntent provisioningIntent)
    {
        _durableProfile = durableProfile ?? throw new ArgumentNullException(nameof(durableProfile));
        _provisioningIntent = provisioningIntent ?? throw new ArgumentNullException(nameof(provisioningIntent));
        if (!OperatingSystem.IsWindows() ||
            !HasExactBinding(_durableProfile, _provisioningIntent))
        {
            throw new ArgumentException("The named-pipe host is not bound to one durable production profile.");
        }
    }

    internal TypedHash DurableProfileDigest => _durableProfile.ProfileDigest;

    internal WindowsSid ServiceSid => _durableProfile.ServiceSid;

    internal WindowsSid UserSid => _provisioningIntent.UserSid;

    /// <summary>
    /// Creates one fresh unconnected native instance, performs exact same-handle
    /// readback, and returns it only after all checks succeed. Cancellation and
    /// every native/descriptor failure close the just-created handle.
    /// </summary>
    internal bool TryCreateDormantCandidate(
        CancellationToken cancellationToken,
        out WindowsProductionNamedPipeCandidate? candidate)
    {
        candidate = null;
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // The namespace state gate covers creation and candidate teardown. In
        // particular, a final candidate cannot close between selection of a
        // later-instance contract and creation of that later native instance.
        using var hostGate = _namespaceState.EnterHostGate();
        if (_namespaceState.IsHostDisposed ||
            cancellationToken.IsCancellationRequested ||
            !HasExactBinding(_durableProfile, _provisioningIntent) ||
            !_namespaceState.TryPlanNextCreation(out var creationPlan))
        {
            return false;
        }

        SafePipeHandle? nativeHandle = null;
        WindowsProductionNamedPipeCandidate? preparedCandidate = null;
        var physicalFirstHandleCreated = false;
        var candidateOwnershipTransferred = false;
        try
        {
            var contract = GetNativeCreationContract(creationPlan.FirstPipeInstance);
            nativeHandle = CreateNativePipeHandle(contract);
            if (nativeHandle is null || nativeHandle.IsInvalid)
            {
                nativeHandle?.Dispose();
                nativeHandle = null;
                return false;
            }

            if (creationPlan.FirstPipeInstance)
            {
                physicalFirstHandleCreated = true;

                // Once the first physical handle exists, make the namespace
                // fail closed before any readback allocation or candidate
                // construction. Only a fully verified ownership transfer can
                // clear this pessimistic rejection.
                if (!_namespaceState.TryLatchFirstPhysicalHandle())
                {
                    return false;
                }
            }

            if (cancellationToken.IsCancellationRequested ||
                !WindowsNamedPipeAclReadback.TryReadExact(
                    nativeHandle,
                    _durableProfile,
                    _provisioningIntent,
                    out var readback) ||
                readback is null ||
                !readback.IsVerifiedFor(nativeHandle) ||
                cancellationToken.IsCancellationRequested ||
                !readback.DurableProfileDigest.FixedTimeEquals(_durableProfile.ProfileDigest) ||
                !readback.ServiceSid.FixedEquals(_durableProfile.ServiceSid) ||
                !readback.UserSid.FixedEquals(_provisioningIntent.UserSid))
            {
                return false;
            }

            // Preparing allocates the sole ownership object while the namespace
            // remains unchanged. In particular, a first attempt is still
            // pessimistically rejected here, and a later allocation failure
            // has not incremented its live candidate count.
            if (!_namespaceState.TryPrepareCandidateLease(
                    creationPlan,
                    nativeHandle,
                    readback,
                    out preparedCandidate) ||
                preparedCandidate is null)
            {
                return false;
            }

            // The prepared candidate now owns the physical handle even before
            // its nonthrowing state commit. The finally path disposes it on a
            // failed commit, which closes the handle without changing a later
            // count or clearing a first-attempt rejection.
            nativeHandle = null;
            if (!_namespaceState.TryCommitPreparedCandidateLease(preparedCandidate))
            {
                return false;
            }

            candidate = preparedCandidate;
            preparedCandidate = null;
            candidateOwnershipTransferred = true;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException or
            DllNotFoundException or
            EntryPointNotFoundException or
            InvalidOperationException or
            MarshalDirectiveException or
            OverflowException or
            SEHException)
        {
            return false;
        }
        finally
        {
            nativeHandle?.Dispose();
            preparedCandidate?.Dispose();
            if (creationPlan.FirstPipeInstance &&
                physicalFirstHandleCreated &&
                !candidateOwnershipTransferred)
            {
                // No error after a physical FIRST handle may permit a retry
                // as an ordinary later instance. This remains permanent even
                // when a failure was raised rather than returned.
                _namespaceState.RejectFirstAttemptPermanently();
            }
        }
    }

    public void Dispose()
    {
        _namespaceState.DisposeHost();
    }

    private SafePipeHandle CreateNativePipeHandle(NativeCreationContract contract)
    {
        // SECURITY_ATTRIBUTES and its descriptor are live only while the
        // native create call consumes them. Readback and candidate ownership
        // never observe this allocation.
        using var securityAttributes = NativeSecurityAttributes.Create(_provisioningIntent);
        return CreateNamedPipeW(
            string.Concat(PipePathPrefix, _provisioningIntent.PipeName),
            contract.OpenMode,
            contract.PipeMode,
            contract.MaximumInstances,
            contract.OutBufferBytes,
            contract.InBufferBytes,
            contract.DefaultTimeoutMilliseconds,
            securityAttributes.Pointer);
    }

    internal static NativeCreationContract GetNativeCreationContract(bool bootstrap) => new(
        PipeAccessDuplex |
        AccessSystemSecurity |
        (bootstrap ? FileFlagFirstPipeInstance : 0),
        PipeRejectRemoteClients,
        MaximumInstances,
        BufferBytes,
        BufferBytes,
        0,
        bootstrap,
        Inheritable: false);

    private static bool HasExactBinding(
        DurableProductionProfile profile,
        WindowsNamedPipeAclProvisioningIntent intent)
    {
        return profile.ProfileDigest is not null &&
            string.Equals(
                profile.ProfileDigest.TypeTag,
                DurableProductionProfile.ProfileDigestType,
                StringComparison.Ordinal) &&
            profile.ServiceSid.PrincipalKind == WindowsSidPrincipalKind.Service &&
            intent.ServiceSid.PrincipalKind == WindowsSidPrincipalKind.Service &&
            intent.UserSid.PrincipalKind == WindowsSidPrincipalKind.User &&
            profile.ServiceSid.FixedEquals(intent.ServiceSid) &&
            !intent.ServiceSid.FixedEquals(intent.UserSid) &&
            CanonicalNamedPipeAcl.TryValidateCanonicalSddl(
                intent.CanonicalSddl,
                intent.ServiceSid,
                intent.UserSid,
                out var canonicalAcl) &&
            canonicalAcl is not null &&
            canonicalAcl.ServiceSid.FixedEquals(profile.ServiceSid) &&
            canonicalAcl.UserSid.FixedEquals(intent.UserSid);
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        ExactSpelling = true,
        SetLastError = true,
        CallingConvention = CallingConvention.Winapi)]
    private static extern SafePipeHandle CreateNamedPipeW(
        string name,
        uint openMode,
        uint pipeMode,
        uint maximumInstances,
        uint outBufferBytes,
        uint inBufferBytes,
        uint defaultTimeoutMilliseconds,
        IntPtr securityAttributes);

    internal readonly record struct NativeCreationContract(
        uint OpenMode,
        uint PipeMode,
        uint MaximumInstances,
        uint OutBufferBytes,
        uint InBufferBytes,
        uint DefaultTimeoutMilliseconds,
        bool FirstPipeInstance,
        bool Inheritable);

    private sealed class NativeSecurityAttributes : IDisposable
    {
        private IntPtr _descriptor;
        private IntPtr _attributes;
        private int _disposed;

        private NativeSecurityAttributes(IntPtr descriptor, IntPtr attributes)
        {
            _descriptor = descriptor;
            _attributes = attributes;
        }

        internal IntPtr Pointer => _attributes != IntPtr.Zero
            ? _attributes
            : throw new ObjectDisposedException(nameof(NativeSecurityAttributes));

        internal static NativeSecurityAttributes Create(
            WindowsNamedPipeAclProvisioningIntent provisioningIntent)
        {
            ArgumentNullException.ThrowIfNull(provisioningIntent);
            var descriptorBytes = provisioningIntent
                .CreatePipeSecurity()
                .GetSecurityDescriptorBinaryForm();
            IntPtr descriptor = IntPtr.Zero;
            IntPtr attributes = IntPtr.Zero;
            try
            {
                if (!WindowsNamedPipeAclReadback.TryValidateExactDescriptor(
                        descriptorBytes,
                        provisioningIntent.ServiceSid,
                        provisioningIntent.UserSid,
                        out _))
                {
                    throw new InvalidOperationException("The pipe descriptor is not exact.");
                }

                descriptor = Marshal.AllocHGlobal(descriptorBytes.Length);
                Marshal.Copy(descriptorBytes, 0, descriptor, descriptorBytes.Length);
                var native = new SecurityAttributes
                {
                    Length = checked((uint)Marshal.SizeOf<SecurityAttributes>()),
                    SecurityDescriptor = descriptor,
                    InheritHandle = false,
                };
                attributes = Marshal.AllocHGlobal(Marshal.SizeOf<SecurityAttributes>());
                Marshal.StructureToPtr(native, attributes, fDeleteOld: false);
                return new NativeSecurityAttributes(descriptor, attributes);
            }
            catch
            {
                if (attributes != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(attributes);
                }

                if (descriptor != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(descriptor);
                }

                throw;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(descriptorBytes);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            var attributes = Interlocked.Exchange(ref _attributes, IntPtr.Zero);
            if (attributes != IntPtr.Zero)
            {
                Marshal.DestroyStructure<SecurityAttributes>(attributes);
                Marshal.FreeHGlobal(attributes);
            }

            var descriptor = Interlocked.Exchange(ref _descriptor, IntPtr.Zero);
            if (descriptor != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(descriptor);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            internal uint Length;
            internal IntPtr SecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            internal bool InheritHandle;
        }
    }
}

/// <summary>
/// Owns exactly one readback-verified but unconnected native pipe handle. It
/// deliberately offers no transport, accept or session operations. The
/// candidate itself is the sole verified authority: its private lifecycle
/// owner closes the native handle and changes the matching state lease as one
/// serialized operation.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProductionNamedPipeCandidate : IDisposable
{
    // The candidate never owns a separately releasable namespace token. Its
    // one composed lifetime owner holds the native handle and the namespace
    // lease state together. The constructor remains exact-SafePipeHandle-only
    // so this lifecycle primitive does not become an ACL/verification surface.
    private readonly PipeInstanceLifetimeOwner _lifetimeOwner;

    private WindowsProductionNamedPipeCandidate(
        SafePipeHandle handle,
        WindowsNamedPipeAclReadback readback,
        WindowsProductionNamedPipeNamespaceState namespaceState,
        bool firstPipeInstance,
        int preparedLiveCandidateLeaseCount)
    {
        _lifetimeOwner = new PipeInstanceLifetimeOwner(
            handle,
            namespaceState,
            firstPipeInstance,
            preparedLiveCandidateLeaseCount);
        DurableProfileDigest = readback.DurableProfileDigest;
        ServiceSid = readback.ServiceSid;
        UserSid = readback.UserSid;
    }

    /// <summary>
    /// Immutable proof metadata only. No SafePipeHandle, raw handle value,
    /// readback object, or independently disposable ownership object escapes
    /// this candidate.
    /// </summary>
    internal TypedHash DurableProfileDigest { get; }

    internal WindowsSid ServiceSid { get; }

    internal WindowsSid UserSid { get; }

    internal static WindowsProductionNamedPipeCandidate CreatePrepared(
        SafePipeHandle handle,
        WindowsNamedPipeAclReadback readback,
        WindowsProductionNamedPipeNamespaceState namespaceState,
        bool firstPipeInstance,
        int preparedLiveCandidateLeaseCount)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(readback);
        ArgumentNullException.ThrowIfNull(namespaceState);
        if (handle.IsInvalid ||
            handle.IsClosed ||
            !readback.IsVerifiedFor(handle) ||
            preparedLiveCandidateLeaseCount <= 0)
        {
            throw new ArgumentException("The pipe candidate ownership is invalid.", nameof(handle));
        }

        // This is the only managed allocation in the candidate lease path.
        // Namespace state has not changed yet, so an allocation exception is
        // fail-closed without consuming a count or clearing first rejection.
        return new WindowsProductionNamedPipeCandidate(
            handle,
            readback,
            namespaceState,
            firstPipeInstance,
            preparedLiveCandidateLeaseCount);
    }

    public void Dispose()
    {
        try
        {
            DisposeCore();
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    ~WindowsProductionNamedPipeCandidate()
    {
        // SafePipeHandle itself has a critical finalizer. This finalizer adds
        // the state-side release in the same close-then-release ordering; it
        // never calls a caller-provided delegate or factory.
        try
        {
            DisposeCore();
        }
        catch
        {
            // Finalization must not throw. The composed lifetime owner either
            // completes close-then-release or leaves the namespace fail-closed,
            // while SafePipeHandle retains its critical-finalizer backstop.
        }
    }

    internal bool TryGetPreparedCommit(
        WindowsProductionNamedPipeNamespaceState expectedState,
        out bool firstPipeInstance,
        out int preparedLiveCandidateLeaseCount)
    {
        return _lifetimeOwner.TryGetPreparedCommit(
            expectedState,
            out firstPipeInstance,
            out preparedLiveCandidateLeaseCount);
    }

    internal bool TryCommitLease() => _lifetimeOwner.TryCommitLease();

    private void DisposeCore() => _lifetimeOwner.Dispose();
}

/// <summary>
/// Owns one physical pipe-instance lifetime only: a SafeHandle and its one
/// namespace lease transition. This is deliberately not a verified candidate,
/// ACL readback, or native-factory authority. Production candidates construct
/// it only after exact SafePipeHandle/readback validation, while its base
/// SafeHandle input lets lifecycle behavior be tested without a native hook.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class PipeInstanceLifetimeOwner : IDisposable
{
    private const int Prepared = 0;
    private const int Committed = 1;
    private const int Released = 2;

    // This gate is allocated before the owner can escape its constructor. It
    // is deliberately held through acquisition of the namespace gate, physical
    // close, and the exact namespace transition: duplicate Dispose/finalizer
    // callers therefore return only after the winning close-and-release (or
    // fail-closed abort) has finished.
    private readonly object _disposeGate = new();
    private SafeHandle? _handle;
    private readonly WindowsProductionNamedPipeNamespaceState _namespaceState;
    private readonly bool _firstPipeInstance;
    private readonly int _preparedLiveCandidateLeaseCount;
    private int _ownershipState;

    internal PipeInstanceLifetimeOwner(
        SafeHandle handle,
        WindowsProductionNamedPipeNamespaceState namespaceState,
        bool firstPipeInstance,
        int preparedLiveCandidateLeaseCount)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(namespaceState);
        if (handle.IsInvalid ||
            handle.IsClosed ||
            preparedLiveCandidateLeaseCount <= 0)
        {
            throw new ArgumentException("The pipe-instance lifetime ownership is invalid.", nameof(handle));
        }

        _handle = handle;
        _namespaceState = namespaceState;
        _firstPipeInstance = firstPipeInstance;
        _preparedLiveCandidateLeaseCount = preparedLiveCandidateLeaseCount;
    }

    public void Dispose()
    {
        try
        {
            DisposeCore();
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    ~PipeInstanceLifetimeOwner()
    {
        try
        {
            DisposeCore();
        }
        catch
        {
            // A finalizer must not escape. A failed physical close makes the
            // namespace fail closed rather than releasing a potentially live
            // lease; SafeHandle retains its critical-finalizer backstop.
        }
    }

    /// <summary>
    /// This method is called only before a candidate/owner is exposed to a
    /// caller. State methods intentionally do not take _disposeGate: normal
    /// teardown orders candidate gate, physical close, then namespace gate.
    /// </summary>
    internal bool TryGetPreparedCommit(
        WindowsProductionNamedPipeNamespaceState expectedState,
        out bool firstPipeInstance,
        out int preparedLiveCandidateLeaseCount)
    {
        firstPipeInstance = _firstPipeInstance;
        preparedLiveCandidateLeaseCount = _preparedLiveCandidateLeaseCount;
        return ReferenceEquals(_namespaceState, expectedState) &&
            _ownershipState == Prepared;
    }

    /// <summary>
    /// Transitions the pre-publication owner to a committed lease. The host
    /// holds the namespace gate, and neither a candidate nor this lifetime
    /// owner can yet be disposed by another caller.
    /// </summary>
    internal bool TryCommitLease()
    {
        var handle = _handle;
        if (handle is null ||
            handle.IsInvalid ||
            handle.IsClosed ||
            _ownershipState != Prepared)
        {
            return false;
        }

        _ownershipState = Committed;
        return true;
    }

    private void DisposeCore()
    {
        lock (_disposeGate)
        {
            if (_ownershipState == Released)
            {
                return;
            }

            // Take the state and the physical owner together under the one
            // preallocated gate. There is deliberately no independent
            // Interlocked.Exchange ordering between these two fields.
            var previousOwnershipState = _ownershipState;
            var handle = _handle;
            _ownershipState = Released;
            _handle = null;
            var releaseCommittedLease = previousOwnershipState == Committed;

            // Candidate gate -> namespace gate is the only teardown order.
            // The namespace method deliberately keeps its production gate
            // across SafeHandle.Dispose/ReleaseHandle and the precise state
            // transition, so no host plan/create can race a closing instance.
            // Monitor is reentrant for the pre-publication host-failure path,
            // where the host already owns that same namespace gate.
            _namespaceState.CloseAndTransitionUnderGate(
                handle,
                releaseCommittedLease,
                _firstPipeInstance);
        }
    }
}

/// <summary>
/// The production namespace lifecycle for one dormant pipe host. A first
/// native handle is pessimistically rejected until its exact readback becomes
/// a live candidate lease. The state is intentionally internal so tests can
/// exercise the production transition rules without replacing native calls.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsProductionNamedPipeNamespaceState
{
    private readonly object _gate = new();
    private bool _hostDisposed;
    private bool _permanentlyRejected;
    private bool _firstAttemptPending;
    private int _liveVerifiedCandidateLeases;

    internal bool IsHostDisposed
    {
        get
        {
            lock (_gate)
            {
                return _hostDisposed;
            }
        }
    }

    internal bool IsPermanentlyRejected
    {
        get
        {
            lock (_gate)
            {
                return _permanentlyRejected;
            }
        }
    }

    internal bool IsFirstAttemptPending
    {
        get
        {
            lock (_gate)
            {
                return _firstAttemptPending;
            }
        }
    }

    internal int LiveVerifiedCandidateLeases
    {
        get
        {
            lock (_gate)
            {
                return _liveVerifiedCandidateLeases;
            }
        }
    }

    /// <summary>
    /// Acquires the same gate used by candidate teardown. The host holds this
    /// over planning, native creation, exact readback and ownership transfer,
    /// preventing its last live handle from disappearing mid-attempt.
    /// </summary>
    internal HostGateLease EnterHostGate() => new(_gate);

    internal bool TryPlanNextCreation(out NamespaceCreationPlan creationPlan)
    {
        lock (_gate)
        {
            creationPlan = default;
            if (_hostDisposed || _permanentlyRejected || _firstAttemptPending)
            {
                return false;
            }

            creationPlan = new NamespaceCreationPlan(
                FirstPipeInstance: _liveVerifiedCandidateLeases == 0);
            return true;
        }
    }

    /// <summary>
    /// Marks a successfully-created FIRST handle as rejected before descriptor
    /// readback or candidate allocation. Only a prepared candidate's successful
    /// nonthrowing commit can clear this pending rejection.
    /// </summary>
    internal bool TryLatchFirstPhysicalHandle()
    {
        lock (_gate)
        {
            if (_hostDisposed ||
                _permanentlyRejected ||
                _firstAttemptPending ||
                _liveVerifiedCandidateLeases != 0)
            {
                return false;
            }

            _permanentlyRejected = true;
            _firstAttemptPending = true;
            return true;
        }
    }

    /// <summary>
    /// Retains permanent closure after any post-handle first-attempt failure.
    /// It is safe to repeat from finally paths and never reopens a namespace.
    /// </summary>
    internal void RejectFirstAttemptPermanently()
    {
        lock (_gate)
        {
            _firstAttemptPending = false;
            _permanentlyRejected = true;
        }
    }

    /// <summary>
    /// Allocates the sole candidate owner while all namespace flags and the
    /// live count remain untouched. The host has already latched a first
    /// physical handle pessimistically; if this allocation fails, its finally
    /// path retains that permanent rejection. A later failure leaves its
    /// existing count unchanged and the host closes the still-owned handle.
    /// </summary>
    internal bool TryPrepareCandidateLease(
        NamespaceCreationPlan creationPlan,
        SafePipeHandle? handle,
        WindowsNamedPipeAclReadback? readback,
        out WindowsProductionNamedPipeCandidate? candidate)
    {
        candidate = null;
        lock (_gate)
        {
            if (handle is null ||
                handle.IsInvalid ||
                handle.IsClosed ||
                readback is null ||
                !readback.IsVerifiedFor(handle) ||
                !CanPrepareCandidateLease(creationPlan.FirstPipeInstance))
            {
                return false;
            }

            int nextLiveVerifiedCandidateLeases;
            try
            {
                nextLiveVerifiedCandidateLeases = checked(
                    _liveVerifiedCandidateLeases + 1);
            }
            catch (OverflowException)
            {
                return false;
            }

            // Do not move this allocation below a count or flag change. The
            // candidate is both the native-handle owner and the only lease;
            // no separate state object can outlive or underlive that handle.
            candidate = WindowsProductionNamedPipeCandidate.CreatePrepared(
                handle,
                readback,
                this,
                creationPlan.FirstPipeInstance,
                nextLiveVerifiedCandidateLeases);
            return true;
        }
    }

    /// <summary>
    /// Commits an already allocated candidate under the namespace gate. Every
    /// operation after allocation is nonthrowing: pre-publication ownership
    /// assignment, integer assignment, and first-attempt flag assignment only.
    /// </summary>
    internal bool TryCommitPreparedCandidateLease(
        WindowsProductionNamedPipeCandidate? candidate)
    {
        lock (_gate)
        {
            if (candidate is null ||
                !candidate.TryGetPreparedCommit(
                    this,
                    out var firstPipeInstance,
                    out var preparedLiveCandidateLeaseCount) ||
                !CanPrepareCandidateLease(firstPipeInstance) ||
                !HasCurrentPreparedLeaseCount(
                    firstPipeInstance,
                    preparedLiveCandidateLeaseCount) ||
                !candidate.TryCommitLease())
            {
                return false;
            }

            _liveVerifiedCandidateLeases = preparedLiveCandidateLeaseCount;
            if (firstPipeInstance)
            {
                _firstAttemptPending = false;
                _permanentlyRejected = false;
            }

            return true;
        }
    }

    /// <summary>
    /// Commits a true internal lifecycle owner for the same state machine used
    /// by production candidates. The owner carries no ACL or verification
    /// authority; its SafeHandle input exists solely so lifetime ordering can
    /// be exercised without a production delegate or native factory hook.
    /// </summary>
    internal bool TryCommitPreparedLifetimeOwner(
        PipeInstanceLifetimeOwner? lifetimeOwner)
    {
        lock (_gate)
        {
            if (lifetimeOwner is null ||
                !lifetimeOwner.TryGetPreparedCommit(
                    this,
                    out var firstPipeInstance,
                    out var preparedLiveCandidateLeaseCount) ||
                !CanPrepareCandidateLease(firstPipeInstance) ||
                !HasCurrentPreparedLeaseCount(
                    firstPipeInstance,
                    preparedLiveCandidateLeaseCount) ||
                !lifetimeOwner.TryCommitLease())
            {
                return false;
            }

            _liveVerifiedCandidateLeases = preparedLiveCandidateLeaseCount;
            if (firstPipeInstance)
            {
                _firstAttemptPending = false;
                _permanentlyRejected = false;
            }

            return true;
        }
    }

    /// <summary>
    /// Takes the same namespace gate as host plan/create and keeps it while
    /// physically closing a candidate handle and applying its exact terminal
    /// state transition. The caller already owns the candidate disposal gate;
    /// this state object never takes that gate. Monitor reentrancy keeps the
    /// pre-publication host-failure cleanup safe when the host already owns
    /// this gate.
    /// </summary>
    internal void CloseAndTransitionUnderGate(
        SafeHandle? handle,
        bool releaseCommittedLease,
        bool firstPipeInstance)
    {
        lock (_gate)
        {
            if (handle is null)
            {
                AbortCandidateLeaseAfterFailedCloseUnderGate();
                return;
            }

            try
            {
                // Keep the production namespace gate during ReleaseHandle.
                // A host cannot plan or create another native instance until
                // this physical close and its state transition are complete.
                handle.Dispose();
            }
            catch
            {
                // Never lower the live count when SafeHandle.Dispose did not
                // return. The namespace is instead permanently fail-closed.
                AbortCandidateLeaseAfterFailedCloseUnderGate();
                throw;
            }

            if (!handle.IsClosed)
            {
                // A normal SafeHandle.Dispose must close the handle. Treat a
                // non-closing implementation as an abort, never as a release.
                AbortCandidateLeaseAfterFailedCloseUnderGate();
                return;
            }

            if (releaseCommittedLease)
            {
                ReleaseCommittedCandidateLeaseUnderGate();
                return;
            }

            AbortPreparedCandidateLeaseAfterCloseUnderGate(firstPipeInstance);
        }
    }

    internal void DisposeHost()
    {
        lock (_gate)
        {
            _hostDisposed = true;
        }
    }

    internal readonly record struct NamespaceCreationPlan(bool FirstPipeInstance);

    private bool CanPrepareCandidateLease(bool firstPipeInstance)
    {
        if (_hostDisposed)
        {
            return false;
        }

        if (firstPipeInstance)
        {
            return _permanentlyRejected &&
                _firstAttemptPending &&
                _liveVerifiedCandidateLeases == 0;
        }

        return !_permanentlyRejected &&
            !_firstAttemptPending &&
            _liveVerifiedCandidateLeases > 0;
    }

    private bool HasCurrentPreparedLeaseCount(
        bool firstPipeInstance,
        int preparedLiveCandidateLeaseCount)
    {
        if (firstPipeInstance)
        {
            return preparedLiveCandidateLeaseCount == 1 &&
                _liveVerifiedCandidateLeases == 0;
        }

        return preparedLiveCandidateLeaseCount > 0 &&
            _liveVerifiedCandidateLeases > 0 &&
            preparedLiveCandidateLeaseCount - 1 == _liveVerifiedCandidateLeases;
    }

    private void ReleaseCommittedCandidateLeaseUnderGate()
    {
        if (_liveVerifiedCandidateLeases == 0)
        {
            // This is unreachable through one candidate-owned committed lease.
            // Preserve fail-closed state rather than silently underflowing.
            _permanentlyRejected = true;
            return;
        }

        _liveVerifiedCandidateLeases--;
    }

    private void AbortPreparedCandidateLeaseAfterCloseUnderGate(bool firstPipeInstance)
    {
        // A prepared later candidate has not changed the live count or any
        // first-attempt flag, so its successful close has no state to release.
        // A prepared FIRST candidate retains the pessimistic rejection, but
        // its pending attempt is now terminal as part of this same close gate.
        if (firstPipeInstance)
        {
            _permanentlyRejected = true;
            _firstAttemptPending = false;
        }
    }

    private void AbortCandidateLeaseAfterFailedCloseUnderGate()
    {
        // Preserve a committed count when physical closure is uncertain. This
        // also terminally rejects an uncommitted FIRST attempt and makes a
        // later attempt fail closed rather than permitting a possible live
        // native handle to coexist with a new creation plan.
        _permanentlyRejected = true;
        _firstAttemptPending = false;
    }

    internal readonly struct HostGateLease : IDisposable
    {
        private readonly object _gate;

        internal HostGateLease(object gate)
        {
            _gate = gate;
            // Assignment above cannot throw. If Monitor.Enter throws while
            // waiting, this lease never owns the gate; after successful entry
            // the constructor performs no potentially-throwing operation.
            Monitor.Enter(gate);
        }

        // A using local invokes this value-type implementation through a
        // constrained call; no HostGateLease object is allocated or boxed.
        public void Dispose() => Monitor.Exit(_gate);
    }
}
