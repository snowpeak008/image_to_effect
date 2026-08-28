using System.Text.Json;

namespace VFXComposer.Protocol.Json;

/// <summary>Helpers for exact object shapes after strict parsing.</summary>
public static class ExactObjectValidator
{
    public static void Validate(
        JsonElement element,
        IEnumerable<string> requiredProperties,
        IEnumerable<string>? optionalProperties = null)
    {
        ArgumentNullException.ThrowIfNull(requiredProperties);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new StrictJsonException("EXPECTED_OBJECT", "Expected a JSON object.");
        }

        var required = new HashSet<string>(requiredProperties, StringComparer.Ordinal);
        var allowed = new HashSet<string>(required, StringComparer.Ordinal);
        if (optionalProperties is not null)
        {
            allowed.UnionWith(optionalProperties);
        }

        var observed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!observed.Add(property.Name))
            {
                throw new StrictJsonException("DUPLICATE_KEY", "An object contains a duplicate decoded property name.");
            }

            if (!allowed.Contains(property.Name))
            {
                throw new StrictJsonException("UNKNOWN_PROPERTY", "An object contains an unknown property.");
            }
        }

        required.ExceptWith(observed);
        if (required.Count != 0)
        {
            throw new StrictJsonException("MISSING_PROPERTY", "An object is missing a required property.");
        }
    }

    public static JsonElement RequireProperty(
        JsonElement element,
        string propertyName,
        JsonValueKind expectedKind)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new StrictJsonException("MISSING_PROPERTY", "An object is missing a required property.");
        }

        if (property.ValueKind != expectedKind)
        {
            throw new StrictJsonException("WRONG_TYPE", "A property has the wrong JSON type.");
        }

        return property;
    }

    public static string RequireString(JsonElement element, string propertyName) =>
        RequireProperty(element, propertyName, JsonValueKind.String).GetString()
        ?? throw new StrictJsonException("WRONG_TYPE", "A required string decoded to null.");
}
