using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Catalog
{
    public interface IAssetReferenceResolver
    {
        AssetReferenceResolution Resolve(string assetGuid);
    }

    public sealed class AssetReferenceResolution
    {
        public bool Found;
        public string AssetPath;
    }

    public sealed class TemplateCatalog
    {
        private readonly Dictionary<string, TemplateManifest> byTemplateId = new Dictionary<string, TemplateManifest>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, TemplateManifest> ByTemplateId { get { return byTemplateId; } }
        public ValidationReport Report { get; private set; } = new ValidationReport();

        public static TemplateCatalog FromManifestJson(IEnumerable<string> manifests, IAssetReferenceResolver resolver = null)
        {
            var catalog = new TemplateCatalog();
            if (manifests == null) return catalog;
            var index = 0;
            foreach (var json in manifests)
            {
                index++;
                var parsed = VfxDomainParser.ParseManifest(json, "/manifests/" + index);
                catalog.AddParsed(parsed, resolver, "/manifests/" + index);
            }
            return catalog;
        }

        public static TemplateCatalog LoadFromDirectory(string directory, IAssetReferenceResolver resolver = null)
        {
            var catalog = new TemplateCatalog();
            if (!Directory.Exists(directory))
            {
                catalog.Report.Add("E200", ValidationSeverity.Error, "/catalog", "Manifest directory does not exist.");
                return catalog;
            }
            // Slash v2 manifests have a deliberately distinct suffix and catalog; v1 must not reinterpret that closed domain as a projectile template.
            var files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).Where(file => !file.EndsWith(".slash.manifest.json", StringComparison.Ordinal)).ToArray();
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var path = "/catalog/" + Path.GetFileName(file);
                try { catalog.AddParsed(VfxDomainParser.ParseManifest(File.ReadAllText(file), path), resolver, path); }
                catch (Exception exception) { catalog.Report.Add("E212", ValidationSeverity.Error, path, "Manifest file could not be loaded: " + exception.Message); }
            }
            return catalog;
        }

        public bool TryGet(string templateId, out TemplateManifest manifest)
        {
            return byTemplateId.TryGetValue(templateId, out manifest);
        }

        private void AddParsed(ParseResult<TemplateManifest> parsed, IAssetReferenceResolver resolver, string path)
        {
            Report.AddRange(parsed.Report);
            if (parsed.Report.HasErrors || parsed.Value == null) return;
            var manifest = parsed.Value;
            var semanticReport = ManifestValidator.ValidateSemantic(manifest, path);
            Report.AddRange(semanticReport);
            if (semanticReport.HasErrors) return;
            if (byTemplateId.ContainsKey(manifest.TemplateId))
            {
                Report.Add("E201", ValidationSeverity.Error, path + "/templateId", "Template ID is duplicated in the catalog.");
                return;
            }
            if (resolver == null) { byTemplateId.Add(manifest.TemplateId, manifest); return; }
            AssetReferenceResolution resolution;
            try { resolution = resolver.Resolve(manifest.AssetGuid); }
            catch (Exception exception) { Report.Add("E211", ValidationSeverity.Error, path + "/assetGuid", "Manifest asset GUID resolver failed: " + exception.Message); return; }
            if (resolution == null || !resolution.Found)
            {
                Report.Add("E202", ValidationSeverity.Error, path + "/assetGuid", "Manifest asset GUID cannot be resolved.");
            }
            else if (!string.Equals(resolution.AssetPath, manifest.AssetPath, StringComparison.Ordinal))
            {
                Report.Add("E203", ValidationSeverity.Error, path + "/assetPath", "Manifest asset path does not match the resolved GUID path.");
            }
            else byTemplateId.Add(manifest.TemplateId, manifest);
        }
    }
}
