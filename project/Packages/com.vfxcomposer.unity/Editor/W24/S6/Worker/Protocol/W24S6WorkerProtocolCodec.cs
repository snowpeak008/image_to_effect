using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.W24;

namespace VFXComposer.Editor.W24.S6.Worker.Protocol
{
    /// <summary>
    /// Narrow Unity-side projection of the Worker handle-lifecycle wire contract.
    /// It parses and seals messages only; it does not open, close, duplicate or use handles.
    /// </summary>
    internal static class W24S6WorkerProtocolCodec
    {
        internal const string ProtocolVersion = "vfxcomposer.protocol/1.0";
        internal const string GrantKind = "worker.project.handle.grant";
#if UNITY_INCLUDE_TESTS
        internal const string GrantAcknowledgementKind = "worker.project.handle.grant.ack";
#endif
        internal const string RevokeKind = "worker.project.handle.revoke";
#if UNITY_INCLUDE_TESTS
        internal const string RevokeAcknowledgementKind = "worker.project.handle.revoke.ack";
#endif
        internal const string HandleEncoding = "win-handle-u64-lower-hex/1";
#if UNITY_INCLUDE_TESTS
        internal const string GrantAccepted = "GRANT_ACCEPTED";
#endif
        internal const string LeaseRevoked = "LEASE_REVOKED";
#if UNITY_INCLUDE_TESTS
        internal const string HandlesClosed = "HANDLES_CLOSED";
#endif
        internal const string GrantSelfHashType = "vfxcomposer.worker-project-handle-grant/1";
#if UNITY_INCLUDE_TESTS
        internal const string GrantAcknowledgementSelfHashType = "vfxcomposer.worker-project-handle-grant-ack/1";
#endif
        internal const string RevokeSelfHashType = "vfxcomposer.worker-project-handle-revoke/1";
#if UNITY_INCLUDE_TESTS
        internal const string RevokeAcknowledgementSelfHashType = "vfxcomposer.worker-project-handle-revoke-ack/1";
#endif
        internal const int MaximumMessageBytes = 65536;

        internal const string ProjectIdentityType = "vfxcomposer.project-identity/1";
        internal const string VolumeIdentityType = "vfxcomposer.volume-identity/1";
        internal const string DirectoryIdentityType = "vfxcomposer.directory-identity/1";
        private const string DigestPrefix = "sha256:";
        private static readonly byte[] TypedHashDomain = Encoding.ASCII.GetBytes(
            "vfxcomposer.typed-sha256.length-prefixed/1\0");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static readonly string[] GrantFields =
        {
            "protocolVersion", "messageKind", "requestId", "leaseId", "registeredProjectId",
            "projectIdentity", "volumeIdentity", "repositoryIdentity", "projectRootIdentity",
            "brokerGeneration", "registrationGeneration", "leaseGeneration", "workerSessionId",
            "workerProcessEpoch", "handleEncoding", "volumeHandle", "repositoryHandle",
            "projectRootHandle", "selfHash"
        };

#if UNITY_INCLUDE_TESTS
        private static readonly string[] GrantAcknowledgementFields =
        {
            "protocolVersion", "messageKind", "requestId", "leaseId", "brokerGeneration",
            "leaseGeneration", "workerSessionId", "workerProcessEpoch", "grantSelfHash",
            "disposition", "selfHash"
        };
#endif

        private static readonly string[] RevokeFields =
        {
            "protocolVersion", "messageKind", "requestId", "leaseId", "brokerGeneration",
            "leaseGeneration", "workerSessionId", "workerProcessEpoch", "grantSelfHash",
            "reasonCode", "selfHash"
        };

#if UNITY_INCLUDE_TESTS
        private static readonly string[] RevokeAcknowledgementFields =
        {
            "protocolVersion", "messageKind", "requestId", "leaseId", "brokerGeneration",
            "leaseGeneration", "workerSessionId", "workerProcessEpoch", "grantSelfHash",
            "revokeSelfHash", "disposition", "selfHash"
        };
#endif

