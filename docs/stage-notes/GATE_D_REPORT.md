# Gate D report — authorization for S10 3D extension

> Decision: **GO** (2026-08-22). This report is an S10 entry audit only; it does not change Recipe v1.

## Gate requirements and evidence

| Gate D requirement | Evidence reviewed | Finding |
|---|---|---|
| 2D complete loop | S4 validates strict Recipe/Manifest contracts; S5 supplies six formal, protected 2D templates; S6 proves deterministic deep-copy compilation, GUID preservation and rollback; S7 proves generated runtime Launch/Travel/Impact playback; S8 proves Patch transactions; S9 proves the Codex file workflow/M6. | Passed. |
| Patch and local-rebuild path | S8 report documents stable ID paths, revision checking, required `travel/core` protection, exact affected-module reports, rollback and the A6 real-template test (`embers` 18 → 9). The planned full-rebuild fallback is explicit rather than a false claim of asset-local writing. | Passed. |
| Recipe v1 has no material/ breaking field defect | The existing fields express 3D needs without an engine-specific expansion: `dimension`, semantic `kind`, stable stage/module IDs, `templateId`, bounded manifest-defined `parameters`, `attachTo`, stage `trigger`/duration and `randomSeed`. S4 already validates strict dimension/kind compatibility; S6 carries generated material handling and stage wiring without Recipe fields. | Passed. |

## Required v1 semantic checks

- **Same-stage attach:** S4 requires `attachTo` to reference a module in the same stage and detects self/cycles. The 3D recipe can retain `trail → core` and `embers → core` exactly.
- **Travel energy body protection:** S8 makes `travel/core` with `kind: energy_body` non-removable in v1. The 3D recipe keeps that same module ID and kind, so the protection remains meaningful.
- **Stable identity:** Stage/module IDs are already globally unique Recipe identities; S8 Patch addressing is ID based, not array-index based. No additional 3D identity form is required.
- **3D-specific facts:** Mesh/billboard/trail/render queue/bounds live in protected Prefabs, Materials, Manifest dimension, bounded parameters and static costs. They do not require 3D-only Recipe fields. Bounds and camera limits are recorded as manifest/template limitations where the v1 Manifest contract permits, rather than silently extending Recipe v1.

## Decision and guardrail

S10 may add 3D templates, manifests, compiler binding handlers, preview evidence and a parallel `fireball-3d.default` Recipe. It must retain the 2D stage IDs, triggers, module IDs/kinds and attach graph except where a documented unsupported capability is explicitly rejected. Any newly discovered need to alter v1 semantics, rename/remove a v1 field, or make an existing v1 recipe invalid is a stop condition: record the issue and return for a v2/migration decision; do not make an implicit breaking change.

No such breaking issue was found in the S4–S9 reports or the current Catalog, compiler, binding, runtime and preview contracts.
