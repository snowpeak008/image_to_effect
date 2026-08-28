using UnityEngine;
using VFXComposer.Capabilities;

namespace VFXComposer
{
    public enum StyledVfxProfile
    {
        Projectile, Impact, Slash, Aura, Area, Beam, Trail, Shield, Spawn, Summon,
        Transform, Environment, ScreenUi, Composite, Decal, WeaponTrail, Destruction,
        DeathRebirth, Teleport, Loot
    }

    public enum StyledVfxLifecycle { OneShot, Sustained, EventDriven }
    public enum PortalEndpointRole { Entry, Exit }
    public enum ElementContentFamily { Neutral, Fire, Frost, Lightning, Water, Wind, Earth, Nature, Toxic, Holy, Shadow, Arcane }

    /// <summary>Player-safe common lifecycle/phase driver used by planned content entries.</summary>
    [DisallowMultipleComponent]
    public sealed class StyledVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private StyledVfxProfile profile;
        [SerializeField] private StyledVfxLifecycle lifecycle;
        [SerializeField, Min(.05f)] private float duration = 1f;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private Transform[] animatedTransforms = new Transform[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];
        [SerializeField] private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField] private TrailRenderer[] trails = new TrailRenderer[0];
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;
        [SerializeField] private float styleMode;
        [SerializeField] private string styleToken = "stylized";
        [SerializeField] private string[] styleKeys = new string[0];
        [SerializeField] private float[] styleValues = new float[0];
        [SerializeField] private string[] styleTextKeys = new string[0];
        [SerializeField] private string[] styleTextValues = new string[0];
        [SerializeField] private float intensity = 1f;
        [SerializeField] private uint seed = 1;
        [Header("Content and behavior protocol")]
        [SerializeField] private ElementContentFamily contentFamily;
        [SerializeField] private string[] contentKeys = new string[0];
        [SerializeField] private float[] contentValues = new float[0];
        [SerializeField] private string[] contentTextKeys = new string[0];
        [SerializeField] private string[] contentTextValues = new string[0];
        [SerializeField] private bool behaviorEnabled;
        [SerializeField] private string motionType = "stationary";
        [SerializeField] private string hitType = "single";
        [SerializeField] private string emissionType = "single";
        [SerializeField] private string timingType = "instant";
        [SerializeField] private string[] motionKeys = new string[0];
        [SerializeField] private float[] motionValues = new float[0];
        [SerializeField] private string[] hitKeys = new string[0];
        [SerializeField] private float[] hitValues = new float[0];
        [SerializeField] private string[] emissionKeys = new string[0];
        [SerializeField] private float[] emissionValues = new float[0];
        [SerializeField] private string[] timingKeys = new string[0];
        [SerializeField] private float[] timingValues = new float[0];
        [Header("Archetype protocol")]
        [SerializeField] private int stackLimit = 4;
        [SerializeField] private int historyPoints = 12;
        [SerializeField] private int pieceCount = 10;
        [SerializeField] private int rarity = 1;
        [SerializeField] private float speedThreshold = .4f;
        [SerializeField] private float fadeTime = .15f;
        [SerializeField] private float explodeForce = 2.4f;
        [SerializeField] private float debrisLifetime = 1.2f;
        [SerializeField] private float portalRadius = 1f;
        [SerializeField] private float swirlSpeed = 2f;
        [SerializeField] private float pickupSpeed = 6f;
        [SerializeField] private string pairId = "default_pair";
        [SerializeField] private string lifecycleDirection = "up";

        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private MaterialPropertyBlock block;
        private float elapsed;
        private bool playing;
        private Renderer[] externalRenderers = new Renderer[0];
        private Vector3 pickupTarget;
        private Vector3 destructionImpulse;
        private Vector3 lastWeaponTip;
        private bool hasWeaponTip;
        private bool pickingUp;
        private float weaponIdle;
        private PortalEndpointRole portalRole;
        private CapabilitySampleTrace behaviorTrace;
        private Vector3 behaviorOrigin;
        private bool hasBehaviorOrigin;
        private float lastAppliedStylePhase;
        private float lastGlitchOffset;
        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<StyledVfxController>> DecalStacks = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<StyledVfxController>>(System.StringComparer.Ordinal);

        public bool IsAlive { get { return playing; } }
        public StyledVfxProfile Profile { get { return profile; } }
        public StyledVfxLifecycle Lifecycle { get { return lifecycle; } }
        public float NormalizedTime { get { return duration <= 0 ? 0 : Mathf.Clamp01(elapsed / duration); } }
        public int Rarity { get { return rarity; } }
        public string PairId { get { return pairId; } }
        public PortalEndpointRole PortalRole { get { return portalRole; } }
        public int HistoryPoints { get { return historyPoints; } }
        public int PieceCount { get { return pieceCount; } }
        public ElementContentFamily ContentFamily { get { return contentFamily; } }
        public string MotionType { get { return motionType; } }
        public string HitType { get { return hitType; } }
        public string EmissionType { get { return emissionType; } }
        public string TimingType { get { return timingType; } }
        public CapabilitySampleTrace BehaviorTrace { get { return behaviorTrace; } }
        public string StyleToken { get { return styleToken; } }
        public float LastAppliedStylePhase { get { return lastAppliedStylePhase; } }
        public float LastGlitchOffset { get { return lastGlitchOffset; } }

        private void Awake()
        {
            CacheBases();
            ResetForPool();
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (profile == StyledVfxProfile.Loot && pickingUp)
            {
                transform.position = Vector3.MoveTowards(transform.position, pickupTarget, pickupSpeed * Mathf.Max(0f, Time.deltaTime));
                if ((transform.position - pickupTarget).sqrMagnitude <= .0001f) { transform.position = pickupTarget; pickingUp = false; Stop(VfxStopMode.Immediate); return; }
            }
            ApplyFrame(elapsed);
            if (lifecycle == StyledVfxLifecycle.OneShot && elapsed >= duration) Stop(VfxStopMode.AllowTail);
            else if (lifecycle == StyledVfxLifecycle.Sustained && duration > 0 && elapsed >= duration) elapsed -= duration;
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            if (basePositions.Length != animatedTransforms.Length) CacheBases();
            elapsed = 0f;
            playing = true;
            behaviorOrigin = transform.localPosition; hasBehaviorOrigin = true;
            behaviorTrace = behaviorEnabled ? CapabilitySampler.SampleTrajectory(BuildBehaviorRequest()) : null;
            for (var index = 0; index < renderers.Length; index++) if (renderers[index] != null) renderers[index].enabled = true;
            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index]; if (particle == null) continue;
                particle.useAutoRandomSeed = false; particle.randomSeed = seed + (uint)index; particle.Play(true);
            }
            for (var index = 0; index < trails.Length; index++) if (trails[index] != null) { trails[index].Clear(); trails[index].enabled = true; trails[index].emitting = true; }
            ApplyFrame(0f);
        }

        /// <summary>Places a Decal on a surface and enforces the per-surface oldest-first stack limit.</summary>
        public void RegisterDecalHit(string surfaceKey, Vector3 point, Vector3 normal)
        {
            if (profile != StyledVfxProfile.Decal) return;
            if (normal.sqrMagnitude < .0001f) normal = Vector3.forward;
            transform.position = point + normal.normalized * .002f;
            transform.rotation = Quaternion.FromToRotation(Vector3.forward, normal.normalized);
            var key = string.IsNullOrEmpty(surfaceKey) ? "default" : surfaceKey;
            System.Collections.Generic.List<StyledVfxController> stack;
            if (!DecalStacks.TryGetValue(key, out stack)) { stack = new System.Collections.Generic.List<StyledVfxController>(); DecalStacks.Add(key, stack); }
            stack.RemoveAll(value => value == null || !value.IsAlive);
            while (stack.Count >= Mathf.Max(1, stackLimit)) { var oldest = stack[0]; stack.RemoveAt(0); if (oldest != null) oldest.Stop(VfxStopMode.Immediate); }
            stack.Add(this); Play();
        }

        /// <summary>External weapon animation supplies blade root/tip; visibility follows measured tip speed.</summary>
        public void SetWeaponEndpoints(Vector3 bladeRoot, Vector3 bladeTip, float deltaTime)
        {
            if (profile != StyledVfxProfile.WeaponTrail) return;
            var delta = bladeTip - bladeRoot; var length = delta.magnitude;
            transform.position = (bladeRoot + bladeTip) * .5f;
            if (length > .0001f) transform.rotation = Quaternion.FromToRotation(Vector3.right, delta / length);
            if (animatedTransforms.Length > 0 && animatedTransforms[0] != null) animatedTransforms[0].localScale = new Vector3(Mathf.Max(.001f, length), 1f, 1f);
            if (lines.Length > 0 && lines[0] != null) { lines[0].positionCount = 2; lines[0].SetPosition(0, bladeRoot); lines[0].SetPosition(1, bladeTip); }
            var speed = hasWeaponTip && deltaTime > .00001f ? Vector3.Distance(lastWeaponTip, bladeTip) / deltaTime : 0f;
            lastWeaponTip = bladeTip; hasWeaponTip = true;
            if (speed >= speedThreshold) { weaponIdle = 0f; if (!playing) Play(); else SetVisible(true); }
            else { weaponIdle += Mathf.Max(0f, deltaTime); if (weaponIdle >= fadeTime) SetVisible(false); }
        }

        public void TriggerDestruction(Vector3 impulse) { if (profile == StyledVfxProfile.Destruction) { destructionImpulse = impulse; Play(); } }
        public void BindExternalRenderers(Renderer[] targets) { externalRenderers = targets ?? new Renderer[0]; }
        public void ConfigurePortal(string id, PortalEndpointRole role) { if (profile == StyledVfxProfile.Teleport) { pairId = string.IsNullOrEmpty(id) ? "default_pair" : id; portalRole = role; } }
        public void TriggerTraverse() { if (profile == StyledVfxProfile.Teleport) Play(); }
        public void SetPickupTarget(Vector3 target) { pickupTarget = target; }
        public void BeginPickup() { if (profile == StyledVfxProfile.Loot) { pickingUp = true; Play(); } }
        public void SetRarity(int value)
        {
            rarity = Mathf.Clamp(value, 1, 5);
            var colors = new[] { Color.white, new Color(.32f, 1f, .4f), new Color(.25f, .55f, 1f), new Color(.75f, .3f, 1f), new Color(1f, .58f, .08f) };
            primary = colors[rarity - 1]; secondary = Color.Lerp(primary, Color.white, .45f); intensity = .8f + rarity * .18f;
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "launch" || eventId == "trigger")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation); Play(); return true;
            }
            if (eventId == "stop" || eventId == "cancel") { Stop(VfxStopMode.AllowTail); return true; }
            if (eventId == "reset") { ResetForPool(); return true; }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            playing = false; pickingUp = false;
            for (var index = 0; index < particles.Length; index++) if (particles[index] != null) particles[index].Stop(true, mode == VfxStopMode.Immediate ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            for (var index = 0; index < trails.Length; index++) if (trails[index] != null) { trails[index].emitting = false; if (mode == VfxStopMode.Immediate) trails[index].Clear(); trails[index].enabled = false; }
            SetVisible(false);
            ApplyExternalLifecycle(1f);
        }

        public void ResetForPool()
        {
            playing = false; elapsed = 0f; pickingUp = false; weaponIdle = 0f; hasWeaponTip = false;lastAppliedStylePhase=0f;lastGlitchOffset=0f;
            behaviorTrace = null;if(hasBehaviorOrigin)transform.localPosition=behaviorOrigin;
            for (var index = 0; index < animatedTransforms.Length; index++) if (animatedTransforms[index] != null && index < basePositions.Length)
            {
                animatedTransforms[index].localPosition = basePositions[index];
                animatedTransforms[index].localRotation = baseRotations[index];
                animatedTransforms[index].localScale = baseScales[index];
            }
            for (var index = 0; index < particles.Length; index++) if (particles[index] != null) particles[index].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (var index = 0; index < trails.Length; index++) if (trails[index] != null) { trails[index].emitting = false; trails[index].Clear(); trails[index].enabled = false; }
            SetVisible(false);
            ApplyExternalLifecycle(profile == StyledVfxProfile.DeathRebirth && lifecycleDirection == "down" ? 0f : 1f);
        }

        private void CacheBases()
        {
            basePositions = new Vector3[animatedTransforms.Length]; baseRotations = new Quaternion[animatedTransforms.Length]; baseScales = new Vector3[animatedTransforms.Length];
            for (var index = 0; index < animatedTransforms.Length; index++) if (animatedTransforms[index] != null)
            {
                basePositions[index] = animatedTransforms[index].localPosition;
                baseRotations[index] = animatedTransforms[index].localRotation;
                baseScales[index] = animatedTransforms[index].localScale;
            }
        }

        private void ApplyFrame(float time)
        {
            var normalized = lifecycle == StyledVfxLifecycle.Sustained ? Mathf.Repeat(time / Mathf.Max(.05f, duration), 1f) : Mathf.Clamp01(time / Mathf.Max(.05f, duration));
            var envelope = lifecycle == StyledVfxLifecycle.OneShot ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .12f, normalized)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, normalized))) : 1f;
            var stylePhase=normalized;if(styleToken=="pixel"){var fps=Mathf.Max(1f,GetStyleNumber("snap_fps",12));stylePhase=Mathf.Clamp01((Mathf.Floor(time*fps)/fps)/Mathf.Max(.05f,duration));}else if(styleToken=="cartoon"){var fps=Mathf.Max(1f,GetStyleNumber("atlas_fps",24));stylePhase=Mathf.Clamp01((Mathf.Floor(time*fps)/fps)/Mathf.Max(.05f,duration));}else if(styleToken=="steampunk"){var fps=Mathf.Max(1f,GetStyleNumber("step_fps",8));stylePhase=Mathf.Clamp01((Mathf.Floor(time*fps)/fps)/Mathf.Max(.05f,duration));}lastAppliedStylePhase=stylePhase;
            if(styleToken=="holo"){var rate=Mathf.Max(.01f,GetStyleNumber("glitch_rate",5));var step=Mathf.FloorToInt(time*rate);unchecked{var h=seed^(uint)(step*374761393);h=(h^(h>>13))*1274126177u;lastGlitchOffset=((h&1023)/1023f*2f-1f)*GetStyleNumber("glitch_offset",.08f);}}else lastGlitchOffset=0f;
            if(styleToken=="ghost")envelope*=.62f+.38f*Mathf.Sin(time*Mathf.PI*2*Mathf.Max(.1f,GetStyleNumber("ghost_pulse_fps",1.5f)))*.5f+.19f;
            if (block == null) block = new MaterialPropertyBlock();
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index]; if (renderer == null) continue;
                renderer.GetPropertyBlock(block); block.SetColor("_PrimaryColor", primary); block.SetColor("_SecondaryColor", secondary); block.SetColor("_AccentColor", accent); block.SetFloat("_StyleMode", styleMode); block.SetFloat("_Intensity", intensity * (1f + .08f * Mathf.Sin(time * 8f + index))); block.SetFloat("_GlobalAlpha", envelope); block.SetFloat("_Phase", stylePhase);block.SetFloat("_GlitchOffset",lastGlitchOffset); renderer.SetPropertyBlock(block);
            }
            ApplyExternalLifecycle(normalized);
            for (var index = 0; index < animatedTransforms.Length; index++)
            {
                var item = animatedTransforms[index]; if (item == null || index >= basePositions.Length) continue;
                var direction = (index & 1) == 0 ? 1f : -1f;
                item.localRotation = baseRotations[index] * Quaternion.Euler(0f, profile == StyledVfxProfile.Area || profile == StyledVfxProfile.Spawn || profile == StyledVfxProfile.Summon ? 0f : direction * time * (18f + index * 5f), direction * time * (profile == StyledVfxProfile.Area ? 22f : 40f));
                var pulse = 1f + envelope * (.035f + index * .008f) * Mathf.Sin(time * (5f + index));
                item.localScale = baseScales[index] * Mathf.Max(.01f, pulse);
                item.localPosition = basePositions[index] + MotionOffset(index, time, normalized);
            }
            for (var index = 0; index < lines.Length; index++) if (lines[index] != null && lines[index].positionCount >= 2)
            {
                var count = lines[index].positionCount; for (var point = 0; point < count; point++) { var t = point / (float)(count - 1); lines[index].SetPosition(point, new Vector3(Mathf.Lerp(-1f, 1f, t), Mathf.Sin(t * 12f + time * 7f + index) * .05f, 0f)); }
            }
            ApplyBehavior(time);
        }

        private Vector3 MotionOffset(int index, float time, float normalized)
        {
            if (profile == StyledVfxProfile.Projectile || profile == StyledVfxProfile.Trail || profile == StyledVfxProfile.WeaponTrail) return new Vector3(Mathf.Lerp(-.8f, .8f, normalized), .08f * Mathf.Sin(time * 9f + index), 0f);
            if (profile == StyledVfxProfile.Spawn || profile == StyledVfxProfile.Summon || profile == StyledVfxProfile.Teleport || profile == StyledVfxProfile.DeathRebirth) return Vector3.up * (normalized * .3f * (index == 0 ? 1f : .25f));
            if (profile == StyledVfxProfile.Destruction)
            {
                var angle = (seed * .6180339f + index * 2.399963f) % (Mathf.PI * 2f);
                var radial = new Vector3(Mathf.Cos(angle), .55f + .1f * (index % 3), Mathf.Sin(angle));
                var velocity = radial * explodeForce + destructionImpulse * .2f;
                var age = Mathf.Min(time, Mathf.Max(.05f, debrisLifetime));
                var y = velocity.y * age - 4.9f * age * age;
                if (y < 0f) y = -y * .35f;
                return new Vector3(velocity.x * age, y, velocity.z * age);
            }
            if (profile == StyledVfxProfile.Environment) return Vector3.right * Mathf.Sin(time * .7f + index) * .12f;
            return Vector3.zero;
        }

        private void ApplyExternalLifecycle(float normalized)
        {
            if (profile != StyledVfxProfile.DeathRebirth || externalRenderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            var phase = lifecycleDirection == "down" ? 1f - normalized : normalized;
            for (var index = 0; index < externalRenderers.Length; index++)
            {
                var target = externalRenderers[index]; if (target == null) continue;
                target.GetPropertyBlock(block); block.SetFloat("_Dissolve", Mathf.Clamp01(phase)); block.SetColor("_DissolveEdgeColor", accent); target.SetPropertyBlock(block);
            }
        }

        private void SetVisible(bool value)
        {
            for (var index = 0; index < renderers.Length; index++) if (renderers[index] != null) renderers[index].enabled = value;
        }

        public float GetContentNumber(string key,float fallback=0f)
        {
            var count=Mathf.Min(contentKeys==null?0:contentKeys.Length,contentValues==null?0:contentValues.Length);for(var i=0;i<count;i++)if(contentKeys[i]==key)return contentValues[i];return fallback;
        }

        public string GetContentText(string key,string fallback="")
        {
            var count=Mathf.Min(contentTextKeys==null?0:contentTextKeys.Length,contentTextValues==null?0:contentTextValues.Length);for(var i=0;i<count;i++)if(contentTextKeys[i]==key)return contentTextValues[i];return fallback;
        }

        public float GetStyleNumber(string key,float fallback=0f){var count=Mathf.Min(styleKeys==null?0:styleKeys.Length,styleValues==null?0:styleValues.Length);for(var i=0;i<count;i++)if(styleKeys[i]==key)return styleValues[i];return fallback;}
        public string GetStyleText(string key,string fallback=""){var count=Mathf.Min(styleTextKeys==null?0:styleTextKeys.Length,styleTextValues==null?0:styleTextValues.Length);for(var i=0;i<count;i++)if(styleTextKeys[i]==key)return styleTextValues[i];return fallback;}

        private CapabilitySampleRequest BuildBehaviorRequest()
        {
            var request=new CapabilitySampleRequest{MotionType=motionType,HitType=hitType,EmissionType=emissionType,TimingType=timingType,Origin=Vector3.zero,Direction=Vector3.right,Target=new Vector3(4f,.4f,0f),Duration=Mathf.Max(.1f,duration),DeltaTime=1f/60f,Seed=seed,CollisionMin=new Vector3(-3f,-.8f,-1f),CollisionMax=new Vector3(3f,.8f,1f)};
            CopyParameters(motionKeys,motionValues,request.Motion);CopyParameters(hitKeys,hitValues,request.Hit);CopyParameters(emissionKeys,emissionValues,request.Emission);CopyParameters(timingKeys,timingValues,request.Timing);return request;
        }

        private void ApplyBehavior(float time)
        {
            if(!behaviorEnabled||behaviorTrace==null||behaviorTrace.Frames.Count==0)return;var sampleTime=lifecycle==StyledVfxLifecycle.Sustained?Mathf.Repeat(time,Mathf.Max(.1f,duration)):Mathf.Min(time,duration);var index=Mathf.Clamp(Mathf.RoundToInt(sampleTime*60f),0,behaviorTrace.Frames.Count-1);var frame=behaviorTrace.Frames[index];
            if(profile==StyledVfxProfile.Projectile||profile==StyledVfxProfile.Trail)transform.localPosition=behaviorOrigin+frame.Position;
            if(profile==StyledVfxProfile.Beam&&lines.Length>0&&lines[0]!=null){var line=lines[0];line.positionCount=Mathf.Max(2,line.positionCount);for(var p=0;p<line.positionCount;p++){var t=p/(float)(line.positionCount-1);var point=Vector3.Lerp(frame.Source,frame.Target,t);if(hitType=="arc_link")point+=Vector3.up*Mathf.Sin(t*Mathf.PI)*GetBehaviorValue(hitKeys,hitValues,"sag",.2f)+Vector3.up*Mathf.Sin(t*31f+sampleTime*43f+seed)*GetBehaviorValue(hitKeys,hitValues,"jitter",.04f);line.SetPosition(p,point);}line.widthMultiplier=Mathf.Max(.02f,.12f*frame.Width);}
            if((profile==StyledVfxProfile.Area||profile==StyledVfxProfile.Impact)&&animatedTransforms.Length>0&&animatedTransforms[0]!=null&&frame.Radius>.001f)animatedTransforms[0].localScale=baseScales[0]*frame.Radius;
        }

        private static void CopyParameters(string[] keys,float[] values,System.Collections.Generic.Dictionary<string,double> target){var count=Mathf.Min(keys==null?0:keys.Length,values==null?0:values.Length);for(var i=0;i<count;i++)if(!string.IsNullOrEmpty(keys[i]))target[keys[i]]=values[i];}
        private static float GetBehaviorValue(string[] keys,float[] values,string key,float fallback){var count=Mathf.Min(keys==null?0:keys.Length,values==null?0:values.Length);for(var i=0;i<count;i++)if(keys[i]==key)return values[i];return fallback;}
    }
}
