using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.Editor.TechniqueFamilies
{
    /// <summary>
    /// Deterministic xorshift RNG for mesh generation. UnityEngine.Random is
    /// global state and would break the byte-for-byte reproducibility contract
    /// (TECH_FAMILY_SPEC_MESH_LIGHT.md section 1.4).
    /// </summary>
    public struct VfxDeterministicRandom
    {
        private uint state;

        public VfxDeterministicRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>[0, 1)</summary>
        public float Next01()
        {
            return (NextUInt() & 0xFFFFFF) / 16777216f;
        }

        public float Range(float min, float max)
        {
            return min + (max - min) * Next01();
        }

        public int RangeInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        public Vector3 OnUnitSphere()
        {
            // Deterministic rejection-free spherical sampling.
            float z = Range(-1f, 1f);
            float a = Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(r * Mathf.Cos(a), r * Mathf.Sin(a), z);
        }
    }

    /// <summary>
    /// Accumulates vertices honouring the generator output contract
    /// (normals + uv0 + uv1-in-uv2-channel + colors) and builds the Mesh.
    /// colors convention: r = structural random, g = normalized boundary
    /// distance, b = segment/fragment id normalized, a = spare (cut-face flag).
    /// </summary>
    public sealed class VfxMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uv0 = new List<Vector2>();
        private readonly List<Vector2> uv1 = new List<Vector2>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public int VertexCount { get { return vertices.Count; } }

        public int AddVertex(Vector3 position, Vector3 normal, Vector2 uv, Vector2 structural, Color color)
        {
            vertices.Add(position);
            normals.Add(normal);
            uv0.Add(uv);
            uv1.Add(structural);
            colors.Add(color);
            return vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>Adds a flat-shaded quad (4 unique vertices, face normal).</summary>
        public void AddFlatQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 uvA, Vector2 uvB, Vector2 structuralA, Vector2 structuralB, Color color)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p3 - p0);
            float len = n.magnitude;
            n = len > 1e-8f ? n / len : Vector3.forward;
            int a = AddVertex(p0, n, new Vector2(uvA.x, uvA.y), structuralA, color);
            int b = AddVertex(p1, n, new Vector2(uvB.x, uvA.y), structuralA, color);
            int c = AddVertex(p2, n, new Vector2(uvB.x, uvB.y), structuralB, color);
            int d = AddVertex(p3, n, new Vector2(uvA.x, uvB.y), structuralB, color);
            AddQuad(a, b, c, d);
        }

        public void AddFlatTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 uv, Vector2 structural, Color color)
        {
            Vector3 n = Vector3.Cross(p1 - p0, p2 - p0);
            float len = n.magnitude;
            n = len > 1e-8f ? n / len : Vector3.forward;
            int a = AddVertex(p0, n, uv, structural, color);
            int b = AddVertex(p1, n, uv, structural, color);
            int c = AddVertex(p2, n, uv, structural, color);
            AddTriangle(a, b, c);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            // Contract: the "uv1" structural channel lives in Mesh.uv2 (TEXCOORD1).
            mesh.SetUVs(1, uv1);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Halton low-discrepancy sequence (deterministic, evenly spread).</summary>
    public static class VfxHalton
    {
        public static float Sample(int index, int b)
        {
            float f = 1f, r = 0f;
            int i = index + 1;
            while (i > 0)
            {
                f /= b;
                r += f * (i % b);
                i /= b;
            }
            return r;
        }

        /// <summary>Point k of a deterministic unit-sphere spread (Fibonacci lattice).</summary>
        public static Vector3 FibonacciSphere(int index, int count)
        {
            float k = index + 0.5f;
            float phi = Mathf.Acos(1f - 2f * k / count);
            float theta = Mathf.PI * (1f + Mathf.Sqrt(5f)) * k;
            return new Vector3(
                Mathf.Sin(phi) * Mathf.Cos(theta),
                Mathf.Sin(phi) * Mathf.Sin(theta),
                Mathf.Cos(phi));
        }
    }
}
