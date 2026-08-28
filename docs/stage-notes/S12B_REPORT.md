# S12B report — Slash v2 compiler, Runtime, and generated preview

> Result: **Main-Agent independent acceptance passed.** Independent verification recorded compile 0, S12A EditMode 6/6, S12B EditMode 6/6, and S12B PlayMode 1/1; Patch and AI workflows remained untouched at S12B close.

## Implementation

- `S12CompilerDispatcher` explicitly routes Recipe v1 to the unchanged `VfxCompiler` and Recipe v2 to the isolated `S12SlashCompiler`. Direct v2 compiler use rejects v1 before writing.
- The v2 compiler validates, dry-runs, and builds only the closed five-phase Slash contract. Build deep-copies the five formal Slash prefabs, applies parameters, seeds real ParticleSystems, clones shared source materials once per source identity, and creates the sole v2 output at `Assets/VFX/Generated/slash_3d_stylized`.
- Generated hierarchy is inspectable by stable phase/module IDs. `SlashEffectController` has explicit serialized sibling phase bindings; it is Runtime-asmdef only and exposes exactly `PlaySlash(Vector3, Quaternion)`, `StopEffect(bool)`, and `ResetForPool()`.
- `SlashAfterimageAlpha` persists afterimage alpha as serialized data and reapplies it through `MaterialPropertyBlock` on enable, avoiding material cloning. The generated Prefab therefore survives reload with non-default alpha intact.
- `S12_SlashGeneratedPreview.unity` contains an actual instance sourced from the generated Prefab and one player-safe `SlashPreviewPlaybackDriver`. It replays only `PlaySlash`; it does not compose Gold Sample templates. The Editor preview dispatcher selects it for Recipe v2.
- `docs/stage-notes/s12b-evidence/` is captured from an instantiated `Assets/VFX/Generated/slash_3d_stylized/VFX_Slash_3D_Stylized.prefab`, through its `SlashEffectController`, with 60° FOV, HDR false, and no Bloom. It records primary-overlap (0.18 s), afterimage, dissipation, and complete PNG SHA-256 values plus source Prefab GUID, recipe hash, build hash, and live ParticleSystem count/distinct-position/bounds facts for every frame. These PNGs are independently generated and do not reuse S12A evidence.

## Determinism, budget, and transaction boundary

The v2 build hash covers canonical recipe bytes, revision, compiler identity, Unity version, and every resolved template ID/version/GUID/dependency hash. A complete matching manifest, prefab, and all recorded generated materials are required before DryRun returns `Unchanged`.

Actual generated Prefab checks enforce at most 7 renderers, 4 ParticleSystems, 48 max particles, and 5 distinct generated VFX materials; the current output is within those gates. The manifest records the material paths read back from the final saved Prefab rather than a declared count.

Commit first copies the precise existing output directory bytes (including nested `.meta` files) to a uniquely named OS temp directory, then writes Prefab/materials/manifest. Fault injection immediately after Prefab/material writes proves both first-output and existing-output recovery. On failure it restores output bytes and GUIDs, removes `.pending`, removes first-output folder/meta where applicable, refreshes AssetDatabase, and finally clears both the direct Generated temp folder and OS byte backup.

## Tests run

- `tools/compile-check.bat`: pass, Unity compile exit 0.
- EditMode filter `VFXComposer.Tests.EditMode.S12BSlashCompilerTests`: **6/6 pass**.
- PlayMode filter `VFXComposer.Tests.PlayMode.S12BSlashRuntimeTests`: **1/1 pass**.

Only these targeted filters were run; no full suite and no Player build were run. The PlayMode command intentionally follows EditMode because the latter serializes/revalidates the generated preview scene.

EditMode covers dispatch/mismatch no-write, Validate/DryRun no-write, plan/build/idempotence/GUID stability, actual generated materials and budgets, no missing MonoBehaviour, serialized phase hierarchy, dependency-only invalidation, non-default `duration`/afterimage `count`/`alpha` prefab reload and MPB application, transaction byte/GUID rollback, residue cleanup, clean-path additive preview creation/source/driver, generated-runtime image evidence hashes, live Spark/Dissipation particle count/distinct-position/bounds gates, primary > afterimage > dissipation > 0 / complete = 0 pixel relations, v1 frozen values, and exact final Generated directory set. Both Edit/Play logs contain zero `Unloading the last loaded scene` warnings.

