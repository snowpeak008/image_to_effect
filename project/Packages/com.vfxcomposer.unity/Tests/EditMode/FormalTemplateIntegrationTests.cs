using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Tests.EditMode
{
    public sealed class FormalTemplateIntegrationTests
    {
        private const string TemplateRoot = "Assets/VFX/Templates/";
        private const string ManifestDirectory = "Assets/VFX/Templates/2D/Manifests";
        private static readonly string[] TemplateIds =
        {
            "PFT_2D_FireCore", "PFT_2D_Embers", "PFT_2D_FireImpact", "PFT_2D_FireTrail", "PFT_2D_LaunchFlash", "PFT_2D_Shockwave"
        };

        [Test]
        public void FormalManifests_ResolveToTheSixRealTemplatePrefabs_AndKeepDependenciesInsideTemplates()
        {
            var directory = Path.Combine(Application.dataPath, "VFX", "Templates", "2D", "Manifests");
            var catalog = TemplateCatalog.LoadFromDirectory(directory, new UnityAssetReferenceResolver());
            Assert.That(catalog.Report.HasErrors, Is.False, Report(catalog));
            Assert.That(catalog.ByTemplateId.Keys.OrderBy(id => id), Is.EquivalentTo(TemplateIds));
            foreach (var id in TemplateIds)
            {
                TemplateManifest manifest;
                Assert.That(catalog.TryGet(id, out manifest), Is.True, id);
                Assert.That(AssetDatabase.GUIDToAssetPath(manifest.AssetGuid), Is.EqualTo(manifest.AssetPath));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath);
                Assert.That(prefab, Is.Not.Null, manifest.AssetPath);
                foreach (var dependency in AssetDatabase.GetDependencies(manifest.AssetPath, true).Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)))
                    Assert.That(dependency.StartsWith(TemplateRoot, StringComparison.Ordinal), Is.True, id + " has a non-template dependency: " + dependency);
            }
        }

        [Test]
        public void FormalSourceTextures_AreSpriteAlphaClampBilinearWithoutMipmaps()
        {
            foreach (var name in new[] { "VFX2D_FireCore.png", "VFX2D_Ember.png", "VFX2D_ImpactStreak.png", "VFX2D_LaunchFlash.png", "VFX2D_ShockwaveRing.png", "VFX2D_FireTrail.png" })
            {
                var importer = AssetImporter.GetAtPath("Assets/VFX/Templates/2D/Textures/" + name) as TextureImporter;
                Assert.That(importer, Is.Not.Null, name);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), name);
                Assert.That(importer.alphaIsTransparency, Is.True, name);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), name);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), name);
                Assert.That(importer.mipmapEnabled, Is.False, name);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(1024), name);
            }
        }

        [Test]
        public void FormalManifests_ExposeFiniteTypedDefaultsAndExtremes()
        {
            var catalog = TemplateCatalog.LoadFromDirectory(Path.Combine(Application.dataPath, "VFX", "Templates", "2D", "Manifests"), new UnityAssetReferenceResolver());
            Assert.That(catalog.Report.HasErrors, Is.False, Report(catalog));
            // S4 ManifestValidator is the formal type/range/default authority; no report means each numeric min/default/max is finite,
            // ordered and type-compatible. Keep the concrete S5 surface explicit so accidental extra/missing parameters are caught.
            Assert.That(catalog.ByTemplateId["PFT_2D_FireCore"].Parameters.Keys, Is.EquivalentTo(new[] { "scale" }));
            Assert.That(catalog.ByTemplateId["PFT_2D_Embers"].Parameters.Keys, Is.EquivalentTo(new[] { "rate", "lifetime" }));
            Assert.That(catalog.ByTemplateId["PFT_2D_FireImpact"].Parameters.Keys, Is.EquivalentTo(new[] { "count", "speed" }));
            Assert.That(catalog.ByTemplateId["PFT_2D_FireTrail"].Parameters.Keys, Is.EquivalentTo(new[] { "time", "width" }));
            Assert.That(catalog.ByTemplateId["PFT_2D_LaunchFlash"].Parameters.Keys, Is.EquivalentTo(new[] { "lifetime", "size" }));
            Assert.That(catalog.ByTemplateId["PFT_2D_Shockwave"].Parameters.Keys, Is.EquivalentTo(new[] { "lifetime", "endSize" }));
        }

        [Test]
        public void FormalTemplates_CanInstantiate_AndTheirParticleAndTrailContractsPlay()
        {
            foreach (var id in TemplateIds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Templates/2D/Prefabs/" + id + ".prefab");
                var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                Assert.That(instance, Is.Not.Null, id);
                try
                {
                    var particle = instance.GetComponent<ParticleSystem>();
                    if (particle != null)
                    {
                        particle.Simulate(.1f, true, true, true);
                        Assert.That(particle.main.maxParticles, Is.GreaterThan(0), id);
                        Assert.That(particle.particleCount, Is.GreaterThan(0), id + " must emit during deterministic simulation.");
                        Assert.That(instance.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null, id);
                    }
                    var trail = instance.GetComponent<TrailRenderer>();
                    if (trail != null)
                    {
                        Assert.That(trail.time, Is.GreaterThan(0f)); Assert.That(trail.widthMultiplier, Is.GreaterThan(0f)); Assert.That(trail.sharedMaterial, Is.Not.Null);
                        instance.transform.position = Vector3.left; trail.AddPosition(Vector3.left); instance.transform.position = Vector3.right; trail.AddPosition(Vector3.right);
                        Assert.That(trail.positionCount, Is.GreaterThanOrEqualTo(2));
                    }
                    Assert.That(instance.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component.GetType().Namespace != null && component.GetType().Namespace.Contains("Editor")), Is.False, id + " must not depend on Editor components.");
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }

        [Test]
        public void FormalManifestThreePoints_ApplyToEveryTemplateThroughExplicitS5TestSwitch()
        {
            foreach (var id in TemplateIds)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Templates/2D/Prefabs/" + id + ".prefab");
                foreach (var parameter in ParametersFor(id))
                foreach (var value in ManifestThreePoints(id, parameter))
                {
                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    try
                    {
                        ApplyS5TestParameter(instance, id, parameter, value);

                        var particle = instance.GetComponent<ParticleSystem>();
                        if (particle != null)
                        {
                            // Keep the observation inside the shortest manifest lifetime so one-shot launch particles
                            // have not already expired before their real particleCount is observed.
                            var playWindow = id == "PFT_2D_Embers" ? .4f : id == "PFT_2D_LaunchFlash" ? .03f : .1f;
                            particle.Simulate(playWindow, true, true, true);
                            Assert.That(particle.particleCount, Is.GreaterThan(0), id + "/" + parameter + "/" + value + " must still play.");
                        }
                        var trail = instance.GetComponent<TrailRenderer>();
                        if (trail != null)
                        {
                            trail.Clear(); trail.AddPosition(Vector3.left); trail.AddPosition(Vector3.zero); trail.AddPosition(Vector3.right);
                            Assert.That(trail.positionCount, Is.GreaterThanOrEqualTo(2), id + "/" + parameter + "/" + value + " retains visible trail points.");
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        private static string[] ParametersFor(string id)
        {
            switch (id)
            {
                case "PFT_2D_FireCore": return new[] { "scale" };
                case "PFT_2D_Embers": return new[] { "rate", "lifetime" };
                case "PFT_2D_FireImpact": return new[] { "count", "speed" };
                case "PFT_2D_FireTrail": return new[] { "time", "width" };
                case "PFT_2D_LaunchFlash": return new[] { "lifetime", "size" };
                case "PFT_2D_Shockwave": return new[] { "lifetime", "endSize" };
                default: throw new ArgumentOutOfRangeException("id", id, "Unexpected formal S5 template.");
            }
        }

        private static float[] ManifestThreePoints(string id, string parameter)
        {
            var json = File.ReadAllText("Assets/VFX/Templates/2D/Manifests/" + id + ".manifest.json");
            var pattern = "\\\"" + Regex.Escape(parameter) + "\\\"\\s*:\\s*\\{.*?\\\"min\\\"\\s*:\\s*([-+.0-9]+).*?\\\"max\\\"\\s*:\\s*([-+.0-9]+).*?\\\"default\\\"\\s*:\\s*([-+.0-9]+)";
            var match = Regex.Match(json, pattern, RegexOptions.Singleline);
            Assert.That(match.Success, Is.True, id + "/" + parameter + " must expose min/max/default numeric contract.");
            return new[] { Parse(match.Groups[1].Value), Parse(match.Groups[3].Value), Parse(match.Groups[2].Value) };
        }

        private static float Parse(string value) { return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }

        // Deliberately test-local and explicit: this is not a S6 binding handler and performs no reflection.
        private static void ApplyS5TestParameter(GameObject instance, string id, string parameter, float value)
        {
            var particle = instance.GetComponent<ParticleSystem>();
            switch (id)
            {
                case "PFT_2D_FireCore": instance.transform.localScale = Vector3.one * value; Assert.That(instance.transform.localScale.x, Is.EqualTo(value).Within(.0001f)); return;
                case "PFT_2D_Embers": if (parameter == "rate") { var emission = particle.emission; emission.rateOverTime = value; Assert.That(emission.rateOverTime.constant, Is.EqualTo(value).Within(.0001f)); } else { var main = particle.main; main.startLifetime = value; Assert.That(main.startLifetime.constant, Is.EqualTo(value).Within(.0001f)); } return;
                case "PFT_2D_FireImpact": if (parameter == "count") { var emission = particle.emission; var count = new ParticleSystem.MinMaxCurve { mode = ParticleSystemCurveMode.TwoConstants, constantMin = value, constantMax = value }; var burst = new ParticleSystem.Burst(0f, count); emission.SetBursts(new[] { burst }); var bursts = new ParticleSystem.Burst[emission.burstCount]; Assert.That(emission.GetBursts(bursts), Is.EqualTo(1)); Assert.That(bursts[0].minCount, Is.EqualTo((short)value), "minCount"); Assert.That(bursts[0].maxCount, Is.EqualTo((short)value), "maxCount"); } else { var main = particle.main; main.startSpeed = value; Assert.That(main.startSpeed.constant, Is.EqualTo(value).Within(.0001f)); } return;
                case "PFT_2D_FireTrail": var trail = instance.GetComponent<TrailRenderer>(); if (parameter == "time") { trail.time = value; Assert.That(trail.time, Is.EqualTo(value).Within(.0001f)); } else { trail.widthMultiplier = value; Assert.That(trail.widthMultiplier, Is.EqualTo(value).Within(.0001f)); } return;
                case "PFT_2D_LaunchFlash": if (parameter == "lifetime") { var main = particle.main; main.startLifetime = value; Assert.That(main.startLifetime.constant, Is.EqualTo(value).Within(.0001f)); } else { var main = particle.main; main.startSize = value; Assert.That(main.startSize.constant, Is.EqualTo(value).Within(.0001f)); } return;
                case "PFT_2D_Shockwave": if (parameter == "lifetime") { var main = particle.main; main.startLifetime = value; Assert.That(main.startLifetime.constant, Is.EqualTo(value).Within(.0001f)); } else { var size = particle.sizeOverLifetime; Assert.That(size.enabled, Is.True); var curve = size.size.curve; var last = curve.length - 1; curve.MoveKey(last, new Keyframe(curve.keys[last].time, value)); size.size = new ParticleSystem.MinMaxCurve(1f, curve); var readback = size.size.curve; Assert.That(readback.keys[readback.length - 1].value, Is.EqualTo(value).Within(.0001f)); } return;
                default: throw new ArgumentOutOfRangeException("id", id, "Unexpected formal S5 template.");
            }
        }

        private static string Report(TemplateCatalog catalog) { return string.Join(" | ", catalog.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
    }
}
