using System.Buffers;
using System.Text;
using System.Text.Json;

namespace VFXComposer.Protocol.Json;

/// <summary>
/// Parses one strict UTF-8 JSON value with bounded resources and decoded-key uniqueness.
/// </summary>
public static class StrictJsonReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static JsonDocument Parse(
        ReadOnlySpan<byte> utf8Json,
        StrictJsonLimits? limits = null)
    {
        limits ??= new StrictJsonLimits();
        if (utf8Json.Length == 0)
        {
            throw new StrictJsonException("EMPTY", "JSON input is empty.");
        }

        if (utf8Json.Length > limits.MaximumBytes)
        {
            throw new StrictJsonException("MAX_BYTES", "JSON input exceeds the byte limit.");
        }

        if (utf8Json.Length >= 3 &&
            utf8Json[0] == 0xef &&
            utf8Json[1] == 0xbb &&
            utf8Json[2] == 0xbf)
        {
            throw new StrictJsonException("UTF8_BOM", "A UTF-8 byte-order mark is not permitted.");
        }

        try
        {
            _ = StrictUtf8.GetCharCount(utf8Json);
        }
        catch (DecoderFallbackException exception)
        {
            throw new StrictJsonException("INVALID_UTF8", "JSON input is not valid UTF-8.", exception);
        }

        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = limits.MaximumDepth,
        };

        var objectKeys = new Stack<HashSet<string>>();
        var nodeCount = 0;

        try
        {
            var reader = new Utf8JsonReader(utf8Json, readerOptions);
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        CountNode(ref nodeCount, limits.MaximumNodes);
                        objectKeys.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;

                    case JsonTokenType.EndObject:
                        if (objectKeys.Count == 0)
                        {
                            throw new StrictJsonException("UNBALANCED_OBJECT", "Object boundary is invalid.");
                        }

                        objectKeys.Pop();
                        break;

                    case JsonTokenType.PropertyName:
                    {
                        ValidateStringToken(ref reader);
                        var propertyName = reader.GetString()
                            ?? throw new StrictJsonException("NULL_PROPERTY", "A property name decoded to null.");
                        if (objectKeys.Count == 0 || !objectKeys.Peek().Add(propertyName))
                        {
                            throw new StrictJsonException(
                                "DUPLICATE_KEY",
                                "An object contains duplicate decoded property names.");
                        }

                        break;
                    }

                    case JsonTokenType.StartArray:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        CountNode(ref nodeCount, limits.MaximumNodes);
                        break;

                    case JsonTokenType.String:
                        CountNode(ref nodeCount, limits.MaximumNodes);
                        ValidateStringToken(ref reader);
                        break;

                    case JsonTokenType.Number:
                        CountNode(ref nodeCount, limits.MaximumNodes);
                        ValidateExactNumber(ref reader);
                        break;
                }
            }

            if (objectKeys.Count != 0)
            {
                throw new StrictJsonException("UNBALANCED_OBJECT", "Object boundary is invalid.");
            }
        }
        catch (StrictJsonException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new StrictJsonException("MALFORMED_JSON", "JSON syntax is invalid.", exception);
        }

        try
        {
            return JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = limits.MaximumDepth,
                });
        }
        catch (JsonException exception)
        {
            throw new StrictJsonException("MALFORMED_JSON", "JSON syntax is invalid.", exception);
        }
    }

    private static void CountNode(ref int nodeCount, int maximumNodes)
    {
        nodeCount++;
        if (nodeCount > maximumNodes)
        {
            throw new StrictJsonException("MAX_NODES", "JSON input exceeds the node limit.");
        }
    }

    private static void ValidateExactNumber(ref Utf8JsonReader reader)
    {
        ReadOnlySpan<byte> raw = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;
        _ = ExactDecimalNormalizer.Normalize(raw);
    }

    private static void ValidateStringToken(ref Utf8JsonReader reader)
    {
        ReadOnlySpan<byte> raw = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;
        ValidateEscapedSurrogates(raw);

        var decoded = reader.GetString();
        if (decoded is null)
        {
            return;
        }

        for (var index = 0; index < decoded.Length; index++)
        {
            var character = decoded[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= decoded.Length || !char.IsLowSurrogate(decoded[index + 1]))
                {
                    throw new StrictJsonException("ISOLATED_SURROGATE", "JSON string contains an isolated surrogate.");
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                throw new StrictJsonException("ISOLATED_SURROGATE", "JSON string contains an isolated surrogate.");
            }
        }
    }

    private static void ValidateEscapedSurrogates(ReadOnlySpan<byte> raw)
    {
        for (var index = 0; index < raw.Length; index++)
        {
            if (raw[index] != (byte)'\\')
            {
                continue;
            }

            index++;
            if (index >= raw.Length)
            {
                return;
            }

            if (raw[index] != (byte)'u')
            {
                continue;
            }

            if (!TryReadHex16(raw, index + 1, out var codeUnit))
            {
                return;
            }

            index += 4;
            if (codeUnit is >= 0xd800 and <= 0xdbff)
            {
                if (index + 6 >= raw.Length ||
                    raw[index + 1] != (byte)'\\' ||
                    raw[index + 2] != (byte)'u' ||
                    !TryReadHex16(raw, index + 3, out var lowSurrogate) ||
                    lowSurrogate is < 0xdc00 or > 0xdfff)
                {
                    throw new StrictJsonException("ISOLATED_SURROGATE", "JSON string contains an isolated surrogate.");
                }

                index += 6;
            }
            else if (codeUnit is >= 0xdc00 and <= 0xdfff)
            {
                throw new StrictJsonException("ISOLATED_SURROGATE", "JSON string contains an isolated surrogate.");
            }
        }
    }

    private static bool TryReadHex16(ReadOnlySpan<byte> raw, int start, out int value)
    {
        value = 0;
        if (start < 0 || start + 4 > raw.Length)
        {
            return false;
        }

        for (var index = start; index < start + 4; index++)
        {
            var digit = raw[index] switch
            {
                >= (byte)'0' and <= (byte)'9' => raw[index] - (byte)'0',
                >= (byte)'a' and <= (byte)'f' => raw[index] - (byte)'a' + 10,
                >= (byte)'A' and <= (byte)'F' => raw[index] - (byte)'A' + 10,
                _ => -1,
            };
            if (digit < 0)
            {
                return false;
            }

            value = (value << 4) | digit;
        }

        return true;
    }
}
