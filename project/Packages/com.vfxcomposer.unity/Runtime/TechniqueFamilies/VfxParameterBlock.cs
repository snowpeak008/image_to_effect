using System;
using UnityEngine;

namespace VFXComposer.TechniqueFamilies
{
    /// <summary>
    /// Parameter block v2 (COMPILER_BOUNDARY_V2.md section 5.7): standard
    /// parameters + recipe-declared custom parameters + a compile-time binding
    /// table. Values set through the API are dirtied and applied in one batch
    /// per LateUpdate. Binding keys are integer ids resolved at compile time;
    /// runtime never parses strings into type/property paths (the v1 allow-list
    /// discipline preserved). Handles resolve once (binary search over the
    /// sorted id table) and are then zero-string-comparison.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxParameterBlock : MonoBehaviour
    {
        public enum MapKind { Direct = 0, Scale = 1, Offset = 2, Curve01 = 3 }

        [Serializable]
        public struct BindingEntry
        {
            public int paramIndex;
            public int targetIndex;   // -1 = disabled layer (tier cut): no-op, interface stays identical
            public int bindingKeyId;  // dense index into VfxBindingKeys.Keys (single id space with the resolver)
            public int nameId;        // family-specific: mat.* = Shader.PropertyToID baked at compile time
            public MapKind mapKind;
            public float mapArg;
        }

        [Header("Standard parameters")]
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [SerializeField, Range(0.25f, 4f)] private float scale = 1f;
        [SerializeField, Range(0.25f, 3f)] private float speed = 1f;
        [SerializeField] private uint seed;
        [SerializeField] private Gradient palette = new Gradient();

        [Header("Custom parameters (compile-time from interface.parameters)")]
        [SerializeField] private string[] customIds = new string[0]; // sorted ordinal
        [SerializeField] private float[] customValues = new float[0];

        [Header("Binding table (compile-time, read-only at runtime)")]
        [SerializeField] private BindingEntry[] bindings = new BindingEntry[0];
        [SerializeField] private Renderer[] rendererTargets = new Renderer[0];
        [SerializeField] private ParticleSystem[] particleTargets = new ParticleSystem[0];
        [SerializeField] private Component[] lightTargets = new Component[0];
        [SerializeField] private Transform[] transformTargets = new Transform[0];

        private MaterialPropertyBlock block;
        private bool dirty = true;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SpeedId = Shader.PropertyToID("_Speed");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        public float Intensity
        {
            get { return intensity; }
            set { intensity = Mathf.Clamp(value, 0f, 2f); dirty = true; }
        }

        public float Scale
        {
            get { return scale; }
            set { scale = Mathf.Clamp(value, 0.25f, 4f); dirty = true; }
        }

        public float Speed
        {
            get { return speed; }
            set { speed = Mathf.Clamp(value, 0.25f, 3f); dirty = true; }
        }

        public uint Seed
        {
            get { return seed; }
            set { seed = value; dirty = true; }
        }

        public Gradient Palette
        {
            get { return palette; }
            set { palette = value; dirty = true; }
        }

        public BindingEntry[] Bindings { get { return bindings; } }
        public string[] CustomIds { get { return customIds; } }

        /// <summary>Compile-time wiring.</summary>
        public void ConfigureCustom(string[] sortedIds, float[] defaults)
        {
            customIds = sortedIds ?? new string[0];
            customValues = defaults ?? new float[0];
        }

        public void ConfigureBindings(BindingEntry[] table, Renderer[] renderers)
        {
            bindings = table ?? new BindingEntry[0];
            rendererTargets = renderers ?? new Renderer[0];
        }

        public void ConfigureLightTargets(Component[] lights)
        {
            lightTargets = lights ?? new Component[0];
        }

        /// <summary>Resolve a custom parameter id to a stable handle (-1 = unknown). Cache the result.</summary>
        public int Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;
            int lo = 0, hi = customIds.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int cmp = string.CompareOrdinal(customIds[mid], id);
                if (cmp == 0) return mid;
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return -1;
        }

        public bool SetFloat(string id, float value)
        {
            return SetFloat(Resolve(id), value);
        }

