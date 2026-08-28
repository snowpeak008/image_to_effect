using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.Editor.Elements
{
    internal static partial class ElementNextCandidateMeshFactory
    {
        private static Mesh CreateW6W8(ElementNextCandidatePlan plan, int role, float scale)
        {
            switch (plan.Profile)
            {
                case ElementNextCandidateProfile.WaterJet: return role>=3?Crown(1,10,scale,plan.Seed+(uint)role):StrandJet(3+Mathf.RoundToInt(plan.Number("pressure",6f)*.35f),plan.Number("foam_amount",.5f),scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.TidalWave: return CurlWall(16,plan.Number("curl_amount",.65f),scale);
                case ElementNextCandidateProfile.BubbleShield: return WobbleShell(24,plan.Number("wobble",.28f),scale);
                case ElementNextCandidateProfile.SplashImpact: return Crown(plan.Integer("ring_count",1),12,scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.Whirlpool: return SpiralRibbon(28,2.5f+plan.Number("spin_accel",8f)*.08f,.08f,scale);
                case ElementNextCandidateProfile.Tornado: return FunnelRibbon(30,plan.Number("height",3.5f),scale);
                case ElementNextCandidateProfile.WindBlade: return ThinArcSet(plan.Integer("blade_count",3),26,scale);
                case ElementNextCandidateProfile.GaleDash: return FlowLineSheet(Mathf.Clamp(plan.Integer("line_density",14),6,24),scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.EarthSpike: return WedgeFault(plan.Integer("spike_count",6),scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.Boulder: return Sphere(8,5,scale,.16f);
                case ElementNextCandidateProfile.QuakeStomp: return CrackPlate(plan.Integer("crack_count",5),scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.ThornSnare: return ThornRing(Mathf.Clamp(plan.Integer("thorn_density",16),8,32),scale);
                case ElementNextCandidateProfile.VineWhip: return SineVine(24,plan.Number("wave_amp",.5f),scale);
                case ElementNextCandidateProfile.HealingBloom: return BotanicalBloom(plan.Integer("flower_count",5),scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.SporeBurst: return SporeCloud(9,scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.AcidLob: return role==3?IrregularPool(14,scale,plan.Seed):ViscousBlob(14,scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.DivineSmite: return role==3?MultiRing(32,1,.76f,1f,scale):TaperedPillar(10,scale);
                case ElementNextCandidateProfile.HolyHalo: return EllipseCross(28,plan.Number("halo_tilt",24f),scale);
                case ElementNextCandidateProfile.Resurrection: return role==1||role==2?TaperedPillar(10,scale):RuneGate(24,scale);
                case ElementNextCandidateProfile.ShadowClaw: return ClawTears(plan.Integer("claw_count",3),plan.Number("tear_jaggedness",.6f),scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.VoidOrb: return Sphere(role==0?12:16,role==0?7:9,scale,role==0?.02f:.12f);
                case ElementNextCandidateProfile.ShadowGrasp: return role==4?GraspHands(plan.Integer("hand_count",3),scale):IrregularPool(18,scale,plan.Seed+(uint)role);
                case ElementNextCandidateProfile.CurseMark: return CurseGlyph(plan.Integer("mark_glyph",2),scale);
                case ElementNextCandidateProfile.ArcaneMissile: return MissileFan(plan.Integer("missile_count",3),scale);
                default: return ArcaneRuneRing(plan.Integer("glyph_count",10),plan.Text("activate_order","forward"),plan.Seed,scale);
            }
        }

        private static Mesh StrandJet(int strands,float foam,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();strands=Mathf.Clamp(strands,3,8);
            for(var strand=0;strand<strands;strand++)
            {
                var y=(strand-(strands-1)*.5f)*(.055f+foam*.018f);var start=v.Count;var segments=8;
                for(var i=0;i<=segments;i++)
                {
                    var x=i/(float)segments-.5f;var wobble=(Hash01(seed+(uint)(strand*31+i))- .5f)*(.025f+foam*.035f);var width=.018f+foam*.012f;
                    v.Add(new Vector3(x,y+wobble-width,0));v.Add(new Vector3(x,y+wobble+width,0));u.Add(new Vector2(x,0));u.Add(new Vector2(x,1));if(i<segments)QuadTriangles(t,start+i*2,start+i*2+1,start+i*2+3,start+i*2+2);
                }
            }
            Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh CurlWall(int segments,float curl,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<=segments;i++)
            {
                var q=i/(float)segments;var angle=q*Mathf.PI*(.45f+curl*.75f);var x=(q-.5f)*1.8f+Mathf.Sin(angle)*curl*.28f;var y=q*1.45f+Mathf.Cos(angle)*curl*.24f;var width=.08f+q*.1f;v.Add(new Vector3(x-width,y));v.Add(new Vector3(x+width,y));u.Add(new Vector2(q,0));u.Add(new Vector2(q,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);
            }
            Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh WobbleShell(int segments,float wobble,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<=segments;i++){var a=i*Mathf.PI*2f/segments;var r=1f+Mathf.Sin(a*3f)*wobble*.08f;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*1.06f)*r;v.Add(d*(.91f)*scale);v.Add(d*scale);u.Add(new Vector2(i/(float)segments,0));u.Add(new Vector2(i/(float)segments,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);}return Mesh(v,u,t);
        }

        private static Mesh Crown(int rings,int points,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();rings=Mathf.Clamp(rings,1,2);
            for(var ring=0;ring<rings;ring++)for(var i=0;i<points;i++){var a=i*Mathf.PI*2f/points;var half=Mathf.PI/points*.34f;var inner=.28f+ring*.2f;var tip=.72f+ring*.18f+Hash01(seed+(uint)(ring*41+i))*.24f;var start=v.Count;v.Add(new Vector3(Mathf.Cos(a-half)*inner,Mathf.Sin(a-half)*inner));v.Add(new Vector3(Mathf.Cos(a+half)*inner,Mathf.Sin(a+half)*inner));v.Add(new Vector3(Mathf.Cos(a)*tip,Mathf.Sin(a)*tip, .2f+Hash01(seed+(uint)(i+99))*.25f));u.Add(Vector2.zero);u.Add(Vector2.right);u.Add(new Vector2(.5f,1));t.Add(start);t.Add(start+2);t.Add(start+1);}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh SpiralRibbon(int segments,float turns,float width,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<=segments;i++){var q=i/(float)segments;var a=q*Mathf.PI*2f*turns;var r=Mathf.Lerp(.12f,1f,q);var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var side=new Vector3(-d.y,d.x)*width;v.Add((d*r-side)*scale);v.Add((d*r+side)*scale);u.Add(new Vector2(q,0));u.Add(new Vector2(q,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);}return Mesh(v,u,t);
        }

        private static Mesh FunnelRibbon(int segments,float authoredHeight,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();var widthFactor=Mathf.Clamp(authoredHeight/3.5f,.85f,1.15f);
            for(var i=0;i<=segments;i++){var q=i/(float)segments;var a=q*Mathf.PI*7f;var r=Mathf.Lerp(.16f,.82f,q)*widthFactor;var center=new Vector3(Mathf.Cos(a)*r,q*1.7f,Mathf.Sin(a)*r);var side=new Vector3(-Mathf.Sin(a),0,Mathf.Cos(a))*.055f;v.Add((center-side)*scale);v.Add((center+side)*scale);u.Add(new Vector2(q,0));u.Add(new Vector2(q,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);}return Mesh(v,u,t);
        }

        private static Mesh ThinArcSet(int count,int segments,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();count=Mathf.Clamp(count,1,3);
            for(var blade=0;blade<count;blade++){var start=v.Count;for(var i=0;i<=segments;i++){var q=i/(float)segments;var a=Mathf.Lerp(-1.05f,.95f,q);var r=.72f+blade*.16f;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var side=d.normalized*.018f;v.Add((d*r-side)*scale+Vector3.up*(blade-1)*.08f);v.Add((d*r+side)*scale+Vector3.up*(blade-1)*.08f);u.Add(new Vector2(q,0));u.Add(new Vector2(q,1));if(i<segments)QuadTriangles(t,start+i*2,start+i*2+1,start+i*2+3,start+i*2+2);}}return Mesh(v,u,t);
        }

        private static Mesh FlowLineSheet(int count,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<count;i++){var y=(Hash01(seed+(uint)(i*17))*2f-1f)*.7f;var x=Hash01(seed+(uint)(i*23+1))*.45f-.7f;var length=.35f+Hash01(seed+(uint)(i*29+2))*.85f;var width=.008f+Hash01(seed+(uint)(i*31+3))*.012f;var start=v.Count;v.Add(new Vector3(x,y-width));v.Add(new Vector3(x,y+width));v.Add(new Vector3(x+length,y+width*.2f));v.Add(new Vector3(x+length,y-width*.2f));u.Add(Vector2.zero);u.Add(Vector2.up);u.Add(Vector2.one);u.Add(Vector2.right);QuadTriangles(t,start,start+1,start+2,start+3);}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh WedgeFault(int count,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();count=Mathf.Clamp(count,5,8);
            for(var i=0;i<count;i++){var q=count==1?.5f:i/(float)(count-1);var center=new Vector3(Mathf.Lerp(-.9f,.9f,q),0,SignedHash(seed+(uint)i)*.12f);AddSequencedPyramid(v,u,t,center,.13f+Hash01(seed+(uint)(i+51))*.09f,.45f+q*.5f,q);}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh CrackPlate(int cracks,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();cracks=Mathf.Clamp(cracks,4,6);
            for(var c=0;c<cracks;c++){var a=c*Mathf.PI*2f/cracks;var side=new Vector3(-Mathf.Sin(a),Mathf.Cos(a))*.025f;var start=v.Count;for(var i=0;i<=5;i++){var q=i/5f;var center=new Vector3(Mathf.Cos(a),Mathf.Sin(a))*q*(.65f+Hash01(seed+(uint)c)*.3f)+side*SignedHash(seed+(uint)(c*17+i))*q*2f;v.Add((center-side)*scale);v.Add((center+side)*scale);u.Add(new Vector2(q,0));u.Add(new Vector2(q,1));if(i<5)QuadTriangles(t,start+i*2,start+i*2+1,start+i*2+3,start+i*2+2);}}return Mesh(v,u,t);
        }

        private static Mesh ThornRing(int count,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<count;i++){var a=i*Mathf.PI*2f/count;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var side=new Vector3(-d.y,d.x);var start=v.Count;v.Add((d*.72f-side*.045f)*scale);v.Add((d*.72f+side*.045f)*scale);v.Add(d*(i%2==0?1.05f:.92f)*scale);u.Add(Vector2.zero);u.Add(Vector2.right);u.Add(new Vector2(.5f,1));t.Add(start);t.Add(start+2);t.Add(start+1);}return Mesh(v,u,t);
        }

        private static Mesh SineVine(int segments,float amplitude,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();var width=.045f;
            for(var i=0;i<=segments;i++){var q=i/(float)segments;var center=new Vector3(Mathf.Lerp(-.8f,.8f,q),Mathf.Sin(q*Mathf.PI*2f)*amplitude*.25f);v.Add((center+Vector3.down*width)*scale);v.Add((center+Vector3.up*width)*scale);u.Add(new Vector2(q,0));u.Add(new Vector2(q,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);}return Mesh(v,u,t);
        }

        private static Mesh BotanicalBloom(int flowers,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();flowers=Mathf.Clamp(flowers,4,6);
            for(var flower=0;flower<flowers;flower++){var center=flower==0?Vector3.zero:new Vector3(Mathf.Cos(flower*Mathf.PI*2f/flowers),Mathf.Sin(flower*Mathf.PI*2f/flowers))*.42f;for(var petal=0;petal<5;petal++){var a=petal*Mathf.PI*2f/5f+Hash01(seed+(uint)flower)*.2f;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var s=new Vector3(-d.y,d.x);var start=v.Count;v.Add(center);v.Add(center+d*.2f-s*.09f);v.Add(center+d*(.42f+Hash01(seed+(uint)(flower*11+petal))*.08f));v.Add(center+d*.2f+s*.09f);u.Add(new Vector2(.5f,0));u.Add(Vector2.zero);u.Add(new Vector2(.5f,1));u.Add(Vector2.right);t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh SporeCloud(int lobes,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var l=0;l<lobes;l++){var a=l*Mathf.PI*2f/lobes;var center=new Vector3(Mathf.Cos(a),Mathf.Sin(a))*(.25f+Hash01(seed+(uint)l)*.35f);var r=.18f+Hash01(seed+(uint)(l+37))*.18f;var start=v.Count;v.Add(center);u.Add(new Vector2(.5f,.5f));for(var s=0;s<=7;s++){var x=s*Mathf.PI*2f/7f;v.Add(center+new Vector3(Mathf.Cos(x),Mathf.Sin(x))*r);u.Add(new Vector2(.5f+Mathf.Cos(x)*.5f,.5f+Mathf.Sin(x)*.5f));}for(var s=0;s<7;s++){t.Add(start);t.Add(start+s+1);t.Add(start+s+2);}}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh ViscousBlob(int sides,float scale,uint seed){return IrregularDisk(sides,.1f,1f,scale,seed);}
        private static Mesh IrregularPool(int sides,float scale,uint seed){return IrregularDisk(sides,.58f,1f,scale,seed);}
        private static Mesh IrregularDisk(int sides,float inner,float outer,float scale,uint seed)
        {
            var v=new List<Vector3>{Vector3.zero};var u=new List<Vector2>{new Vector2(.5f,.5f)};var t=new List<int>();
            for(var i=0;i<=sides;i++){var a=i*Mathf.PI*2f/sides;var r=Mathf.Lerp(inner,outer,.72f+Hash01(seed+(uint)(i*19))*.28f);v.Add(new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*(inner>.5f?.72f:1f))*scale);u.Add(new Vector2(.5f+Mathf.Cos(a)*.5f,.5f+Mathf.Sin(a)*.5f));if(i<sides){t.Add(0);t.Add(i+1);t.Add(i+2);}}return Mesh(v,u,t);
        }

        private static Mesh TaperedPillar(int sides,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var y=0;y<=1;y++)for(var i=0;i<=sides;i++){var a=i*Mathf.PI*2f/sides;var r=y==0?.62f:.42f;v.Add(new Vector3(Mathf.Cos(a)*r,y*1.6f-.8f,Mathf.Sin(a)*r)*scale);u.Add(new Vector2(i/(float)sides,y));if(y==0&&i<sides){int a0=i,b=i+1,c=(sides+1)+i+1,d=(sides+1)+i;QuadTriangles(t,a0,b,c,d);}}return Mesh(v,u,t);
        }

        private static Mesh EllipseCross(int segments,float tilt,float scale)
        {
            var ring=MultiRing(segments,1,.82f,1f,scale);var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();ring.GetVertices(v);ring.GetUVs(0,u);t.AddRange(ring.triangles);Object.DestroyImmediate(ring);var stretch=.58f+Mathf.Abs(tilt)/160f;for(var i=0;i<v.Count;i++)v[i]=new Vector3(v[i].x,v[i].y*stretch,v[i].z);var b=v.Count;v.AddRange(new[]{new Vector3(-.08f,-.42f),new Vector3(.08f,-.42f),new Vector3(.08f,.42f),new Vector3(-.08f,.42f),new Vector3(-.36f,-.08f),new Vector3(.36f,-.08f),new Vector3(.36f,.08f),new Vector3(-.36f,.08f)});for(var i=0;i<8;i++)u.Add(new Vector2((i&1),i<4?(i<2?0:1):(i<6?0:1)));t.AddRange(new[]{b,b+1,b+2,b,b+2,b+3,b+4,b+5,b+6,b+4,b+6,b+7});ScaleTail(v,b,scale);return Mesh(v,u,t);
        }

        private static Mesh RuneGate(int segments,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<=segments;i++){var a=i*Mathf.PI*2f/segments;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));v.Add(d*.72f*scale);v.Add(d*scale);u.Add(new Vector2(i/(float)segments,0));u.Add(new Vector2(i/(float)segments,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);}for(var g=0;g<8;g++){var a=g*Mathf.PI*2f/8f;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var s=new Vector3(-d.y,d.x)*.06f;var b=v.Count;v.Add((d*.78f-s)*scale);v.Add((d*.78f+s)*scale);v.Add(d*1.14f*scale);u.Add(Vector2.zero);u.Add(Vector2.right);u.Add(new Vector2(.5f,1));t.AddRange(new[]{b,b+2,b+1});}return Mesh(v,u,t);
        }

        private static Mesh ClawTears(int count,float jag,float scale,uint seed)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();count=Mathf.Clamp(count,2,4);
            for(var claw=0;claw<count;claw++){var start=v.Count;for(var i=0;i<=8;i++){var q=i/8f;var reveal=(claw+q)/count;var x=Mathf.Lerp(-.82f,.82f,q);var y=(claw-(count-1)*.5f)*.28f+Mathf.Sin(q*Mathf.PI)*.18f+(Hash01(seed+(uint)(claw*31+i))-.5f)*jag*.12f;var width=.035f*(1f-q*.65f);v.Add(new Vector3(x,y-width)*scale);v.Add(new Vector3(x,y+width)*scale);u.Add(new Vector2(reveal,0));u.Add(new Vector2(reveal,1));if(i<8)QuadTriangles(t,start+i*2,start+i*2+1,start+i*2+3,start+i*2+2);}}return Mesh(v,u,t);
        }

        private static Mesh GraspHands(int count,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();count=Mathf.Clamp(count,2,3);
            for(var hand=0;hand<count;hand++){var x=(hand-(count-1)*.5f)*.55f;var b=v.Count;v.AddRange(new[]{new Vector3(x-.12f,-.5f),new Vector3(x+.12f,-.5f),new Vector3(x+.1f,.28f),new Vector3(x+.2f,.72f),new Vector3(x+.06f,.32f),new Vector3(x,.82f),new Vector3(x-.06f,.32f),new Vector3(x-.2f,.7f),new Vector3(x-.1f,.28f)});for(var i=0;i<9;i++)u.Add(new Vector2((i%3)*.5f,i/3f*.33f));t.AddRange(new[]{b,b+1,b+2,b,b+2,b+8,b+2,b+3,b+4,b+4,b+5,b+6,b+6,b+7,b+8,b+2,b+4,b+8,b+4,b+6,b+8});}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh CurseGlyph(int variant,float scale)
        {
            var segments=Mathf.Clamp(variant+3,4,7);var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();
            for(var i=0;i<=segments;i++){var a=i*Mathf.PI*2f/segments+(variant%2)*.24f;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));v.Add(d*.62f*scale);v.Add(d*.82f*scale);u.Add(new Vector2(i/(float)segments,0));u.Add(new Vector2(i/(float)segments,1));if(i<segments)QuadTriangles(t,i*2,i*2+1,i*2+3,i*2+2);}var b=v.Count;v.AddRange(new[]{Vector3.left*.55f*scale,Vector3.right*.55f*scale,Vector3.up*.68f*scale,Vector3.down*.68f*scale});u.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one});t.AddRange(new[]{b,b+2,b+1,b,b+1,b+3});return Mesh(v,u,t);
        }

        private static Mesh MissileFan(int count,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();count=Mathf.Clamp(count,1,5);
            for(var i=0;i<count;i++){var y=(i-(count-1)*.5f)*.22f;var q=count==1?0f:i/(float)(count-1);var b=v.Count;v.AddRange(new[]{new Vector3(-.45f,y),new Vector3(0,y+.13f),new Vector3(.45f,y),new Vector3(0,y-.13f)});u.AddRange(new[]{new Vector2(q,0),new Vector2(q,.33f),new Vector2(q,1),new Vector2(q,.66f)});t.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});}Scale(v,scale);return Mesh(v,u,t);
        }

        private static Mesh ArcaneRuneRing(int glyphs,string order,uint seed,float scale)
        {
            var v=new List<Vector3>();var u=new List<Vector2>();var t=new List<int>();glyphs=Mathf.Clamp(glyphs,8,12);
            for(var g=0;g<glyphs;g++){var a=g*Mathf.PI*2f/glyphs;var d=new Vector3(Mathf.Cos(a),Mathf.Sin(a));var s=new Vector3(-d.y,d.x)*.065f;var c=d*.82f;var b=v.Count;var q=ActivationRank(g,glyphs,order,seed)/(float)Mathf.Max(1,glyphs-1);v.Add((c-s-d*.1f)*scale);v.Add((c+s-d*.1f)*scale);v.Add((c+s+d*.1f)*scale);v.Add((c-s+d*.1f)*scale);u.AddRange(new[]{new Vector2(q,0),new Vector2(q,.33f),new Vector2(q,1),new Vector2(q,.66f)});QuadTriangles(t,b,b+1,b+2,b+3);}return Mesh(v,u,t);
        }

        private static void AddSequencedPyramid(List<Vector3> vertices,List<Vector2> uv,List<int> triangles,Vector3 center,float radius,float height,float sequence)
        {
            var start=vertices.Count;vertices.Add(center+Vector3.up*height);uv.Add(new Vector2(sequence,1));for(var side=0;side<4;side++){var angle=side*Mathf.PI*.5f+Mathf.PI*.25f;vertices.Add(center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));uv.Add(new Vector2(sequence,side*.25f));}for(var side=0;side<4;side++){triangles.Add(start);triangles.Add(start+1+side);triangles.Add(start+1+(side+1)%4);}triangles.AddRange(new[]{start+1,start+4,start+3,start+1,start+3,start+2});
        }

        private static int ActivationRank(int logical,int count,string order,uint seed)
        {
            if(order=="reverse")return count-1-logical;if(order!="seeded_random")return logical;var key=Hash01(seed+(uint)logical*2654435761u);var rank=0;for(var i=0;i<count;i++){if(i==logical)continue;var other=Hash01(seed+(uint)i*2654435761u);if(other<key||(Mathf.Approximately(other,key)&&i<logical))rank++;}return rank;
        }

        private static float SignedHash(uint value){return Hash01(value)*2f-1f;}
        private static void ScaleTail(List<Vector3> values,int start,float scale){for(var i=start;i<values.Count;i++)values[i]*=scale;}
    }
}
