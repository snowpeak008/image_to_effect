using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.Protocol.Commands;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Jobs;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class Phase3CommandJobContractTests
{
    [TestMethod]
    public void ClosedRegistriesContainExactlyThePhase3ContractSet()
    {
        Assert.AreEqual(9, CommandContractRegistry.All.Count);
        Assert.AreEqual(4, JobContractRegistry.All.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                MessageKinds.ValidateRecipeCommand,
                MessageKinds.BuildCandidateCommand,
                MessageKinds.OpenPreviewJobCommand,
                MessageKinds.ClosePreviewJobCommand,
                MessageKinds.SetPreviewPlaybackCommand,
                MessageKinds.ValidatePatchCommand,
                MessageKinds.ApplyPatchCommand,
                MessageKinds.RunFocusedTestsCommand,
                MessageKinds.CancelJobCommand,
            },
            CommandKinds.All.ToArray());
        CollectionAssert.AreEquivalent(
            new[]
            {
                MessageKinds.JobProgress,
                MessageKinds.JobLogEvent,
                MessageKinds.JobArtifact,
                MessageKinds.JobCompletion,
            },
            JobMessageKinds.All.ToArray());
        Assert.IsFalse(CommandKinds.IsKnown("command.unknown"));
        Assert.IsFalse(CommandCapabilityIds.IsKnown("command.unknown.v1"));
        Assert.IsFalse(ConfirmationPolicyIds.IsKnown("confirmation.approved.v1"));
    }

    [TestMethod]
    public void IndependentGoldenVectorsHaveExactPhysicalBytesAndDecodeThroughTheOnlyIngress()
    {
        var vectors = LoadVectors();
        Assert.AreEqual(14, vectors.Count);

        foreach (var vector in vectors.Values)
        {
            var bytes = Convert.FromBase64String(vector.Base64);
            Assert.AreEqual(vector.ByteLength, bytes.Length, vector.Name);
            Assert.AreEqual(
                vector.Sha256,
                "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                vector.Name);

            var decoded = Decode(vector.Name, bytes);
            var selfHash = decoded.GetType().GetProperty("SelfHash")!.GetValue(decoded) as TypedHash;
            Assert.IsNotNull(selfHash, vector.Name);
            Assert.AreEqual(vector.SelfHash, selfHash.Digest, vector.Name);
        }
    }

    [TestMethod]
    public void StrictIngressRejectsUnknownMissingWrongTypeAndHashForEveryPhase3Shape()
    {
        foreach (var vector in LoadVectors().Values)
        {
            var unknown = ParseVector(vector);
            unknown["callerPath"] = "C:/not-admitted";
            AssertMalformed(vector.Name, unknown, "unknown " + vector.Name);

            var missing = ParseVector(vector);
            missing.Remove("selfHash");
            AssertMalformed(vector.Name, missing, "missing " + vector.Name);

            var wrongType = ParseVector(vector);
            var leaseOwner = wrongType["envelope"] as JsonObject ?? wrongType;
            leaseOwner["leaseGeneration"] = false;
            AssertMalformed(vector.Name, wrongType, "wrong type " + vector.Name);

            var hashMismatch = ParseVector(vector);
            ((JsonObject)hashMismatch["selfHash"]!)["digest"] = "sha256:" + new string('0', 64);
            AssertMalformed(vector.Name, hashMismatch, "hash " + vector.Name);
        }
    }

    [TestMethod]
    public void StrictIngressRejectsPositiveInt64OverflowForEveryPhase3LongField()
    {
        const decimal Int64Overflow = 9_223_372_036_854_775_808m;

        foreach (var vector in LoadVectors().Values)
        {
            var overlongGeneration = ParseVector(vector);
            var leaseOwner = overlongGeneration["envelope"] as JsonObject ?? overlongGeneration;
            leaseOwner["leaseGeneration"] = JsonValue.Create(Int64Overflow);
            AssertMalformed(vector.Name, overlongGeneration, "lease generation overflow " + vector.Name);

            if (overlongGeneration["envelope"] is null)
            {
                var overlongSequence = ParseVector(vector);
                overlongSequence["eventSequence"] = JsonValue.Create(Int64Overflow);
                AssertMalformed(vector.Name, overlongSequence, "event sequence overflow " + vector.Name);
            }
        }
    }

    [TestMethod]
    public void StrictIngressRejectsWrongVersionWrongKindAndImpossibleCorrelation()
    {
        var command = ParseVector(LoadVectors()["validateRecipe"]);
        command["protocolVersion"] = "vfxcomposer.protocol/99.0";
        AssertWireCode("validateRecipe", command, StableDiagnosticCodes.UnsupportedProtocolVersion);

        command = ParseVector(LoadVectors()["validateRecipe"]);
        command["messageKind"] = MessageKinds.BuildCandidateCommand;
        AssertWireCode("validateRecipe", command, StableDiagnosticCodes.UnsupportedMessageKind);

        command = ParseVector(LoadVectors()["validateRecipe"]);
        var envelope = (JsonObject)command["envelope"]!;
        envelope["commandKind"] = CommandKinds.BuildCandidate;
        envelope["commandCapability"] = CommandCapabilityIds.BuildCandidateV1;
        AssertMalformed("validateRecipe", command, "cross command envelope");

        var job = ParseVector(LoadVectors()["jobProgress"]);
        var correlation = (JsonObject)job["job"]!;
        ((JsonObject)correlation["originCommandSelfHash"]!)["typeTag"] = BuildCandidateCommand.SelfHashType;
        AssertMalformed("jobProgress", job, "wrong correlation hash domain");

        job = ParseVector(LoadVectors()["jobProgress"]);
        correlation = (JsonObject)job["job"]!;
        correlation["jobId"] = correlation["originRequestId"]!.GetValue<string>();
        AssertMalformed("jobProgress", job, "duplicate correlation identity");
    }

    [TestMethod]
    public void StrictIngressRejectsRawRecipePatchAndLocationClaims()
    {
        foreach (var pair in new[]
                 {
                     ("validateRecipe", "rawRecipeJson"),
                     ("buildCandidate", "rawRecipeJson"),
                     ("validatePatch", "rawPatchJson"),
                     ("applyPatch", "rawPatchJson"),
                     ("jobArtifact", "outputPath"),
                 })
        {
            var value = ParseVector(LoadVectors()[pair.Item1]);
            value[pair.Item2] = "not-a-formal-ticket";
            AssertMalformed(pair.Item1, value, pair.Item2);
        }
    }

    [TestMethod]
    public void ConstructorsRejectClosedVocabularyAndStructuralCorrelationViolations()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CommandEnvelope(
                ProtocolVersions.Current,
                "request-01",
                "command-01",
                "idem-01",
                "lease-01",
                Phase3WireFixtures.Hash("vfxcomposer.project-identity/1", "project"),
                1,
                CommandKinds.ValidateRecipe,
                CommandCapabilityIds.BuildCandidateV1,
                new ConfirmationPolicyReference(
                    ConfirmationPolicyIds.ReferenceV1,
                    Phase3WireFixtures.Hash(ConfirmationPolicyReference.PolicyIdentityType, "policy")),
                Phase3WireFixtures.Hash(CommandEnvelope.SelfHashType, "envelope")));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new JobCorrelation(
                "job-01",
                "request-01",
                "command-01",
                "idem-01",
                CommandKinds.ValidateRecipe,
                Phase3WireFixtures.Hash(BuildCandidateCommand.SelfHashType, "wrong-domain")));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new RunFocusedTestsCommand(
                ProtocolVersions.Current,
                MessageKinds.RunFocusedTestsCommand,
                Phase3WireFixtures.Envelope(CommandKinds.RunFocusedTests, "unsorted"),
                "candidate-01",
                Phase3WireFixtures.Hash("vfxcomposer.candidate-identity/1", "candidate"),
                ["test-z", "test-a"],
                Phase3WireFixtures.Hash("vfxcomposer.focused-test-plan/1", "plan"),
                Phase3WireFixtures.Hash(RunFocusedTestsCommand.SelfHashType, "tests")));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new ClosePreviewJobCommand(
                ProtocolVersions.Current,
                MessageKinds.ClosePreviewJobCommand,
                Phase3WireFixtures.Envelope(CommandKinds.ClosePreviewJob, "wrong-target"),
                Phase3WireFixtures.Hash("vfxcomposer.preview-identity/1", "preview"),
                Phase3WireFixtures.Correlation(CommandKinds.ValidateRecipe),
                Phase3WireFixtures.Hash(ClosePreviewJobCommand.SelfHashType, "close")));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new JobCompletion(
                ProtocolVersions.Current,
                MessageKinds.JobCompletion,
                Phase3WireFixtures.Hash("vfxcomposer.project-identity/1", "project"),
                "lease-01",
                1,
                Phase3WireFixtures.Correlation(),
                1,
                JobCompletionOutcomes.Succeeded,
                0,
                StableDiagnosticCatalog.Create(StableDiagnosticCodes.Disconnected),
                DateTimeOffset.UnixEpoch,
                Phase3WireFixtures.Hash(JobCompletion.SelfHashType, "completion")));
    }

    [TestMethod]
    public void CommandAndJobContractsExposeNoRawPayloadLocationOrAuthorityFields()
    {
        var forbiddenFragments = new[]
        {
            "Path",
            "RawRecipe",
            "RawPatch",
            "Output",
            "Verdict",
            "Machine",
            "Visual",
            "L3",
            "L4",
            "Authority",
        };
        var wireTypes = CommandContractRegistry.All.Select(value => value.DtoType)
            .Concat(JobContractRegistry.All.Select(value => value.DtoType))
            .Append(typeof(CommandEnvelope));

        foreach (var type in wireTypes)
        {
            foreach (var property in type.GetProperties())
            {
                foreach (var fragment in forbiddenFragments)
                {
                    Assert.IsFalse(
                        property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                        $"{type.FullName}.{property.Name} exposes {fragment}.");
                }
            }
        }

        Assert.IsFalse(typeof(ConfirmationPolicyReference).GetProperties()
            .Any(property => property.Name.Contains("Verdict", StringComparison.OrdinalIgnoreCase)));
    }

    private static Dictionary<string, GoldenVector> LoadVectors()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "GoldenVectors", "phase3-command-job-v1.json");
        using var document = StrictJsonReader.Parse(File.ReadAllBytes(path));
        Assert.AreEqual(
            "vfxcomposer.phase3-command-job-golden-vectors/1",
            document.RootElement.GetProperty("schema").GetString());
        Assert.AreEqual("base64-of-exact-utf8-json", document.RootElement.GetProperty("encoding").GetString());
        Assert.AreEqual(
            "python-stdlib-independent-canonical-json-and-length-prefixed-sha256",
            document.RootElement.GetProperty("generator").GetString());

        return document.RootElement.GetProperty("vectors")
            .EnumerateArray()
            .Select(value => new GoldenVector(
                value.GetProperty("name").GetString()!,
                value.GetProperty("dtoType").GetString()!,
                value.GetProperty("base64").GetString()!,
                value.GetProperty("byteLength").GetInt32(),
                value.GetProperty("sha256").GetString()!,
                value.GetProperty("selfHash").GetString()!))
            .ToDictionary(value => value.Name, StringComparer.Ordinal);
    }

    private static JsonObject ParseVector(GoldenVector vector) =>
        JsonNode.Parse(Convert.FromBase64String(vector.Base64))!.AsObject();

    private static void AssertMalformed(string vectorName, JsonObject value, string message)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode(vectorName, Encoding.UTF8.GetBytes(value.ToJsonString())));
        Assert.AreEqual(StableDiagnosticCodes.MalformedMessage, exception.Diagnostic.Code, message);
    }

    private static void AssertWireCode(string vectorName, JsonObject value, string expectedCode)
    {
        var exception = Assert.ThrowsExactly<WireDecodeException>(() =>
            Decode(vectorName, Encoding.UTF8.GetBytes(value.ToJsonString())));
        Assert.AreEqual(expectedCode, exception.Diagnostic.Code);
    }

    private static object Decode(string vectorName, byte[] bytes) => vectorName switch
    {
        "commandEnvelope" => StrictWireCodec.Decode<CommandEnvelope>(bytes),
        "validateRecipe" => StrictWireCodec.Decode<ValidateRecipeCommand>(bytes),
        "buildCandidate" => StrictWireCodec.Decode<BuildCandidateCommand>(bytes),
        "openPreviewJob" => StrictWireCodec.Decode<OpenPreviewJobCommand>(bytes),
        "closePreviewJob" => StrictWireCodec.Decode<ClosePreviewJobCommand>(bytes),
        "setPreviewPlayback" => StrictWireCodec.Decode<SetPreviewPlaybackCommand>(bytes),
        "validatePatch" => StrictWireCodec.Decode<ValidatePatchCommand>(bytes),
        "applyPatch" => StrictWireCodec.Decode<ApplyPatchCommand>(bytes),
        "runFocusedTests" => StrictWireCodec.Decode<RunFocusedTestsCommand>(bytes),
        "cancelJob" => StrictWireCodec.Decode<CancelJobCommand>(bytes),
        "jobProgress" => StrictWireCodec.Decode<JobProgress>(bytes),
        "jobLogEvent" => StrictWireCodec.Decode<JobLogEvent>(bytes),
        "jobArtifact" => StrictWireCodec.Decode<JobArtifact>(bytes),
        "jobCompletion" => StrictWireCodec.Decode<JobCompletion>(bytes),
        _ => throw new ArgumentOutOfRangeException(nameof(vectorName)),
    };

    private sealed record GoldenVector(
        string Name,
        string DtoType,
        string Base64,
        int ByteLength,
        string Sha256,
        string SelfHash);
}
