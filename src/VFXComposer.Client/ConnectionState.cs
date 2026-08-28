using VFXComposer.Protocol;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Projects;

namespace VFXComposer.Client;

/// <summary>
/// A read-only snapshot of the client-side connection state.
/// </summary>
public sealed record ConnectionState(
    ProjectConnectionState ProjectState,
    StableDiagnostic Diagnostic)
{
    public string ProtocolVersion { get; } = ProtocolVersions.Current;

    public bool IsConnected =>
        ProjectState is ProjectConnectionState.ConnectedNoRegisteredProject
            or ProjectConnectionState.ConnectedRegisteredProject;

    public bool HasRegisteredProject =>
        ProjectState is ProjectConnectionState.ConnectedRegisteredProject;

    public string ConnectionDisplay => IsConnected ? "Connected" : "Disconnected";

    public string ProjectDisplay => HasRegisteredProject
        ? "Registered project"
        : "No registered project";

    public static ConnectionState CreateDisconnected() => new(
        ProjectConnectionState.Disconnected,
        new StableDiagnostic(
            StableDiagnosticCodes.Disconnected,
            DiagnosticSeverities.Info,
            "No broker or Unity worker connection is active.",
            retryable: true));
}
