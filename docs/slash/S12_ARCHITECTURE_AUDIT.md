# S12 architecture audit — slash versus projectile v1

> Gate F audit, based on the S4–S11 baseline. No source, asset, Recipe, Manifest, Generated output, or test was changed for this audit.

## Finding

**Recipe v1 is intentionally projectile-specialized. A legitimate 3D slash needs Recipe v2 plus a separate runtime/preview lifecycle.** This is a compatible addition only if v1 parsing, compilation, output locations, hashes, formal `fireball_2d`/`fireball_3d` bytes, and Prefab GUIDs remain untouched.

Using empty `launch`, `travel`, or `impact` stages merely to gain the existing controller is explicitly rejected. In particular, naming a slash's `primary_arc` as `travel`, or placing all visuals in `impact`, would misrepresent both the authored time story and the public runtime API.

## Current projectile hard-coding

| Area | Current fact | Consequence for slash |
| --- | --- | --- |
| Recipe enum/parser | `RecipeArchetype` contains only `Projectile`; parser enum allow-list accepts only `projectile`. | `"archetype":"slash"` is an `E103` v1 error. |
| Stage model | v1 stages have only `id`, trigger, duration, enabled, modules. No phase role or start offset exists. | Sequential triggers cannot express overlapping afterimage/sparks/dissipation truthfully. |
| Authoring contract | `recipe-authoring.md` says buildable v1 runtime effects require `launch`, `travel`, `impact` with projectile triggers. | Current AI contract directs authors toward the forbidden disguise. |
| Compiler assembly | `VfxCompiler.WireControllerStageRoots` wires exactly IDs/names `launch`→`Launch`, `travel`→`Travel`, `impact`→`Impact`; generated-structure validation requires them. | A new phase root cannot compile or be surfaced by the current controller. |
| Runtime state | `VfxRuntimeStage` is `None/Launch/Travel/Impact`; controller serializes exactly three roots and enable flags. | There is no slash state, phase scheduler, primary-arc API, or finite lifecycle completion. |
| Runtime pose API | `SetTravelTransform` and `PlayImpact(position)` encode a moving projectile followed by an impact; `VfxPreviewSequenceDriver` interpolates straight-line travel. | Applying these calls to an arc makes the host contract semantically false and risks Trail-clearing rules becoming accidental behavior. |
| Preview | S7 preview offers Launch Only / Travel Loop / Impact Only / Full Sequence; S10 uses the same generated controller and perspective pose table. | There is no 0.45 s slash playback, camera-relative action-plane review, or phase isolation. |
| Module vocabulary | Existing kinds are fireball-oriented enough to cover generic particles/trails, but have no reviewed curved-ribbon semantic/template contract. | `sub_effect` would conceal the primary visual responsibility; it is not an acceptable primary-arc workaround. |
| Bindings/templates | Formal bindings and templates are Fireball 2D/3D-specific (`core`, `trail`, `embers`, `impact`, `shockwave`). | No Slash template exists; binding a fireball template to a slash would be visually and contractually invalid. |
| Patch | Stable ID paths, transactional revision/history, parameter range validation and impact analysis are generic in mechanics. Required-module protection is specific to any travel `energy_body`. | v2 needs an archetype-aware Patch grammar/required-module policy, but the transaction pattern can be reused. |
| AI exporter | Exporter hard-codes the 2D Manifest root and fireball default Recipe when producing canonical v1 authoring documents. | Slash cannot be added by editing the generated documents; a versioned v2 bundle/selector is required. |
| Release tests | Normal tests assert formal fireball stage roots/API, same input output/GUID, and v1 2D/3D semantic parity. | Preserve them verbatim; add isolated S12 tests rather than widening baseline assertions. |

## Backward-compatible reuse and extension points

