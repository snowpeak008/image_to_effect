using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using VFXComposer.Broker.HandleProbe;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Security;

// DOTNET_STARTUP_HOOKS requires this exact global type name. Tests use it only
// to make the repository's non-publishable HandleProbe speak the U2 test wire.
public static class StartupHook
{
    public static void Initialize()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = Environment.GetEnvironmentVariable("VFXC_U2_TEST_MODE");
        if (string.IsNullOrEmpty(mode))
        {
            return;
        }

        try
        {
            VFXComposer.Broker.Tests.UserModeSessionTestChild
                .RunAsStartupHookAsync(mode)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception)
        {
            Environment.Exit(39);
        }
    }
}

namespace VFXComposer.Broker.Tests
{
    [TestClass]
    [SupportedOSPlatform("windows")]
    public sealed class UserModeBrokerWorkerSessionTests
    {
        [TestMethod]
        public async Task ExactReleaseChildCompletesOneUseCorrelatedAdmission()
        {
            await using var session = await StartAsync("valid", 17);

            Assert.AreEqual(17, session.Generation);
            Assert.IsTrue(session.SessionId.StartsWith("um-session-17-", StringComparison.Ordinal));
            Assert.IsTrue(session.ChildProcessId > 0);
            Assert.IsTrue(session.ChildProcessEpoch.StartsWith(
                $"winproc-{session.ChildProcessId}-", StringComparison.Ordinal));
            Assert.AreEqual(UserModeSessionTestChild.ExpectedExecutablePath, session.ExpectedExecutablePath);
            Assert.IsTrue(session.IsUsable);
        }

