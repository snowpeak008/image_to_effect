using System.Collections.Frozen;
using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Protocol.Commands;

/// <summary>Closed playback-intent vocabulary; it defines no execution semantics.</summary>
public static class PreviewPlaybackDirectives
{
    public const string Play = "PLAY";
    public const string Pause = "PAUSE";
    public const string Stop = "STOP";

    private static readonly FrozenSet<string> KnownDirectives =
        new[] { Play, Pause, Stop }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownDirectives;

    internal static string Require(string value, string parameterName) =>
        KnownDirectives.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>Correlates a vocabulary-only playback intent to a preview job.</summary>
public sealed record SetPreviewPlaybackCommand
{
    public const string SelfHashType = "vfxcomposer.command.set-preview-playback/1";

    [JsonConstructor]
    public SetPreviewPlaybackCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        TypedHash previewIdentity,
        JobCorrelation targetPreviewJob,
        string playbackDirective,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.SetPreviewPlaybackCommand, envelope, CommandKinds.SetPreviewPlayback);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        PreviewIdentity = WireModelGuard.TypedHash(previewIdentity, CommandContentHashTypes.PreviewIdentity, nameof(previewIdentity));
        TargetPreviewJob = CommandWireGuard.RequireTargetJob(
            targetPreviewJob,
            envelope,
            CommandKinds.OpenPreviewJob,
            nameof(targetPreviewJob));
        PlaybackDirective = PreviewPlaybackDirectives.Require(playbackDirective, nameof(playbackDirective));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("previewIdentity")] public TypedHash PreviewIdentity { get; }
    [JsonPropertyName("targetPreviewJob")] public JobCorrelation TargetPreviewJob { get; }
    [JsonPropertyName("playbackDirective")] public string PlaybackDirective { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}
