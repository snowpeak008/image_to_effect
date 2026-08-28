using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.W24.S3;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Editor.W24
{
    /// <summary>
    /// Narrow, non-interactive entry points for the two W24 first-formal authoring batches.
    /// They create only Runtime Entry, Preview Scene, Manifest and evidence-free C0 identity
    /// records. Capture, Visual QA, L3 and L4 have separate, later entry points by design.
    /// </summary>
    public static class W24FormalBatchAuthoringEntrypoints
    {
        // These jobs author assets but do not render or capture. Their -nographics compatibility
        // is a design expectation that must be proven by the isolated smoke, while the later
        // recorder remains unconditionally graphics-backed.
        public const bool RequiresGraphicsDevice = false;

        /// <summary>
        /// Unity -executeMethod entry point. Creates the S0b sustained-flame formal assets in an
        /// isolated batch project, then verifies the exact manifest/C0 identity binding.
        /// </summary>
        public static void BuildS0bFirstFormalAssets()
        {
            RequireBatchMode(nameof(BuildS0bFirstFormalAssets));
            RequireIsolatedShadowProject();
            SustainedFlameAuthoring.BuildAssetsAndPreview();
        }

        /// <summary>
        /// Unity -executeMethod entry point. Creates all three S3 formal assets in an isolated
        /// batch project, then verifies each exact manifest/C0 identity binding.
        /// </summary>
        public static void BuildS3FirstFormalAssets()
        {
            RequireBatchMode(nameof(BuildS3FirstFormalAssets));
            RequireIsolatedShadowProject();
            W24S3BaselineAuthoring.BuildAll();
        }

        /// <summary>Creates the shared Forward renderer only in an isolated shadow project.</summary>
        public static void ProvisionPreviewRendererInfrastructure()
        {
            RequireBatchMode(nameof(ProvisionPreviewRendererInfrastructure));
            RequireIsolatedShadowProject();
            W24PreviewRendererInfrastructure.ProvisionInIsolatedShadow();
        }

        // Public and read-only so CI can validate a completed batch independently. This is not
        // an executeMethod authoring command and cannot create a capture, verdict, L3 or L4.
        public static void VerifyS0bFormalOutputs()
        {
            VerifyFormalOutput(new FormalTarget(
                SustainedFlameAuthoring.EffectId,
                SustainedFlameAuthoring.PrefabPath,
                SustainedFlameAuthoring.PreviewScenePath,
                SustainedFlameAuthoring.OutputFolder));
        }

        // Public and read-only so CI can validate all S3 records without replaying authoring.
        public static void VerifyS3FormalOutputs()
        {
            VerifyFormalOutput(new FormalTarget(
                W24S3BaselineAuthoring.ProjectileId,
                W24S3BaselineAuthoring.ProjectilePrefab,
                W24S3BaselineAuthoring.ProjectilePreview,
                W24S3BaselineAuthoring.ProjectileOutputFolder));
            VerifyFormalOutput(new FormalTarget(
                W24S3BaselineAuthoring.BindingId,
                W24S3BaselineAuthoring.BindingPrefab,
                W24S3BaselineAuthoring.BindingPreview,
                W24S3BaselineAuthoring.BindingOutputFolder));
            VerifyFormalOutput(new FormalTarget(
                W24S3BaselineAuthoring.LightingId,
                W24S3BaselineAuthoring.LightingPrefab,
                W24S3BaselineAuthoring.LightingPreview,
                W24S3BaselineAuthoring.LightingOutputFolder));
        }

        private static void RequireBatchMode(string command)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("W24 formal authoring is batch-only. Run Unity in an isolated shadow project with -batchmode -executeMethod VFXComposer.Editor.W24.W24FormalBatchAuthoringEntrypoints." + command + ". The interactive Editor was left unchanged.");
        }

        private static void RequireIsolatedShadowProject()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            ValidateIsolatedShadowProject(
                projectRoot,
                Environment.GetEnvironmentVariable("VFX_W24_SHADOW_PROJECT_ROOT"),
                Environment.GetEnvironmentVariable("VFX_W24_CANONICAL_PROJECT_ROOT"),
                Path.GetTempPath());
        }

        // A pure seam for EditMode reflection tests and for controlled runners before Unity is
        // launched. It deliberately trusts neither a missing environment variable nor a caller
        // merely claiming that a normal working copy is a shadow.
        internal static void ValidateIsolatedShadowProject(string currentProjectRoot, string declaredShadowProjectRoot, string canonicalProjectRoot, string temporaryRoot)
        {
            var current = CanonicalDirectory(currentProjectRoot, "current Unity project root", false);
            var temporary = CanonicalDirectory(temporaryRoot, "temporary root", true);
            if (!IsDescendantOf(temporary, current))
                throw new InvalidOperationException("W24 formal authoring requires the current Unity project root to be a descendant of Path.GetTempPath().");

            if (string.IsNullOrWhiteSpace(declaredShadowProjectRoot))
                throw new InvalidOperationException("W24 formal authoring requires VFX_W24_SHADOW_PROJECT_ROOT.");
            var declared = CanonicalDirectory(declaredShadowProjectRoot, "VFX_W24_SHADOW_PROJECT_ROOT", false);
            if (!string.Equals(current, declared, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("VFX_W24_SHADOW_PROJECT_ROOT must exactly equal the current Unity project root after Path.GetFullPath normalization.");

            if (string.IsNullOrWhiteSpace(canonicalProjectRoot))
                throw new InvalidOperationException("W24 formal authoring requires VFX_W24_CANONICAL_PROJECT_ROOT.");
            var canonical = CanonicalDirectory(canonicalProjectRoot, "VFX_W24_CANONICAL_PROJECT_ROOT", true);
            if (string.Equals(current, canonical, StringComparison.OrdinalIgnoreCase) || IsDescendantOrSame(canonical, current))
                throw new InvalidOperationException("W24 formal authoring refuses the canonical project or any descendant of VFX_W24_CANONICAL_PROJECT_ROOT.");
        }

        internal static void VerifyFormalOutput(string effectId, string prefabPath, string previewScenePath, string outputFolder)
        {
            VerifyFormalOutput(new FormalTarget(effectId, prefabPath, previewScenePath, outputFolder));
        }

        private static void VerifyFormalOutput(FormalTarget expected)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var repositoryRoot = Directory.GetParent(projectRoot).FullName;
            var prefabAbsolute = ProjectAbsolute(projectRoot, expected.PrefabPath);
            var previewAbsolute = ProjectAbsolute(projectRoot, expected.PreviewScenePath);
            var manifestAbsolute = VfxProjectRules.ManifestAbsolutePath(expected.EffectId);
            var candidateRoot = Path.Combine(repositoryRoot, "docs", "vfx-candidates", expected.EffectId, "C0");
            var receiptAbsolute = Path.Combine(candidateRoot, "candidate-receipt.json");
            var candidateContractAbsolute = Path.Combine(candidateRoot, "design-contract.json");
            var candidateTraceAbsolute = Path.Combine(candidateRoot, "implementation-trace.json");
            var snapshotAbsolute = Path.Combine(candidateRoot, "bootstrap-manifest.json");

            RequireFile(prefabAbsolute, expected.EffectId + " Runtime Entry");
            RequireFile(previewAbsolute, expected.EffectId + " Preview Scene");
            RequireFile(manifestAbsolute, expected.EffectId + " production Manifest");
            RequireFile(receiptAbsolute, expected.EffectId + " C0 receipt");
            RequireFile(candidateContractAbsolute, expected.EffectId + " C0 Contract");
            RequireFile(candidateTraceAbsolute, expected.EffectId + " C0 Trace");
            RequireFile(snapshotAbsolute, expected.EffectId + " C0 Manifest snapshot");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(expected.PrefabPath);
            if (prefab == null) throw new InvalidDataException(expected.EffectId + " Runtime Entry is not an importable Prefab: " + expected.PrefabPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(expected.PreviewScenePath) == null)
                throw new InvalidDataException(expected.EffectId + " Preview Scene is not an importable Scene asset: " + expected.PreviewScenePath);
            var prefabGuid = AssetDatabase.AssetPathToGUID(expected.PrefabPath);
            if (!IsGuid(prefabGuid)) throw new InvalidDataException(expected.EffectId + " Runtime Entry has no stable Unity GUID.");

            var manifest = ReadJson(manifestAbsolute, expected.EffectId + " production Manifest");
            var receipt = ReadJson(receiptAbsolute, expected.EffectId + " C0 receipt");
            var contract = ReadJson(candidateContractAbsolute, expected.EffectId + " C0 Contract");
            var trace = ReadJson(candidateTraceAbsolute, expected.EffectId + " C0 Trace");
            var snapshot = ReadJson(snapshotAbsolute, expected.EffectId + " C0 Manifest snapshot");
            var runtime = manifest["runtimeEntry"] as JObject;
            string ownedOutputError;
            if (!W24S5ProductionGate.VerifyOwnedOutputManifest(manifest, expected.EffectId, expected.PrefabPath, expected.OutputFolder, out ownedOutputError))
                throw new InvalidDataException(expected.EffectId + " Manifest owned-output verification failed: " + ownedOutputError);

            RequireEqual((string)manifest["effectId"], expected.EffectId, expected.EffectId + " manifest effectId");
            RequireEqual((string)runtime?["path"], expected.PrefabPath, expected.EffectId + " manifest Runtime Entry path");
            RequireEqual((string)runtime?["guid"], prefabGuid, expected.EffectId + " manifest Runtime Entry GUID");
            RequireEqual((string)(manifest["formalProduction"] as JObject)?["visualStatus"], "VISUAL_PENDING", expected.EffectId + " manifest visual status");
            RequireEqual((string)(manifest["formalProduction"] as JObject)?["admissionPhase"], "PRE_C0_FIRST_FORMAL_BUILD", expected.EffectId + " manifest admission phase");

            RequireEqual((string)receipt["candidateVersion"], "w24-candidate/1.0", expected.EffectId + " receipt version");
            RequireEqual((string)receipt["candidateId"], "C0", expected.EffectId + " receipt candidate");
            RequireEqual((string)receipt["candidateStatus"], "C0_CAPTURE_PENDING", expected.EffectId + " receipt status");
            RequireEqual((string)receipt["visualStatus"], "VISUAL_PENDING", expected.EffectId + " receipt visual status");
            RequireEqual((string)receipt["effectId"], expected.EffectId, expected.EffectId + " receipt effectId");
            RequireEqual((string)receipt["productionManifestPath"], RelativeProjectPath(manifestAbsolute), expected.EffectId + " receipt manifest path");
            RequireEqual((string)receipt["runtimeEntryPath"], expected.PrefabPath, expected.EffectId + " receipt Runtime Entry path");
            RequireEqual((string)receipt["runtimeEntryGuid"], prefabGuid, expected.EffectId + " receipt Runtime Entry GUID");
            RequireEqual((string)receipt["previewScenePath"], expected.PreviewScenePath, expected.EffectId + " receipt Preview Scene path");
            RequireEqual((string)receipt["previewSceneHash"], HashFile(previewAbsolute), expected.EffectId + " receipt Preview Scene hash");
            RequireEqual((string)receipt["bootstrapManifestSnapshotFileHash"], HashFile(snapshotAbsolute), expected.EffectId + " receipt Manifest snapshot hash");
            RequireEqual((string)receipt["contractFileHash"], HashFile(candidateContractAbsolute), expected.EffectId + " receipt C0 Contract hash");
            RequireEqual((string)receipt["traceFileHash"], HashFile(candidateTraceAbsolute), expected.EffectId + " receipt C0 Trace hash");
            RequireEqual((string)receipt["buildHash"], "sha256:" + (string)manifest["buildHash"], expected.EffectId + " receipt build hash");

            if (!JToken.DeepEquals(receipt["ownedOutputs"], manifest["ownedOutputs"]))
                throw new InvalidDataException(expected.EffectId + " C0 receipt does not bind the exact Manifest owned-output records.");
            if (!JToken.DeepEquals(receipt["ownedOutputs"], snapshot["ownedOutputs"]))
                throw new InvalidDataException(expected.EffectId + " C0 receipt does not bind the exact frozen Manifest snapshot outputs.");
            RequireEqual((string)trace["traceStatus"], "C0_CAPTURE_PENDING", expected.EffectId + " C0 Trace status");
            RequireEqual((string)trace["runtimeEntryAssetPath"], expected.PrefabPath, expected.EffectId + " C0 Trace Runtime Entry path");
            RequireEqual((string)trace["runtimeEntryGuid"], prefabGuid, expected.EffectId + " C0 Trace Runtime Entry GUID");
            RequireEqual((string)((contract["extensions"] as JObject)?["candidateStatus"]), "C0_CAPTURE_PENDING", expected.EffectId + " C0 Contract status");
            RequireEqual((string)((contract["extensions"] as JObject)?["visualStatus"]), "VISUAL_PENDING", expected.EffectId + " C0 Contract visual status");
            RequireEqual((string)((contract["captureProfile"] as JObject)?["sceneSerializedReference"]), expected.PreviewScenePath, expected.EffectId + " C0 Contract Preview Scene path");
            RequireEqual((string)((contract["captureProfile"] as JObject)?["sceneHash"]), HashFile(previewAbsolute), expected.EffectId + " C0 Contract Preview Scene hash");

            // C0 is identity-only. These checks make the authoring command fail rather than
            // accidentally presenting an evidence/QA/signoff result as a successful build.
            if (HasEvidence(trace)) throw new InvalidDataException(expected.EffectId + " C0 Trace must not contain capture or QA evidence.");
        }

        private static bool HasEvidence(JObject trace)
        {
            foreach (var requirement in (trace["requirementTraces"] as JArray ?? new JArray()).OfType<JObject>())
            {
                if ((requirement["authorityEvidence"] as JArray)?.Any() == true || (requirement["crossEvidence"] as JArray)?.Any() == true)
                    return true;
            }
            return false;
        }

        private static JObject ReadJson(string absolutePath, string label)
        {
            try { return JObject.Parse(File.ReadAllText(absolutePath), new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); }
            catch (Exception exception) { throw new InvalidDataException("Could not parse " + label + ": " + absolutePath, exception); }
        }

        private static string RelativeProjectPath(string absolutePath)
        {
            var projectRoot = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var canonical = Path.GetFullPath(absolutePath);
            if (!canonical.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Formal Manifest path escapes the project.");
            return canonical.Substring(projectRoot.Length).Replace('\\', '/');
        }

        private static string ProjectAbsolute(string projectRoot, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) || assetPath.IndexOf("..", StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("Unsafe formal Asset path: " + assetPath);
            var root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var absolute = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Formal Asset path escaped the project: " + assetPath);
            return absolute;
        }

        private static string CanonicalDirectory(string path, string label, bool mustExist)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException(label + " is required.");
            string full;
            try { full = Path.GetFullPath(path); }
            catch (Exception exception) { throw new InvalidOperationException(label + " is not a valid path.", exception); }
            if (mustExist && !Directory.Exists(full)) throw new InvalidOperationException(label + " must be an existing directory: " + full);
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsDescendantOf(string parent, string child)
        {
            return !string.Equals(parent, child, StringComparison.OrdinalIgnoreCase) && IsDescendantOrSame(parent, child);
        }

        private static bool IsDescendantOrSame(string parent, string child)
        {
            return string.Equals(parent, child, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || child.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static string HashFile(string absolutePath)
        {
            using (var stream = File.OpenRead(absolutePath))
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static void RequireFile(string path, string label)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing " + label + ".", path);
        }

        private static void RequireEqual(string actual, string expected, string label)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException(label + " mismatch. Expected '" + expected + "', got '" + actual + "'.");
        }

        private static bool IsGuid(string value)
        {
            return value != null && value.Length == 32 && value.All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private sealed class FormalTarget
        {
            internal readonly string EffectId;
            internal readonly string PrefabPath;
            internal readonly string PreviewScenePath;
            internal readonly string OutputFolder;

            internal FormalTarget(string effectId, string prefabPath, string previewScenePath, string outputFolder)
            {
                EffectId = effectId;
                PrefabPath = prefabPath;
                PreviewScenePath = previewScenePath;
                OutputFolder = outputFolder;
            }
        }
    }
}
