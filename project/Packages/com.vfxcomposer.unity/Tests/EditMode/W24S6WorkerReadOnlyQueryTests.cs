using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.External;
using VFXComposer.Editor.W24.S6.Worker;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Tests.EditMode
{
    [TestFixture]
    public sealed class W24S6WorkerReadOnlyQueryTests
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private readonly List<string> files = new List<string>();
        private readonly List<string> directories = new List<string>();
        private readonly List<string> reparseDirectories = new List<string>();
        private readonly Dictionary<string, byte[]> documents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        private string scratchRoot;
        private string repositoryRoot;
        private string projectRoot;
        private W24S6WorkerAuthenticatedSession session;
        private W24S6WorkerProjectHandleLease lease;

        [SetUp]
        public void CreateExactHandleOwnedFixture()
        {
            Assert.That(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), Is.True);
            W24S6WindowsReadOnlyFile.ResetOpenAttemptCountForTests();
            var activeProject = Directory.GetParent(Application.dataPath).FullName;
            var activeRepository = Directory.GetParent(activeProject).FullName;
            scratchRoot = Path.Combine(
                activeRepository,
                "w24-worker-read-query-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(scratchRoot), Is.False);
            repositoryRoot = Path.Combine(scratchRoot, "repository");
            projectRoot = Path.Combine(repositoryRoot, "project");
            try
            {
                EnsureDirectory(scratchRoot);
                EnsureDirectory(repositoryRoot);
                EnsureDirectory(projectRoot);
                AddDocument(
                    W24S6WorkerReadQueryCodec.LibraryIndexKind,
                    "project",
                    StrictUtf8.GetBytes("{\"schema\":\"vfxcomposer.library-index/1\",\"items\":[]}"));
                AddDocument(
                    W24S6WorkerReadQueryCodec.ManifestKind,
                    "effect_fire",
                    StrictUtf8.GetBytes("{\"effectId\":\"effect_fire\",\"buildHash\":\"sha256:" + new string('a', 64) + "\"}"));
                AddDocument(
                    W24S6WorkerReadQueryCodec.ContractKind,
                    "effect_fire",
                    StrictUtf8.GetBytes("{\"schema\":\"vfx-design-contract/1\",\"effectId\":\"effect_fire\"}"));
                AddDocument(
                    W24S6WorkerReadQueryCodec.TraceKind,
                    "effect_fire",
                    StrictUtf8.GetBytes("{\"schema\":\"vfx-implementation-trace/1\",\"effectId\":\"effect_fire\"}"));

                session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-read-session", 41);
                using (var handles = OpenHandleSet())
                {
                    var grant = CreateGrant(handles);
                    string diagnostic;
                    Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                        session,
                        grant,
                        out lease,
                        out diagnostic), Is.True);
                    Assert.That(diagnostic, Is.Empty);
                    handles.Relinquish();
                }
                W24S6WindowsReadOnlyFile.ResetOpenAttemptCountForTests();
            }
            catch
            {
                CleanupExactFixture();
                throw;
            }
        }

        [TearDown]
        public void RemoveOnlyExactHandleOwnedFixture()
        {
            W24S6WindowsReadOnlyFile.ResetOpenAttemptCountForTests();
            CleanupExactFixture();
        }

        [TestCase("LIBRARY_INDEX", "project")]
        [TestCase("MANIFEST", "effect_fire")]
        [TestCase("CONTRACT", "effect_fire")]
        [TestCase("TRACE", "effect_fire")]
        public void FourClosedDocumentKindsReturnExactBytesAndTypedIdentity(
            string documentKind,
            string documentId)
        {
            var resultBytes = W24S6WorkerReadQueryHandler.Handle(
                Query(documentKind, documentId, null),
                lease);
            var result = DecodeResult(resultBytes);
            Assert.That((bool)result["accepted"], Is.True);
            Assert.That((string)result["requestId"], Is.EqualTo("read-query"));
            Assert.That((string)result["documentKind"], Is.EqualTo(documentKind));
            Assert.That((string)result["documentId"], Is.EqualTo(documentId));
            Assert.That(result["diagnostic"].Type, Is.EqualTo(JTokenType.Null));
            var expected = documents[DocumentKey(documentKind, documentId)];
            Assert.That(Convert.FromBase64String((string)result["contentBase64"]), Is.EqualTo(expected));
            Assert.That((int)result["byteLength"], Is.EqualTo(expected.Length));
            var contentHash = (JObject)result["contentHash"];
            var expectedHash = W24S6WorkerProtocolCodec.ComputeTypedHash(
                W24S6WorkerReadQueryCodec.ContentHashType,
                expected);
            Assert.That((string)contentHash["typeTag"], Is.EqualTo(expectedHash.TypeTag));
            Assert.That((string)contentHash["digest"], Is.EqualTo(expectedHash.Digest));
            Assert.That(resultBytes, Has.None.EqualTo((byte)'\\'));
            Assert.That(StrictUtf8.GetString(resultBytes), Does.Not.Contain(scratchRoot));
        }

        [TestCase("lease")]
        [TestCase("generation")]
        [TestCase("project")]
        public void WrongLeaseIdentityRejectsBeforeAnyTargetOpen(string mutation)
        {
            var query = QueryRoot(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null);
            if (mutation == "lease") query["leaseId"] = "other-lease";
            if (mutation == "generation") query["leaseGeneration"] = lease.LeaseGeneration + 1;
            if (mutation == "project")
                query["projectIdentity"] = TypedHash(
                    W24S6WorkerProtocolCodec.ProjectIdentityType,
                    W24S6WorkerProtocolCodec.ComputeTypedHash(
                        W24S6WorkerProtocolCodec.ProjectIdentityType,
                        new byte[] { 9 }).Digest);

            var result = DecodeResult(W24S6WorkerReadQueryHandler.Handle(Encode(query), lease));
            AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectLeaseRejected);
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void ExpectedContentHashMismatchReturnsNoBytes()
        {
            var mismatch = W24S6WorkerProtocolCodec.ComputeTypedHash(
                W24S6WorkerReadQueryCodec.ContentHashType,
                StrictUtf8.GetBytes("different"));
            var result = DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", mismatch),
                lease));
            AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectDocumentContentMismatch);
        }

        [Test]
        public void MissingOversizedInvalidUtf8AndJsonBoundaryDocumentsFailClosed()
        {
            var manifestPath = AbsoluteTarget(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire");
            File.Delete(manifestPath);
            files.Remove(manifestPath);
            AssertRejected(
                DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                    Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                    lease)),
                W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

            AddDocument(
                W24S6WorkerReadQueryCodec.ManifestKind,
                "effect_fire",
                new byte[W24S6WorkerProjectHandleLease.MaximumReadBytes + 1]);
            AssertRejected(
                DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                    Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                    lease)),
                W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

            File.WriteAllBytes(manifestPath, new byte[] { 0xc3, 0x28 });
            AssertRejected(
                DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                    Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                    lease)),
                W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

            File.WriteAllBytes(manifestPath, StrictUtf8.GetBytes("{]"));
            AssertRejected(
                DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                    Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                    lease)),
                W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

            File.WriteAllBytes(manifestPath, StrictUtf8.GetBytes("{\"a\":1,\"\\u0061\":2}"));
            AssertRejected(
                DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                    Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                    lease)),
                W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

            var exactLimit = StrictUtf8.GetBytes(
                "{\"pad\":\"" +
                new string('a', W24S6WorkerProjectHandleLease.MaximumReadBytes - 10) +
                "\"}");
            Assert.That(exactLimit.Length, Is.EqualTo(W24S6WorkerProjectHandleLease.MaximumReadBytes));
            File.WriteAllBytes(manifestPath, exactLimit);
            var accepted = DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                lease));
            Assert.That((bool)accepted["accepted"], Is.True);
            Assert.That((int)accepted["byteLength"], Is.EqualTo(exactLimit.Length));
            Assert.That(Convert.FromBase64String((string)accepted["contentBase64"]), Is.EqualTo(exactLimit));
        }

        [Test]
        public void MalformedUnknownAndPathShapedQueriesRejectBeforeContentOpen()
        {
            var pathShaped = QueryRoot(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null);
            pathShaped["documentId"] = "../effect_fire";
            Assert.Throws<W24S6WorkerProtocolException>(() =>
                W24S6WorkerReadQueryHandler.Handle(Encode(pathShaped), lease));

            var unknown = QueryRoot(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null);
            unknown["callerPath"] = "C:/untrusted";
            Assert.Throws<W24S6WorkerProtocolException>(() =>
                W24S6WorkerReadQueryHandler.Handle(Encode(unknown), lease));

            var validText = StrictUtf8.GetString(Query(
                W24S6WorkerReadQueryCodec.ManifestKind,
                "effect_fire",
                null));
            var duplicate = StrictUtf8.GetBytes(validText.Replace(
                "{\"protocolVersion\":",
                "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"\\u0070rotocolVersion\":"));
            Assert.Throws<W24S6WorkerProtocolException>(() =>
                W24S6WorkerReadQueryHandler.Handle(duplicate, lease));

            var oversizedGeneration = QueryRoot(
                W24S6WorkerReadQueryCodec.ManifestKind,
                "effect_fire",
                null);
            oversizedGeneration["leaseGeneration"] = JToken.Parse("9223372036854775808");
            Assert.Throws<W24S6WorkerProtocolException>(() =>
                W24S6WorkerReadQueryHandler.Handle(Encode(oversizedGeneration), lease));
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void LocalJunctionParentIsRejectedBeforeLeafAndOutsideBytesRemainUnread()
        {
            var contracts = Path.Combine(repositoryRoot, "docs", "vfx-contracts");
            var contractPath = AbsoluteTarget(W24S6WorkerReadQueryCodec.ContractKind, "effect_fire");
            File.Delete(contractPath);
            files.Remove(contractPath);
            Directory.Delete(contracts, false);
            directories.Remove(contracts);

            var outside = Path.Combine(scratchRoot, "outside");
            EnsureDirectory(outside);
            var outsideFile = Path.Combine(outside, "effect_fire.contract.json");
            File.WriteAllBytes(outsideFile, StrictUtf8.GetBytes("{\"outside\":true}"));
            files.Add(outsideFile);
            var before = File.ReadAllBytes(outsideFile);
            EnsureDirectory(contracts);
            int error;
            Assert.That(CreateDirectoryJunction(contracts, outside, out error), Is.True,
                "Local NTFS junction creation failed. Win32=" + error);
            reparseDirectories.Add(contracts);

            var result = DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                Query(W24S6WorkerReadQueryCodec.ContractKind, "effect_fire", null),
                lease));
            AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.Zero);
            Assert.That(File.ReadAllBytes(outsideFile), Is.EqualTo(before));
        }

        [Test]
        public void SessionRevocationWaitsForInFlightReadAndResultFailsClosed()
        {
            using (var readEntered = new ManualResetEventSlim(false))
            using (var releaseRead = new ManualResetEventSlim(false))
            {
                Task<byte[]> read = null;
                Task revoke = null;
                try
                {
                    W24S6WindowsReadOnlyFile.BeforePostReadIdentityReplayForTests = () =>
                    {
                        readEntered.Set();
                        if (!releaseRead.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                    };
                    read = Task.Run(() => W24S6WorkerReadQueryHandler.Handle(
                        Query(W24S6WorkerReadQueryCodec.TraceKind, "effect_fire", null),
                        lease));
                    Assert.That(readEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    revoke = Task.Run(session.Dispose);
                    Assert.That(revoke.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
                }
                finally
                {
                    releaseRead.Set();
                    if (read != null) Assert.That(read.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    if (revoke != null) Assert.That(revoke.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    W24S6WindowsReadOnlyFile.BeforePostReadIdentityReplayForTests = null;
                }
                AssertRejected(
                    DecodeResult(read.Result),
                    W24S6WorkerReadQueryCodec.ProjectLeaseRejected);
                Assert.That(lease.IsUsable, Is.False);
            }
        }

        [Test]
        public void ExistingWriterExcludesPinnedReadWithoutLeakingContent()
        {
            var path = AbsoluteTarget(W24S6WorkerReadQueryCodec.TraceKind, "effect_fire");
            using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                var result = DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                    Query(W24S6WorkerReadQueryCodec.TraceKind, "effect_fire", null),
                    lease));
                AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
            }
        }

        [Test]
        public void ResultAndLeaseSurfaceExposeNoPathOrNativeHandle()
        {
            var result = DecodeResult(W24S6WorkerReadQueryHandler.Handle(
                Query(W24S6WorkerReadQueryCodec.ManifestKind, "effect_fire", null),
                lease));
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "protocolVersion", "messageKind", "requestId", "accepted", "projectIdentity",
                    "documentKind", "documentId", "contentHash", "byteLength", "contentBase64", "diagnostic"
                },
                result.Properties().Select(value => value.Name));
            Assert.That(result.DescendantsAndSelf().OfType<JProperty>()
                .Any(value => value.Name.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              value.Name.IndexOf("handle", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            var leaseSurface = typeof(W24S6WorkerProjectHandleLease)
                .GetMethods(System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic)
                .Where(value => !value.IsPrivate &&
                                value.Name.StartsWith("TryRead", StringComparison.Ordinal))
                .ToArray();
            Assert.That(leaseSurface.Length, Is.EqualTo(2));
            Assert.That(leaseSurface.All(value => value.GetParameters()
                .All(parameter => parameter.ParameterType != typeof(IntPtr) &&
                                  parameter.ParameterType != typeof(SafeFileHandle))), Is.True);
        }

        private void AddDocument(string kind, string id, byte[] bytes)
        {
            var path = AbsoluteTarget(kind, id);
            EnsureDirectory(Path.GetDirectoryName(path));
            RequireContained(path);
            if (!files.Contains(path, StringComparer.OrdinalIgnoreCase)) files.Add(path);
            File.WriteAllBytes(path, bytes);
            documents[DocumentKey(kind, id)] = (byte[])bytes.Clone();
        }

        private byte[] Query(string kind, string id, W24S6WorkerTypedHash expected)
        {
            return Encode(QueryRoot(kind, id, expected));
        }

        private JObject QueryRoot(string kind, string id, W24S6WorkerTypedHash expected)
        {
            return new JObject
            {
                ["protocolVersion"] = W24S6WorkerProtocolCodec.ProtocolVersion,
                ["messageKind"] = W24S6WorkerReadQueryCodec.QueryKind,
                ["requestId"] = "read-query",
                ["leaseId"] = lease.LeaseId,
                ["projectIdentity"] = TypedHash(lease.ProjectIdentity.TypeTag, lease.ProjectIdentity.Digest),
                ["leaseGeneration"] = lease.LeaseGeneration,
                ["documentKind"] = kind,
                ["documentId"] = id,
                ["expectedContentHash"] = expected == null
                    ? JValue.CreateNull()
                    : TypedHash(expected.TypeTag, expected.Digest)
            };
        }

        private static JObject TypedHash(string typeTag, string digest)
        {
            return new JObject
            {
                ["typeTag"] = typeTag,
                ["digest"] = digest
            };
        }

        private static byte[] Encode(JObject value)
        {
            return StrictUtf8.GetBytes(value.ToString(Formatting.None));
        }

        private static JObject DecodeResult(byte[] value)
        {
            return JObject.Parse(StrictUtf8.GetString(value));
        }

        private static void AssertRejected(JObject result, string code)
        {
            Assert.That((bool)result["accepted"], Is.False);
            Assert.That(result["contentHash"].Type, Is.EqualTo(JTokenType.Null));
            Assert.That((int)result["byteLength"], Is.Zero);
            Assert.That(result["contentBase64"].Type, Is.EqualTo(JTokenType.Null));
            var diagnostic = (JObject)result["diagnostic"];
            Assert.That((string)diagnostic["code"], Is.EqualTo(code));
            Assert.That(diagnostic.Properties().Select(value => value.Name), Is.EquivalentTo(
                new[] { "protocolVersion", "messageKind", "code", "severity", "message", "retryable" }));
        }

        private string AbsoluteTarget(string kind, string id)
        {
            var target = W24S6WorkerReadOnlyHost.Resolve(kind, id);
            var root = target.UseProjectRoot ? projectRoot : repositoryRoot;
            return Path.GetFullPath(Path.Combine(
                root,
                target.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string DocumentKey(string kind, string id)
        {
            return kind + "|" + id;
        }

        private W24S6WorkerProjectHandleGrant CreateGrant(TestHandleSet handles)
        {
            var volume = W24S6WorkerProjectHandleLease.ObserveIdentityForTests(
                handles.Volume,
                W24S6WorkerProtocolCodec.VolumeIdentityType);
            var repository = W24S6WorkerProjectHandleLease.ObserveIdentityForTests(
                handles.Repository,
                W24S6WorkerProtocolCodec.DirectoryIdentityType);
            var project = W24S6WorkerProjectHandleLease.ObserveIdentityForTests(
                handles.Project,
                W24S6WorkerProtocolCodec.DirectoryIdentityType);
            var projectIdentity = W24S6WorkerProjectHandleLease.ComputeProjectIdentityForTests(
                volume,
                repository,
                project);
            return W24S6WorkerProtocolCodec.DecodeGrant(
                W24S6WorkerProtocolCodec.CreateGrantForTests(
                    "worker-read-grant",
                    "worker-read-lease",
                    "worker-read-project",
                    projectIdentity,
                    volume,
                    repository,
                    project,
                    session.BrokerGeneration,
                    13,
                    17,
                    session.SessionId,
                    session.ProcessEpoch,
                    EncodeHandle(handles.Volume),
                    EncodeHandle(handles.Repository),
                    EncodeHandle(handles.Project)));
        }

        private TestHandleSet OpenHandleSet()
        {
            return new TestHandleSet(
                OpenDirectory(scratchRoot),
                OpenDirectory(repositoryRoot),
                OpenDirectory(projectRoot));
        }

        private static IntPtr OpenDirectory(string path)
        {
            const uint access = 0x001000A0;
            const uint shareRead = 0x00000001;
            const uint openExisting = 3;
            const uint flags = 0x02000000 | 0x00200000;
            var handle = CreateFile(path, access, shareRead, IntPtr.Zero, openExisting, flags, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) throw new InvalidDataException();
            return handle;
        }

        private static string EncodeHandle(IntPtr value)
        {
            return unchecked((ulong)value.ToInt64()).ToString("x16");
        }

        private void EnsureDirectory(string path)
        {
            RequireContained(path);
            if (Directory.Exists(path)) return;
            var missing = new Stack<string>();
            for (var current = path; !string.IsNullOrEmpty(current) && !Directory.Exists(current); current = Path.GetDirectoryName(current))
                missing.Push(current);
            while (missing.Count > 0)
            {
                var value = missing.Pop();
                RequireContained(value);
                Directory.CreateDirectory(value);
                directories.Add(value);
            }
        }

        private void CleanupExactFixture()
        {
            if (lease != null) lease.Dispose();
            if (session != null) session.Dispose();
            lease = null;
            session = null;
            foreach (var path in files.Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(value => value.Length))
            {
                if (!File.Exists(path)) continue;
                RequireContained(path);
                File.Delete(path);
            }
            foreach (var path in reparseDirectories.Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(value => value.Length))
            {
                if (!Directory.Exists(path)) continue;
                RequireContained(path);
                Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0, Is.True);
                Directory.Delete(path, false);
            }
            foreach (var path in directories.Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(value => value.Length))
            {
                if (!Directory.Exists(path)) continue;
                RequireContained(path);
                Assert.That(Directory.EnumerateFileSystemEntries(path), Is.Empty);
                Directory.Delete(path, false);
            }
            if (!string.IsNullOrEmpty(scratchRoot)) Assert.That(Directory.Exists(scratchRoot), Is.False);
            files.Clear();
            directories.Clear();
            reparseDirectories.Clear();
            documents.Clear();
        }

        private void RequireContained(string path)
        {
            if (string.IsNullOrEmpty(scratchRoot)) return;
            var root = Path.GetFullPath(scratchRoot).TrimEnd(Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) &&
                !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Scratch operation escaped its exact fixture root.");
        }

        private static bool CreateDirectoryJunction(string junctionPath, string targetPath, out int error)
        {
            const uint genericWrite = 0x40000000;
            const uint shareReadWriteDelete = 0x00000007;
            const uint openExisting = 3;
            const uint backupSemantics = 0x02000000;
            const uint openReparsePoint = 0x00200000;
            const uint fsctlSetReparsePoint = 0x000900A4;
            const uint mountPointTag = 0xA0000003;
            var substituteName = "\\??\\" + targetPath;
            var printName = targetPath;
            var substituteBytes = Encoding.Unicode.GetBytes(substituteName);
            var printBytes = Encoding.Unicode.GetBytes(printName);
            var pathBytes = Encoding.Unicode.GetBytes(substituteName + "\0" + printName + "\0");
            var buffer = new byte[16 + pathBytes.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(mountPointTag), 0, buffer, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(checked((ushort)(8 + pathBytes.Length))), 0, buffer, 4, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)0), 0, buffer, 8, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(checked((ushort)substituteBytes.Length)), 0, buffer, 10, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(checked((ushort)(substituteBytes.Length + 2))), 0, buffer, 12, 2);
            Buffer.BlockCopy(BitConverter.GetBytes(checked((ushort)printBytes.Length)), 0, buffer, 14, 2);
            Buffer.BlockCopy(pathBytes, 0, buffer, 16, pathBytes.Length);
            using (var handle = new SafeFileHandle(CreateFile(
                       junctionPath,
                       genericWrite,
                       shareReadWriteDelete,
                       IntPtr.Zero,
                       openExisting,
                       backupSemantics | openReparsePoint,
                       IntPtr.Zero), true))
            {
                if (handle.IsInvalid)
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }
                uint returned;
                var created = DeviceIoControl(
                    handle,
                    fsctlSetReparsePoint,
                    buffer,
                    buffer.Length,
                    IntPtr.Zero,
                    0,
                    out returned,
                    IntPtr.Zero);
                error = created ? 0 : Marshal.GetLastWin32Error();
                return created;
            }
        }

        private sealed class TestHandleSet : IDisposable
        {
            internal TestHandleSet(IntPtr volume, IntPtr repository, IntPtr project)
            {
                Volume = volume;
                Repository = repository;
                Project = project;
            }

            internal IntPtr Volume { get; private set; }
            internal IntPtr Repository { get; private set; }
            internal IntPtr Project { get; private set; }

            internal void Relinquish()
            {
                Volume = IntPtr.Zero;
                Repository = IntPtr.Zero;
                Project = IntPtr.Zero;
            }

            public void Dispose()
            {
                Close(Volume);
                Close(Repository);
                Close(Project);
                Relinquish();
            }

            private static void Close(IntPtr value)
            {
                if (value != IntPtr.Zero && value != new IntPtr(-1)) CloseHandle(value);
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            uint controlCode,
            byte[] inputBuffer,
            int inputBufferBytes,
            IntPtr outputBuffer,
            int outputBufferBytes,
            out uint bytesReturned,
            IntPtr overlapped);
    }
}
