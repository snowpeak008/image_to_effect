namespace VFXComposer.Protocol;

/// <summary>Exact wire message-kind tokens admitted by the frozen protocol registry.</summary>
public static class MessageKinds
{
    public const string HandshakeRequest = "handshake.request";
    public const string HandshakeResponse = "handshake.response";
    public const string Diagnostic = "diagnostic";
    public const string PeerHello = "peer.hello";
    public const string PeerSessionAccepted = "peer.session.accepted";
    public const string RegisteredProjectSelection = "project.registered.selection";
    public const string ProjectRegistrationAttestation = "project.registration.attestation";
    public const string ProjectLeaseDescriptor = "project.lease.descriptor";
    public const string WorkerProjectLocator = "worker.project.locator";
    public const string WorkerProjectLocatorAcknowledgement = "worker.project.locator.ack";
    public const string WorkerProjectHandleGrant = "worker.project.handle.grant";
    public const string WorkerProjectHandleGrantAcknowledgement = "worker.project.handle.grant.ack";
    public const string WorkerProjectHandleRevoke = "worker.project.handle.revoke";
    public const string WorkerProjectHandleRevokeAcknowledgement = "worker.project.handle.revoke.ack";
    public const string ReadDocumentQuery = "project.document.read.query";
    public const string ReadDocumentResult = "project.document.read.result";
    public const string ValidateRecipeCommand = "command.validate-recipe";
    public const string BuildCandidateCommand = "command.build-candidate";
    public const string OpenPreviewJobCommand = "command.open-preview-job";
    public const string ClosePreviewJobCommand = "command.close-preview-job";
    public const string SetPreviewPlaybackCommand = "command.set-preview-playback";
    public const string ValidatePatchCommand = "command.validate-patch";
    public const string ApplyPatchCommand = "command.apply-patch";
    public const string RunFocusedTestsCommand = "command.run-focused-tests";
    public const string CancelJobCommand = "command.cancel-job";
    public const string JobProgress = "job.progress";
    public const string JobLogEvent = "job.log-event";
    public const string JobArtifact = "job.artifact";
    public const string JobCompletion = "job.completion";
}
