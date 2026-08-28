using System.Security.Cryptography;
using System.Text.Json;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Json;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class WorkerHandleLifecycleGoldenVectorTests
{
    [TestMethod]
    public void FrozenWorkerLifecycleVectorsDecodeWithExactBytesAndSelfHashes()
    {
        var vectors = LoadVectors();
        Assert.AreEqual(4, vectors.Count);

        var grant = StrictWireCodec.Decode<WorkerProjectHandleGrant>(vectors["grant"]);
        var grantAcknowledgement = StrictWireCodec.Decode<WorkerProjectHandleGrantAcknowledgement>(
            vectors["grantAcknowledgement"]);
        var revoke = StrictWireCodec.Decode<WorkerProjectHandleRevoke>(vectors["revoke"]);
        var revokeAcknowledgement = StrictWireCodec.Decode<WorkerProjectHandleRevokeAcknowledgement>(
            vectors["revokeAcknowledgement"]);

        Assert.IsTrue(grant.SelfHash.FixedTimeEquals(grantAcknowledgement.GrantSelfHash));
        Assert.IsTrue(grant.SelfHash.FixedTimeEquals(revoke.GrantSelfHash));
        Assert.IsTrue(grant.SelfHash.FixedTimeEquals(revokeAcknowledgement.GrantSelfHash));
        Assert.IsTrue(revoke.SelfHash.FixedTimeEquals(revokeAcknowledgement.RevokeSelfHash));
        Assert.AreEqual("sha256:9dd30110fb67745bbd21fa955d4fdcae1451c01281d3d97fa8609475e046004b", grant.SelfHash.Digest);
        Assert.AreEqual("sha256:b1e5075aefd9e906d3040350df0b15cbeff614590bfae8bcb8ddc6cb4872d550", grantAcknowledgement.SelfHash.Digest);
        Assert.AreEqual("sha256:0f78bdabe0a350ee2df4129315ff82358d56809d9831b81af468eddff7d8e297", revoke.SelfHash.Digest);
        Assert.AreEqual("sha256:1d1145feb3e5506aebb5c24859735af9c35714ef9f1d1a5571354d95f9684790", revokeAcknowledgement.SelfHash.Digest);
    }

    [TestMethod]
    public void FrozenVectorPhysicalHashesAndLengthsAreExact()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "GoldenVectors",
            "desktop-phase2-worker-handle-lifecycle-v1.json");
        using var document = StrictJsonReader.Parse(File.ReadAllBytes(path));
        var vectors = document.RootElement.GetProperty("vectors").EnumerateArray().ToArray();
        foreach (var vector in vectors)
        {
            var bytes = Convert.FromBase64String(vector.GetProperty("base64").GetString()!);
            Assert.AreEqual(vector.GetProperty("byteLength").GetInt32(), bytes.Length);
            Assert.AreEqual(
                vector.GetProperty("sha256").GetString(),
                "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
    }

    private static Dictionary<string, byte[]> LoadVectors()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "GoldenVectors",
            "desktop-phase2-worker-handle-lifecycle-v1.json");
        using var document = StrictJsonReader.Parse(File.ReadAllBytes(path));
        var root = document.RootElement;
        Assert.AreEqual(
            "vfxcomposer.worker-handle-lifecycle-golden-vectors/1",
            root.GetProperty("schema").GetString());
        Assert.AreEqual("base64-of-exact-utf8-json", root.GetProperty("encoding").GetString());
        return root.GetProperty("vectors")
            .EnumerateArray()
            .ToDictionary(
                vector => vector.GetProperty("name").GetString()!,
                vector => Convert.FromBase64String(vector.GetProperty("base64").GetString()!),
                StringComparer.Ordinal);
    }
}
