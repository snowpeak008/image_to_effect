using System;

namespace VFXComposer.Editor.W24.S6.External
{
    /// <summary>
    /// Immutable routing data for the local read scaffold.  There is deliberately no production
    /// issuer until a project-registration protocol is frozen.  Test builds can issue a synthetic
    /// path binding only through the binding-private token below; that token is separate from the
    /// dormant registration-lease test issuer and no caller-provided root reaches production.
    /// </summary>
    internal sealed class W24S6LocalProjectBinding
    {
        private static readonly object TestBindingIssuer = new object();

        internal string ProjectRoot { get; }
        internal string RepositoryRoot { get; }
        internal string ProjectIdentityHash { get; }

        private W24S6LocalProjectBinding(object issuer, string projectRoot, string repositoryRoot, string projectIdentityHash)
        {
            if (!ReferenceEquals(issuer, TestBindingIssuer))
                throw new InvalidOperationException("The local project binding issuer is not trusted.");
            if (!W24S6McpOperationEnvelopePolicy.IsCanonicalSha256(projectIdentityHash))
                throw new ArgumentException("A canonical registered project identity is required.", nameof(projectIdentityHash));

            var normalizedProject = NormalizeTestRoot(projectRoot, "projectRoot");
            var normalizedRepository = NormalizeTestRoot(repositoryRoot, "repositoryRoot");
            var finalSeparator = normalizedProject.LastIndexOf('\\');
            var projectParent = finalSeparator == 2 ? normalizedProject.Substring(0, 3) : normalizedProject.Substring(0, finalSeparator);
            if (!string.Equals(projectParent, normalizedRepository, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The synthetic project root must be a direct child of its repository root.", nameof(projectRoot));
            if (!string.Equals(normalizedProject.Substring(0, 2), normalizedRepository.Substring(0, 2), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The synthetic project and repository roots must use the same local DOS drive.", nameof(projectRoot));

            ProjectRoot = normalizedProject;
            RepositoryRoot = normalizedRepository;
            ProjectIdentityHash = projectIdentityHash;
        }

        /// <summary>
        /// Production remains closed before parsing a request, resolving a path, querying a drive,
        /// or opening a handle.  The no-input registration resolver currently has no issuer.  Even
        /// if a future implementation were accidentally to return a lease before the binding side
        /// is separately reviewed, this source-only scaffold disposes it and remains closed.
        /// </summary>
        internal static bool TryCreateProduction(out W24S6LocalProjectBinding binding, out string diagnosticCode)
        {
            binding = null;
            W24S6RegisteredProjectLease unavailableLease;
            var acquired = W24S6LocalProjectRegistration.TryAcquire(out unavailableLease, out diagnosticCode);
            if (unavailableLease != null) unavailableLease.Dispose();
            if (!acquired) return false;
            diagnosticCode = W24S6LocalProjectRegistration.PendingDiagnosticCode;
            return false;
        }

#if UNITY_INCLUDE_TESTS
        internal static W24S6LocalProjectBinding IssueForTests(string projectRoot, string repositoryRoot, string registeredProjectIdentityHash)
        {
            return new W24S6LocalProjectBinding(TestBindingIssuer, projectRoot, repositoryRoot, registeredProjectIdentityHash);
        }
#endif

        private static string NormalizeTestRoot(string value, string parameterName)
        {
            string normalized;
            if (!W24S6WindowsReadOnlyFile.TryNormalizeRegisteredRoot(value, out normalized))
                throw new ArgumentException("A canonical bounded local DOS drive root is required.", parameterName);
            return normalized;
        }
    }
}
