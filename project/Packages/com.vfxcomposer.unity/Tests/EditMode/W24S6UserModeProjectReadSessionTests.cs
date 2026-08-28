using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.Worker;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S6UserModeProjectReadSessionTests
    {
        private const string PathIdentityVersion =
            "vfxcomposer.user-mode-project-path-correlation/1\0";
        private const long BrokerGeneration = 401;
        private const long RegistrationGeneration = 51;
        private const long EnrollmentGeneration = 52;
        private const string WorkerSessionId = "um-session-401-00112233445566778899aabbccddeeff";
        private const string WorkerProcessEpoch = "winproc-123-0000000000000123";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Test]
        public void ExplicitProjectIdentityAllowsOnlyBoundedLibraryAndManifestReads()
        {
            using (var project = TestProject.Create())
            using (var current = new CurrentDirectoryScope(project.Root))
            using (var session = Open(project.Root))
            {
                var library = DecodeResult(session.Handle(Query(
                    session,
                    W24S6WorkerReadQueryCodec.LibraryIndexKind,
                    "project")));
                var manifest = DecodeResult(session.Handle(Query(
                    session,
                    W24S6WorkerReadQueryCodec.ManifestKind,
                    "build-01")));

                Assert.That((bool)library["accepted"], Is.True);
                Assert.That(DecodeContent(library), Is.EqualTo(project.LibraryBytes));
                Assert.That((bool)manifest["accepted"], Is.True);
                Assert.That(DecodeContent(manifest), Is.EqualTo(project.ManifestBytes));
                Assert.That((int)manifest["byteLength"], Is.EqualTo(project.ManifestBytes.Length));
                Assert.That((string)manifest["contentHash"]["typeTag"],
                    Is.EqualTo(W24S6WorkerReadQueryCodec.ContentHashType));
            }
        }

        [Test]
        public void LocatorMustMatchIndependentCanonicalWorkingDirectoryAndSessionCorrelation()
        {
            using (var selected = TestProject.Create())
            using (var wrong = TestProject.Create())
            using (var current = new CurrentDirectoryScope(wrong.Root))
            {
                Assert.Throws<InvalidDataException>(() => Open(selected.Root));
            }

            using (var selected = TestProject.Create())
            using (var current = new CurrentDirectoryScope(selected.Root))
            {
                var locator = Locator(selected.Root);
                Assert.Throws<InvalidDataException>(() => W24S6UserModeProjectReadSession.Open(
                    locator, BrokerGeneration + 1, WorkerSessionId, WorkerProcessEpoch));
                Assert.Throws<InvalidDataException>(() => W24S6UserModeProjectReadSession.Open(
                    locator, BrokerGeneration, WorkerSessionId + "x", WorkerProcessEpoch));
                Assert.Throws<InvalidDataException>(() => W24S6UserModeProjectReadSession.Open(
                    locator, BrokerGeneration, WorkerSessionId, WorkerProcessEpoch + "x"));
            }
        }

        [Test]
        public void ContractTraceAndTraversalRemainUnavailableWithoutFallback()
        {
            using (var project = TestProject.Create())
            using (var current = new CurrentDirectoryScope(project.Root))
            using (var session = Open(project.Root))
            {
                foreach (var kind in new[]
                         {
                             W24S6WorkerReadQueryCodec.ContractKind,
                             W24S6WorkerReadQueryCodec.TraceKind,
                         })
                {
                    var result = DecodeResult(session.Handle(Query(session, kind, "document-01")));
                    AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
                }

                Assert.Throws<W24S6WorkerProtocolException>(() => session.Handle(
                    Query(session, W24S6WorkerReadQueryCodec.ManifestKind, "../escape")));
            }
        }

        [Test]
        public void TargetReparseDirectoryIsRejectedBeforeProjectContentRead()
        {
            using (var project = TestProject.Create())
            using (var outside = TestProject.Create())
            using (var current = new CurrentDirectoryScope(project.Root))
            using (var session = Open(project.Root))
            {
                var manifests = Path.Combine(project.Root, "ProjectSettings", "VFXComposer", "BuildManifests");
                Directory.Delete(manifests, true);
                var outsideManifests = Path.Combine(outside.Root, "ProjectSettings", "VFXComposer", "BuildManifests");
                CreateJunction(manifests, outsideManifests);
                try
                {
                    var result = DecodeResult(session.Handle(Query(
                        session,
                        W24S6WorkerReadQueryCodec.ManifestKind,
                        "build-01")));
                    AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
                }
                finally
                {
                    Directory.Delete(manifests);
                }
            }
        }

        [Test]
        public void OversizeInvalidUtf8AndInvalidJsonAreRejected()
        {
            using (var project = TestProject.Create())
            using (var current = new CurrentDirectoryScope(project.Root))
            using (var session = Open(project.Root))
            {
                var path = project.ManifestPath;
                File.WriteAllBytes(path, new byte[512 * 1024 + 1]);
                AssertRejected(
                    DecodeResult(session.Handle(Query(
                        session,
                        W24S6WorkerReadQueryCodec.ManifestKind,
                        "build-01"))),
                    W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

                File.WriteAllBytes(path, new byte[] { 0xff, 0xfe });
                AssertRejected(
                    DecodeResult(session.Handle(Query(
                        session,
                        W24S6WorkerReadQueryCodec.ManifestKind,
                        "build-01"))),
                    W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);

                File.WriteAllText(path, "{not-json}", StrictUtf8);
                AssertRejected(
                    DecodeResult(session.Handle(Query(
                        session,
                        W24S6WorkerReadQueryCodec.ManifestKind,
                        "build-01"))),
                    W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
            }
        }

        [Test]
        public void ExpectedContentHashMismatchRejectsWithoutReturningBytes()
        {
            using (var project = TestProject.Create())
            using (var current = new CurrentDirectoryScope(project.Root))
            using (var session = Open(project.Root))
            {
                var wrongHash = W24S6WorkerProtocolCodec.ComputeTypedHash(
                    W24S6WorkerReadQueryCodec.ContentHashType,
                    StrictUtf8.GetBytes("wrong"));
                var result = DecodeResult(session.Handle(Query(
                    session,
                    W24S6WorkerReadQueryCodec.ManifestKind,
                    "build-01",
                    wrongHash)));

                AssertRejected(result, W24S6WorkerReadQueryCodec.ProjectDocumentContentMismatch);
                Assert.That(result["contentBase64"].Type, Is.EqualTo(JTokenType.Null));
            }
        }

        [Test]
        public void RevokeAndRestartGenerationRejectStaleQueriesAndLocators()
        {
            using (var project = TestProject.Create())
            using (var current = new CurrentDirectoryScope(project.Root))
            {
                var staleLocator = Locator(project.Root);
                var staleQuery = default(byte[]);
                using (var stale = W24S6UserModeProjectReadSession.Open(
                           staleLocator, BrokerGeneration, WorkerSessionId, WorkerProcessEpoch))
                {
                    staleQuery = Query(
                        stale,
                        W24S6WorkerReadQueryCodec.LibraryIndexKind,
                        "project");
                    stale.Revoke();
                    AssertRejected(
                        DecodeResult(stale.Handle(staleQuery)),
                        W24S6WorkerReadQueryCodec.ProjectLeaseRejected);
                }

                Assert.Throws<InvalidDataException>(() => W24S6UserModeProjectReadSession.Open(
                    staleLocator,
                    BrokerGeneration + 1,
                    "um-session-402-00112233445566778899aabbccddeeff",
                    "winproc-124-0000000000000124"));

                using (var replacement = W24S6UserModeProjectReadSession.Open(
                           Locator(
                               project.Root,
                               BrokerGeneration + 1,
                               "um-session-402-00112233445566778899aabbccddeeff",
                               "winproc-124-0000000000000124"),
                           BrokerGeneration + 1,
                           "um-session-402-00112233445566778899aabbccddeeff",
                           "winproc-124-0000000000000124"))
                {
                    AssertRejected(
                        DecodeResult(replacement.Handle(staleQuery)),
                        W24S6WorkerReadQueryCodec.ProjectLeaseRejected);
                }
            }
        }

        [Test]
        public void ProjectRootMarkersAndRootReparseAreRejected()
        {
            using (var missingMarker = TestProject.Create())
            {
                File.Delete(Path.Combine(missingMarker.Root, "Packages", "manifest.json"));
                using (var current = new CurrentDirectoryScope(missingMarker.Root))
                    Assert.Throws<InvalidDataException>(() => Open(missingMarker.Root));
            }

            using (var target = TestProject.Create())
            {
                var junction = target.Root + "-junction";
                CreateJunction(junction, target.Root);
                try
                {
                    using (var current = new CurrentDirectoryScope(junction))
                        Assert.Throws<InvalidDataException>(() => Open(target.Root));
                }
                finally
                {
                    Directory.Delete(junction);
                }
            }
        }

        [Test]
        public void WorkerSourceUsesOnlyFixedContainedReadsAndNoPrivilegedSurface()
        {
            var source = File.ReadAllText(Path.Combine(
                RepositoryRoot(),
                "project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/W24S6UserModeProjectReadSession.cs"));
            foreach (var forbidden in new[]
                     {
                         "AssetDatabase", "EditorPrefs", "Application.dataPath", "OpenSCManager",
                         "ServiceHost", "LocalSystem", "SeSecurityPrivilege", "SeRestorePrivilege",
                         "FileId", "FileIdentity", "Directory.Enumerate", "Directory.GetFiles",
                         "CONTRACT.json", "TRACE.json",
                     })
                Assert.That(source, Does.Not.Contain(forbidden));
            Assert.That(source, Does.Contain("ProjectSettings/VFXComposer/LibraryIndex.json"));
            Assert.That(source, Does.Contain("ProjectSettings/VFXComposer/BuildManifests/"));
        }

        private static W24S6UserModeProjectReadSession Open(string selectedRoot)
        {
            return W24S6UserModeProjectReadSession.Open(
                Locator(selectedRoot),
                BrokerGeneration,
                WorkerSessionId,
                WorkerProcessEpoch);
        }

        private static byte[] Locator(
            string selectedRoot,
            long brokerGeneration = BrokerGeneration,
            string workerSessionId = WorkerSessionId,
            string workerProcessEpoch = WorkerProcessEpoch)
        {
            var normalized = selectedRoot.TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
            var volumeRoot = Path.GetPathRoot(selectedRoot).ToUpperInvariant();
            var projectIdentity = PathIdentity(
                W24S6WorkerProtocolCodec.ProjectIdentityType, "project", normalized);
            var root = new JObject
            {
                ["protocolVersion"] = W24S6WorkerProtocolCodec.ProtocolVersion,
                ["messageKind"] = W24S6WorkerProtocolCodec.LocatorKind,
                ["requestId"] = "um-select-001",
                ["registeredProjectId"] = "um-project-001",
                ["projectIdentity"] = TypedHash(projectIdentity),
                ["volumeIdentity"] = TypedHash(PathIdentity(
                    W24S6WorkerProtocolCodec.VolumeIdentityType, "volume", volumeRoot)),
                ["repositoryIdentity"] = TypedHash(PathIdentity(
                    W24S6WorkerProtocolCodec.DirectoryIdentityType, "repository", normalized)),
                ["projectRootIdentity"] = TypedHash(PathIdentity(
                    W24S6WorkerProtocolCodec.DirectoryIdentityType, "root", normalized)),
                ["brokerGeneration"] = brokerGeneration,
                ["registrationGeneration"] = RegistrationGeneration,
                ["enrollmentGeneration"] = EnrollmentGeneration,
                ["workerSessionId"] = workerSessionId,
                ["workerProcessEpoch"] = workerProcessEpoch,
            };
            var seal = typeof(W24S6WorkerProtocolCodec).GetMethod(
                "Seal",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(seal, Is.Not.Null);
            return (byte[])seal.Invoke(null, new object[]
            {
                root,
                W24S6WorkerProtocolCodec.LocatorSelfHashType,
                null,
                false,
            });
        }

        private static byte[] Query(
            W24S6UserModeProjectReadSession session,
            string documentKind,
            string documentId,
            W24S6WorkerTypedHash expectedContentHash = null)
        {
            var root = new JObject
            {
                ["protocolVersion"] = W24S6WorkerProtocolCodec.ProtocolVersion,
                ["messageKind"] = W24S6WorkerReadQueryCodec.QueryKind,
                ["requestId"] = "um-read-001",
                ["leaseId"] = session.LeaseId,
                ["projectIdentity"] = TypedHash(session.ProjectIdentity),
                ["leaseGeneration"] = session.LeaseGeneration,
                ["documentKind"] = documentKind,
                ["documentId"] = documentId,
                ["expectedContentHash"] = expectedContentHash == null
                    ? JValue.CreateNull()
                    : TypedHash(expectedContentHash),
            };
            return StrictUtf8.GetBytes(root.ToString(Formatting.None));
        }

        private static W24S6WorkerTypedHash PathIdentity(string typeTag, string role, string value)
        {
            return W24S6WorkerProtocolCodec.ComputeTypedHash(
                typeTag,
                StrictUtf8.GetBytes(PathIdentityVersion + role + "\0" + value));
        }

        private static JObject TypedHash(W24S6WorkerTypedHash hash)
        {
            return new JObject
            {
                ["typeTag"] = hash.TypeTag,
                ["digest"] = hash.Digest,
            };
        }

        private static JObject DecodeResult(byte[] result)
        {
            return JObject.Parse(StrictUtf8.GetString(result));
        }

        private static byte[] DecodeContent(JObject result)
        {
            return Convert.FromBase64String((string)result["contentBase64"]);
        }

        private static void AssertRejected(JObject result, string code)
        {
            Assert.That((bool)result["accepted"], Is.False);
            Assert.That((string)result["diagnostic"]["code"], Is.EqualTo(code));
            Assert.That((int)result["byteLength"], Is.Zero);
        }

        private static void CreateJunction(string junction, string target)
        {
            var startInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.Arguments = "/d /c mklink /J \"" + junction + "\" \"" + target + "\"";
            using (var process = Process.Start(startInfo))
            {
                Assert.That(process, Is.Not.Null);
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
            }
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        }

        private sealed class CurrentDirectoryScope : IDisposable
        {
            private readonly string _prior;

            internal CurrentDirectoryScope(string path)
            {
                _prior = Directory.GetCurrentDirectory();
                Directory.SetCurrentDirectory(path);
            }

            public void Dispose()
            {
                Directory.SetCurrentDirectory(_prior);
            }
        }

        private sealed class TestProject : IDisposable
        {
            private TestProject(string root, byte[] libraryBytes, byte[] manifestBytes)
            {
                Root = root;
                LibraryBytes = libraryBytes;
                ManifestBytes = manifestBytes;
            }

            internal string Root { get; private set; }
            internal byte[] LibraryBytes { get; private set; }
            internal byte[] ManifestBytes { get; private set; }
            internal string ManifestPath
            {
                get
                {
                    return Path.Combine(
                        Root,
                        "ProjectSettings",
                        "VFXComposer",
                        "BuildManifests",
                        "build-01.manifest.json");
                }
            }

            internal static TestProject Create()
            {
                var root = Path.GetFullPath(Path.Combine(
                    Path.GetTempPath(),
                    "vfxcomposer-u3-unity-" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(Path.Combine(root, "Assets"));
                Directory.CreateDirectory(Path.Combine(root, "Packages"));
                Directory.CreateDirectory(Path.Combine(root, "ProjectSettings", "VFXComposer", "BuildManifests"));
                File.WriteAllText(Path.Combine(root, "Packages", "manifest.json"), "{}", StrictUtf8);
                File.WriteAllText(
                    Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"),
                    "m_EditorVersion: 2022.3.62f3c1\n",
                    StrictUtf8);
                var library = StrictUtf8.GetBytes("{\"effects\":[]}");
                var manifest = StrictUtf8.GetBytes("{\"buildId\":\"build-01\"}");
                File.WriteAllBytes(
                    Path.Combine(root, "ProjectSettings", "VFXComposer", "LibraryIndex.json"),
                    library);
                File.WriteAllBytes(
                    Path.Combine(
                        root,
                        "ProjectSettings",
                        "VFXComposer",
                        "BuildManifests",
                        "build-01.manifest.json"),
                    manifest);
                return new TestProject(root, library, manifest);
            }

            public void Dispose()
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
    }
}
