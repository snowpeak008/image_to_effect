using System.Collections.Frozen;
using System.Globalization;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// Immutable host-owned requirements for a future Windows production profile.
/// Constructing this data validates nothing beyond policy shape and never issues a
/// registration, session, listener, or project capability.
/// </summary>
internal sealed class ProductionTrustProfile
{
    private readonly FrozenDictionary<string, FrozenSet<TypedHash>> _approvedImagesByRole;

    internal ProductionTrustProfile(
        string pipeName,
        string brokerInstanceId,
        long brokerGeneration,
        WindowsSid serviceSid,
        WindowsSid userSid,
        IReadOnlyDictionary<string, IReadOnlySet<TypedHash>> approvedImagesByRole)
    {
        PipeName = RequireToken(pipeName, nameof(pipeName));
        BrokerInstanceId = RequireToken(brokerInstanceId, nameof(brokerInstanceId));
        if (brokerGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        ArgumentNullException.ThrowIfNull(serviceSid);
        ArgumentNullException.ThrowIfNull(userSid);
        if (serviceSid.PrincipalKind != WindowsSidPrincipalKind.Service ||
            userSid.PrincipalKind != WindowsSidPrincipalKind.User ||
            serviceSid.FixedEquals(userSid))
        {
            throw new ArgumentException("Production profile requires distinct exact service and user SIDs.");
        }

        ArgumentNullException.ThrowIfNull(approvedImagesByRole);
        _approvedImagesByRole = approvedImagesByRole.ToFrozenDictionary(
            pair => PeerRoles.All.Contains(pair.Key)
                ? pair.Key
                : throw new ArgumentException("Production profile has an unknown peer role.", nameof(approvedImagesByRole)),
            pair => FreezeAndValidateImages(pair.Value, nameof(approvedImagesByRole)),
            StringComparer.Ordinal);
        if (_approvedImagesByRole.Count != PeerRoles.All.Count ||
            PeerRoles.All.Any(role => !_approvedImagesByRole.ContainsKey(role)))
        {
            throw new ArgumentException("Production profile must bind every exact peer role.", nameof(approvedImagesByRole));
        }

        BrokerGeneration = brokerGeneration;
        ServiceSid = serviceSid;
        UserSid = userSid;
        PipeAcl = CanonicalNamedPipeAcl.Create(serviceSid, userSid);
    }

    public string PipeName { get; }

    public string BrokerInstanceId { get; }

    public long BrokerGeneration { get; }

    public WindowsSid ServiceSid { get; }

    public WindowsSid UserSid { get; }

    public CanonicalNamedPipeAcl PipeAcl { get; }

    internal bool MatchesExactPeerBinding(
        string? role,
        WindowsSid? peerUserSid,
        TypedHash? imageIdentity,
        int processId,
        string? processEpoch,
        string? sessionId,
        long brokerGeneration)
    {
        if (role is null || peerUserSid is null || imageIdentity is null ||
            processId <= 0 || brokerGeneration != BrokerGeneration ||
            !PeerRoles.All.Contains(role) ||
            peerUserSid.PrincipalKind != WindowsSidPrincipalKind.User ||
            !UserSid.FixedEquals(peerUserSid) ||
            !ProcessEpoch.IsCanonicalForProcess(processId, processEpoch) ||
            !IsCanonicalSessionId(sessionId))
        {
            return false;
        }

        return _approvedImagesByRole.TryGetValue(role, out var approvedImages) &&
            approvedImages.Any(candidate => candidate.FixedTimeEquals(imageIdentity));
    }

    private static FrozenSet<TypedHash> FreezeAndValidateImages(
        IReadOnlySet<TypedHash>? images,
        string parameterName)
    {
        if (images is null || images.Count == 0 ||
            images.Any(image => image is null || !string.Equals(
                image.TypeTag,
                PeerHello.ProcessImageIdentityType,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException("Every production peer role needs exact image identities.", parameterName);
        }

        return images.ToFrozenSet();
    }

    private bool IsCanonicalSessionId(string? sessionId)
    {
        if (sessionId is null)
        {
            return false;
        }

        var prefix = string.Concat(
            "session-",
            BrokerGeneration.ToString(CultureInfo.InvariantCulture),
            "-");
        if (!sessionId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var ordinalText = sessionId[prefix.Length..];
        return long.TryParse(
                   ordinalText,
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out var ordinal) &&
               ordinal > 0 &&
               string.Equals(
                   ordinalText,
                   ordinal.ToString(CultureInfo.InvariantCulture),
                   StringComparison.Ordinal);
    }

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
}
