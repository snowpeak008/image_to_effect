using System.Text.Json;

namespace VFXComposer.Batch.Core;

/// <summary>Outcome of the semantic check on one referenced recipe file.</summary>
public enum BatchRecipeProbeResult
{
    JsonObject,
    Missing,
    NotJsonObject,
}

/// <summary>
/// Resolves a manifest-relative recipe reference. It is a seam so manifest validation stays a
/// pure function in tests and never needs a real directory layout.
/// </summary>
public interface IBatchRecipeProbe
{
    BatchRecipeProbeResult Probe(string relativePath);
}

/// <summary>
/// Reads recipe references from the directory holding the manifest. The relative path has
/// already passed containment validation, and the resolved path is re-checked against the root
/// so a symlinked or normalised escape is still refused.
/// </summary>
public sealed class FileSystemBatchRecipeProbe : IBatchRecipeProbe
{
    private const int MaximumRecipeBytes = 512 * 1024;

    private readonly string _manifestDirectory;

    public FileSystemBatchRecipeProbe(string manifestDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestDirectory);
        _manifestDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(manifestDirectory));
    }

    public override string ToString() => "FileSystemBatchRecipeProbe(<redacted>)";

    public BatchRecipeProbeResult Probe(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string resolved;
        try
        {
            resolved = Path.GetFullPath(Path.Combine(_manifestDirectory, relativePath));
        }
        catch (ArgumentException)
        {
            return BatchRecipeProbeResult.Missing;
        }
        catch (NotSupportedException)
        {
            return BatchRecipeProbeResult.Missing;
        }
        catch (PathTooLongException)
        {
            return BatchRecipeProbeResult.Missing;
        }

        var root = _manifestDirectory + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
        {
            return BatchRecipeProbeResult.Missing;
        }

        try
        {
            using var stream = new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumRecipeBytes)
            {
                return BatchRecipeProbeResult.NotJsonObject;
            }

            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? BatchRecipeProbeResult.JsonObject
                : BatchRecipeProbeResult.NotJsonObject;
        }
        catch (JsonException)
        {
            return BatchRecipeProbeResult.NotJsonObject;
        }
        catch (IOException)
        {
            return BatchRecipeProbeResult.Missing;
        }
        catch (UnauthorizedAccessException)
        {
            return BatchRecipeProbeResult.Missing;
        }
    }
}
