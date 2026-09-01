using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// F8b1 assembler coverage: the byte-exact assembly snapshot, the fragment-boundary message splitting that
/// production inputs never reach, the fail-closed size bounds, and the composite version string. The snapshot
/// pins below were captured from the pre-refactor <c>RecipePromptTemplate</c> on the same catalog snapshot, so a
/// green run proves the refactor changed no produced byte.
/// </summary>
[TestClass]
public sealed class RecipePromptAssemblerTests
{
    private const string FixedDescription = "a short blue spark bolt";

    private const string FixedPreviousOutput = "{\"recipeVersion\":1,\"revision\":1}";

    /// <summary>SHA-256 pins of every message the legacy template produced for the fixed inputs.</summary>
    private const string SystemPromptPin = "7a446a865be00a37eabc0c2695cb1c393adaa242d1f1af95bbb4be8172c7a123";

    private const string RequestMessagePin = "3fa72e6a9829ddf410fc3f05645ca64549583b538a52fa2fc0c58c114e19e0ca";
    private const string PreviousOutputMessagePin = "8db441181876277374a21bf7ecfdf54e3f8645fb20b34e43f30a2503d39b4683";
    private const string RepairMessagePin = "0be7bcb3e6811eb4a0a034cc5e3922b3ecbf174f5c82c5e837d85cbde3ae71e0";
    private const string TruncatedRepairMessagePin = "8dd6f2e4b32e14202c4c8235c7dc5dc1fea0c19cb9e734459efc7b5ca663fa95";

    // ---- assembly snapshot (byte-exact against the pre-refactor output) ----

    [TestMethod]
    public void TheInitialMessageSequenceMatchesThePreRefactorSnapshotByteForByte()
    {
        var messages = RecipePromptAssembler.CreateInitialMessages(FixedDescription);

        CollectionAssert.AreEqual(
            new[]
            {
                "System|4607|" + SystemPromptPin,
                "User|91|" + RequestMessagePin,
            },
            messages.Select(Pin).ToArray());
    }

    [TestMethod]
    public void TheRepairMessageSequenceMatchesThePreRefactorSnapshotByteForByte()
    {
        var messages = RecipePromptAssembler.CreateRepairMessages(
            FixedDescription,
            FixedPreviousOutput,
            FixedIssues());

        CollectionAssert.AreEqual(
            new[]
            {
                "System|4607|" + SystemPromptPin,
                "User|91|" + RequestMessagePin,
                "Assistant|32|" + PreviousOutputMessagePin,
                "User|436|" + RepairMessagePin,
            },
            messages.Select(Pin).ToArray());
    }

    [TestMethod]
    public void AnOversizedPreviousOutputIsDroppedAndTheIssueListIsTruncatedExactlyAsBefore()
    {
        var manyIssues = Enumerable.Range(0, 70)
            .Select(static index => new RecipeValidationIssue(
                "E200",
                RecipeValidationSeverity.Error,
                "$.stages[0].modules[" + index + "].id",
                "Synthetic issue " + index + "."))
            .ToArray();

        var messages = RecipePromptAssembler.CreateRepairMessages(
            FixedDescription,
            new string('x', 16 * 1024 + 1),
            manyIssues);

        CollectionAssert.AreEqual(
            new[]
            {
                "System|4607|" + SystemPromptPin,
                "User|91|" + RequestMessagePin,
                "User|4009|" + TruncatedRepairMessagePin,
            },
            messages.Select(Pin).ToArray());
    }

    [TestMethod]
    public void AssemblyIsDeterministicAcrossCalls()
    {
        var first = RecipePromptAssembler.CreateRepairMessages(FixedDescription, FixedPreviousOutput, FixedIssues());
        var second = RecipePromptAssembler.CreateRepairMessages(FixedDescription, FixedPreviousOutput, FixedIssues());

        CollectionAssert.AreEqual(
            first.Select(static message => message.Role + "|" + message.Content).ToArray(),
            second.Select(static message => message.Role + "|" + message.Content).ToArray());
    }

