using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.W24.Workflow;

namespace VFXComposer.Editor.W24.S1
{
    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class VfxImplementationTrace
    {
        [JsonProperty("traceVersion")] public string TraceVersion;
        [JsonProperty("traceStatus")] public string TraceStatus;
        [JsonProperty("effectId")] public string EffectId;
        [JsonProperty("contractRevision")] public int ContractRevision;
        [JsonProperty("contractHash")] public string ContractHash;
        [JsonProperty("buildHash")] public string BuildHash;
        [JsonProperty("captureProfileHash")] public string CaptureProfileHash;
        [JsonProperty("runtimeEntryAssetPath")] public string RuntimeEntryAssetPath;
        [JsonProperty("runtimeEntryGuid")] public string RuntimeEntryGuid;
        // Candidate and evidence revisions are separate: C0/C1/C2 remain immutable candidate
        // slots while a candidate's captured evidence is sealed beneath that same candidate.
        [JsonProperty("candidateRevision")] public int CandidateRevision;
        [JsonProperty("evidenceRevision")] public int EvidenceRevision;
        // Populated only by the S5-owned candidate evidence transition. These pin an
        // evidence-complete trace to its immutable candidate and graphics capture corpus.
        [JsonProperty("candidateReceiptPath")] public string CandidateReceiptPath;
        [JsonProperty("candidateReceiptFileHash")] public string CandidateReceiptFileHash;
        [JsonProperty("captureMetadataPath")] public string CaptureMetadataPath;
        [JsonProperty("captureMetadataFileHash")] public string CaptureMetadataFileHash;
        [JsonProperty("evidenceTransitionReceiptPath")] public string EvidenceTransitionReceiptPath;
        [JsonProperty("evidenceTransitionReceiptFileHash")] public string EvidenceTransitionReceiptFileHash;
        // Hash of the trace with the two self-referential receipt/hash fields removed. This lets
        // the immutable C1 receipt bind trace semantics without a hash cycle.
        [JsonProperty("completedTraceNormalizedSha256")] public string CompletedTraceNormalizedSha256;
        [JsonProperty("requirementTraces")] public VfxRequirementTrace[] RequirementTraces = Array.Empty<VfxRequirementTrace>();
    }

    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class VfxRequirementTrace
    {
        [JsonProperty("designRequirementId")] public string DesignRequirementId;
        [JsonProperty("evidenceAuthority")] public string EvidenceAuthority;
        [JsonProperty("objects")] public VfxTraceObject[] Objects = Array.Empty<VfxTraceObject>();
        [JsonProperty("stateIds")] public string[] StateIds = Array.Empty<string>();
        [JsonProperty("layerIds")] public string[] LayerIds = Array.Empty<string>();
        [JsonProperty("seeds")] public uint[] Seeds = Array.Empty<uint>();
        [JsonProperty("authorityEvidence")] public VfxTraceEvidence[] AuthorityEvidence = Array.Empty<VfxTraceEvidence>();
        [JsonProperty("crossEvidence")] public VfxTraceEvidence[] CrossEvidence = Array.Empty<VfxTraceEvidence>();
        // Controlled semantic facts are telemetry output, never unverified design assertions.
        [JsonProperty("semanticTokens")] public string[] SemanticTokens = Array.Empty<string>();
    }

    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class VfxTraceObject
    {
        [JsonProperty("assetPath")] public string AssetPath;
        [JsonProperty("hierarchyPath")] public string HierarchyPath;
        [JsonProperty("componentType")] public string ComponentType;
        [JsonProperty("componentInstanceId")] public string ComponentInstanceId;
        [JsonProperty("propertyPath")] public string PropertyPath;
        [JsonProperty("shaderName")] public string ShaderName;
    }

