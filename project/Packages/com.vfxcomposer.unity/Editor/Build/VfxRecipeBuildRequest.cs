namespace VFXComposer.Editor.Build
{
    /// <summary>
    /// One restricted build request. The caller supplies an out-of-project staged recipe file plus the
    /// canonical hash the user confirmed; the entry point trusts neither and recomputes the hash itself
    /// (ADR-007 §2.3).
    /// </summary>
    public sealed class VfxRecipeBuildRequest
    {
        public const string SchemaVersion = "vfxcomposer.recipe-build-request/1";

        /// <summary>Draft identity echoed into the result so the caller can correlate without a path.</summary>
        public string DraftId;

        /// <summary>Absolute path of the staged recipe JSON. Must live outside the Unity project.</summary>
        public string RecipePath;

        /// <summary>Lowercase hex SHA-256 of the canonical recipe text, as bound at confirmation time.</summary>
        public string ExpectedCanonicalSha256;

        /// <summary>The template catalog version the recipe declares. Recorded, never a hard gate.</summary>
        public string DeclaredTemplateCatalogVersion;
    }
}
