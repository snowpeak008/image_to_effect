using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S6.Worker;

namespace VFXComposer.Editor.W24.S6.Worker.Protocol
{
    internal sealed class W24S6WorkerReadDocumentQuery
    {
        internal W24S6WorkerReadDocumentQuery(
            string requestId,
            string leaseId,
            W24S6WorkerTypedHash projectIdentity,
            long leaseGeneration,
            string documentKind,
            string documentId,
            W24S6WorkerTypedHash expectedContentHash)
        {
            RequestId = requestId;
            LeaseId = leaseId;
            ProjectIdentity = projectIdentity;
            LeaseGeneration = leaseGeneration;
            DocumentKind = documentKind;
            DocumentId = documentId;
            ExpectedContentHash = expectedContentHash;
        }

        internal string RequestId { get; private set; }
        internal string LeaseId { get; private set; }
        internal W24S6WorkerTypedHash ProjectIdentity { get; private set; }
        internal long LeaseGeneration { get; private set; }
        internal string DocumentKind { get; private set; }
        internal string DocumentId { get; private set; }
        internal W24S6WorkerTypedHash ExpectedContentHash { get; private set; }
    }

    internal static class W24S6WorkerReadQueryCodec
    {
        internal const string QueryKind = "project.document.read.query";
        internal const string ResultKind = "project.document.read.result";
        internal const string ContentHashType = "vfxcomposer.document-content/1";
        internal const string LibraryIndexKind = "LIBRARY_INDEX";
        internal const string ManifestKind = "MANIFEST";
        internal const string ContractKind = "CONTRACT";
        internal const string TraceKind = "TRACE";
        internal const string ProjectLeaseRejected = "VFXP0007";
        internal const string ProjectDocumentUnavailable = "VFXP0008";
        internal const string ProjectDocumentContentMismatch = "VFXP0009";
        private const int MaximumMessageBytes = 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly string[] QueryFields =
        {
            "protocolVersion", "messageKind", "requestId", "leaseId", "projectIdentity",
            "leaseGeneration", "documentKind", "documentId", "expectedContentHash"
        };
        private static readonly string[] DiagnosticFields =
        {
            "protocolVersion", "messageKind", "code", "severity", "message", "retryable"
        };

        internal static W24S6WorkerReadDocumentQuery DecodeQuery(byte[] utf8Json)
        {
            try
            {
                if (utf8Json == null || utf8Json.Length < 1 || utf8Json.Length > MaximumMessageBytes ||
                    utf8Json.Length >= 3 && utf8Json[0] == 0xef && utf8Json[1] == 0xbb && utf8Json[2] == 0xbf)
                    throw new InvalidDataException();
                var root = W24StrictJsonText.ParseObject(
                    StrictUtf8.GetString((byte[])utf8Json.Clone()),
                    "Worker read query");
                RequireExactFields(root, QueryFields);
                RequireConstant(root, "protocolVersion", W24S6WorkerProtocolCodec.ProtocolVersion);
                RequireConstant(root, "messageKind", QueryKind);
                var documentKind = RequireDocumentKind(root, "documentKind");
                var documentId = RequireDocumentId(root, "documentId", documentKind);
                return new W24S6WorkerReadDocumentQuery(
                    RequireToken(root, "requestId"),
                    RequireToken(root, "leaseId"),
                    RequireTypedHash(root, "projectIdentity", W24S6WorkerProtocolCodec.ProjectIdentityType),
                    RequireGeneration(root, "leaseGeneration"),
                    documentKind,
                    documentId,
                    RequireNullableTypedHash(root, "expectedContentHash", ContentHashType));
            }
            catch (Exception exception) when (IsExpectedFailure(exception))
            {
                throw new W24S6WorkerProtocolException();
            }
        }

