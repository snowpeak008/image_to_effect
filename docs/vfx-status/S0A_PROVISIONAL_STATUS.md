# W24 S0a provisional Generated inventory

This is a deterministic, evidence-presence status registration for the current `Assets/VFX/Generated` inventory. It is not a visual review and does not claim that any effect has been viewed, accepted, or is production ready.

- Scan basis: each pre-W24 BuildManifest at `ProjectSettings/VFXComposer/BuildManifests/{effectId}.manifest.json` supplies the actual `runtimeEntry.path`, GUID, build hash and matching `ownedOutputs` hash. The registry resolves that declared path under the project `Assets` root, then verifies the Prefab file, `.meta` GUID and SHA-256 independently. It does not construct an assumed Prefab filename.
- Result: every entry in the generated snapshot that passes manifest/runtime-entry/path/GUID/hash verification is `L2_VisualPlaceholder` with working status `VISUAL_PENDING` and `hasW24VisualQa: false`; the generated JSON is the authoritative current count and ID list.
- Fallback rule implemented by `W24StatusRegistry`: an absent or unreadable manifest, invalid/escaping path, absent runtime entry, GUID mismatch, owned-output hash mismatch, or malformed build hash is `L0_InvalidOrMissing`; it is not inferred to be functional or visually reviewed.
- Freeze-list hash: `sha256:a694c5acb1af076a6fd320a7ecdf02d6f490510f3421ed6c20a4a7f12ba803f5`.

The hash is a lowercase canonical `sha256:<64 hex>` value over the UTF-8 lines used by `W24StatusRegistry.ComputeFreezeHash`: header `W24-S0A-PROVISIONAL-STATUS-V2`, then ordinal effect id, declared runtime path, evidence-presence/verification flags, declared runtime GUID, owned-output SHA-256, build SHA-256, maturity and working status. The full frozen list is generated from the current inventory with `powershell -NoProfile -ExecutionPolicy Bypass -File tools/Regenerate-W24StatusFreeze.ps1`, not edited by hand, in [s0a-provisional-status.json](s0a-provisional-status.json).
