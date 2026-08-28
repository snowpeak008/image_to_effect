using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24FormalBatchAuthoringEntrypointsTests
    {
        [Test]
        public void BatchEntryPoints_ArePublicStaticVoidExecuteMethods_AndAuthoringNeedsNoGraphicsDevice()
        {
            Assert.That(W24FormalBatchAuthoringEntrypoints.RequiresGraphicsDevice, Is.False, "S0b/S3 authoring creates serialized assets only; graphics are reserved for the later recorder capture.");
            AssertExecuteMethod("ProvisionPreviewRendererInfrastructure");
            AssertExecuteMethod("BuildS0bFirstFormalAssets");
            AssertExecuteMethod("BuildS3FirstFormalAssets");
        }

        [Test]
        public void BatchEntryPoints_FailClosedInTheInteractiveEditor_BeforeAnyAuthoringCall()
        {
            if (Application.isBatchMode) Assert.Ignore("This guard is exercised only by the interactive Editor test runner.");
            AssertBatchGuard("ProvisionPreviewRendererInfrastructure");
            AssertBatchGuard("BuildS0bFirstFormalAssets");
            AssertBatchGuard("BuildS3FirstFormalAssets");
        }

        [Test]
        public void PreviewForwardRenderer_IsRegisteredOnceAsSecondary_And2DRendererRemainsDefault()
        {
            Assert.That(QualitySettings.activeColorSpace, Is.EqualTo(ColorSpace.Linear), "W24 formal captures and typed linear-LDR diagnostics require the project-wide Linear color space frozen by their Contracts.");
            var pipeline = AssetDatabase.LoadMainAssetAtPath(W24PreviewRendererInfrastructure.PipelineAssetPath);
            var renderer = AssetDatabase.LoadMainAssetAtPath(W24PreviewRendererInfrastructure.RendererAssetPath);
            Assert.NotNull(pipeline);
            Assert.NotNull(renderer);

            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            var defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            Assert.NotNull(list);
            Assert.NotNull(defaultIndex);
            Assert.That(list.arraySize, Is.EqualTo(2), "The W24 renderer is the sole secondary renderer.");
            Assert.That(defaultIndex.intValue, Is.Zero, "The existing Renderer2D must remain the project default.");
            Assert.That(AssetDatabase.GetAssetPath(list.GetArrayElementAtIndex(0).objectReferenceValue), Is.EqualTo("Assets/Settings/Renderer2D.asset"));
            Assert.That(list.GetArrayElementAtIndex(1).objectReferenceValue, Is.SameAs(renderer));
            Assert.That(W24PreviewRendererInfrastructure.RequireRendererIndex(), Is.EqualTo(1));
        }

        [Test]
        public void PreviewForwardRenderer_CanBeAppliedToAnEphemeralAuthorityCamera()
        {
            var go = new GameObject("W24_Renderer_Test_Camera");
            try
            {
                var camera = go.AddComponent<Camera>();
                W24PreviewRendererInfrastructure.ApplyToCamera(camera);
                var additionalDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                Assert.NotNull(additionalDataType);
                var additionalData = camera.GetComponent(additionalDataType);
                Assert.NotNull(additionalData);
                var serialized = new SerializedObject(additionalData);
                Assert.That(serialized.FindProperty("m_RendererIndex").intValue, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsolatedShadowValidation_RequiresTempShadowExactDeclarationAndSeparateExistingCanonicalRoot()
        {
            var validation = typeof(W24FormalBatchAuthoringEntrypoints).GetMethod("ValidateIsolatedShadowProject", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(validation, "The isolated-shadow check must remain a pure test seam.");
            var temporaryRoot = Path.GetTempPath();
            var suffix = Guid.NewGuid().ToString("N");
            var current = Path.Combine(temporaryRoot, "w24-shadow-tests", suffix, "project");
            var canonicalFixtureRoot = Path.Combine(temporaryRoot, "w24-canonical-tests", suffix);
            var canonical = Path.Combine(canonicalFixtureRoot, "project");
            Directory.CreateDirectory(canonical);
            try
            {
                Assert.DoesNotThrow(() => validation.Invoke(null, new object[] { current, current, canonical, temporaryRoot }));
                AssertValidationFailure(validation, current, null, canonical, temporaryRoot, "VFX_W24_SHADOW_PROJECT_ROOT");
                AssertValidationFailure(validation, current, Path.Combine(current, "other"), canonical, temporaryRoot, "must exactly equal");
                AssertValidationFailure(validation, current, current, Path.Combine(temporaryRoot, Guid.NewGuid().ToString("N")), temporaryRoot, "existing directory");
                AssertValidationFailure(validation, current, current, temporaryRoot, temporaryRoot, "canonical project");
                var outsideTemporaryRoot = Path.Combine(Path.GetPathRoot(temporaryRoot), "w24-outside-temp-tests", suffix, "project");
                AssertValidationFailure(validation, outsideTemporaryRoot, outsideTemporaryRoot, canonical, temporaryRoot, "descendant of Path.GetTempPath");
            }
            finally
            {
                if (Directory.Exists(canonicalFixtureRoot)) Directory.Delete(canonicalFixtureRoot, true);
            }
        }

        [Test]
        public void EntryPointSource_UsesOnlyAuthoringAndIdentityVerification_NotCaptureQaOrSignoff()
        {
            var sourcePath = Path.Combine(RepositoryRoot(), "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "W24FormalBatchAuthoringEntrypoints.cs");
            var source = File.ReadAllText(sourcePath);
            StringAssert.Contains("RequireBatchMode(nameof(BuildS0bFirstFormalAssets))", source);
            StringAssert.Contains("RequireBatchMode(nameof(BuildS3FirstFormalAssets))", source);
            StringAssert.Contains("RequireBatchMode(nameof(ProvisionPreviewRendererInfrastructure))", source);
            StringAssert.Contains("RequireIsolatedShadowProject();", source);
            StringAssert.Contains("W24PreviewRendererInfrastructure.ProvisionInIsolatedShadow();", source);
            StringAssert.Contains("SustainedFlameAuthoring.BuildAssetsAndPreview();", source);
            StringAssert.Contains("W24S3BaselineAuthoring.BuildAll();", source);
            AssertOrdered(MethodBody(source, "public static void BuildS0bFirstFormalAssets", "public static void BuildS3FirstFormalAssets"),
                "RequireBatchMode(nameof(BuildS0bFirstFormalAssets));", "RequireIsolatedShadowProject();", "SustainedFlameAuthoring.BuildAssetsAndPreview();");
            AssertOrdered(MethodBody(source, "public static void BuildS3FirstFormalAssets", "// Public and read-only"),
                "RequireBatchMode(nameof(BuildS3FirstFormalAssets));", "RequireIsolatedShadowProject();", "W24S3BaselineAuthoring.BuildAll();");
            StringAssert.Contains("C0_CAPTURE_PENDING", source);
            StringAssert.Contains("VISUAL_PENDING", source);
            Assert.That(source, Does.Not.Contain("W24S5RecorderCaptureCompletion"));
            Assert.That(source, Does.Not.Contain("FinalizeSustainedFlameC0Capture"));
            Assert.That(source, Does.Not.Contain("W24S5VisualStatus.L3"));
            Assert.That(source, Does.Not.Contain("W24S5VisualStatus.L4"));
        }

        [Test]
        public void VerificationApi_IsPublicStaticAndReadOnlyByContract()
        {
            AssertVerifyMethod("VerifyS0bFormalOutputs");
            AssertVerifyMethod("VerifyS3FormalOutputs");
        }

        [Test]
        public void PostconditionVerification_IsInsideBothAuthoringTransactionsBeforeFaultAndCommit()
        {
            var root = RepositoryRoot();
            var s0b = File.ReadAllText(Path.Combine(root, "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S0b", "SustainedFlameAuthoring.cs"));
            AssertOrdered(s0b,
                "WriteProductionManifest(firstFormalApproval);",
                "W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(EffectId, PrefabPath, PreviewScenePath, OutputFolder);",
                "W24FirstFormalBuildTransaction.ThrowIfFaultInjected(\"s0b.after-c0-freeze\")",
                "transaction.Commit();");

            var s3 = File.ReadAllText(Path.Combine(root, "project", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S3", "W24S3BaselineAuthoring.cs"));
            AssertOrdered(s3,
                "WriteProductionManifest(LightingId, \"impact\"",
                "W24FormalBatchAuthoringEntrypoints.VerifyFormalOutput(ProjectileId, ProjectilePrefab, ProjectilePreview, ProjectileOutputFolder);",
                "W24FirstFormalBuildTransaction.ThrowIfFaultInjected(\"s3.after-c0-freezes\")",
                "transaction.Commit();");
            StringAssert.Contains("VerifyFormalOutput(BindingId, BindingPrefab, BindingPreview, BindingOutputFolder);", s3);
            StringAssert.Contains("VerifyFormalOutput(LightingId, LightingPrefab, LightingPreview, LightingOutputFolder);", s3);
        }

        private static void AssertExecuteMethod(string name)
        {
            var method = typeof(W24FormalBatchAuthoringEntrypoints).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method, name + " must be discoverable through Unity -executeMethod.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method.GetParameters(), Is.Empty);
        }

        private static void AssertBatchGuard(string name)
        {
            var method = typeof(W24FormalBatchAuthoringEntrypoints).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, null));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains("batch-only", exception.InnerException.Message);
            StringAssert.Contains("left unchanged", exception.InnerException.Message);
        }

        private static void AssertVerifyMethod(string name)
        {
            var method = typeof(W24FormalBatchAuthoringEntrypoints).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method, name + " must be available for non-mutating CI verification.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method.GetParameters(), Is.Empty);
        }

        private static void AssertValidationFailure(MethodInfo method, string current, string declaredShadow, string canonical, string temporaryRoot, string expectedMessage)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { current, declaredShadow, canonical, temporaryRoot }));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            StringAssert.Contains(expectedMessage, exception.InnerException.Message);
        }

        private static void AssertOrdered(string source, params string[] anchors)
        {
            var prior = -1;
            foreach (var anchor in anchors)
            {
                var current = source.IndexOf(anchor, StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(prior), "Expected ordered source anchor: " + anchor);
                prior = current;
            }
        }

        private static string MethodBody(string source, string startAnchor, string endAnchor)
        {
            var start = source.IndexOf(startAnchor, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "Missing source anchor: " + startAnchor);
            var end = source.IndexOf(endAnchor, start + startAnchor.Length, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "Missing source anchor after " + startAnchor + ": " + endAnchor);
            return source.Substring(start, end - start);
        }

        private static string RepositoryRoot()
        {
            return Directory.GetParent(Application.dataPath).Parent.FullName;
        }
    }
}
