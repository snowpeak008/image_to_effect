using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// REQ-004 §10 guarantees over the artist-knowledge asset: the embedded fragment stays synchronized with its
/// human-edited source document (byte-hash pin), the translation table constructively covers every committed
/// catalog parameter, the guard's alias lexicon and the translation table are one asset, and everything that
/// reaches a prompt is English.
/// </summary>
[TestClass]
public sealed class RecipeRefineKnowledgeTests
{
    // ---- source-document synchronization (REQ-004-54) ----

    [TestMethod]
    public void TheFragmentPinsTheExactBytesOfTheSourceDocument()
    {
        // The source document is the single human-edited truth; the fragment carries the SHA-256 of its exact
        // bytes at export time. Editing the document without re-exporting the fragment turns this red. The
        // repository stores files byte-exact (.gitattributes: * -text), so the hash is stable across checkouts.
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Docs", "refine-artist-knowledge.md");
        var sourceHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();

        Assert.AreEqual(
            sourceHash,
            RecipeRefineKnowledge.Default.SourceSha256,
            "docs/ai-workflow/refine-artist-knowledge.md changed: re-export the fragment JSON (mirror content, " +
            "bump version, refresh exportedOn and sourceSha256).");
    }

    [TestMethod]
    public void TheFragmentCarriesTheExportHeader()
    {
        var knowledge = RecipeRefineKnowledge.Default;

        Assert.AreEqual(1, knowledge.Version);
        Assert.IsTrue(
            DateOnly.TryParseExact(knowledge.ExportedOn, "yyyy-MM-dd", out _),
            "exportedOn must be an ISO date.");
        Assert.AreEqual(64, knowledge.SourceSha256.Length);
    }

    // ---- constructive parameter coverage (REQ-004-52, §10.2 rule 3) ----

    [TestMethod]
    public void EveryCommittedCatalogParameterIsCoveredByATranslationEntryWithAliases()
    {
        var knowledge = RecipeRefineKnowledge.Default;
        var coveredPaths = knowledge.FeedbackTranslations
            .SelectMany(static translation => translation.ParameterPaths)
            .ToHashSet(StringComparer.Ordinal);

        var uncovered = new List<string>();
        foreach (var template in RecipeTemplateCatalogSnapshot.Default.Templates)
        {
            foreach (var parameter in template.Parameters)
            {
                var path = template.TemplateId + "." + parameter.Name;
                if (!coveredPaths.Contains(path))
                {
                    uncovered.Add(path);
                }
            }
        }

        Assert.AreEqual(
            0,
            uncovered.Count,
            "A catalog change left translation-table gaps: " + string.Join(", ", uncovered));
        Assert.IsTrue(knowledge.FeedbackTranslations.All(static translation =>
            translation.Aliases.Count > 0 && translation.AliasesZh.Count > 0));
    }

