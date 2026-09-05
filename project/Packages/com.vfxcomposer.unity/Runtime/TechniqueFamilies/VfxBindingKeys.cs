using System;
using System.Collections.Generic;

namespace VFXComposer.TechniqueFamilies
{
    /// <summary>Binding key family (first segment of family.target.property).</summary>
    public enum VfxBindingFamily { Unknown = -1, Mat = 0, Gpu = 1, Cpu = 2, Mesh = 3, Light = 4, Ctrl = 5 }

    /// <summary>
    /// Single source of truth for the three-segment binding key allow-list
    /// (COMPILER_BOUNDARY_V2.md section 6). Lives in the runtime assembly so
    /// both the compile-time resolver (editor) and the runtime dispatcher
    /// (VfxParameterBlock.ApplyBindingEntry) share one id space: a keyId is
    /// always the dense index into <see cref="Keys"/>, and its family is
    /// derived from the key prefix — never from a parallel numeric convention.
    /// ~112 keys, growth bounded by the Unity API surface, not by content.
    /// </summary>
    public static class VfxBindingKeys
    {
        public static readonly string[] Keys =
        {
            // mat.* — 7 keys, data-driven (nameId resolved from the variant manifest at compile time)
            "mat.prop.float", "mat.prop.vector", "mat.prop.color", "mat.prop.int",
            "mat.keyword", "mat.renderer.sortingOrder", "mat.renderer.enabled",
            // gpu.* — 9 keys
            "gpu.exposed.float", "gpu.exposed.vector", "gpu.exposed.int", "gpu.exposed.uint",
            "gpu.exposed.bool", "gpu.exposed.gradient", "gpu.exposed.curve",
            "gpu.playRate", "gpu.sendEvent",
            // cpu.* — Unity ParticleSystem API fields (~50)
            "cpu.main.startLifetime", "cpu.main.startSpeed", "cpu.main.startSize", "cpu.main.startColor",
            "cpu.main.startRotation", "cpu.main.gravityModifier", "cpu.main.maxParticles", "cpu.main.simulationSpeed",
            "cpu.emission.rateOverTime", "cpu.emission.rateOverDistance", "cpu.emission.burstCount",
            "cpu.emission.burstCycles", "cpu.emission.burstInterval",
            "cpu.shape.radius", "cpu.shape.angle", "cpu.shape.arc", "cpu.shape.scale", "cpu.shape.randomDirectionAmount",
            "cpu.velocity.linearX", "cpu.velocity.linearY", "cpu.velocity.linearZ",
            "cpu.velocity.orbitalX", "cpu.velocity.orbitalY", "cpu.velocity.orbitalZ",
            "cpu.velocity.radial", "cpu.velocity.speedModifier",
            "cpu.limit.limit", "cpu.limit.dampen", "cpu.limit.drag",
            "cpu.force.x", "cpu.force.y", "cpu.force.z",
            "cpu.noise.strength", "cpu.noise.frequency", "cpu.noise.scrollSpeed", "cpu.noise.damping",
            "cpu.color.gradient",
            "cpu.size.curve", "cpu.size.sizeMultiplier",
            "cpu.rotation.angularVelocity",
            "cpu.collision.bounce", "cpu.collision.dampen", "cpu.collision.lifetimeLoss", "cpu.collision.radiusScale",
            "cpu.trails.ratio", "cpu.trails.lifetime", "cpu.trails.widthOverTrail", "cpu.trails.colorOverLifetime",
            "cpu.renderer.sortingOrder", "cpu.renderer.lengthScale", "cpu.renderer.velocityScale", "cpu.renderer.enabled",
            // mesh.* — ~20 keys
            "mesh.transform.localScale", "mesh.transform.localPosition", "mesh.transform.localRotation",
            "mesh.renderer.enabled", "mesh.renderer.sortingOrder", "mesh.renderer.shadowCasting",
            "mesh.trail.time", "mesh.trail.widthMultiplier", "mesh.trail.colorGradient", "mesh.trail.emitting",
            "mesh.line.positionCount", "mesh.line.setPositions", "mesh.line.widthMultiplier", "mesh.line.colorGradient",
            "mesh.cloth.externalAcceleration", "mesh.cloth.randomAcceleration", "mesh.cloth.damping", "mesh.cloth.stretchingStiffness",
            "mesh.debris.breakForce", "mesh.debris.explode", "mesh.debris.gravityScale", "mesh.debris.fadeMode",
            // light.* — 15 keys (always through VfxLightBeat, never the Light directly)
            "light.beat.color", "light.beat.intensity", "light.beat.range", "light.beat.innerAngle", "light.beat.outerAngle",
            "light.beat.flickerMode", "light.beat.flickerRate", "light.beat.flickerDepth", "light.beat.decayShape",
            "light.beat.intensitySteps", "light.beat.flickerQuantize", "light.beat.beatFrameRate",
            "light.beat.castShadows", "light.beat.enabled", "light.beat.phaseOffset",
            // ctrl.* — 11 keys
            "ctrl.phase.duration", "ctrl.phase.autoAdvance",
            "ctrl.layer.offset", "ctrl.layer.duration", "ctrl.layer.enabled",
            "ctrl.speed",
            "ctrl.custom.float", "ctrl.custom.int", "ctrl.custom.bool", "ctrl.custom.enum", "ctrl.custom.vector3"
        };

        private static Dictionary<string, int> index;
        private static VfxBindingFamily[] families;

        /// <summary>Compile-time key resolution: unknown keys return -1 (fail-closed).</summary>
        public static int Resolve(string key)
        {
            EnsureTables();
            return !string.IsNullOrEmpty(key) && index.TryGetValue(key, out int id) ? id : -1;
        }

        /// <summary>
        /// Family of a keyId. Derived once from the key prefix so the dispatcher
        /// can never disagree with the resolver about what an id means.
        /// </summary>
        public static VfxBindingFamily GetFamily(int keyId)
        {
            EnsureTables();
            return keyId >= 0 && keyId < families.Length ? families[keyId] : VfxBindingFamily.Unknown;
        }

        private static void EnsureTables()
        {
            if (index != null) return;
            var map = new Dictionary<string, int>(Keys.Length, StringComparer.Ordinal);
            var fams = new VfxBindingFamily[Keys.Length];
            for (int i = 0; i < Keys.Length; i++)
            {
                map[Keys[i]] = i;
                fams[i] = FamilyFromPrefix(Keys[i]);
            }
            index = map;
            families = fams;
        }

        private static VfxBindingFamily FamilyFromPrefix(string key)
        {
            int dot = key.IndexOf('.');
            string prefix = dot > 0 ? key.Substring(0, dot) : key;
            switch (prefix)
            {
                case "mat": return VfxBindingFamily.Mat;
                case "gpu": return VfxBindingFamily.Gpu;
                case "cpu": return VfxBindingFamily.Cpu;
                case "mesh": return VfxBindingFamily.Mesh;
                case "light": return VfxBindingFamily.Light;
                case "ctrl": return VfxBindingFamily.Ctrl;
                default: return VfxBindingFamily.Unknown;
            }
        }
    }
}
