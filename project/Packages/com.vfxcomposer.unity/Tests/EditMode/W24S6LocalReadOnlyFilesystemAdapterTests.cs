using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.External;

namespace VFXComposer.Tests.EditMode
{
    [TestFixture]
    public sealed class W24S6LocalReadOnlyFilesystemAdapterTests
    {
        private const string ProjectIdentity = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string OtherHash = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string RecipeTarget = "Assets/VFX/Recipes/sample.json";
        private const string ManifestTarget = "ProjectSettings/VFXComposer/BuildManifests/sample.manifest.json";
        private const string ContractTarget = "docs/vfx-contracts/sustained_flame_3d.contract.json";
        private readonly List<string> createdFiles = new List<string>();
        private readonly List<string> createdDirectories = new List<string>();
        private readonly List<string> createdReparseDirectories = new List<string>();
        private string scratchRoot;
        private string syntheticRepositoryRoot;
        private string syntheticProjectRoot;
        private W24S6LocalReadOnlyFilesystemAdapter adapter;

        [SetUp]
        public void CreateFixedDriveScratchRepository()
        {
            createdFiles.Clear();
            createdDirectories.Clear();
            createdReparseDirectories.Clear();
            scratchRoot = null;
            syntheticRepositoryRoot = null;
            syntheticProjectRoot = null;
            adapter = null;
            Assert.That(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), Is.True, "The scaffold is intentionally Windows-only.");
            var activeProjectRoot = Directory.GetParent(Application.dataPath).FullName;
            var activeRepositoryRoot = Directory.GetParent(activeProjectRoot).FullName;
            Assert.That(Path.GetPathRoot(activeRepositoryRoot), Is.EqualTo("D:\\").IgnoreCase, "Formal focused coverage requires the declared D: scratch volume.");
            try
            {
                scratchRoot = Path.Combine(activeRepositoryRoot, "w24-s6-local-read-tests-" + Guid.NewGuid().ToString("N"));
                Assert.That(Directory.Exists(scratchRoot), Is.False, "The random scratch GUID root must be absent before its first create.");
                syntheticRepositoryRoot = scratchRoot;
                syntheticProjectRoot = Path.Combine(syntheticRepositoryRoot, "project");
                EnsureDirectory(syntheticProjectRoot);

                WriteTarget(RecipeTarget, Encoding.UTF8.GetBytes("{\"recipeVersion\":1}"));
                WriteTarget(ManifestTarget, Encoding.UTF8.GetBytes("{\"effectId\":\"sample\",\"buildHash\":\"" + new string('a', 64)
                    + "\",\"runtimeEntry\":{\"path\":\"Assets/VFX/Generated/sample/VFX_sample.prefab\"}}"));
                var canonicalContract = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ContractTarget.Replace('/', Path.DirectorySeparatorChar)));
                WriteTarget(ContractTarget, File.ReadAllBytes(canonicalContract));
                adapter = W24S6LocalReadOnlyFilesystemAdapter.CreateForTests(syntheticProjectRoot, syntheticRepositoryRoot, ProjectIdentity);
                W24S6WindowsReadOnlyFile.ResetOpenAttemptCountForTests();
            }
            catch (Exception setupFailure)
            {
                try { CleanupDeclaredScratch(); }
                catch (Exception cleanupFailure) { throw new AggregateException("Fixture setup failed and its exact scratch cleanup also failed.", setupFailure, cleanupFailure); }
                throw;
            }
        }

        [TearDown]
        public void RemoveOnlyDeclaredScratchFiles()
        {
            CleanupDeclaredScratch();
        }

        private void CleanupDeclaredScratch()
        {
            if (string.IsNullOrEmpty(scratchRoot)) return;
            foreach (var directory in createdReparseDirectories.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(value => value.Length))
            {
                if (!Directory.Exists(directory)) continue;
                RequireScratchContained(directory);
                Assert.That((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0, Is.True, "Only an exact declared reparse directory may use link cleanup.");
                Directory.Delete(directory, false);
            }
            foreach (var file in createdFiles.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(value => value.Length))
                if (File.Exists(file)) { RequireScratchContained(file); File.Delete(file); }
            foreach (var directory in createdDirectories.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(value => value.Length))
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) { RequireScratchContained(directory); Directory.Delete(directory, false); }
            Assert.That(Directory.Exists(scratchRoot), Is.False, "The exact D: scratch fixture must leave zero residue.");
        }

        [Test]
        public void ProductionBinding_FailsClosedBeforeParsingOrAnyFileOpen()
        {
            W24S6WindowsReadOnlyFile.ResetOpenAttemptCountForTests();
            var result = W24S6LocalReadOnlyFilesystemAdapter.InspectProduction("not-json", "not-a-hash");
            Assert.That(result.Classification, Is.EqualTo("rejected"));
            Assert.That(result.Diagnostics.Select(value => value.Code), Is.EqualTo(new[] { "W24FS001" }));
            Assert.That(result.RequestId, Is.Null);
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
            Assert.That(W24S6WindowsReadOnlyFile.DriveTypeQueryCountForTests, Is.Zero);
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void ValidEnvelope_ReadsExactThreeAllowlistedFilesAndBindsOrderedResult()
        {
            string planHash;
            var operations = new[]
            {
                Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget),
                Operation("manifest", W24S6McpOperationKind.InspectManifestHeader, ManifestTarget),
                Operation("contract", W24S6McpOperationKind.ValidateContractDocument, ContractTarget)
            };
            var result = adapter.Inspect(EnvelopeJson(operations, ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Classification, Is.EqualTo("inspection-complete"));
            Assert.That(result.Authority, Is.EqualTo("none"));
            Assert.That(result.MachineGatePassed, Is.False);
            Assert.That(result.RequestId, Is.EqualTo("local-read-request"));
            Assert.That(result.ProjectIdentityHash, Is.EqualTo(ProjectIdentity));
            Assert.That(result.PlanHash, Is.EqualTo(planHash));
            Assert.That(result.Operations.Select(value => value.OperationId), Is.EqualTo(new[] { "recipe", "manifest", "contract" }));
            Assert.That(result.Operations.Select(value => value.Classification), Is.All.EqualTo("document-valid"));
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.EqualTo(3), "Only the three exact leaf files may reach a target open.");
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.GreaterThan(5), "Drive roots and every parent segment must be pinned independently.");
        }

        [Test]
        public void InvalidDocument_RemainsACompleteNonAuthoritativeInspection()
        {
            WriteTarget(RecipeTarget, Encoding.UTF8.GetBytes("{\"a\":1,}"));
            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Classification, Is.EqualTo("inspection-complete"));
            Assert.That(result.Operations.Single().Classification, Is.EqualTo("document-invalid"));
            Assert.That(result.Operations.Single().Diagnostics.Single().Message,
                Is.EqualTo("The pinned document bytes did not satisfy the selected in-memory inspection."));
            Assert.That(result.MachineGatePassed, Is.False);
        }

        [Test]
        public void DryRunComparisonValue_CannotOpenTheFilesystem()
        {
            string planHash;
            var json = EnvelopeJson(new[] { Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) },
                ProjectIdentity, W24S6McpAuthority.DryRun, out planHash);
            var result = adapter.Inspect(json, planHash);
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("W24FS003"));
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void EffectivePathTooLongInSecondOperation_RejectsBeforeDriveQueryOrFirstOpen()
        {
            var longTarget = "Assets/VFX/Recipes/" + new string('a', 90) + "/" + new string('b', 90) + ".json";
            string planHash;
            var json = EnvelopeJson(new[]
            {
                Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget),
                Operation("bad", W24S6McpOperationKind.ParseRecipeSyntax, longTarget, OtherHash)
            }, ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash);
            var result = adapter.Inspect(json, planHash);
            Assert.That(result.Classification, Is.EqualTo("rejected"));
            Assert.That(result.Operations, Is.Empty);
            Assert.That(result.Diagnostics.Select(value => value.Code), Does.Contain("W24FS006"));
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
            Assert.That(W24S6WindowsReadOnlyFile.DriveTypeQueryCountForTests, Is.Zero);
        }

        [Test]
        public void ProjectIdentityMismatch_RejectsBeforeAnyFileOpen()
        {
            string planHash;
            var json = EnvelopeJson(new[] { Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) }, OtherHash,
                W24S6McpAuthority.ReadOnly, out planHash);
            Assert.That(adapter.Inspect(json, planHash).Diagnostics.Select(value => value.Code), Does.Contain("W24MCP004"));
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void ExpectedPlanMismatch_RejectsBeforeAnyFileOpen()
        {
            string planHash;
            var json = EnvelopeJson(new[] { Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) }, ProjectIdentity,
                W24S6McpAuthority.ReadOnly, out planHash);
            Assert.That(adapter.Inspect(json, OtherHash).Diagnostics.Select(value => value.Code), Does.Contain("W24MCP013"));
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void DuplicateDocumentTarget_RejectsBeforeAnyFileOpen()
        {
            string planHash;
            var json = EnvelopeJson(new[]
            {
                Operation("recipe-a", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget),
                Operation("recipe-b", W24S6McpOperationKind.ParseRecipeSyntax, "Assets/VFX/Recipes/SAMPLE.json", OtherHash)
            }, ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash);
            Assert.That(adapter.Inspect(json, planHash).Diagnostics.Select(value => value.Code), Does.Contain("W24FS005"));
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
        }

        [TestCase("\\\\server\\share\\recipe.json")]
        [TestCase("\\\\.\\D:\\recipe.json")]
        [TestCase("\\\\?\\D:\\recipe.json")]
        [TestCase("Assets/VFX/Recipes/a.json:stream")]
        [TestCase("Assets/VFX/Recipes/../outside.json")]
        [TestCase("Assets/VFX/Recipes/CON.json")]
        [TestCase("Assets/VFX/Recipes/NUL.json")]
        [TestCase("Assets/VFX/Recipes/bad./a.json")]
        [TestCase("Assets/VFX/Recipes/bad /a.json")]
        [TestCase("Assets/VFX/Recipes/a*.json")]
        [TestCase("Assets/VFX/Recipes/a?.json")]
        [TestCase("Assets/VFX/Recipes/a\"b.json")]
        [TestCase("Assets/VFX/Recipes/a<b>.json")]
        public void UnsafeWindowsTarget_RejectsBeforeAnyFileOpen(string targetPath)
        {
            string planHash;
            var json = EnvelopeJsonUnchecked(new[] { Operation("unsafe", W24S6McpOperationKind.ParseRecipeSyntax, targetPath, OtherHash) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash);
            Assert.That(adapter.Inspect(json, planHash).Classification, Is.EqualTo("rejected"));
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void MissingFile_ReturnsStableRelativeDiagnosticWithoutAnAbsolutePath()
        {
            const string missing = "Assets/VFX/Recipes/missing.json";
            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("missing", W24S6McpOperationKind.ParseRecipeSyntax, missing, OtherHash) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            var operation = result.Operations.Single();
            Assert.That(operation.Classification, Is.EqualTo("rejected"));
            Assert.That(operation.TargetPath, Is.EqualTo(missing));
            Assert.That(operation.Diagnostics.Single().Code, Is.EqualTo("W24FS107"));
            Assert.That(operation.Diagnostics.Single().Message, Does.Not.Contain(scratchRoot));
        }

        [Test]
        public void OversizedFile_IsRejectedFromHandleMetadataBeforeAllocation()
        {
            WriteTarget(RecipeTarget, new byte[W24S6LocalDocumentInspector.MaximumDocumentBytes + 1]);
            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("oversized", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget, OtherHash) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            var operation = result.Operations.Single();
            Assert.That(operation.Classification, Is.EqualTo("rejected"));
            Assert.That(operation.InputSha256, Is.Null);
            Assert.That(operation.Diagnostics.Single().Code, Is.EqualTo("W24FS111"));
        }

        [Test]
        public void Hardlink_IsRejectedByPinnedSingleLinkIdentity()
        {
            const string hardlinkTarget = "Assets/VFX/Recipes/hardlink.json";
            var source = AbsoluteTarget(RecipeTarget);
            var link = AbsoluteTarget(hardlinkTarget);
            EnsureDirectory(Path.GetDirectoryName(link));
            Assert.That(CreateHardLink(link, source, IntPtr.Zero), Is.True, "CreateHardLinkW failed with " + Marshal.GetLastWin32Error());
            createdFiles.Add(link);

            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("hardlink", W24S6McpOperationKind.ParseRecipeSyntax, hardlinkTarget) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Operations.Single().Diagnostics.Single().Code, Is.EqualTo("W24FS109"));
        }

        [Test]
        public void DirectoryTarget_IsActuallyOpenedAndRejectedAsNonRegular()
        {
            const string directoryTarget = "Assets/VFX/Recipes/directory.json";
            EnsureDirectory(AbsoluteTarget(directoryTarget));
            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("directory", W24S6McpOperationKind.ParseRecipeSyntax, directoryTarget, OtherHash) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Operations.Single().Diagnostics.Single().Code, Is.EqualTo("W24FS109"));
        }

        [Test]
        public void RealLocalDirectoryJunction_IsRejectedAtItsSegmentBeforeLeafOrInspector()
        {
            var outside = Path.Combine(syntheticRepositoryRoot, "outside-junction-target");
            EnsureDirectory(outside);
            var outsideFile = Path.Combine(outside, "escape.json");
            File.WriteAllBytes(outsideFile, Encoding.UTF8.GetBytes("{}"));
            createdFiles.Add(outsideFile);
            var link = Path.Combine(syntheticProjectRoot, "Assets", "VFX", "Recipes", "junction");
            EnsureDirectory(link);
            int error;
            Assert.That(CreateDirectoryJunction(link, outside, out error), Is.True,
                "FSCTL_SET_REPARSE_POINT must create the local NTFS junction without a remote or privilege-dependent symlink probe. Win32=" + error);
            createdReparseDirectories.Add(link);

            const string escapedTarget = "Assets/VFX/Recipes/junction/escape.json";
            string planHash;
            var expectedHash = W24S6LocalDocumentInspector.Hash(File.ReadAllBytes(outsideFile));
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("junction", W24S6McpOperationKind.ParseRecipeSyntax, escapedTarget, expectedHash) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Operations.Single().Diagnostics.Single().Code, Is.EqualTo("W24FS110"));
            Assert.That(result.Operations.Single().InputSha256, Is.Null);
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.Zero, "The child leaf must never reach NtOpenFile.");
            Assert.That(adapter.InspectorInvocationCountForTests, Is.Zero, "Rejected junction traversal must never invoke the inspector.");
            Assert.That(W24S6WindowsReadOnlyFile.DirectoryMetadataAcceptedForTests(0x10 | 0x400, 7, 7), Is.False,
                "The deterministic production predicate also rejects the junction attribute even if NtOpenFile returns its handle.");
        }

        [Test]
        public void ExistingRealWriter_IsDeniedBeforeReadAndMtimeDriftIsPartOfIdentity()
        {
            var target = AbsoluteTarget(RecipeTarget);
            string planHash;
            var json = EnvelopeJson(new[] { Operation("writer", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash);
            using (var writer = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                var result = adapter.Inspect(json, planHash);
                Assert.That(result.Operations.Single().Diagnostics.Single().Code, Is.EqualTo("W24FS107"), "FILE_SHARE_READ must refuse a pre-existing writer.");
            }
            var beforeWriteTime = File.GetLastWriteTimeUtc(target).ToFileTimeUtc();
            File.SetLastWriteTimeUtc(target, DateTime.FromFileTimeUtc(beforeWriteTime).AddMinutes(-7));
            var afterWriteTime = File.GetLastWriteTimeUtc(target).ToFileTimeUtc();
            Assert.That(afterWriteTime, Is.Not.EqualTo(beforeWriteTime), "The real NTFS scratch file must expose the induced mtime drift.");
            Assert.That(W24S6WindowsReadOnlyFile.IdentityFieldsMatchForTests(7, 11, 1, 0, 2, beforeWriteTime, 7, 11, 1, 0, 2, afterWriteTime), Is.False,
                "The post-read identity replay must reject an actual scratch-file mtime-only drift.");
            Assert.That(W24S6WindowsReadOnlyFile.IdentityFieldsMatchForTests(7, 11, 1, 0, 2, 100, 7, 11, 1, 0, 2, 100), Is.True);
            Assert.That(W24S6WindowsReadOnlyFile.IdentityFieldsMatchForTests(7, 11, 1, 0, 2, 100, 7, 11, 1, 0, 2, 101), Is.False,
                "The post-read identity replay must reject an mtime-only drift.");
        }

        [Test]
        public void RealReadOnlyBatch_LeavesTheEntireScratchTreeHashUnchanged()
        {
            var before = ScratchTreeHash();
            string planHash;
            var operations = new[]
            {
                Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget),
                Operation("manifest", W24S6McpOperationKind.InspectManifestHeader, ManifestTarget),
                Operation("contract", W24S6McpOperationKind.ValidateContractDocument, ContractTarget)
            };
            var result = adapter.Inspect(EnvelopeJson(operations, ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Classification, Is.EqualTo("inspection-complete"));
            Assert.That(ScratchTreeHash(), Is.EqualTo(before));
        }

        [Test]
        public void MetadataGuard_RejectsDirectoryReparseHardlinkAndOversize()
        {
            Assert.That(W24S6WindowsReadOnlyFile.FileMetadataAcceptedForTests(0, 1, 0), Is.True);
            Assert.That(W24S6WindowsReadOnlyFile.FileMetadataAcceptedForTests(0x10, 1, 0), Is.False);
            Assert.That(W24S6WindowsReadOnlyFile.FileMetadataAcceptedForTests(0x400, 1, 0), Is.False);
            Assert.That(W24S6WindowsReadOnlyFile.FileMetadataAcceptedForTests(0, 2, 0), Is.False);
            Assert.That(W24S6WindowsReadOnlyFile.FileMetadataAcceptedForTests(0, 1, (ulong)W24S6LocalDocumentInspector.MaximumDocumentBytes + 1), Is.False);
        }

        [Test]
        public void FinalIdentityGuard_RequiresExactPathAndRootVolume()
        {
            Assert.That(W24S6WindowsReadOnlyFile.FinalIdentityAcceptedForTests("D:\\root\\a.json", "\\\\?\\d:\\root\\a.json", 7, 7), Is.True);
            Assert.That(W24S6WindowsReadOnlyFile.FinalIdentityAcceptedForTests("D:\\root\\a.json", "\\\\?\\D:\\other\\a.json", 7, 7), Is.False);
            Assert.That(W24S6WindowsReadOnlyFile.FinalIdentityAcceptedForTests("D:\\root\\a.json", "\\\\?\\D:\\root\\a.json", 7, 8), Is.False);
            foreach (var forbidden in new[] { "D:\\root\\a.json", "\\\\?\\UNC\\server\\share\\a.json", "\\\\?\\GLOBALROOT\\Device\\HarddiskVolume1\\a.json",
                "\\\\?\\Volume{11111111-1111-1111-1111-111111111111}\\a.json", "\\Device\\HarddiskVolume1\\a.json", "\\\\.\\D:\\a.json" })
                Assert.Throws<W24S6PinnedReadFailure>(() => W24S6WindowsReadOnlyFile.FinalIdentityAcceptedForTests("D:\\root\\a.json", forbidden, 7, 7), forbidden);
        }

        [Test]
        public void ResultJson_MatchesExactSchemaSurfaceAndContainsNoBytesAbsolutePathOrVerdict()
        {
            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            var json = JObject.Parse(result.ToJson());
            var schemaPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "schemas", "w24-s6-local-filesystem-inspection-result-v1.schema.json"));
            var schema = JObject.Parse(File.ReadAllText(schemaPath));
            CollectionAssert.AreEquivalent(((JArray)schema["required"]).Values<string>(), json.Properties().Select(value => value.Name));
            CollectionAssert.AreEquivalent(((JObject)schema["properties"]).Properties().Select(value => value.Name), json.Properties().Select(value => value.Name));
            AssertCompleteEmittedResultShape(json, true);
            var rejectedJson = JObject.Parse(W24S6LocalReadOnlyFilesystemAdapter.InspectProduction("not-json", "not-a-hash").ToJson());
            AssertCompleteEmittedResultShape(rejectedJson, false);
            var serialized = result.ToJson();
            Assert.That(serialized, Does.Not.Contain("documentBytes").And.Not.Contain("absolutePath").And.Not.Contain("verdict"));
            Assert.That((string)json["authority"], Is.EqualTo("none"));
            Assert.That((bool)json["machineGatePassed"], Is.False);
        }

        [Test]
        public void HashMismatch_ReturnsObservedHashButNeverDocumentBytes()
        {
            string planHash;
            var operation = Operation("recipe", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget, OtherHash);
            var result = adapter.Inspect(EnvelopeJson(new[] { operation }, ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Operations.Single().Classification, Is.EqualTo("rejected"));
            Assert.That(result.Operations.Single().InputSha256, Is.EqualTo(W24S6LocalDocumentInspector.Hash(File.ReadAllBytes(AbsoluteTarget(RecipeTarget)))));
            Assert.That(result.Operations.Single().Diagnostics.Select(value => value.Code), Does.Contain("W24INS005"));
            Assert.That(result.ToJson(), Does.Not.Contain(Convert.ToBase64String(File.ReadAllBytes(AbsoluteTarget(RecipeTarget)))));
        }

        [Test]
        public void InvalidUtf8_IsDelegatedAsDocumentInspectionWithoutLeakingBytes()
        {
            WriteTarget(RecipeTarget, new byte[] { 0xc3, 0x28 });
            string planHash;
            var result = adapter.Inspect(EnvelopeJson(new[] { Operation("utf8", W24S6McpOperationKind.ParseRecipeSyntax, RecipeTarget) },
                ProjectIdentity, W24S6McpAuthority.ReadOnly, out planHash), planHash);
            Assert.That(result.Operations.Single().Classification, Is.EqualTo("rejected"));
            Assert.That(result.Operations.Single().Diagnostics.Select(value => value.Code), Does.Contain("W24INS006"));
            Assert.That(result.ToJson(), Does.Not.Contain(Convert.ToBase64String(new byte[] { 0xc3, 0x28 })));
        }

        [Test]
        public void AdapterSurface_IsInternalStringEnvelopeOnlyAndHasNoExecutorOrCallerRootAPI()
        {
            var type = typeof(W24S6LocalReadOnlyFilesystemAdapter);
            Assert.That(type.IsPublic || type.IsNestedPublic, Is.False);
            Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
            Assert.That(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Empty);
            var inspect = type.GetMethod("Inspect", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(inspect, Is.Not.Null);
            Assert.That(inspect.GetParameters().Select(value => value.ParameterType), Is.EqualTo(new[] { typeof(string), typeof(string) }));
            var production = type.GetMethod("InspectProduction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(production, Is.Not.Null);
            Assert.That(production.GetParameters().Select(value => value.Name), Is.EqualTo(new[] { "envelopeJson", "expectedPlanHash" }));
            Assert.That(production.GetParameters().Any(value => value.Name.IndexOf("root", StringComparison.OrdinalIgnoreCase) >= 0
                || value.Name.IndexOf("absolute", StringComparison.OrdinalIgnoreCase) >= 0 || value.Name.IndexOf("resultPath", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
            var forbiddenActionPrefixes = new[]
            {
                "Write", "TryWrite", "Execute", "TryExecute", "Serve", "TryServe", "Listen", "TryListen", "StartServer", "RunServer"
            };
            Assert.That(type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Any(value => forbiddenActionPrefixes.Any(prefix => value.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))), Is.False);
        }

        [Test]
        public void NativeOpenShare_AllowsReadOnlyAndDeniesWriterAndDeleteSharing()
        {
            Assert.That(W24S6WindowsReadOnlyFile.ShareModeForTests, Is.EqualTo(0x00000001u));
        }

        [Test]
        public void ProductionSources_HaveNoWriteTransportUnityMutationOrPathProbeFallback()
        {
            var externalRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S6", "External"));
            var sources = new[] { "W24S6LocalProjectBinding.cs", "W24S6WindowsReadOnlyFile.cs", "W24S6LocalReadOnlyFilesystemAdapter.cs" }
                .Select(value => File.ReadAllText(Path.Combine(externalRoot, value))).ToArray();
            var combined = string.Join("\n", sources);
            foreach (var forbidden in new[] { "File.Write", "File.Delete", "File.Move", "Directory.Create", "Directory.Delete", "System.Diagnostics.Process",
                "System.Net", "AssetDatabase", "EditorSceneManager", "OpenScene", "File.Exists", "Directory.Exists", "resultPath" })
                Assert.That(combined, Does.Not.Contain(forbidden), forbidden);
            Assert.That(sources[1], Does.Contain("new FileStream(handle"));
            Assert.That(sources[1], Does.Not.Contain("new FileStream(openedPath").And.Not.Contain("File.ReadAllBytes"));
            Assert.That(sources[1], Does.Contain("NtOpenFile").And.Contain("RootDirectory").And.Contain("ObjectAttributesDontReparse")
                .And.Contain("FileOpenReparsePoint").And.Contain("ReplayDirectoryChain"));
            Assert.That(sources[1], Does.Contain("CreateFile(driveRoot").And.Not.Contain("CreateFile(expectedPath").And.Not.Contain("CreateFile(openedPath"));
        }

        private static void AssertCompleteEmittedResultShape(JObject value, bool expectOperation)
        {
            CollectionAssert.AreEquivalent(new[] { "schemaVersion", "authority", "machineGatePassed", "scope", "requestId",
                "projectIdentityHash", "planHash", "classification", "operations", "diagnostics" }, value.Properties().Select(item => item.Name));
            Assert.That((string)value["schemaVersion"], Is.EqualTo("w24-s6/local-filesystem-inspection-result-v1"));
            Assert.That((string)value["authority"], Is.EqualTo("none"));
            Assert.That((bool)value["machineGatePassed"], Is.False);
            Assert.That((string)value["scope"], Is.EqualTo("local-filesystem-document-inspection-only"));
            Assert.That(value["operations"].Type, Is.EqualTo(JTokenType.Array));
            Assert.That(value["diagnostics"].Type, Is.EqualTo(JTokenType.Array));
            var operations = (JArray)value["operations"];
            Assert.That(operations.Count, Is.EqualTo(expectOperation ? 1 : 0));
            Assert.That(operations.All(item => item.Type == JTokenType.Object), Is.True);
            foreach (var operation in operations.Children<JObject>())
            {
                CollectionAssert.AreEquivalent(new[] { "operationId", "operationKind", "targetPath", "inputSha256", "inputBytes",
                    "classification", "diagnostics" }, operation.Properties().Select(item => item.Name));
                Assert.That(operation["operationId"].Type, Is.EqualTo(JTokenType.String));
                Assert.That(operation["operationKind"].Type, Is.EqualTo(JTokenType.String));
                Assert.That(operation["targetPath"].Type, Is.EqualTo(JTokenType.String));
                Assert.That(((string)operation["targetPath"]).Contains(":") || ((string)operation["targetPath"]).StartsWith("\\\\", StringComparison.Ordinal), Is.False);
                Assert.That(operation["inputSha256"].Type == JTokenType.String || operation["inputSha256"].Type == JTokenType.Null, Is.True);
                Assert.That(operation["inputBytes"].Type, Is.EqualTo(JTokenType.Integer));
                Assert.That(operation["classification"].Type, Is.EqualTo(JTokenType.String));
                AssertDiagnosticsShape((JArray)operation["diagnostics"]);
            }
            AssertDiagnosticsShape((JArray)value["diagnostics"]);
            if (!expectOperation)
            {
                Assert.That(value["requestId"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(value["projectIdentityHash"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(value["planHash"].Type, Is.EqualTo(JTokenType.Null));
                Assert.That(((JArray)value["diagnostics"]).Count, Is.EqualTo(1));
            }
        }

        private static void AssertDiagnosticsShape(JArray diagnostics)
        {
            Assert.That(diagnostics, Is.Not.Null);
            Assert.That(diagnostics.All(item => item.Type == JTokenType.Object), Is.True);
            foreach (var diagnostic in diagnostics.Children<JObject>())
            {
                CollectionAssert.AreEquivalent(new[] { "code", "field", "message" }, diagnostic.Properties().Select(item => item.Name));
                Assert.That(diagnostic.Properties().All(item => item.Value.Type == JTokenType.String), Is.True);
            }
        }

        private W24S6McpOperation Operation(string id, W24S6McpOperationKind kind, string targetPath, string expectedHash = null)
        {
            return new W24S6McpOperation
            {
                OperationId = id,
                Kind = kind,
                TargetPath = targetPath,
                ExpectedInputHash = expectedHash ?? W24S6LocalDocumentInspector.Hash(File.ReadAllBytes(AbsoluteTarget(targetPath)))
            };
        }

        private static string EnvelopeJson(W24S6McpOperation[] operations, string projectIdentity, W24S6McpAuthority requestedAuthority, out string planHash)
        {
            var envelope = new W24S6McpOperationEnvelope
            {
                RequestId = "local-read-request",
                ProjectIdentityHash = projectIdentity,
                RequestedAuthority = requestedAuthority,
                Operations = operations
            };
            envelope.PlanHash = W24S6McpOperationEnvelopePolicy.ComputePlanHash(envelope);
            planHash = envelope.PlanHash;
            return envelope.ToJson();
        }

        private static string EnvelopeJsonUnchecked(W24S6McpOperation[] operations, string projectIdentity, W24S6McpAuthority requestedAuthority, out string planHash)
        {
            var envelope = new W24S6McpOperationEnvelope
            {
                RequestId = "local-read-request",
                ProjectIdentityHash = projectIdentity,
                RequestedAuthority = requestedAuthority,
                Operations = operations
            };
            envelope.PlanHash = W24S6McpOperationEnvelopePolicy.ComputePlanHash(envelope);
            planHash = envelope.PlanHash;
            return new JObject
            {
                ["schemaVersion"] = W24S6McpOperationEnvelope.Schema,
                ["requestId"] = envelope.RequestId,
                ["projectIdentityHash"] = envelope.ProjectIdentityHash,
                ["executionMode"] = "DryRun",
                ["requestedAuthority"] = requestedAuthority.ToString(),
                ["rollbackMode"] = "NoWriteRequired",
                ["operations"] = new JArray(operations.Select(value => new JObject
                {
                    ["operationId"] = value.OperationId,
                    ["kind"] = value.Kind.ToString(),
                    ["targetPath"] = value.TargetPath,
                    ["expectedInputHash"] = value.ExpectedInputHash
                })),
                ["planHash"] = envelope.PlanHash
            }.ToString(Newtonsoft.Json.Formatting.None);
        }

        private string ScratchTreeHash()
        {
            using (var buffer = new MemoryStream())
            using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false, true), true))
            {
                foreach (var directory in Directory.GetDirectories(scratchRoot, "*", SearchOption.AllDirectories)
                    .Select(value => value.Substring(scratchRoot.Length + 1).Replace('\\', '/')).OrderBy(value => value, StringComparer.Ordinal))
                {
                    writer.Write((byte)0);
                    WriteHashField(writer, directory);
                }
                foreach (var file in Directory.GetFiles(scratchRoot, "*", SearchOption.AllDirectories)
                    .Select(value => new { Absolute = value, Relative = value.Substring(scratchRoot.Length + 1).Replace('\\', '/') })
                    .OrderBy(value => value.Relative, StringComparer.Ordinal))
                {
                    writer.Write((byte)1);
                    WriteHashField(writer, file.Relative);
                    var bytes = File.ReadAllBytes(file.Absolute);
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                writer.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static void WriteHashField(BinaryWriter writer, string value)
        {
            var bytes = new UTF8Encoding(false, true).GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private void WriteTarget(string relativePath, byte[] bytes)
        {
            var absolute = AbsoluteTarget(relativePath);
            RequireScratchContained(absolute);
            EnsureDirectory(Path.GetDirectoryName(absolute));
            if (!createdFiles.Contains(absolute, StringComparer.OrdinalIgnoreCase)) createdFiles.Add(absolute);
            File.WriteAllBytes(absolute, bytes);
        }

        private string AbsoluteTarget(string relativePath)
        {
            var root = relativePath.StartsWith("docs/", StringComparison.Ordinal) ? syntheticRepositoryRoot : syntheticProjectRoot;
            return Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private void EnsureDirectory(string directory)
        {
            if (Directory.Exists(directory)) return;
            var missing = new Stack<string>();
            for (var current = directory; !string.IsNullOrEmpty(current) && !Directory.Exists(current); current = Path.GetDirectoryName(current)) missing.Push(current);
            while (missing.Count > 0)
            {
                var value = missing.Pop();
                RequireScratchContained(value);
                createdDirectories.Add(value);
                Directory.CreateDirectory(value);
            }
        }

        private void RequireScratchContained(string value)
        {
            var root = Path.GetFullPath(scratchRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(value);
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(candidate.TrimEnd(Path.DirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Scratch cleanup escaped its exact fixture root.");
        }

        private static bool CreateDirectoryJunction(string junctionPath, string targetPath, out int error)
        {
            const uint genericWrite = 0x40000000;
            const uint shareReadWriteDelete = 0x00000007;
            const uint openExisting = 3;
            const uint backupSemantics = 0x02000000;
            const uint openReparsePoint = 0x00200000;
            const uint fsctlSetReparsePoint = 0x000900A4;
            const uint mountPointTag = 0xA0000003;

            var substituteName = "\\??\\" + targetPath;
            var printName = targetPath;
            var substituteBytes = Encoding.Unicode.GetBytes(substituteName);
            var printBytes = Encoding.Unicode.GetBytes(printName);
            var pathBytes = Encoding.Unicode.GetBytes(substituteName + "\0" + printName + "\0");
            var buffer = new byte[16 + pathBytes.Length];
            WriteUInt32(buffer, 0, mountPointTag);
            WriteUInt16(buffer, 4, checked((ushort)(8 + pathBytes.Length)));
            WriteUInt16(buffer, 8, 0);
            WriteUInt16(buffer, 10, checked((ushort)substituteBytes.Length));
            WriteUInt16(buffer, 12, checked((ushort)(substituteBytes.Length + 2)));
            WriteUInt16(buffer, 14, checked((ushort)printBytes.Length));
            Buffer.BlockCopy(pathBytes, 0, buffer, 16, pathBytes.Length);

            using (var handle = CreateFileForJunction(junctionPath, genericWrite, shareReadWriteDelete, IntPtr.Zero,
                openExisting, backupSemantics | openReparsePoint, IntPtr.Zero))
            {
                if (handle == null || handle.IsInvalid)
                {
                    error = Marshal.GetLastWin32Error();
                    return false;
                }
                uint returned;
                var created = DeviceIoControl(handle, fsctlSetReparsePoint, buffer, buffer.Length, IntPtr.Zero, 0, out returned, IntPtr.Zero);
                error = created ? 0 : Marshal.GetLastWin32Error();
                return created;
            }
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 2);
        }

        private static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(string newFileName, string existingFileName, IntPtr securityAttributes);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileForJunction(string fileName, uint desiredAccess, uint shareMode,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode, byte[] inputBuffer,
            int inputBufferBytes, IntPtr outputBuffer, int outputBufferBytes, out uint bytesReturned, IntPtr overlapped);
    }
}
