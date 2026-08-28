# S12 implementation plan — minimum formal 3D slash path

> Gate F plan only. It deliberately stops before code/assets/Recipes/Generated output. It is contingent on the decisions in `S12_ARCHITECTURE_AUDIT.md`.

## Objective and non-negotiable compatibility target

Deliver one formal 3D stylized slash built from reviewed local Unity templates and a deterministic v2 compile path. The output must play a lower-left → upper-right 0.45 s arc in a third-person perspective preview.

The old fireball v1 path is a protected regression baseline: existing Recipe files stay legal and byte-identical; their compiler output shape, canonical/build behavior and formal Prefab GUIDs stay unchanged. S12 is not allowed to replace, rename, or rebuild them as a side effect.

## Proposed minimum contract

Create a **separate Recipe v2 branch**, rather than widening v1:

```json
{
  "recipeVersion": 2,
  "id": "slash_3d_stylized",
  "dimension": "3d",
  "archetype": "slash",
  "timeline": { "duration": 0.45 },
  "phases": [
    { "id": "anticipation", "kind": "anticipation", "startTime": 0.00, "duration": 0.04, "modules": [] },
    { "id": "primary_arc", "kind": "primary_arc", "startTime": 0.04, "duration": 0.16, "modules": [] },
    { "id": "afterimage", "kind": "afterimage", "startTime": 0.12, "duration": 0.18, "modules": [] },
    { "id": "sparks", "kind": "sparks", "startTime": 0.14, "duration": 0.22, "modules": [] },
    { "id": "dissipation", "kind": "dissipation", "startTime": 0.20, "duration": 0.25, "modules": [] }
  ]
}
```

This is an illustrative shape, not an implementation-ready schema. Before code, freeze required top-level identity/metadata/profile/seed fields, module attachment rules, enabled semantics, numeric bounds, exact unknown-field policy, phase ordering/overlap constraints, and v2 error codes. The five phase IDs/kinds are semantic requirements, not optional blank scaffolding.

### Archetype, phase, and module vocabulary

- New archetype: `slash` (v2 only).
- New phase kinds: `anticipation`, `primary_arc`, `afterimage`, `sparks`, `dissipation`.
- New required module kind: `arc_sweep` in `primary_arc`.
- Required semantics: `arc_afterimage` is a separate, non-empty module/template and renderer owned by the `afterimage` phase; `secondary_particles` serves `slash_sparks` and `slash_dissipation`. The earlier proposal to keep fixed afterimages inside `arc_sweep` is superseded by `S12_GATE_F_DECISION.md` and must not be implemented.
- All attachments remain intra-phase and acyclic. Cross-phase parenting is not introduced; overlapping phases are scheduled siblings.

## Formal assets, bindings, and compiler

1. Create protected 3D templates under `Assets/VFX/Templates/3D/` with v1-style GUID/path-protected Manifests where sufficient:
   - `PFT_3D_SlashArcSweep` — curved mesh/ribbon, orange-red body + yellow edge for the primary phase only.
   - `PFT_3D_SlashAfterimage` — independent red residual ribbon renderer for the non-empty `afterimage` phase.
   - `PFT_3D_SlashSparks` — sparse separated ParticleSystem.
   - `PFT_3D_SlashDissipation` — short non-looping residual particles/fade.
   - Optional `PFT_3D_SlashAnticipation` only if the arc template cannot cleanly own the 0.04 s lead-in.
2. Register only explicit symbols such as `3d.slash.arc.scale`, `3d.slash.arc.width`, `3d.slash.arc.duration`, `3d.slash.sparks.count`, `3d.slash.sparks.speed`, `3d.slash.sparks.lifetime`, and `3d.slash.dissipation.lifetime`. Each symbol gets a direct Handler and min/default/max test; no reflection.
3. Add v2 parser/semantic validator/catalog dispatch. Validate phase roles, timer containment (`startTime >= 0`, `startTime + duration <= timeline.duration`), required `primary_arc/arc_sweep`, Manifest kind/dimension, bindings, bounds, and static budget before write.
4. Add a v2 compiler dispatcher. Reuse the existing transaction/deep-copy/material-copy/dependency-hash/GUID-preserving primitives, but create a slash root and named phase roots rather than calling `WireControllerStageRoots`.
5. Build into `Assets/VFX/Generated/slash_3d_stylized/` (or the Recipe-derived v2 ID) only. Include a versioned Build Manifest recording v2 recipe/canonical hash, template versions/dependency hashes, compiler/Unity versions, costs, and output path.

## Runtime and preview

