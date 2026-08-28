using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.W24;

namespace VFXComposer.Editor.W24.S0a
{
    /// <summary>
    /// Parses the operator-only S0a mutation vocabulary.  This is intentionally not a
    /// Recipe Patch parser: its only supported target is an in-memory sustained-flame
    /// fixture clone and every accepted value is explicitly enumerated below.
    /// </summary>
    public enum W24S0aMutationTarget
    {
        FragmentsSharedParentAngularVelocity,
        FlameSteadyStateLinearDrift,
        FlameLoopResetDiscontinuity,
        ParticlesStopResidualSeconds,
        LightStopResidualSeconds,
        SmokeSubjectOcclusionFraction,
        RendererPrimarySmokeSortingOrder,
        StateMachineIgnitionEnabled,
        StateMachineStopContinuityMode,
        LightEnabled,
        CaptureCameraScaleOffset,
        CaptureFrameManifestIntegrity
    }

    public sealed class W24S0aTypedMutation
    {
        public W24S0aMutationTarget Target { get; private set; }
        public string SourceValue { get; private set; }
        public float Number { get; private set; }
        public bool Boolean { get; private set; }
        public bool IsInvalidEvidence { get { return Target == W24S0aMutationTarget.CaptureFrameManifestIntegrity; } }

        private W24S0aTypedMutation(W24S0aMutationTarget target, string sourceValue, float number = 0f, bool boolean = false)
        { Target = target; SourceValue = sourceValue; Number = number; Boolean = boolean; }

        public static W24S0aTypedMutation Parse(string targetKey, string value)
        {
            if (string.IsNullOrEmpty(targetKey) || string.IsNullOrEmpty(value)) throw new InvalidDataException("S0a mutation targetKey and value must be non-empty strings.");
            switch (targetKey)
            {
                case "Fragments.sharedParentAngularVelocity": return NumberValue(targetKey, value, W24S0aMutationTarget.FragmentsSharedParentAngularVelocity, new Dictionary<string, float> { { "180deg_per_second", 180f }, { "22deg_per_second", 22f } });
                case "Flame.steadyStateLinearDrift": return NumberValue(targetKey, value, W24S0aMutationTarget.FlameSteadyStateLinearDrift, new Dictionary<string, float> { { "0.90_units_per_second", .90f }, { "0.11_units_per_second", .11f } });
                case "Flame.loopResetDiscontinuity": return NumberValue(targetKey, value, W24S0aMutationTarget.FlameLoopResetDiscontinuity, new Dictionary<string, float> { { "0.85_normalized_delta", .85f }, { "0.14_normalized_delta", .14f } });
                case "Particles.stopResidualSeconds": return NumberValue(targetKey, value, W24S0aMutationTarget.ParticlesStopResidualSeconds, new Dictionary<string, float> { { "2.50_seconds", 2.5f }, { "0.18_seconds", .18f } });
                case "Light.stopResidualSeconds": return NumberValue(targetKey, value, W24S0aMutationTarget.LightStopResidualSeconds, new Dictionary<string, float> { { "2.50_seconds", 2.5f }, { "0.18_seconds", .18f } });
                case "Smoke.subjectOcclusionFraction": return NumberValue(targetKey, value, W24S0aMutationTarget.SmokeSubjectOcclusionFraction, new Dictionary<string, float> { { "0.78", .78f }, { "0.26", .26f } });
                case "Renderer.primarySmokeSortingOrder": return EnumValue(targetKey, value, W24S0aMutationTarget.RendererPrimarySmokeSortingOrder, "inverted", "near_equal");
                case "StateMachine.ignitionEnabled": return BooleanOrDelay(targetKey, value, W24S0aMutationTarget.StateMachineIgnitionEnabled, "false", "delay_0.42_seconds", .42f);
                case "StateMachine.stopContinuityMode": return EnumValue(targetKey, value, W24S0aMutationTarget.StateMachineStopContinuityMode, "clear_immediate", "fade_0.04_seconds");
                case "Light.enabled": return BooleanOrIntensity(targetKey, value, W24S0aMutationTarget.LightEnabled, "false", "intensity_0.02", .02f);
                case "Capture.cameraScaleOffset": return NumberValue(targetKey, value, W24S0aMutationTarget.CaptureCameraScaleOffset, new Dictionary<string, float> { { "scale_2.20", 2.2f }, { "scale_1.12", 1.12f } });
                case "Capture.frameManifestIntegrity": return EnumValue(targetKey, value, W24S0aMutationTarget.CaptureFrameManifestIntegrity, "missing_key_frame", "sha256_mismatch");
                default: throw new InvalidDataException("S0a mutation targetKey is not whitelisted: " + targetKey);
            }
        }

        private static W24S0aTypedMutation NumberValue(string targetKey, string value, W24S0aMutationTarget target, IDictionary<string, float> allowed)
        {
            float number;
            if (!allowed.TryGetValue(value, out number)) throw new InvalidDataException("Illegal typed S0a value for " + targetKey + ": " + value);
            return new W24S0aTypedMutation(target, value, number);
        }

        private static W24S0aTypedMutation EnumValue(string targetKey, string value, W24S0aMutationTarget target, params string[] allowed)
        {
            if (!allowed.Contains(value, StringComparer.Ordinal)) throw new InvalidDataException("Illegal enum S0a value for " + targetKey + ": " + value);
            return new W24S0aTypedMutation(target, value);
        }

        private static W24S0aTypedMutation BooleanOrDelay(string targetKey, string value, W24S0aMutationTarget target, string falseValue, string delayValue, float delay)
        {
            if (value == falseValue) return new W24S0aTypedMutation(target, value, 0f, false);
            if (value == delayValue) return new W24S0aTypedMutation(target, value, delay, true);
            throw new InvalidDataException("Illegal typed S0a value for " + targetKey + ": " + value);
        }

        private static W24S0aTypedMutation BooleanOrIntensity(string targetKey, string value, W24S0aMutationTarget target, string falseValue, string intensityValue, float intensity)
        {
            if (value == falseValue) return new W24S0aTypedMutation(target, value, 0f, false);
            if (value == intensityValue) return new W24S0aTypedMutation(target, value, intensity, true);
            throw new InvalidDataException("Illegal typed S0a value for " + targetKey + ": " + value);
        }
    }

    public sealed class W24S0aOperatorCommand
    {
        public const string Schema = "s0a-operator-mutation-command/v1";
        public const string EffectId = "sustained_flame_3d";

        public string SampleId { get; private set; }
        public uint FixedSeed { get; private set; }
        public W24S0aTypedMutation Mutation { get; private set; }
        public string SourcePath { get; private set; }
        public string CommandHash { get; private set; }
        /// <summary>A zero-mutation operator command is the generated pass control, not an unlabeled mutation.</summary>
        public bool IsBaselineControl { get { return Mutation == null; } }

        public static W24S0aOperatorCommand Load(string commandPath)
        {
            if (string.IsNullOrEmpty(commandPath) || !File.Exists(commandPath)) throw new FileNotFoundException("S0a operator command does not exist.", commandPath);
            W24S0aOperatorCommandSet.RequireTrustedOperatorCommandPath(commandPath);
            return LoadUnchecked(commandPath);
        }

        internal static W24S0aOperatorCommand LoadUnchecked(string commandPath)
        {
            JObject document;
            try { document = JObject.Parse(File.ReadAllText(commandPath, Encoding.UTF8)); }
            catch (JsonException exception) { throw new InvalidDataException("S0a operator command is not JSON.", exception); }
            var required = new HashSet<string>(StringComparer.Ordinal) { "schemaVersion", "sampleId", "effectId", "fixedSeed", "mutationCommands", "fixtureApplicationStatus", "operatorInstruction", "commandHash" };
            if (!required.SetEquals(document.Properties().Select(property => property.Name))) throw new InvalidDataException("S0a operator command fields are not exact.");
            if (!string.Equals((string)document["schemaVersion"], Schema, StringComparison.Ordinal)) throw new InvalidDataException("Unsupported S0a operator command schema.");
            var effectId = (string)document["effectId"];
            if (!string.Equals(effectId, EffectId, StringComparison.Ordinal)) throw new InvalidDataException("S0a operator command is not bound to sustained_flame_3d.");
            if (!string.Equals((string)document["fixtureApplicationStatus"], "NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER", StringComparison.Ordinal)) throw new InvalidDataException("Only unapplied S0a operator commands may enter the fixture adapter.");
            if (string.IsNullOrWhiteSpace((string)document["operatorInstruction"]) || !HashMatchesCanonicalCommand(document)) throw new InvalidDataException("S0a operator command hash does not bind its canonical content.");
            var sampleId = (string)document["sampleId"];
            if (string.IsNullOrEmpty(sampleId) || sampleId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || sampleId.Contains("..")) throw new InvalidDataException("Unsafe S0a sampleId.");
            var seed = (long?)document["fixedSeed"];
            if (!seed.HasValue || seed.Value <= 0 || seed.Value > uint.MaxValue) throw new InvalidDataException("S0a fixedSeed must be a positive UInt32.");
            var mutations = document["mutationCommands"] as JArray;
            // The generator intentionally emits zero-mutation controls for the pass portion of
            // each cohort.  Every non-control sample still has exactly one allow-listed mutation;
            // accepting two or more would turn this into an unreviewable compound fixture.
            if (mutations == null || mutations.Count > 1) throw new InvalidDataException("Each S0a fixture command must contain zero (baseline control) or exactly one mutation.");
            if (mutations.Count == 0)
                return new W24S0aOperatorCommand { SampleId = sampleId, FixedSeed = (uint)seed.Value, Mutation = null, SourcePath = Path.GetFullPath(commandPath), CommandHash = (string)document["commandHash"] };
            var item = mutations[0] as JObject;
            if (item == null || item.Properties().Select(property => property.Name).OrderBy(name => name).SequenceEqual(new[] { "operation", "targetKey", "value" }) == false || !string.Equals((string)item["operation"], "set", StringComparison.Ordinal) || item["targetKey"].Type != JTokenType.String || item["value"].Type != JTokenType.String) throw new InvalidDataException("S0a mutation command shape is invalid.");
            return new W24S0aOperatorCommand { SampleId = sampleId, FixedSeed = (uint)seed.Value, Mutation = W24S0aTypedMutation.Parse((string)item["targetKey"], (string)item["value"]), SourcePath = Path.GetFullPath(commandPath), CommandHash = (string)document["commandHash"] };
        }

