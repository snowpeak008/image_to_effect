using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W1NextCandidatePlayModeTests
    {
        private static readonly string[] StyleIds =
        {
            "style_orb_stylized_2d",
            "style_orb_cartoon_2d",
            "style_orb_pixel_2d",
            "style_orb_inkwash_2d",
            "style_orb_semireal_3d",
            "style_orb_holo_3d",
            "style_orb_dark_3d",
            "style_orb_neon_2d"
        };

        private static readonly string[] AllIds = StyleIds.Concat(new[]
        {
            "fan_wave_cartoon_showcase_2d",
            "charge_occlude_holo_showcase_3d",
            "telegraph_nova_holy_showcase_3d"
        }).ToArray();

        [UnityTest]
        public IEnumerator EightTokens_PlayDistinctRealCarriersInsideOneUniformEnvelope()
        {
            var signatures = new HashSet<string>();
            var instances = new List<W1NextCandidateRuntimeEntry>();
            foreach (var id in StyleIds)
            {
                var entry = Create(id);
                instances.Add(entry);
                Assert.That(entry.Kind, Is.EqualTo(W1NextCandidateKind.StyleToken));
                Assert.That(entry.DeclaredLocalBounds.size, Is.EqualTo(W1NextCandidateRuntimeEntry.UniformLocalEnvelope.size));
                Assert.That(entry.transform.localScale, Is.EqualTo(Vector3.one));
                entry.Play();
                yield return new WaitForSeconds(.18f);
                Assert.That(entry.IsAlive, Is.True, id);
                Assert.That(entry.VisibleRendererCount, Is.EqualTo(3), id);
                Assert.That(entry.LastStylePhase, Is.InRange(0f, 1f), id);
                Assert.That(entry.LastAppliedIntensity, Is.GreaterThan(.25f), id);
                Assert.That(entry.IsInsideDeclaredEnvelope(.025f), Is.True, id + " " + BoundsDiagnostic(entry));
                var real = ActualSignature(entry);
                signatures.Add(real);
                Assert.That(entry.VisualSignature, Is.EqualTo(real));
                entry.Stop(VfxStopMode.Immediate);
                Assert.That(entry.VisibleRendererCount, Is.EqualTo(0), id);
            }
            Assert.That(signatures.Count, Is.EqualTo(8));
            var dark = instances.Single(value => value.StyleToken == "dark");
            var stylized = instances.Single(value => value.StyleToken == "stylized");
            dark.Play();
            stylized.Play();
            yield return new WaitForSeconds(.22f);
            Assert.That(dark.LastAppliedIntensity, Is.GreaterThan(stylized.LastAppliedIntensity), "The Dark sample has a deliberate energy floor instead of the rejected weak presentation.");
            foreach (var entry in instances) Object.Destroy(entry.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeCapabilitySkinSamples_DriveTraceBackedBoundedVisibleTopology()
        {
            var fan = Create("fan_wave_cartoon_showcase_2d");
            fan.Play();
            Assert.That(fan.BehaviorTrace, Is.Not.Null);
            Assert.That(fan.BehaviorTrace.Events.Count(value => value.Type == "on_emit" && value.Detail == "fan"), Is.EqualTo(5));
            yield return new WaitForSeconds(.28f);
            var fanRenderers = fan.GetComponentsInChildren<MeshRenderer>(true).Where(value => value.enabled).ToArray();
            Assert.That(fanRenderers.Length, Is.EqualTo(5));
            Assert.That(fanRenderers.Select(value => Rounded(value.transform.localPosition)).Distinct().Count(), Is.EqualTo(5));
            Assert.That(fan.BehaviorTrace.Frames.Select(value => Mathf.RoundToInt(value.Position.y * 1000f)).Distinct().Count(), Is.GreaterThan(5));
            Assert.That(fan.IsInsideDeclaredEnvelope(.025f), Is.True);

            var beam = Create("charge_occlude_holo_showcase_3d");
            beam.Play();
            Assert.That(beam.BehaviorTrace.Events.Any(value => value.Detail == "occluded"), Is.True);
            yield return new WaitForSeconds(.42f);
            var earlyWidth = beam.LastBeamWidth;
            var earlyEndpoint = beam.LastBeamEndpoint;
            var line = beam.GetComponentInChildren<LineRenderer>(true);
            Assert.That(line.enabled, Is.True);
            Assert.That(Vector3.Distance(line.GetPosition(line.positionCount - 1), earlyEndpoint), Is.LessThan(.0001f));
            yield return new WaitForSeconds(.72f);
            Assert.That(beam.LastBeamWidth, Is.GreaterThan(earlyWidth));
            Assert.That(Vector3.Distance(beam.LastBeamEndpoint, earlyEndpoint), Is.GreaterThan(.08f), "The real line endpoint follows the changed occlusion distance.");
            Assert.That(beam.OcclusionTransitions, Is.GreaterThanOrEqualTo(1));
            Assert.That(line.endWidth, Is.GreaterThan(.02f));
            Assert.That(beam.IsInsideDeclaredEnvelope(.025f), Is.True);

            var nova = Create("telegraph_nova_holy_showcase_3d");
            nova.Play();
            Assert.That(nova.BehaviorTrace.Events.Count(value => value.Type == "on_emit" && value.Detail == "ring"), Is.EqualTo(12));
            yield return new WaitForSeconds(.22f);
            Assert.That(FindRenderer(nova, "TelegraphCarrier").enabled, Is.True);
            Assert.That(FindRenderer(nova, "NovaRingCarrier").enabled, Is.False);
            yield return new WaitForSeconds(.55f);
            Assert.That(nova.BehaviorTrace.Events.Any(value => value.Detail == "telegraph_complete"), Is.True);
            Assert.That(FindRenderer(nova, "TelegraphCarrier").enabled, Is.False);
            Assert.That(FindRenderer(nova, "NovaRingCarrier").enabled, Is.True);
            var motes = FindRenderer(nova, "TwelveMoteBurstCarrier");
            Assert.That(motes.enabled, Is.True);
            Assert.That(motes.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3, Is.EqualTo(12));
            Assert.That(nova.NovaVisibleMoteCount, Is.EqualTo(12));
            Assert.That(nova.IsInsideDeclaredEnvelope(.025f), Is.True);

            Object.Destroy(fan.gameObject);
            Object.Destroy(beam.gameObject);
            Object.Destroy(nova.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewScheduler_CleansReplaysDeterministicallyAndStaysWithinFixedBudgets()
        {
            var entries = AllIds.Select(Create).ToArray();
            var materialBindings = entries.ToDictionary(value => value, value => value.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.sharedMaterial).ToArray());
            var driverObject = new GameObject("W1NextCandidatePreviewDriverFixture");
            var driver = driverObject.AddComponent<W1NextCandidatePreviewDriver>();
            SetPrivate(driver, "runtimeEntries", entries);
            SetPrivate(driver, "playDuration", .08f);
            SetPrivate(driver, "cleanGap", .06f);
            yield return null;
            Assert.That(driver.ReplayCount, Is.EqualTo(1));
            var firstTraceHashes = entries.Where(value => value.Kind != W1NextCandidateKind.StyleToken).ToDictionary(value => value.Kind, value => value.BehaviorTrace.ToCanonicalJson());
            yield return new WaitForSeconds(.09f);
            Assert.That(driver.InCleanGap, Is.True);
            Assert.That(driver.AllEntriesIdle, Is.True);
            Assert.That(entries.All(value => value.VisibleRendererCount == 0 && !value.IsAlive), Is.True);
            yield return new WaitForSeconds(.075f);
            Assert.That(driver.ReplayCount, Is.EqualTo(2));
            foreach (var entry in entries.Where(value => value.Kind != W1NextCandidateKind.StyleToken)) Assert.That(entry.BehaviorTrace.ToCanonicalJson(), Is.EqualTo(firstTraceHashes[entry.Kind]));
            foreach (var entry in entries)
            {
                var budget = entry.ReadBudget();
                Assert.That(budget.GameObjects, Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxGameObjectsBudget));
                Assert.That(budget.Renderers, Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxRenderersBudget));
                Assert.That(budget.ParticleSystems, Is.EqualTo(W1NextCandidateRuntimeEntry.MaxParticleSystemsBudget));
                Assert.That(budget.Materials, Is.LessThanOrEqualTo(W1NextCandidateRuntimeEntry.MaxMaterialsBudget));
                CollectionAssert.AreEqual(materialBindings[entry], entry.GetComponentsInChildren<Renderer>(true).Select(renderer => renderer.sharedMaterial).ToArray(), "Replay must not instantiate or replace materials.");
            }
            driver.EnterCleanGap();
            Assert.That(driver.AllEntriesIdle, Is.True);
            Object.Destroy(driverObject);
            foreach (var entry in entries) Object.Destroy(entry.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ElevenEntries_StayInsideDeclaredEnvelopeAcrossTheWholePlayback()
        {
            var entries = AllIds.Select(Create).ToArray();
            foreach (var entry in entries)
            {
                Assert.That(entry.transform.localScale, Is.EqualTo(Vector3.one));
                entry.Play();
            }
            var elapsed = 0f;
            while (elapsed < 2.05f)
            {
                yield return null;
                elapsed += Mathf.Max(0f, Time.deltaTime);
                foreach (var entry in entries) Assert.That(entry.IsInsideDeclaredEnvelope(.005f), Is.True, entry.StyleToken + " / " + entry.Kind + " at " + elapsed.ToString("F3") + " " + BoundsDiagnostic(entry));
            }
            foreach (var entry in entries)
            {
                Assert.That(entry.VisibleRendererCount, Is.EqualTo(0));
                Object.Destroy(entry.gameObject);
            }
            yield return null;
        }

        private static W1NextCandidateRuntimeEntry Create(string id)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id + " must be built through W1NextCandidateAuthoring.BuildAllForBatch before PlayMode.");
            var instance = Object.Instantiate(prefab);
            var entry = instance.GetComponent<W1NextCandidateRuntimeEntry>();
            Assert.That(entry, Is.Not.Null, id);
            return entry;
        }

        private static string BoundsDiagnostic(W1NextCandidateRuntimeEntry entry)
        {
            Bounds current;
            return entry.TryGetCurrentLocalBounds(out current)
                ? "current=" + current.min.ToString("F4") + ".." + current.max.ToString("F4") + " allowed=" + entry.DeclaredLocalBounds.min.ToString("F4") + ".." + entry.DeclaredLocalBounds.max.ToString("F4")
                : "current=<none> allowed=" + entry.DeclaredLocalBounds.min.ToString("F4") + ".." + entry.DeclaredLocalBounds.max.ToString("F4");
        }

        private static string ActualSignature(W1NextCandidateRuntimeEntry entry)
        {
            var renderers = entry.GetComponentsInChildren<MeshRenderer>(true);
            var material = renderers.Select(value => value.sharedMaterial).Distinct().Single();
            var meshes = renderers.Select(value => value.GetComponent<MeshFilter>().sharedMesh.name).ToArray();
            return material.shader.name +
                   "|mode=" + Float(material, "_StyleMode") +
                   "|outline=" + Float(material, "_Outline") +
                   "|steps=" + Float(material, "_ShadingSteps") +
                   "|noise=" + Float(material, "_NoiseScale") +
                   "|blend=" + Float(material, "_DstBlend") +
                   "|meshes=" + string.Join(",", meshes) +
                   "|timing=" + entry.TimingProfile;
        }

        private static string Float(Material material, string property)
        {
            return material.GetFloat(property).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Rounded(Vector3 value)
        {
            return Mathf.RoundToInt(value.x * 1000f) + ":" + Mathf.RoundToInt(value.y * 1000f) + ":" + Mathf.RoundToInt(value.z * 1000f);
        }

        private static MeshRenderer FindRenderer(W1NextCandidateRuntimeEntry entry, string name)
        {
            return entry.GetComponentsInChildren<MeshRenderer>(true).Single(value => value.name == name);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
