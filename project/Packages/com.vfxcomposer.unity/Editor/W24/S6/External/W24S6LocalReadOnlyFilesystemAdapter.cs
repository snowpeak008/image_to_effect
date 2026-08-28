using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.W24.S6.External
{
    internal sealed class W24S6LocalFilesystemDiagnostic
    {
        internal string Code { get; }
        internal string Field { get; }
        internal string Message { get; }

        internal W24S6LocalFilesystemDiagnostic(string code, string field, string message)
        {
            Code = code;
            Field = field;
            Message = message;
        }

        internal JObject ToJson() { return new JObject { ["code"] = Code, ["field"] = Field, ["message"] = Message }; }
    }

    internal sealed class W24S6LocalFilesystemOperationResult
    {
        private readonly W24S6LocalFilesystemDiagnostic[] diagnostics;
        internal string OperationId { get; }
        internal string OperationKind { get; }
        internal string TargetPath { get; }
        internal string InputSha256 { get; }
        internal long InputBytes { get; }
        internal string Classification { get; }
        internal IReadOnlyList<W24S6LocalFilesystemDiagnostic> Diagnostics { get { return Array.AsReadOnly(diagnostics); } }

        internal W24S6LocalFilesystemOperationResult(string operationId, string operationKind, string targetPath, string inputSha256,
            long inputBytes, string classification, IEnumerable<W24S6LocalFilesystemDiagnostic> diagnostics)
        {
            OperationId = operationId;
            OperationKind = operationKind;
            TargetPath = targetPath;
            InputSha256 = inputSha256;
            InputBytes = inputBytes;
            Classification = classification;
            this.diagnostics = (diagnostics ?? Enumerable.Empty<W24S6LocalFilesystemDiagnostic>()).ToArray();
        }

        internal JObject ToJson()
        {
            return new JObject
            {
                ["operationId"] = OperationId,
                ["operationKind"] = OperationKind,
                ["targetPath"] = TargetPath,
                ["inputSha256"] = InputSha256 == null ? JValue.CreateNull() : new JValue(InputSha256),
                ["inputBytes"] = InputBytes,
                ["classification"] = Classification,
                ["diagnostics"] = new JArray(diagnostics.Select(value => value.ToJson()))
            };
        }
    }

    internal sealed class W24S6LocalFilesystemInspectionResult
    {
        internal const string Schema = "w24-s6/local-filesystem-inspection-result-v1";
        internal const string Scope = "local-filesystem-document-inspection-only";
        private readonly W24S6LocalFilesystemOperationResult[] operations;
        private readonly W24S6LocalFilesystemDiagnostic[] diagnostics;

        internal string SchemaVersion { get { return Schema; } }
        internal string Authority { get { return "none"; } }
        internal bool MachineGatePassed { get { return false; } }
        internal string InspectionScope { get { return Scope; } }
        internal string RequestId { get; }
        internal string ProjectIdentityHash { get; }
        internal string PlanHash { get; }
        internal string Classification { get; }
        internal IReadOnlyList<W24S6LocalFilesystemOperationResult> Operations { get { return Array.AsReadOnly(operations); } }
        internal IReadOnlyList<W24S6LocalFilesystemDiagnostic> Diagnostics { get { return Array.AsReadOnly(diagnostics); } }

        internal W24S6LocalFilesystemInspectionResult(string requestId, string projectIdentityHash, string planHash, string classification,
            IEnumerable<W24S6LocalFilesystemOperationResult> operations, IEnumerable<W24S6LocalFilesystemDiagnostic> diagnostics)
        {
            RequestId = requestId;
            ProjectIdentityHash = projectIdentityHash;
            PlanHash = planHash;
            Classification = classification;
            this.operations = (operations ?? Enumerable.Empty<W24S6LocalFilesystemOperationResult>()).ToArray();
            this.diagnostics = (diagnostics ?? Enumerable.Empty<W24S6LocalFilesystemDiagnostic>()).ToArray();
        }

        internal string ToJson()
        {
            return new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["authority"] = Authority,
                ["machineGatePassed"] = MachineGatePassed,
                ["scope"] = InspectionScope,
                ["requestId"] = RequestId == null ? JValue.CreateNull() : new JValue(RequestId),
                ["projectIdentityHash"] = ProjectIdentityHash == null ? JValue.CreateNull() : new JValue(ProjectIdentityHash),
                ["planHash"] = PlanHash == null ? JValue.CreateNull() : new JValue(PlanHash),
                ["classification"] = Classification,
                ["operations"] = new JArray(operations.Select(value => value.ToJson())),
                ["diagnostics"] = new JArray(diagnostics.Select(value => value.ToJson()))
            }.ToString(Formatting.Indented);
        }
    }

    /// <summary>
    /// In-process read mechanics for a future local adapter.  This class is internal, has no
    /// transport, persists no result, invokes no Unity operation, and issues no authority.
    /// </summary>
    internal sealed class W24S6LocalReadOnlyFilesystemAdapter
    {
        private enum RootKind { Project, Repository }

        private sealed class FrozenOperation
        {
            internal string OperationId { get; }
            internal W24S6McpOperationKind Kind { get; }
            internal string TargetPath { get; }
            internal string ExpectedInputHash { get; }
            internal RootKind Root { get; }

            internal FrozenOperation(W24S6McpOperation value, RootKind root)
            {
                OperationId = value.OperationId;
                Kind = value.Kind;
                TargetPath = value.TargetPath;
                ExpectedInputHash = value.ExpectedInputHash;
                Root = root;
            }
        }

        private sealed class FrozenPlan
        {
            internal string RequestId { get; }
            internal string ProjectIdentityHash { get; }
            internal string PlanHash { get; }
            internal FrozenOperation[] Operations { get; }

            internal FrozenPlan(string requestId, string projectIdentityHash, string planHash, FrozenOperation[] operations)
            {
                RequestId = requestId;
                ProjectIdentityHash = projectIdentityHash;
                PlanHash = planHash;
                Operations = (FrozenOperation[])operations.Clone();
            }
        }

        private readonly W24S6LocalProjectBinding binding;
        private readonly W24S6LocalDocumentInspector inspector = new W24S6LocalDocumentInspector();
#if UNITY_INCLUDE_TESTS
        private int inspectorInvocationCount;
        internal int InspectorInvocationCountForTests { get { return inspectorInvocationCount; } }
#endif

        private W24S6LocalReadOnlyFilesystemAdapter(W24S6LocalProjectBinding binding)
        {
            this.binding = binding ?? throw new ArgumentNullException(nameof(binding));
        }

        internal static W24S6LocalFilesystemInspectionResult InspectProduction(string envelopeJson, string expectedPlanHash)
        {
            W24S6LocalProjectBinding unavailable;
            string diagnosticCode;
            if (!W24S6LocalProjectBinding.TryCreateProduction(out unavailable, out diagnosticCode))
                return Rejected(null, null, null, diagnosticCode, "/projectIdentityHash", "No frozen production project registration exists; no file was opened.");
            return new W24S6LocalReadOnlyFilesystemAdapter(unavailable).Inspect(envelopeJson, expectedPlanHash);
        }

#if UNITY_INCLUDE_TESTS
        internal static W24S6LocalReadOnlyFilesystemAdapter CreateForTests(string projectRoot, string repositoryRoot, string registeredProjectIdentityHash)
        {
            return new W24S6LocalReadOnlyFilesystemAdapter(W24S6LocalProjectBinding.IssueForTests(projectRoot, repositoryRoot, registeredProjectIdentityHash));
        }
#endif

        internal W24S6LocalFilesystemInspectionResult Inspect(string envelopeJson, string expectedPlanHash)
        {
            try { return InspectCore(envelopeJson, expectedPlanHash); }
            catch (Exception e) when (IsExpectedInspectionBoundaryFailure(e))
            {
                return Rejected(null, null, null, "W24FS116", "/", "The local read boundary rejected an expected malformed-input failure.");
            }
        }

        private W24S6LocalFilesystemInspectionResult InspectCore(string envelopeJson, string expectedPlanHash)
        {
            FrozenPlan plan;
            List<W24S6LocalFilesystemDiagnostic> validationDiagnostics;
            if (!TryFreezeAndValidate(envelopeJson, expectedPlanHash, out plan, out validationDiagnostics))
                return new W24S6LocalFilesystemInspectionResult(plan == null ? null : plan.RequestId,
                    plan == null ? null : plan.ProjectIdentityHash, plan == null ? null : plan.PlanHash,
                    "rejected", Array.Empty<W24S6LocalFilesystemOperationResult>(), validationDiagnostics);

            W24S6PinnedReadRoot projectRoot = null;
            W24S6PinnedReadRoot repositoryRoot = null;
            try
            {
                if (plan.Operations.Any(value => value.Root == RootKind.Project)) projectRoot = W24S6WindowsReadOnlyFile.PinRoot(binding.ProjectRoot);
                if (plan.Operations.Any(value => value.Root == RootKind.Repository)) repositoryRoot = W24S6WindowsReadOnlyFile.PinRoot(binding.RepositoryRoot);
            }
            catch (W24S6PinnedReadFailure failure)
            {
                if (repositoryRoot != null) repositoryRoot.Dispose();
                if (projectRoot != null) projectRoot.Dispose();
                return Rejected(plan.RequestId, plan.ProjectIdentityHash, plan.PlanHash, failure.Code, "/projectBinding", failure.Message);
            }

            try
            {
                var results = new List<W24S6LocalFilesystemOperationResult>(plan.Operations.Length);
                var anyRejected = false;
                foreach (var operation in plan.Operations)
                {
                    var root = operation.Root == RootKind.Project ? projectRoot : repositoryRoot;
                    W24S6PinnedReadBytes pinned;
                    try { pinned = W24S6WindowsReadOnlyFile.ReadExact(root, operation.TargetPath); }
                    catch (W24S6PinnedReadFailure failure)
                    {
                        anyRejected = true;
                        results.Add(new W24S6LocalFilesystemOperationResult(operation.OperationId, operation.Kind.ToString(), operation.TargetPath,
                            null, 0, "rejected", new[] { new W24S6LocalFilesystemDiagnostic(failure.Code, "/targetPath", failure.Message) }));
                        continue;
                    }

                    var observedHash = W24S6LocalDocumentInspector.Hash(pinned.Bytes);
                    W24S6LocalInspectionResult inspection;
                    try
                    {
#if UNITY_INCLUDE_TESTS
                        inspectorInvocationCount++;
#endif
                        inspection = inspector.Inspect(new W24S6LocalInspectionRequest(operation.Kind, operation.TargetPath,
                            operation.ExpectedInputHash, pinned.Bytes));
                    }
                    catch (Exception e) when (IsExpectedInspectionBoundaryFailure(e))
                    {
                        anyRejected = true;
                        results.Add(new W24S6LocalFilesystemOperationResult(operation.OperationId, operation.Kind.ToString(),
                            operation.TargetPath, observedHash, pinned.Bytes.LongLength, "rejected",
                            new[] { new W24S6LocalFilesystemDiagnostic("W24FS115", "/documentBytes",
                                "The in-memory document boundary rejected an expected malformed-token failure.") }));
                        continue;
                    }
                    if (inspection.Classification == "rejected") anyRejected = true;
                    results.Add(new W24S6LocalFilesystemOperationResult(operation.OperationId, operation.Kind.ToString(), operation.TargetPath,
                        observedHash, pinned.Bytes.LongLength, inspection.Classification,
                        inspection.Diagnostics.Select(value => new W24S6LocalFilesystemDiagnostic(value.Code, value.Field,
                            "The pinned document bytes did not satisfy the selected in-memory inspection."))));
                }
                return new W24S6LocalFilesystemInspectionResult(plan.RequestId, plan.ProjectIdentityHash, plan.PlanHash,
                    anyRejected ? "rejected" : "inspection-complete", results, Array.Empty<W24S6LocalFilesystemDiagnostic>());
            }
            finally
            {
                if (repositoryRoot != null) repositoryRoot.Dispose();
                if (projectRoot != null) projectRoot.Dispose();
            }
        }

        private bool TryFreezeAndValidate(string envelopeJson, string expectedPlanHash, out FrozenPlan plan,
            out List<W24S6LocalFilesystemDiagnostic> diagnostics)
        {
            plan = null;
            diagnostics = new List<W24S6LocalFilesystemDiagnostic>();
            W24S6McpOperationEnvelope parsed;
            try { parsed = W24S6McpOperationEnvelope.FromJson(envelopeJson); }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is InvalidOperationException || e is FormatException || e is OverflowException)
            {
                diagnostics.Add(new W24S6LocalFilesystemDiagnostic("W24FS002", "/", "The structural envelope JSON was rejected before any file open."));
                return false;
            }

            var copiedOperations = (parsed.Operations ?? Array.Empty<W24S6McpOperation>()).Select(value => value == null ? null : new W24S6McpOperation
            {
                OperationId = value.OperationId,
                Kind = value.Kind,
                TargetPath = value.TargetPath,
                ExpectedInputHash = value.ExpectedInputHash
            }).ToArray();
            var snapshot = new W24S6McpOperationEnvelope
            {
                SchemaVersion = parsed.SchemaVersion,
                RequestId = parsed.RequestId,
                ProjectIdentityHash = parsed.ProjectIdentityHash,
                ExecutionMode = parsed.ExecutionMode,
                RequestedAuthority = parsed.RequestedAuthority,
                RollbackMode = parsed.RollbackMode,
                ApprovalToken = parsed.ApprovalToken,
                Operations = copiedOperations,
                PlanHash = parsed.PlanHash
            };

            var policy = new W24S6McpOperationEnvelopePolicy(binding.ProjectIdentityHash);
            var policyResult = policy.Validate(snapshot, expectedPlanHash);
            diagnostics.AddRange(policyResult.Errors.Select(value => new W24S6LocalFilesystemDiagnostic(value.Code, value.Field, value.Message)));
            if (snapshot.RequestedAuthority != W24S6McpAuthority.ReadOnly)
                diagnostics.Add(new W24S6LocalFilesystemDiagnostic("W24FS003", "/requestedAuthority", "A real filesystem read requires the exact non-authoritative ReadOnly request value."));

            var roots = new RootKind[copiedOperations.Length];
            var duplicateTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < copiedOperations.Length; index++)
            {
                var operation = copiedOperations[index];
                if (operation == null) continue;
                RootKind root;
                var canonicalTarget = TryClassifyRoot(operation.TargetPath, out root) && IsCanonicalWindowsTarget(operation.TargetPath);
                if (!canonicalTarget)
                    diagnostics.Add(new W24S6LocalFilesystemDiagnostic("W24FS004", "/operations/" + index + "/targetPath", "The target is not a canonical Windows project-relative document path."));
                else if (!IsEffectiveDosPathBounded(root, operation.TargetPath))
                    diagnostics.Add(new W24S6LocalFilesystemDiagnostic("W24FS006", "/operations/" + index + "/targetPath", "The effective registered DOS path exceeds 259 characters."));
                roots[index] = root;
                var duplicateKey = ((int)operation.Kind).ToString() + "\n" + operation.TargetPath;
                if (!duplicateTargets.Add(duplicateKey))
                    diagnostics.Add(new W24S6LocalFilesystemDiagnostic("W24FS005", "/operations/" + index + "/targetPath", "Duplicate document targets are not allowed in one read plan."));
            }

            plan = new FrozenPlan(snapshot.RequestId, snapshot.ProjectIdentityHash, snapshot.PlanHash,
                copiedOperations.Select((value, index) => value == null ? null : new FrozenOperation(value, roots[index])).Where(value => value != null).ToArray());
            return diagnostics.Count == 0;
        }

        private static bool TryClassifyRoot(string targetPath, out RootKind root)
        {
            if (!string.IsNullOrEmpty(targetPath) && targetPath.StartsWith("docs/vfx-contracts/", StringComparison.Ordinal))
            { root = RootKind.Repository; return true; }
            if (!string.IsNullOrEmpty(targetPath) && (targetPath.StartsWith("Assets/VFX/Recipes/", StringComparison.Ordinal)
                || targetPath.StartsWith("ProjectSettings/VFXComposer/BuildManifests/", StringComparison.Ordinal)))
            { root = RootKind.Project; return true; }
            root = RootKind.Project;
            return false;
        }

        private static bool IsCanonicalWindowsTarget(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 260 || value.IndexOfAny(new[] { '\\', ':', '|', '*', '?', '"', '<', '>' }) >= 0
                || value.StartsWith("/", StringComparison.Ordinal) || value.Contains("//") || value.Any(char.IsControl)) return false;
            foreach (var segment in value.Split('/'))
            {
                if (segment.Length == 0 || segment.Length > 96 || segment == "." || segment == ".." || segment.EndsWith(".", StringComparison.Ordinal)
                    || segment.EndsWith(" ", StringComparison.Ordinal) || segment.Any(character => !IsPortablePathCharacter(character)) || IsReservedDeviceSegment(segment)) return false;
            }
            return true;
        }

        private static bool IsPortablePathCharacter(char value)
        {
            return (value >= 'a' && value <= 'z') || (value >= 'A' && value <= 'Z') || (value >= '0' && value <= '9')
                || value == '_' || value == '-' || value == '.';
        }

        private static bool IsReservedDeviceSegment(string segment)
        {
            var stem = segment.Split('.')[0].ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CLOCK$") return true;
            if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal))
                && stem[3] >= '1' && stem[3] <= '9') return true;
            return false;
        }

        private bool IsEffectiveDosPathBounded(RootKind root, string targetPath)
        {
            var declaredRoot = root == RootKind.Project ? binding.ProjectRoot : binding.RepositoryRoot;
            var separatorCharacters = declaredRoot.EndsWith("\\", StringComparison.Ordinal) ? 0 : 1;
            return declaredRoot.Length + separatorCharacters + targetPath.Length <= 259;
        }

        private static bool IsExpectedInspectionBoundaryFailure(Exception value)
        {
            return value is JsonException || value is FormatException || value is OverflowException
                || value is ArgumentException || value is InvalidCastException || value is InvalidOperationException;
        }

        private static W24S6LocalFilesystemInspectionResult Rejected(string requestId, string projectIdentityHash, string planHash,
            string code, string field, string message)
        {
            return new W24S6LocalFilesystemInspectionResult(requestId, projectIdentityHash, planHash, "rejected",
                Array.Empty<W24S6LocalFilesystemOperationResult>(), new[] { new W24S6LocalFilesystemDiagnostic(code, field, message) });
        }
    }
}