        internal static W24S6WorkerProjectHandleGrant DecodeGrant(byte[] utf8Json)
        {
            return Decode(utf8Json, ParseGrant);
        }

        internal static W24S6WorkerProjectHandleRevoke DecodeRevoke(byte[] utf8Json)
        {
            return Decode(utf8Json, ParseRevoke);
        }

#if UNITY_INCLUDE_TESTS
        internal static byte[] CreateGrantForTests(
            string requestId,
            string leaseId,
            string registeredProjectId,
            W24S6WorkerTypedHash projectIdentity,
            W24S6WorkerTypedHash volumeIdentity,
            W24S6WorkerTypedHash repositoryIdentity,
            W24S6WorkerTypedHash projectRootIdentity,
            long brokerGeneration,
            long registrationGeneration,
            long leaseGeneration,
            string workerSessionId,
            string workerProcessEpoch,
            string volumeHandle,
            string repositoryHandle,
            string projectRootHandle)
        {
            try
            {
                var root = new JObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["messageKind"] = GrantKind,
                    ["requestId"] = RequireTokenText(requestId),
                    ["leaseId"] = RequireTokenText(leaseId),
                    ["registeredProjectId"] = RequireTokenText(registeredProjectId),
                    ["projectIdentity"] = WriteTypedHash(projectIdentity),
                    ["volumeIdentity"] = WriteTypedHash(volumeIdentity),
                    ["repositoryIdentity"] = WriteTypedHash(repositoryIdentity),
                    ["projectRootIdentity"] = WriteTypedHash(projectRootIdentity),
                    ["brokerGeneration"] = brokerGeneration,
                    ["registrationGeneration"] = registrationGeneration,
                    ["leaseGeneration"] = leaseGeneration,
                    ["workerSessionId"] = RequireTokenText(workerSessionId),
                    ["workerProcessEpoch"] = RequireTokenText(workerProcessEpoch),
                    ["handleEncoding"] = HandleEncoding,
                    ["volumeHandle"] = volumeHandle,
                    ["repositoryHandle"] = repositoryHandle,
                    ["projectRootHandle"] = projectRootHandle
                };
                var bytes = Seal(root, GrantSelfHashType);
                DecodeGrant(bytes);
                return bytes;
            }
            catch (W24S6WorkerProtocolException)
            {
                throw;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new W24S6WorkerProtocolException();
            }
        }

        internal static W24S6WorkerProjectHandleGrantAcknowledgement DecodeGrantAcknowledgementForTests(
            byte[] utf8Json)
        {
            return Decode(utf8Json, ParseGrantAcknowledgement);
        }

        internal static W24S6WorkerProjectHandleRevokeAcknowledgement DecodeRevokeAcknowledgementForTests(
            byte[] utf8Json)
        {
            return Decode(utf8Json, ParseRevokeAcknowledgement);
        }

