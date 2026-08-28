using System.Reflection;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.Native;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

internal static class BrokerTestFactory
{
    internal static BrokerPolicy CreatePolicy(
        string pipeName,
        string brokerInstanceId,
        long brokerGeneration,
        TypedHash userSidIdentity,
        TypedHash desktopImageIdentity,
        TypedHash workerImageIdentity,
        IEnumerable<BrokerRegistrationDefinition>? registrations = null)
    {
        var constructor = typeof(BrokerPolicy).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (BrokerPolicy)constructor.Invoke(
            [
                pipeName,
                brokerInstanceId,
                brokerGeneration,
                userSidIdentity,
                new Dictionary<string, IReadOnlySet<TypedHash>>(StringComparer.Ordinal)
                {
                    [PeerRoles.Desktop] = new HashSet<TypedHash> { desktopImageIdentity },
                    [PeerRoles.Worker] = new HashSet<TypedHash> { workerImageIdentity },
                },
                registrations ?? Array.Empty<BrokerRegistrationDefinition>(),
            ]);
    }
}
