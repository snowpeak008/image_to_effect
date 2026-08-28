using System.Collections.Frozen;

namespace VFXComposer.Protocol.Queries;

public static class DocumentKinds
{
    public const string LibraryIndex = "LIBRARY_INDEX";
    public const string Manifest = "MANIFEST";
    public const string Contract = "CONTRACT";
    public const string Trace = "TRACE";

    private static readonly FrozenSet<string> KnownKinds =
        new[] { LibraryIndex, Manifest, Contract, Trace }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => KnownKinds;

    internal static string Require(string value, string parameterName) =>
        KnownKinds.Contains(value)
            ? value
            : throw new ArgumentOutOfRangeException(parameterName);

    internal static string RequireDocumentId(
        string documentKind,
        string value,
        string parameterName)
    {
        Require(documentKind, nameof(documentKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (string.Equals(documentKind, LibraryIndex, StringComparison.Ordinal))
        {
            return string.Equals(value, "project", StringComparison.Ordinal)
                ? value
                : throw new ArgumentException(
                    "The library index uses the fixed project document identity.",
                    parameterName);
        }

        if (value.Length > 96 || value[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("Document identity has an invalid shape.", parameterName);
        }

        foreach (var character in value)
        {
            if (character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '_' and not '-')
            {
                throw new ArgumentException("Document identity has an invalid shape.", parameterName);
            }
        }

        return value;
    }
}
