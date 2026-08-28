using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace VFXComposer.Tests.EditMode
{
    /// <summary>
    /// Source-bound preparation checks only. They prove why the measured-failure sealing change
    /// cannot be patched into the current S3 chain without a new frozen bundle revision.
    /// </summary>
    public sealed class W24MetricsFailureSealingDesignTests
    {
        private const string BundlePath = "docs/vfx-contracts/capture-tools/w24-s3-capture-tool.bundle.json";
        private const string DagPath = "project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24MetricsEvidenceDag.cs";
        private const string ToolPath = "tools/vfx/metrics/render_metrics.py";
        private const string TransitionPath = "project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5EvidenceTransition.cs";
        private const string RecorderPath = "project/Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs";

        [Test]
        public void CurrentS3Bundle_ExactlyPinsEverySourceThatWouldOtherwiseBeChanged()
        {
            var bundle = JObject.Parse(File.ReadAllText(Absolute(BundlePath)));
            var sources = (bundle["sources"] as JArray ?? new JArray()).OfType<JObject>()
                .ToDictionary(item => (string)item["path"], item => (string)item["sha256"], StringComparer.Ordinal);
            foreach (var path in new[] { DagPath, ToolPath, TransitionPath,
                "project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5RecorderCaptureCompletion.cs", RecorderPath })
            {
                Assert.That(sources.ContainsKey(path), Is.True, "Frozen S3 bundle unexpectedly omits " + path);
                Assert.That(HashFile(Absolute(path)), Is.EqualTo(sources[path]), "Frozen S3 source bytes drifted before the bundle-revision decision: " + path);
            }
        }

        [Test]
        public void FrozenTool_AlreadyEmitsDistinctTypedSealedMeasuredFailureAndInvalidShapes()
        {
            var source = File.ReadAllText(Absolute(ToolPath));
            Assert.That(source, Does.Contain("\"route\": \"MEASURED\", \"machineGatesPassed\": bool(all(x[\"pass\"] for x in checks))"));
            Assert.That(source, Does.Contain("\"route\": \"EVIDENCE_INVALID\", \"machineGatesPassed\": False"));
            Assert.That(source, Does.Contain("report[\"sealedReportEncoding\"] = TYPED_REPORT_ENCODING"));
            Assert.That(source, Does.Contain("report[\"sealedReportHash\"] = typed_binary_hash(report)"));
        }

        [Test]
        public void CurrentBridgeBlocksPreservation_WhileFormalTransitionStillRejectsNonPassAuthority()
        {
            var dag = File.ReadAllText(Absolute(DagPath));
            Assert.That(dag, Does.Contain("(bool?)report[\"machineGatesPassed\"] != true"), "This preparation fixture must be revised together with the next frozen bridge bundle.");
            Assert.That(dag, Does.Contain("(bool?)check[\"pass\"] != true"), "Current bridge unexpectedly stopped enforcing all-pass before recorder commit.");

            var recorder = File.ReadAllText(Absolute(RecorderPath));
            Assert.That(recorder, Does.Contain("public string WriteMetricsReport("));
            Assert.That(recorder, Does.Contain("var hash = store.WriteBytes(normalized, bytes);"), "Recorder no longer exposes the existing write-once byte-preservation primitive.");

            var transition = File.ReadAllText(Absolute(TransitionPath));
            Assert.That(transition, Does.Contain("!Same((string)reportRoot[\"route\"], \"MEASURED\") || (bool?)reportRoot[\"machineGatesPassed\"] != true"));
            Assert.That(transition, Does.Contain("resultChecks.Any(value => (bool?)value[\"pass\"] != true"));
            Assert.That(transition, Does.Contain("PassingChecks.Contains(evidence.MetricCheckId)"));
        }

        private static string Absolute(string relative)
        {
            return Path.GetFullPath(Path.Combine(RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string HashFile(string path)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(input).Select(value => value.ToString("x2")));
        }

        private static string RepositoryRoot
        {
            get { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        }
    }
}
