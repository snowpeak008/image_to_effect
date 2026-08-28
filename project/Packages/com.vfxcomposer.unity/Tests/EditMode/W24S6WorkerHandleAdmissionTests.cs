using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.Worker;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Tests.EditMode
{
    [TestFixture]
    public sealed class W24S6WorkerHandleAdmissionTests
    {
        private readonly List<string> createdDirectories = new List<string>();
        private readonly List<string> createdReparseDirectories = new List<string>();
        private string scratchRoot;
        private string repositoryRoot;
        private string projectRoot;

        [SetUp]
        public void CreateExactNtfsScratch()
        {
            Assert.That(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), Is.True);
            createdDirectories.Clear();
            createdReparseDirectories.Clear();
            var activeProject = Directory.GetParent(Application.dataPath).FullName;
            var activeRepository = Directory.GetParent(activeProject).FullName;
            scratchRoot = Path.Combine(
                activeRepository,
                "w24-worker-handle-admission-" + Guid.NewGuid().ToString("N"));
            Assert.That(Directory.Exists(scratchRoot), Is.False);
            repositoryRoot = Path.Combine(scratchRoot, "repository");
            projectRoot = Path.Combine(repositoryRoot, "project");
            try
            {
                EnsureDirectory(scratchRoot);
                EnsureDirectory(repositoryRoot);
                EnsureDirectory(projectRoot);
            }
            catch
            {
                CleanupExactScratch();
                throw;
            }
        }

        [TearDown]
        public void RemoveOnlyExactScratch()
        {
            CleanupExactScratch();
        }

        [Test]
        public void ExactAuthenticatedHandlesAreOwnedReplayedAndClosed()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-01", 7))
            using (var handles = OpenHandleSet(projectRoot))
            {
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.True);
                handles.Relinquish();
                Assert.That(diagnostic, Is.Empty);
                Assert.That(lease, Is.Not.Null);
                Assert.That(lease.LeaseId, Is.EqualTo("lease-admission-01"));
                Assert.That(lease.WorkerSessionId, Is.EqualTo(session.SessionId));
                Assert.That(lease.ReplayIdentities(), Is.True);
                Assert.That(lease.IsUsable, Is.True);
                Assert.That(raw.All(IsOpenHandle), Is.True);
                lease.Dispose();
                Assert.That(raw.Any(IsOpenHandle), Is.False);
                Assert.That(lease.IsUsable, Is.False);
                Assert.DoesNotThrow(lease.Dispose);
            }
        }

        [Test]
        public void SessionMismatchRejectsBeforeTouchingProcessLocalHandles()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-02", 8))
            using (var handles = OpenHandleSet(projectRoot))
            {
                var grant = CreateGrant(handles, session, workerSessionId: "worker-session-other");
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(diagnostic, Is.EqualTo(W24S6WorkerProjectHandleLease.AdmissionRejected));
                Assert.That(handles.Values.All(IsOpenHandle), Is.True,
                    "An unauthenticated or cross-session message must not close caller-selected handle numbers.");
            }
        }

        [Test]
        public void ForgedNativeIdentityFailsClosedAndClosesAuthenticatedGrantHandles()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-03", 9))
            using (var handles = OpenHandleSet(projectRoot))
            {
                var raw = handles.Values.ToArray();
                var wrongRepository = W24S6WorkerProtocolCodec.ComputeTypedHash(
                    W24S6WorkerProtocolCodec.DirectoryIdentityType,
                    new byte[] { 1, 2, 3 });
                var grant = CreateGrant(handles, session, repositoryIdentity: wrongRepository);
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.False);
                handles.Relinquish();
                Assert.That(lease, Is.Null);
                Assert.That(diagnostic, Is.EqualTo(W24S6WorkerProjectHandleLease.AdmissionRejected));
                Assert.That(raw.Any(IsOpenHandle), Is.False);
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.False);
                Assert.That(session.CandidateFactoryInvocationCountForTests, Is.EqualTo(1),
                    "A consumed failed grant must never touch its stale raw handle values again.");
            }
        }

        [Test]
        public void InheritableHandleFailsClosedBeforeLeaseIssuance()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-04", 10))
            using (var handles = OpenHandleSet(projectRoot))
            {
                Assert.That(SetHandleInformation(handles.Project, 1, 1), Is.True);
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.False);
                handles.Relinquish();
                Assert.That(lease, Is.Null);
                Assert.That(raw.Any(IsOpenHandle), Is.False);
            }
        }

        [Test]
        public void ReparseDirectoryHandleFailsClosedWithoutFollowingItsTarget()
        {
            var outside = Path.Combine(scratchRoot, "outside");
            var junction = Path.Combine(repositoryRoot, "junction");
            EnsureDirectory(outside);
            EnsureDirectory(junction);
            int error;
            Assert.That(CreateDirectoryJunction(junction, outside, out error), Is.True,
                "The local NTFS junction fixture must be created without network access. Win32=" + error);
            createdReparseDirectories.Add(junction);

            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-05", 11))
            using (var handles = OpenHandleSet(junction))
            {
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.False);
                handles.Relinquish();
                Assert.That(lease, Is.Null);
                Assert.That(raw.Any(IsOpenHandle), Is.False);
                Assert.That(Directory.Exists(outside), Is.True);
                Assert.That(Directory.EnumerateFileSystemEntries(outside), Is.Empty);
            }
        }

        [Test]
        public void SessionRevocationInvalidatesLeaseAndOpaqueSurfaceExposesNoNativeHandle()
        {
            var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-06", 12);
            using (session)
            using (var handles = OpenHandleSet(projectRoot))
            {
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session,
                    grant,
                    out lease,
                    out diagnostic), Is.True);
                handles.Relinquish();
                session.Dispose();
                Assert.That(lease.IsUsable, Is.False);
                Assert.That(raw.Any(IsOpenHandle), Is.False,
                    "Session revocation must close every attached native handle without caller cleanup.");
                lease.Dispose();
                Assert.That(raw.Any(IsOpenHandle), Is.False);
            }

            var constructors = typeof(W24S6WorkerAuthenticatedSession).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(constructors, Is.Not.Empty);
            Assert.That(constructors.All(value => value.IsPrivate), Is.True);
            Assert.That(
                () => constructors.Single().Invoke(new object[]
                {
                    new object(),
                    "worker-session-forged",
                    W24S6WorkerProcessEpoch.ObserveCurrent(),
                    99L
                }),
                Throws.TypeOf<TargetInvocationException>()
                    .With.InnerException.TypeOf<InvalidDataException>());
            var leaseConstructors = typeof(W24S6WorkerProjectHandleLease).GetConstructors(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(leaseConstructors, Is.Not.Empty);
            Assert.That(leaseConstructors.All(value => value.IsPrivate), Is.True);
            var signatures = typeof(W24S6WorkerProjectHandleLease)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(value => value.ReturnType.FullName + " " + value.Name + " " +
                                 string.Join(" ", value.GetParameters().Select(parameter => parameter.ParameterType.FullName)))
                .Concat(typeof(W24S6WorkerProjectHandleLease).GetProperties(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(value => value.PropertyType.FullName + " " + value.Name));
            foreach (var signature in signatures)
            {
                Assert.That(signature, Does.Not.Contain("System.IntPtr"));
                Assert.That(signature, Does.Not.Contain("SafeHandle"));
                Assert.That(signature, Does.Not.Contain("SafeFileHandle"));
            }
        }

        [Test]
        public void RevocationRequestedDuringAdmissionWaitsAndClosesAuthenticatedHandles()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-07", 13))
            using (var handles = OpenHandleSet(projectRoot))
            {
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                var revokeStarted = new ManualResetEventSlim(false);
                Task revoke = null;
                try
                {
                    session.BeforeSessionAttachForTests = () =>
                    {
                        revoke = Task.Run(() =>
                        {
                            revokeStarted.Set();
                            session.Dispose();
                        });
                        if (!revokeStarted.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                    };
                    W24S6WorkerProjectHandleLease lease;
                    string diagnostic;
                    Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                        session,
                        grant,
                        out lease,
                        out diagnostic), Is.True);
                    handles.Relinquish();
                    Assert.That(revoke, Is.Not.Null);
                    Assert.That(revoke.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    Assert.That(lease.IsUsable, Is.False);
                    Assert.That(raw.Any(IsOpenHandle), Is.False);
                }
                finally
                {
                    session.BeforeSessionAttachForTests = null;
                    if (revoke != null) revoke.Wait(TimeSpan.FromSeconds(5));
                    revokeStarted.Dispose();
                }
            }
        }

        [Test]
        public void ExactGrantReplayReturnsTheSameLeaseWithoutASecondNativeOwner()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-08", 14))
            using (var handles = OpenHandleSet(projectRoot))
            {
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                W24S6WorkerProjectHandleLease first;
                W24S6WorkerProjectHandleLease second;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, grant, out first, out diagnostic), Is.True);
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, grant, out second, out diagnostic), Is.True);
                handles.Relinquish();
                Assert.That(second, Is.SameAs(first));
                Assert.That(session.CandidateFactoryInvocationCountForTests, Is.EqualTo(1));
                first.Dispose();
                Assert.That(raw.Any(IsOpenHandle), Is.False);
                Assert.DoesNotThrow(second.Dispose);
            }
        }

        [Test]
        public void ConcurrentExactGrantReplayReturnsOneSharedLease()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-09", 15))
            using (var handles = OpenHandleSet(projectRoot))
            using (var start = new ManualResetEventSlim(false))
            {
                var grant = CreateGrant(handles, session);
                var leases = new W24S6WorkerProjectHandleLease[2];
                var results = new bool[2];
                var tasks = new[]
                {
                    Task.Run(() =>
                    {
                        string diagnostic;
                        start.Wait();
                        results[0] = W24S6WorkerProjectHandleLease.TryAdmit(
                            session, grant, out leases[0], out diagnostic);
                    }),
                    Task.Run(() =>
                    {
                        string diagnostic;
                        start.Wait();
                        results[1] = W24S6WorkerProjectHandleLease.TryAdmit(
                            session, grant, out leases[1], out diagnostic);
                    })
                };
                start.Set();
                Assert.That(Task.WaitAll(tasks, TimeSpan.FromSeconds(10)), Is.True);
                handles.Relinquish();
                Assert.That(results, Is.EqualTo(new[] { true, true }));
                Assert.That(leases[1], Is.SameAs(leases[0]));
                Assert.That(session.CandidateFactoryInvocationCountForTests, Is.EqualTo(1));
                session.Dispose();
            }
        }

        [Test]
        public void ResignedGrantReusingClaimedHandlesIsRejectedWithoutTouchingThem()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-10", 16))
            using (var handles = OpenHandleSet(projectRoot))
            {
                var raw = handles.Values.ToArray();
                var firstGrant = CreateGrant(handles, session);
                var variant = CreateGrant(
                    handles,
                    session,
                    grantId: "grant-admission-variant",
                    leaseId: "lease-admission-variant");
                W24S6WorkerProjectHandleLease first;
                W24S6WorkerProjectHandleLease rejected;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, firstGrant, out first, out diagnostic), Is.True);
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, variant, out rejected, out diagnostic), Is.False);
                handles.Relinquish();
                Assert.That(rejected, Is.Null);
                Assert.That(raw.All(IsOpenHandle), Is.True);
                Assert.That(session.CandidateFactoryInvocationCountForTests, Is.EqualTo(1));
                session.Dispose();
                Assert.That(raw.Any(IsOpenHandle), Is.False);
            }
        }

        [Test]
        public void ConcurrentSessionDisposeWaitsForNativeHandleCloseCompletion()
        {
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-11", 17))
            using (var handles = OpenHandleSet(projectRoot))
            using (var closeEntered = new ManualResetEventSlim(false))
            using (var allowClose = new ManualResetEventSlim(false))
            using (var secondStarted = new ManualResetEventSlim(false))
            {
                var raw = handles.Values.ToArray();
                var grant = CreateGrant(handles, session);
                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, grant, out lease, out diagnostic), Is.True);
                handles.Relinquish();
                Task first = null;
                Task second = null;
                try
                {
                    session.BeforeHandleCloseForTests = () =>
                    {
                        closeEntered.Set();
                        if (!allowClose.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                    };
                    first = Task.Run(session.Dispose);
                    Assert.That(closeEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    second = Task.Run(() =>
                    {
                        secondStarted.Set();
                        session.Dispose();
                    });
                    Assert.That(secondStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    Assert.That(second.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
                }
                finally
                {
                    allowClose.Set();
                    if (first != null) first.Wait(TimeSpan.FromSeconds(5));
                    if (second != null) second.Wait(TimeSpan.FromSeconds(5));
                    session.BeforeHandleCloseForTests = null;
                }
                Assert.That(raw.Any(IsOpenHandle), Is.False);
                Assert.That(lease.IsUsable, Is.False);
            }
        }

        [Test]
        public void AdmissionRegistryCapRejectsBeforeTouchingFreshHandles()
        {
            using (var firstHandles = OpenHandleSet(projectRoot))
            using (var untouchedHandles = OpenHandleSet(projectRoot))
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-cap", 7))
            {
                var firstGrant = CreateGrant(
                    firstHandles,
                    session,
                    grantId: "grant-cap-0000",
                    leaseId: "lease-cap");
                W24S6WorkerProjectHandleLease firstLease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, firstGrant, out firstLease, out diagnostic), Is.True);
                firstHandles.Relinquish();

                for (var index = 1; index < W24S6WorkerAuthenticatedSession.MaxAdmissionsPerSession; index++)
                {
                    var replay = CloneGrantWithRequestId(firstGrant, "grant-cap-" + index.ToString("D4"));
                    W24S6WorkerProjectHandleLease rejected;
                    Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                        session, replay, out rejected, out diagnostic), Is.False);
                    Assert.That(rejected, Is.Null);
                }

                var freshGrant = CreateGrant(
                    untouchedHandles,
                    session,
                    grantId: "grant-cap-overflow",
                    leaseId: "lease-cap-overflow");
                W24S6WorkerProjectHandleLease overflow;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, freshGrant, out overflow, out diagnostic), Is.False);
                Assert.That(overflow, Is.Null);
                Assert.That(untouchedHandles.Values.All(IsOpenHandle), Is.True,
                    "The cap must reject before adopting or closing fresh raw handles.");
                Assert.That(session.CandidateFactoryInvocationCountForTests, Is.EqualTo(1));
            }
        }

        [Test]
        public void DuplicateRawHandleValueIsRejectedAndEachUniqueValueClosesOnce()
        {
            using (var handles = OpenHandleSet(projectRoot))
            using (var session = W24S6WorkerAuthenticatedSession.IssueForTests("worker-session-duplicate-raw", 7))
            {
                var original = CreateGrant(handles, session);
                var duplicate = CloneGrant(
                    original,
                    "grant-duplicate-raw",
                    original.VolumeHandle,
                    original.VolumeHandle,
                    original.ProjectRootHandle);
                var volume = handles.Volume;
                var repository = handles.Repository;
                var project = handles.Project;

                W24S6WorkerProjectHandleLease lease;
                string diagnostic;
                Assert.That(W24S6WorkerProjectHandleLease.TryAdmit(
                    session, duplicate, out lease, out diagnostic), Is.False);
                Assert.That(lease, Is.Null);
                Assert.That(IsOpenHandle(volume), Is.False);
                Assert.That(IsOpenHandle(project), Is.False);
                Assert.That(IsOpenHandle(repository), Is.True,
                    "A raw value absent from the duplicate grant must remain untouched.");
                Assert.That(CloseHandle(repository), Is.True);
                handles.Relinquish();
                Assert.That(session.CandidateFactoryInvocationCountForTests, Is.EqualTo(1));
            }
        }

        private W24S6WorkerProjectHandleGrant CreateGrant(
            TestHandleSet handles,
            W24S6WorkerAuthenticatedSession session,
            string workerSessionId = null,
            W24S6WorkerTypedHash repositoryIdentity = null,
            string grantId = "grant-admission-01",
            string leaseId = "lease-admission-01")
        {
            var volume = W24S6WorkerProjectHandleLease.ObserveIdentityForTests(
                handles.Volume,
                W24S6WorkerProtocolCodec.VolumeIdentityType);
            var repository = repositoryIdentity ?? W24S6WorkerProjectHandleLease.ObserveIdentityForTests(
                handles.Repository,
                W24S6WorkerProtocolCodec.DirectoryIdentityType);
            var project = W24S6WorkerProjectHandleLease.ObserveIdentityForTests(
                handles.Project,
                W24S6WorkerProtocolCodec.DirectoryIdentityType);
            var projectIdentity = W24S6WorkerProjectHandleLease.ComputeProjectIdentityForTests(
                volume,
                repository,
                project);
            var bytes = W24S6WorkerProtocolCodec.CreateGrantForTests(
                grantId,
                leaseId,
                "registered-project-01",
                projectIdentity,
                volume,
                repository,
                project,
                session.BrokerGeneration,
                3,
                5,
                workerSessionId ?? session.SessionId,
                session.ProcessEpoch,
                EncodeHandle(handles.Volume),
                EncodeHandle(handles.Repository),
                EncodeHandle(handles.Project));
            return W24S6WorkerProtocolCodec.DecodeGrant(bytes);
        }

        private static W24S6WorkerProjectHandleGrant CloneGrantWithRequestId(
            W24S6WorkerProjectHandleGrant source,
            string requestId)
        {
            return CloneGrant(
                source,
                requestId,
                source.VolumeHandle,
                source.RepositoryHandle,
                source.ProjectRootHandle);
        }

        private static W24S6WorkerProjectHandleGrant CloneGrant(
            W24S6WorkerProjectHandleGrant source,
            string requestId,
            string volumeHandle,
            string repositoryHandle,
            string projectRootHandle)
        {
            return W24S6WorkerProtocolCodec.DecodeGrant(
                W24S6WorkerProtocolCodec.CreateGrantForTests(
                    requestId,
                    source.LeaseId,
                    source.RegisteredProjectId,
                    source.ProjectIdentity,
                    source.VolumeIdentity,
                    source.RepositoryIdentity,
                    source.ProjectRootIdentity,
                    source.BrokerGeneration,
                    source.RegistrationGeneration,
                    source.LeaseGeneration,
                    source.WorkerSessionId,
                    source.WorkerProcessEpoch,
                    volumeHandle,
                    repositoryHandle,
                    projectRootHandle));
        }

        private TestHandleSet OpenHandleSet(string projectDirectory)
        {
            return new TestHandleSet(
                OpenDirectory(scratchRoot),
                OpenDirectory(repositoryRoot),
                OpenDirectory(projectDirectory));
        }

        private static IntPtr OpenDirectory(string path)
        {
            const uint traverseReadAttributesSynchronize = 0x001000A0;
            const uint shareRead = 0x00000001;
            const uint openExisting = 3;
            const uint backupSemantics = 0x02000000;
            const uint openReparsePoint = 0x00200000;
            var handle = CreateFile(
                path,
                traverseReadAttributesSynchronize,
                shareRead,
                IntPtr.Zero,
                openExisting,
                backupSemantics | openReparsePoint,
                IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                throw new InvalidDataException("Could not create the exact local test directory handle.");
            return handle;
        }

        private static string EncodeHandle(IntPtr value)
        {
            return unchecked((ulong)value.ToInt64()).ToString("x16");
        }

        private static bool IsOpenHandle(IntPtr value)
        {
            uint flags;
            return GetHandleInformation(value, out flags);
        }

        private void EnsureDirectory(string path)
        {
            RequireContained(path);
            if (Directory.Exists(path)) return;
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
        }

        private void CleanupExactScratch()
        {
            if (string.IsNullOrEmpty(scratchRoot)) return;
            foreach (var path in createdReparseDirectories
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(value => value.Length))
            {
                if (!Directory.Exists(path)) continue;
                RequireContained(path);
                Assert.That((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0, Is.True);
                Directory.Delete(path, false);
            }
            foreach (var path in createdDirectories
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(value => value.Length))
            {
                if (!Directory.Exists(path)) continue;
                RequireContained(path);
                Assert.That(Directory.EnumerateFileSystemEntries(path), Is.Empty);
                Directory.Delete(path, false);
            }
            Assert.That(Directory.Exists(scratchRoot), Is.False);
        }

        private void RequireContained(string path)
        {
            if (string.IsNullOrEmpty(scratchRoot)) return;
            var root = Path.GetFullPath(scratchRoot).TrimEnd(Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) &&
                !candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Scratch operation escaped the exact fixture root.");
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
            internal IEnumerable<IntPtr> Values { get { return new[] { Volume, Repository, Project }; } }

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
        private static extern bool GetHandleInformation(IntPtr handle, out uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);

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
