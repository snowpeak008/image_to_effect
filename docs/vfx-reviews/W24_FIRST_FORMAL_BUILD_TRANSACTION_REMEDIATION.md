# W24 first-formal-build transaction remediation

Status: source remediation complete; Unity execution intentionally deferred to an isolated shadow project.

## Changes

1. `W24FirstFormalBuildTransaction` is no longer a general filesystem rollback helper. It admits
   only effect-owned output roots, one Recipe, one Preview Scene, the matching authoritative
   Manifest, and the matching `docs/vfx-candidates/<effectId>/C0` target per effect. It rejects
   target overlap, path escape, inconsistent effect identity, and existing symlink/junction/
   reparse-point paths.
2. The transaction records absent parents below approved boundaries when it starts. Rollback removes
   only those parents, deepest first, only while they are empty, and removes their generated `.meta`
   files. It never removes a pre-existing parent. It also records the exact initial `.meta` bytes
   for every target and approved ancestor; after its owned-only imports, rollback restores those
   bytes or removes a `.meta` that did not exist initially.
3. Global `AssetDatabase.SaveAssets()` and `AssetDatabase.Refresh()` calls were removed from the
   transaction and S0b/S3 authorers. Materials now use `SaveAssetIfDirty` individually; import is
   restricted to transaction-owned Assets paths.
4. Added targeted unit cases for parent cleanup and rejected target classes. Added explicit shadow
   integration tests that call the real S0b and S3 authorers, inject faults after receipt/C0 freeze,
   and compare pre-existing owned bytes, preview, manifest and candidate tree byte-for-byte.

## Execution contract

The integration fixture `W24FirstFormalBuildShadowIntegrationTests` is Explicit and additionally
requires `VFX_W24_SHADOW_INTEGRATION=1`. It must be run only against a disposable copied Unity
project. This prevents a normal contributor project from becoming the destructive first-build
rollback fixture.

## Verification performed

- Focused static Roslyn compilation of all Editor sources plus both first-build test fixtures:
  passed after the concurrent S0a source edit completed.
- No Unity process was started by this remediation.

## Deferred S5-owned follow-up

`W24S5ProductionGate.AppendRollbackFailure` currently catches only `IOException` and
`UnauthorizedAccessException`. Its owner should broaden rollback diagnostic capture so an
`InvalidDataException` (for example a reparse-point refusal) is added to the audit report rather
than obscuring the original bootstrap failure.
