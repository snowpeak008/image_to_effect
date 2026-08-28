# S12C3 report — real frozen AI Patch validation

Status: Main-Agent independent acceptance passed.

Before validation, the Main Agent dispatched three isolated `gpt-5.6-terra` / `high` Patch agents, whose preserved raw responses were each applied once through the real `S12SlashPatchService.ApplyToAsset` against an independently copied canonical v2 Recipe. `S12C3PatchEvidence` itself performs no dispatch. Neither the canonical Recipe nor the frozen v3 contract was modified.

The recorder machine-reads frozen acceptance and enforces a bare one-operation `replace`, the exact stable parameter path, numeric type, and above-default / frozen upper-bound rule before applying each Patch. The results are retained as write-once reports:

- [primary width](s12c3-evidence/primaryWidth.report.json): `0.34`, `/root/s12_patch_width`.
- [spark count](s12c3-evidence/sparkCount.report.json): `18`, `/root/s12_patch_sparks`.
- [afterimage alpha](s12c3-evidence/afterimageAlpha.report.json): `0.40`, `/root/s12_patch_alpha`.

Every report records the raw Patch-file SHA-256, `gpt-5.6-terra` / `high` / `fork none` metadata, real `1 → 2` result and history revision, affected path/value, empty apply-error set, patched build hash, and generated prefab GUID. During recording each patched prefab is read back to verify the concrete compiler binding: width control scale, Spark burst maximum, or serialized afterimage alpha. The history tail is read back for `affectedPaths` and the patched `BuildManifest` must report revision 2 and the exact patched Recipe hash.

The temporary Recipe/history/meta assets are deleted after every individual transaction, then the compiler rebuilds canonical formal Generated output. The final managed manifest is canonical revision 1 / recipe hash `1bf6e95698ae1ef8f76d79d774a094fe70858d194b632e0a190055caa80526b8`; no `.pending`, compiler temp, or Patch/compiler OS backup remains.

Verification:

- `cmd /c tools\compile-check.bat`: exit 0.
- Targeted EditMode `VFXComposer.Tests.EditMode.S12C3PatchEvidenceTests`: 1/1 passed.

Main-Agent independent verification also passed: compile exit 0 and [`s12-main-c3-edit.xml`](../../test-results/s12-main-c3-edit.xml) 1/1, including report and cleanup review.