- Add a player-safe `GeneratedSlashController` (or a clearly discriminated generalized controller) with `PlaySlash(Vector3 position, Quaternion orientation)`, `StopEffect(bool immediate)`, and `ResetForPool()`.
- It schedules phase roots by frozen v2 offsets, supports isolated phase playback for preview only, clears all ParticleSystems/ribbon trails on stop/reset, and exposes no damage/collision/weapon APIs.
- Do not add slash behavior to `SetTravelTransform`, `PlayImpact`, or `VfxPreviewSequenceDriver`; those remain projectile APIs.
- Add `S12SlashPreviewScene` and `Tools/VFX Composer/Slash Preview`: neutral 3D third-person camera, 1.8 m scale reference, normal-game-distance setup, fixed lower-left → upper-right presentation orientation, play/reset, and isolated phase controls.
- Capture front, side, oblique-top, close, and game-distance evidence plus camera metadata/hashes, following S10's retained evidence discipline. The side view is especially important for mesh/ribbon depth and sorting failure detection.

## AI contract and Patch workflow

- Keep `docs/ai-workflow/recipe-v1.schema.json`, canonical v1 recipe/patch files, and existing exporter output untouched.
- Add a separately named v2 authoring bundle: v2 schema/authoring guide/canonical slash Recipe/template table/report guide/Patch guide. The exporter must select a version/archetype source rather than hard-code the 2D fireball root.
- v2 authors must receive only reviewed slash template IDs and Manifest ranges. They may not supply colors, material paths, meshes, camera transforms, weapon sockets, gameplay settings, or Unity property paths.
- Add v2 Patch service/dispatch or an explicit version guard in the current service. Preserve bare operation array, expected-revision, history, atomic snapshot and stable-ID principles, but define phase/module paths and `arc_sweep` protection separately. A v1 Patch must reject v2 input instead of guessing.

## Test matrix

| Category | Required checks |
| --- | --- |
| v1 no-regression | Existing S4–S11 suites; exact `fireball-2d`/`fireball-3d` source bytes, legal validation, output GUIDs, unchanged Dry Runs, and preview references unchanged. |
| v2 model | unknown field/version/archetype rejection; required phases/arc module; unique stable IDs; timing containment/overlap; Manifest kind/dimension/parameter range; v2 error path precision. |
| Templates/bindings | GUID/path/dependency boundary; min/default/max write-and-readback; visible non-looping particles; reviewed material/render ordering; no unregistered symbol. |
| Compiler/transaction | dry run no-write, canonical idempotence, dependencies change→update, Generated boundary, commit rollback, same-path slash GUID retention, template bytes unchanged. |
| Runtime | phase schedule order/overlap, total completion by 0.45 s, immediate/non-immediate stop, reset/replay with no residue, no Editor dependency. |
| Preview/visual | five captured views, dark/neutral/bright readability, direction/scale/timing/layer checklist from Brief, evidence metadata hashes. |
| Patch/AI | v2 Repair reports and authoring export; parameter patch affects only its target module; version/revision conflict and invalid patch leave Recipe/Generated/Templates unchanged; v1/v2 cross-use rejected. |
| Release boundaries | Static performance report includes slash separately and still labels itself preflight; Player Build includes the new preview only after all prior gates stay green. |

## Risks, optional spike, and rollback

| Risk | Containment |
| --- | --- |
| Arc looks flat, clips, or sorts incorrectly at third-person angles | First make a tiny isolated, disposable Unity spike with one curved MeshRenderer/ribbon, materials, and S10-like five views. Record material/camera findings in `docs/spike-notes/`; do not put it under Templates or Generated. |
| Overlapping scheduler expands into a generic graph system | Limit v2 to five known phase roles, finite absolute offsets, and sibling roots. Do not add conditions, nesting, events, or arbitrary timelines. |
| v2 destabilizes fireball v1 | Version dispatch before parser/compiler/runtime wiring; retain v1 code path and golden assertions; stop on any fireball hash/GUID/output difference. |
| Visual scope expands into weapon/gameplay work | Enforce the Brief Unsupported list and keep placement/orientation as the only host input. |
| Material/particle cost exceeds static profile | Reduce echoes/spark count or merge materials inside templates; do not hide a budget error or claim device performance. |

Rollback boundary: new S12 assets and source stay isolated by v2 recipe ID, template IDs, preview scene, docs, and tests. If S12 fails, remove only those reviewed S12 additions through a normal change and leave the S11/M8 fireball baseline unmodified. No migration of v1 Recipes or fireball Generated assets is permitted.

## Gate F recommendation

**GO to a formal S12 change proposal and a disposable render-order spike; NO-GO to production implementation until the five decisions in the architecture audit are approved.** The default visual brief itself requires no user clarification and should be adopted as written for the first spike.
