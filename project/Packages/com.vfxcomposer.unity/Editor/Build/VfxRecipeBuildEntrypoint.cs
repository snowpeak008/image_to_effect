using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Build
{
    /// <summary>
    /// The restricted build execution entry point (ADR-007 §2.3): a short-lived Unity batchmode process
    /// turns one confirmed, hash-bound recipe draft into a Prefab, and writes nothing outside the closed
    /// three-member write surface.
    ///
    /// The flow is deliberately "verify everything before the compiler, verify the commit after it":
    /// recompute the confirmation hash, parse, re-check the write surface at the executor layer, run
    /// authoritative validation, land the provenance recipe, bind the approved plan, commit, then verify
    /// the committed members. Any rejection means zero writes, or a rollback to the previous good state.
    /// </summary>
    public static class VfxRecipeBuildEntrypoint
    {
        /// <summary>Absolute path of the request JSON; always outside the Unity project.</summary>
        public const string RequestPathVariable = "VFX_RECIPE_BUILD_REQUEST";

        /// <summary>Absolute path the structured result JSON is written to; always outside the Unity project.</summary>
        public const string ResultPathVariable = "VFX_RECIPE_BUILD_RESULT";

        /// <summary>Process exit code for a request that was admitted, executed and refused or failed.</summary>
        public const int StructuredFailureExitCode = 4;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// Unity -executeMethod entry point. Reads the request path and result path from the environment,
        /// executes one build and exits 0 on success or <see cref="StructuredFailureExitCode"/> on a
        /// structured refusal. Anything that prevents producing a result at all throws, which Unity turns
        /// into its own non-zero exit.
        /// </summary>
        public static void BuildConfirmedRecipe()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Restricted recipe build is batch-only. Run Unity with -batchmode -executeMethod " +
                    "VFXComposer.Editor.Build.VfxRecipeBuildEntrypoint.BuildConfirmedRecipe.");
            }

            var requestPath = Environment.GetEnvironmentVariable(RequestPathVariable);
            var resultPath = Environment.GetEnvironmentVariable(ResultPathVariable);
            if (string.IsNullOrWhiteSpace(requestPath) || string.IsNullOrWhiteSpace(resultPath))
            {
                throw new InvalidOperationException(
                    "Restricted recipe build requires " + RequestPathVariable + " and " + ResultPathVariable + ".");
            }

            string requestCode;
            string requestError;
            var request = VfxRecipeBuildJson.TryReadRequest(requestPath, out requestCode, out requestError);
            var outcome = request == null
                ? Refuse(new VfxRecipeBuildOutcome(), requestCode, requestError)
                : Execute(request);

            WriteResult(resultPath, outcome);
            EditorApplication.Exit(outcome.Succeeded ? 0 : StructuredFailureExitCode);
        }

        /// <summary>
        /// The whole build, as a seam EditMode tests drive directly. It never reads the environment and
        /// never exits the process, so both the refusal paths and one real build stay unit-testable.
        /// </summary>
        public static VfxRecipeBuildOutcome Execute(VfxRecipeBuildRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            var outcome = new VfxRecipeBuildOutcome
            {
                DraftId = request.DraftId,
                UnityVersion = Application.unityVersion,
                CompilerVersion = VfxCompiler.CompilerVersion,
                DeclaredTemplateCatalogVersion = request.DeclaredTemplateCatalogVersion
            };

            string recipeJson;
            string inputError;
            if (!TryReadStagedRecipe(request.RecipePath, out recipeJson, out inputError))
            {
                return Refuse(outcome, VfxRecipeBuildCodes.RecipeInputRejected, inputError);
            }

            // The entry point does not trust the caller: the confirmation binding is rebuilt here from the
            // bytes actually on disk, so a substitution between confirmation and build is refused.
            string actualHash;
            try { actualHash = RecipeCanonicalizer.ComputeSha256(recipeJson); }
            catch (Exception exception)
            {
                return Refuse(outcome, VfxRecipeBuildCodes.RecipeUnparseable, "Recipe could not be canonicalized: " + exception.Message);
            }

            outcome.RecipeHash = actualHash;
            if (!string.Equals(actualHash, request.ExpectedCanonicalSha256, StringComparison.Ordinal))
            {
                return Refuse(
                    outcome,
                    VfxRecipeBuildCodes.ConfirmationHashMismatch,
                    "Staged recipe does not match the confirmed canonical hash.");
            }

            var parsed = VfxDomainParser.ParseRecipe(recipeJson);
            if (parsed.Report.HasErrors || parsed.Value == null)
            {
                VfxRecipeBuildJson.CopyIssues(outcome, parsed.Report);
                return Refuse(outcome, VfxRecipeBuildCodes.RecipeUnparseable, "Recipe does not parse against Recipe v1.");
            }

            var recipe = parsed.Value;
            outcome.EffectId = recipe.Id;
            outcome.RecipeRevision = recipe.Revision;
            if (recipe.Metadata != null && !string.IsNullOrEmpty(recipe.Metadata.TemplateCatalogVersion))
            {
                outcome.DeclaredTemplateCatalogVersion = recipe.Metadata.TemplateCatalogVersion;
            }

            string idError;
            if (!VfxRecipeBuildWriteSurface.IsAcceptedEffectId(recipe.Id, out idError) ||
                !VfxRecipeBuildWriteSurface.AgreesWithProjectRules(recipe.Id))
            {
                return Refuse(outcome, VfxRecipeBuildCodes.EffectIdRejected, idError ?? "Effect id is not an accepted ownership manifest name.");
            }

            var prefabPath = VfxCompiler.PrefabPath(recipe);
            string containmentError;
            if (!VfxRecipeBuildWriteSurface.AreTargetsContained(recipe.Id, prefabPath, out containmentError))
            {
                return Refuse(outcome, VfxRecipeBuildCodes.WriteSurfaceViolation, containmentError);
            }

            outcome.PrefabPath = prefabPath;
            outcome.BuildManifestPath = VfxRecipeBuildWriteSurface.BuildManifestFor(recipe.Id);
            outcome.OwnershipManifestPath = VfxRecipeBuildWriteSurface.OwnershipManifestFor(recipe.Id);
            outcome.ProvenanceRecipePath = VfxRecipeBuildWriteSurface.ProvenanceRecipeFor(recipe.Id);

            string residueError;
            if (!VfxRecipeBuildWriteSurface.TrySweepResidue(recipe.Id, outcome.CleanedResiduePaths, out residueError))
            {
                return Refuse(outcome, VfxRecipeBuildCodes.UnrecoverableResidue, residueError);
            }

            TemplateCatalog catalog;
            try { catalog = VfxCompiler.LoadFormalCatalog(); }
            catch (Exception exception)
            {
                return Refuse(outcome, VfxRecipeBuildCodes.CatalogUnusable, "Template catalog could not be loaded: " + exception.Message);
            }

            if (catalog.Report.HasErrors)
            {
                VfxRecipeBuildJson.CopyIssues(outcome, catalog.Report);
                return Refuse(outcome, VfxRecipeBuildCodes.CatalogUnusable, "Template catalog reported errors.");
            }

            outcome.CatalogIdentityHash = ComputeCatalogIdentityHash(catalog);

            var validation = RecipeValidator.Validate(recipeJson, catalog);
            if (validation.HasErrors)
            {
                VfxRecipeBuildJson.CopyIssues(outcome, validation);
                return Refuse(outcome, VfxRecipeBuildCodes.AuthoritativeValidationFailed, "Authoritative validation refused the recipe.");
            }

            return CommitBoundPlan(request, outcome, recipe, recipeJson, catalog);
        }

        private static VfxRecipeBuildOutcome CommitBoundPlan(
            VfxRecipeBuildRequest request,
            VfxRecipeBuildOutcome outcome,
            Recipe recipe,
            string recipeJson,
            TemplateCatalog catalog)
        {
            var compiler = new VfxCompiler();
            var approved = compiler.DryRun(recipeJson, catalog);
            if (approved.IsBlocked)
            {
                VfxRecipeBuildJson.CopyIssues(outcome, approved.Report);
                return Refuse(outcome, VfxRecipeBuildCodes.DryRunBlocked, "Dry run blocked the build.");
            }

            outcome.BuildHash = approved.BuildHash;
            outcome.DryRunState = approved.Items.Count == 1 ? approved.Items[0].State.ToString() : null;
            if (approved.Items.Count != 1 ||
                !string.Equals(approved.Items[0].AssetPath, outcome.PrefabPath, StringComparison.Ordinal))
            {
                return Refuse(outcome, VfxRecipeBuildCodes.PlanCommitDrift, "Approved plan does not target the expected Runtime Entry.");
            }

            // Provenance must exist before the commit: strict enforcement resolves the build input by
            // canonical hash under the provenance root and refuses the manifest write without it (E8014).
            byte[] priorProvenance;
            bool provenanceExisted;
            string provenanceError;
            if (!TryWriteProvenance(recipe.Id, recipeJson, out priorProvenance, out provenanceExisted, out provenanceError))
            {
                return Refuse(outcome, VfxRecipeBuildCodes.ProvenanceWriteFailed, provenanceError);
            }

            try
            {
                // Recomputing the plan is the comparison that makes the commit plan-bound: the approved
                // identities must still hold at commit time, mirroring the compiler's own exact-plan check.
                var recheck = compiler.DryRun(recipeJson, catalog);
                string driftError;
                if (!MatchesApprovedPlan(approved, recheck, outcome.PrefabPath, out driftError))
                {
                    return RollBackAndRefuse(outcome, recipe.Id, priorProvenance, provenanceExisted, VfxRecipeBuildCodes.PlanCommitDrift, driftError);
                }

                var result = compiler.Build(recipeJson, catalog);
                VfxRecipeBuildJson.CopyIssues(outcome, result.Plan == null ? null : result.Plan.Report);
                if (!result.Succeeded)
                {
                    return RollBackAndRefuse(outcome, recipe.Id, priorProvenance, provenanceExisted, VfxRecipeBuildCodes.BuildFailed, "The compiler refused or failed the build.");
                }

                string commitDrift;
                if (!MatchesApprovedPlan(approved, result.Plan, outcome.PrefabPath, out commitDrift) ||
                    !string.Equals(result.PrefabPath, outcome.PrefabPath, StringComparison.Ordinal))
                {
                    return RollBackAndRefuse(outcome, recipe.Id, priorProvenance, provenanceExisted, VfxRecipeBuildCodes.PlanCommitDrift, commitDrift ?? "Committed output identity differs from the approved plan.");
                }

                string verifyError;
                if (!TryVerifyCommittedMembers(outcome, approved, out verifyError))
                {
                    return Refuse(outcome, VfxRecipeBuildCodes.CommittedArtifactsUnverified, verifyError);
                }

                outcome.Succeeded = true;
                outcome.FailureCode = null;
                return outcome;
            }
            catch (Exception exception)
            {
                return RollBackAndRefuse(outcome, recipe.Id, priorProvenance, provenanceExisted, VfxRecipeBuildCodes.BuildFailed, "Build transaction threw: " + exception.Message);
            }
        }

        private static bool MatchesApprovedPlan(VfxBuildPlan approved, VfxBuildPlan candidate, string expectedPrefabPath, out string error)
        {
            error = null;
            if (candidate == null || candidate.IsBlocked)
            {
                error = "Recipe or catalog identity is no longer buildable at commit time.";
                return false;
            }

            if (!string.Equals(candidate.RecipeHash, approved.RecipeHash, StringComparison.Ordinal) ||
                candidate.RecipeRevision != approved.RecipeRevision ||
                !string.Equals(candidate.BuildHash, approved.BuildHash, StringComparison.Ordinal))
            {
                error = "Recipe, revision or catalog dependency identity changed after the plan was approved.";
                return false;
            }

            if (candidate.Items.Count != 1 ||
                !string.Equals(candidate.Items[0].AssetPath, expectedPrefabPath, StringComparison.Ordinal))
            {
                error = "Planned output path changed after the plan was approved.";
                return false;
            }

            return true;
        }

        private static bool TryVerifyCommittedMembers(VfxRecipeBuildOutcome outcome, VfxBuildPlan approved, out string error)
        {
            error = null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(outcome.PrefabPath) == null)
            {
                error = "Committed Runtime Entry is not an importable Prefab: " + outcome.PrefabPath;
                return false;
            }

            var buildManifest = ReadJsonOrNull(VfxRecipeBuildWriteSurface.ProjectAbsolute(outcome.BuildManifestPath));
            if (buildManifest == null ||
                !string.Equals((string)buildManifest["recipeHash"], approved.RecipeHash, StringComparison.Ordinal) ||
                !string.Equals((string)buildManifest["buildHash"], approved.BuildHash, StringComparison.Ordinal))
            {
                error = "Committed build manifest is missing or records different identities.";
                return false;
            }

            var ownership = ReadJsonOrNull(VfxRecipeBuildWriteSurface.ProjectAbsolute(outcome.OwnershipManifestPath));
            if (ownership == null ||
                !string.Equals((string)ownership["recipeHash"], approved.RecipeHash, StringComparison.Ordinal) ||
                !string.Equals((string)ownership["buildHash"], approved.BuildHash, StringComparison.Ordinal))
            {
                error = "Committed ownership manifest is missing or records different identities.";
                return false;
            }

            // Strict enforcement resolves the source by canonical hash, so an identical recipe already in
            // the provenance root may legitimately be recorded instead of the file this build wrote. Both
            // are acceptable provenance; a source outside the provenance root or with a different hash is not.
            var recordedSource = (string)ownership["sourceRecipePath"];
            if (!string.Equals(recordedSource, outcome.ProvenanceRecipePath, StringComparison.Ordinal) &&
                !IsProvenanceForHash(recordedSource, approved.RecipeHash))
            {
                error = "Committed ownership manifest does not bind a build provenance recipe for this recipe hash.";
                return false;
            }

            if (!IsProvenanceForHash(outcome.ProvenanceRecipePath, approved.RecipeHash))
            {
                error = "Build provenance recipe is missing or its hash differs: " + outcome.ProvenanceRecipePath;
                return false;
            }

            return true;
        }

        private static bool IsProvenanceForHash(string assetPath, string expectedRecipeHash)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !VfxRecipeBuildWriteSurface.IsSafeProjectRelativePath(assetPath) ||
                !assetPath.StartsWith(VfxRecipeBuildWriteSurface.ProvenanceRecipeRoot + "/", StringComparison.Ordinal))
            {
                return false;
            }

            var absolute = VfxRecipeBuildWriteSurface.ProjectAbsolute(assetPath);
            if (!File.Exists(absolute)) return false;
            try
            {
                var hash = RecipeCanonicalizer.ComputeSha256(StrictUtf8.GetString(File.ReadAllBytes(absolute)));
                return string.Equals(hash, expectedRecipeHash, StringComparison.Ordinal);
            }
            catch (Exception) { return false; }
        }

        /// <summary>
        /// Writes write-surface member 3 with the same single-file atomic discipline as the manifests, and
        /// hands back the previous bytes so a failed build can restore the last good provenance.
        /// </summary>
        private static bool TryWriteProvenance(
            string effectId,
            string recipeJson,
            out byte[] priorBytes,
            out bool existed,
            out string error)
        {
            priorBytes = null;
            existed = false;
            error = null;
            var assetPath = VfxRecipeBuildWriteSurface.ProvenanceRecipeFor(effectId);
            if (!VfxRecipeBuildWriteSurface.IsInsideWriteSurface(assetPath))
            {
                error = "Build provenance target is outside the closed write surface: " + assetPath;
                return false;
            }

            var absolute = VfxRecipeBuildWriteSurface.ProjectAbsolute(assetPath);
            var pending = absolute + ".pending";
            try
            {
                var canonical = RecipeCanonicalizer.Canonicalize(recipeJson);
                Directory.CreateDirectory(Path.GetDirectoryName(absolute));
                existed = File.Exists(absolute);
                if (existed) priorBytes = File.ReadAllBytes(absolute);
                if (File.Exists(pending)) File.Delete(pending);
                File.WriteAllText(pending, canonical, StrictUtf8);
                if (existed) ReplaceWithBoundedRetry(pending, absolute);
                else File.Move(pending, absolute);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                return true;
            }
            catch (Exception exception)
            {
                error = "Build provenance recipe could not be written: " + exception.Message;
                return false;
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
            }
        }

        private static void RestoreProvenance(string effectId, byte[] priorBytes, bool existed)
        {
            var assetPath = VfxRecipeBuildWriteSurface.ProvenanceRecipeFor(effectId);
            var absolute = VfxRecipeBuildWriteSurface.ProjectAbsolute(assetPath);
            if (existed)
            {
                File.WriteAllBytes(absolute, priorBytes);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            if (File.Exists(absolute)) File.Delete(absolute);
            if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta");
            AssetDatabase.Refresh();
        }

        private static void ReplaceWithBoundedRetry(string pending, string destination)
        {
            Exception failure = null;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    File.Replace(pending, destination, null);
                    return;
                }
                catch (IOException exception) { failure = exception; }
                catch (UnauthorizedAccessException exception) { failure = exception; }
                if (attempt < 3) System.Threading.Thread.Sleep(25 * (attempt + 1));
            }

            throw new IOException("Build provenance atomic replacement failed after bounded retries: " + destination, failure);
        }

        private static bool TryReadStagedRecipe(string recipePath, out string recipeJson, out string error)
        {
            recipeJson = null;
            error = null;
            string absolute;
            try { absolute = Path.GetFullPath(recipePath); }
            catch (Exception exception)
            {
                error = "Staged recipe path is not a valid path: " + exception.Message;
                return false;
            }

            // Build inputs are staged outside the project on purpose (ADR-007 §2.1); accepting an
            // in-project file here would let the entry point launder project content as fresh input.
            var projectBoundary = VfxRecipeBuildWriteSurface.ProjectRoot()
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (absolute.StartsWith(projectBoundary, StringComparison.OrdinalIgnoreCase))
            {
                error = "Staged recipe input must live outside the Unity project directory.";
                return false;
            }

            if (!File.Exists(absolute))
            {
                error = "Staged recipe input does not exist.";
                return false;
            }

            try { recipeJson = StrictUtf8.GetString(File.ReadAllBytes(absolute)); }
            catch (Exception exception)
            {
                error = "Staged recipe input could not be read as strict UTF-8: " + exception.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Derives a catalog identity from every registered template's id, version and asset GUID. Unity
        /// has no catalog version constant, so this is a recorded audit value rather than a gate; template
        /// drift is caught by per-template resolution and authoritative validation instead.
        /// </summary>
        public static string ComputeCatalogIdentityHash(TemplateCatalog catalog)
        {
            var builder = new StringBuilder();
            foreach (var manifest in catalog.ByTemplateId.Values.OrderBy(value => value.TemplateId, StringComparer.Ordinal))
            {
                builder.Append(manifest.TemplateId).Append('|')
                    .Append(manifest.TemplateVersion).Append('|')
                    .Append(manifest.AssetGuid).Append('\n');
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var output = new StringBuilder(hash.Length * 2);
                foreach (var value in hash) output.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return output.ToString();
            }
        }

        private static JObject ReadJsonOrNull(string absolutePath)
        {
            if (!File.Exists(absolutePath)) return null;
            try
            {
                return JObject.Parse(
                    File.ReadAllText(absolutePath),
                    new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (JsonException) { return null; }
            catch (IOException) { return null; }
        }

        private static VfxRecipeBuildOutcome RollBackAndRefuse(
            VfxRecipeBuildOutcome outcome,
            string effectId,
            byte[] priorProvenance,
            bool provenanceExisted,
            string code,
            string message)
        {
            try { RestoreProvenance(effectId, priorProvenance, provenanceExisted); }
            catch (Exception exception)
            {
                // The rollback failure must never hide the original refusal, and it is security relevant.
                outcome.Issues.Add(new ValidationEntry
                {
                    Code = VfxRecipeBuildCodes.ProvenanceWriteFailed,
                    Severity = ValidationSeverity.Error,
                    Path = "/provenance",
                    Message = "Build provenance rollback failed: " + exception.Message
                });
            }

            return Refuse(outcome, code, message);
        }

        private static VfxRecipeBuildOutcome Refuse(VfxRecipeBuildOutcome outcome, string code, string message)
        {
            outcome.Succeeded = false;
            outcome.FailureCode = code;
            outcome.Issues.Add(new ValidationEntry
            {
                Code = code,
                Severity = ValidationSeverity.Error,
                Path = "/build",
                Message = message
            });
            return outcome;
        }

        private static void WriteResult(string resultPath, VfxRecipeBuildOutcome outcome)
        {
            var absolute = Path.GetFullPath(resultPath);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(absolute, VfxRecipeBuildJson.Serialize(outcome), StrictUtf8);
        }
    }
}
