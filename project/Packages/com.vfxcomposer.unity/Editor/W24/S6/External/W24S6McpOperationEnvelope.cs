using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VFXComposer.Editor.W24;

namespace VFXComposer.Editor.W24.S6.External
{
    /// <summary>
    /// Pure, dependency-free structural comparison for a future MCP adapter. This type deliberately
    /// has no transport, filesystem, UnityEditor, AssetDatabase, process, or execution API.
    /// Validation is not authorization to mutate the project.
    /// </summary>
    public enum W24S6McpExecutionMode { DryRun, Apply }
    public enum W24S6McpAuthority { DryRun, ReadOnly, Migration, L4Signoff, ProductionApply }
    public enum W24S6McpRollbackMode { NoWriteRequired, TransactionRollback }
    public enum W24S6McpOperationKind { ParseRecipeSyntax, ValidateContractDocument, InspectManifestHeader }

    [Serializable]
    public sealed class W24S6McpOperation
    {
        public string OperationId;
        public W24S6McpOperationKind Kind;
        public string TargetPath;
        public string ExpectedInputHash;
    }

    [Serializable]
    public sealed class W24S6McpOperationEnvelope
    {
        public const string Schema = "w24-s6/mcp-operation-envelope-v2";

        public string SchemaVersion = Schema;
        public string RequestId;
        public string ProjectIdentityHash;
        public W24S6McpExecutionMode ExecutionMode = W24S6McpExecutionMode.DryRun;
        public W24S6McpAuthority RequestedAuthority = W24S6McpAuthority.DryRun;
        public W24S6McpRollbackMode RollbackMode = W24S6McpRollbackMode.NoWriteRequired;
        // This must always remain empty. It exists so an adapter cannot silently discard a token.
        public string ApprovalToken;
        public W24S6McpOperation[] Operations = Array.Empty<W24S6McpOperation>();
        public string PlanHash;

        public static W24S6McpOperationEnvelope FromJson(string json)
        {
            var root = W24StrictJsonText.ParseObject(json, "MCP structural-comparison envelope JSON");
            RequireExactProperties(root,
                new[] { "schemaVersion", "requestId", "projectIdentityHash", "executionMode", "requestedAuthority", "rollbackMode", "operations", "planHash" },
                new[] { "approvalToken" }, "/");

            var schemaVersion = RequireString(root, "schemaVersion", "/schemaVersion");
            if (!string.Equals(schemaVersion, Schema, StringComparison.Ordinal))
                throw new JsonSerializationException("Unknown MCP structural-comparison envelope schema.");

            var executionMode = ParseExecutionMode(RequireString(root, "executionMode", "/executionMode"));
            var requestedAuthority = ParseAuthority(RequireString(root, "requestedAuthority", "/requestedAuthority"));
            var rollbackMode = ParseRollbackMode(RequireString(root, "rollbackMode", "/rollbackMode"));
            var operationsToken = root["operations"];
            if (operationsToken == null || operationsToken.Type != JTokenType.Array)
                throw new JsonSerializationException("/operations must be an array.");

            var operations = ((JArray)operationsToken).Select((token, index) => ParseOperation(token, index)).ToArray();
            string approvalToken = null;
            if (root.Property("approvalToken", StringComparison.Ordinal) != null)
                approvalToken = RequireString(root, "approvalToken", "/approvalToken");

            var envelope = new W24S6McpOperationEnvelope
            {
                SchemaVersion = schemaVersion,
                RequestId = RequireString(root, "requestId", "/requestId"),
                ProjectIdentityHash = RequireString(root, "projectIdentityHash", "/projectIdentityHash"),
                ExecutionMode = executionMode,
                RequestedAuthority = requestedAuthority,
                RollbackMode = rollbackMode,
                ApprovalToken = approvalToken,
                Operations = operations,
                PlanHash = RequireString(root, "planHash", "/planHash")
            };
            ValidateSchemaConstraints(envelope);
            return envelope;
        }

