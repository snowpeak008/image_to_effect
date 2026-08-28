using UnityEngine;
using UnityEngine.UI;

namespace VFXComposer
{
    /// <summary>Procedural soft screen-edge damage vignette; no bitmap dependency and no hard debug frame.</summary>
    [DisallowMultipleComponent]
    public sealed class CoverageScreenFeedbackGraphic : MaskableGraphic
    {
        [SerializeField, Range(.72f, .94f)] private float innerScale = .86f;

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var outerColor = color; outerColor.a *= .34f;
            var middleColor = color; middleColor.a *= .13f;
            var innerColor = color; innerColor.a = 0f;
            AddRing(vertexHelper, rect, 1f, .94f, outerColor, middleColor);
            AddRing(vertexHelper, rect, .94f, innerScale, middleColor, innerColor);
        }

        private static void AddRing(VertexHelper helper, Rect rect, float outerScale, float innerScale, Color outerColor, Color innerColor)
        {
            var center=rect.center;var half=rect.size*.5f;var outerHalf=half*outerScale;var innerHalf=half*innerScale;
            var outer=new[]{center+new Vector2(-outerHalf.x,-outerHalf.y),center+new Vector2(-outerHalf.x,outerHalf.y),center+new Vector2(outerHalf.x,outerHalf.y),center+new Vector2(outerHalf.x,-outerHalf.y)};
            var inner=new[]{center+new Vector2(-innerHalf.x,-innerHalf.y),center+new Vector2(-innerHalf.x,innerHalf.y),center+new Vector2(innerHalf.x,innerHalf.y),center+new Vector2(innerHalf.x,-innerHalf.y)};
            for(var i=0;i<4;i++){var next=(i+1)%4;var start=helper.currentVertCount;helper.AddVert(outer[i],outerColor,Vector2.zero);helper.AddVert(outer[next],outerColor,Vector2.zero);helper.AddVert(inner[next],innerColor,Vector2.zero);helper.AddVert(inner[i],innerColor,Vector2.zero);helper.AddTriangle(start,start+1,start+2);helper.AddTriangle(start,start+2,start+3);}
        }
    }
}
