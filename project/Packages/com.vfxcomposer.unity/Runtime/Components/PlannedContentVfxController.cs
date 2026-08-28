using UnityEngine;
using UnityEngine.UI;

namespace VFXComposer
{
    public enum PlannedContentKind { Environment, HitFeedback, ScreenUi, GameUi }
    public enum PlannedContentLifecycle { OneShot, Sustained, EventDriven }

    /// <summary>Player-safe protocol entry for W11/W12/W14/W17 content lines.</summary>
    [DisallowMultipleComponent]
    public sealed class PlannedContentVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private string contentId;
        [SerializeField] private PlannedContentKind kind;
        [SerializeField] private PlannedContentLifecycle lifecycle;
        [SerializeField, Min(.05f)] private float duration = 1f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];
        [SerializeField] private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField] private Transform[] animatedTransforms = new Transform[0];
        [SerializeField] private Canvas canvas;
        [SerializeField] private Graphic[] graphics = new Graphic[0];
        [SerializeField] private string[] parameterKeys = new string[0];
        [SerializeField] private float[] parameterValues = new float[0];
        [SerializeField] private string[] textKeys = new string[0];
        [SerializeField] private string[] textValues = new string[0];
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;

        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private Color[] baseGraphicColors = new Color[0];
        private MaterialPropertyBlock block;
        private Renderer[] externalRenderers = new Renderer[0];
        private RectTransform anchorRect;
        private bool followAnchor;
        private float elapsed;
        private bool playing;
        private float intensity = 1f;
        private int stackLevel = 1;
        private int rarity = 1;
        private float fillRatio;
        private Vector3 wind;
        private Vector3 source;
        private Vector3 target = Vector3.right;
        private bool skipped;
        private int playCount;
        private string lastProtocolErrorCode;

        public bool IsAlive { get { return playing; } }
        public string ContentId { get { return contentId; } }
        public PlannedContentKind Kind { get { return kind; } }
        public PlannedContentLifecycle Lifecycle { get { return lifecycle; } }
        public float Intensity { get { return intensity; } }
        public int StackLevel { get { return stackLevel; } }
        public int Rarity { get { return rarity; } }
        public float FillRatio { get { return fillRatio; } }
        public int PlayCount { get { return playCount; } }
        public int ActiveUiElementCount { get { return graphics == null ? 0 : graphics.Length; } }
        public int ParticleCapacity { get { var total=0;if(particles!=null)foreach(var value in particles)if(value!=null)total+=value.main.maxParticles;return total; } }
        public string LastProtocolErrorCode { get { return lastProtocolErrorCode; } }

        private void Awake() { Capture(); ResetForPool(); }
        private void Update()
        {
            if (!playing) return;
            var delta=Mathf.Max(0f,Time.deltaTime);elapsed+=delta;
            if(followAnchor&&anchorRect!=null&&canvas!=null){var world=anchorRect.TransformPoint(anchorRect.rect.center);canvas.transform.position=world;}
            ApplyFrame();
            if(lifecycle==PlannedContentLifecycle.OneShot&&elapsed>=duration)Stop(VfxStopMode.Immediate);
            else if(lifecycle==PlannedContentLifecycle.Sustained&&duration>0&&elapsed>=duration)elapsed-=duration;
        }

        public void Initialize(VfxRuntimeContext context){transform.SetPositionAndRotation(context.Position,context.Rotation);ResetForPool();}
        public void Play()
        {
            Capture();playing=true;elapsed=0f;skipped=false;playCount++;
            if(canvas!=null)canvas.enabled=true;
            foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=true;
            foreach(var graphic in graphics)if(graphic!=null)graphic.enabled=true;
            for(var i=0;i<particles.Length;i++){var particle=particles[i];if(particle==null)continue;particle.useAutoRandomSeed=false;particle.randomSeed=seed+(uint)i;particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particle.Play(true);}
            ApplyFrame();
        }
        public void Stop(VfxStopMode mode){playing=false;foreach(var particle in particles)if(particle!=null)particle.Stop(true,mode==VfxStopMode.Immediate?ParticleSystemStopBehavior.StopEmittingAndClear:ParticleSystemStopBehavior.StopEmitting);SetVisible(false);RestoreExternal();}
        public void ResetForPool(){playing=false;elapsed=0f;skipped=false;lastProtocolErrorCode=null;RestoreTransforms();foreach(var particle in particles)if(particle!=null)particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);SetVisible(false);RestoreExternal();}
        public bool SendEvent(string eventId,VfxRuntimeEvent payload)
        {
            if(eventId=="play"||eventId=="start"||eventId=="trigger"){transform.SetPositionAndRotation(payload.Position,payload.Rotation);Play();return true;}
            if(eventId=="stop"||eventId=="cancel"){Stop(VfxStopMode.Immediate);return true;}
            if(eventId=="tick"||eventId=="hit"||eventId=="refresh"){if(!playing)return false;elapsed=0f;return true;}
            if(eventId=="skip_to_reveal"){SkipToReveal();return string.IsNullOrEmpty(lastProtocolErrorCode);}
            lastProtocolErrorCode="E1840";return false;
        }

        public void SetIntensity(float value){intensity=Mathf.Clamp01(value);}
        public void SetWind(Vector3 value){wind=value;}
        public void SetStackLevel(int value){if(contentId!="combo_surge_aura_2d"&&contentId!="poison_veil_ui"&&contentId!="frost_creep_ui"){lastProtocolErrorCode="E1840";return;}lastProtocolErrorCode=null;stackLevel=Mathf.Clamp(value,1,contentId=="combo_surge_aura_2d"?5:3);}
        public void SetRarity(int value){if(contentId.IndexOf("card_",System.StringComparison.Ordinal)<0&&contentId.IndexOf("gacha_",System.StringComparison.Ordinal)<0){lastProtocolErrorCode="E1840";return;}lastProtocolErrorCode=null;rarity=Mathf.Clamp(value,1,5);}
        public void SetFillRatio(float value){if(contentId!="progress_charge_fx_ui"){lastProtocolErrorCode="E1840";return;}lastProtocolErrorCode=null;fillRatio=Mathf.Clamp01(value);}
        public void SetAnchorRect(RectTransform value,bool follow){if(kind!=PlannedContentKind.ScreenUi&&kind!=PlannedContentKind.GameUi){lastProtocolErrorCode="E1840";return;}if(follow&&value==null){lastProtocolErrorCode="E1841";return;}lastProtocolErrorCode=null;anchorRect=value;followAnchor=follow;}
        public void SetWorldEndpoints(Vector3 from,Vector3 to){if(contentId!="lifesteal_link_beam_2d"&&contentId!="reward_fly_collect_ui"){lastProtocolErrorCode="E1840";return;}if(!Finite(from)||!Finite(to)){lastProtocolErrorCode="E1842";return;}lastProtocolErrorCode=null;source=from;target=to;ApplyEndpointGeometry();}
        public void BindExternalRenderers(Renderer[] targets){externalRenderers=targets??new Renderer[0];}
        public void SkipToReveal(){if(contentId!="gacha_single_reveal_ui"&&contentId!="gacha_ten_sequence_ui"){lastProtocolErrorCode="E1840";return;}lastProtocolErrorCode=null;if(!playing)Play();skipped=true;elapsed=Mathf.Max(elapsed,duration*.72f);ApplyFrame();}
        public float GetParameter(string key,float fallback=0f){var count=Mathf.Min(parameterKeys==null?0:parameterKeys.Length,parameterValues==null?0:parameterValues.Length);for(var i=0;i<count;i++)if(parameterKeys[i]==key)return parameterValues[i];return fallback;}
        public string GetText(string key,string fallback=""){var count=Mathf.Min(textKeys==null?0:textKeys.Length,textValues==null?0:textValues.Length);for(var i=0;i<count;i++)if(textKeys[i]==key)return textValues[i];return fallback;}

        private void ApplyFrame()
        {
            var t=lifecycle==PlannedContentLifecycle.Sustained?Mathf.Repeat(elapsed/Mathf.Max(.05f,duration),1f):Mathf.Clamp01(elapsed/Mathf.Max(.05f,duration));
            if(skipped)t=Mathf.Max(t,.72f);var envelope=lifecycle==PlannedContentLifecycle.Sustained?1f:Mathf.Sin(Mathf.PI*t);var effective=intensity*Mathf.Lerp(.72f,1.2f,(stackLevel-1)/4f);
            if(block==null)block=new MaterialPropertyBlock();
            for(var i=0;i<renderers.Length;i++){var renderer=renderers[i];if(renderer==null)continue;renderer.GetPropertyBlock(block);block.SetColor("_PrimaryColor",primary);block.SetColor("_SecondaryColor",secondary);block.SetColor("_AccentColor",accent);block.SetFloat("_GlobalAlpha",envelope*effective);block.SetFloat("_Intensity",effective);block.SetFloat("_Phase",t);block.SetFloat("_RuntimeTime",elapsed);renderer.SetPropertyBlock(block);}
            for(var i=0;i<animatedTransforms.Length;i++){var item=animatedTransforms[i];if(item==null||i>=basePositions.Length)continue;item.localRotation=baseRotations[i]*Quaternion.Euler(0,0,elapsed*(12+i*7)*(i%2==0?1:-1));var pulse=1f+.06f*Mathf.Sin(elapsed*(3+i));item.localScale=baseScales[i]*pulse;item.localPosition=basePositions[i]+(kind==PlannedContentKind.Environment?wind*Mathf.Sin(elapsed*.7f+i)*.025f:Vector3.zero);}
            for(var i=0;i<graphics.Length;i++){var graphic=graphics[i];if(graphic==null)continue;var color=i<baseGraphicColors.Length?baseGraphicColors[i]:(i%2==0?primary:secondary);color.a*=envelope*effective;graphic.color=color;var rect=graphic.rectTransform;if(rect!=null){var phase=t*Mathf.PI*2+i*.41f;rect.localRotation=Quaternion.Euler(0,0,(i%2==0?1:-1)*Mathf.Sin(phase)*5);var scale=1f+.04f*Mathf.Sin(phase)+(contentId=="progress_charge_fx_ui"?fillRatio*.08f:0);rect.localScale=Vector3.one*scale;}}
            ApplyEndpointGeometry();
            ApplyExternal(envelope*effective);
        }
        private void ApplyEndpointGeometry()
        {
            if(contentId=="lifesteal_link_beam_2d"&&lines!=null){var sag=GetParameter("sag",.35f);foreach(var line in lines){if(line==null)continue;var count=Mathf.Max(2,line.positionCount);for(var i=0;i<count;i++){var u=i/(float)(count-1);var point=Vector3.Lerp(source,target,u);point+=Vector3.down*(4f*u*(1f-u)*sag);point+=Vector3.forward*Mathf.Sin((u*3f-elapsed*2f)*Mathf.PI)*.015f;line.SetPosition(i,transform.InverseTransformPoint(point));}}}
            if(contentId=="reward_fly_collect_ui"&&graphics!=null){var span=Mathf.Max(.05f,duration);var arc=GetParameter("arc_height",1.2f);for(var i=0;i<graphics.Length;i++){var rect=graphics[i]==null?null:graphics[i].rectTransform;if(rect==null)continue;var stagger=GetParameter("stagger",.06f)*i;var u=Mathf.Clamp01((elapsed-stagger)/span);var point=Vector3.Lerp(source,target,u)+Vector3.up*(4f*u*(1f-u)*arc);rect.localPosition=point;}}
        }
        private void ApplyExternal(float amount){if(externalRenderers==null)return;if(block==null)block=new MaterialPropertyBlock();foreach(var renderer in externalRenderers){if(renderer==null)continue;renderer.GetPropertyBlock(block);block.SetFloat("_FlashAmount",amount);block.SetColor("_HitTint",primary);renderer.SetPropertyBlock(block);}}
        private void RestoreExternal(){if(externalRenderers==null)return;if(block==null)block=new MaterialPropertyBlock();foreach(var renderer in externalRenderers){if(renderer==null)continue;renderer.GetPropertyBlock(block);block.SetFloat("_FlashAmount",0f);renderer.SetPropertyBlock(block);}}
        private void SetVisible(bool value){foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=value;foreach(var line in lines)if(line!=null)line.enabled=value;foreach(var graphic in graphics)if(graphic!=null)graphic.enabled=value;if(canvas!=null)canvas.enabled=value;}
        private void Capture(){basePositions=new Vector3[animatedTransforms.Length];baseRotations=new Quaternion[animatedTransforms.Length];baseScales=new Vector3[animatedTransforms.Length];for(var i=0;i<animatedTransforms.Length;i++){var item=animatedTransforms[i];if(item==null)continue;basePositions[i]=item.localPosition;baseRotations[i]=item.localRotation;baseScales[i]=item.localScale;}baseGraphicColors=new Color[graphics.Length];for(var i=0;i<graphics.Length;i++)baseGraphicColors[i]=graphics[i]==null?Color.white:graphics[i].color;}
        private void RestoreTransforms(){for(var i=0;i<animatedTransforms.Length&&i<basePositions.Length;i++){var item=animatedTransforms[i];if(item==null)continue;item.localPosition=basePositions[i];item.localRotation=baseRotations[i];item.localScale=baseScales[i];}}
        private static bool Finite(Vector3 value){return !float.IsNaN(value.x)&&!float.IsInfinity(value.x)&&!float.IsNaN(value.y)&&!float.IsInfinity(value.y)&&!float.IsNaN(value.z)&&!float.IsInfinity(value.z);}
    }

}