        public string ToJson()
        {
            ValidateSchemaConstraints(this);
            var root = new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["requestId"] = RequestId,
                ["projectIdentityHash"] = ProjectIdentityHash,
                ["executionMode"] = FormatExecutionMode(ExecutionMode),
                ["requestedAuthority"] = FormatAuthority(RequestedAuthority),
                ["rollbackMode"] = FormatRollbackMode(RollbackMode),
                ["operations"] = new JArray((Operations ?? Array.Empty<W24S6McpOperation>()).Select(ToJson)),
                ["planHash"] = PlanHash
            };
            if (ApprovalToken != null) root["approvalToken"] = ApprovalToken;
            return root.ToString(Formatting.Indented);
        }

        private static W24S6McpOperation ParseOperation(JToken token, int index)
        {
            var path = "/operations/" + index;
            if (token == null || token.Type != JTokenType.Object)
                throw new JsonSerializationException(path + " must be an object.");
            var root = (JObject)token;
            RequireExactProperties(root, new[] { "operationId", "kind", "targetPath", "expectedInputHash" }, Array.Empty<string>(), path);
            return new W24S6McpOperation
            {
                OperationId = RequireString(root, "operationId", path + "/operationId"),
                Kind = ParseOperationKind(RequireString(root, "kind", path + "/kind")),
                TargetPath = RequireString(root, "targetPath", path + "/targetPath"),
                ExpectedInputHash = RequireString(root, "expectedInputHash", path + "/expectedInputHash")
            };
        }

        private static JObject ToJson(W24S6McpOperation operation)
        {
            if (operation == null) return null;
            return new JObject
            {
                ["operationId"] = operation.OperationId,
                ["kind"] = FormatOperationKind(operation.Kind),
                ["targetPath"] = operation.TargetPath,
                ["expectedInputHash"] = operation.ExpectedInputHash
            };
        }

        private static string RequireString(JObject root, string name, string path)
        {
            var token = root[name];
            if (token == null || token.Type != JTokenType.String)
                throw new JsonSerializationException(path + " must be a JSON string.");
            return (string)token;
        }

        private static void RequireExactProperties(JObject root, IEnumerable<string> required, IEnumerable<string> optional, string path)
        {
            var requiredSet = new HashSet<string>(required, StringComparer.Ordinal);
            var allowed = new HashSet<string>(requiredSet, StringComparer.Ordinal);
            allowed.UnionWith(optional);
            var unknown = root.Properties().FirstOrDefault(property => !allowed.Contains(property.Name));
            if (unknown != null) throw new JsonSerializationException(path + " contains unknown property '" + unknown.Name + "'.");
            var missing = requiredSet.FirstOrDefault(name => root.Property(name, StringComparison.Ordinal) == null);
            if (missing != null) throw new JsonSerializationException(path + " is missing required property '" + missing + "'.");
        }

        private static W24S6McpExecutionMode ParseExecutionMode(string value)
        {
            if (value == "DryRun") return W24S6McpExecutionMode.DryRun;
            throw new JsonSerializationException("/executionMode must be the exact string 'DryRun'.");
        }

        private static W24S6McpAuthority ParseAuthority(string value)
        {
            if (value == "DryRun") return W24S6McpAuthority.DryRun;
            if (value == "ReadOnly") return W24S6McpAuthority.ReadOnly;
            throw new JsonSerializationException("/requestedAuthority must be an allow-listed string enum value.");
        }

        private static W24S6McpRollbackMode ParseRollbackMode(string value)
        {
            if (value == "NoWriteRequired") return W24S6McpRollbackMode.NoWriteRequired;
            throw new JsonSerializationException("/rollbackMode must be the exact string 'NoWriteRequired'.");
        }

        private static W24S6McpOperationKind ParseOperationKind(string value)
        {
            if (value == "ParseRecipeSyntax") return W24S6McpOperationKind.ParseRecipeSyntax;
            if (value == "ValidateContractDocument") return W24S6McpOperationKind.ValidateContractDocument;
            if (value == "InspectManifestHeader") return W24S6McpOperationKind.InspectManifestHeader;
            throw new JsonSerializationException("Operation kind must be an allow-listed string enum value.");
        }

