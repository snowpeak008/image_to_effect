using System.IO;
using System.Linq;
using NUnit.Framework;

namespace VFXComposer.Tests.EditMode
{
    public sealed class RuntimeAssemblyBoundaryTests
    {
        [Test]
        public void RuntimeAssembly_IsPlayerSafe_AndDoesNotMentionUnityEditor()
        {
            var packageRoot = Path.GetFullPath(Path.Combine("Packages", "com.vfxcomposer.unity"));
            var runtimeDirectory = Path.Combine(packageRoot, "Runtime");

            Assert.That(File.Exists(Path.Combine(runtimeDirectory, "VFXComposer.Runtime.asmdef")), Is.True);

            foreach (var sourceFile in Directory.GetFiles(runtimeDirectory, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(sourceFile);
                if (extension != ".cs" && extension != ".asmdef")
                {
                    continue;
                }

                var source = File.ReadAllText(sourceFile);
                Assert.That(source, Does.Not.Contain("UnityEditor"), sourceFile);
            }

            var runtimeReferences = typeof(VFXComposer.VFXComposerRuntimeMarker).Assembly
                .GetReferencedAssemblies()
                .Select(assemblyName => assemblyName.Name);
            Assert.That(runtimeReferences, Has.None.StartsWith("UnityEditor"));
        }
    }
}
