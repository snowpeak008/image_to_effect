using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace VFXComposer.Broker.Native;

/// <summary>Host-owned native namespace definition; never admitted from a wire message.</summary>
internal sealed record BrokerRegistrationDefinition
{
    private static readonly Regex VolumeGuidPattern = new(
        "^\\\\\\\\\\?\\\\Volume\\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\\}\\\\$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public BrokerRegistrationDefinition(
        string registeredProjectId,
        string volumeGuidPath,
        IEnumerable<string> repositorySegments,
        IEnumerable<string> projectSegments)
    {
        RegisteredProjectId = RequireToken(registeredProjectId, nameof(registeredProjectId));
        if (!VolumeGuidPattern.IsMatch(volumeGuidPath))
        {
            throw new ArgumentException("Volume identity must be an exact global volume GUID root.", nameof(volumeGuidPath));
        }

        VolumeGuidPath = volumeGuidPath;
        RepositorySegments = ValidateSegments(repositorySegments, nameof(repositorySegments));
        ProjectSegments = ValidateSegments(projectSegments, nameof(projectSegments));
        if (RepositorySegments.Count == 0 || ProjectSegments.Count == 0)
        {
            throw new ArgumentException("Repository and project roots must each add at least one segment.");
        }
    }

    public string RegisteredProjectId { get; }
    public string VolumeGuidPath { get; }
    public IReadOnlyList<string> RepositorySegments { get; }
    public IReadOnlyList<string> ProjectSegments { get; }

    private static IReadOnlyList<string> ValidateSegments(
        IEnumerable<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = values.ToArray();
        if (result.Length is 0 or > 64 || result.Any(value => !WindowsDirectoryHandle.IsSafeSegment(value)))
        {
            throw new ArgumentException("Native directory segments are invalid or unbounded.", parameterName);
        }

        return new ReadOnlyCollection<string>(result);
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
