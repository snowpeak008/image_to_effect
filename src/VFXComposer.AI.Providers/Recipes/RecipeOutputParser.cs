using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Extracts exactly one JSON object from AI output text. Markdown code fences and prose before or after the
/// object are tolerated; anything else is an L1 failure. The output is only ever parsed as data (REQ-001-08).
/// </summary>
internal static class RecipeOutputParser
{
    public const string InvalidJsonCode = "E104";

    public static bool TryExtractJson(
        string text,
        [NotNullWhen(true)] out string? json,
        [NotNullWhen(false)] out RecipeValidationIssue? issue)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (TryExtractFencedBlock(text, out var fenced) && TryParseObject(fenced, out json))
        {
            issue = null;
            return true;
        }

        if (TryExtractBalancedObject(text, out var balanced) && TryParseObject(balanced, out json))
        {
            issue = null;
            return true;
        }

        json = null;
        issue = new RecipeValidationIssue(
            InvalidJsonCode,
            RecipeValidationSeverity.Error,
            "/",
            "Invalid JSON: the output does not contain exactly one parseable JSON object.");
        return false;
    }

    private static bool TryParseObject(string candidate, [NotNullWhen(true)] out string? json)
    {
        json = null;
        try
        {
            using var document = JsonDocument.Parse(candidate);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            json = candidate;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryExtractFencedBlock(string text, [NotNullWhen(true)] out string? content)
    {
        content = null;
        var open = text.IndexOf("```", StringComparison.Ordinal);
        if (open < 0)
        {
            return false;
        }

        // Skip the info string ("json", "JSON", ...) on the opening fence line.
        var bodyStart = text.IndexOf('\n', open);
        if (bodyStart < 0)
        {
            return false;
        }

        var close = text.IndexOf("```", bodyStart + 1, StringComparison.Ordinal);
        if (close < 0)
        {
            return false;
        }

        content = text[(bodyStart + 1)..close].Trim();
        return content.Length > 0;
    }

    private static bool TryExtractBalancedObject(string text, [NotNullWhen(true)] out string? content)
    {
        content = null;
        var start = text.IndexOf('{');
        if (start < 0)
        {
            return false;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var index = start; index < text.Length; index++)
        {
            var character = text[index];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        content = text[start..(index + 1)];
                        return true;
                    }

                    break;
            }
        }

        return false;
    }
}
