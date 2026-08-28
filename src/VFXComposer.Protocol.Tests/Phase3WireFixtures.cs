using VFXComposer.Protocol.Commands;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Tests;

internal static class Phase3WireFixtures
{
    private static readonly TypedHash ProjectIdentity = Hash(
        ProjectRegistrationAttestation.ProjectIdentityType,
        "phase3-project");

    public static IReadOnlyDictionary<Type, object> RepresentativeDtos { get; } = CreateRepresentativeDtos();

    public static TypedHash Hash(string typeTag, string payload) => TypedHash.ComputeUtf8(typeTag, payload);

    public static CommandEnvelope Envelope(string commandKind, string suffix) =>
        new(
            ProtocolVersions.Current,
            "request-" + suffix,
            "command-" + suffix,
            "idem-" + suffix,
            "lease-01",
            ProjectIdentity,
            1,
            commandKind,
            CommandCapabilityIds.ForCommand(commandKind),
            new ConfirmationPolicyReference(
                ConfirmationPolicyIds.ReferenceV1,
                Hash(ConfirmationPolicyReference.PolicyIdentityType, "policy-" + suffix)),
            Hash(CommandEnvelope.SelfHashType, "envelope-" + suffix));

    public static JobCorrelation Correlation(string originCommandKind = CommandKinds.ValidateRecipe) =>
        new(
            "job-origin",
            "request-origin",
            "command-origin",
            "idem-origin",
            originCommandKind,
            Hash(SelfHashTypeFor(originCommandKind), "origin-command"));

