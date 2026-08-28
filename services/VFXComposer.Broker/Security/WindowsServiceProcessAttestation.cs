using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;

namespace VFXComposer.Broker.Security;

/// <summary>
/// A private, query/synchronize-only pin of one Windows process object. The
/// resulting observation is deliberately not an admission, an issuer receipt,
/// or executable-content identity. It is not reachable from Program or policy
/// loading.
/// </summary>
internal sealed class WindowsServiceProcessAttestation : IDisposable
{
    private readonly object _gate = new();
    private readonly WindowsServiceProcessAttestationExpectation _expectation;
    private readonly WindowsServiceProcessAttestationFacts _facts;
    private WindowsServiceProcessAttestationPin? _pin;
    private int _revoked;

    private WindowsServiceProcessAttestation(
        WindowsServiceProcessAttestationExpectation expectation,
        WindowsServiceProcessAttestationFacts facts,
        WindowsServiceProcessAttestationPin pin)
    {
        _expectation = expectation;
        _facts = facts;
        _pin = pin;
    }

    internal int ProcessId => _facts.ProcessId;

    internal string ProcessEpoch => _facts.ProcessEpoch;

    internal uint WindowsSessionId => _facts.WindowsSessionId;

    internal WindowsExecutablePathObservation ExecutablePathObservation =>
        _facts.ExecutablePathObservation;

    // QueryFullProcessImageNameW reports an OS path observation. It cannot prove
    // that the bytes at that path are the immutable loaded-image bytes, so this
    // primitive must never represent it as an executable-content identity.
    internal bool HasExecutableContentIdentity => false;

    internal bool IsRevoked => Volatile.Read(ref _revoked) != 0;

    internal bool IsUsable
    {
        get
        {
            lock (_gate)
            {
                return _revoked == 0 && _pin is not null;
            }
        }
    }

    /// <summary>
    /// Opens the supplied PID only as a locator, immediately pins the resulting
    /// object with a non-inheritable handle, and verifies all facts from that
    /// object. The PID is never accepted as a trust root.
    /// </summary>
    internal static bool TryObserve(
        WindowsServiceProcessAttestationExpectation? expectation,
        out WindowsServiceProcessAttestation? observation)
    {
        observation = null;
        if (expectation is null || !OperatingSystem.IsWindows())
        {
            return false;
        }

        WindowsServiceProcessAttestationPin? pin = null;
        try
        {
            if (!WindowsServiceProcessAttestationPin.TryOpen(
                    expectation.ExpectedProcess.ProcessId,
                    out pin) ||
                pin is null)
            {
                return false;
            }

            if (!TryReadExactFacts(expectation, pin, out var first) ||
                !TryReadExactFacts(expectation, pin, out var replay) ||
                !first.FixedEquals(replay))
            {
                return false;
            }

            observation = new WindowsServiceProcessAttestation(expectation, first, pin);
            pin = null;
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidDataException or
            InvalidOperationException or
            ObjectDisposedException or
            OverflowException or
            UnauthorizedAccessException or
            DllNotFoundException or
            EntryPointNotFoundException or
            MarshalDirectiveException)
        {
            return false;
        }
        finally
        {
            pin?.Dispose();
        }
    }

    /// <summary>
    /// Replays process liveness, creation epoch, token user/groups, service-SID
    /// semantics, Windows session, and native image-path observation while holding
    /// the same process-object pin. It does not turn the observation into an
    /// executable-content identity or an authority grant.
    /// </summary>
    internal bool TryReplayExactFacts()
    {
        lock (_gate)
        {
            return _revoked == 0 &&
                _pin is { } pin &&
                TryReadExactFacts(_expectation, pin, out var replay) &&
                _facts.FixedEquals(replay);
        }
    }

    /// <summary>
    /// Revocation is linearizable with fact replay and closes the uniquely owned
    /// native pin before any competing caller returns from a later revoke.
    /// </summary>
    internal void Revoke()
    {
        lock (_gate)
        {
            if (_revoked != 0)
            {
                return;
            }

            Volatile.Write(ref _revoked, 1);
            var pin = _pin;
            _pin = null;
            pin?.Dispose();
        }
    }

    public void Dispose() => Revoke();

