using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.TechniqueFamilies;
using VFXComposer.TechniqueFamilies;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Unit 5 gate (T2b): compiler boundary v2 constructive predicates —
    /// recognized/excluded component closed sets, dependency roots, six-tier
    /// cost model, ~112 binding keys (content-growth-free), PX-6 as a
    /// conditional predicate (user decision #16), and the technique-family
    /// shader library asset gate. The legacy kind closed set and its exemption
    /// tables are deliberately untouched (T2c scope).
    /// </summary>
    public sealed class TechniqueFamilyBoundaryGateTests
    {
        [Test]
        public void BindingKeys_AreUniqueThreeSegment_AndContentFree()
        {
            string[] keys = VfxTechniqueFamilyBoundary.BindingKeys;
            Assert.That(keys.Length, Is.InRange(100, 125), "~112 keys, bounded by Unity API surface");
            Assert.That(keys, Is.Unique);
            var families = new HashSet<string> { "mat", "gpu", "cpu", "mesh", "light", "ctrl" };
            foreach (string key in keys)
            {
                string[] parts = key.Split('.');
                Assert.That(parts.Length, Is.InRange(2, 3), key + ": three-segment (family.target.property)");
                Assert.That(families, Does.Contain(parts[0]), key + ": family must be one of the 5 + ctrl");
                // Content-growth freedom: no variant / element / archetype names in keys.
                Assert.That(key, Does.Not.Contain("fire").And.Not.Contain("ice").And.Not.Contain("lightning"),
                    key + ": binding keys must never encode content names");
            }
        }

        [Test]
        public void BindingKeys_ResolveRoundTrip_AndUnknownRejected()
        {
            Assert.That(VfxTechniqueFamilyBoundary.ResolveBindingKey("mat.prop.float"), Is.GreaterThanOrEqualTo(0));
            Assert.That(VfxTechniqueFamilyBoundary.ResolveBindingKey("light.beat.flickerDepth"), Is.GreaterThanOrEqualTo(0));
            Assert.That(VfxTechniqueFamilyBoundary.ResolveBindingKey("mat.prop.arbitraryReflectionPath"), Is.EqualTo(-1),
                "unknown keys are rejected at compile time (fail-closed)");
            Assert.That(VfxTechniqueFamilyBoundary.ResolveBindingKey("evil.GetType.Invoke"), Is.EqualTo(-1));
        }

        [Test]
        public void RecognizedAndExcludedComponentSets_DoNotOverlap()
        {
            var recognized = new HashSet<string>(VfxTechniqueFamilyBoundary.RecognizedUnityComponents);
            foreach (string excluded in VfxTechniqueFamilyBoundary.ExcludedComponents)
                Assert.That(recognized, Does.Not.Contain(excluded), excluded);
            // Animator exclusion is load-bearing (baked time series = flipbook kin).
            Assert.That(VfxTechniqueFamilyBoundary.ExcludedComponents, Does.Contain("UnityEngine.Animator"));
            Assert.That(VfxTechniqueFamilyBoundary.ExcludedComponents, Does.Contain("UnityEngine.ParticleSystemForceField"));
        }

        [Test]
        public void DependencyRoots_ExcludeGallery_AndRetiredRoots()
        {
            string[] roots = VfxTechniqueFamilyBoundary.AllowedDependencyRoots;
            // GA-9: gallery is scene tooling, never a product dependency root.
            Assert.That(roots.Any(r => r.StartsWith("Assets/VFX/Gallery", StringComparison.OrdinalIgnoreCase)), Is.False);
            // v2 retirements (spec section 3.4).
            Assert.That(roots, Does.Not.Contain("Assets/VFX/Effects/"));
            Assert.That(roots, Does.Not.Contain("Assets/VFX/Templates/"));
            Assert.That(VfxTechniqueFamilyBoundary.IsDependencyAllowed("Assets/VFX/Gallery/GalleryVolume.asset"), Is.False);
            Assert.That(VfxTechniqueFamilyBoundary.IsDependencyAllowed("Assets/VFX/TechniqueFamilies/Shaders/VFX_GlowStack.shader"), Is.True);
            Assert.That(VfxTechniqueFamilyBoundary.IsDependencyAllowed("Assets/RandomUserFolder/thing.png"), Is.False);
        }

        [Test]
        public void CostLimits_AreMonotonicWhereTheSpecSaysSo()
        {
            var tiers = new[]
            {
                VfxTechniqueFamilyBoundary.Tier.ML, VfxTechniqueFamilyBoundary.Tier.MM,
                VfxTechniqueFamilyBoundary.Tier.MH, VfxTechniqueFamilyBoundary.Tier.PL,
                VfxTechniqueFamilyBoundary.Tier.PM, VfxTechniqueFamilyBoundary.Tier.PH
            };
            for (int i = 1; i < tiers.Length; i++)
            {
                var lo = VfxTechniqueFamilyBoundary.GetLimits(tiers[i - 1]);
                var hi = VfxTechniqueFamilyBoundary.GetLimits(tiers[i]);
                // Sum-based budgets never shrink as tiers rise (C_mat/C_vert are
                // per-layer and legitimately dip at PL per the spec table).
                Assert.That(hi.GpuParticles, Is.GreaterThanOrEqualTo(lo.GpuParticles));
                Assert.That(hi.CpuParticlePeak, Is.GreaterThanOrEqualTo(lo.CpuParticlePeak));
                Assert.That(hi.Fragments, Is.GreaterThanOrEqualTo(lo.Fragments));
                Assert.That(hi.LocalLights, Is.GreaterThanOrEqualTo(lo.LocalLights));
            }
            // Spec spot-checks.
            var ml = VfxTechniqueFamilyBoundary.GetLimits(VfxTechniqueFamilyBoundary.Tier.ML);
            Assert.That(ml.GpuParticles, Is.EqualTo(0), "GPU particles banned at ML");
            Assert.That(ml.LocalLights, Is.EqualTo(0), "ML is the baked-light tier");
            var ph = VfxTechniqueFamilyBoundary.GetLimits(VfxTechniqueFamilyBoundary.Tier.PH);
            Assert.That(ph.Fragments, Is.EqualTo(200));
        }

        [Test]
        public void CostModel_Evaluate_FlagsViolations_AndOverdrawDualThreshold()
        {
            var report = new VfxTechniqueFamilyBoundary.CostReport
            {
                CpuParticlePeak = 500, LocalLights = 3, Fragments = 40,
                OverdrawEstimate = 5, MaxLayerVertexCount = 100
            };
            List<string> failures = VfxTechniqueFamilyBoundary.Evaluate(report,
                VfxTechniqueFamilyBoundary.Tier.MM, out List<string> warnings);
            Assert.That(failures, Has.Some.Contains("C_cpu"));
            Assert.That(failures, Has.Some.Contains("C_light"));
            Assert.That(failures, Has.Some.Contains("C_frag"));
            // Overdraw 5 > MM limit 3 * 1.5 = 4.5 -> hard failure.
            Assert.That(failures, Has.Some.Contains("C_over"));

            // Warn band: overdraw just above the limit but below 1.5x.
            var warnReport = new VfxTechniqueFamilyBoundary.CostReport { OverdrawEstimate = 4 };
            failures = VfxTechniqueFamilyBoundary.Evaluate(warnReport, VfxTechniqueFamilyBoundary.Tier.MM, out warnings);
            Assert.That(failures, Is.Empty);
            Assert.That(warnings, Has.Some.Contains("W403"));
        }

        [Test]
        public void CostModel_ComputeCosts_ReadsBuiltProduct()
        {
            var root = new GameObject("product");
            try
            {
                var psGo = new GameObject("particles");
                psGo.transform.SetParent(root.transform);
                var ps = psGo.AddComponent<ParticleSystem>();
                VfxCpuParticleTemplates.Configure(ps, "cpu_burst_radial", 1u, 100);

                var lightGo = new GameObject("light");
                lightGo.transform.SetParent(root.transform);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;

                VfxTechniqueFamilyBoundary.CostReport report = VfxTechniqueFamilyBoundary.ComputeCosts(root);
                Assert.That(report.CpuParticlePeak, Is.GreaterThan(0));
                Assert.That(report.LocalLights, Is.EqualTo(1));
                Assert.That(report.Fragments, Is.EqualTo(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Px6_ConditionalPredicate_SkipsMissingCategories()
        {
            // A product with no mesh, no Lit layer and no particles imposes no
            // voxel-trio requirements (user decision #16).
            var root = new GameObject("pixel_product_no_categories");
            try
            {
                bool ok = VfxTechniqueFamilyBoundary.ValidatePixelVoxelTrio(root, out List<string> violations);
                Assert.That(ok, Is.True);
                Assert.That(violations, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Px6_ConditionalPredicate_FlagsPixelMeshWithoutSnap()
        {
            Shader shader = Shader.Find("VFXComposer/TechniqueFamilies/SdfShape");
            Assert.That(shader, Is.Not.Null);
            var root = new GameObject("pixel_product");
            var material = new Material(shader);
            try
            {
                material.EnableKeyword(VfxStylePreset.KeywordPixel);
                material.SetFloat("_PixelSize", 0f); // snap missing
                var meshGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                meshGo.transform.SetParent(root.transform);
                meshGo.GetComponent<MeshRenderer>().sharedMaterial = material;

                bool ok = VfxTechniqueFamilyBoundary.ValidatePixelVoxelTrio(root, out List<string> violations);
                Assert.That(ok, Is.False);
                Assert.That(violations, Has.Some.Contains("PX-6a"));

                material.SetFloat("_PixelSize", 0.0625f); // snap present
                ok = VfxTechniqueFamilyBoundary.ValidatePixelVoxelTrio(root, out violations);
                Assert.That(ok, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        // ---------------- technique-family shader library gate ----------------

        private const string ShaderRoot = "Assets/VFX/TechniqueFamilies/Shaders";

        [Test]
        public void ShaderLibrary_AllShadersImport_AndCarryStyleStage()
        {
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { ShaderRoot });
            Assert.That(guids.Length, Is.GreaterThanOrEqualTo(20), "the 20-variant shader library must import");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.That(shader, Is.Not.Null, path);
                string source = File.ReadAllText(path);
                Assert.That(source, Does.Contain("VfxApplyStyleStage").Or.Contain("GlowFalloff"),
                    path + ": every variant ends in the StyleStage slot");
                Assert.That(source, Does.Contain("multi_compile_local"), path);
                // Red line: no flipbook/sequence sampling anywhere in the library.
                Assert.That(source.ToLowerInvariant(), Does.Not.Contain("flipbook"), path);
            }
        }

        [Test]
        public void ShaderLibrary_ZeroTextureSamplingProperties()
        {
            // SG-5 analogue for the hand-written HLSL route: the form library
            // must be fully procedural — no Texture2D properties at all.
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { ShaderRoot });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                {
                    Assert.That(shader.GetPropertyType(i),
                        Is.Not.EqualTo(UnityEngine.Rendering.ShaderPropertyType.Texture),
                        path + ": gate SG-5 — no texture sampling in the form library");
                }
            }
        }

        [Test]
        public void RefractionShader_DeclaresBothRoutes()
        {
            string path = ShaderRoot + "/VFX_RefractDual.shader";
            string source = File.ReadAllText(path);
            // MV-8: multi_compile keeps both routes alive in player builds.
            Assert.That(source, Does.Contain("multi_compile_local _ _REFRACT_SCENECOLOR"));
            Assert.That(source, Does.Contain("SampleSceneColor"), "true refraction route");
            Assert.That(source, Does.Contain("reflect("), "degraded specular route");
        }

        [Test]
        public void GlowShader_CarriesTheFullParameterSurface()
        {
            var shader = Shader.Find("VFXComposer/TechniqueFamilies/GlowStack");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                // ADR-010 section 4bis-6 mandated parameter surface, including
                // the M-13 size-intensity coupling pair.
                foreach (string prop in new[]
                {
                    "_InnerColor", "_OuterColor", "_ColorMixPower", "_FalloffParams",
                    "_BreakupNoise", "_BreakupAngularFreq", "_BreakupSeed",
                    "_Anisotropy", "_GlowAxisWS", "_BeatValue", "_FlickerCoupling", "_LayerIndex",
                    "_GlowSize", "_SizeCouplingK"
                })
                {
                    Assert.That(material.HasProperty(prop), Is.True, prop);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(material); }
        }

        [Test]
        public void GlowShader_SizeCouplingCompensation_IsInTheInstructionStream()
        {
            // M-13 (TECHNIQUE_VARIANT_LIBRARY_v1 section 3): the compensation
            // chain glowIntensity * pow(glowSize, k) must exist in the shader
            // so raising glowSize never silently fades the glow.
            string source = File.ReadAllText(ShaderRoot + "/VFX_GlowStack.shader");
            Assert.That(source, Does.Contain("_GlowSize"));
            Assert.That(source, Does.Match(@"pow\(max\(_GlowSize[^)]*\),\s*_SizeCouplingK\)"),
                "M-13: pow(size, k) compensation term must be present");
            Assert.That(source, Does.Contain("sizeCompensation"),
                "compensation must feed the final colour, not just be declared");
        }

        [Test]
        public void BindingKeyIds_ResolverAndDispatcher_ShareOneIdSpace()
        {
            // Every id the resolver can hand out must be either handled or
            // explicitly rejected by the dispatcher — never silently misrouted
            // through a parallel numeric convention.
            var go = new GameObject("pb");
            try
            {
                var pb = go.AddComponent<VfxParameterBlock>();
                var rendererGo = new GameObject("r");
                rendererGo.transform.SetParent(go.transform);
                var renderer = rendererGo.AddComponent<MeshRenderer>();
                var beatGo = new GameObject("beat");
                beatGo.transform.SetParent(go.transform);
                var beat = beatGo.AddComponent<VfxLightBeat>();
                pb.ConfigureCustom(new[] { "p" }, new[] { 0.5f });
                pb.ConfigureBindings(new VfxParameterBlock.BindingEntry[0], new Renderer[] { renderer });
                pb.ConfigureLightTargets(new Component[] { beat });

                string[] keys = VfxTechniqueFamilyBoundary.BindingKeys;
                var handled = new List<string>();
                for (int keyId = 0; keyId < keys.Length; keyId++)
                {
                    // Resolver and key table must agree on the dense index.
                    Assert.That(VfxTechniqueFamilyBoundary.ResolveBindingKey(keys[keyId]), Is.EqualTo(keyId), keys[keyId]);
                    // Family derivation must match the key prefix.
                    VfxBindingFamily family = VfxTechniqueFamilyBoundary.GetBindingFamily(keyId);
                    Assert.That(family, Is.Not.EqualTo(VfxBindingFamily.Unknown), keys[keyId]);
                    Assert.That(keys[keyId], Does.StartWith(family.ToString().ToLowerInvariant() + "."), keys[keyId]);

                    var entry = new VfxParameterBlock.BindingEntry
                    {
                        paramIndex = 0,
                        targetIndex = 0,
                        bindingKeyId = keyId,
                        nameId = Shader.PropertyToID("_Progress")
                    };
                    bool consumed = false;
                    Assert.DoesNotThrow(() => consumed = pb.ApplyBindingEntry(entry, 0.5f), keys[keyId]);
                    if (consumed) handled.Add(keys[keyId]);
                }
                // v1 runtime scope: the mat float path + 4 light.beat handlers.
                Assert.That(handled, Is.EquivalentTo(new[]
                {
                    "mat.prop.float",
                    "light.beat.intensity", "light.beat.range",
                    "light.beat.flickerRate", "light.beat.flickerDepth"
                }), "handled set must be exact: anything else is rejected, not misrouted");

                // Out-of-range / unknown ids are rejected, never dispatched.
                var bogus = new VfxParameterBlock.BindingEntry { paramIndex = 0, targetIndex = 0, bindingKeyId = -1 };
                Assert.That(pb.ApplyBindingEntry(bogus, 1f), Is.False);
                bogus.bindingKeyId = keys.Length + 50;
                Assert.That(pb.ApplyBindingEntry(bogus, 1f), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BindingDispatch_LightKey_WritesTheBeatDriver()
        {
            var go = new GameObject("pb");
            try
            {
                var pb = go.AddComponent<VfxParameterBlock>();
                var beatGo = new GameObject("beat");
                beatGo.transform.SetParent(go.transform);
                var beat = beatGo.AddComponent<VfxLightBeat>();
                pb.ConfigureLightTargets(new Component[] { beat });

                int keyId = VfxTechniqueFamilyBoundary.ResolveBindingKey("light.beat.flickerDepth");
                Assert.That(keyId, Is.GreaterThanOrEqualTo(0));
                var entry = new VfxParameterBlock.BindingEntry { paramIndex = 0, targetIndex = 0, bindingKeyId = keyId };
                Assert.That(pb.ApplyBindingEntry(entry, 0.85f), Is.True);
                Assert.That(beat.FlickerDepth, Is.EqualTo(0.85f).Within(1e-5f));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void Px6b_ConditionalPredicate_FlagsPixelLitWithoutNormalQuantize()
        {
            // PX-6b: only products that actually contain a Lit layer are asked
            // for normal quantization (user decision #16). The all-unlit
            // technique-family library is exempt; simulate a Lit material via
            // the placeholder detection contract (_NormalQuantize property or
            // "Lit" in the shader name) using a URP Lit shader.
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(litShader, Is.Not.Null, "URP Lit must exist in this project");
            var root = new GameObject("pixel_lit_product");
            var material = new Material(litShader);
            try
            {
                // URP Lit does not declare _STYLESTAGE_PIXEL, so EnableKeyword
                // would be dropped; the legacy shaderKeywords string array
                // bypasses local-keyword declaration checks (what a compiled
                // product would carry once SG_VfxLit exists).
                material.shaderKeywords = new[] { VfxStylePreset.KeywordPixel };

                var meshGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                meshGo.transform.SetParent(root.transform);
                meshGo.GetComponent<MeshRenderer>().sharedMaterial = material;

                // Lit + pixel + no _NormalQuantize -> PX-6b violation.
                bool ok = VfxTechniqueFamilyBoundary.ValidatePixelVoxelTrio(root, out List<string> violations);
                Assert.That(ok, Is.False);
                Assert.That(violations, Has.Some.Contains("PX-6b"));

                // Non-pixel Lit imposes no requirement (conditional predicate).
                material.shaderKeywords = new string[0];
                ok = VfxTechniqueFamilyBoundary.ValidatePixelVoxelTrio(root, out violations);
                Assert.That(ok, Is.True, "PX-6b only applies under the pixel style");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Px6b_ConditionalPredicate_SkipsUnlitLibraryMaterials()
        {
            // The shipped technique-family library is all unlit: pixel style on
            // it must never trip PX-6b.
            Shader shader = Shader.Find("VFXComposer/TechniqueFamilies/SdfShape");
            Assert.That(shader, Is.Not.Null);
            var root = new GameObject("pixel_unlit_product");
            var material = new Material(shader);
            try
            {
                material.EnableKeyword(VfxStylePreset.KeywordPixel);
                material.SetFloat("_PixelSize", 0.0625f);
                var meshGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                meshGo.transform.SetParent(root.transform);
                meshGo.GetComponent<MeshRenderer>().sharedMaterial = material;

                bool ok = VfxTechniqueFamilyBoundary.ValidatePixelVoxelTrio(root, out List<string> violations);
                Assert.That(ok, Is.True, string.Join("; ", violations));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }

    /// <summary>Gallery predicates (GA-*) that are checkable in EditMode against generated assets.</summary>
    public sealed class GallerySceneGateTests
    {
        private const string GalleryRoot = "Assets/VFX/Gallery";

        [Test]
        public void GalleryAssembly_IsNotReferencedByRuntime()
        {
            // GA-10: gallery depends on runtime, never the reverse.
            string runtimeAsmdef = "Packages/com.vfxcomposer.unity/Runtime/VFXComposer.Runtime.asmdef";
            string json = File.ReadAllText(Path.GetFullPath(runtimeAsmdef));
            Assert.That(json, Does.Not.Contain("VFXComposer.Gallery"));
        }

        [Test]
        public void GalleryScenes_Exist_AndAreNotInBuildSettings()
        {
            Assert.That(File.Exists(Path.GetFullPath(GalleryRoot + "/VFXGallery_3D.unity")), Is.True,
                "run VFXComposer/Gallery/Rebuild Gallery Scenes");
            Assert.That(File.Exists(Path.GetFullPath(GalleryRoot + "/VFXGallery_2D.unity")), Is.True);
            // GA-8: gallery scenes never enter player builds.
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                Assert.That(scene.path, Does.Not.Contain("VFXGallery"));
            }
        }

        [Test]
        public void BuildSettings_CarryNoDanglingScenePaths()
        {
            // ADR-010 §9 cleanup hygiene: a Build Settings entry whose scene asset was deleted is
            // dead configuration (the retired Preview/NextCandidate scenes once lingered here).
            var dangling = EditorBuildSettings.scenes
                .Where(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
                .Select(scene => scene.path)
                .ToArray();
            Assert.That(dangling, Is.Empty,
                "EditorBuildSettings references deleted scene asset(s):\n" + string.Join("\n", dangling));
        }

        [Test]
        public void GalleryVolumeProfile_HasExactlyBloomAndTonemapping()
        {
            var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(GalleryRoot + "/GalleryVolume.asset");
            Assert.That(profile, Is.Not.Null);
            // GA-3: only Bloom + Tonemapping (no vignette/DoF that would skew judgement).
            Assert.That(profile.components.Count, Is.EqualTo(2));
            Assert.That(profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom), Is.True);
            Assert.That(bloom.active, Is.True, "default ON: users see the full look first");
            Assert.That(profile.TryGet(out UnityEngine.Rendering.Universal.Tonemapping _), Is.True);
        }

        [Test]
        public void GalleryPageSets_Validate()
        {
            foreach (string name in new[] { "GalleryPages_3D.asset", "GalleryPages_2D.asset" })
            {
                var pageSet = AssetDatabase.LoadAssetAtPath<VFXComposer.Gallery.GalleryPageSet>(GalleryRoot + "/" + name);
                Assert.That(pageSet, Is.Not.Null, name);
                Assert.That(pageSet.Validate(out string error), Is.True, name + ": " + error);
                Assert.That(pageSet.Pages.Length, Is.GreaterThanOrEqualTo(2), name + ": structure + verdict pages");
            }
        }

        [Test]
        public void GalleryScenes_SatisfyStructuralPredicates()
        {
            // GA-1/GA-2/GA-4 on the serialized scene, opened additively in EditMode.
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                GalleryRoot + "/VFXGallery_3D.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots.Length, Is.EqualTo(1), "single GalleryRoot");
                GameObject root = roots[0];

                // GA-2: no ENABLED directional light (the inspection light must default off).
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    if (light.type == LightType.Directional)
                        Assert.That(light.enabled, Is.False, "GA-2: inspection light defaults to disabled");

                // GA-4: exactly 9 cells, anchors empty at edit time.
                Transform cells = root.transform.Find("Cells");
                Assert.That(cells, Is.Not.Null);
                Assert.That(cells.childCount, Is.EqualTo(9));
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        Transform cell = cells.Find($"Cell_{r}_{c}");
                        Assert.That(cell, Is.Not.Null, $"Cell_{r}_{c}");
                        Transform anchor = cell.Find("Anchor");
                        Assert.That(anchor, Is.Not.Null);
                        Assert.That(anchor.childCount, Is.EqualTo(0), "anchors are empty at edit time");
                    }
                }

                // Exactly one Volume with the shared gallery profile.
                var volumes = root.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true);
                Assert.That(volumes.Length, Is.EqualTo(1));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
