using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace VFXComposer.Editor.Domain
{
    public enum RecipeDimension { TwoD, ThreeD }
    public enum RecipeArchetype { Projectile, Impact, Slash, Aura, Area, Beam, Trail, Shield, Spawn, Transform, Composite, Environment, ScreenUi, Status, Decal, WeaponTrail, Destruction, LifeCycle, Portal, Loot }
    public enum TargetProfile { MobileMedium, PcEditor }
    public enum StageTrigger { Manual, AfterPrevious, OnLaunch, OnHit, OnEnd }
    public enum ModuleKind { EnergyBody, SpriteEmitter, SecondaryParticles, MotionTrail, ImpactFlash, ImpactBurst, Shockwave, SubEffect }
    public enum ManifestParameterType { Float, Integer, Boolean, String }
    public enum ValidationSeverity { Error, Warning, Info }

    public sealed class Recipe
    {
        public int RecipeVersion;
        public int Revision = 1;
        public string Id;
        public string Name;
        public RecipeDimension Dimension;
        public RecipeArchetype Archetype;
        public RecipeStyleContract Style;
        public RecipeBehaviorContract Behavior;
        public RecipeContentContract Content;
        public RecipeCompositeContract Composite = new RecipeCompositeContract();
        public readonly Dictionary<string, JToken> ArchetypeParameters = new Dictionary<string, JToken>(StringComparer.Ordinal);
        public TargetProfile TargetProfile;
        public uint RandomSeed;
        public List<RecipeStage> Stages = new List<RecipeStage>();
        public RecipeMetadata Metadata = new RecipeMetadata();
    }

    /// <summary>
    /// Composite-only orchestration data. These records reference Runtime Entry ids; they never
    /// embed or copy a child effect's owned assets.
    /// </summary>
    public sealed class RecipeCompositeContract
    {
        public readonly List<RecipeTimelineEvent> Timeline = new List<RecipeTimelineEvent>();
        public readonly List<RecipeCameraHint> CameraHints = new List<RecipeCameraHint>();
        public readonly List<RecipeStageGate> Gates = new List<RecipeStageGate>();
        public bool IsDeclared { get { return Timeline.Count > 0 || CameraHints.Count > 0 || Gates.Count > 0; } }
    }

    public sealed class RecipeTimelineEvent
    {
        public double Time;
        public string RefId;
        public string Action;
        public readonly Dictionary<string, JToken> Overrides = new Dictionary<string, JToken>(StringComparer.Ordinal);
    }

    public sealed class RecipeCameraHint
    {
        public double Time;
        public string Type;
        public double Strength;
    }

    public sealed class RecipeStageGate
    {
        public double Time;
        public string WaitFor;
    }

    /// <summary>
    /// Visual-content semantics are deliberately separate from behavior (mechanics) and style
    /// (rendering language). The family and parameter names are validated by the live content
    /// registry; this keeps an elemental skin from redefining motion or hit topology.
    /// </summary>
    public sealed class RecipeContentContract
    {
        public string Family;
        public readonly Dictionary<string, JToken> Parameters = new Dictionary<string, JToken>(StringComparer.Ordinal);
    }

    public sealed class RecipeStyleContract
    {
        public string Token = "stylized";
        public readonly Dictionary<string, string> Palette = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, JToken> Parameters = new Dictionary<string, JToken>(StringComparer.Ordinal);
        public bool UsedLegacyStringForm;
    }

    public sealed class RecipeBehaviorContract
    {
        public RecipeCapabilityBlock Motion;
        public RecipeCapabilityBlock Hit;
        public RecipeCapabilityBlock Emission;
        public RecipeCapabilityBlock Timing;

        public IEnumerable<RecipeCapabilityBlock> Blocks()
        {
            if (Motion != null) yield return Motion;
            if (Hit != null) yield return Hit;
            if (Emission != null) yield return Emission;
            if (Timing != null) yield return Timing;
        }
    }

    public sealed class RecipeCapabilityBlock
    {
        public string Domain;
        public string Type;
        public readonly Dictionary<string, JToken> Parameters = new Dictionary<string, JToken>(StringComparer.Ordinal);
    }

    public sealed class RecipeMetadata
    {
        public string CreatedBy;
        public string TemplateCatalogVersion;
    }

    public sealed class RecipeStage
    {
        public string Id;
        public StageTrigger Trigger;
        public double Duration;
        public bool Enabled;
        public List<RecipeModule> Modules = new List<RecipeModule>();
    }

    public sealed class RecipeModule
    {
        public string Id;
        public ModuleKind Kind;
        public string TemplateId;
        public Dictionary<string, JToken> Parameters = new Dictionary<string, JToken>(StringComparer.Ordinal);
        public string AttachTo;
        public bool Enabled;
    }

    public sealed class TemplateManifest
    {
        public int ManifestVersion;
        public string TemplateId;
        public string TemplateVersion;
        public ModuleKind Kind;
        public RecipeDimension Dimension;
        public string AssetGuid;
        public string AssetPath;
        public List<string> Tags = new List<string>();
        public Dictionary<string, ManifestParameter> Parameters = new Dictionary<string, ManifestParameter>(StringComparer.Ordinal);
        public TemplateCost Cost = new TemplateCost();
    }

    public sealed class ManifestParameter
    {
        public ManifestParameterType Type;
        public JToken Min;
        public JToken Max;
        public JToken Default;
        public string Binding;
    }

    public sealed class TemplateCost
    {
        public int EstimatedPeakParticles;
        public int Materials;
        public int Trails;
    }

    public sealed class ValidationEntry
    {
        public string Code;
        public ValidationSeverity Severity;
        public string Path;
        public string Message;
        public JToken ActualValue;
        public string AllowedRange;
    }

    public sealed class ValidationReport
    {
        public readonly List<ValidationEntry> Entries = new List<ValidationEntry>();
        public bool HasErrors { get { return Entries.Exists(entry => entry.Severity == ValidationSeverity.Error); } }

        public void Add(string code, ValidationSeverity severity, string path, string message, JToken actualValue = null, string allowedRange = null)
        {
            Entries.Add(new ValidationEntry
            {
                Code = code,
                Severity = severity,
                Path = path,
                Message = message,
                ActualValue = actualValue,
                AllowedRange = allowedRange
            });
        }

        public void AddRange(ValidationReport other)
        {
            if (other != null) Entries.AddRange(other.Entries);
        }

        public bool Contains(string code, string path)
        {
            return Entries.Exists(entry => entry.Code == code && entry.Path == path);
        }
    }

    public sealed class ParseResult<T>
    {
        public T Value;
        public ValidationReport Report = new ValidationReport();
    }
}
