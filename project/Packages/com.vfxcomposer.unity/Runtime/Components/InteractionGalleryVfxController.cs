using UnityEngine;

namespace VFXComposer
{
    public enum InteractionGalleryProfile
    {
        Charge, Channel, Telegraph, Chain, Homing, WeaponEnchant, Dash, DissolveTransform, MultiStage
    }

    /// <summary>Player-safe Runtime Entry for interaction/composition validation profiles.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionGalleryVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private InteractionGalleryProfile profile;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private Transform[] animatedTransforms = new Transform[0];
        [SerializeField] private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private float[] shapeModes = new float[0];
        [SerializeField] private float[] intensities = new float[0];
        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;
        [SerializeField] private bool sustained;
        [SerializeField, Min(.4f)] private float duration = 4f;

        private MaterialPropertyBlock block;
        private Vector3[] initialPositions;
        private Vector3[] initialScales;
        private Quaternion[] initialRotations;
        private bool captured, playing, stopping;
        private float elapsed, stopElapsed, eventAge=99f;

        public InteractionGalleryProfile Profile { get { return profile; } }
        public bool Sustained { get { return sustained; } }
        public bool IsAlive { get { return playing || stopping; } }
        private MaterialPropertyBlock Block { get { if(block==null)block=new MaterialPropertyBlock();return block; } }

        private void Awake(){Capture();ResetForPool();}
        private void Update()
        {
            if(!playing&&!stopping)return;var delta=Mathf.Max(0f,Time.deltaTime);eventAge+=delta;if(playing){elapsed+=delta;if(!sustained&&elapsed>=duration)BeginStop();}if(stopping){stopElapsed+=delta;if(stopElapsed>=.35f){ResetForPool();return;}}ApplyVisuals();
        }

        public void Initialize(VfxRuntimeContext context){ResetForPool();transform.SetPositionAndRotation(context.Position,context.Rotation);}
        public void Play()
        {
            Capture();Restore();playing=true;stopping=false;elapsed=0;stopElapsed=0;eventAge=0;foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=true;if(trail!=null){trail.Clear();trail.enabled=true;trail.emitting=true;}ApplyVisuals();
        }
        public bool SendEvent(string eventId,VfxRuntimeEvent payload)
        {
            if(eventId=="play"||eventId=="start"){transform.SetPositionAndRotation(payload.Position,payload.Rotation);Play();return true;}if(eventId=="release"||eventId=="tick"||eventId=="hit"||eventId=="retarget"||eventId=="refresh"){if(!playing)return false;eventAge=0;return true;}if(eventId=="cancel"||eventId=="break"||eventId=="stop"){Stop(VfxStopMode.AllowTail);return true;}return false;
        }
        public void Stop(VfxStopMode mode){if(mode==VfxStopMode.Immediate){ResetForPool();return;}BeginStop();}
        public void ResetForPool()
        {
            Capture();Restore();playing=false;stopping=false;elapsed=stopElapsed=0;eventAge=99;if(trail!=null){trail.emitting=false;trail.Clear();trail.enabled=false;}ApplyProperties(0,0,0);foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=false;
        }
        private void BeginStop(){if(!playing&&!stopping)return;playing=false;stopping=true;stopElapsed=0;if(trail!=null)trail.emitting=false;}