        internal static byte[] CreateGrantAcknowledgementForTests(
            W24S6WorkerProjectHandleGrant grant,
            string requestId)
        {
            if (grant == null) throw new W24S6WorkerProtocolException();
            try
            {
                var root = new JObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["messageKind"] = GrantAcknowledgementKind,
                    ["requestId"] = RequireTokenText(requestId),
                    ["leaseId"] = grant.LeaseId,
                    ["brokerGeneration"] = grant.BrokerGeneration,
                    ["leaseGeneration"] = grant.LeaseGeneration,
                    ["workerSessionId"] = grant.WorkerSessionId,
                    ["workerProcessEpoch"] = grant.WorkerProcessEpoch,
                    ["grantSelfHash"] = WriteTypedHash(grant.SelfHash),
                    ["disposition"] = GrantAccepted
                };
                var bytes = Seal(root, GrantAcknowledgementSelfHashType);
                DecodeGrantAcknowledgementForTests(bytes);
                return bytes;
            }
            catch (W24S6WorkerProtocolException)
            {
                throw;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new W24S6WorkerProtocolException();
            }
        }

        internal static byte[] CreateRevokeAcknowledgementForTests(
            W24S6WorkerProjectHandleGrant grant,
            W24S6WorkerProjectHandleRevoke revoke,
            string requestId)
        {
            if (grant == null || revoke == null || !MatchesGrant(grant, revoke))
                throw new W24S6WorkerProtocolException();
            try
            {
                var root = new JObject
                {
                    ["protocolVersion"] = ProtocolVersion,
                    ["messageKind"] = RevokeAcknowledgementKind,
                    ["requestId"] = RequireTokenText(requestId),
                    ["leaseId"] = grant.LeaseId,
                    ["brokerGeneration"] = grant.BrokerGeneration,
                    ["leaseGeneration"] = grant.LeaseGeneration,
                    ["workerSessionId"] = grant.WorkerSessionId,
                    ["workerProcessEpoch"] = grant.WorkerProcessEpoch,
                    ["grantSelfHash"] = WriteTypedHash(grant.SelfHash),
                    ["revokeSelfHash"] = WriteTypedHash(revoke.SelfHash),
                    ["disposition"] = HandlesClosed
                };
                var bytes = Seal(root, RevokeAcknowledgementSelfHashType);
                DecodeRevokeAcknowledgementForTests(bytes);
                return bytes;
            }
            catch (W24S6WorkerProtocolException)
            {
                throw;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new W24S6WorkerProtocolException();
            }
        }
#endif

        private static T Decode<T>(byte[] utf8Json, Func<JObject, T> projector)
        {
            try
            {
                if (utf8Json == null || utf8Json.Length == 0 || utf8Json.Length > MaximumMessageBytes ||
                    (utf8Json.Length >= 3 && utf8Json[0] == 0xef && utf8Json[1] == 0xbb && utf8Json[2] == 0xbf))
                    throw new InvalidDataException();
                var text = StrictUtf8.GetString((byte[])utf8Json.Clone());
                var root = W24StrictJsonText.ParseObject(text, "Worker protocol message");
                return projector(root);
            }
            catch (W24S6WorkerProtocolException)
            {
                throw;
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new W24S6WorkerProtocolException();
            }
        }

        private static W24S6WorkerProjectHandleGrant ParseGrant(JObject root)
        {
            RequireExactFields(root, GrantFields);
            RequireConstant(root, "protocolVersion", ProtocolVersion);
            RequireConstant(root, "messageKind", GrantKind);
            var selfHash = RequireTypedHash(root, "selfHash", GrantSelfHashType);
            RequireSelfHash(root, selfHash, GrantSelfHashType);
            return new W24S6WorkerProjectHandleGrant(
                ProtocolVersion,
                GrantKind,
                RequireToken(root, "requestId"),
                RequireToken(root, "leaseId"),
                RequireToken(root, "registeredProjectId"),
                RequireTypedHash(root, "projectIdentity", ProjectIdentityType),
                RequireTypedHash(root, "volumeIdentity", VolumeIdentityType),
                RequireTypedHash(root, "repositoryIdentity", DirectoryIdentityType),
                RequireTypedHash(root, "projectRootIdentity", DirectoryIdentityType),
                RequireGeneration(root, "brokerGeneration"),
                RequireGeneration(root, "registrationGeneration"),
                RequireGeneration(root, "leaseGeneration"),
                RequireToken(root, "workerSessionId"),
                RequireToken(root, "workerProcessEpoch"),
                RequireConstant(root, "handleEncoding", HandleEncoding),
                RequireHandle(root, "volumeHandle"),
                RequireHandle(root, "repositoryHandle"),
                RequireHandle(root, "projectRootHandle"),
                selfHash);
        }

