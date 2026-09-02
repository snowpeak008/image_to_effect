using System.Collections.ObjectModel;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// An ordered fragment run spoken by one role. A section may produce several messages after splitting,
/// but two sections never share a message, so role boundaries always survive assembly.
/// </summary>
internal sealed class RecipePromptSection
{
    public RecipePromptSection(ChatRole role, IEnumerable<RecipePromptFragment> fragments)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        ArgumentNullException.ThrowIfNull(fragments);
        var copied = fragments.ToArray();
        if (copied.Length == 0 || copied.Any(static fragment => fragment is null))
        {
            throw new ArgumentException("Section fragments are invalid.", nameof(fragments));
        }

        Role = role;
        Fragments = new ReadOnlyCollection<RecipePromptFragment>(copied);
    }

    public ChatRole Role { get; }
    public IReadOnlyList<RecipePromptFragment> Fragments { get; }

    public override string ToString() => "RecipePromptSection(" + Role + "," + Fragments.Count + ")";
}
