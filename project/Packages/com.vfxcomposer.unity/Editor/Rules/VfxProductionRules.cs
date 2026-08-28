using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Editor.Rules
{
    public static class VfxProductionRules
    {
        public static VfxOutputAuditResult EnforceAndWriteManifest(string effectId, string archetype, int recipeVersion, int recipeRevision, string recipeHash, string buildHash, string compilerVersion, string runtimePrefabPath, string outputFolder, double duration, string sourceRecipePathOverride = null)
        {
            // Ordinary production writers are intentionally incapable of manufacturing W24
            // authority: formalProduction is not part of their public API.
            if (W24S5ProductionGate.IsW24ProtectedEffect(effectId))
            {
                var rejected = new VfxOutputAuditResult();
                rejected.Report.Add("E24S5-090", ValidationSeverity.Error, "/formalProduction", "A W24-protected effect may only be written through an S5 gate-owned commit.");
                return rejected;
            }
            return EnforceAndWriteManifestCore(effectId, archetype, recipeVersion, recipeRevision, recipeHash, buildHash, compilerVersion, runtimePrefabPath, outputFolder, duration, sourceRecipePathOverride, null, false);
        }

        /// <summary>
        /// The only writer that can persist a PRE_C0 bootstrap receipt. The unconstructible S5
        /// issuer is deliberately checked here rather than trusting a caller-built binding model.
        /// </summary>
        internal static VfxOutputAuditResult EnforceAndWriteBootstrapManifest(object issuer, string effectId, string archetype, int recipeVersion, int recipeRevision, string recipeHash, string buildHash, string compilerVersion, string runtimePrefabPath, string outputFolder, double duration, string sourceRecipePathOverride, VfxFormalProductionBinding formalProduction)
        {
            if (!W24S5ProductionGate.IsFirstFormalWriteIssuer(issuer))
            {
                var rejected = new VfxOutputAuditResult();
                rejected.Report.Add("E24S5PRE040", ValidationSeverity.Error, "/formalProduction", "PRE_C0 bootstrap manifest writes require a gate-issued first-formal authority.");
                return rejected;
            }
            return EnforceAndWriteManifestCore(effectId, archetype, recipeVersion, recipeRevision, recipeHash, buildHash, compilerVersion, runtimePrefabPath, outputFolder, duration, sourceRecipePathOverride, formalProduction, true);
        }

        /// <summary>Normal formal writes are gate-owned just like bootstrap writes.</summary>
        internal static VfxOutputAuditResult EnforceAndWriteFormalManifest(object issuer, string effectId, string archetype, int recipeVersion, int recipeRevision, string recipeHash, string buildHash, string compilerVersion, string runtimePrefabPath, string outputFolder, double duration, string sourceRecipePathOverride, VfxFormalProductionBinding formalProduction)
        {
            if (!W24S5ProductionGate.IsFormalWriteIssuer(issuer))
            {
                var rejected = new VfxOutputAuditResult();
                rejected.Report.Add("E24S5-091", ValidationSeverity.Error, "/formalProduction", "Normal formal manifest writes require a gate-issued formal authority.");
                return rejected;
            }
            return EnforceAndWriteManifestCore(effectId, archetype, recipeVersion, recipeRevision, recipeHash, buildHash, compilerVersion, runtimePrefabPath, outputFolder, duration, sourceRecipePathOverride, formalProduction, false);
        }

        private static VfxOutputAuditResult EnforceAndWriteManifestCore(string effectId, string archetype, int recipeVersion, int recipeRevision, string recipeHash, string buildHash, string compilerVersion, string runtimePrefabPath, string outputFolder, double duration, string sourceRecipePathOverride, VfxFormalProductionBinding formalProduction, bool permitsPreC0Bootstrap)
        {
            var audit = VfxOutputAuditor.Audit(effectId, archetype, runtimePrefabPath, outputFolder);
            audit.Cost.Duration = duration;
            if (audit.Report.HasErrors) return audit;
            if (formalProduction != null && !ValidateFormalBinding(formalProduction, audit, permitsPreC0Bootstrap)) return audit;
            var path = VfxProjectRules.ManifestAbsolutePath(effectId);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            VfxOutputManifest old = null;
            try { if (File.Exists(path)) old = JsonConvert.DeserializeObject<VfxOutputManifest>(File.ReadAllText(path)); } catch { old = null; }
            var manifest = new VfxOutputManifest
            {
                RulesVersion = VfxProjectRules.Load().RulesVersion,
                Enforcement = VfxProjectRules.EnforcementFor(effectId) == VfxRulesEnforcement.Strict ? "strict" : "legacy_audit",
                EffectId = effectId,
                Archetype = archetype,
                RecipeVersion = recipeVersion,
                RecipeRevision = recipeRevision,
                RecipeHash = recipeHash,
                BuildHash = buildHash,
                CompilerVersion = compilerVersion,
                UnityVersion = Application.unityVersion,
                SourceRecipePath = string.IsNullOrEmpty(sourceRecipePathOverride) ? FindRecipeSource(recipeHash) : sourceRecipePathOverride,
                RuntimeEntry = new VfxRuntimeEntryRecord { Kind = "prefab", Path = runtimePrefabPath, Guid = AssetDatabase.AssetPathToGUID(runtimePrefabPath) },
                OwnedOutputs = audit.OwnedOutputs,
                Dependencies = audit.Dependencies,
                Cost = audit.Cost,
                FormalProduction = formalProduction,
                GeneratedAtUtc = old != null && string.Equals(old.BuildHash, buildHash, StringComparison.Ordinal) ? old.GeneratedAtUtc : DateTime.UtcNow.ToString("O")
            };
            manifest.Audit = audit.Report.Entries.Select(entry => new VfxOutputAuditEntry { Code = entry.Code, Severity = entry.Severity.ToString().ToLowerInvariant(), Path = entry.Path, Message = entry.Message }).ToList();
            if (!string.IsNullOrEmpty(manifest.SourceRecipePath) && (!manifest.SourceRecipePath.StartsWith("Assets/VFX/Recipes/", StringComparison.Ordinal) || manifest.SourceRecipePath.IndexOf("..", StringComparison.Ordinal) >= 0))
            {
                audit.Report.Add("E8014", ValidationSeverity.Error, "/sourceRecipePath", "Production output source override must remain under Assets/VFX/Recipes/.");
                return audit;
            }
            if (string.IsNullOrEmpty(manifest.SourceRecipePath) && VfxProjectRules.EnforcementFor(effectId) == VfxRulesEnforcement.Strict)
            {
                audit.Report.Add("E8014", ValidationSeverity.Error, "/sourceRecipePath", "Strict production output requires a saved Recipe under Assets/VFX/Recipes whose canonical hash matches the build input.");
                return audit;
            }
            WriteAtomic(path, JsonConvert.SerializeObject(manifest, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver(), NullValueHandling = NullValueHandling.Include }));
            return audit;
        }

        public static string CaptureManifest(string effectId)
        {
            var path = VfxProjectRules.ManifestAbsolutePath(effectId);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        // Recovery is an editor-internal transactional primitive, not a public writer.  Keeping
        // it internal prevents consumers from replacing a W24 formal manifest by bypassing the
        // normal writer's gate-owned authority check.
        internal static void RestoreManifest(string effectId, string content)
        {
            var path = VfxProjectRules.ManifestAbsolutePath(effectId);
            if (content == null) { if (File.Exists(path)) File.Delete(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            WriteAtomic(path, content);
        }

        private static string FindRecipeSource(string recipeHash)
        {
            var root = Path.Combine(Application.dataPath, "VFX", "Recipes");
            if (!Directory.Exists(root)) return null;
            foreach (var absolute in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var assetPath = Path.GetFullPath(absolute).Substring(projectRoot.Length).Replace('\\', '/');
                if (assetPath.IndexOf("/Patches/", StringComparison.OrdinalIgnoreCase) >= 0 || assetPath.EndsWith(".patch.json", StringComparison.OrdinalIgnoreCase)) continue;
                try { if (string.Equals(VFXComposer.Editor.Validation.RecipeCanonicalizer.ComputeSha256(File.ReadAllText(absolute)), recipeHash, StringComparison.OrdinalIgnoreCase)) return assetPath; }
                catch { }
            }
            return null;
        }

        private static bool ValidateFormalBinding(VfxFormalProductionBinding binding, VfxOutputAuditResult audit, bool permitsPreC0Bootstrap)
        {
            var valid = binding.ContractRevision >= 1 && CanonicalHash(binding.ContractFileHash) && CanonicalHash(binding.ContractHash) && CanonicalHash(binding.TraceFileHash) && !string.IsNullOrEmpty(binding.ContractPath) && !string.IsNullOrEmpty(binding.TracePath) && (binding.VisualStatus == "VISUAL_PENDING" || binding.VisualStatus == "L3" || binding.VisualStatus == "L4") && (string.IsNullOrEmpty(binding.AdmissionPhase) || binding.AdmissionPhase == "PRE_C0_FIRST_FORMAL_BUILD");
            if (binding.AdmissionPhase == "PRE_C0_FIRST_FORMAL_BUILD") valid = valid && permitsPreC0Bootstrap && binding.VisualStatus == "VISUAL_PENDING" && string.IsNullOrEmpty(binding.EvidenceCorpusPath) && string.IsNullOrEmpty(binding.EvidenceCorpusHash) && string.IsNullOrEmpty(binding.UserVerdictRecordPath) && string.IsNullOrEmpty(binding.UserVerdictRecordHash) && string.IsNullOrEmpty(binding.VisualQaRecordPath) && string.IsNullOrEmpty(binding.VisualQaRecordHash) && string.IsNullOrEmpty(binding.S0aStatusRecordPath) && string.IsNullOrEmpty(binding.S0aStatusRecordHash);
            if (binding.VisualStatus == "L3") valid = valid && CanonicalHash(binding.VisualQaRecordHash) && CanonicalHash(binding.S0aStatusRecordHash) && !string.IsNullOrEmpty(binding.VisualQaRecordPath) && !string.IsNullOrEmpty(binding.S0aStatusRecordPath);
            if (binding.VisualStatus == "L4") valid = valid && CanonicalHash(binding.EvidenceCorpusHash) && CanonicalHash(binding.UserVerdictRecordHash) && !string.IsNullOrEmpty(binding.EvidenceCorpusPath) && !string.IsNullOrEmpty(binding.UserVerdictRecordPath);
            if (!valid) audit.Report.Add("E8020", ValidationSeverity.Error, "/formalProduction", "Formal manifest bindings are incomplete or invalid.");
            return valid;
        }

        private static bool CanonicalHash(string value)
        {
            return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal) && value.Skip(7).All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private static void WriteAtomic(string path, string content)
        {
            if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal)) return;
            var pending = path + ".pending";
            var backup = path + ".atomic-backup";
            try
            {
                if (File.Exists(pending) || File.Exists(backup)) throw new IOException("Manifest atomic-write residue must be recovered before retrying: " + path);
                File.WriteAllText(pending, content, new UTF8Encoding(false));
                if (!File.Exists(path))
                {
                    File.Move(pending, path);
                    return;
                }

                IOException replaceFailure = null;
                for (var attempt = 0; attempt < 4; attempt++)
                {
                    try
                    {
                        File.Replace(pending, path, null, true);
                        return;
                    }
                    catch (IOException exception)
                    {
                        replaceFailure = exception;
                        if (attempt < 3) System.Threading.Thread.Sleep(25 * (attempt + 1));
                    }
                }

                // AssetDatabase may briefly hold the destination in a mode where ReplaceFile
                // cannot delete it.  A same-directory rename transaction preserves the old
                // bytes and gives us an explicit rollback path without overwriting in place.
                File.Move(path, backup);
                try
                {
                    File.Move(pending, path);
                    File.Delete(backup);
                }
                catch (Exception commitFailure)
                {
                    try
                    {
                        if (File.Exists(path)) File.Delete(path);
                        if (File.Exists(backup)) File.Move(backup, path);
                    }
                    catch (Exception rollbackFailure)
                    {
                        throw new IOException("Manifest atomic write and rollback both failed: " + path, new AggregateException(commitFailure, rollbackFailure));
                    }
                    throw new IOException("Manifest atomic replacement failed after File.Replace retries: " + path, replaceFailure ?? commitFailure);
                }
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
                // A surviving backup means rollback itself failed.  Do not silently delete the
                // only known-good bytes; surface it on the next call through the residue guard.
            }
        }
    }
}