#if UNITY_INCLUDE_TESTS
        private static W24S6WorkerProjectHandleGrantAcknowledgement ParseGrantAcknowledgement(JObject root)
        {
            RequireExactFields(root, GrantAcknowledgementFields);
            RequireConstant(root, "protocolVersion", ProtocolVersion);
            RequireConstant(root, "messageKind", GrantAcknowledgementKind);
            var selfHash = RequireTypedHash(root, "selfHash", GrantAcknowledgementSelfHashType);
            RequireSelfHash(root, selfHash, GrantAcknowledgementSelfHashType);
            return new W24S6WorkerProjectHandleGrantAcknowledgement(
                ProtocolVersion,
                GrantAcknowledgementKind,
                RequireToken(root, "requestId"),
                RequireToken(root, "leaseId"),
                RequireGeneration(root, "brokerGeneration"),
                RequireGeneration(root, "leaseGeneration"),
                RequireToken(root, "workerSessionId"),
                RequireToken(root, "workerProcessEpoch"),
                RequireTypedHash(root, "grantSelfHash", GrantSelfHashType),
                RequireConstant(root, "disposition", GrantAccepted),
                selfHash);
        }
#endif

        private static W24S6WorkerProjectHandleRevoke ParseRevoke(JObject root)
        {
            RequireExactFields(root, RevokeFields);
            RequireConstant(root, "protocolVersion", ProtocolVersion);
            RequireConstant(root, "messageKind", RevokeKind);
            var selfHash = RequireTypedHash(root, "selfHash", RevokeSelfHashType);
            RequireSelfHash(root, selfHash, RevokeSelfHashType);
            return new W24S6WorkerProjectHandleRevoke(
                ProtocolVersion,
                RevokeKind,
                RequireToken(root, "requestId"),
                RequireToken(root, "leaseId"),
                RequireGeneration(root, "brokerGeneration"),
                RequireGeneration(root, "leaseGeneration"),
                RequireToken(root, "workerSessionId"),
                RequireToken(root, "workerProcessEpoch"),
                RequireTypedHash(root, "grantSelfHash", GrantSelfHashType),
                RequireConstant(root, "reasonCode", LeaseRevoked),
                selfHash);
        }

#if UNITY_INCLUDE_TESTS
        private static W24S6WorkerProjectHandleRevokeAcknowledgement ParseRevokeAcknowledgement(JObject root)
        {
            RequireExactFields(root, RevokeAcknowledgementFields);
            RequireConstant(root, "protocolVersion", ProtocolVersion);
            RequireConstant(root, "messageKind", RevokeAcknowledgementKind);
            var selfHash = RequireTypedHash(root, "selfHash", RevokeAcknowledgementSelfHashType);
            RequireSelfHash(root, selfHash, RevokeAcknowledgementSelfHashType);
            return new W24S6WorkerProjectHandleRevokeAcknowledgement(
                ProtocolVersion,
                RevokeAcknowledgementKind,
                RequireToken(root, "requestId"),
                RequireToken(root, "leaseId"),
                RequireGeneration(root, "brokerGeneration"),
                RequireGeneration(root, "leaseGeneration"),
                RequireToken(root, "workerSessionId"),
                RequireToken(root, "workerProcessEpoch"),
                RequireTypedHash(root, "grantSelfHash", GrantSelfHashType),
                RequireTypedHash(root, "revokeSelfHash", RevokeSelfHashType),
                RequireConstant(root, "disposition", HandlesClosed),
                selfHash);
        }

        private static bool MatchesGrant(
            W24S6WorkerProjectHandleGrant grant,
            W24S6WorkerProjectHandleRevoke revoke)
        {
            return string.Equals(grant.LeaseId, revoke.LeaseId, StringComparison.Ordinal) &&
                   grant.BrokerGeneration == revoke.BrokerGeneration &&
                   grant.LeaseGeneration == revoke.LeaseGeneration &&
                   string.Equals(grant.WorkerSessionId, revoke.WorkerSessionId, StringComparison.Ordinal) &&
                   string.Equals(grant.WorkerProcessEpoch, revoke.WorkerProcessEpoch, StringComparison.Ordinal) &&
                   FixedTimeEquals(grant.SelfHash, revoke.GrantSelfHash);
        }
