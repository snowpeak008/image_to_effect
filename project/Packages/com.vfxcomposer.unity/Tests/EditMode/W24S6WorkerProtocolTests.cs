using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S6WorkerProtocolTests
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Test]
        public void Net8GoldenVectorsDecodeAndUnityAcknowledgementsAreByteExact()
        {
            var vectors = LoadVectors();
            var grant = W24S6WorkerProtocolCodec.DecodeGrant(vectors["grant"]);
            var grantAcknowledgement = W24S6WorkerProtocolCodec.DecodeGrantAcknowledgementForTests(
                vectors["grantAcknowledgement"]);
            var revoke = W24S6WorkerProtocolCodec.DecodeRevoke(vectors["revoke"]);
            var revokeAcknowledgement = W24S6WorkerProtocolCodec.DecodeRevokeAcknowledgementForTests(
                vectors["revokeAcknowledgement"]);

            Assert.That(grant.SelfHash.Digest,
                Is.EqualTo("sha256:9dd30110fb67745bbd21fa955d4fdcae1451c01281d3d97fa8609475e046004b"));
            Assert.That(grantAcknowledgement.GrantSelfHash.Digest, Is.EqualTo(grant.SelfHash.Digest));
            Assert.That(revoke.GrantSelfHash.Digest, Is.EqualTo(grant.SelfHash.Digest));
            Assert.That(revokeAcknowledgement.GrantSelfHash.Digest, Is.EqualTo(grant.SelfHash.Digest));
            Assert.That(revokeAcknowledgement.RevokeSelfHash.Digest, Is.EqualTo(revoke.SelfHash.Digest));

            CollectionAssert.AreEqual(
                vectors["grantAcknowledgement"],
                W24S6WorkerProtocolCodec.CreateGrantAcknowledgementForTests(grant, "golden-grant-ack-01"));
            CollectionAssert.AreEqual(
                vectors["revokeAcknowledgement"],
                W24S6WorkerProtocolCodec.CreateRevokeAcknowledgementForTests(
                    grant,
                    revoke,
                    "golden-revoke-ack-01"));
        }

        [Test]
        public void C2LocatorGoldenVectorsDecodeProjectAndSealExactAcknowledgementBytes()
        {
            var vectors = LoadLocatorVectors();
            var locator = W24S6WorkerProtocolCodec.DecodeLocator(vectors["locator"]);
            var acknowledgement = W24S6WorkerProtocolCodec.DecodeLocatorAcknowledgementForTests(
                vectors["locatorAcknowledgement"],
                locator);

            Assert.That(locator.ProtocolVersion, Is.EqualTo(W24S6WorkerProtocolCodec.ProtocolVersion));
            Assert.That(locator.MessageKind, Is.EqualTo(W24S6WorkerProtocolCodec.LocatorKind));
            Assert.That(locator.RequestId, Is.EqualTo("locator-request-01"));
            Assert.That(locator.RegisteredProjectId, Is.EqualTo("registered-project-01"));
            Assert.That(locator.BrokerGeneration, Is.EqualTo(17));
            Assert.That(locator.RegistrationGeneration, Is.EqualTo(23));
            Assert.That(locator.EnrollmentGeneration, Is.EqualTo(29));
            Assert.That(locator.WorkerSessionId, Is.EqualTo("worker-session-01"));
            Assert.That(locator.WorkerProcessEpoch, Is.EqualTo("worker-epoch-01"));
            Assert.That(locator.ProjectIdentity.TypeTag, Is.EqualTo(W24S6WorkerProtocolCodec.ProjectIdentityType));
            Assert.That(locator.VolumeIdentity.TypeTag, Is.EqualTo(W24S6WorkerProtocolCodec.VolumeIdentityType));
            Assert.That(locator.RepositoryIdentity.TypeTag, Is.EqualTo(W24S6WorkerProtocolCodec.DirectoryIdentityType));
            Assert.That(locator.ProjectRootIdentity.TypeTag, Is.EqualTo(W24S6WorkerProtocolCodec.DirectoryIdentityType));
            Assert.That(locator.SelfHash.Digest,
                Is.EqualTo("sha256:d5f66b315be8d5677467e795638e30b8a3e5d1f12007836690686035022d6fc6"));
            Assert.That(acknowledgement.SelfHash.Digest,
                Is.EqualTo("sha256:7f2a07288195216a6d25a547d415d32ef37a258a04f9c90d8a1a664428ac8b47"));
            Assert.That(acknowledgement.LocatorSelfHash.Digest, Is.EqualTo(locator.SelfHash.Digest));
            CollectionAssert.AreEqual(
                vectors["locatorAcknowledgement"],
                W24S6WorkerProtocolCodec.CreateLocatorAcknowledgementForTests(locator));
        }

        [Test]
        public void C2LocatorRejectsMalformedPathAuthorityTypeGenerationAndHashDrift()
        {
            var locatorBytes = LoadLocatorVectors()["locator"];
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(
                new byte[] { 0xef, 0xbb, 0xbf }.Concat(locatorBytes).ToArray()));

            var text = StrictUtf8.GetString(locatorBytes);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(StrictUtf8.GetBytes(
                text.Replace(
                    "{\"brokerGeneration\":17",
                    "{\"brokerGeneration\":17,\"\\u0062rokerGeneration\":17"))));

            var callerPath = ParseVector(locatorBytes);
            callerPath["callerPath"] = "C:/untrusted";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(callerPath)));

            var authority = ParseVector(locatorBytes);
            authority["authority"] = "L4";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(authority)));

            var missing = ParseVector(locatorBytes);
            missing.Remove("workerSessionId");
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(missing)));

            var wrongType = ParseVector(locatorBytes);
            wrongType["enrollmentGeneration"] = "29";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(wrongType)));

            var zeroGeneration = ParseVector(locatorBytes);
            zeroGeneration["registrationGeneration"] = 0;
            ResealForTests(zeroGeneration, W24S6WorkerProtocolCodec.LocatorSelfHashType);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(zeroGeneration)));

            var wrongIdentityType = ParseVector(locatorBytes);
            ((JObject)wrongIdentityType["projectIdentity"])["typeTag"] =
                W24S6WorkerProtocolCodec.DirectoryIdentityType;
            ResealForTests(wrongIdentityType, W24S6WorkerProtocolCodec.LocatorSelfHashType);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(wrongIdentityType)));

            var tampered = ParseVector(locatorBytes);
            tampered["registeredProjectId"] = "registered-project-02";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocator(Encode(tampered)));
        }

        [Test]
        public void LocatorAcknowledgementRequiresExactDecodedLocatorCorrelation()
        {
            var vectors = LoadLocatorVectors();
            var locator = W24S6WorkerProtocolCodec.DecodeLocator(vectors["locator"]);
            var changedRoot = ParseVector(vectors["locator"]);
            changedRoot["workerSessionId"] = "worker-session-02";
            ResealForTests(changedRoot, W24S6WorkerProtocolCodec.LocatorSelfHashType);
            var changedLocator = W24S6WorkerProtocolCodec.DecodeLocator(Encode(changedRoot));

            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocatorAcknowledgementForTests(
                vectors["locatorAcknowledgement"],
                changedLocator));

            var disposition = ParseVector(vectors["locatorAcknowledgement"]);
            disposition["disposition"] = "GRANT_ACCEPTED";
            ResealForTests(disposition, W24S6WorkerProtocolCodec.LocatorAcknowledgementSelfHashType);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocatorAcknowledgementForTests(
                Encode(disposition),
                locator));

            var wrongLocatorHash = ParseVector(vectors["locatorAcknowledgement"]);
            ((JObject)wrongLocatorHash["locatorSelfHash"])["digest"] =
                "sha256:" + new string('d', 64);
            ResealForTests(wrongLocatorHash, W24S6WorkerProtocolCodec.LocatorAcknowledgementSelfHashType);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeLocatorAcknowledgementForTests(
                Encode(wrongLocatorHash),
                locator));
        }

        [Test]
        public void GoldenVectorWrapperPinsExactPhysicalBytesAndLengths()
        {
            var root = LoadVectorRoot();
            Assert.That((string)root["schema"],
                Is.EqualTo("vfxcomposer.worker-handle-lifecycle-golden-vectors/1"));
            Assert.That((string)root["encoding"], Is.EqualTo("base64-of-exact-utf8-json"));
            foreach (var vector in ((JArray)root["vectors"]).OfType<JObject>())
            {
                var bytes = Convert.FromBase64String((string)vector["base64"]);
                Assert.That(bytes.Length, Is.EqualTo((int)vector["byteLength"]));
                Assert.That("sha256:" + Hex(SHA256.Create().ComputeHash(bytes)),
                    Is.EqualTo((string)vector["sha256"]));
            }
        }

        [Test]
        public void GrantRejectsBomDuplicateUnknownMissingWrongTypeAndTamper()
        {
            var grantBytes = LoadVectors()["grant"];
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(
                new byte[] { 0xef, 0xbb, 0xbf }.Concat(grantBytes).ToArray()));
            var text = StrictUtf8.GetString(grantBytes);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(StrictUtf8.GetBytes(
                text.Replace(
                    "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\"",
                    "{\"protocolVersion\":\"vfxcomposer.protocol/1.0\",\"\\u0070rotocolVersion\":\"vfxcomposer.protocol/1.0\""))));

            var unknown = ParseVector(grantBytes);
            unknown["callerPath"] = "C:/untrusted";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(unknown)));

            var missing = ParseVector(grantBytes);
            missing.Remove("workerSessionId");
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(missing)));

            var wrongType = ParseVector(grantBytes);
            wrongType["brokerGeneration"] = "7";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(wrongType)));

            var fractional = ParseVector(grantBytes);
            fractional["leaseGeneration"] = 13.0;
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(fractional)));

            var invalidHandle = ParseVector(grantBytes);
            invalidHandle["volumeHandle"] = "0000000000000000";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(invalidHandle)));

            var uppercaseHandle = ParseVector(grantBytes);
            uppercaseHandle["volumeHandle"] = "00000000000000AF";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(uppercaseHandle)));

            var nestedUnknown = ParseVector(grantBytes);
            ((JObject)nestedUnknown["projectIdentity"])["path"] = "C:/untrusted";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(nestedUnknown)));

            var tampered = ParseVector(grantBytes);
            tampered["registeredProjectId"] = "project-fire-02";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(Encode(tampered)));
        }

        [Test]
        public void LifecycleMessagesRejectWrongKindDispositionHashAndCrossBinding()
        {
            var vectors = LoadVectors();
            var wrongKind = ParseVector(vectors["revoke"]);
            wrongKind["messageKind"] = W24S6WorkerProtocolCodec.GrantKind;
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeRevoke(Encode(wrongKind)));

            var wrongDisposition = ParseVector(vectors["grantAcknowledgement"]);
            wrongDisposition["disposition"] = "AUTHORITY_GRANTED";
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrantAcknowledgementForTests(Encode(wrongDisposition)));

            var wrongGrantHash = ParseVector(vectors["revoke"]);
            ((JObject)wrongGrantHash["grantSelfHash"])["digest"] = "sha256:" + new string('d', 64);
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeRevoke(Encode(wrongGrantHash)));

            var grant = W24S6WorkerProtocolCodec.DecodeGrant(vectors["grant"]);
            var revoke = W24S6WorkerProtocolCodec.DecodeRevoke(vectors["revoke"]);
            var mismatchedGrant = ParseVector(vectors["grant"]);
            mismatchedGrant["leaseId"] = "lease-other";
            ResealForTests(mismatchedGrant, W24S6WorkerProtocolCodec.GrantSelfHashType);
            var decodedMismatch = W24S6WorkerProtocolCodec.DecodeGrant(Encode(mismatchedGrant));
            AssertReject(() => W24S6WorkerProtocolCodec.CreateRevokeAcknowledgementForTests(
                decodedMismatch,
                revoke,
                "cross-binding-ack"));
            Assert.That(grant.LeaseId, Is.EqualTo(revoke.LeaseId));
        }

        [Test]
        public void DecoderIsBoundedAndFailureNeverEchoesInput()
        {
            var oversized = new byte[W24S6WorkerProtocolCodec.MaximumMessageBytes + 1];
            for (var index = 0; index < oversized.Length; index++) oversized[index] = (byte)' ';
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(oversized));

            var invalidUtf8 = new byte[] { (byte)'{', (byte)'"', 0xff, (byte)'"', (byte)'}' };
            AssertReject(() => W24S6WorkerProtocolCodec.DecodeGrant(invalidUtf8));
        }

        [Test]
        public void AdapterSurfaceHasNoNativeHandleIoTransportUnityOrAuthorityCapability()
        {
            var surface = typeof(W24S6WorkerProtocolCodec).Assembly
                .GetTypes()
                .Where(type => type.Namespace == "VFXComposer.Editor.W24.S6.Worker.Protocol")
                .ToArray();
            Assert.That(surface, Is.Not.Empty);
            foreach (var type in surface)
            {
                var signatures = type.GetMethods(
                        System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.Static)
                    .Select(method => method.ReturnType.FullName + " " + method.Name + " " +
                                      string.Join(" ", method.GetParameters().Select(parameter => parameter.ParameterType.FullName)))
                    .Concat(type.GetProperties().Select(property => property.PropertyType.FullName + " " + property.Name));
                foreach (var signature in signatures)
                {
                    Assert.That(signature, Does.Not.Contain("System.IntPtr"));
                    Assert.That(signature, Does.Not.Contain("SafeHandle"));
                    Assert.That(signature, Does.Not.Contain("UnityEngine"));
                    Assert.That(signature, Does.Not.Contain("Authority"));
                }
            }

            var source = File.ReadAllText(Path.Combine(
                RepositoryRoot(),
                "project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Protocol/W24S6WorkerProtocolCodec.cs"));
            foreach (var forbidden in new[]
                     {
                         "File.Open", "File.Read", "File.Write", "Directory.", "NamedPipe", "Socket", "Http",
                         "Tcp", "UnityEngine", "UnityEditor", "AssetDatabase", "EditorPrefs", "Environment."
                     })
                Assert.That(source, Does.Not.Contain(forbidden));
        }

        private static void AssertReject(TestDelegate action)
        {
            var exception = Assert.Throws<W24S6WorkerProtocolException>(action);
            Assert.That(exception.Message, Is.EqualTo(W24S6WorkerProtocolException.MalformedMessage));
            Assert.That(exception.InnerException, Is.Null);
        }

        private static Dictionary<string, byte[]> LoadVectors()
        {
            return ((JArray)LoadVectorRoot()["vectors"])
                .OfType<JObject>()
                .ToDictionary(
                    vector => (string)vector["name"],
                    vector => Convert.FromBase64String((string)vector["base64"]),
                    StringComparer.Ordinal);
        }

        private static Dictionary<string, byte[]> LoadLocatorVectors()
        {
            return ((JArray)LoadLocatorVectorRoot()["vectors"])
                .OfType<JObject>()
                .ToDictionary(
                    vector => (string)vector["name"],
                    vector => Convert.FromBase64String((string)vector["base64"]),
                    StringComparer.Ordinal);
        }

        private static JObject LoadVectorRoot()
        {
            return W24StrictJsonText.ParseObject(
                File.ReadAllText(
                    Path.Combine(
                        RepositoryRoot(),
                        "docs/protocol-vectors/desktop-phase2-worker-handle-lifecycle-v1.json"),
                    StrictUtf8),
                "Worker lifecycle golden vectors");
        }

        private static JObject LoadLocatorVectorRoot()
        {
            return W24StrictJsonText.ParseObject(
                File.ReadAllText(
                    Path.Combine(
                        RepositoryRoot(),
                        "src/VFXComposer.Protocol.Tests/GoldenVectors/desktop-phase2-worker-project-locator-v1.json"),
                    StrictUtf8),
                "Worker project locator golden vectors");
        }

        private static JObject ParseVector(byte[] bytes)
        {
            return W24StrictJsonText.ParseObject(StrictUtf8.GetString(bytes), "Worker lifecycle vector");
        }

        private static byte[] Encode(JObject value)
        {
            return StrictUtf8.GetBytes(value.ToString(Formatting.None));
        }

        private static void ResealForTests(JObject value, string typeTag)
        {
            // The production encoder remains private. Tests use the already verified golden grant
            // and replace only the lease token, then derive the exact expected digest by invoking
            // the acknowledgement-independent grant decoder through a temporary canonical clone.
            value["selfHash"] = new JObject
            {
                ["typeTag"] = typeTag,
                ["digest"] = "sha256:" + new string('0', 64)
            };
            var method = typeof(W24S6WorkerProtocolCodec).GetMethod(
                "Canonicalize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var compute = typeof(W24S6WorkerProtocolCodec).GetMethod(
                "ComputeTypedHash",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var payload = (byte[])method.Invoke(null, new object[] { value });
            var hash = (W24S6WorkerTypedHash)compute.Invoke(null, new object[] { typeTag, payload });
            value["selfHash"] = new JObject { ["typeTag"] = hash.TypeTag, ["digest"] = hash.Digest };
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        }

        private static string Hex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes) builder.Append(value.ToString("x2"));
            return builder.ToString();
        }
    }
}
