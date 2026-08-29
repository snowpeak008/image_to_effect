using System.Collections.Generic;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Build
{
    /// <summary>
    /// Structured outcome of one restricted build. It carries identities, the three committed
    /// write-surface members and diagnostics, never a machine-specific absolute path.
    /// </summary>
    public sealed class VfxRecipeBuildOutcome
    {
        public const string SchemaVersion = "vfxcomposer.recipe-build-result/1";

        public string DraftId;
        public bool Succeeded;

        /// <summary>Null when the build succeeded, otherwise a <see cref="VfxRecipeBuildCodes"/> member.</summary>
        public string FailureCode;

        public string EffectId;
        public string RecipeHash;
        public string BuildHash;
        public int RecipeRevision;
        public string CompilerVersion;
        public string UnityVersion;

        /// <summary>Author-declared catalog provenance stamp copied from the recipe metadata.</summary>
        public string DeclaredTemplateCatalogVersion;

        /// <summary>
        /// SHA-256 over the live catalog's template id / version / asset GUID triples. Unity has no
        /// catalog version constant, so this derived identity is what makes a catalog drift auditable;
        /// the hard gate remains per-template resolution plus authoritative validation.
        /// </summary>
        public string CatalogIdentityHash;

        /// <summary>Write-surface member 1: the Prefab under the generated asset root.</summary>
        public string PrefabPath;

        /// <summary>Write-surface member 1: the in-root build manifest beside the Prefab.</summary>
        public string BuildManifestPath;

        /// <summary>Write-surface member 2: the authoritative ownership manifest single point.</summary>
        public string OwnershipManifestPath;

        /// <summary>Write-surface member 3: the build provenance recipe.</summary>
        public string ProvenanceRecipePath;

        /// <summary>Create, Update or Unchanged as decided by the approved plan.</summary>
        public string DryRunState;

        /// <summary>Orphan temporary directories and pending residue removed before the build.</summary>
        public readonly List<string> CleanedResiduePaths = new List<string>();

        /// <summary>Compiler and validation diagnostics, in report order.</summary>
        public readonly List<ValidationEntry> Issues = new List<ValidationEntry>();
    }
}
