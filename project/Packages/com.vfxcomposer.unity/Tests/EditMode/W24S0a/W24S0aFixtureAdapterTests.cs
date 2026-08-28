using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S0a;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode.W24S0a
{
    public sealed class W24S0aFixtureAdapterTests
    {
        [Test]
        public void OperatorVocabulary_ParsesEveryWhitelistedTargetAndRejectsUnknownOrIllegalValues()
        {
            var accepted = new Dictionary<string, string>
            {
                { "Fragments.sharedParentAngularVelocity", "180deg_per_second" }, { "Flame.steadyStateLinearDrift", "0.90_units_per_second" },
                { "Flame.loopResetDiscontinuity", "0.85_normalized_delta" }, { "Particles.stopResidualSeconds", "2.50_seconds" },
                { "Light.stopResidualSeconds", "2.50_seconds" }, { "Smoke.subjectOcclusionFraction", "0.78" },
                { "Renderer.primarySmokeSortingOrder", "inverted" }, { "StateMachine.ignitionEnabled", "false" },
                { "StateMachine.stopContinuityMode", "clear_immediate" }, { "Light.enabled", "false" },
                { "Capture.cameraScaleOffset", "scale_2.20" }, { "Capture.frameManifestIntegrity", "missing_key_frame" }
            };
            Assert.That(accepted.Count, Is.EqualTo(12));
            foreach (var pair in accepted) Assert.DoesNotThrow(() => W24S0aTypedMutation.Parse(pair.Key, pair.Value), pair.Key);
            Assert.Throws<InvalidDataException>(() => W24S0aTypedMutation.Parse("Prefab.assetPath", "Assets/escape.prefab"));
            Assert.Throws<InvalidDataException>(() => W24S0aTypedMutation.Parse("Light.enabled", "true"));
            Assert.Throws<InvalidDataException>(() => W24S0aTypedMutation.Parse("Capture.frameManifestIntegrity", "delete_all"));
        }

        [Test]
        public void ExistingReducedOperatorCommands_AreHashBoundAndConsumable()
        {
            var repositoryRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
            var commands = Directory.GetFiles(Path.Combine(repositoryRoot, "docs", "vfx-calibration", "reduced", "operator", "mutation-commands"), "*.mutation-command.json", SearchOption.TopDirectoryOnly);
            Assert.That(commands.Length, Is.GreaterThan(0));
            foreach (var path in commands)
            {
                Assert.That((string)JObject.Parse(File.ReadAllText(path))["effectId"], Is.EqualTo(W24S0aOperatorCommand.EffectId), path);
                Assert.DoesNotThrow(() => W24S0aOperatorCommand.Load(path), path);
            }
        }

        [Test]
        public void FormalBatch_UsesOnlyFixedOperatorCommandCohorts_AndAllowsGeneratedPassControls()
        {
            var reduced = W24S0aOperatorCommandSet.LoadCohort(W24S0aCalibrationCohort.Reduced);
            Assert.That(reduced.Commands.Count, Is.EqualTo(66));
            Assert.That(W24S0aOperatorCommandSet.ExpectedSampleCount(W24S0aCalibrationCohort.Full), Is.EqualTo(110));
            Assert.That(reduced.Commands.All(command => command.SourcePath.StartsWith(reduced.CommandDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(reduced.Commands.Any(command => command.IsBaselineControl), Is.True, "The generated pass portion is a zero-mutation baseline control, not an unsupported compound command.");
            Assert.That(reduced.Commands.All(command => command.IsBaselineControl || command.Mutation != null), Is.True);
            Assert.That(reduced.Commands.Select(command => command.CommandHash).Distinct().Count(), Is.EqualTo(66));

            var repositoryRoot = Directory.GetParent(Application.dataPath).Parent.FullName;
            var labels = Path.Combine(repositoryRoot, "docs", "vfx-calibration", "reduced", "operator", "calibration-labels.json");
            var ledger = Path.Combine(repositoryRoot, "docs", "vfx-calibration", "reduced", "operator", "generation-ledger.json");
            Assert.Throws<InvalidDataException>(() => W24S0aOperatorCommand.Load(labels));
            Assert.Throws<InvalidDataException>(() => W24S0aOperatorCommand.Load(ledger));
            Assert.Throws<ArgumentOutOfRangeException>(() => W24S0aOperatorCommandSet.GetCommandDirectory((W24S0aCalibrationCohort)999));
        }

        [Test]
        public void FormalBatch_RejectsArbitraryDirectories_AndNeverSilentlyResumesPartialEvidence()
        {
            var arbitrary = Path.Combine(Path.GetTempPath(), "w24-s0a-arbitrary-" + Guid.NewGuid().ToString("N") + ".mutation-command.json");
            try
            {
                File.WriteAllText(arbitrary, "{}");
                Assert.Throws<InvalidDataException>(() => W24S0aOperatorCommand.Load(arbitrary));
            }
            finally
            {
                if (File.Exists(arbitrary)) File.Delete(arbitrary);
            }

            Assert.That(W24S0aBatchCaptureRecovery.Classify(66, 0, 0), Is.EqualTo(W24S0aBatchCaptureState.Fresh));
            Assert.That(W24S0aBatchCaptureRecovery.Classify(66, 66, 66), Is.EqualTo(W24S0aBatchCaptureState.Complete));
            Assert.Throws<InvalidOperationException>(() => W24S0aBatchCaptureRecovery.Classify(66, 1, 1));
            Assert.Throws<InvalidOperationException>(() => W24S0aBatchCaptureRecovery.Classify(110, 110, 109));
        }

        [Test]
        public void CandidateDirectory_RejectsTraversalAndRemainsBelowKnownCalibrationRoot()
        {
            Assert.Throws<ArgumentException>(() => W24S0aCalibrationPaths.CandidateDirectory("../outside"));
            Assert.Throws<ArgumentException>(() => W24S0aCalibrationPaths.CandidateDirectory("nested/sample"));
            var candidate = W24S0aCalibrationPaths.CandidateDirectory("s0a-test-safe-id");
            var root = W24S0aCalibrationPaths.RootAbsolutePath;
            Assert.That(candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
        }

        [Test]
        public void FormalFixture_RejectsAnyLoadedSceneOtherThanTheSerializedAuthorityPreviewScene()
        {
            var substitute = EditorSceneManager.NewPreviewScene();
            try
            {
                Assert.Throws<InvalidOperationException>(() => W24S0aFixtureSession.Create(null, substitute));
            }
            finally
            {
                if (substitute.IsValid() && substitute.isLoaded) EditorSceneManager.ClosePreviewScene(substitute);
            }
        }

        [Test]
        public void FormalSeedProtocol_RejectsWrongCanonicalOrRobustnessSeeds()
        {
            const uint fixedSeed = 2885465331u;
            var derived = W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(fixedSeed);
            var profile = new W24CaptureProfile { CanonicalSeed = unchecked((int)fixedSeed), RobustnessSeeds = new[] { unchecked((int)derived[0]), unchecked((int)derived[1]) } };
            Assert.DoesNotThrow(() => W24S0aFormalCaptureProtocol.RequireExactSeeds(profile, fixedSeed));
            profile.CanonicalSeed = 11;
            Assert.Throws<InvalidOperationException>(() => W24S0aFormalCaptureProtocol.RequireExactSeeds(profile, fixedSeed));
            profile.CanonicalSeed = unchecked((int)fixedSeed); profile.RobustnessSeeds[1] ^= 1;
            Assert.Throws<InvalidOperationException>(() => W24S0aFormalCaptureProtocol.RequireExactSeeds(profile, fixedSeed));
        }

        [Test]
        public void FormalSemanticValidator_RejectsSelfSealedSingleFrameEvidence()
        {
            var root = Path.Combine(Path.GetTempPath(), "w24-s0a-self-sealed-" + Guid.NewGuid().ToString("N"));
            try
            {
                var command = W24S0aOperatorCommandSet.LoadCohort(W24S0aCalibrationCohort.Reduced).Commands.First();
                const string profile = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                var store = W24EvidenceStore.Create(root, command.SampleId, profile);
                var beauty = store.WriteBytes("frames/seed_1/frame_00001_beauty.png", Encoding.UTF8.GetBytes("beauty"));
                var diagnostic = store.WriteBytes("frames/seed_1/frame_00001_effect-only.png", Encoding.UTF8.GetBytes("diagnostic"));
                store.WriteText("diagnostic-pass-manifest.json", "{\"schema\":\"w24-s0a-diagnostic-pass-manifest/v1\",\"passes\":[]}");
                var metadata = "{\"candidateId\":\"" + command.SampleId + "\",\"captureProfileSha256\":\"" + profile + "\",\"frames\":[{\"beauty\":{\"file\":\"frames/seed_1/frame_00001_beauty.png\",\"sha256\":\"" + beauty + "\"},\"diagnostics\":[{\"file\":\"frames/seed_1/frame_00001_effect-only.png\",\"sha256\":\"" + diagnostic + "\"}]}]}";
                var metadataHash = store.WriteText("capture-metadata.json", metadata);
                store.Seal("{\"operatorCommandHash\":\"" + command.CommandHash + "\",\"captureToolSha256\":\"" + profile + "\",\"sourceHashesSha256\":\"" + profile + "\",\"captureMetadataSha256\":\"" + metadataHash + "\"}");
                Assert.DoesNotThrow(() => W24S0aInvalidEvidenceInjector.ValidateSealedCapture(root, command.CommandHash));
                Assert.Throws<InvalidDataException>(() => W24S0aInvalidEvidenceInjector.ValidateFormalCaptureSemantics(root, command));
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        [Test]
        public void CompletedCandidateShape_RejectsForeignRootEntry()
        {
            var command = W24S0aOperatorCommandSet.LoadCohort(W24S0aCalibrationCohort.Reduced).Commands.First(value => value.IsBaselineControl);
            var candidate = W24S0aCalibrationPaths.CandidateDirectory(command.SampleId);
            if (Directory.Exists(candidate)) Assert.Ignore("A real write-once S0a candidate already owns this command ID; this shape-only negative test must not touch it.");
            try
            {
                Directory.CreateDirectory(Path.Combine(candidate, "capture"));
                Directory.CreateDirectory(Path.Combine(candidate, "ledger"));
                File.WriteAllText(Path.Combine(candidate, "candidate-completion.json"), "placeholder");
                Directory.CreateDirectory(Path.Combine(candidate, "foreign-directory"));
                var method = typeof(W24S0aBatchCaptureRecovery).GetMethod("ValidateCandidateDirectoryShape", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(method);
                var failure = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { candidate, command }));
                Assert.That(failure.InnerException, Is.TypeOf<InvalidDataException>());
            }
            finally { DeleteOwnedCandidate(candidate); }
        }

        [Test]
        public void OfficialAssetSnapshot_RemainsVerifiableWithoutWritingFormalSources()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            if (!File.Exists(Path.Combine(projectRoot, SustainedFlameAuthoring.PrefabPath.Replace('/', Path.DirectorySeparatorChar))) || !File.Exists(Path.Combine(projectRoot, SustainedFlameAuthoring.PreviewScenePath.Replace('/', Path.DirectorySeparatorChar))) || !File.Exists(Path.Combine(projectRoot, SustainedFlameAuthoring.ManifestPath.Replace('/', Path.DirectorySeparatorChar))))
                Assert.Ignore("S0a formal-source hash proof resumes after the sustained_flame_3d baseline is built.");
            var snapshot = W24S0aFixtureSession.SnapshotOfficialSources();
            Assert.That(snapshot.Count, Is.EqualTo(6 + W24S0aFormalCaptureProtocol.CaptureToolRelativePaths.Length), "The formal source snapshot must cover all six effect authorities plus every frozen capture-tool source and asmdef.");
            Assert.That(snapshot.Values.All(value => value.StartsWith("sha256:", StringComparison.Ordinal) && value.Length == 71), Is.True);
            Assert.That(W24S0aFixtureSession.VerifyOfficialSourcesUnchanged(snapshot), Is.True);
        }

        [Test]
        public void InvalidEvidence_IsAuditedOnlyOnDerivedPostCaptureCopy()
        {
            var candidate = W24S0aCalibrationPaths.CandidateDirectory("s0a-test-invalid-" + Guid.NewGuid().ToString("N"));
            var capture = Path.Combine(candidate, "capture");
            var entries = new List<string>();
            try
            {
                var commandHash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
                const string profileHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                var store = W24EvidenceStore.Create(capture, "test-candidate", profileHash);
                var sourceFrame = "frames/seed_1/frame_00001_beauty.png";
                var diagnosticFrame = "frames/seed_1/frame_00001_effect-only.png";
                var sourceHash = store.WriteBytes(sourceFrame, System.Text.Encoding.UTF8.GetBytes("captured-frame"));
                var diagnosticHash = store.WriteBytes(diagnosticFrame, System.Text.Encoding.UTF8.GetBytes("diagnostic-frame"));
                store.WriteText("diagnostic-pass-manifest.json", "{\"schema\":\"w24-s0a-diagnostic-pass-manifest/v1\",\"passes\":[]}");
                var metadataHash = store.WriteText("capture-metadata.json", "{\"candidateId\":\"test-candidate\",\"captureProfileSha256\":\"" + profileHash + "\",\"frames\":[{\"beauty\":{\"file\":\"" + sourceFrame + "\",\"sha256\":\"" + sourceHash + "\"},\"diagnostics\":[{\"file\":\"" + diagnosticFrame + "\",\"sha256\":\"" + diagnosticHash + "\"}]}]}");
                store.Seal("{\"operatorCommandHash\":\"" + commandHash + "\",\"captureToolSha256\":\"" + profileHash + "\",\"sourceHashesSha256\":\"" + profileHash + "\",\"captureMetadataSha256\":\"" + metadataHash + "\"}");
                Assert.DoesNotThrow(() => W24S0aInvalidEvidenceInjector.ValidateSealedCapture(capture, commandHash), "The immutable source capture must have a complete artifact seal before post-capture derivation.");
                W24S0aInvalidEvidenceInjector.Inject(candidate, capture, W24S0aTypedMutation.Parse("Capture.frameManifestIntegrity", "missing_key_frame"), commandHash, (kind, details) => entries.Add(kind + ":" + details["postCaptureOnly"]));
                Assert.That(File.Exists(Path.Combine(capture, sourceFrame.Replace('/', Path.DirectorySeparatorChar))), Is.True, "The actual recorder output is immutable input to the injected evidence copy.");
                Assert.That(Directory.GetFiles(Path.Combine(candidate, "invalid-evidence"), "*_beauty.png", SearchOption.AllDirectories), Is.Empty);
                CollectionAssert.AreEqual(new[] { "invalid-evidence-injected:True" }, entries);
            }
            finally { DeleteOwnedCandidate(candidate); }
        }

        [Test]
        public void CleanupGuard_IsIdempotent()
        {
            var guard = new W24S0aFixtureCleanupGate();
            Assert.That(guard.TryEnter(), Is.True);
            Assert.That(guard.TryEnter(), Is.False);
            Assert.That(guard.IsEntered, Is.True);
        }

        [Test]
        public void FailureRecovery_PreservesPrimaryErrorWhenCleanupAlsoFails()
        {
            var primary = new InvalidOperationException("primary-capture-failure");
            W24S0aFailureRecovery.CleanupWithoutMasking(primary, () => { throw new IOException("cleanup-failure"); });
            Assert.That(primary.Message, Is.EqualTo("primary-capture-failure"));
            StringAssert.Contains("cleanup-failure", (string)primary.Data[W24S0aFailureRecovery.CleanupFailureDataKey]);
        }

        [Test]
        public void StrongAndBoundaryValues_HaveDistinctReadableFixtureConfiguration()
        {
            var root = new GameObject("fixture-readback");
            try
            {
                var obviousLoop = root.AddComponent<W24S0aFixtureMotion>(); obviousLoop.Configure(W24S0aFixtureMotion.Mode.LoopReset, W24S0aTypedMutation.Parse("Flame.loopResetDiscontinuity", "0.85_normalized_delta").Number);
                var boundaryLoop = root.AddComponent<W24S0aFixtureMotion>(); boundaryLoop.Configure(W24S0aFixtureMotion.Mode.LoopReset, W24S0aTypedMutation.Parse("Flame.loopResetDiscontinuity", "0.14_normalized_delta").Number);
                Assert.That(obviousLoop.Magnitude, Is.GreaterThan(boundaryLoop.Magnitude));
                obviousLoop.ApplyObservedLoopBoundary(); Assert.That(obviousLoop.LoopPhase, Is.True);
                obviousLoop.ApplyObservedLoopBoundary(); Assert.That(obviousLoop.LoopPhase, Is.False, "Each successive natural loop boundary restores origin instead of repeating one displacement.");

                var firstObject = new GameObject("residual-particle-a"); firstObject.transform.SetParent(root.transform, false);
                var secondObject = new GameObject("residual-particle-b"); secondObject.transform.SetParent(root.transform, false);
                var first = firstObject.AddComponent<ParticleSystem>(); var second = secondObject.AddComponent<ParticleSystem>();
                var obviousResidual = root.AddComponent<W24S0aParticleResidualConfiguration>(); obviousResidual.Configure(W24S0aTypedMutation.Parse("Particles.stopResidualSeconds", "2.50_seconds").Number, new[] { first, second });
                var boundaryResidual = root.AddComponent<W24S0aParticleResidualConfiguration>(); boundaryResidual.Configure(W24S0aTypedMutation.Parse("Particles.stopResidualSeconds", "0.18_seconds").Number, new[] { first, second });
                Assert.That(obviousResidual.TargetSeconds, Is.GreaterThan(boundaryResidual.TargetSeconds));
                Assert.That(obviousResidual.ControlledSystemCount, Is.EqualTo(2));

                var behaviour = root.AddComponent<W24S0aFixtureBehaviour>();
                behaviour.ConfigureIgnition(null, null, W24S0aTypedMutation.Parse("StateMachine.ignitionEnabled", "delay_0.42_seconds").Number);
                Assert.That(behaviour.ConfiguredIgnitionDelay, Is.EqualTo(.42f));
                behaviour.ConfigureLightResidual(null, null, W24S0aTypedMutation.Parse("Light.stopResidualSeconds", "2.50_seconds").Number);
                Assert.That(behaviour.ConfiguredLightResidualSeconds, Is.EqualTo(2.5f));
                var boundaryLight = root.AddComponent<W24S0aFixtureBehaviour>(); boundaryLight.ConfigureLightResidual(null, null, W24S0aTypedMutation.Parse("Light.stopResidualSeconds", "0.18_seconds").Number);
                Assert.That(behaviour.ConfiguredLightResidualSeconds, Is.GreaterThan(boundaryLight.ConfiguredLightResidualSeconds));
                var disabledIgnition = root.AddComponent<W24S0aFixtureBehaviour>(); disabledIgnition.ConfigureIgnition(null, null, -1f);
                Assert.That(disabledIgnition.ConfiguredIgnitionDelay, Is.LessThan(0f), "False ignition is distinct from the delayed 0.42-second release.");

                var sorting = root.AddComponent<W24S0aSortingConfiguration>(); sorting.Configure(7, 44, 7, 43, "near_equal");
                Assert.That(sorting.SmokeLayerId, Is.EqualTo(sorting.PrimaryLayerId));
                Assert.That(sorting.SmokeOrder, Is.EqualTo(sorting.PrimaryOrder - 1));
                var inverted = root.AddComponent<W24S0aSortingConfiguration>(); inverted.Configure(7, 44, 7, 45, "inverted");
                Assert.That(inverted.SmokeOrder, Is.GreaterThan(inverted.PrimaryOrder));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Ledger_VerifiesHashChainAndRejectsTampering()
        {
            var candidate = W24S0aCalibrationPaths.CandidateDirectory("s0a-test-ledger-" + Guid.NewGuid().ToString("N"));
            var ledger = Path.Combine(candidate, "ledger");
            try
            {
                WriteLedgerEntry(ledger, 0, "created", new JObject { ["value"] = "one" }, null);
                WriteLedgerEntry(ledger, 1, "cleanup", new JObject { ["value"] = "two" }, ReadEntryHash(ledger, "0000-created.json"));
                Assert.DoesNotThrow(() => W24S0aFixtureLedger.VerifyDirectory(ledger));
                File.WriteAllText(Path.Combine(ledger, "foreign.txt"), "must invalidate completed lifecycle evidence");
                Assert.Throws<InvalidDataException>(() => W24S0aFixtureLedger.VerifyDirectory(ledger));
                File.Delete(Path.Combine(ledger, "foreign.txt"));
                Assert.Throws<ArgumentException>(() => W24S0aFixtureLedger.Append(ledger, 2, "../escape", new JObject(), ReadEntryHash(ledger, "0001-cleanup.json")));
                Assert.Throws<ArgumentException>(() => W24S0aFixtureLedger.Append(ledger, 2, "next", new JObject(), "not-a-canonical-hash"));
                var first = Path.Combine(ledger, "0000-created.json"); File.SetAttributes(first, FileAttributes.Normal); File.AppendAllText(first, " ");
                Assert.Throws<InvalidDataException>(() => W24S0aFixtureLedger.VerifyDirectory(ledger));
            }
            finally { DeleteOwnedCandidate(candidate); }
        }

        [Test]
        public void FormalCaptureSource_UsesBatchSafeNaturalPlayerLoopTokens()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(projectRoot, "Packages", "com.vfxcomposer.unity", "Tests", "PlayMode", "W24S0aFormal", "W24S0aFormalCalibrationCaptureTests.cs");
            var source = File.ReadAllText(path, new UTF8Encoding(false, true));
            Assert.That(source, Does.Contain("yield return null;"));
            Assert.That(source, Does.Contain("session.ObserveCompletedPlayerLoopFrame();"));
            Assert.That(source, Does.Not.Contain("new WaitForEndOfFrame()"), "Editor batchmode can stall forever on WaitForEndOfFrame; the formal LateUpdate token is the actual natural-frame authority.");

            var proxyPath = Path.Combine(projectRoot, "Packages", "com.vfxcomposer.unity", "Tests", "PlayMode", "W24S0aFormalRuntime", "W24S0aFormalPlayModeProxyTests.cs");
            var proxy = File.ReadAllText(proxyPath, new UTF8Encoding(false, true));
            Assert.That(proxy, Does.Contain("[Timeout(60 * 60 * 1000)]"), "The fixed 66/110 natural-frame cohorts must not inherit Unity Test Framework's three-minute default timeout.");
        }

        [Test]
        public void SealedEvidenceValidator_RejectsReadOnlyMetadataWithForgedLockBinding()
        {
            var candidate = W24S0aCalibrationPaths.CandidateDirectory("s0a-test-forged-" + Guid.NewGuid().ToString("N"));
            var capture = Path.Combine(candidate, "capture");
            try
            {
                Directory.CreateDirectory(capture);
                File.WriteAllText(Path.Combine(capture, "evidence-lock.json"), "{\"candidateId\":\"wrong\",\"captureProfileSha256\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}");
                var metadata = Path.Combine(capture, "capture-metadata.json"); File.WriteAllText(metadata, "{\"candidateId\":\"right\",\"captureProfileSha256\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"frames\":[]}"); File.SetAttributes(metadata, FileAttributes.ReadOnly);
                Assert.Throws<InvalidOperationException>(() => W24S0aInvalidEvidenceInjector.ValidateSealedCapture(capture));
            }
            finally { DeleteOwnedCandidate(candidate); }
        }

        [Test]
        public void SealedEvidenceValidator_RejectsMatchingHandwrittenFramesWithoutFinalArtifactSeal()
        {
            var candidate = W24S0aCalibrationPaths.CandidateDirectory("s0a-test-forged-complete-" + Guid.NewGuid().ToString("N"));
            var capture = Path.Combine(candidate, "capture");
            try
            {
                Directory.CreateDirectory(Path.Combine(capture, "frames", "seed_1"));
                const string profile = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                const string command = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
                var beauty = Path.Combine(capture, "frames", "seed_1", "frame_00001_beauty.png"); var diagnostic = Path.Combine(capture, "frames", "seed_1", "frame_00001_effect-only.png");
                File.WriteAllText(beauty, "beauty"); File.WriteAllText(diagnostic, "diagnostic");
                File.WriteAllText(Path.Combine(capture, "evidence-lock.json"), "{\"candidateId\":\"candidate\",\"captureProfileSha256\":\"" + profile + "\"}");
                var metadata = Path.Combine(capture, "capture-metadata.json");
                File.WriteAllText(metadata, "{\"candidateId\":\"candidate\",\"captureProfileSha256\":\"" + profile + "\",\"frames\":[{\"beauty\":{\"file\":\"frames/seed_1/frame_00001_beauty.png\",\"sha256\":\"" + Hash(beauty) + "\"},\"diagnostics\":[{\"file\":\"frames/seed_1/frame_00001_effect-only.png\",\"sha256\":\"" + Hash(diagnostic) + "\"}]}]}");
                File.SetAttributes(metadata, File.GetAttributes(metadata) | FileAttributes.ReadOnly);
                Assert.Throws<InvalidOperationException>(() => W24S0aInvalidEvidenceInjector.ValidateSealedCapture(capture, command));
            }
            finally { DeleteOwnedCandidate(candidate); }
        }

        [Test]
        public void SealedEvidenceValidator_RejectsArtifactAddedAfterFinalSeal()
        {
            var candidate = W24S0aCalibrationPaths.CandidateDirectory("s0a-test-extra-artifact-" + Guid.NewGuid().ToString("N"));
            var capture = Path.Combine(candidate, "capture");
            try
            {
                const string profile = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                const string command = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
                var store = W24EvidenceStore.Create(capture, "candidate", profile);
                store.WriteText("capture-metadata.json", "{\"candidateId\":\"candidate\",\"captureProfileSha256\":\"" + profile + "\",\"frames\":[]}");
                var metadataHash = Hash(Path.Combine(capture, "capture-metadata.json"));
                store.Seal("{\"operatorCommandHash\":\"" + command + "\",\"captureToolSha256\":\"" + profile + "\",\"sourceHashesSha256\":\"" + profile + "\",\"captureMetadataSha256\":\"" + metadataHash + "\"}");
                var extra = Path.Combine(capture, "unexpected.txt");
                File.WriteAllText(extra, "foreign post-seal mutation");
                Assert.Throws<InvalidDataException>(() => W24S0aInvalidEvidenceInjector.ValidateSealedCapture(capture, command));
            }
            finally { DeleteOwnedCandidate(candidate); }
        }

        [Test]
        public void EvidenceStore_FinalSealIndexesNestedArtifactsWithCanonicalForwardSlashes()
        {
            var root = Path.Combine(Path.GetTempPath(), "w24-s0a-canonical-seal-" + Guid.NewGuid().ToString("N"));
            try
            {
                const string profile = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                const string command = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
                var store = W24EvidenceStore.Create(root, "candidate", profile);
                var beauty = store.WriteBytes(@"frames\seed_1\frame_00001_beauty.png", Encoding.UTF8.GetBytes("beauty"));
                var diagnostic = store.WriteBytes(@"frames\seed_1\frame_00001_effect-only.png", Encoding.UTF8.GetBytes("diagnostic"));
                store.WriteText("diagnostic-pass-manifest.json", "{\"schema\":\"w24-s0a-diagnostic-pass-manifest/v1\",\"passes\":[]}");
                var metadata = "{\"candidateId\":\"candidate\",\"captureProfileSha256\":\"" + profile + "\",\"frames\":[{\"beauty\":{\"file\":\"frames/seed_1/frame_00001_beauty.png\",\"sha256\":\"" + beauty + "\"},\"diagnostics\":[{\"file\":\"frames/seed_1/frame_00001_effect-only.png\",\"sha256\":\"" + diagnostic + "\"}]}]}";
                var metadataHash = store.WriteText("capture-metadata.json", metadata);
                store.Seal("{\"operatorCommandHash\":\"" + command + "\",\"captureToolSha256\":\"" + profile + "\",\"sourceHashesSha256\":\"" + profile + "\",\"captureMetadataSha256\":\"" + metadataHash + "\"}");

                var seal = JObject.Parse(File.ReadAllText(Path.Combine(root, "evidence-seal.json"), Encoding.UTF8));
                var files = ((JArray)seal["artifacts"]).OfType<JObject>().Select(item => (string)item["file"]).ToArray();
                Assert.That(files, Does.Contain("frames/seed_1/frame_00001_beauty.png"));
                Assert.That(files, Does.Contain("frames/seed_1/frame_00001_effect-only.png"));
                Assert.That(files.All(path => path != null && path.IndexOf('\\') < 0), Is.True, "Seal protocol paths must remain host-independent even on Windows.");
                Assert.DoesNotThrow(() => W24S0aInvalidEvidenceInjector.ValidateSealedCapture(root, command));
            }
            finally { DeleteTemporaryDirectory(root); }
        }

        private static void DeleteOwnedCandidate(string candidate)
        {
            if (!Directory.Exists(candidate)) return;
            foreach (var file in Directory.GetFiles(candidate, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(candidate, true);
        }

        private static void DeleteTemporaryDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(directory, true);
        }

        private static string ReadEntryHash(string directory, string filename) { return (string)JObject.Parse(File.ReadAllText(Path.Combine(directory, filename)))["entryHash"]; }

        private static string WriteLedgerEntry(string directory, int sequence, string kind, JObject details, string previousEntryHash)
        {
            var entry = new JObject
            {
                ["schema"] = "w24-s0a-fixture-ledger/v2",
                ["sequence"] = sequence,
                ["kind"] = kind,
                ["details"] = details,
                // Preserve the exact JSON string token even when it is date-shaped.  Sequence 1
                // deliberately ends in a fractional zero; permissive Json.NET date coercion used
                // to trim that zero and invalidate an otherwise correct ledger hash chain.
                ["recordedUtc"] = sequence == 0 ? "2026-08-25T06:40:21.4532625Z" : "2026-08-25T06:40:21.4681870Z",
                ["previousEntryHash"] = previousEntryHash == null ? JValue.CreateNull() : new JValue(previousEntryHash)
            };
            entry["entryHash"] = W24S0aIntegrity.CanonicalHash(entry, "entryHash");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, sequence.ToString("D4") + "-" + kind + ".json");
            File.WriteAllText(path, entry.ToString(Newtonsoft.Json.Formatting.None), new UTF8Encoding(false));
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            return (string)entry["entryHash"];
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }
    }
}
