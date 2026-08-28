using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Security;

/// <summary>
/// A dormant, Windows-only durable profile/replay-store primitive.
///
/// The only location capability accepted by this type is an already-open,
/// caller-owned, non-inheritable directory handle. It never discovers a path,
/// creates ancestors, loads policy, starts a service, creates a listener, or
/// grants authority. Every file operation is a fixed single segment relative to
/// that held directory handle and uses object-manager no-follow flags.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsDurableProfileStore : IDisposable
{
    internal const int NonceLength = 32;
    internal const int StoreIdentifierLength = 32;
    internal const int MaximumRecordCount = 128;

    private const uint HeaderVersion = 1;
    private const uint RecordVersion = 1;
    private const uint GenericRead = 0x80000000;
    private const uint FileReadData = 0x00000001;
    private const uint FileWriteData = 0x00000002;
    private const uint FileReadAttributes = 0x00000080;
    private const uint ReadControl = 0x00020000;
    private const uint DeleteAccess = 0x00010000;
    private const uint AccessSystemSecurity = 0x01000000;
    internal const uint RequiredRootGrantedAccess =
        DurableProductionProfile.RootAccessMask | AccessSystemSecurity;
    internal const uint StoreReadOpenAccess =
        FileReadData | FileReadAttributes | ReadControl | DeleteAccess | AccessSystemSecurity | Synchronize;
    internal const uint StoreCreateOpenAccess =
        FileReadData | FileWriteData | FileReadAttributes | ReadControl | DeleteAccess | AccessSystemSecurity | Synchronize;
    private const uint Synchronize = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint HandleFlagInherit = 0x00000001;
    private const uint FileTypeDisk = 0x00000001;
    private const uint FileDeviceDisk = 0x00000007;
    private const uint FileRemoteDevice = 0x00000010;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint FileOpen = 0x00000001;
    private const uint FileCreate = 0x00000002;
    private const uint FileOpenIf = 0x00000003;
    private const int FileRenameInformationClass = 10;
    private const int FileFsDeviceInformationClass = 4;
    private const int FileAttributeTagInformationClass = 9;
    private const int FileIdInformationClass = 18;
    private const int ObjectBasicInformationClass = 0;
    private const int StatusSuccess = 0;
    private const int StatusObjectNameNotFound = unchecked((int)0xC0000034);
    private const int StatusObjectPathNotFound = unchecked((int)0xC000003A);
    internal const int MaximumHeaderBytes = 8_192;
    private const int MaximumProtectedKeyBytes = 4_096;
    private const int StoreKeyLength = 32;
    private const int AuthenticatorLength = 32;
    private const uint CryptProtectUiForbidden = 0x00000001;

    private const string OwnerLockSegment = "vfx-durable-profile-owner.lock";
    private const string HeaderPendingSegment = "vfx-durable-profile-header.pending";
    private const string HeaderFinalSegment = "vfx-durable-profile-header.v1";

    private static readonly byte[] HeaderMagic = Encoding.ASCII.GetBytes("VFXDPSH1");
    private static readonly byte[] RecordMagic = Encoding.ASCII.GetBytes("VFXDPSR1");
    private static readonly byte[] TipMagic = Encoding.ASCII.GetBytes("VFXDPST1");
    private static readonly byte[] RecordHmacDomain = Encoding.ASCII.GetBytes(
        "vfxcomposer.durable-profile-store.record-hmac-sha256/2\0");
    private static readonly byte[] SealedTipHmacDomain = Encoding.ASCII.GetBytes(
        "vfxcomposer.durable-profile-store.sealed-tip-hmac-sha256/2\0");
    private static readonly byte[] NonceFingerprintHmacDomain = Encoding.ASCII.GetBytes(
        "vfxcomposer.durable-profile-store.nonce-fingerprint-hmac-sha256/2\0");

    private readonly object _gate = new();
    private readonly DurableRootCapability _root;
    private readonly DurableProductionProfile _profile;
    private readonly SafeFileHandle _ownerLock;
    private readonly byte[] _storeIdentifier;
    private readonly TypedHash _storeIdentifierDigest;
    private readonly TypedHash _issuerEpochDigest;
    private readonly HashSet<NonceFingerprint> _consumedCurrentGenerationNonces = [];
    private byte[]? _hmacKey;
    private byte[]? _issuerEpoch;
    private ChainState _state;
    private bool _disposed;

    private WindowsDurableProfileStore(
        DurableRootCapability root,
        DurableProductionProfile profile,
        SafeFileHandle ownerLock,
        byte[] storeIdentifier,
        byte[] hmacKey)
    {
        _root = root;
        _profile = profile;
        _ownerLock = ownerLock;
        _storeIdentifier = storeIdentifier;
        _hmacKey = hmacKey;
        _storeIdentifierDigest = TypedHash.Compute(
            "vfxcomposer.durable-profile-store-id/1",
            _storeIdentifier);
        byte[]? issuerEpoch = null;
        try
        {
            issuerEpoch = RandomNumberGenerator.GetBytes(NonceLength);
            _issuerEpochDigest = TypedHash.Compute(
                "vfxcomposer.durable-profile-issuer-epoch/1",
                issuerEpoch);
            _issuerEpoch = issuerEpoch;
            issuerEpoch = null;
            _state = ChainState.Empty;
        }
        finally
        {
            if (issuerEpoch is not null)
            {
                CryptographicOperations.ZeroMemory(issuerEpoch);
            }
        }
    }

    /// <summary>
    /// Creates the first immutable header and first generation record. The
    /// supplied root must already exist and be pinned by the caller; D1 never
    /// creates or discovers that root or any ancestor.
    /// </summary>
    internal static WindowsDurableProfileStore CreateNew(
        SafeFileHandle pinnedDirectory,
        DurableProductionProfile profile,
        long initialGeneration)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (initialGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialGeneration));
        }

        RequireWindows();
        DurableRootCapability? root = null;
        SafeFileHandle? ownerLock = null;
        byte[]? storeIdentifier = null;
        byte[]? storeKey = null;
        WindowsDurableProfileStore? store = null;
        try
        {
            root = DurableRootCapability.Acquire(pinnedDirectory, profile);
            ownerLock = OpenExclusiveOwnerLock(root, profile);
            RequireAbsent(root, profile, HeaderPendingSegment);
            RequireAbsent(root, profile, HeaderFinalSegment);
            RequireAbsent(root, profile, RecordPendingSegment(1));
            RequireAbsent(root, profile, RecordFinalSegment(1));
            RequireAbsent(root, profile, TipPendingSegment(1));
            RequireAbsent(root, profile, TipFinalSegment(1));

            storeIdentifier = RandomNumberGenerator.GetBytes(StoreIdentifierLength);
            storeKey = RandomNumberGenerator.GetBytes(StoreKeyLength);
            byte[]? protectedStoreKey = null;
            byte[]? header = null;
            try
            {
                protectedStoreKey = ProtectStoreKey(storeKey, profile);
                header = SerializeHeader(profile, storeIdentifier, protectedStoreKey);
                PublishImmutable(root, profile, HeaderPendingSegment, HeaderFinalSegment, header);
            }
            finally
            {
                if (header is not null)
                {
                    CryptographicOperations.ZeroMemory(header);
                }

                if (protectedStoreKey is not null)
                {
                    CryptographicOperations.ZeroMemory(protectedStoreKey);
                }
            }

            store = new WindowsDurableProfileStore(root, profile, ownerLock, storeIdentifier, storeKey);
            root = null;
            ownerLock = null;
            storeIdentifier = null;
            storeKey = null;
            if (!store.TryPublishProfileGeneration(initialGeneration, out _))
            {
                throw new InvalidDataException("The initial durable generation could not be published.");
            }

            return store;
        }
        catch
        {
            store?.Dispose();
            throw;
        }
        finally
        {
            ownerLock?.Dispose();
            root?.Dispose();
            if (storeIdentifier is not null)
            {
                CryptographicOperations.ZeroMemory(storeIdentifier);
            }

            if (storeKey is not null)
            {
                CryptographicOperations.ZeroMemory(storeKey);
            }
        }
    }

    /// <summary>
    /// Reopens and fully replays an existing immutable record chain. Pending,
    /// missing-first, gap, malformed, cross-store, cross-profile, stale-key,
    /// or authenticator-drift artifacts fail closed.
    /// </summary>
    internal static WindowsDurableProfileStore OpenExisting(
        SafeFileHandle pinnedDirectory,
        DurableProductionProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        RequireWindows();
        DurableRootCapability? root = null;
        SafeFileHandle? ownerLock = null;
        byte[]? decryptedKey = null;
        byte[]? header = null;
        byte[]? storeIdentifier = null;
        ParsedHeader? parsedHeader = null;
        WindowsDurableProfileStore? store = null;
        try
        {
            root = DurableRootCapability.Acquire(pinnedDirectory, profile);
            ownerLock = OpenExclusiveOwnerLock(root, profile);
            RequireAbsent(root, profile, HeaderPendingSegment);
            header = ReadImmutable(root, profile, HeaderFinalSegment, MaximumHeaderBytes);
            parsedHeader = ParseHeader(header, profile);
            decryptedKey = UnprotectStoreKey(parsedHeader.ProtectedStoreKey, profile);
            storeIdentifier = parsedHeader.DetachStoreIdentifier();
            store = new WindowsDurableProfileStore(
                root,
                profile,
                ownerLock,
                storeIdentifier,
                decryptedKey);
            root = null;
            ownerLock = null;
            decryptedKey = null;
            storeIdentifier = null;
            store.ReplayExistingChain();
            return store;
        }
        catch
        {
            store?.Dispose();
            throw;
        }
        finally
        {
            ownerLock?.Dispose();
            root?.Dispose();
            if (decryptedKey is not null)
            {
                CryptographicOperations.ZeroMemory(decryptedKey);
            }

            if (header is not null)
            {
                CryptographicOperations.ZeroMemory(header);
            }

            if (storeIdentifier is not null)
            {
                CryptographicOperations.ZeroMemory(storeIdentifier);
            }

            parsedHeader?.Dispose();
        }
    }

    internal long CurrentGeneration
    {
        get
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                return _state.Generation;
            }
        }
    }

    internal TypedHash ProfileDigest => _profile.ProfileDigest;

    internal bool TryPublishProfileGeneration(
        long nextGeneration,
        out DurableProfileStoreReceipt? receipt)
    {
        receipt = null;
        lock (_gate)
        {
            if (_disposed ||
                nextGeneration <= 0 ||
                (_state.Sequence != 0 && nextGeneration != checked(_state.Generation + 1)))
            {
                return false;
            }

            try
            {
                PublishRecord(DurableProfileStoreRecordKind.ProfileGeneration, nextGeneration, null);
                receipt = CreateReceipt(DurableProfileStoreRecordKind.ProfileGeneration);
                return true;
            }
            catch
            {
                DisposeAfterFailure();
                return false;
            }
        }
    }

    /// <summary>
    /// Durably consumes one exact 32-byte nonce for the current profile and
    /// generation. The nonce is not returned in a receipt and no receipt is an
    /// authority token.
    /// </summary>
    internal bool TryConsumeNonce(
        ReadOnlySpan<byte> nonce,
        out DurableProfileStoreReceipt? receipt)
    {
        receipt = null;
        if (nonce.Length != NonceLength)
        {
            return false;
        }

        var nonceCopy = nonce.ToArray();
        try
        {
            lock (_gate)
            {
                if (_disposed || _state.Sequence == 0)
                {
                    return false;
                }

                using var nonceFingerprint = CreateNonceFingerprint(
                    _hmacKey ?? throw new ObjectDisposedException(nameof(WindowsDurableProfileStore)),
                    _storeIdentifier,
                    _profile,
                    _state.Generation,
                    nonceCopy);
                if (_consumedCurrentGenerationNonces.Contains(nonceFingerprint))
                {
                    return false;
                }

                var persistedFingerprint = nonceFingerprint.CopyBytes();
                try
                {
                    PublishRecord(
                        DurableProfileStoreRecordKind.NonceConsumption,
                        _state.Generation,
                        persistedFingerprint);
                    receipt = CreateReceipt(DurableProfileStoreRecordKind.NonceConsumption);
                    return true;
                }
                catch
                {
                    DisposeAfterFailure();
                    return false;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(persistedFingerprint);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(nonceCopy);
        }
    }

    /// <summary>
    /// Validates only a volatile, immutable, non-authority observation from
    /// this exact live store instance. Reopening the store produces a new issuer
    /// epoch, so all receipts from an earlier instance fail this check.
    /// </summary>
    internal bool IsCurrentReceipt(DurableProfileStoreReceipt? receipt)
    {
        lock (_gate)
        {
            return !_disposed &&
                receipt is not null &&
                receipt.Sequence == _state.Sequence &&
                receipt.Generation == _state.Generation &&
                _storeIdentifierDigest.FixedTimeEquals(receipt.StoreIdentifierDigest) &&
                _profile.ProfileDigest.FixedTimeEquals(receipt.ProfileDigest) &&
                _issuerEpochDigest.FixedTimeEquals(receipt.IssuerEpochDigest) &&
                TypedHash.Compute(
                    "vfxcomposer.durable-profile-sealed-tip/1",
                    _state.SealedTipAuthenticator).FixedTimeEquals(receipt.SealedTipDigest);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ClearConsumedCurrentGenerationNonces();
            if (_hmacKey is { } key)
            {
                CryptographicOperations.ZeroMemory(key);
                _hmacKey = null;
            }

            if (_issuerEpoch is { } epoch)
            {
                CryptographicOperations.ZeroMemory(epoch);
                _issuerEpoch = null;
            }

            CryptographicOperations.ZeroMemory(_storeIdentifier);
            _state.ZeroAuthenticator();
            _ownerLock.Dispose();
            _root.Dispose();
        }
    }

    private void ReplayExistingChain()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _root.Revalidate();
            for (ulong sequence = 1; sequence <= MaximumRecordCount; sequence++)
            {
                byte[]? serializedRecord = null;
                byte[]? serializedTip = null;
                try
                {
                    RequireAbsent(_root, _profile, RecordPendingSegment(sequence));
                    RequireAbsent(_root, _profile, TipPendingSegment(sequence));
                    var hasRecord = TryReadImmutable(
                        _root,
                        _profile,
                        RecordFinalSegment(sequence),
                        MaximumRecordBytes,
                        out serializedRecord);
                    var hasTip = TryReadImmutable(
                        _root,
                        _profile,
                        TipFinalSegment(sequence),
                        MaximumTipBytes,
                        out serializedTip);
                    var disposition = GetReplayDisposition(
                        sequence,
                        ClassifyRecordTipArtifacts(
                                recordPending: false,
                                recordFinal: hasRecord,
                                tipPending: false,
                                tipFinal: hasTip));
                    switch (disposition)
                    {
                        case DurableReplayDisposition.Finish:
                            EnsureNoLaterArtifacts(checked(sequence + 1));
                            return;

                        case DurableReplayDisposition.Apply:
                        case DurableReplayDisposition.ApplyAndFinish:
                            var key = _hmacKey ?? throw new ObjectDisposedException(nameof(WindowsDurableProfileStore));
                            using (var record = ParseAndAuthenticateRecord(
                                       key,
                                       _storeIdentifier,
                                       _profile,
                                       serializedRecord!))
                            using (var tip = ParseAndAuthenticateSealedTip(
                                       key,
                                       _storeIdentifier,
                                       _profile,
                                       serializedTip!))
                            {
                                ApplyRecordAndSealedTip(record, tip);
                            }

                            if (disposition == DurableReplayDisposition.ApplyAndFinish)
                            {
                                EnsureNoLaterArtifacts(checked(MaximumRecordCount + 1));
                                return;
                            }

                            break;

                        case DurableReplayDisposition.MissingFirst:
                            throw new InvalidDataException("The durable store has no first record-and-tip pair.");

                        case DurableReplayDisposition.RejectIncomplete:
                            throw new InvalidDataException("The durable store record-and-tip pair is incomplete.");

                        case DurableReplayDisposition.RejectPending:
                            throw new InvalidDataException("A pending durable record-and-tip artifact was found.");

                        default:
                            throw new InvalidDataException("The durable store sequence is invalid.");
                    }
                }
                finally
                {
                    if (serializedRecord is not null)
                    {
                        CryptographicOperations.ZeroMemory(serializedRecord);
                    }

                    if (serializedTip is not null)
                    {
                        CryptographicOperations.ZeroMemory(serializedTip);
                    }
                }
            }

            throw new InvalidDataException("The durable store exceeded its bounded record count.");
        }
    }

    private void EnsureNoLaterArtifacts(ulong firstUnexpectedSequence)
    {
        var firstOutOfRange = checked(MaximumRecordCount + 1UL);
        if (firstUnexpectedSequence == 0 || firstUnexpectedSequence > firstOutOfRange)
        {
            throw new InvalidDataException("The durable store replay boundary is invalid.");
        }

        for (var sequence = firstUnexpectedSequence; sequence <= MaximumRecordCount; sequence++)
        {
            RequireAbsent(_root, _profile, RecordPendingSegment(sequence));
            RequireAbsent(_root, _profile, RecordFinalSegment(sequence));
            RequireAbsent(_root, _profile, TipPendingSegment(sequence));
            RequireAbsent(_root, _profile, TipFinalSegment(sequence));
        }

        // Replay never accepts an artifact outside its fixed sequence space.
        // Probing the first out-of-range name catches an attempted 129th pair
        // without path enumeration or an unbounded suffix scan.
        RequireAbsent(_root, _profile, RecordPendingSegment(firstOutOfRange));
        RequireAbsent(_root, _profile, RecordFinalSegment(firstOutOfRange));
        RequireAbsent(_root, _profile, TipPendingSegment(firstOutOfRange));
        RequireAbsent(_root, _profile, TipFinalSegment(firstOutOfRange));
    }

    /// <summary>
    /// Classifies a sequence's immutable record/tip topology. Production
    /// replay and synthetic crash-state tests use this same closed state
    /// machine; no test-only persistence path exists.
    /// </summary>
    internal static DurableRecordTipArtifactState ClassifyRecordTipArtifacts(
        bool recordPending,
        bool recordFinal,
        bool tipPending,
        bool tipFinal)
    {
        if (recordPending || tipPending)
        {
            return DurableRecordTipArtifactState.Pending;
        }

        if (recordFinal && tipFinal)
        {
            return DurableRecordTipArtifactState.Complete;
        }

        return !recordFinal && !tipFinal
            ? DurableRecordTipArtifactState.Absent
            : DurableRecordTipArtifactState.Incomplete;
    }

    /// <summary>
    /// The single bounded sequence contract used by both publication and
    /// replay. A value outside this closed range can never become store state.
    /// </summary>
    internal static bool IsRecordSequenceInBounds(ulong sequence) =>
        sequence is > 0 and <= MaximumRecordCount;

    /// <summary>
    /// Evaluates one persisted pair without touching the file system. Replay
    /// consumes this same disposition, which keeps synthetic topology coverage
    /// and the live bounded state machine on one production rule.
    /// </summary>
    internal static DurableReplayDisposition GetReplayDisposition(
        ulong sequence,
        DurableRecordTipArtifactState artifactState)
    {
        if (!IsRecordSequenceInBounds(sequence))
        {
            return DurableReplayDisposition.RejectOutOfRange;
        }

        return artifactState switch
        {
            DurableRecordTipArtifactState.Pending => DurableReplayDisposition.RejectPending,
            DurableRecordTipArtifactState.Incomplete => DurableReplayDisposition.RejectIncomplete,
            DurableRecordTipArtifactState.Absent when sequence == 1 => DurableReplayDisposition.MissingFirst,
            DurableRecordTipArtifactState.Absent => DurableReplayDisposition.Finish,
            DurableRecordTipArtifactState.Complete when sequence == MaximumRecordCount =>
                DurableReplayDisposition.ApplyAndFinish,
            DurableRecordTipArtifactState.Complete => DurableReplayDisposition.Apply,
            _ => DurableReplayDisposition.RejectOutOfRange,
        };
    }

    /// <summary>
    /// Validates the authenticated pair boundary before state mutation. The
    /// live replay path and direct codec consumers share this exact binding so
    /// a valid HMAC alone cannot substitute a record or sealed tip from another
    /// sequence, generation, kind, or chain predecessor.
    /// </summary>
    internal static void ValidateRecordAndSealedTipBinding(
        AuthenticatedRecord record,
        AuthenticatedSealedTip tip,
        ulong previousSequence,
        ReadOnlySpan<byte> previousAuthenticator)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(tip);
        if ((previousSequence != 0 && !IsRecordSequenceInBounds(previousSequence)) ||
            previousSequence >= MaximumRecordCount ||
            previousAuthenticator.Length != AuthenticatorLength ||
            !IsRecordSequenceInBounds(record.Sequence) ||
            record.PreviousAuthenticator.Length != AuthenticatorLength ||
            record.Authenticator.Length != AuthenticatorLength ||
            tip.RecordAuthenticator.Length != AuthenticatorLength ||
            tip.Authenticator.Length != AuthenticatorLength ||
            record.Sequence != previousSequence + 1 ||
            !CryptographicOperations.FixedTimeEquals(record.PreviousAuthenticator, previousAuthenticator) ||
            tip.Sequence != record.Sequence ||
            tip.Generation != record.Generation ||
            tip.Kind != record.Kind ||
            !CryptographicOperations.FixedTimeEquals(tip.RecordAuthenticator, record.Authenticator))
        {
            throw new InvalidDataException("The durable record-and-tip chain is not contiguous.");
        }
    }

    /// <summary>
    /// Validates the semantic transition after pair authentication and before
    /// mutable replay state changes. It is deliberately independent of file
    /// access so publication and replay share one bounded generation/nonce
    /// contract.
    /// </summary>
    internal static void ValidateRecordStateTransition(
        ulong previousSequence,
        long previousGeneration,
        DurableProfileStoreRecordKind kind,
        long generation,
        byte[]? nonceFingerprint)
    {
        if ((previousSequence != 0 && !IsRecordSequenceInBounds(previousSequence)) || generation <= 0)
        {
            throw new InvalidDataException("The durable record state transition is invalid.");
        }

        switch (kind)
        {
            case DurableProfileStoreRecordKind.ProfileGeneration:
                if (nonceFingerprint is not null ||
                    (previousSequence != 0 &&
                     (previousGeneration == long.MaxValue || generation != previousGeneration + 1)))
                {
                    throw new InvalidDataException("The durable profile generation is not monotonic.");
                }

                return;

            case DurableProfileStoreRecordKind.NonceConsumption:
                if (previousSequence == 0 ||
                    generation != previousGeneration ||
                    nonceFingerprint is null ||
                    nonceFingerprint.Length != AuthenticatorLength)
                {
                    throw new InvalidDataException("The durable nonce record is invalid or replayed.");
                }

                return;

            default:
                throw new InvalidDataException("The durable record kind is unknown.");
        }
    }

    private void PublishRecord(
        DurableProfileStoreRecordKind kind,
        long generation,
        byte[]? nonceFingerprint)
    {
        ThrowIfDisposed();
        _root.Revalidate();
        if (_state.Sequence >= MaximumRecordCount)
        {
            throw new InvalidDataException("The durable store record sequence is exhausted.");
        }

        var nextSequence = _state.Sequence + 1;
        if (!IsRecordSequenceInBounds(nextSequence))
        {
            throw new InvalidDataException("The durable store record sequence is exhausted.");
        }

        var key = _hmacKey ?? throw new ObjectDisposedException(nameof(WindowsDurableProfileStore));
        byte[]? record = null;
        byte[]? finalRecord = null;
        byte[]? sealedTip = null;
        byte[]? finalTip = null;
        try
        {
            record = SerializeRecord(
                key,
                _storeIdentifier,
                _profile,
                kind,
                nextSequence,
                generation,
                _state.Authenticator,
                nonceFingerprint);
            PublishImmutable(
                _root,
                _profile,
                RecordPendingSegment(nextSequence),
                RecordFinalSegment(nextSequence),
                record);
            finalRecord = ReadImmutable(
                _root,
                _profile,
                RecordFinalSegment(nextSequence),
                MaximumRecordBytes);
            using var parsedRecord = ParseAndAuthenticateRecord(
                key,
                _storeIdentifier,
                _profile,
                finalRecord!);
            sealedTip = SerializeSealedTip(
                key,
                _storeIdentifier,
                _profile,
                parsedRecord.Kind,
                parsedRecord.Sequence,
                parsedRecord.Generation,
                parsedRecord.Authenticator);
            PublishImmutable(
                _root,
                _profile,
                TipPendingSegment(nextSequence),
                TipFinalSegment(nextSequence),
                sealedTip);
            finalTip = ReadImmutable(
                _root,
                _profile,
                TipFinalSegment(nextSequence),
                MaximumTipBytes);
            using var parsedTip = ParseAndAuthenticateSealedTip(
                key,
                _storeIdentifier,
                _profile,
                finalTip!);
            ApplyRecordAndSealedTip(parsedRecord, parsedTip);
        }
        finally
        {
            if (record is not null)
            {
                CryptographicOperations.ZeroMemory(record);
            }
            if (finalRecord is not null)
            {
                CryptographicOperations.ZeroMemory(finalRecord);
            }

            if (sealedTip is not null)
            {
                CryptographicOperations.ZeroMemory(sealedTip);
            }

            if (finalTip is not null)
            {
                CryptographicOperations.ZeroMemory(finalTip);
            }
        }
    }

    private void ApplyRecordAndSealedTip(AuthenticatedRecord record, AuthenticatedSealedTip tip)
    {
        ValidateRecordAndSealedTipBinding(record, tip, _state.Sequence, _state.Authenticator);
        ValidateRecordStateTransition(
            _state.Sequence,
            _state.Generation,
            record.Kind,
            record.Generation,
            record.NonceFingerprint);

        switch (record.Kind)
        {
            case DurableProfileStoreRecordKind.ProfileGeneration:
                ClearConsumedCurrentGenerationNonces();
                break;

            case DurableProfileStoreRecordKind.NonceConsumption:
                NonceFingerprint? fingerprint = new NonceFingerprint(record.NonceFingerprint!.ToArray());
                try
                {
                    if (!_consumedCurrentGenerationNonces.Add(fingerprint))
                    {
                        throw new InvalidDataException("The durable nonce record is invalid or replayed.");
                    }

                    fingerprint = null;
                }
                finally
                {
                    fingerprint?.Dispose();
                }

                break;

            default:
                throw new InvalidDataException("The durable record kind is unknown.");
        }

        _state.Replace(record.Sequence, record.Generation, record.Authenticator, tip.Authenticator);
    }

    internal static AuthenticatedRecord ParseAndAuthenticateRecord(
        byte[] key,
        byte[] storeIdentifier,
        DurableProductionProfile profile,
        byte[] serialized)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(storeIdentifier);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(serialized);
        if (key.Length != StoreKeyLength || storeIdentifier.Length != StoreIdentifierLength)
        {
            throw new ArgumentException("The durable record authentication inputs are invalid.");
        }

        if (serialized.Length is < 128 or > MaximumRecordBytes)
        {
            throw new InvalidDataException("The durable record length is invalid.");
        }

        var signedLength = serialized.Length - AuthenticatorLength;
        byte[]? suppliedAuthenticator = serialized.AsSpan(signedLength, AuthenticatorLength).ToArray();
        byte[]? profileDigest = null;
        byte[]? previousAuthenticator = null;
        byte[]? payload = null;
        byte[]? nonceFingerprint = null;
        try
        {
            var expectedAuthenticator = ComputeRecordAuthenticator(
                key,
                storeIdentifier,
                serialized.AsSpan(0, signedLength));
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(suppliedAuthenticator!, expectedAuthenticator))
                {
                    throw new InvalidDataException("The durable record authenticator is invalid.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedAuthenticator);
            }

            var offset = 0;
            RequireExactMagic(serialized, ref offset, RecordMagic);
            if (ReadUInt32(serialized, ref offset) != RecordVersion)
            {
                throw new InvalidDataException("The durable record version is invalid.");
            }

            var kind = ReadRecordKind(serialized, ref offset);
            var sequence = ReadUInt64(serialized, ref offset);
            var generation = ReadInt64(serialized, ref offset);
            var profileType = ReadUtf8(serialized, ref offset, 96);
            profileDigest = ReadExactBytes(serialized, ref offset, AuthenticatorLength);
            previousAuthenticator = ReadExactBytes(serialized, ref offset, AuthenticatorLength);
            var payloadLength = ReadUInt32(serialized, ref offset);
            if (payloadLength > MaximumRecordBytes ||
                offset > signedLength ||
                payloadLength != signedLength - offset)
            {
                throw new InvalidDataException("The durable record payload length is invalid.");
            }

            payload = ReadExactBytes(serialized, ref offset, checked((int)payloadLength));
            var expectedProfileDigest = GetProfileDigestBytes(profile);
            try
            {
                if (offset != signedLength ||
                    !IsRecordSequenceInBounds(sequence) ||
                    generation <= 0 ||
                    !string.Equals(profileType, DurableProductionProfile.ProfileDigestType, StringComparison.Ordinal) ||
                    !CryptographicOperations.FixedTimeEquals(profileDigest, expectedProfileDigest))
                {
                    throw new InvalidDataException("The durable record profile binding is invalid.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedProfileDigest);
            }

            switch (kind)
            {
                case DurableProfileStoreRecordKind.ProfileGeneration when payload.Length == 0:
                    break;
                case DurableProfileStoreRecordKind.NonceConsumption when payload.Length == AuthenticatorLength:
                    nonceFingerprint = payload;
                    payload = null;
                    break;
                default:
                    throw new InvalidDataException("The durable record payload kind is invalid.");
            }

            var record = new AuthenticatedRecord(
                kind,
                sequence,
                generation,
                previousAuthenticator!,
                suppliedAuthenticator!,
                nonceFingerprint);
            previousAuthenticator = null;
            suppliedAuthenticator = null;
            nonceFingerprint = null;
            return record;
        }
        finally
        {
            if (suppliedAuthenticator is not null)
            {
                CryptographicOperations.ZeroMemory(suppliedAuthenticator);
            }
            if (profileDigest is not null)
            {
                CryptographicOperations.ZeroMemory(profileDigest);
            }

            if (previousAuthenticator is not null)
            {
                CryptographicOperations.ZeroMemory(previousAuthenticator);
            }

            if (payload is not null)
            {
                CryptographicOperations.ZeroMemory(payload);
            }

            if (nonceFingerprint is not null)
            {
                CryptographicOperations.ZeroMemory(nonceFingerprint);
            }
        }
    }

    /// <summary>
    /// Creates the immutable sealed tip paired with one final record. The tip
    /// is deliberately authenticated in a domain distinct from record HMACs
    /// and binds the final record authenticator, profile, sequence, generation
    /// and kind. A surviving tip therefore exposes a deleted record suffix.
    /// </summary>
    internal static byte[] SerializeSealedTip(
        byte[] key,
        byte[] storeIdentifier,
        DurableProductionProfile profile,
        DurableProfileStoreRecordKind kind,
        ulong sequence,
        long generation,
        byte[] finalRecordAuthenticator)
    {
        if (key.Length != StoreKeyLength ||
            storeIdentifier.Length != StoreIdentifierLength ||
            finalRecordAuthenticator.Length != AuthenticatorLength ||
            !IsRecordSequenceInBounds(sequence) ||
            generation <= 0 ||
            kind is not DurableProfileStoreRecordKind.ProfileGeneration and
                not DurableProfileStoreRecordKind.NonceConsumption)
        {
            throw new ArgumentException("The durable sealed-tip inputs are invalid.");
        }

        using var writer = new SensitiveBufferWriter(192);
        var profileDigest = GetProfileDigestBytes(profile);
        try
        {
            writer.Write(TipMagic);
            AppendUInt32(writer, RecordVersion);
            writer.GetSpan(1)[0] = (byte)kind;
            writer.Advance(1);
            AppendUInt64(writer, sequence);
            AppendInt64(writer, generation);
            AppendUtf8(writer, DurableProductionProfile.ProfileDigestType);
            writer.Write(profileDigest);
            writer.Write(finalRecordAuthenticator);
            byte[]? signedBytes = writer.WrittenSpan.ToArray();
            try
            {
                byte[]? authenticator = null;
                try
                {
                    authenticator = ComputeSealedTipAuthenticator(key, storeIdentifier, signedBytes!);
                    writer.Write(authenticator);
                    return writer.WrittenSpan.ToArray();
                }
                finally
                {
                    if (authenticator is not null)
                    {
                        CryptographicOperations.ZeroMemory(authenticator);
                    }
                }
            }
            finally
            {
                if (signedBytes is not null)
                {
                    CryptographicOperations.ZeroMemory(signedBytes);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(profileDigest);
            CryptographicOperations.ZeroMemory(writer.WrittenSpan);
        }
    }

    internal static AuthenticatedSealedTip ParseAndAuthenticateSealedTip(
        byte[] key,
        byte[] storeIdentifier,
        DurableProductionProfile profile,
        byte[] serialized)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(storeIdentifier);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(serialized);
        if (key.Length != StoreKeyLength || storeIdentifier.Length != StoreIdentifierLength)
        {
            throw new ArgumentException("The durable sealed-tip authentication inputs are invalid.");
        }

        if (serialized.Length is < 128 or > MaximumTipBytes)
        {
            throw new InvalidDataException("The durable sealed-tip length is invalid.");
        }

        var signedLength = serialized.Length - AuthenticatorLength;
        byte[]? suppliedAuthenticator = serialized.AsSpan(signedLength, AuthenticatorLength).ToArray();
        byte[]? profileDigest = null;
        byte[]? recordAuthenticator = null;
        try
        {
            var expectedAuthenticator = ComputeSealedTipAuthenticator(
                key,
                storeIdentifier,
                serialized.AsSpan(0, signedLength));
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(suppliedAuthenticator!, expectedAuthenticator))
                {
                    throw new InvalidDataException("The durable sealed-tip authenticator is invalid.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedAuthenticator);
            }

            var offset = 0;
            RequireExactMagic(serialized, ref offset, TipMagic);
            if (ReadUInt32(serialized, ref offset) != RecordVersion)
            {
                throw new InvalidDataException("The durable sealed-tip version is invalid.");
            }

            var kind = ReadRecordKind(serialized, ref offset);
            var sequence = ReadUInt64(serialized, ref offset);
            var generation = ReadInt64(serialized, ref offset);
            var profileType = ReadUtf8(serialized, ref offset, 96);
            profileDigest = ReadExactBytes(serialized, ref offset, AuthenticatorLength);
            recordAuthenticator = ReadExactBytes(serialized, ref offset, AuthenticatorLength);
            if (offset != signedLength ||
                !IsRecordSequenceInBounds(sequence) ||
                generation <= 0 ||
                kind is not DurableProfileStoreRecordKind.ProfileGeneration and
                    not DurableProfileStoreRecordKind.NonceConsumption ||
                !string.Equals(profileType, DurableProductionProfile.ProfileDigestType, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The durable sealed-tip fields are invalid.");
            }

            var expectedProfileDigest = GetProfileDigestBytes(profile);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(profileDigest, expectedProfileDigest))
                {
                    throw new InvalidDataException("The durable sealed-tip profile binding is invalid.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedProfileDigest);
            }

            var tip = new AuthenticatedSealedTip(
                kind,
                sequence,
                generation,
                recordAuthenticator!,
                suppliedAuthenticator!);
            recordAuthenticator = null;
            suppliedAuthenticator = null;
            return tip;
        }
        finally
        {
            if (suppliedAuthenticator is not null)
            {
                CryptographicOperations.ZeroMemory(suppliedAuthenticator);
            }
            if (profileDigest is not null)
            {
                CryptographicOperations.ZeroMemory(profileDigest);
            }

            if (recordAuthenticator is not null)
            {
                CryptographicOperations.ZeroMemory(recordAuthenticator);
            }
        }
    }

    internal static byte[] ComputeSealedTipAuthenticator(
        byte[] key,
        byte[] storeIdentifier,
        ReadOnlySpan<byte> signedTip)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        hmac.AppendData(SealedTipHmacDomain);
        hmac.AppendData(storeIdentifier);
        hmac.AppendData(signedTip);
        return hmac.GetHashAndReset();
    }

    private static NonceFingerprint CreateNonceFingerprint(
        byte[] key,
        byte[] storeIdentifier,
        DurableProductionProfile profile,
        long generation,
        ReadOnlySpan<byte> nonce)
    {
        byte[]? fingerprintBytes = null;
        try
        {
            fingerprintBytes = ComputeNonceFingerprint(
                key,
                storeIdentifier,
                profile,
                generation,
                nonce);
            var fingerprint = new NonceFingerprint(fingerprintBytes);
            fingerprintBytes = null;
            return fingerprint;
        }
        finally
        {
            if (fingerprintBytes is not null)
            {
                CryptographicOperations.ZeroMemory(fingerprintBytes);
            }
        }
    }

    /// <summary>
    /// Computes the persisted nonce fingerprint under its dedicated HMAC domain.
    /// It is a pure production codec operation; callers take ownership of the
    /// returned authenticator and must clear it after use.
    /// </summary>
    internal static byte[] ComputeNonceFingerprint(
        byte[] key,
        byte[] storeIdentifier,
        DurableProductionProfile profile,
        long generation,
        ReadOnlySpan<byte> nonce)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(storeIdentifier);
        ArgumentNullException.ThrowIfNull(profile);
        if (key.Length != StoreKeyLength ||
            storeIdentifier.Length != StoreIdentifierLength ||
            generation <= 0 ||
            nonce.Length != NonceLength)
        {
            throw new ArgumentException("The durable nonce fingerprint input is invalid.");
        }

        var profileDigest = GetProfileDigestBytes(profile);
        Span<byte> generationBytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(generationBytes, generation);
        try
        {
            using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
            hmac.AppendData(NonceFingerprintHmacDomain);
            hmac.AppendData(storeIdentifier);
            hmac.AppendData(profileDigest);
            hmac.AppendData(generationBytes);
            hmac.AppendData(nonce);
            return hmac.GetHashAndReset();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(profileDigest);
            CryptographicOperations.ZeroMemory(generationBytes);
        }
    }

    private void ClearConsumedCurrentGenerationNonces()
    {
        foreach (var fingerprint in _consumedCurrentGenerationNonces)
        {
            fingerprint.Dispose();
        }

        _consumedCurrentGenerationNonces.Clear();
    }

    private DurableProfileStoreReceipt CreateReceipt(DurableProfileStoreRecordKind kind) =>
        new(
            _storeIdentifierDigest,
            _profile.ProfileDigest,
            _issuerEpochDigest,
            TypedHash.Compute("vfxcomposer.durable-profile-sealed-tip/1", _state.SealedTipAuthenticator),
            _state.Sequence,
            _state.Generation,
            kind);

    private void DisposeAfterFailure()
    {
        if (!_disposed)
        {
            Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsDurableProfileStore));
        }
    }

    private static void PublishImmutable(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string pendingSegment,
        string finalSegment,
        byte[] bytes)
    {
        if (bytes.Length is 0 or > MaximumHeaderBytes)
        {
            throw new InvalidDataException("The durable immutable payload is out of bounds.");
        }

        RequireFixedSegment(pendingSegment);
        RequireFixedSegment(finalSegment);
        root.Revalidate();
        RequireAbsent(root, profile, pendingSegment);
        RequireAbsent(root, profile, finalSegment);
        using (var pending = CreateNewRelativeFile(root, profile, pendingSegment))
        {
            WriteAndFlushExact(pending, bytes);
            ValidateStoreFile(pending, profile);
        }

        using (var pending = OpenExistingRelativeFile(root, profile, pendingSegment))
        {
            var pendingBytes = ReadExactBytesFromFile(pending, profile, bytes.Length);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(pendingBytes, bytes))
                {
                    throw new InvalidDataException("The durable pending artifact did not read back exactly.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(pendingBytes);
            }

            RenameNoReplaceRelativeToRoot(pending, root, finalSegment);
            FlushOrThrow(pending);
        }

        var finalBytes = ReadImmutable(root, profile, finalSegment, bytes.Length);
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(finalBytes, bytes))
            {
                throw new InvalidDataException("The durable published artifact did not read back exactly.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(finalBytes);
        }
    }

    private static void RequireAbsent(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment)
    {
        if (TryOpenExistingRelativeFile(root, profile, segment, out var existing))
        {
            existing!.Dispose();
            throw new InvalidDataException("A durable store artifact already exists.");
        }
    }

    private static byte[] ReadImmutable(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment,
        int maximumLength)
    {
        using var file = OpenExistingRelativeFile(root, profile, segment);
        return ReadBoundedBytesFromFile(file, profile, maximumLength);
    }

    private static bool TryReadImmutable(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment,
        int maximumLength,
        out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (!TryOpenExistingRelativeFile(root, profile, segment, out var file))
        {
            return false;
        }

        using (file)
        {
            bytes = ReadBoundedBytesFromFile(file!, profile, maximumLength);
            return true;
        }
    }

    private static byte[] ReadBoundedBytesFromFile(
        SafeFileHandle file,
        DurableProductionProfile profile,
        int maximumLength)
    {
        var before = ValidateStoreFile(file, profile);
        if (before.ByteLength == 0 || before.ByteLength > (ulong)maximumLength || before.ByteLength > int.MaxValue)
        {
            throw new InvalidDataException("The durable file length is invalid.");
        }

        byte[]? bytes = null;
        try
        {
            bytes = ReadExactBytesFromFile(file, profile, checked((int)before.ByteLength));
            var after = ValidateStoreFile(file, profile);
            if (!before.FixedEquals(after))
            {
                throw new InvalidDataException("The durable file identity drifted while reading.");
            }

            var result = bytes;
            bytes = null;
            return result;
        }
        finally
        {
            if (bytes is not null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    private static byte[] ReadExactBytesFromFile(
        SafeFileHandle file,
        DurableProductionProfile profile,
        int expectedLength)
    {
        if (expectedLength < 0 ||
            !SetFilePointerEx(file, 0, out var position, 0) ||
            position != 0)
        {
            throw new InvalidDataException("The durable file could not be rewound.");
        }

        byte[]? bytes = new byte[expectedLength];
        try
        {
            if (!ReadFile(file, bytes, checked((uint)bytes.Length), out var bytesRead, IntPtr.Zero) ||
                bytesRead != bytes.Length)
            {
                throw new InvalidDataException("The durable file did not provide its exact length.");
            }

            var probe = new byte[1];
            try
            {
                if (!ReadFile(file, probe, 1, out var probeRead, IntPtr.Zero) || probeRead != 0)
                {
                    throw new InvalidDataException("The durable file has trailing data.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(probe);
            }

            _ = profile;
            var result = bytes;
            bytes = null;
            return result;
        }
        finally
        {
            if (bytes is not null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    private static void WriteAndFlushExact(SafeFileHandle file, byte[] bytes)
    {
        if (!WriteFile(file, bytes, checked((uint)bytes.Length), out var bytesWritten, IntPtr.Zero) ||
            bytesWritten != bytes.Length)
        {
            throw new InvalidDataException("The durable file write was incomplete.");
        }

        FlushOrThrow(file);
    }

    private static void FlushOrThrow(SafeFileHandle file)
    {
        if (!FlushFileBuffers(file))
        {
            throw new InvalidDataException("The durable file flush failed.");
        }
    }

    private static SafeFileHandle OpenExclusiveOwnerLock(
        DurableRootCapability root,
        DurableProductionProfile profile)
    {
        RequireFixedSegment(OwnerLockSegment);
        root.Revalidate();
        var file = OpenOrCreateRelativeFile(root, profile, OwnerLockSegment, FileOpenIf, shareAccess: 0);
        try
        {
            _ = ValidateStoreFile(file, profile);
            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private static SafeFileHandle CreateNewRelativeFile(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment) =>
        OpenOrCreateRelativeFile(root, profile, segment, FileCreate, FileShareRead);

    private static SafeFileHandle OpenExistingRelativeFile(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment)
    {
        if (!TryOpenExistingRelativeFile(root, profile, segment, out var file) || file is null)
        {
            throw new InvalidDataException("A required durable store artifact is missing.");
        }

        return file;
    }

    private static bool TryOpenExistingRelativeFile(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment,
        out SafeFileHandle? file)
    {
        RequireFixedSegment(segment);
        root.Revalidate();
        var rawHandle = IntPtr.Zero;
        var rootAddedRef = false;
        var nameBuffer = IntPtr.Zero;
        var unicodeBuffer = IntPtr.Zero;
        try
        {
            BuildRelativeObjectName(segment, out nameBuffer, out unicodeBuffer);
            root.Handle.DangerousAddRef(ref rootAddedRef);
            var attributes = BuildObjectAttributes(root.Handle.DangerousGetHandle(), unicodeBuffer, IntPtr.Zero);
            var status = NtOpenFile(
                out rawHandle,
                StoreReadOpenAccess,
                ref attributes,
                out _,
                FileShareRead,
                FileNonDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint);
            if (status is StatusObjectNameNotFound or StatusObjectPathNotFound)
            {
                CloseRawHandle(rawHandle);
                rawHandle = IntPtr.Zero;
                file = null;
                return false;
            }

            if (status != StatusSuccess || IsInvalidRawHandle(rawHandle))
            {
                throw new InvalidDataException("A durable store artifact could not be opened.");
            }

            file = new SafeFileHandle(rawHandle, ownsHandle: true);
            rawHandle = IntPtr.Zero;
            EnsureNonInheritable(file);
            _ = ValidateStoreFile(file, profile);
            return true;
        }
        catch
        {
            CloseRawHandle(rawHandle);
            throw;
        }
        finally
        {
            if (rootAddedRef)
            {
                root.Handle.DangerousRelease();
            }

            FreeRelativeObjectName(nameBuffer, unicodeBuffer);
        }
    }

    private static SafeFileHandle OpenOrCreateRelativeFile(
        DurableRootCapability root,
        DurableProductionProfile profile,
        string segment,
        uint createDisposition,
        uint shareAccess)
    {
        RequireFixedSegment(segment);
        root.Revalidate();
        var rawHandle = IntPtr.Zero;
        var rootAddedRef = false;
        var nameBuffer = IntPtr.Zero;
        var unicodeBuffer = IntPtr.Zero;
        var securityDescriptor = IntPtr.Zero;
        var descriptorBytes = profile.GetStoreFileSecurityDescriptorBytes();
        try
        {
            securityDescriptor = Marshal.AllocHGlobal(descriptorBytes.Length);
            Marshal.Copy(descriptorBytes, 0, securityDescriptor, descriptorBytes.Length);
            BuildRelativeObjectName(segment, out nameBuffer, out unicodeBuffer);
            root.Handle.DangerousAddRef(ref rootAddedRef);
            var attributes = BuildObjectAttributes(
                root.Handle.DangerousGetHandle(),
                unicodeBuffer,
                securityDescriptor);
            var status = NtCreateFile(
                out rawHandle,
                StoreCreateOpenAccess,
                ref attributes,
                out _,
                IntPtr.Zero,
                FileAttributeNormal,
                shareAccess,
                createDisposition,
                FileNonDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint,
                IntPtr.Zero,
                0);
            if (status != StatusSuccess || IsInvalidRawHandle(rawHandle))
            {
                throw new InvalidDataException("A durable store artifact could not be created.");
            }

            var file = new SafeFileHandle(rawHandle, ownsHandle: true);
            rawHandle = IntPtr.Zero;
            try
            {
                EnsureNonInheritable(file);
                _ = ValidateStoreFile(file, profile);
                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        catch
        {
            CloseRawHandle(rawHandle);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(descriptorBytes);
            if (rootAddedRef)
            {
                root.Handle.DangerousRelease();
            }

            if (securityDescriptor != IntPtr.Zero)
            {
                ZeroNativeBuffer(securityDescriptor, descriptorBytes.Length);
                Marshal.FreeHGlobal(securityDescriptor);
            }

            FreeRelativeObjectName(nameBuffer, unicodeBuffer);
        }
    }

    private static NativeObjectSnapshot ValidateStoreFile(
        SafeFileHandle file,
        DurableProductionProfile profile)
    {
        if (file.IsClosed || file.IsInvalid ||
            !IsLocalNtfsDisk(file) ||
            !TryReadObjectSnapshot(file, expectedDirectory: false, out var snapshot) ||
            !profile.MatchesExpectedStoreFileSecurityDescriptor(ReadSecurityDescriptor(file)))
        {
            throw new InvalidDataException("A durable store file does not match the protected profile.");
        }

        return snapshot;
    }

    private static void RenameNoReplaceRelativeToRoot(
        SafeFileHandle file,
        DurableRootCapability root,
        string finalSegment)
    {
        RequireFixedSegment(finalSegment);
        var rootAddedRef = false;
        var fileNameBuffer = IntPtr.Zero;
        byte[]? fileName = null;
        try
        {
            fileName = Encoding.Unicode.GetBytes(finalSegment);
            var prefixLength = checked((int)Marshal.OffsetOf<FileRenameInformationHeader>(
                nameof(FileRenameInformationHeader.FileNameLength)).ToInt64() + sizeof(uint));
            fileNameBuffer = Marshal.AllocHGlobal(checked(prefixLength + fileName.Length));
            var header = new FileRenameInformationHeader
            {
                ReplaceIfExists = false,
                RootDirectory = IntPtr.Zero,
                FileNameLength = checked((uint)fileName.Length),
            };
            Marshal.StructureToPtr(header, fileNameBuffer, false);
            root.Handle.DangerousAddRef(ref rootAddedRef);
            Marshal.WriteIntPtr(
                fileNameBuffer,
                checked((int)Marshal.OffsetOf<FileRenameInformationHeader>(
                    nameof(FileRenameInformationHeader.RootDirectory)).ToInt64()),
                root.Handle.DangerousGetHandle());
            Marshal.Copy(fileName, 0, IntPtr.Add(fileNameBuffer, prefixLength), fileName.Length);
            var status = NtSetInformationFile(
                file,
                out _,
                fileNameBuffer,
                checked((uint)(prefixLength + fileName.Length)),
                FileRenameInformationClass);
            if (status != StatusSuccess)
            {
                throw new InvalidDataException($"The durable store publish rename failed: 0x{status:X8}.");
            }
        }
        finally
        {
            if (rootAddedRef)
            {
                root.Handle.DangerousRelease();
            }

            if (fileNameBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(fileNameBuffer);
            }

            if (fileName is not null)
            {
                CryptographicOperations.ZeroMemory(fileName);
            }
        }
    }

    private static void BuildRelativeObjectName(
        string segment,
        out IntPtr nameBuffer,
        out IntPtr unicodeBuffer)
    {
        nameBuffer = Marshal.StringToHGlobalUni(segment);
        var name = new UnicodeString
        {
            Length = checked((ushort)(segment.Length * sizeof(char))),
            MaximumLength = checked((ushort)((segment.Length + 1) * sizeof(char))),
            Buffer = nameBuffer,
        };
        unicodeBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
        Marshal.StructureToPtr(name, unicodeBuffer, false);
    }

    private static ObjectAttributes BuildObjectAttributes(
        IntPtr rootDirectory,
        IntPtr objectName,
        IntPtr securityDescriptor) =>
        new()
        {
            Length = checked((uint)Marshal.SizeOf<ObjectAttributes>()),
            RootDirectory = rootDirectory,
            ObjectName = objectName,
            Attributes = ObjectCaseInsensitive | ObjectDontReparse,
            SecurityDescriptor = securityDescriptor,
            SecurityQualityOfService = IntPtr.Zero,
        };

    private static void FreeRelativeObjectName(IntPtr nameBuffer, IntPtr unicodeBuffer)
    {
        if (unicodeBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(unicodeBuffer);
        }

        if (nameBuffer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    private static void EnsureNonInheritable(SafeFileHandle file)
    {
        if (!SetHandleInformation(file, HandleFlagInherit, 0) ||
            !GetHandleInformation(file, out var handleFlags) ||
            (handleFlags & HandleFlagInherit) != 0)
        {
            throw new InvalidDataException("A durable handle is inheritable.");
        }
    }

    /// <summary>
    /// Reads the exact NT object-manager access mask granted to an already-open
    /// handle. This is intentionally a query of the pinned handle, never a
    /// path reopen or privilege-enabling fallback.
    /// </summary>
    internal static uint QueryGrantedAccess(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.IsClosed || handle.IsInvalid)
        {
            throw new InvalidDataException("The durable root handle is invalid.");
        }

        var expectedLength = checked((uint)Marshal.SizeOf<PublicObjectBasicInformation>());
        var status = NtQueryObject(
            handle,
            ObjectBasicInformationClass,
            out var objectInformation,
            expectedLength,
            out var returnedLength);
        if (status != StatusSuccess || returnedLength != expectedLength)
        {
            throw new InvalidDataException("The durable root granted-access query failed.");
        }

        return objectInformation.GrantedAccess;
    }

    private static bool IsLocalNtfsDisk(SafeFileHandle handle)
    {
        if (handle.IsClosed || handle.IsInvalid ||
            GetFileType(handle) != FileTypeDisk ||
            NtQueryVolumeInformationFile(
                handle,
                out var ioStatus,
                out var deviceInformation,
                checked((uint)Marshal.SizeOf<FileFsDeviceInformation>()),
                FileFsDeviceInformationClass) != StatusSuccess ||
            ioStatus.StatusOrPointer != IntPtr.Zero ||
            deviceInformation.DeviceType != FileDeviceDisk ||
            (deviceInformation.Characteristics & FileRemoteDevice) != 0)
        {
            return false;
        }

        var fileSystemName = new StringBuilder(32);
        return GetVolumeInformationByHandleW(
            handle,
            null,
            0,
            out _,
            out _,
            out _,
            fileSystemName,
            fileSystemName.Capacity) &&
            string.Equals(fileSystemName.ToString(), "NTFS", StringComparison.Ordinal);
    }

    private static bool TryReadObjectSnapshot(
        SafeFileHandle handle,
        bool expectedDirectory,
        out NativeObjectSnapshot snapshot)
    {
        snapshot = default;
        if (!GetFileInformationByHandle(handle, out var basic) ||
            !GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInformationClass,
                out FileAttributeTagInfo attributeTag,
                checked((uint)Marshal.SizeOf<FileAttributeTagInfo>())) ||
            !GetFileInformationByHandleEx(
                handle,
                FileIdInformationClass,
                out FileIdInfo fileId,
                checked((uint)Marshal.SizeOf<FileIdInfo>())) ||
            (uint)fileId.VolumeSerialNumber != basic.VolumeSerialNumber ||
            basic.NumberOfLinks != 1 ||
            (attributeTag.FileAttributes & FileAttributeReparsePoint) != 0 ||
            (((attributeTag.FileAttributes & FileAttributeDirectory) != 0) != expectedDirectory))
        {
            return false;
        }

        var byteLength = ((ulong)basic.FileSizeHigh << 32) | basic.FileSizeLow;
        snapshot = new NativeObjectSnapshot(
            fileId.VolumeSerialNumber,
            fileId.FileIdLow,
            fileId.FileIdHigh,
            attributeTag.FileAttributes,
            basic.NumberOfLinks,
            byteLength);
        return true;
    }

    private static RawSecurityDescriptor ReadSecurityDescriptor(SafeFileHandle handle)
    {
        var result = GetSecurityInfo(
            handle,
            SeFileObject,
            OwnerSecurityInformation |
            GroupSecurityInformation |
            DaclSecurityInformation |
            SaclSecurityInformation,
            out _,
            out _,
            out _,
            out _,
            out var descriptorPointer);
        if (result != 0 || descriptorPointer == IntPtr.Zero)
        {
            throw new InvalidDataException("The durable security descriptor could not be read back.");
        }

        try
        {
            var length = checked((int)GetSecurityDescriptorLength(descriptorPointer));
            if (length is <= 0 or > MaximumHeaderBytes)
            {
                throw new InvalidDataException("The durable security descriptor is out of bounds.");
            }

            var bytes = new byte[length];
            try
            {
                Marshal.Copy(descriptorPointer, bytes, 0, bytes.Length);
                return new RawSecurityDescriptor(bytes, 0);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new InvalidDataException("The durable security descriptor is malformed.", exception);
        }
        finally
        {
            _ = LocalFree(descriptorPointer);
        }
    }

    private static byte[] SerializeHeader(
        DurableProductionProfile profile,
        byte[] storeIdentifier,
        byte[] protectedStoreKey)
    {
        if (storeIdentifier.Length != StoreIdentifierLength ||
            protectedStoreKey.Length is 0 or > MaximumProtectedKeyBytes)
        {
            throw new ArgumentException("The durable store header input is invalid.");
        }

        using var writer = new SensitiveBufferWriter(256);
        writer.Write(HeaderMagic);
        AppendUInt32(writer, HeaderVersion);
        AppendUtf8(writer, DurableProductionProfile.ProfileDigestType);
        var profileDigest = GetProfileDigestBytes(profile);
        try
        {
            writer.Write(profileDigest);
            AppendUInt32(writer, StoreIdentifierLength);
            writer.Write(storeIdentifier);
            AppendUInt32(writer, checked((uint)protectedStoreKey.Length));
            writer.Write(protectedStoreKey);
            return writer.WrittenSpan.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(profileDigest);
            CryptographicOperations.ZeroMemory(writer.WrittenSpan);
        }
    }

    private static ParsedHeader ParseHeader(byte[] header, DurableProductionProfile profile)
    {
        byte[]? profileDigest = null;
        byte[]? storeIdentifier = null;
        byte[]? protectedStoreKey = null;
        try
        {
            var offset = 0;
            RequireExactMagic(header, ref offset, HeaderMagic);
            if (ReadUInt32(header, ref offset) != HeaderVersion ||
                !string.Equals(
                    ReadUtf8(header, ref offset, 96),
                    DurableProductionProfile.ProfileDigestType,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("The durable store header version is invalid.");
            }

            profileDigest = ReadExactBytes(header, ref offset, AuthenticatorLength);
            var storeIdentifierLength = ReadUInt32(header, ref offset);
            if (storeIdentifierLength != StoreIdentifierLength)
            {
                throw new InvalidDataException("The durable store identifier length is invalid.");
            }

            storeIdentifier = ReadExactBytes(header, ref offset, StoreIdentifierLength);
            var protectedKeyLength = ReadUInt32(header, ref offset);
            if (protectedKeyLength is 0 or > MaximumProtectedKeyBytes ||
                protectedKeyLength > header.Length - offset)
            {
                throw new InvalidDataException("The durable protected key length is invalid.");
            }

            protectedStoreKey = ReadExactBytes(header, ref offset, checked((int)protectedKeyLength));
            var expectedProfileDigest = GetProfileDigestBytes(profile);
            try
            {
                if (offset != header.Length ||
                    !CryptographicOperations.FixedTimeEquals(profileDigest, expectedProfileDigest))
                {
                    throw new InvalidDataException("The durable store profile binding is invalid.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expectedProfileDigest);
            }

            var parsed = new ParsedHeader(storeIdentifier, protectedStoreKey);
            storeIdentifier = null;
            protectedStoreKey = null;
            return parsed;
        }
        finally
        {
            if (profileDigest is not null)
            {
                CryptographicOperations.ZeroMemory(profileDigest);
            }

            if (storeIdentifier is not null)
            {
                CryptographicOperations.ZeroMemory(storeIdentifier);
            }

            if (protectedStoreKey is not null)
            {
                CryptographicOperations.ZeroMemory(protectedStoreKey);
            }
        }
    }

    internal static byte[] SerializeRecord(
        byte[] key,
        byte[] storeIdentifier,
        DurableProductionProfile profile,
        DurableProfileStoreRecordKind kind,
        ulong sequence,
        long generation,
        byte[] previousAuthenticator,
        byte[]? nonceFingerprint)
    {
        if (key.Length != StoreKeyLength ||
            storeIdentifier.Length != StoreIdentifierLength ||
            previousAuthenticator.Length != AuthenticatorLength ||
            !IsRecordSequenceInBounds(sequence) ||
            generation <= 0)
        {
            throw new ArgumentException("The durable record inputs are invalid.");
        }

        var payload = kind switch
        {
            DurableProfileStoreRecordKind.ProfileGeneration when nonceFingerprint is null => Array.Empty<byte>(),
            DurableProfileStoreRecordKind.NonceConsumption when nonceFingerprint is { Length: AuthenticatorLength } => nonceFingerprint,
            _ => throw new ArgumentException("The durable record kind and payload are invalid."),
        };
        using var writer = new SensitiveBufferWriter(256);
        writer.Write(RecordMagic);
        AppendUInt32(writer, RecordVersion);
        writer.GetSpan(1)[0] = (byte)kind;
        writer.Advance(1);
        AppendUInt64(writer, sequence);
        AppendInt64(writer, generation);
        AppendUtf8(writer, DurableProductionProfile.ProfileDigestType);
        var profileDigest = GetProfileDigestBytes(profile);
        try
        {
            writer.Write(profileDigest);
            writer.Write(previousAuthenticator);
            AppendUInt32(writer, checked((uint)payload.Length));
            writer.Write(payload);
            byte[]? signedBytes = writer.WrittenSpan.ToArray();
            try
            {
                byte[]? authenticator = null;
                try
                {
                    authenticator = ComputeRecordAuthenticator(key, storeIdentifier, signedBytes!);
                    writer.Write(authenticator);
                    return writer.WrittenSpan.ToArray();
                }
                finally
                {
                    if (authenticator is not null)
                    {
                        CryptographicOperations.ZeroMemory(authenticator);
                    }
                }
            }
            finally
            {
                if (signedBytes is not null)
                {
                    CryptographicOperations.ZeroMemory(signedBytes);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(profileDigest);
            CryptographicOperations.ZeroMemory(writer.WrittenSpan);
        }
    }

    internal static byte[] ComputeRecordAuthenticator(
        byte[] key,
        byte[] storeIdentifier,
        ReadOnlySpan<byte> signedRecord)
    {
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        hmac.AppendData(RecordHmacDomain);
        hmac.AppendData(storeIdentifier);
        hmac.AppendData(signedRecord);
        return hmac.GetHashAndReset();
    }

    private static byte[] ProtectStoreKey(byte[] key, DurableProductionProfile profile)
    {
        var entropy = GetProfileDigestBytes(profile);
        DataBlob protectedBlob = default;
        NativeBlob? output = null;
        byte[]? result = null;
        try
        {
            using var input = NativeBlob.FromBytes(key);
            using var optionalEntropy = NativeBlob.FromBytes(entropy);
            if (!CryptProtectData(
                    ref input.Value,
                    null,
                    ref optionalEntropy.Value,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out protectedBlob))
            {
                throw new InvalidDataException("The durable store key could not be protected.");
            }

            var ownedBlob = protectedBlob;
            protectedBlob = default;
            output = NativeBlob.Adopt(ownedBlob);
            result = output.CopyToManaged(MaximumProtectedKeyBytes);
            var transferred = TakeSensitiveBytesWithinBounds(result, 1, MaximumProtectedKeyBytes);
            result = null;
            return transferred;
        }
        finally
        {
            output?.Dispose();
            NativeBlob.ReleaseUnadopted(ref protectedBlob);
            if (result is not null)
            {
                CryptographicOperations.ZeroMemory(result);
            }

            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    private static byte[] UnprotectStoreKey(byte[] protectedStoreKey, DurableProductionProfile profile)
    {
        var entropy = GetProfileDigestBytes(profile);
        DataBlob unprotectedBlob = default;
        NativeBlob? output = null;
        byte[]? result = null;
        try
        {
            using var input = NativeBlob.FromBytes(protectedStoreKey);
            using var optionalEntropy = NativeBlob.FromBytes(entropy);
            if (!CryptUnprotectData(
                    ref input.Value,
                    IntPtr.Zero,
                    ref optionalEntropy.Value,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    CryptProtectUiForbidden,
                    out unprotectedBlob))
            {
                throw new InvalidDataException("The durable store key could not be unprotected.");
            }

            var ownedBlob = unprotectedBlob;
            unprotectedBlob = default;
            output = NativeBlob.Adopt(ownedBlob);
            result = output.CopyToManaged(StoreKeyLength);
            var transferred = TakeSensitiveBytesWithinBounds(result, StoreKeyLength, StoreKeyLength);
            result = null;
            return transferred;
        }
        finally
        {
            output?.Dispose();
            NativeBlob.ReleaseUnadopted(ref unprotectedBlob);
            if (result is not null)
            {
                CryptographicOperations.ZeroMemory(result);
            }

            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    private static byte[] GetProfileDigestBytes(DurableProductionProfile profile) =>
        Convert.FromHexString(profile.ProfileDigest.Digest["sha256:".Length..]);

    /// <summary>
    /// Transfers a freshly produced sensitive buffer only after its bounds are
    /// accepted. On every failure path this method remains its owner and clears
    /// the original array before it throws.
    /// </summary>
    internal static byte[] TakeSensitiveBytesWithinBounds(
        byte[] bytes,
        int minimumLength,
        int maximumLength)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        byte[]? owned = bytes;
        try
        {
            if (minimumLength < 0 || maximumLength < minimumLength ||
                owned.Length < minimumLength || owned.Length > maximumLength)
            {
                throw new InvalidDataException("The durable sensitive buffer is out of bounds.");
            }

            var transferred = owned;
            owned = null;
            return transferred;
        }
        finally
        {
            if (owned is not null)
            {
                CryptographicOperations.ZeroMemory(owned);
            }
        }
    }

    private static string RecordPendingSegment(ulong sequence) =>
        string.Concat(
            "vfx-durable-profile-r-",
            sequence.ToString("D20", CultureInfo.InvariantCulture),
            ".pending");

    private static string RecordFinalSegment(ulong sequence) =>
        string.Concat(
            "vfx-durable-profile-r-",
            sequence.ToString("D20", CultureInfo.InvariantCulture),
            ".record");

    private static string TipPendingSegment(ulong sequence) =>
        string.Concat(
            "vfx-durable-profile-r-",
            sequence.ToString("D20", CultureInfo.InvariantCulture),
            ".tip.pending");

    private static string TipFinalSegment(ulong sequence) =>
        string.Concat(
            "vfx-durable-profile-r-",
            sequence.ToString("D20", CultureInfo.InvariantCulture),
            ".tip");

    internal const int MaximumRecordBytes = 512;
    internal const int MaximumTipBytes = 512;

    internal static void RequireFixedSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) ||
            segment.Length > 255 ||
            segment is "." or ".." ||
            segment[^1] is '.' or ' ' ||
            segment.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            segment.Any(character => character <= '\u001f' || character == '\u007f'))
        {
            throw new ArgumentException("A durable store segment is invalid.", nameof(segment));
        }
    }

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The durable profile store is Windows-only.");
        }
    }

    private static bool IsInvalidRawHandle(IntPtr handle) =>
        handle == IntPtr.Zero || handle == new IntPtr(-1);

    private static void CloseRawHandle(IntPtr handle)
    {
        if (!IsInvalidRawHandle(handle))
        {
            new SafeFileHandle(handle, ownsHandle: true).Dispose();
        }
    }

    private static void ZeroNativeBuffer(IntPtr pointer, int byteCount)
    {
        if (pointer != IntPtr.Zero && byteCount > 0)
        {
            _ = RtlSecureZeroMemory(pointer, checked((UIntPtr)(uint)byteCount));
        }
    }

    private static void RequireExactMagic(byte[] bytes, ref int offset, byte[] magic)
    {
        if (offset > bytes.Length - magic.Length ||
            !bytes.AsSpan(offset, magic.Length).SequenceEqual(magic))
        {
            throw new InvalidDataException("The durable binary magic is invalid.");
        }

        offset += magic.Length;
    }

    private static DurableProfileStoreRecordKind ReadRecordKind(byte[] bytes, ref int offset)
    {
        if (offset >= bytes.Length)
        {
            throw new InvalidDataException("The durable record kind is missing.");
        }

        return (DurableProfileStoreRecordKind)bytes[offset++];
    }

    private static uint ReadUInt32(byte[] bytes, ref int offset)
    {
        if (offset > bytes.Length - sizeof(uint))
        {
            throw new InvalidDataException("The durable binary integer is truncated.");
        }

        var value = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        return value;
    }

    private static ulong ReadUInt64(byte[] bytes, ref int offset)
    {
        if (offset > bytes.Length - sizeof(ulong))
        {
            throw new InvalidDataException("The durable binary integer is truncated.");
        }

        var value = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(offset, sizeof(ulong)));
        offset += sizeof(ulong);
        return value;
    }

    private static long ReadInt64(byte[] bytes, ref int offset) =>
        unchecked((long)ReadUInt64(bytes, ref offset));

    private static byte[] ReadExactBytes(byte[] bytes, ref int offset, int length)
    {
        if (length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("The durable binary field is truncated.");
        }

        var result = bytes.AsSpan(offset, length).ToArray();
        offset += length;
        return result;
    }

    private static string ReadUtf8(byte[] bytes, ref int offset, int maximumLength)
    {
        var length = ReadUInt32(bytes, ref offset);
        if (length > maximumLength || length > bytes.Length - offset)
        {
            throw new InvalidDataException("The durable UTF-8 field is out of bounds.");
        }

        try
        {
            var value = new UTF8Encoding(false, true).GetString(bytes, offset, checked((int)length));
            offset += checked((int)length);
            return value;
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The durable UTF-8 field is invalid.", exception);
        }
    }

    private static void AppendUInt32(IBufferWriter<byte> writer, uint value)
    {
        var destination = writer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        writer.Advance(sizeof(uint));
    }

    private static void AppendUInt64(IBufferWriter<byte> writer, ulong value)
    {
        var destination = writer.GetSpan(sizeof(ulong));
        BinaryPrimitives.WriteUInt64BigEndian(destination, value);
        writer.Advance(sizeof(ulong));
    }

    private static void AppendInt64(IBufferWriter<byte> writer, long value) =>
        AppendUInt64(writer, unchecked((ulong)value));

    private static void AppendUtf8(IBufferWriter<byte> writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            AppendUInt32(writer, checked((uint)bytes.Length));
            writer.Write(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        internal ushort Length;
        internal ushort MaximumLength;
        internal IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        internal uint Length;
        internal IntPtr RootDirectory;
        internal IntPtr ObjectName;
        internal uint Attributes;
        internal IntPtr SecurityDescriptor;
        internal IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        internal IntPtr StatusOrPointer;
        internal UIntPtr Information;
    }

    // PUBLIC_OBJECT_BASIC_INFORMATION is 14 fixed-width ULONG fields on both
    // x86 and x64. Do not substitute pointer-sized members here: GrantedAccess
    // must be read from the native ObjectBasicInformation ABI exactly.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PublicObjectBasicInformation
    {
        internal uint Attributes;
        internal uint GrantedAccess;
        internal uint HandleCount;
        internal uint PointerCount;
        private uint _reserved0;
        private uint _reserved1;
        private uint _reserved2;
        private uint _reserved3;
        private uint _reserved4;
        private uint _reserved5;
        private uint _reserved6;
        private uint _reserved7;
        private uint _reserved8;
        private uint _reserved9;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileFsDeviceInformation
    {
        internal uint DeviceType;
        internal uint Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal NativeFileTime CreationTime;
        internal NativeFileTime LastAccessTime;
        internal NativeFileTime LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfo
    {
        internal uint FileAttributes;
        internal uint ReparseTag;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        internal ulong VolumeSerialNumber;
        internal ulong FileIdLow;
        internal ulong FileIdHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileRenameInformationHeader
    {
        [MarshalAs(UnmanagedType.U1)]
        internal bool ReplaceIfExists;
        internal IntPtr RootDirectory;
        internal uint FileNameLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        internal int ByteCount;
        internal IntPtr Data;
    }

    private readonly struct NativeObjectSnapshot
    {
        internal NativeObjectSnapshot(
            ulong volumeSerialNumber,
            ulong fileIdLow,
            ulong fileIdHigh,
            uint attributes,
            uint linkCount,
            ulong byteLength)
        {
            VolumeSerialNumber = volumeSerialNumber;
            FileIdLow = fileIdLow;
            FileIdHigh = fileIdHigh;
            Attributes = attributes;
            LinkCount = linkCount;
            ByteLength = byteLength;
        }

        internal ulong VolumeSerialNumber { get; }
        internal ulong FileIdLow { get; }
        internal ulong FileIdHigh { get; }
        internal uint Attributes { get; }
        internal uint LinkCount { get; }
        internal ulong ByteLength { get; }

        internal bool FixedEquals(NativeObjectSnapshot other) =>
            VolumeSerialNumber == other.VolumeSerialNumber &&
            FileIdLow == other.FileIdLow &&
            FileIdHigh == other.FileIdHigh &&
            Attributes == other.Attributes &&
            LinkCount == other.LinkCount &&
            ByteLength == other.ByteLength;
    }

    private sealed class DurableRootCapability : IDisposable
    {
        private readonly DurableProductionProfile _profile;
        private SafeFileHandle? _handle;
        private readonly NativeObjectSnapshot _identity;
        private readonly uint _grantedAccess;
        private int _referenceHeld;

        private DurableRootCapability(
            SafeFileHandle handle,
            DurableProductionProfile profile,
            NativeObjectSnapshot identity,
            uint grantedAccess)
        {
            _handle = handle;
            _profile = profile;
            _identity = identity;
            _grantedAccess = grantedAccess;
            _referenceHeld = 1;
        }

        internal SafeFileHandle Handle => _handle
            ?? throw new ObjectDisposedException(nameof(DurableRootCapability));

        internal static DurableRootCapability Acquire(
            SafeFileHandle handle,
            DurableProductionProfile profile)
        {
            ArgumentNullException.ThrowIfNull(handle);
            var referenceAdded = false;
            try
            {
                handle.DangerousAddRef(ref referenceAdded);
                if (handle.IsClosed || handle.IsInvalid)
                {
                    throw new InvalidDataException("The caller-supplied durable root is not an exact protected directory capability.");
                }

                var grantedAccess = QueryGrantedAccess(handle);
                if (grantedAccess != RequiredRootGrantedAccess ||
                    !GetHandleInformation(handle, out var flags) ||
                    (flags & HandleFlagInherit) != 0 ||
                    !IsLocalNtfsDisk(handle) ||
                    !TryReadObjectSnapshot(handle, expectedDirectory: true, out var identity) ||
                    !profile.MatchesExpectedRootSecurityDescriptor(ReadSecurityDescriptor(handle)))
                {
                    throw new InvalidDataException("The caller-supplied durable root is not an exact protected directory capability.");
                }

                return new DurableRootCapability(handle, profile, identity, grantedAccess);
            }
            catch
            {
                if (referenceAdded)
                {
                    handle.DangerousRelease();
                }

                throw;
            }
        }

        internal void Revalidate()
        {
            var handle = Handle;
            if (handle.IsClosed || handle.IsInvalid)
            {
                throw new InvalidDataException("The caller-supplied durable root drifted.");
            }

            var grantedAccess = QueryGrantedAccess(handle);
            if (grantedAccess != RequiredRootGrantedAccess ||
                grantedAccess != _grantedAccess ||
                !GetHandleInformation(handle, out var flags) ||
                (flags & HandleFlagInherit) != 0 ||
                !IsLocalNtfsDisk(handle) ||
                !TryReadObjectSnapshot(handle, expectedDirectory: true, out var current) ||
                !_identity.FixedEquals(current) ||
                !_profile.MatchesExpectedRootSecurityDescriptor(ReadSecurityDescriptor(handle)))
            {
                throw new InvalidDataException("The caller-supplied durable root drifted.");
            }
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, null);
            if (handle is not null && Interlocked.Exchange(ref _referenceHeld, 0) != 0)
            {
                handle.DangerousRelease();
            }
        }
    }

    /// <summary>
    /// A small mutable writer for transient key, authenticator, and nonce-
    /// fingerprint serialization. Unlike <see cref="ArrayBufferWriter{T}"/>,
    /// its backing bytes are directly writable and are always cleared when the
    /// serializer returns or grows its buffer.
    /// </summary>
    private sealed class SensitiveBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private byte[]? _buffer;
        private int _written;

        internal SensitiveBufferWriter(int initialCapacity)
        {
            if (initialCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            _buffer = new byte[initialCapacity];
        }

        internal Span<byte> WrittenSpan => Buffer.AsSpan(0, _written);

        public void Advance(int count)
        {
            if (count < 0 || count > Buffer.Length - _written)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _written += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return Buffer.AsMemory(_written);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return Buffer.AsSpan(_written);
        }

        public void Dispose()
        {
            var buffer = Interlocked.Exchange(ref _buffer, null);
            if (buffer is not null)
            {
                CryptographicOperations.ZeroMemory(buffer);
            }

            _written = 0;
        }

        private byte[] Buffer => _buffer
            ?? throw new ObjectDisposedException(nameof(SensitiveBufferWriter));

        private void EnsureCapacity(int sizeHint)
        {
            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            }

            var buffer = Buffer;
            var required = checked(_written + Math.Max(sizeHint, 1));
            if (required <= buffer.Length)
            {
                return;
            }

            var grown = new byte[Math.Max(required, checked(buffer.Length * 2))];
            buffer.AsSpan(0, _written).CopyTo(grown);
            CryptographicOperations.ZeroMemory(buffer);
            _buffer = grown;
        }
    }

    private sealed class NativeBlob : IDisposable
    {
        private IntPtr _pointer;
        private readonly int _byteCount;
        private readonly bool _usesLocalFree;

        private NativeBlob(IntPtr pointer, int byteCount, bool usesLocalFree)
        {
            _pointer = pointer;
            _byteCount = byteCount;
            _usesLocalFree = usesLocalFree;
            Value = new DataBlob
            {
                ByteCount = byteCount,
                Data = pointer,
            };
        }

        internal DataBlob Value;

        internal static NativeBlob FromBytes(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length == 0)
            {
                return new NativeBlob(IntPtr.Zero, 0, usesLocalFree: false);
            }

            var pointer = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                var blob = new NativeBlob(pointer, bytes.Length, usesLocalFree: false);
                pointer = IntPtr.Zero;
                return blob;
            }
            finally
            {
                ReleasePointer(pointer, bytes.Length, usesLocalFree: false);
            }
        }

        internal static NativeBlob Adopt(DataBlob blob)
        {
            var pointer = blob.Data;
            var byteCount = blob.ByteCount;
            if (byteCount < 0 || (byteCount != 0 && pointer == IntPtr.Zero))
            {
                ReleasePointer(pointer, Math.Max(byteCount, 0), usesLocalFree: true);
                throw new InvalidDataException("The protected data blob is invalid.");
            }

            try
            {
                var adopted = new NativeBlob(pointer, byteCount, usesLocalFree: true);
                pointer = IntPtr.Zero;
                return adopted;
            }
            finally
            {
                ReleasePointer(pointer, byteCount, usesLocalFree: true);
            }
        }

        internal byte[] CopyToManaged(int maximumLength)
        {
            if (Value.ByteCount < 0 || Value.ByteCount > maximumLength)
            {
                throw new InvalidDataException("The protected data blob is out of bounds.");
            }

            byte[]? result = new byte[Value.ByteCount];
            try
            {
                if (result.Length != 0)
                {
                    Marshal.Copy(Value.Data, result, 0, result.Length);
                }

                var transferred = result;
                result = null;
                return transferred;
            }
            finally
            {
                if (result is not null)
                {
                    CryptographicOperations.ZeroMemory(result);
                }
            }
        }

        public void Dispose()
        {
            var pointer = Interlocked.Exchange(ref _pointer, IntPtr.Zero);
            ReleasePointer(pointer, _byteCount, _usesLocalFree);

            Value = default;
        }

        internal static void ReleaseUnadopted(ref DataBlob blob)
        {
            var pointer = blob.Data;
            var byteCount = blob.ByteCount;
            blob = default;
            ReleasePointer(pointer, Math.Max(byteCount, 0), usesLocalFree: true);
        }

        private static void ReleasePointer(IntPtr pointer, int byteCount, bool usesLocalFree)
        {
            if (pointer == IntPtr.Zero)
            {
                return;
            }

            ZeroNativeBuffer(pointer, byteCount);
            if (usesLocalFree)
            {
                _ = LocalFree(pointer);
            }
            else
            {
                Marshal.FreeHGlobal(pointer);
            }
        }
    }

    private sealed class ParsedHeader : IDisposable
    {
        private byte[]? _storeIdentifier;
        private byte[]? _protectedStoreKey;

        internal ParsedHeader(byte[] storeIdentifier, byte[] protectedStoreKey)
        {
            _storeIdentifier = storeIdentifier;
            _protectedStoreKey = protectedStoreKey;
        }

        internal byte[] ProtectedStoreKey => _protectedStoreKey
            ?? throw new ObjectDisposedException(nameof(ParsedHeader));

        internal byte[] DetachStoreIdentifier() =>
            Interlocked.Exchange(ref _storeIdentifier, null)
            ?? throw new ObjectDisposedException(nameof(ParsedHeader));

        public void Dispose()
        {
            var storeIdentifier = Interlocked.Exchange(ref _storeIdentifier, null);
            var protectedStoreKey = Interlocked.Exchange(ref _protectedStoreKey, null);
            if (storeIdentifier is not null)
            {
                CryptographicOperations.ZeroMemory(storeIdentifier);
            }

            if (protectedStoreKey is not null)
            {
                CryptographicOperations.ZeroMemory(protectedStoreKey);
            }
        }
    }

    internal sealed class AuthenticatedRecord : IDisposable
    {
        internal AuthenticatedRecord(
            DurableProfileStoreRecordKind kind,
            ulong sequence,
            long generation,
            byte[] previousAuthenticator,
            byte[] authenticator,
            byte[]? nonceFingerprint)
        {
            Kind = kind;
            Sequence = sequence;
            Generation = generation;
            PreviousAuthenticator = previousAuthenticator;
            Authenticator = authenticator;
            NonceFingerprint = nonceFingerprint;
        }

        internal DurableProfileStoreRecordKind Kind { get; }
        internal ulong Sequence { get; }
        internal long Generation { get; }
        internal byte[] PreviousAuthenticator { get; }
        internal byte[] Authenticator { get; }
        internal byte[]? NonceFingerprint { get; }

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(PreviousAuthenticator);
            CryptographicOperations.ZeroMemory(Authenticator);
            if (NonceFingerprint is not null)
            {
                CryptographicOperations.ZeroMemory(NonceFingerprint);
            }
        }
    }

    internal sealed class AuthenticatedSealedTip : IDisposable
    {
        internal AuthenticatedSealedTip(
            DurableProfileStoreRecordKind kind,
            ulong sequence,
            long generation,
            byte[] recordAuthenticator,
            byte[] authenticator)
        {
            Kind = kind;
            Sequence = sequence;
            Generation = generation;
            RecordAuthenticator = recordAuthenticator;
            Authenticator = authenticator;
        }

        internal DurableProfileStoreRecordKind Kind { get; }
        internal ulong Sequence { get; }
        internal long Generation { get; }
        internal byte[] RecordAuthenticator { get; }
        internal byte[] Authenticator { get; }

        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(RecordAuthenticator);
            CryptographicOperations.ZeroMemory(Authenticator);
        }
    }

    private sealed class NonceFingerprint : IEquatable<NonceFingerprint>, IDisposable
    {
        private readonly byte[] _bytes;

        internal NonceFingerprint(byte[] bytes)
        {
            if (bytes.Length != AuthenticatorLength)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new ArgumentException("The durable nonce fingerprint length is invalid.", nameof(bytes));
            }

            _bytes = bytes;
        }

        public bool Equals(NonceFingerprint? other) =>
            other is not null && CryptographicOperations.FixedTimeEquals(_bytes, other._bytes);

        public override bool Equals(object? obj) => Equals(obj as NonceFingerprint);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var value in _bytes)
            {
                hash.Add(value);
            }

            return hash.ToHashCode();
        }

        internal byte[] CopyBytes() => _bytes.ToArray();

        public void Dispose() => CryptographicOperations.ZeroMemory(_bytes);
    }

    private sealed class ChainState
    {
        private byte[] _authenticator = new byte[AuthenticatorLength];
        private byte[] _sealedTipAuthenticator = new byte[AuthenticatorLength];

        internal static ChainState Empty => new();

        internal ulong Sequence { get; private set; }
        internal long Generation { get; private set; }
        internal byte[] Authenticator => _authenticator;
        internal byte[] SealedTipAuthenticator => _sealedTipAuthenticator;

        internal void Replace(
            ulong sequence,
            long generation,
            byte[] authenticator,
            byte[] sealedTipAuthenticator)
        {
            if (authenticator.Length != AuthenticatorLength ||
                sealedTipAuthenticator.Length != AuthenticatorLength)
            {
                throw new ArgumentException("The durable authenticator length is invalid.", nameof(authenticator));
            }

            byte[]? nextAuthenticator = authenticator.ToArray();
            byte[]? nextSealedTipAuthenticator = null;
            try
            {
                nextSealedTipAuthenticator = sealedTipAuthenticator.ToArray();
                var previousAuthenticator = _authenticator;
                var previousSealedTipAuthenticator = _sealedTipAuthenticator;
                _authenticator = nextAuthenticator;
                nextAuthenticator = null;
                _sealedTipAuthenticator = nextSealedTipAuthenticator;
                nextSealedTipAuthenticator = null;
                Sequence = sequence;
                Generation = generation;
                CryptographicOperations.ZeroMemory(previousAuthenticator);
                CryptographicOperations.ZeroMemory(previousSealedTipAuthenticator);
            }
            finally
            {
                if (nextAuthenticator is not null)
                {
                    CryptographicOperations.ZeroMemory(nextAuthenticator);
                }

                if (nextSealedTipAuthenticator is not null)
                {
                    CryptographicOperations.ZeroMemory(nextSealedTipAuthenticator);
                }
            }
        }

        internal void ZeroAuthenticator()
        {
            CryptographicOperations.ZeroMemory(_authenticator);
            CryptographicOperations.ZeroMemory(_sealedTipAuthenticator);
            Sequence = 0;
            Generation = 0;
        }
    }

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtCreateFile(
        out IntPtr fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        IntPtr allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        IntPtr eaBuffer,
        uint eaLength);

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtOpenFile(
        out IntPtr fileHandle,
        uint desiredAccess,
        ref ObjectAttributes objectAttributes,
        out IoStatusBlock ioStatusBlock,
        uint shareAccess,
        uint openOptions);

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtSetInformationFile(
        SafeFileHandle fileHandle,
        out IoStatusBlock ioStatusBlock,
        IntPtr fileInformation,
        uint length,
        int fileInformationClass);

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtQueryObject(
        SafeFileHandle handle,
        int objectInformationClass,
        out PublicObjectBasicInformation objectInformation,
        uint objectInformationLength,
        out uint returnLength);

    [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
    private static extern int NtQueryVolumeInformationFile(
        SafeFileHandle fileHandle,
        out IoStatusBlock ioStatusBlock,
        out FileFsDeviceInformation fsInformation,
        uint length,
        int fsInformationClass);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(
        SafeFileHandle handle,
        uint mask,
        uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(
        SafeFileHandle handle,
        out uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetFileType(SafeFileHandle handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle handle,
        out ByHandleFileInformation fileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle handle,
        int fileInformationClass,
        out FileAttributeTagInfo fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle handle,
        int fileInformationClass,
        out FileIdInfo fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformationByHandleW(
        SafeFileHandle fileHandle,
        StringBuilder? volumeNameBuffer,
        int volumeNameSize,
        out uint volumeSerialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer,
        int fileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(
        SafeFileHandle fileHandle,
        byte[] buffer,
        uint numberOfBytesToWrite,
        out uint numberOfBytesWritten,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle fileHandle,
        byte[] buffer,
        uint numberOfBytesToRead,
        out uint numberOfBytesRead,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFilePointerEx(
        SafeFileHandle fileHandle,
        long distanceToMove,
        out long newFilePointer,
        uint moveMethod);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlushFileBuffers(SafeFileHandle fileHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint GetSecurityInfo(
        SafeFileHandle handle,
        uint objectType,
        uint securityInformation,
        out IntPtr owner,
        out IntPtr group,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern uint GetSecurityDescriptorLength(IntPtr securityDescriptor);

    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob inputData,
        string? description,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DataBlob outputData);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob inputData,
        IntPtr description,
        ref DataBlob optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        uint flags,
        out DataBlob outputData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    [DllImport("kernel32.dll", EntryPoint = "RtlSecureZeroMemory", ExactSpelling = true)]
    private static extern IntPtr RtlSecureZeroMemory(IntPtr pointer, UIntPtr byteCount);

    private const uint SeFileObject = 1;
    private const uint OwnerSecurityInformation = 0x00000001;
    private const uint GroupSecurityInformation = 0x00000002;
    private const uint DaclSecurityInformation = 0x00000004;
    private const uint SaclSecurityInformation = 0x00000008;
}

/// <summary>
/// Immutable observation from one live durable-store instance. It contains no
/// key, nonce, path, handle, policy activation, or authority capability.
/// </summary>
internal sealed class DurableProfileStoreReceipt
{
    internal DurableProfileStoreReceipt(
        TypedHash storeIdentifierDigest,
        TypedHash profileDigest,
        TypedHash issuerEpochDigest,
        TypedHash sealedTipDigest,
        ulong sequence,
        long generation,
        DurableProfileStoreRecordKind recordKind)
    {
        StoreIdentifierDigest = storeIdentifierDigest ?? throw new ArgumentNullException(nameof(storeIdentifierDigest));
        ProfileDigest = profileDigest ?? throw new ArgumentNullException(nameof(profileDigest));
        IssuerEpochDigest = issuerEpochDigest ?? throw new ArgumentNullException(nameof(issuerEpochDigest));
        SealedTipDigest = sealedTipDigest ?? throw new ArgumentNullException(nameof(sealedTipDigest));
        if (sequence == 0 || generation <= 0 ||
            recordKind is not DurableProfileStoreRecordKind.ProfileGeneration and
                not DurableProfileStoreRecordKind.NonceConsumption)
        {
            throw new ArgumentException("The durable receipt is invalid.");
        }

        Sequence = sequence;
        Generation = generation;
        RecordKind = recordKind;
    }

    internal TypedHash StoreIdentifierDigest { get; }

    internal TypedHash ProfileDigest { get; }

    internal TypedHash IssuerEpochDigest { get; }

    internal TypedHash SealedTipDigest { get; }

    internal ulong Sequence { get; }

    internal long Generation { get; }

    internal DurableProfileStoreRecordKind RecordKind { get; }
}

internal enum DurableProfileStoreRecordKind : byte
{
    ProfileGeneration = 1,
    NonceConsumption = 2,
}

/// <summary>
/// Closed immutable-artifact topology for one sequence. This is deliberately
/// separate from authenticated record contents so a crash-state cannot be
/// silently interpreted as a valid state transition.
/// </summary>
internal enum DurableRecordTipArtifactState : byte
{
    Absent = 0,
    Complete = 1,
    Pending = 2,
    Incomplete = 3,
}

/// <summary>
/// Closed replay action for one in-range record/tip topology. This is a
/// production state result used by durable replay; it carries no path, handle,
/// key, nonce, or authority capability.
/// </summary>
internal enum DurableReplayDisposition : byte
{
    Apply = 1,
    ApplyAndFinish = 2,
    Finish = 3,
    MissingFirst = 4,
    RejectPending = 5,
    RejectIncomplete = 6,
    RejectOutOfRange = 7,
}
