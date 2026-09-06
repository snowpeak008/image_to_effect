using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VFXComposer.TechniqueFamilies;

namespace VFXComposer.Editor.TechniqueFamilies
{
    /// <summary>
    /// Compiler boundary v2 (COMPILER_BOUNDARY_V2.md sections 2-4, 6): the
    /// recognized-component closed set, the excluded set, the allowed
    /// dependency roots, the six-tier cost model and the three-segment binding
    /// key allow-list (~112 keys, growth bounded by Unity API surface — never
    /// by content). This extends the paradigm gate for technique families; the
    /// legacy kind closed set and its exemption tables are untouched (T2c).
    /// </summary>
    public static class VfxTechniqueFamilyBoundary
    {
        // ------------------------------------------------------------ section 2.2: recognized components

        /// <summary>The 18 recognized Unity component categories (type names).</summary>
        public static readonly string[] RecognizedUnityComponents =
        {
            "UnityEngine.MeshFilter", "UnityEngine.MeshRenderer", "UnityEngine.SpriteRenderer",
            "UnityEngine.ParticleSystem", "UnityEngine.ParticleSystemRenderer",
            "UnityEngine.VFX.VisualEffect",
            "UnityEngine.Light",
            "UnityEngine.Rendering.Universal.Light2D",
            "UnityEngine.Rigidbody", "UnityEngine.Rigidbody2D",
            "UnityEngine.BoxCollider", "UnityEngine.MeshCollider", "UnityEngine.SphereCollider",
            "UnityEngine.CapsuleCollider", "UnityEngine.PolygonCollider2D", "UnityEngine.BoxCollider2D",
            "UnityEngine.Cloth",
            "UnityEngine.TrailRenderer", "UnityEngine.LineRenderer",
            "UnityEngine.Rendering.SortingGroup",
            "UnityEngine.CanvasRenderer",
            "UnityEngine.Rendering.Universal.DecalProjector"
        };

        /// <summary>The 6 product-owned runtime scripts.</summary>
        public static readonly Type[] RecognizedRuntimeScripts =
        {
            typeof(VfxController),
            typeof(VfxParameterBlock),
            typeof(VfxLightBeat),
            typeof(VfxUrpCapabilityProbe),
            typeof(VfxElementPreset),  // asset-side, listed for closure completeness
            typeof(VfxStylePreset)
        };

        /// <summary>Section 2.3: components whose presence in a product is an immediate FAIL.</summary>
        public static readonly string[] ExcludedComponents =
        {
            "UnityEngine.Camera",
            "UnityEngine.Rendering.Volume",
            "UnityEngine.Canvas", "UnityEngine.UI.CanvasScaler", "UnityEngine.UI.GraphicRaycaster",
            "UnityEngine.EventSystems.EventSystem",
            "UnityEngine.AudioSource",
            "UnityEngine.Animator", "UnityEngine.Animation",
            "UnityEngine.Playables.PlayableDirector",
            "UnityEngine.WindZone",
            "UnityEngine.ParticleSystemForceField",
            "UnityEngine.ReflectionProbe",
            "UnityEngine.LightProbeGroup"
        };

        // ------------------------------------------------------------ section 3: dependency roots

        /// <summary>
        /// v2 allowed dependency roots. Assets/VFX/Templates and Assets/VFX/Effects
        /// are retired (legacy cleanup belongs to T2c and is not touched here);
        /// Assets/VFX/Gallery is deliberately NOT in this list (GA-9): gallery is
        /// scene tooling, never a product dependency.
        /// </summary>
        public static readonly string[] AllowedDependencyRoots =
        {
            "Assets/VFX/Shared/",
            "Assets/VFX/Generated/",
            "Assets/VFX/TechniqueFamilies/",
            "Packages/com.vfxcomposer.unity/Runtime/",
            "Packages/com.unity.render-pipelines.core/",
            "Packages/com.unity.render-pipelines.universal/",
            "Packages/com.unity.ugui/"
        };

