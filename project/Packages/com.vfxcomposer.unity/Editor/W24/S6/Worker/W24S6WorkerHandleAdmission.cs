using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Editor.W24.S6.External;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Editor.W24.S6.Worker
{
    /// <summary>
    /// Opaque proof that a future Broker connection authenticated this exact Worker process.
    /// No production issuer exists until the authenticated connection slice is implemented.
    /// </summary>
    internal sealed class W24S6WorkerAuthenticatedSession : IDisposable
    {
        internal const int MaxAdmissionsPerSession = 1024;
        private static readonly object CandidateIssuer = new object();
#if UNITY_INCLUDE_TESTS
        private static readonly object TestIssuer = new object();
        internal Action BeforeSessionAttachForTests;
        internal Action BeforeHandleCloseForTests;
        internal int CandidateFactoryInvocationCountForTests;
#endif
        private readonly object issuer;
        private readonly object admissionGate = new object();
        private readonly object disposeGate = new object();
        private readonly object lifecycleGate = new object();
        private readonly HashSet<W24S6WorkerProjectHandleLease> leases =
            new HashSet<W24S6WorkerProjectHandleLease>();
        private readonly Dictionary<string, W24S6WorkerProjectHandleLease> successfulAdmissions =
            new Dictionary<string, W24S6WorkerProjectHandleLease>(StringComparer.Ordinal);
        private readonly HashSet<string> consumedGrantHashes =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> claimedHandleValues =
            new HashSet<string>(StringComparer.Ordinal);
        private int usable = 1;

        private W24S6WorkerAuthenticatedSession(
            object issuer,
            string sessionId,
            string processEpoch,
            long brokerGeneration)
        {
#if UNITY_INCLUDE_TESTS
            if (!ReferenceEquals(issuer, TestIssuer)) throw new InvalidDataException();
            this.issuer = issuer;
            SessionId = sessionId;
            ProcessEpoch = processEpoch;
            BrokerGeneration = brokerGeneration;
#else
            throw new InvalidOperationException("WORKER_SESSION_ISSUER_PENDING");
#endif
        }

        internal string SessionId { get; private set; }
        internal string ProcessEpoch { get; private set; }
        internal long BrokerGeneration { get; private set; }
        internal bool IsUsable { get { return Volatile.Read(ref usable) == 1; } }

        private bool Matches(W24S6WorkerProjectHandleGrant grant)
        {
#if UNITY_INCLUDE_TESTS
            return ReferenceEquals(issuer, TestIssuer) && IsUsable && grant != null &&
                   grant.BrokerGeneration == BrokerGeneration &&
                   string.Equals(grant.WorkerSessionId, SessionId, StringComparison.Ordinal) &&
                   string.Equals(grant.WorkerProcessEpoch, ProcessEpoch, StringComparison.Ordinal) &&
                   string.Equals(ProcessEpoch, W24S6WorkerProcessEpoch.ObserveCurrent(), StringComparison.Ordinal);
#else
            return false;
#endif
        }

        public void Dispose()
        {
            lock (disposeGate)
            {
                lock (admissionGate)
                {
                    W24S6WorkerProjectHandleLease[] snapshot;
                    lock (lifecycleGate)
                    {
                        if (Interlocked.Exchange(ref usable, 0) != 1) return;
                        snapshot = new W24S6WorkerProjectHandleLease[leases.Count];
                        leases.CopyTo(snapshot);
                        leases.Clear();
                    }
                    foreach (var lease in snapshot) lease.Dispose();
                    successfulAdmissions.Clear();
                    consumedGrantHashes.Clear();
                    claimedHandleValues.Clear();
                }
            }
        }

        internal bool TryAdmit(
            W24S6WorkerProjectHandleGrant grant,
            out W24S6WorkerProjectHandleLease lease)
        {
            lease = null;
            lock (admissionGate)
            {
                if (!Matches(grant)) return false;
                var grantKey = grant.SelfHash.Digest;
                W24S6WorkerProjectHandleLease existing;
                if (consumedGrantHashes.Contains(grantKey))
                {
                    if (successfulAdmissions.TryGetValue(grantKey, out existing) && existing.IsAttached)
                    {
                        lease = existing;
                        return true;
                    }
                    return false;
                }
                if (consumedGrantHashes.Count >= MaxAdmissionsPerSession) return false;

                var handleValues = new[]
                {
                    grant.VolumeHandle,
                    grant.RepositoryHandle,
                    grant.ProjectRootHandle
                };
                consumedGrantHashes.Add(grantKey);
                var distinct = new HashSet<string>(handleValues, StringComparer.Ordinal);
                foreach (var value in distinct)
                    if (claimedHandleValues.Contains(value)) return false;
                foreach (var value in distinct) claimedHandleValues.Add(value);

                W24S6WorkerProjectHandleLease candidate = null;
                try
                {
#if UNITY_INCLUDE_TESTS
                    CandidateFactoryInvocationCountForTests++;
#endif
                    candidate = W24S6WorkerProjectHandleLease.CreateCandidateForSession(
                        CandidateIssuer,
                        this,
                        grant);
                    if (candidate == null) return false;
#if UNITY_INCLUDE_TESTS
                    var beforeAttach = BeforeSessionAttachForTests;
                    if (beforeAttach != null) beforeAttach();
#endif
                    lock (lifecycleGate)
                    {
                        if (Volatile.Read(ref usable) != 1 || !leases.Add(candidate))
                        {
                            candidate.Dispose();
                            return false;
                        }
                    }
                    successfulAdmissions.Add(grantKey, candidate);
                    lease = candidate;
                    candidate = null;
                    return true;
                }
                finally
                {
                    if (candidate != null) candidate.Dispose();
                }
            }
        }

        internal void Detach(W24S6WorkerProjectHandleLease lease)
        {
            lock (lifecycleGate) leases.Remove(lease);
        }

        internal static bool IsCandidateIssuer(object issuer)
        {
            return ReferenceEquals(issuer, CandidateIssuer);
        }

#if UNITY_INCLUDE_TESTS
        internal static W24S6WorkerAuthenticatedSession IssueForTests(
            string sessionId,
            long brokerGeneration)
        {
            if (!IsToken(sessionId) || brokerGeneration < 1) throw new InvalidDataException();
            return new W24S6WorkerAuthenticatedSession(
                TestIssuer,
                sessionId,
                W24S6WorkerProcessEpoch.ObserveCurrent(),
                brokerGeneration);
        }

        private static bool IsToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < 'A' || character > 'Z') &&
                    (character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '.' && character != '_' && character != ':' && character != '-') return false;
            }
            return true;
        }
