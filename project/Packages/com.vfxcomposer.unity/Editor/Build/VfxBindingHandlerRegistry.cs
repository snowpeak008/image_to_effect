using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Build
{
    /// <summary>Explicit Unity-property bindings. Recipe data is never interpreted as a type or property path.</summary>
    public sealed class VfxBindingHandlerRegistry
    {
        public delegate void Handler(GameObject target, JToken value);
        private readonly Dictionary<string, Handler> handlers = new Dictionary<string, Handler>(StringComparer.Ordinal);

        public VfxBindingHandlerRegistry Register(string key, Handler handler)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A binding key is required.", "key");
            if (handler == null) throw new ArgumentNullException("handler");
            handlers[key] = handler;
            return this;
        }

        public bool Contains(string key) { return !string.IsNullOrEmpty(key) && handlers.ContainsKey(key); }

        public void Apply(string key, GameObject target, JToken value)
        {
            Handler handler;
            if (!handlers.TryGetValue(key ?? string.Empty, out handler)) throw new InvalidOperationException("Binding is not allow-listed: " + key);
            handler(target, value);
        }

        public static VfxBindingHandlerRegistry CreateFormal2D()
        {
            var registry = new VfxBindingHandlerRegistry();
            registry.Register(VfxBindingKeys.CoreScale, (target, value) => target.transform.localScale = Vector3.one * Float(value));
            registry.Register(VfxBindingKeys.EmbersRate, (target, value) => { var emission = Particle(target).emission; emission.rateOverTime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.EmbersLifetime, (target, value) => { var main = Particle(target).main; main.startLifetime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ImpactCount, ApplyImpactCount);
            registry.Register(VfxBindingKeys.ImpactSpeed, (target, value) => { var main = Particle(target).main; main.startSpeed = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.TrailTime, (target, value) => Trail(target).time = Float(value));
            registry.Register(VfxBindingKeys.TrailWidth, (target, value) => Trail(target).widthMultiplier = Float(value));
            registry.Register(VfxBindingKeys.LaunchLifetime, (target, value) => { var main = Particle(target).main; main.startLifetime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.LaunchSize, (target, value) => { var main = Particle(target).main; main.startSize = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ShockwaveLifetime, (target, value) => { var main = Particle(target).main; main.startLifetime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ShockwaveEndSize, ApplyShockwaveEndSize);
            return registry;
        }

        /// <summary>Registers the S10 spatial handlers in addition to the frozen 2D table.</summary>
        public static VfxBindingHandlerRegistry CreateFormal()
        {
            var registry = CreateFormal2D();
            RegisterFormal3D(registry);
            return registry;
        }

        private static void RegisterFormal3D(VfxBindingHandlerRegistry registry)
        {
            // These are separate registrations even where the underlying Unity
            // component happens to be shared.  This keeps a 3D Manifest from
            // accidentally inheriting 2D sorting/binding semantics.
            registry.Register(VfxBindingKeys.ThreeDCoreScale, (target, value) => target.transform.localScale = Vector3.one * Float(value));
            registry.Register(VfxBindingKeys.ThreeDEmbersRate, (target, value) => { var emission = Particle(target).emission; emission.rateOverTime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ThreeDEmbersLifetime, (target, value) => { var main = Particle(target).main; main.startLifetime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ThreeDImpactCount, ApplyImpactCount);
            registry.Register(VfxBindingKeys.ThreeDImpactSpeed, (target, value) => { var main = Particle(target).main; main.startSpeed = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ThreeDTrailTime, (target, value) => Trail(target).time = Float(value));
            registry.Register(VfxBindingKeys.ThreeDTrailWidth, (target, value) => Trail(target).widthMultiplier = Float(value));
            registry.Register(VfxBindingKeys.ThreeDLaunchLifetime, (target, value) => { var main = Particle(target).main; main.startLifetime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ThreeDLaunchSize, (target, value) => { var main = Particle(target).main; main.startSize = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ThreeDShockwaveLifetime, (target, value) => { var main = Particle(target).main; main.startLifetime = new ParticleSystem.MinMaxCurve(Float(value)); });
            registry.Register(VfxBindingKeys.ThreeDShockwaveEndSize, ApplyShockwaveEndSize);
        }

        private static void ApplyImpactCount(GameObject target, JToken value)
        {
            var emission = Particle(target).emission;
            var count = (short)value.Value<int>();
            var curve = new ParticleSystem.MinMaxCurve { mode = ParticleSystemCurveMode.TwoConstants, constantMin = count, constantMax = count };
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, curve) });
        }

        private static void ApplyShockwaveEndSize(GameObject target, JToken value)
        {
            var size = Particle(target).sizeOverLifetime;
            if (!size.enabled) throw new InvalidOperationException("Shockwave template must enable size-over-lifetime.");
            var curve = size.size.curve;
            if (curve == null || curve.length == 0) throw new InvalidOperationException("Shockwave template must have a size curve.");
            var last = curve.length - 1;
            curve.MoveKey(last, new Keyframe(curve.keys[last].time, Float(value)));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static ParticleSystem Particle(GameObject target)
        {
            var component = target.GetComponent<ParticleSystem>();
            if (component == null) throw new InvalidOperationException("Template has no ParticleSystem for this binding.");
            return component;
        }

        private static TrailRenderer Trail(GameObject target)
        {
            var component = target.GetComponent<TrailRenderer>();
            if (component == null) throw new InvalidOperationException("Template has no TrailRenderer for this binding.");
            return component;
        }

        private static float Float(JToken token) { return Convert.ToSingle(((JValue)token).Value, System.Globalization.CultureInfo.InvariantCulture); }
    }
}
