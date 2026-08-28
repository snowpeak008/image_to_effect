using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace VFXComposer.Protocol.Commands;

/// <summary>Closed command vocabulary. These identifiers are data, not authority grants.</summary>
public static class CommandKinds
{
    public const string ValidateRecipe = MessageKinds.ValidateRecipeCommand;
    public const string BuildCandidate = MessageKinds.BuildCandidateCommand;
    public const string OpenPreviewJob = MessageKinds.OpenPreviewJobCommand;
    public const string ClosePreviewJob = MessageKinds.ClosePreviewJobCommand;
    public const string SetPreviewPlayback = MessageKinds.SetPreviewPlaybackCommand;
    public const string ValidatePatch = MessageKinds.ValidatePatchCommand;
    public const string ApplyPatch = MessageKinds.ApplyPatchCommand;
    public const string RunFocusedTests = MessageKinds.RunFocusedTestsCommand;
    public const string CancelJob = MessageKinds.CancelJobCommand;

    private static readonly FrozenSet<string> KnownKinds =
        new[]
        {
            ValidateRecipe,
            BuildCandidate,
            OpenPreviewJob,
            ClosePreviewJob,
            SetPreviewPlayback,
            ValidatePatch,
            ApplyPatch,
            RunFocusedTests,
            CancelJob,
        }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownKinds;

    public static bool IsKnown(string? value) => value is not null && KnownKinds.Contains(value);

    internal static string Require(string value, string parameterName) =>
        IsKnown(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>Closed capability identifiers. A requested identifier does not admit a command.</summary>
public static class CommandCapabilityIds
{
    public const string ValidateRecipeV1 = "command.validate-recipe.v1";
    public const string BuildCandidateV1 = "command.build-candidate.v1";
    public const string OpenPreviewJobV1 = "command.open-preview-job.v1";
    public const string ClosePreviewJobV1 = "command.close-preview-job.v1";
    public const string SetPreviewPlaybackV1 = "command.set-preview-playback.v1";
    public const string ValidatePatchV1 = "command.validate-patch.v1";
    public const string ApplyPatchV1 = "command.apply-patch.v1";
    public const string RunFocusedTestsV1 = "command.run-focused-tests.v1";
    public const string CancelJobV1 = "command.cancel-job.v1";

    private static readonly FrozenDictionary<string, string> ByCommandKind =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CommandKinds.ValidateRecipe] = ValidateRecipeV1,
            [CommandKinds.BuildCandidate] = BuildCandidateV1,
            [CommandKinds.OpenPreviewJob] = OpenPreviewJobV1,
            [CommandKinds.ClosePreviewJob] = ClosePreviewJobV1,
            [CommandKinds.SetPreviewPlayback] = SetPreviewPlaybackV1,
            [CommandKinds.ValidatePatch] = ValidatePatchV1,
            [CommandKinds.ApplyPatch] = ApplyPatchV1,
            [CommandKinds.RunFocusedTests] = RunFocusedTestsV1,
            [CommandKinds.CancelJob] = CancelJobV1,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> KnownCapabilities =
        ByCommandKind.Values.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownCapabilities;

    public static bool IsKnown(string? value) => value is not null && KnownCapabilities.Contains(value);

    public static string ForCommand(string commandKind) =>
        ByCommandKind[CommandKinds.Require(commandKind, nameof(commandKind))];

    internal static string RequireForCommand(
        string commandKind,
        string capability,
        string parameterName) =>
        string.Equals(capability, ForCommand(commandKind), StringComparison.Ordinal)
            ? capability
            : throw new ArgumentException("Command capability does not match the command kind.", parameterName);
}

/// <summary>
/// A confirmation policy identifier names a frozen policy definition only. It never
/// carries a user, machine, visual, L3, or L4 verdict.
/// </summary>
public static class ConfirmationPolicyIds
{
    public const string ReferenceV1 = "confirmation.policy.reference.v1";

    private static readonly FrozenSet<string> KnownPolicies =
        new[] { ReferenceV1 }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownPolicies;

    public static bool IsKnown(string? value) => value is not null && KnownPolicies.Contains(value);

