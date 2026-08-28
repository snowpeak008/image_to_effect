using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.Workflow;

namespace VFXComposer.Tests.EditMode.W24S1
{
    public sealed class W24S1ContractAndTraceTests
    {
        private const string Hash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private static readonly string[] FormalContractFiles =
        {
            "sustained_flame_3d.contract.json",
            "w24_moving_projectile_trail.contract.json",
            "w24_weapon_socket_fragments.contract.json",
            "w24_real_light_receivers.contract.json"
        };

        [Test]
        public void AllFourFormalContracts_UseTheSameStrictLowerCamelAuthorityAndMatchCanonicalHash()
        {
            foreach (var file in FormalContractFiles)
            {
                var json = File.ReadAllText(ContractPath(file));
                VfxDesignContract contract;
                var report = VfxDesignContractJson.ValidateJson(json, out contract);
                Assert.That(report.HasErrors, Is.False, file + ": " + Describe(report));
                Assert.That(contract.ContractHash, Is.EqualTo(VfxDesignContractJson.ComputeContractHash(json)), file);
                var status = contract.Extensions.Value<string>("captureBindingStatus");
                if (status == "PENDING_FIRST_FORMAL_BUILD") Assert.That(contract.CaptureProfile.SceneHash, Is.EqualTo("pending:formal-build"), file);
                else Assert.That(status, Is.EqualTo("FROZEN_PRE_C0"), file);
            }
        }

        [Test]
        public void FormalSustainedFlame_LowerCamelJsonNestedShapeAndPythonCompatibleHash_AllPass()
        {
            var json = FormalJson();
            VfxDesignContract contract;
            var report = VfxDesignContractJson.ValidateJson(json, out contract);
            Assert.That(report.HasErrors, Is.False, Describe(report));
            Assert.That(contract.EffectId, Is.EqualTo("sustained_flame_3d"));
            Assert.That(contract.CaptureProfile.CameraPose.Position, Has.Length.EqualTo(3));
            Assert.That(contract.CaptureProfile.Resolution.Width, Is.EqualTo(960));
            Assert.That(contract.CaptureProfile.Bloom.Enabled, Is.False);
            Assert.That(contract.Layers[0].BudgetCost, Is.TypeOf<JObject>());
            Assert.That(contract.Budget.TextureResidency.LocalExclusiveMb, Is.Zero);
            Assert.That(contract.Requirements.Single(v => v.DesignRequirementId == "REQ-LIGHT-RECEIVER").VisualVerdict.EvidenceLocation.FrameInterval, Is.EqualTo(new[] { 60, 180 }));
            Assert.That(contract.SemanticStateMachine.CompletionExits, Does.Contain("stopping to idle"), "semantic exit descriptions are legal and are not state IDs");
            Assert.That(VfxDesignContractJson.ComputeContractHash(json), Is.EqualTo(contract.ContractHash));
            Assert.That(contract.ComputeContractHash(), Is.EqualTo(contract.ContractHash));
        }

        [Test]
        public void StrictJsonEntry_RejectsUnknownField()
        {
            var root = JObject.Parse(FormalJson()); root["inventedField"] = true;
            VfxDesignContract ignored; var report = VfxDesignContractJson.ValidateJson(root.ToString(Formatting.None), out ignored);
            Assert.That(report.Issues.Any(v => v.Code == "W24J001"), Is.True, Describe(report));
        }

        [Test]
        public void StrictJsonEntry_RejectsMissingNestedResolution()
        {
            var root = JObject.Parse(FormalJson()); ((JObject)root["captureProfile"]).Remove("resolution"); Rehash(root);
            VfxDesignContract ignored; var report = VfxDesignContractJson.ValidateJson(root.ToString(Formatting.None), out ignored);
            Assert.That(report.Issues.Any(v => v.Code == "W24J001"), Is.True, Describe(report));
        }

        [Test]
        public void StrictJsonEntry_RejectsLayerBudgetCostStringInsteadOfObject()
        {
            var root = JObject.Parse(FormalJson()); root["layers"][0]["budgetCost"] = "not-an-object"; Rehash(root);
            VfxDesignContract ignored; var report = VfxDesignContractJson.ValidateJson(root.ToString(Formatting.None), out ignored);
            Assert.That(report.HasErrors, Is.True);
            Assert.That(report.Issues.Any(v => v.Code == "W24J001" || v.Code == "W24C062"), Is.True, Describe(report));
        }

