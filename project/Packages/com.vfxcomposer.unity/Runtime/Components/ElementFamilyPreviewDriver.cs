using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-scene-only replay harness. It never belongs to a generated Runtime Entry.</summary>
    [DisallowMultipleComponent]
    public sealed class ElementFamilyPreviewDriver : MonoBehaviour
    {
        [SerializeField] private StyledVfxController[] entries = new StyledVfxController[0];
        [SerializeField, Min(.1f)] private float replayDelay = .35f;
        private float[] ages = new float[0];

        private void OnEnable()
        {
            ages=new float[entries==null?0:entries.Length];
            for(var i=0;i<ages.Length;i++){ages[i]=i*.055f;if(entries[i]!=null){entries[i].ResetForPool();entries[i].Play();}}
        }

        private void Update()
        {
            for(var i=0;i<ages.Length;i++)
            {
                var entry=entries[i];if(entry==null)continue;ages[i]+=Mathf.Max(0f,Time.deltaTime);
                if(entry.Lifecycle==StyledVfxLifecycle.Sustained){if(!entry.IsAlive)entry.Play();continue;}
                if(!entry.IsAlive&&ages[i]>=replayDelay){entry.ResetForPool();entry.Play();ages[i]=0f;}
            }
        }
    }
}
