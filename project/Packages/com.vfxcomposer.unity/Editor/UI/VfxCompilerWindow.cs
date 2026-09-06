using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;

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
            var plan = new VfxCompiler().DryRun(recipe.text);
            report = Format(plan, false);
        }
        private void DryRun()
        {
            var plan = new VfxCompiler().DryRun(recipe.text);
            report = Format(plan, true);
        }
        private void Build()
        {
            var result = new VfxCompiler().Build(recipe.text);
            report = Format(result.Plan, true) + "\nBuild: " + (result.Succeeded ? "succeeded" : "failed or blocked") + (string.IsNullOrEmpty(result.PrefabPath) ? string.Empty : "\nPrefab: " + result.PrefabPath);
        }
        private void ValidatePatch()
        {
            var result = new VfxPatchService().Validate(recipe.text, patch.text, expectedRevision);
            lastPatchValidation = result;
            patchInputsChanged = false;
            report = FormatPatch(result);
        }
        private void ApplyPatch()
        {
            var path = AssetDatabase.GetAssetPath(recipe);
            var result = new VfxPatchService().ApplyToAsset(path, patch.text, expectedRevision);
            report = FormatPatch(result);
            lastPatchValidation = null;
            patchInputsChanged = true;
            if (result.IsValid) { expectedRevision = result.AfterRevision; recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(path); }
        }
        /// <summary>
        /// ADR-010 §8: the paradigm gallery scenes are the acceptance surface. The old per-dimension
        /// preview scene generators were removed with the legacy content layer.
        /// </summary>
        public static bool PreviewSelectedRecipe(TextAsset selectedRecipe, out string status)
        {
            if (selectedRecipe == null) { status = "Preview blocked: choose a Recipe."; return false; }
            status = "Preview: open Assets/VFX/Gallery/VFXGallery_2D.unity or VFXGallery_3D.unity (ADR-010 §8 acceptance surface).";
            return false;
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