    private static bool TryReadExactFacts(
        WindowsServiceProcessAttestationExpectation expectation,
        WindowsServiceProcessAttestationPin pin,
        out WindowsServiceProcessAttestationFacts facts)
    {
        facts = null!;
        if (!pin.IsNonInheritable ||
            pin.ProcessId != expectation.ExpectedProcess.ProcessId ||
            !pin.IsActive ||
            !pin.TryObserveEpoch(out var processEpoch) ||
            !string.Equals(
                processEpoch,
                expectation.ExpectedProcess.ProcessEpoch,
                StringComparison.Ordinal) ||
            !pin.TryObserveWindowsSessionId(out var windowsSessionId) ||
            windowsSessionId != expectation.ExpectedWindowsSessionId ||
            !pin.TryObserveTokenFacts(out var tokenFacts) ||
            !tokenFacts.HasExactlyOneEnabledServiceSid(
                expectation.ExpectedProcess.ServiceSid) ||
            !pin.TryObserveNativeImagePath(out var nativeImagePath) ||
            !WindowsExecutablePathObservation.TryCreate(
                nativeImagePath,
                out var imagePathObservation) ||
            imagePathObservation is null ||
            expectation.ExpectedImagePathObservation is { } expectedImagePath &&
            !expectedImagePath.FixedEquals(imagePathObservation) ||
            !pin.IsActive)
        {
            return false;
        }

        facts = new WindowsServiceProcessAttestationFacts(
            pin.ProcessId,
            processEpoch,
            windowsSessionId,
            tokenFacts,
            imagePathObservation);
        return true;
    }
}

/// <summary>
/// Host-owned expectation for one supplied service identity and Windows session.
/// Its optional image observation can only correlate a prior OS path observation;
/// it cannot validate the existing typed image hash or executable bytes.
/// </summary>
internal sealed class WindowsServiceProcessAttestationExpectation
{
    internal WindowsServiceProcessAttestationExpectation(
        WindowsServiceProcessIdentity expectedProcess,
        uint expectedWindowsSessionId,
        WindowsExecutablePathObservation? expectedImagePathObservation = null)
    {
        ExpectedProcess = expectedProcess ?? throw new ArgumentNullException(nameof(expectedProcess));
        ExpectedWindowsSessionId = expectedWindowsSessionId;
        ExpectedImagePathObservation = expectedImagePathObservation;
    }

    internal WindowsServiceProcessIdentity ExpectedProcess { get; }

    internal uint ExpectedWindowsSessionId { get; }

    internal WindowsExecutablePathObservation? ExpectedImagePathObservation { get; }

    internal WindowsServiceProcessAttestationExpectation WithExpectedImagePath(
        WindowsServiceProcessAttestation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.IsRevoked ||
            observation.ProcessId != ExpectedProcess.ProcessId ||
            !string.Equals(
                observation.ProcessEpoch,
                ExpectedProcess.ProcessEpoch,
                StringComparison.Ordinal) ||
            observation.WindowsSessionId != ExpectedWindowsSessionId)
        {
            throw new ArgumentException(
                "Path observations may only seed the exact pinned process expectation.",
                nameof(observation));
        }

        return new WindowsServiceProcessAttestationExpectation(
            ExpectedProcess,
            ExpectedWindowsSessionId,
            observation.ExecutablePathObservation);
    }
}

/// <summary>
/// Private native-path observation from a pinned process handle. The path string
/// is deliberately retained only for exact in-memory replay and is never exposed
/// as a caller path, project path, or executable-content identity.
/// </summary>
internal sealed class WindowsExecutablePathObservation
{
    private const int MaximumNativePathCharacters = 32_768;
    private readonly string _nativePath;

    private WindowsExecutablePathObservation(string nativePath) => _nativePath = nativePath;

    internal static bool TryCreate(
        string? nativePath,
        out WindowsExecutablePathObservation? observation)
    {
        observation = null;
        if (string.IsNullOrEmpty(nativePath) ||
            nativePath.Length > MaximumNativePathCharacters ||
            !nativePath.StartsWith("\\Device\\", StringComparison.Ordinal) ||
            nativePath.Any(character => character is '\0' or < ' ' or '\u007f'))
        {
            return false;
        }

        observation = new WindowsExecutablePathObservation(nativePath);
        return true;
    }

    internal bool FixedEquals(WindowsExecutablePathObservation? other) =>
        other is not null &&
        string.Equals(_nativePath, other._nativePath, StringComparison.Ordinal);
}

/// <summary>
/// The token user is inspected and retained privately, while every token group is
/// replayed privately. Only the configured service SID can satisfy the exact
/// enabled/non-deny-only membership predicate; TokenUser is never substituted.
/// </summary>
internal sealed class WindowsTokenFactSnapshot
{
    internal const uint GroupEnabled = 0x00000004;
    internal const uint GroupUseForDenyOnly = 0x00000010;

