using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VFXComposer;

namespace VFXComposer.Tests.PlayMode
{
    // Exercises serialized instances of the retained Generated Prefabs, not a synthetic fixture.
    public sealed class S11FormalGeneratedRuntimeTests
    {
        [UnityTest]
        public IEnumerator A1_A7_FormalGeneratedPrefabsInFixedScenes_PlayLaunchTravelImpactAtRuntime()
        {
            yield return Exercise("Assets/VFX/Preview/S7_2D_FireballPreview.unity", new Vector3(2f, 0f, 0f));
            yield return Exercise("Assets/VFX/Preview/S10_3D_FireballPreview.unity", new Vector3(2f, 1f, 3f));
        }

        private static IEnumerator Exercise(string path, Vector3 impact)
        {
            var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Assert.That(operation, Is.Not.Null, "Fixed release preview must be loadable by its serialized scene path.");
            yield return operation;
            var scene = SceneManager.GetSceneByPath(path);
            Assert.That(scene.isLoaded, Is.True);
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GeneratedVfxController>(true)).FirstOrDefault();
            Assert.That(controller, Is.Not.Null, "Fixed scene must contain a retained Generated Prefab instance.");
            controller.gameObject.SetActive(true);
            controller.PlayLaunch(); yield return null; Assert.That(controller.CurrentStage, Is.EqualTo(VfxRuntimeStage.Launch));
            controller.StartTravel(); yield return null; Assert.That(controller.CurrentStage, Is.EqualTo(VfxRuntimeStage.Travel));
            controller.PlayImpact(impact); yield return null; Assert.That(controller.CurrentStage, Is.EqualTo(VfxRuntimeStage.Impact));
            controller.StopEffect(true); Assert.That(controller.CurrentStage, Is.EqualTo(VfxRuntimeStage.None));
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
