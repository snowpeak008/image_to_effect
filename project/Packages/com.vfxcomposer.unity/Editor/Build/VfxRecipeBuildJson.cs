using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Build
{
    /// <summary>
    /// Hand-written wire format for the restricted build request and result. Unknown fields and unknown
    /// schema versions are refused rather than ignored, so a drifted caller fails closed.
    /// </summary>
    public static class VfxRecipeBuildJson
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        /// <summary>Reads a request. Returns null and sets <paramref name="error"/> on any rejection.</summary>
        public static VfxRecipeBuildRequest TryReadRequest(string absolutePath, out string code, out string error)
        {
            code = null;
            error = null;
            string text;
            try { text = StrictUtf8.GetString(File.ReadAllBytes(absolutePath)); }
            catch (Exception exception)
            {
                code = VfxRecipeBuildCodes.RequestUnreadable;
                error = "Build request could not be read as strict UTF-8: " + exception.Message;
                return null;
            }

            JObject root;
            try
            {
                root = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            }
            catch (Exception exception)
            {
                code = VfxRecipeBuildCodes.RequestInvalid;
                error = "Build request is not a JSON object: " + exception.Message;
                return null;
            }

            var known = new[] { "schemaVersion", "draftId", "recipePath", "expectedCanonicalSha256", "declaredTemplateCatalogVersion" };
            foreach (var property in root.Properties())
            {
                if (Array.IndexOf(known, property.Name) >= 0) continue;
                code = VfxRecipeBuildCodes.RequestInvalid;
                error = "Build request carries an unknown field: " + property.Name;
                return null;
            }

            if (!string.Equals((string)root["schemaVersion"], VfxRecipeBuildRequest.SchemaVersion, StringComparison.Ordinal))
            {
                code = VfxRecipeBuildCodes.RequestInvalid;
                error = "Build request schema version is not " + VfxRecipeBuildRequest.SchemaVersion + ".";
                return null;
            }

            var request = new VfxRecipeBuildRequest
            {
                DraftId = (string)root["draftId"],
                RecipePath = (string)root["recipePath"],
                ExpectedCanonicalSha256 = (string)root["expectedCanonicalSha256"],
                DeclaredTemplateCatalogVersion = (string)root["declaredTemplateCatalogVersion"]
            };

            if (string.IsNullOrWhiteSpace(request.DraftId) || request.DraftId.Length > 128)
            {
                code = VfxRecipeBuildCodes.RequestInvalid;
                error = "Build request draft id is missing or too long.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(request.RecipePath))
            {
                code = VfxRecipeBuildCodes.RequestInvalid;
                error = "Build request recipe path is missing.";
                return null;
            }

            if (!IsLowercaseSha256(request.ExpectedCanonicalSha256))
            {
                code = VfxRecipeBuildCodes.RequestInvalid;
                error = "Build request expects a lowercase hex SHA-256 confirmation hash.";
                return null;
            }

            return request;
        }

        public static bool IsLowercaseSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var character in value)
            {
                if (!(character >= '0' && character <= '9') && !(character >= 'a' && character <= 'f')) return false;
            }

            return true;
        }

        public static string Serialize(VfxRecipeBuildOutcome outcome)
        {
            var issues = new JArray();
            foreach (var issue in outcome.Issues)
            {
                issues.Add(new JObject
                {
                    ["code"] = issue.Code,
                    ["severity"] = issue.Severity.ToString(),
                    ["path"] = issue.Path,
                    ["message"] = issue.Message,
                    ["actualValue"] = issue.ActualValue == null ? null : issue.ActualValue.ToString(Formatting.None),
                    ["allowedRange"] = issue.AllowedRange
                });
            }

            var cleaned = new JArray();
            foreach (var path in outcome.CleanedResiduePaths) cleaned.Add(path);

            var root = new JObject
            {
                ["schemaVersion"] = VfxRecipeBuildOutcome.SchemaVersion,
                ["draftId"] = outcome.DraftId,
                ["succeeded"] = outcome.Succeeded,
                ["failureCode"] = outcome.FailureCode,
                ["effectId"] = outcome.EffectId,
                ["recipeHash"] = outcome.RecipeHash,
                ["buildHash"] = outcome.BuildHash,
                ["recipeRevision"] = outcome.RecipeRevision,
                ["compilerVersion"] = outcome.CompilerVersion,
                ["unityVersion"] = outcome.UnityVersion,
                ["declaredTemplateCatalogVersion"] = outcome.DeclaredTemplateCatalogVersion,
                ["catalogIdentityHash"] = outcome.CatalogIdentityHash,
                ["prefabPath"] = outcome.PrefabPath,
                ["buildManifestPath"] = outcome.BuildManifestPath,
                ["ownershipManifestPath"] = outcome.OwnershipManifestPath,
                ["provenanceRecipePath"] = outcome.ProvenanceRecipePath,
                ["dryRunState"] = outcome.DryRunState,
                ["cleanedResiduePaths"] = cleaned,
                ["issues"] = issues
            };
            return root.ToString(Formatting.Indented);
        }

        /// <summary>Copies a validation report into the outcome, bounded so a pathological report cannot grow without limit.</summary>
        public static void CopyIssues(VfxRecipeBuildOutcome outcome, ValidationReport report)
        {
            if (report == null) return;
            const int maximumIssues = 256;
            foreach (var entry in report.Entries)
            {
                if (outcome.Issues.Count >= maximumIssues) return;
                outcome.Issues.Add(entry);
            }
        }
    }
}
