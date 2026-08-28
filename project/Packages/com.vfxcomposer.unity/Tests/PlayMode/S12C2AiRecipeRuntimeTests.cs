using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VFXComposer;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class S12C2AiRecipeRuntimeTests
    {
        [UnityTest]
        public IEnumerator S12C2_LocalAiSnapshotRunsTheRecordedNonDefaultSlash()
        {
            const string scenePath = "Assets/VFX/Preview/S12_AI_ValidatedSlash/S12_AI_ValidatedSlashPreview.unity";
            var operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive); Assert.That(operation, Is.Not.Null, "Run the S12C2 EditMode recorder before this PlayMode test."); yield return operation; var scene = SceneManager.GetSceneByPath(scenePath); var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SlashEffectController>(true)).Single();
            Assert.That(controller.GetComponentInChildren<SlashAfterimageAlpha>(true).Alpha, Is.EqualTo(.4f).Within(.0001f)); controller.PlaySlash(Vector3.zero, Quaternion.identity); yield return new WaitForSeconds(.18f); Assert.That(controller.IsPhaseVisible("primary_arc"), Is.True); Assert.That(controller.IsPhaseVisible("afterimage"), Is.True); var sparks = controller.GetComponentsInChildren<ParticleSystem>(true).Single(item => item.name == "Slash_sparks"); Assert.That(sparks.particleCount, Is.GreaterThanOrEqualTo(5)); var particles = new ParticleSystem.Particle[sparks.particleCount]; var count = sparks.GetParticles(particles); Assert.That(particles.Take(count).Select(item => item.position.ToString("F4")).Distinct().Count(), Is.GreaterThanOrEqualTo(5)); yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
