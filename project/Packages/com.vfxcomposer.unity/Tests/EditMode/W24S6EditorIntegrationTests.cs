using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.UI;
using VFXComposer.Editor.W24.S1;

namespace VFXComposer.Tests.EditMode
{
    [TestFixture]
    public sealed class W24S6EditorIntegrationTests
    {
        private const string EffectId = "w24_s6_editor_integration_scratch";
        private const string L3EffectId = "w24_s6_editor_integration_l3_scratch";
        private const string InvalidEffectId = "w24_s6_editor_integration_duplicate";
        private const string MissingPrefabEffectId = "w24_s6_editor_integration_missing";
        private const string RecipePath = "Assets/VFX/Recipes/__w24_s6_editor_integration_scratch.json";
        private const string L3RecipePath = "Assets/VFX/Recipes/__w24_s6_editor_integration_l3_scratch.json";
        private const string InvalidRecipePath = "Assets/VFX/Recipes/__w24_s6_editor_integration_duplicate.json";
        private const string HostScenePath = "Assets/VFX/Preview/__W24S6EditorIntegrationHost.unity";
        private const string TargetScenePath = "Assets/VFX/Preview/__W24S6EditorIntegrationTarget.unity";
        private const string ManifestPath = "ProjectSettings/VFXComposer/BuildManifests/w24_s6_editor_integration_scratch.manifest.json";
        private const string L3ManifestPath = "ProjectSettings/VFXComposer/BuildManifests/w24_s6_editor_integration_l3_scratch.manifest.json";
        private const string ContractPath = "docs/vfx-contracts/__w24_s6_editor_integration_scratch.contract.json";
        private const string TracePath = "docs/vfx-traces/__w24_s6_editor_integration_scratch.trace.json";
        private const string GeneratedFolderPath = "Assets/VFX/Generated/w24_s6_editor_integration_scratch";
        private const string RuntimeEntryPath = "Assets/VFX/Generated/w24_s6_editor_integration_scratch/VFX_w24_s6_editor_integration_scratch.prefab";
        private const string L3RuntimeEntryPath = "Assets/VFX/Generated/w24_s6_editor_integration_l3_scratch/VFX_w24_s6_editor_integration_l3_scratch.prefab";
        private const string MissingPrefabRuntimeEntryPath = "Assets/VFX/Generated/w24_s6_editor_integration_missing/VFX_w24_s6_editor_integration_missing.prefab";
        private static readonly string RawBuildHash = new string('1', 64);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly string[] AllowedScratchFiles =
        {
            "project/Assets/VFX/Recipes/__w24_s6_editor_integration_scratch.json",
            "project/Assets/VFX/Recipes/__w24_s6_editor_integration_scratch.json.meta",
            "project/Assets/VFX/Recipes/__w24_s6_editor_integration_l3_scratch.json",
            "project/Assets/VFX/Recipes/__w24_s6_editor_integration_l3_scratch.json.meta",
            "project/Assets/VFX/Recipes/__w24_s6_editor_integration_duplicate.json",
            "project/Assets/VFX/Recipes/__w24_s6_editor_integration_duplicate.json.meta",
            "project/Assets/VFX/Preview/__W24S6EditorIntegrationHost.unity",
            "project/Assets/VFX/Preview/__W24S6EditorIntegrationHost.unity.meta",
            "project/Assets/VFX/Preview/__W24S6EditorIntegrationTarget.unity",
            "project/Assets/VFX/Preview/__W24S6EditorIntegrationTarget.unity.meta",
            "project/Assets/VFX/Generated/w24_s6_editor_integration_scratch.meta",
            "project/Assets/VFX/Generated/w24_s6_editor_integration_scratch/VFX_w24_s6_editor_integration_scratch.prefab",
            "project/Assets/VFX/Generated/w24_s6_editor_integration_scratch/VFX_w24_s6_editor_integration_scratch.prefab.meta",
            "project/ProjectSettings/VFXComposer/BuildManifests/w24_s6_editor_integration_scratch.manifest.json",
            "project/ProjectSettings/VFXComposer/BuildManifests/w24_s6_editor_integration_l3_scratch.manifest.json",
            "docs/vfx-contracts/__w24_s6_editor_integration_scratch.contract.json",
            "docs/vfx-traces/__w24_s6_editor_integration_scratch.trace.json"
        };

