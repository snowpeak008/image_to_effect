using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class UiPreferencesStoreTests
{
    private const string DocumentName = "ui-preferences.json";

    private string _storageDirectory = string.Empty;

    [TestInitialize]
    public void CreateStorageDirectory() => _storageDirectory = Path.Combine(
        Path.GetTempPath(),
        "vfxcomposer-ui-preferences-tests",
        Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void RemoveStorageDirectory()
    {
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void EveryLanguageAndModeSurvivesARestart()
    {
        foreach (var language in UiStringCatalog.Languages)
        {
            foreach (var mode in Enum.GetValues<GenerationMode>())
            {
                new UiPreferencesStore(_storageDirectory).Save(new UiPreferences(language, mode));

                var reloaded = new UiPreferencesStore(_storageDirectory).Load();

                Assert.IsNotNull(reloaded);
                Assert.AreEqual(language, reloaded.Language);
                Assert.AreEqual(mode, reloaded.GenerationMode);
            }
        }
    }

    [TestMethod]
    public void ALegacyDocumentUpgradesWithoutLosingTheLanguage()
    {
        // REQ-004-09 end to end: a stored /1 Chinese document starts the session in Chinese with the default mode
        // and without any diagnostic; the next explicit save rebuilds the file as /2 with the language intact.
        WriteDocument("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"ChineseSimplified\"}");
        var diagnostics = new InMemoryDiagnosticSink();
        var store = new UiPreferencesStore(_storageDirectory, diagnostics);

        var loaded = store.Load();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(UiLanguage.ChineseSimplified, loaded.Language);
        Assert.AreEqual(GenerationMode.Simple, loaded.GenerationMode);
        Assert.AreEqual(0, diagnostics.Snapshot.Count, "A readable legacy document is not a failure.");

        store.Save(loaded with { GenerationMode = GenerationMode.Professional });

        var text = File.ReadAllText(Path.Combine(_storageDirectory, DocumentName), Encoding.UTF8);
        StringAssert.Contains(text, UiPreferencesCodec.SchemaId);
        Assert.IsFalse(text.Contains(UiPreferencesCodec.LegacySchemaId, StringComparison.Ordinal));
        var reloaded = store.Load();
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(UiLanguage.ChineseSimplified, reloaded.Language, "The upgrade never resets the language.");
        Assert.AreEqual(GenerationMode.Professional, reloaded.GenerationMode);
    }

    [TestMethod]
    public void SavingLeavesOnlyTheDocumentBehind()
    {
        var store = new UiPreferencesStore(_storageDirectory);

        store.Save(new UiPreferences(UiLanguage.English));
        store.Save(new UiPreferences(UiLanguage.ChineseSimplified));

        CollectionAssert.AreEqual(
            new[] { DocumentName },
            Directory.GetFiles(_storageDirectory).Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void AnAbsentDocumentIsANormalFirstRun()
    {
        var diagnostics = new InMemoryDiagnosticSink();

        Assert.IsNull(new UiPreferencesStore(_storageDirectory, diagnostics).Load());
        Assert.AreEqual(0, diagnostics.Snapshot.Count);
    }

    [TestMethod]
    [DataRow("not json")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\",\"generationMode\":\"Wizard\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"Klingon\"}")]
    [DataRow("")]
    public void AnUnusableDocumentFallsBackToTheDefaultAndIsRecorded(string content)
    {
        WriteDocument(content);
        var diagnostics = new InMemoryDiagnosticSink();

        var loaded = new UiPreferencesStore(_storageDirectory, diagnostics).Load();

        Assert.IsNull(loaded);
        var recorded = diagnostics.Snapshot.Single();
        Assert.AreEqual(UiPreferencesStore.LoadFailureDiagnosticCode, recorded.Code);
        Assert.IsFalse(recorded.Message.Contains(_storageDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AnUnknownModeValueFallsBackWithoutRewritingTheFile()
    {
        // AC-12: the /2 document names an unknown mode; the whole document is unusable, the diagnostic is recorded,
        // and the file stays byte-identical until the next explicit save.
        const string content = "{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"ChineseSimplified\",\"generationMode\":\"Wizard\"}";
        WriteDocument(content);
        var diagnostics = new InMemoryDiagnosticSink();
        var documentLocation = Path.Combine(_storageDirectory, DocumentName);
        var bytesBefore = File.ReadAllBytes(documentLocation);

        var loaded = new UiPreferencesStore(_storageDirectory, diagnostics).Load();

        Assert.IsNull(loaded, "Language and mode fall back together: the document is unusable as a whole.");
        Assert.AreEqual(UiPreferencesStore.LoadFailureDiagnosticCode, diagnostics.Snapshot.Single().Code);
        CollectionAssert.AreEqual(bytesBefore, File.ReadAllBytes(documentLocation), "Loading never rewrites the file.");
    }

    [TestMethod]
    public void AnOversizedDocumentIsRejectedWithoutBeingParsed()
    {
        WriteDocument(new string('a', 8192));
        var diagnostics = new InMemoryDiagnosticSink();

        Assert.IsNull(new UiPreferencesStore(_storageDirectory, diagnostics).Load());
        Assert.AreEqual(UiPreferencesStore.LoadFailureDiagnosticCode, diagnostics.Snapshot.Single().Code);
    }

    [TestMethod]
    public void AnUnusableDocumentIsRebuiltByTheNextExplicitSave()
    {
        WriteDocument("not json");
        var store = new UiPreferencesStore(_storageDirectory);

        store.Save(new UiPreferences(UiLanguage.ChineseSimplified));

        var reloaded = store.Load();
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(UiLanguage.ChineseSimplified, reloaded.Language);
    }

    [TestMethod]
    public void AStorageFailureIsRecordedInsteadOfThrown()
    {
        // A document location occupied by a directory cannot be replaced: the session must still keep running.
        Directory.CreateDirectory(Path.Combine(_storageDirectory, DocumentName));
        var diagnostics = new InMemoryDiagnosticSink();

        new UiPreferencesStore(_storageDirectory, diagnostics).Save(new UiPreferences(UiLanguage.English));

        Assert.AreEqual(UiPreferencesStore.SaveFailureDiagnosticCode, diagnostics.Snapshot.Single().Code);
        CollectionAssert.AreEqual(
            Array.Empty<string>(),
            Directory.GetFiles(_storageDirectory).Select(Path.GetFileName).ToArray());
    }

    private void WriteDocument(string content)
    {
        Directory.CreateDirectory(_storageDirectory);
        // BOM-less on purpose: the store writes plain UTF-8, and a leading BOM would fail JSON parsing and turn a
        // deliberately valid fixture document into a false negative.
        File.WriteAllText(Path.Combine(_storageDirectory, DocumentName), content, new UTF8Encoding(false));
    }
}