        public bool SetFloat(int handle, float value)
        {
            if (handle < 0 || handle >= customValues.Length) return false;
            customValues[handle] = value;
            dirty = true;
            return true;
        }

        public float GetFloat(int handle)
        {
            return handle >= 0 && handle < customValues.Length ? customValues[handle] : 0f;
        }

        public float GetFloat(string id)
        {
            return GetFloat(Resolve(id));
        }

        private void OnValidate()
        {
            dirty = true;
        }

        private void LateUpdate()
        {
            if (!dirty) return;
            dirty = false;
            Apply();
        }

        /// <summary>Batched application of standard + bound parameters.</summary>
        public void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            // Standard parameters broadcast to every renderer (auto-bound, RECIPE_V2 section 5).
            for (int i = 0; i < rendererTargets.Length; i++)
            {
                Renderer r = rendererTargets[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(IntensityId, intensity);
                block.SetFloat(SpeedId, speed);
                block.SetFloat(SeedId, seed % 1024u);
                r.SetPropertyBlock(block);
            }
            transform.localScale = Vector3.one * scale;

            // Custom bindings: compile-time table, integer keys, no reflection.
            for (int i = 0; i < bindings.Length; i++)
            {
                BindingEntry entry = bindings[i];
                if (entry.targetIndex < 0) continue; // disabled layer: no-op keeps the interface identical
                if (entry.paramIndex < 0 || entry.paramIndex >= customValues.Length) continue;
                float value = MapValue(customValues[entry.paramIndex], entry.mapKind, entry.mapArg);
                ApplyBindingEntry(entry, value);
            }
        }

        private static float MapValue(float v, MapKind kind, float arg)
        {
            switch (kind)
            {
                case MapKind.Scale: return v * arg;
                case MapKind.Offset: return v + arg;
                case MapKind.Curve01: return Mathf.Pow(Mathf.Clamp01(v), Mathf.Max(arg, 0.01f));
                default: return v;
            }
        }

        /// <summary>
        /// Dispatches one binding entry. bindingKeyId is the dense index into
        /// VfxBindingKeys.Keys — the exact id ResolveBindingKey returns — and
        /// the family is derived from the key itself, so the resolver and this
        /// dispatcher can never disagree about what an id means. Returns true
        /// when a handler consumed the entry; unknown/out-of-scope ids are
        /// explicitly rejected (false), never silently misrouted.
        /// </summary>
        public bool ApplyBindingEntry(in BindingEntry entry, float value)
        {
            switch (VfxBindingKeys.GetFamily(entry.bindingKeyId))
            {
                case VfxBindingFamily.Mat:
                {
                    string key = VfxBindingKeys.Keys[entry.bindingKeyId];
                    if (!string.Equals(key, "mat.prop.float", StringComparison.Ordinal))
                        return false; // v1 runtime scope: only the float MPB path; other mat keys are compile-time
                    if (entry.targetIndex >= rendererTargets.Length) return false;
                    Renderer r = rendererTargets[entry.targetIndex];
                    if (r == null) return false;
                    if (block == null) block = new MaterialPropertyBlock();
                    r.GetPropertyBlock(block);
                    // nameId comes from the compile-time variant manifest, never from recipe strings.
                    block.SetFloat(entry.nameId != 0 ? entry.nameId : IntensityId, value);
                    r.SetPropertyBlock(block);
                    return true;
                }
                case VfxBindingFamily.Light:
                {
                    if (entry.targetIndex >= lightTargets.Length) return false;
                    var beat = lightTargets[entry.targetIndex] as VfxLightBeat;
                    if (beat == null) return false;
                    switch (VfxBindingKeys.Keys[entry.bindingKeyId])
                    {
                        case "light.beat.intensity": beat.Intensity = value; return true;
                        case "light.beat.range": beat.Range = value; return true;
                        case "light.beat.flickerRate": beat.FlickerRate = value; return true;
                        case "light.beat.flickerDepth": beat.FlickerDepth = value; return true;
                        default: return false; // key exists in the allow-list but has no v1 runtime handler
                    }
                }
                default:
                    // gpu/cpu/mesh/ctrl handlers arrive with the T3 compiler wiring;
                    // until then these ids are explicitly rejected, not misrouted.
                    return false;
            }
        }
    }
}