        private void ApplyVisuals()
        {
            var progress=sustained?Mathf.Repeat(elapsed,Mathf.Max(.4f,duration))/Mathf.Max(.4f,duration):Mathf.Clamp01(elapsed/Mathf.Max(.4f,duration));var alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.16f));if(!sustained)alpha*=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-duration+.42f)/.42f));if(stopping)alpha*=1-Mathf.SmoothStep(0,1,stopElapsed/.35f);var pulse=eventAge<.6f?Mathf.Sin(Mathf.PI*Mathf.Clamp01(eventAge/.6f)):0;ApplyProperties(alpha,progress,pulse);Animate(progress,pulse);UpdateLines(progress,pulse);
        }

        private void ApplyProperties(float alpha,float progress,float pulse)
        {
            for(var i=0;i<renderers.Length;i++)
            {
                var renderer=renderers[i];if(renderer==null)continue;var layer=LayerAlpha(renderer.name,progress);renderer.GetPropertyBlock(Block);Block.SetColor("_PrimaryColor",primaryColor);Block.SetColor("_SecondaryColor",secondaryColor);Block.SetFloat("_GlobalAlpha",alpha*layer);Block.SetFloat("_RuntimeTime",elapsed);Block.SetFloat("_Progress",progress);Block.SetFloat("_Pulse",pulse);Block.SetFloat("_ShapeMode",i<shapeModes.Length?shapeModes[i]:0);Block.SetFloat("_Intensity",i<intensities.Length?intensities[i]:1);renderer.SetPropertyBlock(Block);
            }
        }

        private float LayerAlpha(string name,float p)
        {
            if(profile==InteractionGalleryProfile.Telegraph){if(name.Contains("Detonation"))return SmoothWindow(.68f,.76f,.84f,1f,p);if(name.Contains("Countdown"))return .25f+.75f*p;return .58f+.28f*p;}
            if(profile==InteractionGalleryProfile.Dash){if(name.Contains("StartGhost"))return 1-Smooth01(.12f,.42f,p);if(name.Contains("DashHead")||name.Contains("Trail")||name.Contains("DashStreak"))return SmoothWindow(.12f,.22f,.66f,.84f,p);if(name.Contains("EndGhost"))return .18f+.82f*Smooth01(.52f,.72f,p);}
            if(profile==InteractionGalleryProfile.DissolveTransform){if(name.Contains("Source"))return 1-Smooth01(.2f,.58f,p);if(name.Contains("Fragments"))return Mathf.Sin(Mathf.PI*Mathf.Clamp01((p-.18f)/.62f));if(name.Contains("Target"))return Smooth01(.48f,.78f,p);}
            if(profile==InteractionGalleryProfile.MultiStage){if(name.Contains("Charge"))return 1-Smooth01(.22f,.36f,p);if(name.Contains("Projectile"))return SmoothWindow(.18f,.28f,.58f,.68f,p);if(name.Contains("Impact"))return SmoothWindow(.56f,.66f,.82f,.92f,p);if(name.Contains("Residue"))return .72f*Smooth01(.7f,.88f,p);}
            return 1;
        }

        private static float Smooth01(float from,float to,float value){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));}
        private static float SmoothWindow(float enterFrom,float enterTo,float exitFrom,float exitTo,float value){return Smooth01(enterFrom,enterTo,value)*(1-Smooth01(exitFrom,exitTo,value));}

        private void Animate(float progress,float pulse)
        {
            var cycle=progress*Mathf.PI*2;
            foreach(var target in animatedTransforms)
            {
                if(target==null)continue;var index=System.Array.IndexOf(animatedTransforms,target);var initial=initialPositions[index];var scale=initialScales[index];var rotation=initialRotations[index];
                if(profile==InteractionGalleryProfile.Charge){var angle=cycle*(1.5f+index*.18f)+index*2.1f;var radius=Mathf.Lerp(.72f,.18f,Mathf.SmoothStep(0,1,progress));target.localPosition=initial+new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius*.72f,.12f*Mathf.Sin(angle*.7f));target.localScale=scale*(.55f+progress*.75f+pulse*.3f);}
                else if(profile==InteractionGalleryProfile.Channel){target.localPosition=initial+(target.name.Contains("Target")?new Vector3(0,Mathf.Sin(cycle)*.22f,Mathf.Cos(cycle)*.08f):new Vector3(0,Mathf.Sin(cycle+.8f)*.05f,0));target.localScale=scale*(1+pulse*.18f);}
                else if(profile==InteractionGalleryProfile.Chain){target.localPosition=initial+new Vector3(0,Mathf.Sin(cycle*1.3f+index*1.7f)*.1f,Mathf.Cos(cycle+index)*.04f);target.localScale=scale*(1+pulse*.2f);}
                else if(profile==InteractionGalleryProfile.Telegraph){target.localRotation=rotation*Quaternion.Euler(0,0,elapsed*(index%2==0?42:-31));target.localScale=scale*(.8f+.32f*progress+pulse*.18f);}
                else if(profile==InteractionGalleryProfile.Homing&&target.name.Contains("HomingHead")){var a=new Vector3(-1.05f,-.34f,0);var b=new Vector3(-.25f,.82f,.18f);var c=new Vector3(.95f,.08f,0);var t=progress;target.localPosition=(1-t)*(1-t)*a+2*(1-t)*t*b+t*t*c;}
                else if(profile==InteractionGalleryProfile.WeaponEnchant){target.localRotation=rotation*Quaternion.Euler(0,0,-24+Mathf.Sin(cycle)*36);target.localPosition=initial+new Vector3(Mathf.Sin(cycle)*.14f,Mathf.Cos(cycle*2)*.08f,0);}
                else if(profile==InteractionGalleryProfile.Dash&&target.name.Contains("DashHead")){var dashT=Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.14f)/.42f));target.localPosition=Vector3.Lerp(new Vector3(-1.05f,-.2f,0),new Vector3(1.05f,.25f,0),dashT);}
                else if(profile==InteractionGalleryProfile.DissolveTransform&&target.name.Contains("Fragments")){target.localRotation=rotation*Quaternion.Euler(elapsed*24,elapsed*38,elapsed*52);target.localScale=scale*(.72f+.42f*Mathf.Sin(Mathf.PI*progress));}
                else if(profile==InteractionGalleryProfile.MultiStage&&target.name.Contains("Projectile")){target.localPosition=Vector3.Lerp(new Vector3(-.72f,0,0),new Vector3(.72f,.12f,0),Mathf.Clamp01((progress-.2f)/.45f));}
                else{target.localRotation=rotation*Quaternion.Euler(Mathf.Sin(cycle+index)*6,elapsed*(9+index*4),elapsed*(12-index*2));target.localScale=scale*(1+.05f*Mathf.Sin(cycle+index)+pulse*.1f);}
            }
        }

        private void UpdateLines(float progress,float pulse)
        {
            for(var lineIndex=0;lineIndex<lines.Length;lineIndex++)
            {
                var line=lines[lineIndex];if(line==null)continue;if(profile==InteractionGalleryProfile.Chain){var start=animatedTransforms[Mathf.Min(lineIndex,animatedTransforms.Length-1)].localPosition;var end=animatedTransforms[Mathf.Min(lineIndex+1,animatedTransforms.Length-1)].localPosition;const int count=12;line.positionCount=count;for(var i=0;i<count;i++){var t=i/(count-1f);var basePoint=Vector3.Lerp(start,end,t);var normal=new Vector3(-(end-start).y,(end-start).x,0).normalized;line.SetPosition(i,basePoint+normal*Mathf.Sin(t*Mathf.PI*5+elapsed*13+lineIndex)*.055f*Mathf.Sin(Mathf.PI*t));}}
                else{const int count=28;line.positionCount=count;var targetY=profile==InteractionGalleryProfile.Channel?Mathf.Sin(progress*Mathf.PI*2)*.22f:.25f;for(var i=0;i<count;i++){var t=i/(count-1f);var x=Mathf.Lerp(-1.02f,1.02f,t);var envelope=Mathf.Sin(Mathf.PI*t);var y=Mathf.Lerp(0,targetY,t)+Mathf.Sin(t*18+elapsed*7+lineIndex*1.4f)*(.04f+lineIndex*.025f)*envelope;line.SetPosition(i,new Vector3(x,y,Mathf.Cos(t*11-elapsed*4)*.045f*envelope));}}
            }
        }

        private void Capture()
        {
            if(captured)return;captured=true;initialPositions=new Vector3[animatedTransforms.Length];initialScales=new Vector3[animatedTransforms.Length];initialRotations=new Quaternion[animatedTransforms.Length];for(var i=0;i<animatedTransforms.Length;i++){var target=animatedTransforms[i];initialPositions[i]=target==null?Vector3.zero:target.localPosition;initialScales[i]=target==null?Vector3.one:target.localScale;initialRotations[i]=target==null?Quaternion.identity:target.localRotation;}
        }
        private void Restore(){if(!captured)return;for(var i=0;i<animatedTransforms.Length;i++){var target=animatedTransforms[i];if(target==null)continue;target.localPosition=initialPositions[i];target.localScale=initialScales[i];target.localRotation=initialRotations[i];}}
    }
}
