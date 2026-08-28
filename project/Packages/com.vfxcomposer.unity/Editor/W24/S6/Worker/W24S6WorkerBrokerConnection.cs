#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Editor.W24.S6.Worker
{
    /// <summary>
    /// Test-only local-pipe connector. It proves cross-runtime framing, handle
    /// lifecycle and fixed read queries against the non-publishable Broker test host;
    /// no production issuer, general command or authority is exposed.
    /// </summary>
    internal static class W24S6WorkerBrokerConnection
    {
        internal const string ConnectionFailed = "W24WKR003";
        internal const int ExpectedReadQueryCount = 5;
        private const int HeaderLength = 10;
        private const int MaximumFrameBytes = 1024 * 1024;

        internal static W24S6WorkerBrokerLifecycleResult RunLifecycleForTests(
            string pipeName,
            string imageDigest,
            int connectTimeoutMilliseconds)
        {
            if (!IsToken(pipeName) || !IsDigest(imageDigest) ||
                connectTimeoutMilliseconds < 1 || connectTimeoutMilliseconds > 60000)
                throw new InvalidDataException(ConnectionFailed);

            NamedPipeClientStream pipe = null;
            W24S6WorkerAuthenticatedSession session = null;
            W24S6WorkerProjectHandleLease lease = null;
            try
            {
                var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                var processEpoch = W24S6WorkerProcessEpoch.ObserveCurrent();
                const string helloRequestId = "unity-worker-hello";
                pipe = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.None);
                pipe.Connect(connectTimeoutMilliseconds);

                WriteFrame(
                    pipe,
                    W24S6WorkerPeerHandshakeCodec.CreateHelloForTests(
                        helloRequestId,
                        "unity-worker-test",
                        processId,
                        processEpoch,
                        imageDigest));
                pipe.Flush();
                var receipt = W24S6WorkerPeerHandshakeCodec.DecodeSessionAcceptedForTests(
                    ReadFrame(pipe),
                    helloRequestId,
                    processEpoch);
                session = W24S6WorkerAuthenticatedSession.IssueForTests(
                    receipt.SessionId,
                    receipt.BrokerGeneration);

                var grant = W24S6WorkerProtocolCodec.DecodeGrant(ReadFrame(pipe));
                string diagnosticCode;
                if (!W24S6WorkerProjectHandleLease.TryAdmit(
                        session,
                        grant,
                        out lease,
                        out diagnosticCode) ||
                    lease == null || !lease.IsUsable || !string.IsNullOrEmpty(diagnosticCode))
                    throw new InvalidDataException(ConnectionFailed);

                WriteFrame(
                    pipe,
                    W24S6WorkerProtocolCodec.CreateGrantAcknowledgementForTests(
                        grant,
                        "unity-worker-grant-ack"));
                pipe.Flush();

                for (var index = 0; index < ExpectedReadQueryCount; index++)
                {
                    WriteFrame(
                        pipe,
                        W24S6WorkerReadQueryHandler.Handle(ReadFrame(pipe), lease));
                    pipe.Flush();
                }

                var revoke = W24S6WorkerProtocolCodec.DecodeRevoke(ReadFrame(pipe));
                lease.Dispose();
                if (lease.IsAttached || lease.IsUsable)
                    throw new InvalidDataException(ConnectionFailed);
                WriteFrame(
                    pipe,
                    W24S6WorkerProtocolCodec.CreateRevokeAcknowledgementForTests(
                        grant,
                        revoke,
                        "unity-worker-revoke-ack"));
                pipe.Flush();

                return new W24S6WorkerBrokerLifecycleResult(
                    receipt.SessionId,
                    grant.LeaseId,
                    grant.SelfHash.Digest,
                    revoke.SelfHash.Digest,
                    ExpectedReadQueryCount);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new InvalidDataException(ConnectionFailed);
            }
            finally
            {
                if (lease != null) lease.Dispose();
                if (session != null) session.Dispose();
                if (pipe != null) pipe.Dispose();
            }
        }

        private static void WriteFrame(Stream stream, byte[] payload)
        {
            if (stream == null || payload == null || payload.Length < 1 ||
                payload.Length > MaximumFrameBytes)
                throw new InvalidDataException(ConnectionFailed);
            var header = new byte[HeaderLength];
            header[0] = (byte)'V';
            header[1] = (byte)'F';
            header[2] = (byte)'X';
            header[3] = (byte)'C';
            header[4] = 1;
            header[5] = 0;
            var length = (uint)payload.Length;
            header[6] = (byte)(length >> 24);
            header[7] = (byte)(length >> 16);
            header[8] = (byte)(length >> 8);
            header[9] = (byte)length;
            stream.Write(header, 0, header.Length);
            stream.Write(payload, 0, payload.Length);
        }

        private static byte[] ReadFrame(Stream stream)
        {
            var header = new byte[HeaderLength];
            ReadExactly(stream, header);
            if (header[0] != (byte)'V' || header[1] != (byte)'F' ||
                header[2] != (byte)'X' || header[3] != (byte)'C' ||
                header[4] != 1 || header[5] != 0)
                throw new InvalidDataException(ConnectionFailed);
            var length = ((uint)header[6] << 24) |
                         ((uint)header[7] << 16) |
                         ((uint)header[8] << 8) |
                         header[9];
            if (length < 1 || length > MaximumFrameBytes)
                throw new InvalidDataException(ConnectionFailed);
            var payload = new byte[(int)length];
            ReadExactly(stream, payload);
            return payload;
        }

        private static void ReadExactly(Stream stream, byte[] output)
        {
            var offset = 0;
            while (offset < output.Length)
            {
                var read = stream.Read(output, offset, output.Length - offset);
                if (read <= 0) throw new EndOfStreamException();
                offset += read;
            }
        }

        private static bool IsToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            return value.All(character =>
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '.' || character == '_' || character == ':' || character == '-');
        }

        private static bool IsDigest(string value)
        {
            if (value == null || value.Length != 71 ||
                !value.StartsWith("sha256:", StringComparison.Ordinal)) return false;
            for (var index = 7; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f')) return false;
            }
            return true;
        }

        private static bool IsExpectedFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is TimeoutException || exception is InvalidOperationException ||
                   exception is ArgumentException || exception is ObjectDisposedException ||
                   exception is PlatformNotSupportedException ||
                   exception is W24S6WorkerProtocolException;
        }
    }

    internal sealed class W24S6WorkerBrokerLifecycleResult
    {
        internal W24S6WorkerBrokerLifecycleResult(
            string sessionId,
            string leaseId,
            string grantSelfHash,
            string revokeSelfHash,
            int readQueryCount)
        {
            SessionId = sessionId;
            LeaseId = leaseId;
            GrantSelfHash = grantSelfHash;
            RevokeSelfHash = revokeSelfHash;
            ReadQueryCount = readQueryCount;
        }

        internal string SessionId { get; private set; }
        internal string LeaseId { get; private set; }
        internal string GrantSelfHash { get; private set; }
        internal string RevokeSelfHash { get; private set; }
        internal int ReadQueryCount { get; private set; }
    }

    internal static class W24S6WorkerPeerHandshakeCodec
    {
        private const int MaximumMessageBytes = 65536;
        private const string ProtocolVersion = "vfxcomposer.protocol/1.0";
        private const string HelloKind = "peer.hello";
        private const string AcceptedKind = "peer.session.accepted";
        private const string WorkerRole = "WORKER";
        private const string ImageIdentityType = "vfxcomposer.process-image/1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly string[] ReceiptFields =
        {
            "protocolVersion", "messageKind", "requestId", "sessionId", "peerRole",
            "brokerInstanceId", "brokerGeneration", "processEpoch", "negotiatedCapabilities"
        };
        private static readonly string[] WorkerCapabilities =
        {
            "broker.peer-session.v1",
            "project.readonly-query.v1",
            "project.registration.v1",
            "worker.handle-lifecycle.v1"
        };

        internal static byte[] CreateHelloForTests(
            string requestId,
            string peerInstanceId,
            int processId,
            string processEpoch,
            string imageDigest)
        {
            try
            {
                if (processId < 1 || !IsToken(requestId) || !IsToken(peerInstanceId) ||
                    !IsToken(processEpoch) || !IsDigest(imageDigest))
                    throw new InvalidDataException();
                var root = new JObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["messageKind"] = HelloKind,
                    ["requestId"] = requestId,
                    ["peerRole"] = WorkerRole,
                    ["peerInstanceId"] = peerInstanceId,
                    ["processId"] = processId,
                    ["processEpoch"] = processEpoch,
                    ["offeredCapabilities"] = new JArray(WorkerCapabilities),
                    ["imageIdentity"] = new JObject
                    {
                        ["typeTag"] = ImageIdentityType,
                        ["digest"] = imageDigest
                    }
                };
                return StrictUtf8.GetBytes(root.ToString(Formatting.None));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new InvalidDataException(W24S6WorkerBrokerConnection.ConnectionFailed);
            }
        }

        internal static W24S6WorkerPeerSessionReceipt DecodeSessionAcceptedForTests(
            byte[] bytes,
            string expectedRequestId,
            string expectedProcessEpoch)
        {
            try
            {
                if (bytes == null || bytes.Length < 1 || bytes.Length > MaximumMessageBytes ||
                    (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf))
                    throw new InvalidDataException();
                var root = W24StrictJsonText.ParseObject(
                    StrictUtf8.GetString((byte[])bytes.Clone()),
                    "Worker peer session receipt");
                RequireExactFields(root, ReceiptFields);
                RequireConstant(root, "protocolVersion", ProtocolVersion);
                RequireConstant(root, "messageKind", AcceptedKind);
                RequireConstant(root, "requestId", expectedRequestId);
                RequireConstant(root, "peerRole", WorkerRole);
                RequireConstant(root, "processEpoch", expectedProcessEpoch);
                var capabilities = root["negotiatedCapabilities"] as JArray;
                if (capabilities == null || capabilities.Count != WorkerCapabilities.Length)
                    throw new InvalidDataException();
                for (var index = 0; index < WorkerCapabilities.Length; index++)
                {
                    if (capabilities[index].Type != JTokenType.String ||
                        !string.Equals((string)capabilities[index], WorkerCapabilities[index], StringComparison.Ordinal))
                        throw new InvalidDataException();
                }
                return new W24S6WorkerPeerSessionReceipt(
                    RequireToken(root, "sessionId"),
                    RequireToken(root, "brokerInstanceId"),
                    RequireGeneration(root, "brokerGeneration"),
                    expectedProcessEpoch);
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new InvalidDataException(W24S6WorkerBrokerConnection.ConnectionFailed);
            }
        }

        private static void RequireExactFields(JObject root, IEnumerable<string> expected)
        {
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            var actual = root.Properties().Select(property => property.Name).ToArray();
            if (actual.Length != expectedSet.Count || actual.Any(value => !expectedSet.Contains(value)))
                throw new InvalidDataException();
        }

        private static string RequireConstant(JObject root, string field, string expected)
        {
            var value = RequireString(root, field);
            if (!string.Equals(value, expected, StringComparison.Ordinal)) throw new InvalidDataException();
            return value;
        }

        private static string RequireToken(JObject root, string field)
        {
            var value = RequireString(root, field);
            if (!IsToken(value)) throw new InvalidDataException();
            return value;
        }

        private static string RequireString(JObject root, string field)
        {
            var token = root[field];
            if (token == null || token.Type != JTokenType.String) throw new InvalidDataException();
            return (string)token;
        }

        private static long RequireGeneration(JObject root, string field)
        {
            var token = root[field];
            if (token == null || token.Type != JTokenType.Integer) throw new InvalidDataException();
            var value = Convert.ToInt64(((JValue)token).Value, CultureInfo.InvariantCulture);
            if (value < 1) throw new InvalidDataException();
            return value;
        }

        private static bool IsToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) return false;
            return value.All(character =>
                (character >= 'A' && character <= 'Z') ||
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '.' || character == '_' || character == ':' || character == '-');
        }

        private static bool IsDigest(string value)
        {
            if (value == null || value.Length != 71 ||
                !value.StartsWith("sha256:", StringComparison.Ordinal)) return false;
            for (var index = 7; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') &&
                    (character < 'a' || character > 'f')) return false;
            }
            return true;
        }

        private static bool IsExpectedFailure(Exception exception)
        {
            return exception is JsonException || exception is InvalidDataException ||
                   exception is DecoderFallbackException || exception is ArgumentException ||
                   exception is OverflowException || exception is FormatException;
        }
    }

    internal sealed class W24S6WorkerPeerSessionReceipt
    {
        internal W24S6WorkerPeerSessionReceipt(
            string sessionId,
            string brokerInstanceId,
            long brokerGeneration,
            string processEpoch)
        {
            SessionId = sessionId;
            BrokerInstanceId = brokerInstanceId;
            BrokerGeneration = brokerGeneration;
            ProcessEpoch = processEpoch;
        }

        internal string SessionId { get; private set; }
        internal string BrokerInstanceId { get; private set; }
        internal long BrokerGeneration { get; private set; }
        internal string ProcessEpoch { get; private set; }
    }
}
#endif