    internal static string Require(string value, string parameterName) =>
        IsKnown(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

internal static class CommandContentHashTypes
{
    internal const string RecipeContent = "vfxcomposer.recipe-content/1";
    internal const string RecipeContract = "vfxcomposer.recipe-contract/1";
    internal const string RecipeValidation = "vfxcomposer.recipe-validation/1";
    internal const string BuildDefinition = "vfxcomposer.build-definition/1";
    internal const string CandidateIdentity = "vfxcomposer.candidate-identity/1";
    internal const string PreviewIdentity = "vfxcomposer.preview-identity/1";
    internal const string PatchContent = "vfxcomposer.patch-content/1";
    internal const string PatchValidation = "vfxcomposer.patch-validation/1";
    internal const string FocusedTestPlan = "vfxcomposer.focused-test-plan/1";
    internal const string FocusedTestReport = "vfxcomposer.focused-test-report/1";
}

public sealed record CommandContractDescriptor(
    string MessageKind,
    string CommandKind,
    Type DtoType,
    string RequiredCapability,
    string SelfHashType);

/// <summary>One closed command kind/capability/version registry for wire data only.</summary>
public static class CommandContractRegistry
{
    private static readonly ReadOnlyCollection<CommandContractDescriptor> Descriptors =
        Array.AsReadOnly(
        [
            new CommandContractDescriptor(MessageKinds.ValidateRecipeCommand, CommandKinds.ValidateRecipe, typeof(ValidateRecipeCommand), CommandCapabilityIds.ValidateRecipeV1, ValidateRecipeCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.BuildCandidateCommand, CommandKinds.BuildCandidate, typeof(BuildCandidateCommand), CommandCapabilityIds.BuildCandidateV1, BuildCandidateCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.OpenPreviewJobCommand, CommandKinds.OpenPreviewJob, typeof(OpenPreviewJobCommand), CommandCapabilityIds.OpenPreviewJobV1, OpenPreviewJobCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.ClosePreviewJobCommand, CommandKinds.ClosePreviewJob, typeof(ClosePreviewJobCommand), CommandCapabilityIds.ClosePreviewJobV1, ClosePreviewJobCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.SetPreviewPlaybackCommand, CommandKinds.SetPreviewPlayback, typeof(SetPreviewPlaybackCommand), CommandCapabilityIds.SetPreviewPlaybackV1, SetPreviewPlaybackCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.ValidatePatchCommand, CommandKinds.ValidatePatch, typeof(ValidatePatchCommand), CommandCapabilityIds.ValidatePatchV1, ValidatePatchCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.ApplyPatchCommand, CommandKinds.ApplyPatch, typeof(ApplyPatchCommand), CommandCapabilityIds.ApplyPatchV1, ApplyPatchCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.RunFocusedTestsCommand, CommandKinds.RunFocusedTests, typeof(RunFocusedTestsCommand), CommandCapabilityIds.RunFocusedTestsV1, RunFocusedTestsCommand.SelfHashType),
            new CommandContractDescriptor(MessageKinds.CancelJobCommand, CommandKinds.CancelJob, typeof(CancelJobCommand), CommandCapabilityIds.CancelJobV1, CancelJobCommand.SelfHashType),
        ]);

    private static readonly FrozenDictionary<string, CommandContractDescriptor> ByMessageKind =
        Descriptors.ToFrozenDictionary(descriptor => descriptor.MessageKind, StringComparer.Ordinal);

    private static readonly FrozenDictionary<Type, CommandContractDescriptor> ByDtoType =
        Descriptors.ToFrozenDictionary(descriptor => descriptor.DtoType);

    public static IReadOnlyList<CommandContractDescriptor> All => Descriptors;

    public static bool TryGetByMessageKind(string messageKind, out CommandContractDescriptor? descriptor) =>
        ByMessageKind.TryGetValue(messageKind, out descriptor);

    public static bool TryGetByType(Type dtoType, out CommandContractDescriptor? descriptor) =>
        ByDtoType.TryGetValue(dtoType, out descriptor);

    internal static CommandContractDescriptor RequireForMessageKind(string messageKind, string parameterName) =>
        ByMessageKind.TryGetValue(messageKind, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(parameterName);
}

internal static class CommandSelfHashTypes
{
    internal static string ForKind(string commandKind) =>
        CommandContractRegistry.RequireForMessageKind(
            CommandKinds.Require(commandKind, nameof(commandKind)),
            nameof(commandKind)).SelfHashType;
}
