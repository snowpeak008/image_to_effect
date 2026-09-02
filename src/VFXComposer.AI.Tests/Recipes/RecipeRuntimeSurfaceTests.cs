using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers;
using VFXComposer.AI.Providers.Desktop;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

[TestClass]
public sealed class RecipeRuntimeSurfaceTests
{
    [TestMethod]
    public async Task TheDesktopRuntimeExposesTheRecipeChannelAndWritesDraftsOnlyToTheGivenPath()
    {
        using var directory = new A1TestDirectory();
        var draftPath = Path.Combine(directory.Path, "drafts", "recipe-drafts.json");
        await using var runtime = new ProviderDesktopRuntime(
            new ProviderConfigurationStore(Path.Combine(directory.Path, "providers.json")),
            new ProviderSecretStore(Path.Combine(directory.Path, "secrets")),
            new ProviderHealthRegistry(),
            privateImageTempRoot: null,
            recipeDraftStorePath: draftPath);

        Assert.IsNotNull(runtime.RecipeGeneration);
        Assert.IsNotNull(runtime.RecipeDrafts);

        // Composition alone starts nothing: no draft file exists until a draft is explicitly saved.
        Assert.IsFalse(File.Exists(draftPath));

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
        var record = runtime.RecipeDrafts.Save(RecipeDraftRecord.Create(
            RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, [])]),
            DateTimeOffset.UtcNow));

        Assert.IsTrue(File.Exists(draftPath));
        Assert.IsNotNull(runtime.RecipeDrafts.TryGet(record.DraftId));
    }
}
