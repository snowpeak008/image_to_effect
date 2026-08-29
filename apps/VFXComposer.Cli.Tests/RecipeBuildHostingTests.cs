using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// Hosting-side coverage for the restricted build: the real project-lock probe's live-versus-residue
/// rule, build-host discovery, and the manifest path that turns a recipe entry into a build job now
/// that the capability gate is open.
/// </summary>
[TestClass]
public sealed class RecipeBuildHostingTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() => _root = Directory.CreateTempSubdirectory("vfxcomposer-build-host-").FullName;

    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void AProjectWithNoEditorArtefactsIsFree()
    {
        Assert.AreEqual(ProjectLockAvailability.Free, new UnityProjectLockProbe(_root, _ => null).Probe());
    }

    [TestMethod]
    public void AHeldLockFileIsBusyWhileTheSameFileLeftBehindIsNot()
    {
        var lockPath = Path.Combine(_root, "Temp", "UnityLockfile");
        Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
        File.WriteAllText(lockPath, string.Empty);
        var probe = new UnityProjectLockProbe(_root, _ => null);

        // Residue on its own must never be read as busy: a force-killed editor leaves it behind and
        // treating it as busy would wedge the queue for good.
        Assert.AreEqual(ProjectLockAvailability.Free, probe.Probe());

        using (new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.AreEqual(ProjectLockAvailability.Busy, probe.Probe());
        }

        Assert.AreEqual(ProjectLockAvailability.Free, probe.Probe());
    }

    [TestMethod]
    public void ARecordedEditorProcessIsBusyOnlyWhileThatExactProcessIsAliveAndIsTheEditor()
    {
        WriteEditorInstance("{\"process_id\" : 4321, \"version\" : \"2022.3.62f3c1\"}");

        Assert.AreEqual(
            ProjectLockAvailability.Busy,
            new UnityProjectLockProbe(_root, id => id == 4321 ? "Unity" : null).Probe());

        // A recycled process id must not be mistaken for the editor.
        Assert.AreEqual(
            ProjectLockAvailability.Free,
            new UnityProjectLockProbe(_root, _ => "notepad").Probe());

        // A dead process id is residue.
        Assert.AreEqual(
            ProjectLockAvailability.Free,
            new UnityProjectLockProbe(_root, _ => null).Probe());
    }

    [TestMethod]
    public void ACorruptOrIncompleteEditorInstanceRecordIsTreatedAsResidue()
    {
        foreach (var body in new[] { "{ this is not json", "{}", "{\"process_id\":\"1234\"}", "{\"process_id\":0}", "[]" })
        {
            WriteEditorInstance(body);
            Assert.AreEqual(
                ProjectLockAvailability.Free,
                new UnityProjectLockProbe(_root, _ => "Unity").Probe(),
                body);
        }
    }

    [TestMethod]
    public void BuildHostDiscoveryRequiresBothTheUnityProjectAndTheWrapperScript()
    {
        Assert.IsNull(UnityBuildHostLocator.TryLocateAt(_root));

        Directory.CreateDirectory(Path.Combine(_root, "project", "Assets"));
        Assert.IsNull(UnityBuildHostLocator.TryLocateAt(_root), "The wrapper script alone is missing.");

        Directory.CreateDirectory(Path.Combine(_root, "tools"));
        File.WriteAllText(Path.Combine(_root, "tools", "Invoke-Unity.ps1"), "param()");

        var located = UnityBuildHostLocator.TryLocateAt(_root);
        Assert.IsNotNull(located);
        Assert.AreEqual(Path.Combine(_root, "project"), located.ProjectPath);
        Assert.AreEqual(Path.Combine(_root, "tools", "Invoke-Unity.ps1"), located.WrapperScriptPath);
    }

    [TestMethod]
    public void BuildDiscoveryFindsThisRepositoryFromTheRunningAssembly()
    {
        // The CLI and MCP roots rely on this walk to decide whether they can offer build capability.
        var located = UnityBuildHostLocator.TryLocate();

        Assert.IsNotNull(located, "The test host runs inside the repository, so discovery must succeed.");
        Assert.IsTrue(Directory.Exists(Path.Combine(located.ProjectPath, "Assets")));
        Assert.IsTrue(File.Exists(located.WrapperScriptPath));
    }

    [TestMethod]
    public void AnOpenCapabilityGateTurnsARecipeEntryIntoABuildJobCarryingItsContent()
    {
        var recipeJson = "{\"recipeVersion\":1,\"id\":\"probe_effect\"}";
        var recipePath = Path.Combine(_root, "probe.json");
        File.WriteAllText(recipePath, recipeJson, new UTF8Encoding(false));
        var manifestJson = Manifest("probe.json");
        var recipes = new FileSystemBatchRecipeProbe(_root);

        var parsed = BatchManifestParser.Parse(manifestJson, recipes, BatchCapabilityProfile.GenerationAndRecipeBuild);
        Assert.IsTrue(parsed.IsValid, string.Join("; ", parsed.Issues.Select(static issue => issue.Code)));

        var request = BatchSubmissionService.CreateRequest(
            JobSourceEntries.Cli,
            parsed.Manifest!,
            BatchFailurePolicies.ToQueuePolicy(parsed.Manifest!.FailurePolicy),
            parsed.Manifest.Items[0],
            recipes.Read("probe.json"));

        Assert.AreEqual(BatchJobKinds.RecipeBuild, request.JobKind);
        var content = BatchRecipeBuildPayload.Parse(request.Payload);
        Assert.IsNull(content.DraftId, "A manifest entry carries no draft identity.");
        Assert.AreEqual(recipeJson, content.RecipeJson);
        Assert.AreEqual(
            VFXComposer.AI.Contracts.Recipes.RecipeCanonicalJson.ComputeSha256(recipeJson),
            content.CanonicalSha256);
    }

    [TestMethod]
    public void AClosedCapabilityGateStillRefusesTheWholeManifest()
    {
        File.WriteAllText(Path.Combine(_root, "probe.json"), "{\"recipeVersion\":1}", new UTF8Encoding(false));

        var parsed = BatchManifestParser.Parse(
            Manifest("probe.json"),
            new FileSystemBatchRecipeProbe(_root),
            BatchCapabilityProfile.GenerationOnly);

        Assert.IsFalse(parsed.IsValid);
        CollectionAssert.Contains(
            parsed.Issues.Select(static issue => issue.Code).ToArray(),
            BatchDiagnosticCodes.RecipeBuildNotSupported);
    }

    [TestMethod]
    public void ARecipeEntryWithoutAReadableSourceFailsTheWholeSubmission()
    {
        File.WriteAllText(Path.Combine(_root, "probe.json"), "{\"recipeVersion\":1}", new UTF8Encoding(false));
        var parsed = BatchManifestParser.Parse(
            Manifest("probe.json"),
            new FileSystemBatchRecipeProbe(_root),
            BatchCapabilityProfile.GenerationAndRecipeBuild);

        Assert.ThrowsExactly<ArgumentException>(() => BatchSubmissionService.CreateRequest(
            JobSourceEntries.Cli,
            parsed.Manifest!,
            BatchFailurePolicies.ToQueuePolicy(parsed.Manifest!.FailurePolicy),
            parsed.Manifest.Items[0]));
    }

    [TestMethod]
    public void TheRecipeSourceRefusesAReferenceThatEscapesTheManifestDirectory()
    {
        var source = new FileSystemBatchRecipeProbe(_root);
        Assert.ThrowsExactly<InvalidDataException>(() => source.Read("../outside.json"));
        Assert.ThrowsExactly<InvalidDataException>(() => source.Read("absent.json"));

        File.WriteAllText(Path.Combine(_root, "not-an-object.json"), "[]", new UTF8Encoding(false));
        Assert.ThrowsExactly<InvalidDataException>(() => source.Read("not-an-object.json"));
    }

    [TestMethod]
    public void TheBuildExecutorRequiresTheProjectLockAndOwnsTheBuildJobKind()
    {
        var executor = new RecipeBuildJobExecutor(new RecipeBuildOrchestrator(
            new NeverStartingRunner(),
            () => new InMemoryRecipeDraftStore()));

        Assert.AreEqual(BatchJobKinds.RecipeBuild, executor.JobKind);
        Assert.IsTrue(executor.RequiresProjectLock, "A build opens the Unity project and must wait for the lock.");
    }

    private void WriteEditorInstance(string body)
    {
        var path = Path.Combine(_root, "Library", "EditorInstance.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, body, new UTF8Encoding(false));
    }

    private static string Manifest(string recipePath) =>
        "{\"schemaVersion\":\"vfxcomposer.batch-manifest/1\",\"batchId\":\"f2probe\",\"items\":[" +
        "{\"itemId\":\"one\",\"kind\":\"recipe\",\"recipePath\":" + JsonSerializer.Serialize(recipePath) + "}]}";

    private sealed class NeverStartingRunner : IUnityRecipeBuildRunner
    {
        public IUnityRecipeBuildProcess Start(UnityRecipeBuildLaunch launch) =>
            throw new IOException("This test never starts a process.");
    }
}
