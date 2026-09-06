using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VFXComposer.TechniqueFamilies;

namespace VFXComposer.Editor.TechniqueFamilies
{
    /// <summary>
    /// T2c sample assembler: the deterministic, general-input constructor for
    /// paradigm sample prefabs. Input = (archetype id, element preset, style
    /// preset, dimension, tier); output = one fully self-contained prefab under
    /// the gallery page-set root convention
    ///   {root}/{archetype}_{element}_{style}/{recipeId}__{tier}.prefab
    /// (GalleryController.ResolveCellPrefabPath). Everything the prefab needs
    /// besides the shared technique-family shaders is embedded as sub-assets
    /// (meshes + materials), so the dependency closure is exactly
    /// {own prefab, Assets/VFX/TechniqueFamilies shaders, package runtime}.
    ///
    /// This is scaffolding for the paradigm verdict (ADR-010 section 8), not
    /// the Recipe v2 compiler (T3): archetype structure comes from the
    /// prototype catalog pages, element and style flavour comes exclusively
    /// from the preset assets — no per-combination special cases. Samples are
    /// verdict evidence; coverage itself arrives with T3/T4.
    /// </summary>
    public static class VfxSampleAssembler
    {
        public static readonly string[] ArchetypeIds = { "shield", "chain_link", "dissolve_out" };
        public static readonly string[] ElementIds = { "ice", "lightning", "poison" };
        public const string StyleId = "cartoon";
        public const string Tier = "PM";
        public const string OutputRoot3D = "Assets/VFX/Generated/3d/";
        public const string OutputRoot2D = "Assets/VFX/Generated/2d/";
        private const string PresetRoot = "Assets/VFX/TechniqueFamilies/Presets/";
        private const string ShaderRoot = "VFXComposer/TechniqueFamilies/";
        private const int PmVertexBudget = 8192;

        // 2D sorting offsets inside one product (TECH_FAMILY_SPEC_MATERIAL section 11 range -40..+30).
        private const int SortVeil = -40;
        private const int SortBody = 0;
        private const int SortEdge = 8;
        private const int SortGlow = 16;
        private const int SortFlash = 24;

        [MenuItem("VFXComposer/Paradigm Samples/Build All (3D + 2D)")]
        public static void BuildAllSamples()
        {
            foreach (bool is3D in new[] { true, false })
                foreach (string archetype in ArchetypeIds)
                    foreach (string element in ElementIds)
                        BuildSample(archetype, element, StyleId, is3D, Tier);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VfxSampleAssembler] 18 paradigm sample prefabs built.");
        }

        /// <summary>Batchmode entry (tools/Invoke-Unity.ps1 -executeMethod).</summary>
        public static void BuildAllFromBatchmode()
        {
            try
            {
                BuildAllSamples();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[VfxSampleAssembler] " + exception);
                EditorApplication.Exit(1);
            }
        }

        public static void Build3DFromBatchmode() { BuildDimensionFromBatchmode(true); }

        public static void Build2DFromBatchmode() { BuildDimensionFromBatchmode(false); }

        private static void BuildDimensionFromBatchmode(bool is3D)
        {
            try
            {
                foreach (string archetype in ArchetypeIds)
                    foreach (string element in ElementIds)
                        BuildSample(archetype, element, StyleId, is3D, Tier);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[VfxSampleAssembler] " + exception);
                EditorApplication.Exit(1);
            }
        }

        public static string RecipeId(string archetype, string element, string style)
        {
            return archetype + "_" + element + "_" + style;
        }

        public static string PrefabPath(string archetype, string element, string style, bool is3D, string tier)
        {
            string id = RecipeId(archetype, element, style);
            return (is3D ? OutputRoot3D : OutputRoot2D) + id + "/" + id + "__" + tier + ".prefab";
        }

