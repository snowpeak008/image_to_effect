using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Registration;

internal sealed record RegisteredProjectIdentity(
    string RegisteredProjectId,
    TypedHash ProjectIdentity,
    TypedHash VolumeIdentity,
    TypedHash RepositoryIdentity,
    TypedHash ProjectRootIdentity,
    long RegistrationGeneration);
