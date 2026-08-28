using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.Worker;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S6WorkerBrokerSessionTests
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Test]
        public void TestOnlyBrokerHostCompletesFourReadsAndContentMismatchBeforeRevoke()
        {
            Assert.That(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), Is.True);
            Assert.That(IntPtr.Size, Is.EqualTo(8));
            var root = RepositoryRoot();
            var helper = Path.Combine(
                root,
                "services/VFXComposer.Broker.HandleProbe/bin/Release/net8.0/VFXComposer.Broker.HandleProbe.exe");
            Assert.That(File.Exists(helper), Is.True, "Build the non-publishable Release HandleProbe before this gate.");
            var pipeName = "vfxcomposer-unity-worker-" + Guid.NewGuid().ToString("N");
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = helper,
                    Arguments = "--unity-worker-lifecycle " +
                                Process.GetCurrentProcess().Id + " " + pipeName,
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                Assert.That(process.Start(), Is.True);
                try
                {
                    var readyRead = process.StandardOutput.ReadLineAsync();
                    Assert.That(readyRead.Wait(TimeSpan.FromSeconds(20)), Is.True,
                        "Broker test host did not become ready.");
                    var ready = readyRead.Result;
                    Assert.That(ready, Does.Match("^READY sha256:[0-9a-f]{64}$"));
                    var imageDigest = ready.Substring("READY ".Length);

                    var result = W24S6WorkerBrokerConnection.RunLifecycleForTests(
                        pipeName,
                        imageDigest,
                        20000);
                    Assert.That(process.WaitForExit(20000), Is.True, "Broker test host did not exit.");
                    var remainingOutput = process.StandardOutput.ReadToEnd().Trim();
                    var error = process.StandardError.ReadToEnd().Trim();
                    Assert.That(error, Is.Empty);
                    Assert.That(process.ExitCode, Is.EqualTo(0));
                    Assert.That(
                        remainingOutput,
                        Is.EqualTo(
                            "PASS " + result.SessionId + " " + result.LeaseId + " " +
                            W24S6WorkerBrokerConnection.ExpectedReadQueryCount));
                    Assert.That(result.GrantSelfHash, Does.Match("^sha256:[0-9a-f]{64}$"));
                    Assert.That(result.RevokeSelfHash, Does.Match("^sha256:[0-9a-f]{64}$"));
                    Assert.That(
                        result.ReadQueryCount,
                        Is.EqualTo(W24S6WorkerBrokerConnection.ExpectedReadQueryCount));
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(10000);
                    }
                }
            }
        }

        [Test]
        public void SessionReceiptRejectsUnknownMissingWrongCorrelationEpochAndCapabilities()
        {
            var valid = ValidReceipt();
            Assert.That(
                W24S6WorkerPeerHandshakeCodec.DecodeSessionAcceptedForTests(
                    Encode(valid),
                    "hello-request",
                    "winproc-1-0000000000000001").SessionId,
                Is.EqualTo("session-1-1"));

            var unknown = (JObject)valid.DeepClone();
            unknown["callerPath"] = "C:/untrusted";
            AssertReject(unknown, "hello-request", "winproc-1-0000000000000001");

            var missing = (JObject)valid.DeepClone();
            missing.Remove("brokerGeneration");
            AssertReject(missing, "hello-request", "winproc-1-0000000000000001");

            AssertReject(valid, "different-request", "winproc-1-0000000000000001");
            AssertReject(valid, "hello-request", "winproc-1-0000000000000002");

            var capabilities = (JObject)valid.DeepClone();
            capabilities["negotiatedCapabilities"] = new JArray(
                "worker.handle-lifecycle.v1",
                "broker.peer-session.v1",
                "project.readonly-query.v1",
                "project.registration.v1");
            AssertReject(capabilities, "hello-request", "winproc-1-0000000000000001");

            var duplicate = StrictUtf8.GetBytes(
                StrictUtf8.GetString(Encode(valid)).Replace(
                    "{\"protocolVersion\":",
                    "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"\\u0070rotocolVersion\":"));
            Assert.That(
                Assert.Throws<InvalidDataException>(() =>
                    W24S6WorkerPeerHandshakeCodec.DecodeSessionAcceptedForTests(
                        duplicate,
                        "hello-request",
                        "winproc-1-0000000000000001")).Message,
                Is.EqualTo(W24S6WorkerBrokerConnection.ConnectionFailed));
        }

        private static JObject ValidReceipt()
        {
            return new JObject
            {
                ["protocolVersion"] = "vfxcomposer.protocol/1.0",
                ["messageKind"] = "peer.session.accepted",
                ["requestId"] = "hello-request",
                ["sessionId"] = "session-1-1",
                ["peerRole"] = "WORKER",
                ["brokerInstanceId"] = "broker-test",
                ["brokerGeneration"] = 1,
                ["processEpoch"] = "winproc-1-0000000000000001",
                ["negotiatedCapabilities"] = new JArray(
                    "broker.peer-session.v1",
                    "project.readonly-query.v1",
                    "project.registration.v1",
                    "worker.handle-lifecycle.v1")
            };
        }

        private static void AssertReject(JObject value, string requestId, string epoch)
        {
            var exception = Assert.Throws<InvalidDataException>(() =>
                W24S6WorkerPeerHandshakeCodec.DecodeSessionAcceptedForTests(
                    Encode(value),
                    requestId,
                    epoch));
            Assert.That(exception.Message, Is.EqualTo(W24S6WorkerBrokerConnection.ConnectionFailed));
            Assert.That(exception.InnerException, Is.Null);
        }

        private static byte[] Encode(JObject value)
        {
            return StrictUtf8.GetBytes(value.ToString(Formatting.None));
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        }
    }
}
