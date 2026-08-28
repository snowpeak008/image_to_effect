using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.W24.S1;

namespace VFXComposer.Editor.W24.S6.External
{
    /// <summary>
    /// Pure in-memory document helper. It has no filesystem, transport, Unity, process, shell,
    /// network, reviewed-plan, execution-ticket, write, gate, or visual authority.
    /// </summary>
    public sealed class W24S6LocalDocumentInspector
    {
        public const string RequestSchema = "w24-s6/local-document-inspection-request-v1";
        public const string ResultSchema = "w24-s6/local-document-inspection-result-v1";
        public const string Scope = "syntax-and-document-inspection-only";
        public const int MaximumDocumentBytes = 4 * 1024 * 1024;
        public const int MaximumBase64Characters = ((MaximumDocumentBytes + 2) / 3) * 4;
        public const int MaximumRequestEnvelopeCharacters = 4096;
        public const int MaximumRequestJsonCharacters = MaximumBase64Characters + MaximumRequestEnvelopeCharacters;

        public W24S6LocalInspectionResult Inspect(W24S6LocalInspectionRequest request)
        {
            var diagnostics = new List<W24S6LocalInspectionDiagnostic>();
            if (request == null)
                return Result(null, null, null, 0, "rejected", diagnostics, "W24INS001", "/", "Inspection request is required.");
            if (!W24S6McpOperationEnvelopePolicy.IsCanonicalSha256(request.ExpectedInputHash))
                return Result(request.OperationKind, request.TargetPath, null, request.DocumentBytes.Count, "rejected", diagnostics, "W24INS002", "/expectedInputHash", "Expected hash must be canonical sha256.");
            if (!IsExactTarget(request.OperationKind, request.TargetPath))
                return Result(request.OperationKind, request.TargetPath, null, request.DocumentBytes.Count, "rejected", diagnostics, "W24INS003", "/targetPath", "Target path is not canonical for this document operation.");
            if (request.DocumentBytes.Count > MaximumDocumentBytes)
                return Result(request.OperationKind, request.TargetPath, null, request.DocumentBytes.Count, "rejected", diagnostics, "W24INS004", "/documentBytes", "Document exceeds the 4 MiB inspection limit.");

            var bytes = request.CopyBytes();
            var actualHash = Hash(bytes);
            if (!string.Equals(actualHash, request.ExpectedInputHash, StringComparison.Ordinal))
                return Result(request.OperationKind, request.TargetPath, actualHash, bytes.Length, "rejected", diagnostics, "W24INS005", "/expectedInputHash", "Document bytes do not match the declared hash.");
            string text;
            try { text = DecodeUtf8(bytes); }
            catch (DecoderFallbackException)
            {
                return Result(request.OperationKind, request.TargetPath, actualHash, bytes.Length, "rejected", diagnostics,
                    "W24INS006", "/documentBytes", "Document bytes are not strict UTF-8.");
            }

            try
            {
                switch (request.OperationKind)
                {
                    case W24S6McpOperationKind.ParseRecipeSyntax:
                        W24StrictJsonText.ParseObject(text,"Recipe JSON");
                        break;
                    case W24S6McpOperationKind.ValidateContractDocument:
                        var contractRoot = W24StrictJsonText.ParseObject(text, "Contract JSON");
                        if (!HasExpectedContractTokenShape(contractRoot, typeof(VfxDesignContract)))
                        {
                            diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS009", "/documentBytes",
                                "Contract document fields have invalid JSON token shapes."));
                            break;
                        }
                        VfxDesignContract contract;
                        var report = VfxDesignContractJson.ValidateJson(text, out contract);
                        diagnostics.AddRange(report.Issues.Where(issue => issue.Severity == W24GateSeverity.Error).Select(issue =>
                            new W24S6LocalInspectionDiagnostic(string.IsNullOrEmpty(issue.Code) ? "W24INS010" : issue.Code,
                                "/documentBytes", "Contract document failed schema or semantic validation.")));
                        break;
                    case W24S6McpOperationKind.InspectManifestHeader:
                        InspectManifest(text, request.TargetPath, diagnostics);
                        break;
                    default:
                        diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS007", "/operationKind", "Operation is not in the document-inspection allowlist."));
                        break;
                }
            }
            catch (Exception e) when (IsExpectedDocumentFailure(e))
            {
                diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS008", "/documentBytes",
                    "Document JSON is malformed or contains an invalid token conversion."));
            }
            return new W24S6LocalInspectionResult(request.OperationKind.ToString(), request.TargetPath, actualHash, bytes.Length,
                diagnostics.Count == 0 ? "document-valid" : "document-invalid", diagnostics);
        }

        private static void InspectManifest(string text, string targetPath, List<W24S6LocalInspectionDiagnostic> diagnostics)
        {
            var manifest = W24StrictJsonText.ParseObject(text,"Manifest JSON");
            var fileName = targetPath.Substring(targetPath.LastIndexOf('/') + 1);
            var effectId = fileName.Substring(0, fileName.Length - ".manifest.json".Length);
            var effectToken = manifest["effectId"];
            var buildHashToken = manifest["buildHash"];
            var runtimeEntry = manifest["runtimeEntry"] as JObject;
            var runtimePathToken = runtimeEntry == null ? null : runtimeEntry["path"];
            if (effectToken == null || effectToken.Type != JTokenType.String || buildHashToken == null
                || buildHashToken.Type != JTokenType.String || runtimeEntry == null || runtimePathToken == null
                || runtimePathToken.Type != JTokenType.String)
            {
                diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS023", "/documentBytes",
                    "Manifest header fields have invalid JSON token shapes."));
                return;
            }
            var declaredEffectId = effectToken.Value<string>();
            if (!string.Equals(declaredEffectId, effectId, StringComparison.Ordinal)) diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS020", "/effectId", "Manifest effectId does not match its declared target name."));
            var buildHash = buildHashToken.Value<string>();
            if (buildHash == null || buildHash.Length != 64 || buildHash.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))) diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS021", "/buildHash", "Manifest buildHash must be 64 lowercase hexadecimal characters."));
            var runtimePath = runtimePathToken.Value<string>();
            if (!IsExactManifestRuntimeEntry(runtimePath,effectId)) diagnostics.Add(new W24S6LocalInspectionDiagnostic("W24INS022", "/runtimeEntry/path", "Manifest Runtime Entry must be a canonical effect-owned prefab path."));
        }

        private static bool HasExpectedContractTokenShape(JToken token, Type expectedType)
        {
            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined) return false;
            if (expectedType.IsArray)
            {
                var array = token as JArray;
                return array != null && array.All(item => HasExpectedContractTokenShape(item, expectedType.GetElementType()));
            }
            if (expectedType == typeof(string)) return token.Type == JTokenType.String;
            if (expectedType == typeof(bool)) return token.Type == JTokenType.Boolean;
            if (expectedType == typeof(int) || expectedType == typeof(uint)) return token.Type == JTokenType.Integer;
            if (expectedType == typeof(double)) return token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
            if (expectedType == typeof(JObject)) return token.Type == JTokenType.Object;

            var value = token as JObject;
            if (value == null || !expectedType.IsClass) return false;
            var fields = expectedType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Select(field => new
                {
                    Field = field,
                    Property = field.GetCustomAttributes(typeof(JsonPropertyAttribute), false).Cast<JsonPropertyAttribute>().SingleOrDefault()
                })
                .Where(item => item.Property != null)
                .ToDictionary(item => item.Property.PropertyName, item => item.Field.FieldType, StringComparer.Ordinal);
            foreach (var property in value.Properties())
            {
                Type propertyType;
                if (!fields.TryGetValue(property.Name, out propertyType) || !HasExpectedContractTokenShape(property.Value, propertyType)) return false;
            }
            return true;
        }

        private static bool IsExpectedDocumentFailure(Exception value)
        {
            return value is JsonException || value is FormatException || value is OverflowException
                || value is ArgumentException || value is InvalidCastException || value is InvalidOperationException;
        }

        internal static bool IsExactManifestRuntimeEntry(string path,string effectId)
        {
            if(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(effectId)||path.IndexOf('\\')>=0||path.IndexOf(':')>=0||path.IndexOfAny(new[]{'*','?','\"','<','>','|'})>=0||path.Any(char.IsControl)||path.Contains("//"))return false;
            var segments=path.Split('/');if(segments.Length<5||segments.Any(segment=>segment.Length==0||segment=="."||segment==".."))return false;
            return segments[0]=="Assets"&&segments[1]=="VFX"&&segments[2]=="Generated"&&segments[3]==effectId&&segments[segments.Length-1].EndsWith(".prefab",StringComparison.Ordinal)&&segments[segments.Length-1].Length>".prefab".Length;
        }

        private static bool IsExactTarget(W24S6McpOperationKind kind, string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 260 || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 || path.IndexOf('|') >= 0 || path.Any(char.IsControl) || path.StartsWith("/", StringComparison.Ordinal) || path.Contains("//")) return false;
            if (path.Split('/').Any(segment => segment.Length == 0 || segment == "." || segment == "..")) return false;
            switch (kind)
            {
                case W24S6McpOperationKind.ParseRecipeSyntax: return path.StartsWith("Assets/VFX/Recipes/", StringComparison.Ordinal) && path.EndsWith(".json", StringComparison.Ordinal);
                case W24S6McpOperationKind.ValidateContractDocument: return path.StartsWith("docs/vfx-contracts/", StringComparison.Ordinal) && path.EndsWith(".contract.json", StringComparison.Ordinal);
                case W24S6McpOperationKind.InspectManifestHeader: return path.StartsWith("ProjectSettings/VFXComposer/BuildManifests/", StringComparison.Ordinal) && path.EndsWith(".manifest.json", StringComparison.Ordinal);
                default: return false;
            }
        }

        private static W24S6LocalInspectionResult Result(W24S6McpOperationKind? kind, string targetPath, string hash, long bytes, string classification, List<W24S6LocalInspectionDiagnostic> diagnostics, string code, string field, string message)
        {
            diagnostics.Add(new W24S6LocalInspectionDiagnostic(code, field, message));
            var operationName=kind.HasValue&&Enum.IsDefined(typeof(W24S6McpOperationKind),kind.Value)?kind.Value.ToString():null;
            return new W24S6LocalInspectionResult(operationName, classification=="rejected"?null:targetPath, hash, bytes, classification, diagnostics);
        }

        private static string DecodeUtf8(byte[] bytes)
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return text.Length > 0 && text[0] == '\ufeff' ? text.Substring(1) : text;
        }

        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(bytes ?? Array.Empty<byte>())).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    public sealed class W24S6LocalInspectionRequest
    {
        private readonly byte[] documentBytes;
        public string SchemaVersion { get { return W24S6LocalDocumentInspector.RequestSchema; } }
        public W24S6McpOperationKind OperationKind { get; }
        public string TargetPath { get; }
        public string ExpectedInputHash { get; }
        public IReadOnlyList<byte> DocumentBytes { get { return Array.AsReadOnly(documentBytes); } }

        internal W24S6LocalInspectionRequest(W24S6McpOperationKind operationKind, string targetPath, string expectedInputHash, byte[] documentBytes)
        {
            OperationKind = operationKind;
            TargetPath = targetPath;
            ExpectedInputHash = expectedInputHash;
            this.documentBytes = documentBytes == null ? Array.Empty<byte>() : (byte[])documentBytes.Clone();
        }

        internal byte[] CopyBytes() { return (byte[])documentBytes.Clone(); }

        public static W24S6LocalInspectionRequest FromJson(string json)
        {
            if(json==null||json.Length>W24S6LocalDocumentInspector.MaximumRequestJsonCharacters)throw new JsonSerializationException("Inspection request JSON exceeds the bounded 4 MiB document envelope before parsing.");
            var root=W24StrictJsonText.ParseObject(json,"Inspection request JSON");
            var expected=new[]{"schemaVersion","operationKind","targetPath","expectedInputHash","documentBytes"};
            if(root.Properties().Any(property=>!expected.Contains(property.Name,StringComparer.Ordinal))||expected.Any(name=>root[name]==null))throw new JsonSerializationException("Inspection request must have the exact v1 property set.");
            if(root["schemaVersion"].Type!=JTokenType.String||!string.Equals((string)root["schemaVersion"],W24S6LocalDocumentInspector.RequestSchema,StringComparison.Ordinal))throw new JsonSerializationException("Unknown inspection request schema.");
            W24S6McpOperationKind kind;if(root["operationKind"].Type!=JTokenType.String||!Enum.TryParse((string)root["operationKind"],false,out kind)||!Enum.IsDefined(typeof(W24S6McpOperationKind),kind))throw new JsonSerializationException("Unknown document inspection operation.");
            if(root["targetPath"].Type!=JTokenType.String||root["expectedInputHash"].Type!=JTokenType.String||root["documentBytes"].Type!=JTokenType.String)throw new JsonSerializationException("Inspection request fields have invalid JSON types.");
            var encoded=(string)root["documentBytes"];if(encoded.Length>W24S6LocalDocumentInspector.MaximumBase64Characters)throw new JsonSerializationException("documentBytes exceeds the encoded 4 MiB limit.");
            byte[] bytes;try{bytes=Convert.FromBase64String(encoded);}catch(FormatException e){throw new JsonSerializationException("documentBytes must be canonical base64.",e);}
            if(!string.Equals(Convert.ToBase64String(bytes),encoded,StringComparison.Ordinal))throw new JsonSerializationException("documentBytes must use canonical padded base64.");
            return new W24S6LocalInspectionRequest(kind,(string)root["targetPath"],(string)root["expectedInputHash"],bytes);
        }
    }

    public sealed class W24S6LocalInspectionDiagnostic
    {
        public string Code { get; }
        public string Field { get; }
        public string Message { get; }
        internal W24S6LocalInspectionDiagnostic(string code, string field, string message) { Code = code; Field = field; Message = message; }
        internal JObject ToJson() { return new JObject { ["code"] = Code, ["field"] = Field, ["message"] = Message }; }
    }

    public sealed class W24S6LocalInspectionResult
    {
        private readonly W24S6LocalInspectionDiagnostic[] diagnostics;
        public string SchemaVersion { get { return W24S6LocalDocumentInspector.ResultSchema; } }
        public string Authority { get { return "none"; } }
        public bool MachineGatePassed { get { return false; } }
        public string Scope { get { return W24S6LocalDocumentInspector.Scope; } }
        public string OperationKind { get; }
        public string TargetPath { get; }
        public string InputSha256 { get; }
        public long InputBytes { get; }
        public string Classification { get; }
        public IReadOnlyList<W24S6LocalInspectionDiagnostic> Diagnostics { get { return Array.AsReadOnly(diagnostics); } }

        internal W24S6LocalInspectionResult(string operationKind, string targetPath, string inputSha256, long inputBytes, string classification, IEnumerable<W24S6LocalInspectionDiagnostic> diagnostics)
        {
            OperationKind = operationKind; TargetPath = targetPath; InputSha256 = inputSha256; InputBytes = inputBytes; Classification = classification;
            this.diagnostics = (diagnostics ?? Enumerable.Empty<W24S6LocalInspectionDiagnostic>()).ToArray();
        }

        public string ToJson()
        {
            return new JObject
            {
                ["schemaVersion"] = SchemaVersion, ["authority"] = Authority, ["machineGatePassed"] = MachineGatePassed,
                ["scope"] = Scope, ["operationKind"] = OperationKind == null ? JValue.CreateNull() : new JValue(OperationKind),
                ["targetPath"] = TargetPath == null ? JValue.CreateNull() : new JValue(TargetPath),
                ["inputSha256"] = InputSha256 == null ? JValue.CreateNull() : new JValue(InputSha256), ["inputBytes"] = InputBytes,
                ["classification"] = Classification, ["diagnostics"] = new JArray(diagnostics.Select(value => value.ToJson()))
            }.ToString(Formatting.Indented);
        }
    }
}
