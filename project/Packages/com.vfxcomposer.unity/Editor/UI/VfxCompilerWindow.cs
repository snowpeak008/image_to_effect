using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Preview;
using VFXComposer.Editor.SlashV2;

namespace VFXComposer.Editor.UI
{
    public sealed class VfxCompilerWindow : EditorWindow
    {
        private TextAsset recipe;
        private TextAsset patch;
        private int expectedRevision = 1;
        private VfxPatchResult lastPatchValidation;
        private bool patchInputsChanged;
        private Vector2 scroll;
        private string report = "Choose a Recipe JSON, then Validate, Dry Run, or Build.";

        [MenuItem("Tools/VFX Composer/Compiler")]
        public static void Open() { GetWindow<VfxCompilerWindow>("VFX Compiler"); }

        private void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            recipe = (TextAsset)EditorGUILayout.ObjectField("Recipe", recipe, typeof(TextAsset), false);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("S8 Patch (full rebuild fallback; impact report remains module-precise)", EditorStyles.boldLabel);
            patch = (TextAsset)EditorGUILayout.ObjectField("Patch TextAsset", patch, typeof(TextAsset), false);
            expectedRevision = EditorGUILayout.IntField("Expected Revision", expectedRevision);
            if (EditorGUI.EndChangeCheck()) { lastPatchValidation = null; patchInputsChanged = true; }
            using (new EditorGUI.DisabledScope(recipe == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Validate")) Validate();
                if (GUILayout.Button("Dry Run")) DryRun();
                if (GUILayout.Button("Build")) Build();
                if (GUILayout.Button("Preview")) PreviewSelectedRecipe(recipe, out report);
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(recipe == null || patch == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Validate Patch")) ValidatePatch();
                EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUI.DisabledScope(recipe == null || patch == null || patchInputsChanged || lastPatchValidation == null || !lastPatchValidation.IsValid))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply Patch")) ApplyPatch();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Report");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void Validate()
        {
            var plan = new S12CompilerDispatcher().Validate(recipe.text);
            report = Format(plan, false);
        }
        private void DryRun()
        {
            var plan = new S12CompilerDispatcher().DryRun(recipe.text);
            report = Format(plan, true);
        }
        private void Build()
        {
            var result = new S12CompilerDispatcher().Build(recipe.text);
            report = Format(result.Plan, true) + "\nBuild: " + (result.Succeeded ? "succeeded" : "failed or blocked") + (string.IsNullOrEmpty(result.PrefabPath) ? string.Empty : "\nPrefab: " + result.PrefabPath);
        }
        private void ValidatePatch()
        {
            var dispatch = S12RecipeDispatcher.Parse(recipe.text);
            var result = dispatch.RecipeVersion == 2 ? new S12SlashPatchService().Validate(recipe.text, patch.text, expectedRevision) : new VfxPatchService().Validate(recipe.text, patch.text, expectedRevision);
            lastPatchValidation = result;
            patchInputsChanged = false;
            report = FormatPatch(result);
        }
        private void ApplyPatch()
        {
            var path = AssetDatabase.GetAssetPath(recipe);
            var dispatch = S12RecipeDispatcher.Parse(recipe.text);
            var result = dispatch.RecipeVersion == 2 ? new S12SlashPatchService().ApplyToAsset(path, patch.text, expectedRevision) : new VfxPatchService().ApplyToAsset(path, patch.text, expectedRevision);
            report = FormatPatch(result);
            lastPatchValidation = null;
            patchInputsChanged = true;
            if (result.IsValid) { expectedRevision = result.AfterRevision; recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(path); }
        }
        /// <summary>Dimension-safe Preview dispatch used by the UI and EditMode integration tests.</summary>
        public static bool PreviewSelectedRecipe(TextAsset selectedRecipe, out string status)
        {
            if (selectedRecipe == null) { status = "Preview blocked: choose a Recipe."; return false; }
            var slashDispatch = S12RecipeDispatcher.Parse(selectedRecipe.text);
            if (!slashDispatch.Report.HasErrors && slashDispatch.RecipeVersion == 2)
            {
                if (!string.Equals(slashDispatch.SlashV2.Id, "slash_3d_stylized", System.StringComparison.Ordinal)) { status = "Preview blocked: S12 v2 output currently supports only id slash_3d_stylized."; return false; }
                try { if (!S12SlashGeneratedPreview.OpenOrCreate(selectedRecipe)) { status = "Preview cancelled before selected S12 v2 Recipe build."; return false; } }
                catch (System.Exception exception) { status = "Preview blocked: selected S12 v2 Recipe could not build.\n" + exception.Message; return false; }
                status = "Built selected S12 Slash v2 Recipe revision " + slashDispatch.SlashV2.Revision + " and opened its generated preview."; return true;
            }
            var parsed = VfxDomainParser.ParseRecipe(selectedRecipe.text);
            if (parsed.Report.HasErrors)
            {
                status = "Preview blocked: Recipe dimension could not be parsed.\n" + string.Join("\n", parsed.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " — " + entry.Message));
                return false;
            }
            if (parsed.Value.Dimension == RecipeDimension.ThreeD)
            {
                S10PreviewScene.OpenOrCreate();
                status = "Opened S10 3D perspective preview for " + parsed.Value.Id + ".";
                return true;
            }
            S7PreviewScene.OpenOrCreate();
            status = "Opened S7 2D orthographic preview for " + parsed.Value.Id + ".";
            return true;
        }
        private static string Format(VfxBuildPlan plan, bool includeItems)
        {
            var lines = plan.Report.Entries.Select(entry => entry.Code + " " + entry.Severity + " " + entry.Path + " — " + entry.Message).ToList();
            if (includeItems) lines.AddRange(plan.Items.Select(item => item.State + " " + item.AssetPath + " — " + item.Reason));
            if (lines.Count == 0) lines.Add("Valid.");
            return string.Join("\n", lines);
        }
        private static string FormatPatch(VfxPatchResult result)
        {
            var lines = result.Report.Entries.Select(entry => entry.Code + " " + entry.Severity + " " + entry.Path + " — " + entry.Message).ToList();
            if (result.FailedOperationIndex.HasValue) lines.Add("Failed operation index: " + result.FailedOperationIndex.Value);
            else if (result.IsPostPatchValidationFailure) lines.Add("Failed operation index: post-patch validation (unattributed).");
            if (result.IsValid) lines.Add("Revision: " + result.BeforeRevision + " -> " + result.AfterRevision);
            lines.AddRange(result.AffectedItems.Select(item => item.State + (item.IsStage ? " /stages/" + item.StageId : " /stages/" + item.StageId + "/modules/" + item.ModuleId)));
            lines.Add("Build mode: full rebuild fallback; no asset-level partial write is claimed.");
            return string.Join("\n", lines);
        }
    }
}