        [TestMethod]
        public async Task WrongNonceFailsClosedAndCleansChild()
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                StartAsync("wrong-nonce", 18));
        }

        [TestMethod]
        public async Task WrongGenerationFailsClosedAndCleansChild()
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                StartAsync("wrong-generation", 19));
        }

        [TestMethod]
        public async Task WrongSessionFailsClosedAndCleansChild()
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                StartAsync("wrong-session", 20));
        }

        [TestMethod]
        public async Task ChildCrashBeforeHelloFailsAdmissionWithoutReplaySurface()
        {
            await Assert.ThrowsExactlyAsync<EndOfStreamException>(() =>
                StartAsync("crash", 21));
        }

        [TestMethod]
        public async Task AdmissionTimeoutCleansNonConnectingChild()
        {
            await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
                StartAsync("no-connect", 22, TimeSpan.FromMilliseconds(500)));
        }

        [TestMethod]
        public async Task AdmissionCancellationCleansNonConnectingChild()
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
                StartAsync(
                    "no-connect",
                    23,
                    TimeSpan.FromSeconds(10),
                    cancellation.Token));
        }

        [TestMethod]
        public async Task ConcurrentDisposeIsIdempotentAndRevokesUsability()
        {
            var session = await StartAsync("valid", 24);

            await Task.WhenAll(
                session.DisposeAsync().AsTask(),
                session.DisposeAsync().AsTask(),
                Task.Run(session.Dispose));

            Assert.IsFalse(session.IsUsable);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = session.Transport);
        }

        [TestMethod]
        public async Task PostAdmissionChildExitAutomaticallyRevokesAndCleansSession()
        {
            await using var session = await StartAsync("exit-after-hello", 241);

            await session.ChildExitMonitor.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(session.IsUsable);
            Assert.ThrowsExactly<ObjectDisposedException>(() => _ = session.Transport);
        }

        [TestMethod]
        public void ProductionStartSurfaceHasNoOptionalJobOrExecutableBypass()
        {
            var start = typeof(UserModeBrokerWorkerSession).GetMethods(
                    BindingFlags.Static | BindingFlags.NonPublic)
                .Single(method => method.Name == "StartAsync");
            var parameters = start.GetParameters();
            Assert.AreEqual(typeof(string), parameters[0].ParameterType);
            Assert.AreEqual(typeof(ProcessStartInfo), parameters[1].ParameterType);
            Assert.IsFalse(parameters.Any(parameter => parameter.ParameterType == typeof(bool)));

            var productionLaunches = typeof(UserModeChildProcess).GetMethods(
                    BindingFlags.Static | BindingFlags.NonPublic)
                .Where(method => method.Name == "Launch")
                .ToArray();
            Assert.HasCount(1, productionLaunches);
            CollectionAssert.AreEqual(
                new[] { typeof(string), typeof(ProcessStartInfo) },
                productionLaunches[0].GetParameters().Select(value => value.ParameterType).ToArray());
        }

        [TestMethod]
        public void SurfaceContainsNoPrivilegedOrSameUserAttackerClaims()
        {
            var names = typeof(UserModeBrokerWorkerSession).Assembly
                .GetTypes()
                .Where(type => type.Name.StartsWith("UserMode", StringComparison.Ordinal) ||
                               type == typeof(VFXComposer.Broker.Native.WindowsKillOnCloseJob))
                .SelectMany(type => type.GetMembers())
                .Select(member => member.Name)
                .ToArray();

            Assert.IsFalse(names.Any(name =>
                name.Contains("Service", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Scm", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Sac", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Sandbox", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("GlobalOwnership", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public async Task AbandonedSessionFinalizerDoesNotLeaveOrphanChild()
        {
            var abandoned = await CreateAbandonedSessionAsync();
            for (var attempt = 0; attempt < 20 && abandoned.Session.IsAlive; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                await Task.Delay(50);
            }

            Assert.IsFalse(abandoned.Session.IsAlive);
            Assert.ThrowsExactly<ArgumentException>(() => Process.GetProcessById(abandoned.ProcessId));
        }

        private static Task<UserModeBrokerWorkerSession> StartAsync(
            string mode,
            long generation,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default) =>
            UserModeBrokerWorkerSession.StartAsync(
                UserModeSessionTestChild.ExpectedExecutablePath,
                UserModeSessionTestChild.Create(mode),
                generation,
                timeout ?? TimeSpan.FromSeconds(10),
                cancellationToken);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static async Task<(WeakReference Session, int ProcessId)> CreateAbandonedSessionAsync()
        {
            var session = await StartAsync("valid", 242);
            return (new WeakReference(session), session.ChildProcessId);
        }
    }

    [SupportedOSPlatform("windows")]
    internal static class UserModeSessionTestChild
    {
        internal static string ExpectedExecutablePath { get; } =
            Path.GetFullPath(Path.ChangeExtension(typeof(ProbeMarker).Assembly.Location, ".exe"));

        internal static ProcessStartInfo Create(string mode)
        {
            if (mode is not ("valid" or "wrong-nonce" or "wrong-generation" or
                "wrong-session" or "crash" or "no-connect" or "exit-after-hello"))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            var startInfo = new ProcessStartInfo(ExpectedExecutablePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            startInfo.Environment["DOTNET_STARTUP_HOOKS"] =
                typeof(StartupHook).Assembly.Location;
            startInfo.Environment["VFXC_U2_TEST_MODE"] = mode;
            return startInfo;
        }

        internal static async Task RunAsStartupHookAsync(string mode)
        {
            using var bootstrap = await UserModeNamedPipeServer.ReadBootstrapAsync(
                Console.OpenStandardInput());
            if (mode == "no-connect")
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
                return;
            }

            await using var client = new NamedPipeClientStream(
                ".",
                bootstrap.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(CancellationToken.None);
            if (mode == "crash")
            {
                Environment.Exit(31);
            }

            using var process = Process.GetCurrentProcess();
            var epoch = ProcessEpoch.Observe(process.SafeHandle, process.Id);
            UserModeWorkerBootstrap? altered = null;
            byte[]? nonce = null;
            try
            {
                var hello = bootstrap;
                if (mode == "wrong-nonce")
                {
                    nonce = bootstrap.CopyNonce();
                    nonce[0] ^= 1;
                    altered = new UserModeWorkerBootstrap(
                        bootstrap.PipeName,
                        bootstrap.Generation,
                        bootstrap.SessionId,
                        nonce);
                    hello = altered;
                }
                else if (mode == "wrong-generation")
                {
                    nonce = bootstrap.CopyNonce();
                    var generation = checked(bootstrap.Generation + 1);
                    altered = new UserModeWorkerBootstrap(
                        bootstrap.PipeName,
                        generation,
                        CanonicalSession(generation),
                        nonce);
                    hello = altered;
                }
                else if (mode == "wrong-session")
                {
                    nonce = bootstrap.CopyNonce();
                    altered = new UserModeWorkerBootstrap(
                        bootstrap.PipeName,
                        bootstrap.Generation,
                        CanonicalSession(bootstrap.Generation),
                        nonce);
                    hello = altered;
                }

                await UserModeNamedPipeServer.WriteHelloAsync(
                    client,
                    hello,
                    process.Id,
                    epoch);
            }
            finally
            {
                altered?.Dispose();
                if (nonce is not null)
                {
                    CryptographicOperations.ZeroMemory(nonce);
                }
            }

            if (mode == "exit-after-hello")
            {
                Environment.Exit(0);
            }

            await Task.Delay(TimeSpan.FromSeconds(30));
        }

        private static string CanonicalSession(long generation) =>
            $"um-session-{generation}-{new string('0', 32)}";
    }
}
