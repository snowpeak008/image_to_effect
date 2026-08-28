using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.Preview;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class S12C2AiRecipeEvidenceTests
    {
        [Test]
        public void S12C2_RecordsMachineReadAiRecipeEvidenceAndRestoresCanonicalGeneratedOutput()
        {
            S12C2AiRecipeEvidence.EnsureRecorded();
            Assert.That(S12C2AiRecipeEvidence.VerifyExistingEvidence(), Is.True);
            var canonical = File.ReadAllText(Path.Combine(Application.dataPath, "VFX", "Recipes", "Slash", "slash-3d-stylized.default.v2.json")); var manifest = JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "VFX", "Generated", "slash_3d_stylized", "BuildManifest.json")));
            Assert.That((string)manifest["recipeHash"], Is.EqualTo(RecipeCanonicalizer.ComputeSha256(canonical)), "S12C2 must restore canonical formal Generated output after the AI evidence capture.");
            S12C2AiRecipeEvidence.AssertNoBuildResidue();
        }

        [Test]
        public void S12C2_LocalAiPrefabIsDeepCopiedAndExposesTheRecordedAiParameters()
        {
            S12C2AiRecipeEvidence.EnsureRecorded();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(S12C2AiRecipeEvidence.LocalPrefabPath); Assert.That(prefab, Is.Not.Null); Assert.That(AssetDatabase.AssetPathToGUID(S12C2AiRecipeEvidence.LocalPrefabPath), Is.Not.Empty); Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Null, "Local AI snapshot must not serialize missing scripts.");
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) foreach (var material in renderer.sharedMaterials) if (material != null) { var path = AssetDatabase.GetAssetPath(material); Assert.That(path, Does.StartWith(S12C2AiRecipeEvidence.LocalRoot + "/Materials/")); Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty); }
            Assert.That(prefab.transform.Find("Primary_arc/Arc_sweep/RibbonWidthControl").localScale.x, Is.EqualTo(1.25f).Within(.0001f)); var sparks = prefab.transform.Find("Sparks/Slash_sparks").GetComponent<ParticleSystem>(); var burst = sparks.emission.GetBurst(0); Assert.That(burst.maxCount, Is.EqualTo(18)); Assert.That(prefab.GetComponentInChildren<SlashAfterimageAlpha>(true).Alpha, Is.EqualTo(.4f).Within(.0001f));
        }
    }
}
