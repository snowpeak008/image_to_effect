namespace VFXComposer.Editor.Build
{
    // Test-only seam for verifying recovery after the destructive commit has started. Production callers have no hook.
    internal interface IVfxCompilerBuildHook
    {
        void AfterPrefabAndMaterialsSaved(string outputFolder);
    }
}
