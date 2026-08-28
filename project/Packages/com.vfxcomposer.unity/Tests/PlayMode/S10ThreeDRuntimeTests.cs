using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class S10ThreeDRuntimeTests
    {
        [UnityTest]
        public IEnumerator SpatialFireballFixture_CompletesLaunchTravelImpactWithMeshBillboardAndTrail()
        {
            var root = new GameObject("S10_3D_RuntimeFixture");
            var launch = Stage(root, "Launch", false, false);
            var travel = Stage(root, "Travel", true, true);
            var impact = Stage(root, "Impact", false, false);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere); core.transform.SetParent(travel.transform, false); core.AddComponent<VFXComposer.CameraFacingBillboard>();
            var trail = travel.AddComponent<TrailRenderer>(); trail.time = .2f; trail.minVertexDistance = .001f;
            var controller = root.AddComponent<VFXComposer.GeneratedVfxController>();
            Set(controller, "launchRoot", launch); Set(controller, "travelRoot", travel); Set(controller, "impactRoot", impact);
            yield return null;
            controller.PlayLaunch(); Assert.That(controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.Launch));
            controller.StartTravel(); controller.SetTravelTransform(new Vector3(1f, .5f, 2f), Quaternion.Euler(0f, 35f, 0f)); trail.AddPositions(new[] { Vector3.zero, new Vector3(1f, .5f, 2f) }); Assert.That(trail.positionCount, Is.GreaterThan(0));
            controller.PlayImpact(new Vector3(3f, 0f, 4f)); Assert.That(controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.Impact)); Assert.That(trail.positionCount, Is.EqualTo(0));
            controller.StopEffect(true); Assert.That(controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.None));
            Object.Destroy(root);
        }

        private static GameObject Stage(GameObject root, string name, bool loop, bool active)
        {
            var stage = new GameObject(name); stage.transform.SetParent(root.transform, false); stage.SetActive(active);
            var particle = stage.AddComponent<ParticleSystem>(); var main = particle.main; main.loop = loop; main.startLifetime = .1f; main.playOnAwake = false; return stage;
        }
        private static void Set(object instance, string field, object value) { instance.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(instance, value); }
    }
}
