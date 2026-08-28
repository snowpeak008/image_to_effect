# S12 Slash render-order Spike report

> Scope: isolated Unity **2022.3.62f3c1**/URP render-order Spike only. **GO for the separate S12 Recipe v2 implementation path, pending Main-Agent review; NO-GO for any projectile-v1 disguise or for treating this Spike as a production asset.** No formal v2, Compiler, Runtime, Template, Recipe, Generated, package-version, or release-document implementation was performed.

## Exit result

The Spike demonstrates a lower-left to upper-right character-scale curved slash at the reviewed 0.45 s cadence. It uses an extruded mesh ribbon, not a camera card or the reference image; a yellow-white inner edge; an independent red afterimage renderer; and detached live ParticleSystem sparks plus deterministic diamond witnesses for capture. Camera HDR is disabled and there is no Bloom, floor, reflection, gameplay, character, weapon, hit, collision, or animation dependency.

The visual target remains `docs/slash/reference/slash-visual-target-v1.png`, SHA-256 `D5ED31F1F7C8A1C37DBE828980188CC2EEB58515953915A8727C2FE25B4362A5`. It was inspected as a design reference only: no copy, texture, flipbook, or whole-image card was imported.

The temporary authoring boundary is deliberately retained for review:

- `project/Assets/VFX/Spike/S12/` — preview scene, six meshes, seven URP materials, and whole-file `UNITY_EDITOR` authoring/test code.
- `docs/spike-notes/s12-evidence/` — the nine PNG witnesses and metadata.

Nothing under `Assets/VFX/Templates`, `Assets/VFX/Recipes`, or `Assets/VFX/Generated` was written by this Spike.

## Visual and scheduling evidence

The fixed preview scene is `project/Assets/VFX/Spike/S12/S12_SlashSpikePreview.unity`. The independent non-empty roots are `anticipation`, `primary_arc`, `afterimage`, `sparks`, and `dissipation`; renderer enablement is applied explicitly before each hidden-graphics-device `Camera.Render` capture. Capture never uses `-nographics`.

| Witness | Review condition | Result |
| --- | --- | --- |
| `front.png` | dark, front, 60° FOV | Rising pointed sweep, inner edge, red residual ribbons, detached sparks, 1.8 m scale reference. |
| `side.png` | neutral, true side, 60° FOV | Camera `[8.2, 2.8, -0.6]` toward `[0, 0.38, 0]`: lateral offset is 8.2 and depth offset 0.6 (7.3%), proving side-on thickness/order rather than a 45° oblique. |
| `oblique_top.png` | bright, oblique-top, 60° FOV | Readable on a bright background with Bloom disabled. |
| `close.png` | dark, close, 60° FOV | Confirms subordinate red afterimage and ordering. |
| `game_distance.png` | neutral, third-person distance, 60° FOV | Confirms character-scale readability without a narrow-FOV aid. |
| `time_anticipation.png` at 0.02 s | short orange lead-in only | Non-empty and smaller than primary. |
| `time_primary.png` at 0.16 s | primary orange body + yellow-white inner blade | Largest warm coverage. |
| `time_afterimage.png` at 0.24 s | independent red residual ribbons + detached sparks | Non-empty and materially larger than dissipation. |
| `time_dissipation.png` at 0.38 s | residual motes only | Non-empty sparse motes; no primary ribbon. |

`metadata.json` records every pose/FOV/target/background/SHA-256, mesh topology, material/shader/queue/alpha, renderer bounds, and capture facts. Every standard view is locked by the targeted test to the 55–65° third-person range; the side pose is additionally locked to `|z| <= 15% |x|`. The four time hashes are distinct. Pixel analysis excludes the fixed neutral scale reference by comparing warm/red occupancy: anticipation `157`, primary `1968`, afterimage `1685`, dissipation `129` warm pixels. Thus primary > afterimage > dissipation and anticipation < primary; every phase also has visible/warm/red pixels.

The actual particle emission states are sampled after `ApplyTime`: sparks at 0.16 s have local bounds `1.6825 × 1.3725 × 0.3225`; dissipation at 0.24 s has `1.44 × 0.56 × 0.2`. This replaces the invalid near-zero stopped-system bounds seen in the earlier capture.

