using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Editor.Build
{
    public enum VfxBuildItemState { Create, Update, Unchanged, Blocked }

    public sealed class VfxBuildItem
    {
        public VfxBuildItemState State;
        public string AssetPath;
        public string Reason;
    }

    public sealed class VfxBuildPlan
    {
        public readonly List<VfxBuildItem> Items = new List<VfxBuildItem>();
        public ValidationReport Report = new ValidationReport();
        public int RecipeRevision;
        public string RecipeHash;
        public string BuildHash;
        // Attached only by DryRunProduction; a caller-created VfxBuildPlan has no authority.
        internal W24S5FormalApproval ProductionApproval;
        public bool IsBlocked { get { return Report.HasErrors || Items.Exists(item => item.State == VfxBuildItemState.Blocked); } }
    }

    public sealed class VfxBuildResult
    {
        public VfxBuildPlan Plan;
        public bool Succeeded;
        public string PrefabPath;
    }

    [Serializable]
    public sealed class VfxBuildManifest
    {
        public string RecipeId;
        public int RecipeRevision;
        public string RecipeHash;
        public string BuildHash;
        public string CompilerVersion;
        public string UnityVersion;
        public string OutputPrefabPath;
        public string GeneratedAtUtc;
        public VfxBuildCost Cost;
        public List<VfxBuildTemplate> Templates = new List<VfxBuildTemplate>();
    }

    [Serializable]
    public sealed class VfxBuildTemplate
    {
        public string TemplateId;
        public string TemplateVersion;
        public string AssetGuid;
        public string AssetPath;
        public string DependencyHash;
    }

    [Serializable]
    public sealed class VfxBuildCost
    {
        public int EstimatedPeakParticles;
        public int Materials;
        public int Trails;
        public double TotalDuration;
    }
}
