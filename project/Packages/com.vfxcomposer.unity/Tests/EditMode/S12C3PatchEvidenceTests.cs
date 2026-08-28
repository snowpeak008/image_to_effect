using System.IO;
using NUnit.Framework;
using UnityEditor;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    public sealed class S12C3PatchEvidenceTests
    {
        [Test]
        public void S12C3_ThreeFrozenAiPatchAttemptsApplyAndLeaveAuditableWriteOnceEvidence()
        {
            S12C3PatchEvidence.EnsureRecorded(); Assert.That(S12C3PatchEvidence.VerifyExisting(), Is.True); Assert.That(File.Exists(Path.Combine(Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName, "docs", "stage-notes", "s12c3-evidence", "primaryWidth.report.json")), Is.True); S12C3PatchEvidence.AssertNoResidue();
        }
    }
}