        private static string FormatExecutionMode(W24S6McpExecutionMode value)
        {
            if (value == W24S6McpExecutionMode.DryRun) return "DryRun";
            throw new JsonSerializationException("Only the schema-defined DryRun execution mode can be serialized.");
        }

        private static string FormatAuthority(W24S6McpAuthority value)
        {
            if (value == W24S6McpAuthority.DryRun) return "DryRun";
            if (value == W24S6McpAuthority.ReadOnly) return "ReadOnly";
            throw new JsonSerializationException("Only schema-defined non-authority values can be serialized.");
        }

        private static string FormatRollbackMode(W24S6McpRollbackMode value)
        {
            if (value == W24S6McpRollbackMode.NoWriteRequired) return "NoWriteRequired";
            throw new JsonSerializationException("Only the schema-defined no-write rollback mode can be serialized.");
        }

        private static string FormatOperationKind(W24S6McpOperationKind value)
        {
            if (value == W24S6McpOperationKind.ParseRecipeSyntax) return "ParseRecipeSyntax";
            if (value == W24S6McpOperationKind.ValidateContractDocument) return "ValidateContractDocument";
            if (value == W24S6McpOperationKind.InspectManifestHeader) return "InspectManifestHeader";
            throw new JsonSerializationException("Only schema-defined document operation kinds can be serialized.");
        }

        private static void ValidateSchemaConstraints(W24S6McpOperationEnvelope envelope)
        {
            if (!string.Equals(envelope.SchemaVersion, Schema, StringComparison.Ordinal)) throw new JsonSerializationException("/schemaVersion does not match the v2 schema.");
            if (!IsSchemaToken(envelope.RequestId)) throw new JsonSerializationException("/requestId does not match the v2 schema.");
            if (!W24S6McpOperationEnvelopePolicy.IsCanonicalSha256(envelope.ProjectIdentityHash)) throw new JsonSerializationException("/projectIdentityHash does not match the v2 schema.");
            if (envelope.ApprovalToken != null && envelope.ApprovalToken.Length != 0) throw new JsonSerializationException("/approvalToken must be empty when present.");
            if (!W24S6McpOperationEnvelopePolicy.IsCanonicalSha256(envelope.PlanHash)) throw new JsonSerializationException("/planHash does not match the v2 schema.");
            if (envelope.Operations == null || envelope.Operations.Length < 1 || envelope.Operations.Length > 16) throw new JsonSerializationException("/operations must contain 1 through 16 items.");
            for (var index = 0; index < envelope.Operations.Length; index++)
            {
                var operation = envelope.Operations[index];
                if (operation == null) throw new JsonSerializationException("/operations/" + index + " must be an object.");
                if (!IsSchemaToken(operation.OperationId)) throw new JsonSerializationException("/operations/" + index + "/operationId does not match the v2 schema.");
                if (!IsSchemaTargetPath(operation.TargetPath)) throw new JsonSerializationException("/operations/" + index + "/targetPath does not match the v2 schema.");
                if (!W24S6McpOperationEnvelopePolicy.IsCanonicalSha256(operation.ExpectedInputHash)) throw new JsonSerializationException("/operations/" + index + "/expectedInputHash does not match the v2 schema.");
            }
        }

