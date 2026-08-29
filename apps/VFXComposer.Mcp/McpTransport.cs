using System.Text;

namespace VFXComposer.Mcp;

/// <summary>Why a read stopped.</summary>
public enum McpFrameStatus
{
    /// <summary>One complete frame was read.</summary>
    Message,

    /// <summary>The peer closed the stream; the session ends normally.</summary>
    EndOfStream,

    /// <summary>The frame exceeded the bound, so the stream can no longer be trusted.</summary>
    Oversized,
}

/// <summary>One read outcome; <see cref="Text"/> is only meaningful for <see cref="McpFrameStatus.Message"/>.</summary>
public readonly record struct McpFrame(McpFrameStatus Status, string Text);

/// <summary>
/// Reader for the MCP stdio transport framing: UTF-8 JSON messages delimited by newlines, one
/// message per line, with no embedded newline. Blank lines are skipped rather than reported as
/// malformed frames. The frame length is bounded; an oversized frame ends the session instead of
/// being truncated, because a truncated frame leaves the remainder of the line looking like a
/// fresh message and the stream can no longer be resynchronised safely.
/// </summary>
public sealed class McpFrameReader
{
    /// <summary>
    /// Bound on one frame. A manifest is at most 512 KiB and travels as a JSON string argument,
    /// so the bound leaves ample room for escaping while keeping the read allocation bounded.
    /// </summary>
    public const int DefaultMaximumFrameCharacters = 2 * 1024 * 1024;

    private readonly TextReader _reader;
    private readonly int _maximumFrameCharacters;

    public McpFrameReader(TextReader reader, int maximumFrameCharacters = DefaultMaximumFrameCharacters)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        if (maximumFrameCharacters < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFrameCharacters));
        }

        _maximumFrameCharacters = maximumFrameCharacters;
    }

    public override string ToString() => "McpFrameReader";

    public McpFrame Read()
    {
        var builder = new StringBuilder(256);
        while (true)
        {
            var next = _reader.Read();
            if (next < 0)
            {
                return IsBlank(builder)
                    ? new McpFrame(McpFrameStatus.EndOfStream, string.Empty)
                    : new McpFrame(McpFrameStatus.Message, builder.ToString());
            }

            var character = (char)next;
            if (character == '\n')
            {
                if (IsBlank(builder))
                {
                    builder.Clear();
                    continue;
                }

                return new McpFrame(McpFrameStatus.Message, builder.ToString());
            }

            if (character == '\r')
            {
                continue;
            }

            if (builder.Length >= _maximumFrameCharacters)
            {
                return new McpFrame(McpFrameStatus.Oversized, string.Empty);
            }

            builder.Append(character);
        }
    }

    private static bool IsBlank(StringBuilder builder)
    {
        for (var index = 0; index < builder.Length; index++)
        {
            if (!char.IsWhiteSpace(builder[index]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Writer for the same framing. Each frame is followed by a single line feed and flushed, so the
/// client sees one complete message per write regardless of platform line-ending conventions.
/// </summary>
public sealed class McpFrameWriter
{
    private readonly TextWriter _writer;

    public McpFrameWriter(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public override string ToString() => "McpFrameWriter";

    public void Write(string frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.Contains('\n', StringComparison.Ordinal) || frame.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("A frame must not contain a line break.", nameof(frame));
        }

        _writer.Write(frame);
        _writer.Write('\n');
        _writer.Flush();
    }
}
