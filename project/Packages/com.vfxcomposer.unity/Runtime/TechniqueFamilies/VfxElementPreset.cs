using UnityEngine;

namespace VFXComposer.TechniqueFamilies
{
    /// <summary>
    /// Element preset (ELEMENT_CATALOG_v1 / recipe-v2 element block). One asset
    /// per element id; the compiler merges it into every layer of a recipe so
    /// "define the element once, every archetype gets it" holds. Field names
    /// follow the ACCEPTED schema: the flavor concentration is `strength`
    /// (renamed from draft `intensity`, T2A_REPORT #6-8 / user decision #8).
    /// Format-pipeline validation scope for T2b: fire / ice / lightning samples.
    /// </summary>
    [CreateAssetMenu(menuName = "VFXComposer/Element Preset", fileName = "ElementPreset")]
    public sealed class VfxElementPreset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string elementId = "none";
        [SerializeField, Range(0f, 1f)] private float strength = 0.7f;

        [Header("Palette (linear HDR)")]
        [SerializeField, ColorUsage(false, true)] private Color primary = Color.white;
        [SerializeField, ColorUsage(false, true)] private Color secondary = Color.white;
        [SerializeField, ColorUsage(false, true)] private Color hot = Color.white;
        [SerializeField, ColorUsage(false, true)] private Color cool = Color.gray;
        [SerializeField, ColorUsage(false, true)] private Color residue = Color.black;

        [Header("Material tendencies")]
        [SerializeField] private string primaryMaterialVariant = "mat_volume_fbm";
        [SerializeField] private Vector2 anisotropy = Vector2.one;
        [SerializeField, Range(0f, 3f)] private float speedMul = 1f;
        [SerializeField, Range(0.001f, 0.5f)] private float thresholdWidth = 0.15f;
        [SerializeField] private Vector4 stopMultipliers = new Vector4(0.06f, 0.30f, 1.0f, 4.0f);

        [Header("Light tendencies")]
        [SerializeField] private bool lightEnabled = true;
        [SerializeField, ColorUsage(false, true)] private Color lightColor = Color.white;
        [SerializeField, Range(0f, 4f)] private float lightIntensityMul = 1f;
        [SerializeField] private VfxFlickerMode flickerMode = VfxFlickerMode.Steady;
        [SerializeField, Range(0.1f, 30f)] private float flickerRate = 1f;
        [SerializeField, Range(0f, 1f)] private float flickerDepth = 0.2f;
        [SerializeField] private VfxDecayShape decayShape = VfxDecayShape.Exp;

        [Header("Particle tendencies")]
        [SerializeField] private string gpuTemplate = "vfx_buoyancy_turbulence";
        [SerializeField] private string cpuVariant = "cpu_buoyancy_turbulence";
        [SerializeField] private Vector2 particleLifetime = new Vector2(0.3f, 1.2f);

        [Header("Mesh tendencies")]
        [SerializeField] private string primaryMeshGenerator = "sweep_band";

        public string ElementId { get { return elementId; } }
        public float Strength { get { return strength; } }
        public Color Primary { get { return primary; } }
        public Color Secondary { get { return secondary; } }
        public Color Hot { get { return hot; } }
        public Color Cool { get { return cool; } }
        public Color Residue { get { return residue; } }
        public string PrimaryMaterialVariant { get { return primaryMaterialVariant; } }
        public Vector2 Anisotropy { get { return anisotropy; } }
        public float SpeedMul { get { return speedMul; } }
        public float ThresholdWidth { get { return thresholdWidth; } }
        public Vector4 StopMultipliers { get { return stopMultipliers; } }
        public bool LightEnabled { get { return lightEnabled; } }
        public Color LightColor { get { return lightColor; } }
        public float LightIntensityMul { get { return lightIntensityMul; } }
        public VfxFlickerMode FlickerMode { get { return flickerMode; } }
        public float FlickerRate { get { return flickerRate; } }
        public float FlickerDepth { get { return flickerDepth; } }
        public VfxDecayShape DecayShape { get { return decayShape; } }
        public string GpuTemplate { get { return gpuTemplate; } }
        public string CpuVariant { get { return cpuVariant; } }
        public Vector2 ParticleLifetime { get { return particleLifetime; } }
        public string PrimaryMeshGenerator { get { return primaryMeshGenerator; } }

        /// <summary>Writes the palette + tendencies into a material (compile-time application).</summary>
        public void ApplyToMaterial(Material material)
        {
            if (material == null) return;
            material.SetColor("_ColorPrimary", primary);
            material.SetColor("_ColorSecondary", secondary);
            material.SetColor("_ColorHot", hot);
            material.SetColor("_ColorCool", cool);
            material.SetColor("_ColorResidue", residue);
            if (material.HasProperty("_Speed"))
                material.SetFloat("_Speed", Mathf.Clamp(speedMul, 0.25f, 3f));
            if (material.HasProperty("_ThresholdWidth"))
                material.SetFloat("_ThresholdWidth", thresholdWidth);
        }

        /// <summary>Constructive validation used by the gate predicates (EL-*).</summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(elementId) || !System.Text.RegularExpressions.Regex.IsMatch(elementId, "^[a-z][a-z0-9_.]*$"))
            {
                error = "elementId must match ^[a-z][a-z0-9_.]*$";
                return false;
            }
            if (strength < 0f || strength > 1f)
            {
                error = "strength out of [0,1]";
                return false;
            }
            // HG-1 (adjacent stop luminance ratio >= 2) evaluated on the combined
            // palette * multiplier chain; here assert the multiplier chain alone
            // is non-decreasing and the hot stop is emissive when light is on.
            if (stopMultipliers.x > stopMultipliers.y || stopMultipliers.y > stopMultipliers.z || stopMultipliers.z > stopMultipliers.w)
            {
                error = "stopMultipliers must be non-decreasing";
                return false;
            }
            if (lightEnabled)
            {
                float hotLum = hot.r * 0.2126f + hot.g * 0.7152f + hot.b * 0.0722f;
                if (hotLum * stopMultipliers.w < 1.5f)
                {
                    error = "HG-2: hot stop combined luminance must be >= 1.5 for light-emitting elements";
                    return false;
                }
            }
            error = null;
            return true;
        }
    }
}
