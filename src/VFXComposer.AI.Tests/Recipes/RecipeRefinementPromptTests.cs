using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// F8b4 refinement assembly coverage (REQ-004-13/-22, AC-18): the anchored-triple context and nothing else, the
/// refine-knowledge fragment riding the system section inside the per-message bound (size pinned), byte-exact
/// determinism, the repair shape, and the split behavior for an oversized head recipe.
/// </summary>
[TestClass]
public sealed class RecipeRefinementPromptTests
{
    private const string FixedDescription = "a short blue spark bolt";
    private const string FixedFeedback = "make the fire core bigger";
    private const string SecondRoundFeedback = "now shorten the trail";

    /// <summary>The pinned size and hash of the refinement System message (system prompt + knowledge fragment).</summary>
    private const int RefinementSystemMessageCharacters = 8903;

    private const string RefinementSystemMessagePin = "a4bc5518dcdeb53470f87e0609681b7ce2718d3cdecb6f2e6679e8f6ce08bf0b";

    private static string HeadRecipeJson => RecipeCanonicalJson.Canonicalize(RecipePromptAssembler.ReferenceRecipeJson);

    [TestMethod]
    public void TheRefinementSystemMessageCarriesTheKnowledgeFragmentInsideTheMessageBoundAtThePinnedSize()
    {
        var messages = RecipePromptAssembler.CreateRefinementMessages(FixedDescription, HeadRecipeJson, FixedFeedback);

        Assert.AreEqual(ChatRole.System, messages[0].Role);
        StringAssert.StartsWith(messages[0].Content, RecipePromptAssembler.SystemPrompt);
        StringAssert.Contains(messages[0].Content, "Refinement knowledge for the current template catalog:");
        Assert.IsTrue(messages[0].Content.Length <= RecipePromptAssembler.MaximumMessageCharacters);
        Assert.AreEqual(
            "System|" + RefinementSystemMessageCharacters + "|" + RefinementSystemMessagePin,
            Pin(messages[0]),
            "The knowledge fragment changed size or content: re-pin after a deliberate knowledge re-export.");
    }

    [TestMethod]
    public void TheContextIsExactlyTheAnchoredTriple()
    {
        var messages = RecipePromptAssembler.CreateRefinementMessages(FixedDescription, HeadRecipeJson, FixedFeedback);

        Assert.AreEqual(2, messages.Count, "One System message plus one User message carrying the triple.");
        Assert.AreEqual(ChatRole.User, messages[1].Role);
        StringAssert.Contains(messages[1].Content, FixedDescription);
        StringAssert.Contains(messages[1].Content, HeadRecipeJson);
        StringAssert.Contains(messages[1].Content, FixedFeedback);
    }

    [TestMethod]
    public void ASecondRoundNeverCarriesTheFirstRoundsFeedback()
    {
        // AC-18 / REQ-004-13: rounds are independent; the only feedback in a request is this round's.
        var secondRound = RecipePromptAssembler.CreateRefinementMessages(
            FixedDescription,
            HeadRecipeJson,
            SecondRoundFeedback);

        Assert.IsFalse(secondRound.Any(static message =>
            message.Content.Contains(FixedFeedback, StringComparison.Ordinal)));
        Assert.IsTrue(secondRound.Any(message =>
            message.Content.Contains(SecondRoundFeedback, StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RefinementAssemblyIsDeterministicByteForByte()
    {
        var first = RecipePromptAssembler.CreateRefinementMessages(FixedDescription, HeadRecipeJson, FixedFeedback);
        var second = RecipePromptAssembler.CreateRefinementMessages(FixedDescription, HeadRecipeJson, FixedFeedback);

        CollectionAssert.AreEqual(
            first.Select(static message => message.Role + "|" + message.Content).ToArray(),
            second.Select(static message => message.Role + "|" + message.Content).ToArray());
    }

    [TestMethod]
    public void TheCompositeVersionRegistersTheRefinementFragments()
    {
        StringAssert.Contains(RecipePromptAssembler.Version, ";refine-knowledge/1");
        StringAssert.Contains(RecipePromptAssembler.Version, ";refine-request/1");
        Assert.AreEqual(1, RecipeRefineKnowledge.Default.Version, "The registry mirrors the knowledge asset version.");
    }

    [TestMethod]
    public void ARefinementRepairEchoesThePreviousOutputAndListsTheIssues()
    {
        var issues = new[]
        {
            new RecipeValidationIssue(
                "E123",
                RecipeValidationSeverity.Error,
                "$.stages[1].modules[0].parameters.scale",
                "Value is out of range.",
                "9.5",
                "[0.6, 2.4]"),
        };
        var previousOutput = "{\"recipeVersion\":1,\"revision\":1}";

        var messages = RecipePromptAssembler.CreateRefinementRepairMessages(
            FixedDescription,
            HeadRecipeJson,
            FixedFeedback,
            previousOutput,
            issues);

        Assert.AreEqual(4, messages.Count);
        Assert.AreEqual(ChatRole.System, messages[0].Role);
        Assert.AreEqual(ChatRole.User, messages[1].Role);
        Assert.AreEqual(ChatRole.Assistant, messages[2].Role);
        Assert.AreEqual(previousOutput, messages[2].Content);
        Assert.AreEqual(ChatRole.User, messages[3].Role);
        StringAssert.Contains(messages[3].Content, "E123");
        StringAssert.Contains(messages[3].Content, "failed VFX Composer Recipe v1 validation");
        StringAssert.Contains(messages[1].Content, FixedFeedback);
    }

    [TestMethod]
    public void AnOversizedHeadRecipeSplitsIntoSameRoleMessagesWithoutLosingABit()
    {
        // The contract admits drafts up to 128 KiB characters while a message holds 16 KiB, so the head recipe
        // is the one piece the refinement request must be able to split rather than refuse.
        var oversized = "{\"pad\":\"" + new string('x', 40 * 1024) + "\"}";

        var messages = RecipePromptAssembler.CreateRefinementMessages(FixedDescription, oversized, FixedFeedback);

        Assert.IsTrue(messages.Count > 2);
        Assert.AreEqual(ChatRole.System, messages[0].Role);
        Assert.IsTrue(messages.Skip(1).All(static message => message.Role == ChatRole.User));
        Assert.IsTrue(messages.All(static message =>
            message.Content.Length <= RecipePromptAssembler.MaximumMessageCharacters));
        StringAssert.Contains(string.Concat(messages.Skip(1).Select(static message => message.Content)), oversized);
    }

    [TestMethod]
    public void AMissingTriplePieceIsRejectedBeforeAnyAssembly()
    {
        Assert.ThrowsExactly<ArgumentException>(static () =>
            RecipePromptAssembler.CreateRefinementMessages(" ", "{}", FixedFeedback));
        Assert.ThrowsExactly<ArgumentException>(static () =>
            RecipePromptAssembler.CreateRefinementMessages(FixedDescription, " ", FixedFeedback));
        Assert.ThrowsExactly<ArgumentException>(static () =>
            RecipePromptAssembler.CreateRefinementMessages(FixedDescription, "{}", " "));
    }

    private static string Pin(ChatChannelMessage message) =>
        message.Role + "|" + message.Content.Length + "|" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Content))).ToLowerInvariant();
}
