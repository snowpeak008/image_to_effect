using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-scene-only deterministic driver for W15; never belongs to a production Runtime Entry.</summary>
    [DisallowMultipleComponent]
    public sealed class NewArchetypePreviewDriver : MonoBehaviour
    {
        [SerializeField] private StyledVfxController[] entries = new StyledVfxController[0];
        [SerializeField, Min(2f)] private float cycleDuration = 4f;
        private readonly List<Vector3> origins = new List<Vector3>();
        private float elapsed;
        private bool started, pickup;

        private void Start()
        {
            origins.Clear(); var portalIndex = 0; var lootIndex = 0;
            foreach (var entry in entries)
            {
                if (entry == null) { origins.Add(Vector3.zero); continue; }
                entry.gameObject.SetActive(true); origins.Add(entry.transform.position); entry.ResetForPool();
                if (entry.Profile == StyledVfxProfile.Teleport) entry.ConfigurePortal(entry.PairId, portalIndex++ == 0 ? PortalEndpointRole.Entry : PortalEndpointRole.Exit);
                if (entry.Profile == StyledVfxProfile.Loot) entry.SetRarity(++lootIndex);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!started && elapsed >= .08f) { started = true; StartEntries(); }
            DriveWeaponTrails();
            if (!pickup && elapsed >= 2.15f) { pickup = true; foreach (var entry in entries) if (entry != null && entry.Profile == StyledVfxProfile.Loot) { entry.SetPickupTarget(entry.transform.position + new Vector3(.62f,.55f,0)); entry.BeginPickup(); } }
            if (elapsed >= cycleDuration) ResetCycle();
        }

        private void StartEntries()
        {
            var decalIndex = 0;
            foreach (var entry in entries)
            {
                if (entry == null) continue;
                if (entry.Profile == StyledVfxProfile.Decal)
                {
                    var normals = new[] { Vector3.forward, Quaternion.Euler(0,45,0)*Vector3.forward, Vector3.right };
                    entry.RegisterDecalHit("preview_surface_"+decalIndex,entry.transform.position,normals[decalIndex++%normals.Length]);
                }
                else if (entry.Profile == StyledVfxProfile.Destruction) entry.TriggerDestruction(new Vector3(.3f,.2f,0));
                else if (entry.Profile == StyledVfxProfile.Teleport) entry.TriggerTraverse();
                else if (entry.Profile != StyledVfxProfile.WeaponTrail) entry.Play();
            }
        }

        private void DriveWeaponTrails()
        {
            for (var index=0;index<entries.Length;index++)
            {
                var entry=entries[index]; if(entry==null||entry.Profile!=StyledVfxProfile.WeaponTrail)continue;
                var center=origins[index]; var angle=elapsed*5.5f+(index*.8f); var root=center+new Vector3(-.42f,0,0); var tip=center+new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.58f;
                entry.SetWeaponEndpoints(root,tip,Mathf.Max(.0001f,Time.deltaTime));
            }
        }

        private void ResetCycle()
        {
            for(var index=0;index<entries.Length;index++)if(entries[index]!=null){entries[index].Stop(VfxStopMode.Immediate);entries[index].ResetForPool();entries[index].transform.position=origins[index];}
            elapsed=0;started=pickup=false;
        }
    }
}