        internal static byte[] CreateAcceptedResult(
            W24S6WorkerReadDocumentQuery query,
            byte[] content)
        {
            if (query == null || content == null ||
                content.Length > W24S6WorkerProjectHandleLease.MaximumReadBytes)
                throw new W24S6WorkerProtocolException();
            var root = ResultEnvelope(query, true);
            root["contentHash"] = WriteTypedHash(
                W24S6WorkerProtocolCodec.ComputeTypedHash(ContentHashType, content));
            root["byteLength"] = content.Length;
            root["contentBase64"] = Convert.ToBase64String(content);
            root["diagnostic"] = JValue.CreateNull();
            return StrictUtf8.GetBytes(root.ToString(Formatting.None));
        }

        internal static byte[] CreateRejectedResult(
            W24S6WorkerReadDocumentQuery query,
            string diagnosticCode)
        {
            if (query == null) throw new W24S6WorkerProtocolException();
            var diagnostic = Diagnostic(diagnosticCode);
            RequireExactFields(diagnostic, DiagnosticFields);
            var root = ResultEnvelope(query, false);
            root["contentHash"] = JValue.CreateNull();
            root["byteLength"] = 0;
            root["contentBase64"] = JValue.CreateNull();
            root["diagnostic"] = diagnostic;
            return StrictUtf8.GetBytes(root.ToString(Formatting.None));
        }

        private static JObject ResultEnvelope(W24S6WorkerReadDocumentQuery query, bool accepted)
        {
            return new JObject
            {
                ["protocolVersion"] = W24S6WorkerProtocolCodec.ProtocolVersion,
                ["messageKind"] = ResultKind,
                ["requestId"] = query.RequestId,
                ["accepted"] = accepted,
                ["projectIdentity"] = WriteTypedHash(query.ProjectIdentity),
                ["documentKind"] = query.DocumentKind,
                ["documentId"] = query.DocumentId
            };
        }

        private static JObject Diagnostic(string code)
        {
            string message;
            if (string.Equals(code, ProjectLeaseRejected, StringComparison.Ordinal))
                message = "The project lease is unavailable or no longer current.";
            else if (string.Equals(code, ProjectDocumentUnavailable, StringComparison.Ordinal))
                message = "The requested project document is unavailable.";
            else if (string.Equals(code, ProjectDocumentContentMismatch, StringComparison.Ordinal))
                message = "The project document does not match the requested content identity.";
            else
                throw new W24S6WorkerProtocolException();
            return new JObject
            {
                ["protocolVersion"] = W24S6WorkerProtocolCodec.ProtocolVersion,
                ["messageKind"] = "diagnostic",
                ["code"] = code,
                ["severity"] = "ERROR",
                ["message"] = message,
                ["retryable"] = true
            };
        }

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
            var value = RequireString(root, field);
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

        private static string RequireDocumentKind(JObject root, string field)
        {
            var value = RequireString(root, field);
            if (value != LibraryIndexKind && value != ManifestKind &&
                value != ContractKind && value != TraceKind)
                throw new InvalidDataException();
            return value;
        }

        private static string RequireDocumentId(JObject root, string field, string documentKind)
        {
            var value = RequireString(root, field);
            if (documentKind == LibraryIndexKind)
            {
                if (value != "project") throw new InvalidDataException();
                return value;
            }
            if (string.IsNullOrEmpty(value) || value.Length > 96 || value[0] < 'a' || value[0] > 'z')
                throw new InvalidDataException();
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') &&
                    character != '_' && character != '-')
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
            var raw = ((JValue)token).Value;
            if (!(raw is long)) throw new InvalidDataException();
            var value = (long)raw;
            if (value < 1) throw new InvalidDataException();
            return value;
        }

        private static W24S6WorkerTypedHash RequireNullableTypedHash(
            JObject root,
            string field,
            string expectedType)
        {
            var token = root[field];
            if (token == null) throw new InvalidDataException();
            return token.Type == JTokenType.Null
                ? null
                : RequireTypedHash(root, field, expectedType);
        }

        private static W24S6WorkerTypedHash RequireTypedHash(
            JObject root,
            string field,
            string expectedType)
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

        private static JObject WriteTypedHash(W24S6WorkerTypedHash hash)
        {
            if (hash == null) throw new InvalidDataException();
            return new JObject
            {
                ["typeTag"] = hash.TypeTag,
                ["digest"] = hash.Digest
            };
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
}