#endif

        private static void RequireExactFields(JObject root, IEnumerable<string> expected)
        {
            var expectedSet = new HashSet<string>(expected, StringComparer.Ordinal);
            var actual = root.Properties().Select(property => property.Name).ToArray();
            if (actual.Length != expectedSet.Count || actual.Any(name => !expectedSet.Contains(name)))
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
            return RequireTokenText(RequireString(root, field));
        }

        private static string RequireTokenText(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 128) throw new InvalidDataException();
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < 'A' || character > 'Z') &&
                    (character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '.' && character != '_' && character != ':' && character != '-')
                    throw new InvalidDataException();
            }
            return value;
        }

        private static string RequireHandle(JObject root, string field)
        {
            var value = RequireString(root, field);
            if (value.Length != 16 || value == "0000000000000000" || value == "ffffffffffffffff")
                throw new InvalidDataException();
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
                    throw new InvalidDataException();
            }
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

        private static W24S6WorkerTypedHash RequireTypedHash(JObject root, string field, string expectedType)
        {
            var value = root[field] as JObject;
            if (value == null) throw new InvalidDataException();
            RequireExactFields(value, new[] { "typeTag", "digest" });
            var typeTag = RequireString(value, "typeTag");
            var digest = RequireString(value, "digest");
            if (!string.Equals(typeTag, expectedType, StringComparison.Ordinal) || !IsDigest(digest))
                throw new InvalidDataException();
            return new W24S6WorkerTypedHash(typeTag, digest);
        }

        private static void RequireSelfHash(
            JObject root,
            W24S6WorkerTypedHash claimed,
            string typeTag)
        {
            var computed = ComputeTypedHash(typeTag, Canonicalize(root));
            if (!FixedTimeEquals(claimed, computed)) throw new InvalidDataException();
        }

#if UNITY_INCLUDE_TESTS
        private static byte[] Seal(JObject root, string typeTag)
        {
            if (root["selfHash"] != null) throw new InvalidDataException();
            root["selfHash"] = WriteTypedHash(ComputeTypedHash(typeTag, Canonicalize(root)));
            return StrictUtf8.GetBytes(root.ToString(Formatting.None));
        }

        private static JObject WriteTypedHash(W24S6WorkerTypedHash hash)
        {
            if (hash == null) throw new InvalidDataException();
            return new JObject
            {
                ["typeTag"] = hash.TypeTag,
                ["digest"] = hash.Digest
            };
        }