        [Test]
        public void PreC0Identity_RejectsFakeZeroCaptureToolHash()
        {
            var root = JObject.Parse(FormalJson());
            root["captureProfile"]["captureToolHash"] = "sha256:" + new string('0', 64); Rehash(root);
            VfxDesignContract ignored; var report = VfxDesignContractJson.ValidateJson(root.ToString(Formatting.None), out ignored);
            Assert.That(report.Issues.Any(v => v.Code == "W24C048A"), Is.True, Describe(report));
        }

        [Test]
        public void RealJsonContract_DrivesFormalBidirectionalTraceValidation()
        {
            var contract = VfxDesignContract.FromJson(FormalJson());
            var trace = Trace(contract);
            var good = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(good.Report.HasErrors, Is.False, Describe(good.Report));
            Assert.That(good.KnownCheatFindings.Any(finding => finding.CheatId == "additive-fake-light"), Is.False, "The formal fixture must represent a real Light plus an independent receiver A/B probe.");
            trace.RequirementTraces = trace.RequirementTraces.Take(trace.RequirementTraces.Length - 1).ToArray();
            var bad = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(bad.Report.Issues.Any(v => v.Code == "W24T012"), Is.True);
        }

        [Test]
        public void FormalTraceJsonEntry_IsStrictLowerCamel_AndPendingRegistrationCannotPretendToBeBuilt()
        {
            var contract = VfxDesignContract.FromJson(FormalJson());
            var goodJson = JsonConvert.SerializeObject(Trace(contract), Formatting.None);
            StringAssert.Contains("\"traceVersion\"", goodJson);
            StringAssert.DoesNotContain("\"TraceVersion\"", goodJson);
            VfxImplementationTrace parsed;
            var good = VfxImplementationTraceJson.ValidateJson(goodJson, contract, out parsed);
            Assert.That(good.Report.HasErrors, Is.False, Describe(good.Report));

            var root = JObject.Parse(goodJson); root["inventedTraceField"] = true;
            var unknown = VfxImplementationTraceJson.ValidateJson(root.ToString(Formatting.None), contract, out parsed);
            Assert.That(unknown.Report.Issues.Any(issue => issue.Code == "W24TJ001"), Is.True, Describe(unknown.Report));

            foreach (var pair in new[]
            {
                new[] { "sustained_flame_3d.contract.json", "sustained_flame_3d.implementation-trace.json" },
                new[] { "w24_moving_projectile_trail.contract.json", "w24_moving_projectile_trail.implementation-trace.json" },
                new[] { "w24_weapon_socket_fragments.contract.json", "w24_weapon_socket_fragments.implementation-trace.json" },
                new[] { "w24_real_light_receivers.contract.json", "w24_real_light_receivers.implementation-trace.json" }
            })
            {
                var pendingJson = File.ReadAllText(TracePath(pair[1]));
                Assert.DoesNotThrow(() => VfxImplementationTraceJson.FromJson(pendingJson), pair[1] + " must remain strict lowerCamel JSON.");
                var pendingContract = VfxDesignContract.FromJson(File.ReadAllText(ContractPath(pair[0])));
                var pending = VfxImplementationTraceJson.ValidateJson(pendingJson, pendingContract, out parsed);
                Assert.That(pending.Report.Issues.Any(issue => issue.Code == "W24T004" || issue.Code == "W24T005"), Is.True, pair[1] + " must fail formal build/GUID binding until authoring fills real identities.");
                Assert.That(pending.Report.Issues.Any(issue => issue.Code == "W24TJ001" || issue.Code == "W24T001" || issue.Code == "W24T002" || issue.Code == "W24T003" || issue.Code == "W24T010" || issue.Code == "W24T011" || issue.Code == "W24T012" || issue.Code == "W24T038" || issue.Code == "W24T039"), Is.False, pair[1] + " preregistration must already cover the exact contract requirements/states/layers.");
            }
        }

