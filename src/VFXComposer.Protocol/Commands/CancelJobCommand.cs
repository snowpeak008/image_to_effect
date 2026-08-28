using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Protocol.Commands;

/// <summary>Correlates cancellation intent only; it does not define cancellation behavior.</summary>
public sealed record CancelJobCommand
{
    public const string SelfHashType = "vfxcomposer.command.cancel-job/1";

    [JsonConstructor]
    public CancelJobCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        JobCorrelation targetJob,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.CancelJobCommand, envelope, CommandKinds.CancelJob);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        TargetJob = CommandWireGuard.RequireTargetJob(targetJob, envelope, expectedOriginCommandKind: null, nameof(targetJob));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("targetJob")] public JobCorrelation TargetJob { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}
