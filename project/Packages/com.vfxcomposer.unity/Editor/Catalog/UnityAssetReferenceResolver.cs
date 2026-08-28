using UnityEditor;

namespace VFXComposer.Editor.Catalog
{
    /// <summary>Production AssetDatabase resolver used when cataloguing formal Unity template assets.</summary>
    public sealed class UnityAssetReferenceResolver : IAssetReferenceResolver
    {
        public AssetReferenceResolution Resolve(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            return new AssetReferenceResolution { Found = !string.IsNullOrEmpty(path), AssetPath = path };
        }
    }
}
