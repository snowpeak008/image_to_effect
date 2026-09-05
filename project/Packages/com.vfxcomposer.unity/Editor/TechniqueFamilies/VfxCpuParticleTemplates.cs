using System;
using UnityEngine;

namespace VFXComposer.Editor.TechniqueFamilies
{
    /// <summary>
    /// CPU particle family: the 14 per-template ParticleSystem module
    /// combinations (TECH_FAMILY_SPEC_PARTICLES.md section 4.2). Each entry is
    /// the honest degrade of the matching GPU field template. Discipline
    /// (section 4.3) is enforced here at build time: playOnAwake off,
    /// autoRandomSeed off (deterministic capture), TextureSheetAnimation /
    /// Lights / ExternalForces never enabled.
    /// GPU note: the 14 .vfx graph assets are NOT generated in this unit — the
    /// project manifest does not ship com.unity.visualeffectgraph and hand
    /// authoring .vfx YAML is unreliable; the CPU set carries the full
    /// parameter surface so the GPU set can be authored against it in T2c
    /// (registered as a known limitation in T2B_IMPL_REPORT.md).
    /// </summary>
    public static class VfxCpuParticleTemplates
    {
        public static readonly string[] Ids =
        {
            "cpu_buoyancy_turbulence", "cpu_gravity_settle", "cpu_instant_rephase", "cpu_gravity_drag_split",
            "cpu_vortex", "cpu_orbital_hover", "cpu_step_grid", "cpu_attract_target",
            "cpu_drift_curl", "cpu_fall_wind", "cpu_burst_radial", "cpu_strip_trail",
            "cpu_surface_scatter", "cpu_mesh_shatter"
        };

