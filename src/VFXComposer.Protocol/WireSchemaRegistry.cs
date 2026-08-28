using System.Collections.Frozen;
using System.Collections.ObjectModel;
using VFXComposer.Protocol.Commands;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Handshake;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;
using VFXComposer.Protocol.Status;

namespace VFXComposer.Protocol;

public static class WireSchemaIds
{
    private const string Root = "https://schemas.vfxcomposer.dev/desktop/";

    public const string HandshakeRequestV1 = Root + "vfxcomposer-handshake-request-v1.schema.json";
    public const string HandshakeResponseV1 = Root + "vfxcomposer-handshake-response-v1.schema.json";
    public const string DiagnosticV1 = Root + "vfxcomposer-diagnostic-v1.schema.json";
    public const string MachineStatusV1 = Root + "vfxcomposer-machine-status-v1.schema.json";
    public const string VisualStatusV1 = Root + "vfxcomposer-visual-status-v1.schema.json";
    public const string UserVerdictStatusV1 = Root + "vfxcomposer-user-verdict-status-v1.schema.json";
    public const string L3StatusV1 = Root + "vfxcomposer-l3-status-v1.schema.json";
    public const string L4StatusV1 = Root + "vfxcomposer-l4-status-v1.schema.json";
    public const string StatusProvenanceV1 = Root + "vfxcomposer-status-provenance-v1.schema.json";
    public const string PeerHelloV1 = Root + "vfxcomposer-peer-hello-v1.schema.json";
    public const string PeerSessionAcceptedV1 = Root + "vfxcomposer-peer-session-accepted-v1.schema.json";
    public const string RegisteredProjectSelectionV1 = Root + "vfxcomposer-registered-project-selection-v1.schema.json";
    public const string ProjectRegistrationAttestationV1 = Root + "vfxcomposer-project-registration-attestation-v1.schema.json";
    public const string ProjectLeaseV1 = Root + "vfxcomposer-project-lease-v1.schema.json";
    public const string WorkerProjectLocatorV1 = Root + "vfxcomposer-worker-project-locator-v1.schema.json";
    public const string WorkerProjectLocatorAcknowledgementV1 = Root + "vfxcomposer-worker-project-locator-ack-v1.schema.json";
    public const string WorkerProjectHandleGrantV1 = Root + "vfxcomposer-worker-project-handle-grant-v1.schema.json";
    public const string WorkerProjectHandleGrantAcknowledgementV1 = Root + "vfxcomposer-worker-project-handle-grant-ack-v1.schema.json";
    public const string WorkerProjectHandleRevokeV1 = Root + "vfxcomposer-worker-project-handle-revoke-v1.schema.json";
    public const string WorkerProjectHandleRevokeAcknowledgementV1 = Root + "vfxcomposer-worker-project-handle-revoke-ack-v1.schema.json";
    public const string ReadDocumentQueryV1 = Root + "vfxcomposer-read-document-query-v1.schema.json";
    public const string ReadDocumentResultV1 = Root + "vfxcomposer-read-document-result-v1.schema.json";
    public const string CommandEnvelopeV1 = Root + "commands/vfxcomposer-command-envelope-v1.schema.json";
    public const string ValidateRecipeCommandV1 = Root + "commands/vfxcomposer-validate-recipe-command-v1.schema.json";
    public const string BuildCandidateCommandV1 = Root + "commands/vfxcomposer-build-candidate-command-v1.schema.json";
    public const string OpenPreviewJobCommandV1 = Root + "commands/vfxcomposer-open-preview-job-command-v1.schema.json";
    public const string ClosePreviewJobCommandV1 = Root + "commands/vfxcomposer-close-preview-job-command-v1.schema.json";
    public const string SetPreviewPlaybackCommandV1 = Root + "commands/vfxcomposer-set-preview-playback-command-v1.schema.json";
    public const string ValidatePatchCommandV1 = Root + "commands/vfxcomposer-validate-patch-command-v1.schema.json";
    public const string ApplyPatchCommandV1 = Root + "commands/vfxcomposer-apply-patch-command-v1.schema.json";
    public const string RunFocusedTestsCommandV1 = Root + "commands/vfxcomposer-run-focused-tests-command-v1.schema.json";
    public const string CancelJobCommandV1 = Root + "commands/vfxcomposer-cancel-job-command-v1.schema.json";
    public const string JobProgressV1 = Root + "jobs/vfxcomposer-job-progress-v1.schema.json";
    public const string JobLogEventV1 = Root + "jobs/vfxcomposer-job-log-event-v1.schema.json";
    public const string JobArtifactV1 = Root + "jobs/vfxcomposer-job-artifact-v1.schema.json";
    public const string JobCompletionV1 = Root + "jobs/vfxcomposer-job-completion-v1.schema.json";
}