#endif

        internal static W24S6WorkerTypedHash ComputeTypedHash(string typeTag, byte[] payload)
        {
            var typeBytes = StrictUtf8.GetBytes(typeTag);
            byte[] digest;
            using (var preimage = new MemoryStream())
            {
                preimage.Write(TypedHashDomain, 0, TypedHashDomain.Length);
                WriteU32(preimage, (uint)typeBytes.Length);
                preimage.Write(typeBytes, 0, typeBytes.Length);
                WriteU64(preimage, (ulong)payload.Length);
                preimage.Write(payload, 0, payload.Length);
                using (var sha = SHA256.Create()) digest = sha.ComputeHash(preimage.ToArray());
            }
            return new W24S6WorkerTypedHash(typeTag, DigestPrefix + Hex(digest));
        }

        private static byte[] Canonicalize(JObject root)
        {
            using (var output = new MemoryStream())
            {
                WriteCanonical(root, output, true);
                return output.ToArray();
            }
        }

        private static void WriteCanonical(JToken token, Stream output, bool isRoot)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                WriteAscii(output, "null");
                return;
            }
            if (token.Type == JTokenType.Object)
            {
                output.WriteByte((byte)'{');
                var properties = ((JObject)token).Properties()
                    .Where(property => !isRoot || !string.Equals(property.Name, "selfHash", StringComparison.Ordinal))
                    .Select(property => new CanonicalProperty(property, StrictUtf8.GetBytes(property.Name)))
                    .ToList();
                properties.Sort((left, right) => CompareBytes(left.NameBytes, right.NameBytes));
                for (var index = 0; index < properties.Count; index++)
                {
                    if (index != 0) output.WriteByte((byte)',');
                    WriteJsonString(output, properties[index].Property.Name);
                    output.WriteByte((byte)':');
                    WriteCanonical(properties[index].Property.Value, output, false);
                }
                output.WriteByte((byte)'}');
                return;
            }
            if (token.Type == JTokenType.Array)
            {
                output.WriteByte((byte)'[');
                var array = (JArray)token;
                for (var index = 0; index < array.Count; index++)
                {
                    if (index != 0) output.WriteByte((byte)',');
                    WriteCanonical(array[index], output, false);
                }
                output.WriteByte((byte)']');
                return;
            }
            if (token.Type == JTokenType.String)
            {
                WriteJsonString(output, (string)token);
                return;
            }
            if (token.Type == JTokenType.Integer)
            {
                WriteAscii(output, Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture));
                return;
            }
            if (token.Type == JTokenType.Boolean)
            {
                WriteAscii(output, (bool)token ? "true" : "false");
                return;
            }
            throw new InvalidDataException();
        }

        private static void WriteJsonString(Stream output, string value)
        {
            if (value == null) throw new InvalidDataException();
            output.WriteByte((byte)'"');
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (character > 0x7f) throw new InvalidDataException();
                switch (character)
                {
                    case '"': WriteAscii(output, "\\\""); break;
                    case '\\': WriteAscii(output, "\\\\"); break;
                    case '\b': WriteAscii(output, "\\b"); break;
                    case '\f': WriteAscii(output, "\\f"); break;
                    case '\n': WriteAscii(output, "\\n"); break;
                    case '\r': WriteAscii(output, "\\r"); break;
                    case '\t': WriteAscii(output, "\\t"); break;
                    default:
                        if (character < 0x20)
                        {
                            WriteAscii(output, "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else output.WriteByte((byte)character);
                        break;
                }
            }
            output.WriteByte((byte)'"');
        }

        internal static bool FixedTimeEquals(W24S6WorkerTypedHash left, W24S6WorkerTypedHash right)
        {
            if (left == null || right == null ||
                !string.Equals(left.TypeTag, right.TypeTag, StringComparison.Ordinal) ||
                left.Digest.Length != right.Digest.Length)
                return false;
            var difference = 0;
            for (var index = 0; index < left.Digest.Length; index++)
                difference |= left.Digest[index] ^ right.Digest[index];
            return difference == 0;
        }

        private static bool IsDigest(string value)
        {
            if (value == null || value.Length != 71 || !value.StartsWith(DigestPrefix, StringComparison.Ordinal))
                return false;
            for (var index = DigestPrefix.Length; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < '0' || character > '9') && (character < 'a' || character > 'f')) return false;
            }
            return true;
        }

        private static bool IsExpectedFailure(Exception exception)
        {
            return exception is JsonException || exception is InvalidDataException ||
                   exception is DecoderFallbackException || exception is ArgumentException ||
                   exception is OverflowException || exception is FormatException ||
                   exception is CryptographicException;
        }

        private static void WriteAscii(Stream output, string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            output.Write(bytes, 0, bytes.Length);
        }

        private static void WriteU32(Stream output, uint value)
        {
            output.WriteByte((byte)(value >> 24));
            output.WriteByte((byte)(value >> 16));
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private static void WriteU64(Stream output, ulong value)
        {
            for (var shift = 56; shift >= 0; shift -= 8) output.WriteByte((byte)(value >> shift));
        }

        private static int CompareBytes(byte[] left, byte[] right)
        {
            var count = Math.Min(left.Length, right.Length);
            for (var index = 0; index < count; index++)
            {
                var difference = left[index].CompareTo(right[index]);
                if (difference != 0) return difference;
            }
            return left.Length.CompareTo(right.Length);
        }

        private static string Hex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private sealed class CanonicalProperty
        {
            internal CanonicalProperty(JProperty property, byte[] nameBytes)
            {
                Property = property;
                NameBytes = nameBytes;
            }

            internal JProperty Property { get; private set; }
            internal byte[] NameBytes { get; private set; }
        }
    }
}