    [Serializable, JsonObject(MemberSerialization.OptIn)]
    public sealed class VfxTraceEvidence
    {
        [JsonProperty("kind")] public string Kind; // telemetry / diagnostic / visualQa / user
        [JsonProperty("reference")] public string Reference;
        [JsonProperty("sha256")] public string Sha256;
        [JsonProperty("passed")] public bool Passed;
        [JsonProperty("detail")] public string Detail;
        // Optional for legacy telemetry/Beauty cross evidence.  When a trace claims a typed
        // diagnostic or metrics result, S5 requires all four fields and binds them back to the
        // sealed capture metadata; a generic JSON summary therefore cannot impersonate a pass.
        [JsonProperty("passId")] public string PassId;
        [JsonProperty("encoding")] public string Encoding;
        [JsonProperty("metricCheckId")] public string MetricCheckId;
        [JsonProperty("analysisInputSha256")] public string AnalysisInputSha256;
    }

    public static class VfxImplementationTraceJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { MissingMemberHandling=MissingMemberHandling.Error, NullValueHandling=NullValueHandling.Ignore, Culture=CultureInfo.InvariantCulture };
        public static VfxImplementationTrace FromJson(string json)
        {
            if(string.IsNullOrWhiteSpace(json)) throw new JsonSerializationException("Implementation Trace JSON is required.");
            var root=JObject.Parse(json,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
            return root.ToObject<VfxImplementationTrace>(JsonSerializer.Create(Settings));
        }
        public static VfxTraceValidationResult ValidateJson(string json,VfxDesignContract contract,out VfxImplementationTrace trace)
        {
            trace=null;
            try { trace=FromJson(json); }
            catch(Exception e) when(e is JsonException||e is FormatException||e is OverflowException)
            {
                var failed=new VfxTraceValidationResult(); failed.Report.Error("W24TJ001","$",e.Message); return failed;
            }
            return VfxImplementationTraceValidator.Validate(contract,trace);
        }
    }

    public sealed class W24KnownCheatFinding
    {
        public string CheatId;
        public string RequirementId;
        public bool AuthorityFailed;
        public bool CrossEvidenceAlarmed;
        public string Detail;
    }

    public sealed class VfxTraceValidationResult
    {
        public W24GateReport Report = new W24GateReport();
        public IReadOnlyList<W24KnownCheatFinding> KnownCheatFindings => knownCheats;
        private readonly List<W24KnownCheatFinding> knownCheats = new List<W24KnownCheatFinding>();
        internal void AddCheat(W24KnownCheatFinding finding) { knownCheats.Add(finding); }
    }