        [TestCase("Independent fragments rotate separately", "whole_group_rotation", "whole-image-rotation-fake-fragments")]
        [TestCase("A projectile trail follows movement", "static_trail", "static-fake-trail")]
        [TestCase("A real Light illuminates a receiver", "additive_fake_light", "additive-fake-light")]
        public void FrozenKnownCheats_AuthorityMustFailAndIndependentEvidenceMustAlarm(string statement, string token, string cheatId)
        {
            var contract = VfxDesignContract.FromJson(FormalJson());
            var requirement = new VfxDesignRequirement { DesignRequirementId = "KNOWN-CHEAT", Type = "structural", EvidenceAuthority = "telemetry", Statement = statement };
            contract.Requirements = new[] { requirement }; contract.ContractHash = contract.ComputeContractHash();
            var trace = Trace(contract); var target = trace.RequirementTraces.Single();
            target.SemanticTokens = target.SemanticTokens.Concat(new[] { token }).ToArray();
            target.AuthorityEvidence[0].Passed = true; target.CrossEvidence[0].Passed = true;
            var result = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(result.KnownCheatFindings.Any(v => v.CheatId == cheatId), Is.True);
            Assert.That(result.Report.Issues.Any(v => v.Code == "W24T020"), Is.True);
            Assert.That(result.Report.Issues.Any(v => v.Code == "W24T021"), Is.True);
        }

        [Test]
        public void RepositoryUserSignatureCannotGrantL4BeforeHostOwnedAuthorityExists()
        {
            var contract = VfxDesignContract.FromJson(FormalJson()); var trace = Trace(contract);
            Assert.That(W24MaturityPolicy.CanMarkL3(W24S0aTerminalStatus.S0A_ADVISORY_ONLY, true, true), Is.False);
            Assert.That(W24MaturityPolicy.CanMarkL4(contract, trace, null), Is.False);
            var signature = new W24UserSignature { UserIdentity = "user", ContractRevision = contract.ContractRevision, BuildHash = trace.BuildHash, CaptureProfileHash = trace.CaptureProfileHash, VerdictCorpusReference = "docs/vfx-verdicts/user.json" };
            Assert.That(W24MaturityPolicy.CanMarkL4(contract, trace, signature), Is.False, "A caller-constructible signature DTO must not grant L4 before a host-owned opaque authority exists.");
            signature.BuildHash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            Assert.That(W24MaturityPolicy.CanMarkL4(contract, trace, signature), Is.False);
        }

        [Test]
        public void TypedMetricTraceEvidence_RequiresReportIdentityAndCanonicalAnalysisInput()
        {
            var contract = VfxDesignContract.FromJson(FormalJson());
            var trace = Trace(contract);
            var evidence = trace.RequirementTraces[0].AuthorityEvidence[0];
            evidence.Kind = "diagnostic"; evidence.PassId = "metrics-report"; evidence.Encoding = "json";
            evidence.MetricCheckId = "trail-corridor-seed-1"; evidence.AnalysisInputSha256 = "not-a-hash";
            var result = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(result.Report.Issues.Any(issue => issue.Code == "W24T047" || issue.Code == "W24T048"), Is.True, Describe(result.Report));
            evidence.AnalysisInputSha256 = Hash; evidence.PassId = "trail-only-mask";
            result = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(result.Report.Issues.Any(issue => issue.Code == "W24T048"), Is.True, Describe(result.Report));
        }

        [Test]
        public void TypedMatrix_DiagnosticAuthorityRejectsGenericSummaryAndDuplicateMetricCheck()
        {
            var contract = VfxDesignContract.FromJson(File.ReadAllText(ContractPath("w24_moving_projectile_trail.contract.json")));
            var trace = Trace(contract);
            var target = trace.RequirementTraces.Single(value => value.DesignRequirementId == "REQ-B-TRAIL-CORRIDOR");
            var generic = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(generic.Report.Issues.Any(issue => issue.Code == "W24T053"), Is.True, "A generic diagnostic JSON must not satisfy Contract-frozen typed authority.");

            var evidence = target.AuthorityEvidence[0];
            evidence.PassId = "metrics-report"; evidence.Encoding = "json"; evidence.MetricCheckId = "trail-corridor-24101-18"; evidence.AnalysisInputSha256 = Hash;
            var typed = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(typed.Report.Issues.Any(issue => issue.Code == "W24T053"), Is.False, Describe(typed.Report));

            target.AuthorityEvidence = new[] { evidence, new VfxTraceEvidence { Kind = evidence.Kind, Reference = evidence.Reference, Sha256 = evidence.Sha256, Passed = true, PassId = evidence.PassId, Encoding = evidence.Encoding, MetricCheckId = evidence.MetricCheckId, AnalysisInputSha256 = evidence.AnalysisInputSha256 } };
            var duplicate = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(duplicate.Report.Issues.Any(issue => issue.Code == "W24T054"), Is.True, Describe(duplicate.Report));
        }

