using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class UiPreferencesCodecTests
{
    [TestMethod]
    public void SerializeWritesTheVersionedSchemaWithTheLiteralModeName()
    {
        Assert.AreEqual(
            "{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"ChineseSimplified\",\"generationMode\":\"Simple\"}",
            UiPreferencesCodec.Serialize(new UiPreferences(UiLanguage.ChineseSimplified)));
        Assert.AreEqual(
            "{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\",\"generationMode\":\"Professional\"}",
            UiPreferencesCodec.Serialize(new UiPreferences(UiLanguage.English, GenerationMode.Professional)));
    }

    [TestMethod]
    public void EveryLanguageAndModeCombinationRoundTrips()
    {
        foreach (var language in UiStringCatalog.Languages)
        {
            foreach (var mode in Enum.GetValues<GenerationMode>())
            {
                var text = UiPreferencesCodec.Serialize(new UiPreferences(language, mode));

                Assert.IsTrue(UiPreferencesCodec.TryParse(text, out var parsed));
                Assert.AreEqual(language, parsed.Language);
                Assert.AreEqual(mode, parsed.GenerationMode);
            }
        }
    }

    [TestMethod]
    public void ALegacyDocumentIsAdoptedWithItsLanguageAndTheDefaultMode()
    {
        // REQ-004-09: the /1 shape is exactly {schema, language}; its language survives, the mode defaults.
        Assert.IsTrue(UiPreferencesCodec.TryParse(
            "{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"ChineseSimplified\"}",
            out var parsed));
        Assert.AreEqual(UiLanguage.ChineseSimplified, parsed.Language);
        Assert.AreEqual(GenerationMode.Simple, parsed.GenerationMode);

        // A re-serialized adoption is a /2 document: /1 is read for upgrade, never written again.
        StringAssert.Contains(UiPreferencesCodec.Serialize(parsed), UiPreferencesCodec.SchemaId);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\"}")]
    [DataRow("{\"language\":\"English\",\"generationMode\":\"Simple\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/3\",\"language\":\"English\",\"generationMode\":\"Simple\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"Klingon\",\"generationMode\":\"Simple\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\",\"generationMode\":\"Wizard\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\",\"generationMode\":\"simple\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\",\"generationMode\":0}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\",\"generationMode\":\"Simple\",\"theme\":\"dark\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"Klingon\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"english\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":0}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"English\",\"theme\":\"dark\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"English\",\"generationMode\":\"Simple\"}")]
    public void UnusableDocumentsAreReportedInsteadOfGuessed(string? text)
    {
        Assert.IsFalse(UiPreferencesCodec.TryParse(text, out var parsed));
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void SerializeRejectsAnUndeclaredLanguage() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            UiPreferencesCodec.Serialize(new UiPreferences((UiLanguage)42)));

    [TestMethod]
    public void SerializeRejectsAnUndeclaredGenerationMode() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            UiPreferencesCodec.Serialize(new UiPreferences(UiLanguage.English, (GenerationMode)42)));
}
