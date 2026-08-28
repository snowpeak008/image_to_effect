using System.Reflection;
using System.Text.Json;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Queries;
using VFXComposer.Broker.Registration;
using VFXComposer.Protocol;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class BrokerAdmissionAndRoutingTests
{
    [TestMethod]
    public void ProductionPolicyAndRegistrationRemainFailClosed()
    {
        Assert.IsFalse(BrokerPolicy.TryLoadProduction(out var policy));
        Assert.IsNull(policy);

        using var fixture = CreateFixture();
        Assert.IsFalse(fixture.Registrations.TryRegisterProduction(
            out var project,
            out var diagnosticCode));
        Assert.IsNull(project);
        Assert.AreEqual(BrokerDiagnosticCodes.RegistrationIssuerPending, diagnosticCode);
    }

    [TestMethod]
    public void AuthenticationRequiresObservedPidEpochSidAndExactAllowedImage()
    {
        using var fixture = CreateFixture();
        var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        var observed = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        var observedProcessId = observed.ProcessId;
        var observedEpoch = observed.ProcessEpoch;
        var observedImage = observed.ImageIdentity;
        var hello = CreateHello(PeerRoles.Worker, observed);

        Assert.IsTrue(fixture.Sessions.TryAuthenticate(
            hello,
            observed,
            out var session,
            out var receipt,
            out var diagnostic));
        Assert.IsNotNull(session);
        Assert.IsNotNull(receipt);
        Assert.AreEqual(string.Empty, diagnostic);
        Assert.AreEqual(observedProcessId, session.ProcessId);
        Assert.AreEqual(observedEpoch, receipt.ProcessEpoch);

        foreach (var mismatchHello in new[]
                 {
                     new PeerHello("bad-pid", PeerRoles.Worker, "worker-01", observedProcessId + 1, observedEpoch,
                         [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1, PeerCapabilityIds.ProjectRegistrationV1, PeerCapabilityIds.WorkerHandleLifecycleV1], observedImage),
                     new PeerHello("bad-epoch", PeerRoles.Worker, "worker-01", observedProcessId, "different",
                         [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1, PeerCapabilityIds.ProjectRegistrationV1, PeerCapabilityIds.WorkerHandleLifecycleV1], observedImage),
                     new PeerHello("bad-image", PeerRoles.Worker, "worker-01", observedProcessId, observedEpoch,
                         [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1, PeerCapabilityIds.ProjectRegistrationV1, PeerCapabilityIds.WorkerHandleLifecycleV1],
                         TypedHash.ComputeUtf8(PeerHello.ProcessImageIdentityType, "other-image")),
                 })
        {
            var mismatch = WindowsNamedPipePeerFactsSource.ObserveProcess(
                processId,
                allowHandleDuplication: true);
            Assert.IsFalse(fixture.Sessions.TryAuthenticate(
                mismatchHello,
                mismatch,
                out var rejectedSession,
                out var rejectedReceipt,
                out var rejectedCode));
            Assert.IsNull(rejectedSession);
            Assert.IsNull(rejectedReceipt);
            Assert.AreEqual(BrokerDiagnosticCodes.PeerRejected, rejectedCode);
        }

        using var actual = WindowsNamedPipePeerFactsSource.ObserveProcess(processId);
        var wrongSidPolicy = BrokerTestFactory.CreatePolicy(
            "vfxcomposer-wrong-sid",
            "broker-02",
            1,
            TypedHash.ComputeUtf8(BrokerPolicy.UserSidIdentityType, "other-user"),
            actual.ImageIdentity,
            actual.ImageIdentity);
        using var wrongSidSessions = new PeerSessionRegistry(wrongSidPolicy);
        var wrongSidHello = CreateHello(PeerRoles.Worker, actual);
        var wrongSidObserved = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        Assert.IsFalse(wrongSidSessions.TryAuthenticate(
            wrongSidHello,
            wrongSidObserved,
            out _,
            out _,
            out _));
    }

    [TestMethod]
    public void SessionRevocationInvalidatesOnlyTheExactOpaqueSession()
    {
        using var fixture = CreateFixture();
        var worker = Authenticate(fixture, PeerRoles.Worker);
        var desktop = Authenticate(fixture, PeerRoles.Desktop);
        Assert.IsTrue(fixture.Sessions.IsCurrent(worker, PeerRoles.Worker));
        Assert.IsTrue(fixture.Sessions.IsCurrent(desktop, PeerRoles.Desktop));
        Assert.IsTrue(fixture.Sessions.Revoke(worker.SessionId));
        Assert.IsFalse(fixture.Sessions.IsCurrent(worker, PeerRoles.Worker));
        Assert.IsTrue(fixture.Sessions.IsCurrent(desktop, PeerRoles.Desktop));
    }

    [TestMethod]
    public void WorkerProcessEpochAllowsOnlyOneLiveAuthenticatedSession()
    {
        using var fixture = CreateFixture();
        var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        var firstFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        var firstHello = CreateHello(PeerRoles.Worker, firstFacts);
        Assert.IsTrue(fixture.Sessions.TryAuthenticate(
            firstHello,
            firstFacts,
            out var first,
            out _,
            out _));

        var duplicateFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        var duplicateHello = new PeerHello(
            "duplicate-worker-session",
            PeerRoles.Worker,
            "duplicate-worker",
            duplicateFacts.ProcessId,
            duplicateFacts.ProcessEpoch,
            [
                PeerCapabilityIds.PeerSessionV1,
                PeerCapabilityIds.ReadOnlyQueryV1,
                PeerCapabilityIds.ProjectRegistrationV1,
                PeerCapabilityIds.WorkerHandleLifecycleV1,
            ],
            duplicateFacts.ImageIdentity);
        Assert.IsFalse(fixture.Sessions.TryAuthenticate(
            duplicateHello,
            duplicateFacts,
            out var duplicate,
            out _,
            out _));
        Assert.IsNull(duplicate);

        Assert.IsTrue(fixture.Sessions.Revoke(first!.SessionId));
        var replacementFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        var replacementHello = CreateHello(PeerRoles.Worker, replacementFacts);
        Assert.IsTrue(fixture.Sessions.TryAuthenticate(
            replacementHello,
            replacementFacts,
            out var replacement,
            out _,
            out _));
        Assert.IsTrue(fixture.Sessions.IsCurrent(replacement, PeerRoles.Worker));
    }

    [TestMethod]
    public async Task ConcurrentWorkerAuthenticationForOneProcessEpochHasOneWinner()
    {
        using var fixture = CreateFixture();
        var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
        var firstFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        var secondFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            processId,
            allowHandleDuplication: true);
        var firstHello = CreateHello(PeerRoles.Worker, firstFacts);
        var secondHello = new PeerHello(
            "concurrent-worker-02",
            PeerRoles.Worker,
            "worker-concurrent-02",
            secondFacts.ProcessId,
            secondFacts.ProcessEpoch,
            [
                PeerCapabilityIds.PeerSessionV1,
                PeerCapabilityIds.ReadOnlyQueryV1,
                PeerCapabilityIds.ProjectRegistrationV1,
                PeerCapabilityIds.WorkerHandleLifecycleV1,
            ],
            secondFacts.ImageIdentity);

        var attempts = await Task.WhenAll(
            Task.Run(() => AuthenticateCandidate(fixture.Sessions, firstHello, firstFacts)),
            Task.Run(() => AuthenticateCandidate(fixture.Sessions, secondHello, secondFacts)));

        Assert.AreEqual(1, attempts.Count(attempt => attempt.Succeeded));
        var winner = attempts.Single(attempt => attempt.Succeeded).Session;
        Assert.IsNotNull(winner);
        Assert.IsTrue(fixture.Sessions.IsCurrent(winner, PeerRoles.Worker));
        Assert.IsTrue(fixture.Sessions.Revoke(winner.SessionId));
    }

    [TestMethod]
    public async Task WorkerReplacementWaitsForFullRevocationObserverCleanup()
    {
        using var fixture = CreateFixture();
        var first = Authenticate(fixture, PeerRoles.Worker);
        using var observerEntered = new ManualResetEventSlim();
        using var releaseObserver = new ManualResetEventSlim();
        fixture.Sessions.SessionRevoked += session =>
        {
            if (ReferenceEquals(session, first))
            {
                observerEntered.Set();
                releaseObserver.Wait(TimeSpan.FromSeconds(10));
            }
        };

        var revoke = Task.Run(() => fixture.Sessions.Revoke(first.SessionId));
        Assert.IsTrue(observerEntered.Wait(TimeSpan.FromSeconds(10)));
        Assert.IsFalse(first.IsUsable);

        var replacementFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            System.Diagnostics.Process.GetCurrentProcess().Id,
            allowHandleDuplication: true);
        var replacementHello = CreateHello(PeerRoles.Worker, replacementFacts);
        var replacementAttempt = Task.Run(() => AuthenticateCandidate(
            fixture.Sessions,
            replacementHello,
            replacementFacts));
        var overlappingReplacement = await replacementAttempt;
        Assert.IsFalse(overlappingReplacement.Succeeded);
        Assert.IsNull(overlappingReplacement.Session);

        releaseObserver.Set();
        Assert.IsTrue(await revoke);
        var finalReplacementFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
            System.Diagnostics.Process.GetCurrentProcess().Id,
            allowHandleDuplication: true);
        var finalReplacementHello = CreateHello(PeerRoles.Worker, finalReplacementFacts);
        var replacement = AuthenticateCandidate(
            fixture.Sessions,
            finalReplacementHello,
            finalReplacementFacts);
        Assert.IsTrue(replacement.Succeeded);
        Assert.IsNotNull(replacement.Session);
        Assert.IsTrue(fixture.Sessions.Revoke(replacement.Session!.SessionId));
    }

    [TestMethod]
    public void RegistryDisposeRevokesEverySessionAndAggregatesObserverFailures()
    {
        var fixture = CreateFixture();
        var worker = Authenticate(fixture, PeerRoles.Worker);
        var desktop = Authenticate(fixture, PeerRoles.Desktop);
        var observed = new List<string>();
        fixture.Sessions.SessionRevoked += session =>
        {
            observed.Add(session.SessionId);
            throw new InvalidOperationException("synthetic-observer-failure");
        };
        fixture.Sessions.SessionRevoked += session =>
            observed.Add("second:" + session.SessionId);

        var exception = Assert.ThrowsExactly<AggregateException>(
            () => fixture.Sessions.Dispose());
        Assert.AreEqual(2, exception.InnerExceptions.Count);
        Assert.IsFalse(worker.IsUsable);
        Assert.IsFalse(desktop.IsUsable);
        Assert.IsTrue(observed.Contains(worker.SessionId));
        Assert.IsTrue(observed.Contains(desktop.SessionId));
        Assert.IsTrue(observed.Contains("second:" + worker.SessionId));
        Assert.IsTrue(observed.Contains("second:" + desktop.SessionId));

        fixture.Registrations.Dispose();
    }

    [TestMethod]
    public void RevocationObserverCannotSynchronouslyDisposeItsOwnRegistry()
    {
        using var fixture = CreateFixture();
        var worker = Authenticate(fixture, PeerRoles.Worker);
        fixture.Sessions.SessionRevoked += _ => fixture.Sessions.Dispose();

        var exception = Assert.ThrowsExactly<AggregateException>(
            () => fixture.Sessions.Revoke(worker.SessionId));
        Assert.AreEqual(1, exception.InnerExceptions.Count);
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerExceptions[0]);
        Assert.IsFalse(worker.IsUsable);

        fixture.Sessions.Dispose();
    }

    [TestMethod]
    public void WireDescriptorsCannotBeConvertedToOpaqueSessionOrLeaseCapabilities()
    {
        Assert.IsFalse(typeof(AuthenticatedPeerSession).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance).Any());
        Assert.IsFalse(typeof(RegisteredProjectLease).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance).Any());
        Assert.IsFalse(typeof(ProjectRegistrationAttestation).IsAssignableTo(typeof(RegisteredProjectIdentity)));
        Assert.IsFalse(typeof(ProjectLeaseDescriptor).IsAssignableTo(typeof(RegisteredProjectLease)));
    }

    [TestMethod]
    public void BrokerRegistrationAndRoutingSurfaceContainsNoCallerProjectPath()
    {
        var prohibited = new[]
        {
            "CallerPath",
            "ProjectPath",
            "AbsolutePath",
            "EditorPrefs",
            "Environment",
        };
        foreach (var type in new[]
                 {
                     typeof(BrokerPolicy),
                     typeof(ProjectRegistrationStore),
                     typeof(RegisteredProjectIdentity),
                     typeof(RegisteredProjectLease),
                     typeof(ReadOnlyQueryRouter),
                 })
        {
            foreach (var member in type.GetMembers(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.IsFalse(
                    prohibited.Any(value => member.Name.Contains(value, StringComparison.OrdinalIgnoreCase)),
                    $"{type.FullName}.{member.Name}");
            }
        }
    }

    private static Fixture CreateFixture()
    {
        using var observed = WindowsNamedPipePeerFactsSource.ObserveProcess(
            System.Diagnostics.Process.GetCurrentProcess().Id);
        var policy = BrokerTestFactory.CreatePolicy(
            "vfxcomposer-test-broker",
            "broker-01",
            1,
            observed.UserSidIdentity,
            observed.ImageIdentity,
            observed.ImageIdentity);
        var sessions = new PeerSessionRegistry(policy);
        var registrations = new ProjectRegistrationStore(policy, sessions);
        return new Fixture(
            sessions,
            registrations,
            new ReadOnlyQueryRouter(registrations, sessions));
    }

    private static AuthenticatedPeerSession Authenticate(
        Fixture fixture,
        string role)
    {
        var observed = WindowsNamedPipePeerFactsSource.ObserveProcess(
            System.Diagnostics.Process.GetCurrentProcess().Id,
            allowHandleDuplication: string.Equals(role, PeerRoles.Worker, StringComparison.Ordinal));
        var hello = CreateHello(role, observed);
        Assert.IsTrue(fixture.Sessions.TryAuthenticate(
            hello,
            observed,
            out var session,
            out _,
            out _));
        return session!;
    }

    private static PeerHello CreateHello(
        string role,
        ObservedPeerFacts facts)
    {
        var capabilities = role == PeerRoles.Worker
            ? new[]
            {
                PeerCapabilityIds.PeerSessionV1,
                PeerCapabilityIds.ReadOnlyQueryV1,
                PeerCapabilityIds.ProjectRegistrationV1,
                PeerCapabilityIds.WorkerHandleLifecycleV1,
            }
            : new[]
            {
                PeerCapabilityIds.PeerSessionV1,
                PeerCapabilityIds.ReadOnlyQueryV1,
            };
        return new PeerHello(
            $"hello-{facts.ProcessId}-{role.ToLowerInvariant()}",
            role,
            $"peer-{facts.ProcessId}-{role.ToLowerInvariant()}",
            facts.ProcessId,
            facts.ProcessEpoch,
            capabilities,
            facts.ImageIdentity);
    }

    private static AuthenticationAttempt AuthenticateCandidate(
        PeerSessionRegistry sessions,
        PeerHello hello,
        ObservedPeerFacts facts)
    {
        var succeeded = sessions.TryAuthenticate(
            hello,
            facts,
            out var session,
            out _,
            out _);
        return new AuthenticationAttempt(succeeded, session);
    }

    private sealed record AuthenticationAttempt(
        bool Succeeded,
        AuthenticatedPeerSession? Session);

    private sealed class Fixture : IDisposable
    {
        public Fixture(
            PeerSessionRegistry sessions,
            ProjectRegistrationStore registrations,
            ReadOnlyQueryRouter router)
        {
            Sessions = sessions;
            Registrations = registrations;
            Router = router;
        }

        public PeerSessionRegistry Sessions { get; }
        public ProjectRegistrationStore Registrations { get; }
        public ReadOnlyQueryRouter Router { get; }

        public void Dispose()
        {
            Registrations.Dispose();
            Sessions.Dispose();
        }
    }
}
