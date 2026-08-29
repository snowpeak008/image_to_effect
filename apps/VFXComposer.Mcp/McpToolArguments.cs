using System.Text;
using System.Text.Json;

namespace VFXComposer.Mcp;

/// <summary>
/// Hand-written binder for one tool's <c>arguments</c> object. Every accessor is bounded and the
/// member set is closed: an unknown field, a wrong type or an out-of-bounds value refuses the call
/// instead of being ignored. This is what keeps an authority, approval or skip-validation
/// argument from ever being accepted (REQ-002-12) — no such name is in any tool's field set, so
/// passing one is an unknown field.
/// </summary>
internal sealed class McpToolArguments
{
    /// <summary>Bound on an identifier argument, matching the store's token discipline.</summary>
    public const int MaximumIdentifierLength = 128;

    private readonly JsonElement _arguments;
    private readonly bool _present;

    private McpToolArguments(JsonElement arguments, bool present)
    {
        _arguments = arguments;
        _present = present;
    }

    /// <summary>
    /// Binds the arguments object against the tool's closed field set. Absent arguments bind to an
    /// empty object so that a missing required field, not a missing envelope member, is what the
    /// caller is told about.
    /// </summary>
    public static bool TryBind(
        JsonElement? arguments,
        IReadOnlySet<string> knownFields,
        out McpToolArguments bound)
    {
        ArgumentNullException.ThrowIfNull(knownFields);
        if (arguments is not JsonElement element)
        {
            bound = new McpToolArguments(default, present: false);
            return true;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            bound = new McpToolArguments(default, present: false);
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!knownFields.Contains(property.Name))
            {
                bound = new McpToolArguments(default, present: false);
                return false;
            }
        }

        bound = new McpToolArguments(element, present: true);
        return true;
    }

    /// <summary>Reads a required text argument bounded by its UTF-8 byte length.</summary>
    public bool TryReadText(string name, int maximumUtf8Bytes, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString() ?? string.Empty;
        if (text.Length == 0 || Encoding.UTF8.GetByteCount(text) > maximumUtf8Bytes)
        {
            return false;
        }

        value = text;
        return true;
    }

    /// <summary>
    /// Reads a required identifier argument. The alphabet and bound restate the store's token
    /// discipline, so an identifier the queue could never hold is refused before it reaches the
    /// queue at all.
    /// </summary>
    public bool TryReadIdentifier(string name, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString() ?? string.Empty;
        if (text.Length is 0 or > MaximumIdentifierLength || !IsToken(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    /// <summary>Reads an optional argument constrained to a closed vocabulary.</summary>
    public bool TryReadOptionalVocabulary(string name, IReadOnlySet<string> vocabulary, out string? value)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        value = null;
        if (!TryGetProperty(name, out var element))
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString() ?? string.Empty;
        if (!vocabulary.Contains(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    /// <summary>Reads a required object argument, bounded by its serialised UTF-8 byte length.</summary>
    public bool TryReadObject(string name, int maximumUtf8Bytes, out JsonElement value)
    {
        value = default;
        if (!TryGetProperty(name, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var raw = element.GetRawText();
        if (Encoding.UTF8.GetByteCount(raw) > maximumUtf8Bytes)
        {
            return false;
        }

        value = element;
        return true;
    }

    private bool TryGetProperty(string name, out JsonElement element)
    {
        element = default;
        return _present && _arguments.TryGetProperty(name, out element);
    }

    private static bool IsToken(string value)
    {
        foreach (var character in value)
        {
            if (character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not ':' and not '-')
            {
                return false;
            }
        }

        return true;
    }
}
