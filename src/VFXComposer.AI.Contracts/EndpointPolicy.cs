namespace VFXComposer.AI.Contracts;

/// <summary>
/// The single semantic authority for provider service-root endpoints. It accepts only an explicit,
/// bounded URI input and returns an immutable canonical representation suitable for configuration wire data.
/// </summary>
public static class EndpointPolicy
{
    /// <summary>
    /// Canonical, minified JSON Schema for the <c>endpoint</c> object. The checked-in configuration schema embeds
    /// these exact bytes as its endpoint fragment; profile-level conditionals add the auth-scope relationship for HTTP.
    /// </summary>
    public static string SchemaProjection { get; } = """{"type":"object","additionalProperties":false,"required":["uri","allowLoopbackHttp"],"properties":{"uri":{"type":"string","minLength":9,"maxLength":2048,"pattern":"^(?:[Hh][Tt][Tt][Pp][Ss]?://)(?:[A-Za-z0-9.-]+|\\[[0-9A-Fa-f:.]+\\])(?::[0-9]{1,5})?(?:/[A-Za-z0-9._~!$&'()*+,;=:@%/-]*)?$"},"allowLoopbackHttp":{"type":"boolean"}}}""";

    /// <summary>Creates a canonical endpoint or throws when the supplied endpoint is outside this policy.</summary>
    public static EndpointDefinition Create(string uriText, bool allowLoopbackHttp, SecretScope secretScope)
    {
        if (!TryCreate(uriText, allowLoopbackHttp, secretScope, out var endpoint))
        {
            throw new ArgumentException("Endpoint URI is not allowed by the endpoint policy.", nameof(uriText));
        }

        return endpoint!;
    }

    /// <summary>Creates a canonical endpoint from the URI's original wire text.</summary>
    public static EndpointDefinition Create(Uri uri, bool allowLoopbackHttp, SecretScope secretScope)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return Create(uri.OriginalString, allowLoopbackHttp, secretScope);
    }

    /// <summary>Attempts to create a canonical endpoint without exposing partial state.</summary>
    public static bool TryCreate(
        string? uriText,
        bool allowLoopbackHttp,
        SecretScope secretScope,
        out EndpointDefinition? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrEmpty(uriText) ||
            uriText.Length > EndpointDefinition.MaximumUriLength ||
            !Enum.IsDefined(secretScope) ||
            !IsVisibleAscii(uriText) ||
            !Uri.TryCreate(uriText, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!TryCanonicalize(parsed, allowLoopbackHttp, secretScope, out var canonicalUri, out var canonicalWireUri))
        {
            return false;
        }

        endpoint = new EndpointDefinition(canonicalUri!, canonicalWireUri!, allowLoopbackHttp);
        return true;
    }

    /// <summary>Attempts to create a canonical endpoint from a URI while preserving its original wire text rules.</summary>
    public static bool TryCreate(
        Uri? uri,
        bool allowLoopbackHttp,
        SecretScope secretScope,
        out EndpointDefinition? endpoint)
    {
        endpoint = null;
        return uri is not null && TryCreate(uri.OriginalString, allowLoopbackHttp, secretScope, out endpoint);
    }

    /// <summary>
    /// Confirms that a trusted endpoint still represents the exact canonical result for the declared secret scope.
    /// This is the policy call used by every configuration consumer after deserialization.
    /// </summary>
    public static bool IsValid(EndpointDefinition? endpoint, SecretScope secretScope)
    {
        if (endpoint is null ||
            !TryCreate(endpoint.CanonicalWireUri, endpoint.AllowLoopbackHttp, secretScope, out var canonical))
        {
            return false;
        }

        return string.Equals(
                canonical!.CanonicalWireUri,
                endpoint.CanonicalWireUri,
                StringComparison.Ordinal) &&
            canonical.AllowLoopbackHttp == endpoint.AllowLoopbackHttp;
    }

    /// <summary>
    /// HTTP is intentionally narrower than address-family loopback: only the three explicit service-root spellings
    /// are trusted, never DNS aliases or a broad 127/8 interpretation.
    /// </summary>
    public static bool IsExplicitLoopbackHost(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        var normalized = host[0] == '[' && host[^1] == ']'
            ? host[1..^1]
            : host;
        return string.Equals(normalized, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "127.0.0.1", StringComparison.Ordinal) ||
            string.Equals(normalized, "::1", StringComparison.Ordinal);
    }

    private static bool TryCanonicalize(
        Uri parsed,
        bool allowLoopbackHttp,
        SecretScope secretScope,
        out Uri? canonicalUri,
        out string? canonicalWireUri)
    {
        canonicalUri = null;
        canonicalWireUri = null;
        if (!parsed.IsAbsoluteUri ||
            string.IsNullOrEmpty(parsed.Host) ||
            parsed.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        var isHttps = string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
        var isHttp = string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal);
        if (!isHttps && !isHttp)
        {
            return false;
        }

        if (isHttps)
        {
            if (allowLoopbackHttp)
            {
                return false;
            }
        }
        else if (!allowLoopbackHttp ||
            secretScope != SecretScope.DevelopmentOnly ||
            !IsExplicitLoopbackHost(parsed.DnsSafeHost))
        {
            return false;
        }

        var absoluteUri = parsed.AbsoluteUri;
        if (absoluteUri.Length > EndpointDefinition.MaximumUriLength ||
            !IsVisibleAscii(absoluteUri) ||
            !Uri.TryCreate(absoluteUri, UriKind.Absolute, out var canonical) ||
            !canonical.IsAbsoluteUri)
        {
            return false;
        }

        canonicalUri = canonical;
        canonicalWireUri = absoluteUri;
        return true;
    }

    private static bool IsVisibleAscii(string value)
    {
        foreach (var character in value)
        {
            if (character is < '!' or > '~')
            {
                return false;
            }
        }

        return true;
    }
}
