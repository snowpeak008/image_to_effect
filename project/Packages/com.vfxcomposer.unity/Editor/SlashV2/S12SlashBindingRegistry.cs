using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VFXComposer.Editor.SlashV2
{
    /// <summary>Closed S12 template bindings. These names are symbols, never caller-provided Unity property paths.</summary>
    public sealed class S12SlashBindingRegistry
    {
        public delegate void Handler(GameObject target, JToken value);
        private readonly System.Collections.Generic.Dictionary<string, Handler> handlers = new System.Collections.Generic.Dictionary<string, Handler>(StringComparer.Ordinal);
        public bool Contains(string key) { return !string.IsNullOrEmpty(key) && handlers.ContainsKey(key); }
        public void Apply(string key, GameObject target, JToken value) { Handler handler; if (!handlers.TryGetValue(key ?? string.Empty, out handler)) throw new InvalidOperationException("S12 binding is not allow-listed: " + key); handler(target, value); }

        public static S12SlashBindingRegistry CreateFormal()
        {
            var registry = new S12SlashBindingRegistry();
            registry.Register("3d.slash.arc.scale", (target, value) => target.transform.localScale = Vector3.one * Float(value));
            registry.Register("3d.slash.arc.width", (target, value) => Find(target, "RibbonWidthControl").localScale = Vector3.one * (Float(value) / .24f));
            registry.Register("3d.slash.arc.duration", (target, value) => { var main = Particle(target, "PrimarySweepRunner").main; main.startLifetime = Float(value); });
            registry.Register("3d.slash.afterimage.count", (target, value) => Find(target, "EchoB").gameObject.SetActive(Int(value) >= 2));
            registry.Register("3d.slash.afterimage.alpha", (target, value) => { foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) { var material = renderer.sharedMaterial; var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color; color.a = Float(value); var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); if (material.HasProperty("_BaseColor")) block.SetColor("_BaseColor", color); else block.SetColor("_Color", color); renderer.SetPropertyBlock(block); } });
            registry.Register("3d.slash.sparks.count", (target, value) => Burst(Particle(target, null), Int(value)));
            registry.Register("3d.slash.sparks.speed", (target, value) => { var main = Particle(target, null).main; main.startSpeed = Float(value); });
            registry.Register("3d.slash.sparks.lifetime", (target, value) => { var main = Particle(target, null).main; main.startLifetime = Float(value); });
            registry.Register("3d.slash.dissipation.lifetime", (target, value) => { var main = Particle(target, null).main; main.startLifetime = Float(value); });
            return registry;
        }

        private void Register(string key, Handler handler) { handlers.Add(key, handler); }
        private static Transform Find(GameObject target, string child) { var value = target.transform.Find(child); if (value == null) throw new InvalidOperationException("S12 template is missing reviewed binding target: " + child); return value; }
        private static ParticleSystem Particle(GameObject target, string child) { var root = string.IsNullOrEmpty(child) ? target.transform : Find(target, child); var particle = root.GetComponent<ParticleSystem>(); if (particle == null) particle = root.GetComponentInChildren<ParticleSystem>(true); if (particle == null) throw new InvalidOperationException("S12 template has no reviewed ParticleSystem binding target."); return particle; }
        private static void Burst(ParticleSystem particle, int count) { var emission = particle.emission; var curve = new ParticleSystem.MinMaxCurve { mode = ParticleSystemCurveMode.Constant, constant = count }; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, curve) }); }
        private static float Float(JToken value) { return Convert.ToSingle(((JValue)value).Value, System.Globalization.CultureInfo.InvariantCulture); }
        private static int Int(JToken value) { return Convert.ToInt32(((JValue)value).Value, System.Globalization.CultureInfo.InvariantCulture); }
    }
}