PlayMode covers generated preview loading, controller transform pose, anticipation/primary/afterimage/sparks overlap timing, natural Spark playback at about 0.18 s with at least five live particles at five distinct positions, graceful Stop final cleanup, completion through `WaitUntil(!IsPlaying)` within timeline + 0.10s, Reset, replay particle seed stability, and no accumulated particle count. The preview driver is asserted singular then disabled for deterministic controller timing; there is no second replay source.

The later S12 final full-suite audit found that regenerating these committed PNGs inside an ordinary EditMode run could crash Unity 2022.3 in the native URP particle render path. `CaptureBatch` remains available as an explicit evidence-authoring menu action. Routine regression now calls the read-only verifier and checks the committed PNG hashes, metadata-to-current Recipe/Build/Prefab identity, recorded particle facts, and visual-state relations without invoking `Camera.Render`.

## Repair chain

1. Initial v1 catalog recursive `*.json` scan saw v2 Slash manifests. It now explicitly excludes the distinct `.slash.manifest.json` suffix, preserving v1 parser/catalog/compiler semantics.
2. Initial material cloning copied per renderer slot. It now uses a source-material path map and the final Prefab’s real distinct material paths.
3. Initial rollback used AssetDatabase backup copies, which cannot prove byte-exact recovery and caused duplicate-GUID import warnings when `.meta` files lived under Generated. It was replaced with a safe OS-temp byte snapshot and exact directory restore.
4. Initial non-immediate Stop set no draining completion. Runtime now drains until live particles clear, then clears phase roots and MPB state.
5. Initial preview captured a one-time Editor call. It now contains a serialized Runtime-safe playback driver and validates the generated Prefab source. Preview creation itself always uses `NewSceneMode.Additive`. For a batch/test untitled host, it opens the existing saved Gold host with `OpenSceneMode.Single`, retains that last saved scene, then creates and closes only the additive preview. It never saves or modifies a caller's untitled scene and leaves no `project/Temp/s12b_preview_host_*` scratch residue.
6. A fixed `WaitForSeconds(.46)` completion assertion was frame-boundary sensitive. The controller uses `elapsed >= timelineDuration`; PlayMode waits for actual completion with a bounded tolerance.
7. Initial generated evidence advanced a too-young Spark phase at 0.16 s, so its first runtime sample was empty even though later sampling produced natural particles. The evidence’s primary-overlap capture is therefore 0.18 s (primary remains active through 0.20 s), where the template's own burst has 14 live randomized Spark particles; PlayMode independently verifies the same natural controller playback at about 0.18 s. Formal Spark/Dissipation templates now use real Box shapes/random direction/speed rather than hand-written capture positions. The generated capture never calls `Emit` or sets particle positions; primary/afterimage show 14 distinct Sparks, dissipation shows 6 distinct motes, and complete is empty.

## v1 protection recheck

| Protected value | Result |
| --- | --- |
| 2D Recipe SHA | `53C308EBD4C5DCED06A65618A71ECAB27955F160A174EB7CB91CDB4CBBEEDB88` match |
| 3D Recipe SHA | `1311E824313C3043EC6F75B4A086BB8A7D96FCE9408117D929D3C82E25B60AF2` match |
| 2D/3D Prefab GUID | `edfdb8327c7bd234c94f0f4338c35816` / `27d60143a7650dd4fb850abed3ca178b` match |
| 2D output hash | `B86A5932C8CC20644E0A7B2FB6FB2F2C51B4EB2BF6842F867FC0A8AD31EC1240` match |
| 3D output hash | `4B8FC85CCF7E8EF9D2489E3706EF1413238D231FFB91831E754E0D886AA20FCD` match |
| Generated directory set | exactly `fireball_2d`, `fireball_3d`, `slash_3d_stylized` |

No `s12btmp_*`, backup, or `.pending` residue remains in Generated or the OS byte-backup temp location.

## Boundary

No Patch or AI implementation was added. S12B is complete pending Main-Agent independent review; do not begin the next stage without authorization.
