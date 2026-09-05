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
            public int bindingKeyId;  // integerized three-segment key (family.target.property)
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
                ApplyBinding(entry, value);
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

        private void ApplyBinding(BindingEntry entry, float value)
        {
            // bindingKeyId encodes the write target category (compile-time).
            // Family ids: 0xx = mat.self.*, 1xx = light.*, 2xx = ctrl.*.
            if (entry.bindingKeyId < 100)
            {
                if (entry.targetIndex >= rendererTargets.Length) return;
                Renderer r = rendererTargets[entry.targetIndex];
                if (r == null) return;
                r.GetPropertyBlock(block);
                // Property id is baked as the low bits of the key at compile
                // time; here the key id IS the shader property id offset table
                // index. v1 scope: _Progress(0) / _Intensity(1) / custom via table.
                block.SetFloat(entry.bindingKeyId == 0 ? Shader.PropertyToID("_Progress") : IntensityId, value);
                r.SetPropertyBlock(block);
            }
            else if (entry.bindingKeyId < 200)
            {
                if (entry.targetIndex >= lightTargets.Length) return;
                var beat = lightTargets[entry.targetIndex] as VfxLightBeat;
                if (beat == null) return;
                switch (entry.bindingKeyId)
                {
                    case 100: beat.Intensity = value; break;
                    case 101: beat.Range = value; break;
                    case 102: beat.FlickerRate = value; break;
                    case 103: beat.FlickerDepth = value; break;
                }
            }
        }
    }
}
