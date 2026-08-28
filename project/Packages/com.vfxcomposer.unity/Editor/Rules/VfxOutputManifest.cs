using System;
using System.Collections.Generic;

namespace VFXComposer.Editor.Rules
{
    [Serializable]
    public sealed class VfxRuntimeEntryRecord
    {
        public string Kind;
        public string Path;
        public string Guid;
    }

    [Serializable]
    public sealed class VfxOwnedOutputRecord
    {
        public string Path;
        public string Guid;
        public string AssetType;
        public string Sha256;
    }

    [Serializable]
    public sealed class VfxDependencyRecord
    {
        public string Path;
        public string Guid;
        public string AssetType;
        public string Version;
        public string DependencyHash;
    }

    [Serializable]
    public sealed class VfxOutputCostRecord
    {
        public int Particles;
        public int ParticleSystems;
        public int Renderers;
        public int Materials;
        public int Trails;
        public double Duration;
        public long LocalTextureBytes;
        public long DependencyResidentTextureBytes;
        public int GameObjects;
        public int MaxDepth;
    }

    [Serializable]
    public sealed class VfxOutputAuditEntry
    {
        public string Code;
        public string Severity;
        public string Path;
        public string Message;
    }

    /// <summary>
    /// Serialized record shape only. It is deliberately mutable for Unity/JSON serialization,
    /// but no public production writer may accept one as authority; S5 constructs it internally.
    /// </summary>
    [Serializable]
    public sealed class VfxFormalProductionBinding
    {
        public string ContractPath;
        public string ContractFileHash;
        public string ContractHash;
        public int ContractRevision;
        public string TracePath;
        public string TraceFileHash;
        public string VisualStatus;
        public string EvidenceCorpusPath;
        public string EvidenceCorpusHash;
        public string UserVerdictRecordPath;
        public string UserVerdictRecordHash;
        // L3 is also derived from a persisted authority; callers never get to promote an
        // entry merely by passing VisualStatus=L3 in a request.
        public string VisualQaRecordPath;
        public string VisualQaRecordHash;
        public string S0aStatusRecordPath;
        public string S0aStatusRecordHash;
        // Set only by the internal S5 pre-C0 admission. It documents a one-time identity
        // population build and is never a visual, capture, or publication authorization.
        public string AdmissionPhase;
    }

    [Serializable]
    public sealed class VfxOutputManifest
    {
        public int ManifestVersion = 1;
        public string RulesVersion;
        public string Enforcement;
        public string EffectId;
        public string Archetype;
        public int RecipeVersion;
        public int RecipeRevision;
        public string RecipeHash;
        public string BuildHash;
        public string CompilerVersion;
        public string UnityVersion;
        public string SourceRecipePath;
        public VfxRuntimeEntryRecord RuntimeEntry;
        public List<VfxOwnedOutputRecord> OwnedOutputs = new List<VfxOwnedOutputRecord>();
        public List<VfxDependencyRecord> Dependencies = new List<VfxDependencyRecord>();
        public VfxOutputCostRecord Cost = new VfxOutputCostRecord();
        public List<VfxOutputAuditEntry> Audit = new List<VfxOutputAuditEntry>();
        public VfxFormalProductionBinding FormalProduction;
        public string GeneratedAtUtc;
    }
}
