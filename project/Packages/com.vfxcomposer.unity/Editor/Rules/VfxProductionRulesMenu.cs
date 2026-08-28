using System.IO;
using UnityEditor;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.SlashV2;

namespace VFXComposer.Editor.Rules
{
    public static class VfxProductionRulesMenu
    {
        [MenuItem("Tools/VFX Composer/Production Rules/Reconcile Current Outputs")]
        public static void ReconcileCurrentOutputs()
        {
            BuildV1("Assets/VFX/Recipes/fireball-2d.default.json");
            BuildV1("Assets/VFX/Recipes/fireball-3d.default.json");
            var slash = File.ReadAllText(Absolute("Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json"));
            var slashResult = new S12SlashCompiler().Build(slash);
            if (!slashResult.Succeeded) throw new System.InvalidOperationException("Slash production-rule reconciliation failed.");
        }

        private static void BuildV1(string recipePath)
        {
            var result = new VfxCompiler().Build(File.ReadAllText(Absolute(recipePath)));
            if (!result.Succeeded) throw new System.InvalidOperationException("Production-rule reconciliation failed for " + recipePath);
        }

        private static string Absolute(string assetPath) { return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length)); }
    }
}
