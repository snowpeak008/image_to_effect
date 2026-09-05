using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// ADR-009 (TEMPLATE_VISUAL_QUALITY_V1) machine enforcement: constructive traversal of every
    /// template manifest under Assets/VFX/Templates/**/Manifests, asserting the per-kind minimum
    /// visual-behavior predicates against the prefab's serialized component state. Templates that
    /// currently fail are carried by the explicit exemption table below (ADR-009 §4); the table is
    /// consumed exactly (no silent growth, no stale entries) and every exempt template is re-run so
    /// a template that becomes compliant must be removed from the table.
    /// </summary>
    public sealed class TemplateVisualQualityTests
    {
        private const string TemplatesAssetRoot = "Assets/VFX/Templates";

        // ADR-009 §3-4: deliberately separate v2 closed domain (own .slash.manifest.json catalog).
        // Closed set, same discipline as the R-5 "separately manifested" containers.
        private static readonly string[] SeparatelyManifestedSubtrees =
        {
            "Assets/VFX/Templates/3D/Slash",
            "Assets/VFX/Templates/3D/SlashManifests"
        };

        // ADR-009 §5: only these kinds are legislated; anything else (including sprite_emitter and
        // sub_effect, which the parser accepts) is rejected until the ADR is amended with predicates.
        private static readonly ModuleKind[] LegislatedKinds =
        {
            ModuleKind.EnergyBody, ModuleKind.ImpactBurst, ModuleKind.ImpactFlash,
            ModuleKind.Shockwave, ModuleKind.MotionTrail, ModuleKind.SecondaryParticles
        };

        private sealed class Exemption
        {
            public readonly string Reason;
            public readonly string DueCard;
            public Exemption(string reason, string dueCard) { Reason = reason; DueCard = dueCard; }
        }

        // ADR-009 §4 exemption table. Every entry must name the follow-up card that retires it.
        // T1b remade the 2D fire set (entries retired); the 3D fire set is scheduled by the T1b delivery report.
        private static readonly Dictionary<string, Exemption> ExemptTemplates = new Dictionary<string, Exemption>(StringComparer.Ordinal)
        {
            { "PFT_3D_FireCore", new Exemption("EB-1/EB-2/EB-4: static MeshRenderer body, no ParticleSystem, peak=0", "T1b-3D") },
            { "PFT_3D_Embers", new Exemption("SP-1: gravityModifier is zero and LimitVelocityOverLifetime is disabled", "T1b-3D") },
            { "PFT_3D_FireImpact", new Exemption("IM-2: single renderer, no secondary/flash layer", "T1b-3D") },
            { "PFT_3D_LaunchFlash", new Exemption("IM-2: single renderer, no secondary/flash layer", "T1b-3D") },
            { "PFT_3D_Shockwave", new Exemption("SW-2: ColorOverLifetime disabled, no alpha decay", "T1b-3D") }
        };

        [Test]
        public void TemplateLibrary_EveryPrefabIsManifested_AndDimensionsFailClosed()
        {
            var templatesFullPath = FullPathOf(TemplatesAssetRoot);
            Assert.That(Directory.Exists(templatesFullPath), Is.True, TemplatesAssetRoot + " must exist.");

            var dimensionDirectories = Directory.GetDirectories(templatesFullPath);
            Assert.That(dimensionDirectories.Length, Is.GreaterThan(0), "Template library must contain at least one dimension directory.");

            foreach (var dimensionFullPath in dimensionDirectories)
            {
                var dimensionAssetPath = AssetPathOf(dimensionFullPath);
                var prefabPaths = CollectGovernedPrefabAssetPaths(dimensionFullPath);
                if (prefabPaths.Count == 0)
                    continue;

                // Fail-closed: a dimension that ships prefabs must ship a non-empty Manifests directory.
                var manifestsFullPath = Path.Combine(dimensionFullPath, "Manifests");
                Assert.That(Directory.Exists(manifestsFullPath), Is.True,
                    dimensionAssetPath + " contains template prefabs but has no Manifests directory (ADR-009 §5).");
                var catalog = TemplateCatalog.LoadFromDirectory(manifestsFullPath, new UnityAssetReferenceResolver());
                Assert.That(catalog.Report.HasErrors, Is.False, dimensionAssetPath + " manifests must parse: " + Report(catalog));
                Assert.That(catalog.ByTemplateId.Count, Is.GreaterThan(0),
                    dimensionAssetPath + " contains template prefabs but declares zero manifests (ADR-009 §5).");

                var manifestedPaths = new HashSet<string>(catalog.ByTemplateId.Values.Select(manifest => manifest.AssetPath), StringComparer.Ordinal);
                foreach (var prefabPath in prefabPaths)
                    Assert.That(manifestedPaths.Contains(prefabPath), Is.True,
                        prefabPath + " is not referenced by any template manifest; unmanifested prefabs must not exist in the template library (ADR-009 §3-3).");
            }
        }

        [Test]
        public void TemplateVisualQuality_PerKindPredicatesHold_WithExplicitlyConsumedExemptions()
        {
            var consumedExemptions = new HashSet<string>(StringComparer.Ordinal);
            var violations = new List<string>();
            var seenTemplateIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var catalog in LoadAllCatalogs())
            {
                foreach (var manifest in catalog.ByTemplateId.Values.OrderBy(value => value.TemplateId, StringComparer.Ordinal))
                {
                    seenTemplateIds.Add(manifest.TemplateId);

                    // Fail-closed: unknown/unlegislated kind is rejected outright, never exempted.
                    Assert.That(LegislatedKinds, Does.Contain(manifest.Kind),
                        manifest.TemplateId + " uses kind '" + manifest.Kind + "' which has no ADR-009 predicates; amend the ADR before shipping this kind (§5).");

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath);
                    Assert.That(prefab, Is.Not.Null, manifest.TemplateId + ": prefab failed to load at " + manifest.AssetPath);

                    var failures = EvaluatePredicates(manifest, prefab);
                    Exemption exemption;
                    var isExempt = ExemptTemplates.TryGetValue(manifest.TemplateId, out exemption);
                    if (failures.Count == 0)
                    {
                        if (isExempt)
                            violations.Add(manifest.TemplateId + " now satisfies all ADR-009 predicates; remove it from the exemption table (§4-2).");
                        continue;
                    }

                    if (isExempt)
                    {
                        consumedExemptions.Add(manifest.TemplateId);
                        continue;
                    }

                    violations.Add(manifest.TemplateId + " (kind=" + manifest.Kind + ") fails ADR-009 predicates without exemption: " + string.Join(", ", failures));
                }
            }

            // No ghost exemptions: every declared entry must name a real template.
            foreach (var exemptId in ExemptTemplates.Keys)
                Assert.That(seenTemplateIds.Contains(exemptId), Is.True,
                    "Exemption table names '" + exemptId + "' which is not a template in the library (ADR-009 §4-4).");

            Assert.That(violations, Is.Empty, string.Join("\n", violations));

            // The consumed exemption set must equal the declared list exactly (ADR-009 §4-3).
            Assert.That(consumedExemptions.OrderBy(id => id, StringComparer.Ordinal),
                Is.EqualTo(ExemptTemplates.Keys.OrderBy(id => id, StringComparer.Ordinal)),
                "Consumed exemptions must exactly match the declared exemption table; no silent growth or shrinkage (ADR-009 §4-3).");
        }

        private static List<string> EvaluatePredicates(TemplateManifest manifest, GameObject prefab)
        {
            var failures = new List<string>();
            var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var trails = prefab.GetComponentsInChildren<TrailRenderer>(true);

            switch (manifest.Kind)
            {
                case ModuleKind.EnergyBody:
                    if (particles.Length < 1)
                        failures.Add("EB-1 requires >=1 ParticleSystem");
                    if (!particles.Any(system => system.emission.enabled
                        && (system.colorOverLifetime.enabled || system.textureSheetAnimation.enabled)
                        && system.sizeOverLifetime.enabled))
                        failures.Add("EB-2 requires a ParticleSystem with emission enabled, (colorOverLifetime or textureSheetAnimation) enabled and sizeOverLifetime enabled");
                    if (renderers.Length == 0 || renderers.All(renderer => renderer is SpriteRenderer))
                        failures.Add("EB-3 forbids a bare SpriteRenderer as the only render body");
                    if (manifest.Cost.EstimatedPeakParticles < 8)
                        failures.Add("EB-4 requires manifest estimatedPeakParticles >= 8 (actual " + manifest.Cost.EstimatedPeakParticles + ")");
                    break;

                case ModuleKind.ImpactBurst:
                case ModuleKind.ImpactFlash:
                    if (!particles.Any(system => system.emission.enabled && system.emission.burstCount >= 1))
                        failures.Add("IM-1 requires a ParticleSystem with emission enabled and a non-empty burst list");
                    if (renderers.Length < 2)
                        failures.Add("IM-2 requires >=2 renderer layers (actual " + renderers.Length + ")");
                    break;

                case ModuleKind.Shockwave:
                    if (!particles.Any(system => system.sizeOverLifetime.enabled && CurveExpands(system.sizeOverLifetime.size)))
                        failures.Add("SW-1 requires sizeOverLifetime enabled with an expanding size curve");
                    if (!particles.Any(system => system.colorOverLifetime.enabled && GradientAlphaDecays(system.colorOverLifetime.color)))
                        failures.Add("SW-2 requires colorOverLifetime enabled with decaying alpha");
                    break;

                case ModuleKind.MotionTrail:
                    var hasShapedTrail = trails.Any(trail => WidthCurveIsNonConstant(trail.widthCurve))
                        || particles.Any(system => system.trails.enabled);
                    if (!hasShapedTrail)
                        failures.Add("MT-1 requires a TrailRenderer with a non-constant width curve or a ParticleSystem trails module");
                    var gradients = trails.Select(trail => trail.colorGradient)
                        .Concat(particles.Where(system => system.trails.enabled).Select(system => system.trails.colorOverTrail.gradient))
                        .Where(gradient => gradient != null);
                    if (!gradients.Any(GradientIsNonMonochrome))
                        failures.Add("MT-2 requires a non-monochrome trail color gradient");
                    break;

                case ModuleKind.SecondaryParticles:
                    if (!particles.Any(system => GravityIsNonZero(system.main) || system.limitVelocityOverLifetime.enabled))
                        failures.Add("SP-1 requires non-zero gravity or an enabled LimitVelocityOverLifetime module");
                    break;

                default:
                    // Unreachable: the legislated-kind gate asserts before predicate evaluation.
                    failures.Add("kind '" + manifest.Kind + "' has no predicates");
                    break;
            }

            return failures;
        }

        private static bool CurveExpands(ParticleSystem.MinMaxCurve size)
        {
            var curve = size.mode == ParticleSystemCurveMode.Constant || size.mode == ParticleSystemCurveMode.TwoConstants
                ? null
                : size.curve;
            if (curve == null || curve.length < 2)
                return false;
            return curve.keys[curve.length - 1].value > curve.keys[0].value;
        }

        private static bool GradientAlphaDecays(ParticleSystem.MinMaxGradient color)
        {
            var gradient = color.mode == ParticleSystemGradientMode.Color || color.mode == ParticleSystemGradientMode.TwoColors
                ? null
                : color.gradient;
            if (gradient == null || gradient.alphaKeys == null || gradient.alphaKeys.Length < 2)
                return false;
            return gradient.alphaKeys[gradient.alphaKeys.Length - 1].alpha < gradient.alphaKeys[0].alpha;
        }

        private static bool WidthCurveIsNonConstant(AnimationCurve widthCurve)
        {
            if (widthCurve == null || widthCurve.length < 2)
                return false;
            var first = widthCurve.keys[0].value;
            return widthCurve.keys.Any(key => Math.Abs(key.value - first) > .0001f);
        }

        private static bool GradientIsNonMonochrome(Gradient gradient)
        {
            var colorKeys = gradient.colorKeys;
            var alphaKeys = gradient.alphaKeys;
            var colorVaries = colorKeys.Length >= 2 && colorKeys.Any(key => key.color != colorKeys[0].color);
            var alphaVaries = alphaKeys.Length >= 2 && alphaKeys.Any(key => Math.Abs(key.alpha - alphaKeys[0].alpha) > .0001f);
            return colorVaries || alphaVaries;
        }

        private static bool GravityIsNonZero(ParticleSystem.MainModule main)
        {
            var gravity = main.gravityModifier;
            switch (gravity.mode)
            {
                case ParticleSystemCurveMode.Constant: return Math.Abs(gravity.constant) > .0001f;
                case ParticleSystemCurveMode.TwoConstants: return Math.Abs(gravity.constantMin) > .0001f || Math.Abs(gravity.constantMax) > .0001f;
                default: return Math.Abs(gravity.curveMultiplier) > .0001f && gravity.curve != null && gravity.curve.keys.Any(key => Math.Abs(key.value) > .0001f);
            }
        }

        private static IEnumerable<TemplateCatalog> LoadAllCatalogs()
        {
            var templatesFullPath = FullPathOf(TemplatesAssetRoot);
            foreach (var dimensionFullPath in Directory.GetDirectories(templatesFullPath).OrderBy(path => path, StringComparer.Ordinal))
            {
                var manifestsFullPath = Path.Combine(dimensionFullPath, "Manifests");
                if (!Directory.Exists(manifestsFullPath))
                    continue;
                var catalog = TemplateCatalog.LoadFromDirectory(manifestsFullPath, new UnityAssetReferenceResolver());
                Assert.That(catalog.Report.HasErrors, Is.False, AssetPathOf(dimensionFullPath) + " manifests must parse: " + Report(catalog));
                yield return catalog;
            }
        }

        private static List<string> CollectGovernedPrefabAssetPaths(string dimensionFullPath)
        {
            return Directory.GetFiles(dimensionFullPath, "*.prefab", SearchOption.AllDirectories)
                .Select(AssetPathOf)
                .Where(assetPath => !SeparatelyManifestedSubtrees.Any(subtree => assetPath.StartsWith(subtree + "/", StringComparison.Ordinal)))
                .OrderBy(assetPath => assetPath, StringComparer.Ordinal)
                .ToList();
        }

        private static string FullPathOf(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string AssetPathOf(string fullPath)
        {
            var normalized = Path.GetFullPath(fullPath).Replace('\\', '/');
            var dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            Assert.That(normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase), Is.True, fullPath + " is outside Assets/");
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        private static string Report(TemplateCatalog catalog)
        {
            return string.Join(" | ", catalog.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message));
        }
    }
}