        [Test]
        public void TypedMatrix_FragmentDiagnosticCrossEvidenceCannotUseGenericSummary()
        {
            var contract = VfxDesignContract.FromJson(File.ReadAllText(ContractPath("w24_weapon_socket_fragments.contract.json")));
            var trace = Trace(contract);
            var target = trace.RequirementTraces.Single(value => value.DesignRequirementId == "REQ-C-FRAGMENT-INDEPENDENCE");
            Assert.That(target.CrossEvidence[0].Kind, Is.EqualTo("diagnostic"));
            var generic = VfxImplementationTraceValidator.Validate(contract, trace);
            Assert.That(generic.Report.Issues.Any(issue => issue.Code == "W24T056" && issue.Path.Contains("REQ-C-FRAGMENT-INDEPENDENCE")), Is.True, Describe(generic.Report));
        }

        private static VfxImplementationTrace Trace(VfxDesignContract contract)
        {
            return new VfxImplementationTrace
            {
                TraceVersion="w24-s1/v1",EffectId=contract.EffectId,ContractRevision=contract.ContractRevision,ContractHash=contract.ContractHash,BuildHash=Hash,CaptureProfileHash=Hash,RuntimeEntryAssetPath="Assets/VFX/test.prefab",RuntimeEntryGuid="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                RequirementTraces=contract.Requirements.Select(requirement=>TraceRequirement(contract,requirement)).ToArray()
            };
        }

        private static VfxRequirementTrace TraceRequirement(VfxDesignContract contract,VfxDesignRequirement requirement)
        {
            var isLight=requirement.Statement.IndexOf("light",StringComparison.OrdinalIgnoreCase)>=0;
            var crossKind=isLight?(requirement.EvidenceAuthority=="diagnostic"?"telemetry":"diagnostic"):requirement.EvidenceAuthority=="diagnostic"?"telemetry":requirement.EvidenceAuthority=="user"?"visualQa":"diagnostic";
            var tokens=requirement.Type=="budget"?new[]{"budget-readback"}:requirement.Statement.IndexOf("trail",StringComparison.OrdinalIgnoreCase)>=0?new[]{"emitter_position_history","trail_vertices_from_motion"}:Array.Empty<string>();
            return new VfxRequirementTrace
            {
                DesignRequirementId=requirement.DesignRequirementId,EvidenceAuthority=requirement.EvidenceAuthority,
                Objects=(requirement.EvidenceAuthority=="telemetry"||isLight)?new[]{new VfxTraceObject{AssetPath="Assets/VFX/test.prefab",HierarchyPath=isLight?"Root/Light":"Root/Carrier",ComponentType=isLight?"Light":"ParticleSystem",ComponentInstanceId=requirement.DesignRequirementId}}:Array.Empty<VfxTraceObject>(),
                StateIds=requirement.Type=="behavioral"?new[]{contract.SemanticStateMachine.States[0].StateId}:Array.Empty<string>(),
                LayerIds=requirement.Type=="visual-measurable"||requirement.Type=="visual-semantic"?new[]{contract.Layers[0].LayerId}:Array.Empty<string>(),
                Seeds=new[]{contract.CaptureProfile.CanonicalSeed,contract.CaptureProfile.RobustnessSeeds[0],contract.CaptureProfile.RobustnessSeeds[1]},SemanticTokens=tokens,
                AuthorityEvidence=new[]{Evidence(requirement.EvidenceAuthority,true,"authority")},CrossEvidence=new[]{Evidence(crossKind,true,isLight?"receiver-linear-luminance":"independent check")}
            };
        }
        private static VfxTraceEvidence Evidence(string kind,bool pass,string detail){return new VfxTraceEvidence{Kind=kind,Reference="artifacts/"+kind,Sha256=Hash,Passed=pass,Detail=detail};}
        private static string FormalJson(){return File.ReadAllText(ContractPath("sustained_flame_3d.contract.json"));}
        private static string ContractPath(string file){return Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","vfx-contracts",file));}
        private static string TracePath(string file){return Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","vfx-traces",file));}
        private static void Rehash(JObject root){root["contractHash"]=VfxDesignContractJson.ComputeContractHash(root.ToString(Formatting.None));}
        private static string Describe(W24GateReport report){return string.Join(" | ",report.Issues.Select(v=>v.Code+":"+v.Path+":"+v.Message));}
    }
}