    private readonly byte[] _tokenUserSid;
    private readonly WindowsTokenGroupObservation[] _groups;

    internal WindowsTokenFactSnapshot(
        ReadOnlySpan<byte> tokenUserSid,
        IEnumerable<WindowsTokenGroupObservation> groups)
    {
        if (tokenUserSid.Length is 0 or > 1024)
        {
            throw new ArgumentException("Token user SID observation is invalid.", nameof(tokenUserSid));
        }

        ArgumentNullException.ThrowIfNull(groups);
        _tokenUserSid = tokenUserSid.ToArray();
        _groups = groups.ToArray();
        if (_groups.Length > 4096 || _groups.Any(group => group is null))
        {
            throw new ArgumentException("Token group observation is invalid.", nameof(groups));
        }

        Array.Sort(_groups, WindowsTokenGroupObservation.OrdinalComparer);
    }

    internal bool HasExactlyOneEnabledServiceSid(WindowsSid expectedServiceSid)
    {
        ArgumentNullException.ThrowIfNull(expectedServiceSid);
        if (expectedServiceSid.PrincipalKind != WindowsSidPrincipalKind.Service)
        {
            return false;
        }

        var matchingGroups = 0;
        foreach (var group in _groups)
        {
            if (!group.MatchesServiceSid(expectedServiceSid))
            {
                continue;
            }

            matchingGroups++;
            if (!group.IsEnabledForAllowChecks)
            {
                return false;
            }
        }

        return matchingGroups == 1;
    }

    internal bool FixedEquals(WindowsTokenFactSnapshot? other)
    {
        if (other is null)
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(_tokenUserSid, other._tokenUserSid) ||
            _groups.Length != other._groups.Length)
        {
            return false;
        }

