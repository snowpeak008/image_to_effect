using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using VFXComposer.Protocol.Commands;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Handshake;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Queries;
using VFXComposer.Protocol.Registration;
using VFXComposer.Protocol.Status;

namespace VFXComposer.Protocol.Json;

/// <summary>
/// The only admitted wire ingress: strict UTF-8 parse, exact root shape, frozen
/// version/kind checks, unmapped-member rejection, then DTO constructor validation.
/// </summary>
public static class StrictWireCodec
{
    private static readonly FrozenSet<string> DiagnosticProperties = new[]
    {
        "protocolVersion",
        "messageKind",
        "code",
        "severity",
        "message",
        "retryable",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ProvenanceProperties = new[]
    {
        "protocolVersion",
        "statusDomain",
        "sourceKind",
        "sourceIdentity",
        "observedAtUtc",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TypedHashProperties = new[]
    {
        "typeTag",
        "digest",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ConfirmationPolicyProperties = new[]
    {
        "policyId",
        "policyIdentity",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> CommandEnvelopeProperties = new[]
    {
        "protocolVersion",
        "requestId",
        "commandId",
        "idempotencyKey",
        "leaseId",
        "projectIdentity",
        "leaseGeneration",
        "commandKind",
        "commandCapability",
        "confirmationPolicy",
        "selfHash",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> JobCorrelationProperties = new[]
    {
        "jobId",
        "originRequestId",
        "originCommandId",
        "originIdempotencyKey",
        "originCommandKind",
        "originCommandSelfHash",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = StrictJsonLimits.DefaultMaximumDepth,
        NumberHandling = JsonNumberHandling.Strict,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static T Decode<T>(
        ReadOnlySpan<byte> utf8Json,
        StrictJsonLimits? limits = null)
        where T : class
    {
        if (!WireSchemaRegistry.TryGetByType(typeof(T), out var descriptor) || descriptor is null)
        {
            throw new InvalidOperationException("Requested type is not a registered wire DTO.");
        }

        try
        {
            using var document = StrictJsonReader.Parse(utf8Json, limits);
            ExactObjectValidator.Validate(
                document.RootElement,
                descriptor.RequiredTopLevelProperties);

            var version = ExactObjectValidator.RequireString(document.RootElement, "protocolVersion");
            if (!string.Equals(version, ProtocolVersions.Current, StringComparison.Ordinal))
            {
                throw new WireDecodeException(StableDiagnosticCodes.UnsupportedProtocolVersion);
            }

            if (descriptor.MessageKind is not null)
            {
                var messageKind = ExactObjectValidator.RequireString(document.RootElement, "messageKind");
                if (!string.Equals(messageKind, descriptor.MessageKind, StringComparison.Ordinal))
                {
                    throw new WireDecodeException(StableDiagnosticCodes.UnsupportedMessageKind);
                }
            }

            ValidateNestedRequiredProperties(typeof(T), document.RootElement);

            return document.RootElement.Deserialize<T>(SerializerOptions)
                ?? throw new StrictJsonException("NULL_DTO", "Wire DTO decoded to null.");
        }
        catch (WireDecodeException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is StrictJsonException or
            JsonException or
            ArgumentException or
            NotSupportedException)
        {
            throw new WireDecodeException(StableDiagnosticCodes.MalformedMessage);
        }
    }

    private static void ValidateNestedRequiredProperties(Type dtoType, JsonElement root)
    {
        if (dtoType == typeof(HandshakeRequest) || dtoType == typeof(StableDiagnostic))
        {
            return;
        }

        if (dtoType == typeof(HandshakeResponse))
        {
            ValidateNullableObject(root, "diagnostic", ValidateDiagnostic);
            return;
        }

        if (dtoType == typeof(PeerHello))
        {
            ValidateTypedHash(root, "imageIdentity");
            return;
        }

        if (dtoType == typeof(PeerSessionAccepted))
        {
            return;
        }

        if (dtoType == typeof(RegisteredProjectSelection))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidatePositiveInt64(root, "brokerGeneration");
            ValidatePositiveInt64(root, "registrationGeneration");
            return;
        }

        if (dtoType == typeof(ProjectRegistrationAttestation))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidateTypedHash(root, "volumeIdentity");
            ValidateTypedHash(root, "repositoryIdentity");
            ValidateTypedHash(root, "projectRootIdentity");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, ProjectRegistrationAttestation.SelfHashType);
            return;
        }

        if (dtoType == typeof(ProjectLeaseDescriptor))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, ProjectLeaseDescriptor.SelfHashType);
            return;
        }

        if (dtoType == typeof(WorkerProjectLocator))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidateTypedHash(root, "volumeIdentity");
            ValidateTypedHash(root, "repositoryIdentity");
            ValidateTypedHash(root, "projectRootIdentity");
            ValidatePositiveInt64(root, "brokerGeneration");
            ValidatePositiveInt64(root, "registrationGeneration");
            ValidatePositiveInt64(root, "enrollmentGeneration");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, WorkerProjectLocator.SelfHashType);
            return;
        }

