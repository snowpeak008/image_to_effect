using System.Collections.Frozen;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;
using VFXComposer.Broker.Native;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// Host-owned admission policy. Project JSON, caller paths, environment variables,
/// and EditorPrefs are intentionally absent from this type and from production loading.
/// </summary>
internal sealed class BrokerPolicy
{
    public const string UserSidIdentityType = "vfxcomposer.windows-user-sid/1";

    private BrokerPolicy(
        string pipeName,
        string brokerInstanceId,
        long brokerGeneration,
        TypedHash userSidIdentity,
        IReadOnlyDictionary<string, IReadOnlySet<TypedHash>> allowedImagesByRole,
        IEnumerable<BrokerRegistrationDefinition> registrationDefinitions)
    {
        if (brokerGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        PipeName = RequireToken(pipeName, nameof(pipeName));
        BrokerInstanceId = RequireToken(brokerInstanceId, nameof(brokerInstanceId));
        BrokerGeneration = brokerGeneration;
        UserSidIdentity = RequireType(userSidIdentity, UserSidIdentityType, nameof(userSidIdentity));
        AllowedImagesByRole = allowedImagesByRole.ToFrozenDictionary(
            pair => PeerRoles.All.Contains(pair.Key)
                ? pair.Key
                : throw new ArgumentOutOfRangeException(nameof(allowedImagesByRole)),
            pair => pair.Value.ToFrozenSet(),
            StringComparer.Ordinal);
        if (AllowedImagesByRole.Count != PeerRoles.All.Count ||
            AllowedImagesByRole.Any(pair => pair.Value.Count == 0 ||
                pair.Value.Any(value => !string.Equals(
                    value.TypeTag,
                    Protocol.Ipc.PeerHello.ProcessImageIdentityType,
                    StringComparison.Ordinal))))
        {
            throw new ArgumentException("Every peer role needs at least one exact image identity.", nameof(allowedImagesByRole));
        }

        RegistrationDefinitions = registrationDefinitions.ToFrozenDictionary(
            value => value.RegisteredProjectId,
            StringComparer.Ordinal);
    }

    public string PipeName { get; }

    public string BrokerInstanceId { get; }

    public long BrokerGeneration { get; }

    public TypedHash UserSidIdentity { get; }

    public FrozenDictionary<string, FrozenSet<TypedHash>> AllowedImagesByRole { get; }

    public FrozenDictionary<string, BrokerRegistrationDefinition> RegistrationDefinitions { get; }

    public static bool TryLoadProduction(out BrokerPolicy? policy)
    {
        // ProductionTrustProfile models only the immutable SID/image/ACL shape.
        // No independently privileged service/bootstrap issuer exists in this
        // repository, so this branch must stay closed before any pipe, request,
        // path, registration, or project-content operation.
        policy = null;
        return false;
    }

    internal bool AllowsImage(string role, TypedHash imageIdentity) =>
        AllowedImagesByRole.TryGetValue(role, out var allowed) &&
        allowed.Any(value => value.FixedTimeEquals(imageIdentity));

    private static string RequireToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 128 || value.Any(character =>
                character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not ':' and not '-'))
        {
            throw new ArgumentException("Token has an invalid shape.", parameterName);
        }

        return value;
    }

    private static TypedHash RequireType(TypedHash value, string typeTag, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return string.Equals(value.TypeTag, typeTag, StringComparison.Ordinal)
            ? value
            : throw new ArgumentException("Typed identity uses the wrong domain.", parameterName);
    }
}