public sealed record WireSchemaDescriptor
{
    public WireSchemaDescriptor(
        string schemaId,
        Type dtoType,
        IEnumerable<string> requiredTopLevelProperties,
        string? messageKind = null)
    {
        SchemaId = schemaId;
        DtoType = dtoType;
        RequiredTopLevelProperties = requiredTopLevelProperties.ToFrozenSet(StringComparer.Ordinal);
        MessageKind = messageKind;
    }

    public string SchemaId { get; }

    public Type DtoType { get; }

    public IReadOnlySet<string> RequiredTopLevelProperties { get; }

    public string? MessageKind { get; }
}

/// <summary>Compile-time schema ownership registry. It performs no filesystem lookup.</summary>
public static class WireSchemaRegistry
{
    private static readonly ReadOnlyCollection<WireSchemaDescriptor> Descriptors =
        Array.AsReadOnly(new[]
        {
            new WireSchemaDescriptor(
                WireSchemaIds.HandshakeRequestV1,
                typeof(HandshakeRequest),
                ["protocolVersion", "messageKind", "requestId", "clientInstanceId", "offeredCapabilities"],
                MessageKinds.HandshakeRequest),
            new WireSchemaDescriptor(
                WireSchemaIds.HandshakeResponseV1,
                typeof(HandshakeResponse),
                ["protocolVersion", "messageKind", "requestId", "serverInstanceId", "accepted", "negotiatedCapabilities", "diagnostic"],
                MessageKinds.HandshakeResponse),
            new WireSchemaDescriptor(
                WireSchemaIds.DiagnosticV1,
                typeof(StableDiagnostic),
                ["protocolVersion", "messageKind", "code", "severity", "message", "retryable"],
                MessageKinds.Diagnostic),
            new WireSchemaDescriptor(
                WireSchemaIds.MachineStatusV1,
                typeof(MachineStatus),
                ["protocolVersion", "state", "provenance"]),
            new WireSchemaDescriptor(
                WireSchemaIds.VisualStatusV1,
                typeof(VisualStatus),
                ["protocolVersion", "state", "provenance"]),
            new WireSchemaDescriptor(
                WireSchemaIds.UserVerdictStatusV1,
                typeof(UserVerdictStatus),
                ["protocolVersion", "state", "provenance"]),
            new WireSchemaDescriptor(
                WireSchemaIds.L3StatusV1,
                typeof(L3Status),
                ["protocolVersion", "state", "provenance"]),
            new WireSchemaDescriptor(
                WireSchemaIds.L4StatusV1,
                typeof(L4Status),
                ["protocolVersion", "state", "provenance"]),
            new WireSchemaDescriptor(
                WireSchemaIds.StatusProvenanceV1,
                typeof(StatusProvenance),
                ["protocolVersion", "statusDomain", "sourceKind", "sourceIdentity", "observedAtUtc"]),
            new WireSchemaDescriptor(
                WireSchemaIds.PeerHelloV1,
                typeof(PeerHello),
                ["protocolVersion", "messageKind", "requestId", "peerRole", "peerInstanceId", "processId", "processEpoch", "offeredCapabilities", "imageIdentity"],
                MessageKinds.PeerHello),
            new WireSchemaDescriptor(
                WireSchemaIds.PeerSessionAcceptedV1,
                typeof(PeerSessionAccepted),
                ["protocolVersion", "messageKind", "requestId", "sessionId", "peerRole", "brokerInstanceId", "brokerGeneration", "processEpoch", "negotiatedCapabilities"],
                MessageKinds.PeerSessionAccepted),
            new WireSchemaDescriptor(
                WireSchemaIds.RegisteredProjectSelectionV1,
                typeof(RegisteredProjectSelection),
                ["protocolVersion", "messageKind", "requestId", "registeredProjectId", "projectIdentity", "brokerGeneration", "registrationGeneration"],
                MessageKinds.RegisteredProjectSelection),
            new WireSchemaDescriptor(
                WireSchemaIds.ProjectRegistrationAttestationV1,
                typeof(ProjectRegistrationAttestation),
                ["protocolVersion", "messageKind", "requestId", "registeredProjectId", "projectIdentity", "volumeIdentity", "repositoryIdentity", "projectRootIdentity", "brokerGeneration", "registrationGeneration", "workerSessionId", "workerProcessEpoch", "selfHash"],
                MessageKinds.ProjectRegistrationAttestation),
            new WireSchemaDescriptor(
                WireSchemaIds.ProjectLeaseV1,
                typeof(ProjectLeaseDescriptor),
                ["protocolVersion", "messageKind", "requestId", "leaseId", "registeredProjectId", "projectIdentity", "brokerGeneration", "registrationGeneration", "workerSessionId", "workerProcessEpoch", "leaseGeneration", "selfHash"],
                MessageKinds.ProjectLeaseDescriptor),
            new WireSchemaDescriptor(
                WireSchemaIds.WorkerProjectLocatorV1,
                typeof(WorkerProjectLocator),
                ["protocolVersion", "messageKind", "requestId", "registeredProjectId", "projectIdentity", "volumeIdentity", "repositoryIdentity", "projectRootIdentity", "brokerGeneration", "registrationGeneration", "enrollmentGeneration", "workerSessionId", "workerProcessEpoch", "selfHash"],
                MessageKinds.WorkerProjectLocator),
            new WireSchemaDescriptor(
                WireSchemaIds.WorkerProjectLocatorAcknowledgementV1,
                typeof(WorkerProjectLocatorAcknowledgement),
                ["protocolVersion", "messageKind", "requestId", "registeredProjectId", "brokerGeneration", "registrationGeneration", "enrollmentGeneration", "workerSessionId", "workerProcessEpoch", "locatorSelfHash", "disposition", "selfHash"],
                MessageKinds.WorkerProjectLocatorAcknowledgement),
            new WireSchemaDescriptor(
                WireSchemaIds.WorkerProjectHandleGrantV1,
                typeof(WorkerProjectHandleGrant),
                ["protocolVersion", "messageKind", "requestId", "leaseId", "registeredProjectId", "projectIdentity", "volumeIdentity", "repositoryIdentity", "projectRootIdentity", "brokerGeneration", "registrationGeneration", "leaseGeneration", "workerSessionId", "workerProcessEpoch", "handleEncoding", "volumeHandle", "repositoryHandle", "projectRootHandle", "selfHash"],
                MessageKinds.WorkerProjectHandleGrant),
            new WireSchemaDescriptor(
                WireSchemaIds.WorkerProjectHandleGrantAcknowledgementV1,
                typeof(WorkerProjectHandleGrantAcknowledgement),
                ["protocolVersion", "messageKind", "requestId", "leaseId", "brokerGeneration", "leaseGeneration", "workerSessionId", "workerProcessEpoch", "grantSelfHash", "disposition", "selfHash"],
                MessageKinds.WorkerProjectHandleGrantAcknowledgement),
            new WireSchemaDescriptor(
                WireSchemaIds.WorkerProjectHandleRevokeV1,
                typeof(WorkerProjectHandleRevoke),
                ["protocolVersion", "messageKind", "requestId", "leaseId", "brokerGeneration", "leaseGeneration", "workerSessionId", "workerProcessEpoch", "grantSelfHash", "reasonCode", "selfHash"],
                MessageKinds.WorkerProjectHandleRevoke),
            new WireSchemaDescriptor(
                WireSchemaIds.WorkerProjectHandleRevokeAcknowledgementV1,
                typeof(WorkerProjectHandleRevokeAcknowledgement),
                ["protocolVersion", "messageKind", "requestId", "leaseId", "brokerGeneration", "leaseGeneration", "workerSessionId", "workerProcessEpoch", "grantSelfHash", "revokeSelfHash", "disposition", "selfHash"],
                MessageKinds.WorkerProjectHandleRevokeAcknowledgement),
            new WireSchemaDescriptor(
                WireSchemaIds.ReadDocumentQueryV1,
                typeof(ReadDocumentQuery),
                ["protocolVersion", "messageKind", "requestId", "leaseId", "projectIdentity", "leaseGeneration", "documentKind", "documentId", "expectedContentHash"],
                MessageKinds.ReadDocumentQuery),
            new WireSchemaDescriptor(
                WireSchemaIds.ReadDocumentResultV1,
                typeof(ReadDocumentResult),
                ["protocolVersion", "messageKind", "requestId", "accepted", "projectIdentity", "documentKind", "documentId", "contentHash", "byteLength", "contentBase64", "diagnostic"],
                MessageKinds.ReadDocumentResult),
            new WireSchemaDescriptor(
                WireSchemaIds.CommandEnvelopeV1,
                typeof(CommandEnvelope),
                ["protocolVersion", "requestId", "commandId", "idempotencyKey", "leaseId", "projectIdentity", "leaseGeneration", "commandKind", "commandCapability", "confirmationPolicy", "selfHash"]),
            new WireSchemaDescriptor(
                WireSchemaIds.ValidateRecipeCommandV1,
                typeof(ValidateRecipeCommand),
                ["protocolVersion", "messageKind", "envelope", "recipeId", "recipeContentHash", "recipeContractHash", "selfHash"],
                MessageKinds.ValidateRecipeCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.BuildCandidateCommandV1,
                typeof(BuildCandidateCommand),
                ["protocolVersion", "messageKind", "envelope", "recipeId", "recipeValidationHash", "buildDefinitionHash", "candidateIdentity", "selfHash"],
                MessageKinds.BuildCandidateCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.OpenPreviewJobCommandV1,
                typeof(OpenPreviewJobCommand),
                ["protocolVersion", "messageKind", "envelope", "candidateId", "candidateIdentity", "previewId", "previewIdentity", "selfHash"],
                MessageKinds.OpenPreviewJobCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.ClosePreviewJobCommandV1,
                typeof(ClosePreviewJobCommand),
                ["protocolVersion", "messageKind", "envelope", "previewIdentity", "targetPreviewJob", "selfHash"],
                MessageKinds.ClosePreviewJobCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.SetPreviewPlaybackCommandV1,
                typeof(SetPreviewPlaybackCommand),
                ["protocolVersion", "messageKind", "envelope", "previewIdentity", "targetPreviewJob", "playbackDirective", "selfHash"],
                MessageKinds.SetPreviewPlaybackCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.ValidatePatchCommandV1,
                typeof(ValidatePatchCommand),
                ["protocolVersion", "messageKind", "envelope", "patchId", "patchContentHash", "targetCandidateId", "targetCandidateIdentity", "selfHash"],
                MessageKinds.ValidatePatchCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.ApplyPatchCommandV1,
                typeof(ApplyPatchCommand),
                ["protocolVersion", "messageKind", "envelope", "patchId", "patchValidationHash", "targetCandidateId", "targetCandidateIdentity", "selfHash"],
                MessageKinds.ApplyPatchCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.RunFocusedTestsCommandV1,
                typeof(RunFocusedTestsCommand),
                ["protocolVersion", "messageKind", "envelope", "targetCandidateId", "targetCandidateIdentity", "testIds", "focusedTestPlanHash", "selfHash"],
                MessageKinds.RunFocusedTestsCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.CancelJobCommandV1,
                typeof(CancelJobCommand),
                ["protocolVersion", "messageKind", "envelope", "targetJob", "selfHash"],
                MessageKinds.CancelJobCommand),
            new WireSchemaDescriptor(
                WireSchemaIds.JobProgressV1,
                typeof(JobProgress),
                ["protocolVersion", "messageKind", "projectIdentity", "leaseId", "leaseGeneration", "job", "eventSequence", "state", "progressPermille", "selfHash"],
                MessageKinds.JobProgress),
            new WireSchemaDescriptor(
                WireSchemaIds.JobLogEventV1,
                typeof(JobLogEvent),
                ["protocolVersion", "messageKind", "projectIdentity", "leaseId", "leaseGeneration", "job", "eventSequence", "level", "diagnostic", "selfHash"],
                MessageKinds.JobLogEvent),
            new WireSchemaDescriptor(
                WireSchemaIds.JobArtifactV1,
                typeof(JobArtifact),
                ["protocolVersion", "messageKind", "projectIdentity", "leaseId", "leaseGeneration", "job", "eventSequence", "artifactKind", "artifactId", "artifactHash", "byteLength", "selfHash"],
                MessageKinds.JobArtifact),
            new WireSchemaDescriptor(
                WireSchemaIds.JobCompletionV1,
                typeof(JobCompletion),
                ["protocolVersion", "messageKind", "projectIdentity", "leaseId", "leaseGeneration", "job", "eventSequence", "outcome", "finalArtifactCount", "diagnostic", "completedAtUtc", "selfHash"],
                MessageKinds.JobCompletion),
        });

    private static readonly FrozenDictionary<Type, WireSchemaDescriptor> ByDtoType =
        Descriptors.ToFrozenDictionary(descriptor => descriptor.DtoType);

    public static IReadOnlyList<WireSchemaDescriptor> All => Descriptors;

    public static bool TryGetById(string schemaId, out WireSchemaDescriptor? descriptor)
    {
        descriptor = Descriptors.SingleOrDefault(
            value => string.Equals(value.SchemaId, schemaId, StringComparison.Ordinal));
        return descriptor is not null;
    }

    public static bool TryGetByType(Type dtoType, out WireSchemaDescriptor? descriptor) =>
        ByDtoType.TryGetValue(dtoType, out descriptor);
}
