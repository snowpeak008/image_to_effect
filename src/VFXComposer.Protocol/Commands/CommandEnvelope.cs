using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Commands;

/// <summary>
/// Opaque reference to a frozen confirmation policy. It is not a confirmation result
/// or any authority-bearing state.
/// </summary>
public sealed record ConfirmationPolicyReference
{
    public const string PolicyIdentityType = "vfxcomposer.confirmation-policy/1";

    [JsonConstructor]
    public ConfirmationPolicyReference(string policyId, TypedHash policyIdentity)
    {
        PolicyId = ConfirmationPolicyIds.Require(policyId, nameof(policyId));
        PolicyIdentity = WireModelGuard.TypedHash(policyIdentity, PolicyIdentityType, nameof(policyIdentity));
    }

    [JsonPropertyName("policyId")] public string PolicyId { get; }
    [JsonPropertyName("policyIdentity")] public TypedHash PolicyIdentity { get; }
}

/// <summary>
/// Shared immutable command correlation and replay envelope. It is a wire-data shape
/// only and cannot authenticate, admit, execute, or authorize a command.
/// </summary>
public sealed record CommandEnvelope
{
    public const string SelfHashType = "vfxcomposer.command-envelope/1";

    [JsonConstructor]
    public CommandEnvelope(
        string protocolVersion,
        string requestId,
        string commandId,
        string idempotencyKey,
        string leaseId,
        TypedHash projectIdentity,
        long leaseGeneration,
        string commandKind,
        string commandCapability,
        ConfirmationPolicyReference confirmationPolicy,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (leaseGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseGeneration));
        }

        ProtocolVersion = protocolVersion;
        RequestId = Guard.Token(requestId, nameof(requestId));
        CommandId = Guard.Token(commandId, nameof(commandId));
        IdempotencyKey = Guard.Token(idempotencyKey, nameof(idempotencyKey));
        RequireDistinctIdentityTokens(RequestId, CommandId, IdempotencyKey);
        LeaseId = Guard.Token(leaseId, nameof(leaseId));
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectRegistrationAttestation.ProjectIdentityType,
            nameof(projectIdentity));
        LeaseGeneration = leaseGeneration;
        CommandKind = CommandKinds.Require(commandKind, nameof(commandKind));
        CommandCapability = CommandCapabilityIds.RequireForCommand(
            CommandKind,
            commandCapability,
            nameof(commandCapability));
        ConfirmationPolicy = confirmationPolicy ?? throw new ArgumentNullException(nameof(confirmationPolicy));
        SelfHash = WireModelGuard.TypedHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("commandId")] public string CommandId { get; }
    [JsonPropertyName("idempotencyKey")] public string IdempotencyKey { get; }
    [JsonPropertyName("leaseId")] public string LeaseId { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("leaseGeneration")] public long LeaseGeneration { get; }
    [JsonPropertyName("commandKind")] public string CommandKind { get; }
    [JsonPropertyName("commandCapability")] public string CommandCapability { get; }
    [JsonPropertyName("confirmationPolicy")] public ConfirmationPolicyReference ConfirmationPolicy { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }

    private static void RequireDistinctIdentityTokens(string requestId, string commandId, string idempotencyKey)
    {
        if (string.Equals(requestId, commandId, StringComparison.Ordinal) ||
            string.Equals(requestId, idempotencyKey, StringComparison.Ordinal) ||
            string.Equals(commandId, idempotencyKey, StringComparison.Ordinal))
        {
            throw new ArgumentException("Request, command, and idempotency identities must be distinct.");
        }
    }
}

internal static class CommandWireGuard
{
    internal static void RequireHeader(
        string protocolVersion,
        string messageKind,
        string expectedMessageKind,
        CommandEnvelope envelope,
        string expectedCommandKind)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, expectedMessageKind, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        var descriptor = CommandContractRegistry.RequireForMessageKind(expectedMessageKind, nameof(expectedMessageKind));
        ArgumentNullException.ThrowIfNull(envelope, nameof(envelope));
        if (!string.Equals(envelope.ProtocolVersion, protocolVersion, StringComparison.Ordinal) ||
            !string.Equals(envelope.CommandKind, expectedCommandKind, StringComparison.Ordinal) ||
            !string.Equals(envelope.CommandKind, descriptor.CommandKind, StringComparison.Ordinal) ||
            !string.Equals(envelope.CommandCapability, descriptor.RequiredCapability, StringComparison.Ordinal))
        {
            throw new ArgumentException("Command envelope does not match the command wire type.", nameof(envelope));
        }
    }

    internal static TypedHash RequireSelfHash(TypedHash value, string expectedTypeTag, string parameterName) =>
        WireModelGuard.TypedHash(value, expectedTypeTag, parameterName);

    internal static JobCorrelation RequireTargetJob(
        JobCorrelation value,
        CommandEnvelope currentEnvelope,
        string? expectedOriginCommandKind,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        ArgumentNullException.ThrowIfNull(currentEnvelope, nameof(currentEnvelope));
        if (expectedOriginCommandKind is not null &&
            !string.Equals(value.OriginCommandKind, expectedOriginCommandKind, StringComparison.Ordinal))
        {
            throw new ArgumentException("Target job belongs to the wrong command kind.", parameterName);
        }

        var current = new[] { currentEnvelope.RequestId, currentEnvelope.CommandId, currentEnvelope.IdempotencyKey };
        var target = new[] { value.JobId, value.OriginRequestId, value.OriginCommandId, value.OriginIdempotencyKey };
        if (target.Any(targetValue => current.Contains(targetValue, StringComparer.Ordinal)))
        {
            throw new ArgumentException("A command cannot target its own correlation identity.", parameterName);
        }

        return value;
    }

    internal static IReadOnlyList<string> RequireSortedTokens(
        IEnumerable<string> values,
        string parameterName,
        int minimumCount,
        int maximumCount,
        int maximumTokenLength = 96)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = values.Select(value => Guard.Token(value, parameterName, maximumTokenLength)).ToArray();
        if (result.Length < minimumCount ||
            result.Length > maximumCount ||
            result.Distinct(StringComparer.Ordinal).Count() != result.Length ||
            !result.SequenceEqual(result.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException("The token list must be bounded, unique, and ordinal-sorted.", parameterName);
        }

        return Array.AsReadOnly(result);
    }
}