    /// <summary>
    /// Formal bidirectional trace validator. Requirement coverage must be exact; a trace cannot
    /// silently add a requirement or omit one. This module reports semantic/structural facts and
    /// deliberately does not decide visual quality or L4.
    /// </summary>
    public static class VfxImplementationTraceValidator
    {
        public static VfxTraceValidationResult Validate(VfxDesignContract contract, VfxImplementationTrace trace)
        {
            var result = new VfxTraceValidationResult();
            var report = result.Report;
            var contractReport = VfxDesignContractValidator.Validate(contract);
            foreach (var issue in contractReport.Issues) report.Error(issue.Code, issue.Path, issue.Message);
            if (trace == null) { report.Error("W24T000", "$", "Implementation Trace is required."); return result; }
            Required(report, trace.TraceVersion, "traceVersion");
            if (contract == null) return result;
            if (!Same(trace.EffectId, contract.EffectId)) report.Error("W24T001", "effectId", "Trace effectId must equal contract effectId.");
            if (trace.ContractRevision != contract.ContractRevision) report.Error("W24T002", "contractRevision", "Trace must bind the exact contract revision.");
            if (!Same(trace.ContractHash, contract.ContractHash) || !W24Hash.IsCanonical(trace.ContractHash)) report.Error("W24T003", "contractHash", "Trace must bind exact canonical contract hash.");
            if (!W24Hash.IsCanonical(trace.BuildHash)) report.Error("W24T004", "buildHash", "Trace needs a canonical build hash.");
            if (!W24Hash.IsCanonical(trace.CaptureProfileHash)) report.Error("W24T004A", "captureProfileHash", "Trace needs the exact canonical capture profile hash.");
            if (!ProjectAssetPath(trace.RuntimeEntryAssetPath) || !Guid(trace.RuntimeEntryGuid)) report.Error("W24T005", "runtimeEntry", "Trace runtime entry must have an Assets path and lowercase GUID.");

            var requirements = new Dictionary<string, VfxDesignRequirement>(StringComparer.Ordinal);
            foreach (var requirement in contract.Requirements ?? Array.Empty<VfxDesignRequirement>())
                if (requirement != null && !string.IsNullOrEmpty(requirement.DesignRequirementId) && !requirements.ContainsKey(requirement.DesignRequirementId))
                    requirements.Add(requirement.DesignRequirementId, requirement);
            var traceByRequirement = new Dictionary<string, VfxRequirementTrace>(StringComparer.Ordinal);
            foreach (var pair in (trace.RequirementTraces ?? Array.Empty<VfxRequirementTrace>()).Select((value, index) => new { value, index }))
            {
                var item = pair.value; var path = "requirementTraces[" + pair.index + "]";
                if (item == null || string.IsNullOrEmpty(item.DesignRequirementId) || traceByRequirement.ContainsKey(item.DesignRequirementId))
                { report.Error("W24T010", path, "Trace requirement IDs must be non-empty and unique."); continue; }
                traceByRequirement.Add(item.DesignRequirementId, item);
                VfxDesignRequirement requirement;
                if (!requirements.TryGetValue(item.DesignRequirementId, out requirement))
                { report.Error("W24T011", path, "Trace refers to a requirement not in the contract."); continue; }
                ValidateRequirementTrace(report, contract, requirement, item, path);
                CheckKnownCheats(result, requirement, item);
            }
            foreach (var requirement in requirements.Values)
                if (!traceByRequirement.ContainsKey(requirement.DesignRequirementId)) report.Error("W24T012", "requirements." + requirement.DesignRequirementId, "Every contract requirement needs an implementation trace.");
            ValidateTypedDiagnosticAuthorities(report, contract, requirements, traceByRequirement);
            foreach (var finding in result.KnownCheatFindings)
            {
                if (!finding.AuthorityFailed) report.Error("W24T020", finding.RequirementId, finding.CheatId + " was not rejected by its authority evidence.");
                if (!finding.CrossEvidenceAlarmed) report.Error("W24T021", finding.RequirementId, finding.CheatId + " has no independent cross-evidence alarm.");
            }
            return result;
        }

        /// <summary>
        /// Returns the frozen metric kinds associated with each Contract requirement.  The
        /// mapping is intentionally derived from the signed typedDiagnostics block rather than
        /// from a producer-authored trace.  An empty map means that the Contract has no frozen
        /// typed evidence matrix and preserves S0a/S0b compatibility.
        /// </summary>
        internal static Dictionary<string, HashSet<string>> TypedDiagnosticRequirementKinds(VfxDesignContract contract)
        {
            var output = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var typed = contract == null || contract.Extensions == null ? null : contract.Extensions["typedDiagnostics"] as JObject;
            var matrix = typed == null ? null : typed["requiredEvidenceMatrix"] as JArray;
            if (typed == null || matrix == null || matrix.Count == 0) return output;
            foreach (var property in typed.Properties())
            {
                var block = property.Value as JObject;
                var plan = block == null ? null : block["metricPlan"] as JObject;
                var kind = plan == null ? null : (string)plan["kind"];
                if (!ProtocolToken(kind)) continue;
                var requirementIds = new List<string>();
                var single = (string)block["requirementId"];
                if (!string.IsNullOrEmpty(single)) requirementIds.Add(single);
                requirementIds.AddRange((block["requirementIds"] as JArray ?? new JArray()).Values<string>().Where(value => !string.IsNullOrEmpty(value)));
                foreach (var requirementId in requirementIds.Distinct(StringComparer.Ordinal))
                {
                    if (!output.TryGetValue(requirementId, out var kinds)) output.Add(requirementId, kinds = new HashSet<string>(StringComparer.Ordinal));
                    kinds.Add(kind);
                }
            }
            return output;
        }

