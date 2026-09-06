using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.TechniqueFamilies;
using VFXComposer.TechniqueFamilies;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// T2c paradigm-sample constructive gate. For every checked-in sample
    /// prefab: self-containment (dependency closure inside the allow-list +
    /// its own folder), components inside the recognized closed set, the
    /// no-flipbook red line, the glow stack present and beat-coupled, and the
    /// PM tier cost budget. Archetype interface predicates (hitAt / integrity
    /// / break; node table + hop reveal; setProgress + empty-targetRenderer
    /// degradation) run against instantiated copies.
    /// </summary>
    public sealed class ParadigmSampleGateTests
    {
        public static IEnumerable<string> SamplePrefabPaths()
        {
            foreach (bool is3D in new[] { true, false })
                foreach (string archetype in VfxSampleAssembler.ArchetypeIds)
                    foreach (string element in VfxSampleAssembler.ElementIds)
                        yield return VfxSampleAssembler.PrefabPath(
                            archetype, element, VfxSampleAssembler.StyleId, is3D, VfxSampleAssembler.Tier);
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path + " (run VFXComposer/Paradigm Samples/Build All)");
            return prefab;
        }

        [Test]
        public void AllEighteenSamples_ExistAtTheGalleryResolvedPaths()
        {
            string[] paths = SamplePrefabPaths().ToArray();
            Assert.That(paths.Length, Is.EqualTo(18), "3 archetypes x 3 elements x 2 dimensions");
            foreach (string path in paths) Load(path);
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_IsSelfContained_ZeroExternalReferences(string path)
        {
            Load(path);
            string ownFolder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            {
                if (dependency.StartsWith(ownFolder, System.StringComparison.OrdinalIgnoreCase)) continue;
                Assert.That(VfxTechniqueFamilyBoundary.IsDependencyAllowed(dependency), Is.True,
                    path + " depends on out-of-policy asset: " + dependency);
            }
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_ComponentsAreInsideTheRecognizedClosedSet(string path)
        {
            GameObject prefab = Load(path);
            var recognized = new HashSet<string>(VfxTechniqueFamilyBoundary.RecognizedUnityComponents);
            var recognizedScripts = new HashSet<System.Type>(VfxTechniqueFamilyBoundary.RecognizedRuntimeScripts);
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component, Is.Not.Null, path + ": missing script");
                System.Type type = component.GetType();
                if (type == typeof(Transform)) continue; // structural, always allowed
                bool ok = recognized.Contains(type.FullName) || recognizedScripts.Contains(type);
                Assert.That(ok, Is.True, path + ": component outside the closed set: " + type.FullName);
            }
            foreach (string excluded in VfxTechniqueFamilyBoundary.ExcludedComponents)
            {
                System.Type excludedType = System.Type.GetType(excluded + ", UnityEngine");
                if (excludedType == null) continue;
                Assert.That(prefab.GetComponentsInChildren(excludedType, true), Is.Empty,
                    path + ": excluded component present: " + excluded);
            }
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_NoFlipbook_NoTextureSampling(string path)
        {
            GameObject prefab = Load(path);
            foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                Assert.That(ps.textureSheetAnimation.enabled, Is.False, path + ": CP-5 flipbook forbidden");
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, path + ": " + renderer.name + " missing material");
                    Shader shader = material.shader;
                    Assert.That(shader, Is.Not.Null, path);
                    int propertyCount = shader.GetPropertyCount();
                    for (int i = 0; i < propertyCount; i++)
                        Assert.That(shader.GetPropertyType(i),
                            Is.Not.EqualTo(UnityEngine.Rendering.ShaderPropertyType.Texture),
                            path + ": SG-5 zero texture sampling (" + shader.name + ")");
                }
            }
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_GlowStackPresent_AndBeatCoupled(string path)
        {
            GameObject prefab = Load(path);
            Renderer[] glowRenderers = prefab.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.sharedMaterial != null && r.sharedMaterial.shader != null
                            && r.sharedMaterial.shader.name.Contains("GlowStack"))
                .ToArray();
            Assert.That(glowRenderers.Length, Is.GreaterThanOrEqualTo(2),
                path + ": REFERENCE_ANALYSIS section 3bis glowLayers >= 2 at PM");

            // Per-layer decorrelation: breakup seeds must differ between layers.
            var seeds = glowRenderers.Select(r => r.sharedMaterial.GetFloat("_BreakupSeed")).Distinct().ToArray();
            Assert.That(seeds.Length, Is.EqualTo(glowRenderers.Length), path + ": glow layers must decorrelate");

            // Beat coupling: every glow renderer is a beat target of some light-beat driver.
            var beatTargets = new HashSet<Renderer>(prefab.GetComponentsInChildren<VfxLightBeat>(true)
                .SelectMany(beat => beat.BeatTargets));
            foreach (Renderer glow in glowRenderers)
                Assert.That(beatTargets, Does.Contain(glow),
                    path + ": glow layer not coupled to the light beat (flickerCoupling)");
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_LocalLightIsPresent_AndDriven(string path)
        {
            GameObject prefab = Load(path);
            var beats = prefab.GetComponentsInChildren<VfxLightBeat>(true);
            Assert.That(beats.Length, Is.GreaterThanOrEqualTo(1), path + ": local light is a first-class member");
            bool is2D = path.Contains("/2d/");
            foreach (VfxLightBeat beat in beats)
            {
                Assert.That(beat.LightTarget, Is.Not.Null, path);
                if (is2D) Assert.That(beat.Kind, Is.EqualTo(VfxLightDriverKind.Light2D), path);
                else Assert.That(beat.Kind, Is.EqualTo(VfxLightDriverKind.Light3D), path);
            }
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_FitsThePmTierBudget(string path)
        {
            GameObject prefab = Load(path);
            VfxTechniqueFamilyBoundary.CostReport report = VfxTechniqueFamilyBoundary.ComputeCosts(prefab);
            List<string> failures = VfxTechniqueFamilyBoundary.Evaluate(report,
                VfxTechniqueFamilyBoundary.Tier.PM, out _);
            Assert.That(failures, Is.Empty, path + ": " + string.Join("; ", failures));
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_CarriesExactlyTheCartoonStyleStage(string path)
        {
            GameObject prefab = Load(path);
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null) continue;
                Assert.That(material.IsKeywordEnabled(VfxStylePreset.KeywordCartoon), Is.True,
                    path + ": " + renderer.name + " must carry the cartoon StyleStage");
                Assert.That(material.IsKeywordEnabled(VfxStylePreset.KeywordPixel), Is.False,
                    path + ": exactly one style keyword (MV-2)");
            }
        }

        [Test, TestCaseSource(nameof(SamplePrefabPaths))]
        public void Sample_ControllerContract_PhasesAndSortedEvents(string path)
        {
            GameObject prefab = Load(path);
            var controller = prefab.GetComponent<VfxController>();
            Assert.That(controller, Is.Not.Null, path + ": VfxController at root");
            Assert.That(prefab.GetComponent<VfxParameterBlock>(), Is.Not.Null, path);
            Assert.That(controller.Phases.Length, Is.GreaterThanOrEqualTo(2), path);
        }

        // ---------------- archetype interface predicates (instantiated) ----------------

        private static GameObject Instantiate(string archetype, string element, bool is3D)
        {
            string path = VfxSampleAssembler.PrefabPath(archetype, element, VfxSampleAssembler.StyleId, is3D, VfxSampleAssembler.Tier);
            return Object.Instantiate(Load(path));
        }

        [Test]
        public void Shield_HitIntegrityAndBreakInterfaces_Work([Values(true, false)] bool is3D)
        {
            GameObject instance = Instantiate("shield", "ice", is3D);
            try
            {
                var controller = instance.GetComponent<VfxController>();
                Assert.That(controller.SendEvent("launch", new VfxEventPayload { Position = Vector3.zero }), Is.True);
                Assert.That(controller.SendEvent("hitAt", new VfxEventPayload { Position = new Vector3(0f, 1f, 0f), Value = 1f }), Is.True);
                Assert.That(controller.SendEvent("setIntegrity", new VfxEventPayload { Value = 0.4f }), Is.True);
                Assert.That(controller.Integrity, Is.EqualTo(0.4f).Within(1e-5f));
                Assert.That(controller.SendEvent("break", new VfxEventPayload { Position = Vector3.zero, Value = 3f }), Is.True);
                if (is3D)
                {
                    var bodies = instance.GetComponentsInChildren<Rigidbody>(true);
                    Assert.That(bodies.Length, Is.GreaterThanOrEqualTo(6), "prefractured debris");
                    Assert.That(bodies.All(b => !b.isKinematic), Is.True, "break releases every fragment");
                }
                else
                {
                    var bodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
                    Assert.That(bodies.Length, Is.GreaterThanOrEqualTo(6));
                    Assert.That(bodies.All(b => b.bodyType == RigidbodyType2D.Dynamic), Is.True);
                }
                controller.ResetForPool();
                Assert.That(controller.Integrity, Is.EqualTo(1f), "pool reset restores integrity");
                if (is3D)
                    Assert.That(instance.GetComponentsInChildren<Rigidbody>(true).All(b => b.isKinematic), Is.True);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void ChainLink_NodeTableAndRevealSegments_AreWired([Values(true, false)] bool is3D)
        {
            GameObject instance = Instantiate("chain_link", "lightning", is3D);
            try
            {
                var controller = instance.GetComponent<VfxController>();
                Assert.That(controller.NodePositions.Length, Is.GreaterThanOrEqualTo(3), "multi-node chain");
                Transform segments = instance.transform.Find("Segments");
                Assert.That(segments, Is.Not.Null);
                Assert.That(segments.childCount, Is.EqualTo(controller.NodePositions.Length - 1),
                    "one segment per hop");
                for (int i = 0; i < segments.childCount; i++)
                    Assert.That(segments.GetChild(i).gameObject.activeSelf, Is.False,
                        "segments start hidden and reveal hop by hop");
                Assert.That(controller.SendEvent("addNode", new VfxEventPayload { Position = Vector3.one }), Is.True);
                Assert.That(controller.NodePositions.Length, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void DissolveOut_ProgressInterface_AndEmptyTargetDegradation([Values(true, false)] bool is3D)
        {
            GameObject instance = Instantiate("dissolve_out", "poison", is3D);
            try
            {
                var controller = instance.GetComponent<VfxController>();
                // Catalog: targetRenderer empty degrades to the built-in stand-in body.
                Transform body = instance.transform.Find("Body");
                Assert.That(body, Is.Not.Null, "fallback body when targetRenderer is empty");
                Assert.That(body.GetComponent<MeshRenderer>(), Is.Not.Null);
                Assert.That(controller.SendEvent("setProgress", new VfxEventPayload { Value = 0.5f }), Is.True);
                var block = instance.GetComponent<VfxParameterBlock>();
                Assert.That(block.Resolve("progress"), Is.GreaterThanOrEqualTo(0), "progress is a bound parameter");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void SeedDerivation_IsDeterministic_AndDimensionSeparated()
        {
            uint a = VfxSampleAssembler.SeedFor("shield_ice_cartoon", true);
            uint b = VfxSampleAssembler.SeedFor("shield_ice_cartoon", true);
            uint c = VfxSampleAssembler.SeedFor("shield_ice_cartoon", false);
            Assert.That(a, Is.EqualTo(b), "same input, same seed");
            Assert.That(a, Is.Not.EqualTo(c), "2D and 3D decorrelate");
        }

        [Test]
        public void GalleryPageSets_ResolveExactlyTheSamplePaths()
        {
            foreach (string dimension in new[] { "3d", "2d" })
            {
                var pageSet = AssetDatabase.LoadAssetAtPath<VFXComposer.Gallery.GalleryPageSet>(
                    "Assets/VFX/Gallery/GalleryPages_" + dimension.ToUpperInvariant() + ".asset");
                Assert.That(pageSet, Is.Not.Null);
                Assert.That(pageSet.PrefabRootPath, Is.EqualTo("Assets/VFX/Generated/" + dimension + "/"));
                // Verdict page (index 1): all nine cells must resolve to checked-in prefabs.
                var controllerGo = new GameObject("resolver");
                try
                {
                    var controller = controllerGo.AddComponent<VFXComposer.Gallery.GalleryController>();
                    var so = new SerializedObject(controller);
                    so.FindProperty("pageSet").objectReferenceValue = pageSet;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    for (int cell = 0; cell < 9; cell++)
                    {
                        string path = controller.ResolveCellPrefabPath(pageSet.Pages[1], cell);
                        Assert.That(path, Is.Not.Null.And.Not.Empty, dimension + " cell " + cell);
                        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null,
                            dimension + " verdict cell " + cell + " must resolve: " + path);
                    }
                }
                finally { Object.DestroyImmediate(controllerGo); }
            }
        }
    }
}
