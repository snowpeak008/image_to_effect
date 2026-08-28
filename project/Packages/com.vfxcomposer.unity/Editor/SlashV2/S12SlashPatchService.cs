using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.SlashV2
{
    /// <summary>Closed v2 Slash parameter Patch service. It intentionally does not share v1 Patch grammar or writes.</summary>
    public sealed class S12SlashPatchService
    {
        public const string HistorySuffix = ".history.json";
        private const int MaxOperations = 12;
        private readonly Func<string> snapshotFactory;
        private readonly Action afterRecipeWrittenBeforeHistoryWritten;
        private readonly IS12SlashBuildHook buildHook;
        public S12SlashPatchService() : this(null, null, null) { }
        internal S12SlashPatchService(Func<string> snapshotFactory, Action afterRecipeWrittenBeforeHistoryWritten, IS12SlashBuildHook buildHook)
        { this.snapshotFactory = snapshotFactory ?? Snapshot; this.afterRecipeWrittenBeforeHistoryWritten = afterRecipeWrittenBeforeHistoryWritten; this.buildHook = buildHook; }
        public VfxPatchResult Validate(string recipeJson, string patchJson, int expectedRevision, S12SlashTemplateCatalog catalog = null)
        {
            var result = new VfxPatchResult(); var dispatch = S12RecipeDispatcher.Parse(recipeJson); result.Report.AddRange(dispatch.Report);
            if (!result.Report.HasErrors && dispatch.RecipeVersion != 2) result.Report.Add("E1280", ValidationSeverity.Error, "/recipeVersion", "Slash v2 Patch accepts Recipe v2 only; v1 is owned by VfxPatchService.");
            catalog = catalog ?? S12SlashCompiler.LoadFormalCatalog(); result.Report.AddRange(catalog.Report);
            if (!result.Report.HasErrors) result.Report.AddRange(S12SlashV2Validator.Validate(recipeJson, catalog));
            if (!result.Report.HasErrors) result.Report.AddRange(S12SlashBudgetCalculator.Evaluate(dispatch.SlashV2, catalog));
            if (result.Report.HasErrors) return result;
            result.BeforeRevision = dispatch.SlashV2.Revision; result.BeforeCanonicalHash = RecipeCanonicalizer.ComputeSha256(recipeJson);
            if (expectedRevision != result.BeforeRevision) { result.Report.Add("E1281", ValidationSeverity.Error, "/revision", "expectedRevision does not match Recipe revision.", new JValue(expectedRevision), result.BeforeRevision.ToString(CultureInfo.InvariantCulture)); return result; }
            var ops = Parse(patchJson, result); if (!result.IsValid) return result;
            JObject root; try { root = JObject.Parse(recipeJson, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); } catch (Exception e) { result.Report.Add("E1282", ValidationSeverity.Error, "/", "Recipe clone failed: " + e.Message); return result; }
            var patched = (JObject)root.DeepClone(); foreach (var op in ops) if (!Apply(patched, dispatch.SlashV2, catalog, op, result)) return result;
            patched["revision"] = expectedRevision + 1; var text = patched.ToString(Formatting.Indented);
            var post = S12SlashV2Validator.Validate(text, catalog); post.AddRange(S12SlashBudgetCalculator.Evaluate(S12RecipeDispatcher.Parse(text).SlashV2, catalog)); post.AddRange(new S12SlashCompiler().DryRun(text, catalog).Report); result.Report.AddRange(post);
            if (result.Report.HasErrors) { var matches = post.Entries.Where(x => x.Severity == ValidationSeverity.Error).SelectMany(x => ops.Where(op => x.Path == op.Path || x.Path.StartsWith(op.Path + "/", StringComparison.Ordinal)).Select(op => op.Index)).Distinct().ToArray(); if (matches.Length == 1) result.FailedOperationIndex = matches[0]; else result.IsPostPatchValidationFailure = true; return result; }
            result.AfterRevision = expectedRevision + 1; result.PatchedRecipeJson = text; result.AfterCanonicalHash = RecipeCanonicalizer.ComputeSha256(text); foreach (var op in ops) result.AffectedItems.Add(new VfxPatchImpactItem { StageId = op.Phase, ModuleId = op.Module, State = VfxPatchImpactState.Update }); return result;
        }

        public VfxPatchResult ApplyToAsset(string recipeAssetPath, string patchJson, int expectedRevision)
        {
            if (string.IsNullOrEmpty(recipeAssetPath) || !recipeAssetPath.StartsWith("Assets/VFX/Recipes/Slash/", StringComparison.Ordinal)) return Error("E1283", "/recipe", "Slash Patch writes only formal Slash Recipes.");
            var recipe = Absolute(recipeAssetPath); if (!File.Exists(recipe)) return Error("E1283", "/recipe", "Slash Recipe asset does not exist."); var before = File.ReadAllText(recipe); var result = Validate(before, patchJson, expectedRevision); if (!result.IsValid) return result;
            var history = recipe + HistorySuffix; string oldHistory; try { oldHistory = File.Exists(history) ? File.ReadAllText(history) : null; if (oldHistory != null && !(JToken.Parse(oldHistory) is JArray)) throw new InvalidOperationException("Existing Slash history must be a bare JSON array."); } catch (Exception e) { result.Report.Add("E1284", ValidationSeverity.Error, "/history", e.Message); return result; }
            string backup; try { backup = snapshotFactory(); } catch (Exception e) { result.Report.Add("E1285", ValidationSeverity.Error, "/transaction/snapshot", "Slash Patch snapshot failed: " + e.Message); return result; } try
            {
                var build = new S12SlashCompiler(null, buildHook, null).Build(result.PatchedRecipeJson); result.Report.AddRange(build.Plan.Report); if (!build.Succeeded) throw new InvalidOperationException("Slash Generated build did not succeed.");
                Atomic(recipe, result.PatchedRecipeJson); if (afterRecipeWrittenBeforeHistoryWritten != null) afterRecipeWrittenBeforeHistoryWritten(); Atomic(history, History(oldHistory, result, patchJson, build)); AssetDatabase.ImportAsset(recipeAssetPath, ImportAssetOptions.ForceUpdate); AssetDatabase.ImportAsset(recipeAssetPath + HistorySuffix, ImportAssetOptions.ForceUpdate); AssetDatabase.SaveAssets(); return result;
            }
            catch (Exception e) { try { RestoreText(recipe, before, history, oldHistory); } catch (Exception rollback) { result.Report.Add("E1286", ValidationSeverity.Error, "/transaction/rollback/text", "Slash Patch text rollback failed; manual recovery required: " + rollback.Message); } try { Restore(backup); } catch (Exception rollback) { result.Report.Add("E1286", ValidationSeverity.Error, "/transaction/rollback/generated", "Slash Patch Generated rollback failed; manual recovery required: " + rollback.Message); } result.Report.Add("E1285", ValidationSeverity.Error, "/transaction", "Slash Patch transaction failed: " + e.Message); return result; }
            finally { try { if (Directory.Exists(backup)) Directory.Delete(backup, true); } catch (Exception e) { result.Report.Add("E1286", ValidationSeverity.Error, "/transaction/backup", "Slash Patch backup cleanup failed; manual recovery required: " + e.Message); } }
        }

        private static List<Op> Parse(string json, VfxPatchResult r)
        {
            var list = new List<Op>(); JArray array; try { array = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }) as JArray; } catch (Exception e) { r.Report.Add("E1287", ValidationSeverity.Error, "/", "Patch JSON must be a bare array: " + e.Message); return list; }
            if (array == null) { r.Report.Add("E1287", ValidationSeverity.Error, "/", "Patch top level must be a bare array."); return list; } if (array.Count == 0 || array.Count > MaxOperations) { r.Report.Add("E1288", ValidationSeverity.Error, "/", "Patch must contain 1-" + MaxOperations + " operations."); return list; }
            var seen = new HashSet<string>(StringComparer.Ordinal); for (var i = 0; i < array.Count; i++) { var o = array[i] as JObject; var prefix = "/" + i; if (o == null || o.Properties().Any(p => p.Name != "op" && p.Name != "path" && p.Name != "value") || (string)o["op"] != "replace" || o["path"]?.Type != JTokenType.String || o["value"] == null) { r.Report.Add("E1289", ValidationSeverity.Error, prefix, "Slash Patch operation must contain only replace, path and numeric value."); r.FailedOperationIndex = i; return list; } var op = new Op { Index = i, Path = (string)o["path"], Value = o["value"] }; var p = op.Path.Split('/'); if (p.Length != 7 || p[0] != "" || p[1] != "phases" || p[3] != "modules" || p[5] != "parameters" || !Id(p[2]) || !Id(p[4]) || !Id(p[6])) { r.Report.Add("E1290", ValidationSeverity.Error, prefix + "/path", "Path must be /phases/{phaseId}/modules/{moduleId}/parameters/{parameter}."); r.FailedOperationIndex = i; return list; } op.Phase = p[2]; op.Module = p[4]; op.Parameter = p[6]; if (!seen.Add(op.Path)) { r.Report.Add("E1291", ValidationSeverity.Error, prefix + "/path", "Duplicate Slash Patch target."); r.FailedOperationIndex = i; return list; } list.Add(op); }
            return list;
        }

        private static bool Apply(JObject root, S12SlashRecipe recipe, S12SlashTemplateCatalog catalog, Op op, VfxPatchResult r)
        {
            var phase = recipe.Phases.SingleOrDefault(x => x.Id == op.Phase); var module = phase?.Modules.SingleOrDefault(x => x.Id == op.Module); S12SlashManifest manifest; if (module == null || !catalog.TryGet(module.TemplateId, out manifest) || !manifest.Parameters.ContainsKey(op.Parameter)) return Fail(r, op, "E1292", "Target is not a formal Slash parameter."); var decl = manifest.Parameters[op.Parameter]; if (op.Value.Type != JTokenType.Integer && op.Value.Type != JTokenType.Float) { r.Report.Add("E1293", ValidationSeverity.Error, op.Path, "Replacement value must be finite numeric.", op.Value, decl.Type); r.FailedOperationIndex = op.Index; return false; } var value = op.Value.Value<double>(); if (double.IsNaN(value) || double.IsInfinity(value) || value < decl.Min.Value<double>() || value > decl.Max.Value<double>() || (decl.Type == "integer" && Math.Abs(value - Math.Round(value)) > 0.000001)) { r.Report.Add("E1293", ValidationSeverity.Error, op.Path, "Replacement value is outside the Manifest type/range.", op.Value, decl.Type + " [" + decl.Min + "," + decl.Max + "]"); r.FailedOperationIndex = op.Index; return false; }
            var jsonPhase = ((JArray)root["phases"]).Children<JObject>().Single(x => (string)x["id"] == op.Phase); var jsonModule = ((JArray)jsonPhase["modules"]).Children<JObject>().Single(x => (string)x["id"] == op.Module); jsonModule["parameters"][op.Parameter] = op.Value.DeepClone(); return true;
        }
        private static bool Fail(VfxPatchResult r, Op op, string code, string message) { r.Report.Add(code, ValidationSeverity.Error, op.Path, message, op.Value); r.FailedOperationIndex = op.Index; return false; }
        private static bool Id(string v) { return !string.IsNullOrEmpty(v) && char.IsLetter(v[0]) && v.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'); }
        private static VfxPatchResult Error(string code, string path, string message) { var r = new VfxPatchResult(); r.Report.Add(code, ValidationSeverity.Error, path, message); return r; }
        private static string Absolute(string asset) { return Path.Combine(UnityEngine.Application.dataPath, asset.Substring("Assets/".Length)); }
        private static void Atomic(string file, string text) { var pending = file + ".pending"; try { if (File.Exists(pending)) File.Delete(pending); File.WriteAllText(pending, text, new UTF8Encoding(false)); if (File.Exists(file)) File.Replace(pending, file, null); else File.Move(pending, file); } finally { if (File.Exists(pending)) File.Delete(pending); } }
        private static string History(string old, VfxPatchResult r, string patch, VfxBuildResult build) { var items = string.IsNullOrWhiteSpace(old) ? new JArray() : JArray.Parse(old); items.Add(new JObject { ["beforeRevision"] = r.BeforeRevision, ["afterRevision"] = r.AfterRevision, ["beforeCanonicalHash"] = r.BeforeCanonicalHash, ["afterCanonicalHash"] = r.AfterCanonicalHash, ["ops"] = JToken.Parse(patch), ["affectedPaths"] = new JArray(JArray.Parse(patch).Children<JObject>().Select(x => (string)x["path"])), ["buildHash"] = build.Plan.BuildHash }); return items.ToString(Formatting.Indented); }
        private static string Snapshot() { var outDir = Absolute(S12SlashCompiler.OutputFolderPath); var root = Path.Combine(Path.GetTempPath(), "vfxcomposer_s12c1_backup_" + Guid.NewGuid().ToString("N")); try { Directory.CreateDirectory(root); if (Directory.Exists(outDir)) Copy(outDir, Path.Combine(root, "out")); if (File.Exists(outDir + ".meta")) File.Copy(outDir + ".meta", Path.Combine(root, "out.meta")); return root; } catch { if (Directory.Exists(root)) Directory.Delete(root, true); throw; } }
        private static void Restore(string backup) { var output = Absolute(S12SlashCompiler.OutputFolderPath); if (Directory.Exists(output)) Directory.Delete(output, true); if (File.Exists(output + ".meta")) File.Delete(output + ".meta"); if (Directory.Exists(Path.Combine(backup, "out"))) Copy(Path.Combine(backup, "out"), output); if (File.Exists(Path.Combine(backup, "out.meta"))) File.Copy(Path.Combine(backup, "out.meta"), output + ".meta"); AssetDatabase.Refresh(); }
        private static void RestoreText(string recipe, string before, string history, string oldHistory) { Atomic(recipe, before); if (oldHistory == null) { if (File.Exists(history)) File.Delete(history); if (File.Exists(history + ".meta")) File.Delete(history + ".meta"); } else Atomic(history, oldHistory); foreach (var pending in new[] { recipe + ".pending", history + ".pending", Absolute(S12SlashCompiler.ManifestPath) + ".pending" }) if (File.Exists(pending)) File.Delete(pending); AssetDatabase.Refresh(); }
        private static void Copy(string source, string dest) { Directory.CreateDirectory(dest); foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { var to = Path.Combine(dest, file.Substring(source.Length).TrimStart('\\', '/')); Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Copy(file, to, true); } }
        private sealed class Op { public int Index; public string Path; public string Phase; public string Module; public string Parameter; public JToken Value; }
    }
}
