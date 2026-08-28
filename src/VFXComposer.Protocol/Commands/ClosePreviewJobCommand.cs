using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Protocol.Commands;

/// <summary>Correlates a preview-close intent without defining its runtime effect.</summary>
public sealed record ClosePreviewJobCommand
{
    public const string SelfHashType = "vfxcomposer.command.close-preview-job/1";

    [JsonConstructor]
    public ClosePreviewJobCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        TypedHash previewIdentity,
        JobCorrelation targetPreviewJob,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.ClosePreviewJobCommand, envelope, CommandKinds.ClosePreviewJob);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        PreviewIdentity = WireModelGuard.TypedHash(previewIdentity, CommandContentHashTypes.PreviewIdentity, nameof(previewIdentity));
        TargetPreviewJob = CommandWireGuard.RequireTargetJob(
            targetPreviewJob,
            envelope,
            CommandKinds.OpenPreviewJob,
            nameof(targetPreviewJob));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("previewIdentity")] public TypedHash PreviewIdentity { get; }
    [JsonPropertyName("targetPreviewJob")] public JobCorrelation TargetPreviewJob { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}
