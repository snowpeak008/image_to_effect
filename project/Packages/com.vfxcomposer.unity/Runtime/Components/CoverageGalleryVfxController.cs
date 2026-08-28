using UnityEngine;
using UnityEngine.UI;

namespace VFXComposer
{
    public enum CoverageGalleryProfile
    {
        Impact3D, Aura3D, Area3D, Beam3D, Trail3D, Shield3D, Spawn3D, Environment, ScreenUi
    }

    /// <summary>Player-safe Runtime Entry used by the second coverage gallery.</summary>
    [DisallowMultipleComponent]
    public sealed class CoverageGalleryVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private CoverageGalleryProfile profile;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];
        [SerializeField] private Transform[] animatedTransforms = new Transform[0];
        [SerializeField] private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Canvas screenCanvas;
        [SerializeField] private Graphic[] graphics = new Graphic[0];
        [SerializeField] private float[] shapeModes = new float[0];
        [SerializeField] private float[] intensities = new float[0];
        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;
        [SerializeField] private bool sustained;
        [SerializeField, Min(.2f)] private float duration = 2f;
        [SerializeField, Min(.05f)] private float stopDuration = .35f;

        private MaterialPropertyBlock block;
        private Vector3[] initialPositions;
        private Vector3[] initialScales;
        private Quaternion[] initialRotations;
        private bool captured;
        private bool playing;
        private bool stopping;
        private float elapsed;
        private float stopElapsed;
        private float pulseAge = 99f;

        public CoverageGalleryProfile Profile { get { return profile; } }
        public bool Sustained { get { return sustained; } }
        public bool IsAlive { get { return playing || stopping; } }
        private MaterialPropertyBlock Block { get { if (block == null) block = new MaterialPropertyBlock(); return block; } }

        private void Awake() { Capture(); ResetForPool(); }

        private void Update()
        {
            if (!playing && !stopping) return;
            var delta = Mathf.Max(0f, Time.deltaTime); pulseAge += delta;
            if (playing)
            {
                elapsed += delta;
                if (!sustained && elapsed >= duration) BeginStop();
            }
            if (stopping)
            {
                stopElapsed += delta;
                if (stopElapsed >= stopDuration) { ResetForPool(); return; }
            }
            ApplyVisuals();
        }

        public void Initialize(VfxRuntimeContext context) { ResetForPool(); transform.SetPositionAndRotation(context.Position, context.Rotation); }

        public void Play()
        {
            Capture(); RestoreTransforms(); playing = true; stopping = false; elapsed = 0f; stopElapsed = 0f; pulseAge = profile == CoverageGalleryProfile.ScreenUi ? 0f : 99f;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = true;
            foreach (var graphic in graphics) if (graphic != null) graphic.enabled = true;
            if (screenCanvas != null) { screenCanvas.renderMode = RenderMode.ScreenSpaceCamera; screenCanvas.enabled = true; if (screenCanvas.worldCamera == null) screenCanvas.worldCamera = Camera.main; }
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            foreach (var particle in particles) if (particle != null) { ConfigureParticle(particle); particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(true); }
            ApplyVisuals();
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start") { transform.SetPositionAndRotation(payload.Position, payload.Rotation); Play(); return true; }
            if (eventId == "tick" || eventId == "hit" || eventId == "refresh") { if (!playing) return false; pulseAge = 0f; return true; }
            if (eventId == "break" || eventId == "stop") { Stop(VfxStopMode.AllowTail); return true; }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate) { ResetForPool(); return; }
            BeginStop();
        }

        public void ResetForPool()
        {
            Capture(); RestoreTransforms(); playing = false; stopping = false; elapsed = 0f; stopElapsed = 0f; pulseAge = 99f;
            if (trail != null) { trail.emitting = false; trail.Clear(); }
            foreach (var particle in particles) if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyProperties(0f, 0f, 0f);
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
            foreach (var graphic in graphics) if (graphic != null) graphic.enabled = false;
            if (screenCanvas != null) screenCanvas.enabled = false;
        }

        private void BeginStop()
        {
            if (!playing && !stopping) return;
            playing = false; stopping = true; stopElapsed = 0f;
            if (trail != null) trail.emitting = false;
            foreach (var particle in particles) if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ConfigureParticle(ParticleSystem particle)
        {
            var main=particle.main;var emission=particle.emission;var shape=particle.shape;var velocity=particle.velocityOverLifetime;
            var burst=profile==CoverageGalleryProfile.Impact3D||profile==CoverageGalleryProfile.Spawn3D;var weather=profile==CoverageGalleryProfile.Environment;
            main.loop=!burst;main.duration=2f;main.maxParticles=weather?72:(burst?28:48);main.startLifetime=weather?new ParticleSystem.MinMaxCurve(.75f,1.45f):(burst?new ParticleSystem.MinMaxCurve(.28f,.58f):new ParticleSystem.MinMaxCurve(.55f,1.2f));main.startSize=weather?new ParticleSystem.MinMaxCurve(.045f,.085f):(burst?new ParticleSystem.MinMaxCurve(.1f,.2f):new ParticleSystem.MinMaxCurve(.055f,.12f));main.startSpeed=weather?new ParticleSystem.MinMaxCurve(.28f,.55f):new ParticleSystem.MinMaxCurve(.08f,.42f);
            emission.rateOverTime=weather?38f:(burst?0f:12f);emission.SetBursts(burst?new[]{new ParticleSystem.Burst(0,(short)20)}:new ParticleSystem.Burst[0]);shape.enabled=true;shape.shapeType=weather?ParticleSystemShapeType.Box:(burst?ParticleSystemShapeType.Sphere:ParticleSystemShapeType.Circle);shape.scale=weather?new Vector3(1.5f,.9f,.45f):new Vector3(.8f,.8f,.2f);if(!weather){shape.radius=burst?.32f:.65f;shape.radiusThickness=burst?1f:.2f;}
            velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;if(weather){velocity.x=new ParticleSystem.MinMaxCurve(0f,0f);velocity.y=new ParticleSystem.MinMaxCurve(-.6f,-.28f);velocity.z=new ParticleSystem.MinMaxCurve(0f,0f);}else{velocity.x=new ParticleSystem.MinMaxCurve(-.16f,.16f);velocity.y=new ParticleSystem.MinMaxCurve(.04f,.3f);velocity.z=new ParticleSystem.MinMaxCurve(-.12f,.12f);}
        }

        private void ApplyVisuals()
        {
            var alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .22f));
            if(profile==CoverageGalleryProfile.ScreenUi)alpha=pulseAge<.9f?Mathf.Sin(Mathf.PI*Mathf.Clamp01(pulseAge/.9f)):0f;
            if (!sustained) alpha *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - duration + stopDuration) / stopDuration));
            if (stopping) alpha *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stopElapsed / stopDuration));
            var progress = sustained ? Mathf.Repeat(elapsed, Mathf.Max(.2f, duration)) / Mathf.Max(.2f, duration) : Mathf.Clamp01(elapsed / Mathf.Max(.2f, duration));
            var pulse = pulseAge < .65f ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulseAge / .65f)) : 0f;
            ApplyProperties(alpha, progress, pulse); Animate(progress, pulse);
        }

        private void ApplyProperties(float alpha, float progress, float pulse)
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i]; if (renderer == null) continue;
                renderer.GetPropertyBlock(Block); Block.SetColor("_PrimaryColor", primaryColor); Block.SetColor("_SecondaryColor", secondaryColor);
                Block.SetFloat("_GlobalAlpha", alpha); Block.SetFloat("_RuntimeTime", elapsed); Block.SetFloat("_Progress", progress); Block.SetFloat("_Pulse", pulse);
                Block.SetFloat("_ShapeMode", i < shapeModes.Length ? shapeModes[i] : 0f); Block.SetFloat("_Intensity", i < intensities.Length ? intensities[i] : 1f); renderer.SetPropertyBlock(Block);
            }
            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i]; if (graphic == null) continue;var vignette=graphic.name=="DamageVignette";var baseColor=vignette?primaryColor:(i%2==0?secondaryColor:primaryColor);baseColor.a*=alpha*(vignette?.58f:.9f);graphic.color=baseColor;
            }
        }

        private void Animate(float progress, float pulse)
        {
            var cycle = progress * Mathf.PI * 2f;
            if (profile == CoverageGalleryProfile.Impact3D)
            {
                var expansion = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 4.2f)); var settle = 1f - .18f * Mathf.SmoothStep(.55f, 1f, progress);
                for (var i = 0; i < animatedTransforms.Length; i++) { animatedTransforms[i].localScale = initialScales[i] * Mathf.Lerp(.05f, 1.5f + i * .16f, expansion) * settle; animatedTransforms[i].localRotation = initialRotations[i] * Quaternion.Euler(0, elapsed * (18f + i * 11f), elapsed * (28f - i * 8f)); }
            }
            else if (profile == CoverageGalleryProfile.Aura3D)
            {
                for(var i=0;i<animatedTransforms.Length;i++){var sign=i%2==0?1f:-1f;animatedTransforms[i].localRotation=initialRotations[i]*Quaternion.Euler(6f*Mathf.Sin(cycle+i),sign*elapsed*(18+i*6),sign*elapsed*(24+i*7));animatedTransforms[i].localScale=initialScales[i]*(1f+.07f*Mathf.Sin(cycle+i)+pulse*.14f);}
            }
            else if (profile == CoverageGalleryProfile.Area3D)
            {
                for(var i=0;i<animatedTransforms.Length;i++){var sign=i%2==0?1f:-1f;animatedTransforms[i].localRotation=initialRotations[i]*Quaternion.Euler(0,0,sign*elapsed*(16+i*9));animatedTransforms[i].localScale=initialScales[i]*(1f+.045f*Mathf.Sin(cycle+i)+pulse*.12f);}
            }
            else if (profile == CoverageGalleryProfile.Shield3D)
            {
                for(var i=0;i<animatedTransforms.Length;i++){var sign=i%2==0?1f:-1f;animatedTransforms[i].localRotation=initialRotations[i]*Quaternion.Euler(sign*elapsed*(8+i*3),sign*elapsed*(18+i*6),sign*elapsed*(10+i*4));animatedTransforms[i].localScale=initialScales[i]*(1f+.025f*Mathf.Sin(cycle+i)+pulse*.18f);}
            }
            else if (profile == CoverageGalleryProfile.Spawn3D && animatedTransforms.Length > 0)
            {
                var rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress * 1.7f)); animatedTransforms[0].localPosition = initialPositions[0] + Vector3.up * (.58f * rise);animatedTransforms[0].localScale=initialScales[0]*(.35f+.8f*rise);
                for(var i=1;i<animatedTransforms.Length;i++){var sign=i%2==0?1f:-1f;animatedTransforms[i].localRotation=initialRotations[i]*Quaternion.Euler(0,0,sign*elapsed*(26+i*8));animatedTransforms[i].localScale=initialScales[i]*(1f+.06f*Mathf.Sin(cycle+i));}
            }
            else if (profile == CoverageGalleryProfile.Trail3D && animatedTransforms.Length > 0)
            {
                animatedTransforms[0].localPosition = initialPositions[0] + new Vector3(Mathf.Sin(cycle) * 1.02f, Mathf.Cos(cycle * 2f) * .26f, Mathf.Cos(cycle) * .42f);
            }
            else if(profile==CoverageGalleryProfile.Environment)
            {
                for(var i=0;i<animatedTransforms.Length;i++){animatedTransforms[i].localPosition=initialPositions[i]+new Vector3(Mathf.Sin(cycle+i)*.16f,Mathf.Cos(cycle*.6f+i)*.06f,0);animatedTransforms[i].localScale=initialScales[i]*(1f+.08f*Mathf.Sin(cycle*.7f+i));}
            }
            else if(profile==CoverageGalleryProfile.ScreenUi)
            {
                for(var i=0;i<animatedTransforms.Length;i++){animatedTransforms[i].localRotation=initialRotations[i];animatedTransforms[i].localScale=initialScales[i]*(1f+.06f*Mathf.Sin(cycle+i)+pulse*.14f);}
            }
            UpdateLines(cycle);
        }

        private void UpdateLines(float cycle)
        {
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex]; if (line == null) continue; const int count = 24; line.positionCount = count;
                for (var i = 0; i < count; i++)
                {
                    var t = i / (count - 1f); var x = Mathf.Lerp(-.92f, .92f, t); var envelope = Mathf.Sin(Mathf.PI * t); var y = Mathf.Sin(t * 16f + cycle * 3f + lineIndex * 1.7f) * (.055f + lineIndex * .025f) * envelope; var z = Mathf.Cos(t * 11f - cycle * 2f + lineIndex) * (.06f + lineIndex * .02f) * envelope; line.SetPosition(i, new Vector3(x, y, z));
                }
            }
        }

        private void Capture()
        {
            if (captured) return; captured = true; initialPositions = new Vector3[animatedTransforms.Length]; initialScales = new Vector3[animatedTransforms.Length]; initialRotations = new Quaternion[animatedTransforms.Length];
            for (var i = 0; i < animatedTransforms.Length; i++) { var target = animatedTransforms[i]; initialPositions[i] = target == null ? Vector3.zero : target.localPosition; initialScales[i] = target == null ? Vector3.one : target.localScale; initialRotations[i] = target == null ? Quaternion.identity : target.localRotation; }
        }

        private void RestoreTransforms()
        {
            if (!captured) return; for (var i = 0; i < animatedTransforms.Length; i++) { var target = animatedTransforms[i]; if (target == null) continue; target.localPosition = initialPositions[i]; target.localScale = initialScales[i]; target.localRotation = initialRotations[i]; }
        }
    }
}