    // ---- multi-message splitting (production inputs never trigger it; driven synthetically) ----

    [TestMethod]
    public void ASectionLargerThanOneMessageSplitsAtFragmentBoundariesIntoSameRoleMessages()
    {
        var fragments = Enumerable.Range(0, 5)
            .Select(static index => Fragment("part-" + index, new string((char)('a' + index), 6 * 1024)))
            .ToArray();

        var messages = RecipePromptAssembler.Assemble(
        [
            new RecipePromptSection(ChatRole.System, fragments),
        ]);

        Assert.AreEqual(3, messages.Count, "5 × 6 KiB fragments pack two per 16 KiB message.");
        Assert.IsTrue(messages.All(static message => message.Role == ChatRole.System));
        Assert.IsTrue(messages.All(static message => message.Content.Length <= RecipePromptAssembler.MaximumMessageCharacters));
        Assert.AreEqual(
            string.Concat(fragments.Select(static fragment => fragment.Content)),
            string.Concat(messages.Select(static message => message.Content)),
            "Splitting must preserve every byte in order.");
        Assert.AreEqual(2 * 6 * 1024, messages[0].Content.Length, "The split point is the fragment boundary.");
    }

    [TestMethod]
    public void SectionsNeverShareAMessageEvenWhenBothWouldFitInOne()
    {
        var messages = RecipePromptAssembler.Assemble(
        [
            new RecipePromptSection(ChatRole.System, [Fragment("first", "alpha")]),
            new RecipePromptSection(ChatRole.User, [Fragment("second", "beta")]),
        ]);

        Assert.AreEqual(2, messages.Count);
        Assert.AreEqual(ChatRole.System, messages[0].Role);
        Assert.AreEqual("alpha", messages[0].Content);
        Assert.AreEqual(ChatRole.User, messages[1].Role);
        Assert.AreEqual("beta", messages[1].Content);
    }

    [TestMethod]
    public void AFragmentLargerThanOneMessageFailsClosedWithoutTruncation()
    {
        var oversized = Fragment("oversized", new string('x', RecipePromptAssembler.MaximumMessageCharacters + 1));

        var exception = Assert.ThrowsExactly<ChatChannelException>(() =>
            RecipePromptAssembler.Assemble([new RecipePromptSection(ChatRole.User, [oversized])]));

        Assert.AreEqual(ChatChannelErrorCode.PayloadTooLarge, exception.Code);
    }

    [TestMethod]
    public void MoreMessagesThanTheChannelAllowsFailClosed()
    {
        // 65 single-fragment sections assemble into 65 messages, one past ChatChannelLimits.MaximumMessages.
        var sections = Enumerable.Range(0, ChatChannelLimits.MaximumMessages + 1)
            .Select(static index => new RecipePromptSection(ChatRole.User, [Fragment("section-" + index, "content " + index)]))
            .ToArray();

        var exception = Assert.ThrowsExactly<ChatChannelException>(() => RecipePromptAssembler.Assemble(sections));

        Assert.AreEqual(ChatChannelErrorCode.PayloadTooLarge, exception.Code);
    }

    [TestMethod]
    public void ARequestBeyondTheByteBoundFailsClosedBeforeTheMessageCountBound()
    {
        // 17 × 16 KiB ASCII fragments = 272 KiB in 17 messages: inside the message-count bound, past the
        // 256 KiB request byte bound.
        var fragments = Enumerable.Range(0, 17)
            .Select(static index => Fragment("bulk-" + index, new string('y', 16 * 1024)))
            .ToArray();

        var exception = Assert.ThrowsExactly<ChatChannelException>(() =>
            RecipePromptAssembler.Assemble([new RecipePromptSection(ChatRole.User, fragments)]));

        Assert.AreEqual(ChatChannelErrorCode.PayloadTooLarge, exception.Code);
    }

