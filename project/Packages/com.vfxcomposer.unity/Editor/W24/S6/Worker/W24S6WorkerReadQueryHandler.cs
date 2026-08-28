using System;
using System.IO;
using System.Text;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Editor.W24.S6.Worker
{
    /// <summary>
    /// Read-only Worker endpoint. A query is decoded before any content read, then
    /// bound to the exact opaque lease and a closed registry-to-relative-path mapping.
    /// </summary>
    internal static class W24S6WorkerReadQueryHandler
    {
        internal const string QueryFailed = "W24WKR004";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static byte[] Handle(
            byte[] queryBytes,
            W24S6WorkerProjectHandleLease lease)
        {
            var query = W24S6WorkerReadQueryCodec.DecodeQuery(queryBytes);
            if (!MatchesLease(query, lease))
                return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                    query,
                    W24S6WorkerReadQueryCodec.ProjectLeaseRejected);

            var target = W24S6WorkerReadOnlyHost.Resolve(query.DocumentKind, query.DocumentId);
            byte[] content;
            var read = target.UseProjectRoot
                ? lease.TryReadProjectRelative(target.RelativePath, out content)
                : lease.TryReadRepositoryRelative(target.RelativePath, out content);
            if (!read || content == null)
            {
                var code = MatchesLease(query, lease)
                    ? W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable
                    : W24S6WorkerReadQueryCodec.ProjectLeaseRejected;
                return W24S6WorkerReadQueryCodec.CreateRejectedResult(query, code);
            }

            try
            {
                W24StrictJsonText.ParseObject(
                    StrictUtf8.GetString((byte[])content.Clone()),
                    "Worker project document");
            }
            catch (Exception exception) when (
                exception is Newtonsoft.Json.JsonException ||
                exception is DecoderFallbackException || exception is InvalidDataException ||
                exception is ArgumentException)
            {
                return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                    query,
                    W24S6WorkerReadQueryCodec.ProjectDocumentUnavailable);
            }

            var contentHash = W24S6WorkerProtocolCodec.ComputeTypedHash(
                W24S6WorkerReadQueryCodec.ContentHashType,
                content);
            if (query.ExpectedContentHash != null &&
                !W24S6WorkerProtocolCodec.FixedTimeEquals(query.ExpectedContentHash, contentHash))
                return W24S6WorkerReadQueryCodec.CreateRejectedResult(
                    query,
                    W24S6WorkerReadQueryCodec.ProjectDocumentContentMismatch);
            return W24S6WorkerReadQueryCodec.CreateAcceptedResult(query, content);
        }

        private static bool MatchesLease(
            W24S6WorkerReadDocumentQuery query,
            W24S6WorkerProjectHandleLease lease)
        {
            return query != null && lease != null && lease.IsUsable &&
                   query.LeaseGeneration == lease.LeaseGeneration &&
                   string.Equals(query.LeaseId, lease.LeaseId, StringComparison.Ordinal) &&
                   W24S6WorkerProtocolCodec.FixedTimeEquals(
                       query.ProjectIdentity,
                       lease.ProjectIdentity);
        }
    }
}