        private static bool HashMatchesCanonicalCommand(JObject document)
        {
            var supplied = (string)document["commandHash"];
            if (string.IsNullOrEmpty(supplied) || supplied.Length != 71 || !supplied.StartsWith("sha256:", StringComparison.Ordinal) || supplied.Skip(7).Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))) return false;
            var copy = (JObject)document.DeepClone(); copy.Remove("commandHash");
            var text = new StringBuilder();
            using (var writer = new JsonTextWriter(new StringWriter(text, CultureInfo.InvariantCulture)) { Formatting = Formatting.None })
            {
                WriteCanonicalToken(writer, copy); writer.Flush();
            }
            using (var sha = SHA256.Create())
            {
                var actual = "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
                return string.Equals(actual, supplied, StringComparison.Ordinal);
            }
        }

        private static void WriteCanonicalToken(JsonWriter writer, JToken token)
        {
            var obj = token as JObject;
            if (obj != null)
            {
                writer.WriteStartObject();
                foreach (var property in obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonicalToken(writer, property.Value); }
                writer.WriteEndObject(); return;
            }
            var array = token as JArray;
            if (array != null) { writer.WriteStartArray(); foreach (var child in array) WriteCanonicalToken(writer, child); writer.WriteEndArray(); return; }
            token.WriteTo(writer);
        }
    }

    /// <summary>Strict filesystem boundary for untrusted operator input.  All run files live below this one Library root.</summary>
    public static class W24S0aCalibrationPaths
    {
        public const string RootRelativePath = "Library/VFXComposer/W24S0aCalibration";
        public const string CalibrationFixturesRelativePath = "docs/vfx-calibration";
        public static string ProjectRootAbsolutePath
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }
        public static string RepositoryRootAbsolutePath
        {
            get { return Directory.GetParent(ProjectRootAbsolutePath).FullName; }
        }
        public static string CalibrationFixturesAbsolutePath
        {
            get { return Path.GetFullPath(Path.Combine(RepositoryRootAbsolutePath, CalibrationFixturesRelativePath.Replace('/', Path.DirectorySeparatorChar))); }
        }
        public static string RootAbsolutePath
        {
            get
            {
                return Path.GetFullPath(Path.Combine(ProjectRootAbsolutePath, RootRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        public static string CandidateDirectory(string sampleId)
        {
            if (string.IsNullOrEmpty(sampleId) || sampleId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || sampleId.Contains("..") || sampleId.IndexOf(Path.DirectorySeparatorChar) >= 0 || sampleId.IndexOf(Path.AltDirectorySeparatorChar) >= 0) throw new ArgumentException("Unsafe S0a sample id.", "sampleId");
            var root = RootAbsolutePath;
            var candidate = Path.GetFullPath(Path.Combine(root, sampleId));
            if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("S0a candidate path escapes the calibration root.");
            return candidate;
        }
    }

    /// <summary>Single frozen seed derivation and tool identity shared by formal capture and verification.</summary>
    public static class W24S0aFormalCaptureProtocol
    {
        public const string CaptureToolVersion = "w24-s0a-formal-calibration-capture/1.1.3";
        public const string RendererAssetReference = "Assets/Settings/VFXPreviewUniversalRenderer.asset";
        public const string VolumeReference = "ProjectSettings/GraphicsSettings.asset (no per-scene Volume; bloom/tone mapping caller-frozen)";
        public static readonly int[] RetainedFrames = { 1, 21, 60, 120, 180, 240, 300, 360 };
        public static readonly string[] CaptureToolRelativePaths =
        {
            "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24CaptureProfile.cs",
            "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24ContinuousCaptureRecorder.cs",
            "Packages/com.vfxcomposer.unity/Runtime/Diagnostics/W24EvidenceStore.cs",
            "Packages/com.vfxcomposer.unity/Editor/W24/S0a/W24S0aFixtureAdapter.cs",
            "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormal/W24S0aFormalCalibrationCaptureTests.cs",
            "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormal/VFXComposer.Tests.PlayMode.W24S0aFormal.asmdef",
            "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormalRuntime/W24S0aFormalPlayModeProxyTests.cs",
            "Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S0aFormalRuntime/VFXComposer.Tests.PlayMode.W24S0aFormalRuntime.asmdef"
        };

        public static uint[] DeriveRobustnessSeeds(uint fixedSeed)
        {
            var first = Derive(fixedSeed, 0x9e3779b9u);
            var second = Derive(fixedSeed, 0x85ebca6bu);
            while (second == fixedSeed || second == first || second == 0u)
                second = Derive(second + 1u, 0xc2b2ae35u);
            return new[] { first, second };
        }

        public static void RequireExactSeeds(W24CaptureProfile profile, uint fixedSeed)
        {
            var expected = DeriveRobustnessSeeds(fixedSeed);
            if (profile == null || unchecked((uint)profile.CanonicalSeed) != fixedSeed || profile.RobustnessSeeds == null || profile.RobustnessSeeds.Length != 2
                || unchecked((uint)profile.RobustnessSeeds[0]) != expected[0] || unchecked((uint)profile.RobustnessSeeds[1]) != expected[1])
                throw new InvalidOperationException("S0a Capture Profile seeds must be the exact fixed operator seed followed by the frozen two-seed robustness derivation.");
        }

        public static string CaptureToolIdentityPath { get { return string.Join(";", CaptureToolRelativePaths); } }

        public static string CaptureToolSha256()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var identity = string.Join("\n", CaptureToolRelativePaths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path + ":" + W24S0aIntegrity.HashFile(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar))).Substring(7)));
            return W24S0aIntegrity.HashText(identity);
        }

        private static uint Derive(uint source, uint salt)
        {
            var result = source ^ salt;
            return result == 0u || result == source ? source + salt + 1u : result;
        }
    }

    internal static class W24S0aIntegrity
    {
        public static bool IsCanonicalHash(string value)
        {
            return value != null && value.Length == 71 && value.StartsWith("sha256:", StringComparison.Ordinal)
                && !value.Skip(7).Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')));
        }

        public static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        public static string HashText(string text)
        {
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        public static string CanonicalHash(JObject document, string omittedField)
        {
            var copy = (JObject)document.DeepClone();
            copy.Remove(omittedField);
            var text = new StringBuilder();
            using (var writer = new JsonTextWriter(new StringWriter(text, CultureInfo.InvariantCulture)) { Formatting = Formatting.None })
            {
                WriteCanonical(writer, copy);
                writer.Flush();
            }
            using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        public static void WriteCanonical(JsonWriter writer, JToken token)
        {
            var obj = token as JObject;
            if (obj != null)
            {
                writer.WriteStartObject();
                foreach (var property in obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                return;
            }
            var array = token as JArray;
            if (array != null)
            {
                writer.WriteStartArray();
                foreach (var child in array) WriteCanonical(writer, child);
                writer.WriteEndArray();
                return;
            }
            token.WriteTo(writer);
        }
    }

    /// <summary>
    /// The formal runner deliberately selects a named cohort rather than accepting a caller
    /// supplied directory.  This prevents the blind side, labels, answer ledger, or an arbitrary
    /// JSON file from being accidentally treated as fixture input.
    /// </summary>
    public enum W24S0aCalibrationCohort { Reduced, Full }

    public sealed class W24S0aOperatorCommandSet
    {
        private const string CommandSetSchema = "s0a-operator-command-set/v1";
        private const string CaptureFrozen = "FROZEN_FOR_CAPTURE";
        private readonly W24S0aOperatorCommand[] commands;
        public W24S0aCalibrationCohort Cohort { get; private set; }
        public string CommandDirectory { get; private set; }
        public IReadOnlyList<W24S0aOperatorCommand> Commands { get { return commands; } }

        private W24S0aOperatorCommandSet(W24S0aCalibrationCohort cohort, string commandDirectory, W24S0aOperatorCommand[] values)
        { Cohort = cohort; CommandDirectory = commandDirectory; commands = values; }

        public static int ExpectedSampleCount(W24S0aCalibrationCohort cohort)
        {
            switch (cohort)
            {
                case W24S0aCalibrationCohort.Reduced: return 66;
                case W24S0aCalibrationCohort.Full: return 110;
                default: throw new ArgumentOutOfRangeException("cohort", "Only the reduced 66-sample and full 110-sample S0a cohorts are permitted.");
            }
        }

        public static string GetCommandDirectory(W24S0aCalibrationCohort cohort)
        {
            string name;
            switch (cohort)
            {
                case W24S0aCalibrationCohort.Reduced: name = "reduced"; break;
                case W24S0aCalibrationCohort.Full: name = "full"; break;
                default: throw new ArgumentOutOfRangeException("cohort", "Only the reduced 66-sample and full 110-sample S0a cohorts are permitted.");
            }
            return Path.GetFullPath(Path.Combine(W24S0aCalibrationPaths.CalibrationFixturesAbsolutePath, name, "operator", "mutation-commands"));
        }

        public static string GetCommandSetManifestPath(W24S0aCalibrationCohort cohort)
        {
            return Path.GetFullPath(Path.Combine(GetCommandDirectory(cohort), "..", "command-set.json"));
        }

        public static W24S0aOperatorCommandSet LoadCohort(W24S0aCalibrationCohort cohort)
        {
            var directory = GetCommandDirectory(cohort);
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The named S0a operator-command cohort has not been generated: " + directory);
            RejectReparsePoint(directory, "S0a operator command directory");
            var commandSetManifest = LoadCaptureFrozenCommandSetManifest(cohort);
            var entries = Directory.GetFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly);
            if (entries.Length != ExpectedSampleCount(cohort)) throw new InvalidDataException("S0a operator command cohort has the wrong entry count; expected " + ExpectedSampleCount(cohort) + ".");
            var loaded = new List<W24S0aOperatorCommand>();
            foreach (var entry in entries.OrderBy(path => path, StringComparer.Ordinal))
            {
                if (Directory.Exists(entry) || !entry.EndsWith(".mutation-command.json", StringComparison.Ordinal) || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("S0a cohort accepts only direct operator mutation-command JSON files; labels, ledgers, blind inputs, nested directories, and links are forbidden.");
                RequireTrustedOperatorCommandPath(entry);
                var command = LoadUnchecked(entry);
                if (!string.Equals(Path.GetFileName(entry), command.SampleId + ".mutation-command.json", StringComparison.Ordinal))
                    throw new InvalidDataException("S0a operator command filename must be exactly its anonymous sampleId.");
                loaded.Add(command);
            }
            if (loaded.Select(command => command.SampleId).Distinct(StringComparer.Ordinal).Count() != loaded.Count)
                throw new InvalidDataException("S0a operator command cohort contains duplicate sample IDs.");
            var ordered = loaded.OrderBy(command => command.SampleId, StringComparer.Ordinal).ToArray();
            var actual = ordered.Select(command => command.SampleId + "|" + command.CommandHash).ToArray();
            var expected = ((JArray)commandSetManifest["commands"]).Select(item => (string)item["sampleId"] + "|" + (string)item["commandHash"]).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidDataException("S0a operator command files do not exactly match the frozen non-answer command-set manifest.");
            return new W24S0aOperatorCommandSet(cohort, directory, ordered);
        }

        private static JObject LoadCaptureFrozenCommandSetManifest(W24S0aCalibrationCohort cohort)
        {
            var path = GetCommandSetManifestPath(cohort);
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("S0a formal capture requires the fixed non-answer operator command-set manifest.");
            JObject document;
            try { document = JObject.Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (JsonException exception) { throw new InvalidDataException("S0a operator command-set manifest is malformed.", exception); }
            var required = new[] { "schemaVersion", "holdoutCohort", "freezeStatus", "commands", "commandSetHash" };
            if (!document.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(required.OrderBy(name => name, StringComparer.Ordinal))
                || !string.Equals((string)document["schemaVersion"], CommandSetSchema, StringComparison.Ordinal)
                || !string.Equals((string)document["freezeStatus"], CaptureFrozen, StringComparison.Ordinal)
                || !string.Equals((string)document["holdoutCohort"], cohort == W24S0aCalibrationCohort.Reduced ? "reduced-36-12-12-6" : "full-60-20-20-10", StringComparison.Ordinal)
                || !string.Equals((string)document["commandSetHash"], W24S0aIntegrity.CanonicalHash(document, "commandSetHash"), StringComparison.Ordinal))
                throw new InvalidDataException("S0a operator command-set manifest is not the required frozen canonical capture manifest.");
            var commands = document["commands"] as JArray;
            if (commands == null || commands.Count != ExpectedSampleCount(cohort)) throw new InvalidDataException("S0a command-set manifest has the wrong fixed cohort size.");
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var command in commands)
            {
                var item = command as JObject;
                var id = item == null ? null : (string)item["sampleId"];
                var hash = item == null ? null : (string)item["commandHash"];
                if (item == null || !item.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(new[] { "commandHash", "sampleId" })
                    || string.IsNullOrEmpty(id) || !W24S0aIntegrity.IsCanonicalHash(hash) || !pairs.Add(id + "|" + hash))
                    throw new InvalidDataException("S0a command-set manifest contains an invalid or duplicate non-answer command identity.");
            }
            return document;
        }

        /// <summary>Called by the command parser before decoding JSON; no generic input path is a formal capture input.</summary>
        public static void RequireTrustedOperatorCommandPath(string commandPath)
        {
            if (string.IsNullOrEmpty(commandPath)) throw new ArgumentException("S0a operator command path is required.", "commandPath");
            var full = Path.GetFullPath(commandPath);
            var parent = Path.GetDirectoryName(full);
            var accepted = false;
            foreach (W24S0aCalibrationCohort cohort in Enum.GetValues(typeof(W24S0aCalibrationCohort)))
            {
                var expected = GetCommandDirectory(cohort);
                if (string.Equals(parent, expected, StringComparison.OrdinalIgnoreCase)) { accepted = true; break; }
            }
            if (!accepted || !full.EndsWith(".mutation-command.json", StringComparison.Ordinal) || (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Formal S0a capture accepts only direct files from the fixed reduced/full operator mutation-command directories; arbitrary directories, answer ledgers, labels, and blind inputs are rejected.");
        }

        private static W24S0aOperatorCommand LoadUnchecked(string commandPath)
        {
            // Keep the public parser's validation in one place while avoiding a recursive path check.
            return W24S0aOperatorCommand.LoadUnchecked(commandPath);
        }

        private static void RejectReparsePoint(string path, string label)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException(label + " must not be a link or reparse point.");
        }
    }

    public enum W24S0aBatchCaptureState { Fresh, Complete }

    /// <summary>All-or-nothing recovery policy for a cohort's write-once candidate directories.</summary>
    public static class W24S0aBatchCaptureRecovery
    {
        /// <summary>Pure all-or-nothing policy seam used by tests; it never writes, deletes, or resumes evidence.</summary>
        public static W24S0aBatchCaptureState Classify(int expectedCandidateCount, int existingCandidateCount, int sealedCaptureCount)
        {
            if (expectedCandidateCount <= 0 || existingCandidateCount < 0 || sealedCaptureCount < 0 || existingCandidateCount > expectedCandidateCount || sealedCaptureCount > existingCandidateCount)
                throw new ArgumentOutOfRangeException("S0a batch capture counts are inconsistent.");
            if (existingCandidateCount == 0) return W24S0aBatchCaptureState.Fresh;
            if (existingCandidateCount != expectedCandidateCount || sealedCaptureCount != expectedCandidateCount)
                throw new InvalidOperationException("S0a formal cohort is partially captured. Preserve the write-once evidence and recover manually; do not overwrite or silently resume it.");
            return W24S0aBatchCaptureState.Complete;
        }

        public static W24S0aBatchCaptureState GetState(W24S0aOperatorCommandSet commandSet)
        {
            if (commandSet == null) throw new ArgumentNullException("commandSet");
            // The reduced and full cohorts may be captured sequentially beneath the same
            // project-local root.  A completed, independently verified sibling cohort is not
            // foreign evidence; anything else at that root is.  We only load the other frozen
            // command set, never labels, blind submissions, or the answer ledger.
            var authorized = commandSet.Commands.ToDictionary(command => command.SampleId, command => command, StringComparer.Ordinal);
            foreach (W24S0aCalibrationCohort otherCohort in Enum.GetValues(typeof(W24S0aCalibrationCohort)))
            {
                if (otherCohort == commandSet.Cohort || !Directory.Exists(W24S0aOperatorCommandSet.GetCommandDirectory(otherCohort))) continue;
                foreach (var other in W24S0aOperatorCommandSet.LoadCohort(otherCohort).Commands)
                {
                    W24S0aOperatorCommand existingCommand;
                    if (authorized.TryGetValue(other.SampleId, out existingCommand) && !string.Equals(existingCommand.CommandHash, other.CommandHash, StringComparison.Ordinal))
                        throw new InvalidDataException("S0a reduced/full command cohorts reuse a sampleId with conflicting command identity.");
                    authorized[other.SampleId] = other;
                }
            }
            if (Directory.Exists(W24S0aCalibrationPaths.RootAbsolutePath))
            {
                foreach (var entry in Directory.GetFileSystemEntries(W24S0aCalibrationPaths.RootAbsolutePath, "*", SearchOption.TopDirectoryOnly))
                {
                    W24S0aOperatorCommand owner;
                    if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 || !Directory.Exists(entry) || !authorized.TryGetValue(Path.GetFileName(entry), out owner))
                        throw new InvalidOperationException("S0a formal cohort has a foreign, linked, or malformed candidate-root entry; preserve it for manual recovery and do not resume.");
                    ValidateCompletedCandidate(owner);
                }
            }
            var existing = commandSet.Commands.Where(command => Directory.Exists(W24S0aCalibrationPaths.CandidateDirectory(command.SampleId))).ToArray();
            if (existing.Length == 0) return Classify(commandSet.Commands.Count, 0, 0);
            var sealedCount = 0;
            foreach (var command in existing)
            {
                ValidateCompletedCandidate(command);
                sealedCount++;
            }
            return Classify(commandSet.Commands.Count, existing.Length, sealedCount);
        }

        private static void ValidateCompletedCandidate(W24S0aOperatorCommand command)
        {
            var candidate = W24S0aCalibrationPaths.CandidateDirectory(command.SampleId);
            ValidateCandidateDirectoryShape(candidate, command);
            var capture = Path.Combine(candidate, "capture");
            W24S0aInvalidEvidenceInjector.ValidateSealedCapture(capture, command.CommandHash);
            W24S0aInvalidEvidenceInjector.ValidateFormalCaptureSemantics(capture, command);
            var completionPath = Path.Combine(candidate, "candidate-completion.json");
            if (!File.Exists(completionPath) || (File.GetAttributes(completionPath) & FileAttributes.ReadOnly) == 0)
                throw new InvalidDataException("S0a candidate has sealed raw files but no final completion marker.");
            JObject completion;
            try { completion = JObject.Parse(File.ReadAllText(completionPath, Encoding.UTF8)); }
            catch (JsonException exception) { throw new InvalidDataException("S0a candidate completion marker is malformed.", exception); }
            var required = new[] { "schema", "sampleId", "commandHash", "captureSealHash", "invalidEvidenceManifestHash", "ledgerTailHash", "completionHash" };
            if (!completion.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(required.OrderBy(name => name, StringComparer.Ordinal))
                || !string.Equals((string)completion["schema"], "w24-s0a-candidate-completion/v1", StringComparison.Ordinal)
                || !string.Equals((string)completion["sampleId"], command.SampleId, StringComparison.Ordinal)
                || !string.Equals((string)completion["commandHash"], command.CommandHash, StringComparison.Ordinal)
                || !string.Equals((string)completion["captureSealHash"], W24S0aIntegrity.HashFile(Path.Combine(capture, "evidence-seal.json")), StringComparison.Ordinal)
                || !W24S0aIntegrity.IsCanonicalHash((string)completion["ledgerTailHash"])
                || !string.Equals((string)completion["completionHash"], W24S0aIntegrity.CanonicalHash(completion, "completionHash"), StringComparison.Ordinal))
                throw new InvalidDataException("S0a candidate completion marker does not bind its command, raw capture, and ledger identities.");
            var ledger = Path.Combine(candidate, "ledger");
            W24S0aFixtureLedger.VerifyDirectory(ledger);
            ValidateCompletedLifecycleLedger(command, ledger);
            var last = Directory.GetFiles(ledger, "*.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal).LastOrDefault();
            if (last == null || !string.Equals((string)JObject.Parse(File.ReadAllText(last, Encoding.UTF8))["entryHash"], (string)completion["ledgerTailHash"], StringComparison.Ordinal))
                throw new InvalidDataException("S0a candidate completion marker does not bind the final lifecycle ledger entry.");
            if (command.Mutation != null && command.Mutation.IsInvalidEvidence)
            {
                var derived = W24S0aInvalidEvidenceInjector.ValidateDerivedInvalidEvidence(candidate, capture, command.Mutation, command.CommandHash);
                W24S0aInvalidEvidenceInjector.ValidateFormalCaptureTree(Path.Combine(candidate, "invalid-evidence"), command, command.Mutation);
                if (!string.Equals((string)completion["invalidEvidenceManifestHash"], derived, StringComparison.Ordinal))
                    throw new InvalidDataException("S0a invalid-evidence candidate completion marker does not bind its derived evidence manifest.");
            }
            else if (completion["invalidEvidenceManifestHash"].Type != JTokenType.Null)
                throw new InvalidDataException("S0a non-invalid candidate must not claim a derived invalid-evidence manifest.");
        }

        private static void ValidateCandidateDirectoryShape(string candidate, W24S0aOperatorCommand command)
        {
            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                "capture", "ledger", "candidate-completion.json"
            };
            if (command.Mutation != null && command.Mutation.IsInvalidEvidence) expected.Add("invalid-evidence");
            var actual = Directory.GetFileSystemEntries(candidate, "*", SearchOption.TopDirectoryOnly);
            if (actual.Length != expected.Count) throw new InvalidDataException("S0a completed candidate has an unexpected root artifact.");
            foreach (var entry in actual)
            {
                var name = Path.GetFileName(entry);
                if (!expected.Remove(name) || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("S0a completed candidate has a foreign or linked root artifact: " + name);
                var isDirectory = Directory.Exists(entry);
                if ((name == "candidate-completion.json" && isDirectory) || (name != "candidate-completion.json" && !isDirectory))
                    throw new InvalidDataException("S0a completed candidate root artifact has the wrong file/directory kind: " + name);
            }
            if (expected.Count != 0) throw new InvalidDataException("S0a completed candidate is missing a required root artifact.");
        }

        private static void ValidateCompletedLifecycleLedger(W24S0aOperatorCommand command, string ledger)
        {
            var entries = Directory.GetFiles(ledger, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => JObject.Parse(File.ReadAllText(path, Encoding.UTF8))).ToArray();
            var expected = new List<string> { "created" };
            if (command.Mutation != null) expected.Add(command.Mutation.IsInvalidEvidence ? "queued-invalid-evidence" : "visual-mutation-applied");
            expected.Add("capture-begun");
            for (var seedOrdinal = 0; seedOrdinal < 3; seedOrdinal++) { expected.Add("seed-started"); expected.Add("seed-stop-requested"); }
            expected.Add("raw-capture-sealed");
            if (command.Mutation != null && command.Mutation.IsInvalidEvidence) expected.Add("invalid-evidence-injected");
            expected.Add("candidate-finalized");
            expected.Add("cleanup");
            var actual = entries.Select(entry => (string)entry["kind"]).ToArray();
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
                throw new InvalidDataException("S0a completed candidate ledger is missing, reordering, or adding lifecycle events.");
            var created = entries[0]["details"] as JObject;
            if (created == null || !string.Equals((string)created["sampleId"], command.SampleId, StringComparison.Ordinal)
                || !string.Equals((string)created["commandHash"], command.CommandHash, StringComparison.Ordinal))
                throw new InvalidDataException("S0a completed candidate ledger does not begin with its expected command identity.");
        }
    }

    /// <summary>Small transaction primitive shared by fixture cleanup and its edit-mode proof.</summary>
    public sealed class W24S0aFixtureCleanupGate
    {
        private bool entered;
        public bool IsEntered { get { return entered; } }
        public bool TryEnter()
        {
            if (entered) return false;
            entered = true;
            return true;
        }
    }

    public static class W24S0aFailureRecovery
    {
        public const string CleanupFailureDataKey = "W24S0aFixtureCleanupFailure";
        public static void CleanupWithoutMasking(Exception primary, Action cleanup)
        {
            if (primary == null || cleanup == null) throw new ArgumentNullException("S0a failure recovery requires primary exception and cleanup action.");
            try { cleanup(); }
            catch (Exception cleanupFailure) { primary.Data[CleanupFailureDataKey] = cleanupFailure.ToString(); }
        }
    }

    /// <summary>
    /// Owns one in-memory candidate clone.  It never calls AssetDatabase save/import APIs and
    /// it does not create or overwrite Prefabs, Scenes, Recipes, contracts, or labels.
    /// A PlayMode driver must call ObserveCompletedPlayerLoopFrame after normal playback.
    /// </summary>
    public sealed class W24S0aFixtureSession : IDisposable
    {
        private readonly Dictionary<string, string> sourceHashes;
        private GameObject candidate;
        private SustainedEffectController controller;
        private readonly W24S0aOperatorCommand command;
        private readonly string candidateDirectory;
        private readonly Scene fixtureScene;
        private readonly Camera authorityCamera;
        private W24ContinuousCaptureRecorder recorder;
        private W24CaptureProfile captureProfile;
        private bool captureCompleted;
        private int observedNaturalFrameCount;
        private uint[] requiredSeeds = new uint[0];
        private int activeSeedIndex = -1;
        private readonly W24S0aFixtureCleanupGate cleanupGate = new W24S0aFixtureCleanupGate();
        private int ledgerSequence;
        private string previousLedgerHash;

        public GameObject Candidate { get { return candidate; } }
        public SustainedEffectController Controller { get { return controller; } }
        public string CandidateDirectory { get { return candidateDirectory; } }
        public string CaptureDirectory { get { return Path.Combine(candidateDirectory, "capture"); } }
        public uint FixedSeed { get { return command.FixedSeed; } }
        public bool IsBaselineControl { get { return command.IsBaselineControl; } }

        public static W24S0aFixtureSession Create(string commandPath, Scene fixtureScene)
        {
            if (!fixtureScene.IsValid() || !fixtureScene.isLoaded) throw new InvalidOperationException("S0a fixture scene must be valid and loaded.");
            if (!string.Equals((fixtureScene.path ?? string.Empty).Replace('\\', '/'), SustainedFlameAuthoring.PreviewScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("S0a fixture must be the serialized sustained-flame authority preview scene, not a temporary or substituted scene.");
            var command = W24S0aOperatorCommand.Load(commandPath);
            var cameras = fixtureScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
            if (cameras.Length != 1 || cameras[0].gameObject.scene != fixtureScene || !string.Equals(cameras[0].name, "MainCamera", StringComparison.Ordinal) || !cameras[0].CompareTag("MainCamera"))
                throw new InvalidOperationException("S0a fixture requires exactly one serialized MainCamera in the authority preview scene.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SustainedFlameAuthoring.PrefabPath);
            if (source == null) throw new InvalidOperationException("S0a fixture requires the existing sustained_flame_3d Prefab; it will not build or modify it.");
            var frozenSources = SnapshotOfficialSources();
            var directory = W24S0aCalibrationPaths.CandidateDirectory(command.SampleId);
            if (Directory.Exists(directory)) throw new InvalidOperationException("S0a candidate directory is write-once and already exists: " + directory);
            Directory.CreateDirectory(Path.Combine(directory, "ledger"));
            var session = new W24S0aFixtureSession(command, directory, frozenSources, fixtureScene, cameras[0]);
            try
            {
                var instance = PrefabUtility.InstantiatePrefab(source, fixtureScene) as GameObject;
                if (instance == null) throw new InvalidOperationException("Could not instantiate the isolated sustained flame fixture clone.");
                session.candidate = instance;
                session.controller = instance.GetComponent<SustainedEffectController>();
                if (session.controller == null) throw new InvalidOperationException("The isolated fixture clone has no SustainedEffectController.");
                var mutation = command.Mutation;
                session.WriteLedger("created", new JObject { ["sampleId"] = command.SampleId, ["commandHash"] = command.CommandHash, ["effectId"] = W24S0aOperatorCommand.EffectId, ["sourcePrefab"] = SustainedFlameAuthoring.PrefabPath, ["sourceHashes"] = JObject.FromObject(session.sourceHashes), ["fixtureKind"] = mutation == null ? "BASELINE_CONTROL" : "SINGLE_MUTATION", ["mutationTarget"] = mutation == null ? "none" : mutation.Target.ToString(), ["mutationValue"] = mutation == null ? "none" : mutation.SourceValue, ["evidencePhase"] = mutation != null && mutation.IsInvalidEvidence ? "POST_CAPTURE_ONLY" : "PRE_CAPTURE_VISUAL" });
                if (mutation != null && !mutation.IsInvalidEvidence) session.ApplyVisualMutation();
                else if (mutation != null) session.WriteLedger("queued-invalid-evidence", new JObject { ["rule"] = "No evidence mutation before W24ContinuousCaptureRecorder.Complete." });
                return session;
            }
            catch (Exception primary)
            {
                session.CleanupWithoutMasking(primary);
                throw;
            }
        }

        private W24S0aFixtureSession(W24S0aOperatorCommand command, string directory, Dictionary<string, string> hashes, Scene scene, Camera camera)
        { this.command = command; candidateDirectory = directory; sourceHashes = hashes; fixtureScene = scene; authorityCamera = camera; }

        public void BeginActualCapture(W24ContinuousCaptureRecorder actualRecorder, W24CaptureProfile profile, W24CaptureSourceHashes sources)
        {
            try
            {
                if (actualRecorder == null || profile == null || sources == null) throw new ArgumentNullException("S0a actual capture requires recorder, profile, and source hashes.");
                if (cleanupGate.IsEntered || recorder != null) throw new InvalidOperationException("S0a fixture capture may begin exactly once.");
                VerifyOfficialSourcesUnchanged(sourceHashes);
                ValidateAuthorityCaptureIdentity(actualRecorder, profile, sources);
                W24S0aFormalCaptureProtocol.RequireExactSeeds(profile, command.FixedSeed);
                var expectedRobustness = W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(command.FixedSeed);
                // BeginFormal forbids caller supplied timing/frame metadata; retained images can
                // only be captured from the recorder's own completed LateUpdate observations.
                actualRecorder.BeginFormal(CaptureDirectory, command.SampleId, profile, sources, command.CommandHash);
                recorder = actualRecorder;
                captureProfile = profile;
                requiredSeeds = new[] { command.FixedSeed, expectedRobustness[0], expectedRobustness[1] };
                activeSeedIndex = -1;
                observedNaturalFrameCount = 0;
                WriteLedger("capture-begun", new JObject { ["recorder"] = typeof(W24ContinuousCaptureRecorder).FullName, ["captureDirectory"] = "capture", ["policy"] = "normal-player-loop-only", ["serializedCamera"] = SustainedFlameAuthoring.PreviewScenePath + "#MainCamera", ["captureProfileHash"] = profile.Sha256 });
            }
            catch (Exception primary) { CleanupWithoutMasking(primary); throw; }
        }

        /// <summary>Consumes exactly one real LateUpdate observation. The driver cannot supply a frame, time, state, or seed.</summary>
        public void ObserveCompletedPlayerLoopFrame()
        {
            try
            {
                if (recorder == null || captureCompleted) throw new InvalidOperationException("S0a actual capture is not active.");
                if (activeSeedIndex < 0 || activeSeedIndex >= requiredSeeds.Length) throw new InvalidOperationException("S0a formal capture requires StartNextProfileSeed before observing frames.");
                observedNaturalFrameCount++;
                var token = recorder.ConsumeCompletedPlayerLoopToken();
                if (captureProfile.IsRetainedFrameIndex(observedNaturalFrameCount))
                    recorder.CaptureObservedPlayerLoopFrame(token, observedNaturalFrameCount, controller.State.ToString().ToLowerInvariant(), requiredSeeds[activeSeedIndex]);
                else
                    recorder.AcknowledgeObservedPlayerLoopFrame(token);
            }
            catch (Exception primary) { CleanupWithoutMasking(primary); throw; }
        }

        /// <summary>Starts the next, predeclared canonical/robustness seed in profile order.</summary>
        public uint StartNextProfileSeed()
        {
            if (recorder == null || captureCompleted) throw new InvalidOperationException("S0a actual capture is not active.");
            if (activeSeedIndex >= 0 && observedNaturalFrameCount < captureProfile.RetainedFrameIndices.Max())
                throw new InvalidOperationException("S0a cannot advance to a robustness seed before the current seed completed its full natural PlayerLoop span.");
            if (activeSeedIndex + 1 >= requiredSeeds.Length) throw new InvalidOperationException("S0a Capture Profile has no remaining unobserved seed.");
            activeSeedIndex++;
            observedNaturalFrameCount = 0;
            controller.ResetForPool();
            controller.PlayWithSeed(requiredSeeds[activeSeedIndex]);
            WriteLedger("seed-started", new JObject { ["seedOrdinal"] = activeSeedIndex, ["seed"] = requiredSeeds[activeSeedIndex], ["kind"] = activeSeedIndex == 0 ? "canonical" : "robustness" });
            return requiredSeeds[activeSeedIndex];
        }

        public void StopCurrentProfileSeed()
        {
            if (recorder == null || captureCompleted || activeSeedIndex < 0) throw new InvalidOperationException("S0a actual capture has no active profile seed.");
            controller.Stop(VfxStopMode.AllowTail);
            WriteLedger("seed-stop-requested", new JObject { ["seedOrdinal"] = activeSeedIndex, ["seed"] = requiredSeeds[activeSeedIndex], ["afterNaturalFrame"] = observedNaturalFrameCount });
        }

        /// <summary>Semantic telemetry is a distinct write-once artifact, never a hidden label or visual verdict.</summary>
        public string WriteActualSemanticTelemetry(string relativePath, byte[] bytes, string description)
        {
            try
            {
                if (recorder == null || captureCompleted) throw new InvalidOperationException("S0a actual capture is not active.");
                if (activeSeedIndex != requiredSeeds.Length - 1 || observedNaturalFrameCount < captureProfile.RetainedFrameIndices.Max())
                    throw new InvalidOperationException("S0a cannot seal until canonical and both robustness seeds complete their full natural PlayerLoop frame span.");
                return recorder.WriteSemanticTelemetry(relativePath, bytes, description);
            }
            catch (Exception primary) { CleanupWithoutMasking(primary); throw; }
        }

        public void CompleteActualCapture()
        {
            try
            {
                if (recorder == null || captureCompleted) throw new InvalidOperationException("S0a actual capture is not active.");
                recorder.Complete();
                WriteLedger("raw-capture-sealed", new JObject { ["captureDirectory"] = "capture", ["sourceHashesUnchanged"] = VerifyOfficialSourcesUnchanged(sourceHashes) });
                if (command.Mutation != null && command.Mutation.IsInvalidEvidence)
                    W24S0aInvalidEvidenceInjector.Inject(candidateDirectory, CaptureDirectory, command.Mutation, command.CommandHash, WriteLedger);
                WriteLedger("candidate-finalized", new JObject { ["captureDirectory"] = "capture", ["invalidEvidenceRequired"] = command.Mutation != null && command.Mutation.IsInvalidEvidence });
                // The completion marker binds the final ledger tail.  Cleanup must therefore be
                // part of the lifecycle before that marker is written; Dispose then becomes a
                // no-op and cannot make a previously complete candidate appear partial.
                Cleanup();
                WriteCompletionMarker();
                captureCompleted = true;
            }
            catch (Exception primary) { CleanupWithoutMasking(primary); throw; }
        }

        private void ValidateAuthorityCaptureIdentity(W24ContinuousCaptureRecorder actualRecorder, W24CaptureProfile profile, W24CaptureSourceHashes sources)
        {
            if (actualRecorder.AuthorityCamera != authorityCamera || authorityCamera == null || authorityCamera.gameObject.scene != fixtureScene)
                throw new InvalidOperationException("S0a formal capture must use the session's exact serialized authority MainCamera.");
            if (!string.Equals(profile.ScenePath, SustainedFlameAuthoring.PreviewScenePath, StringComparison.Ordinal)
                || !string.Equals(profile.SerializedCameraReference, SustainedFlameAuthoring.PreviewScenePath + "#MainCamera", StringComparison.Ordinal))
                throw new InvalidOperationException("S0a Capture Profile does not bind the authority preview scene/MainCamera reference.");
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var expectedScene = Path.Combine(projectRoot, SustainedFlameAuthoring.PreviewScenePath.Replace('/', Path.DirectorySeparatorChar));
            var expectedPrefab = Path.Combine(projectRoot, SustainedFlameAuthoring.PrefabPath.Replace('/', Path.DirectorySeparatorChar));
            var expectedManifest = Path.Combine(projectRoot, SustainedFlameAuthoring.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
            if (!PathEquals(sources.SceneSourcePath, expectedScene) || !PathEquals(sources.PrefabSourcePath, expectedPrefab) || !PathEquals(sources.ManifestSourcePath, expectedManifest)
                || !string.Equals(sources.SceneSha256, HashFile(expectedScene), StringComparison.Ordinal)
                || !string.Equals(sources.PrefabSha256, HashFile(expectedPrefab), StringComparison.Ordinal)
                || !string.Equals(sources.ManifestSha256, HashFile(expectedManifest), StringComparison.Ordinal)
                || !string.Equals(sources.PrefabGuid, ReadGuid(expectedPrefab + ".meta"), StringComparison.Ordinal))
                throw new InvalidOperationException("S0a capture source identities do not match the authority scene, Runtime Entry, or BuildManifest.");
        }

        private void WriteCompletionMarker()
        {
            W24S0aFixtureLedger.VerifyDirectory(Path.Combine(candidateDirectory, "ledger"));
            var rawSeal = Path.Combine(CaptureDirectory, "evidence-seal.json");
            W24S0aInvalidEvidenceInjector.ValidateSealedCapture(CaptureDirectory, command.CommandHash);
            var invalidManifest = command.Mutation != null && command.Mutation.IsInvalidEvidence
                ? W24S0aInvalidEvidenceInjector.ValidateDerivedInvalidEvidence(candidateDirectory, CaptureDirectory, command.Mutation, command.CommandHash)
                : null;
            var document = new JObject
            {
                ["schema"] = "w24-s0a-candidate-completion/v1",
                ["sampleId"] = command.SampleId,
                ["commandHash"] = command.CommandHash,
                ["captureSealHash"] = HashFile(rawSeal),
                ["invalidEvidenceManifestHash"] = invalidManifest == null ? JValue.CreateNull() : new JValue(invalidManifest),
                ["ledgerTailHash"] = previousLedgerHash
            };
            document["completionHash"] = W24S0aIntegrity.CanonicalHash(document, "completionHash");
            var path = Path.Combine(candidateDirectory, "candidate-completion.json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(document.ToString(Formatting.None));
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        }

        private static bool PathEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left) && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadGuid(string metaPath)
        {
            var line = File.ReadLines(metaPath).FirstOrDefault(value => value.StartsWith("guid: ", StringComparison.Ordinal));
            var guid = line == null ? null : line.Substring("guid: ".Length).Trim();
            if (string.IsNullOrEmpty(guid) || guid.Length != 32 || guid.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))))
                throw new InvalidDataException("S0a authority Prefab meta has no canonical GUID.");
            return guid;
        }

        private void ApplyVisualMutation()
        {
            var target = command.Mutation.Target;
            var value = command.Mutation;
            var smoke = Child("Steady/Smoke");
            var embers = Child("Steady/Embers");
            var ignition = Child("Ignition");
            var light = candidate.GetComponentInChildren<Light>(true);
            switch (target)
            {
                case W24S0aMutationTarget.FragmentsSharedParentAngularVelocity: AddMotion(embers.gameObject, W24S0aFixtureMotion.Mode.SharedParentRotation, value.Number); break;
                case W24S0aMutationTarget.FlameSteadyStateLinearDrift: AddMotion(candidate, W24S0aFixtureMotion.Mode.LinearDrift, value.Number); break;
                case W24S0aMutationTarget.FlameLoopResetDiscontinuity: AddMotion(candidate, W24S0aFixtureMotion.Mode.LoopReset, value.Number); break;
                case W24S0aMutationTarget.ParticlesStopResidualSeconds: ConfigureParticleResidual(value.Number); break;
                case W24S0aMutationTarget.LightStopResidualSeconds: AddLightHold(light, value.Number); break;
                case W24S0aMutationTarget.SmokeSubjectOcclusionFraction: ScaleSmoke(smoke, value.Number); break;
                case W24S0aMutationTarget.RendererPrimarySmokeSortingOrder: SetSorting(smoke, value.SourceValue); break;
                case W24S0aMutationTarget.StateMachineIgnitionEnabled: AddIgnitionRule(ignition.gameObject, value); break;
                case W24S0aMutationTarget.StateMachineStopContinuityMode: AddStopRule(value.SourceValue); break;
                case W24S0aMutationTarget.LightEnabled: AddLightRule(light, value); break;
                case W24S0aMutationTarget.CaptureCameraScaleOffset: candidate.transform.localScale *= value.Number; break;
                default: throw new InvalidOperationException("Invalid evidence mutation cannot be visually applied.");
            }
            WriteLedger("visual-mutation-applied", new JObject { ["target"] = target.ToString(), ["value"] = value.SourceValue, ["cloneOnly"] = true });
        }

        private Transform Child(string path)
        {
            var found = candidate.transform.Find(path);
            if (found == null) throw new InvalidOperationException("S0a fixture clone lacks required sustained-flame child: " + path);
            return found;
        }
        private void SetControllerFloat(string property, float value)
        {
            var serialized = new SerializedObject(controller);
            var field = serialized.FindProperty(property);
            if (field == null) throw new InvalidOperationException("S0a fixture controller field is unavailable: " + property);
            field.floatValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void AddMotion(GameObject target, W24S0aFixtureMotion.Mode mode, float magnitude)
        { var mutation = target.AddComponent<W24S0aFixtureMotion>(); mutation.Configure(mode, magnitude); }
        private void ConfigureParticleResidual(float seconds)
        {
            var systems = new[] { Child("Steady/CoreFlame"), Child("Steady/OuterFlame"), Child("Steady/Smoke"), Child("Steady/Embers"), Child("StopTail") }
                .Select(transform => transform.GetComponent<ParticleSystem>()).ToArray();
            foreach (var system in systems)
            {
                if (system == null) throw new InvalidOperationException("S0a fixture residual mutation requires controlled ParticleSystems.");
                var main = system.main; main.startLifetime = new ParticleSystem.MinMaxCurve(seconds);
            }
            // The controller observes live particles through normal Update telemetry.  The
            // deadline is only a safety bound, so it must not clear the configured lifetime first.
            SetControllerFloat("cleanupDeadline", seconds + .05f);
            var readback = candidate.AddComponent<W24S0aParticleResidualConfiguration>();
            readback.Configure(seconds, systems);
        }
        private void AddLightHold(Light light, float seconds)
        { if (light == null) throw new InvalidOperationException("S0a fixture lacks the required real light."); var mutation = candidate.AddComponent<W24S0aFixtureBehaviour>(); mutation.ConfigureLightResidual(controller, light, seconds); }
        private static void ScaleSmoke(Transform smoke, float fraction)
        { smoke.localScale = Vector3.one * Mathf.Lerp(1f, 4f, Mathf.Clamp01(fraction)); }
        private void SetSorting(Transform smoke, string mode)
        {
            var smokeRenderer = smoke.GetComponent<ParticleSystemRenderer>();
            var primaryRenderer = Child("Steady/CoreFlame").GetComponent<ParticleSystemRenderer>();
            if (smokeRenderer == null) throw new InvalidOperationException("S0a fixture smoke has no ParticleSystemRenderer.");
            if (primaryRenderer == null) throw new InvalidOperationException("S0a fixture core flame has no primary ParticleSystemRenderer.");
            smokeRenderer.sortingLayerID = primaryRenderer.sortingLayerID;
            smokeRenderer.sortingOrder = primaryRenderer.sortingOrder + (mode == "inverted" ? 1 : -1);
            var readback = candidate.AddComponent<W24S0aSortingConfiguration>();
            readback.Configure(primaryRenderer.sortingLayerID, primaryRenderer.sortingOrder, smokeRenderer.sortingLayerID, smokeRenderer.sortingOrder, mode);
        }
        private void AddIgnitionRule(GameObject ignition, W24S0aTypedMutation value)
        { var mutation = candidate.AddComponent<W24S0aFixtureBehaviour>(); mutation.ConfigureIgnition(controller, ignition, value.Boolean ? value.Number : -1f); }
        private void AddStopRule(string mode)
        { var mutation = candidate.AddComponent<W24S0aFixtureBehaviour>(); mutation.ConfigureStop(controller, mode == "clear_immediate" ? 0f : .04f); }
        private void AddLightRule(Light light, W24S0aTypedMutation value)
        { if (light == null) throw new InvalidOperationException("S0a fixture lacks the required real light."); var mutation = candidate.AddComponent<W24S0aFixtureBehaviour>(); mutation.ConfigureLightEnabled(light, value.Boolean ? value.Number : -1f); }

        public void Cleanup()
        {
            if (!cleanupGate.TryEnter()) return;
            var failures = new List<Exception>();
            if (recorder != null && recorder.IsActive)
            {
                try { recorder.Abort(); }
                catch (Exception exception) { failures.Add(new InvalidOperationException("S0a recorder abort failed during cleanup.", exception)); }
            }
            if (candidate != null)
            {
                try { UnityEngine.Object.DestroyImmediate(candidate); candidate = null; controller = null; }
                catch (Exception exception) { failures.Add(new InvalidOperationException("S0a fixture clone destruction failed during cleanup.", exception)); }
            }
            var unchanged = false;
            try { unchanged = VerifyOfficialSourcesUnchanged(sourceHashes); }
            catch (Exception exception) { failures.Add(new InvalidOperationException("S0a formal source verification failed during cleanup.", exception)); }
            try
            {
                WriteLedger("cleanup", new JObject
                {
                    ["inMemoryCloneDestroyed"] = candidate == null,
                    ["captureCompleted"] = captureCompleted,
                    ["sourceHashesUnchanged"] = unchanged,
                    ["evidencePreservedForRecovery"] = true,
                    ["cleanupFailureCountBeforeLedger"] = failures.Count
                });
            }
            catch (Exception exception) { failures.Add(new InvalidOperationException("S0a cleanup ledger write failed.", exception)); }
            if (failures.Count == 1) throw failures[0];
            if (failures.Count > 1) throw new AggregateException("S0a cleanup completed with multiple failures.", failures);
        }

        public void Dispose() { Cleanup(); }

        private void CleanupWithoutMasking(Exception primary)
        {
            W24S0aFailureRecovery.CleanupWithoutMasking(primary, Cleanup);
        }

        private void WriteLedger(string kind, JObject details)
        {
            var ledgerDirectory = Path.Combine(candidateDirectory, "ledger");
            if (ledgerSequence > 0) W24S0aFixtureLedger.VerifyDirectory(ledgerDirectory);
            previousLedgerHash = W24S0aFixtureLedger.Append(ledgerDirectory, ledgerSequence++, kind, details, previousLedgerHash);
            W24S0aFixtureLedger.VerifyDirectory(ledgerDirectory);
        }

        public static Dictionary<string, string> SnapshotOfficialSources()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var repositoryRoot = Directory.GetParent(projectRoot).FullName;
            var authorityPaths = new[]
            {
                Path.Combine(projectRoot, SustainedFlameAuthoring.PrefabPath.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(projectRoot, SustainedFlameAuthoring.PreviewScenePath.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(projectRoot, SustainedFlameAuthoring.RecipePath.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(projectRoot, SustainedFlameAuthoring.ManifestPath.Replace('/', Path.DirectorySeparatorChar)),
                Path.Combine(repositoryRoot, "docs", "vfx-contracts", "sustained_flame_3d.contract.json"),
                Path.Combine(projectRoot, "Packages", "com.vfxcomposer.unity", "Runtime", "Components", "SustainedEffectController.cs"),
            };
            var sourcePaths = authorityPaths.Concat(W24S0aFormalCaptureProtocol.CaptureToolRelativePaths
                .Select(path => Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return sourcePaths.ToDictionary(path => path, HashFile, StringComparer.OrdinalIgnoreCase);
        }

        public static bool VerifyOfficialSourcesUnchanged(IDictionary<string, string> before)
        {
            if (before == null || before.Count == 0) throw new ArgumentException("S0a source hash snapshot is required.", "before");
            foreach (var pair in before)
                if (!File.Exists(pair.Key) || !string.Equals(pair.Value, HashFile(pair.Key), StringComparison.Ordinal)) throw new InvalidOperationException("S0a fixture observed a formal sustained-flame source change: " + pair.Key);
            return true;
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }

    /// <summary>Natural-Update-only visual perturbations attached to the in-memory clone.</summary>
    public sealed class W24S0aFixtureMotion : MonoBehaviour
    {
        public enum Mode { SharedParentRotation, LinearDrift, LoopReset }
        private Mode mode; private float magnitude; private Vector3 initial; private float nextReset; private bool loopPhase;
        public float Magnitude { get { return magnitude; } }
        public bool LoopPhase { get { return loopPhase; } }
        public void Configure(Mode value, float amount) { mode = value; magnitude = amount; initial = transform.localPosition; nextReset = Time.time + 1f; loopPhase = false; }
        private void Update()
        {
            if (mode == Mode.SharedParentRotation) transform.Rotate(Vector3.forward, magnitude * Time.deltaTime, Space.Self);
            else if (mode == Mode.LinearDrift) transform.localPosition += Vector3.right * magnitude * Time.deltaTime;
            else if (Time.time >= nextReset)
            {
                // Alternate offset and origin on every deterministic loop boundary: unlike a
                // one-time displacement this produces a repeatable visible seam every cycle.
                ApplyObservedLoopBoundary();
                nextReset += 1f;
            }
        }
        public void ApplyObservedLoopBoundary()
        {
            if (mode != Mode.LoopReset) throw new InvalidOperationException("Only loop-reset mutations have loop boundaries.");
            loopPhase = !loopPhase;
            transform.localPosition = initial + Vector3.right * (loopPhase ? magnitude : 0f);
        }
    }

    /// <summary>Telemetry-adjacent readback for residual-particle configuration; no simulation is driven here.</summary>
    public sealed class W24S0aParticleResidualConfiguration : MonoBehaviour
    {
        private ParticleSystem[] controlled = new ParticleSystem[0];
        public float TargetSeconds { get; private set; }
        public int ControlledSystemCount { get { return controlled.Length; } }
        public float MinimumConfiguredLifetime { get { return controlled.Length == 0 ? 0f : controlled.Min(system => system.main.startLifetime.constant); } }
        public void Configure(float targetSeconds, ParticleSystem[] systems) { TargetSeconds = targetSeconds; controlled = systems ?? new ParticleSystem[0]; }
    }

    /// <summary>Records relative sorting rather than hard-coding baseline renderer numbers.</summary>
    public sealed class W24S0aSortingConfiguration : MonoBehaviour
    {
        public int PrimaryLayerId { get; private set; } public int PrimaryOrder { get; private set; }
        public int SmokeLayerId { get; private set; } public int SmokeOrder { get; private set; }
        public string Mode { get; private set; }
        public void Configure(int primaryLayerId, int primaryOrder, int smokeLayerId, int smokeOrder, string mode)
        { PrimaryLayerId = primaryLayerId; PrimaryOrder = primaryOrder; SmokeLayerId = smokeLayerId; SmokeOrder = smokeOrder; Mode = mode; }
    }

    /// <summary>Lifecycle mutations observe ordinary controller state; they never advance it.</summary>
    public sealed class W24S0aFixtureBehaviour : MonoBehaviour
    {
        private SustainedEffectController controller; private Light controlledLight; private GameObject ignition;
        private float ignitionDelay = float.NaN, stopAt = float.NaN, lightResidual = float.NaN, forcedLight = float.NaN;
        private bool ignitionObserved, ignitionReleased, stoppingObserved, lightResidualActive;
        private float ignitionStartedAt, stoppingStartedAt;
        public bool IgnitionReleased { get { return ignitionReleased; } }
        public bool LightResidualActive { get { return lightResidualActive; } }
        public float LightResidualElapsed { get { return stoppingObserved ? Mathf.Max(0f, Time.time - stoppingStartedAt) : 0f; } }
        public float ConfiguredIgnitionDelay { get { return ignitionDelay; } }
        public float ConfiguredLightResidualSeconds { get { return lightResidual; } }
        public float ConfiguredStopCutSeconds { get { return stopAt; } }
        public void ConfigureIgnition(SustainedEffectController source, GameObject target, float delay) { controller = source; ignition = target; ignitionDelay = delay; }
        public void ConfigureStop(SustainedEffectController source, float delay) { controller = source; stopAt = delay; }
        public void ConfigureLightResidual(SustainedEffectController source, Light target, float seconds) { controller = source; controlledLight = target; lightResidual = seconds; }
        public void ConfigureLightEnabled(Light target, float intensity) { controlledLight = target; forcedLight = intensity; }
        private void Update()
        {
            if (controller != null && ignition != null && controller.State == SustainedEffectState.Starting && !ignitionObserved) { ignitionObserved = true; ignitionStartedAt = Time.time; }
            if (ignitionObserved && ignition != null && !ignitionReleased)
            {
                if (ignitionDelay < 0f) ignition.SetActive(false);
                else if (Time.time - ignitionStartedAt < ignitionDelay) ignition.SetActive(false);
                else
                {
                    ignition.SetActive(true);
                    foreach (var particle in ignition.GetComponentsInChildren<ParticleSystem>(true)) if (!particle.isPlaying) particle.Play(true);
                    ignitionReleased = true;
                }
            }
            if (controller != null && !float.IsNaN(stopAt) && controller.State == SustainedEffectState.Stopping && controller.StateElapsed >= stopAt) controller.ResetForPool();
            if (controller != null && controlledLight != null && !float.IsNaN(lightResidual) && controller.State == SustainedEffectState.Stopping && !stoppingObserved) { stoppingObserved = true; stoppingStartedAt = Time.time; lightResidualActive = true; }
            if (stoppingObserved && controlledLight != null && !float.IsNaN(lightResidual))
            {
                if (Time.time - stoppingStartedAt <= lightResidual) { controlledLight.enabled = true; controlledLight.intensity = Mathf.Max(.02f, controlledLight.intensity); lightResidualActive = true; }
                else { controlledLight.enabled = false; controlledLight.intensity = 0f; lightResidualActive = false; }
            }
            if (controlledLight != null && !float.IsNaN(forcedLight)) { if (forcedLight < 0f) { controlledLight.enabled = false; controlledLight.intensity = 0f; } else { controlledLight.enabled = true; controlledLight.intensity = forcedLight; } }
        }
    }

    /// <summary>Canonical, hash-chained, write-once ledger used for fixture lifecycle audit.</summary>
    public static class W24S0aFixtureLedger
    {
        public static string Append(string directory, int sequence, string kind, JObject details, string previousEntryHash)
        {
            if (string.IsNullOrEmpty(directory) || sequence < 0 || string.IsNullOrEmpty(kind) || details == null) throw new ArgumentException("S0a ledger entry requires directory, non-negative sequence, kind, and details.");
            if (kind.Any(character => !(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '-') || kind[0] == '-' || kind[kind.Length - 1] == '-')
                throw new ArgumentException("S0a ledger kind must be a safe lower-kebab token.", "kind");
            if (previousEntryHash != null && (previousEntryHash.Length != 71 || !previousEntryHash.StartsWith("sha256:", StringComparison.Ordinal) || previousEntryHash.Skip(7).Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))))
                throw new ArgumentException("S0a previous ledger hash must be canonical sha256.", "previousEntryHash");
            var document = new JObject
            {
                ["schema"] = "w24-s0a-fixture-ledger/v2", ["sequence"] = sequence, ["kind"] = kind,
                ["details"] = details, ["recordedUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                ["previousEntryHash"] = previousEntryHash == null ? JValue.CreateNull() : new JValue(previousEntryHash)
            };
            document["entryHash"] = CanonicalHash(document, "entryHash");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, sequence.ToString("D4", CultureInfo.InvariantCulture) + "-" + kind + ".json");
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(document.ToString(Formatting.None));
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            return (string)document["entryHash"];
        }

        public static void VerifyDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) throw new InvalidDataException("S0a ledger directory does not exist.");
            string prior = null;
            var entries = Directory.GetFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly);
            if (entries.Any(path => Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || !path.EndsWith(".json", StringComparison.Ordinal)))
                throw new InvalidDataException("S0a ledger permits only direct, non-linked JSON lifecycle entries.");
            var files = entries.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (files.Length == 0) throw new InvalidDataException("S0a ledger is empty.");
            for (var index = 0; index < files.Length; index++)
            {
                var entry = ParseLedgerEntry(File.ReadAllText(files[index], new UTF8Encoding(false, true)));
                var kind = (string)entry["kind"];
                var expectedName = index.ToString("D4", CultureInfo.InvariantCulture) + "-" + kind + ".json";
                if ((File.GetAttributes(files[index]) & FileAttributes.ReadOnly) == 0 || !string.Equals(Path.GetFileName(files[index]), expectedName, StringComparison.Ordinal) || (int?)entry["sequence"] != index || !string.Equals((string)entry["schema"], "w24-s0a-fixture-ledger/v2", StringComparison.Ordinal) || !string.Equals((string)entry["previousEntryHash"], prior, StringComparison.Ordinal) || !string.Equals((string)entry["entryHash"], CanonicalHash(entry, "entryHash"), StringComparison.Ordinal))
                    throw new InvalidDataException("S0a ledger chain verification failed at " + Path.GetFileName(files[index]) + ".");
                prior = (string)entry["entryHash"];
            }
        }

        private static string CanonicalHash(JObject document, string omittedField)
        {
            var copy = (JObject)document.DeepClone(); copy.Remove(omittedField);
            var text = new StringBuilder();
            using (var writer = new JsonTextWriter(new StringWriter(text, CultureInfo.InvariantCulture)) { Formatting = Formatting.None }) { WriteCanonical(writer, copy); writer.Flush(); }
            using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static void WriteCanonical(JsonWriter writer, JToken token)
        {
            var obj = token as JObject;
            if (obj != null) { writer.WriteStartObject(); foreach (var property in obj.Properties().OrderBy(property => property.Name, StringComparer.Ordinal)) { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); } writer.WriteEndObject(); return; }
            var array = token as JArray;
            if (array != null) { writer.WriteStartArray(); foreach (var child in array) WriteCanonical(writer, child); writer.WriteEndArray(); return; }
            token.WriteTo(writer);
        }

        private static JObject ParseLedgerEntry(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("S0a ledger entry JSON is empty.");
            using (var source = new StringReader(text))
            using (var reader = new JsonTextReader(source)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Double
            })
            {
                JObject result;
                try
                {
                    result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("S0a ledger entry contains trailing JSON content.");
                }
                catch (InvalidDataException) { throw; }
                catch (Exception exception) { throw new InvalidDataException("S0a ledger entry is not strict JSON.", exception); }
                return result;
            }
        }
    }

    /// <summary>Applies invalid-evidence mutations only to a sealed capture copy, never to the recorder's source directory.</summary>
    public static class W24S0aInvalidEvidenceInjector
    {
        public static void Inject(string candidateDirectory, string sealedCaptureDirectory, W24S0aTypedMutation mutation, string commandHash, Action<string, JObject> writeLedger)
        {
            if (mutation == null || !mutation.IsInvalidEvidence || writeLedger == null || !W24S0aIntegrity.IsCanonicalHash(commandHash)) throw new ArgumentException("S0a invalid-evidence injection requires the specific post-capture mutation, command hash, and ledger.");
            var root = Path.GetFullPath(candidateDirectory);
            var capture = Path.GetFullPath(sealedCaptureDirectory);
            if (!capture.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("S0a invalid-evidence injection requires a capture directory beneath its candidate root.");
            ValidateSealedCapture(capture, commandHash);
            var derived = Path.Combine(root, "invalid-evidence");
            if (Directory.Exists(derived)) throw new InvalidOperationException("S0a invalid-evidence copy is write-once.");
            CopyDirectory(capture, derived);
            foreach (var file in Directory.GetFiles(derived, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            if (mutation.SourceValue == "missing_key_frame")
            {
                var frame = Directory.GetFiles(Path.Combine(derived, "frames"), "*_beauty.png", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
                if (frame == null) throw new InvalidOperationException("S0a invalid-evidence mutation needs at least one captured beauty frame.");
                File.Delete(frame);
                writeLedger("invalid-evidence-injected", new JObject { ["kind"] = "missing_key_frame", ["sourceCapture"] = "capture", ["derivedEvidence"] = "invalid-evidence", ["deletedRelativePath"] = Relative(derived, frame), ["postCaptureOnly"] = true });
            }
            else if (mutation.SourceValue == "sha256_mismatch")
            {
                File.AppendAllText(Path.Combine(derived, "capture-metadata.json"), "\n", new UTF8Encoding(false));
                writeLedger("invalid-evidence-injected", new JObject { ["kind"] = "sha256_mismatch", ["sourceCapture"] = "capture", ["derivedEvidence"] = "invalid-evidence", ["tamperedRelativePath"] = "capture-metadata.json", ["postCaptureOnly"] = true });
            }
            else throw new InvalidDataException("Unsupported typed invalid-evidence value.");
            var sourceSeal = Path.Combine(capture, "evidence-seal.json");
            var manifest = new JObject
            {
                ["schema"] = "w24-s0a-derived-invalid-evidence/v1",
                ["commandHash"] = commandHash,
                ["sourceCaptureSealHash"] = W24S0aIntegrity.HashFile(sourceSeal),
                ["kind"] = mutation.SourceValue,
                ["derivation"] = mutation.SourceValue == "missing_key_frame" ? "deleted-beauty-frame" : "metadata-hash-mismatch"
            };
            manifest["derivedManifestHash"] = W24S0aIntegrity.CanonicalHash(manifest, "derivedManifestHash");
            var manifestPath = Path.Combine(derived, "invalid-evidence-manifest.json");
            using (var stream = new FileStream(manifestPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(manifest.ToString(Formatting.None));
            foreach (var file in Directory.GetFiles(derived, "*", SearchOption.AllDirectories)) File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(directory.Replace(source, destination));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, file.Replace(source, destination), false);
        }
        private static string Relative(string root, string path) { return path.Substring(root.Length + 1).Replace('\\', '/'); }

        /// <summary>Checks recorder lock binding plus every metadata-declared frame file/hash; read-only alone is not evidence sealing.</summary>
        public static void ValidateSealedCapture(string capture, string expectedCommandHash = null)
        {
            var lockPath = Path.Combine(capture, "evidence-lock.json");
            var metadataPath = Path.Combine(capture, "capture-metadata.json");
            var sealPath = Path.Combine(capture, "evidence-seal.json");
            if (!File.Exists(lockPath) || !File.Exists(metadataPath) || !File.Exists(sealPath) || (File.GetAttributes(metadataPath) & FileAttributes.ReadOnly) == 0 || (File.GetAttributes(sealPath) & FileAttributes.ReadOnly) == 0) throw new InvalidOperationException("S0a invalid-evidence injection requires final sealed recorder output.");
            JObject lockDocument, metadata, seal;
            try { lockDocument = JObject.Parse(File.ReadAllText(lockPath, Encoding.UTF8)); metadata = JObject.Parse(File.ReadAllText(metadataPath, Encoding.UTF8)); seal = JObject.Parse(File.ReadAllText(sealPath, Encoding.UTF8)); }
            catch (JsonException exception) { throw new InvalidDataException("S0a recorder seal documents are malformed.", exception); }
            if (!string.Equals((string)lockDocument["candidateId"], (string)metadata["candidateId"], StringComparison.Ordinal) || !string.Equals((string)lockDocument["captureProfileSha256"], (string)metadata["captureProfileSha256"], StringComparison.Ordinal)) throw new InvalidDataException("S0a evidence lock does not bind capture metadata identity.");
            ValidateFinalSeal(capture, seal, metadata, expectedCommandHash);
            var frames = metadata["frames"] as JArray;
            if (frames == null || frames.Count == 0) throw new InvalidDataException("S0a sealed capture metadata has no retained source frames.");
            foreach (var frame in frames)
            {
                var frameObject = frame as JObject;
                var beauty = frameObject == null ? null : frameObject["beauty"] as JObject;
                ValidateDeclaredArtifact(capture, beauty);
                var diagnostics = frameObject == null ? null : frameObject["diagnostics"] as JArray;
                if (diagnostics == null || diagnostics.Count == 0) throw new InvalidDataException("S0a sealed capture frame lacks diagnostic artifacts.");
                foreach (var diagnostic in diagnostics) ValidateDeclaredArtifact(capture, diagnostic as JObject);
            }
        }

        /// <summary>
        /// Candidate completion is stricter than raw-seal verification: it proves that the
        /// sealed metadata is a formal S0a run for this command, rather than merely a
        /// self-consistent directory with one image in it.
        /// </summary>
        public static void ValidateFormalCaptureSemantics(string capture, W24S0aOperatorCommand command)
        {
            if (command == null) throw new ArgumentNullException("command");
            ValidateSealedCapture(capture, command.CommandHash);
            var metadata = ParseObject(Path.Combine(capture, "capture-metadata.json"), "S0a formal capture metadata");
            RequireExactFields(metadata, "S0a formal capture metadata", "schema", "candidateId", "captureModePolicy", "executedInBatchMode", "frameRetentionPolicy", "retainedFrameIndices", "retainedFrameIndicesSha256", "formalPlayerLoop", "captureProfile", "captureProfileSha256", "sourceHashes", "diagnosticPassManifest", "typedRawDiagnostics", "metricInputs", "metricReports", "semanticTelemetry", "supplementalDiagnostics", "frames");
            if (!string.Equals((string)metadata["schema"], "w24-s0a-capture-evidence/v1", StringComparison.Ordinal)
                || !string.Equals((string)metadata["candidateId"], command.SampleId, StringComparison.Ordinal)
                || metadata["executedInBatchMode"].Type != JTokenType.Boolean || !(bool)metadata["executedInBatchMode"])
                throw new InvalidDataException("S0a completed candidate metadata is not a graphics-backed formal capture for its command.");

            var playerLoop = metadata["formalPlayerLoop"] as JObject;
            RequireExactFields(playerLoop, "S0a formal PlayerLoop provenance", "observedSerial", "consumedSerial", "allObservedFramesConsumed");
            var observedSerial = (long?)playerLoop["observedSerial"];
            var consumedSerial = (long?)playerLoop["consumedSerial"];
            if (!observedSerial.HasValue || observedSerial.Value <= 0L || consumedSerial != observedSerial
                || playerLoop["allObservedFramesConsumed"].Type != JTokenType.Boolean || !(bool)playerLoop["allObservedFramesConsumed"])
                throw new InvalidDataException("S0a formal capture did not consume every observed natural PlayerLoop frame.");

            // S0a calibrates visual QA against Beauty/effect-only evidence. Typed render metrics
            // belong to the later S3 contracts and must not silently enter this frozen cohort.
            foreach (var field in new[] { "typedRawDiagnostics", "metricInputs", "metricReports" })
            {
                var records = metadata[field] as JArray;
                if (records == null || records.Count != 0)
                    throw new InvalidDataException("S0a formal capture requires an explicitly empty " + field + " array.");
            }

            var profile = metadata["captureProfile"] as JObject;
            if (profile == null || !string.Equals((string)metadata["captureProfileSha256"], W24S0aIntegrity.HashText(profile.ToString(Formatting.None)), StringComparison.Ordinal))
                throw new InvalidDataException("S0a completed candidate metadata has an unbound Capture Profile.");
            ValidateFrozenProfile(profile, command);
            var retained = W24S0aFormalCaptureProtocol.RetainedFrames;
            if (!string.Equals((string)metadata["retainedFrameIndicesSha256"], W24S0aIntegrity.HashText(string.Join(",", retained)), StringComparison.Ordinal)
                || !IntArrayEquals(metadata["retainedFrameIndices"] as JArray, retained))
                throw new InvalidDataException("S0a metadata retained-frame table is not the frozen formal table.");

            ValidateSourceHashes(metadata["sourceHashes"] as JObject);
            ValidateFormalFrameSet(capture, metadata["frames"] as JArray, command, "capture metadata frames");
            if (!(metadata["supplementalDiagnostics"] is JArray) || ((JArray)metadata["supplementalDiagnostics"]).Count != 0)
                throw new InvalidDataException("S0a formal capture does not permit unplanned supplemental diagnostic artifacts.");
            ValidateSemanticTelemetry(capture, metadata["semanticTelemetry"] as JArray, command);
            ValidateFormalCaptureTree(capture, command, null);
        }

        private static void ValidateFrozenProfile(JObject profile, W24S0aOperatorCommand command)
        {
            RequireExactFields(profile, "S0a Capture Profile", "profileVersion", "unityVersion", "urpVersion", "graphicsApi", "graphicsDevice", "graphicsDriverVersion", "renderTextureFormat", "rendererAsset", "volume", "scenePath", "serializedCameraReference", "resolution", "fps", "background", "colorSpace", "hdr", "msaa", "bloom", "toneMapping", "canonicalSeed", "robustnessSeeds", "retainedFrameIndices", "retainedFrameIndicesSha256");
            var expectedRobustness = W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(command.FixedSeed);
            var renderer = profile["rendererAsset"] as JObject; var volume = profile["volume"] as JObject;
            RequireExactFields(renderer, "S0a Capture Profile renderer asset", "reference", "sha256");
            RequireExactFields(volume, "S0a Capture Profile volume", "reference", "sha256");
            var project = Directory.GetParent(Application.dataPath).FullName;
            var rendererPath = Path.Combine(project, W24S0aFormalCaptureProtocol.RendererAssetReference.Replace('/', Path.DirectorySeparatorChar));
            var graphicsPath = Path.Combine(project, "ProjectSettings", "GraphicsSettings.asset");
            if (!string.Equals((string)profile["profileVersion"], "w24-s0a-formal-calibration-capture-profile/v1", StringComparison.Ordinal)
                || !string.Equals((string)profile["scenePath"], SustainedFlameAuthoring.PreviewScenePath, StringComparison.Ordinal)
                || !string.Equals((string)profile["serializedCameraReference"], SustainedFlameAuthoring.PreviewScenePath + "#MainCamera", StringComparison.Ordinal)
                || !string.Equals((string)profile["renderTextureFormat"], "ARGB32", StringComparison.Ordinal)
                || !IntArrayEquals(profile["resolution"] as JArray, new[] { 960, 540 })
                || !string.Equals((string)renderer["reference"], W24S0aFormalCaptureProtocol.RendererAssetReference, StringComparison.Ordinal) || !string.Equals((string)renderer["sha256"], HashFile(rendererPath), StringComparison.Ordinal)
                || !string.Equals((string)volume["reference"], W24S0aFormalCaptureProtocol.VolumeReference, StringComparison.Ordinal) || !string.Equals((string)volume["sha256"], HashFile(graphicsPath), StringComparison.Ordinal)
                || UInt32(profile["canonicalSeed"], "canonicalSeed") != command.FixedSeed
                || !UIntArrayEquals(profile["robustnessSeeds"] as JArray, expectedRobustness)
                || !IntArrayEquals(profile["retainedFrameIndices"] as JArray, W24S0aFormalCaptureProtocol.RetainedFrames)
                || !string.Equals((string)profile["retainedFrameIndicesSha256"], W24S0aIntegrity.HashText(string.Join(",", W24S0aFormalCaptureProtocol.RetainedFrames)), StringComparison.Ordinal)
                || (int?)profile["fps"] != 60)
                throw new InvalidDataException("S0a Capture Profile does not bind the authority scene, fixed seed derivation, or frozen sampling table.");
        }

        private static void ValidateSourceHashes(JObject sources)
        {
            if (sources == null) throw new InvalidDataException("S0a formal metadata has no source hashes.");
            RequireExactFields(sources, "S0a source hashes", "scene", "prefab", "manifest", "captureTool");
            var project = Directory.GetParent(Application.dataPath).FullName;
            var scene = Path.Combine(project, SustainedFlameAuthoring.PreviewScenePath.Replace('/', Path.DirectorySeparatorChar));
            var prefab = Path.Combine(project, SustainedFlameAuthoring.PrefabPath.Replace('/', Path.DirectorySeparatorChar));
            var manifest = Path.Combine(project, SustainedFlameAuthoring.ManifestPath.Replace('/', Path.DirectorySeparatorChar));
            var sceneRecord = sources["scene"] as JObject; var prefabRecord = sources["prefab"] as JObject; var manifestRecord = sources["manifest"] as JObject; var toolRecord = sources["captureTool"] as JObject;
            RequireExactFields(sceneRecord, "S0a scene source", "path", "sha256");
            RequireExactFields(prefabRecord, "S0a Prefab source", "path", "guid", "sha256");
            RequireExactFields(manifestRecord, "S0a Manifest source", "path", "sha256", "buildHash");
            RequireExactFields(toolRecord, "S0a capture-tool source", "path", "version", "sha256");
            if (!PathEquals((string)sceneRecord["path"], scene) || !string.Equals((string)sceneRecord["sha256"], HashFile(scene), StringComparison.Ordinal)
                || !PathEquals((string)prefabRecord["path"], prefab) || !string.Equals((string)prefabRecord["sha256"], HashFile(prefab), StringComparison.Ordinal) || !string.Equals((string)prefabRecord["guid"], ReadGuid(prefab + ".meta"), StringComparison.Ordinal)
                || !PathEquals((string)manifestRecord["path"], manifest) || !string.Equals((string)manifestRecord["sha256"], HashFile(manifest), StringComparison.Ordinal) || !string.Equals((string)manifestRecord["buildHash"], ReadBuildHash(manifest), StringComparison.Ordinal)
                || !string.Equals((string)toolRecord["path"], W24S0aFormalCaptureProtocol.CaptureToolIdentityPath, StringComparison.Ordinal) || !string.Equals((string)toolRecord["version"], W24S0aFormalCaptureProtocol.CaptureToolVersion, StringComparison.Ordinal) || !string.Equals((string)toolRecord["sha256"], W24S0aFormalCaptureProtocol.CaptureToolSha256(), StringComparison.Ordinal))
                throw new InvalidDataException("S0a formal metadata source identities do not match the authority scene, Runtime Entry, Manifest, or capture tool.");
        }

        private static void ValidateFormalFrameSet(string capture, JArray frames, W24S0aOperatorCommand command, string label)
        {
            var seeds = new[] { command.FixedSeed }.Concat(W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(command.FixedSeed)).ToArray();
            var expected = new HashSet<string>(seeds.SelectMany(seed => W24S0aFormalCaptureProtocol.RetainedFrames.Select(frame => seed.ToString(CultureInfo.InvariantCulture) + ":" + frame.ToString(CultureInfo.InvariantCulture))), StringComparer.Ordinal);
            if (frames == null || frames.Count != expected.Count) throw new InvalidDataException("S0a " + label + " does not contain the exact three-seed retained-frame cohort.");
            foreach (var token in frames)
            {
                var frame = token as JObject;
                RequireExactFields(frame, "S0a " + label + " entry", "frameIndex", "simulationTime", "state", "seed", "beauty", "diagnostics");
                var key = UInt32(frame["seed"], "frame seed").ToString(CultureInfo.InvariantCulture) + ":" + ((int?)frame["frameIndex"] ?? -1).ToString(CultureInfo.InvariantCulture);
                if (!expected.Remove(key) || string.IsNullOrWhiteSpace((string)frame["state"])) throw new InvalidDataException("S0a " + label + " has an unexpected, duplicate, or state-less frame.");
                ValidateDeclaredArtifact(capture, frame["beauty"] as JObject);
                var diagnostics = frame["diagnostics"] as JArray;
                if (diagnostics == null || diagnostics.Count != 1) throw new InvalidDataException("S0a formal frame must have exactly one effect-only diagnostic.");
                ValidateDeclaredArtifact(capture, diagnostics[0] as JObject);
            }
            if (expected.Count != 0) throw new InvalidDataException("S0a " + label + " is missing a required seed/frame pair.");
        }

        private static void ValidateSemanticTelemetry(string capture, JArray telemetryRecords, W24S0aOperatorCommand command)
        {
            if (telemetryRecords == null || telemetryRecords.Count != 1) throw new InvalidDataException("S0a formal capture requires exactly one semantic telemetry artifact.");
            var record = telemetryRecords[0] as JObject;
            RequireExactFields(record, "S0a semantic telemetry record", "kind", "description", "file", "sha256");
            if (!string.Equals((string)record["kind"], "semantic-telemetry", StringComparison.Ordinal) || !string.Equals((string)record["file"], "diagnostics/semantic-telemetry.json", StringComparison.Ordinal)) throw new InvalidDataException("S0a formal semantic telemetry record is not the required artifact.");
            ValidateDeclaredArtifact(capture, record);
            var telemetry = ParseObject(Path.Combine(capture, "diagnostics", "semantic-telemetry.json"), "S0a semantic telemetry");
            RequireExactFields(telemetry, "S0a semantic telemetry", "schema", "sampleId", "fixedSeed", "frames");
            if (!string.Equals((string)telemetry["schema"], "w24-s0a-semantic-telemetry/v1", StringComparison.Ordinal) || !string.Equals((string)telemetry["sampleId"], command.SampleId, StringComparison.Ordinal) || UInt32(telemetry["fixedSeed"], "telemetry fixedSeed") != command.FixedSeed)
                throw new InvalidDataException("S0a semantic telemetry does not bind this operator command.");
            ValidateSemanticTelemetryFrameSet(telemetry["frames"] as JArray, command);
        }

        private static void ValidateSemanticTelemetryFrameSet(JArray frames, W24S0aOperatorCommand command)
        {
            var seeds = new[] { command.FixedSeed }.Concat(W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(command.FixedSeed)).ToArray();
            var expected = new HashSet<string>(seeds.SelectMany(seed => W24S0aFormalCaptureProtocol.RetainedFrames.Select(frame => seed.ToString(CultureInfo.InvariantCulture) + ":" + frame.ToString(CultureInfo.InvariantCulture))), StringComparer.Ordinal);
            if (frames == null || frames.Count != expected.Count) throw new InvalidDataException("S0a semantic telemetry does not contain the exact three-seed retained-frame cohort.");
            foreach (var token in frames)
            {
                var frame = token as JObject;
                RequireExactFields(frame, "S0a semantic telemetry frame", "frameIndex", "state", "seed", "liveParticleCount", "enabledRendererCount", "enabledLightCount", "transitionSerial", "cleanupComplete");
                var key = UInt32(frame["seed"], "telemetry seed").ToString(CultureInfo.InvariantCulture) + ":" + ((int?)frame["frameIndex"] ?? -1).ToString(CultureInfo.InvariantCulture);
                if (!expected.Remove(key) || string.IsNullOrWhiteSpace((string)frame["state"])) throw new InvalidDataException("S0a semantic telemetry has an unexpected, duplicate, or state-less frame.");
            }
            if (expected.Count != 0) throw new InvalidDataException("S0a semantic telemetry is missing a required seed/frame pair.");
        }

        internal static void ValidateFormalCaptureTree(string capture, W24S0aOperatorCommand command, W24S0aTypedMutation derivedMutation)
        {
            RejectReparsePoints(capture);
            var expectedRootFiles = new HashSet<string>(StringComparer.Ordinal) { "evidence-lock.json", "diagnostic-pass-manifest.json", "capture-metadata.json", "evidence-seal.json" };
            var expectedRootDirectories = new HashSet<string>(StringComparer.Ordinal) { "frames", "diagnostics" };
            if (derivedMutation != null) expectedRootFiles.Add("invalid-evidence-manifest.json");
            ValidateDirectEntries(capture, expectedRootFiles, expectedRootDirectories, "S0a capture root");
            var seeds = new[] { command.FixedSeed }.Concat(W24S0aFormalCaptureProtocol.DeriveRobustnessSeeds(command.FixedSeed)).ToArray();
            var framesRoot = Path.Combine(capture, "frames");
            ValidateDirectEntries(framesRoot, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(seeds.Select(seed => "seed_" + seed.ToString(CultureInfo.InvariantCulture)), StringComparer.Ordinal), "S0a frames root");
            var missingBeautyAllowed = derivedMutation != null && derivedMutation.SourceValue == "missing_key_frame";
            var missingBeautySeen = false;
            foreach (var seed in seeds)
            {
                var expectedFiles = new HashSet<string>(W24S0aFormalCaptureProtocol.RetainedFrames.SelectMany(frame => new[] { "frame_" + frame.ToString("D5", CultureInfo.InvariantCulture) + "_beauty.png", "frame_" + frame.ToString("D5", CultureInfo.InvariantCulture) + "_effect-only.png" }), StringComparer.Ordinal);
                ValidateSeedFrameEntries(Path.Combine(framesRoot, "seed_" + seed.ToString(CultureInfo.InvariantCulture)), expectedFiles, missingBeautyAllowed, ref missingBeautySeen);
            }
            if (missingBeautyAllowed != missingBeautySeen) throw new InvalidDataException("S0a missing-frame derived evidence must remove exactly one beauty artifact.");
            ValidateDirectEntries(Path.Combine(capture, "diagnostics"), new HashSet<string>(StringComparer.Ordinal) { "semantic-telemetry.json" }, new HashSet<string>(StringComparer.Ordinal), "S0a diagnostics directory");
        }

        private static void ValidateSeedFrameEntries(string directory, ISet<string> expectedFiles, bool missingBeautyAllowed, ref bool missingBeautySeen)
        {
            if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("S0a seed frame directory is missing or linked.");
            var entries = Directory.GetFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly);
            if (entries.Any(path => Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)) throw new InvalidDataException("S0a seed frame directory contains a foreign directory or link.");
            var actual = new HashSet<string>(entries.Select(Path.GetFileName), StringComparer.Ordinal);
            if (!actual.IsSubsetOf(expectedFiles)) throw new InvalidDataException("S0a seed frame directory contains a foreign artifact.");
            expectedFiles.ExceptWith(actual);
            if (expectedFiles.Count == 0) return;
            if (!missingBeautyAllowed || missingBeautySeen || expectedFiles.Count != 1 || !expectedFiles.Single().EndsWith("_beauty.png", StringComparison.Ordinal)) throw new InvalidDataException("S0a seed frame directory is missing a required artifact.");
            missingBeautySeen = true;
        }

        private static void ValidateDirectEntries(string directory, ISet<string> expectedFiles, ISet<string> expectedDirectories, string label)
        {
            if (!Directory.Exists(directory) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException(label + " is missing or linked.");
            var entries = Directory.GetFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly);
            if (entries.Length != expectedFiles.Count + expectedDirectories.Count) throw new InvalidDataException(label + " has a foreign or missing entry.");
            foreach (var entry in entries)
            {
                var name = Path.GetFileName(entry);
                var isDirectory = Directory.Exists(entry);
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0 || (isDirectory ? !expectedDirectories.Remove(name) : !expectedFiles.Remove(name))) throw new InvalidDataException(label + " has a foreign, linked, or wrong-kind entry: " + name);
            }
            if (expectedFiles.Count != 0 || expectedDirectories.Count != 0) throw new InvalidDataException(label + " is missing an expected entry.");
        }

        private static void RejectReparsePoints(string directory)
        {
            foreach (var entry in Directory.GetFileSystemEntries(directory, "*", SearchOption.AllDirectories))
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("S0a formal capture evidence may not contain linked/reparse artifacts.");
        }

        private static void RequireExactFields(JObject value, string label, params string[] fields)
        {
            if (value == null || !value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(fields.OrderBy(name => name, StringComparer.Ordinal))) throw new InvalidDataException(label + " fields are not exact.");
        }

        private static JObject ParseObject(string path, string label)
        {
            try { return JObject.Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception exception) when (exception is IOException || exception is JsonException) { throw new InvalidDataException(label + " is malformed.", exception); }
        }

        private static uint UInt32(JToken token, string label)
        {
            long value;
            try { value = token == null || token.Type != JTokenType.Integer ? -1L : Convert.ToInt64(((JValue)token).Value, CultureInfo.InvariantCulture); }
            catch (Exception) { value = -1L; }
            if (value < 0 || value > uint.MaxValue) throw new InvalidDataException("S0a " + label + " must be an unsigned 32-bit integer.");
            return (uint)value;
        }

        private static bool IntArrayEquals(JArray actual, IEnumerable<int> expected) { return actual != null && actual.Select(value => (int?)value).SequenceEqual(expected.Select(value => (int?)value)); }
        private static bool UIntArrayEquals(JArray actual, IEnumerable<uint> expected) { return actual != null && actual.Select(value => value == null ? (uint?)null : UInt32(value, "robustness seed")).SequenceEqual(expected.Select(value => (uint?)value)); }
        private static bool PathEquals(string left, string right) { return !string.IsNullOrEmpty(left) && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        private static string ReadGuid(string metaPath) { var line = File.ReadLines(metaPath).FirstOrDefault(value => value.StartsWith("guid: ", StringComparison.Ordinal)); var guid = line == null ? null : line.Substring("guid: ".Length).Trim(); if (string.IsNullOrEmpty(guid) || guid.Length != 32 || guid.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))) throw new InvalidDataException("S0a authority Prefab meta has no canonical GUID."); return guid; }
        private static string ReadBuildHash(string manifestPath) { var value = (string)ParseObject(manifestPath, "S0a authority BuildManifest")["buildHash"]; if (string.IsNullOrEmpty(value) || value.Length != 64 || value.Any(character => !((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))) throw new InvalidDataException("S0a authority BuildManifest has no canonical buildHash."); return "sha256:" + value; }

        public static string ValidateDerivedInvalidEvidence(string candidateDirectory, string sealedCaptureDirectory, W24S0aTypedMutation mutation, string commandHash)
        {
            if (mutation == null || !mutation.IsInvalidEvidence || !W24S0aIntegrity.IsCanonicalHash(commandHash)) throw new ArgumentException("S0a derived invalid-evidence validation requires its exact invalid mutation and command hash.");
            ValidateSealedCapture(sealedCaptureDirectory, commandHash);
            var derived = Path.Combine(candidateDirectory, "invalid-evidence");
            var manifestPath = Path.Combine(derived, "invalid-evidence-manifest.json");
            if (!Directory.Exists(derived) || !File.Exists(manifestPath) || (File.GetAttributes(manifestPath) & FileAttributes.ReadOnly) == 0)
                throw new InvalidDataException("S0a invalid-evidence sample has no sealed derived evidence manifest.");
            JObject manifest;
            try { manifest = JObject.Parse(File.ReadAllText(manifestPath, Encoding.UTF8)); }
            catch (JsonException exception) { throw new InvalidDataException("S0a derived invalid-evidence manifest is malformed.", exception); }
            var required = new[] { "schema", "commandHash", "sourceCaptureSealHash", "kind", "derivation", "derivedManifestHash" };
            if (!manifest.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(required.OrderBy(name => name, StringComparer.Ordinal))
                || !string.Equals((string)manifest["schema"], "w24-s0a-derived-invalid-evidence/v1", StringComparison.Ordinal)
                || !string.Equals((string)manifest["commandHash"], commandHash, StringComparison.Ordinal)
                || !string.Equals((string)manifest["sourceCaptureSealHash"], W24S0aIntegrity.HashFile(Path.Combine(sealedCaptureDirectory, "evidence-seal.json")), StringComparison.Ordinal)
                || !string.Equals((string)manifest["kind"], mutation.SourceValue, StringComparison.Ordinal)
                || !string.Equals((string)manifest["derivedManifestHash"], W24S0aIntegrity.CanonicalHash(manifest, "derivedManifestHash"), StringComparison.Ordinal))
                throw new InvalidDataException("S0a derived invalid-evidence manifest identity is invalid.");
            if (mutation.SourceValue == "missing_key_frame")
            {
                var rawBeauty = Directory.GetFiles(Path.Combine(sealedCaptureDirectory, "frames"), "*_beauty.png", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).FirstOrDefault();
                var derivedBeauty = rawBeauty == null ? null : Path.Combine(derived, Relative(sealedCaptureDirectory, rawBeauty).Replace('/', Path.DirectorySeparatorChar));
                if (rawBeauty == null || File.Exists(derivedBeauty) || !string.Equals((string)manifest["derivation"], "deleted-beauty-frame", StringComparison.Ordinal))
                    throw new InvalidDataException("S0a missing-frame derived evidence does not contain the required post-capture deletion.");
            }
            else if (mutation.SourceValue == "sha256_mismatch")
            {
                var rawMetadata = Path.Combine(sealedCaptureDirectory, "capture-metadata.json");
                var derivedMetadata = Path.Combine(derived, "capture-metadata.json");
                if (!File.Exists(derivedMetadata) || string.Equals(W24S0aIntegrity.HashFile(rawMetadata), W24S0aIntegrity.HashFile(derivedMetadata), StringComparison.Ordinal) || !string.Equals((string)manifest["derivation"], "metadata-hash-mismatch", StringComparison.Ordinal))
                    throw new InvalidDataException("S0a metadata-mismatch derived evidence does not contain the required post-capture mismatch.");
            }
            else throw new InvalidDataException("Unsupported typed invalid-evidence value.");
            return (string)manifest["derivedManifestHash"];
        }

        private static void ValidateFinalSeal(string capture, JObject seal, JObject metadata, string expectedCommandHash)
        {
            var required = new[] { "schema", "candidateId", "captureProfileSha256", "artifacts", "provenance", "sealHash" };
            if (!seal.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(required.OrderBy(name => name, StringComparer.Ordinal))
                || !string.Equals((string)seal["schema"], "w24-s0a-final-evidence-seal/v1", StringComparison.Ordinal)
                || !string.Equals((string)seal["candidateId"], (string)metadata["candidateId"], StringComparison.Ordinal)
                || !string.Equals((string)seal["captureProfileSha256"], (string)metadata["captureProfileSha256"], StringComparison.Ordinal))
                throw new InvalidDataException("S0a final evidence seal identity is invalid.");
            var unhashed = (JObject)seal.DeepClone(); unhashed.Remove("sealHash");
            if (!string.Equals((string)seal["sealHash"], W24S0aIntegrity.HashText(unhashed.ToString(Formatting.None)), StringComparison.Ordinal))
                throw new InvalidDataException("S0a final evidence seal hash is invalid.");
            var provenance = seal["provenance"] as JObject;
            var commandHash = provenance == null ? null : (string)provenance["operatorCommandHash"];
            if (expectedCommandHash != null && !string.Equals(commandHash, expectedCommandHash, StringComparison.Ordinal))
                throw new InvalidDataException("S0a final evidence seal does not bind the expected operator command hash.");
            if (provenance == null || !W24S0aIntegrity.IsCanonicalHash((string)provenance["captureToolSha256"]) || !W24S0aIntegrity.IsCanonicalHash((string)provenance["sourceHashesSha256"]) || !string.Equals((string)provenance["captureMetadataSha256"], W24S0aIntegrity.HashFile(Path.Combine(capture, "capture-metadata.json")), StringComparison.Ordinal))
                throw new InvalidDataException("S0a final evidence seal provenance is incomplete or does not bind capture metadata.");
            var index = seal["artifacts"] as JArray;
            if (index == null || index.Count == 0) throw new InvalidDataException("S0a final evidence seal has no artifact index.");
            var declared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var artifact in index)
            {
                var item = artifact as JObject;
                var relative = item == null ? null : (string)item["file"];
                var hash = item == null ? null : (string)item["sha256"];
                ValidateDeclaredArtifact(capture, item);
                if (!declared.Add(relative) || !string.Equals(hash, W24S0aIntegrity.HashFile(Path.Combine(capture, relative.Replace('/', Path.DirectorySeparatorChar))), StringComparison.Ordinal))
                    throw new InvalidDataException("S0a final evidence seal has duplicate or mismatched artifact entries.");
            }
            var actual = Directory.GetFiles(capture, "*", SearchOption.AllDirectories)
                .Select(path => Relative(capture, path)).Where(path => !string.Equals(path, "evidence-seal.json", StringComparison.Ordinal)).ToArray();
            if (!declared.SetEquals(actual)) throw new InvalidDataException("S0a final evidence seal does not index exactly the complete recorder artifact set.");
        }

        private static void ValidateDeclaredArtifact(string capture, JObject artifact)
        {
            var relative = artifact == null ? null : (string)artifact["file"];
            var declaredHash = artifact == null ? null : (string)artifact["sha256"];
            if (string.IsNullOrEmpty(relative) || string.IsNullOrEmpty(declaredHash) || Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(part => part == ".." || part.Length == 0)) throw new InvalidDataException("S0a sealed capture contains unsafe or incomplete artifact metadata.");
            var path = Path.GetFullPath(Path.Combine(capture, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(Path.GetFullPath(capture) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) || !string.Equals(declaredHash, HashFile(path), StringComparison.Ordinal)) throw new InvalidDataException("S0a evidence lock metadata/frame hash binding failed for " + relative + ".");
        }

        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return "sha256:" + string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
    }
}
