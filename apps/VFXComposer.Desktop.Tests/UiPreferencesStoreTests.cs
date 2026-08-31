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
    public void EveryLanguageSurvivesARestart()
    {
        foreach (var language in UiStringCatalog.Languages)
        {
            new UiPreferencesStore(_storageDirectory).Save(new UiPreferences(language));

            var reloaded = new UiPreferencesStore(_storageDirectory).Load();

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(language, reloaded.Language);
        }
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
        File.WriteAllText(Path.Combine(_storageDirectory, DocumentName), content, Encoding.UTF8);
    }
}