        /// <summary>Deterministic per-recipe seed: FNV-1a over the id + dimension.</summary>
        public static uint SeedFor(string recipeId, bool is3D)
        {
            unchecked
            {
                uint hash = 2166136261u;
                string text = recipeId + (is3D ? "|3d" : "|2d");
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619u;
                }
                return hash == 0 ? 1u : hash;
            }
        }

        /// <summary>
        /// Builds one sample prefab. General input surface: any archetype in the
        /// structural table x any element preset x any style preset x dimension.
        /// </summary>
        public static string BuildSample(string archetype, string element, string style, bool is3D, string tier)
        {
            VfxElementPreset elementPreset = LoadElementPreset(element);
            VfxStylePreset stylePreset = LoadStylePreset(style);
            if (elementPreset == null) throw new InvalidOperationException("Element preset not found: " + element);
            if (stylePreset == null) throw new InvalidOperationException("Style preset not found: " + style);

            string recipeId = RecipeId(archetype, element, style);
            string path = PrefabPath(archetype, element, style, is3D, tier);
            uint seed = SeedFor(recipeId, is3D);

            var ctx = new BuildContext
            {
                RecipeId = recipeId,
                Is3D = is3D,
                Seed = seed,
                Element = elementPreset,
                Style = stylePreset
            };

            GameObject root;
            switch (archetype)
            {
                case "shield": root = BuildShield(ctx); break;
                case "chain_link": root = BuildChainLink(ctx); break;
                case "dissolve_out": root = BuildDissolveOut(ctx); break;
                default: throw new ArgumentException("Unknown sample archetype: " + archetype);
            }

            try
            {
                SavePrefab(root, path, ctx);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
            return path;
        }

        // ------------------------------------------------------------ context + helpers

        private sealed class BuildContext
        {
            public string RecipeId;
            public bool Is3D;
            public uint Seed;
            public VfxElementPreset Element;
            public VfxStylePreset Style;
            public readonly List<UnityEngine.Object> SubAssets = new List<UnityEngine.Object>();
            public readonly List<Renderer> BeatTargets = new List<Renderer>();
            public Mesh SharedQuad;
        }

        private static VfxElementPreset LoadElementPreset(string element)
        {
            string name = char.ToUpperInvariant(element[0]) + element.Substring(1);
            return AssetDatabase.LoadAssetAtPath<VfxElementPreset>(PresetRoot + "Element_" + name + ".asset");
        }

        private static VfxStylePreset LoadStylePreset(string style)
        {
            string name = char.ToUpperInvariant(style[0]) + style.Substring(1);
            return AssetDatabase.LoadAssetAtPath<VfxStylePreset>(PresetRoot + "Style_" + name + ".asset");
        }

        private static Material MakeMaterial(BuildContext ctx, string shaderName, string materialName)
        {
            var shader = Shader.Find(ShaderRoot + shaderName);
            if (shader == null) throw new InvalidOperationException("Shader missing: " + shaderName);
            var material = new Material(shader) { name = ctx.RecipeId + "_" + materialName };
            ctx.Element.ApplyToMaterial(material);
            ctx.Style.ApplyToMaterial(material);
            if (material.HasProperty("_Seed")) material.SetFloat("_Seed", ctx.Seed % 1024u);
            ctx.SubAssets.Add(material);
            return material;
        }

        private static Mesh RegisterMesh(BuildContext ctx, Mesh mesh, string name)
        {
            mesh.name = ctx.RecipeId + "_" + name;
            ctx.SubAssets.Add(mesh);
            return mesh;
        }

        private static Mesh Quad(BuildContext ctx)
        {
            if (ctx.SharedQuad != null) return ctx.SharedQuad;
            var b = new VfxMeshBuilder();
            int a = b.AddVertex(new Vector3(-0.5f, -0.5f, 0f), Vector3.back, new Vector2(0f, 0f), Vector2.zero, Color.white);
            int c = b.AddVertex(new Vector3(0.5f, -0.5f, 0f), Vector3.back, new Vector2(1f, 0f), Vector2.zero, Color.white);
            int d = b.AddVertex(new Vector3(0.5f, 0.5f, 0f), Vector3.back, new Vector2(1f, 1f), Vector2.zero, Color.white);
            int e = b.AddVertex(new Vector3(-0.5f, 0.5f, 0f), Vector3.back, new Vector2(0f, 1f), Vector2.zero, Color.white);
            b.AddQuad(a, c, d, e);
            ctx.SharedQuad = RegisterMesh(ctx, b.Build("quad"), "quad");
            return ctx.SharedQuad;
        }

        private static GameObject MakeLayer(GameObject root, string name)
        {
            var layer = new GameObject(name);
            layer.transform.SetParent(root.transform, false);
            return layer;
        }

        private static MeshRenderer AddMesh(BuildContext ctx, GameObject node, Mesh mesh, Material material, int sortingOrder)
        {
            var filter = node.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (!ctx.Is3D) renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>
        /// Glow stack: layerCount additive billboard quads with per-layer
        /// decorrelated breakup, outward radius/intensity ratio and beat
        /// coupling (REFERENCE_ANALYSIS section 3bis; M-13 compensation lives
        /// in the shader). Registered as beat targets so light and glow
        /// breathe together.
        /// </summary>
        private static GameObject BuildGlowStack(BuildContext ctx, GameObject root, string name,
            float baseScale, int layerCount, float layerRadiusRatio, float layerIntensityRatio, float anisotropy)
        {
            // Quality yardstick (REFERENCE_ANALYSIS section 2-2): the glow is
            // the SOFT half of hard+soft — a halo BEHIND the hard form layers,
            // never a wash over them. Additive blending sums layer energy, so
            // the per-layer target luminance must be well below the form
            // layers' hot stop. Palette colours are HDR (hot up to 8): they are
            // luminance-normalized here so every element gets the same halo
            // energy, and the shader's M-13 size compensation is divided back
            // out (we want a fixed target, not size-proportional gain).
            const float innerTargetLum = 0.5f;
            const float outerTargetLum = 0.22f;
            var stack = MakeLayer(root, name);
            for (int k = 0; k < layerCount; k++)
            {
                var layer = new GameObject("Glow_" + k);
                layer.transform.SetParent(stack.transform, false);
                float scale = baseScale * Mathf.Pow(layerRadiusRatio, k);
                layer.transform.localScale = new Vector3(scale, scale, 1f);
                Material glow = MakeMaterial(ctx, "GlowStack", name + "_glow" + k);
                // Cartoon compatibility measure #1 (STYLE_IMPL_CARTOON_PIXEL
                // section 2.8): the glow stack is NEVER cel-quantized — its
                // cartoon reading comes from the stepped falloff ring stack.
                glow.DisableKeyword(VfxStylePreset.KeywordCartoon);
                glow.DisableKeyword(VfxStylePreset.KeywordPixel);
                glow.DisableKeyword("_FALLOFF_GAUSSIAN");
                glow.EnableKeyword("_FALLOFF_STEP");
                glow.SetVector("_FalloffParams", new Vector4(1.5f, 4f, 0f, 0f)); // 4-step ring stack
                glow.SetColor("_InnerColor", NormalizeLum(ctx.Element.Hot, innerTargetLum));
                glow.SetColor("_OuterColor", NormalizeLum(ctx.Element.Primary, outerTargetLum));
                glow.SetFloat("_ColorMixPower", 2.2f); // hot core only at the very centre
                float compensation = Mathf.Pow(Mathf.Max(scale, 1f), 0.85f);
                glow.SetFloat("_Intensity", Mathf.Pow(layerIntensityRatio, k) / compensation);
                glow.SetFloat("_GlowSize", Mathf.Max(scale, 1f));
                glow.SetFloat("_LayerIndex", k);
                glow.SetFloat("_BreakupSeed", ctx.Seed % 97u + k * 17f);
                glow.SetFloat("_BreakupNoise", 0.55f); // broken edges, no "sticker disc"
                glow.SetFloat("_Anisotropy", anisotropy);
                glow.SetFloat("_Billboard", ctx.Is3D ? 1f : 0f);
                var renderer = AddMesh(ctx, layer, Quad(ctx), glow, SortGlow + k);
                ctx.BeatTargets.Add(renderer);
            }
            return stack;
        }

        private static Color NormalizeLum(Color c, float targetLum)
        {
            float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            if (lum <= 1e-4f) return c;
            float mul = targetLum / lum;
            return new Color(c.r * mul, c.g * mul, c.b * mul, c.a);
        }

        /// <summary>Local light node: Light (3D) or Light2D (2D) + VfxLightBeat from the element preset.</summary>
        private static VfxLightBeat BuildLight(BuildContext ctx, GameObject root, string name, Vector3 localPosition)
        {
            var node = MakeLayer(root, name);
            node.transform.localPosition = localPosition;
            Component target;
            VfxLightDriverKind kind;
            if (ctx.Is3D)
            {
                var light = node.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = ctx.Element.LightColor;
                light.intensity = ctx.Element.LightIntensityMul * 2.2f;
                light.range = 6f;
                light.shadows = LightShadows.None;
                target = light;
                kind = VfxLightDriverKind.Light3D;
            }
            else
            {
                var light2D = node.AddComponent<Light2D>();
                light2D.lightType = Light2D.LightType.Point;
                light2D.color = ctx.Element.LightColor;
                light2D.intensity = ctx.Element.LightIntensityMul * 1.4f;
                light2D.pointLightOuterRadius = 4f;
                light2D.shadowsEnabled = false;
                target = light2D;
                kind = VfxLightDriverKind.Light2D;
            }
            var beat = node.AddComponent<VfxLightBeat>();
            beat.Configure(kind, target, null, 0);
            beat.ConfigureLight(ctx.Element.LightColor, ctx.Is3D ? ctx.Element.LightIntensityMul * 2.2f : ctx.Element.LightIntensityMul * 1.4f, ctx.Is3D ? 6f : 4f, false);
            beat.ConfigureBeat(ctx.Element.FlickerMode, ctx.Element.FlickerRate, ctx.Element.FlickerDepth, ctx.Element.DecayShape, 0f);
            ctx.Style.ApplyToLightBeat(beat);
            return beat;
        }

        private static ParticleSystem BuildParticles(BuildContext ctx, GameObject root, string name, int maxParticles, int sortingOrder)
        {
            var node = MakeLayer(root, name);
            var ps = node.AddComponent<ParticleSystem>();
            VfxCpuParticleTemplates.Configure(ps, ctx.Element.CpuVariant, ctx.Seed, maxParticles);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(ctx.Element.ParticleLifetime.x, ctx.Element.ParticleLifetime.y);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = ctx.Element.Primary;
            var renderer = node.GetComponent<ParticleSystemRenderer>();
            Material sparkMaterial = MakeMaterial(ctx, "SdfShape", name + "_spark");
            sparkMaterial.SetFloat("_Shape", ElementSparkShape(ctx.Element.ElementId));
            sparkMaterial.SetFloat("_Softness", 0.06f);
            renderer.sharedMaterial = sparkMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (!ctx.Is3D)
            {
                renderer.sortingOrder = sortingOrder;
                var shape = ps.shape;
                if (shape.enabled) shape.scale = new Vector3(shape.scale.x, shape.scale.y, 0.01f);
            }
            return ps;
        }

        private static float ElementSparkShape(string elementId)
        {
            switch (elementId)
            {
                case "lightning": return 2f; // star
                case "ice": return 3f;       // polygon shard
                default: return 0f;          // circle droplet
            }
        }

        private static void FinishController(BuildContext ctx, GameObject root, VfxController controller,
            GameObject[] layers, VfxLightBeat beat, string[] sortedEventIds, int[] handlers,
            string[] customParams, float[] customDefaults, VfxParameterBlock.BindingEntry[] bindings,
            Renderer[] bindingRenderers)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            controller.ConfigureLayers(layers, renderers);
            controller.ConfigureEvents(sortedEventIds, handlers);
            controller.ConfigureParticles(root.GetComponentsInChildren<ParticleSystem>(true));
            controller.ConfigureLightBeats(new[] { beat });
            beat.Configure(beat.Kind, beat.LightTarget, ctx.BeatTargets.ToArray(), 0);

            var block = root.GetComponent<VfxParameterBlock>();
            block.ConfigureCustom(customParams, customDefaults);
            block.ConfigureBindings(bindings, bindingRenderers);
            block.ConfigureLightTargets(new Component[] { beat });

            if (!ctx.Is3D)
            {
                var group = root.AddComponent<SortingGroup>();
                group.sortingOrder = 0;
            }
        }

        private static VfxParameterBlock.BindingEntry MatFloatBinding(int paramIndex, int rendererIndex, string shaderProperty)
        {
            int keyId = VfxBindingKeys.Resolve("mat.prop.float");
            return new VfxParameterBlock.BindingEntry
            {
                paramIndex = paramIndex,
                targetIndex = rendererIndex,
                bindingKeyId = keyId,
                nameId = Shader.PropertyToID(shaderProperty),
                mapKind = VfxParameterBlock.MapKind.Direct
            };
        }

        private static VfxController.PhaseEntry Phase(string id, float duration, bool loop, params int[] activeLayers)
        {
            return new VfxController.PhaseEntry
            {
                id = id,
                duration = duration,
                loop = loop,
                autoAdvance = !loop,
                activeLayers = activeLayers
            };
        }

        // ------------------------------------------------------------ B04 shield

        /// <summary>
        /// B04 shield (PROTOTYPE_CATALOG_v1): shell (fresnel + cell pattern) /
        /// ground contact ring / up-to-4 hit ripples via hitAt / crack channel
        /// via setIntegrity / prefractured debris + rigidbodies released by
        /// break / glow stack / breathing local light. Element flavour enters
        /// only through the preset (palette, cell look, beat, particles).
        /// </summary>
        private static GameObject BuildShield(BuildContext ctx)
        {
            var root = new GameObject(ctx.RecipeId);
            var controller = root.AddComponent<VfxController>();
            root.AddComponent<VfxParameterBlock>();

            var genArgs = new VfxMeshGenArgs(ctx.Seed, ctx.Is3D, PmVertexBudget);

            // Layer 0: shell.
            var shellNode = MakeLayer(root, "Shell");
            Mesh shellMesh = ctx.Is3D
                ? RegisterMesh(ctx, VfxMeshGenerators.ShellPolyhedron(genArgs, 2, false, 0f, 1.1f), "shell")
                : Quad(ctx);
            if (!ctx.Is3D) shellNode.transform.localScale = new Vector3(2.4f, 2.6f, 1f);
            Material shellMaterial = MakeMaterial(ctx, "ShellRipple", "shell");
            shellMaterial.SetFloat("_Sdf2DPath", ctx.Is3D ? 0f : 1f);
            shellMaterial.SetFloat("_CellDensity", ElementCellDensity(ctx.Element.ElementId));
            shellMaterial.SetColor("_ColorHot", ctx.Element.Hot);
            var shellRenderer = AddMesh(ctx, shellNode, shellMesh, shellMaterial, SortBody);
            ctx.BeatTargets.Add(shellRenderer);

            // Layer 1: ground contact ring (edge role).
            var ringNode = MakeLayer(root, "ContactRing");
            Material ringMaterial = MakeMaterial(ctx, "RingPolar", "ring");
            ringMaterial.SetFloat("_RingWidth", 0.18f);
            ringMaterial.SetFloat("_Intensity", 0.7f);
            if (ctx.Is3D)
            {
                ringNode.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                ringNode.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ringNode.transform.localScale = new Vector3(2.6f, 2.6f, 1f);
            }
            else
            {
                ringNode.transform.localPosition = new Vector3(0f, -1.25f, 0f);
                ringNode.transform.localScale = new Vector3(2.4f, 0.8f, 1f); // flattened ellipse = 2D ground line
            }
            AddMesh(ctx, ringNode, Quad(ctx), ringMaterial, SortEdge);

            // Layer 2: glow stack (halo just outside the shell radius).
            GameObject glow = BuildGlowStack(ctx, root, "Glow", ctx.Is3D ? 2.6f : 2.8f, 2, 1.5f, 0.45f, 0f);

            // Layer 3: prefractured debris (kinematic until break).
            var debrisRoot = MakeLayer(root, "Debris");
            Material debrisMaterial = MakeMaterial(ctx, ElementDebrisShader(ctx.Element.ElementId), "debris");
            List<Mesh> fragments = VfxMeshGenerators.VoronoiPrefracture(
                new VfxMeshGenArgs(ctx.Seed, ctx.Is3D, PmVertexBudget), 12,
                ctx.Is3D ? new Vector3(2.2f, 2.2f, 2.2f) : new Vector3(2.4f, 2.6f, 0.2f), 0, Vector3.zero);
            var bodies3D = new List<Rigidbody>();
            var bodies2D = new List<Rigidbody2D>();
            for (int i = 0; i < fragments.Count; i++)
            {
                Mesh fragment = RegisterMesh(ctx, fragments[i], "frag" + i);
                var fragmentNode = new GameObject("Fragment_" + i);
                fragmentNode.transform.SetParent(debrisRoot.transform, false);
                AddMesh(ctx, fragmentNode, fragment, debrisMaterial, SortFlash);
                if (ctx.Is3D)
                {
                    var collider = fragmentNode.AddComponent<BoxCollider>();
                    collider.center = fragment.bounds.center;
                    collider.size = Vector3.Max(fragment.bounds.size, Vector3.one * 0.05f);
                    var body = fragmentNode.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                    bodies3D.Add(body);
                }
                else
                {
                    var collider2D = fragmentNode.AddComponent<BoxCollider2D>();
                    collider2D.offset = fragment.bounds.center;
                    collider2D.size = Vector2.Max(fragment.bounds.size, Vector2.one * 0.05f);
                    var body2D = fragmentNode.AddComponent<Rigidbody2D>();
                    body2D.bodyType = RigidbodyType2D.Kinematic;
                    bodies2D.Add(body2D);
                }
            }
            if (ctx.Is3D) controller.ConfigureDebris(bodies3D.ToArray());
            else controller.ConfigureDebris2D(bodies2D.ToArray());

            // Layer 4: light.
            VfxLightBeat beat = BuildLight(ctx, root, "ShieldLight", ctx.Is3D ? new Vector3(0f, 0.6f, 0f) : Vector3.zero);

            // Phase table: launch -> sustain(loop) -> break -> end. Hits are
            // events, not phases (catalog: impact == hitAt ripple).
            controller.ConfigurePhases(new[]
            {
                Phase("launch", 0.35f, false, 0, 1, 2, 4),
                Phase("sustain", 2.4f, true, 0, 1, 2, 4),
                Phase("break", 1.3f, false, 2, 3, 4),
                Phase("end", 0.5f, false, 2, 4)
            });

            var layers = new[] { shellNode, ringNode, glow, debrisRoot, beat.gameObject };
            // Sorted ordinal event table; impact aliases hitAt (B04 impact == hit ripple).
            string[] eventIds = { "break", "end", "hitAt", "impact", "launch", "setIntegrity", "setProgress", "travel" };
            int[] handlers = { 4, 3, 7, 7, 0, 8, 5, 1 };
            string[] customIds = { "breakForce", "hitRippleDuration", "integrity", "shellRadius" };
            float[] customDefaults = { 3f, 0.45f, 1f, 1.1f };
            var bindings = new[]
            {
                MatFloatBinding(1, 0, "_RippleDuration"), // hitRippleDuration -> shell material
                MatFloatBinding(2, 0, "_Integrity")       // integrity -> crack channel (event path also writes it)
            };
            FinishController(ctx, root, controller, layers, beat, eventIds, handlers,
                customIds, customDefaults, bindings, new Renderer[] { shellRenderer });
            return root;
        }

        private static float ElementCellDensity(string elementId)
        {
            switch (elementId)
            {
                case "ice": return 10f;       // hard crystalline facets
                case "lightning": return 14f; // fine energy mesh
                default: return 5f;           // large sticky cells
            }
        }

        private static string ElementDebrisShader(string elementId)
        {
            switch (elementId)
            {
                case "ice": return "NoiseVoronoiCrystal";
                case "lightning": return "FresnelEdge";
                default: return "BubbleField";
            }
        }

        // ------------------------------------------------------------ A10 chain_link

        /// <summary>
        /// A10 chain_link: node sequence with per-hop segment reveal, node
        /// flash + latest-node light + glow followers, per-hop particle bursts
        /// and onHop outbound events. Segment topology per element personality:
        /// jagged fork (jump), near-straight crystalline band (hard edge) or
        /// sagging catenary (viscous).
        /// </summary>
        private static GameObject BuildChainLink(BuildContext ctx)
        {
            var root = new GameObject(ctx.RecipeId);
            var controller = root.AddComponent<VfxController>();
            root.AddComponent<VfxParameterBlock>();

            // Node layout: 4 nodes / 3 segments zig-zagging upward through the cell.
            Vector3[] nodes = ctx.Is3D
                ? new[]
                {
                    new Vector3(-1.1f, 0.3f, 0f), new Vector3(0.2f, 0.9f, 0.35f),
                    new Vector3(-0.35f, 1.5f, -0.3f), new Vector3(1.05f, 1.9f, 0.15f)
                }
                : new[]
                {
                    new Vector3(-1.4f, -0.9f, 0f), new Vector3(0.1f, -0.1f, 0f),
                    new Vector3(-0.5f, 0.8f, 0f), new Vector3(1.3f, 1.3f, 0f)
                };
            const float hopInterval = 0.18f;

            // Layer 0: segments (revealed hop by hop by the controller sequencer).
            var segmentsRoot = MakeLayer(root, "Segments");
            Material segmentMaterial = MakeMaterial(ctx, ElementSegmentShader(ctx.Element.ElementId), "segment");
            var revealNodes = new GameObject[nodes.Length];
            revealNodes[0] = null; // node 0 is the origin: nothing to reveal
            for (int i = 1; i < nodes.Length; i++)
            {
                var segmentNode = new GameObject("Segment_" + (i - 1));
                segmentNode.transform.SetParent(segmentsRoot.transform, false);
                Mesh segmentMesh = RegisterMesh(ctx, BuildSegmentMesh(ctx, nodes[i - 1], nodes[i], i), "seg" + (i - 1));
                AddMesh(ctx, segmentNode, segmentMesh, segmentMaterial, SortBody);
                segmentNode.SetActive(false);
                revealNodes[i] = segmentNode;
            }

            // Layer 1: node flash (follower quad).
            var flashNode = MakeLayer(root, "NodeFlash");
            Material flashMaterial = MakeMaterial(ctx, "SdfShape", "flash");
            flashMaterial.SetFloat("_Shape", ElementSparkShape(ctx.Element.ElementId));
            flashMaterial.SetFloat("_Intensity", 1.6f);
            flashNode.transform.localScale = Vector3.one * 0.55f;
            AddMesh(ctx, flashNode, Quad(ctx), flashMaterial, SortFlash);

            // Layer 2: latest-node glow stack (follower).
            GameObject glow = BuildGlowStack(ctx, root, "NodeGlow", 1.1f, 2, 1.6f, 0.4f,
                ctx.Element.ElementId == "lightning" ? 1.2f : 0f);

            // Layer 3: per-hop emission bursts.
            ParticleSystem burst = BuildParticles(ctx, root, "NodeEmission", 120, SortFlash);
            var burstEmission = burst.emission;
            burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new ParticleSystem.Burst[0]); // manual Emit per hop only

            // Layer 4: latest-node light (follower).
            VfxLightBeat beat = BuildLight(ctx, root, "NodeLight", nodes[0]);

            controller.ConfigureNodes(nodes, hopInterval,
                new[] { beat.transform, flashNode.transform, glow.transform },
                new[] { burst }, revealNodes);

            float travelDuration = nodes.Length * hopInterval + 0.35f;
            controller.ConfigurePhases(new[]
            {
                Phase("launch", 0.08f, false, 0, 4),
                Phase("travel", travelDuration, false, 0, 1, 2, 3, 4),
                Phase("end", 0.5f, false, 2, 4)
            });

            var layers = new[] { segmentsRoot, flashNode, glow, burst.gameObject, beat.gameObject };
            string[] eventIds = { "addNode", "end", "impact", "launch", "travel" };
            int[] handlers = { 9, 3, 2, 0, 1 };
            string[] customIds = { "damping", "hopCount", "hopInterval", "jitter", "sag", "segmentLifetime" };
            float[] customDefaults = { 0.15f, nodes.Length, hopInterval, 0.35f, ctx.Element.ElementId == "poison" ? 0.5f : 0f, 0.4f };
            var bindings = new[]
            {
                MatFloatBinding(3, 0, ctx.Element.ElementId == "lightning" ? "_Jitter" : "_Intensity")
            };
            FinishController(ctx, root, controller, layers, beat, eventIds, handlers,
                customIds, customDefaults, bindings, new Renderer[] { segmentsRoot.transform.GetChild(0).GetComponent<Renderer>() });
            return root;
        }

        private static string ElementSegmentShader(string elementId)
        {
            switch (elementId)
            {
                case "lightning": return "NoiseJagged1D";
                case "ice": return "NoiseVoronoiCrystal";
                default: return "BubbleField";
            }
        }

        private static Mesh BuildSegmentMesh(BuildContext ctx, Vector3 start, Vector3 end, int index)
        {
            var args = new VfxMeshGenArgs(ctx.Seed + (uint)index * 101u, ctx.Is3D, 1024);
            switch (ctx.Element.ElementId)
            {
                case "poison":
                    // Viscous sagging band between the nodes.
                    return VfxMeshGenerators.CatenaryBand(args, start, end, 0.45f, 20, 0.14f, 0.12f);
                case "ice":
                    // Hard, nearly straight crystalline band (low jitter, no forks).
                    return VfxMeshGenerators.JaggedPolyline(args, start, end, 10, 0.08f, 0, 0f, 0.4f, 0.12f);
                default:
                    // Jagged forked bolt.
                    return VfxMeshGenerators.JaggedPolyline(args, start, end, 14, 0.3f, 2, 0.55f, 0.45f, 0.08f);
            }
        }

        // ------------------------------------------------------------ C06 dissolve_out

        /// <summary>
        /// C06 dissolve_out: threshold dissolve over a neutral stand-in body
        /// (targetRenderer is empty in the gallery, so the catalog's fallback
        /// geometry applies), edge-front glow inside the dissolve material,
        /// particles shed from the body surface, ground residue, light decaying
        /// with progress. progress is externally drivable via setProgress.
        /// </summary>
        private static GameObject BuildDissolveOut(BuildContext ctx)
        {
            var root = new GameObject(ctx.RecipeId);
            var controller = root.AddComponent<VfxController>();
            root.AddComponent<VfxParameterBlock>();

            var genArgs = new VfxMeshGenArgs(ctx.Seed, ctx.Is3D, PmVertexBudget);

            // Layer 0: dissolving body (neutral stand-in: rock chunk / quad).
            var bodyNode = MakeLayer(root, "Body");
            Mesh bodyMesh = ctx.Is3D
                ? RegisterMesh(ctx, VfxMeshGenerators.RockChunk(genArgs, 2, 0.3f, 0.2f, 0.25f), "body")
                : Quad(ctx);
            if (ctx.Is3D)
            {
                bodyNode.transform.localScale = Vector3.one * 1.5f;
                bodyNode.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            }
            else
            {
                bodyNode.transform.localScale = new Vector3(1.9f, 2.3f, 1f);
            }
            Material dissolveMaterial = MakeMaterial(ctx, "ThresholdDissolve", "dissolve");
            dissolveMaterial.SetFloat("_Mode", 2f); // grow mode: threshold front advances
            dissolveMaterial.SetFloat("_EdgeGlowWidth", 0.1f);
            dissolveMaterial.SetFloat("_Progress", 0f);
            var bodyRenderer = AddMesh(ctx, bodyNode, bodyMesh, dissolveMaterial, SortBody);
            ctx.BeatTargets.Add(bodyRenderer);

            // Layer 1: shed particles from the body surface (approximates
            // threshold-edge emission on the CPU family: mesh-shape source).
            ParticleSystem shed = BuildParticles(ctx, root, "Shed", 240, SortFlash);
            var shedShape = shed.shape;
            shedShape.enabled = true;
            if (ctx.Is3D)
            {
                shedShape.shapeType = ParticleSystemShapeType.Mesh;
                shedShape.mesh = bodyMesh;
                shed.transform.localPosition = bodyNode.transform.localPosition;
                shed.transform.localScale = bodyNode.transform.localScale;
            }
            else
            {
                shedShape.shapeType = ParticleSystemShapeType.Rectangle;
                shedShape.scale = new Vector3(1.9f, 2.3f, 1f);
            }
            var shedEmission = shed.emission;
            shedEmission.enabled = true;
            shedEmission.rateOverTime = 60f;

            // Layer 2: ground residue.
            var residueNode = MakeLayer(root, "Residue");
            Material residueMaterial = MakeMaterial(ctx, ElementResidueShader(ctx.Element.ElementId), "residue");
            residueMaterial.SetFloat("_Intensity", 0.5f);
            if (ctx.Is3D)
            {
                residueNode.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                residueNode.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                residueNode.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
            }
            else
            {
                residueNode.transform.localPosition = new Vector3(0f, -1.3f, 0f);
                residueNode.transform.localScale = new Vector3(2f, 0.7f, 1f);
            }
            AddMesh(ctx, residueNode, Quad(ctx), residueMaterial, SortVeil);

            // Layer 3: glow stack around the dissolve front.
            GameObject glow = BuildGlowStack(ctx, root, "Glow", 2.1f, 2, 1.6f, 0.4f, 0f);
            if (ctx.Is3D) glow.transform.localPosition = bodyNode.transform.localPosition;

            // Layer 4: light (decays with the phase tail).
            VfxLightBeat beat = BuildLight(ctx, root, "DissolveLight",
                ctx.Is3D ? new Vector3(0f, 1.1f, 0f) : Vector3.zero);

            controller.ConfigurePhases(new[]
            {
                Phase("launch", 0.01f, false, 0, 3, 4),
                Phase("travel", 1.6f, false, 0, 1, 2, 3, 4),
                Phase("end", 0.6f, false, 1, 2, 4)
            });

            var layers = new[] { bodyNode, shed.gameObject, residueNode, glow, beat.gameObject };
            string[] eventIds = { "end", "launch", "setProgress", "travel" };
            int[] handlers = { 3, 0, 5, 1 };
            string[] customIds = { "dissolveDuration", "edgeWidth", "progress", "shedDensity" };
            float[] customDefaults = { 1.6f, 0.1f, 0f, 60f };
            var bindings = new[]
            {
                MatFloatBinding(1, 0, "_EdgeGlowWidth"),
                MatFloatBinding(2, 0, "_Progress")
            };
            FinishController(ctx, root, controller, layers, beat, eventIds, handlers,
                customIds, customDefaults, bindings, new Renderer[] { bodyRenderer });
            return root;
        }

        private static string ElementResidueShader(string elementId)
        {
            switch (elementId)
            {
                case "ice": return "NoiseVoronoiCrystal";  // frost patch
                case "lightning": return "SdfCrackBranch"; // scorch forks
                default: return "BubbleField";             // corroding blotch
            }
        }

        // ------------------------------------------------------------ prefab save

        /// <summary>
        /// Persist generated meshes/materials as sibling assets inside the
        /// product folder first (the battle-tested compiler pattern: references
        /// from a prefab must point at persistent objects), then save the
        /// prefab. Dependency closure = own folder + shared shader library.
        /// </summary>
        private static void SavePrefab(GameObject root, string path, BuildContext ctx)
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
                AssetDatabase.DeleteAsset(folder); // deterministic rebuild: no stale outputs
            EnsureFolder(folder);

            foreach (UnityEngine.Object subAsset in ctx.SubAssets)
            {
                string extension = subAsset is Material ? ".mat" : ".asset";
                AssetDatabase.CreateAsset(subAsset, folder + "/" + Sanitize(subAsset.name) + extension);
            }
            if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                throw new InvalidOperationException("Could not save sample prefab: " + path);
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