    [TestMethod]
    public void EveryTranslationEntryPointsAtACommittedParameterAndItsOwnPath()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        foreach (var translation in RecipeRefineKnowledge.Default.FeedbackTranslations)
        {
            Assert.IsTrue(
                snapshot.TryGetParameter(translation.TemplateId, translation.Parameter, out _),
                translation.TemplateId + "." + translation.Parameter + " is not a committed catalog parameter.");
            CollectionAssert.Contains(
                translation.ParameterPaths.ToList(),
                translation.TemplateId + "." + translation.Parameter,
                "A row's path family must contain its own templateId.parameter path.");
            foreach (var path in translation.ParameterPaths)
            {
                var separator = path.IndexOf('.', StringComparison.Ordinal);
                Assert.IsTrue(separator > 0, "Parameter paths are templateId.parameter: " + path);
                Assert.IsTrue(
                    snapshot.TryGetParameter(path[..separator], path[(separator + 1)..], out _),
                    "The path family entry " + path + " is not a committed catalog parameter.");
            }
        }
    }

    // ---- one asset for prompt and guard (REQ-004-56) ----

    [TestMethod]
    public void ThePromptTextAndTheGuardLexiconComeFromTheSameLoadedAsset()
    {
        // Both consumers read RecipeRefineKnowledge.Default; there is no second alias store to drift. The
        // prompt renders exactly the translation guidance rows, so a row cannot exist for the guard without
        // being taught to the model and vice versa.
        var knowledge = RecipeRefineKnowledge.Default;
        var promptText = knowledge.RenderPromptText();

        foreach (var translation in knowledge.FeedbackTranslations)
        {
            StringAssert.Contains(promptText, translation.TemplateId + "." + translation.Parameter);
            StringAssert.Contains(promptText, translation.Guidance);
        }

        foreach (var convention in knowledge.AestheticConventions)
        {
            StringAssert.Contains(promptText, convention);
        }

        foreach (var rule in knowledge.RefinementDiscipline)
        {
            StringAssert.Contains(promptText, rule);
        }
    }

    [TestMethod]
    public void ThePromptTextRendersTheCommittedBoundsSoTranslationActionsStayInsideThem()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var promptText = RecipeRefineKnowledge.Default.RenderPromptText();

        foreach (var translation in RecipeRefineKnowledge.Default.FeedbackTranslations)
        {
            Assert.IsTrue(snapshot.TryGetParameter(translation.TemplateId, translation.Parameter, out var parameter));
            StringAssert.Contains(
                promptText,
                translation.TemplateId + "." + translation.Parameter + " (" + parameter!.Type + ", " + parameter.RangeLiteral + ")",
                "Each guidance row renders its committed inclusive bounds.");
        }
    }

    // ---- prompt parts are pure English (REQ-004-53); the zh alias lexicon is local-only (O-3) ----

    [TestMethod]
    public void EverythingThatReachesThePromptIsFreeOfHanCharacters()
    {
        var knowledge = RecipeRefineKnowledge.Default;

        AssertNoHan(knowledge.RenderPromptText(), "rendered prompt text");
        foreach (var translation in knowledge.FeedbackTranslations)
        {
            AssertNoHan(translation.Guidance, translation.TemplateId + "." + translation.Parameter + " guidance");
            foreach (var alias in translation.Aliases)
            {
                AssertNoHan(alias, "English alias '" + alias + "'");
            }
        }

        foreach (var sentence in knowledge.AestheticConventions.Concat(knowledge.RefinementDiscipline))
        {
            AssertNoHan(sentence, "prompt sentence");
        }
    }

    [TestMethod]
    public void TheChineseAliasLexiconExistsButNeverEntersTheRenderedPrompt()
    {
        var knowledge = RecipeRefineKnowledge.Default;
        var promptText = knowledge.RenderPromptText();

        foreach (var translation in knowledge.FeedbackTranslations)
        {
            foreach (var alias in translation.AliasesZh)
            {
                Assert.IsTrue(
                    alias.Any(static character => IsHan(character)),
                    "A zh alias should actually be Chinese: '" + alias + "'");
                Assert.IsFalse(
                    promptText.Contains(alias, StringComparison.Ordinal),
                    "The zh alias '" + alias + "' is local matching data and must not reach the prompt.");
            }
        }
    }

    [TestMethod]
    public void TheRenderedPromptTextIsDeterministicAndFitsOneMessage()
    {
        var first = RecipeRefineKnowledge.Default.RenderPromptText();
        var second = RecipeRefineKnowledge.Default.RenderPromptText();

        Assert.AreEqual(first, second);
        Assert.IsTrue(
            first.Length <= RecipePromptAssembler.MaximumMessageCharacters,
            "The knowledge fragment must fit the per-message bound on its own; it is " + first.Length + " characters.");
    }

    private static void AssertNoHan(string text, string description)
    {
        foreach (var character in text)
        {
            Assert.IsFalse(IsHan(character), description + " contains the Han character '" + character + "'.");
        }
    }

    /// <summary>CJK Unified Ideographs and their common extensions; enough to catch any Chinese prose.</summary>
    private static bool IsHan(char character) =>
        (character >= '\u4E00' && character <= '\u9FFF') ||
        (character >= '\u3400' && character <= '\u4DBF') ||
        (character >= '\uF900' && character <= '\uFAFF');
}