        public static bool IsDependencyAllowed(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string normalized = assetPath.Replace('\\', '/');
            // Unity built-in resources resolve outside the Assets/Packages space.
            if (normalized.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("Library/", StringComparison.OrdinalIgnoreCase)) return true;
            for (int i = 0; i < AllowedDependencyRoots.Length; i++)
                if (normalized.StartsWith(AllowedDependencyRoots[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // ------------------------------------------------------------ section 4: six-tier cost model

        public enum Tier { ML = 0, MM = 1, MH = 2, PL = 3, PM = 4, PH = 5 }

        /// <summary>Cost snapshot for one product (per-prefab sums; per-layer items carry the max).</summary>
        public struct CostReport
        {
            public int GpuParticles;
            public int CpuParticlePeak;
            public int MaxMaterialSampleCost; // per-layer max
            public int LocalLights;
            public int ShadowedLights;
            public int Fragments;
            public int ClothTier;
            public int OverdrawEstimate;
            public int MaxLayerVertexCount;   // per-layer max
        }

        public struct CostLimits
        {
            public int GpuParticles, CpuParticlePeak, MaterialSampleCost, LocalLights,
                       ShadowedLights, Fragments, ClothTier, Overdraw, LayerVertexCount;
        }

        /// <summary>Section 4.2 limit table, indexed by tier.</summary>
        public static CostLimits GetLimits(Tier tier)
        {
            switch (tier)
            {
                case Tier.ML: return new CostLimits { GpuParticles = 0, CpuParticlePeak = 60, MaterialSampleCost = 1, LocalLights = 0, ShadowedLights = 0, Fragments = 8, ClothTier = 0, Overdraw = 2, LayerVertexCount = 256 };
                case Tier.MM: return new CostLimits { GpuParticles = 0, CpuParticlePeak = 150, MaterialSampleCost = 2, LocalLights = 1, ShadowedLights = 0, Fragments = 16, ClothTier = 0, Overdraw = 3, LayerVertexCount = 512 };
                case Tier.MH: return new CostLimits { GpuParticles = 2000, CpuParticlePeak = 300, MaterialSampleCost = 3, LocalLights = 2, ShadowedLights = 0, Fragments = 32, ClothTier = 1, Overdraw = 4, LayerVertexCount = 2048 };
                case Tier.PL: return new CostLimits { GpuParticles = 5000, CpuParticlePeak = 400, MaterialSampleCost = 2, LocalLights = 2, ShadowedLights = 0, Fragments = 48, ClothTier = 1, Overdraw = 4, LayerVertexCount = 2048 };
                case Tier.PM: return new CostLimits { GpuParticles = 20000, CpuParticlePeak = 600, MaterialSampleCost = 3, LocalLights = 3, ShadowedLights = 1, Fragments = 96, ClothTier = 2, Overdraw = 6, LayerVertexCount = 8192 };
                default: return new CostLimits { GpuParticles = 100000, CpuParticlePeak = 1000, MaterialSampleCost = 4, LocalLights = 4, ShadowedLights = 2, Fragments = 200, ClothTier = 3, Overdraw = 8, LayerVertexCount = 32768 };
            }
        }

        /// <summary>
        /// Evaluates a report against tier limits. Overdraw uses the dual
        /// threshold (warn at limit, fail at 1.5x) because it is an estimate.
        /// Returns the violated item names (hard failures only).
        /// </summary>
        public static List<string> Evaluate(in CostReport report, Tier tier, out List<string> warnings)
        {
            CostLimits limits = GetLimits(tier);
            var failures = new List<string>();
            warnings = new List<string>();
            if (report.GpuParticles > limits.GpuParticles) failures.Add($"C_gpu {report.GpuParticles} > {limits.GpuParticles}");
            if (report.CpuParticlePeak > limits.CpuParticlePeak) failures.Add($"C_cpu {report.CpuParticlePeak} > {limits.CpuParticlePeak}");
            if (report.MaxMaterialSampleCost > limits.MaterialSampleCost) failures.Add($"C_mat {report.MaxMaterialSampleCost} > {limits.MaterialSampleCost}");
            if (report.LocalLights > limits.LocalLights) failures.Add($"C_light {report.LocalLights} > {limits.LocalLights}");
            if (report.ShadowedLights > limits.ShadowedLights) failures.Add($"C_shadow {report.ShadowedLights} > {limits.ShadowedLights}");
            if (report.Fragments > limits.Fragments) failures.Add($"C_frag {report.Fragments} > {limits.Fragments}");
            if (report.ClothTier > limits.ClothTier) failures.Add($"C_cloth {report.ClothTier} > {limits.ClothTier}");
            if (report.MaxLayerVertexCount > limits.LayerVertexCount) failures.Add($"C_vert {report.MaxLayerVertexCount} > {limits.LayerVertexCount}");
            if (report.OverdrawEstimate > limits.Overdraw * 1.5f)
                failures.Add($"C_over {report.OverdrawEstimate} > {limits.Overdraw}x1.5 (E300)");
            else if (report.OverdrawEstimate > limits.Overdraw)
                warnings.Add($"C_over {report.OverdrawEstimate} > {limits.Overdraw} (W403, estimate)");
            return failures;
        }

        /// <summary>Computes the cost report from a built product root (EditMode, no rendering).</summary>
        public static CostReport ComputeCosts(GameObject productRoot)
        {
            var report = new CostReport();
            if (productRoot == null) return report;

            var particles = productRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in particles)
                report.CpuParticlePeak += VfxCpuParticleTemplates.TheoreticalPeak(ps);

            var lights3D = productRoot.GetComponentsInChildren<Light>(true);
            foreach (Light l in lights3D)
            {
                report.LocalLights++;
                if (l.shadows != LightShadows.None) report.ShadowedLights++;
            }
            var lights2D = productRoot.GetComponentsInChildren<Light2D>(true);
            foreach (Light2D l in lights2D)
            {
                report.LocalLights++;
                if (l.shadowsEnabled) report.ShadowedLights++;
            }

            report.Fragments = productRoot.GetComponentsInChildren<Rigidbody>(true).Length
                             + productRoot.GetComponentsInChildren<Rigidbody2D>(true).Length;

            var cloths = productRoot.GetComponentsInChildren<Cloth>(true);
            report.ClothTier = cloths.Length > 0 ? 1 : 0;

            var meshFilters = productRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
                if (mf.sharedMesh != null)
                    report.MaxLayerVertexCount = Mathf.Max(report.MaxLayerVertexCount, mf.sharedMesh.vertexCount);

            report.OverdrawEstimate = EstimateOverdraw(productRoot);
            return report;
        }

        /// <summary>
        /// C_over static estimate per spec section 4.4: project every transparent
        /// layer's local-space bounds onto the three principal planes (XY only
        /// for products without depth), rasterize each plane at 32x32 over the
        /// union bounds, count covering layers per cell, and take the 95th
        /// percentile of the busiest plane (never the max: bounding-box corner
        /// gaps produce spurious extremes). Particle bounds inflate by emitter
        /// shape + max speed x max lifetime. Calibrated against the 18 paradigm
        /// samples (T2c unit E, ledger #12).
        /// </summary>
        public static int EstimateOverdraw(GameObject productRoot)
        {
            const int gridSize = 32;
            var boxes = new List<Bounds>();
            Transform rootTransform = productRoot.transform;

            // Voronoi-prefracture fragments partition space by construction:
            // per fragment they never overlap, but their AABBs all cross the
            // partition centre. Counting each AABB would systematically
            // overstate C_over, so one fragment set (renderers that carry a
            // rigidbody, grouped by parent) contributes a single union box.
            var fragmentUnions = new Dictionary<Transform, Bounds>();
            var fragmentSeen = new HashSet<Transform>();

            foreach (Renderer r in productRoot.GetComponentsInChildren<Renderer>(true))
            {
                Material m = r.sharedMaterial;
                if (m == null || m.renderQueue < 2500) continue;
                Bounds local;
                var ps = r.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    local = ParticleBoundsEstimate(ps);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    Mesh mesh = mf != null ? mf.sharedMesh : null;
                    local = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                }
                // Into product root space: lossyScale is identity at edit time for
                // prefab roots, so compose the local TRS chain manually.
                Transform t = r.transform;
                Vector3 center = rootTransform.InverseTransformPoint(t.TransformPoint(local.center));
                Vector3 size = Vector3.Scale(local.size, WorldScaleWithin(t, rootTransform));
                var box = new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));

                bool isFragment = (r.GetComponent<Rigidbody>() != null || r.GetComponent<Rigidbody2D>() != null)
                                  && t.parent != null;
                if (isFragment)
                {
                    Transform group = t.parent;
                    if (fragmentSeen.Add(group)) fragmentUnions[group] = box;
                    else { Bounds u = fragmentUnions[group]; u.Encapsulate(box); fragmentUnions[group] = u; }
                }
                else
                {
                    boxes.Add(box);
                }
            }
            foreach (KeyValuePair<Transform, Bounds> union in fragmentUnions)
                boxes.Add(union.Value);
            if (boxes.Count == 0) return 0;

            // Spec: three principal planes for 3D, XY only for flat (2D) products
            // — the depth-degenerate side planes of a sorting-plane product would
            // count z-stacking twice.
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (Bounds b in boxes) { zMin = Mathf.Min(zMin, b.min.z); zMax = Mathf.Max(zMax, b.max.z); }
            bool flat = (zMax - zMin) < 0.3f;

            int worst = 0;
            // Plane axes: XY, XZ, YZ (axis index pairs).
            var planes = flat
                ? new[] { new Vector2Int(0, 1) }
                : new[] { new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2) };
            foreach (Vector2Int plane in planes)
            {
                float minA = float.MaxValue, minB = float.MaxValue, maxA = float.MinValue, maxB = float.MinValue;
                foreach (Bounds b in boxes)
                {
                    minA = Mathf.Min(minA, b.min[plane.x]); maxA = Mathf.Max(maxA, b.max[plane.x]);
                    minB = Mathf.Min(minB, b.min[plane.y]); maxB = Mathf.Max(maxB, b.max[plane.y]);
                }
                float spanA = Mathf.Max(maxA - minA, 1e-4f);
                float spanB = Mathf.Max(maxB - minB, 1e-4f);
                var counts = new int[gridSize * gridSize];
                foreach (Bounds b in boxes)
                {
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((b.min[plane.x] - minA) / spanA * gridSize), 0, gridSize - 1);
                    int x1 = Mathf.Clamp(Mathf.CeilToInt((b.max[plane.x] - minA) / spanA * gridSize) - 1, 0, gridSize - 1);
                    int y0 = Mathf.Clamp(Mathf.FloorToInt((b.min[plane.y] - minB) / spanB * gridSize), 0, gridSize - 1);
                    int y1 = Mathf.Clamp(Mathf.CeilToInt((b.max[plane.y] - minB) / spanB * gridSize) - 1, 0, gridSize - 1);
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                            counts[y * gridSize + x]++;
                }
                Array.Sort(counts);
                int p95 = counts[Mathf.Clamp(Mathf.FloorToInt(counts.Length * 0.95f), 0, counts.Length - 1)];
                worst = Mathf.Max(worst, p95);
            }
            return worst;
        }

        private static Bounds ParticleBoundsEstimate(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            ParticleSystem.ShapeModule shape = ps.shape;
            float speed = main.startSpeed.mode == ParticleSystemCurveMode.TwoConstants
                ? main.startSpeed.constantMax : main.startSpeed.constant;
            float lifetime = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                ? main.startLifetime.constantMax : main.startLifetime.constant;
            float travel = Mathf.Abs(speed) * Mathf.Max(lifetime, 0f);
            Vector3 emitter = shape.enabled
                ? Vector3.Max(shape.scale, Vector3.one * shape.radius * 2f)
                : Vector3.one * 0.1f;
            Vector3 size = emitter + Vector3.one * (travel * 2f);
            return new Bounds(Vector3.zero, size);
        }

        private static Vector3 WorldScaleWithin(Transform leaf, Transform root)
        {
            Vector3 scale = Vector3.one;
            Transform t = leaf;
            while (t != null && t != root)
            {
                scale = Vector3.Scale(scale, t.localScale);
                t = t.parent;
            }
            return scale;
        }

        // ------------------------------------------------------------ section 6: binding key allow-list

        /// <summary>
        /// The three-segment binding keys (family.target.property). Single
        /// source of truth is <see cref="VfxBindingKeys"/> (runtime assembly)
        /// so the compile-time resolver and the runtime dispatcher share one
        /// id space; this alias exists for editor-side callers and tests.
        /// </summary>
        public static string[] BindingKeys { get { return VfxBindingKeys.Keys; } }

        /// <summary>Compile-time key resolution: unknown keys are rejected (fail-closed).</summary>
        public static int ResolveBindingKey(string key)
        {
            return VfxBindingKeys.Resolve(key);
        }

        /// <summary>Family of a resolved keyId (prefix-derived, never a parallel numeric convention).</summary>
        public static VfxBindingFamily GetBindingFamily(int keyId)
        {
            return VfxBindingKeys.GetFamily(keyId);
        }

        // ------------------------------------------------------------ PX-6 conditional predicate (user decision #16)

        /// <summary>
        /// PX-6 as a conditional predicate: the voxel trio is only demanded for
        /// the categories that actually exist in the product. Missing categories
        /// impose no requirement (user decision #16, T2B_REPORT section 9).
        /// </summary>
        public static bool ValidatePixelVoxelTrio(GameObject productRoot, out List<string> violations)
        {
            violations = new List<string>();
            if (productRoot == null) return true;

            // Pixel-style detection covers both carriers a compiled product can
            // have: the declared local keyword (technique-family shaders) and
            // the legacy shaderKeywords string list (materials whose shader does
            // not declare the keyword yet, e.g. Lit placeholders before T3).
            bool IsPixelStyled(Material m)
            {
                if (m.IsKeywordEnabled(VfxStylePreset.KeywordPixel)) return true;
                string[] legacy = m.shaderKeywords;
                for (int i = 0; i < legacy.Length; i++)
                    if (string.Equals(legacy[i], VfxStylePreset.KeywordPixel, StringComparison.Ordinal)) return true;
                return false;
            }

            // Category 1: mesh-family layers (a MeshFilter with a generated mesh)
            // must have vertex grid snap — approximated by the material carrying
            // a positive _PixelSize (the vdisp grid snap parameter surface).
            var meshFilters = productRoot.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                var renderer = mf.GetComponent<MeshRenderer>();
                Material m = renderer != null ? renderer.sharedMaterial : null;
                if (m == null) continue;
                if (!IsPixelStyled(m)) continue;
                if (!m.HasProperty("_PixelSize") || m.GetFloat("_PixelSize") <= 0f)
                    violations.Add($"PX-6a: mesh layer '{mf.gameObject.name}' lacks vertex grid snap (_PixelSize)");
            }

            // Category 2 (PX-6b): Lit layers under pixel style must quantize
            // their normals. The current library is all-unlit so this branch is
            // vacuous today; the detection is a placeholder contract ("material
            // is Lit" = shader name contains "Lit" or declares _NormalQuantize)
            // that becomes live when T3 introduces the SG_VfxLit master graph.
            var meshRenderers = productRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (MeshRenderer mr in meshRenderers)
            {
                Material m = mr.sharedMaterial;
                if (m == null || m.shader == null) continue;
                if (!IsPixelStyled(m)) continue;
                bool isLit = m.shader.name.Contains("Lit") || m.HasProperty("_NormalQuantize");
                if (!isLit) continue;
                if (!m.HasProperty("_NormalQuantize") || m.GetFloat("_NormalQuantize") <= 0f)
                    violations.Add($"PX-6b: lit layer '{mr.gameObject.name}' lacks normal quantization (_NormalQuantize)");
            }

            // Category 3 (PX-6c): particle layers under pixel style must carry
            // the position-snap parameter on their material.
            var particles = productRoot.GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (ParticleSystemRenderer pr in particles)
            {
                Material m = pr.sharedMaterial;
                if (m == null) continue;
                if (!IsPixelStyled(m)) continue;
                if (!m.HasProperty("_PixelSize") || m.GetFloat("_PixelSize") <= 0f)
                    violations.Add($"PX-6c: particle layer '{pr.gameObject.name}' lacks position snap (_PixelSize)");
            }

            return violations.Count == 0;
        }
    }
}