#endif
    }

    /// <summary>
    /// Owns the exact three process-local directory handles after native identity replay.
    /// It exposes identities and lifecycle only; raw handles are never returned to callers.
    /// </summary>
    internal sealed class W24S6WorkerProjectHandleLease : IDisposable
    {
        internal const string AdmissionRejected = "W24WKR002";
        internal const int MaximumReadBytes = 512 * 1024;
        private readonly object disposeGate = new object();
        private readonly W24S6WorkerAuthenticatedSession session;
        private readonly SafeFileHandle volumeHandle;
        private readonly SafeFileHandle repositoryHandle;
        private readonly SafeFileHandle projectRootHandle;
        private readonly W24S6WorkerNativeDirectoryIdentity volumeNativeIdentity;
        private readonly W24S6WorkerNativeDirectoryIdentity repositoryNativeIdentity;
        private readonly W24S6WorkerNativeDirectoryIdentity projectRootNativeIdentity;
        private int usable = 1;

        private W24S6WorkerProjectHandleLease(
            W24S6WorkerAuthenticatedSession session,
            W24S6WorkerProjectHandleGrant grant,
            SafeFileHandle volumeHandle,
            SafeFileHandle repositoryHandle,
            SafeFileHandle projectRootHandle,
            W24S6WorkerNativeDirectoryIdentity volumeNativeIdentity,
            W24S6WorkerNativeDirectoryIdentity repositoryNativeIdentity,
            W24S6WorkerNativeDirectoryIdentity projectRootNativeIdentity)
        {
            this.session = session;
            this.volumeHandle = volumeHandle;
            this.repositoryHandle = repositoryHandle;
            this.projectRootHandle = projectRootHandle;
            this.volumeNativeIdentity = volumeNativeIdentity;
            this.repositoryNativeIdentity = repositoryNativeIdentity;
            this.projectRootNativeIdentity = projectRootNativeIdentity;
            LeaseId = grant.LeaseId;
            RegisteredProjectId = grant.RegisteredProjectId;
            ProjectIdentity = grant.ProjectIdentity;
            VolumeIdentity = grant.VolumeIdentity;
            RepositoryIdentity = grant.RepositoryIdentity;
            ProjectRootIdentity = grant.ProjectRootIdentity;
            BrokerGeneration = grant.BrokerGeneration;
            RegistrationGeneration = grant.RegistrationGeneration;
            LeaseGeneration = grant.LeaseGeneration;
            WorkerSessionId = grant.WorkerSessionId;
            WorkerProcessEpoch = grant.WorkerProcessEpoch;
            GrantSelfHash = grant.SelfHash;
        }

        internal string LeaseId { get; private set; }
        internal string RegisteredProjectId { get; private set; }
        internal W24S6WorkerTypedHash ProjectIdentity { get; private set; }
        internal W24S6WorkerTypedHash VolumeIdentity { get; private set; }
        internal W24S6WorkerTypedHash RepositoryIdentity { get; private set; }
        internal W24S6WorkerTypedHash ProjectRootIdentity { get; private set; }
        internal long BrokerGeneration { get; private set; }
        internal long RegistrationGeneration { get; private set; }
        internal long LeaseGeneration { get; private set; }
        internal string WorkerSessionId { get; private set; }
        internal string WorkerProcessEpoch { get; private set; }
        internal W24S6WorkerTypedHash GrantSelfHash { get; private set; }

        internal bool IsUsable
        {
            get
            {
                return Volatile.Read(ref usable) == 1 && session.IsUsable && ReplayIdentities();
            }
        }

        internal bool IsAttached
        {
            get { return Volatile.Read(ref usable) == 1 && session.IsUsable; }
        }

        internal bool ReplayIdentities()
        {
            lock (disposeGate)
            {
                if (Volatile.Read(ref usable) != 1 || !session.IsUsable) return false;
                try
                {
                    return ReplayIdentitiesUnderLock();
                }
                catch (Exception exception) when (IsExpectedFailure(exception))
                {
                    return false;
                }
            }
        }

        internal bool TryReadRepositoryRelative(string relativePath, out byte[] bytes)
        {
            return TryReadRelative(LeaseRoot.Repository, relativePath, out bytes);
        }

        internal bool TryReadProjectRelative(string relativePath, out byte[] bytes)
        {
            return TryReadRelative(LeaseRoot.ProjectRoot, relativePath, out bytes);
        }

        // The opaque lease surface may never name a native handle type, not even on a private
        // member: callers select a pinned root by identity and the handle stays inside the lock.
        private enum LeaseRoot { Repository, ProjectRoot }

        private bool TryReadRelative(
            LeaseRoot root,
            string relativePath,
            out byte[] bytes)
        {
            bytes = null;
            lock (disposeGate)
            {
                if (Volatile.Read(ref usable) != 1 || !session.IsUsable) return false;
                try
                {
                    if (!ReplayIdentitiesUnderLock()) return false;
                    var read = W24S6WindowsReadOnlyFile.ReadExactFromPinnedDirectoryHandle(
                        root == LeaseRoot.Repository ? repositoryHandle : projectRootHandle,
                        relativePath,
                        MaximumReadBytes);
                    if (Volatile.Read(ref usable) != 1 || !session.IsUsable ||
                        !ReplayIdentitiesUnderLock())
                        return false;
                    bytes = read.Bytes;
                    return true;
                }
                catch (Exception exception) when (IsExpectedFailure(exception))
                {
                    bytes = null;
                    return false;
                }
            }
        }

        private bool ReplayIdentitiesUnderLock()
        {
            return volumeNativeIdentity.FixedEquals(W24S6WorkerNativeDirectoryIdentity.Observe(volumeHandle)) &&
                   repositoryNativeIdentity.FixedEquals(W24S6WorkerNativeDirectoryIdentity.Observe(repositoryHandle)) &&
                   projectRootNativeIdentity.FixedEquals(W24S6WorkerNativeDirectoryIdentity.Observe(projectRootHandle));
        }

        internal static bool TryAdmit(
            W24S6WorkerAuthenticatedSession session,
            W24S6WorkerProjectHandleGrant grant,
            out W24S6WorkerProjectHandleLease lease,
            out string diagnosticCode)
        {
            lease = null;
            diagnosticCode = AdmissionRejected;
            if (session == null || grant == null) return false;
            try
            {
                if (!session.TryAdmit(grant, out lease)) return false;
                diagnosticCode = string.Empty;
                return true;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                return false;
            }
        }

        internal static W24S6WorkerProjectHandleLease CreateCandidateForSession(
            object issuer,
            W24S6WorkerAuthenticatedSession session,
            W24S6WorkerProjectHandleGrant grant)
        {
            if (!W24S6WorkerAuthenticatedSession.IsCandidateIssuer(issuer)) return null;
            var owned = new List<SafeFileHandle>(3);
            try
            {
                var raw = new[]
                {
                    ParseHandle(grant.VolumeHandle),
                    ParseHandle(grant.RepositoryHandle),
                    ParseHandle(grant.ProjectRootHandle)
                };
                var distinct = new HashSet<long>();
                foreach (var value in raw)
                    if (distinct.Add(value.ToInt64())) owned.Add(new SafeFileHandle(value, true));
                if (owned.Count != 3) return null;

                var volume = W24S6WorkerNativeDirectoryIdentity.Observe(owned[0]);
                var repository = W24S6WorkerNativeDirectoryIdentity.Observe(owned[1]);
                var projectRoot = W24S6WorkerNativeDirectoryIdentity.Observe(owned[2]);
                if (!IsAdmissibleDirectory(owned[0], volume) ||
                    !IsAdmissibleDirectory(owned[1], repository) ||
                    !IsAdmissibleDirectory(owned[2], projectRoot) ||
                    !IsNtfs(owned[0]) ||
                    volume.VolumeSerialNumber != repository.VolumeSerialNumber ||
                    volume.VolumeSerialNumber != projectRoot.VolumeSerialNumber)
                    return null;

                var observedVolume = volume.ComputeTypedHash(W24S6WorkerProtocolCodec.VolumeIdentityType);
                var observedRepository = repository.ComputeTypedHash(W24S6WorkerProtocolCodec.DirectoryIdentityType);
                var observedProjectRoot = projectRoot.ComputeTypedHash(W24S6WorkerProtocolCodec.DirectoryIdentityType);
                var observedProject = ComputeProjectIdentity(
                    observedVolume,
                    observedRepository,
                    observedProjectRoot);
                if (!W24S6WorkerProtocolCodec.FixedTimeEquals(grant.VolumeIdentity, observedVolume) ||
                    !W24S6WorkerProtocolCodec.FixedTimeEquals(grant.RepositoryIdentity, observedRepository) ||
                    !W24S6WorkerProtocolCodec.FixedTimeEquals(grant.ProjectRootIdentity, observedProjectRoot) ||
                    !W24S6WorkerProtocolCodec.FixedTimeEquals(grant.ProjectIdentity, observedProject) ||
                    !volume.FixedEquals(W24S6WorkerNativeDirectoryIdentity.Observe(owned[0])) ||
                    !repository.FixedEquals(W24S6WorkerNativeDirectoryIdentity.Observe(owned[1])) ||
                    !projectRoot.FixedEquals(W24S6WorkerNativeDirectoryIdentity.Observe(owned[2])))
                    return null;

                var candidate = new W24S6WorkerProjectHandleLease(
                    session,
                    grant,
                    owned[0],
                    owned[1],
                    owned[2],
                    volume,
                    repository,
                    projectRoot);
                owned.Clear();
                return candidate;
            }
            finally
            {
                for (var index = owned.Count - 1; index >= 0; index--) owned[index].Dispose();
            }
        }

        private static bool IsExpectedFailure(Exception exception)
        {
            return exception is InvalidDataException || exception is IOException ||
                   exception is W24S6PinnedReadFailure ||
                   exception is UnauthorizedAccessException || exception is ObjectDisposedException ||
                   exception is ArgumentException || exception is FormatException ||
                   exception is OverflowException || exception is PlatformNotSupportedException;
        }

        private static IntPtr ParseHandle(string value)
        {
            if (IntPtr.Size != 8 || string.IsNullOrEmpty(value) || value.Length != 16)
                throw new InvalidDataException();
            var numeric = ulong.Parse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            if (numeric == 0 || numeric == ulong.MaxValue) throw new InvalidDataException();
            return new IntPtr(unchecked((long)numeric));
        }

        private static bool IsAdmissibleDirectory(
            SafeFileHandle handle,
            W24S6WorkerNativeDirectoryIdentity identity)
        {
            uint flags;
            return identity.IsDirectory && !identity.IsReparsePoint &&
                   GetFileType(handle) == 1 &&
                   GetHandleInformation(handle, out flags) && (flags & 1) == 0;
        }

        private static bool IsNtfs(SafeFileHandle handle)
        {
            var fileSystemName = new StringBuilder(32);
            uint serial;
            uint componentLength;
            uint flags;
            return GetVolumeInformationByHandleW(
                       handle,
                       null,
                       0,
                       out serial,
                       out componentLength,
                       out flags,
                       fileSystemName,
                       fileSystemName.Capacity) &&
                   string.Equals(fileSystemName.ToString(), "NTFS", StringComparison.Ordinal);
        }

        private static W24S6WorkerTypedHash ComputeProjectIdentity(
            W24S6WorkerTypedHash volume,
            W24S6WorkerTypedHash repository,
            W24S6WorkerTypedHash projectRoot)
        {
            var payload = Encoding.UTF8.GetBytes(
                volume.Digest + "|" + repository.Digest + "|" + projectRoot.Digest);
            return W24S6WorkerProtocolCodec.ComputeTypedHash(
                W24S6WorkerProtocolCodec.ProjectIdentityType,
                payload);
        }

#if UNITY_INCLUDE_TESTS
        internal static W24S6WorkerTypedHash ObserveIdentityForTests(IntPtr rawHandle, string typeTag)
        {
            using (var handle = new SafeFileHandle(rawHandle, false))
                return W24S6WorkerNativeDirectoryIdentity.Observe(handle).ComputeTypedHash(typeTag);
        }

        internal static W24S6WorkerTypedHash ComputeProjectIdentityForTests(
            W24S6WorkerTypedHash volume,
            W24S6WorkerTypedHash repository,
            W24S6WorkerTypedHash projectRoot)
        {
            return ComputeProjectIdentity(volume, repository, projectRoot);
        }
#endif

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetFileType(SafeFileHandle handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetHandleInformation(SafeFileHandle handle, out uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVolumeInformationByHandleW(
            SafeFileHandle fileHandle,
            StringBuilder volumeNameBuffer,
            int volumeNameSize,
            out uint volumeSerialNumber,
            out uint maximumComponentLength,
            out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer,
            int fileSystemNameSize);

        public void Dispose()
        {
            lock (disposeGate)
            {
                if (Interlocked.Exchange(ref usable, 0) != 1) return;
#if UNITY_INCLUDE_TESTS
                var hook = session.BeforeHandleCloseForTests;
                if (hook != null) hook();
#endif
                projectRootHandle.Dispose();
                repositoryHandle.Dispose();
                volumeHandle.Dispose();
                session.Detach(this);
            }
        }
    }

    internal sealed class W24S6WorkerNativeDirectoryIdentity
    {
        private const uint FileAttributeDirectory = 0x00000010;
        private const uint FileAttributeReparsePoint = 0x00000400;
        private readonly byte[] fileId;

        private W24S6WorkerNativeDirectoryIdentity(
            ulong volumeSerialNumber,
            byte[] fileId,
            uint fileAttributes)
        {
            VolumeSerialNumber = volumeSerialNumber;
            this.fileId = fileId;
            FileAttributes = fileAttributes;
        }

        internal ulong VolumeSerialNumber { get; private set; }
        internal uint FileAttributes { get; private set; }
        internal bool IsDirectory { get { return (FileAttributes & FileAttributeDirectory) != 0; } }
        internal bool IsReparsePoint { get { return (FileAttributes & FileAttributeReparsePoint) != 0; } }

        internal static W24S6WorkerNativeDirectoryIdentity Observe(SafeFileHandle handle)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || handle == null ||
                handle.IsInvalid || handle.IsClosed)
                throw new InvalidDataException();
            FileAttributeTagInfo attributes;
            FileIdInfo identity;
            if (!GetFileInformationByHandleEx(
                    handle,
                    9,
                    out attributes,
                    (uint)Marshal.SizeOf(typeof(FileAttributeTagInfo))) ||
                !GetFileInformationByHandleEx(
                    handle,
                    18,
                    out identity,
                    (uint)Marshal.SizeOf(typeof(FileIdInfo))))
                throw new InvalidDataException();
            var bytes = new byte[16];
            WriteLittleEndian(bytes, 0, identity.FileIdLow);
            WriteLittleEndian(bytes, 8, identity.FileIdHigh);
            return new W24S6WorkerNativeDirectoryIdentity(
                identity.VolumeSerialNumber,
                bytes,
                attributes.FileAttributes);
        }

        internal W24S6WorkerTypedHash ComputeTypedHash(string typeTag)
        {
            var payload = new byte[24];
            WriteBigEndian(payload, 0, VolumeSerialNumber);
            Buffer.BlockCopy(fileId, 0, payload, 8, fileId.Length);
            return W24S6WorkerProtocolCodec.ComputeTypedHash(typeTag, payload);
        }

        internal bool FixedEquals(W24S6WorkerNativeDirectoryIdentity other)
        {
            if (other == null || VolumeSerialNumber != other.VolumeSerialNumber ||
                FileAttributes != other.FileAttributes || fileId.Length != other.fileId.Length) return false;
            var difference = 0;
            for (var index = 0; index < fileId.Length; index++) difference |= fileId[index] ^ other.fileId[index];
            return difference == 0;
        }

        private static void WriteLittleEndian(byte[] output, int offset, ulong value)
        {
            for (var index = 0; index < 8; index++) output[offset + index] = (byte)(value >> (index * 8));
        }

        private static void WriteBigEndian(byte[] output, int offset, ulong value)
        {
            for (var index = 0; index < 8; index++) output[offset + index] = (byte)(value >> ((7 - index) * 8));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileAttributeTagInfo
        {
            public uint FileAttributes;
            public uint ReparseTag;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileIdInfo
        {
            public ulong VolumeSerialNumber;
            public ulong FileIdLow;
            public ulong FileIdHigh;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle handle,
            int informationClass,
            out FileAttributeTagInfo information,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle handle,
            int informationClass,
            out FileIdInfo information,
            uint bufferSize);
    }

    internal static class W24S6WorkerProcessEpoch
    {
        internal static string ObserveCurrent()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) throw new PlatformNotSupportedException();
            NativeFileTime creation;
            NativeFileTime exit;
            NativeFileTime kernel;
            NativeFileTime user;
            if (!GetProcessTimes(GetCurrentProcess(), out creation, out exit, out kernel, out user))
                throw new InvalidDataException();
            var ticks = ((ulong)(uint)creation.High << 32) | (uint)creation.Low;
            return string.Format(
                CultureInfo.InvariantCulture,
                "winproc-{0}-{1:x16}",
                GetCurrentProcessId(),
                ticks);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileTime
        {
            public int Low;
            public int High;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(
            IntPtr process,
            out NativeFileTime creationTime,
            out NativeFileTime exitTime,
            out NativeFileTime kernelTime,
            out NativeFileTime userTime);
    }
}
