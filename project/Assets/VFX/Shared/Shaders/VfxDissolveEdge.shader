Shader "VFXComposer/Style/DissolveEdge"
{
    Properties { _PrimaryColor("Primary",Color)=(.15,.01,.04,1) _SecondaryColor("Secondary",Color)=(.55,.02,.08,1) _AccentColor("Accent",Color)=(1,.15,.03,1) _Intensity("Intensity",Range(0,8))=1 _GlobalAlpha("Global Alpha",Range(0,1))=1 _StyleMode("Style Mode",Float)=6 _Phase("Phase",Range(0,1))=0 _NoiseScale("Noise Scale",Range(.01,32))=2 _Outline("Outline",Range(0,1))=.2 _ShadingSteps("Shading Steps",Range(1,8))=3 [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src",Float)=5 [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst",Float)=1 }
    Fallback "VFXComposer/Style/LayeredRamp"
}
