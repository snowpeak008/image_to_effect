using VFXComposer.Protocol.Projects;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Tests;

[TestClass]
public sealed class WireSchemaRegistryTests
{
    [TestMethod]
    public void RegistryContainsThirtySixUniqueFrozenDtosAndSchemaIds()
    {
        Assert.AreEqual(36, WireSchemaRegistry.All.Count);
        Assert.AreEqual(
            WireSchemaRegistry.All.Count,
            WireSchemaRegistry.All.Select(value => value.SchemaId).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(
            WireSchemaRegistry.All.Count,
            WireSchemaRegistry.All.Select(value => value.DtoType).Distinct().Count());

        foreach (var descriptor in WireSchemaRegistry.All)
        {
            Assert.IsTrue(WireSchemaRegistry.TryGetById(descriptor.SchemaId, out var resolved));
            Assert.AreSame(descriptor, resolved);
            Assert.IsTrue(WireSchemaRegistry.TryGetByType(descriptor.DtoType, out var byType));
            Assert.AreSame(descriptor, byType);
            Assert.ThrowsExactly<NotSupportedException>(() =>
                ((ISet<string>)descriptor.RequiredTopLevelProperties).Add("unknown"));
        }

        Assert.IsFalse(WireSchemaRegistry.TryGetById(
            "https://schemas.vfxcomposer.dev/desktop/unknown.schema.json",
            out var unknown));
        Assert.IsNull(unknown);
    }

    [TestMethod]
    public void EveryRegistryEntryHasAnExactDraft202012DocumentWithMatchingId()
    {
        foreach (var descriptor in WireSchemaRegistry.All)
        {
            using var schema = ProtocolSchemaTestSupport.LoadSchema(descriptor.SchemaId);
            Assert.AreEqual(
                "https://json-schema.org/draft/2020-12/schema",
                schema.RootElement.GetProperty("$schema").GetString());
            Assert.AreEqual(descriptor.SchemaId, schema.RootElement.GetProperty("$id").GetString());
            Assert.AreEqual("object", schema.RootElement.GetProperty("type").GetString());
            Assert.AreEqual(false, schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        }
    }

    [TestMethod]
    public void RegisteredProjectSelectionDescriptorIsExactAndCorrelationOnly()
    {
        Assert.IsTrue(WireSchemaRegistry.TryGetByType(
            typeof(RegisteredProjectSelection),
            out var descriptor));
        Assert.IsNotNull(descriptor);
        Assert.AreEqual(WireSchemaIds.RegisteredProjectSelectionV1, descriptor.SchemaId);
        Assert.AreEqual(MessageKinds.RegisteredProjectSelection, descriptor.MessageKind);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "protocolVersion",
                "messageKind",
                "requestId",
                "registeredProjectId",
                "projectIdentity",
                "brokerGeneration",
                "registrationGeneration",
            },
            descriptor.RequiredTopLevelProperties.ToArray());
    }

    [TestMethod]
    public void WorkerProjectLocatorDescriptorsAreExactAndSeparateFromHandleLifecycle()
    {
        Assert.IsTrue(WireSchemaRegistry.TryGetByType(typeof(WorkerProjectLocator), out var locator));
        Assert.IsNotNull(locator);
        Assert.AreEqual(WireSchemaIds.WorkerProjectLocatorV1, locator.SchemaId);
        Assert.AreEqual(MessageKinds.WorkerProjectLocator, locator.MessageKind);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "protocolVersion",
                "messageKind",
                "requestId",
                "registeredProjectId",
                "projectIdentity",
                "volumeIdentity",
                "repositoryIdentity",
                "projectRootIdentity",
                "brokerGeneration",
                "registrationGeneration",
                "enrollmentGeneration",
                "workerSessionId",
                "workerProcessEpoch",
                "selfHash",
            },
            locator.RequiredTopLevelProperties.ToArray());

        Assert.IsTrue(WireSchemaRegistry.TryGetByType(
            typeof(WorkerProjectLocatorAcknowledgement),
            out var acknowledgement));
        Assert.IsNotNull(acknowledgement);
        Assert.AreEqual(WireSchemaIds.WorkerProjectLocatorAcknowledgementV1, acknowledgement.SchemaId);
        Assert.AreEqual(MessageKinds.WorkerProjectLocatorAcknowledgement, acknowledgement.MessageKind);
        Assert.AreNotEqual(MessageKinds.WorkerProjectHandleGrantAcknowledgement, acknowledgement.MessageKind);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "protocolVersion",
                "messageKind",
                "requestId",
                "registeredProjectId",
                "brokerGeneration",
                "registrationGeneration",
                "enrollmentGeneration",
                "workerSessionId",
                "workerProcessEpoch",
                "locatorSelfHash",
                "disposition",
                "selfHash",
            },
            acknowledgement.RequiredTopLevelProperties.ToArray());
    }
}
