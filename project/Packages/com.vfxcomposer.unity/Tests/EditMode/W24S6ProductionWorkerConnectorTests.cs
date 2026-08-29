using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.Worker.Production;
using VFXComposer.Editor.W24.S6.Worker.Protocol;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S6ProductionWorkerConnectorTests
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        [Test]
        public void ConnectorConsumesFrozenC2BytesThroughTheOnlyC3Projection()
        {
            var locatorBytes = LoadLocatorVector("locator");
            var connector = new W24S6DedicatedWorkerConnector();
            var accepted = connector.AcceptHostOwnedLocator(locatorBytes);
            var directProjection = W24S6ProductionWorkerWireCodec.ProjectLocator(locatorBytes);

            Assert.That(connector.IsConnected, Is.True);
            Assert.That(accepted.Projection.RequestId, Is.EqualTo(directProjection.RequestId));
            Assert.That(accepted.Projection.RegisteredProjectId, Is.EqualTo(directProjection.RegisteredProjectId));
            Assert.That(accepted.Projection.ProjectIdentity.Digest, Is.EqualTo(directProjection.ProjectIdentity.Digest));
            Assert.That(accepted.Projection.BrokerGeneration, Is.EqualTo(directProjection.BrokerGeneration));
            Assert.That(accepted.Projection.RegistrationGeneration,
                Is.EqualTo(directProjection.RegistrationGeneration));
            Assert.That(accepted.Projection.EnrollmentGeneration,
                Is.EqualTo(directProjection.EnrollmentGeneration));
            Assert.That(accepted.Projection.WorkerSessionId, Is.EqualTo(directProjection.WorkerSessionId));
            Assert.That(accepted.Projection.WorkerProcessEpoch, Is.EqualTo(directProjection.WorkerProcessEpoch));
            Assert.That(accepted.Projection.SelfHash.Digest, Is.EqualTo(directProjection.SelfHash.Digest));
        }

        [Test]
        public void LocatorBindingIsSingleActiveAndDisconnectAllowsAFreshBinding()
        {
            var locatorBytes = LoadLocatorVector("locator");
            var connector = new W24S6DedicatedWorkerConnector();
            connector.AcceptHostOwnedLocator(locatorBytes);

            Assert.Throws<InvalidOperationException>(() => connector.AcceptHostOwnedLocator(locatorBytes));
            Assert.That(connector.IsConnected, Is.True);

            connector.Disconnect();
            connector.Disconnect();
            Assert.That(connector.IsConnected, Is.False);
            Assert.That(connector.AcceptHostOwnedLocator(locatorBytes), Is.Not.Null);
            Assert.That(connector.IsConnected, Is.True);
        }

        [Test]
        public void ConnectorPreservesC3StrictFailureWithoutFallbackOrStateChange()
        {
            var root = ParseLocator(LoadLocatorVector("locator"));
            root["projectPath"] = "C:/interactive-project";
            var connector = new W24S6DedicatedWorkerConnector();

            var exception = Assert.Throws<W24S6WorkerProtocolException>(() =>
                connector.AcceptHostOwnedLocator(Encode(root)));
            Assert.That(exception.Message, Is.EqualTo(W24S6WorkerProtocolException.MalformedMessage));
            Assert.That(exception.InnerException, Is.Null);
            Assert.That(connector.IsConnected, Is.False);
        }

        [Test]
        public void ProductionCompositionHasNoIndependentWireTransportProjectReadOrAuthoritySurface()
        {
            var productionTypes = typeof(W24S6DedicatedWorkerConnector).Assembly
                .GetTypes()
                .Where(type => type.Namespace == "VFXComposer.Editor.W24.S6.Worker.Production")
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            Assert.That(productionTypes.Select(type => type.Name), Is.EquivalentTo(new[]
            {
                "W24S6DedicatedWorkerConnector",
                "W24S6HostOwnedProjectLocator",
                "W24S6ProductionWorkerWireCodec"
            }));

            var methods = productionTypes.SelectMany(type => type.GetMethods(
                    BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method => !method.IsSpecialName)
                .ToArray();
            Assert.That(methods.Select(method => method.Name), Is.EquivalentTo(new[]
            {
                "AcceptHostOwnedLocator",
                "Disconnect",
                "ProjectLocator"
            }));

            var root = RepositoryRoot();
            foreach (var path in new[]
                     {
                         "project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6DedicatedWorkerConnector.cs",
                         "project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6HostOwnedProjectLocator.cs",
                         "project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Production/W24S6ProductionWorkerWireCodec.cs"
                     })
            {
                var source = File.ReadAllText(Path.Combine(root, path));
                foreach (var forbidden in new[]
                         {
                             "NamedPipe", "Socket", "Http", "Tcp", "Process.", "ProcessStartInfo",
                             "Windows Service", "ServiceHost", "LocalSystem", "SCM", "Privilege",
                             "UnityEngine", "UnityEditor", "AssetDatabase", "EditorPrefs", "Application.dataPath",
                             "File.Open", "File.Read", "File.Write", "Directory.", "SafeHandle", "IntPtr",
                             "handle.grant", "GRANT_ACCEPTED", "LOCATOR_ACCEPTED", "selfHash", "typeTag",
                             "Canonical", "SHA256", "JsonConvert", "JObject"
                         })
                    Assert.That(source, Does.Not.Contain(forbidden), path + " contains " + forbidden);
            }
        }

        private static byte[] LoadLocatorVector(string name)
        {
            var root = JObject.Parse(File.ReadAllText(
                Path.Combine(
                    RepositoryRoot(),
                    "src/VFXComposer.Protocol.Tests/GoldenVectors/desktop-phase2-worker-project-locator-v1.json"),
                StrictUtf8));
            var vector = ((JArray)root["vectors"])
                .OfType<JObject>()
                .Single(candidate => string.Equals((string)candidate["name"], name, StringComparison.Ordinal));
            return Convert.FromBase64String((string)vector["base64"]);
        }

        private static JObject ParseLocator(byte[] bytes)
        {
            return JObject.Parse(StrictUtf8.GetString(bytes));
        }

        private static byte[] Encode(JObject value)
        {
            return StrictUtf8.GetBytes(value.ToString(Newtonsoft.Json.Formatting.None));
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
        }
    }
}
