using VFXComposer.Protocol.Queries;

namespace VFXComposer.Client;

public enum UserModeDesktopSessionState
{
    Disconnected,
    Starting,
    ConnectedNoProject,
    Selecting,
    Selected,
    Reading,
    RecoveryRequired,
    Restarting,
}

public sealed record UserModeDesktopReadPresentation(
    bool Accepted,
    string DocumentKind,
    string DocumentId,
    int ByteLength,
    string? ContentBase64,
    string? DiagnosticCode);

public interface IUserModeDesktopSession : IAsyncDisposable
{
    UserModeDesktopSessionState State { get; }
    long Generation { get; }
    UserModeDesktopReadPresentation? LastRead { get; }
    event EventHandler? StateChanged;

    ValueTask ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask SelectAsync(string selection, CancellationToken cancellationToken = default);
    ValueTask<UserModeDesktopReadPresentation> ReadAsync(
        string documentKind = DocumentKinds.LibraryIndex,
        string documentId = "project",
        CancellationToken cancellationToken = default);
    ValueTask RestartAsync(CancellationToken cancellationToken = default);
}
