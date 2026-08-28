using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFXComposer.W15NextCandidate;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W15NextCandidateRuntimeTests
    {
        private static readonly string[] ProductionIds =
        {
            "w15nc_scorch_decal_3d",
            "w15nc_frost_decal_3d",
            "w15nc_katana_trail_weapon_3d",
            "w15nc_energy_whip_trail_2d",
            "w15nc_crate_break_destruction_3d",
            "w15nc_crystal_shatter_destruction_3d",
            "w15nc_death_dissolve_lifecycle_3d",
            "w15nc_hero_entrance_lifecycle_3d",
            "w15nc_twin_portal_3d",
            "w15nc_loot_beam_pickup_3d"
        };

        [UnityTest]
        public IEnumerator Decal_AttachesToGroundWallAndFortyFiveDegreeSurfaceWithDepthAndStackSemantics()
        {
            var ground = new Fixture(W15NextArchetype.Decal);
            var wall = new Fixture(W15NextArchetype.Decal);
            var slope = new Fixture(W15NextArchetype.Decal);
            yield return null;

            var cases = new[]
            {
                new { Fixture = ground, Key = "ground", Point = new Vector3(-1f, .1f, 0f), Normal = Vector3.up, Tangent = Vector3.forward },
                new { Fixture = wall, Key = "wall", Point = new Vector3(0f, .8f, .4f), Normal = Vector3.forward, Tangent = Vector3.up },
                new { Fixture = slope, Key = "slope-45", Point = new Vector3(1f, .3f, 0f), Normal = new Vector3(0f, 1f, 1f).normalized, Tangent = Vector3.right }
            };
            foreach (var value in cases)
            {
                value.Fixture.Controller.AttachToSurface(value.Key, value.Point, value.Normal, value.Tangent);
                Assert.That(Vector3.Dot(value.Fixture.Controller.transform.forward, value.Normal), Is.GreaterThan(.999f), value.Key);
                Assert.That(Vector3.Dot(value.Fixture.Controller.SurfaceNormal, value.Fixture.Controller.SurfaceTangent), Is.EqualTo(0f).Within(.0001f), value.Key);
                Assert.That(Vector3.Dot(value.Fixture.Controller.transform.position - value.Point, value.Normal), Is.EqualTo(value.Fixture.Controller.SurfaceBias).Within(.0001f), value.Key);
                Assert.That(value.Fixture.DecalLayers.Select(layer => layer.localPosition.z), Is.Ordered.Ascending, value.Key + " depth layers");
            }

            SetPrivate(ground.Controller, "stackLimit", 2);
            var stackB = new Fixture(W15NextArchetype.Decal);
            var stackC = new Fixture(W15NextArchetype.Decal);
            SetPrivate(stackB.Controller, "stackLimit", 2);
            SetPrivate(stackC.Controller, "stackLimit", 2);
            ground.Controller.AttachToSurface("stacked-ground", Vector3.zero, Vector3.up, Vector3.forward);
            stackB.Controller.AttachToSurface("stacked-ground", Vector3.zero, Vector3.up, Vector3.forward);
            stackC.Controller.AttachToSurface("stacked-ground", Vector3.zero, Vector3.up, Vector3.forward);
            Assert.That(ground.Controller.IsAlive, Is.False, "The oldest decal is recycled at the fixed stack limit.");
            Assert.That(stackB.Controller.IsAlive && stackC.Controller.IsAlive, Is.True);
            Destroy(ground, wall, slope, stackB, stackC);
        }

        [UnityTest]
        public IEnumerator WeaponTrail_FastAndSlowSwingsProduceDifferentMeasuredTrajectories()
        {
            var fast = new Fixture(W15NextArchetype.WeaponTrail);
            var slow = new Fixture(W15NextArchetype.WeaponTrail);
            SetPrivate(fast.Controller, "speedThreshold", 1f);
            SetPrivate(slow.Controller, "speedThreshold", 1f);
            SetPrivate(fast.Controller, "fadeTime", .08f);
            SetPrivate(slow.Controller, "fadeTime", .08f);
            yield return null;

            for (var index = 0; index < 7; index++)
            {
                var fastAngle = index * 12f * Mathf.Deg2Rad;
                var slowAngle = index * .2f * Mathf.Deg2Rad;
                fast.Controller.DriveWeaponEndpoints(Vector3.zero, new Vector3(Mathf.Cos(fastAngle), Mathf.Sin(fastAngle), 0f), .01f);
                slow.Controller.DriveWeaponEndpoints(Vector3.zero, new Vector3(Mathf.Cos(slowAngle), Mathf.Sin(slowAngle), 0f), .2f);
            }
            Assert.That(fast.Controller.LastWeaponSpeed, Is.GreaterThan(1f));
            Assert.That(slow.Controller.LastWeaponSpeed, Is.LessThan(1f));
            Assert.That(fast.Controller.WeaponSampleCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(slow.Controller.WeaponSampleCount, Is.Zero);
            Assert.That(fast.Controller.WeaponOpacity, Is.GreaterThan(slow.Controller.WeaponOpacity));
            Assert.That(fast.DynamicFilter.sharedMesh.vertexCount, Is.EqualTo(((fast.Controller.WeaponSampleCount - 1) * 2 + 1) * 2), "Catmull-Rom midpoints make the swept trajectory smoother than raw history joins.");
            Assert.That(fast.DynamicRenderer.enabled, Is.True);
            Assert.That(slow.DynamicRenderer.enabled, Is.False);
            fast.Controller.Stop(VfxStopMode.Immediate);
            Assert.That(fast.DynamicFilter.sharedMesh.vertexCount, Is.Zero);
            Destroy(fast, slow);
        }

        [UnityTest]
        public IEnumerator Destruction_UsesAllIndependentDeterministicFragmentsWithTwoBounceCleanup()
        {
            var first = new Fixture(W15NextArchetype.Destruction);
            var replay = new Fixture(W15NextArchetype.Destruction);
            SetPrivate(first.Controller, "seed", (uint)731);
            SetPrivate(replay.Controller, "seed", (uint)731);
            SetPrivate(first.Controller, "pieceCount", 12);
            SetPrivate(replay.Controller, "pieceCount", 12);
            SetPrivate(first.Controller, "duration", .32f);
            SetPrivate(replay.Controller, "duration", .32f);
            var impulse = new Vector3(.35f, .12f, -.08f);
            first.Controller.TriggerDestruction(impulse);
            replay.Controller.TriggerDestruction(impulse);
            yield return new WaitForSeconds(.2f);

            Assert.That(first.Controller.ActiveDestructionPieceCount, Is.EqualTo(12));
            Assert.That(first.DynamicFilter.sharedMesh.vertexCount, Is.EqualTo(48), "Every fragment owns an independently transformed quad.");
            Assert.That(Enumerable.Range(0, 12).Select(index => Quantize(first.Controller.GetCurrentPiecePosition(index))).Distinct().Count(), Is.EqualTo(12));
            for (var index = 0; index < 12; index++)
            {
                Assert.That(first.Controller.GetDeterministicPiecePosition(index, .43f), Is.EqualTo(replay.Controller.GetDeterministicPiecePosition(index, .43f)));
                Assert.That(first.Controller.GetDeterministicPieceBounceCount(index, 5f), Is.EqualTo(2));
            }
            Assert.That(first.Root.GetComponentInChildren<Rigidbody>(true), Is.Null);
            yield return new WaitForSeconds(.4f);
            Assert.That(first.Controller.IsAlive, Is.False);
            Assert.That(first.DynamicFilter.sharedMesh.vertexCount, Is.Zero);
            Assert.That(first.Renderers.All(renderer => !renderer.enabled), Is.True);
            Destroy(first, replay);
        }

        [UnityTest]
        public IEnumerator LifeCycle_DissolvesVisibleBoundCharacterForDeathAndEntranceWithoutDisablingIt()
        {
            var death = new Fixture(W15NextArchetype.LifeCycle);
            var entrance = new Fixture(W15NextArchetype.LifeCycle);
            SetPrivate(death.Controller, "duration", .3f);
            SetPrivate(entrance.Controller, "duration", .3f);
            SetPrivate(entrance.Controller, "inverseEntrance", true);
            var deathBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var entranceBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            deathBody.name = "VisibleDeathCharacterModel";
            entranceBody.name = "VisibleEntranceCharacterModel";
            var deathRenderer = deathBody.GetComponent<Renderer>();
            var entranceRenderer = entranceBody.GetComponent<Renderer>();
            var originalBlock = new MaterialPropertyBlock();
            originalBlock.SetFloat("_GlobalAlpha", .63f);
            deathRenderer.SetPropertyBlock(originalBlock);
            death.Controller.BindCharacterRenderers(new[] { deathRenderer });
            entrance.Controller.BindCharacterRenderers(new[] { entranceRenderer });
            death.Controller.Play();
            entrance.Controller.Play();
            Assert.That(entrance.Controller.LifecycleProgress, Is.EqualTo(1f));
            yield return new WaitForSeconds(.1f);

            Assert.That(death.Controller.BoundRendererCount, Is.EqualTo(1));
            Assert.That(death.Controller.LifecycleProgress, Is.InRange(.1f, .9f));
            Assert.That(entrance.Controller.LifecycleProgress, Is.InRange(.1f, .9f));
            Assert.That(death.Controller.LifecycleProgress, Is.GreaterThan(1f - entrance.Controller.LifecycleProgress - .08f));
            var block = new MaterialPropertyBlock();
            deathRenderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_Dissolve"), Is.EqualTo(death.Controller.LifecycleProgress).Within(.06f));
            Assert.That(deathRenderer.enabled && entranceRenderer.enabled, Is.True, "The effect binds to the visible model instead of replacing or disabling it.");
            death.Controller.Stop(VfxStopMode.Immediate);
            deathRenderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_Dissolve"), Is.Zero);
            Assert.That(block.GetFloat("_GlobalAlpha"), Is.EqualTo(.63f).Within(.0001f), "Reset restores the gameplay model's pre-existing property block.");
            Assert.That(death.Controller.BoundRendererCount, Is.Zero);
            Assert.That(deathRenderer.enabled, Is.True);
            Destroy(death, entrance);
            UnityEngine.Object.Destroy(deathBody);
            UnityEngine.Object.Destroy(entranceBody);
        }

        [UnityTest]
        public IEnumerator Portal_EntryIntakeAndDelayedExitEjectionHaveDifferentTimingShapesAndFlow()
        {
            var entry = new Fixture(W15NextArchetype.Portal);
            var exit = new Fixture(W15NextArchetype.Portal);
            entry.Controller.ConfigurePortal("w15-pair-test", PortalEndpointRole.Entry);
            exit.Controller.ConfigurePortal("w15-pair-test", PortalEndpointRole.Exit);
            entry.Controller.TriggerTraverse();
            exit.Controller.TriggerTraverse();
            yield return new WaitForSeconds(.1f);

            Assert.That(entry.Controller.PortalPhase, Is.EqualTo(W15PortalPhase.EntryIntake));
            Assert.That(exit.Controller.PortalPhase, Is.EqualTo(W15PortalPhase.ExitDelay));
            Assert.That(entry.PortalEntryRenderer.enabled, Is.True);
            Assert.That(exit.PortalExitRenderer.enabled, Is.False);
            Assert.That(entry.Controller.PortalFlowDirection, Is.EqualTo(-1f));
            Assert.That(exit.Controller.PortalFlowDirection, Is.EqualTo(1f));
            Assert.That(entry.PortalLine.positionCount, Is.EqualTo(20));
            Assert.That(exit.PortalLine.positionCount, Is.Zero, "Exit geometry remains absent during the explicit transit delay.");
            yield return new WaitForSeconds(.29f);

            Assert.That(entry.Controller.PortalPhase, Is.EqualTo(W15PortalPhase.HiddenTransit));
            Assert.That(exit.Controller.PortalPhase, Is.EqualTo(W15PortalPhase.ExitEjection));
            Assert.That(exit.PortalExitRenderer.enabled, Is.True);
            Assert.That(entry.PortalEntry.localScale, Is.Not.EqualTo(exit.PortalExit.localScale));
            Assert.That(exit.PortalLine.positionCount, Is.EqualTo(20));
            Assert.That(entry.Controller.PairId, Is.EqualTo(exit.Controller.PairId));
            yield return new WaitForSeconds(.12f);
            Assert.That(entry.PortalEntryRenderer.enabled, Is.False, "Entry geometry is actually hidden after intake/transit fade.");
            Assert.That(exit.PortalExitRenderer.enabled, Is.True, "Exit remains in its later ejection/settle morphology.");
            Destroy(entry, exit);
        }

        [UnityTest]
        public IEnumerator Loot_FiveTiersDifferInGeometryLayersCadencePeakAndCurvedPickup()
        {
            var tiers = Enumerable.Range(1, 5).Select(_ => new Fixture(W15NextArchetype.Loot)).ToArray();
            yield return null;
            for (var index = 0; index < tiers.Length; index++) tiers[index].Controller.ConfigureRarity(index + 1);

            CollectionAssert.AreEqual(new[] { W15LootGeometry.Circle, W15LootGeometry.Diamond, W15LootGeometry.Hexagon, W15LootGeometry.Crown, W15LootGeometry.Star }, tiers.Select(value => value.Controller.LootGeometry).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, tiers.Select(value => value.Controller.LootLayerCount).ToArray());
            Assert.That(tiers.Select(value => value.DynamicFilter.sharedMesh.vertexCount).Distinct().Count(), Is.EqualTo(5), "Each tier has different generated geometry, not a color-only switch.");
            for (var index = 0; index < tiers.Length; index++) Assert.That(tiers[index].DynamicFilter.sharedMesh.vertices.Select(vertex => Mathf.RoundToInt(vertex.y * 1000f)).Distinct().Count(), Is.EqualTo(index + 1), "Tier " + (index + 1) + " owns real stacked geometry layers.");
            AssertStrictlyIncreasing(tiers.Select(value => value.Controller.LootCadenceHz), "cadence");
            AssertStrictlyIncreasing(tiers.Select(value => value.Controller.LootPeakScale), "peak");
            AssertStrictlyIncreasing(tiers.Select(value => value.Controller.LootBeamHeight), "beam height");
            AssertStrictlyIncreasing(tiers.Select(value => value.Controller.LootSparkleRate), "sparkle rate");

            var legendary = tiers[4];
            SetPrivate(legendary.Controller, "pickupSpeed", 2f);
            legendary.Controller.SetPickupTarget(new Vector3(2f, 0f, 0f));
            legendary.Controller.BeginPickup();
            yield return new WaitForSeconds(.1f);
            Assert.That(legendary.Controller.PickupProgress, Is.InRange(.02f, .5f));
            Assert.That(legendary.Controller.PickupTravelPosition.y, Is.GreaterThan(.02f), "Pickup follows the authored quadratic arc rather than a straight line.");
            Assert.That(legendary.LootLine.positionCount, Is.EqualTo(17));
            legendary.Controller.Stop(VfxStopMode.Immediate);
            Assert.That(legendary.LootLine.positionCount, Is.Zero);
            Destroy(tiers);
        }

        [UnityTest]
        public IEnumerator AllArchetypes_AllowTailAndImmediateExitsResetForDeterministicReplay()
        {
            var fixtures = Enum.GetValues(typeof(W15NextArchetype)).Cast<W15NextArchetype>().Select(value => new Fixture(value)).ToArray();
            foreach (var fixture in fixtures) fixture.Controller.Play();
            foreach (var fixture in fixtures) fixture.Controller.Stop(VfxStopMode.AllowTail);
            yield return new WaitForSeconds(.24f);
            foreach (var fixture in fixtures)
            {
                Assert.That(fixture.Controller.IsAlive, Is.False, fixture.Controller.Archetype.ToString());
                Assert.That(fixture.Renderers.All(renderer => !renderer.enabled), Is.True, fixture.Controller.Archetype + " render cleanup");
                fixture.Controller.Play();
                fixture.Controller.Stop(VfxStopMode.Immediate);
                Assert.That(fixture.Controller.IsAlive, Is.False, fixture.Controller.Archetype + " immediate exit");
                Assert.That(fixture.Controller.PlayCount, Is.EqualTo(2), fixture.Controller.Archetype + " replay count");
                Assert.That(fixture.Renderers.All(renderer => !renderer.enabled), Is.True, fixture.Controller.Archetype + " replay cleanup");
            }
            Destroy(fixtures);
        }

        [UnityTest]
        public IEnumerator ProductionPrefabs_AllTenExerciseOwnedParticlesAndBothExitModes()
        {
            var instances = ProductionIds.Select(id =>
            {
                var path = "Assets/VFX/Generated/W15NextCandidate/" + id + "/VFX_" + id + ".prefab";
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = id + "_ProductionExitProbe";
                return instance;
            }).ToArray();
            yield return null;

            var controllers = instances.Select(instance => instance.GetComponentsInChildren<W15NextCandidateController>(true).Single()).ToArray();
            Assert.That(instances.Sum(instance => instance.GetComponentsInChildren<ParticleSystem>(true).Length), Is.GreaterThan(0), "The production topology must exercise real owned ParticleSystems, not only synthetic empty arrays.");
            Assert.That(instances.All(instance => instance.GetComponentInChildren<VFXComposer.W15NextCandidate.NewArchetypePreviewDriver>(true) == null && instance.GetComponentInChildren<VFXComposer.NewArchetypePreviewDriver>(true) == null), Is.True, "Preview drivers may not leak into production Prefabs.");

            foreach (var controller in controllers) controller.Play();
            yield return new WaitForSeconds(.20f);
            foreach (var instance in instances)
            {
                Assert.That(instance.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled), Is.True, instance.name + " must expose a real carrier after Play.");
                var particles = instance.GetComponentsInChildren<ParticleSystem>(true);
                if (particles.Length > 0) Assert.That(particles.Any(particle => particle.IsAlive(true)), Is.True, instance.name + " owned particles must enter a real playing state.");
            }

            foreach (var controller in controllers) controller.Stop(VfxStopMode.AllowTail);
            yield return new WaitForSeconds(.28f);
            for (var index = 0; index < instances.Length; index++)
            {
                Assert.That(controllers[index].IsAlive, Is.False, instances[index].name + " allow-tail state");
                Assert.That(instances[index].GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True, instances[index].name + " allow-tail renderer cleanup");
                Assert.That(instances[index].GetComponentsInChildren<ParticleSystem>(true).All(particle => !particle.IsAlive(true) && particle.particleCount == 0), Is.True, instances[index].name + " allow-tail particle cleanup");
            }

            foreach (var controller in controllers) controller.Play();
            yield return null;
            foreach (var controller in controllers) controller.Stop(VfxStopMode.Immediate);
            for (var index = 0; index < instances.Length; index++)
            {
                Assert.That(controllers[index].IsAlive, Is.False, instances[index].name + " immediate state");
                Assert.That(controllers[index].PlayCount, Is.EqualTo(2), instances[index].name + " deterministic replay count");
                Assert.That(instances[index].GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True, instances[index].name + " immediate renderer cleanup");
                Assert.That(instances[index].GetComponentsInChildren<ParticleSystem>(true).All(particle => !particle.IsAlive(true) && particle.particleCount == 0), Is.True, instances[index].name + " immediate particle cleanup");
                UnityEngine.Object.Destroy(instances[index]);
            }
            yield return null;
        }

        private sealed class Fixture
        {
            public readonly GameObject Root;
            public readonly W15NextCandidateController Controller;
            public readonly Renderer[] Renderers;
            public readonly Transform[] DecalLayers;
            public readonly MeshFilter DynamicFilter;
            public readonly MeshRenderer DynamicRenderer;
            public readonly Transform PortalEntry;
            public readonly Renderer PortalEntryRenderer;
            public readonly Transform PortalExit;
            public readonly Renderer PortalExitRenderer;
            public readonly LineRenderer PortalLine;
            public readonly LineRenderer LootLine;

            public Fixture(W15NextArchetype archetype)
            {
                Root = new GameObject("W15NextRuntime_" + archetype);
                Root.SetActive(false);
                var renderers = new List<Renderer>();
                Transform[] decalLayers = new Transform[0];
                MeshFilter dynamicFilter = null;
                MeshRenderer dynamicRenderer = null;
                Transform portalEntry = null;
                Renderer portalEntryRenderer = null;
                Transform portalExit = null;
                Renderer portalExitRenderer = null;
                LineRenderer portalLine = null;
                LineRenderer lootLine = null;

                Controller = Root.AddComponent<W15NextCandidateController>();
                SetPrivate(Controller, "archetype", archetype);
                SetPrivate(Controller, "variant", VariantFor(archetype));
                SetPrivate(Controller, "duration", .8f);
                SetPrivate(Controller, "seed", (uint)731);

                switch (archetype)
                {
                    case W15NextArchetype.Decal:
                    {
                        decalLayers = Enumerable.Range(0, 3).Select(index =>
                        {
                            MeshFilter ignored;
                            var renderer = AddMesh(Root.transform, "DepthLayer_" + index, out ignored);
                            renderer.transform.localPosition = Vector3.forward * index * .001f;
                            renderers.Add(renderer);
                            return renderer.transform;
                        }).ToArray();
                        SetPrivate(Controller, "decalLayers", decalLayers);
                        SetPrivate(Controller, "surfaceBias", .006f);
                        break;
                    }
                    case W15NextArchetype.WeaponTrail:
                    {
                        dynamicRenderer = AddMesh(Root.transform, "SweptBladeRibbon", out dynamicFilter);
                        var line = AddLine(Root.transform, "BladeEndpoints");
                        renderers.Add(dynamicRenderer); renderers.Add(line);
                        SetPrivate(Controller, "weaponRibbonFilter", dynamicFilter);
                        SetPrivate(Controller, "weaponRibbonRenderer", dynamicRenderer);
                        SetPrivate(Controller, "weaponEndpointLine", line);
                        break;
                    }
                    case W15NextArchetype.Destruction:
                    {
                        MeshFilter intactFilter;
                        var intact = AddMesh(Root.transform, "IntactObject", out intactFilter);
                        dynamicRenderer = AddMesh(Root.transform, "IndependentFragments", out dynamicFilter);
                        renderers.Add(intact); renderers.Add(dynamicRenderer);
                        SetPrivate(Controller, "destructionIntact", intact.transform);
                        SetPrivate(Controller, "destructionIntactRenderer", intact);
                        SetPrivate(Controller, "destructionFragmentsFilter", dynamicFilter);
                        SetPrivate(Controller, "destructionFragmentsRenderer", dynamicRenderer);
                        SetPrivate(Controller, "pieceCount", 12);
                        SetPrivate(Controller, "debrisLifetime", .7f);
                        break;
                    }
                    case W15NextArchetype.LifeCycle:
                    {
                        MeshFilter ignored;
                        var edge = AddMesh(Root.transform, "DissolveEdgeCarrier", out ignored);
                        renderers.Add(edge);
                        SetPrivate(Controller, "lifecycleEdgeRenderer", edge);
                        break;
                    }
                    case W15NextArchetype.Portal:
                    {
                        MeshFilter ignored;
                        var ring = AddMesh(Root.transform, "Ring", out ignored);
                        var interior = AddMesh(Root.transform, "Interior", out ignored);
                        var entry = AddMesh(Root.transform, "EntryFunnel", out ignored);
                        var exit = AddMesh(Root.transform, "ExitBurst", out ignored);
                        portalLine = AddLine(Root.transform, "DirectionalFlow");
                        renderers.Add(ring); renderers.Add(interior); renderers.Add(entry); renderers.Add(exit); renderers.Add(portalLine);
                        SetPrivate(Controller, "portalRing", ring.transform);
                        SetPrivate(Controller, "portalRingRenderer", ring);
                        SetPrivate(Controller, "portalInterior", interior.transform);
                        SetPrivate(Controller, "portalInteriorRenderer", interior);
                        SetPrivate(Controller, "portalEntryFunnel", entry.transform);
                        SetPrivate(Controller, "portalEntryFunnelRenderer", entry);
                        SetPrivate(Controller, "portalExitBurst", exit.transform);
                        SetPrivate(Controller, "portalExitBurstRenderer", exit);
                        SetPrivate(Controller, "portalFlowLine", portalLine);
                        portalEntry = entry.transform; portalEntryRenderer = entry;
                        portalExit = exit.transform; portalExitRenderer = exit;
                        break;
                    }
                    case W15NextArchetype.Loot:
                    {
                        MeshFilter ignored;
                        var lootBase = AddMesh(Root.transform, "LootBody", out ignored);
                        var beam = AddMesh(Root.transform, "LootBeam", out ignored);
                        dynamicRenderer = AddMesh(Root.transform, "TierGeometry", out dynamicFilter);
                        lootLine = AddLine(Root.transform, "PickupArc");
                        renderers.Add(lootBase); renderers.Add(beam); renderers.Add(dynamicRenderer); renderers.Add(lootLine);
                        SetPrivate(Controller, "lootBase", lootBase.transform);
                        SetPrivate(Controller, "lootBaseRenderer", lootBase);
                        SetPrivate(Controller, "lootBeam", beam.transform);
                        SetPrivate(Controller, "lootBeamRenderer", beam);
                        SetPrivate(Controller, "lootCrownFilter", dynamicFilter);
                        SetPrivate(Controller, "lootCrownRenderer", dynamicRenderer);
                        SetPrivate(Controller, "lootPickupArc", lootLine);
                        break;
                    }
                }
                Renderers = renderers.ToArray();
                DecalLayers = decalLayers;
                DynamicFilter = dynamicFilter;
                DynamicRenderer = dynamicRenderer;
                PortalEntry = portalEntry;
                PortalEntryRenderer = portalEntryRenderer;
                PortalExit = portalExit;
                PortalExitRenderer = portalExitRenderer;
                PortalLine = portalLine;
                LootLine = lootLine;
                SetPrivate(Controller, "ownedRenderers", Renderers);
                SetPrivate(Controller, "particles", new ParticleSystem[0]);
                Root.SetActive(true);
            }
        }

        private static MeshRenderer AddMesh(Transform parent, string name, out MeshFilter filter)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            filter = child.AddComponent<MeshFilter>();
            return child.AddComponent<MeshRenderer>();
        }

        private static LineRenderer AddLine(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.positionCount = 0;
            return line;
        }

        private static W15NextVariant VariantFor(W15NextArchetype value)
        {
            switch (value)
            {
                case W15NextArchetype.Decal: return W15NextVariant.ScorchDecal;
                case W15NextArchetype.WeaponTrail: return W15NextVariant.KatanaTrail;
                case W15NextArchetype.Destruction: return W15NextVariant.CrateBreak;
                case W15NextArchetype.LifeCycle: return W15NextVariant.DeathDissolve;
                case W15NextArchetype.Portal: return W15NextVariant.TwinPortal;
                default: return W15NextVariant.LootBeam;
            }
        }

        private static string Quantize(Vector3 value)
        {
            return Mathf.RoundToInt(value.x * 1000f) + ":" + Mathf.RoundToInt(value.y * 1000f) + ":" + Mathf.RoundToInt(value.z * 1000f);
        }

        private static void AssertStrictlyIncreasing(IEnumerable<float> source, string label)
        {
            var values = source.ToArray();
            for (var index = 1; index < values.Length; index++) Assert.That(values[index], Is.GreaterThan(values[index - 1]), label + " tier " + (index + 1));
        }

        private static void Destroy(params Fixture[] fixtures)
        {
            foreach (var fixture in fixtures) if (fixture != null && fixture.Root != null) UnityEngine.Object.Destroy(fixture.Root);
        }

        private static void SetPrivate(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }
    }
}