        if (dtoType == typeof(WorkerProjectLocatorAcknowledgement))
        {
            ValidatePositiveInt64(root, "brokerGeneration");
            ValidatePositiveInt64(root, "registrationGeneration");
            ValidatePositiveInt64(root, "enrollmentGeneration");
            ValidateTypedHash(root, "locatorSelfHash");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, WorkerProjectLocatorAcknowledgement.SelfHashType);
            return;
        }

        if (dtoType == typeof(WorkerProjectHandleGrant))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidateTypedHash(root, "volumeIdentity");
            ValidateTypedHash(root, "repositoryIdentity");
            ValidateTypedHash(root, "projectRootIdentity");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, WorkerProjectHandleGrant.SelfHashType);
            return;
        }

        if (dtoType == typeof(WorkerProjectHandleGrantAcknowledgement))
        {
            ValidateTypedHash(root, "grantSelfHash");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, WorkerProjectHandleGrantAcknowledgement.SelfHashType);
            return;
        }

        if (dtoType == typeof(WorkerProjectHandleRevoke))
        {
            ValidateTypedHash(root, "grantSelfHash");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, WorkerProjectHandleRevoke.SelfHashType);
            return;
        }

        if (dtoType == typeof(WorkerProjectHandleRevokeAcknowledgement))
        {
            ValidateTypedHash(root, "grantSelfHash");
            ValidateTypedHash(root, "revokeSelfHash");
            ValidateTypedHash(root, "selfHash");
            ValidateSelfHash(root, WorkerProjectHandleRevokeAcknowledgement.SelfHashType);
            return;
        }

        if (dtoType == typeof(ReadDocumentQuery))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidateNullableObject(root, "expectedContentHash", ValidateTypedHashElement);
            return;
        }

        if (dtoType == typeof(ReadDocumentResult))
        {
            ValidateTypedHash(root, "projectIdentity");
            ValidateNullableObject(root, "contentHash", ValidateTypedHashElement);
            ValidateNullableObject(root, "diagnostic", ValidateDiagnostic);
            return;
        }

        if (dtoType == typeof(CommandEnvelope))
        {
            ValidateCommandEnvelope(root);
            return;
        }

        if (dtoType == typeof(ValidateRecipeCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.ValidateRecipe,
                ValidateRecipeCommand.SelfHashType,
                "recipeContentHash",
                "recipeContractHash");
            return;
        }

        if (dtoType == typeof(BuildCandidateCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.BuildCandidate,
                BuildCandidateCommand.SelfHashType,
                "recipeValidationHash",
                "buildDefinitionHash",
                "candidateIdentity");
            return;
        }

        if (dtoType == typeof(OpenPreviewJobCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.OpenPreviewJob,
                OpenPreviewJobCommand.SelfHashType,
                "candidateIdentity",
                "previewIdentity");
            return;
        }

        if (dtoType == typeof(ClosePreviewJobCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.ClosePreviewJob,
                ClosePreviewJobCommand.SelfHashType,
                "previewIdentity");
            ValidateJobCorrelation(root, "targetPreviewJob");
            return;
        }

        if (dtoType == typeof(SetPreviewPlaybackCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.SetPreviewPlayback,
                SetPreviewPlaybackCommand.SelfHashType,
                "previewIdentity");
            ValidateJobCorrelation(root, "targetPreviewJob");
            return;
        }

        if (dtoType == typeof(ValidatePatchCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.ValidatePatch,
                ValidatePatchCommand.SelfHashType,
                "patchContentHash",
                "targetCandidateIdentity");
            return;
        }

        if (dtoType == typeof(ApplyPatchCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.ApplyPatch,
                ApplyPatchCommand.SelfHashType,
                "patchValidationHash",
                "targetCandidateIdentity");
            return;
        }

        if (dtoType == typeof(RunFocusedTestsCommand))
        {
            ValidateCommand(
                root,
                CommandKinds.RunFocusedTests,
                RunFocusedTestsCommand.SelfHashType,
                "targetCandidateIdentity",
                "focusedTestPlanHash");
            ValidateStringArray(root, "testIds");
            return;
        }

        if (dtoType == typeof(CancelJobCommand))
        {
            ValidateCommand(root, CommandKinds.CancelJob, CancelJobCommand.SelfHashType);
            ValidateJobCorrelation(root, "targetJob");
            return;
        }

        if (dtoType == typeof(JobProgress))
        {
            ValidateJobEventEnvelope(root);
            ValidateSelfHash(root, JobProgress.SelfHashType);
            return;
        }

        if (dtoType == typeof(JobLogEvent))
        {
            ValidateJobEventEnvelope(root);
            ValidateDiagnostic(ExactObjectValidator.RequireProperty(root, "diagnostic", JsonValueKind.Object));
            ValidateSelfHash(root, JobLogEvent.SelfHashType);
            return;
        }

        if (dtoType == typeof(JobArtifact))
        {
            ValidateJobEventEnvelope(root);
            ValidateTypedHash(root, "artifactHash");
            ValidateSelfHash(root, JobArtifact.SelfHashType);
            return;
        }

        if (dtoType == typeof(JobCompletion))
        {
            ValidateJobEventEnvelope(root);
            ValidateNullableObject(root, "diagnostic", ValidateDiagnostic);
            ValidateUtcTimestampLexeme(
                ExactObjectValidator.RequireString(root, "completedAtUtc"));
            ValidateSelfHash(root, JobCompletion.SelfHashType);
            return;
        }

        if (dtoType == typeof(StatusProvenance))
        {
            ValidateProvenance(root);
            return;
        }

        if (dtoType == typeof(MachineStatus) ||
            dtoType == typeof(VisualStatus) ||
            dtoType == typeof(UserVerdictStatus) ||
            dtoType == typeof(L3Status) ||
            dtoType == typeof(L4Status))
        {
            ValidateNullableObject(root, "provenance", ValidateProvenance);
            return;
        }

        throw new InvalidOperationException("Registered wire DTO lacks a compiled nested shape.");
    }

    private static void ValidateDiagnostic(JsonElement diagnostic) =>
        ExactObjectValidator.Validate(diagnostic, DiagnosticProperties);

    private static void ValidateCommand(
        JsonElement root,
        string expectedCommandKind,
        string selfHashType,
        params string[] typedHashProperties)
    {
        var envelope = ExactObjectValidator.RequireProperty(root, "envelope", JsonValueKind.Object);
        ValidateCommandEnvelope(envelope);
        if (!string.Equals(
                ExactObjectValidator.RequireString(root, "protocolVersion"),
                ExactObjectValidator.RequireString(envelope, "protocolVersion"),
                StringComparison.Ordinal))
        {
            throw new StrictJsonException("COMMAND_ENVELOPE_MISMATCH", "Command protocol versions do not match.");
        }

        if (!string.Equals(
                ExactObjectValidator.RequireString(envelope, "commandKind"),
                expectedCommandKind,
                StringComparison.Ordinal) ||
            !string.Equals(
                ExactObjectValidator.RequireString(envelope, "commandCapability"),
                CommandCapabilityIds.ForCommand(expectedCommandKind),
                StringComparison.Ordinal))
        {
            throw new StrictJsonException("COMMAND_ENVELOPE_MISMATCH", "Command envelope does not match the root command type.");
        }

        foreach (var propertyName in typedHashProperties)
        {
            ValidateTypedHash(root, propertyName);
        }

        ValidateSelfHash(root, selfHashType);
    }

    private static void ValidateCommandEnvelope(JsonElement envelope)
    {
        ExactObjectValidator.Validate(envelope, CommandEnvelopeProperties);
        ValidatePositiveInt64(envelope, "leaseGeneration");
        ValidateTypedHash(envelope, "projectIdentity");
        var confirmationPolicy = ExactObjectValidator.RequireProperty(
            envelope,
            "confirmationPolicy",
            JsonValueKind.Object);
        ExactObjectValidator.Validate(confirmationPolicy, ConfirmationPolicyProperties);
        ValidateTypedHash(confirmationPolicy, "policyIdentity");
        var commandKind = ExactObjectValidator.RequireString(envelope, "commandKind");
        var commandCapability = ExactObjectValidator.RequireString(envelope, "commandCapability");
        try
        {
            if (!string.Equals(
                    commandCapability,
                    CommandCapabilityIds.ForCommand(commandKind),
                    StringComparison.Ordinal))
            {
                throw new StrictJsonException("COMMAND_CAPABILITY_MISMATCH", "Command capability does not match the command kind.");
            }
        }
        catch (ArgumentException)
        {
            throw new StrictJsonException("UNKNOWN_COMMAND_KIND", "Command kind is outside the closed vocabulary.");
        }

        ValidateSelfHash(envelope, CommandEnvelope.SelfHashType);
    }

    private static void ValidateJobCorrelation(JsonElement parent, string propertyName)
    {
        var correlation = ExactObjectValidator.RequireProperty(parent, propertyName, JsonValueKind.Object);
        ExactObjectValidator.Validate(correlation, JobCorrelationProperties);
        ValidateTypedHash(correlation, "originCommandSelfHash");
        var commandKind = ExactObjectValidator.RequireString(correlation, "originCommandKind");
        var originHash = ExactObjectValidator.RequireProperty(correlation, "originCommandSelfHash", JsonValueKind.Object);
        var typeTag = ExactObjectValidator.RequireString(originHash, "typeTag");
        try
        {
            if (!string.Equals(typeTag, CommandSelfHashTypes.ForKind(commandKind), StringComparison.Ordinal))
            {
                throw new StrictJsonException("JOB_CORRELATION_MISMATCH", "Job correlation uses the wrong command hash domain.");
            }
        }
        catch (ArgumentException)
        {
            throw new StrictJsonException("UNKNOWN_COMMAND_KIND", "Job correlation uses an unknown command kind.");
        }
    }

    private static void ValidateJobEventEnvelope(JsonElement root)
    {
        ValidatePositiveInt64(root, "leaseGeneration");
        ValidatePositiveInt64(root, "eventSequence");
        ValidateTypedHash(root, "projectIdentity");
        ValidateJobCorrelation(root, "job");
    }

    private static void ValidatePositiveInt64(JsonElement parent, string propertyName)
    {
        var value = ExactObjectValidator.RequireProperty(parent, propertyName, JsonValueKind.Number);
        if (!value.TryGetInt64(out var number) || number <= 0)
        {
            throw new StrictJsonException(
                "INVALID_POSITIVE_INT64",
                "A required positive Int64 field is out of range or non-positive.");
        }
    }

    private static void ValidateStringArray(JsonElement parent, string propertyName)
    {
        var values = ExactObjectValidator.RequireProperty(parent, propertyName, JsonValueKind.Array);
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                throw new StrictJsonException("WRONG_TYPE", "An array item has the wrong JSON type.");
            }
        }
    }

    private static void ValidateTypedHash(JsonElement parent, string propertyName) =>
        ValidateTypedHashElement(ExactObjectValidator.RequireProperty(parent, propertyName, JsonValueKind.Object));

    private static void ValidateTypedHashElement(JsonElement value) =>
        ExactObjectValidator.Validate(value, TypedHashProperties);

    private static void ValidateSelfHash(JsonElement root, string typeTag)
    {
        if (!SelfHash.VerifyElement(root, typeTag))
        {
            throw new StrictJsonException("SELF_HASH_MISMATCH", "A wire self-hash is invalid.");
        }
    }

    private static void ValidateProvenance(JsonElement provenance)
    {
        ExactObjectValidator.Validate(provenance, ProvenanceProperties);
        ValidateUtcTimestampLexeme(
            ExactObjectValidator.RequireString(provenance, "observedAtUtc"));
        var sourceIdentity = ExactObjectValidator.RequireProperty(
            provenance,
            "sourceIdentity",
            JsonValueKind.Object);
        ExactObjectValidator.Validate(sourceIdentity, TypedHashProperties);
    }

    private static void ValidateUtcTimestampLexeme(string value)
    {
        var text = value.AsSpan();
        if (text.Length < 20 ||
            !AreAsciiDigits(text, 0, 4) || text[4] != '-' ||
            !AreAsciiDigits(text, 5, 2) || text[7] != '-' ||
            !AreAsciiDigits(text, 8, 2) || text[10] != 'T' ||
            !AreAsciiDigits(text, 11, 2) || text[13] != ':' ||
            !AreAsciiDigits(text, 14, 2) || text[16] != ':' ||
            !AreAsciiDigits(text, 17, 2))
        {
            throw new StrictJsonException(
                "INVALID_UTC_TIMESTAMP",
                "A UTC timestamp has an invalid lexical shape.");
        }

        var suffixStart = 19;
        if (text[suffixStart] == '.')
        {
            var fractionStart = ++suffixStart;
            while (suffixStart < text.Length && IsAsciiDigit(text[suffixStart]))
            {
                suffixStart++;
            }

            var fractionLength = suffixStart - fractionStart;
            if (fractionLength is < 1 or > 7)
            {
                throw new StrictJsonException(
                    "INVALID_UTC_TIMESTAMP",
                    "A UTC timestamp has an invalid lexical shape.");
            }
        }

        var suffix = text[suffixStart..];
        if (!(suffix.SequenceEqual("Z") || suffix.SequenceEqual("+00:00")))
        {
            throw new StrictJsonException(
                "INVALID_UTC_TIMESTAMP",
                "A UTC timestamp has an invalid lexical shape.");
        }
    }

    private static bool AreAsciiDigits(ReadOnlySpan<char> text, int start, int count)
    {
        for (var index = start; index < start + count; index++)
        {
            if (!IsAsciiDigit(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';

    private static void ValidateNullableObject(
        JsonElement parent,
        string propertyName,
        Action<JsonElement> validate)
    {
        if (!parent.TryGetProperty(propertyName, out var nested))
        {
            throw new StrictJsonException("MISSING_PROPERTY", "An object is missing a required property.");
        }

        if (nested.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (nested.ValueKind != JsonValueKind.Object)
        {
            throw new StrictJsonException("WRONG_TYPE", "A property has the wrong JSON type.");
        }

        validate(nested);
    }
}
