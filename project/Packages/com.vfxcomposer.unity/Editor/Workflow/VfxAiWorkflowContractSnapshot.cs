using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace VFXComposer.Editor.Workflow
{
    /// <summary>Produces the exact formal-document bundle that an isolated AI cohort receives.</summary>
    public static class VfxAiWorkflowContractSnapshot
    {
        private static readonly string[] Inputs = { "README.md", "recipe-authoring.md", "canonical-recipe.generated.json", "canonical-patches.generated.md", "template-parameters.generated.md", "recipe-v1.schema.json", "validation-reports.md", "patch-authoring.md" };
        public static string OutputPath { get { return OutputPathFor("cohort-e"); } }
        public static string OutputPathFor(string cohort) { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", cohort, "contract-snapshot.md"); }
        public static string BuildBundle()
        {
            var root = Path.Combine(RepositoryRoot(), "docs", "ai-workflow");
            var bundle = new StringBuilder();
            foreach (var input in Inputs) bundle.Append("<!-- BEGIN ").Append(input).Append(" -->\n").Append(File.ReadAllText(Path.Combine(root, input))).Append("\n<!-- END ").Append(input).Append(" -->\n");
            return bundle.ToString();
        }
        public static string ExportOnce()
        {
            return ExportOnce("cohort-e", "Cohort E");
        }
        public static string ExportOnce(string cohort, string title)
        {
            var text = BuildBundle();
            var hash = Hash(text);
            var output = "# " + title + " formal contract snapshot\n\nSHA-256: `" + hash + "`\n\n" + text;
            var path = OutputPathFor(cohort);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path)) throw new InvalidOperationException(title + " contract snapshot already exists and must not be overwritten.");
            File.WriteAllText(path, output, new UTF8Encoding(false));
            return output;
        }
        public static bool VerifyExisting(out string hash)
        {
            return VerifyExisting("cohort-e", out hash);
        }
        public static bool VerifyExisting(string cohort, out string hash)
        {
            hash = null;
            var path = OutputPathFor(cohort);
            if (!File.Exists(path)) return false;
            var output = File.ReadAllText(path);
            const string marker = "SHA-256: `";
            var start = output.IndexOf(marker, StringComparison.Ordinal); if (start < 0) return false;
            start += marker.Length; var end = output.IndexOf('`', start); if (end < start) return false;
            hash = output.Substring(start, end - start);
            var bodyStart = output.IndexOf("\n\n", end, StringComparison.Ordinal); if (bodyStart < 0) return false;
            return string.Equals(hash, Hash(output.Substring(bodyStart + 2)), StringComparison.Ordinal);
        }
        public static string Hash(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
    }
}
