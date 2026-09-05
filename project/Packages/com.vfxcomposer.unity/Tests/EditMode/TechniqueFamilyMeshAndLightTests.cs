using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.TechniqueFamilies;
using VFXComposer.TechniqueFamilies;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Unit 3 constructive predicates (T2b): the 20 mesh generators
    /// (vertices > 0, finite non-degenerate bounds, budget clamp, seed
    /// determinism, output contract channels), VfxLightBeat waveforms and
    /// VfxController v2 phase machine — all EditMode, no rendering.
    /// </summary>
    public sealed class TechniqueFamilyMeshAndLightTests
    {
        private static IEnumerable<string> GeneratorIds()
        {
            return VfxMeshGenerators.Ids;
        }

        [Test]
        public void GeneratorRegistry_HasExactlyTheTwentySpecGenerators()
        {
            Assert.That(VfxMeshGenerators.Ids.Length, Is.EqualTo(20));
            Assert.That(VfxMeshGenerators.Ids, Is.Unique);
            foreach (var id in VfxMeshGenerators.Ids)
                Assert.That(id, Does.Match("^[a-z][a-z0-9_]*$"), id);
            // The ADR-010 section 4bis-8 addition must be present.
            Assert.That(VfxMeshGenerators.Ids, Does.Contain("radial_spike_array"));
            Assert.That(VfxMeshGenerators.Ids, Does.Contain("splash_crown"));
            Assert.That(VfxMeshGenerators.Ids, Does.Contain("voronoi_prefracture"));
        }

        [Test]
        public void AllGenerators_3D_ProduceValidMeshes([ValueSource(nameof(GeneratorIds))] string id)
        {
            var args = new VfxMeshGenArgs(1234u, true, 2048);
            Mesh mesh = VfxMeshGenerators.Generate(id, args);
            AssertMeshContract(mesh, id, 2048);
        }

        [Test]
        public void AllGenerators_2D_ProduceValidMeshes([ValueSource(nameof(GeneratorIds))] string id)
        {
            var args = new VfxMeshGenArgs(1234u, false, 2048);
            Mesh mesh = VfxMeshGenerators.Generate(id, args);
            AssertMeshContract(mesh, id, 2048);
        }

        [Test]
        public void AllGenerators_HonourTightVertexBudget([ValueSource(nameof(GeneratorIds))] string id)
        {
            var args = new VfxMeshGenArgs(77u, true, 256); // ML tier budget
            Mesh mesh = VfxMeshGenerators.Generate(id, args);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), id);
            // Generators reduce detail instead of throwing; small structural
            // overhang (caps/welds) is tolerated at 25%.
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(320), id);
        }

        [Test]
        public void AllGenerators_AreSeedDeterministic([ValueSource(nameof(GeneratorIds))] string id)
        {
            var args = new VfxMeshGenArgs(4242u, true, 2048);
            Mesh a = VfxMeshGenerators.Generate(id, args);
            Mesh b = VfxMeshGenerators.Generate(id, args);
            Assert.That(a.vertexCount, Is.EqualTo(b.vertexCount), id);
            Assert.That(a.triangles.Length, Is.EqualTo(b.triangles.Length), id);
            Vector3[] va = a.vertices;
            Vector3[] vb = b.vertices;
            for (int i = 0; i < va.Length; i++)
                Assert.That((va[i] - vb[i]).sqrMagnitude, Is.LessThan(1e-12f), id + " vertex " + i);
        }

        [Test]
        public void AllGenerators_DifferentSeedsChangeRandomizedOutput()
        {
            // Only assert on generators with strong random structure.
            foreach (var id in new[] { "crystal_cluster", "rock_chunk", "jagged_polyline", "radial_spike_array", "voronoi_prefracture" })
            {
                Mesh a = VfxMeshGenerators.Generate(id, new VfxMeshGenArgs(1u, true, 2048));
                Mesh b = VfxMeshGenerators.Generate(id, new VfxMeshGenArgs(2u, true, 2048));
                bool differs = a.vertexCount != b.vertexCount;
                if (!differs)
                {
                    Vector3[] va = a.vertices;
                    Vector3[] vb = b.vertices;
                    for (int i = 0; i < va.Length && !differs; i++)
                        differs = (va[i] - vb[i]).sqrMagnitude > 1e-10f;
                }
                Assert.That(differs, Is.True, id + ": different seeds must alter geometry");
            }
        }

        [Test]
        public void VoronoiPrefracture_FragmentsMarkCutFaces()
        {
            List<Mesh> frags = VfxMeshGenerators.VoronoiPrefracture(new VfxMeshGenArgs(9u, true, 2048), 8);
            Assert.That(frags.Count, Is.GreaterThanOrEqualTo(2));
            bool anyCut = false;
            bool anyOriginal = false;
            foreach (Mesh frag in frags)
            {
                Assert.That(frag.vertexCount, Is.GreaterThan(0));
                Assert.That(frag.bounds.size.sqrMagnitude, Is.GreaterThan(0f));
                foreach (Color c in frag.colors)
                {
                    if (c.a > 0.5f) anyCut = true;
                    else anyOriginal = true;
                }
            }
            Assert.That(anyCut, Is.True, "cut faces must be marked with colors.a = 1");
            Assert.That(anyOriginal, Is.True, "original surface must keep colors.a = 0");
        }

        [Test]
        public void RadialSpikeArray_BimodalLengthsProduceLongAndShortSpikes()
        {
            Mesh mesh = VfxMeshGenerators.RadialSpikeArray(
                new VfxMeshGenArgs(5u, true, 4096), spikeCount: 48, lengthDistribution: 1);
            // colors.r stores the per-spike normalized length pick.
            bool hasLong = false, hasShort = false;
            foreach (Color c in mesh.colors)
            {
                if (c.r > 0.7f) hasLong = true;
                if (c.r < 0.5f) hasShort = true;
            }
            Assert.That(hasLong && hasShort, Is.True, "bimodal distribution must yield both long and short spikes");
        }

        [Test]
        public void JaggedPolyline_AnchorsBothEndpoints()
        {
            Vector3 start = new Vector3(0.5f, 0f, 0f);
            Vector3 end = new Vector3(0.5f, 3f, 0f);
            Mesh mesh = VfxMeshGenerators.JaggedPolyline(new VfxMeshGenArgs(11u, true, 2048), start, end);
            Bounds bounds = mesh.bounds;
            Assert.That(bounds.Contains(start) || (bounds.ClosestPoint(start) - start).magnitude < 0.15f, Is.True);
            Assert.That(bounds.Contains(end) || (bounds.ClosestPoint(end) - end).magnitude < 0.15f, Is.True);
        }

        private static void AssertMeshContract(Mesh mesh, string id, int budget)
        {
            Assert.That(mesh, Is.Not.Null, id);
            Assert.That(mesh.vertexCount, Is.GreaterThan(0), id + ": vertexCount");
            Assert.That(mesh.triangles.Length, Is.GreaterThan(0), id + ": triangles");
            Assert.That(mesh.vertexCount, Is.LessThanOrEqualTo(budget * 5 / 4), id + ": budget");
            // Output contract: normals + uv0 + structural uv (uv2 channel) + colors.
            Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount), id + ": normals");
            Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), id + ": uv0");
            Assert.That(mesh.uv2.Length, Is.EqualTo(mesh.vertexCount), id + ": uv1 structural channel");
            Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount), id + ": colors");
            Bounds bounds = mesh.bounds;
            Assert.That(float.IsFinite(bounds.size.x) && float.IsFinite(bounds.size.y) && float.IsFinite(bounds.size.z),
                Is.True, id + ": finite bounds");
            Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0f), id + ": non-degenerate bounds");
            foreach (Vector3 v in mesh.vertices)
                Assert.That(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z), Is.True, id + ": finite vertex");
        }
    }

    /// <summary>VfxLightBeat waveform + quantization + decay (pure evaluation, no PlayMode).</summary>
    public sealed class VfxLightBeatTests
    {
        private static VfxLightBeat MakeBeat(VfxFlickerMode mode, float depth = 0.5f, float rate = 2f)
        {
            var go = new GameObject("beat");
            var beat = go.AddComponent<VfxLightBeat>();
            beat.FlickerMode = mode;
            beat.FlickerDepth = depth;
            beat.FlickerRate = rate;
            return beat;
        }

        [Test]
        public void Steady_IsConstantOne()
        {
            var beat = MakeBeat(VfxFlickerMode.Steady);
            try
            {
                for (float t = 0f; t < 3f; t += 0.13f)
                    Assert.That(beat.EvaluateBeat(t), Is.EqualTo(1f).Within(1e-5f));
            }
            finally { Object.DestroyImmediate(beat.gameObject); }
        }

        [Test]
        public void Breathe_OscillatesWithinDepthBand()
        {
            var beat = MakeBeat(VfxFlickerMode.Breathe, 0.6f, 1f);
            try
            {
                float min = 1f, max = 0f;
                for (float t = 0f; t < 2f; t += 0.01f)
                {
                    float v = beat.EvaluateBeat(t);
                    min = Mathf.Min(min, v);
                    max = Mathf.Max(max, v);
                    Assert.That(v, Is.InRange(1f - 0.6f - 1e-4f, 1f + 1e-4f));
                }
                Assert.That(max - min, Is.GreaterThan(0.4f), "breathe must actually oscillate");
            }
            finally { Object.DestroyImmediate(beat.gameObject); }
        }

        [Test]
        public void Strobe_IsHardOnOff()
        {
            var beat = MakeBeat(VfxFlickerMode.Strobe, 1f, 1f);
            try
            {
                var seen = new HashSet<float>();
                for (float t = 0f; t < 2f; t += 0.05f)
                    seen.Add(Mathf.Round(beat.EvaluateBeat(t) * 100f) / 100f);
                Assert.That(seen.Count, Is.LessThanOrEqualTo(2), "strobe output must be binary");
                Assert.That(seen, Does.Contain(1f));
                Assert.That(seen, Does.Contain(0f));
            }
            finally { Object.DestroyImmediate(beat.gameObject); }
        }

        [Test]
        public void FrameRateQuantization_SnapsTime()
        {
            var beat = MakeBeat(VfxFlickerMode.Breathe, 0.8f, 3f);
            try
            {
                beat.ConfigureStyle(0, false, 12f, 1f);
                // Two times inside the same 1/12s frame must give identical beats.
                float a = beat.EvaluateBeat(0.500f);
                float b = beat.EvaluateBeat(0.579f); // same floor(t*12) bucket
                Assert.That(a, Is.EqualTo(b).Within(1e-6f));
                float c = beat.EvaluateBeat(0.584f); // next bucket
                Assert.That(Mathf.Approximately(a, c), Is.False);
            }
            finally { Object.DestroyImmediate(beat.gameObject); }
        }

        [Test]
        public void IntensityQuantization_LimitsDistinctLevels()
        {
            var beat = MakeBeat(VfxFlickerMode.Breathe, 1f, 1f);
            try
            {
                beat.ConfigureStyle(3, true, 0f, 1f);
                var levels = new HashSet<float>();
                for (float t = 0f; t < 1f; t += 0.005f)
                    levels.Add(Mathf.Round(beat.EvaluateBeat(t) * 1000f) / 1000f);
                Assert.That(levels.Count, Is.LessThanOrEqualTo(4), "3-step quantization allows at most 4 beat levels");
            }
            finally { Object.DestroyImmediate(beat.gameObject); }
        }

        [Test]
        public void DecayProgress_DrivesBeatTowardsZero()
        {
            var beat = MakeBeat(VfxFlickerMode.Steady);
            try
            {
                beat.SetDecayProgress(0f);
                float full = beat.EvaluateBeat(1f);
                beat.SetDecayProgress(1f);
                float dead = beat.EvaluateBeat(1f);
                Assert.That(full, Is.EqualTo(1f).Within(1e-5f));
                Assert.That(dead, Is.LessThan(0.05f));
            }
            finally { Object.DestroyImmediate(beat.gameObject); }
        }

        [Test]
        public void Configure_SetsDriverKindAndTargets()
        {
            var go = new GameObject("beat");
            try
            {
                var beat = go.AddComponent<VfxLightBeat>();
                var rendererGo = new GameObject("r");
                rendererGo.transform.SetParent(go.transform);
                var renderer = rendererGo.AddComponent<MeshRenderer>();
                beat.Configure(VfxLightDriverKind.MaterialOnly, null, new Renderer[] { renderer }, 2);
                Assert.That(beat.Kind, Is.EqualTo(VfxLightDriverKind.MaterialOnly));
                Assert.That(beat.BeatChannel, Is.EqualTo(2));
                Assert.That(beat.BeatTargets, Has.Length.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

    /// <summary>VfxController v2: data-driven phase table, binary-search event dispatch, pool reset.</summary>
    public sealed class VfxControllerV2Tests
    {
        private static VfxController MakeController(params VfxController.PhaseEntry[] table)
        {
            var go = new GameObject("vfx");
            var controller = go.AddComponent<VfxController>();
            controller.ConfigurePhases(table);
            // Standard event table (sorted ordinal) -> handler indices.
            controller.ConfigureEvents(
                new[] { "break", "end", "impact", "launch", "setProgress", "setTravelPose", "travel" },
                new[] { 4, 3, 2, 0, 5, 6, 1 });
            return controller;
        }

        private static VfxController.PhaseEntry Phase(string id, float duration, bool loop = false, params int[] layers)
        {
            return new VfxController.PhaseEntry { id = id, duration = duration, loop = loop, activeLayers = layers };
        }

        [Test]
        public void PhaseTable_IsArbitrary_NotAFixedEnum()
        {
            var c = MakeController(Phase("sustain", -1f, true), Phase("end", 0.4f));
            try
            {
                Assert.That(c.Phases.Length, Is.EqualTo(2));
                Assert.That(c.CurrentPhaseIndex, Is.EqualTo(-1));
                c.Play();
                Assert.That(c.CurrentPhaseId, Is.EqualTo("sustain"));
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void SendEvent_UnknownId_ReturnsFalse_NoThrow()
        {
            var c = MakeController(Phase("launch", 0.5f));
            try
            {
                Assert.That(c.SendEvent("definitely_not_registered", default), Is.False);
                Assert.That(c.SendEvent(null, default), Is.False);
                Assert.That(c.SendEvent("", default), Is.False);
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void SendEvent_Launch_StartsPhaseZero_AndRaisesOutboundEvent()
        {
            var c = MakeController(Phase("launch", 0.5f), Phase("impact", 0.3f));
            try
            {
                int launches = 0;
                c.OnLaunch += _ => launches++;
                bool handled = c.SendEvent("launch", new VfxEventPayload { Position = new Vector3(1f, 2f, 3f) });
                Assert.That(handled, Is.True);
                Assert.That(c.CurrentPhaseId, Is.EqualTo("launch"));
                Assert.That(launches, Is.EqualTo(1));
                Assert.That(c.transform.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void SendEvent_Impact_AdvancesToImpactPhase()
        {
            var c = MakeController(Phase("launch", 0.5f), Phase("impact", 0.3f));
            try
            {
                c.Play();
                int impacts = 0;
                c.OnImpact += _ => impacts++;
                bool handled = c.SendEvent("impact", new VfxEventPayload { Position = Vector3.one });
                Assert.That(handled, Is.True);
                Assert.That(c.CurrentPhaseId, Is.EqualTo("impact"));
                Assert.That(impacts, Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void SendEvent_MissingPhase_ReturnsFalse()
        {
            var c = MakeController(Phase("launch", 0.5f)); // no impact phase in the table
            try
            {
                c.Play();
                Assert.That(c.SendEvent("travel", default), Is.False);
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void PhaseActivation_TogglesLayerNodes()
        {
            var c = MakeController(Phase("launch", 0.5f, false, 0), Phase("impact", 0.3f, false, 1));
            try
            {
                var layerA = new GameObject("layerA");
                var layerB = new GameObject("layerB");
                layerA.transform.SetParent(c.transform);
                layerB.transform.SetParent(c.transform);
                c.ConfigureLayers(new[] { layerA, layerB }, new Renderer[0]);
                c.Play();
                Assert.That(layerA.activeSelf, Is.True, "launch activates layer 0");
                Assert.That(layerB.activeSelf, Is.False);
                c.SendEvent("impact", default);
                Assert.That(layerA.activeSelf, Is.False, "impact deactivates layer 0");
                Assert.That(layerB.activeSelf, Is.True, "impact activates layer 1");
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void ResetForPool_RestoresTransform_AndPhaseState()
        {
            var c = MakeController(Phase("launch", 0.5f));
            try
            {
                Vector3 home = c.transform.localPosition;
                c.SendEvent("launch", new VfxEventPayload { Position = new Vector3(9f, 9f, 9f) });
                Assert.That(c.CurrentPhaseIndex, Is.EqualTo(0));
                c.ResetForPool();
                Assert.That(c.CurrentPhaseIndex, Is.EqualTo(-1));
                Assert.That(c.transform.localPosition, Is.EqualTo(home));
                Assert.That(c.IsAlive, Is.False);
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void ResetForPool_RestoresDebrisToKinematicInitialPose()
        {
            var c = MakeController(Phase("launch", 0.5f));
            try
            {
                var frag = new GameObject("frag");
                frag.transform.SetParent(c.transform);
                frag.transform.localPosition = new Vector3(0.5f, 0f, 0f);
                var body = frag.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                c.ConfigureDebris(new[] { body });

                c.SendEvent("break", new VfxEventPayload { Position = Vector3.zero, Value = 3f });
                Assert.That(body.isKinematic, Is.False, "break releases the rigidbody");
                frag.transform.localPosition = new Vector3(4f, -2f, 1f);

                c.ResetForPool();
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.useGravity, Is.False);
                Assert.That(frag.transform.localPosition, Is.EqualTo(new Vector3(0.5f, 0f, 0f)));
            }
            finally { Object.DestroyImmediate(c.gameObject); }
        }

        [Test]
        public void ParameterBlock_ResolveHandle_RoundTrips()
        {
            var go = new GameObject("pb");
            try
            {
                var pb = go.AddComponent<VfxParameterBlock>();
                pb.ConfigureCustom(new[] { "alpha", "integrity", "zeta" }, new[] { 0.1f, 1f, 0.5f });
                int handle = pb.Resolve("integrity");
                Assert.That(handle, Is.GreaterThanOrEqualTo(0));
                Assert.That(pb.SetFloat(handle, 0.25f), Is.True);
                Assert.That(pb.GetFloat(handle), Is.EqualTo(0.25f).Within(1e-6f));
                Assert.That(pb.Resolve("missing"), Is.EqualTo(-1));
                Assert.That(pb.SetFloat("missing", 1f), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ParameterBlock_DisabledLayerBinding_IsNoOp()
        {
            var go = new GameObject("pb");
            try
            {
                var pb = go.AddComponent<VfxParameterBlock>();
                pb.ConfigureCustom(new[] { "p" }, new[] { 0.5f });
                pb.ConfigureBindings(new[]
                {
                    new VfxParameterBlock.BindingEntry { paramIndex = 0, targetIndex = -1, bindingKeyId = 0 }
                }, new Renderer[0]);
                Assert.DoesNotThrow(() => pb.Apply(), "tier-disabled binding (targetIndex -1) must be a silent no-op");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