    [TestMethod]
    public void TheLargestConformingSyntheticRequestStillAssembles()
    {
        // 16 × 16 KiB = exactly 256 KiB of ASCII content: the positive edge of the request byte bound.
        var fragments = Enumerable.Range(0, 16)
            .Select(static index => Fragment("edge-" + index, new string('z', 16 * 1024)))
            .ToArray();

        var messages = RecipePromptAssembler.Assemble([new RecipePromptSection(ChatRole.User, fragments)]);

        Assert.AreEqual(16, messages.Count);
        Assert.AreEqual(
            ChatChannelLimits.MaximumRequestBytes,
            messages.Sum(static message => Encoding.UTF8.GetByteCount(message.Content)));
    }

    [TestMethod]
    public void AnEmptySectionListIsRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => RecipePromptAssembler.Assemble([]));
    }

    // ---- composite version string ----

    [TestMethod]
    public void TheCompositeVersionLeadsWithTheAssemblerRevisionAndListsEveryFragment()
    {
        var version = RecipePromptAssembler.Version;

        Assert.AreEqual(
            "vfxcomposer.ai.recipe-prompt-assembler/1;system/1;contract/1;redline/1;catalog/1;reference/1;request/1;previous-output/1;repair/1",
            version);
        Assert.IsTrue(version.Length <= 256, "The draft record bounds PromptTemplateVersion as short text.");
    }

    [TestMethod]
    public void TheCompositeVersionSurvivesTheDraftRecordShortTextGuard()
    {
        var recipeJson = RecipeTemplateCatalogSnapshot.Default.CanonicalExampleJson;
        var draft = new RecipeDraft(
            Guid.NewGuid().ToString("N"),
            recipeJson,
            RecipeCanonicalJson.ComputeSha256(recipeJson),
            "fireball_2d",
            "projectile",
            "2d",
            "mobile_medium",
            RecipePromptAssembler.Version,
            RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion);

        Assert.AreEqual(RecipePromptAssembler.Version, draft.PromptTemplateVersion);
    }

    // ---- fragment and section guards (new behavior classes: rejection paths) ----

    [TestMethod]
    public void AFragmentAcceptsAWellFormedIdVersionAndContent()
    {
        var fragment = new RecipePromptFragment("artist-knowledge-2d", 3, "some prompt text");

        Assert.AreEqual("artist-knowledge-2d", fragment.Id);
        Assert.AreEqual(3, fragment.Version);
        Assert.AreEqual("some prompt text", fragment.Content);
        Assert.IsFalse(fragment.ToString().Contains("some prompt text", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FragmentGuardsRejectMalformedIdsVersionsAndContent()
    {
        Assert.ThrowsExactly<ArgumentException>(static () => new RecipePromptFragment("Upper", 1, "text"));
        Assert.ThrowsExactly<ArgumentException>(static () => new RecipePromptFragment("has space", 1, "text"));
        Assert.ThrowsExactly<ArgumentException>(static () => new RecipePromptFragment(new string('a', 65), 1, "text"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(static () => new RecipePromptFragment("ok", 0, "text"));
        Assert.ThrowsExactly<ArgumentException>(static () => new RecipePromptFragment("ok", 1, " "));
        Assert.ThrowsExactly<ArgumentException>(static () => new RecipePromptFragment("ok", 1, "a\0b"));
    }

    [TestMethod]
    public void SectionGuardsRejectAnEmptyFragmentListAndAnUndefinedRole()
    {
        Assert.ThrowsExactly<ArgumentException>(static () => new RecipePromptSection(ChatRole.User, []));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(static () =>
            new RecipePromptSection((ChatRole)99, [Fragment("ok", "text")]));
    }

    private static RecipePromptFragment Fragment(string id, string content) => new(id, 1, content);

    private static RecipeValidationIssue[] FixedIssues() =>
    [
        new(
            "E123",
            RecipeValidationSeverity.Error,
            "$.stages[1].modules[0].parameters.scale",
            "Value is out of range.",
            "9.5",
            "[0.5, 3]"),
        new(
            "E101",
            RecipeValidationSeverity.Error,
            "$.recipeVersion",
            "Field is required."),
    ];

    private static string Pin(ChatChannelMessage message) =>
        message.Role + "|" + message.Content.Length + "|" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Content))).ToLowerInvariant();
}
