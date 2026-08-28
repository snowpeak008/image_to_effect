using System.Collections.Frozen;
using VFXComposer.Protocol.Commands;

namespace VFXComposer.Protocol.Jobs;

/// <summary>Vocabulary only; no ordering or runtime transition is implied.</summary>
public static class JobProgressStates
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string CancellationRequested = "CANCELLATION_REQUESTED";

    private static readonly FrozenSet<string> KnownStates =
        new[] { Queued, Running, CancellationRequested }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownStates;

    internal static string Require(string value, string parameterName) =>
        KnownStates.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

public static class JobLogLevels
{
    public const string Info = "INFO";
    public const string Warning = "WARNING";
    public const string Error = "ERROR";

    private static readonly FrozenSet<string> KnownLevels =
        new[] { Info, Warning, Error }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownLevels;

    internal static string Require(string value, string parameterName) =>
        KnownLevels.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>Closed artifact identity vocabulary. Artifact metadata never carries a location.</summary>
public static class JobArtifactKinds
{
    public const string CandidateIdentity = "CANDIDATE_IDENTITY";
    public const string PreviewIdentity = "PREVIEW_IDENTITY";
    public const string PatchValidation = "PATCH_VALIDATION";
    public const string FocusedTestReport = "FOCUSED_TEST_REPORT";

    private static readonly FrozenDictionary<string, string> HashTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CandidateIdentity] = CommandContentHashTypes.CandidateIdentity,
            [PreviewIdentity] = CommandContentHashTypes.PreviewIdentity,
            [PatchValidation] = CommandContentHashTypes.PatchValidation,
            [FocusedTestReport] = CommandContentHashTypes.FocusedTestReport,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => HashTypes.Keys.ToFrozenSet(StringComparer.Ordinal);

    internal static string RequireHashType(string artifactKind, string parameterName) =>
        HashTypes.TryGetValue(artifactKind, out var hashType)
            ? hashType
            : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>Completion vocabulary only. It has no relationship to authority domains.</summary>
public static class JobCompletionOutcomes
{
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
    public const string Disconnected = "DISCONNECTED";

    private static readonly FrozenSet<string> KnownOutcomes =
        new[] { Succeeded, Failed, Cancelled, Disconnected }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownOutcomes;

    internal static string Require(string value, string parameterName) =>
        KnownOutcomes.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}
