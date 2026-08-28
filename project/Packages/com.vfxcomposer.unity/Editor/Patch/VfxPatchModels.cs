using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Patch
{
    public enum VfxPatchOperationKind { Replace, Add, Remove, Enable, Disable, SetBehaviorParam, SetStyleToken, SetPalette, SetArchetypeParam, SetContentParam }
    public enum VfxPatchImpactState { Create, Update, Remove, Unchanged }

    public sealed class VfxPatchOperation
    {
        public int Index;
        public VfxPatchOperationKind Kind;
        public string Path;
        public JToken Value;
    }

    public sealed class VfxPatchImpactItem
    {
        public VfxPatchImpactState State;
        public string StageId;
        public string ModuleId;
        public bool IsStage;
    }

    public sealed class VfxPatchResult
    {
        public ValidationReport Report = new ValidationReport();
        public readonly List<VfxPatchImpactItem> AffectedItems = new List<VfxPatchImpactItem>();
        public int? FailedOperationIndex;
        public bool IsPostPatchValidationFailure;
        public int BeforeRevision;
        public int AfterRevision;
        public string BeforeCanonicalHash;
        public string AfterCanonicalHash;
        public string PatchedRecipeJson;
        public bool IsValid { get { return !Report.HasErrors; } }
    }
}
