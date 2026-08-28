using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24TypedBinaryCanonicalEncodingTests
    {
        [Test]
        public void PythonVectors_PreserveTypesUtf8OrderingAndFiniteDoubleBits()
        {
            Assert.That(W24TypedBinaryCanonicalEncoding.Hash(new JValue(1)), Is.Not.EqualTo(W24TypedBinaryCanonicalEncoding.Hash(new JValue(1.0))));
            Assert.That(W24TypedBinaryCanonicalEncoding.Hash(new JValue(0.0)), Is.Not.EqualTo(W24TypedBinaryCanonicalEncoding.Hash(new JValue(-0.0))));
            var unicode = new JObject { ["\u00e9"] = "\u503c", ["a"] = "snowman \u2603" };
            Assert.That(W24TypedBinaryCanonicalEncoding.Hash(unicode), Is.EqualTo("sha256:d96fc4926441837c6b4e7cffa4a044a9348cbfdf2917eedf76cd3fa9846d83b4"));
            Assert.That(W24TypedBinaryCanonicalEncoding.Hash(new JValue(ulong.MaxValue)), Is.EqualTo(W24TypedBinaryCanonicalEncoding.Hash(new JValue(new BigInteger(ulong.MaxValue)))));
            var report = JObject.Parse("{\"schema\":\"w24-render-metrics-report/v1\",\"route\":\"MEASURED\",\"machineGatesPassed\":true,\"checks\":[{\"id\":\"receiver\",\"kind\":\"receiver_luminance\",\"pass\":true,\"linearLuminanceDelta\":0.5,\"receiverPixels\":36}],\"inputSha256\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"toolSha256\":\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\",\"sealedReportEncoding\":\"w24-typed-binary-v1\"}");
            var seal = W24TypedBinaryCanonicalEncoding.Hash(report);
            Assert.That(seal, Is.EqualTo("sha256:88f8b75d242347a18a0f9834d56b13c4fbd625f28600409bab2a45b571dc6c6f"));
            report["route"] = "EVIDENCE_INVALID";
            Assert.That(W24TypedBinaryCanonicalEncoding.Verify(seal, report), Is.False);
        }

        [Test]
        public void NonFiniteAndLoneSurrogateAreRejected()
        {
            Assert.Catch<Exception>(() => W24TypedBinaryCanonicalEncoding.Hash(new JValue(double.NaN)));
            Assert.Catch<Exception>(() => W24TypedBinaryCanonicalEncoding.Hash(new JValue(double.PositiveInfinity)));
            Assert.Catch<Exception>(() => W24TypedBinaryCanonicalEncoding.Hash(new JValue("bad\ud800")));
        }

        [Test]
        public void TypedMetricsCompatibility_OnlyPermitsContractsWithoutTypedDeclarations()
        {
            Assert.That(ContractDeclaresTypedMetrics(new JObject()), Is.False, "A genuine pre-typed S0a/S0b Contract may retain empty typed arrays.");
            Assert.That(ContractDeclaresTypedMetrics(new JObject { ["typedDiagnostics"] = new JObject() }), Is.True);
            Assert.That(ContractDeclaresTypedMetrics(new JObject { ["typedDiagnostics"] = new JObject { ["requiredEvidenceMatrix"] = new JArray() } }), Is.True);
            Assert.That(ContractDeclaresTypedMetrics(new JObject { ["typedDiagnostics"] = new JObject { ["receiver"] = new JObject { ["metricPlan"] = new JObject() } } }), Is.True);
            var legacyGate = new W24S5ProductionGateResult();
            Assert.That(VerifyTypedMetricsDag(legacyGate, new JObject(), new JObject()), Is.True);
            Assert.That(legacyGate.HasErrors, Is.False);
            var typedGate = new W24S5ProductionGateResult();
            Assert.That(VerifyTypedMetricsDag(typedGate, new JObject { ["typedDiagnostics"] = new JObject { ["requiredEvidenceMatrix"] = new JArray() } }, new JObject()), Is.False);
            Assert.That(typedGate.Issues.Any(item => item.Code == "W24S5-C140" && item.IsError), Is.True, "A typed Contract cannot bypass report self-seal verification with empty record groups.");
            var partialGate = new W24S5ProductionGateResult();
            Assert.That(VerifyTypedMetricsDag(partialGate, new JObject { ["typedDiagnostics"] = new JObject() }, new JObject { ["metricInputs"] = new JArray(new JObject()), ["metricReports"] = new JArray(new JObject()) }), Is.False);
            Assert.That(partialGate.Issues.Any(item => item.Code == "W24S5-C140" && item.IsError), Is.True, "A partial typed DAG must fail before any legacy compatibility path.");
        }

        [Test]
        public void MetricsJsonParsers_RejectDuplicateAndNonFiniteAndPinFloatToBinary64()
        {
            foreach (var target in new[] { typeof(W24MetricsEvidenceDag), typeof(W24S5EvidenceTransition) })
            {
                var method = target == typeof(W24MetricsEvidenceDag) ? "ParseStrict" : "Parse";
                var parsed = ParsePrivate(target, method, "{\"value\":0.5}");
                Assert.That(((JValue)parsed["value"]).Value, Is.TypeOf<double>(), target.Name + " must parse floats as binary64.");
                Assert.That((string)ParsePrivate(target, method, "{\"x\":\"\\ud83d\\ude00\"}")["x"], Is.EqualTo("\ud83d\ude00"), target.Name + " must accept a valid escaped surrogate pair.");
                Assert.That((string)ParsePrivate(target, method, "{\"\ud83d\ude00\":\"\ud83d\ude00\"}")["\ud83d\ude00"], Is.EqualTo("\ud83d\ude00"), target.Name + " must accept valid raw supplementary scalars in keys and values.");
                Assert.That((string)ParsePrivate(target, method, "{\"x\":\"\\\\ud800\"}")["x"], Is.EqualTo("\\ud800"), target.Name + " must not treat an escaped backslash as a Unicode escape.");
                Assert.Catch<Exception>(() => ParsePrivate(target, method, "{\"x\":1,\"x\":2}"), target.Name + " must reject duplicate fields.");
                foreach (var invalid in new[] { "{\"x\":NaN}", "{\"x\":Infinity}", "{\"x\":1e999}", "{\"x\":\"\\ud800\"}", "{\"x\":\"\\udc00\"}", "{\"x\":\"\\ud800A\"}", "{\"x\":\"\\ud800\\u0041\"}", "{\"x\":\"\\ud800\\\\udc00\"}", "{\"\\ud800\":1}" })
                    Assert.Catch<Exception>(() => ParsePrivate(target, method, invalid), target.Name + " must reject non-finite floats and lone surrogates: " + invalid);
            }
        }

        private static bool ContractDeclaresTypedMetrics(JObject extensions)
        {
            var method = typeof(W24S5EvidenceTransition).GetMethod("ContractDeclaresTypedMetrics", BindingFlags.Static | BindingFlags.NonPublic);
            return (bool)method.Invoke(null, new object[] { new VfxDesignContract { Extensions = extensions } });
        }

        private static bool VerifyTypedMetricsDag(W24S5ProductionGateResult gate, JObject extensions, JObject metadata)
        {
            var contextType = typeof(W24S5EvidenceTransition).GetNestedType("CandidateContext", BindingFlags.NonPublic);
            var context = Activator.CreateInstance(contextType, true);
            contextType.GetField("Contract", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(context, new VfxDesignContract { Extensions = extensions });
            var method = typeof(W24S5EvidenceTransition).GetMethod("VerifyTypedMetricsDag", BindingFlags.Static | BindingFlags.NonPublic);
            return (bool)method.Invoke(null, new[] { (object)gate, metadata, context });
        }

        private static JObject ParsePrivate(Type target, string methodName, string text)
        {
            var method = target.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            try { return (JObject)method.Invoke(null, new object[] { text }); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }
    }
}
