using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.Workflow;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24WorkflowTests
    {
        [Test]
        public void QualifiedVisualPass_LeavesL4UnavailableUntilHostOwnedSignoffExists()
        {
            var candidate = Candidate(W24CandidateId.C0, 1, "build-a", "capture-a");
            var state = W24WorkflowAggregator.Start(candidate, W24S0aTerminalStatus.S0A_GATE_QUALIFIED);

            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.VISUAL_PASS);

            Assert.That(state.Maturity, Is.EqualTo(W24MaturityLevel.L3_ProductionCandidate));
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingOrdinaryUserSignoff));
            Assert.That(state.UserEntryPath, Is.EqualTo(W24UserEntryPath.OrdinarySignoff));
            Assert.Throws<InvalidOperationException>(() => W24WorkflowAggregator.ApplyUserDecision(state, W24UserDecision.Signed, Candidate(W24CandidateId.C0, 1, "different", "capture-a")));

            Assert.Throws<InvalidOperationException>(() => W24WorkflowAggregator.ApplyUserDecision(state, W24UserDecision.Signed, candidate));
            Assert.That(state.Maturity, Is.Not.EqualTo(W24MaturityLevel.L4_UserSignedProductionReady));
        }

        [Test]
        public void AdvisoryS0a_CannotCreateOrdinaryL3AndUsesMarkedUpgrade()
        {
            var state = W24WorkflowAggregator.Start(Candidate(W24CandidateId.C0, 1), W24S0aTerminalStatus.S0A_ADVISORY_ONLY);
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.VISUAL_PASS);

            Assert.That(state.QaGateAuthority, Is.EqualTo(W24QaGateAuthority.AdvisoryOnly));
            Assert.That(state.Maturity, Is.EqualTo(W24MaturityLevel.L2_VisualPlaceholder));
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingMarkedUserUpgrade));
            Assert.That(state.UserEntryPath, Is.EqualTo(W24UserEntryPath.MarkedUpgrade));
        }

        [Test]
        public void MachineFailureBypassesQa_AndC2FailureEscalates()
        {
            var state = W24WorkflowAggregator.Start(Candidate(W24CandidateId.C0, 1), W24S0aTerminalStatus.S0A_GATE_QUALIFIED);
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_FAIL, Candidate(W24CandidateId.C1, 1));
            Assert.That(state.Candidate.CandidateId, Is.EqualTo(W24CandidateId.C1));
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingMachineGate));

            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_FAIL, Candidate(W24CandidateId.C2, 1));
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_FAIL);
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.NeedsUserDecision));
            Assert.That(state.UserEntryPath, Is.EqualTo(W24UserEntryPath.MarkedUpgrade));
        }

        [Test]
        public void EvidenceInvalid_AllowsOneRecaptureThenRecordsCaptureBlockedBeforeUserDecision()
        {
            var state = QaReady();
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.EVIDENCE_INVALID);
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingRecapture));
            W24WorkflowAggregator.ApplyRecaptureCompleted(state);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.EVIDENCE_INVALID);

            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.NeedsUserDecision));
            Assert.That(state.StatusHistory, Does.Contain(W24WorkingStatus.CaptureBlocked));
            Assert.That(state.UserEntryPath, Is.EqualTo(W24UserEntryPath.MarkedUpgrade));
        }

        [Test]
        public void SecondConsecutiveContractReopen_RequiresConfirmationAndStartsNewC0Revision()
        {
            var state = QaReady();
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.CONTRACT_AMBIGUOUS);
            W24WorkflowAggregator.ApplyContractRevision(state, Candidate(W24CandidateId.C0, 2, "build-b", "capture-b"), false);
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.CONTRACT_AMBIGUOUS);

            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingUserConfirmation));
            Assert.That(state.RequiresUserConfirmation, Is.True);
            Assert.Throws<InvalidOperationException>(() => W24WorkflowAggregator.ApplyContractRevision(state, Candidate(W24CandidateId.C0, 3), false));

            W24WorkflowAggregator.ApplyContractRevision(state, Candidate(W24CandidateId.C0, 3, "build-c", "capture-c"), true);
            Assert.That(state.Candidate.ContractRevision, Is.EqualTo(3));
            Assert.That(state.Candidate.CandidateId, Is.EqualTo(W24CandidateId.C0));
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingMachineGate));
        }

        [Test]
        public void NonAmbiguousVisualQaProgress_ResetsContractAmbiguitySequence()
        {
            var state = QaReady();
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.CONTRACT_AMBIGUOUS);
            W24WorkflowAggregator.ApplyContractRevision(state, Candidate(W24CandidateId.C0, 2), false);
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.VISUAL_FAIL, Candidate(W24CandidateId.C1, 2));
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.CONTRACT_AMBIGUOUS);

            Assert.That(state.ConsecutiveContractReopens, Is.EqualTo(1));
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingDesignRevision));
        }

        [Test]
        public void CandidateTransitions_AreRecordedWithTheCandidateIdentity()
        {
            var state = W24WorkflowAggregator.Start(Candidate(W24CandidateId.C0, 1), W24S0aTerminalStatus.S0A_GATE_QUALIFIED);
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_FAIL, Candidate(W24CandidateId.C1, 1));
            var advanced = state.History.Last();
            Assert.That(advanced.Action, Is.EqualTo("candidate-advanced"));
            Assert.That(advanced.CandidateId, Is.EqualTo(W24CandidateId.C1));
            Assert.That(advanced.Status, Is.EqualTo(W24WorkingStatus.AwaitingMachineGate));

            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.CONTRACT_AMBIGUOUS);
            W24WorkflowAggregator.ApplyContractRevision(state, Candidate(W24CandidateId.C0, 2), false);
            var redesigned = state.History.Last();
            Assert.That(redesigned.Action, Is.EqualTo("contract-revision-applied"));
            Assert.That(redesigned.CandidateId, Is.EqualTo(W24CandidateId.C0));
            Assert.That(redesigned.ContractRevision, Is.EqualTo(2));
        }

        [Test]
        public void VisualUncertain_GoesOnlyToMarkedUserUpgrade()
        {
            var state = QaReady();
            W24WorkflowAggregator.ApplyVisualQaRoute(state, W24VisualQaRoute.VISUAL_UNCERTAIN);
            Assert.That(state.WorkingStatus, Is.EqualTo(W24WorkingStatus.AwaitingMarkedUserUpgrade));
            Assert.That(state.UserEntryPath, Is.EqualTo(W24UserEntryPath.MarkedUpgrade));
            Assert.That(state.Maturity, Is.EqualTo(W24MaturityLevel.L2_VisualPlaceholder));
        }

        [Test]
        public void StatusRegistry_RegistersAllGeneratedEntriesAsProvisionalWithoutVisualClaim()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var first = W24StatusRegistry.ScanProject(projectRoot);
            var second = W24StatusRegistry.ScanProject(projectRoot);

            Assert.That(first.Entries.Count, Is.GreaterThan(0), "The registry must scan the current Generated inventory instead of relying on a hand-maintained count.");
            Assert.That(first.FreezeHash, Is.EqualTo(second.FreezeHash));
            Assert.That(first.Entries.All(entry => entry.Maturity == W24MaturityLevel.L2_VisualPlaceholder), Is.True);
            Assert.That(first.Entries.All(entry => entry.WorkingStatus == W24WorkingStatus.VISUAL_PENDING), Is.True);
            Assert.That(first.Entries.All(entry => entry.HasRuntimeEntry && entry.RuntimeEntryPathIsValid && entry.RuntimeEntryExists && entry.RuntimeEntryGuidIsVerifiable && entry.RuntimeEntryHashIsVerifiable), Is.True);
            Assert.That(first.Entries.All(entry => W24StatusRegistry.IsCanonicalSha256(entry.RuntimeEntryHash) && W24StatusRegistry.IsCanonicalSha256(entry.BuildHash)), Is.True);
            Assert.That(first.Entries.All(entry => !entry.HasW24VisualQa && entry.Basis.Contains("no W24 visual-QA evidence")), Is.True);
            Assert.That(first.Entries.Select(entry => entry.EffectId).Intersect(
                    W24StatusRegistry.EffectContainerDirectories.Concat(W24StatusRegistry.SeparatelyManifestedDirectories), StringComparer.Ordinal),
                Is.Empty, "A declared grouping directory is never itself an effect registration.");
            CollectionAssert.IsSubsetOf(new[] { "w11nc_ambient_dust_volume", "w13nc_blade_tempest_ultimate_3d" }, first.Entries.Select(entry => entry.EffectId).ToArray());
        }

        [Test]
        public void StatusRegistry_DeclaredContainersAreNotEffectsAndEverythingElseStillFailsClosed()
        {
            var root = Path.Combine(Path.GetTempPath(), "w24-status-container-" + Guid.NewGuid().ToString("N"));
            var generated = Path.Combine(root, "Generated");
            var manifests = Path.Combine(root, "Manifests");
            try
            {
                Assert.That(W24StatusRegistry.EffectContainerDirectories, Is.EqualTo(new[] { "W11W13NextCandidate", "W15NextCandidate" }));
                Assert.That(W24StatusRegistry.SeparatelyManifestedDirectories, Is.EqualTo(new[] { "W17W18NextCandidate" }));
                Directory.CreateDirectory(Path.Combine(generated, "W11W13NextCandidate", "w11nc_declared_child"));
                Directory.CreateDirectory(Path.Combine(generated, "W17W18NextCandidate", "W17"));
                Directory.CreateDirectory(Path.Combine(generated, "undeclared_group", "child_effect"));
                Directory.CreateDirectory(manifests);

                var entries = W24StatusRegistry.ScanDirectories(root, generated, manifests).Entries;

                CollectionAssert.AreEqual(new[] { "undeclared_group", "w11nc_declared_child" },
                    entries.Select(entry => entry.EffectId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    "Containers register their children, the separately manifested set registers nothing, and undeclared directories stay in the scan.");
                Assert.That(entries.All(entry => entry.Maturity == W24MaturityLevel.L0_InvalidOrMissing), Is.True,
                    "A missing BuildManifest still fails closed inside a declared container and for every undeclared directory.");
                Assert.That(entries.All(entry => entry.WorkingStatus == W24WorkingStatus.None), Is.True);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void StatusRegistry_MissingAnyRequiredEntryIsL0()
        {
            var root = Path.Combine(Path.GetTempPath(), "w24-status-" + Guid.NewGuid().ToString("N"));
            var generated = Path.Combine(root, "Generated");
            var manifests = Path.Combine(root, "Manifests");
            try
            {
                Directory.CreateDirectory(Path.Combine(generated, "missing_manifest"));
                Directory.CreateDirectory(Path.Combine(generated, "missing_prefab"));
                Directory.CreateDirectory(manifests);
                File.WriteAllText(Path.Combine(manifests, "missing_prefab.manifest.json"), "{}");

                var entries = W24StatusRegistry.ScanDirectories(root, generated, manifests).Entries;
                Assert.That(entries.All(entry => entry.Maturity == W24MaturityLevel.L0_InvalidOrMissing), Is.True);
                Assert.That(entries.All(entry => entry.WorkingStatus == W24WorkingStatus.None), Is.True);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void StatusRegistry_UsesTheManifestRuntimeEntryAndRejectsPathGuidAndHashForgery()
        {
            var root = Path.Combine(Path.GetTempPath(), "w24-status-manifest-" + Guid.NewGuid().ToString("N"));
            try
            {
                var effectId = "manifest_directed";
                Directory.CreateDirectory(Path.Combine(root, "Assets", "VFX", "Generated", effectId));
                var assetPath = "Assets/VFX/Elsewhere/unexpected.prefab";
                var prefab = Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(prefab));
                File.WriteAllText(prefab, "actual prefab bytes");
                const string guid = "0123456789abcdef0123456789abcdef";
                File.WriteAllText(prefab + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
                var manifests = Path.Combine(root, "ProjectSettings", "VFXComposer", "BuildManifests");
                Directory.CreateDirectory(manifests);
                var manifest = Path.Combine(manifests, effectId + ".manifest.json");
                WriteManifest(manifest, effectId, assetPath, guid, HashFile(prefab));

                var verified = W24StatusRegistry.ScanProject(root).Entries.Single();
                Assert.That(verified.PrefabPath, Is.EqualTo(assetPath));
                Assert.That(verified.Maturity, Is.EqualTo(W24MaturityLevel.L2_VisualPlaceholder));

                WriteManifest(manifest, effectId, "Assets/../outside.prefab", guid, HashFile(prefab));
                var escaped = W24StatusRegistry.ScanProject(root).Entries.Single();
                Assert.That(escaped.Maturity, Is.EqualTo(W24MaturityLevel.L0_InvalidOrMissing));
                Assert.That(escaped.RuntimeEntryPathIsValid, Is.False);

                WriteManifest(manifest, effectId, assetPath, "fedcba9876543210fedcba9876543210", HashFile(prefab));
                var wrongGuid = W24StatusRegistry.ScanProject(root).Entries.Single();
                Assert.That(wrongGuid.Maturity, Is.EqualTo(W24MaturityLevel.L0_InvalidOrMissing));
                Assert.That(wrongGuid.RuntimeEntryGuidIsVerifiable, Is.False);

                WriteManifest(manifest, effectId, assetPath, guid, "sha256:" + new string('0', 64));
                var tampered = W24StatusRegistry.ScanProject(root).Entries.Single();
                Assert.That(tampered.Maturity, Is.EqualTo(W24MaturityLevel.L0_InvalidOrMissing));
                Assert.That(tampered.RuntimeEntryHashIsVerifiable, Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test, Explicit("Repository freeze verification must run against a clean checkout before any legacy authoring tests mutate formal assets.")]
        public void FrozenStatusDocument_MatchesTheDynamicScanCountIdsAndCanonicalHash()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var snapshot = W24StatusRegistry.ScanProject(projectRoot);
            var statusPath = Path.Combine(projectRoot, "..", "docs", "vfx-status", "s0a-provisional-status.json");
            var document = JObject.Parse(File.ReadAllText(statusPath));
            var ids = ((JArray)document["effectIds"]).Values<string>().ToArray();

            Assert.That((int)document["entryCount"], Is.EqualTo(snapshot.Entries.Count));
            Assert.That(ids, Is.EqualTo(snapshot.Entries.Select(entry => entry.EffectId).ToArray()));
            Assert.That((string)document["freezeHash"], Is.EqualTo(snapshot.FreezeHash));
            Assert.That(W24StatusRegistry.IsCanonicalSha256((string)document["freezeHash"]), Is.True);
        }

        private static W24WorkflowState QaReady()
        {
            var state = W24WorkflowAggregator.Start(Candidate(W24CandidateId.C0, 1), W24S0aTerminalStatus.S0A_GATE_QUALIFIED);
            W24WorkflowAggregator.ApplyMachineVerdict(state, W24MachineGateVerdict.MACHINE_PASS);
            return state;
        }

        private static W24CandidateIdentity Candidate(W24CandidateId id, int revision, string build = "build", string capture = "capture")
        {
            return new W24CandidateIdentity(id, revision, build + "-" + revision, capture + "-" + revision);
        }

        private static void WriteManifest(string path, string effectId, string runtimePath, string guid, string hash)
        {
            var manifest = new JObject
            {
                ["effectId"] = effectId,
                ["buildHash"] = new string('a', 64),
                ["runtimeEntry"] = new JObject { ["kind"] = "prefab", ["path"] = runtimePath, ["guid"] = guid },
                ["ownedOutputs"] = new JArray(new JObject { ["path"] = runtimePath, ["guid"] = guid, ["sha256"] = hash })
            };
            File.WriteAllText(path, manifest.ToString());
        }

        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