        private static bool IsSchemaToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 96 || value[0] == '-' || value[value.Length - 1] == '-') return false;
            var previousHyphen = false;
            foreach (var character in value)
            {
                if (character == '-') { if (previousHyphen) return false; previousHyphen = true; continue; }
                if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))) return false;
                previousHyphen = false;
            }
            return true;
        }

        private static bool IsSchemaTargetPath(string path)
        {
            return !string.IsNullOrEmpty(path) && path.Length <= 260 && path.IndexOf('\\') < 0 && path.IndexOf(':') < 0
                && path.IndexOf('|') < 0 && !path.Any(character => character <= '\u001f' || character == '\u007f');
        }
    }

    public sealed class W24S6McpValidationError
    {
        public string Code;
        public string Field;
        public string Message;
    }

    public sealed class W24S6McpValidationResult
    {
        public readonly List<W24S6McpValidationError> Errors = new List<W24S6McpValidationError>();
        public bool IsValid { get { return Errors.Count == 0; } }

        internal void Add(string code, string field, string message)
        {
            Errors.Add(new W24S6McpValidationError { Code = code, Field = field, Message = message });
        }
    }

    /// <summary>
    /// Structural comparison only. Caller-provided comparison values are not reviewed-plan
    /// authority, an execution ticket, or permission to execute or mutate anything.
    /// </summary>
    public sealed class W24S6McpOperationEnvelopePolicy
    {
        private const int MaximumOperationCount = 16;
        private readonly string expectedProjectIdentityHash;

        public W24S6McpOperationEnvelopePolicy(string expectedProjectIdentityHash)
        {
            if (!IsCanonicalSha256(expectedProjectIdentityHash))
                throw new ArgumentException("A canonical sha256 project identity is required.", nameof(expectedProjectIdentityHash));
            this.expectedProjectIdentityHash = expectedProjectIdentityHash;
        }

        public W24S6McpValidationResult Validate(W24S6McpOperationEnvelope envelope, string expectedPlanHash)
        {
            var result = new W24S6McpValidationResult();
            if (envelope == null) { result.Add("W24MCP001", "/", "Operation envelope is required."); return result; }
            if (!string.Equals(envelope.SchemaVersion, W24S6McpOperationEnvelope.Schema, StringComparison.Ordinal))
                result.Add("W24MCP002", "/schemaVersion", "Unknown operation-envelope schema.");
            if (!IsSafeToken(envelope.RequestId)) result.Add("W24MCP003", "/requestId", "requestId must be a lower-kebab token.");
            if (!string.Equals(envelope.ProjectIdentityHash, expectedProjectIdentityHash, StringComparison.Ordinal))
                result.Add("W24MCP004", "/projectIdentityHash", "Project identity differs from the caller-supplied structural comparison value.");
            if (envelope.ExecutionMode != W24S6McpExecutionMode.DryRun)
                result.Add("W24MCP005", "/executionMode", "Only dry-run requests are structurally accepted.");
            if (envelope.RequestedAuthority != W24S6McpAuthority.DryRun && envelope.RequestedAuthority != W24S6McpAuthority.ReadOnly)
                result.Add("W24MCP006", "/requestedAuthority", "Migration, L4 sign-off, and production-apply authority are forbidden.");
            if (envelope.RollbackMode != W24S6McpRollbackMode.NoWriteRequired)
                result.Add("W24MCP007", "/rollbackMode", "A dry-run has no write transaction; rollback mode must be no-write-required.");
            if (!string.IsNullOrEmpty(envelope.ApprovalToken))
                result.Add("W24MCP008", "/approvalToken", "No user, migration, or L4 approval token is accepted by this boundary.");
            if (!IsCanonicalSha256(expectedPlanHash)) result.Add("W24MCP009", "/expectedPlanHash", "Caller must supply an exact canonical expected plan hash.");
            if (!IsCanonicalSha256(envelope.PlanHash)) result.Add("W24MCP010", "/planHash", "Envelope planHash must be canonical sha256.");

            var operations = envelope.Operations ?? Array.Empty<W24S6McpOperation>();
            if (operations.Length == 0 || operations.Length > MaximumOperationCount)
                result.Add("W24MCP011", "/operations", "Envelope must contain 1 through " + MaximumOperationCount + " operations.");
            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < operations.Length; index++) ValidateOperation(result, operations[index], index, operationIds);

            var computedPlanHash = ComputePlanHash(envelope);
            if (!string.Equals(envelope.PlanHash, computedPlanHash, StringComparison.Ordinal))
            {
                result.Add("W24MCP012", "/planHash", "Envelope planHash does not bind its exact declared contents.");
                // A stale/forged envelope hash is an integrity failure.  Do not also classify
                // it as a caller-comparison mismatch: that makes the failure non-deterministic
                // and obscures the distinct comparison-value signal below.
                return result;
            }
            if (!string.Equals(envelope.PlanHash, expectedPlanHash, StringComparison.Ordinal))
                result.Add("W24MCP013", "/planHash", "Envelope planHash differs from the caller-supplied comparison hash; this is not reviewed-plan authority.");
            return result;
        }

        public static string ComputePlanHash(W24S6McpOperationEnvelope envelope)
        {
            if (envelope == null) return null;
            var operations = envelope.Operations ?? Array.Empty<W24S6McpOperation>();
            using (var bytes = new MemoryStream())
            using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
            {
                WriteField(writer, envelope.SchemaVersion);
                WriteField(writer, envelope.RequestId);
                WriteField(writer, envelope.ProjectIdentityHash);
                writer.Write((int)envelope.ExecutionMode);
                writer.Write((int)envelope.RequestedAuthority);
                writer.Write((int)envelope.RollbackMode);
                WriteField(writer, envelope.ApprovalToken);
                writer.Write(operations.Length);
                foreach (var operation in operations)
                {
                    writer.Write(operation != null);
                    if (operation == null) continue;
                    WriteField(writer, operation.OperationId);
                    writer.Write((int)operation.Kind);
                    WriteField(writer, operation.TargetPath);
                    WriteField(writer, operation.ExpectedInputHash);
                }
                writer.Flush();
                return HashBytes(bytes.ToArray());
            }
        }

        public static bool IsCanonicalSha256(string value)
        {
            return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal)
                && value.Skip(7).All(character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));
        }

        private static void ValidateOperation(W24S6McpValidationResult result, W24S6McpOperation operation, int index, HashSet<string> operationIds)
        {
            var root = "/operations/" + index;
            if (operation == null) { result.Add("W24MCP014", root, "Operation must be an object."); return; }
            if (!IsSafeToken(operation.OperationId) || !operationIds.Add(operation.OperationId))
                result.Add("W24MCP015", root + "/operationId", "operationId must be unique lower-kebab token.");
            if (!IsCanonicalSha256(operation.ExpectedInputHash))
                result.Add("W24MCP016", root + "/expectedInputHash", "Each operation must bind a canonical input sha256.");

            string allowedRoot;
            string suffix;
            switch (operation.Kind)
            {
                case W24S6McpOperationKind.ParseRecipeSyntax:
                    allowedRoot = "Assets/VFX/Recipes/"; suffix = ".json"; break;
                case W24S6McpOperationKind.InspectManifestHeader:
                    allowedRoot = "ProjectSettings/VFXComposer/BuildManifests/"; suffix = ".manifest.json"; break;
                case W24S6McpOperationKind.ValidateContractDocument:
                    allowedRoot = "docs/vfx-contracts/"; suffix = ".contract.json"; break;
                default:
                    result.Add("W24MCP017", root + "/kind", "Operation kind is not allow-listed."); return;
            }
            if (!IsSafeProjectPath(operation.TargetPath, allowedRoot, suffix))
                result.Add("W24MCP018", root + "/targetPath", "Target must be a canonical " + suffix + " path directly under " + allowedRoot + ".");
        }

        private static bool IsSafeToken(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 96 || value[0] == '-' || value[value.Length - 1] == '-') return false;
            var previousHyphen = false;
            foreach (var character in value)
            {
                if (character == '-') { if (previousHyphen) return false; previousHyphen = true; continue; }
                if (!((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))) return false;
                previousHyphen = false;
            }
            return true;
        }

        private static bool IsSafeProjectPath(string path, string allowedRoot, string requiredSuffix)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 260 || path.IndexOf('\\') >= 0 || path.IndexOf(':') >= 0 || path.IndexOf('|') >= 0 || path.Any(char.IsControl) || path.StartsWith("/", StringComparison.Ordinal)
                || path.Contains("//") || !path.StartsWith(allowedRoot, StringComparison.Ordinal) || !path.EndsWith(requiredSuffix, StringComparison.Ordinal)) return false;
            var segments = path.Split('/');
            return segments.All(segment => !string.IsNullOrEmpty(segment) && segment != "." && segment != "..");
        }

        private static void WriteField(BinaryWriter writer, string value)
        {
            if (value == null) { writer.Write(-1); return; }
            var bytes = new UTF8Encoding(false, true).GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string HashBytes(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return "sha256:" + BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
