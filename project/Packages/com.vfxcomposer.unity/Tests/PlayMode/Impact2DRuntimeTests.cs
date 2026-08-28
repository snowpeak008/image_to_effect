using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class Impact2DRuntimeTests
    {
        [UnityTest]
        public IEnumerator FrostImpact_PlaysNaturallyCompletesAndResetsForPool()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/frost_impact_2d/VFX_frost_impact_2d.prefab");
#endif
            Assert.That(prefab, Is.Not.Null); var instance = Object.Instantiate(prefab); try
            {
                var entry = instance.GetComponent<IVfxRuntimeEntry>(); Assert.That(entry, Is.Not.Null); entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play(); yield return new WaitForSeconds(.08f); Assert.That(entry.IsAlive, Is.True); Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).Sum(system => system.particleCount), Is.GreaterThan(0)); yield return new WaitForSeconds(.5f); Assert.That(entry.IsAlive, Is.False); entry.Play(); yield return null; entry.Stop(VfxStopMode.Immediate); Assert.That(entry.IsAlive, Is.False); entry.ResetForPool(); Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).All(system => system.particleCount == 0), Is.True);
            }
            finally { Object.Destroy(instance); }
        }
    }
}