    private static IReadOnlyDictionary<Type, object> CreateRepresentativeDtos()
    {
        var validationEnvelope = Envelope(CommandKinds.ValidateRecipe, "validate");
        var buildEnvelope = Envelope(CommandKinds.BuildCandidate, "build");
        var openEnvelope = Envelope(CommandKinds.OpenPreviewJob, "open");
        var closeEnvelope = Envelope(CommandKinds.ClosePreviewJob, "close");
        var playbackEnvelope = Envelope(CommandKinds.SetPreviewPlayback, "playback");
        var validatePatchEnvelope = Envelope(CommandKinds.ValidatePatch, "validate-patch");
        var applyPatchEnvelope = Envelope(CommandKinds.ApplyPatch, "apply-patch");
        var testsEnvelope = Envelope(CommandKinds.RunFocusedTests, "tests");
        var cancelEnvelope = Envelope(CommandKinds.CancelJob, "cancel");
        var previewJob = Correlation(CommandKinds.OpenPreviewJob);
        var genericJob = Correlation();

        return new Dictionary<Type, object>
        {
            [typeof(CommandEnvelope)] = validationEnvelope,
            [typeof(ValidateRecipeCommand)] = new ValidateRecipeCommand(
                ProtocolVersions.Current,
                MessageKinds.ValidateRecipeCommand,
                validationEnvelope,
                "recipe-01",
                Hash("vfxcomposer.recipe-content/1", "recipe"),
                Hash("vfxcomposer.recipe-contract/1", "contract"),
                Hash(ValidateRecipeCommand.SelfHashType, "validate")),
            [typeof(BuildCandidateCommand)] = new BuildCandidateCommand(
                ProtocolVersions.Current,
                MessageKinds.BuildCandidateCommand,
                buildEnvelope,
                "recipe-01",
                Hash("vfxcomposer.recipe-validation/1", "validated"),
                Hash("vfxcomposer.build-definition/1", "build"),
                Hash("vfxcomposer.candidate-identity/1", "candidate"),
                Hash(BuildCandidateCommand.SelfHashType, "build")),
            [typeof(OpenPreviewJobCommand)] = new OpenPreviewJobCommand(
                ProtocolVersions.Current,
                MessageKinds.OpenPreviewJobCommand,
                openEnvelope,
                "candidate-01",
                Hash("vfxcomposer.candidate-identity/1", "candidate"),
                "preview-01",
                Hash("vfxcomposer.preview-identity/1", "preview"),
                Hash(OpenPreviewJobCommand.SelfHashType, "open")),
            [typeof(ClosePreviewJobCommand)] = new ClosePreviewJobCommand(
                ProtocolVersions.Current,
                MessageKinds.ClosePreviewJobCommand,
                closeEnvelope,
                Hash("vfxcomposer.preview-identity/1", "preview"),
                previewJob,
                Hash(ClosePreviewJobCommand.SelfHashType, "close")),
            [typeof(SetPreviewPlaybackCommand)] = new SetPreviewPlaybackCommand(
                ProtocolVersions.Current,
                MessageKinds.SetPreviewPlaybackCommand,
                playbackEnvelope,
                Hash("vfxcomposer.preview-identity/1", "preview"),
                previewJob,
                PreviewPlaybackDirectives.Play,
                Hash(SetPreviewPlaybackCommand.SelfHashType, "playback")),
            [typeof(ValidatePatchCommand)] = new ValidatePatchCommand(
                ProtocolVersions.Current,
                MessageKinds.ValidatePatchCommand,
                validatePatchEnvelope,
                "patch-01",
                Hash("vfxcomposer.patch-content/1", "patch"),
                "candidate-01",
                Hash("vfxcomposer.candidate-identity/1", "candidate"),
                Hash(ValidatePatchCommand.SelfHashType, "validate-patch")),
            [typeof(ApplyPatchCommand)] = new ApplyPatchCommand(
                ProtocolVersions.Current,
                MessageKinds.ApplyPatchCommand,
                applyPatchEnvelope,
                "patch-01",
                Hash("vfxcomposer.patch-validation/1", "patch-validation"),
                "candidate-01",
                Hash("vfxcomposer.candidate-identity/1", "candidate"),
                Hash(ApplyPatchCommand.SelfHashType, "apply-patch")),
            [typeof(RunFocusedTestsCommand)] = new RunFocusedTestsCommand(
                ProtocolVersions.Current,
                MessageKinds.RunFocusedTestsCommand,
                testsEnvelope,
                "candidate-01",
                Hash("vfxcomposer.candidate-identity/1", "candidate"),
                ["test-alpha", "test-beta"],
                Hash("vfxcomposer.focused-test-plan/1", "focused-plan"),
                Hash(RunFocusedTestsCommand.SelfHashType, "tests")),
            [typeof(CancelJobCommand)] = new CancelJobCommand(
                ProtocolVersions.Current,
                MessageKinds.CancelJobCommand,
                cancelEnvelope,
                genericJob,
                Hash(CancelJobCommand.SelfHashType, "cancel")),
            [typeof(JobProgress)] = new JobProgress(
                ProtocolVersions.Current,
                MessageKinds.JobProgress,
                ProjectIdentity,
                "lease-01",
                1,
                genericJob,
                1,
                JobProgressStates.Queued,
                0,
                Hash(JobProgress.SelfHashType, "progress")),
            [typeof(JobLogEvent)] = new JobLogEvent(
                ProtocolVersions.Current,
                MessageKinds.JobLogEvent,
                ProjectIdentity,
                "lease-01",
                1,
                genericJob,
                2,
                JobLogLevels.Info,
                StableDiagnosticCatalog.Create(StableDiagnosticCodes.Disconnected),
                Hash(JobLogEvent.SelfHashType, "log")),
            [typeof(JobArtifact)] = new JobArtifact(
                ProtocolVersions.Current,
                MessageKinds.JobArtifact,
                ProjectIdentity,
                "lease-01",
                1,
                genericJob,
                3,
                JobArtifactKinds.CandidateIdentity,
                "artifact-01",
                Hash("vfxcomposer.candidate-identity/1", "candidate"),
                42,
                Hash(JobArtifact.SelfHashType, "artifact")),
            [typeof(JobCompletion)] = new JobCompletion(
                ProtocolVersions.Current,
                MessageKinds.JobCompletion,
                ProjectIdentity,
                "lease-01",
                1,
                genericJob,
                4,
                JobCompletionOutcomes.Succeeded,
                1,
                null,
                DateTimeOffset.UnixEpoch,
                Hash(JobCompletion.SelfHashType, "completion")),
        };
    }

    private static string SelfHashTypeFor(string commandKind) => commandKind switch
    {
        CommandKinds.ValidateRecipe => ValidateRecipeCommand.SelfHashType,
        CommandKinds.BuildCandidate => BuildCandidateCommand.SelfHashType,
        CommandKinds.OpenPreviewJob => OpenPreviewJobCommand.SelfHashType,
        CommandKinds.ClosePreviewJob => ClosePreviewJobCommand.SelfHashType,
        CommandKinds.SetPreviewPlayback => SetPreviewPlaybackCommand.SelfHashType,
        CommandKinds.ValidatePatch => ValidatePatchCommand.SelfHashType,
        CommandKinds.ApplyPatch => ApplyPatchCommand.SelfHashType,
        CommandKinds.RunFocusedTests => RunFocusedTestsCommand.SelfHashType,
        CommandKinds.CancelJob => CancelJobCommand.SelfHashType,
        _ => throw new ArgumentOutOfRangeException(nameof(commandKind)),
    };
}
