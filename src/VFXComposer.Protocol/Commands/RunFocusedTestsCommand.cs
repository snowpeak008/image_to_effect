using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Commands;

/// <summary>Focused-test identity data with a bounded deterministic test identifier set.</summary>
public sealed record RunFocusedTestsCommand
{
    public const string SelfHashType = "vfxcomposer.command.run-focused-tests/1";
    public const int MaximumTestIds = 32;

    [JsonConstructor]
    public RunFocusedTestsCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        string targetCandidateId,
        TypedHash targetCandidateIdentity,
        IReadOnlyList<string> testIds,
        TypedHash focusedTestPlanHash,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.RunFocusedTestsCommand, envelope, CommandKinds.RunFocusedTests);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        TargetCandidateId = Guard.Token(targetCandidateId, nameof(targetCandidateId), 96);
        TargetCandidateIdentity = WireModelGuard.TypedHash(targetCandidateIdentity, CommandContentHashTypes.CandidateIdentity, nameof(targetCandidateIdentity));
        TestIds = CommandWireGuard.RequireSortedTokens(testIds, nameof(testIds), minimumCount: 1, maximumCount: MaximumTestIds);
        FocusedTestPlanHash = WireModelGuard.TypedHash(focusedTestPlanHash, CommandContentHashTypes.FocusedTestPlan, nameof(focusedTestPlanHash));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("targetCandidateId")] public string TargetCandidateId { get; }
    [JsonPropertyName("targetCandidateIdentity")] public TypedHash TargetCandidateIdentity { get; }
    [JsonPropertyName("testIds")] public IReadOnlyList<string> TestIds { get; }
    [JsonPropertyName("focusedTestPlanHash")] public TypedHash FocusedTestPlanHash { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}