| Reuse / extension | Safe condition |
| --- | --- |
| JSON parsing/report model, canonical hash primitives, strict unknown-field policy | v2 has its own parser/schema branch; v1 parser and allowed fields do not change. |
| Catalog, Manifest GUID/path protection, dependency hash, deep-copy compiler transaction, Generated-root boundary | retain these infrastructure invariants. Introduce a v2 Manifest type only if new fields are indispensable; otherwise reuse v1 Manifest format with new 3D template IDs/kinds. |
| Static cost model and `mobile_medium`/`pc_editor` profiles | add slash cost tests; do not reinterpret old fireball costs. |
| Explicit binding registry | add only reviewed `3d.slash.*` symbols and handlers. No reflection/property path bridge. |
| Stable IDs and Patch revision/history/snapshot | clone/parameterize the mechanism for v2 after slash-specific path grammar is frozen. Do not let v1 Patch mutate a v2 Recipe. |
| S10 perspective evidence capture conventions | reuse five-view method and metadata hash discipline, but create an S12 scene/window instead of changing S7/S10 artifacts. |
| Runtime particle/trail cleanup lessons | share private utility or duplicate the verified reset behavior; do not retrofit projectile calls into a slash controller. |

## What crosses the Recipe v2 boundary

The following is a genuine structural/semantic change and must be versioned, not silently admitted to Recipe v1:

1. `archetype: slash` and an independently valid slash lifecycle.
2. Timed overlapping phase descriptors (`phase`, `startTime`, `duration`) for `anticipation`, `primary_arc`, `afterimage`, `sparks`, and `dissipation`.
3. A slash controller/runtime API whose input is placement/orientation and one-shot play/reset, not projectile travel and hit APIs.
4. Archetype-scoped module-kind and required-module policy for an `arc_sweep` primary visual.
5. Separate v2 AI schema, canonical Recipe, generated template table, validation report guidance, Patch contract, and exporter selection.
6. Version dispatch in parser/validator/compiler/patch/UI that preserves v1 behavior exactly.

The following do **not** by themselves require v2: new reviewed 3D templates, new Manifest entries under protected Templates, new binding symbols, static-cost entries, or a new isolated Preview scene. They become v2 work here because the lifecycle and archetype contract change, not because a new Prefab exists.

## Compatibility invariants to lock before code

- Every existing v1 JSON remains legal, resolves the same templates, produces the same canonical/build hash inputs, and follows the same v1 compiler path.
- `fireball-2d.default.json` and `fireball-3d.default.json` remain byte-identical; their Managed Prefab and Preview-scene GUID references remain unchanged.
- v1 `GeneratedVfxController`, v1 `VfxPreviewSequenceDriver`, v1 S7/S10 Preview scenes, S9 evidence, and release acceptance tests are not reinterpreted or migrated.
- v2 output is a new recipe ID and a separate `Assets/VFX/Generated/{slash-id}/` folder. It may not replace either formal fireball output.
- Missing/unsupported version/archetype combinations fail with a structured report before any Generated write. No fallback to projectile behavior.

## Gate F recommendation

**NO-GO for direct S12 implementation against Recipe v1. GO for a scoped Recipe v2 design-and-build proposal** once the product owner accepts the exact v2 lifecycle/AI/Patch boundary below. A minimal Unity API spike is optional rather than currently blocking: Unity 2022.3 supports the needed MeshRenderer/ParticleSystem/Material/Prefab APIs already used by S10, but the planned arc template must prove its camera-facing/material ordering visually before being promoted.

## Exact decisions required before implementation

1. Approve the v2 phase model (`startTime` + duration overlaps) instead of retaining v1 triggers for slash.
2. Approve `arc_sweep` as the required slash primary module and the Brief's four visual responsibilities; decide whether `arc_afterimage` is an independent template/module or a fixed child of the arc template. Recommendation: fixed child for the first case, yielding three required templates plus an optional anticipation template.
3. Approve runtime host surface: `PlaySlash(Vector3 position, Quaternion orientation)` plus `StopEffect`/`ResetForPool`, with no weapon socket or hit input. Recommendation: approve.
4. Approve v1/v2 Patch separation. Recommendation: a v2 Patch uses the same bare-array/revision transaction but only v2 paths; v1 patch service rejects v2 Recipe explicitly.
5. Approve whether S12 remains P2 extension work after MVP 0.1.0. Recommendation: yes; do not alter the released internal MVP baseline.
