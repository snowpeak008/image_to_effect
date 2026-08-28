# S10 stage report — 3D extension

> Status: passed independent main-Agent acceptance; S10 complete and stopped before S11. Date: 2026-08-22.

## Independent acceptance

At 2026-08-22 21:09:29 +08:00 the main Agent independently reran the repository ignore audit, compile check, full EditMode suite and full PlayMode suite. Results were: Git-ignore audit passed; compile exit 0; EditMode 104 total / 69 passed / 0 failed / 35 intentionally Explicit historical tests skipped; PlayMode 5/5 passed. The final EditMode log no longer contains the previously observed last-scene unload warning.

The main Agent also independently verified that the 2D and 3D Recipes have identical stage IDs, triggers, module IDs, module kinds and attachment graph; all five evidence-file SHA-256 values match `views.json`; `Generated` contains only the two formal baselines; no S10 test, temp, backup, pending or history residue remains; and no Unity process holds the project. Milestone M7 is accepted.

## Gate D

Gate D was independently audited before implementation in [GATE_D_REPORT.md](GATE_D_REPORT.md). S4–S9 prove the 2D closed loop, Patch/rebuild transaction and stable ID semantics. Recipe v1 already expresses the 3D case with `dimension`, semantic module kind, bounded Manifest parameters and same-stage attachment. No breaking field defect was found, and no Recipe v2 field was added.

## Delivered

- Formal protected assets in `Assets/VFX/Templates/3D/`: six Prefabs, material assets and the retained `MESH_3D_ShockwaveRing.asset` with valid Unity GUID-backed Manifests.
  - Sphere Mesh energy core plus `CameraFacingBillboard` flame layer.
  - Billboard ParticleSystem embers, 3D TrailRenderer, camera-facing launch/impact particles, and mesh-particle Ring shockwave.
- Catalog now scans the formal Templates root recursively. Existing validator dimension matching remains a hard gate; `VfxBindingHandlerRegistry` adds explicit `3d.*` bindings without reflection; 3D render queues are protected Material/template policy rather than Recipe data.
- `fireball-3d.default.json` keeps every 2D stage/module ID, trigger, kind, duration and attach edge. The formal managed result is `Assets/VFX/Generated/fireball_3d/VFX_Fireball_3D.prefab`; a repeat authoring/build keeps its GUID and clears the one pre-release lowercase generated Prefab spelling.
- `S10_3D_FireballPreview.unity` is a perspective Gold Sample containing three instances of the official generated 3D Prefab: spatially separated Launch, Travel and Impact. It uses real generated ParticleSystem renderers for frozen visible Ember and Burst samples; there is no screenshot-only substitute geometry.
- The interactive `Tools/VFX Composer/3D Preview` window opens that formal S10 scene and exposes Front, Side, Oblique, Close and Game Distance controls. `VfxCompilerWindow` parses the selected Recipe before Preview: 2D dispatches to S7; a valid 3D Recipe dispatches only to S10; parse failure is reported and opens neither scene. The pose table is a single S10 code source shared by the window and evidence capture.
- Five images and structured capture metadata are in `docs/s10-evidence/`. `views.json` records the hidden-graphics-device `Camera.Render` method (not `-nographics`), target, camera positions, FOV and SHA-256 for front, side, oblique-top, close and game-distance captures.

## Evidence and tests

`S10ThreeDIntegrationTests` covers actual 3D Catalog/GUID/Prefab/mesh/billboard assets; all parameter min/default/max bindings; 3D compilation, canonical idempotence, output GUID and template immutability; strict dimension/unknown-binding errors; five distinct evidence images; the preview's three official generated-Prefab instances; all five operational perspective view calls; and Compiler 3D Preview dispatch.

`S10ThreeDRuntimeTests` runs the runtime-only spatial fixture through Launch → Travel → Impact → Stop with a Mesh/Billboard/Trail arrangement. Existing 2D compiler, Patch, runtime and S9 suites remain part of the full test run. The sole S9 cleanup expectation was widened from the retained `fireball_2d` to the two formal retained outputs (`fireball_2d`, `fireball_3d`); no historical evidence payload was changed.

| Verification | Result |
|---|---|
| compile check | exit 0 |
| EditMode full suite | 104 total / 69 passed / 0 failed / 35 historical Explicit skipped |
| PlayMode full suite | 5 total / 5 passed / 0 failed |
| Generated cleanup | only `fireball_2d` and `fireball_3d`; no S10 test/temp/backup/pending output |
| Recipe/history cleanup | only formal 2D/3D Recipes and retained 2D Patch; no temporary S10 Recipe/history |

## Scope limits

This stage does not enter S11: no release stabilization, performance certification, versioning, migration, broad documentation finalization or git submission. Static Manifest cost remains a budget precheck, not device performance proof. See [S10_2D_3D_DIFFERENCES.md](S10_2D_3D_DIFFERENCES.md) for the sharing boundary and all explicit Unsupported behavior.

## Main-acceptance critical correction

Main acceptance found that the former unanchored `.gitignore` rule `[Bb]uild/` matched the formal package path `project/Packages/com.vfxcomposer.unity/Editor/Build/`, making compiler source and `.meta` files invisible to Git. This Critical repository-integrity defect is corrected in S10: all Unity generated-folder and generated-file rules are now explicitly anchored to `/project/` (or the intended repository-root cache), and `tools/audit-gitignore.bat` machine-checks both protected formal paths and intended ignored outputs. This correction is limited to repository tracking policy; it does not change S10 semantics or enter S11.
