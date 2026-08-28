using System.Security.Cryptography;
using System.Text.Json;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class WorkerProjectLocatorGoldenVectorTests
{
    private const string LocatorDigest =
        "sha256:d5f66b315be8d5677467e795638e30b8a3e5d1f12007836690686035022d6fc6";
    private const string AcknowledgementDigest =
        "sha256:7f2a07288195216a6d25a547d415d32ef37a258a04f9c90d8a1a664428ac8b47";

    [TestMethod]
    public void FrozenLocatorAndAcknowledgementVectorsAreCanonicalExactBytesAndExactCorrelations()
    {
        var vectors = LoadVectors();
        Assert.AreEqual(2, vectors.Count);

        var locatorVector = vectors["locator"];
        var acknowledgementVector = vectors["locatorAcknowledgement"];
        AssertVectorIsCanonicalAndPhysical(locatorVector);
        AssertVectorIsCanonicalAndPhysical(acknowledgementVector);

        var locator = StrictWireCodec.Decode<WorkerProjectLocator>(locatorVector.Bytes);
        var acknowledgement = StrictWireCodec.Decode<WorkerProjectLocatorAcknowledgement>(acknowledgementVector.Bytes);
        Assert.AreEqual(LocatorDigest, locator.SelfHash.Digest);
        Assert.AreEqual(AcknowledgementDigest, acknowledgement.SelfHash.Digest);
        Assert.IsTrue(locator.SelfHash.FixedTimeEquals(locatorVector.SelfHash));
        Assert.IsTrue(acknowledgement.SelfHash.FixedTimeEquals(acknowledgementVector.SelfHash));
        Assert.IsTrue(locator.SelfHash.FixedTimeEquals(acknowledgement.LocatorSelfHash));
        Assert.AreEqual(locator.RequestId, acknowledgement.RequestId);
        Assert.AreEqual(locator.RegisteredProjectId, acknowledgement.RegisteredProjectId);
        Assert.AreEqual(locator.BrokerGeneration, acknowledgement.BrokerGeneration);
        Assert.AreEqual(locator.RegistrationGeneration, acknowledgement.RegistrationGeneration);
        Assert.AreEqual(locator.EnrollmentGeneration, acknowledgement.EnrollmentGeneration);
        Assert.AreEqual(locator.WorkerSessionId, acknowledgement.WorkerSessionId);
        Assert.AreEqual(locator.WorkerProcessEpoch, acknowledgement.WorkerProcessEpoch);
        Assert.AreEqual(WorkerProjectLocatorAcknowledgement.AcceptedDisposition, acknowledgement.Disposition);
    }

    [TestMethod]
    public void FrozenVectorEnvelopeHasOnlyItsDocumentedNonRuntimeShape()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "GoldenVectors",
            "desktop-phase2-worker-project-locator-v1.json");
        var payload = File.ReadAllBytes(path);
        using var document = StrictJsonReader.Parse(payload);
        var root = document.RootElement;
        ExactObjectValidator.Validate(root, ["encoding", "schema", "vectors"]);
        Assert.AreEqual(
            "vfxcomposer.worker-project-locator-golden-vectors/1",
            ExactObjectValidator.RequireString(root, "schema"));
        Assert.AreEqual(
            "base64-of-exact-utf8-json",
            ExactObjectValidator.RequireString(root, "encoding"));

        foreach (var vector in ExactObjectValidator.RequireProperty(root, "vectors", JsonValueKind.Array).EnumerateArray())
        {
            ExactObjectValidator.Validate(vector, ["base64", "byteLength", "name", "selfHash", "sha256"]);
            Assert.AreEqual(JsonValueKind.String, vector.GetProperty("name").ValueKind);
            Assert.AreEqual(JsonValueKind.Number, vector.GetProperty("byteLength").ValueKind);
            Assert.AreEqual(JsonValueKind.String, vector.GetProperty("sha256").ValueKind);
            Assert.AreEqual(JsonValueKind.String, vector.GetProperty("base64").ValueKind);
            _ = TypedHash.FromJson(vector.GetProperty("selfHash"));
        }
    }

    private static void AssertVectorIsCanonicalAndPhysical(GoldenVector vector)
    {
        CollectionAssert.AreEqual(vector.Bytes, CanonicalJson.Canonicalize(vector.Bytes), vector.Name);
        Assert.AreEqual(vector.ByteLength, vector.Bytes.Length, vector.Name);
        Assert.AreEqual(
            vector.Sha256,
            "sha256:" + Convert.ToHexString(SHA256.HashData(vector.Bytes)).ToLowerInvariant(),
            vector.Name);
    }

    private static Dictionary<string, GoldenVector> LoadVectors()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "GoldenVectors",
            "desktop-phase2-worker-project-locator-v1.json");
        using var document = StrictJsonReader.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        ExactObjectValidator.Validate(root, ["encoding", "schema", "vectors"]);
        Assert.AreEqual(
            "vfxcomposer.worker-project-locator-golden-vectors/1",
            ExactObjectValidator.RequireString(root, "schema"));
        Assert.AreEqual(
            "base64-of-exact-utf8-json",
            ExactObjectValidator.RequireString(root, "encoding"));

        return ExactObjectValidator.RequireProperty(root, "vectors", JsonValueKind.Array)
            .EnumerateArray()
            .Select(vector =>
            {
                ExactObjectValidator.Validate(vector, ["base64", "byteLength", "name", "selfHash", "sha256"]);
                return new GoldenVector(
                    ExactObjectValidator.RequireString(vector, "name"),
                    Convert.FromBase64String(ExactObjectValidator.RequireString(vector, "base64")),
                    ExactObjectValidator.RequireProperty(vector, "byteLength", JsonValueKind.Number).GetInt32(),
                    ExactObjectValidator.RequireString(vector, "sha256"),
                    TypedHash.FromJson(ExactObjectValidator.RequireProperty(vector, "selfHash", JsonValueKind.Object)));
            })
            .ToDictionary(vector => vector.Name, StringComparer.Ordinal);
    }

    private sealed record GoldenVector(
        string Name,
        byte[] Bytes,
        int ByteLength,
        string Sha256,
        TypedHash SelfHash);
}
