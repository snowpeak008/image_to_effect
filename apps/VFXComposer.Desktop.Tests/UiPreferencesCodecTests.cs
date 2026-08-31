using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class UiPreferencesCodecTests
{
    [TestMethod]
    public void SerializeWritesTheVersionedSchema()
    {
        Assert.AreEqual(
            "{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"ChineseSimplified\"}",
            UiPreferencesCodec.Serialize(new UiPreferences(UiLanguage.ChineseSimplified)));
    }

    [TestMethod]
    public void EveryLanguageRoundTrips()
    {
        foreach (var language in UiStringCatalog.Languages)
        {
            var text = UiPreferencesCodec.Serialize(new UiPreferences(language));

            Assert.IsTrue(UiPreferencesCodec.TryParse(text, out var parsed));
            Assert.AreEqual(language, parsed.Language);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not json")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\"}")]
    [DataRow("{\"language\":\"English\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/2\",\"language\":\"English\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"Klingon\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"english\"}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":0}")]
    [DataRow("{\"schema\":\"vfxcomposer.ui-preferences/1\",\"language\":\"English\",\"theme\":\"dark\"}")]
    public void UnusableDocumentsAreReportedInsteadOfGuessed(string? text)
    {
        Assert.IsFalse(UiPreferencesCodec.TryParse(text, out var parsed));
        Assert.IsNull(parsed);
    }

    [TestMethod]
    public void SerializeRejectsAnUndeclaredLanguage() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            UiPreferencesCodec.Serialize(new UiPreferences((UiLanguage)42)));
}