        private Dictionary<string, string> baselineSnapshot;
        private Dictionary<string, string> fixtureSnapshot;
        private string sceneHash;
        private string manifestHash;
        private string contractFileHash;
        private string traceFileHash;
        private bool ownsScratch;

        [OneTimeSetUp]
        public void CreateOneTimeScratchFixture()
        {
            RequireSafeInitialBatchRunner("before creating the Editor integration fixture");
            var existing = AllowedScratchFiles.Where(path => File.Exists(RepositoryAbsolute(path))).ToArray();
            Assert.That(existing, Is.Empty, "The integration gate will not overwrite a pre-existing scratch path: " + string.Join(", ", existing));
            Assert.That(Directory.Exists(ProjectAbsolute(GeneratedFolderPath)), Is.False, "The integration gate will not reuse a pre-existing scratch Runtime Entry folder.");
            baselineSnapshot = ProjectTreeSnapshot();
            ownsScratch = true;
            try
            {
                WriteProject(RecipePath, "{\"id\":\"" + EffectId + "\",\"name\":\"S6 editor integration scratch\",\"archetype\":\"projectile\",\"dimension\":\"3d\",\"style\":\"stylized\"}\n");
                WriteProject(L3RecipePath, "{\"id\":\"" + L3EffectId + "\",\"name\":\"S6 editor integration L3 scratch\",\"archetype\":\"projectile\",\"dimension\":\"3d\",\"style\":\"stylized\"}\n");
                WriteProject(InvalidRecipePath, "{\"id\":\"" + InvalidEffectId + "\",\"\\u0069d\":\"forged_duplicate\",\"name\":\"must not index\"}\n");
                AssetDatabase.ImportAsset(RecipePath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(L3RecipePath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(InvalidRecipePath, ImportAssetOptions.ForceSynchronousImport);

                CreateScratchHostScene();
                CreateScratchPrefab();
                CreateScratchScene(TargetScenePath);
                sceneHash = HashFile(ProjectAbsolute(TargetScenePath));

                WriteRepository(ContractPath, "{\"fixture\":\"W24 S6 Studio Contract bytes\",\"effectId\":\"" + EffectId + "\"}\n");
                WriteRepository(TracePath, "{\"fixture\":\"W24 S6 Studio Trace bytes\",\"effectId\":\"" + EffectId + "\"}\n");
                contractFileHash = HashFile(RepositoryAbsolute(ContractPath));
                traceFileHash = HashFile(RepositoryAbsolute(TracePath));

                WriteManifest(ManifestPath, EffectId, RuntimeEntryPath, "L4");
                WriteManifest(L3ManifestPath, L3EffectId, L3RuntimeEntryPath, "L3");
                manifestHash = HashFile(ProjectAbsolute(ManifestPath));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(HostScenePath), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScenePath), Is.Not.Null);
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeEntryPath), Is.Not.Null);
                fixtureSnapshot = ProjectTreeSnapshot();
                AssertOnlyAllowlistedFixtureChanges(baselineSnapshot, fixtureSnapshot);
            }
            catch
            {
                CleanupScratch();
                throw;
            }
        }

        [SetUp]
        public void OpenCleanScratchHost()
        {
            RequireOnlyCleanScratchScenes("before opening the clean scratch host");
            EditorSceneManager.OpenScene(HostScenePath, OpenSceneMode.Single);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HostScenePath));
        }

        [TearDown]
        public void RestoreCleanScratchHost()
        {
            ResetToCleanScratchHost();
        }

        [OneTimeTearDown]
        public void RemoveOneTimeScratchFixture()
        {
            CleanupScratch();
            if (baselineSnapshot != null)
                AssertTreesEqual(baselineSnapshot, ProjectTreeSnapshot(), "Scratch teardown must restore the exact pre-gate project tree.");
        }

        [Test]
        public void LibraryScanAndWindowRefresh_UseRealEditorPathsAndRejectDuplicateRecipe()
        {
            var before = ProjectTreeSnapshot();
            var items = VfxStudioLibrary.Scan();
            Assert.That(items.Any(value => value.Id == EffectId), Is.True);
            Assert.That(items.Any(value => value.Id == L3EffectId), Is.True);
            Assert.That(items.Any(value => value.Id == InvalidEffectId), Is.False, "Decoded-equivalent duplicate Recipe keys must fail closed during the real Studio scan.");
            Assert.Catch<JsonException>(() => VfxStudioDraftBuilder.FromRecipe(File.ReadAllText(ProjectAbsolute(InvalidRecipePath)), "copy", "copy", "stylized"));

            var window = ScriptableObject.CreateInstance<VfxStudioWindow>();
            try
            {
                Assert.That(window.RefreshForIntegrationTests(), Is.GreaterThan(0));
                Assert.That(window.ContainsItemForIntegrationTests(EffectId), Is.True);
                Assert.That(window.ContainsItemForIntegrationTests(L3EffectId), Is.True);
                Assert.That(window.ContainsItemForIntegrationTests(InvalidEffectId), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
            AssertTreesEqual(before, ProjectTreeSnapshot(), "Library Scan and the real window Refresh callback must be read-only.");
        }

        [TestCase(L3EffectId, "L3")]
        [TestCase(EffectId, "L4")]
        public void LibraryScan_ForgedManifestStatusFailsClosed(string effectId, string forgedStatus)
        {
            var before = ProjectTreeSnapshot();
            var item = VfxStudioLibrary.Scan().Single(value => value.Id == effectId);
            Assert.That(item.ProductionStatus, Is.EqualTo("VISUAL_PENDING"));
            Assert.That(item.Maturity, Is.EqualTo("UNASSESSED"));
            Assert.That(item.CommercialEligible, Is.False);
            Assert.That(item.StatusReasons.Any(value => value.Contains(forgedStatus + " is never trusted", StringComparison.Ordinal)), Is.True);
            AssertTreesEqual(before, ProjectTreeSnapshot(), "A forged " + forgedStatus + " display value must fail closed without writing project files.");
        }

        [TestCase("Library", "DrawLibrary")]
        [TestCase("Create", "DrawCreate")]
        [TestCase("Preview", "DrawPreview")]
        [TestCase("Patch", "DrawPatch")]
        [TestCase("Review", "DrawReview")]
        public void Tab_ResolvesItsRealEditorCallbackWithoutClaimingPixelRender(string tab, string callback)
        {
            var before = ProjectTreeSnapshot();
            CollectionAssert.AreEqual(new[] { "Library", "Create", "Preview", "Patch", "Review" }, VfxStudioWindow.IntegrationTabNamesForTests());
            var window = ScriptableObject.CreateInstance<VfxStudioWindow>();
            try
            {
                Assert.That(window.ResolveTabCallbackForIntegrationTests(tab), Is.EqualTo(callback), tab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
            AssertTreesEqual(before, ProjectTreeSnapshot(), "Resolving production Editor callbacks must not write project files.");
        }

        [Test]
        public void PreviewGuard_TamperedContractBytesRejectBeforeSceneReplacementAndWritesNothing()
        {
            var before = ProjectTreeSnapshot();
            var activeBefore = SceneManager.GetActiveScene().path;
            var replacementAsked = false;
            string status;
            var contractAbsolute = RepositoryAbsolute(ContractPath);
            var originalContract = File.ReadAllBytes(contractAbsolute);
            bool opened;
            try
            {
                File.WriteAllBytes(contractAbsolute, originalContract.Concat(new byte[] { (byte)' ' }).ToArray());
                opened = VfxStudioAuthoritativePreview.TryOpenForIntegrationTests(
                    PreviewItem(sceneHash),
                    () => { replacementAsked = true; return true; },
                    out status);
            }
            finally
            {
                File.WriteAllBytes(contractAbsolute, originalContract);
            }
            Assert.That(opened, Is.False);
            Assert.That(replacementAsked, Is.False, "Stale persisted Contract bytes must not ask to replace the current scene.");
            Assert.That(status, Does.StartWith("Preview blocked: current Contract/Trace bytes"));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(activeBefore));
            AssertTreesEqual(before, ProjectTreeSnapshot(), "A stale physical Contract binding must fail closed and restore the exact scratch fixture bytes.");
        }

        [Test]
        public void PreviewGuard_MissingRuntimePrefabRejectsBeforeSceneReplacementAndWritesNothing()
        {
            var before = ProjectTreeSnapshot();
            var activeBefore = SceneManager.GetActiveScene().path;
            var replacementAsked = false;
            var item = PreviewItem(sceneHash);
            item.Id = MissingPrefabEffectId;
            item.RuntimeEntryPath = MissingPrefabRuntimeEntryPath;
            item.PrefabPath = MissingPrefabRuntimeEntryPath;
            item.Trace.RuntimeEntryAssetPath = MissingPrefabRuntimeEntryPath;
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(MissingPrefabRuntimeEntryPath), Is.Null);
            string status;
            var opened = VfxStudioAuthoritativePreview.TryOpenForIntegrationTests(item, () => { replacementAsked = true; return true; }, out status);
            Assert.That(opened, Is.False);
            Assert.That(replacementAsked, Is.False, "A missing indexed Runtime Entry Prefab must not ask to replace the current scene.");
            Assert.That(status, Is.EqualTo("Preview blocked: indexed Runtime Entry is not the exact loadable Prefab selected by the manifest."));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(activeBefore));
            AssertTreesEqual(before, ProjectTreeSnapshot(), "A missing Runtime Entry Prefab must fail closed without writing project files.");
        }

        [Test]
        public void PreviewGuard_UserCancellationLeavesScratchHostOpenAndWritesNothing()
        {
            var before = ProjectTreeSnapshot();
            var callbackCount = 0;
            var sentinel = new GameObject("W24S6_DirtyScratchSentinel");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.True);
            string status;
            var opened = VfxStudioAuthoritativePreview.TryOpenForIntegrationTests(
                PreviewItem(sceneHash),
                () => { callbackCount++; return false; },
                out status);
            Assert.That(opened, Is.False);
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(status, Is.EqualTo("Preview cancelled: current modified scenes were not approved for replacement."));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HostScenePath));
            Assert.That(sentinel, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.True, "Cancellation must preserve the unsaved state of the scratch host Scene.");
            AssertTreesEqual(before, ProjectTreeSnapshot(), "Cancelling scene replacement must not write project files.");
        }

        [Test]
        public void PreviewGuard_PositiveOpensOnlyScratchTargetAndWritesNothing()
        {
            var before = ProjectTreeSnapshot();
            RequireOnlyCleanScratchScenes("immediately before the positive scratch Preview replacement");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HostScenePath));
            string status;
            Assert.That(VfxStudioAuthoritativePreview.TryOpen(PreviewItem(sceneHash), out status), Is.True, status);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(TargetScenePath));
            Assert.That(status, Does.StartWith("Opened exact contract scene: " + TargetScenePath));
            AssertTreesEqual(before, ProjectTreeSnapshot(), "Opening the allow-listed scratch Preview scene must not write project files.");
        }

        private VfxStudioLibraryItem PreviewItem(string expectedSceneHash)
        {
            return new VfxStudioLibraryItem
            {
                Id = EffectId,
                HasContract = true,
                HasTrace = true,
                ContractPath = ContractPath,
                ContractFileHash = contractFileHash,
                TracePath = TracePath,
                TraceFileHash = traceFileHash,
                HasRuntimeEntry = true,
                RuntimeEntryPath = RuntimeEntryPath,
                PrefabPath = RuntimeEntryPath,
                ManifestPath = ProjectAbsolute(ManifestPath),
                BuildHash = RawBuildHash,
                Contract = new VfxDesignContract
                {
                    CaptureProfile = new VfxCaptureContract
                    {
                        SceneSerializedReference = TargetScenePath,
                        SceneHash = expectedSceneHash,
                        PrefabManifestSerializedReference = ManifestPath,
                        PrefabManifestHash = manifestHash
                    }
                },
                Trace = new VfxImplementationTrace
                {
                    RuntimeEntryAssetPath = RuntimeEntryPath,
                    BuildHash = "sha256:" + RawBuildHash
                }
            };
        }

        private static void CreateScratchHostScene()
        {
            RequireSafeInitialBatchRunner("immediately before creating the scratch host Scene");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(scene, HostScenePath), Is.True, HostScenePath);
            AssetDatabase.ImportAsset(HostScenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateScratchPrefab()
        {
            var folderGuid = AssetDatabase.CreateFolder("Assets/VFX/Generated", EffectId);
            Assert.That(folderGuid, Is.Not.Empty, "Could not create the exact scratch Runtime Entry folder.");
            var sandbox = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject root = null;
            try
            {
                if (SceneManager.GetActiveScene() != sandbox)
                    Assert.That(SceneManager.SetActiveScene(sandbox), Is.True, "Could not activate the disposable Prefab-authoring Scene.");
                root = new GameObject("VFX_" + EffectId);
                if (root.scene != sandbox) SceneManager.MoveGameObjectToScene(root, sandbox);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, RuntimeEntryPath), Is.Not.Null, RuntimeEntryPath);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (sandbox.IsValid() && sandbox.isLoaded)
                    Assert.That(EditorSceneManager.CloseScene(sandbox, true), Is.True, "Could not close the disposable Prefab-authoring Scene.");
            }
            AssetDatabase.ImportAsset(RuntimeEntryPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void CreateScratchScene(string path)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True, path);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                    Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True, path);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private void CleanupScratch()
        {
            if (!ownsScratch) return;
            LeaveOnlyEmptyBatchRunner();
            DeleteAsset(RecipePath);
            DeleteAsset(L3RecipePath);
            DeleteAsset(InvalidRecipePath);
            DeleteAsset(HostScenePath);
            DeleteAsset(TargetScenePath);
            DeleteAssetFolder(GeneratedFolderPath);
            var manifest = ProjectAbsolute(ManifestPath);
            if (File.Exists(manifest)) File.Delete(manifest);
            var l3Manifest = ProjectAbsolute(L3ManifestPath);
            if (File.Exists(l3Manifest)) File.Delete(l3Manifest);
            var contract = RepositoryAbsolute(ContractPath);
            if (File.Exists(contract)) File.Delete(contract);
            var trace = RepositoryAbsolute(TracePath);
            if (File.Exists(trace)) File.Delete(trace);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ownsScratch = false;
        }

        private static void RequireSafeInitialBatchRunner(string label)
        {
            if (!Application.isBatchMode)
                Assert.Ignore("W24 S6 Editor integration is isolated-batch-only; no interactive Scene may be opened or replaced (" + label + ").");
            var loaded = LoadedScenes();
            if (loaded.Length == 0) return;
            Assert.That(loaded.Length, Is.EqualTo(1), "The batch fixture accepts at most one initial default runner Scene (" + label + ").");
            var runner = loaded[0];
            Assert.That(runner.isDirty, Is.False, "The batch fixture refuses a dirty initial runner Scene (" + label + ").");
            Assert.That(string.IsNullOrEmpty(runner.path), Is.True, "The batch fixture refuses to replace any pre-existing saved Scene (" + label + ").");
            Assert.That(runner.name == string.Empty || runner.name == "Untitled", Is.True, "The sole clean unsaved batch runner must have Unity's default empty or Untitled Scene name (" + label + ").");
        }

        private static void RequireOnlyCleanScratchScenes(string label)
        {
            Assert.That(Application.isBatchMode, Is.True, "The scratch Scene fixture cannot run in an interactive Editor (" + label + ").");
            var loaded = LoadedScenes();
            Assert.That(loaded, Is.Not.Empty, "The isolated gate requires an already-loaded scratch host/target Scene (" + label + ").");
            var unexpected = loaded.Where(scene => scene.path != HostScenePath && scene.path != TargetScenePath).Select(scene => string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path).ToArray();
            Assert.That(unexpected, Is.Empty, "After setup, the integration fixture permits only its two exact scratch Scenes (" + label + ").");
            Assert.That(loaded.Where(scene => scene.isDirty).Select(scene => scene.path).ToArray(), Is.Empty, "The integration fixture refuses to replace a dirty scratch Scene outside its exact teardown reset (" + label + ").");
        }

        private static void ResetToCleanScratchHost()
        {
            Assert.That(Application.isBatchMode, Is.True, "Scratch teardown may run only in batch mode.");
            var loaded = LoadedScenes();
            var unexpected = loaded.Where(scene => scene.path != HostScenePath && scene.path != TargetScenePath).Select(scene => string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path).ToArray();
            Assert.That(unexpected, Is.Empty, "Scratch teardown refuses to touch any Scene outside its exact host/target paths.");
            EditorSceneManager.OpenScene(HostScenePath, OpenSceneMode.Single);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(HostScenePath));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.False);
        }

        private static void LeaveOnlyEmptyBatchRunner()
        {
            Assert.That(Application.isBatchMode, Is.True, "Scratch cleanup may run only in batch mode.");
            var loaded = LoadedScenes();
            if (loaded.Length == 0) return;
            if (loaded.Length == 1 && (loaded[0].name == string.Empty || loaded[0].name == "Untitled") && string.IsNullOrEmpty(loaded[0].path) && !loaded[0].isDirty) return;
            var unexpected = loaded.Where(scene => scene.path != HostScenePath && scene.path != TargetScenePath).Select(scene => string.IsNullOrEmpty(scene.path) ? "<untitled>" : scene.path).ToArray();
            Assert.That(unexpected, Is.Empty, "Scratch cleanup refuses to replace any Scene outside its exact host/target paths.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static Scene[] LoadedScenes(){return Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(scene=>scene.IsValid()&&scene.isLoaded).ToArray();}

        private static void DeleteAsset(string path)
        {
            var absolute = ProjectAbsolute(path);
            var meta = absolute + ".meta";
            if ((File.Exists(absolute) || File.Exists(meta)) && !AssetDatabase.DeleteAsset(path))
            {
                if (File.Exists(absolute)) File.Delete(absolute);
                if (File.Exists(meta)) File.Delete(meta);
            }
        }

        private static void DeleteAssetFolder(string path)
        {
            var absolute = ProjectAbsolute(path);
            var meta = absolute + ".meta";
            if (AssetDatabase.IsValidFolder(path))
            {
                if (!AssetDatabase.DeleteAsset(path)) throw new IOException("Could not delete exact scratch Asset folder: " + path);
                return;
            }
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
            if (File.Exists(meta)) File.Delete(meta);
        }

        private static void AssertOnlyAllowlistedFixtureChanges(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            var changes = Changes(before, after);
            Assert.That(changes.Select(value => value.Path).ToArray(), Is.SubsetOf(AllowedScratchFiles), "Fixture setup changed a non-allow-listed project file:\n" + string.Join("\n", changes.Select(value => value.Description)));
            foreach (var path in AllowedScratchFiles) Assert.That(after.ContainsKey(path), Is.True, "Scratch fixture did not create its exact allow-listed file: " + path);
        }

        private static void AssertTreesEqual(Dictionary<string, string> expected, Dictionary<string, string> actual, string message)
        {
            var changes = Changes(expected, actual);
            Assert.That(changes, Is.Empty, message + "\n" + string.Join("\n", changes.Select(value => value.Description)));
        }

        private static List<TreeChange> Changes(Dictionary<string, string> before, Dictionary<string, string> after)
        {
            var changes = new List<TreeChange>();
            foreach (var path in before.Keys.Concat(after.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                string left;
                string right;
                if (!before.TryGetValue(path, out left)) changes.Add(new TreeChange(path, "+ " + path + " | " + after[path]));
                else if (!after.TryGetValue(path, out right)) changes.Add(new TreeChange(path, "- " + path + " | " + left));
                else if (!string.Equals(left, right, StringComparison.Ordinal)) changes.Add(new TreeChange(path, "~ " + path + " | " + left + " -> " + right));
            }
            return changes;
        }

        private static Dictionary<string, string> ProjectTreeSnapshot()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            AddTree(values, Path.Combine(ProjectRoot(), "Assets"), "project/Assets");
            AddTree(values, Path.Combine(ProjectRoot(), "Packages"), "project/Packages");
            AddTree(values, Path.Combine(ProjectRoot(), "ProjectSettings"), "project/ProjectSettings");
            AddTree(values, Path.Combine(RepositoryRoot(), "docs"), "docs");
            return values;
        }

        private static void AddTree(Dictionary<string, string> values, string root, string prefix)
        {
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                var relative = file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                values.Add(prefix + "/" + relative, HashFile(file));
            }
        }

        private static void WriteProject(string relative, string text)
        {
            var path = ProjectAbsolute(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, StrictUtf8);
        }

        private static void WriteRepository(string relative, string text)
        {
            var path = RepositoryAbsolute(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, StrictUtf8);
        }

        private static void WriteManifest(string path, string effectId, string runtimeEntryPath, string forgedStatus)
        {
            var manifest = new JObject
            {
                ["effectId"] = effectId,
                ["buildHash"] = RawBuildHash,
                ["runtimeEntry"] = new JObject { ["path"] = runtimeEntryPath },
                ["formalProduction"] = new JObject { ["visualStatus"] = forgedStatus }
            };
            WriteProject(path, manifest.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string RepositoryAbsolute(string snapshotPath)
        {
            return Path.GetFullPath(Path.Combine(RepositoryRoot(), snapshotPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ProjectAbsolute(string relative)
        {
            return Path.GetFullPath(Path.Combine(ProjectRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ProjectRoot() { return Directory.GetParent(Application.dataPath).FullName; }
        private static string RepositoryRoot() { return Directory.GetParent(ProjectRoot()).FullName; }

        private sealed class TreeChange
        {
            public string Path { get; }
            public string Description { get; }
            public TreeChange(string path, string description) { Path = path; Description = description; }
        }
    }
}
