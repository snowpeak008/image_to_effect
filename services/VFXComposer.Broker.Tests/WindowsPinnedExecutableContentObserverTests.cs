using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Native;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class WindowsPinnedExecutableContentObserverTests
{
    private const string ServiceSidText = "S-1-5-80-101-202-303-404-505";
    private const string UserSidText = "S-1-5-21-1001-1002-1003-1004";
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareDelete = 0x00000004;
    private const uint DeleteAccess = 0x00010000;
    private const uint Synchronize = 0x00100000;
    private const uint FileTraverse = 0x00000020;
    private const uint FileReadAttributes = 0x00000080;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint ObjectDontReparse = 0x00001000;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const int FileAttributeTagInfoClass = 9;
    private const int FileIdInfoClass = 18;
    private const int FileDispositionInfoClass = 4;

    [TestMethod]
    public void ObserveProducesExactStreamingTypedHashAndPreservesBorrowedHandleOwnershipAndPosition()
    {
        RequireWindows();
        using var scratch = new ExactScratchTree();
        var payload = new byte[] { 0, 1, 2, 3, 4, 5, 250, 255 };
        var file = scratch.CreateExactFile("payload.bin", payload);
        using var source = scratch.OpenBorrowedFile(file, FileShare.Read);
        source.Position = 3;
        var borrowedHandle = source.SafeFileHandle;

        Assert.IsTrue(WindowsPinnedExecutableContentObserver.TryObserve(
            borrowedHandle,
            out var observation));
        Assert.IsNotNull(observation);
        Assert.AreEqual(3L, source.Position);
        Assert.IsFalse(borrowedHandle.IsClosed);
        Assert.AreEqual(payload[3], source.ReadByte());
        Assert.AreEqual(3L, source.Position - 1);

        var expected = TypedHash.Compute(
            WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
            payload);
        Assert.IsTrue(observation!.ContentHash.FixedTimeEquals(expected));
        Assert.AreEqual((long)payload.Length, observation.ByteLength);
        Assert.IsNotNull(observation.FileIdentity);

        source.Dispose();
        Assert.IsTrue(borrowedHandle.IsClosed);
        Assert.IsTrue(observation.ContentHash.FixedTimeEquals(expected));
    }

    [TestMethod]
    public void ObserveRejectsNullInvalidAndClosedBorrowedHandles()
    {
        RequireWindows();
        Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(null, out var nullObservation));
        Assert.IsNull(nullObservation);

        using (var invalid = new SafeFileHandle(IntPtr.Zero, ownsHandle: false))
        {
            Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(invalid, out var invalidObservation));
            Assert.IsNull(invalidObservation);
        }

        using var scratch = new ExactScratchTree();
        var file = scratch.CreateExactFile("closed.bin", new byte[] { 9 });
        var source = scratch.OpenBorrowedFile(file, FileShare.Read);
        var closed = source.SafeFileHandle;
        source.Dispose();

        Assert.IsTrue(closed.IsClosed);
        Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(closed, out var closedObservation));
        Assert.IsNull(closedObservation);
    }

    [TestMethod]
    public void ObserveRejectsDirectoryZeroOversizeAndReadShareConflicts()
    {
        RequireWindows();
        using var scratch = new ExactScratchTree();

        using (var directory = OpenBorrowedDirectory(scratch.Project))
        {
            Assert.IsFalse(directory.IsInvalid);
            Assert.IsFalse(directory.IsClosed);
            Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(directory, out var directoryObservation));
            Assert.IsNull(directoryObservation);
        }

        var emptyFile = scratch.CreateExactFile("empty.bin", Array.Empty<byte>());
        using (var empty = scratch.OpenBorrowedFile(emptyFile, FileShare.Read))
        {
            Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(empty.SafeFileHandle, out var emptyObservation));
            Assert.IsNull(emptyObservation);
        }

        var oversizedFile = scratch.CreateOversizedFile("oversized.bin");
        using (var oversized = scratch.OpenBorrowedFile(oversizedFile, FileShare.Read))
        {
            Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(
                oversized.SafeFileHandle,
                out var oversizedObservation));
            Assert.IsNull(oversizedObservation);
        }

        var conflictFile = scratch.CreateExactFile("share-conflict.bin", new byte[] { 7 });
        using (var conflict = scratch.OpenBorrowedFile(conflictFile, FileShare.None))
        {
            Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(
                conflict.SafeFileHandle,
                out var conflictObservation));
            Assert.IsNull(conflictObservation);
        }
    }

    [TestMethod]
    public void ObserveRejectsAFileWithMoreThanOneHardLink()
    {
        RequireWindows();
        using var scratch = new ExactScratchTree();
        var sourceFile = scratch.CreateExactFile("source.bin", new byte[] { 1, 2, 3 });
        scratch.CreateHardLink("alternate.bin", sourceFile);

        using var source = scratch.OpenBorrowedFile(sourceFile, FileShare.Read);
        Assert.IsFalse(WindowsPinnedExecutableContentObserver.TryObserve(
            source.SafeFileHandle,
            out var observation));
        Assert.IsNull(observation);
    }

    [TestMethod]
    public void CorrelationRequiresTheCompletePolicyBindingAndExactObservedHashAndLength()
    {
        RequireWindows();
        using var scratch = new ExactScratchTree();
        var payload = new byte[] { 10, 20, 30, 40, 50 };
        var file = scratch.CreateExactFile("correlation.bin", payload);
        using var source = scratch.OpenBorrowedFile(file, FileShare.Read);

        Assert.IsTrue(WindowsPinnedExecutableContentObserver.TryObserve(
            source.SafeFileHandle,
            out var observation));
        Assert.IsNotNull(observation);

        var profile = CreateProfile();
        var expectedIdentity = CreateExecutableIdentity(
            profile,
            observation!.ContentHash,
            observation.ByteLength);
        var matchingPolicy = new WindowsServiceExecutableIdentityPolicy(expectedIdentity);
        Assert.IsTrue(HostBootstrapExecutableContentCorrelation.MatchesObservedContent(
            observation,
            matchingPolicy,
            profile,
            expectedIdentity));

        var wrongContentIdentity = CreateExecutableIdentity(
            profile,
            TypedHash.Compute(
                WindowsServiceExecutableContentIdentity.ExecutableContentIdentityType,
                new byte[] { 50, 40, 30, 20, 10 }),
            observation.ByteLength);
        Assert.IsFalse(HostBootstrapExecutableContentCorrelation.MatchesObservedContent(
            observation,
            new WindowsServiceExecutableIdentityPolicy(wrongContentIdentity),
            profile,
            wrongContentIdentity));

        var wrongLengthIdentity = CreateExecutableIdentity(
            profile,
            observation.ContentHash,
            observation.ByteLength + 1);
        Assert.IsFalse(HostBootstrapExecutableContentCorrelation.MatchesObservedContent(
            observation,
            new WindowsServiceExecutableIdentityPolicy(wrongLengthIdentity),
            profile,
            wrongLengthIdentity));

        Assert.IsFalse(HostBootstrapExecutableContentCorrelation.MatchesObservedContent(
            observation,
            matchingPolicy,
            CreateProfile(),
            expectedIdentity));
    }

    [TestMethod]
    public void OutputAndCorrelationSurfaceAreInternalImmutableOpaqueAndNonAuthoritative()
    {
        var observerType = typeof(WindowsPinnedExecutableContentObserver);
        var observationType = typeof(WindowsPinnedExecutableContentObservation);
        var identityType = typeof(WindowsPinnedExecutableFileIdentity);
        var correlationType = typeof(HostBootstrapExecutableContentCorrelation);

        foreach (var type in new[] { observerType, observationType, identityType, correlationType })
        {
            Assert.IsFalse(type.IsPublic);
            Assert.IsTrue(type.IsSealed);
            Assert.IsFalse(type.GetCustomAttributes(typeof(SerializableAttribute), inherit: false).Any());
            Assert.IsFalse(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Any());
        }

        foreach (var type in new[] { observationType, identityType })
        {
            Assert.IsFalse(type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(property => property.SetMethod is not null));
            Assert.IsFalse(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => !field.IsInitOnly));
            Assert.IsFalse(type.GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(property => property.PropertyType == typeof(string) ||
                    property.PropertyType == typeof(IntPtr) ||
                    property.PropertyType == typeof(UIntPtr) ||
                    typeof(SafeHandle).IsAssignableFrom(property.PropertyType)));
        }

        var observe = observerType.GetMethod(
            "TryObserve",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(observe);
        Assert.AreEqual(typeof(bool), observe!.ReturnType);
        var observeParameters = observe.GetParameters();
        Assert.AreEqual(2, observeParameters.Length);
        Assert.AreEqual(typeof(SafeFileHandle), observeParameters[0].ParameterType);
        Assert.IsTrue(observeParameters[1].IsOut);

        var correlate = correlationType.GetMethod(
            "MatchesObservedContent",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(correlate);
        Assert.AreEqual(typeof(bool), correlate!.ReturnType);
        Assert.IsFalse(correlate.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(string) ||
            parameter.ParameterType == typeof(IntPtr) ||
            parameter.ParameterType == typeof(UIntPtr) ||
            typeof(SafeHandle).IsAssignableFrom(parameter.ParameterType) ||
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType)));
    }

    [TestMethod]
    public void ProductSourceUsesOnlyTheBoundedNativeHandleObservationSurface()
    {
        var observerSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Native/WindowsPinnedExecutableContentObserver.cs");
        var correlationSource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/HostBootstrapExecutableContentCorrelation.cs");
        var productSources = string.Concat(observerSource, "\n", correlationSource);

        StringAssert.Contains(observerSource, "ReOpenFile(");
        StringAssert.Contains(observerSource, "!TryRequireLocalDiskFileSystemDevice(reopenedHandle)");
        StringAssert.Contains(observerSource, "NtQueryVolumeInformationFile(");
        StringAssert.Contains(observerSource, "FileFsDeviceInformationClass");
        StringAssert.Contains(observerSource, "FileDeviceDisk");
        StringAssert.Contains(observerSource, "FileRemoteDevice");
        StringAssert.Contains(observerSource, "SetFilePointerEx(");
        StringAssert.Contains(observerSource, "basic.NumberOfLinks != 1");
        StringAssert.Contains(observerSource, "FileAttributeReparsePoint");
        StringAssert.Contains(observerSource, "eofBytesRead != 0");
        StringAssert.Contains(correlationSource,
            "WindowsServiceExecutableIdentityPolicyValidator.MatchesDormantCandidate(");

        foreach (var forbidden in new[]
                 {
                     "DuplicateHandle",
                      "DUPLICATE_CLOSE_SOURCE",
                      "GetFinalPathNameByHandle",
                      "GetVolumeInformation",
                      "GetDriveType",
                      "WNet",
                      "CreateFile",
                     "OpenProcess",
                     "GetCurrentProcess",
                     "OpenSCManager",
                     "CreateService",
                     "ChangeServiceConfig",
                     "DeleteService",
                     "RegOpenKey",
                     "Microsoft.Win32.Registry",
                     "NamedPipe",
                     "Socket",
                     "Tcp",
                     "Http",
                     "UnityEngine",
                     "UnityEditor",
                     "Directory.",
                     "Path.",
                     "File.",
                     "Environment.",
                     "Authenticode",
                     "Certificate",
                     "Signature",
                     "X509",
                 })
        {
            Assert.IsFalse(productSources.Contains(forbidden, StringComparison.Ordinal), forbidden);
        }

        var programSource = ReadWorkspaceSource("services/VFXComposer.Broker/Program.cs");
        var policySource = ReadWorkspaceSource(
            "services/VFXComposer.Broker/Configuration/BrokerPolicy.cs");
        Assert.IsFalse(programSource.Contains(
            "WindowsPinnedExecutableContentObserver",
            StringComparison.Ordinal));
        Assert.IsFalse(programSource.Contains(
            "HostBootstrapExecutableContentCorrelation",
            StringComparison.Ordinal));
        Assert.IsFalse(policySource.Contains(
            "WindowsPinnedExecutableContentObserver",
            StringComparison.Ordinal));
        Assert.IsFalse(policySource.Contains(
            "HostBootstrapExecutableContentCorrelation",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void NewNativeAbiIsBoundedToTheRequiredObservationImports()
    {
        var observerType = typeof(WindowsPinnedExecutableContentObserver);
        var imports = observerType
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Select(method => new
            {
                Method = method,
                Import = method.GetCustomAttribute<DllImportAttribute>(),
            })
            .Where(value => value.Import is not null)
            .Select(value => value.Import!.EntryPoint ?? value.Method.Name)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "GetFileInformationByHandle",
                "GetFileInformationByHandleEx",
                "GetFileType",
                "GetHandleInformation",
                "NtQueryVolumeInformationFile",
                "ReOpenFile",
                "ReadFile",
                "SetFilePointerEx",
                "SetHandleInformation",
            },
            imports);

        var nativeFileTime = observerType.GetNestedType(
            "NativeFileTime",
            BindingFlags.NonPublic);
        var basicInfo = observerType.GetNestedType(
            "ByHandleFileInformation",
            BindingFlags.NonPublic);
        var fileIdInfo = observerType.GetNestedType(
            "FileIdInfo",
            BindingFlags.NonPublic);
        var ioStatusBlock = observerType.GetNestedType(
            "IoStatusBlock",
            BindingFlags.NonPublic);
        var deviceInformation = observerType.GetNestedType(
            "FileFsDeviceInformation",
            BindingFlags.NonPublic);
        Assert.IsNotNull(nativeFileTime);
        Assert.IsNotNull(basicInfo);
        Assert.IsNotNull(fileIdInfo);
        Assert.IsNotNull(ioStatusBlock);
        Assert.IsNotNull(deviceInformation);
        Assert.AreEqual(8, Marshal.SizeOf(nativeFileTime!));
        Assert.AreEqual(52, Marshal.SizeOf(basicInfo!));
        Assert.AreEqual(24, Marshal.SizeOf(fileIdInfo!));
        Assert.AreEqual(IntPtr.Size * 2, Marshal.SizeOf(ioStatusBlock!));
        Assert.AreEqual(8, Marshal.SizeOf(deviceInformation!));
    }

    [TestMethod]
    public void VolumeDeviceGateAcceptsOnlyNativeLocalDiskFacts()
    {
        var observerType = typeof(WindowsPinnedExecutableContentObserver);
        var predicate = observerType.GetMethod(
            "IsLocalDiskFileSystemDevice",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(predicate);

        Assert.IsTrue((bool)predicate!.Invoke(null, new object[] { 0x00000007u, 0u })!);
        Assert.IsFalse((bool)predicate.Invoke(null, new object[] { 0x00000007u, 0x00000010u })!);
        Assert.IsFalse((bool)predicate.Invoke(null, new object[] { 0x00000008u, 0u })!);
    }

    [TestMethod]
    public void TestScratchCleanupUsesPinnedExactLeafHandlesInsteadOfPathDeletion()
    {
        var source = ReadWorkspaceSource(
            "services/VFXComposer.Broker.Tests/WindowsPinnedExecutableContentObserverTests.cs");
        var cleanupStart = source.IndexOf(
            "private sealed class ExactScratchTree",
            StringComparison.Ordinal);
        Assert.IsTrue(cleanupStart >= 0);
        var cleanupSource = source[cleanupStart..];

        foreach (var forbidden in new[]
                 {
                     string.Concat("File", ".Delete("),
                     string.Concat("File", ".Exists("),
                     string.Concat("Directory", ".Delete("),
                 })
        {
            Assert.IsFalse(cleanupSource.Contains(forbidden, StringComparison.Ordinal), forbidden);
        }

        StringAssert.Contains(cleanupSource, "NtOpenFile(");
        StringAssert.Contains(cleanupSource, "ObjectDontReparse");
        StringAssert.Contains(cleanupSource, "FileOpenReparsePoint");
        StringAssert.Contains(cleanupSource, "SetFileInformationByHandle(");
        StringAssert.Contains(cleanupSource, "PinnedScratchTreeCleanup.DeleteExactEmptyTree(");
    }

    private static ProductionTrustProfile CreateProfile(long generation = 17) =>
        new(
            "vfxcomposer-production",
            "broker-production",
            generation,
            WindowsSid.ParseService(ServiceSidText),
            WindowsSid.ParseUser(UserSidText),
            new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
            {
                [PeerRoles.Desktop] = new HashSet<TypedHash>
                {
                    ProcessImage("desktop-image"),
                },
                [PeerRoles.Worker] = new HashSet<TypedHash>
                {
                    ProcessImage("worker-image"),
                },
            });

    private static WindowsServiceExecutableContentIdentity CreateExecutableIdentity(
        ProductionTrustProfile profile,
        TypedHash contentHash,
        long byteLength) =>
        new(
            new WindowsServiceInstallationIdentity(
                profile,
                WindowsSid.ParseService(ServiceSidText),
                ProcessImage("broker-service-image"),
                profile.BrokerGeneration),
            contentHash,
            byteLength);

    private static TypedHash ProcessImage(string token) =>
        TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, token);

    private static SafeFileHandle OpenBorrowedDirectory(string directory) =>
        CreateFileW(
            directory,
            GenericRead,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);

    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Pinned executable byte observation is Windows-only.");
        }
    }

    private static string ReadWorkspaceSource(string repositoryRelativePath)
    {
        for (DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                directory.FullName,
                repositoryRelativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                return File.ReadAllText(candidate);
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        throw new AssertFailedException($"Could not locate {repositoryRelativePath} from the test output.");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);

    private sealed class ExactScratchTree : IDisposable
    {
        private readonly List<RecordedLeaf> _leaves = [];
        private readonly NativeNodeSnapshot _scratchAtCreation;
        private readonly NativeNodeSnapshot _repositoryAtCreation;
        private readonly NativeNodeSnapshot _projectAtCreation;
        private bool _disposed;

        internal ExactScratchTree()
        {
            Scratch = Path.Combine(
                Path.GetTempPath(),
                "vfxcomposer-broker-byte-observation-" + Guid.NewGuid().ToString("N"));
            Repository = Path.Combine(Scratch, "repository");
            Project = Path.Combine(Repository, "project");
            Directory.CreateDirectory(Project);
            using var physicalTree = OpenPhysicalTree();
            _scratchAtCreation = physicalTree.Scratch.Snapshot;
            _repositoryAtCreation = physicalTree.Repository.Snapshot;
            _projectAtCreation = physicalTree.Project.Snapshot;
        }

        internal string Scratch { get; }

        internal string Repository { get; }

        internal string Project { get; }

        internal string CreateExactFile(string leafName, ReadOnlySpan<byte> content)
        {
            var segment = ReserveLeafSegment(leafName);
            var file = Path.Combine(Project, segment);
            using (var writer = new FileStream(
                       file,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                writer.Write(content);
                writer.Flush(flushToDisk: true);
            }

            RecordLeafAtCreation(segment);
            return file;
        }

        internal string CreateOversizedFile(string leafName)
        {
            var segment = ReserveLeafSegment(leafName);
            var file = Path.Combine(Project, segment);
            using (var writer = new FileStream(
                       file,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                writer.SetLength(WindowsPinnedExecutableContentObserver.MaximumObservedByteLength + 1);
                writer.Flush(flushToDisk: true);
            }

            RecordLeafAtCreation(segment);
            return file;
        }

        internal FileStream OpenBorrowedFile(string file, FileShare share) =>
            new(file, FileMode.Open, FileAccess.Read, share);

        internal string CreateHardLink(string leafName, string existingFile)
        {
            var segment = ReserveLeafSegment(leafName);
            var sourceSegment = GetRecordedLeafSegment(existingFile);
            var link = Path.Combine(Project, segment);
            if (!CreateHardLinkW(link, existingFile, IntPtr.Zero))
            {
                throw new InvalidOperationException("Exact hard-link setup failed.");
            }

            var sourceSnapshot = CaptureCurrentLeaf(sourceSegment);
            var linkSnapshot = CaptureCurrentLeaf(segment);
            if (!sourceSnapshot.IsRegularNonReparseFile ||
                !linkSnapshot.IsRegularNonReparseFile ||
                sourceSnapshot.NumberOfLinks != 2 ||
                linkSnapshot.NumberOfLinks != 2 ||
                !sourceSnapshot.SameFileIdentity(linkSnapshot))
            {
                throw new InvalidOperationException("Exact hard-link identity setup failed.");
            }

            FindRecordedLeaf(sourceSegment).ReplaceExpectedSnapshot(sourceSnapshot);
            _leaves.Add(new RecordedLeaf(segment, linkSnapshot));
            return link;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var failures = new List<Exception>();
            PinnedDirectoryTree? physicalTree = null;
            var pinnedLeaves = new List<PinnedLeaf>(_leaves.Count);
            var allLeafPreflighted = false;
            try
            {
                physicalTree = PinPhysicalTree();
                foreach (var leaf in _leaves)
                {
                    pinnedLeaves.Add(OpenVerifiedLeaf(physicalTree.Project, leaf));
                }

                allLeafPreflighted = true;
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (allLeafPreflighted)
            {
                foreach (var leaf in pinnedLeaves.AsEnumerable().Reverse())
                {
                    try
                    {
                        leaf.RequestDeleteDisposition();
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                }
            }

            foreach (var leaf in pinnedLeaves.AsEnumerable().Reverse())
            {
                try
                {
                    leaf.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (physicalTree is not null)
            {
                physicalTree.DisposeAll(failures);
            }

            try
            {
                PinnedScratchTreeCleanup.DeleteExactEmptyTree(Project, Repository, Scratch);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            if (failures.Count != 0)
            {
                throw new AggregateException("Pinned scratch cleanup failed.", failures);
            }
        }

        private PinnedDirectoryTree PinPhysicalTree()
        {
            var physicalTree = OpenPhysicalTree();
            if (!_scratchAtCreation.MatchesPhysicalDirectory(physicalTree.Scratch.Snapshot) ||
                !_repositoryAtCreation.MatchesPhysicalDirectory(physicalTree.Repository.Snapshot) ||
                !_projectAtCreation.MatchesPhysicalDirectory(physicalTree.Project.Snapshot))
            {
                physicalTree.Dispose();
                throw new InvalidOperationException("Pinned scratch directory identity drifted.");
            }

            return physicalTree;
        }

        private PinnedDirectoryTree OpenPhysicalTree()
        {
            PinnedDirectory? scratch = null;
            PinnedDirectory? repository = null;
            PinnedDirectory? project = null;
            try
            {
                scratch = OpenAbsolutePhysicalDirectory(Scratch);
                repository = OpenPhysicalDirectoryChild(scratch, "repository");
                project = OpenPhysicalDirectoryChild(repository, "project");
                var result = new PinnedDirectoryTree(scratch, repository, project);
                scratch = null;
                repository = null;
                project = null;
                return result;
            }
            finally
            {
                project?.Dispose();
                repository?.Dispose();
                scratch?.Dispose();
            }
        }

        private void RecordLeafAtCreation(string segment)
        {
            var snapshot = CaptureCurrentLeaf(segment);
            if (!snapshot.IsRegularNonReparseFile || snapshot.NumberOfLinks != 1)
            {
                throw new InvalidOperationException("Exact scratch leaf creation identity is invalid.");
            }

            _leaves.Add(new RecordedLeaf(segment, snapshot));
        }

        private NativeNodeSnapshot CaptureCurrentLeaf(string segment)
        {
            using var physicalTree = PinPhysicalTree();
            using var leaf = OpenPinnedLeaf(physicalTree.Project, segment);
            return leaf.Snapshot;
        }

        private static PinnedDirectory OpenAbsolutePhysicalDirectory(string directory)
        {
            var handle = CreateFileW(
                directory,
                FileTraverse | FileReadAttributes | Synchronize,
                FileShareRead,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new InvalidOperationException("Scratch cleanup root could not be pinned.");
            }

            return AdoptPhysicalDirectory(handle);
        }

        private static PinnedDirectory OpenPhysicalDirectoryChild(
            PinnedDirectory parent,
            string segment)
        {
            var handle = OpenSingleSegment(
                parent,
                segment,
                FileTraverse | FileReadAttributes | Synchronize,
                FileShareRead,
                FileDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint);
            return AdoptPhysicalDirectory(handle);
        }

        private static PinnedDirectory AdoptPhysicalDirectory(SafeFileHandle handle)
        {
            try
            {
                var snapshot = QuerySnapshot(handle);
                if (!snapshot.IsPhysicalDirectory)
                {
                    throw new InvalidOperationException("Scratch cleanup directory is not physical.");
                }

                return new PinnedDirectory(handle, snapshot);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static PinnedLeaf OpenVerifiedLeaf(
            PinnedDirectory project,
            RecordedLeaf expected)
        {
            using var leaf = OpenPinnedLeaf(project, expected.Segment);
            if (!leaf.Snapshot.IsRegularNonReparseFile ||
                !leaf.Snapshot.FixedEquals(expected.ExpectedSnapshot))
            {
                throw new InvalidOperationException("Pinned scratch leaf identity drifted.");
            }

            return leaf.Detach();
        }

        private static PinnedLeaf OpenPinnedLeaf(PinnedDirectory project, string segment)
        {
            var handle = OpenSingleSegment(
                project,
                segment,
                DeleteAccess | FileReadAttributes | Synchronize,
                FileShareRead | FileShareDelete,
                FileNonDirectoryFile | FileSynchronousIoNonAlert | FileOpenReparsePoint);
            try
            {
                var snapshot = QuerySnapshot(handle);
                if (!snapshot.IsRegularNonReparseFile)
                {
                    throw new InvalidOperationException("Scratch cleanup leaf is not a physical file.");
                }

                return new PinnedLeaf(handle, snapshot);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static SafeFileHandle OpenSingleSegment(
            PinnedDirectory parent,
            string segment,
            uint desiredAccess,
            uint shareAccess,
            uint openOptions)
        {
            if (!IsExactSegment(segment))
            {
                throw new InvalidOperationException("Scratch cleanup segment is invalid.");
            }

            var rawHandle = IntPtr.Zero;
            var parentReferenceAdded = false;
            var nameBuffer = IntPtr.Zero;
            var unicodeBuffer = IntPtr.Zero;
            try
            {
                nameBuffer = Marshal.StringToHGlobalUni(segment);
                var name = new UnicodeString
                {
                    Length = checked((ushort)(segment.Length * 2)),
                    MaximumLength = checked((ushort)(segment.Length * 2 + 2)),
                    Buffer = nameBuffer,
                };
                unicodeBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
                Marshal.StructureToPtr(name, unicodeBuffer, false);
                parent.Handle.DangerousAddRef(ref parentReferenceAdded);
                var attributes = new ObjectAttributes
                {
                    Length = (uint)Marshal.SizeOf<ObjectAttributes>(),
                    RootDirectory = parent.Handle.DangerousGetHandle(),
                    ObjectName = unicodeBuffer,
                    Attributes = ObjectCaseInsensitive | ObjectDontReparse,
                };
                var status = NtOpenFile(
                    out rawHandle,
                    desiredAccess,
                    ref attributes,
                    out _,
                    shareAccess,
                    openOptions);
                if (status != 0 || rawHandle == IntPtr.Zero || rawHandle == new IntPtr(-1))
                {
                    CloseRawHandle(rawHandle);
                    rawHandle = IntPtr.Zero;
                    throw new InvalidOperationException("Scratch cleanup child could not be pinned.");
                }

                var handle = new SafeFileHandle(rawHandle, ownsHandle: true);
                rawHandle = IntPtr.Zero;
                return handle;
            }
            finally
            {
                CloseRawHandle(rawHandle);
                if (parentReferenceAdded)
                {
                    parent.Handle.DangerousRelease();
                }

                if (unicodeBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(unicodeBuffer);
                }

                if (nameBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(nameBuffer);
                }
            }
        }

        private static NativeNodeSnapshot QuerySnapshot(SafeFileHandle handle)
        {
            if (handle.IsClosed ||
                handle.IsInvalid ||
                !GetFileInformationByHandleEx(
                    handle,
                    FileAttributeTagInfoClass,
                    out FileAttributeTagInfo attributeTag,
                    (uint)Marshal.SizeOf<FileAttributeTagInfo>()) ||
                !GetFileInformationByHandleEx(
                    handle,
                    FileIdInfoClass,
                    out FileIdInfo fileId,
                    (uint)Marshal.SizeOf<FileIdInfo>()) ||
                !GetFileInformationByHandle(handle, out ByHandleFileInformation basic) ||
                basic.FileAttributes != attributeTag.FileAttributes)
            {
                throw new InvalidOperationException("Scratch cleanup native identity query failed.");
            }

            return new NativeNodeSnapshot(
                fileId.VolumeSerialNumber,
                fileId.FileIdLow,
                fileId.FileIdHigh,
                attributeTag.FileAttributes,
                basic.NumberOfLinks);
        }

        private static void CloseRawHandle(IntPtr handle)
        {
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            {
                new SafeFileHandle(handle, ownsHandle: true).Dispose();
            }
        }

        private string ReserveLeafSegment(string leafName)
        {
            if (!IsExactSegment(leafName) ||
                _leaves.Any(leaf => string.Equals(
                    leaf.Segment,
                    leafName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("Scratch leaf is invalid.", nameof(leafName));
            }

            return leafName;
        }

        private string GetRecordedLeafSegment(string file)
        {
            var segment = Path.GetFileName(file);
            if (!IsExactSegment(segment) ||
                !string.Equals(
                    Path.GetFullPath(file),
                    Path.GetFullPath(Path.Combine(Project, segment)),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Hard-link source is not an exact scratch leaf.", nameof(file));
            }

            _ = FindRecordedLeaf(segment);
            return segment;
        }

        private RecordedLeaf FindRecordedLeaf(string segment) =>
            _leaves.FirstOrDefault(leaf => string.Equals(
                leaf.Segment,
                segment,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Scratch leaf is not recorded.");

        private static bool IsExactSegment(string? segment) =>
            !string.IsNullOrWhiteSpace(segment) &&
            segment.Length <= 255 &&
            segment is not "." and not ".." &&
            segment.IndexOfAny(['/', '\\', ':']) < 0;

        private sealed class RecordedLeaf
        {
            internal RecordedLeaf(string segment, NativeNodeSnapshot expectedSnapshot)
            {
                Segment = segment;
                ExpectedSnapshot = expectedSnapshot;
            }

            internal string Segment { get; }

            internal NativeNodeSnapshot ExpectedSnapshot { get; private set; }

            internal void ReplaceExpectedSnapshot(NativeNodeSnapshot expectedSnapshot) =>
                ExpectedSnapshot = expectedSnapshot;
        }

        private sealed class PinnedDirectoryTree : IDisposable
        {
            private PinnedDirectory? _scratch;
            private PinnedDirectory? _repository;
            private PinnedDirectory? _project;

            internal PinnedDirectoryTree(
                PinnedDirectory scratch,
                PinnedDirectory repository,
                PinnedDirectory project)
            {
                _scratch = scratch;
                _repository = repository;
                _project = project;
            }

            internal PinnedDirectory Scratch => _scratch
                ?? throw new ObjectDisposedException(nameof(PinnedDirectoryTree));

            internal PinnedDirectory Repository => _repository
                ?? throw new ObjectDisposedException(nameof(PinnedDirectoryTree));

            internal PinnedDirectory Project => _project
                ?? throw new ObjectDisposedException(nameof(PinnedDirectoryTree));

            public void Dispose()
            {
                var failures = new List<Exception>();
                DisposeAll(failures);
                if (failures.Count != 0)
                {
                    throw new AggregateException("Pinned directory disposal failed.", failures);
                }
            }

            internal void DisposeAll(List<Exception> failures)
            {
                DisposeOne(Interlocked.Exchange(ref _project, null), failures);
                DisposeOne(Interlocked.Exchange(ref _repository, null), failures);
                DisposeOne(Interlocked.Exchange(ref _scratch, null), failures);
            }

            private static void DisposeOne(
                PinnedDirectory? directory,
                List<Exception> failures)
            {
                try
                {
                    directory?.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
        }

        private sealed class PinnedDirectory : IDisposable
        {
            private SafeFileHandle? _handle;

            internal PinnedDirectory(SafeFileHandle handle, NativeNodeSnapshot snapshot)
            {
                _handle = handle;
                Snapshot = snapshot;
            }

            internal SafeFileHandle Handle => _handle
                ?? throw new ObjectDisposedException(nameof(PinnedDirectory));

            internal NativeNodeSnapshot Snapshot { get; }

            public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Dispose();
        }

        private sealed class PinnedLeaf : IDisposable
        {
            private SafeFileHandle? _handle;

            internal PinnedLeaf(SafeFileHandle handle, NativeNodeSnapshot snapshot)
            {
                _handle = handle;
                Snapshot = snapshot;
            }

            internal NativeNodeSnapshot Snapshot { get; }

            internal PinnedLeaf Detach()
            {
                var handle = Interlocked.Exchange(ref _handle, null)
                    ?? throw new ObjectDisposedException(nameof(PinnedLeaf));
                return new PinnedLeaf(handle, Snapshot);
            }

            internal void RequestDeleteDisposition()
            {
                var disposition = new FileDispositionInfo { DeleteFile = true };
                if (!SetFileInformationByHandle(
                        _handle ?? throw new ObjectDisposedException(nameof(PinnedLeaf)),
                        FileDispositionInfoClass,
                        ref disposition,
                        (uint)Marshal.SizeOf<FileDispositionInfo>()))
                {
                    throw new InvalidOperationException("Pinned scratch leaf delete disposition failed.");
                }
            }

            public void Dispose() => Interlocked.Exchange(ref _handle, null)?.Dispose();
        }

        private readonly struct NativeNodeSnapshot
        {
            internal NativeNodeSnapshot(
                ulong volumeSerialNumber,
                ulong fileIdLow,
                ulong fileIdHigh,
                uint fileAttributes,
                uint numberOfLinks)
            {
                VolumeSerialNumber = volumeSerialNumber;
                FileIdLow = fileIdLow;
                FileIdHigh = fileIdHigh;
                FileAttributes = fileAttributes;
                NumberOfLinks = numberOfLinks;
            }

            internal ulong VolumeSerialNumber { get; }

            internal ulong FileIdLow { get; }

            internal ulong FileIdHigh { get; }

            internal uint FileAttributes { get; }

            internal uint NumberOfLinks { get; }

            internal bool IsPhysicalDirectory =>
                (FileAttributes & FileAttributeDirectory) != 0 &&
                (FileAttributes & FileAttributeReparsePoint) == 0;

            internal bool IsRegularNonReparseFile =>
                (FileAttributes & (FileAttributeDirectory | FileAttributeReparsePoint)) == 0;

            internal bool SameFileIdentity(NativeNodeSnapshot other) =>
                VolumeSerialNumber == other.VolumeSerialNumber &&
                FileIdLow == other.FileIdLow &&
                FileIdHigh == other.FileIdHigh;

            internal bool MatchesPhysicalDirectory(NativeNodeSnapshot other) =>
                IsPhysicalDirectory &&
                other.IsPhysicalDirectory &&
                SameFileIdentity(other) &&
                FileAttributes == other.FileAttributes;

            internal bool FixedEquals(NativeNodeSnapshot other) =>
                SameFileIdentity(other) &&
                FileAttributes == other.FileAttributes &&
                NumberOfLinks == other.NumberOfLinks;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UnicodeString
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ObjectAttributes
        {
            public uint Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoStatusBlock
        {
            public IntPtr StatusOrPointer;
            public UIntPtr Information;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeFileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public NativeFileTime CreationTime;
            public NativeFileTime LastAccessTime;
            public NativeFileTime LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileDispositionInfo
        {
            [MarshalAs(UnmanagedType.U1)]
            public bool DeleteFile;
        }

        [DllImport("ntdll.dll", ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        private static extern int NtOpenFile(
            out IntPtr fileHandle,
            uint desiredAccess,
            ref ObjectAttributes objectAttributes,
            out IoStatusBlock ioStatusBlock,
            uint shareAccess,
            uint openOptions);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle fileHandle,
            out ByHandleFileInformation fileInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle fileHandle,
            int fileInformationClass,
            out FileAttributeTagInfo fileInformation,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(
            SafeFileHandle fileHandle,
            int fileInformationClass,
            out FileIdInfo fileInformation,
            uint bufferSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(
            SafeFileHandle fileHandle,
            int fileInformationClass,
            ref FileDispositionInfo fileInformation,
            uint bufferSize);
    }
}
