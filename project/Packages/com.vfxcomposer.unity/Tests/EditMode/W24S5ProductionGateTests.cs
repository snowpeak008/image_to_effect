using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S5ProductionGateTests
    {
        private const string TracePath = "docs/w24-s5-test-artifacts/trace.json";
        private const string PreContractPath = "docs/w24-s5-test-artifacts/precontract.json";
        private const string PreTracePath = "docs/w24-s5-test-artifacts/pretrace.json";
        private const string EffectId = "sustained_flame_3d";
        private const string RuntimeEntry = "Assets/VFX/test.prefab";
        private const string PreEffectId = "w24_s5_pre_probe";
        private const string PreRuntimeEntry = "Assets/VFX/Generated/w24_s5_pre_probe/VFX_w24_s5_pre_probe.prefab";
        private const string PreOutputRoot = "Assets/VFX/Generated/w24_s5_pre_probe";

        [SetUp] public void SetUp() { WriteTrace(ValidTrace()); WriteFirstFormalArtifacts(); }
        [TearDown] public void TearDown() { var directory = Path.GetDirectoryName(Absolute(TracePath)); if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void MissingPersistedContract_BlocksFormalDevelopmentBuild()
        {
            var request = ValidRequest(); request.ContractPath = "docs/vfx-contracts/missing.contract.json";
            var result = W24S5ProductionGate.Evaluate(request);
            Assert.That(result.CanBuild, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-010" && issue.IsError), Is.True);
        }

        [Test]
        public void RawOrMismatchedTraceBytes_CannotReplacePersistedAuthority()
        {
            var request = ValidRequest(); request.TraceFileHash = Hash("forged bytes");
            var result = W24S5ProductionGate.Evaluate(request);
            Assert.That(result.CanBuild, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-020" && issue.IsError), Is.True);
        }

        [Test]
        public void PersistedDocsResolveFromRepositoryRootAndRejectTraversal()
        {
            var accepted = W24S5ProductionGate.Evaluate(ValidRequest());
            Assert.That(accepted.CanBuild, Is.False, "A syntactically valid trace without the immutable C0/evidence/capture chain must not enter the formal gate.");
            Assert.That(accepted.Issues.Any(issue => issue.Code == "W24S5-C200" && issue.IsError), Is.True, Describe(accepted));

            var traversal = ValidRequest();
            traversal.ContractPath = "docs/vfx-contracts/../vfx-contracts/sustained_flame_3d.contract.json";
            var blocked = W24S5ProductionGate.Evaluate(traversal);
            Assert.That(blocked.CanBuild, Is.False);
            Assert.That(blocked.Issues.Any(issue => issue.Code == "W24S5-010" && issue.IsError), Is.True, Describe(blocked));
        }

        [Test]
        public void VisualPending_StillRequiresTheImmutableC0EvidenceChain()
        {
            var development = W24S5ProductionGate.Evaluate(ValidRequest());
            Assert.That(development.CanBuild, Is.False, Describe(development));
            Assert.That(development.Issues.Any(issue => issue.Code == "W24S5-C200" && issue.IsError), Is.True, Describe(development));
            Assert.That(development.CanPublish, Is.False);
            Assert.That(development.EffectiveStatus, Is.EqualTo(W24S5VisualStatus.VISUAL_PENDING));

            var publication = ValidRequest(); publication.Intent = W24S5BuildIntent.Publication;
            var blocked = W24S5ProductionGate.Evaluate(publication);
            Assert.That(blocked.CanBuild, Is.False);
            Assert.That(blocked.Issues.Any(issue => issue.Code == "W24S5-062" && issue.IsError), Is.True);
        }

        [Test]
        public void PublicRequestCannotForgeL4()
        {
            var request = ValidRequest(); request.VisualStatus = W24S5VisualStatus.L4; request.Intent = W24S5BuildIntent.Publication;
            var result = W24S5ProductionGate.Evaluate(request);
            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-070" || issue.Code == "W24S5-073"), Is.True, Describe(result));
        }

        [Test]
        public void PublicRequestCannotForgeL3OrSubstituteAdvisoryStringsForQaAuthority()
        {
            var request = ValidRequest();
            request.VisualStatus = W24S5VisualStatus.L3;
            request.VisualQaRecordPath = "docs/vfx-qa/advisory.json";
            request.VisualQaRecordHash = Hash("caller supplied advisory record");
            request.S0aStatusRecordPath = "docs/vfx-calibration/advisory.json";
            request.S0aStatusRecordHash = Hash("S0A_ADVISORY_ONLY");

            var result = W24S5ProductionGate.Evaluate(request);
            Assert.That(result.CanBuild, Is.False, Describe(result));
            Assert.That(result.Issues.Any(issue => (issue.Code == "W24S5-080" || issue.Code == "W24S5-083" || issue.Code == "W24S5-085") && issue.IsError), Is.True, Describe(result));
            Assert.That(result.EffectiveStatus, Is.EqualTo(W24S5VisualStatus.VISUAL_PENDING));
        }

        [Test]
        public void PublicRequestsFailClosedForL3AndL4UntilOpaqueIssuersExist()
        {
            var l3 = ValidRequest();
            l3.VisualStatus = W24S5VisualStatus.L3;
            l3.VisualQaRecordPath = "docs/vfx-qa/claimed-pass.json";
            l3.VisualQaRecordHash = Hash("claimed qa pass");
            l3.S0aStatusRecordPath = "docs/vfx-calibration/claimed-qualified.json";
            l3.S0aStatusRecordHash = Hash("claimed qualified calibration");
            var l3Result = W24S5ProductionGate.Evaluate(l3);
            Assert.That(l3Result.CanBuild, Is.False);
            Assert.That(l3Result.Issues.Any(issue => issue.Code == "W24S5-086" && issue.IsError), Is.True, Describe(l3Result));

            var l4 = ValidRequest();
            l4.VisualStatus = W24S5VisualStatus.L4;
            l4.Intent = W24S5BuildIntent.Publication;
            l4.UserVerdictRecordPath = "docs/vfx-verdicts/claimed-user-signoff.json";
            l4.UserVerdictRecordHash = Hash("claimed user signoff");
            var l4Result = W24S5ProductionGate.Evaluate(l4);
            Assert.That(l4Result.CanBuild, Is.False);
            Assert.That(l4Result.CanPublish, Is.False);
            Assert.That(l4Result.Issues.Any(issue => issue.Code == "W24S5-076" && issue.IsError), Is.True, Describe(l4Result));
        }

        [Test]
        public void CandidateEvidenceSealDoesNotConsumeCandidateC1()
        {
            Assert.That(W24S5EvidenceTransition.EvidenceDirectory, Is.EqualTo("evidence"));
            Assert.That(W24S5EvidenceTransition.C0 + "/" + W24S5EvidenceTransition.EvidenceDirectory, Is.EqualTo("C0/evidence"));
            Assert.That(W24S5EvidenceTransition.C0CandidateRevision, Is.EqualTo(0));
            Assert.That(W24S5EvidenceTransition.FirstEvidenceRevision, Is.EqualTo(1));
        }

        [Test]
        public void MetricsToolBundle_RejectsArbitrarySelfHashedScript()
        {
            const string fakeRelative = "tools/vfx/tests/w24-arbitrary-metrics.py";
            const string bundleRelative = "docs/vfx-contracts/capture-tools/w24-arbitrary.bundle.json";
            var fakeAbsolute = Path.Combine(RepositoryRoot, fakeRelative.Replace('/', Path.DirectorySeparatorChar));
            var bundleAbsolute = Path.Combine(RepositoryRoot, bundleRelative.Replace('/', Path.DirectorySeparatorChar));
            var realRelative = "tools/vfx/metrics/render_metrics.py"; var realAbsolute = Path.Combine(RepositoryRoot, realRelative.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                File.WriteAllText(fakeAbsolute, "print('self-hashed impostor')\n", new UTF8Encoding(false));
                var fakeHash = HashFileAbsolute(fakeAbsolute); var realHash = HashFileAbsolute(realAbsolute);
                var bundle = new JObject
                {
                    ["bundleVersion"] = "w24-capture-tool-bundle/1", ["toolVersion"] = "negative-test",
                    ["sources"] = new JArray(
                        new JObject { ["path"] = realRelative, ["sha256"] = realHash },
                        new JObject { ["path"] = fakeRelative, ["sha256"] = fakeHash })
                };
                Directory.CreateDirectory(Path.GetDirectoryName(bundleAbsolute)); File.WriteAllText(bundleAbsolute, bundle.ToString(Formatting.Indented), new UTF8Encoding(false));
                var bundleHash = Hash(CanonicalForTest(bundle));
                Assert.Throws<InvalidDataException>(() => W24MetricsEvidenceDag.VerifyToolBundleForTests(bundleRelative, bundleHash, fakeHash, fakeAbsolute), "An arbitrary script plus a self-consistent bundle/hash must not replace the uniquely named frozen metrics source.");
            }
            finally { if (File.Exists(fakeAbsolute)) File.Delete(fakeAbsolute); if (File.Exists(bundleAbsolute)) File.Delete(bundleAbsolute); }
        }

        [Test]
        public void SelfDeclaredLegacyDoesNotBypassFormalGate()
        {
            var request = ValidRequest(); request.VisualStatus = W24S5VisualStatus.LEGACY;
            var result = W24S5ProductionGate.Evaluate(request);
            Assert.That(result.CanBuild, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-004" && issue.IsError), Is.True);
        }

        [Test]
        public void LegacyCompatibilityRequiresEveryOwnedFileHashAndMetaGuid()
        {
            const string effectId = "w24_s5_legacy_probe";
            const string runtime = "Assets/VFX/Generated/w24_s5_legacy_probe/VFX_w24_s5_legacy_probe.prefab";
            const string guid = "0123456789abcdef0123456789abcdef";
            var configPath = ProjectAbsolute("ProjectSettings/VFXComposer/VfxProjectRules.json");
            var manifestPath = ProjectAbsolute(W24S5ProductionGate.ManifestRoot + effectId + ".manifest.json");
            var runtimePath = ProjectAbsolute(runtime);
            var outputFolder = Path.GetDirectoryName(runtimePath);
            var originalConfig = File.ReadAllText(configPath);
            try
            {
                var config = JObject.Parse(originalConfig); ((JArray)config["legacyEffectIds"]).Add(effectId); File.WriteAllText(configPath, config.ToString(Formatting.Indented)); VFXComposer.Editor.Rules.VfxProjectRules.ReloadForTests();
                Directory.CreateDirectory(outputFolder); File.WriteAllText(runtimePath, "legacy-probe"); File.WriteAllText(runtimePath + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
                Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
                var manifest = new JObject
                {
                    ["manifestVersion"] = 1, ["effectId"] = effectId, ["enforcement"] = "legacy_audit",
                    ["runtimeEntry"] = new JObject { ["kind"] = "prefab", ["path"] = runtime, ["guid"] = guid },
                    ["ownedOutputs"] = new JArray { new JObject { ["path"] = runtime, ["guid"] = guid, ["assetType"] = "GameObject", ["sha256"] = RawFileHash(runtime) } }
                };
                File.WriteAllText(manifestPath, manifest.ToString(Formatting.None));
                var request = new W24S5ProductionGateRequest { EffectId = effectId, ExpectedRuntimeEntryPath = runtime, ExpectedManifestPath = W24S5ProductionGate.ManifestRoot + effectId + ".manifest.json", Intent = W24S5BuildIntent.Development };
                Assert.That(W24S5ProductionGate.Evaluate(request).CanBuild, Is.True);
                File.AppendAllText(runtimePath, "tampered");
                var blocked = W24S5ProductionGate.Evaluate(request);
                Assert.That(blocked.CanBuild, Is.False);
                Assert.That(blocked.Issues.Any(issue => issue.Code == "W24S5-005" && issue.IsError), Is.True, Describe(blocked));
            }
            finally
            {
                if (File.Exists(manifestPath)) File.Delete(manifestPath);
                if (Directory.Exists(outputFolder)) Directory.Delete(outputFolder, true);
                File.WriteAllText(configPath, originalConfig); VFXComposer.Editor.Rules.VfxProjectRules.ReloadForTests();
            }
        }

        [Test]
        public void TraceMustBindExactGatedBuildAndRuntimeEntry()
        {
            var trace = JObject.Parse(File.ReadAllText(Absolute(TracePath))); trace["buildHash"] = Hash("other"); WriteTrace(trace.ToString(Formatting.None));
            var result = W24S5ProductionGate.Evaluate(ValidRequest());
            Assert.That(result.CanBuild, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-027" && issue.IsError), Is.True, Describe(result));
        }

        [Test]
        public void OrdinaryFormalGateRejectsAnyNonFinalTraceStatus()
        {
            foreach (var status in new[] { null, "PENDING_FIRST_FORMAL_BUILD_BINDING", "C0_CAPTURE_PENDING" })
            {
                var trace = JObject.Parse(File.ReadAllText(Absolute(TracePath))); trace["traceStatus"] = status; WriteTrace(trace.ToString(Formatting.None));
                var result = W24S5ProductionGate.Evaluate(ValidRequest());
                Assert.That(result.CanBuild, Is.False, "status=" + (status ?? "<null>") + " " + Describe(result));
                Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-029" && issue.IsError), Is.True, "status=" + (status ?? "<null>") + " " + Describe(result));
                WriteTrace(ValidTrace());
            }
        }

        [Test]
        public void CallerCreatedPlanCannotSubstituteAnApprovedProductionPlan()
        {
            var result = new VfxCompiler().BuildProduction(new VfxBuildPlan(), "not a recipe");
            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void FormalManifestModelCarriesAllNonVisualBindings()
        {
            var binding = new VFXComposer.Editor.Rules.VfxFormalProductionBinding { ContractPath = "docs/contracts/a.json", ContractFileHash = Hash("contract-file"), ContractHash = Hash("contract"), ContractRevision = 2, TracePath = "docs/traces/a.json", TraceFileHash = Hash("trace"), VisualStatus = "VISUAL_PENDING" };
            var manifest = new VFXComposer.Editor.Rules.VfxOutputManifest { FormalProduction = binding, RuntimeEntry = new VFXComposer.Editor.Rules.VfxRuntimeEntryRecord { Path = RuntimeEntry }, OwnedOutputs = new System.Collections.Generic.List<VFXComposer.Editor.Rules.VfxOwnedOutputRecord> { new VFXComposer.Editor.Rules.VfxOwnedOutputRecord { Path = RuntimeEntry } } };
            var root = JObject.Parse(JsonConvert.SerializeObject(manifest, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));
            Assert.That((string)root.SelectToken("formalProduction.contractPath"), Is.EqualTo(binding.ContractPath));
            Assert.That((string)root.SelectToken("formalProduction.traceFileHash"), Is.EqualTo(binding.TraceFileHash));
            Assert.That((string)root.SelectToken("formalProduction.visualStatus"), Is.EqualTo("VISUAL_PENDING"));
            Assert.That((string)root.SelectToken("runtimeEntry.path"), Is.EqualTo(RuntimeEntry));
            Assert.That((string)root.SelectToken("ownedOutputs[0].path"), Is.EqualTo(RuntimeEntry));
        }

        [Test]
        public void ExactBootstrapBinding_AcceptsExplicitJsonNullEvidence_AndRejectsAnyEvidenceOrOmission()
        {
            var binding = new JObject
            {
                ["admissionPhase"] = "PRE_C0_FIRST_FORMAL_BUILD",
                ["contractPath"] = "docs/contracts/a.json",
                ["contractFileHash"] = Hash("contract-file"),
                ["contractHash"] = Hash("contract"),
                ["contractRevision"] = 2,
                ["tracePath"] = "docs/traces/a.json",
                ["traceFileHash"] = Hash("trace"),
                ["visualStatus"] = "VISUAL_PENDING",
                ["evidenceCorpusPath"] = JValue.CreateNull(),
                ["evidenceCorpusHash"] = JValue.CreateNull(),
                ["userVerdictRecordPath"] = JValue.CreateNull(),
                ["userVerdictRecordHash"] = JValue.CreateNull(),
                ["visualQaRecordPath"] = JValue.CreateNull(),
                ["visualQaRecordHash"] = JValue.CreateNull(),
                ["s0aStatusRecordPath"] = JValue.CreateNull(),
                ["s0aStatusRecordHash"] = JValue.CreateNull()
            };

            Assert.That(W24S5ProductionGate.HasExactEvidenceFreeBootstrapBinding(binding, "docs/contracts/a.json", Hash("contract-file"), Hash("contract"), 2, "docs/traces/a.json", Hash("trace")), Is.True);

            binding["visualQaRecordPath"] = "docs/vfx-qa/forged.json";
            Assert.That(W24S5ProductionGate.HasExactEvidenceFreeBootstrapBinding(binding, "docs/contracts/a.json", Hash("contract-file"), Hash("contract"), 2, "docs/traces/a.json", Hash("trace")), Is.False);

            binding["visualQaRecordPath"] = JValue.CreateNull();
            binding.Remove("s0aStatusRecordHash");
            Assert.That(W24S5ProductionGate.HasExactEvidenceFreeBootstrapBinding(binding, "docs/contracts/a.json", Hash("contract-file"), Hash("contract"), 2, "docs/traces/a.json", Hash("trace")), Is.False);
        }

        [Test]
        public void PublicManifestWriter_HasNoFormalBindingParameter()
        {
            var method = typeof(VFXComposer.Editor.Rules.VfxProductionRules).GetMethods().Single(item => item.Name == "EnforceAndWriteManifest");
            Assert.That(method.GetParameters().Any(item => item.ParameterType == typeof(VFXComposer.Editor.Rules.VfxFormalProductionBinding) || item.Name == "formalProduction"), Is.False, "Public production writer must have no caller-supplied formal authority parameter.");
        }

        [Test]
        public void PublicManifestSurfaceCannotRestoreOrOverwriteAW24ProtectedEffect()
        {
            Assert.That(W24S5ProductionGate.IsW24ProtectedEffect(EffectId), Is.True, "A persisted W24 contract must protect the effect before any normal compiler write.");
            Assert.That(typeof(VFXComposer.Editor.Rules.VfxProductionRules).GetMethod("RestoreManifest", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static), Is.Null, "Manifest restore is a transaction-only internal primitive, not a public bypass writer.");
            var rejected = VFXComposer.Editor.Rules.VfxProductionRules.EnforceAndWriteManifest(EffectId, "projectile", 1, 1, Hash("recipe"), Hash("build"), "test", RuntimeEntry, "Assets/VFX/Generated/sustained_flame_3d", 1.0);
            Assert.That(rejected.Report.Entries.Any(entry => entry.Code == "E24S5-090" && entry.Severity == ValidationSeverity.Error), Is.True);
        }

        [Test]
        public void ProtectedEffectDiscoveryFailsClosedWhenAContractCannotBeRead()
        {
            const string effectId = "w24_s5_locked_contract_probe";
            var contractPath = Absolute("docs/vfx-contracts/" + effectId + ".contract.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(contractPath));
                File.WriteAllText(contractPath, "{\"effectId\":\"" + effectId + "\",\"contractVersion\":\"w24-contract/1\"}");
                using (new FileStream(contractPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.That(W24S5ProductionGate.IsW24ProtectedEffect(effectId), Is.True,
                        "An unreadable W24 contract must block public compiler/writer entry points.");
                }
            }
            finally
            {
                if (File.Exists(contractPath)) File.Delete(contractPath);
            }
        }

        [Test]
        public void FormalManifestRollbackRestoresThePriorManifestBytes()
        {
            const string effectId = "w24_s5_rollback_probe";
            var original = VFXComposer.Editor.Rules.VfxProductionRules.CaptureManifest(effectId);
            try
            {
                var prior = "{\"effectId\":\"w24_s5_rollback_probe\",\"formalProduction\":{\"visualStatus\":\"VISUAL_PENDING\"}}";
                VFXComposer.Editor.Rules.VfxProductionRules.RestoreManifest(effectId, prior);
                var snapshot = VFXComposer.Editor.Rules.VfxProductionRules.CaptureManifest(effectId);
                VFXComposer.Editor.Rules.VfxProductionRules.RestoreManifest(effectId, "{\"effectId\":\"replacement\"}");
                VFXComposer.Editor.Rules.VfxProductionRules.RestoreManifest(effectId, snapshot);
                Assert.That(VFXComposer.Editor.Rules.VfxProductionRules.CaptureManifest(effectId), Is.EqualTo(prior));
            }
            finally { VFXComposer.Editor.Rules.VfxProductionRules.RestoreManifest(effectId, original); }
        }

        [Test]
        public void RollbackRecordsNonIoFailureWithoutReplacingTheOriginalTransactionError()
        {
            var snapshot = new W24S5OwnedOutputSnapshot(false, new System.Collections.Generic.Dictionary<string, byte[]> { ["../escaped.asset"] = new byte[] { 1 } });
            var audit = new VFXComposer.Editor.Rules.VfxOutputAuditResult();
            audit.Report.Add("E24S5PRE047", ValidationSeverity.Error, "/formalProduction", "original transaction error");
            var append = typeof(W24S5ProductionGate).GetMethod("AppendRollbackFailure", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

            Assert.That(append, Is.Not.Null);
            Assert.DoesNotThrow(() => append.Invoke(null, new object[] { audit, PreEffectId, PreOutputRoot, snapshot, null }));
            Assert.That(audit.Report.Entries.Any(entry => entry.Code == "E24S5PRE047"), Is.True, "Rollback diagnostics must not replace the original transaction failure.");
            var rollback = audit.Report.Entries.Single(entry => entry.Code == "E24S5PRE048");
            StringAssert.Contains("InvalidDataException", rollback.Message);
        }

        [Test]
        public void FirstFormalBuild_AcceptsOnlyTheStrictPendingPreregistration()
        {
            var result = W24S5ProductionGate.EvaluateFirstFormalBuild(ValidFirstFormalRequest());
            Assert.That(result.CanBuild, Is.True, Describe(result));
            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.EffectiveStatus, Is.EqualTo(W24S5VisualStatus.VISUAL_PENDING));
            Assert.That(result.FirstFormalApproval, Is.Not.Null);
        }

        [Test]
        public void FirstFormalBuild_RejectsPublicationL3AndNonPendingTraceIdentity()
        {
            var publication = ValidFirstFormalRequest(); publication.Intent = W24S5BuildIntent.Publication;
            var publicationResult = W24S5ProductionGate.EvaluateFirstFormalBuild(publication);
            Assert.That(publicationResult.CanBuild, Is.False);
            Assert.That(publicationResult.Issues.Any(issue => issue.Code == "W24S5-PRE002" && issue.IsError), Is.True, Describe(publicationResult));

            foreach (var visualStatus in new[] { W24S5VisualStatus.L3, W24S5VisualStatus.L4 })
            {
                var nonPending = ValidFirstFormalRequest(); nonPending.VisualStatus = visualStatus;
                var nonPendingResult = W24S5ProductionGate.EvaluateFirstFormalBuild(nonPending);
                Assert.That(nonPendingResult.CanBuild, Is.False, Describe(nonPendingResult));
                Assert.That(nonPendingResult.Issues.Any(issue => issue.Code == "W24S5-PRE003" && issue.IsError), Is.True, Describe(nonPendingResult));
            }

            var trace = JObject.Parse(File.ReadAllText(Absolute(PreTracePath))); trace["runtimeEntryGuid"] = "0123456789abcdef0123456789abcdef"; WriteArtifact(PreTracePath, trace.ToString(Formatting.None));
            var identityResult = W24S5ProductionGate.EvaluateFirstFormalBuild(ValidFirstFormalRequest());
            Assert.That(identityResult.CanBuild, Is.False);
            Assert.That(identityResult.Issues.Any(issue => issue.Code == "W24S5-PRE023" && issue.IsError), Is.True, Describe(identityResult));
        }

        [Test]
        public void FirstFormalBuild_RejectsMissingReverseLayerMappingAndCallerOwnedRootSubstitution()
        {
            var trace = JObject.Parse(File.ReadAllText(Absolute(PreTracePath))); ((JObject)((JArray)trace["requirementTraces"])[4])["layerIds"] = new JArray("world_trail"); WriteArtifact(PreTracePath, trace.ToString(Formatting.None));
            var mappingResult = W24S5ProductionGate.EvaluateFirstFormalBuild(ValidFirstFormalRequest());
            Assert.That(mappingResult.CanBuild, Is.False);
            Assert.That(mappingResult.Issues.Any(issue => issue.Code == "W24S5-PRE037" && issue.IsError), Is.True, Describe(mappingResult));

            WriteFirstFormalArtifacts();
            var rootSubstitution = ValidFirstFormalRequest(); rootSubstitution.OwnedOutputRoot = "Assets/VFX/Generated/other";
            var rootResult = W24S5ProductionGate.EvaluateFirstFormalBuild(rootSubstitution);
            Assert.That(rootResult.CanBuild, Is.False);
            Assert.That(rootResult.Issues.Any(issue => issue.Code == "W24S5-PRE006" && issue.IsError), Is.True, Describe(rootResult));
        }

        [Test]
        public void FirstFormalBuild_RejectsContractTraceAndManifestPathSubstitution()
        {
            var contract = JObject.Parse(File.ReadAllText(Absolute(PreContractPath)));
            var extensions = (JObject)contract["extensions"];
            extensions["implementationTrace"] = TracePath;
            extensions["manifest"] = "ProjectSettings/VFXComposer/BuildManifests/other.manifest.json";
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            WriteArtifact(PreContractPath, contract.ToString(Formatting.None));

            var result = W24S5ProductionGate.EvaluateFirstFormalBuild(ValidFirstFormalRequest());
            Assert.That(result.CanBuild, Is.False);
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-PRE017" && issue.IsError), Is.True, Describe(result));
            Assert.That(result.Issues.Any(issue => issue.Code == "W24S5-PRE018" && issue.IsError), Is.True, Describe(result));
        }

        [Test]
        public void FirstFormalBuild_ApprovalCannotBeForgedOrUsedAfterPinnedTraceChanges()
        {
            Assert.Throws<InvalidOperationException>(() => new W24S5FirstFormalBuildApproval(new object(), null, null, PreEffectId, PreRuntimeEntry, W24S5ProductionGate.ManifestRoot + PreEffectId + ".manifest.json", PreOutputRoot, Hash("forged-contract"), 1, null));
            Assert.Throws<InvalidOperationException>(() => new W24S5FormalApproval(new object(), null, null, null, null, EffectId, Hash("planned-build"), RuntimeEntry, W24S5VisualStatus.VISUAL_PENDING));

            var gated = W24S5ProductionGate.EvaluateFirstFormalBuild(ValidFirstFormalRequest());
            Assert.That(gated.CanBuild, Is.True, Describe(gated));
            W24S5BootstrapReceipt receipt;
            string receiptError;
            Assert.That(W24S5ProductionGate.TryGetBootstrapReceipt(gated.FirstFormalApproval, out receipt, out receiptError), Is.False);
            Assert.That(receipt, Is.Null);

            var trace = JObject.Parse(File.ReadAllText(Absolute(PreTracePath))); trace["runtimeEntryGuid"] = "0123456789abcdef0123456789abcdef"; WriteArtifact(PreTracePath, trace.ToString(Formatting.None));
            var audit = W24S5ProductionGate.CommitFirstFormalBuild(gated.FirstFormalApproval, "projectile", 1, 1, Hash("recipe"), RawHash("first-build"), "w24-test", 1.0, "Assets/VFX/Recipes/test.json");
            Assert.That(audit.Report.Entries.Any(entry => entry.Code == "E24S5PRE041" && entry.Severity == ValidationSeverity.Error), Is.True);
        }

        private static W24S5ProductionGateRequest ValidRequest()
        {
            return new W24S5ProductionGateRequest
            {
                EffectId = EffectId,
                ContractPath = "docs/vfx-contracts/sustained_flame_3d.contract.json",
                ContractFileHash = FileHash("docs/vfx-contracts/sustained_flame_3d.contract.json"),
                TracePath = TracePath,
                TraceFileHash = FileHash(TracePath),
                PlannedBuildHash = Hash("planned-build"),
                ExpectedRuntimeEntryPath = RuntimeEntry,
                ExpectedManifestPath = W24S5ProductionGate.ManifestRoot + EffectId + ".manifest.json",
                Intent = W24S5BuildIntent.Development,
                VisualStatus = W24S5VisualStatus.VISUAL_PENDING
            };
        }

        private static W24S5FirstFormalBuildRequest ValidFirstFormalRequest()
        {
            return new W24S5FirstFormalBuildRequest
            {
                EffectId = PreEffectId,
                ContractPath = PreContractPath,
                ContractFileHash = FileHash(PreContractPath),
                TracePath = PreTracePath,
                TraceFileHash = FileHash(PreTracePath),
                ExpectedRuntimeEntryPath = PreRuntimeEntry,
                ExpectedManifestPath = W24S5ProductionGate.ManifestRoot + PreEffectId + ".manifest.json",
                OwnedOutputRoot = PreOutputRoot,
                Intent = W24S5BuildIntent.Development,
                VisualStatus = W24S5VisualStatus.VISUAL_PENDING
            };
        }

        private static void WriteFirstFormalArtifacts()
        {
            var contract = JObject.Parse(File.ReadAllText(Absolute("docs/vfx-contracts/w24_moving_projectile_trail.contract.json")));
            contract["effectId"] = PreEffectId;
            var capture = (JObject)contract["captureProfile"];
            capture["prefabManifestSerializedReference"] = W24S5ProductionGate.ManifestRoot + PreEffectId + ".manifest.json#buildHash";
            var extensions = (JObject)contract["extensions"];
            extensions["runtimeEntry"] = PreRuntimeEntry;
            extensions["manifest"] = W24S5ProductionGate.ManifestRoot + PreEffectId + ".manifest.json";
            extensions["implementationTrace"] = PreTracePath;
            contract["contractHash"] = VfxDesignContractJson.ComputeContractHash(contract.ToString(Formatting.None));
            WriteArtifact(PreContractPath, contract.ToString(Formatting.None));

            var trace = JObject.Parse(File.ReadAllText(Absolute("docs/vfx-traces/w24_moving_projectile_trail.implementation-trace.json")));
            trace["effectId"] = PreEffectId;
            trace["contractHash"] = (string)contract["contractHash"];
            trace["runtimeEntryAssetPath"] = PreRuntimeEntry;
            foreach (var requirement in ((JArray)trace["requirementTraces"]).OfType<JObject>())
            foreach (var target in (requirement["objects"] as JArray ?? new JArray()).OfType<JObject>())
                if (target["assetPath"] != null) target["assetPath"] = PreRuntimeEntry;
            WriteArtifact(PreTracePath, trace.ToString(Formatting.None));
        }

        private static string ValidTrace()
        {
            var contract = VfxDesignContract.FromJson(File.ReadAllText(Absolute("docs/vfx-contracts/sustained_flame_3d.contract.json")));
            var trace = new VfxImplementationTrace
            {
                TraceVersion = "w24-s5-test/1", TraceStatus = "FORMAL_EVIDENCE_BOUND", EffectId = contract.EffectId, ContractRevision = contract.ContractRevision, ContractHash = contract.ContractHash,
                BuildHash = Hash("planned-build"), CaptureProfileHash = Hash("capture"), RuntimeEntryAssetPath = RuntimeEntry, RuntimeEntryGuid = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                RequirementTraces = contract.Requirements.Select(requirement => RequirementTrace(contract, requirement)).ToArray()
            };
            return JsonConvert.SerializeObject(trace, Formatting.None, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
        }

        private static VfxRequirementTrace RequirementTrace(VfxDesignContract contract, VfxDesignRequirement requirement)
        {
            var light = requirement.Statement.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0;
            var authority = requirement.EvidenceAuthority;
            var cross = authority == "telemetry" ? "diagnostic" : authority == "diagnostic" ? "telemetry" : authority == "visualQa" ? "telemetry" : "visualQa";
            var visual = requirement.Type == "visual-measurable" || requirement.Type == "visual-semantic";
            return new VfxRequirementTrace
            {
                DesignRequirementId = requirement.DesignRequirementId, EvidenceAuthority = authority,
                Objects = authority == "telemetry" ? new[] { new VfxTraceObject { AssetPath = RuntimeEntry, HierarchyPath = "Root/Carrier", ComponentType = light ? "Light" : "ParticleSystem", ComponentInstanceId = requirement.DesignRequirementId } } : Array.Empty<VfxTraceObject>(),
                StateIds = requirement.Type == "behavioral" ? new[] { contract.SemanticStateMachine.States[0].StateId } : Array.Empty<string>(),
                LayerIds = visual ? new[] { contract.Layers[0].LayerId } : Array.Empty<string>(),
                Seeds = new[] { contract.CaptureProfile.CanonicalSeed }.Concat(contract.CaptureProfile.RobustnessSeeds).ToArray(),
                SemanticTokens = requirement.Type == "budget" ? new[] { "budget-readback" } : Array.Empty<string>(),
                AuthorityEvidence = new[] { Evidence(authority, light ? "receiver-linear-luminance" : "authority") },
                CrossEvidence = new[] { Evidence(cross, light ? "receiver-linear-luminance" : "cross") }
            };
        }

        private static VfxTraceEvidence Evidence(string kind, string detail) { return new VfxTraceEvidence { Kind = kind, Reference = "artifacts/immutable", Sha256 = Hash(kind), Passed = true, Detail = detail }; }
        private static void WriteTrace(string text) { WriteArtifact(TracePath, text); }
        private static void WriteArtifact(string relativePath, string text) { var path = Absolute(relativePath); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, new UTF8Encoding(false)); }
        private static string FileHash(string path) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(File.ReadAllBytes(Absolute(path))).Select(value => value.ToString("x2"))); }
        private static string HashFileAbsolute(string path) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(value => value.ToString("x2"))); }
        private static string RawFileHash(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(ProjectAbsolute(path))).Select(value => value.ToString("x2"))); }
        private static string Hash(string seed) { using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(seed)).Select(value => value.ToString("x2"))); }
        private static string RawHash(string seed) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(seed)).Select(value => value.ToString("x2"))); }
        private static string CanonicalForTest(JToken value) { if (value is JObject obj) { var sorted = new JObject(); foreach (var property in obj.Properties().OrderBy(item => item.Name, StringComparer.Ordinal)) sorted.Add(property.Name, JToken.Parse(CanonicalForTest(property.Value))); return sorted.ToString(Formatting.None); } if (value is JArray array) return new JArray(array.Select(item => JToken.Parse(CanonicalForTest(item)))).ToString(Formatting.None); return value.ToString(Formatting.None); }
        private static string Absolute(string relative) { return relative.StartsWith("docs/", StringComparison.Ordinal) ? Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)) : ProjectAbsolute(relative); }
        private static string ProjectAbsolute(string relative) { return Path.GetFullPath(Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
        private static string ProjectRoot { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); } }
        private static string RepositoryRoot { get { return Path.GetFullPath(Path.Combine(ProjectRoot, "..")); } }
        private static string Describe(W24S5ProductionGateResult result) { return string.Join(" | ", result.Issues.Select(issue => issue.Code + ":" + issue.Message)); }
    }
}
