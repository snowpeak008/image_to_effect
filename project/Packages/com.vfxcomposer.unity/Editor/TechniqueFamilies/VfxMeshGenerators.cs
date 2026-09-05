using System;
using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.Editor.TechniqueFamilies
{
    /// <summary>Uniform arguments for every generator (TECH_FAMILY_SPEC_MESH_LIGHT.md section 1.4).</summary>
    public struct VfxMeshGenArgs
    {
        public uint Seed;
        public bool Is3D;
        public int VertexBudget;

        public VfxMeshGenArgs(uint seed, bool is3D, int vertexBudget)
        {
            Seed = seed;
            Is3D = is3D;
            VertexBudget = vertexBudget;
        }
    }

    /// <summary>
    /// The 20 procedural mesh generators of the mesh technique family
    /// (TECH_FAMILY_SPEC_MESH_LIGHT.md section 4). All compile-time (editor
    /// assembly), all deterministic for a given (params, seed, dimension,
    /// budget), all honouring the vertex-budget clamp by reducing detail
    /// rather than throwing. Output contract: normals + uv0 + structural
    /// parametrization in uv2 + colors(r structural random, g boundary
    /// distance, b segment id, a cut-face flag).
    /// </summary>
    public static class VfxMeshGenerators
    {
        public static readonly string[] Ids =
        {
            "crystal_cluster", "rock_chunk", "tendril_blob", "shell_polyhedron", "wire_polyhedron",
            "jagged_polyline", "spiral_ribbon", "sweep_band", "catenary_band", "vine_spline",
            "branch_tree", "parabola_tube", "ring_torus_segments", "cylinder_beam", "radial_spike_array",
            "subdivided_plane", "tech_panel", "splash_crown", "voronoi_prefracture", "cloth_patch"
        };

        public static Mesh Generate(string id, VfxMeshGenArgs args)
        {
            switch (id)
            {
                case "crystal_cluster": return CrystalCluster(args);
                case "rock_chunk": return RockChunk(args);
                case "tendril_blob": return TendrilBlob(args);
                case "shell_polyhedron": return ShellPolyhedron(args);
                case "wire_polyhedron": return WirePolyhedron(args);
                case "jagged_polyline": return JaggedPolyline(args, Vector3.zero, new Vector3(0f, 2f, 0f));
                case "spiral_ribbon": return SpiralRibbon(args);
                case "sweep_band": return SweepBand(args);
                case "catenary_band": return CatenaryBand(args, new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f));
                case "vine_spline": return VineSpline(args);
                case "branch_tree": return BranchTree(args);
                case "parabola_tube": return ParabolaTube(args);
                case "ring_torus_segments": return RingTorusSegments(args);
                case "cylinder_beam": return CylinderBeam(args);
                case "radial_spike_array": return RadialSpikeArray(args);
                case "subdivided_plane": return SubdividedPlane(args);
                case "tech_panel": return TechPanel(args);
                case "splash_crown": return SplashCrown(args);
                case "voronoi_prefracture": return VoronoiPrefractureCombined(args);
                case "cloth_patch": return ClothPatch(args);
                default: throw new ArgumentException("Unknown mesh generator id: " + id);
            }
        }

        // ---------------------------------------------------------------- cluster / chunk

        public static Mesh CrystalCluster(VfxMeshGenArgs args, int shardCount = 8, int sides = 6,
            float lengthMin = 0.4f, float lengthMax = 1f, float radiusMin = 0.06f, float radiusMax = 0.14f,
            float tipRatio = 0.12f, float spreadCone = 55f, float baseRadius = 0.25f, float alignment = 0.6f)
        {
            // Budget clamp: verts per shard = sides * 2 (flat side quads use 4 each; approximate).
            int perShard = sides * 8;
            shardCount = Mathf.Clamp(shardCount, 1, Mathf.Max(1, args.VertexBudget / Mathf.Max(perShard, 1)));

            var rng = new VfxDeterministicRandom(args.Seed);
            var b = new VfxMeshBuilder();
            for (int s = 0; s < shardCount; s++)
            {
                Vector3 origin, dir;
                if (args.Is3D)
                {
                    origin = VfxHalton.FibonacciSphere(s, shardCount) * baseRadius;
                    origin.y = Mathf.Abs(origin.y) * 0.5f;
                    Vector3 random = rng.OnUnitSphere();
                    random.y = Mathf.Abs(random.y);
                    dir = Vector3.Slerp(random, Vector3.up, alignment).normalized;
                }
                else
                {
                    float ang = VfxHalton.Sample(s, 2) * Mathf.PI * 2f;
                    origin = new Vector3(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) * 0.4f, 0f) * baseRadius;
                    float spread = (rng.Next01() - 0.5f) * spreadCone * Mathf.Deg2Rad;
                    dir = new Vector3(Mathf.Sin(spread) * (1f - alignment), Mathf.Cos(spread), 0f).normalized;
                }
                float len = rng.Range(lengthMin, lengthMax);
                float radius = rng.Range(radiusMin, radiusMax);
                float shardId = shardCount > 1 ? s / (float)(shardCount - 1) : 0f;
                var color = new Color(rng.Next01(), 0f, shardId, 0f);

                Vector3 tip = origin + dir * len;
                Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(dir, side);
                int n = args.Is3D ? sides : 2;
                for (int k = 0; k < n; k++)
                {
                    float a0 = k / (float)n * Mathf.PI * 2f;
                    float a1 = (k + 1) / (float)n * Mathf.PI * 2f;
                    Vector3 r0 = (side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0)) * radius;
                    Vector3 r1 = (side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1)) * radius;
                    // Flat-shaded prism side: base edge -> tip edge (tipRatio shrink).
                    b.AddFlatQuad(
                        origin + r0, origin + r1,
                        tip + r1 * tipRatio, tip + r0 * tipRatio,
                        new Vector2(k / (float)n, 0f), new Vector2((k + 1f) / n, 1f),
                        new Vector2(0f, 0f), new Vector2(1f, 0f), color);
                }
            }
            return b.Build("crystal_cluster");
        }

        public static Mesh RockChunk(VfxMeshGenArgs args, int subdivisions = 1, float roughness = 0.35f,
            float lowFreqAmp = 0.25f, float flatnessBias = 0.3f)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            if (!args.Is3D)
            {
                // 2D: noisy convex polygon fan, flat -Z normals.
                int edges = Mathf.Clamp(8 + subdivisions * 3, 3, Mathf.Max(3, args.VertexBudget / 3));
                var fan = new VfxMeshBuilder();
                var pts = new Vector3[edges];
                for (int i = 0; i < edges; i++)
                {
                    float a = i / (float)edges * Mathf.PI * 2f;
                    float r = 1f + (rng.Next01() - 0.5f) * roughness;
                    pts[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                }
                for (int i = 0; i < edges; i++)
                {
                    var color = new Color(rng.Next01(), 1f, i / (float)edges, 0f);
                    fan.AddFlatTriangle(Vector3.zero, pts[i], pts[(i + 1) % edges],
                        new Vector2(0.5f, 0.5f), new Vector2(i / (float)edges, 0f), color);
                }
                return fan.Build("rock_chunk");
            }

            // 3D: flat-shaded displaced icosphere. Budget clamp on subdivision level.
            while (subdivisions > 0 && 20 * Mathf.Pow(4, subdivisions) * 3 > args.VertexBudget) subdivisions--;
            List<Vector3[]> tris = IcosphereTriangles(Mathf.Max(subdivisions, 0));
            Vector3 flattenAxis = rng.OnUnitSphere();
            float seedOffset = rng.Range(0f, 100f);
            var b = new VfxMeshBuilder();
            foreach (var tri in tris)
            {
                var displaced = new Vector3[3];
                for (int i = 0; i < 3; i++)
                {
                    Vector3 v = tri[i];
                    float noise = ValueNoise3(v * 2.1f + Vector3.one * seedOffset);
                    float high = ValueNoise3(v * 6.3f + Vector3.one * (seedOffset + 31f));
                    v *= 1f + (noise - 0.5f) * lowFreqAmp * 2f + (high - 0.5f) * roughness * 0.6f;
                    // flatnessBias: squash along a random axis.
                    float d = Vector3.Dot(v, flattenAxis);
                    v -= flattenAxis * d * flatnessBias;
                    displaced[i] = v;
                }
                float g = ((displaced[0] + displaced[1] + displaced[2]) / 3f).magnitude;
                var color = new Color(0f, Mathf.Clamp01(g), 0f, 0f);
                b.AddFlatTriangle(displaced[0], displaced[1], displaced[2],
                    new Vector2(0.5f, 0.5f), new Vector2(g, 0f), color);
            }
            return b.Build("rock_chunk");
        }

        public static Mesh TendrilBlob(VfxMeshGenArgs args, int tendrilCount = 6, float bodyRadius = 0.4f,
            float tendrilLengthMin = 0.5f, float tendrilLengthMax = 0.9f, int segmentsPerTendril = 5,
            float thickness = 0.09f, float curl = 40f, float taper = 0.7f)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            var b = new VfxMeshBuilder();
            int ringVerts = args.Is3D ? 6 : 2;
            int bodyVerts = args.Is3D ? 56 : 18;
            int perTendril = segmentsPerTendril * ringVerts + ringVerts;
            tendrilCount = Mathf.Clamp(tendrilCount, 1,
                Mathf.Max(1, (args.VertexBudget - bodyVerts) / Mathf.Max(perTendril, 1)));

            // Body: low-res sphere (3D) or fan disc (2D).
            if (args.Is3D) AppendUvSphere(b, bodyRadius, 7, 4, new Color(0f, 0f, 0f, 0f));
            else AppendDisc(b, bodyRadius, 16, new Color(0f, 0f, 0f, 0f));

            for (int t = 0; t < tendrilCount; t++)
            {
                Vector3 dir = args.Is3D
                    ? VfxHalton.FibonacciSphere(t, tendrilCount)
                    : new Vector3(Mathf.Cos(t / (float)tendrilCount * Mathf.PI * 2f), Mathf.Sin(t / (float)tendrilCount * Mathf.PI * 2f), 0f);
                float len = rng.Range(tendrilLengthMin, tendrilLengthMax);
                float tendrilId = tendrilCount > 1 ? t / (float)(tendrilCount - 1) : 0f;
                Vector3 curlAxis = args.Is3D ? rng.OnUnitSphere() : Vector3.forward;
                var path = new Vector3[segmentsPerTendril + 1];
                Vector3 pos = dir * bodyRadius * 0.95f;
                Vector3 step = dir;
                for (int s = 0; s <= segmentsPerTendril; s++)
                {
                    path[s] = pos;
                    step = Quaternion.AngleAxis(curl / segmentsPerTendril, curlAxis) * step;
                    pos += step * (len / segmentsPerTendril);
                }
                AppendTube(b, path, thickness, thickness * (1f - taper), ringVerts, args.Is3D,
                    new Color(rng.Next01(), 0f, tendrilId, 0f));
            }
            return b.Build("tendril_blob");
        }

        public static Mesh ShellPolyhedron(VfxMeshGenArgs args, int subdivisions = 2, bool facetMode = false,
            float openTop = 0f, float radius = 1f)
        {
            if (!args.Is3D)
            {
                // 2D: annulus band; openTop clips an arc.
                int segs = Mathf.Clamp(32, 8, Mathf.Max(8, args.VertexBudget / 2 - 1));
                return AnnulusBand(args, radius * 0.85f, radius, segs, openTop);
            }
            int budgetForFacet = facetMode ? 3 : 1;
            while (subdivisions > 0 && 20 * Mathf.Pow(4, subdivisions) * budgetForFacet > args.VertexBudget) subdivisions--;
            List<Vector3[]> tris = IcosphereTriangles(Mathf.Max(subdivisions, 0));
            var b = new VfxMeshBuilder();
            float clipY = Mathf.Cos(openTop * Mathf.PI); // polar clip
            var shared = new Dictionary<Vector3, int>();
            int faceIndex = 0;
            foreach (var tri in tris)
            {
                Vector3 c = (tri[0] + tri[1] + tri[2]) / 3f;
                if (openTop > 0f && c.normalized.y > clipY) continue;
                float faceId = faceIndex / (float)Mathf.Max(tris.Count - 1, 1);
                faceIndex++;
                if (facetMode)
                {
                    var color = new Color(0f, 0f, faceId, 0f);
                    b.AddFlatTriangle(tri[0] * radius, tri[1] * radius, tri[2] * radius,
                        SphereUv(c), new Vector2(SphereUv(c).y, SphereUv(c).x), color);
                }
                else
                {
                    var idx = new int[3];
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 v = tri[i];
                        if (!shared.TryGetValue(v, out int vi))
                        {
                            Vector2 uv = SphereUv(v);
                            vi = b.AddVertex(v * radius, v, uv, new Vector2(uv.y, uv.x), new Color(0f, 0f, faceId, 0f));
                            shared[v] = vi;
                        }
                        idx[i] = vi;
                    }
                    b.AddTriangle(idx[0], idx[1], idx[2]);
                }
            }
            return b.Build("shell_polyhedron");
        }

        public static Mesh WirePolyhedron(VfxMeshGenArgs args, int solid = 2, float wireRadius = 0.035f, int wireSides = 4)
        {
            GetPlatonic(solid, out Vector3[] verts, out int[][] edges);
            if (!args.Is3D)
            {
                // 2D: regular N-gon wireframe of band quads.
                int n = Mathf.Clamp(verts.Length, 3, 12);
                var b2 = new VfxMeshBuilder();
                for (int i = 0; i < n; i++)
                {
                    float a0 = i / (float)n * Mathf.PI * 2f;
                    float a1 = (i + 1) / (float)n * Mathf.PI * 2f;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f);
                    Vector3 p1 = new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f);
                    Vector3 side = Vector3.Cross((p1 - p0).normalized, Vector3.forward) * wireRadius;
                    var color = new Color(0f, 0f, i / (float)n, 0f);
                    b2.AddFlatQuad(p0 - side, p0 + side, p1 + side, p1 - side,
                        Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(1f, 0f), color);
                }
                return b2.Build("wire_polyhedron");
            }
            int perEdge = wireSides * 8;
            while (wireSides > 3 && edges.Length * perEdge > args.VertexBudget) { wireSides--; perEdge = wireSides * 8; }
            var b = new VfxMeshBuilder();
            for (int e = 0; e < edges.Length; e++)
            {
                Vector3 p0 = verts[edges[e][0]];
                Vector3 p1 = verts[edges[e][1]];
                var color = new Color(0f, 0f, edges.Length > 1 ? e / (float)(edges.Length - 1) : 0f, 0f);
                AppendTube(b, new[] { p0, p1 }, wireRadius, wireRadius, wireSides, true, color);
            }
            return b.Build("wire_polyhedron");
        }

        // ---------------------------------------------------------------- lines / bands

        public static Mesh JaggedPolyline(VfxMeshGenArgs args, Vector3 start, Vector3 end, int segments = 16,
            float jitter = 0.35f, int branchDepth = 2, float branchProbability = 0.55f,
            float branchLengthRatio = 0.45f, float bandWidth = 0.07f)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            // Budget: main line segments*4 verts + branches; clamp segments.
            segments = Mathf.Clamp(segments, 2, Mathf.Max(2, args.VertexBudget / 8));
            var b = new VfxMeshBuilder();
            BuildBoltRecursive(b, ref rng, start, end, segments, jitter, branchDepth, branchProbability,
                branchLengthRatio, bandWidth, args.Is3D, 0f, 1f, 0, args.VertexBudget);
            return b.Build("jagged_polyline");
        }

        private static void BuildBoltRecursive(VfxMeshBuilder b, ref VfxDeterministicRandom rng,
            Vector3 start, Vector3 end, int segments, float jitter, int depthRemaining, float branchProbability,
            float branchLengthRatio, float bandWidth, bool is3D, float arcStart, float arcSpan, int depth, int budget)
        {
            if (b.VertexCount >= budget) return;
            // Midpoint displacement path.
            var pts = new List<Vector3> { start, end };
            int levels = Mathf.CeilToInt(Mathf.Log(Mathf.Max(segments, 2), 2f));
            Vector3 axis = (end - start).normalized;
            for (int level = 0; level < levels; level++)
            {
                float amp = jitter * Mathf.Pow(0.5f, level) * (end - start).magnitude * 0.5f;
                var next = new List<Vector3>();
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    next.Add(pts[i]);
                    Vector3 mid = (pts[i] + pts[i + 1]) * 0.5f;
                    Vector3 perp = is3D
                        ? Vector3.Cross(axis, rng.OnUnitSphere()).normalized
                        : Vector3.Cross(axis, Vector3.forward).normalized;
                    float endFade = Mathf.Sin((i + 0.5f) / (pts.Count - 1) * Mathf.PI); // anchor endpoints
                    mid += perp * (rng.Next01() - 0.5f) * 2f * amp * endFade;
                    next.Add(mid);
                }
                next.Add(pts[pts.Count - 1]);
                pts = next;
            }
            // Band expansion in a fixed plane.
            float depthNorm = depth / 3f;
            var color = new Color(rng.Next01(), 0f, depthNorm, 0f);
            AppendBand(b, pts, bandWidth * Mathf.Pow(0.6f, depth), is3D, arcStart, arcSpan, color);

            if (depthRemaining <= 0) return;
            for (int i = 1; i < pts.Count - 1; i++)
            {
                if (rng.Next01() > branchProbability * 0.35f) continue;
                if (b.VertexCount >= budget) return;
                float u = i / (float)(pts.Count - 1);
                Vector3 branchDir = is3D
                    ? Vector3.Slerp(axis, rng.OnUnitSphere(), 0.55f).normalized
                    : (Quaternion.AngleAxis(rng.Range(20f, 55f) * (rng.Next01() > 0.5f ? 1f : -1f), Vector3.forward) * axis);
                float branchLen = (end - start).magnitude * branchLengthRatio;
                BuildBoltRecursive(b, ref rng, pts[i], pts[i] + branchDir * branchLen,
                    Mathf.Max(segments / 2, 2), jitter, depthRemaining - 1, branchProbability,
                    branchLengthRatio, bandWidth, is3D,
                    arcStart + u * arcSpan, arcSpan * branchLengthRatio, depth + 1, budget);
            }
        }

        public static Mesh SpiralRibbon(VfxMeshGenArgs args, float turns = 3f, float radiusStart = 0.6f,
            float radiusEnd = 0.25f, float heightStart = 0f, float heightEnd = 1.4f, float ribbonWidth = 0.16f,
            int segmentsPerTurn = 16)
        {
            int total = Mathf.Clamp(Mathf.RoundToInt(turns * segmentsPerTurn), 4, Mathf.Max(4, args.VertexBudget / 2 - 1));
            var pts = new List<Vector3>(total + 1);
            for (int i = 0; i <= total; i++)
            {
                float u = i / (float)total;
                float ang = u * turns * Mathf.PI * 2f;
                float r = Mathf.Lerp(radiusStart, radiusEnd, u);
                float h = args.Is3D ? Mathf.Lerp(heightStart, heightEnd, u) : 0f;
                float r2 = args.Is3D ? r : Mathf.Lerp(radiusStart, radiusEnd * 0.3f, u); // 2D: Archimedean
                pts.Add(new Vector3(Mathf.Cos(ang) * r2, args.Is3D ? h : Mathf.Sin(ang) * r2, args.Is3D ? Mathf.Sin(ang) * r : 0f));
            }
            var b = new VfxMeshBuilder();
            AppendBand(b, pts, ribbonWidth, args.Is3D, 0f, 1f, new Color(0f, 0f, 0f, 0f));
            return b.Build("spiral_ribbon");
        }

        public static Mesh SweepBand(VfxMeshGenArgs args, float arcAngle = 150f, float innerRadius = 0.55f,
            float outerRadius = 1f, int segments = 32, int layerCount = 2)
        {
            int perLayer = (segments + 1) * 2;
            layerCount = Mathf.Clamp(layerCount, 1, Mathf.Max(1, args.VertexBudget / perLayer));
            segments = Mathf.Clamp(segments, 4, Mathf.Max(4, args.VertexBudget / (2 * layerCount) - 1));
            if (!args.Is3D) layerCount = Mathf.Min(layerCount, 3);
            var b = new VfxMeshBuilder();
            for (int layer = 0; layer < layerCount; layer++)
            {
                float phase = layer * 8f * Mathf.Deg2Rad;
                float shrink = 1f - layer * 0.06f;
                float z = args.Is3D ? layer * 0.02f : 0f;
                float layerId = layerCount > 1 ? layer / (float)(layerCount - 1) : 0f;
                int prevInner = -1, prevOuter = -1;
                for (int i = 0; i <= segments; i++)
                {
                    float u = i / (float)segments;
                    float ang = (u - 0.5f) * arcAngle * Mathf.Deg2Rad + phase;
                    // lens profile: widest mid-arc.
                    float widthMul = Mathf.Sin(u * Mathf.PI) * 0.5f + 0.5f;
                    float ri = Mathf.Lerp(outerRadius, innerRadius, widthMul) * shrink;
                    float ro = outerRadius * shrink;
                    Vector3 dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                    Vector3 normal = args.Is3D ? Vector3.up : Vector3.back;
                    var color = new Color(0f, 0f, layerId, 0f);
                    Vector3 pi = args.Is3D ? new Vector3(dir.x * ri, z, dir.y * ri) : dir * ri + Vector3.forward * z;
                    Vector3 po = args.Is3D ? new Vector3(dir.x * ro, z, dir.y * ro) : dir * ro + Vector3.forward * z;
                    int vi = b.AddVertex(pi, normal, new Vector2(u, 0f), new Vector2(u, 0f), color);
                    int vo = b.AddVertex(po, normal, new Vector2(u, 1f), new Vector2(u, 1f), color);
                    if (i > 0) b.AddQuad(prevInner, prevOuter, vo, vi);
                    prevInner = vi;
                    prevOuter = vo;
                }
            }
            return b.Build("sweep_band");
        }

        public static Mesh CatenaryBand(VfxMeshGenArgs args, Vector3 pointA, Vector3 pointB, float sag = 0.35f,
            int segments = 24, float bandWidth = 0.08f, float slackNoise = 0.1f)
        {
            segments = Mathf.Clamp(segments, 2, Mathf.Max(2, args.VertexBudget / 2 - 1));
            var rng = new VfxDeterministicRandom(args.Seed);
            var pts = new List<Vector3>(segments + 1);
            float span = Vector3.Distance(pointA, pointB);
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                Vector3 p = Vector3.Lerp(pointA, pointB, u);
                // Quadratic sag approximation of a catenary; exact anchoring at ends.
                float drop = 4f * sag * span * u * (1f - u);
                p.y -= drop;
                float endFade = Mathf.Sin(u * Mathf.PI);
                p += new Vector3(0f, (ValueNoise1(u * 5f + args.Seed % 97) - 0.5f) * slackNoise * endFade, 0f);
                pts.Add(p);
            }
            var b = new VfxMeshBuilder();
            AppendBand(b, pts, bandWidth, args.Is3D, 0f, 1f, new Color(rng.Next01(), 0f, 0f, 0f));
            return b.Build("catenary_band");
        }

        public static Mesh VineSpline(VfxMeshGenArgs args, int segments = 32, float tubeRadius = 0.05f,
            int tubeSides = 5, float taper = 0.6f, int leafCount = 12, float leafSize = 0.16f,
            float wanderAmplitude = 0.35f)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            int ringVerts = args.Is3D ? tubeSides : 2;
            int tubeBudget = args.VertexBudget - leafCount * 4;
            segments = Mathf.Clamp(segments, 3, Mathf.Max(3, tubeBudget / ringVerts - 1));
            var path = new Vector3[segments + 1];
            Vector3 start = Vector3.zero, end = new Vector3(0f, 1.6f, 0f);
            float phase = rng.Range(0f, 10f);
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                Vector3 p = Vector3.Lerp(start, end, u);
                float fade = Mathf.Sin(u * Mathf.PI);
                p.x += (ValueNoise1(u * 3f + phase) - 0.5f) * 2f * wanderAmplitude * fade;
                if (args.Is3D) p.z += (ValueNoise1(u * 3f + phase + 47f) - 0.5f) * 2f * wanderAmplitude * fade;
                path[i] = p;
            }
            var b = new VfxMeshBuilder();
            AppendTube(b, path, tubeRadius, tubeRadius * (1f - taper), ringVerts, args.Is3D, new Color(0f, 0f, 0f, 0f));
            // Leaves: single quads along the spline; leaf shape comes from the material SDF.
            for (int l = 0; l < leafCount && b.VertexCount + 4 <= args.VertexBudget; l++)
            {
                float u = VfxHalton.Sample(l, 2) * 0.8f + 0.1f;
                int idx = Mathf.Clamp(Mathf.RoundToInt(u * segments), 1, segments - 1);
                Vector3 at = path[idx];
                Vector3 tangent = (path[idx + 1] - path[idx - 1]).normalized;
                Vector3 outDir = args.Is3D
                    ? (Quaternion.AngleAxis(rng.Range(0f, 360f), tangent) * Vector3.Cross(tangent, Vector3.up).normalized)
                    : (rng.Next01() > 0.5f ? Vector3.right : Vector3.left);
                Vector3 side = Vector3.Cross(outDir, tangent).normalized * leafSize * 0.5f;
                Vector3 baseP = at + outDir * tubeRadius;
                Vector3 tipP = baseP + outDir * leafSize;
                var color = new Color(rng.Next01(), 0f, l / Mathf.Max(leafCount - 1f, 1f), 0f);
                b.AddFlatQuad(baseP - side, baseP + side, tipP + side, tipP - side,
                    Vector2.zero, Vector2.one, new Vector2(u, 0f), new Vector2(u, 1f), color);
            }
            return b.Build("vine_spline");
        }

        public static Mesh BranchTree(VfxMeshGenArgs args, int depth = 3, int branchesPerNode = 2,
            float lengthRatio = 0.62f, float radiusRatio = 0.62f, float angleSpread = 32f,
            float rootRadius = 0.07f, float rootLength = 0.7f, int tubeSides = 4, float planarBias = 0.2f)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            var b = new VfxMeshBuilder();
            if (!args.Is3D) planarBias = 1f;
            int ringVerts = args.Is3D ? tubeSides : 2;
            GrowBranch(b, ref rng, Vector3.zero, Vector3.up, rootLength, rootRadius, depth, branchesPerNode,
                lengthRatio, radiusRatio, angleSpread, ringVerts, args.Is3D, planarBias, 0f, 1f, 0, depth,
                args.VertexBudget);
            return b.Build("branch_tree");
        }

        private static void GrowBranch(VfxMeshBuilder b, ref VfxDeterministicRandom rng, Vector3 origin,
            Vector3 dir, float length, float radius, int depthRemaining, int branchesPerNode, float lengthRatio,
            float radiusRatio, float angleSpread, int ringVerts, bool is3D, float planarBias,
            float arcStart, float arcSpan, int depth, int totalDepth, int budget)
        {
            if (b.VertexCount + ringVerts * 2 > budget) return;
            Vector3 endP = origin + dir * length;
            float depthNorm = totalDepth > 0 ? depth / (float)totalDepth : 0f;
            AppendTube(b, new[] { origin, endP }, radius, radius * radiusRatio, ringVerts, is3D,
                new Color(rng.Next01(), 0f, depthNorm, 0f));
            if (depthRemaining <= 0) return;
            for (int c = 0; c < branchesPerNode; c++)
            {
                Vector3 childDir;
                if (is3D)
                {
                    Vector3 jitterAxis = VfxHalton.FibonacciSphere(c + depth * branchesPerNode, branchesPerNode * (totalDepth + 1));
                    childDir = Vector3.Slerp(dir, jitterAxis, angleSpread / 90f).normalized;
                    childDir = Vector3.Lerp(childDir, new Vector3(childDir.x, childDir.y, 0f).normalized, planarBias).normalized;
                }
                else
                {
                    float sign = c % 2 == 0 ? 1f : -1f;
                    childDir = Quaternion.AngleAxis(angleSpread * sign * (0.6f + rng.Next01() * 0.7f), Vector3.forward) * dir;
                }
                GrowBranch(b, ref rng, endP, childDir, length * lengthRatio, radius * radiusRatio,
                    depthRemaining - 1, branchesPerNode, lengthRatio, radiusRatio, angleSpread, ringVerts,
                    is3D, planarBias, arcStart + arcSpan * c / branchesPerNode, arcSpan / branchesPerNode,
                    depth + 1, totalDepth, budget);
            }
        }

        public static Mesh ParabolaTube(VfxMeshGenArgs args, float speed = 2.2f, float angleDeg = 55f,
            float gravity = 9.81f, float duration = 0.55f, int segments = 24, float radiusStart = 0.08f,
            float radiusEnd = 0.045f, int tubeSides = 6)
        {
            int ringVerts = args.Is3D ? tubeSides : 2;
            segments = Mathf.Clamp(segments, 3, Mathf.Max(3, args.VertexBudget / ringVerts - 1));
            Vector3 v0 = new Vector3(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad), 0f) * speed;
            var path = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments * duration;
                path[i] = new Vector3(v0.x * t, v0.y * t - 0.5f * gravity * t * t, 0f);
            }
            var b = new VfxMeshBuilder();
            AppendTube(b, path, radiusStart, radiusEnd, ringVerts, args.Is3D, new Color(0f, 0f, 0f, 0f));
            return b.Build("parabola_tube");
        }

        // ---------------------------------------------------------------- rings / columns

        public static Mesh RingTorusSegments(VfxMeshGenArgs args, float majorRadius = 1f, float minorRadius = 0.08f,
            int majorSegments = 32, int minorSegments = 6, int segmentCount = 1, float segmentGap = 0.08f,
            bool flatRing = false)
        {
            if (!args.Is3D) flatRing = true;
            if (flatRing)
            {
                majorSegments = Mathf.Clamp(majorSegments, 8, Mathf.Max(8, args.VertexBudget / 2 - 1));
                var bFlat = new VfxMeshBuilder();
                AppendFlatRingSegments(bFlat, majorRadius - minorRadius * 2f, majorRadius, majorSegments, segmentCount, segmentGap, args.Is3D);
                return bFlat.Build("ring_torus_segments");
            }
            while (minorSegments > 3 && (majorSegments + segmentCount) * (minorSegments + 1) > args.VertexBudget) minorSegments--;
            while (majorSegments > 8 && (majorSegments + segmentCount) * (minorSegments + 1) > args.VertexBudget) majorSegments -= 4;
            var b = new VfxMeshBuilder();
            segmentCount = Mathf.Max(segmentCount, 1);
            float segArc = Mathf.PI * 2f / segmentCount;
            float gapArc = segArc * segmentGap;
            int stepsPerSeg = Mathf.Max(majorSegments / segmentCount, 2);
            for (int seg = 0; seg < segmentCount; seg++)
            {
                float a0 = seg * segArc + gapArc * 0.5f;
                float a1 = (seg + 1) * segArc - gapArc * 0.5f;
                float segId = segmentCount > 1 ? seg / (float)(segmentCount - 1) : 0f;
                var color = new Color(0f, 0f, segId, 0f);
                int[] prevRing = null;
                for (int i = 0; i <= stepsPerSeg; i++)
                {
                    float ang = Mathf.Lerp(a0, a1, i / (float)stepsPerSeg);
                    Vector3 centre = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * majorRadius;
                    Vector3 radial = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    var ring = new int[minorSegments + 1];
                    for (int m = 0; m <= minorSegments; m++)
                    {
                        float ma = m / (float)minorSegments * Mathf.PI * 2f;
                        Vector3 n = radial * Mathf.Cos(ma) + Vector3.up * Mathf.Sin(ma);
                        ring[m] = b.AddVertex(centre + n * minorRadius, n,
                            new Vector2(ang / (Mathf.PI * 2f), m / (float)minorSegments),
                            new Vector2(ang / (Mathf.PI * 2f), 0f), color);
                    }
                    if (prevRing != null)
                        for (int m = 0; m < minorSegments; m++)
                            b.AddQuad(prevRing[m], prevRing[m + 1], ring[m + 1], ring[m]);
                    prevRing = ring;
                }
            }
            return b.Build("ring_torus_segments");
        }

        public static Mesh CylinderBeam(VfxMeshGenArgs args, float length = 1.6f, float radiusStart = 0.22f,
            float radiusEnd = 0.16f, int radialSegments = 12, int heightSegments = 8, int crossSection = 0)
        {
            if (!args.Is3D)
            {
                // 2D: banded rectangle with height subdivisions (bendable).
                heightSegments = Mathf.Clamp(heightSegments, 1, Mathf.Max(1, args.VertexBudget / 2 - 1));
                var b2 = new VfxMeshBuilder();
                int prevL = -1, prevR = -1;
                for (int h = 0; h <= heightSegments; h++)
                {
                    float v = h / (float)heightSegments;
                    float r = Mathf.Lerp(radiusStart, radiusEnd, v);
                    var color = new Color(0f, 0f, 0f, 0f);
                    int l = b2.AddVertex(new Vector3(-r, v * length, 0f), Vector3.back, new Vector2(0f, v), new Vector2(0f, v), color);
                    int rr = b2.AddVertex(new Vector3(r, v * length, 0f), Vector3.back, new Vector2(1f, v), new Vector2(1f, v), color);
                    if (h > 0) b2.AddQuad(prevL, prevR, rr, l);
                    prevL = l;
                    prevR = rr;
                }
                return b2.Build("cylinder_beam");
            }
            while (radialSegments > 6 && radialSegments * (heightSegments + 1) > args.VertexBudget) radialSegments -= 2;
            while (heightSegments > 1 && radialSegments * (heightSegments + 1) > args.VertexBudget) heightSegments--;
            int sides = crossSection == 1 ? 6 : crossSection == 2 ? 4 : radialSegments;
            var b = new VfxMeshBuilder();
            int[] prevRing = null;
            for (int h = 0; h <= heightSegments; h++)
            {
                float v = h / (float)heightSegments;
                float r = Mathf.Lerp(radiusStart, radiusEnd, v);
                var ring = new int[sides + 1];
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    ring[i] = b.AddVertex(n * r + Vector3.up * (v * length), n,
                        new Vector2(i / (float)sides, v), new Vector2(i / (float)sides, v),
                        new Color(0f, 0f, 0f, 0f));
                }
                if (prevRing != null)
                    for (int i = 0; i < sides; i++)
                        b.AddQuad(prevRing[i], prevRing[i + 1], ring[i + 1], ring[i]);
                prevRing = ring;
            }
            return b.Build("cylinder_beam");
        }

        /// <summary>ADR-010 section 4bis-8: radial spike/needle array with bimodal length option.</summary>
        public static Mesh RadialSpikeArray(VfxMeshGenArgs args, int spikeCount = 32, float lengthMin = 0.4f,
            float lengthMax = 1.2f, float widthMin = 0.02f, float widthMax = 0.06f, float taper = 0.85f,
            float angleJitter = 0.35f, float coneAngle = 180f, float originRadius = 0.05f,
            int lengthDistribution = 1, bool doubleQuad = false)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            int perSpike = 4 * (doubleQuad && args.Is3D ? 2 : 1);
            spikeCount = Mathf.Clamp(spikeCount, 1, Mathf.Max(1, args.VertexBudget / perSpike));
            var b = new VfxMeshBuilder();
            for (int s = 0; s < spikeCount; s++)
            {
                Vector3 dir;
                if (args.Is3D)
                {
                    Vector3 baseDir = VfxHalton.FibonacciSphere(s, spikeCount);
                    if (coneAngle < 180f)
                    {
                        // Constrain to a cone about +Y.
                        float cosLimit = Mathf.Cos(coneAngle * Mathf.Deg2Rad * 0.5f);
                        baseDir.y = Mathf.Abs(baseDir.y);
                        baseDir = Vector3.Slerp(Vector3.up, baseDir, 1f - cosLimit).normalized;
                    }
                    dir = Vector3.Slerp(baseDir, rng.OnUnitSphere(), angleJitter * 0.5f).normalized;
                }
                else
                {
                    float baseAng = s / (float)spikeCount * Mathf.PI * 2f;
                    baseAng += (rng.Next01() - 0.5f) * angleJitter * (Mathf.PI * 2f / spikeCount);
                    dir = new Vector3(Mathf.Cos(baseAng), Mathf.Sin(baseAng), 0f);
                }

                // bimodal: 20% take upper-quartile lengths, 80% take the lower half.
                float lenT;
                if (lengthDistribution == 1)
                    lenT = rng.Next01() < 0.2f ? rng.Range(0.75f, 1f) : rng.Range(0f, 0.5f);
                else if (lengthDistribution == 2)
                    lenT = Mathf.Pow(rng.Next01(), 2.2f);
                else
                    lenT = rng.Next01();
                float len = Mathf.Lerp(lengthMin, lengthMax, lenT);
                float rootW = rng.Range(widthMin, widthMax);
                float tipW = rootW * (1f - taper);

                Vector3 sideRef = args.Is3D
                    ? (Mathf.Abs(dir.y) > 0.95f ? Vector3.right : Vector3.up)
                    : Vector3.forward;
                Vector3 side = Vector3.Cross(dir, sideRef).normalized;
                Vector3 p0 = dir * originRadius;
                Vector3 p1 = dir * (originRadius + len);
                float spikeId = spikeCount > 1 ? s / (float)(spikeCount - 1) : 0f;
                var color = new Color(lenT, 0f, spikeId, 0f);
                b.AddFlatQuad(p0 - side * rootW * 0.5f, p0 + side * rootW * 0.5f,
                    p1 + side * tipW * 0.5f, p1 - side * tipW * 0.5f,
                    Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(1f, 0f), color);
                if (doubleQuad && args.Is3D)
                {
                    Vector3 side2 = Vector3.Cross(dir, side).normalized;
                    b.AddFlatQuad(p0 - side2 * rootW * 0.5f, p0 + side2 * rootW * 0.5f,
                        p1 + side2 * tipW * 0.5f, p1 - side2 * tipW * 0.5f,
                        Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(1f, 0f), color);
                }
            }
            return b.Build("radial_spike_array");
        }

        // ---------------------------------------------------------------- planes / panels

        public static Mesh SubdividedPlane(VfxMeshGenArgs args, float sizeX = 2f, float sizeY = 2f,
            int subdivX = 16, int subdivY = 16, int shape = 0)
        {
            while ((subdivX + 1) * (subdivY + 1) > args.VertexBudget && (subdivX > 1 || subdivY > 1))
            {
                if (subdivX >= subdivY) subdivX--; else subdivY--;
            }
            var b = new VfxMeshBuilder();
            var grid = new int[subdivX + 1, subdivY + 1];
            for (int y = 0; y <= subdivY; y++)
            {
                for (int x = 0; x <= subdivX; x++)
                {
                    float u = x / (float)subdivX;
                    float v = y / (float)subdivY;
                    Vector3 p = args.Is3D
                        ? new Vector3((u - 0.5f) * sizeX, 0f, (v - 0.5f) * sizeY)
                        : new Vector3((u - 0.5f) * sizeX, (v - 0.5f) * sizeY, 0f);
                    Vector3 n = args.Is3D ? Vector3.up : Vector3.back;
                    float boundary = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 2f;
                    grid[x, y] = b.AddVertex(p, n, new Vector2(u, v), new Vector2(u, v),
                        new Color(0f, Mathf.Clamp01(boundary), 0f, 0f));
                }
            }
            for (int y = 0; y < subdivY; y++)
            {
                for (int x = 0; x < subdivX; x++)
                {
                    if (shape == 1)
                    {
                        // disc clip by quad centre
                        float cu = (x + 0.5f) / subdivX - 0.5f;
                        float cv = (y + 0.5f) / subdivY - 0.5f;
                        if (cu * cu + cv * cv > 0.25f) continue;
                    }
                    b.AddQuad(grid[x, y], grid[x + 1, y], grid[x + 1, y + 1], grid[x, y + 1]);
                }
            }
            return b.Build("subdivided_plane");
        }

        public static Mesh TechPanel(VfxMeshGenArgs args, int gridType = 0, int cols = 8, int rows = 8,
            float cellGap = 0.06f, float curvature = 0f, float missingRatio = 0.08f)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            int perCell = gridType == 1 ? 7 : 4;
            while (cols * rows * perCell > args.VertexBudget && (cols > 1 || rows > 1))
            {
                if (cols >= rows) cols--; else rows--;
            }
            var b = new VfxMeshBuilder();
            float cellW = 2f / cols;
            float cellH = 2f / rows;
            int total = cols * rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    if (rng.Next01() < missingRatio) continue;
                    int cellIndex = y * cols + x;
                    float cellId = total > 1 ? cellIndex / (float)(total - 1) : 0f;
                    float cx = -1f + (x + 0.5f) * cellW + (gridType == 1 && y % 2 == 1 ? cellW * 0.5f : 0f);
                    float cy = -1f + (y + 0.5f) * cellH;
                    float centreDist = Mathf.Clamp01(new Vector2(cx, cy).magnitude / 1.4142f);
                    var color = new Color(centreDist, 0f, cellId, 0f);
                    float hw = (cellW - cellGap) * 0.5f;
                    float hh = (cellH - cellGap) * 0.5f;
                    Vector3 Project(float px, float py)
                    {
                        Vector3 flat = args.Is3D ? new Vector3(px, 0f, py) : new Vector3(px, py, 0f);
                        if (args.Is3D && curvature > 0f)
                        {
                            float d2 = px * px + py * py;
                            flat.y = -d2 * curvature * 0.4f;
                        }
                        return flat;
                    }
                    if (gridType == 1)
                    {
                        // hex cell: 6-triangle fan around the centre.
                        Vector3 centre = Project(cx, cy);
                        float hr = Mathf.Min(hw, hh);
                        var ring = new Vector3[6];
                        for (int k = 0; k < 6; k++)
                        {
                            float a = (k / 6f + 1f / 12f) * Mathf.PI * 2f;
                            ring[k] = Project(cx + Mathf.Cos(a) * hr, cy + Mathf.Sin(a) * hr);
                        }
                        for (int k = 0; k < 6; k++)
                            b.AddFlatTriangle(centre, ring[k], ring[(k + 1) % 6],
                                new Vector2(0.5f, 0.5f), new Vector2(cellId, 0f), color);
                    }
                    else
                    {
                        b.AddFlatQuad(Project(cx - hw, cy - hh), Project(cx + hw, cy - hh),
                            Project(cx + hw, cy + hh), Project(cx - hw, cy + hh),
                            Vector2.zero, Vector2.one, new Vector2(cellId, 0f), new Vector2(cellId, 1f), color);
                    }
                }
            }
            return b.Build("tech_panel");
        }

        public static Mesh SplashCrown(VfxMeshGenArgs args, float crownRadius = 0.6f, int spikeCount = 12,
            float spikeHeightMin = 0.25f, float spikeHeightMax = 0.6f, float spikeWidth = 0.07f,
            float rimHeight = 0.28f, float irregularity = 0.55f, int sheetSegments = 24)
        {
            var rng = new VfxDeterministicRandom(args.Seed);
            sheetSegments = Mathf.Clamp(sheetSegments, 8, Mathf.Max(8, (args.VertexBudget - spikeCount * 4) / 2 - 1));
            spikeCount = Mathf.Clamp(spikeCount, 0, Mathf.Max(0, (args.VertexBudget - (sheetSegments + 1) * 2) / 4));
            var b = new VfxMeshBuilder();
            // Crown wall: base ring -> flared top ring.
            int prevB = -1, prevT = -1;
            for (int i = 0; i <= sheetSegments; i++)
            {
                float u = i / (float)sheetSegments;
                float ang = u * Mathf.PI * 2f;
                float irr = 1f + (ValueNoise1(u * 6f + args.Seed % 89) - 0.5f) * irregularity * 0.5f;
                Vector3 dir = args.Is3D
                    ? new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang))
                    : new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 baseP = dir * crownRadius;
                Vector3 up = args.Is3D ? Vector3.up : Vector3.up;
                Vector3 topP = dir * crownRadius * 1.25f + up * rimHeight * irr;
                var color = new Color(irr - 1f + 0.5f, 0f, 0f, 0f);
                int vb = b.AddVertex(baseP, dir, new Vector2(u, 0f), new Vector2(u, 0f), color);
                int vt = b.AddVertex(topP, dir, new Vector2(u, 1f), new Vector2(u, 1f), color);
                if (i > 0) b.AddQuad(prevB, prevT, vt, vb);
                prevB = vb;
                prevT = vt;
            }
            // Spikes along the rim.
            for (int s = 0; s < spikeCount; s++)
            {
                float u = (s + 0.5f) / spikeCount + (rng.Next01() - 0.5f) * irregularity / spikeCount;
                float ang = u * Mathf.PI * 2f;
                Vector3 dir = args.Is3D
                    ? new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang))
                    : new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                Vector3 rimP = dir * crownRadius * 1.25f + Vector3.up * rimHeight;
                float h = rng.Range(spikeHeightMin, spikeHeightMax);
                Vector3 tip = rimP + (Vector3.up + dir * 0.4f).normalized * h;
                Vector3 side = Vector3.Cross(args.Is3D ? Vector3.up : Vector3.forward, dir).normalized * spikeWidth * 0.5f;
                float spikeId = spikeCount > 1 ? s / (float)(spikeCount - 1) : 0f;
                var color = new Color(rng.Next01(), 0f, spikeId, 0f);
                b.AddFlatQuad(rimP - side, rimP + side, tip + side * 0.15f, tip - side * 0.15f,
                    Vector2.zero, Vector2.one, new Vector2(0.98f, 0f), new Vector2(1f, 0f), color);
            }
            return b.Build("splash_crown");
        }

        // ---------------------------------------------------------------- fracture / cloth

        /// <summary>
        /// Voronoi pre-fracture: 3D half-space clipping of a box (or 2D cell fans).
        /// Returns one Mesh per fragment; colors.a = 1 marks cut faces, 0 original
        /// surface (element presets give cut faces their own look).
        /// </summary>
        public static List<Mesh> VoronoiPrefracture(VfxMeshGenArgs args, int fragmentCount = 12,
            Vector3 boxSize = default, int sitePattern = 0, Vector3 impactPoint = default)
        {
            if (boxSize == default) boxSize = Vector3.one;
            var rng = new VfxDeterministicRandom(args.Seed);
            // Flat-shaded convex cells cost ~75-90 vertices each (fan triangles
            // don't share vertices); clamp conservatively so the layer total
            // honours the tier budget (MS-4).
            int perFragment = args.Is3D ? 96 : 24;
            fragmentCount = Mathf.Clamp(fragmentCount, 2, Mathf.Max(2, args.VertexBudget / perFragment));

            var sites = new Vector3[fragmentCount];
            for (int i = 0; i < fragmentCount; i++)
            {
                Vector3 p = new Vector3(
                    (VfxHalton.Sample(i, 2) - 0.5f) * boxSize.x,
                    (VfxHalton.Sample(i, 3) - 0.5f) * boxSize.y,
                    args.Is3D ? (VfxHalton.Sample(i, 5) - 0.5f) * boxSize.z : 0f);
                if (sitePattern == 2)
                {
                    // radialFromPoint: denser near impact.
                    p = Vector3.Lerp(p, impactPoint, rng.Next01() * 0.5f);
                }
                p += new Vector3(rng.Range(-0.03f, 0.03f), rng.Range(-0.03f, 0.03f), args.Is3D ? rng.Range(-0.03f, 0.03f) : 0f);
                sites[i] = p;
            }

            var meshes = new List<Mesh>(fragmentCount);
            for (int i = 0; i < fragmentCount; i++)
            {
                Mesh m = args.Is3D
                    ? ClipCellFromBox(sites, i, boxSize)
                    : Clip2DCell(sites, i, boxSize);
                m.name = "voronoi_frag_" + i.ToString("000");
                meshes.Add(m);
            }
            return meshes;
        }

        /// <summary>Unified-registry entry: all fragments combined into one mesh (colors.b = fragment id).</summary>
        public static Mesh VoronoiPrefractureCombined(VfxMeshGenArgs args)
        {
            List<Mesh> frags = VoronoiPrefracture(args);
            var combine = new CombineInstance[frags.Count];
            for (int i = 0; i < frags.Count; i++)
                combine[i] = new CombineInstance { mesh = frags[i], transform = Matrix4x4.identity };
            var combined = new Mesh { name = "voronoi_prefracture" };
            combined.CombineMeshes(combine, true, false);
            return combined;
        }

        public static Mesh ClothPatch(VfxMeshGenArgs args, float sizeX = 1f, float sizeY = 1.4f,
            int subdivX = 12, int subdivY = 16, int pinMode = 0)
        {
            while ((subdivX + 1) * (subdivY + 1) > args.VertexBudget && (subdivX > 1 || subdivY > 1))
            {
                if (subdivX >= subdivY) subdivX--; else subdivY--;
            }
            var b = new VfxMeshBuilder();
            var grid = new int[subdivX + 1, subdivY + 1];
            for (int y = 0; y <= subdivY; y++)
            {
                for (int x = 0; x <= subdivX; x++)
                {
                    float u = x / (float)subdivX;
                    float v = y / (float)subdivY;
                    // v=1 is the top edge; pin coefficients derive from colors.g
                    // (g = 0 means pinned; the compiler maps g -> maxDistance).
                    float pinDist = pinMode == 0
                        ? 1f - v                                   // topEdge
                        : pinMode == 1
                            ? Mathf.Min(Mathf.Min(u, 1f - u) + (1f - v), 1f) // corners-ish
                            : Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 1f)); // topCenter
                    Vector3 p = new Vector3((u - 0.5f) * sizeX, v * sizeY, 0f);
                    grid[x, y] = b.AddVertex(p, Vector3.back, new Vector2(u, v), new Vector2(u, v),
                        new Color(0f, Mathf.Clamp01(pinDist), 0f, 0f));
                }
            }
            for (int y = 0; y < subdivY; y++)
                for (int x = 0; x < subdivX; x++)
                    b.AddQuad(grid[x, y], grid[x + 1, y], grid[x + 1, y + 1], grid[x, y + 1]);
            return b.Build("cloth_patch");
        }

        // ---------------------------------------------------------------- shared helpers

        private static void AppendTube(VfxMeshBuilder b, IReadOnlyList<Vector3> path, float radiusStart,
            float radiusEnd, int ringVerts, bool is3D, Color color)
        {
            if (path.Count < 2) return;
            if (!is3D || ringVerts < 3)
            {
                AppendBand(b, path, radiusStart * 2f, is3D, 0f, 1f, color);
                return;
            }
            int[] prevRing = null;
            for (int i = 0; i < path.Count; i++)
            {
                float u = i / (float)(path.Count - 1);
                Vector3 tangent = i == 0
                    ? (path[1] - path[0]).normalized
                    : i == path.Count - 1
                        ? (path[i] - path[i - 1]).normalized
                        : (path[i + 1] - path[i - 1]).normalized;
                if (tangent.sqrMagnitude < 1e-10f) tangent = Vector3.up;
                Vector3 side = Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(tangent, side);
                float r = Mathf.Lerp(radiusStart, radiusEnd, u);
                var ring = new int[ringVerts + 1];
                for (int k = 0; k <= ringVerts; k++)
                {
                    float a = k / (float)ringVerts * Mathf.PI * 2f;
                    Vector3 n = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                    ring[k] = b.AddVertex(path[i] + n * r, n, new Vector2(k / (float)ringVerts, u),
                        new Vector2(u, k / (float)ringVerts), color);
                }
                if (prevRing != null)
                    for (int k = 0; k < ringVerts; k++)
                        b.AddQuad(prevRing[k], prevRing[k + 1], ring[k + 1], ring[k]);
                prevRing = ring;
            }
        }

        private static void AppendBand(VfxMeshBuilder b, IReadOnlyList<Vector3> path, float width, bool is3D,
            float arcStart, float arcSpan, Color color)
        {
            if (path.Count < 2) return;
            int prevL = -1, prevR = -1;
            for (int i = 0; i < path.Count; i++)
            {
                float u = i / (float)(path.Count - 1);
                Vector3 tangent = i == 0
                    ? (path[1] - path[0]).normalized
                    : i == path.Count - 1
                        ? (path[i] - path[i - 1]).normalized
                        : (path[i + 1] - path[i - 1]).normalized;
                if (tangent.sqrMagnitude < 1e-10f) tangent = Vector3.up;
                Vector3 side = is3D
                    ? Vector3.Cross(tangent, Mathf.Abs(tangent.y) > 0.95f ? Vector3.right : Vector3.up).normalized
                    : Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 normal = is3D ? Vector3.Cross(tangent, side) : Vector3.back;
                float arc = arcStart + u * arcSpan; // globally continuous arclength (branch children inherit)
                int l = b.AddVertex(path[i] - side * width * 0.5f, normal, new Vector2(u, 0f), new Vector2(arc, 0f), color);
                int r = b.AddVertex(path[i] + side * width * 0.5f, normal, new Vector2(u, 1f), new Vector2(arc, 1f), color);
                if (i > 0) b.AddQuad(prevL, prevR, r, l);
                prevL = l;
                prevR = r;
            }
        }

        private static void AppendUvSphere(VfxMeshBuilder b, float radius, int lon, int lat, Color color)
        {
            var grid = new int[lon + 1, lat + 1];
            for (int y = 0; y <= lat; y++)
            {
                float phi = y / (float)lat * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float theta = x / (float)lon * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    grid[x, y] = b.AddVertex(n * radius, n, new Vector2(x / (float)lon, y / (float)lat),
                        new Vector2(y / (float)lat, x / (float)lon), color);
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                    b.AddQuad(grid[x, y], grid[x + 1, y], grid[x + 1, y + 1], grid[x, y + 1]);
        }

        private static void AppendDisc(VfxMeshBuilder b, float radius, int segments, Color color)
        {
            int centre = b.AddVertex(Vector3.zero, Vector3.back, new Vector2(0.5f, 0.5f), Vector2.zero, color);
            int first = -1, prev = -1;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                int v = b.AddVertex(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius, Vector3.back,
                    new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f),
                    new Vector2(i / (float)segments, 1f), color);
                if (i == 0) first = v;
                if (prev >= 0) b.AddTriangle(centre, prev, v);
                prev = v;
            }
        }

        private static Mesh AnnulusBand(VfxMeshGenArgs args, float inner, float outer, int segments, float openArc)
        {
            var b = new VfxMeshBuilder();
            float arcSpan = (1f - Mathf.Clamp01(openArc)) * Mathf.PI * 2f;
            int prevI = -1, prevO = -1;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float a = u * arcSpan;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                int vi = b.AddVertex(dir * inner, Vector3.back, new Vector2(u, 0f), new Vector2(u, 0f), new Color(0f, 0f, 0f, 0f));
                int vo = b.AddVertex(dir * outer, Vector3.back, new Vector2(u, 1f), new Vector2(u, 1f), new Color(0f, 0f, 0f, 0f));
                if (i > 0) b.AddQuad(prevI, prevO, vo, vi);
                prevI = vi;
                prevO = vo;
            }
            return b.Build("shell_polyhedron_2d");
        }

        private static void AppendFlatRingSegments(VfxMeshBuilder b, float inner, float outer, int majorSegments,
            int segmentCount, float segmentGap, bool is3D)
        {
            segmentCount = Mathf.Max(segmentCount, 1);
            float segArc = Mathf.PI * 2f / segmentCount;
            float gapArc = segArc * segmentGap;
            int stepsPerSeg = Mathf.Max(majorSegments / segmentCount, 2);
            for (int seg = 0; seg < segmentCount; seg++)
            {
                float a0 = seg * segArc + gapArc * 0.5f;
                float a1 = (seg + 1) * segArc - gapArc * 0.5f;
                float segId = segmentCount > 1 ? seg / (float)(segmentCount - 1) : 0f;
                var color = new Color(0f, 0f, segId, 0f);
                int prevI = -1, prevO = -1;
                for (int i = 0; i <= stepsPerSeg; i++)
                {
                    float ang = Mathf.Lerp(a0, a1, i / (float)stepsPerSeg);
                    Vector3 dir = is3D ? new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) : new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                    Vector3 n = is3D ? Vector3.up : Vector3.back;
                    float u = ang / (Mathf.PI * 2f);
                    int vi = b.AddVertex(dir * inner, n, new Vector2(u, 0f), new Vector2(u, 0f), color);
                    int vo = b.AddVertex(dir * outer, n, new Vector2(u, 1f), new Vector2(u, 1f), color);
                    if (i > 0) b.AddQuad(prevI, prevO, vo, vi);
                    prevI = vi;
                    prevO = vo;
                }
            }
        }

        private static Vector2 SphereUv(Vector3 n)
        {
            n = n.normalized;
            return new Vector2(Mathf.Atan2(n.z, n.x) / (Mathf.PI * 2f) + 0.5f, Mathf.Acos(Mathf.Clamp(n.y, -1f, 1f)) / Mathf.PI);
        }

        private static List<Vector3[]> IcosphereTriangles(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0).normalized, new Vector3(1, t, 0).normalized,
                new Vector3(-1, -t, 0).normalized, new Vector3(1, -t, 0).normalized,
                new Vector3(0, -1, t).normalized, new Vector3(0, 1, t).normalized,
                new Vector3(0, -1, -t).normalized, new Vector3(0, 1, -t).normalized,
                new Vector3(t, 0, -1).normalized, new Vector3(t, 0, 1).normalized,
                new Vector3(-t, 0, -1).normalized, new Vector3(-t, 0, 1).normalized
            };
            int[][] faces =
            {
                new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
                new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
                new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
            };
            var tris = new List<Vector3[]>();
            foreach (var f in faces) tris.Add(new[] { v[f[0]], v[f[1]], v[f[2]] });
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<Vector3[]>(tris.Count * 4);
                foreach (var tri in tris)
                {
                    Vector3 a = tri[0], bb = tri[1], c = tri[2];
                    Vector3 ab = ((a + bb) * 0.5f).normalized;
                    Vector3 bc = ((bb + c) * 0.5f).normalized;
                    Vector3 ca = ((c + a) * 0.5f).normalized;
                    next.Add(new[] { a, ab, ca });
                    next.Add(new[] { ab, bb, bc });
                    next.Add(new[] { ca, bc, c });
                    next.Add(new[] { ab, bc, ca });
                }
                tris = next;
            }
            return tris;
        }

        private static void GetPlatonic(int solid, out Vector3[] verts, out int[][] edges)
        {
            switch (solid)
            {
                case 0: // tetrahedron
                    verts = new[]
                    {
                        new Vector3(1, 1, 1).normalized, new Vector3(1, -1, -1).normalized,
                        new Vector3(-1, 1, -1).normalized, new Vector3(-1, -1, 1).normalized
                    };
                    edges = new[] { new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 2, 3 } };
                    return;
                case 1: // cube
                    verts = new Vector3[8];
                    for (int i = 0; i < 8; i++)
                        verts[i] = new Vector3((i & 1) * 2 - 1, ((i >> 1) & 1) * 2 - 1, ((i >> 2) & 1) * 2 - 1).normalized;
                    edges = new[]
                    {
                        new[]{0,1}, new[]{2,3}, new[]{4,5}, new[]{6,7},
                        new[]{0,2}, new[]{1,3}, new[]{4,6}, new[]{5,7},
                        new[]{0,4}, new[]{1,5}, new[]{2,6}, new[]{3,7}
                    };
                    return;
                case 3: // icosahedron
                {
                    float t = (1f + Mathf.Sqrt(5f)) / 2f;
                    verts = new[]
                    {
                        new Vector3(-1, t, 0).normalized, new Vector3(1, t, 0).normalized,
                        new Vector3(-1, -t, 0).normalized, new Vector3(1, -t, 0).normalized,
                        new Vector3(0, -1, t).normalized, new Vector3(0, 1, t).normalized,
                        new Vector3(0, -1, -t).normalized, new Vector3(0, 1, -t).normalized,
                        new Vector3(t, 0, -1).normalized, new Vector3(t, 0, 1).normalized,
                        new Vector3(-t, 0, -1).normalized, new Vector3(-t, 0, 1).normalized
                    };
                    var edgeSet = new HashSet<(int, int)>();
                    int[][] faces =
                    {
                        new[]{0,11,5}, new[]{0,5,1}, new[]{0,1,7}, new[]{0,7,10}, new[]{0,10,11},
                        new[]{1,5,9}, new[]{5,11,4}, new[]{11,10,2}, new[]{10,7,6}, new[]{7,1,8},
                        new[]{3,9,4}, new[]{3,4,2}, new[]{3,2,6}, new[]{3,6,8}, new[]{3,8,9},
                        new[]{4,9,5}, new[]{2,4,11}, new[]{6,2,10}, new[]{8,6,7}, new[]{9,8,1}
                    };
                    foreach (var f in faces)
                        for (int i = 0; i < 3; i++)
                        {
                            int a = f[i], bb = f[(i + 1) % 3];
                            edgeSet.Add(a < bb ? (a, bb) : (bb, a));
                        }
                    var list = new List<int[]>();
                    foreach (var (a, bb) in edgeSet) list.Add(new[] { a, bb });
                    list.Sort((x, y) => x[0] != y[0] ? x[0].CompareTo(y[0]) : x[1].CompareTo(y[1]));
                    edges = list.ToArray();
                    return;
                }
                default: // octahedron
                    verts = new[]
                    {
                        Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back
                    };
                    edges = new[]
                    {
                        new[]{0,2}, new[]{0,3}, new[]{0,4}, new[]{0,5},
                        new[]{1,2}, new[]{1,3}, new[]{1,4}, new[]{1,5},
                        new[]{2,4}, new[]{2,5}, new[]{3,4}, new[]{3,5}
                    };
                    return;
            }
        }

        // ------------- Voronoi cell clipping (convex polyhedron vs half-spaces)

        private static Mesh ClipCellFromBox(Vector3[] sites, int index, Vector3 boxSize)
        {
            // Start from the box as a list of faces (each a vertex loop) and clip
            // by the bisector plane of (site_i, site_j) for every other site.
            Vector3 h = boxSize * 0.5f;
            var faces = new List<List<Vector3>>
            {
                new List<Vector3> { new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x, h.y,-h.z), new Vector3( h.x, h.y,-h.z), new Vector3( h.x,-h.y,-h.z) },
                new List<Vector3> { new Vector3(-h.x,-h.y, h.z), new Vector3( h.x,-h.y, h.z), new Vector3( h.x, h.y, h.z), new Vector3(-h.x, h.y, h.z) },
                new List<Vector3> { new Vector3(-h.x,-h.y,-h.z), new Vector3( h.x,-h.y,-h.z), new Vector3( h.x,-h.y, h.z), new Vector3(-h.x,-h.y, h.z) },
                new List<Vector3> { new Vector3(-h.x, h.y,-h.z), new Vector3(-h.x, h.y, h.z), new Vector3( h.x, h.y, h.z), new Vector3( h.x, h.y,-h.z) },
                new List<Vector3> { new Vector3(-h.x,-h.y,-h.z), new Vector3(-h.x,-h.y, h.z), new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, h.y,-h.z) },
                new List<Vector3> { new Vector3( h.x,-h.y,-h.z), new Vector3( h.x, h.y,-h.z), new Vector3( h.x, h.y, h.z), new Vector3( h.x,-h.y, h.z) }
            };
            var isCut = new List<bool> { false, false, false, false, false, false };

            Vector3 self = sites[index];
            for (int j = 0; j < sites.Length; j++)
            {
                if (j == index) continue;
                Vector3 mid = (self + sites[j]) * 0.5f;
                Vector3 n = (sites[j] - self).normalized; // keep side where dot(p - mid, n) <= 0
                ClipFacesByPlane(faces, isCut, mid, n);
                if (faces.Count == 0) break;
            }

            var b = new VfxMeshBuilder();
            for (int f = 0; f < faces.Count; f++)
            {
                List<Vector3> loop = faces[f];
                if (loop.Count < 3) continue;
                Vector3 c = Vector3.zero;
                foreach (var p in loop) c += p;
                c /= loop.Count;
                float fragId = sites.Length > 1 ? index / (float)(sites.Length - 1) : 0f;
                var color = new Color(0f, 0f, fragId, isCut[f] ? 1f : 0f);
                for (int i = 0; i < loop.Count; i++)
                    b.AddFlatTriangle(c, loop[i], loop[(i + 1) % loop.Count],
                        new Vector2(0.5f, 0.5f), new Vector2(fragId, 0f), color);
            }
            return b.Build("voronoi_cell");
        }

        private static void ClipFacesByPlane(List<List<Vector3>> faces, List<bool> isCut, Vector3 planePoint, Vector3 planeNormal)
        {
            var sectionPoints = new List<Vector3>();
            for (int f = faces.Count - 1; f >= 0; f--)
            {
                List<Vector3> loop = faces[f];
                var kept = new List<Vector3>();
                int count = loop.Count;
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = loop[i];
                    Vector3 bb = loop[(i + 1) % count];
                    float da = Vector3.Dot(a - planePoint, planeNormal);
                    float db = Vector3.Dot(bb - planePoint, planeNormal);
                    if (da <= 0f) kept.Add(a);
                    if ((da <= 0f) != (db <= 0f))
                    {
                        float t = da / (da - db);
                        Vector3 x = Vector3.Lerp(a, bb, t);
                        kept.Add(x);
                        sectionPoints.Add(x);
                    }
                }
                if (kept.Count < 3) { faces.RemoveAt(f); isCut.RemoveAt(f); }
                else faces[f] = kept;
            }
            // Cap face from the section points (sorted around their centroid).
            if (sectionPoints.Count >= 3)
            {
                Vector3 c = Vector3.zero;
                foreach (var p in sectionPoints) c += p;
                c /= sectionPoints.Count;
                Vector3 refDir = (sectionPoints[0] - c).normalized;
                Vector3 up = planeNormal;
                sectionPoints.Sort((p, q) =>
                {
                    float ap = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, (p - c).normalized), up), Vector3.Dot(refDir, (p - c).normalized));
                    float aq = Mathf.Atan2(Vector3.Dot(Vector3.Cross(refDir, (q - c).normalized), up), Vector3.Dot(refDir, (q - c).normalized));
                    return ap.CompareTo(aq);
                });
                // Deduplicate near-identical points.
                var cap = new List<Vector3>();
                foreach (var p in sectionPoints)
                    if (cap.Count == 0 || (cap[cap.Count - 1] - p).sqrMagnitude > 1e-10f)
                        cap.Add(p);
                if (cap.Count >= 3)
                {
                    // Wind the cap so its normal points along planeNormal (outward).
                    Vector3 capN = Vector3.Cross(cap[1] - cap[0], cap[2] - cap[0]);
                    if (Vector3.Dot(capN, planeNormal) < 0f) cap.Reverse();
                    faces.Add(cap);
                    isCut.Add(true);
                }
            }
        }

        private static Mesh Clip2DCell(Vector3[] sites, int index, Vector3 boxSize)
        {
            var poly = new List<Vector3>
            {
                new Vector3(-boxSize.x * 0.5f, -boxSize.y * 0.5f, 0f),
                new Vector3(boxSize.x * 0.5f, -boxSize.y * 0.5f, 0f),
                new Vector3(boxSize.x * 0.5f, boxSize.y * 0.5f, 0f),
                new Vector3(-boxSize.x * 0.5f, boxSize.y * 0.5f, 0f)
            };
            Vector3 self = sites[index];
            for (int j = 0; j < sites.Length && poly.Count >= 3; j++)
            {
                if (j == index) continue;
                Vector3 mid = (self + sites[j]) * 0.5f;
                Vector3 n = (sites[j] - self).normalized;
                var kept = new List<Vector3>();
                for (int i = 0; i < poly.Count; i++)
                {
                    Vector3 a = poly[i];
                    Vector3 bb = poly[(i + 1) % poly.Count];
                    float da = Vector3.Dot(a - mid, n);
                    float db = Vector3.Dot(bb - mid, n);
                    if (da <= 0f) kept.Add(a);
                    if ((da <= 0f) != (db <= 0f)) kept.Add(Vector3.Lerp(a, bb, da / (da - db)));
                }
                poly = kept;
            }
            var b = new VfxMeshBuilder();
            if (poly.Count >= 3)
            {
                Vector3 c = Vector3.zero;
                foreach (var p in poly) c += p;
                c /= poly.Count;
                float fragId = sites.Length > 1 ? index / (float)(sites.Length - 1) : 0f;
                var color = new Color(0f, 0f, fragId, 0f);
                for (int i = 0; i < poly.Count; i++)
                    b.AddFlatTriangle(c, poly[i], poly[(i + 1) % poly.Count],
                        new Vector2(0.5f, 0.5f), new Vector2(fragId, 0f), color);
            }
            return b.Build("voronoi_cell_2d");
        }

        // ------------- deterministic value noise (seed folded by caller)

        private static float Hash1(float p)
        {
            p = (p * 0.1031f) % 1f;
            if (p < 0f) p += 1f;
            p *= p + 33.33f;
            p *= p + p;
            p %= 1f;
            return p < 0f ? p + 1f : p;
        }

        private static float ValueNoise1(float x)
        {
            float i = Mathf.Floor(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash1(i), Hash1(i + 1f), u);
        }

        private static float ValueNoise3(Vector3 p)
        {
            return (ValueNoise1(p.x * 7.13f + p.y * 3.71f + p.z * 1.97f)
                  + ValueNoise1(p.y * 5.87f + p.z * 2.83f + p.x * 1.13f)) * 0.5f;
        }
    }
}
