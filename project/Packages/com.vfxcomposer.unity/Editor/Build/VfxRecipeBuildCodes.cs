namespace VFXComposer.Editor.Build
{
    /// <summary>
    /// Stable executor-layer codes for the restricted recipe build entry point (ADR-007 §5).
    ///
    /// They deliberately use the VFXB prefix rather than the compiler's E/I vocabulary: these are
    /// entry-point admission failures raised before or around the compiler, not recipe/asset
    /// diagnostics, and the closed E/I vocabulary is contract-audited against the release error
    /// code document. Compiler diagnostics keep flowing through unchanged inside the issue list.
    /// </summary>
    public static class VfxRecipeBuildCodes
    {
        /// <summary>The request file is absent, unreadable or not strict UTF-8.</summary>
        public const string RequestUnreadable = "VFXB0001";

        /// <summary>The request is not a known schema version, or carries unknown/invalid fields.</summary>
        public const string RequestInvalid = "VFXB0002";

        /// <summary>The staged recipe input is absent, unreadable, or located inside the Unity project.</summary>
        public const string RecipeInputRejected = "VFXB0003";

        /// <summary>The recomputed canonical hash differs from the hash the user confirmed.</summary>
        public const string ConfirmationHashMismatch = "VFXB0004";

        /// <summary>The recipe does not parse against the Recipe v1 domain.</summary>
        public const string RecipeUnparseable = "VFXB0005";

        /// <summary>The effect id is outside the accepted charset, too long, or a reserved device name.</summary>
        public const string EffectIdRejected = "VFXB0006";

        /// <summary>A computed write target falls outside the closed three-member write surface.</summary>
        public const string WriteSurfaceViolation = "VFXB0007";

        /// <summary>Authoritative L2 validation refused the recipe.</summary>
        public const string AuthoritativeValidationFailed = "VFXB0008";

        /// <summary>The formal template catalog could not be loaded or reported errors.</summary>
        public const string CatalogUnusable = "VFXB0009";

        /// <summary>The provenance recipe could not be written, or its rollback failed.</summary>
        public const string ProvenanceWriteFailed = "VFXB0010";

        /// <summary>Dry run reported a blocked plan; nothing was committed.</summary>
        public const string DryRunBlocked = "VFXB0011";

        /// <summary>Recipe, catalog or output identity moved between the approved plan and the commit.</summary>
        public const string PlanCommitDrift = "VFXB0012";

        /// <summary>The compiler refused or failed the build; its diagnostics are in the issue list.</summary>
        public const string BuildFailed = "VFXB0013";

        /// <summary>A committed write-surface member is missing or its recorded hashes disagree.</summary>
        public const string CommittedArtifactsUnverified = "VFXB0014";

        /// <summary>Residue that must be recovered by hand was found before the build started.</summary>
        public const string UnrecoverableResidue = "VFXB0015";
    }
}
