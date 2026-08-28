using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S6.External;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S6McpOperationEnvelopeTests
    {
        private const string ProjectHash = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string InputHash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Test]
        public void StructuralEnvelopeComparison_IsNotExecutionOrReviewedPlanAuthority()
        {
            var envelope = Envelope(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/fireball.json");
            var result = new W24S6McpOperationEnvelopePolicy(ProjectHash).Validate(envelope, envelope.PlanHash);
            Assert.That(result.IsValid, Is.True);
            Assert.That(typeof(W24S6LocalDocumentInspector).GetMethods().Any(method => method.Name.Contains("Execute") || method.Name.Contains("Write")), Is.False);
        }

        [Test]
        public void ApplyTokensAndAuthority_AreStructurallyRejected()
        {
            var envelope = Envelope(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/fireball.json");
            envelope.ExecutionMode = W24S6McpExecutionMode.Apply; envelope.RequestedAuthority = W24S6McpAuthority.L4Signoff;
            envelope.RollbackMode = W24S6McpRollbackMode.TransactionRollback; envelope.ApprovalToken = "token"; Rehash(envelope);
            var codes = new W24S6McpOperationEnvelopePolicy(ProjectHash).Validate(envelope, envelope.PlanHash).Errors.Select(error => error.Code).ToArray();
            Assert.That(codes, Does.Contain("W24MCP005")); Assert.That(codes, Does.Contain("W24MCP006"));
            Assert.That(codes, Does.Contain("W24MCP007")); Assert.That(codes, Does.Contain("W24MCP008"));
        }

        [TestCase("bad|request")]
        [TestCase("bad\nrequest")]
        [TestCase("Bad")]
        public void RequestId_RejectsDelimitersControlsAndNonCanonicalText(string requestId)
        {
            var envelope = Envelope(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/fireball.json");
            envelope.RequestId = requestId; Rehash(envelope);
            Assert.That(new W24S6McpOperationEnvelopePolicy(ProjectHash).Validate(envelope, envelope.PlanHash).Errors.Select(error => error.Code), Does.Contain("W24MCP003"));
        }

        [TestCase("Assets/VFX/Recipes/../outside.json")]
        [TestCase("Assets/VFX/Recipes/bad|name.json")]
        [TestCase("Assets/VFX/Recipes/bad\nname.json")]
        [TestCase("\\\\server\\share\\recipe.json")]
        public void TargetPath_RejectsTraversalDelimitersControlsAndUnc(string path)
        {
            var envelope = Envelope(W24S6McpOperationKind.ParseRecipeSyntax, path);
            Assert.That(new W24S6McpOperationEnvelopePolicy(ProjectHash).Validate(envelope, envelope.PlanHash).Errors.Select(error => error.Code), Does.Contain("W24MCP018"));
        }

        [Test]
        public void PlanHash_IsLengthPrefixedAndCannotAliasDelimiterPartitioning()
        {
            var left = Envelope(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/a.json");
            var right = Envelope(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/a.json");
            left.Operations[0].OperationId = "a|b"; left.Operations[0].TargetPath = "c";
            right.Operations[0].OperationId = "a"; right.Operations[0].TargetPath = "b|c";
            Assert.That(W24S6McpOperationEnvelopePolicy.ComputePlanHash(left), Is.Not.EqualTo(W24S6McpOperationEnvelopePolicy.ComputePlanHash(right)));
        }

        [Test]
        public void EnvelopeV2_UsesExactCamelCaseStringEnumsAndRoundTripsAgainstSchema()
        {
            var envelope=Envelope(W24S6McpOperationKind.InspectManifestHeader,"ProjectSettings/VFXComposer/BuildManifests/fire.manifest.json");
            envelope.RequestedAuthority=W24S6McpAuthority.ReadOnly;Rehash(envelope);
            var json=envelope.ToJson();var root=W24StrictJsonText.ParseObject(json,"envelope test");
            var schema=JObject.Parse(File.ReadAllText(SchemaPath("w24-s6-mcp-operation-envelope-v2.schema.json")));
            CollectionAssert.AreEquivalent(((JArray)schema["required"]).Values<string>(),root.Properties().Select(value=>value.Name));
            var operation=(JObject)((JArray)root["operations"])[0];
            CollectionAssert.AreEquivalent(((JArray)schema.SelectToken("$defs.operation.required")).Values<string>(),operation.Properties().Select(value=>value.Name));
            Assert.That((string)root["executionMode"],Is.EqualTo((string)schema.SelectToken("properties.executionMode.const")));
            Assert.That((string)root["rollbackMode"],Is.EqualTo((string)schema.SelectToken("properties.rollbackMode.const")));
            Assert.That(((JArray)schema.SelectToken("properties.requestedAuthority.enum")).Values<string>(),Does.Contain((string)root["requestedAuthority"]));
            Assert.That(((JArray)schema.SelectToken("$defs.operation.properties.kind.enum")).Values<string>(),Does.Contain((string)operation["kind"]));
            var roundTrip=W24S6McpOperationEnvelope.FromJson(json);
            Assert.That(roundTrip.PlanHash,Is.EqualTo(envelope.PlanHash));Assert.That(roundTrip.RequestedAuthority,Is.EqualTo(W24S6McpAuthority.ReadOnly));
            Assert.That(roundTrip.Operations.Single().Kind,Is.EqualTo(W24S6McpOperationKind.InspectManifestHeader));
            Assert.That(W24S6McpOperationEnvelopePolicy.ComputePlanHash(roundTrip),Is.EqualTo(envelope.PlanHash));
        }

        [Test]
        public void EnvelopeV2_FromJsonRejectsMissingExtraWrongTypeAndNonSchemaEnums()
        {
            var json=Envelope(W24S6McpOperationKind.ParseRecipeSyntax,"Assets/VFX/Recipes/fire.json").ToJson();
            var missing=JObject.Parse(json);missing.Remove("requestId");
            Assert.Throws<JsonSerializationException>(()=>W24S6McpOperationEnvelope.FromJson(missing.ToString(Formatting.None)));
            var extra=JObject.Parse(json);extra["callerAuthority"]="forged";
            Assert.Throws<JsonSerializationException>(()=>W24S6McpOperationEnvelope.FromJson(extra.ToString(Formatting.None)));
            var wrongType=JObject.Parse(json);wrongType["executionMode"]=0;
            Assert.Throws<JsonSerializationException>(()=>W24S6McpOperationEnvelope.FromJson(wrongType.ToString(Formatting.None)));
            var forbiddenEnum=JObject.Parse(json);forbiddenEnum["requestedAuthority"]="Migration";
            Assert.Throws<JsonSerializationException>(()=>W24S6McpOperationEnvelope.FromJson(forbiddenEnum.ToString(Formatting.None)));
            var forbiddenToken=JObject.Parse(json);forbiddenToken["approvalToken"]="caller-token";
            Assert.Throws<JsonSerializationException>(()=>W24S6McpOperationEnvelope.FromJson(forbiddenToken.ToString(Formatting.None)));
            var wrongOperation=JObject.Parse(json);wrongOperation.SelectToken("operations[0].kind").Replace(new JValue(0));
            Assert.Throws<JsonSerializationException>(()=>W24S6McpOperationEnvelope.FromJson(wrongOperation.ToString(Formatting.None)));
            var invalidOutput=Envelope(W24S6McpOperationKind.ParseRecipeSyntax,"Assets/VFX/Recipes/fire.json");invalidOutput.ExecutionMode=W24S6McpExecutionMode.Apply;Rehash(invalidOutput);
            Assert.Throws<JsonSerializationException>(()=>invalidOutput.ToJson());
        }

        [Test]
        public void Inspector_ReturnsExactNonAuthorityResultForRecipeSyntax()
        {
            var bytes = Encoding.UTF8.GetBytes("{\"recipeVersion\":1}");
            var requestJson = new JObject
            {
                ["schemaVersion"] = W24S6LocalDocumentInspector.RequestSchema, ["operationKind"] = "ParseRecipeSyntax",
                ["targetPath"] = "Assets/VFX/Recipes/sample.json", ["expectedInputHash"] = W24S6LocalDocumentInspector.Hash(bytes),
                ["documentBytes"] = Convert.ToBase64String(bytes)
            }.ToString();
            var result = new W24S6LocalDocumentInspector().Inspect(W24S6LocalInspectionRequest.FromJson(requestJson));
            var json = JObject.Parse(result.ToJson());
            Assert.That(result.Classification, Is.EqualTo("document-valid"));
            Assert.That((string)json["authority"], Is.EqualTo("none")); Assert.That((bool)json["machineGatePassed"], Is.False);
            Assert.That((string)json["scope"], Is.EqualTo(W24S6LocalDocumentInspector.Scope));
            Assert.That(json.Properties().Select(property => property.Name), Does.Not.Contain("passed").And.Not.Contain("succeeded"));
            Assert.That((string)json["classification"], Is.EqualTo("document-valid"));
            var extra=JObject.Parse(requestJson);extra["unknown"]=true;
            Assert.Throws<JsonSerializationException>(() => W24S6LocalInspectionRequest.FromJson(extra.ToString()));
            var wrongSchemaType=JObject.Parse(requestJson);wrongSchemaType["schemaVersion"]=new JObject();
            Assert.Throws<JsonSerializationException>(() => W24S6LocalInspectionRequest.FromJson(wrongSchemaType.ToString(Formatting.None)));
        }

        [Test]
        public void Inspector_HashMismatchBadUtf8AndLimit_AreRejected()
        {
            var inspector = new W24S6LocalDocumentInspector(); var bytes = Encoding.UTF8.GetBytes("{}");
            var mismatch = inspector.Inspect(new W24S6LocalInspectionRequest(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/a.json", InputHash, bytes));
            Assert.That(mismatch.Diagnostics.Select(value => value.Code), Does.Contain("W24INS005"));
            var badUtf8 = new byte[] { 0xc3, 0x28 };
            Assert.That(Inspect(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/a.json", badUtf8).Diagnostics.Select(value => value.Code), Does.Contain("W24INS006"));
            var oversized = new byte[W24S6LocalDocumentInspector.MaximumDocumentBytes + 1];
            Assert.That(Inspect(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/a.json", oversized).Diagnostics.Select(value => value.Code), Does.Contain("W24INS004"));
        }

        [Test]
        public void Inspector_ManifestHeaderRejectsObjectArrayNullAndNumberTokenMatrixWithoutThrowing()
        {
            var replacements = new[] { "{}", "[]", "null", "17" };
            foreach (var field in new[] { "effectId", "buildHash", "runtimeEntry", "runtimeEntry.path" })
            foreach (var replacement in replacements)
            {
                var manifest = new JObject
                {
                    ["effectId"] = "fire",
                    ["buildHash"] = new string('a', 64),
                    ["runtimeEntry"] = new JObject { ["path"] = "Assets/VFX/Generated/fire/VFX_fire.prefab" }
                };
                if (field == "runtimeEntry.path") ((JObject)manifest["runtimeEntry"])["path"] = JToken.Parse(replacement);
                else manifest[field] = JToken.Parse(replacement);
                var result = Inspect(W24S6McpOperationKind.InspectManifestHeader,
                    "ProjectSettings/VFXComposer/BuildManifests/fire.manifest.json",
                    Encoding.UTF8.GetBytes(manifest.ToString(Formatting.None)));
                Assert.That(result.Classification, Is.EqualTo("document-invalid"), field + "=" + replacement);
                Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("W24INS023"), field + "=" + replacement);
                Assert.That(result.Diagnostics.Select(value => value.Message), Is.All.EqualTo("Manifest header fields have invalid JSON token shapes."));
            }
        }

        [Test]
        public void Inspector_ContractRejectsRecursiveObjectArrayNullStringBooleanAndNumberMatrixWithoutThrowing()
        {
            var mutations = new Dictionary<string, string[]>
            {
                ["effectId"] = new[] { "{}", "[]", "null", "17" },
                ["contractRevision"] = new[] { "{}", "[]", "null", "\"wrong\"", "1.5" },
                ["lifecycle"] = new[] { "[]", "null", "17", "\"wrong\"" },
                ["layers"] = new[] { "{}", "null", "17", "\"wrong\"" },
                ["lifecycle.start.deadlineSeconds"] = new[] { "{}", "[]", "null", "\"wrong\"", "true" },
                ["lifecycle.start.entryConditions[0]"] = new[] { "{}", "[]", "null", "17", "true" },
                ["layers[0]"] = new[] { "[]", "null", "17", "\"wrong\"" }
            };
            foreach (var mutation in mutations)
            foreach (var replacement in mutation.Value)
            {
                var contract = JObject.Parse(ContractSource());
                var token = contract.SelectToken(mutation.Key);
                Assert.That(token, Is.Not.Null, mutation.Key);
                token.Replace(JToken.Parse(replacement));
                var result = Inspect(W24S6McpOperationKind.ValidateContractDocument,
                    "docs/vfx-contracts/sustained_flame_3d.contract.json",
                    Encoding.UTF8.GetBytes(contract.ToString(Formatting.None)));
                Assert.That(result.Classification, Is.EqualTo("document-invalid"), mutation.Key + "=" + replacement);
                Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("W24INS009"), mutation.Key + "=" + replacement);
                Assert.That(result.Diagnostics.Select(value => value.Message), Is.All.EqualTo("Contract document fields have invalid JSON token shapes."));
            }
        }

        [TestCase("{\"a\":1,//comment\n\"b\":2}")]
        [TestCase("{\"a\":1,/*comment*/\"b\":2}")]
        [TestCase("{'a':1}")]
        [TestCase("{\"a\":1,}")]
        [TestCase("{\"a\":NaN}")]
        [TestCase("{\"a\":Infinity}")]
        [TestCase("{\"a\":1} {\"b\":2}")]
        [TestCase("{\"a\":1,\"a\":2}")]
        [TestCase("{\"a\":1,\"\\u0061\":2}")]
        [TestCase("{\"\\uD800\":1}")]
        [TestCase("{\"a\":\"\\uD800\"}")]
        public void StrictJsonPreflight_RejectsEveryForbiddenTextClass(string json)
        {
            Assert.Throws<JsonSerializationException>(()=>W24StrictJsonText.ParseObject(json,"test"));
        }

        [Test]
        public void StrictJsonPreflight_NormalizesRawEscapedAndMixedSurrogatePairsWithoutChangingNumbers()
        {
            var pureRaw="{\"a\":\""+'\uD83D'+'\uDE00'+"\"}";
            var pureEscaped="{\"a\":\"\\uD83D\\uDE00\"}";
            var rawHighEscapedLow="{\"a\":\""+'\uD83D'+"\\uDE00\"}";
            var escapedHighRawLow="{\"a\":\"\\uD83D"+'\uDE00'+"\"}";
            foreach(var source in new[]{pureRaw,pureEscaped,rawHighEscapedLow,escapedHighRawLow})
            {
                var value=(string)W24StrictJsonText.ParseObject(source,"surrogate normalization")["a"];
                Assert.That(value.Length,Is.EqualTo(2));Assert.That(value[0],Is.EqualTo('\uD83D'));Assert.That(value[1],Is.EqualTo('\uDE00'));
            }
            var mixedProperty=W24StrictJsonText.ParseObject("{\""+'\uD83D'+"\\uDE00\":1}","property surrogate").Properties().Single();
            Assert.That(mixedProperty.Name.Length,Is.EqualTo(2));Assert.That(mixedProperty.Name[0],Is.EqualTo('\uD83D'));Assert.That(mixedProperty.Name[1],Is.EqualTo('\uDE00'));
            Assert.That((string)W24StrictJsonText.ParseObject("{\"a\":\"\\\\ud800\"}","literal escape")["a"],Is.EqualTo("\\ud800"));
            var numbers=W24StrictJsonText.ParseObject("{\"integer\":1,\"fraction\":1.25,\"exponent\":1e2}","number semantics");
            Assert.That(numbers["integer"].Type,Is.EqualTo(JTokenType.Integer));Assert.That(numbers["fraction"].Type,Is.EqualTo(JTokenType.Float));Assert.That(numbers["exponent"].Type,Is.EqualTo(JTokenType.Float));
            Assert.That((long)numbers["integer"],Is.EqualTo(1L));Assert.That((decimal)numbers["fraction"],Is.EqualTo(1.25m));Assert.That((decimal)numbers["exponent"],Is.EqualTo(100m));
        }

        [Test]
        public void StrictJsonPreflight_EnforcesContainerDepthAndExplicitNodeBudget()
        {
            Assert.DoesNotThrow(()=>W24StrictJsonText.ParseObject(NestedObject(W24StrictJsonText.MaxDepth),"depth boundary"));
            Assert.Throws<JsonSerializationException>(()=>W24StrictJsonText.ParseObject(NestedObject(W24StrictJsonText.MaxDepth+1),"depth overflow"));
            Assert.Throws<JsonSerializationException>(()=>W24StrictJsonText.ParseObjectForTests("{\"a\":1,\"b\":2}","node overflow",W24StrictJsonText.MaxDepth,4));
        }

        [Test]
        public void FourDocumentEntrypoints_RejectMoreThanSixtyFourContainers()
        {
            var request=new JObject{{"schemaVersion",W24S6LocalDocumentInspector.RequestSchema},{"operationKind","ParseRecipeSyntax"},{"targetPath","Assets/VFX/Recipes/a.json"},{"expectedInputHash",InputHash},{"documentBytes","e30="}}.ToString(Formatting.None);
            request=request.Insert(request.Length-1,",\"deep\":"+NestedObject(W24StrictJsonText.MaxDepth));
            Assert.Throws<JsonSerializationException>(()=>W24S6LocalInspectionRequest.FromJson(request));
            Assert.That(Inspect(W24S6McpOperationKind.ParseRecipeSyntax,"Assets/VFX/Recipes/a.json",Encoding.UTF8.GetBytes(NestedObject(W24StrictJsonText.MaxDepth+1))).Classification,Is.EqualTo("document-invalid"));
            var contractSource=ContractSource();contractSource=contractSource.Insert(contractSource.IndexOf('{')+1,"\"deep\":"+NestedObject(W24StrictJsonText.MaxDepth)+",");
            VfxDesignContract contract;Assert.That(VfxDesignContractJson.ValidateJson(contractSource,out contract).HasErrors,Is.True);
            var manifest="{\"effectId\":\"a\",\"buildHash\":\""+new string('a',64)+"\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/a/VFX_a.prefab\"},\"deep\":"+NestedObject(W24StrictJsonText.MaxDepth)+"}";
            Assert.That(Inspect(W24S6McpOperationKind.InspectManifestHeader,"ProjectSettings/VFXComposer/BuildManifests/a.manifest.json",Encoding.UTF8.GetBytes(manifest)).Classification,Is.EqualTo("document-invalid"));
        }

        [Test]
        public void RequestRecipeContractAndManifest_AllUseStrictPreflight()
        {
            var request="{'schemaVersion':'"+W24S6LocalDocumentInspector.RequestSchema+"'}";
            Assert.Throws<JsonSerializationException>(()=>W24S6LocalInspectionRequest.FromJson(request));
            var recipe=Inspect(W24S6McpOperationKind.ParseRecipeSyntax,"Assets/VFX/Recipes/a.json",Encoding.UTF8.GetBytes("{\"a\":1,}"));
            Assert.That(recipe.Classification,Is.EqualTo("document-invalid"));
            VfxDesignContract contract;Assert.That(VfxDesignContractJson.ValidateJson(ContractSource()+" {}",out contract).HasErrors,Is.True);
            var manifest=Inspect(W24S6McpOperationKind.InspectManifestHeader,"ProjectSettings/VFXComposer/BuildManifests/a.manifest.json",Encoding.UTF8.GetBytes("{\"effectId\":\"a\",\"effectId\":\"a\"}"));
            Assert.That(manifest.Classification,Is.EqualTo("document-invalid"));
        }

        [Test]
        public void ManifestRuntimeEntry_RequiresCanonicalEffectOwnedSegments()
        {
            Assert.That(W24S6LocalDocumentInspector.IsExactManifestRuntimeEntry("Assets/VFX/Generated/fire/a/VFX_fire.prefab","fire"),Is.True);
            foreach(var path in new[]{"Assets\\VFX\\Generated\\fire\\a.prefab","C:/Assets/VFX/Generated/fire/a.prefab","Assets/VFX/Generated/fire//a.prefab","Assets/VFX/Generated/fire/./a.prefab","Assets/VFX/Generated/fire/../a.prefab","Assets/VFX/Generated/other/a.prefab","Assets/VFX/Generated/fire/","Assets/VFX/Generated/fire/a.txt","Assets/VFX/Generated/fire/a|b.prefab","Assets/VFX/Generated/fire/a\n.prefab"})
            {
                Assert.That(W24S6LocalDocumentInspector.IsExactManifestRuntimeEntry(path,"fire"),Is.False,path);
                var invalid=Encoding.UTF8.GetBytes(new JObject{{"effectId","fire"},{"buildHash",new string('a',64)},{"runtimeEntry",new JObject{{"path",path}}}}.ToString(Newtonsoft.Json.Formatting.None));
                Assert.That(Inspect(W24S6McpOperationKind.InspectManifestHeader,"ProjectSettings/VFXComposer/BuildManifests/fire.manifest.json",invalid).Classification,Is.EqualTo("document-invalid"),path);
            }
            var valid=Encoding.UTF8.GetBytes("{\"effectId\":\"fire\",\"buildHash\":\""+new string('a',64)+"\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/fire/a/VFX_fire.prefab\"}}");
            Assert.That(Inspect(W24S6McpOperationKind.InspectManifestHeader,"ProjectSettings/VFXComposer/BuildManifests/fire.manifest.json",valid).Classification,Is.EqualTo("document-valid"));
            var wrongEffect=Encoding.UTF8.GetBytes("{\"effectId\":\"other\",\"buildHash\":\""+new string('a',64)+"\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/fire/a/VFX_fire.prefab\"}}");
            var wrongEffectResult=Inspect(W24S6McpOperationKind.InspectManifestHeader,"ProjectSettings/VFXComposer/BuildManifests/fire.manifest.json",wrongEffect);
            Assert.That(wrongEffectResult.Diagnostics.Select(value=>value.Code),Does.Contain("W24INS020"));
        }

        [Test]
        public void RejectedResultRedactsTargetAndRuntimePropertySetsMatchSchemas()
        {
            var bytes=Encoding.UTF8.GetBytes("{}");
            var rejected=new W24S6LocalDocumentInspector().Inspect(new W24S6LocalInspectionRequest(W24S6McpOperationKind.ParseRecipeSyntax,"Assets/VFX/Recipes/a.json",InputHash,bytes));
            var result=JObject.Parse(rejected.ToJson());Assert.That(result["targetPath"].Type,Is.EqualTo(JTokenType.Null));
            var resultSchema=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","schemas","w24-s6-local-document-inspection-result-v1.schema.json"))));
            CollectionAssert.AreEquivalent(((JObject)resultSchema["properties"]).Properties().Select(value=>value.Name),result.Properties().Select(value=>value.Name));
            CollectionAssert.AreEquivalent(((JArray)resultSchema["required"]).Values<string>(),result.Properties().Select(value=>value.Name));
            var requestSchema=JObject.Parse(File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","schemas","w24-s6-local-document-inspection-request-v1.schema.json"))));
            CollectionAssert.AreEquivalent(((JObject)requestSchema["properties"]).Properties().Select(value=>value.Name),new[]{"schemaVersion","operationKind","targetPath","expectedInputHash","documentBytes"});
            CollectionAssert.AreEquivalent(((JArray)requestSchema["required"]).Values<string>(),new[]{"schemaVersion","operationKind","targetPath","expectedInputHash","documentBytes"});
            CollectionAssert.AreEquivalent(((JArray)requestSchema.SelectToken("properties.operationKind.enum")).Values<string>(),Enum.GetNames(typeof(W24S6McpOperationKind)));
            CollectionAssert.AreEquivalent(((JArray)resultSchema.SelectToken("properties.operationKind.enum")).Where(value=>value.Type==JTokenType.String).Values<string>(),Enum.GetNames(typeof(W24S6McpOperationKind)));
            Assert.That((int)requestSchema.SelectToken("properties.documentBytes.maxLength"),Is.EqualTo(W24S6LocalDocumentInspector.MaximumBase64Characters));
            Assert.That((int)requestSchema["x-maxJsonCharacters"],Is.EqualTo(W24S6LocalDocumentInspector.MaximumRequestJsonCharacters));
        }

        [Test]
        public void RequestRejectsOversizedBase64BeforeDecode()
        {
            var request=new JObject{{"schemaVersion",W24S6LocalDocumentInspector.RequestSchema},{"operationKind","ParseRecipeSyntax"},{"targetPath","Assets/VFX/Recipes/a.json"},{"expectedInputHash",InputHash},{"documentBytes",new string('A',W24S6LocalDocumentInspector.MaximumBase64Characters+1)}};
            Assert.Throws<JsonSerializationException>(()=>W24S6LocalInspectionRequest.FromJson(request.ToString(Newtonsoft.Json.Formatting.None)));
            request["documentBytes"]="not-base64";Assert.Throws<JsonSerializationException>(()=>W24S6LocalInspectionRequest.FromJson(request.ToString(Newtonsoft.Json.Formatting.None)));
        }

        [Test]
        public void RequestRejectsOversizedRawJsonBeforeStrictParsing()
        {
            Assert.Throws<JsonSerializationException>(()=>W24S6LocalInspectionRequest.FromJson(new string(' ',W24S6LocalDocumentInspector.MaximumRequestJsonCharacters+1)));
        }

        [Test]
        public void Inspector_RequestAndResultAreImmutableAndHaveNoFilesystemJunctionSurface()
        {
            var bytes = Encoding.UTF8.GetBytes("{}");
            var request = new W24S6LocalInspectionRequest(W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/a.json", W24S6LocalDocumentInspector.Hash(bytes), bytes);
            bytes[0] = (byte)'x'; var result = new W24S6LocalDocumentInspector().Inspect(request);
            Assert.That(result.Classification, Is.EqualTo("document-valid"));
            Assert.That(typeof(W24S6LocalDocumentInspector).GetConstructors().SelectMany(value => value.GetParameters()).Any(value => value.ParameterType == typeof(string) && value.Name.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            Assert.That(typeof(W24S6LocalDocumentInspector).Assembly.GetType("VFXComposer.Editor.W24.S6.External.W24S6LocalReadOnlyExecutor"), Is.Null);
            var unknown=new W24S6LocalDocumentInspector().Inspect(new W24S6LocalInspectionRequest((W24S6McpOperationKind)999,"Assets/VFX/Recipes/a.json",W24S6LocalDocumentInspector.Hash(bytes),bytes));
            var unknownJson=JObject.Parse(unknown.ToJson());Assert.That(unknown.Classification,Is.EqualTo("rejected"));Assert.That(unknownJson["operationKind"].Type,Is.EqualTo(JTokenType.Null));Assert.That(unknownJson["targetPath"].Type,Is.EqualTo(JTokenType.Null));
        }

        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("-Infinity")]
        [TestCase("1e999")]
        public void ContractStrictParser_RejectsNonFiniteNumbers(string number)
        {
            var source = ContractSource(); var marker = "\"deadlineSeconds\": 0.35"; Assert.That(source, Does.Contain(marker));
            VfxDesignContract contract;
            Assert.That(VfxDesignContractJson.ValidateJson(source.Replace(marker, "\"deadlineSeconds\": " + number), out contract).HasErrors, Is.True);
        }

        [Test]
        public void ContractStrictParser_RejectsNestedDuplicateAndIsolatedSurrogate()
        {
            var source = ContractSource(); VfxDesignContract contract;
            Assert.That(VfxDesignContractJson.ValidateJson(source.Replace("\"lifecycle\": {", "\"lifecycle\": {\"kind\":\"duplicate\","), out contract).HasErrors, Is.True);
            Assert.That(VfxDesignContractJson.ValidateJson(source.Replace("sustained_flame_3d", "sustained_\ud800_flame_3d"), out contract).HasErrors, Is.True);
        }

        private static string ContractSource() { return File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "vfx-contracts", "sustained_flame_3d.contract.json"))); }
        private static string SchemaPath(string name) { return Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","docs","schemas",name)); }
        private static string NestedObject(int containerCount) { var value="0";for(var index=0;index<containerCount;index++)value="{\"x\":"+value+"}";return value; }
        private static W24S6LocalInspectionResult Inspect(W24S6McpOperationKind kind, string path, byte[] bytes) { return new W24S6LocalDocumentInspector().Inspect(new W24S6LocalInspectionRequest(kind, path, W24S6LocalDocumentInspector.Hash(bytes), bytes)); }
        private static W24S6McpOperationEnvelope Envelope(W24S6McpOperationKind kind, string path) { var envelope = new W24S6McpOperationEnvelope { RequestId = "s6-structure-only", ProjectIdentityHash = ProjectHash, Operations = new[] { new W24S6McpOperation { OperationId = "operation-1", Kind = kind, TargetPath = path, ExpectedInputHash = InputHash } } }; Rehash(envelope); return envelope; }
        private static void Rehash(W24S6McpOperationEnvelope envelope) { envelope.PlanHash = W24S6McpOperationEnvelopePolicy.ComputePlanHash(envelope); }
    }
}