        /// <summary>Configures a ParticleSystem as the given CPU template variant.</summary>
        public static void Configure(ParticleSystem ps, string id, uint seed, int maxParticles)
        {
            if (ps == null) throw new ArgumentNullException(nameof(ps));

            // ---- uniform discipline (section 4.3) ----
            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;                 // controller owns playback
            main.maxParticles = Mathf.Max(maxParticles, 1);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.useAutoRandomSeed = false;             // deterministic capture
            if (ps.randomSeed != seed)
            {
                bool wasPlaying = ps.isPlaying;
                if (wasPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.randomSeed = seed;
            }
            var tsa = ps.textureSheetAnimation;
            tsa.enabled = false;                      // CP-5: no flipbooks, ever
            var lights = ps.lights;
            lights.enabled = false;                   // local light is the local_light family's job
            var external = ps.externalForces;
            external.enabled = false;                 // no scene force-field assumptions

            switch (id)
            {
                case "cpu_buoyancy_turbulence": BuoyancyTurbulence(ps); break;
                case "cpu_gravity_settle": GravitySettle(ps); break;
                case "cpu_instant_rephase": InstantRephase(ps); break;
                case "cpu_gravity_drag_split": GravityDragSplit(ps); break;
                case "cpu_vortex": Vortex(ps); break;
                case "cpu_orbital_hover": OrbitalHover(ps); break;
                case "cpu_step_grid": StepGrid(ps); break;
                case "cpu_attract_target": AttractTarget(ps); break;
                case "cpu_drift_curl": DriftCurl(ps); break;
                case "cpu_fall_wind": FallWind(ps); break;
                case "cpu_burst_radial": BurstRadial(ps); break;
                case "cpu_strip_trail": StripTrail(ps); break;
                case "cpu_surface_scatter": SurfaceScatter(ps); break;
                case "cpu_mesh_shatter": MeshShatter(ps); break;
                default: throw new ArgumentException("Unknown CPU particle template: " + id);
            }
        }

        /// <summary>Theoretical peak = max(maxParticles, rate*lifetime + bursts) for budget checks (CP-3).</summary>
        public static int TheoreticalPeak(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            ParticleSystem.EmissionModule emission = ps.emission;
            float lifetimeMax = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                ? main.startLifetime.constantMax
                : main.startLifetime.constant;
            float rate = emission.rateOverTime.mode == ParticleSystemCurveMode.TwoConstants
                ? emission.rateOverTime.constantMax
                : emission.rateOverTime.constant;
            int burstTotal = 0;
            int burstCount = emission.burstCount;
            var bursts = new ParticleSystem.Burst[burstCount];
            emission.GetBursts(bursts);
            for (int i = 0; i < burstCount; i++) burstTotal += (int)bursts[i].maxCount;
            return Mathf.Max(1, Mathf.CeilToInt(rate * lifetimeMax) + burstTotal);
        }

        // ---- template bodies -------------------------------------------------

        private static void BuoyancyTurbulence(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.gravityModifier = 0f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.4f)); // buoyancy ramp
            var noise = ps.noise;
            noise.enabled = true;
            noise.frequency = 0.8f;
            noise.strength = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.damping = true;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));
        }

        private static void GravitySettle(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.gravityModifier = 1f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.15f;
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0.2f;
            collision.dampen = 0.6f;
            collision.lifetimeLoss = 0f;
            collision.enableDynamicColliders = false; // no assumptions about user scenes
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
        }

        private static void InstantRephase(ParticleSystem ps)
        {
            // No displacement: high-frequency short-lifetime rebirth approximates
            // the GPU instant-rephase (particles never interpolate anyway).
            ParticleSystem.MainModule main = ps.main;
            float rephaseRate = 15f;
            main.startLifetime = 1f / rephaseRate;
            main.startSpeed = 0f;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 12, 12, 30, 1f / rephaseRate)
            });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.SingleSidedEdge;
            shape.radius = 1f;
        }

        private static void GravityDragSplit(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 1f);
            main.gravityModifier = 1f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.5f; // viscous
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.lifetimeLoss = 1f; // splat on contact
            collision.enableDynamicColliders = false;
        }

        private static void Vortex(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2.4f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = 2.2f;   // tangential
            vel.radial = -0.4f;    // centripetal
            vel.y = 0.8f;          // axial rise
            var noise = ps.noise;
            noise.enabled = true;
            noise.frequency = 1.2f;
            noise.strength = 0.9f;
        }

        private static void OrbitalHover(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 4f);
            main.gravityModifier = 0f;
            main.startSpeed = 0f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = 1.4f;
            vel.radial = 0f;
            var col = ps.colorOverLifetime;
            col.enabled = true; // multi-peak gradient approximates the phase pulse
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.9f;
        }

        private static void StepGrid(ParticleSystem ps)
        {
            // Constant velocity; the quantized step happens in the particle
            // material's vertex stage (PixelSnapSize path), not in modules.
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = 0.9f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = false; // rotation locked to 90-degree steps by the material
        }

        private static void AttractTarget(ParticleSystem ps)
        {
            // Local space + negative radial velocity pulls particles toward the
            // system origin; the controller binds the origin to the target.
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = 0f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.4f;
            shape.radiusThickness = 0f; // shell spawn
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.radial = -1.6f;
            vel.orbitalY = 0.5f; // spiral bias
        }

        private static void DriftCurl(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            main.gravityModifier = 0.02f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var noise = ps.noise;
            noise.enabled = true;
            noise.frequency = 0.3f;
            noise.strength = 0.25f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3f, 2f, 3f);
        }

        private static void FallWind(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = 0f;
            main.gravityModifier = 0.5f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = 0.6f; // constant wind (height gradient lost: honest degrade)
            var noise = ps.noise;
            noise.enabled = true;
            noise.frequency = 0.5f;
            noise.strength = 0.3f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4f, 0.1f, 4f);
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.lifetimeLoss = 1f;
            collision.enableDynamicColliders = false;
        }

        private static void BurstRadial(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.35f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }

        private static void StripTrail(ParticleSystem ps)
        {
            // Cross-family degrade candidate is TrailRenderer; when kept inside
            // the particle family, the Trails module in Ribbon mode is used.
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = 0.2f;
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.Ribbon;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
        }

        private static void SurfaceScatter(ParticleSystem ps)
        {
            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = 0.05f;
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.MeshRenderer; // mesh source: generator output or host reference
            var noise = ps.noise;
            noise.enabled = true;
            noise.frequency = 0.4f;
            noise.strength = 0.05f;
        }

        private static void MeshShatter(ParticleSystem ps)
        {
            // Lightweight shatter; the heavy path is the mesh family's
            // voronoi_prefracture + Rigidbody (cross-family degrade).
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.gravityModifier = 1f;
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.renderMode = ParticleSystemRenderMode.Mesh;
        }
    }
}
