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
/// Reads a manifest-relative recipe reference so its content can be sealed into the queue payload at
/// submission time. It is separate from <see cref="IBatchRecipeProbe"/> because validation must stay
/// a pure function: only submission needs the bytes.
/// </summary>
public interface IBatchRecipeSource
{
    /// <summary>Returns the recipe text, or throws <see cref="InvalidDataException"/> if unreadable.</summary>
    string Read(string relativePath);
}

/// <summary>
/// Reads recipe references from the directory holding the manifest. The relative path has
/// already passed containment validation, and the resolved path is re-checked against the root
/// so a symlinked or normalised escape is still refused.
/// </summary>
public sealed class FileSystemBatchRecipeProbe : IBatchRecipeProbe, IBatchRecipeSource
{
    private const int MaximumRecipeBytes = 512 * 1024;

    private readonly string _manifestDirectory;

    public FileSystemBatchRecipeProbe(string manifestDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestDirectory);
        _manifestDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(manifestDirectory));
    }

    public override string ToString() => "FileSystemBatchRecipeProbe(<redacted>)";

    /// <summary>Reads a recipe through the same containment rule the probe applies.</summary>
    public string Read(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Probe(relativePath) != BatchRecipeProbeResult.JsonObject)
        {
            throw new InvalidDataException("The referenced recipe is missing or is not a JSON object.");
        }

        var resolved = Path.GetFullPath(Path.Combine(_manifestDirectory, relativePath));
        var root = _manifestDirectory + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The referenced recipe escaped the manifest directory.");
        }

        return File.ReadAllText(resolved);
    }

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
