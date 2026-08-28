namespace VFXComposer.Protocol.Json;

public sealed record StrictJsonLimits
{
    public const int DefaultMaximumBytes = 1024 * 1024;
    public const int DefaultMaximumDepth = 32;
    public const int DefaultMaximumNodes = 100_000;

    public StrictJsonLimits(
        int maximumBytes = DefaultMaximumBytes,
        int maximumDepth = DefaultMaximumDepth,
        int maximumNodes = DefaultMaximumNodes)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        if (maximumDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        if (maximumNodes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumNodes));
        }

        MaximumBytes = maximumBytes;
        MaximumDepth = maximumDepth;
        MaximumNodes = maximumNodes;
    }

    public int MaximumBytes { get; }

    public int MaximumDepth { get; }

    public int MaximumNodes { get; }
}
