using UnityEditor;

namespace VFXComposer.Editor.Build
{
    /// <summary>Hashes a template Prefab together with its recursive Unity dependencies.</summary>
    internal interface ITemplateDependencyHashProvider
    {
        string GetDependencyHash(string assetPath);
    }

    internal sealed class UnityTemplateDependencyHashProvider : ITemplateDependencyHashProvider
    {
        public string GetDependencyHash(string assetPath) { return AssetDatabase.GetAssetDependencyHash(assetPath).ToString(); }
    }
}
