using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S0bFormalEvidenceReplayTests
    {
        private const string EffectId = "sustained_flame_3d";
        private const string CaptureRoot = "artifacts/vfx-evidence/sustained_flame_3d/C0";
        private bool ownsFixture;

        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Absolute(CaptureRoot))) Assert.Ignore("Never overlay a real write-once S0b evidence directory.");
            ownsFixture = true;
            BuildMetadata(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (ownsFixture && Directory.Exists(Absolute(CaptureRoot))) Directory.Delete(Absolute(CaptureRoot), true);
        }

        [Test]
        public void ReplayProof_AcceptsSealedReceiverTokenAndContractBoundMachineFacts()
        {
            var gate = new W24S5ProductionGateResult();
            Assert.That(W24S5EvidenceTransition.VerifySustainedFlameFormalMetadata(gate, BuildMetadata(false), Contract()), Is.True);
            Assert.That(gate.HasErrors, Is.False);
        }

        [Test]
        public void ReplayProof_RejectsReceiverSummaryOnADifferentObservedToken()
        {
            var metadata = BuildMetadata(false);
            var records = (JArray)metadata.SelectToken("s0bFormalEvidence.supplementalDiagnostics");
            ((JObject)records.Single(item => (string)item["kind"] == "receiver-linear-luminance-ab"))["observedPlayerLoop"]["logicalFrameIndex"] = 181;
            var gate = new W24S5ProductionGateResult();
            Assert.That(W24S5EvidenceTransition.VerifySustainedFlameFormalMetadata(gate, metadata, Contract()), Is.False);
            Assert.That(gate.Issues.Any(item => item.Code == "W24S5-C174" && item.IsError), Is.True);
        }

        [Test]
        public void ReplayProof_RejectsLifecycleFactsThatDisagreeWithRecordedBranchFrames()
        {
            var gate = new W24S5ProductionGateResult();
            Assert.That(W24S5EvidenceTransition.VerifySustainedFlameFormalMetadata(gate, BuildMetadata(true), Contract()), Is.False);
            Assert.That(gate.Issues.Any(item => item.Code == "W24S5-C178" && item.IsError), Is.True);
        }

        private static JObject BuildMetadata(bool wrongFirstSteady)
        {
            var diagnostics = Absolute(CaptureRoot + "/diagnostics");
            Directory.CreateDirectory(diagnostics);
            var command = new JObject
            {
                ["schema"] = "w24-s0b-formal-operator-command/v1", ["effectId"] = EffectId, ["candidateId"] = "C0",
                ["seeds"] = new JArray(24001, 24011, 24021), ["retainedFrameIndices"] = RetainedFrames(),
                ["branches"] = new JArray(BranchPlan(24001, "stop"), BranchPlan(24011, "stop"), BranchPlan(24021, "interrupt"))
            };
            var telemetry = new JObject
            {
                ["schema"] = "w24-s0b-semantic-telemetry/v2", ["captureCompleteness"] = "complete",
                ["runtimeFacts"] = new JObject { ["layersIndependent"] = true, ["lightWithinContract"] = true, ["budgetWithinContract"] = true, ["particleSystemCount"] = 7, ["particleCapacity"] = 144, ["particleRendererCount"] = 7, ["materialCount"] = 2, ["lightCount"] = 1 },
                ["branches"] = new JArray(TelemetryBranch(24001, "stop", wrongFirstSteady), TelemetryBranch(24011, "stop", false), TelemetryBranch(24021, "interrupt", false))
            };
            var receiver = new JObject { ["schema"] = "w24-s0b-receiver-light-ab/v2", ["onlyChangedBetweenSamples"] = "UnityEngine.Light.enabled", ["offLinearLuminance"] = .1, ["onLinearLuminance"] = .2 };
            var commandHash = WriteJson("diagnostics/operator-command.json", command);
            var telemetryHash = WriteJson("diagnostics/semantic-telemetry.json", telemetry);
            var receiverHash = WriteJson("diagnostics/receiver-light-ab.json", receiver);
            var offHash = WriteBytes("diagnostics/receiver-light-off.png", new byte[] { 1 });
            var onHash = WriteBytes("diagnostics/receiver-light-on.png", new byte[] { 2 });
            var token = new JObject { ["serial"] = 180, ["frame"] = 180, ["time"] = 3.0, ["logicalFrameIndex"] = 180, ["seed"] = 24001 };
            var supplemental = new JArray
            {
                Record("formal-capture-command", "diagnostics/operator-command.json", commandHash),
                Record("receiver-light-off", "diagnostics/receiver-light-off.png", offHash, token),
                Record("receiver-light-on", "diagnostics/receiver-light-on.png", onHash, token),
                Record("receiver-linear-luminance-ab", "diagnostics/receiver-light-ab.json", receiverHash, token)
            };
            var semantic = new JArray(Record("semantic-telemetry", "diagnostics/semantic-telemetry.json", telemetryHash));
            var artifacts = new JArray
            {
                Artifact("diagnostics/operator-command.json", commandHash, "diagnostic"), Artifact("diagnostics/semantic-telemetry.json", telemetryHash, "telemetry"),
                Artifact("diagnostics/receiver-light-off.png", offHash, "diagnostic"), Artifact("diagnostics/receiver-light-on.png", onHash, "diagnostic"), Artifact("diagnostics/receiver-light-ab.json", receiverHash, "diagnostic")
            };
            var frames = new JArray();
            foreach (var seed in new[] { 24001, 24011, 24021 }) foreach (var frame in RetainedFrames().Values<int>()) frames.Add(new JObject { ["seed"] = seed, ["frameIndex"] = frame });
            return new JObject
            {
                ["schemaVersion"] = "w24-capture-metadata-v1", ["effectId"] = EffectId, ["artifacts"] = artifacts,
                ["s0bFormalEvidence"] = new JObject
                {
                    ["schema"] = "w24-s0b-formal-evidence-projection/v1",
                    ["captureProfile"] = new JObject { ["fps"] = 60, ["canonicalSeed"] = 24001, ["robustnessSeeds"] = new JArray(24011, 24021), ["retainedFrameIndices"] = RetainedFrames() },
                    ["formalPlayerLoop"] = new JObject { ["observedSerial"] = 1098, ["consumedSerial"] = 1098, ["allObservedFramesConsumed"] = true },
                    ["frames"] = frames, ["semanticTelemetry"] = semantic, ["supplementalDiagnostics"] = supplemental,
                    ["recorderProvenance"] = new JObject { ["operatorCommandHash"] = commandHash }
                }
            };
        }

        private static JObject TelemetryBranch(int seed, string exit, bool wrongFirstSteady)
        {
            var frames = new JArray();
            for (var frame = 1; frame <= 366; frame++)
            {
                var state = frame < 21 ? "starting" : frame <= 291 ? "steady" : frame == 292 ? (exit == "interrupt" ? "interrupted" : "stopping") : "idle";
                frames.Add(new JObject { ["frameIndex"] = frame, ["state"] = state, ["sample"] = new JObject { ["enabledLightCount"] = frame == 292 ? 1 : 0 } });
            }
            return new JObject { ["seed"] = seed, ["exit"] = exit, ["observedFrames"] = 366, ["retainedFrames"] = 11, ["firstSteadyFrame"] = wrongFirstSteady ? 20 : 21, ["steadyAtExitRequest"] = true, ["sawRequestedExit"] = true, ["sawExitCarrier"] = true, ["lastLitExitFrame"] = 292, ["frames"] = frames, ["final"] = new JObject { ["state"] = "idle", ["cleanupComplete"] = true, ["enabledLightCount"] = 0 } };
        }

        private static JObject BranchPlan(int seed, string exit) { return new JObject { ["seed"] = seed, ["exit"] = exit, ["steadyFramesBeforeExit"] = 291, ["cleanupThroughFrame"] = 366 }; }
        private static JObject Record(string kind, string file, string hash, JObject token = null) { var value = new JObject { ["kind"] = kind, ["file"] = file, ["sha256"] = hash }; if (token != null) value["observedPlayerLoop"] = token.DeepClone(); return value; }
        private static JObject Artifact(string file, string hash, string kind) { return new JObject { ["path"] = CaptureRoot + "/" + file, ["sha256"] = hash, ["kind"] = kind }; }
        private static JArray RetainedFrames() { return new JArray(1, 21, 60, 120, 180, 240, 270, 291, 293, 321, 366); }
        private static string WriteJson(string relative, JObject value) { return WriteBytes(relative, Encoding.UTF8.GetBytes(value.ToString(Formatting.None))); }
        private static string WriteBytes(string relative, byte[] bytes) { var path = Absolute(CaptureRoot + "/" + relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes); using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(bytes).Select(value => value.ToString("x2"))); }
        private static VfxDesignContract Contract() { return VfxDesignContract.FromJson(File.ReadAllText(Absolute("docs/vfx-contracts/sustained_flame_3d.contract.json"))); }
        private static string Absolute(string relative) { return Path.GetFullPath(Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar))); }
        private static string RepositoryRoot { get { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; } }
    }
}
