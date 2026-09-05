using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.TechniqueFamilies;
using VFXComposer.TechniqueFamilies;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Unit 4 constructive predicates (T2b): the 14 CPU particle template
    /// configurations and their module discipline (CP-* family), plus the
    /// ElementPreset / StylePreset format pipeline with the committed sample
    /// assets (fire / ice / lightning + cartoon / pixel).
    /// </summary>
    public sealed class TechniqueFamilyCpuParticleTests
    {
        private static IEnumerable<string> TemplateIds()
        {
            return VfxCpuParticleTemplates.Ids;
        }

        private static ParticleSystem MakeSystem()
        {
            var go = new GameObject("ps");
            return go.AddComponent<ParticleSystem>();
        }

        [Test]
        public void Registry_HasExactlyTheFourteenSpecTemplates()
        {
            Assert.That(VfxCpuParticleTemplates.Ids.Length, Is.EqualTo(14));
            Assert.That(VfxCpuParticleTemplates.Ids, Is.Unique);
            foreach (var id in VfxCpuParticleTemplates.Ids)
                Assert.That(id, Does.Match("^cpu_[a-z][a-z0-9_]*$"), id);
        }

        [Test]
        public void AllTemplates_Configure_EnforcesModuleDiscipline([ValueSource(nameof(TemplateIds))] string id)
        {
            ParticleSystem ps = MakeSystem();
            try
            {
                VfxCpuParticleTemplates.Configure(ps, id, 424242u, 300);
                // Section 4.3 discipline, machine-checked:
                Assert.That(ps.main.playOnAwake, Is.False, id + ": controller owns playback");
                Assert.That(ps.useAutoRandomSeed, Is.False, id + ": deterministic capture");
                Assert.That(ps.randomSeed, Is.EqualTo(424242u), id);
                Assert.That(ps.textureSheetAnimation.enabled, Is.False, id + ": CP-5 no flipbook, ever");
                Assert.That(ps.lights.enabled, Is.False, id + ": lights module banned");
                Assert.That(ps.externalForces.enabled, Is.False, id + ": external forces banned");
                Assert.That(ps.main.maxParticles, Is.EqualTo(300), id);
            }
            finally { Object.DestroyImmediate(ps.gameObject); }
        }

        [Test]
        public void AllTemplates_TheoreticalPeak_IsPositive([ValueSource(nameof(TemplateIds))] string id)
        {
            ParticleSystem ps = MakeSystem();
            try
            {
                VfxCpuParticleTemplates.Configure(ps, id, 7u, 300);
                Assert.That(VfxCpuParticleTemplates.TheoreticalPeak(ps), Is.GreaterThan(0), id);
            }
            finally { Object.DestroyImmediate(ps.gameObject); }
        }

        [Test]
        public void AttractTarget_UsesLocalSpaceNegativeRadial()
        {
            ParticleSystem ps = MakeSystem();
            try
            {
                VfxCpuParticleTemplates.Configure(ps, "cpu_attract_target", 7u, 200);
                Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.Local),
                    "target must be the system origin (controller binds origin to target)");
                Assert.That(ps.velocityOverLifetime.radial.constant, Is.LessThan(0f), "negative radial = attraction");
            }
            finally { Object.DestroyImmediate(ps.gameObject); }
        }

        [Test]
        public void CollisionTemplates_NeverEnableDynamicColliders()
        {
            foreach (var id in new[] { "cpu_gravity_settle", "cpu_gravity_drag_split", "cpu_fall_wind" })
            {
                ParticleSystem ps = MakeSystem();
                try
                {
                    VfxCpuParticleTemplates.Configure(ps, id, 7u, 200);
                    Assert.That(ps.collision.enabled, Is.True, id);
                    Assert.That(ps.collision.enableDynamicColliders, Is.False,
                        id + ": no assumptions about user-scene colliders");
                }
                finally { Object.DestroyImmediate(ps.gameObject); }
            }
        }

        [Test]
        public void InstantRephase_UsesShortLifetimeRebirth()
        {
            ParticleSystem ps = MakeSystem();
            try
            {
                VfxCpuParticleTemplates.Configure(ps, "cpu_instant_rephase", 7u, 200);
                Assert.That(ps.main.startLifetime.constant, Is.LessThanOrEqualTo(0.1f),
                    "rebirth interval must approximate the rephase rate");
                Assert.That(ps.emission.burstCount, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(ps.gameObject); }
        }
    }

    /// <summary>ElementPreset / StylePreset format-pipeline validation (unit 4 scope).</summary>
    public sealed class TechniqueFamilyPresetTests
    {
        private const string PresetRoot = "Assets/VFX/TechniqueFamilies/Presets/";

        private static readonly (string path, string id)[] ElementSamples =
        {
            (PresetRoot + "Element_Fire.asset", "fire"),
            (PresetRoot + "Element_Ice.asset", "ice"),
            (PresetRoot + "Element_Lightning.asset", "lightning")
        };

        [Test]
        public void ElementSamples_LoadAndValidate()
        {
            foreach ((string path, string id) in ElementSamples)
            {
                var preset = AssetDatabase.LoadAssetAtPath<VfxElementPreset>(path);
                Assert.That(preset, Is.Not.Null, path);
                Assert.That(preset.ElementId, Is.EqualTo(id), path);
                Assert.That(preset.Validate(out string error), Is.True, path + ": " + error);
                Assert.That(preset.Strength, Is.InRange(0f, 1f), path);
            }
        }

        [Test]
        public void ElementSamples_HotStopSatisfiesHG2()
        {
            // HG-2: hot stop combined luminance >= 1.5 for light-emitting elements.
            foreach ((string path, string _) in ElementSamples)
            {
                var preset = AssetDatabase.LoadAssetAtPath<VfxElementPreset>(path);
                if (!preset.LightEnabled) continue;
                Color hot = preset.Hot;
                float lum = hot.r * 0.2126f + hot.g * 0.7152f + hot.b * 0.0722f;
                Assert.That(lum * preset.StopMultipliers.w, Is.GreaterThanOrEqualTo(1.5f), path);
            }
        }

        [Test]
        public void ElementSamples_ReferenceOnlyRegisteredVariantsAndGenerators()
        {
            var meshIds = new HashSet<string>(VfxMeshGenerators.Ids);
            var cpuIds = new HashSet<string>(VfxCpuParticleTemplates.Ids);
            foreach ((string path, string _) in ElementSamples)
            {
                var preset = AssetDatabase.LoadAssetAtPath<VfxElementPreset>(path);
                Assert.That(meshIds, Does.Contain(preset.PrimaryMeshGenerator), path + ": mesh generator must be registered");
                Assert.That(cpuIds, Does.Contain(preset.CpuVariant), path + ": cpu variant must be registered");
                Assert.That(preset.GpuTemplate, Does.Match("^vfx_[a-z][a-z0-9_]*$"), path);
                Assert.That(preset.PrimaryMaterialVariant, Does.Match("^mat_[a-z][a-z0-9_]*$"), path);
            }
        }

        [Test]
        public void StyleSamples_LoadAndValidate()
        {
            var cartoon = AssetDatabase.LoadAssetAtPath<VfxStylePreset>(PresetRoot + "Style_Cartoon.asset");
            var pixel = AssetDatabase.LoadAssetAtPath<VfxStylePreset>(PresetRoot + "Style_Pixel.asset");
            Assert.That(cartoon, Is.Not.Null);
            Assert.That(pixel, Is.Not.Null);
            Assert.That(cartoon.StyleId, Is.EqualTo(VfxStyleId.Cartoon));
            Assert.That(pixel.StyleId, Is.EqualTo(VfxStyleId.Pixel));
            Assert.That(cartoon.Validate(out string cErr), Is.True, cErr);
            Assert.That(pixel.Validate(out string pErr), Is.True, pErr);
            // LT-7: pixel must not allow shadows.
            Assert.That(pixel.AllowShadows, Is.False);
            // Pixel time quantization present (STYLE_IMPL section 3.5).
            Assert.That(pixel.LightBeatFrameRate, Is.GreaterThan(0f));
        }

        [Test]
        public void StylePreset_AppliesExactlyOneStyleStageKeyword()
        {
            Shader shader = Shader.Find("VFXComposer/TechniqueFamilies/NoiseFbm2Layer");
            Assert.That(shader, Is.Not.Null, "technique-family shader library must be importable");
            var material = new Material(shader);
            try
            {
                var cartoon = AssetDatabase.LoadAssetAtPath<VfxStylePreset>(PresetRoot + "Style_Cartoon.asset");
                var pixel = AssetDatabase.LoadAssetAtPath<VfxStylePreset>(PresetRoot + "Style_Pixel.asset");

                cartoon.ApplyToMaterial(material);
                Assert.That(material.IsKeywordEnabled(VfxStylePreset.KeywordCartoon), Is.True);
                Assert.That(material.IsKeywordEnabled(VfxStylePreset.KeywordPixel), Is.False);

                pixel.ApplyToMaterial(material);
                Assert.That(material.IsKeywordEnabled(VfxStylePreset.KeywordCartoon), Is.False);
                Assert.That(material.IsKeywordEnabled(VfxStylePreset.KeywordPixel), Is.True);
                Assert.That(material.GetFloat("_PixelSize"), Is.EqualTo(0.0625f).Within(1e-5f));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void ElementPreset_AppliesPaletteToMaterial()
        {
            Shader shader = Shader.Find("VFXComposer/TechniqueFamilies/NoiseFbm2Layer");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                var fire = AssetDatabase.LoadAssetAtPath<VfxElementPreset>(PresetRoot + "Element_Fire.asset");
                fire.ApplyToMaterial(material);
                Assert.That(material.GetColor("_ColorHot"), Is.EqualTo(fire.Hot));
                Assert.That(material.GetColor("_ColorPrimary"), Is.EqualTo(fire.Primary));
            }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
