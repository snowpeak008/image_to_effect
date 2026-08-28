using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VFXComposer.Editor.W24.S6.External;

namespace VFXComposer.Tests.EditMode
{
    [TestFixture]
    public sealed class W24S6LocalProjectRegistrationTests
    {
        private const string ProjectIdentity = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [SetUp]
        public void ResetCounters()
        {
            W24S6LocalProjectRegistration.ResetProductionAcquireAttemptCountForTests();
            W24S6WindowsReadOnlyFile.ResetOpenAttemptCountForTests();
        }

        [Test]
        public void ProductionRegistration_IsExactPendingNoInputAndReturnsNoLease()
        {
            var method = typeof(W24S6LocalProjectRegistration).GetMethod("TryAcquire", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.GetParameters().Length, Is.EqualTo(2));
            Assert.That(method.GetParameters().All(value => value.IsOut), Is.True, "The resolver may have out values but no caller-controlled input.");

            W24S6RegisteredProjectLease lease;
            string diagnosticCode;
            Assert.That(W24S6LocalProjectRegistration.TryAcquire(out lease, out diagnosticCode), Is.False);
            Assert.That(lease, Is.Null);
            Assert.That(diagnosticCode, Is.EqualTo("W24FS001"));
            Assert.That(W24S6LocalProjectRegistration.SchemaVersion,
                Is.EqualTo("w24-s6/local-project-registration-lease-scaffold-v1"));
            Assert.That(W24S6LocalProjectRegistration.ProductionState, Is.EqualTo("REGISTRATION_ISSUER_PENDING"));
            Assert.That(W24S6LocalProjectRegistration.ProductionAcquireAttemptCountForTests, Is.EqualTo(1));
        }

        [Test]
        public void ProductionAdapter_StopsAtPendingRegistrationBeforeEnvelopeDriveOrOpen()
        {
            var result = W24S6LocalReadOnlyFilesystemAdapter.InspectProduction("not-json", "not-a-hash");
            Assert.That(result.Classification, Is.EqualTo("rejected"));
            Assert.That(result.Diagnostics.Select(value => value.Code), Is.EqualTo(new[] { "W24FS001" }));
            Assert.That(result.RequestId, Is.Null);
            Assert.That(W24S6LocalProjectRegistration.ProductionAcquireAttemptCountForTests, Is.EqualTo(1));
            Assert.That(W24S6WindowsReadOnlyFile.DriveTypeQueryCountForTests, Is.Zero);
            Assert.That(W24S6WindowsReadOnlyFile.OpenAttemptCountForTests, Is.Zero);
            Assert.That(W24S6WindowsReadOnlyFile.TargetOpenAttemptCountForTests, Is.Zero);
        }

        [Test]
        public void TestLease_IsOpaqueNonSerializableAndCannotEnterProductionAdapter()
        {
            var leaseType = typeof(W24S6RegisteredProjectLease);
            Assert.That(leaseType.IsPublic || leaseType.IsNestedPublic, Is.False);
            Assert.That(leaseType.IsSerializable, Is.False);
            Assert.That(leaseType.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty);
            Assert.That(leaseType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance), Has.Length.EqualTo(1));
            Assert.That(leaseType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(value => value.Name), Is.EqualTo(new[] { "Dispose" }));
            Assert.That(leaseType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly), Is.Empty);
            Assert.That(leaseType.GetMethod("ToJson", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static), Is.Null);
            Assert.That(leaseType.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Any(value => value.Name.IndexOf("Path", StringComparison.OrdinalIgnoreCase) >= 0
                    || value.Name.IndexOf("Handle", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);

            var production = typeof(W24S6LocalReadOnlyFilesystemAdapter).GetMethod("InspectProduction", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(production, Is.Not.Null);
            Assert.That(production.GetParameters().Select(value => value.ParameterType), Is.EqualTo(new[] { typeof(string), typeof(string) }));
            Assert.That(production.GetParameters().Any(value => value.ParameterType == leaseType), Is.False);
        }

        [Test]
        public void TestLease_GenerationRevocationAndDisposeAreFailClosedAndIdempotent()
        {
            var lease = W24S6RegisteredProjectLease.IssueForTests(ProjectIdentity, 7);
            Assert.That(lease.ProjectIdentityHash, Is.EqualTo(ProjectIdentity));
            Assert.That(lease.Generation, Is.EqualTo(7));
            Assert.That(lease.IsUsable(7), Is.True);
            Assert.That(lease.IsUsable(6), Is.False);

            lease.Revoke();
            lease.Revoke();
            Assert.That(lease.IsUsable(7), Is.False);
            lease.Dispose();
            lease.Dispose();
            Assert.That(lease.IsUsable(7), Is.False);
        }

        [Test]
        public void TestLease_RejectsCallerShapedIdentityAndNonPositiveGeneration()
        {
            Assert.Throws<ArgumentException>(() => W24S6RegisteredProjectLease.IssueForTests("not-a-hash", 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => W24S6RegisteredProjectLease.IssueForTests(ProjectIdentity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => W24S6RegisteredProjectLease.IssueForTests(ProjectIdentity, -1));
        }

        [Test]
        public void RegistrationSource_HasNoBrokerTransportFilesystemDriveOrMutableProviderSurface()
        {
            var externalRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "com.vfxcomposer.unity", "Editor", "W24", "S6", "External"));
            var source = File.ReadAllText(Path.Combine(externalRoot, "W24S6LocalProjectRegistration.cs"));
            foreach (var forbidden in new[]
            {
                "CreateFile", "GetDriveType", "QueryDosDevice", "File.", "Directory.", "Application.", "EditorPrefs",
                "Microsoft.Win32.Registry", "Environment.", "System.Net", "System.Diagnostics.Process", "Register(",
                "SetProvider", "InstallProvider", "AssetDatabase", "EditorSceneManager"
            })
                Assert.That(source, Does.Not.Contain(forbidden), forbidden);

            var productionMethods = typeof(W24S6LocalProjectRegistration)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(value => value.Name.IndexOf("ForTests", StringComparison.Ordinal) < 0)
                .ToArray();
            Assert.That(productionMethods.Select(value => value.Name), Is.EqualTo(new[] { "TryAcquire" }));
            Assert.That(productionMethods.Single().GetParameters().All(value => value.IsOut), Is.True);
        }
    }
}
