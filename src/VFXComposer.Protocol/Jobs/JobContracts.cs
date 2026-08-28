using System.Collections.Frozen;
using System.Collections.ObjectModel;

namespace VFXComposer.Protocol.Jobs;

/// <summary>Closed job-event message vocabulary. It does not describe a state machine.</summary>
public static class JobMessageKinds
{
    public const string Progress = MessageKinds.JobProgress;
    public const string LogEvent = MessageKinds.JobLogEvent;
    public const string Artifact = MessageKinds.JobArtifact;
    public const string Completion = MessageKinds.JobCompletion;

    private static readonly FrozenSet<string> KnownKinds =
        new[] { Progress, LogEvent, Artifact, Completion }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownKinds;

    public static bool IsKnown(string? value) => value is not null && KnownKinds.Contains(value);

    internal static string Require(string value, string parameterName) =>
        IsKnown(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

public sealed record JobContractDescriptor(string MessageKind, Type DtoType, string SelfHashType);

/// <summary>Closed registry for job-event data. It does not manage or transition jobs.</summary>
public static class JobContractRegistry
{
    private static readonly ReadOnlyCollection<JobContractDescriptor> Descriptors =
        Array.AsReadOnly(
        [
            new JobContractDescriptor(JobMessageKinds.Progress, typeof(JobProgress), JobProgress.SelfHashType),
            new JobContractDescriptor(JobMessageKinds.LogEvent, typeof(JobLogEvent), JobLogEvent.SelfHashType),
            new JobContractDescriptor(JobMessageKinds.Artifact, typeof(JobArtifact), JobArtifact.SelfHashType),
            new JobContractDescriptor(JobMessageKinds.Completion, typeof(JobCompletion), JobCompletion.SelfHashType),
        ]);

    private static readonly FrozenDictionary<string, JobContractDescriptor> ByMessageKind =
        Descriptors.ToFrozenDictionary(descriptor => descriptor.MessageKind, StringComparer.Ordinal);

    private static readonly FrozenDictionary<Type, JobContractDescriptor> ByDtoType =
        Descriptors.ToFrozenDictionary(descriptor => descriptor.DtoType);

    public static IReadOnlyList<JobContractDescriptor> All => Descriptors;

    public static bool TryGetByMessageKind(string messageKind, out JobContractDescriptor? descriptor) =>
        ByMessageKind.TryGetValue(messageKind, out descriptor);

    public static bool TryGetByType(Type dtoType, out JobContractDescriptor? descriptor) =>
        ByDtoType.TryGetValue(dtoType, out descriptor);

    internal static JobContractDescriptor RequireForMessageKind(string messageKind, string parameterName) =>
        ByMessageKind.TryGetValue(messageKind, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(parameterName);
}