        for (var index = 0; index < _groups.Length; index++)
        {
            if (!_groups[index].FixedEquals(other._groups[index]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>One private SID-and-attributes element from TokenGroups.</summary>
internal sealed class WindowsTokenGroupObservation
{
    private readonly byte[] _sid;
    private readonly WindowsSid? _serviceSid;

    private WindowsTokenGroupObservation(
        ReadOnlySpan<byte> sid,
        uint attributes,
        WindowsSid? serviceSid)
    {
        if (sid.Length is 0 or > 1024)
        {
            throw new ArgumentException("Token group SID observation is invalid.", nameof(sid));
        }

        _sid = sid.ToArray();
        Attributes = attributes;
        _serviceSid = serviceSid;
    }

    internal static IComparer<WindowsTokenGroupObservation> OrdinalComparer { get; } =
        Comparer<WindowsTokenGroupObservation>.Create((left, right) =>
        {
            var comparison = left._sid.AsSpan().SequenceCompareTo(right._sid);
            return comparison != 0
                ? comparison
                : left.Attributes.CompareTo(right.Attributes);
        });

    internal uint Attributes { get; }

    internal bool IsEnabledForAllowChecks =>
        (Attributes & WindowsTokenFactSnapshot.GroupEnabled) != 0 &&
        (Attributes & WindowsTokenFactSnapshot.GroupUseForDenyOnly) == 0;

    internal static WindowsTokenGroupObservation FromNative(
        ReadOnlySpan<byte> sid,
        uint attributes)
    {
        WindowsSid? serviceSid = null;
        try
        {
            serviceSid = WindowsSid.FromBinary(sid, WindowsSidPrincipalKind.Service);
        }
        catch (ArgumentException)
        {
            // Most token groups are not service SIDs. They remain observed but
            // cannot satisfy the configured service-SID predicate.
        }

        return new WindowsTokenGroupObservation(sid, attributes, serviceSid);
    }

    internal bool MatchesServiceSid(WindowsSid expectedServiceSid) =>
        _serviceSid is not null && _serviceSid.FixedEquals(expectedServiceSid);

    internal bool FixedEquals(WindowsTokenGroupObservation? other) =>
        other is not null &&
        Attributes == other.Attributes &&
        CryptographicOperations.FixedTimeEquals(_sid, other._sid);
}

internal sealed class WindowsServiceProcessAttestationFacts
{
    internal WindowsServiceProcessAttestationFacts(
        int processId,
        string processEpoch,
        uint windowsSessionId,
        WindowsTokenFactSnapshot tokenFacts,
        WindowsExecutablePathObservation executablePathObservation)
    {
        ProcessId = processId;
        ProcessEpoch = processEpoch;
        WindowsSessionId = windowsSessionId;
        TokenFacts = tokenFacts;
        ExecutablePathObservation = executablePathObservation;
    }

    internal int ProcessId { get; }

    internal string ProcessEpoch { get; }

    internal uint WindowsSessionId { get; }

    internal WindowsTokenFactSnapshot TokenFacts { get; }

    internal WindowsExecutablePathObservation ExecutablePathObservation { get; }

    internal bool FixedEquals(WindowsServiceProcessAttestationFacts? other) =>
        other is not null &&
        ProcessId == other.ProcessId &&
        string.Equals(ProcessEpoch, other.ProcessEpoch, StringComparison.Ordinal) &&
        WindowsSessionId == other.WindowsSessionId &&
        TokenFacts.FixedEquals(other.TokenFacts) &&
        ExecutablePathObservation.FixedEquals(other.ExecutablePathObservation);
}

/// <summary>
/// Sealed native-only owner constructed by the attestation primitive. It has no
/// caller-supplied or externally forgeable observation source.
/// </summary>
internal sealed class WindowsServiceProcessAttestationPin
{
    private const uint ProcessQueryLimitedInformation = 0x00001000;
    private const uint ProcessSynchronize = 0x00100000;
    private const uint TokenQuery = 0x0008;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint ProcessNameNative = 0x00000001;
    private const int TokenUserClass = 1;
    private const int TokenGroupsClass = 2;
    private const int MaximumTokenInformationBytes = 1_048_576;
    private const int MaximumTokenGroupCount = 4096;
    private const int MaximumNativePathCharacters = 32_768;

    private SafeProcessHandle? _process;

    private WindowsServiceProcessAttestationPin(SafeProcessHandle process, int processId)
    {
        _process = process;
        ProcessId = processId;
    }

    internal int ProcessId { get; }

    internal bool IsNonInheritable
    {
        get
        {
            var process = Volatile.Read(ref _process);
            return process is not null &&
                !process.IsClosed &&
                !process.IsInvalid &&
                GetHandleInformation(process, out var flags) &&
                (flags & HandleFlagInherit) == 0;
        }
    }

    internal bool IsActive
    {
        get
        {
            var process = Volatile.Read(ref _process);
            return process is not null && ProcessEpoch.IsActive(process);
        }
    }

    internal static bool TryOpen(
        int processId,
        out WindowsServiceProcessAttestationPin? pin)
    {
        pin = null;
        if (!OperatingSystem.IsWindows() || processId <= 0)
        {
            return false;
        }

        var process = OpenProcess(
            ProcessQueryLimitedInformation | ProcessSynchronize,
            inheritHandle: false,
            processId);
        if (process.IsInvalid)
        {
            process.Dispose();
            return false;
        }

        if (!GetHandleInformation(process, out var flags) ||
            (flags & HandleFlagInherit) != 0)
        {
            process.Dispose();
            return false;
        }

        pin = new WindowsServiceProcessAttestationPin(process, processId);
        return true;
    }

    internal bool TryObserveEpoch(out string processEpoch)
    {
        processEpoch = string.Empty;
        var process = Volatile.Read(ref _process);
        if (process is null || process.IsClosed || process.IsInvalid)
        {
            return false;
        }

        try
        {
            processEpoch = ProcessEpoch.Observe(process, ProcessId);
            return true;
        }
        catch (Exception exception) when (
            exception is InvalidDataException or ObjectDisposedException)
        {
            return false;
        }
    }

    internal bool TryObserveWindowsSessionId(out uint windowsSessionId)
    {
        windowsSessionId = 0;
        return Volatile.Read(ref _process) is not null &&
            ProcessIdToSessionId((uint)ProcessId, out windowsSessionId);
    }

    internal bool TryObserveTokenFacts(out WindowsTokenFactSnapshot tokenFacts)
    {
        tokenFacts = null!;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var process = Volatile.Read(ref _process);
        if (process is null || process.IsClosed || process.IsInvalid)
        {
            return false;
        }

        if (!OpenProcessToken(process, TokenQuery, out var token) || token is null)
        {
            token?.Dispose();
            return false;
        }

        using (token)
        {
            if (token.IsInvalid ||
                !GetHandleInformation(token, out var flags) ||
                (flags & HandleFlagInherit) != 0)
            {
                return false;
            }

            IntPtr userBuffer = IntPtr.Zero;
            IntPtr groupsBuffer = IntPtr.Zero;
            try
            {
                if (!TryReadTokenInformation(
                        token,
                        TokenUserClass,
                        out userBuffer,
                        out _) ||
                    !TryReadTokenInformation(
                        token,
                        TokenGroupsClass,
                        out groupsBuffer,
                        out var groupsLength))
                {
                    return false;
                }

                if (!TryCopySid(Marshal.PtrToStructure<SidAndAttributes>(userBuffer).Sid, out var userSid) ||
                    !TryReadGroups(groupsBuffer, groupsLength, out var groups))
                {
                    return false;
                }

                tokenFacts = new WindowsTokenFactSnapshot(userSid, groups);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                OverflowException or
                InvalidDataException)
            {
                return false;
            }
            finally
            {
                if (userBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(userBuffer);
                }

                if (groupsBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(groupsBuffer);
                }
            }
        }
    }

    internal bool TryObserveNativeImagePath(out string nativeImagePath)
    {
        nativeImagePath = string.Empty;
        var process = Volatile.Read(ref _process);
        if (process is null || process.IsClosed || process.IsInvalid)
        {
            return false;
        }

        var path = new StringBuilder(MaximumNativePathCharacters);
        var length = (uint)path.Capacity;
        if (!QueryFullProcessImageNameW(
                process,
                ProcessNameNative,
                path,
                ref length) ||
            length == 0 ||
            length >= path.Capacity)
        {
            return false;
        }

        nativeImagePath = path.ToString();
        return nativeImagePath.Length == length;
    }

    internal void Dispose() =>
        Interlocked.Exchange(ref _process, null)?.Dispose();

    private static bool TryReadTokenInformation(
        SafeAccessTokenHandle token,
        int informationClass,
        out IntPtr buffer,
        out int length)
    {
        buffer = IntPtr.Zero;
        length = 0;
        _ = GetTokenInformation(token, informationClass, IntPtr.Zero, 0, out var required);
        if (required <= 0 || required > MaximumTokenInformationBytes)
        {
            return false;
        }

        buffer = Marshal.AllocHGlobal(required);
        if (!GetTokenInformation(token, informationClass, buffer, required, out var actual) ||
            actual <= 0 ||
            actual > required)
        {
            Marshal.FreeHGlobal(buffer);
            buffer = IntPtr.Zero;
            return false;
        }

        length = actual;
        return true;
    }

    private static bool TryReadGroups(
        IntPtr buffer,
        int bufferLength,
        out WindowsTokenGroupObservation[] groups)
    {
        groups = Array.Empty<WindowsTokenGroupObservation>();
        if (buffer == IntPtr.Zero || bufferLength < sizeof(uint))
        {
            return false;
        }

        var groupCount = unchecked((uint)Marshal.ReadInt32(buffer));
        if (groupCount > MaximumTokenGroupCount)
        {
            return false;
        }

        var groupsOffset = checked((int)Marshal.OffsetOf<TokenGroupsHeader>(
            nameof(TokenGroupsHeader.FirstGroup)));
        var groupSize = Marshal.SizeOf<SidAndAttributes>();
        var minimumLength = checked(groupsOffset + checked((int)groupCount) * groupSize);
        if (bufferLength < minimumLength)
        {
            return false;
        }

        var observed = new WindowsTokenGroupObservation[checked((int)groupCount)];
        for (var index = 0; index < observed.Length; index++)
        {
            var elementOffset = checked(groupsOffset + index * groupSize);
            var element = Marshal.PtrToStructure<SidAndAttributes>(
                IntPtr.Add(buffer, elementOffset));
            if (!TryCopySid(element.Sid, out var sid))
            {
                return false;
            }

            observed[index] = WindowsTokenGroupObservation.FromNative(sid, element.Attributes);
        }

        groups = observed;
        return true;
    }

    private static bool TryCopySid(IntPtr sid, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (sid == IntPtr.Zero || !IsValidSid(sid))
        {
            return false;
        }

        var length = GetLengthSid(sid);
        if (length == 0 || length > 1024)
        {
            return false;
        }

        bytes = new byte[length];
        Marshal.Copy(sid, bytes, 0, checked((int)length));
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenGroupsHeader
    {
        public uint GroupCount;
        public SidAndAttributes FirstGroup;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(
        SafeHandle handle,
        out uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(
        uint processId,
        out uint sessionId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(
        SafeProcessHandle process,
        uint flags,
        StringBuilder executablePath,
        ref uint size);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(
        SafeProcessHandle process,
        uint desiredAccess,
        out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle token,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsValidSid(IntPtr sid);

    [DllImport("advapi32.dll")]
    private static extern uint GetLengthSid(IntPtr sid);
}