## Technical facts

- Six meshes: each ribbon has 100 vertices / 192 triangles; the diamond spark mesh has 8 vertices / 4 triangles. EditMode verification rejects NaN, infinity, degenerate triangles, and empty bounds.
- All seven materials use `Universal Render Pipeline/Particles/Unlit`. Reviewed transparent render queues are scale `3020`, dissipation `3050`, afterimage `3070`, body `3100`, anticipation `3120`, inner blade `3130`, sparks `3150`.
- The afterimage is two separate red mesh renderers under the `afterimage` root, never fixed children hidden within the primary arc. The prior Gate-F-plan phrase “Prefer fixed afterimages inside `arc_sweep` initially” is superseded in `docs/slash/S12_IMPLEMENTATION_PLAN.md` by the Gate F decision.

## Validation run

Only the requested checks ran; no full suite and no Player build ran.

1. `cmd /c tools/compile-check.bat` — pass, Unity exit code 0.
2. Unity 2022.3.62f3c1 hidden-graphics-device batch authoring/capture (`Camera.Render`, no `-nographics`) — pass, return code 0.
3. Targeted EditMode filter `VFXComposer.Spike.S12.Tests.S12SlashSpikeEditorTests` — **2/2 pass**. It verifies topology/materials, independent roots, live particle emission at 0.16/0.24, phase-sampled nontrivial particle bounds, separated positions, five distinct views/background coverage, four different time hashes, rendered color-occupancy relations, metadata hashes, material queues, and recorded bounds.

## Failure and repair trail

The Gate was not relaxed during iteration.

1. The first capture left phase visibility dependent on staged editor state. Timeline images duplicated, the afterimage could be blank, and ParticleSystem bounds were zero. The repair introduced explicit per-frame renderer-state application and `Camera.Render` captures.
2. A serialised phase-marker experiment was invalid because the component lived in an Editor-only script and Unity could not attach it reliably. It was removed rather than masked; named non-empty phase roots are now the authoritative Spike boundary.
3. The second targeted test exposed an erroneous absolute `afterimage <= 12 pixels` assertion and expected non-serialised particle emission to survive reopening the scene. The repair applies time at test time, emits and simulates actual ParticleSystems, derives local bounds from live particle positions, records `phaseSampleTime`, and asserts relative warm/red coverage instead of a brittle arbitrary pixel cap.

## Protected v1 recheck

Post-Spike recheck used the Gate-F filename-plus-SHA method: lexicographically ordered generated files, each `/relative/path SHA256`, newline-joined without a trailing newline. All exact values match the pre-Spike decision:

| Protected value | Result |
| --- | --- |
| 2D Recipe SHA-256 | `53C308EBD4C5DCED06A65618A71ECAB27955F160A174EB7CB91CDB4CBBEEDB88` — match |
| 3D Recipe SHA-256 | `1311E824313C3043EC6F75B4A086BB8A7D96FCE9408117D929D3C82E25B60AF2` — match |
| 2D Prefab GUID | `edfdb8327c7bd234c94f0f4338c35816` — match |
| 3D Prefab GUID | `27d60143a7650dd4fb850abed3ca178b` — match |
| 2D output manifest, 18 files | `B86A5932C8CC20644E0A7B2FB6FB2F2C51B4EB2BF6842F867FC0A8AD31EC1240` — match |
| 3D output manifest, 20 files | `4B8FC85CCF7E8EF9D2489E3706EF1413238D231FFB91831E754E0D886AA20FCD` — match |

## Remaining boundary and recommendation

This is enough to authorize the separately dispatched formal v2 work after Main-Agent acceptance: it proves the independent afterimage, phase render ordering, URP transparency, live particle bounds, and Bloom-off readability gate. It is not a production-quality content pass: the ribbons and diamond motes are deliberately primitive, there is no formal v2 schema/compiler/runtime/preview/AI/Patch contract yet, and the Spike's frame controls are evidence controls rather than a shipping scheduler.

Do not promote the Spike assets by moving them into formal folders. Formal work must begin from the revised plan and preserve v1 byte outputs/GUIDs, keep every phase semantically non-empty, and retain `afterimage` as its own module/template/renderer.
