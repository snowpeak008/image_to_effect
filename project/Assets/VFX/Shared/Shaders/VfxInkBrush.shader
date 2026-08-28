Shader "VFXComposer/Style/InkBrush"
{
    Properties { _PrimaryColor("Primary",Color)=(.03,.04,.05,1) _SecondaryColor("Secondary",Color)=(.35,.38,.42,1) _AccentColor("Accent",Color)=(.8,.12,.08,1) _Intensity("Intensity",Range(0,8))=1 _GlobalAlpha("Global Alpha",Range(0,1))=1 _StyleMode("Style Mode",Float)=4 _Phase("Phase",Range(0,1))=0 _NoiseScale("Noise Scale",Range(.01,32))=3 _Outline("Outline",Range(0,1))=.3 _ShadingSteps("Shading Steps",Range(1,8))=3 [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src",Float)=5 [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst",Float)=10 }
    Fallback "VFXComposer/Style/LayeredRamp"
}
