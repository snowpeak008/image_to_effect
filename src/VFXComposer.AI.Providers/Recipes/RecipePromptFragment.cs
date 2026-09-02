namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// One named, independently versioned piece of prompt content. Fragments are the unit of message splitting:
/// the assembler never breaks inside a fragment, so a fragment must fit the per-message bound on its own.
/// Fragment content is prompt text and is never logged and never appears in diagnostics.
/// </summary>
internal sealed class RecipePromptFragment
{
    public RecipePromptFragment(string id, int version, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
        if (id.Length > 64 ||
            !id.All(static character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            throw new ArgumentException("Fragment id is invalid.", nameof(id));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content, nameof(content));
        if (content.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Fragment content is invalid.", nameof(content));
        }

        Id = id;
        Version = version;
        Content = content;
    }

    public string Id { get; }
    public int Version { get; }
    public string Content { get; }

    public override string ToString() => "RecipePromptFragment(" + Id + "/" + Version + ")";
}