        private static void ValidateTypedDiagnosticAuthorities(W24GateReport report, VfxDesignContract contract, Dictionary<string, VfxDesignRequirement> requirements, Dictionary<string, VfxRequirementTrace> traces)
        {
            var typed = contract.Extensions == null ? null : contract.Extensions["typedDiagnostics"] as JObject;
            var matrix = typed == null ? null : typed["requiredEvidenceMatrix"] as JArray;
            if (matrix == null || matrix.Count == 0) return;
            var bindings = TypedDiagnosticRequirementKinds(contract);
            foreach (var requirement in requirements.Values.Where(value => Same(value.EvidenceAuthority, "diagnostic")))
            {
                var path = "requirements." + requirement.DesignRequirementId + ".authorityEvidence";
                if (!bindings.TryGetValue(requirement.DesignRequirementId, out var kinds) || kinds.Count == 0)
                {
                    report.Error("W24T052", path, "A Contract with a typed evidence matrix must map every diagnostic authority requirement to a frozen metric plan.");
                    continue;
                }
                if (!traces.TryGetValue(requirement.DesignRequirementId, out var trace)) continue;
                var authority = Safe(trace.AuthorityEvidence).Where(value => value != null).ToArray();
                foreach (var evidence in authority)
                {
                    if (!Same(evidence.Kind, "diagnostic") || !Same(evidence.PassId, "metrics-report") || !Same(evidence.Encoding, "json") || !ProtocolToken(evidence.MetricCheckId) || !W24Hash.IsCanonical(evidence.AnalysisInputSha256))
                        report.Error("W24T053", path, "Typed diagnostic authority must bind passId=metrics-report, encoding=json, a frozen metricCheckId, and the canonical analysis-input hash; a generic diagnostic summary is cross/supplemental evidence only.");
                }
                var ids = authority.Select(value => value.MetricCheckId).Where(value => !string.IsNullOrEmpty(value)).ToArray();
                if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) report.Error("W24T054", path, "Typed diagnostic authority may not repeat a metric check ID.");
            }
            foreach (var tracePair in traces)
            {
                var diagnostic = Safe(tracePair.Value.AuthorityEvidence).Concat(Safe(tracePair.Value.CrossEvidence)).Where(value => value != null && value.Passed && Same(value.Kind, "diagnostic")).ToArray();
                if (diagnostic.Length == 0) continue;
                if (!bindings.TryGetValue(tracePair.Key, out var kinds) || kinds.Count == 0) report.Error("W24T055", "requirements." + tracePair.Key, "A passed diagnostic item is not allowed for a requirement with no Contract-frozen metric plan; generic summaries remain supplemental only.");
                foreach (var evidence in diagnostic)
                    if (!Same(evidence.PassId, "metrics-report") || !Same(evidence.Encoding, "json") || !ProtocolToken(evidence.MetricCheckId) || !W24Hash.IsCanonical(evidence.AnalysisInputSha256))
                        report.Error("W24T056", "requirements." + tracePair.Key, "Every passed diagnostic item, including cross-evidence, must bind a frozen metrics-report/json check and analysis-input hash when typed diagnostics is active.");
                var ids = diagnostic.Select(value => value.MetricCheckId).Where(value => !string.IsNullOrEmpty(value)).ToArray();
                if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) report.Error("W24T057", "requirements." + tracePair.Key, "A frozen metric check may be referenced only once per requirement.");
            }
        }

        private static void ValidateRequirementTrace(W24GateReport report, VfxDesignContract contract, VfxDesignRequirement requirement, VfxRequirementTrace trace, string path)
        {
            if (!Same(requirement.EvidenceAuthority, trace.EvidenceAuthority)) report.Error("W24T030", path + ".evidenceAuthority", "Trace authority must match its single contract authority.");
            if (!Any(trace.AuthorityEvidence)) report.Error("W24T031", path + ".authorityEvidence", "At least one authority evidence record is required.");
            foreach (var evidence in Safe(trace.AuthorityEvidence)) ValidateEvidence(report, evidence, path + ".authorityEvidence", requirement.EvidenceAuthority, true);
            if (!Any(trace.CrossEvidence)) report.Error("W24T032", path + ".crossEvidence", "At least one independent cross-evidence record is required.");
            foreach (var evidence in Safe(trace.CrossEvidence)) ValidateEvidence(report, evidence, path + ".crossEvidence", requirement.EvidenceAuthority, false);
            if (requirement.EvidenceAuthority == "telemetry")
            {
                if (!Any(trace.Objects)) report.Error("W24T033", path + ".objects", "Telemetry requirements need real object/component identities.");
                foreach (var obj in Safe(trace.Objects)) ValidateObject(report, obj, path + ".objects");
                if (requirement.Type == "behavioral" && !Any(trace.StateIds)) report.Error("W24T034", path + ".stateIds", "Behavioral trace needs semantic state references.");
                if (requirement.Type == "budget" && !trace.SemanticTokens.Contains("budget-readback")) report.Error("W24T035", path + ".semanticTokens", "Budget trace needs budget-readback telemetry.");
            }
            if ((requirement.Type == "visual-measurable" || requirement.Type == "visual-semantic") && !Any(trace.LayerIds)) report.Error("W24T036", path + ".layerIds", "Visual trace needs target layer identity.");
            if (contract.CaptureProfile != null && trace.Seeds != null && trace.Seeds.Length > 0)
            {
                var expected = new[] { contract.CaptureProfile.CanonicalSeed }.Concat(contract.CaptureProfile.RobustnessSeeds ?? Array.Empty<uint>()).ToArray();
                if (!expected.All(seed => trace.Seeds.Contains(seed))) report.Error("W24T037", path + ".seeds", "Trace needs canonical and both robustness seed evidence.");
            }
            foreach (var state in Safe(trace.StateIds)) if (!Safe(contract.SemanticStateMachine == null ? null : contract.SemanticStateMachine.States).Any(value => value != null && Same(value.StateId, state))) report.Error("W24T038", path + ".stateIds", "Trace state IDs must exist in the contract state machine.");
            foreach (var layer in Safe(trace.LayerIds)) if (!Safe(contract.Layers).Any(value => value != null && Same(value.LayerId, layer))) report.Error("W24T039", path + ".layerIds", "Trace layer IDs must exist in the contract layers.");
        }

        private static void ValidateEvidence(W24GateReport report, VfxTraceEvidence evidence, string path, string expectedAuthority, bool authority)
        {
            if (evidence == null || string.IsNullOrEmpty(evidence.Kind) || string.IsNullOrEmpty(evidence.Reference) || !W24Hash.IsCanonical(evidence.Sha256)) { report.Error("W24T040", path, "Evidence requires kind, immutable reference and canonical hash."); return; }
            if (authority && !Same(evidence.Kind, expectedAuthority)) report.Error("W24T041", path + ".kind", "Authority evidence kind must match contract authority.");
            if (!authority && Same(evidence.Kind, expectedAuthority)) report.Error("W24T042", path + ".kind", "Cross evidence must be independent from authority evidence.");
            var typed = !string.IsNullOrEmpty(evidence.PassId) || !string.IsNullOrEmpty(evidence.Encoding) || !string.IsNullOrEmpty(evidence.MetricCheckId) || !string.IsNullOrEmpty(evidence.AnalysisInputSha256);
            if (typed && !Same(evidence.Kind, "diagnostic")) report.Error("W24T043", path, "Typed pass/metric fields are valid only for diagnostic evidence.");
            if (!string.IsNullOrEmpty(evidence.PassId) && !ProtocolToken(evidence.PassId)) report.Error("W24T044", path + ".passId", "Diagnostic passId must be a protocol token.");
            if (!string.IsNullOrEmpty(evidence.Encoding) && !ProtocolToken(evidence.Encoding)) report.Error("W24T045", path + ".encoding", "Diagnostic encoding must be a protocol token.");
            if (!string.IsNullOrEmpty(evidence.MetricCheckId) && !ProtocolToken(evidence.MetricCheckId)) report.Error("W24T046", path + ".metricCheckId", "Metric check ID must be a protocol token.");
            if (!string.IsNullOrEmpty(evidence.AnalysisInputSha256) && !W24Hash.IsCanonical(evidence.AnalysisInputSha256)) report.Error("W24T047", path + ".analysisInputSha256", "Analysis-input hash must be canonical SHA-256.");
            if (!string.IsNullOrEmpty(evidence.MetricCheckId) && (!Same(evidence.PassId, "metrics-report") || !Same(evidence.Encoding, "json") || !W24Hash.IsCanonical(evidence.AnalysisInputSha256))) report.Error("W24T048", path, "Metric evidence requires passId=metrics-report, encoding=json, and its analysis-input SHA-256.");
            if (Same(evidence.PassId, "metrics-report") && string.IsNullOrEmpty(evidence.MetricCheckId)) report.Error("W24T049", path + ".metricCheckId", "Metrics-report evidence must name its measured check.");
        }

        private static bool ProtocolToken(string value) { return !string.IsNullOrEmpty(value) && value.All(character => char.IsLower(character) || char.IsDigit(character) || character == '-' || character == '_' || character == '.'); }

        private static void ValidateObject(W24GateReport report, VfxTraceObject obj, string path)
        {
            if (obj == null || !ProjectAssetPath(obj.AssetPath) || string.IsNullOrEmpty(obj.HierarchyPath) || string.IsNullOrEmpty(obj.ComponentType) || string.IsNullOrEmpty(obj.ComponentInstanceId)) report.Error("W24T050", path, "Trace object requires Asset path, hierarchy, component type and instance identity.");
            else if (obj.HierarchyPath.Contains("..")) report.Error("W24T051", path + ".hierarchyPath", "Hierarchy paths may not escape with '..'.");
        }

        private static void CheckKnownCheats(VfxTraceValidationResult result, VfxDesignRequirement requirement, VfxRequirementTrace trace)
        {
            var tokens = new HashSet<string>(Safe(trace.SemanticTokens), StringComparer.OrdinalIgnoreCase);
            var text = requirement.Statement ?? string.Empty;
            if (Contains(text, "fragment") || Contains(text, "碎片"))
            {
                var fake = tokens.Contains("whole_group_rotation") || tokens.Contains("whole-image-rotation") || Safe(trace.Objects).Select(x => x == null ? null : x.ComponentInstanceId).Where(x => x != null).Distinct().Count() < 2;
                if (fake) result.AddCheat(Finding("whole-image-rotation-fake-fragments", requirement, trace, "Fragment motion must have independent instances rather than a shared parent rotation."));
            }
            if (Contains(text, "trail") || Contains(text, "拖尾"))
            {
                var fake = tokens.Contains("static_trail") || tokens.Contains("fake_trail") || !tokens.Contains("emitter_position_history") || !tokens.Contains("trail_vertices_from_motion");
                if (fake) result.AddCheat(Finding("static-fake-trail", requirement, trace, "Trail must derive vertices from emitter motion history."));
            }
            if (Contains(text, "light") || Contains(text, "光照") || Contains(text, "illumination"))
            {
                var hasRealLight = Safe(trace.Objects).Any(obj => obj != null && (Same(obj.ComponentType, "Light") || Same(obj.ComponentType, "Light2D")));
                // REQ-LIGHT-RECEIVER has diagnostic authority, so an independent cross record
                // cannot also be diagnostic (W24T042).  Keep the receiver A/B criterion strict
                // while accepting any *independent* evidence kind that carries that measurement.
                var hasIndependentReceiverProbe = Safe(trace.CrossEvidence).Any(e => e != null && !Same(e.Kind, requirement.EvidenceAuthority) && Contains(e.Detail, "receiver-linear-luminance"));
                var fake = tokens.Contains("additive_fake_light") || !hasRealLight || !hasIndependentReceiverProbe;
                if (fake) result.AddCheat(Finding("additive-fake-light", requirement, trace, "Real light needs Light/Light2D telemetry and an independent receiver linear-luminance A/B record."));
            }
        }

        private static W24KnownCheatFinding Finding(string cheat, VfxDesignRequirement requirement, VfxRequirementTrace trace, string detail)
        {
            // A known cheat must fail the requirement's authoritative record and at least one independent record must alarm.
            return new W24KnownCheatFinding
            {
                CheatId = cheat,
                RequirementId = requirement.DesignRequirementId,
                AuthorityFailed = Safe(trace.AuthorityEvidence).Any(e => e != null && !e.Passed && Same(e.Kind, requirement.EvidenceAuthority)),
                CrossEvidenceAlarmed = Safe(trace.CrossEvidence).Any(e => e != null && !e.Passed && !Same(e.Kind, requirement.EvidenceAuthority)),
                Detail = detail
            };
        }

        private static bool ProjectAssetPath(string path) { return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Contains(".."); }
        private static bool Guid(string value) { return !string.IsNullOrEmpty(value) && value.Length == 32 && value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')); }
        private static bool Contains(string source, string needle) { return source != null && source.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0; }
        private static bool Same(string a, string b) { return string.Equals(a, b, StringComparison.Ordinal); }
        private static bool Any<T>(IEnumerable<T> values) { return values != null && values.Any(); }
        private static IEnumerable<T> Safe<T>(IEnumerable<T> values) { return values ?? Enumerable.Empty<T>(); }
        private static void Required(W24GateReport report, string value, string path) { if (string.IsNullOrEmpty(value)) report.Error("W24T060", path, "Field is required."); }
    }

    [Serializable]
    public sealed class W24UserSignature
    {
        public string UserIdentity;
        public int ContractRevision;
        public string BuildHash;
        public string CaptureProfileHash;
        public string VerdictCorpusReference;
    }

    /// <summary>
    /// Permission policy. Until the host supplies an opaque user-signoff authority, a mutable
    /// W24UserSignature data object is never sufficient to grant L4.
    /// </summary>
    public static class W24MaturityPolicy
    {
        public static bool CanMarkL3(W24S0aTerminalStatus? s0a, bool machineGatesPassed, bool visualQaPass)
        { return s0a == W24S0aTerminalStatus.S0A_GATE_QUALIFIED && machineGatesPassed && visualQaPass; }

        public static bool CanMarkL4(VfxDesignContract contract, VfxImplementationTrace trace, W24UserSignature signature)
        {
            // A caller-constructible DTO cannot establish that a user signed anything.  S5's
            // future host-owned opaque authority is the only allowed promotion route.
            return false;
        }
        public static W24MaturityLevel HighestWithoutUserSignature(bool machineGatesPassed, bool visualQaPass, W24S0aTerminalStatus? s0a)
        { return CanMarkL3(s0a, machineGatesPassed, visualQaPass) ? W24MaturityLevel.L3_ProductionCandidate : W24MaturityLevel.L2_VisualPlaceholder; }
        private static bool Same(string a,string b){return string.Equals(a,b,StringComparison.Ordinal);}
    }
}
