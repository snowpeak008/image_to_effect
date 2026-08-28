using System;
using System.Threading;

namespace VFXComposer.Editor.W24.S6.External
{
    /// <summary>
    /// Dormant source-only registration boundary.  A production registration may eventually be
    /// supplied only by a separately reviewed host-owned issuer that delivers already-pinned
    /// handles.  No caller path, identity, JSON document, provider, or transport is accepted here.
    /// </summary>
    internal static class W24S6LocalProjectRegistration
    {
        internal const string SchemaVersion = "w24-s6/local-project-registration-lease-scaffold-v1";
        internal const string ProductionState = "REGISTRATION_ISSUER_PENDING";
        internal const string PendingDiagnosticCode = "W24FS001";

#if UNITY_INCLUDE_TESTS
        private static int productionAcquireAttemptCount;
        internal static int ProductionAcquireAttemptCountForTests { get { return Volatile.Read(ref productionAcquireAttemptCount); } }
        internal static void ResetProductionAcquireAttemptCountForTests() { Volatile.Write(ref productionAcquireAttemptCount, 0); }
#endif

        /// <summary>
        /// There is intentionally no production issuer.  This method has no caller-controlled
        /// input and must remain before envelope parsing, drive/path queries, or any file open.
        /// </summary>
        internal static bool TryAcquire(out W24S6RegisteredProjectLease lease, out string diagnosticCode)
        {
#if UNITY_INCLUDE_TESTS
            Interlocked.Increment(ref productionAcquireAttemptCount);
#endif
            lease = null;
            diagnosticCode = PendingDiagnosticCode;
            return false;
        }
    }

    /// <summary>
    /// Opaque lifecycle scaffold for a future request-scoped registered-project capability.  It
    /// contains no path or handle today, is not serializable, and grants no execution, machine,
    /// visual, migration, L3, L4, publication, or user authority.  Test issuance cannot reach the
    /// production adapter path.
    /// </summary>
    internal sealed class W24S6RegisteredProjectLease : IDisposable
    {
        private const int Active = 0;
        private const int Revoked = 1;
        private const int Disposed = 2;
        private static readonly object TestLeaseIssuer = new object();

        private readonly string projectIdentityHash;
        private readonly long generation;
        private int lifecycleState;

        private W24S6RegisteredProjectLease(object issuer, string projectIdentityHash, long generation)
        {
            if (!ReferenceEquals(issuer, TestLeaseIssuer))
                throw new InvalidOperationException("The registered-project lease issuer is not trusted.");
            if (!W24S6McpOperationEnvelopePolicy.IsCanonicalSha256(projectIdentityHash))
                throw new ArgumentException("A canonical registered project identity is required.", nameof(projectIdentityHash));
            if (generation <= 0)
                throw new ArgumentOutOfRangeException(nameof(generation), "A positive lease generation is required.");

            this.projectIdentityHash = projectIdentityHash;
            this.generation = generation;
            lifecycleState = Active;
        }

        internal string ProjectIdentityHash { get { return projectIdentityHash; } }
        internal long Generation { get { return generation; } }
        internal bool IsUsable(long expectedGeneration)
        {
            return expectedGeneration == generation && Volatile.Read(ref lifecycleState) == Active;
        }

        internal void Revoke()
        {
            Interlocked.CompareExchange(ref lifecycleState, Revoked, Active);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref lifecycleState, Disposed);
        }

#if UNITY_INCLUDE_TESTS
        internal static W24S6RegisteredProjectLease IssueForTests(string projectIdentityHash, long generation)
        {
            return new W24S6RegisteredProjectLease(TestLeaseIssuer, projectIdentityHash, generation);
        }
#endif
    }
}
