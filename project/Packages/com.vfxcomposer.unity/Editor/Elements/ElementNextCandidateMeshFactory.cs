using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VFXComposer.Editor.Elements
{
    /// <summary>Recipe-shaped procedural carriers; no W3-W5 body is selected from the old fixed mesh set.</summary>
    internal static partial class ElementNextCandidateMeshFactory
    {
        public static string[] EnsureRoleMeshes(ElementNextCandidatePlan plan, string generatedFolder)
        {
            var meshFolder = generatedFolder + "/Meshes";
            EnsureFolder(meshFolder);
            var roleCount = plan.Family == ElementNextCandidateFamily.Lightning ? 1 : 5;
            var result = new string[roleCount];
            for (var role = 0; role < roleCount; role++)
            {
                var path = meshFolder + "/M_" + plan.EffectId + "_" + RoleName(role) + ".asset";
                var source = Create(plan, role); source.name = Path.GetFileNameWithoutExtension(path);
                Save(path, source); result[role] = path;
            }
            return result;
        }

        public static string EnsureDetailMesh(string sharedMeshFolder)
        {
            EnsureFolder(sharedMeshFolder);
            var path = sharedMeshFolder + "/M_ElementNext_DetailShard.asset";
            Save(path, Prism(1f, .23f, 0));
            return path;
        }

        private static Mesh Create(ElementNextCandidatePlan plan, int role)
        {
            var scale = role == 1 ? .72f : role == 2 ? 1.12f : role == 3 ? 1.02f : role == 4 ? .58f : 1f;
            switch (plan.Profile)
            {
                case ElementNextCandidateProfile.FlameSlash: return Crescent(30, plan.Number("sweep_angle", 110f), .34f, .34f + plan.Number("arc_width", .72f) * .42f, .14f, scale);
                case ElementNextCandidateProfile.FireNova: return TongueRing(plan.Integer("tongue_count", 12), .54f, 1f, .24f, scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.Flamethrower: return JaggedCone(20, plan.Number("cone_angle", 24f), .12f, scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.BurningStatus: return FlameCluster(plan.Integer("flame_count", 3), scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.EmberRain: return PatchField(plan.Integer("burn_patch_count", 5), scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.PhoenixDart: return Phoenix(scale);
                case ElementNextCandidateProfile.ChainBlast: return BurstRosette(14, .3f, scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.FireShield: return Sphere(12, 8, scale, .08f);
                case ElementNextCandidateProfile.IceSpike: return SpikeCluster(plan.Integer("spike_count", 5), plan.Text("pattern", "fan"), scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.Blizzard: return WindCutSheet(18, scale, plan.Text("wind_dir", "north_east"), plan.Seed + (uint)role);
                case ElementNextCandidateProfile.FrostBreath: return JaggedCone(18, plan.Number("cone_angle", 52f), .06f, scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.IceShard: return Prism(1f, .22f + plan.Integer("shard_variant", 2) * .035f, plan.Integer("shard_variant", 2));
                case ElementNextCandidateProfile.FreezeStatus: return FacetedShell(8, scale, .14f);
                case ElementNextCandidateProfile.CrystalShield: return CrystalPetals(plan.Integer("petal_count", 6), scale);
                case ElementNextCandidateProfile.FlashFreeze: return VerticalCrystalShell(7, scale, plan.Seed + (uint)role);
                case ElementNextCandidateProfile.ThunderStrike: return BurstRosette(plan.Integer("ground_arc_count", 5), .22f, scale, plan.Seed);
                case ElementNextCandidateProfile.BallLightning: return Sphere(10, 7, scale, .04f);
                case ElementNextCandidateProfile.StaticField: return MultiRing(32, 1, .72f, 1f, scale);
                case ElementNextCandidateProfile.StormCharge: return CloudRing(8, scale, plan.Seed);
                case ElementNextCandidateProfile.ElectroSlash: return Crescent(24, 112f, .38f, .72f, plan.Number("jag_amplitude", .35f), scale);
                case ElementNextCandidateProfile.EmpNova: return MultiRing(40, plan.Integer("ring_count", 2), .72f, 1f, scale);
                case ElementNextCandidateProfile.VoltShield: return Sphere(12, 8, scale, Mathf.Clamp(plan.Number("net_density", 4f) * .012f, .03f, .14f));
                default: return CreateW6W8(plan, role, scale);
            }
        }

        private static Mesh Crescent(int segments, float degrees, float inner, float outer, float jaggedness, float scale)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            var radians = Mathf.Clamp(degrees, 40f, 170f) * Mathf.Deg2Rad;
            for (var index = 0; index <= segments; index++)
            {
                var t = index / (float)segments; var angle = Mathf.Lerp(-radians * .5f, radians * .5f, t); var jag = 1f + jaggedness * (index % 2 == 0 ? -.18f : .18f); var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(direction * inner * scale); vertices.Add(direction * outer * jag * scale); uv.Add(new Vector2(t, 0f)); uv.Add(new Vector2(t, 1f));
                if (index < segments) QuadTriangles(triangles, index * 2, index * 2 + 1, index * 2 + 3, index * 2 + 2);
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh TongueRing(int tongues, float inner, float outer, float height, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); tongues = Mathf.Clamp(tongues, 3, 24);
            for (var index = 0; index < tongues; index++)
            {
                var a = index * Mathf.PI * 2f / tongues; var width = Mathf.PI / tongues * .38f; var tip = outer * (1f + Hash01(seed + (uint)index) * .18f); var start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a - width), 0f, Mathf.Sin(a - width)) * inner * scale);
                vertices.Add(new Vector3(Mathf.Cos(a + width), 0f, Mathf.Sin(a + width)) * inner * scale);
                vertices.Add(new Vector3(Mathf.Cos(a), height * (1f + Hash01(seed + (uint)(index + 71)) * .45f), Mathf.Sin(a)) * tip * scale);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(new Vector2(.5f, 1f)); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh JaggedCone(int segments, float angle, float jaggedness, float scale, uint seed)
        {
            var vertices = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(0f, .5f) }; var triangles = new List<int>(); var half = Mathf.Tan(Mathf.Clamp(angle, 5f, 80f) * Mathf.Deg2Rad * .5f);
            for (var index = 0; index <= segments; index++)
            {
                var t = index / (float)segments; var y = Mathf.Lerp(-half, half, t) * (1f + (Hash01(seed + (uint)index) - .5f) * jaggedness); vertices.Add(new Vector3(1f + (index % 2) * jaggedness * .12f, y, 0f) * scale); uv.Add(new Vector2(1f, t));
                if (index < segments) { triangles.Add(0); triangles.Add(index + 1); triangles.Add(index + 2); }
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh FlameCluster(int count, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); count = Mathf.Clamp(count, 1, 8);
            for (var index = 0; index < count; index++)
            {
                var center = count == 1 ? 0f : Mathf.Lerp(-.62f, .62f, index / (float)(count - 1)); var height = .72f + Hash01(seed + (uint)index) * .38f; var width = .16f + Hash01(seed + (uint)(index + 19)) * .09f; var start = vertices.Count;
                vertices.Add(new Vector3(center - width, -.48f)); vertices.Add(new Vector3(center + width, -.48f)); vertices.Add(new Vector3(center + width * .35f, .08f)); vertices.Add(new Vector3(center + (Hash01(seed + (uint)(index + 37)) - .5f) * width, height - .48f)); vertices.Add(new Vector3(center - width * .35f, .08f));
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(new Vector2(1f, .55f)); uv.Add(new Vector2(.5f, 1f)); uv.Add(new Vector2(0f, .55f));
                for (var tri = 1; tri < 4; tri++) { triangles.Add(start); triangles.Add(start + tri); triangles.Add(start + tri + 1); }
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh PatchField(int count, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); count = Mathf.Clamp(count, 3, 8);
            for (var patch = 0; patch < count; patch++)
            {
                var angle = Hash01(seed + (uint)(patch * 13)) * Mathf.PI * 2f; var center = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (.18f + .55f * Hash01(seed + (uint)(patch * 17 + 1))); var radius = .18f + .1f * Hash01(seed + (uint)(patch * 19 + 2)); var start = vertices.Count; var sides = 6;
                vertices.Add(center); uv.Add(new Vector2(.5f, .5f));
                for (var side = 0; side <= sides; side++) { var a = side * Mathf.PI * 2f / sides; vertices.Add(center + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * radius * (side % 2 == 0 ? 1f : .78f)); uv.Add(new Vector2(.5f + Mathf.Cos(a) * .5f, .5f + Mathf.Sin(a) * .5f)); }
                for (var side = 0; side < sides; side++) { triangles.Add(start); triangles.Add(start + side + 1); triangles.Add(start + side + 2); }
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh Phoenix(float scale)
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-.8f,.12f),new Vector3(-.18f,.05f),new Vector3(-.55f,.62f),new Vector3(0f,-.05f),new Vector3(.55f,.62f),new Vector3(.18f,.05f),new Vector3(.8f,.12f),new Vector3(0f,.55f),new Vector3(-.12f,-.62f),new Vector3(.12f,-.62f)
            };
            var uv = new List<Vector2>(); for (var index = 0; index < vertices.Count; index++) uv.Add(new Vector2(vertices[index].x + .8f, vertices[index].y + .62f));
            var triangles = new List<int> { 0,1,2,1,3,2,2,3,7,3,5,7,5,4,7,5,6,4,3,8,9,3,9,5 }; Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh BurstRosette(int rays, float inner, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); rays = Mathf.Clamp(rays, 3, 24);
            for (var index = 0; index < rays; index++)
            {
                var angle = index * Mathf.PI * 2f / rays; var width = Mathf.PI / rays * .24f; var outer = .72f + Hash01(seed + (uint)index) * .32f; var start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(angle - width), Mathf.Sin(angle - width)) * inner); vertices.Add(new Vector3(Mathf.Cos(angle + width), Mathf.Sin(angle + width)) * inner); vertices.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * outer);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(new Vector2(.5f, 1f)); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh SpikeCluster(int count, string pattern, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); count = Mathf.Clamp(count, 3, 9);
            for (var index = 0; index < count; index++)
            {
                Vector3 center;
                if (pattern == "ring") { var angle = index * Mathf.PI * 2f / count; center = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * .55f; }
                else if (pattern == "line") center = new Vector3(Mathf.Lerp(-.72f, .72f, index / (float)(count - 1)), 0f, 0f);
                else { var t = count == 1 ? .5f : index / (float)(count - 1); var angle = Mathf.Lerp(-.85f, .85f, t); center = new Vector3(Mathf.Sin(angle) * .72f, 0f, Mathf.Cos(angle) * .28f); }
                AddPyramid(vertices, uv, triangles, center, .13f + Hash01(seed + (uint)index) * .08f, .65f + Hash01(seed + (uint)(index + 31)) * .35f);
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh WindCutSheet(int count, float scale, string direction, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); var sign = direction == "west" ? -1f : 1f;
            for (var index = 0; index < count; index++)
            {
                var x = (Hash01(seed + (uint)(index * 7)) * 2f - 1f) * .8f; var y = (Hash01(seed + (uint)(index * 11 + 1)) * 2f - 1f) * .55f; var length = .12f + Hash01(seed + (uint)(index * 13 + 2)) * .22f; var start = vertices.Count;
                vertices.Add(new Vector3(x, y)); vertices.Add(new Vector3(x + sign * length, y - length * .35f)); vertices.Add(new Vector3(x + sign * length * .82f, y - length * .35f - .035f));
                uv.Add(Vector2.zero); uv.Add(Vector2.one); uv.Add(Vector2.right); triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh Prism(float height, float width, int variant)
        {
            var depth = width * (.65f + variant * .04f); var vertices = new List<Vector3>
            {
                new Vector3(0,height*.5f,0),new Vector3(-width,-height*.35f,depth),new Vector3(width,-height*.35f,depth),new Vector3(0,-height*.5f,-depth)
            };
            var uv = new List<Vector2> { new Vector2(.5f,1f),Vector2.zero,Vector2.right,new Vector2(.5f,0f) };
            var triangles = new List<int> { 0,1,2,0,3,1,0,2,3,1,3,2 }; return Mesh(vertices, uv, triangles);
        }

        private static Mesh FacetedShell(int sides, float scale, float thickness)
        {
            return MultiRing(sides, 1, 1f - thickness, 1f, scale);
        }

        private static Mesh CrystalPetals(int petals, float scale)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); petals = Mathf.Clamp(petals, 3, 10);
            for (var index = 0; index < petals; index++)
            {
                var angle = index * Mathf.PI * 2f / petals; var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)); var side = new Vector3(-direction.y, direction.x); var start = vertices.Count;
                vertices.Add(direction * .35f - side * .12f); vertices.Add(direction * .35f + side * .12f); vertices.Add(direction * 1f); vertices.Add(direction * .58f + Vector3.forward * .18f);
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(new Vector2(.5f,1f)); uv.Add(new Vector2(.5f,.5f)); triangles.AddRange(new[] { start,start+3,start+1,start,start+2,start+3,start+1,start+3,start+2 });
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh VerticalCrystalShell(int spikes, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var index = 0; index < spikes; index++)
            {
                var angle = index * Mathf.PI * 2f / spikes; var center = new Vector3(Mathf.Cos(angle) * .38f, -.48f, Mathf.Sin(angle) * .38f); AddPyramid(vertices, uv, triangles, center, .14f, .82f + Hash01(seed + (uint)index) * .28f);
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh MultiRing(int segments, int ringCount, float inner, float outer, float scale)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>(); ringCount = Mathf.Clamp(ringCount, 1, 4);
            for (var ring = 0; ring < ringCount; ring++)
            {
                var factor = ringCount == 1 ? 1f : Mathf.Lerp(.55f, 1f, ring / (float)(ringCount - 1)); var baseIndex = vertices.Count;
                for (var index = 0; index <= segments; index++)
                {
                    var angle = index * Mathf.PI * 2f / segments; var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)); vertices.Add(direction * inner * factor * scale); vertices.Add(direction * outer * factor * scale); uv.Add(new Vector2(index / (float)segments,0f)); uv.Add(new Vector2(index / (float)segments,1f));
                    if (index < segments) QuadTriangles(triangles, baseIndex + index * 2, baseIndex + index * 2 + 1, baseIndex + index * 2 + 3, baseIndex + index * 2 + 2);
                }
            }
            return Mesh(vertices, uv, triangles);
        }

        private static Mesh CloudRing(int lobes, float scale, uint seed)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var lobe = 0; lobe < lobes; lobe++)
            {
                var angle = lobe * Mathf.PI * 2f / lobes; var center = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .45f) * .48f; var radius = .24f + Hash01(seed + (uint)lobe) * .08f; var start = vertices.Count; vertices.Add(center); uv.Add(new Vector2(.5f,.5f));
                for (var side = 0; side <= 8; side++) { var a = side * Mathf.PI * 2f / 8; vertices.Add(center + new Vector3(Mathf.Cos(a),Mathf.Sin(a))*radius); uv.Add(new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f)); }
                for (var side = 0; side < 8; side++) { triangles.Add(start); triangles.Add(start+side+1); triangles.Add(start+side+2); }
            }
            Scale(vertices, scale); return Mesh(vertices, uv, triangles);
        }

        private static Mesh Sphere(int longitude, int latitude, float scale, float ripple)
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (var lat = 0; lat <= latitude; lat++) for (var lon = 0; lon <= longitude; lon++)
            {
                var v = lat / (float)latitude; var u = lon / (float)longitude; var phi = v * Mathf.PI; var theta = u * Mathf.PI * 2f; var radius = scale * (1f + ripple * Mathf.Sin(theta * 3f) * Mathf.Sin(phi));
                vertices.Add(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta))*radius); uv.Add(new Vector2(u,v));
                if (lat < latitude && lon < longitude) { var a = lat * (longitude + 1) + lon; var b = a + longitude + 1; triangles.AddRange(new[] { a,b,a+1,a+1,b,b+1 }); }
            }
            return Mesh(vertices, uv, triangles);
        }

        private static void AddPyramid(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 center, float radius, float height)
        {
            var start = vertices.Count; vertices.Add(center + Vector3.up * height); uv.Add(new Vector2(.5f,1f));
            for (var side = 0; side < 4; side++) { var angle = side * Mathf.PI * .5f + Mathf.PI * .25f; vertices.Add(center + new Vector3(Mathf.Cos(angle)*radius,0f,Mathf.Sin(angle)*radius)); uv.Add(new Vector2((side&1)==0?0f:1f,side<2?0f:1f)); }
            for (var side = 0; side < 4; side++) { triangles.Add(start); triangles.Add(start+1+side); triangles.Add(start+1+(side+1)%4); }
            triangles.AddRange(new[] { start+1,start+4,start+3,start+1,start+3,start+2 });
        }

        private static void QuadTriangles(List<int> triangles, int a, int b, int c, int d) { triangles.AddRange(new[] { a,b,c,a,c,d }); }
        private static void Scale(List<Vector3> values, float scale) { for (var index = 0; index < values.Count; index++) values[index] *= scale; }
        private static Mesh Mesh(List<Vector3> vertices, List<Vector2> uv, List<int> triangles) { var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh; }
        private static float Hash01(uint value) { unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; return (value & 0x00ffffffu) / 16777215f; } }

        private static void Save(string path, Mesh source)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(source,path); return; }
            EditorUtility.CopySerialized(source,existing); existing.name=Path.GetFileNameWithoutExtension(path); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(source);
        }

        private static string RoleName(int role) { return role == 0 ? "Primary" : role == 1 ? "Highlight" : role == 2 ? "Outer" : role == 3 ? "Residual" : "Event"; }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\','/'); if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
    }
}
