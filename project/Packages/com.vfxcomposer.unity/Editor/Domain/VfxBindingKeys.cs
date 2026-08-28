namespace VFXComposer.Editor.Domain
{
    /// <summary>Stable, allow-listed template parameter symbols. S6 handlers consume these values; they are never reflected into code.</summary>
    public static class VfxBindingKeys
    {
        public const string CoreScale = "core.scale";
        public const string EmbersRate = "embers.rate";
        public const string EmbersLifetime = "embers.lifetime";
        public const string ImpactCount = "impact.count";
        public const string ImpactSpeed = "impact.speed";
        public const string TrailTime = "trail.time";
        public const string TrailWidth = "trail.width";
        public const string LaunchLifetime = "launch.lifetime";
        public const string LaunchSize = "launch.size";
        public const string ShockwaveLifetime = "shockwave.lifetime";
        public const string ShockwaveEndSize = "shockwave.endSize";

        // 3D symbols deliberately differ from the 2D keys.  A Manifest selects an
        // implementation, never a Unity property path, so render-space rules stay
        // in the protected template/compiler boundary rather than leaking into Recipe.
        public const string ThreeDCoreScale = "3d.core.scale";
        public const string ThreeDEmbersRate = "3d.embers.rate";
        public const string ThreeDEmbersLifetime = "3d.embers.lifetime";
        public const string ThreeDImpactCount = "3d.impact.count";
        public const string ThreeDImpactSpeed = "3d.impact.speed";
        public const string ThreeDTrailTime = "3d.trail.time";
        public const string ThreeDTrailWidth = "3d.trail.width";
        public const string ThreeDLaunchLifetime = "3d.launch.lifetime";
        public const string ThreeDLaunchSize = "3d.launch.size";
        public const string ThreeDShockwaveLifetime = "3d.shockwave.lifetime";
        public const string ThreeDShockwaveEndSize = "3d.shockwave.endSize";
    }
}
