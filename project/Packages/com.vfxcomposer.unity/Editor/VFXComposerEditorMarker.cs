using UnityEditor;

namespace VFXComposer.Editor
{
    internal static class VFXComposerEditorMarker
    {
        internal static bool IsEditor => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
