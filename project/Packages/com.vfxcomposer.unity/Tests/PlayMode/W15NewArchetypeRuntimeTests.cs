using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W15NewArchetypeRuntimeTests
    {
        [UnityTest]
        public IEnumerator DecalSurfaceAlignmentStackLimitAndWeaponSpeedFadeAreRuntimeProtocols()
        {
            var decals=new[]{new Fixture(StyledVfxProfile.Decal,StyledVfxLifecycle.OneShot,.4f),new Fixture(StyledVfxProfile.Decal,StyledVfxLifecycle.OneShot,.4f),new Fixture(StyledVfxProfile.Decal,StyledVfxLifecycle.OneShot,.4f)};foreach(var decal in decals)SetPrivate(decal.Controller,"stackLimit",2);yield return null;
            var normal=new Vector3(0,1,1).normalized;decals[0].Controller.RegisterDecalHit("wall-a",Vector3.zero,normal);decals[1].Controller.RegisterDecalHit("wall-a",Vector3.zero,normal);decals[2].Controller.RegisterDecalHit("wall-a",Vector3.zero,normal);
            Assert.That(decals[0].Controller.IsAlive,Is.False,"Third hit replaces the oldest entry at stack limit two.");Assert.That(decals[1].Controller.IsAlive,Is.True);Assert.That(decals[2].Controller.IsAlive,Is.True);Assert.That(Vector3.Dot(decals[2].Controller.transform.forward,normal),Is.GreaterThan(.999f));Assert.That(Vector3.Dot(decals[2].Controller.transform.position,normal),Is.EqualTo(.002f).Within(.0001f));
            var weapon=new Fixture(StyledVfxProfile.WeaponTrail,StyledVfxLifecycle.Sustained,.2f);SetPrivate(weapon.Controller,"speedThreshold",1f);SetPrivate(weapon.Controller,"fadeTime",.05f);weapon.Controller.SetWeaponEndpoints(Vector3.zero,Vector3.right,.01f);weapon.Controller.SetWeaponEndpoints(Vector3.zero,new Vector3(1,1,0),.01f);Assert.That(weapon.Controller.IsAlive,Is.True);Assert.That(weapon.Renderer.enabled,Is.True);weapon.Controller.SetWeaponEndpoints(Vector3.zero,new Vector3(1,1,0),.06f);Assert.That(weapon.Renderer.enabled,Is.False,"Slow/stationary weapon fades without ending the externally driven protocol.");Assert.That(weapon.Controller.HistoryPoints,Is.EqualTo(12));
            foreach(var decal in decals)decal.Destroy();weapon.Destroy();
        }

        [UnityTest]
        public IEnumerator DestructionIsSeedDeterministicAndLifeCycleUsesExternalMpbWithoutDisablingGameplayRenderer()
        {
            var a=new Fixture(StyledVfxProfile.Destruction,StyledVfxLifecycle.OneShot,.25f,3);var b=new Fixture(StyledVfxProfile.Destruction,StyledVfxLifecycle.OneShot,.25f,3);SetPrivate(a.Controller,"seed",(uint)77);SetPrivate(b.Controller,"seed",(uint)77);SetPrivate(a.Controller,"explodeForce",2f);SetPrivate(b.Controller,"explodeForce",2f);a.Controller.TriggerDestruction(new Vector3(.2f,.1f,0));b.Controller.TriggerDestruction(new Vector3(.2f,.1f,0));yield return null;for(var index=0;index<3;index++)Assert.That(a.Transforms[index].localPosition,Is.EqualTo(b.Transforms[index].localPosition));Assert.That(a.Root.GetComponentInChildren<Rigidbody>(),Is.Null);
            var body=new GameObject("ExternalGameplayBody");var bodyRenderer=body.AddComponent<MeshRenderer>();var life=new Fixture(StyledVfxProfile.DeathRebirth,StyledVfxLifecycle.OneShot,.12f);SetPrivate(life.Controller,"lifecycleDirection","up");life.Controller.BindExternalRenderers(new Renderer[]{bodyRenderer});life.Controller.Play();yield return new WaitForSeconds(.06f);var block=new MaterialPropertyBlock();bodyRenderer.GetPropertyBlock(block);Assert.That(block.GetFloat("_Dissolve"),Is.GreaterThan(0));life.Controller.Stop(VfxStopMode.Immediate);Assert.That(bodyRenderer.enabled,Is.True,"LifeCycle MPB must not disable the Gameplay Renderer.");
            a.Destroy();b.Destroy();life.Destroy();Object.Destroy(body);
        }

        [UnityTest]
        public IEnumerator PortalPairRolesAndLootRarityPickupEndpointRemainExternallyDriven()
        {
            var entry=new Fixture(StyledVfxProfile.Teleport,StyledVfxLifecycle.Sustained,.2f);var exit=new Fixture(StyledVfxProfile.Teleport,StyledVfxLifecycle.Sustained,.2f);entry.Controller.ConfigurePortal("pair-7",PortalEndpointRole.Entry);exit.Controller.ConfigurePortal("pair-7",PortalEndpointRole.Exit);entry.Controller.TriggerTraverse();exit.Controller.TriggerTraverse();Assert.That(entry.Controller.PairId,Is.EqualTo("pair-7"));Assert.That(entry.Controller.PortalRole,Is.EqualTo(PortalEndpointRole.Entry));Assert.That(exit.Controller.PortalRole,Is.EqualTo(PortalEndpointRole.Exit));Assert.That(entry.Controller.IsAlive&&exit.Controller.IsAlive,Is.True);
            var loot=new Fixture(StyledVfxProfile.Loot,StyledVfxLifecycle.EventDriven,2f);SetPrivate(loot.Controller,"pickupSpeed",10f);loot.Controller.SetRarity(5);Assert.That(loot.Controller.Rarity,Is.EqualTo(5));loot.Controller.SetPickupTarget(new Vector3(.15f,0,0));loot.Controller.BeginPickup();yield return new WaitForSeconds(.08f);Assert.That(loot.Controller.IsAlive,Is.False);Assert.That(loot.Controller.transform.position.x,Is.EqualTo(.15f).Within(.001f));
            entry.Destroy();exit.Destroy();loot.Destroy();
        }

        private sealed class Fixture
        {
            public readonly GameObject Root=new GameObject("W15RuntimeFixture");public readonly StyledVfxController Controller;public readonly MeshRenderer Renderer;public readonly Transform[] Transforms;
            public Fixture(StyledVfxProfile profile,StyledVfxLifecycle lifecycle,float duration,int count=1)
            {
                var transforms=new List<Transform>();MeshRenderer first=null;for(var index=0;index<count;index++){var visual=new GameObject("Visual_"+index);visual.transform.SetParent(Root.transform,false);visual.AddComponent<MeshFilter>();var renderer=visual.AddComponent<MeshRenderer>();if(first==null)first=renderer;transforms.Add(visual.transform);}Transforms=transforms.ToArray();Renderer=first;Controller=Root.AddComponent<StyledVfxController>();SetPrivate(Controller,"profile",profile);SetPrivate(Controller,"lifecycle",lifecycle);SetPrivate(Controller,"duration",duration);SetPrivate(Controller,"renderers",Root.GetComponentsInChildren<Renderer>(true));SetPrivate(Controller,"animatedTransforms",Transforms);Controller.ResetForPool();
            }
            public void Destroy(){Object.Destroy(Root);}
        }

        private static void SetPrivate(object target,string name,object value){target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
    }
}
