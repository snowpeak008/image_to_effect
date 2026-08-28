# S12A report — Slash Recipe v2 domain and formal Gold Sample

> Result: **GO for independent review and, only after that review, the separately authorized Compiler stage.** This stage did not modify the v1 compiler, Runtime, Patch, AI workflow, or `Assets/VFX/Generated`.

## Scope completed

- Added a closed, independently dispatched Recipe v2 Slash model/parser/validator/budget/catalog under `Editor/SlashV2`; v1 still calls the existing `VfxDomainParser` unchanged.
- Added the canonical `slash-3d-stylized.default.v2.json`: finite 0.45 s timeline, exactly five ordered sibling visual phases, exactly one non-empty role module per phase, and no graph/events/conditions/nesting/cross-phase attachment.
- Added five original formal 3D prefabs and separate manifests in the formal Slash boundary: anticipation glint, primary arc sweep, separate afterimage, sparks, and dissipation. The Spike remains isolated and was not copied into these inputs.
- Added the formal Gold Sample scene and hash-recorded evidence under `docs/stage-notes/s12a-evidence/`. Captures use a hidden graphics-device Unity batch `Camera.Render`, not `-nographics`; HDR is disabled and the effect is readable without Bloom.

## Contract and asset gates

The v2 contract rejects unknown fields, non-finite values, unsafe IDs, wrong version/archetype/dimension, arbitrary phase order/timing, empty or wrong-role phases/modules, unsupported template/parameters, and out-of-range values. Manifests require the exact closed phase/module/parameter/binding map, safe prefab paths, non-negative cost, valid/unique material GUIDs, and a mandatory resolver.

The actual prefab audit—not only manifest declarations—records: 7 transparent renderers, 3 ParticleSystems, 37 max particles, and 4 distinct real VFX material GUIDs. It rejects missing/incorrect URP shaders, bad queues, mesh NaN/Infinity/repeated indices/scale-relative degeneracy, TrailRenderer, light, collider, sub-emitter, foreign formal dependencies, oversized textures, material-GUID/path resolver failures, and cost mismatches.

`arc.duration` is bound to the visible `PrimarySweepRunner` particle lifetime, not a hidden clock. `afterimage.alpha` uses `MaterialPropertyBlock`, so it does not clone materials. All public parameter minima/defaults/maxima are applied and read back from real prefab targets. The canonical recipe’s numeric values equal the corresponding manifest defaults. Both `mobile_medium` and a cloned `pc_editor` canonical recipe execute validation and the same budget evaluator.

## Gold evidence

`metadata.json` records Unity `2022.3.62f3c1`, 1.8 m neutral-grey scale reference, target/pose/background/FOV/SHA for five views and five timeline frames.

- Front, true-side, oblique-top, close, and game-distance views are distinct; every view is 60° FOV. The side camera has |z|/|x| = 0.073, proving a near +X side view rather than a 45° oblique substitute.
- Dark, neutral, and bright backgrounds are represented. The primary sample at 0.16 s contains its deliberate afterimage/spark overlap; the independent 0.24 s afterimage sample is subordinate, and the 0.38 s dissipation sample is sparse. Pixel gates require primary warm area > afterimage > dissipation > 0.
- At 0.24 s the real ParticleSystem supplies 8 particles at 8 distinct positions with a substantive local bounds size `[3.18, 2.8, 1.26]`, and image analysis finds at least five separated warm clusters. At 0.38 s dissipation supplies 5 particles at 5 distinct positions and image analysis finds 3–6 clusters. At 0.451 s warm VFX pixels are zero.

## Verification

- `tools/compile-check.bat`: passed, Unity compile exit 0.
- Targeted EditMode `VFXComposer.Tests.EditMode.S12ASlashDomainAndAssetsTests`: **6/6 passed**. This is the only test filter run; no full suite or Player build was run.
- The targeted protection test locks exactly two v1 recipe byte SHA values, two generated Prefab GUIDs, and both 18/20-file generated directory hashes. The original two-directory absence assertion was a valid S12A pre-Compiler gate; after S12B authorization it is superseded by the lifecycle assertion that the complete Generated set is exactly `{ fireball_2d, fireball_3d, slash_3d_stylized }`, with the Slash Prefab GUID and BuildManifest present.

The generated-manifest hash intentionally reproduces the Gate F PowerShell algorithm: invariant-culture path ordering, slash-relative paths, upper-case byte SHA-256 values, LF joining, and no trailing LF. An earlier use of ordinal ordering produced a different hash solely because `VFX_Fireball` sorted before lower-case names; it was corrected without changing any frozen asset or expected value.

## Failure and repair chain

1. Initial formal particle visuals were tiny because Mesh-mode particle `startSize` multiplies the small diamond mesh. A temporary static witness idea was rejected: the witness renderers/assets were removed, and captures now emit/Simulate actual formal ParticleSystems at visible sizes with real particle/bounds/cluster checks.
2. The first absolute mesh-area threshold flagged the narrow tip of `MESH_S12A_Anticipation` (triangle 217, indices `109,112,113`) rather than a repeated-index or zero-area triangle. The test now names asset path, mesh, triangle index, indices, cross², and threshold, rejects repeated indices, and uses a scale-relative nonzero threshold. The Ribbon’s narrow-end taper was also increased to 18%, so the formal geometry retains substantive area.
3. Initial material labels could have hidden actual material multiplicity. They are now real material GUIDs, resolver-validated inside formal Slash Materials, and actual prefab material-GUID sets must match every manifest exactly.
4. The first v1 directory-hash test used ordinal ordering and got `070FE…`; Gate F had frozen the PowerShell/invariant-culture ordering `B86A…`. The algorithm is now explicitly reproduced and tested.
5. During S12B generated-runtime review, hand-authored Gold Sample particle positions were correctly judged insufficient proof of natural playback. The formal Spark and Dissipation ParticleSystems were upgraded with real Box shapes, randomized directions, authored speed/lifetime/mesh size, and bounded renderer extents. S12A’s rerun confirms the unchanged formal contract/budget; S12B then rebuilds the generated clone and captures it via the Runtime controller without `Emit`, static witnesses, or written particle positions.

## Frozen v1 recheck

| Protected value | Recheck |
| --- | --- |
| 2D Recipe SHA | `53C308EBD4C5DCED06A65618A71ECAB27955F160A174EB7CB91CDB4CBBEEDB88` match |
| 3D Recipe SHA | `1311E824313C3043EC6F75B4A086BB8A7D96FCE9408117D929D3C82E25B60AF2` match |
| 2D/3D Prefab GUID | `edfdb8327c7bd234c94f0f4338c35816` / `27d60143a7650dd4fb850abed3ca178b` match |
| 2D output manifest (18 files) | `B86A5932C8CC20644E0A7B2FB6FB2F2C51B4EB2BF6842F867FC0A8AD31EC1240` match |
| 3D output manifest (20 files) | `4B8FC85CCF7E8EF9D2489E3706EF1413238D231FFB91831E754E0D886AA20FCD` match |

## Boundary

The original S12A implementation boundary excluded Compiler/Runtime/Patch/AI/Generated work. Its S12B-authorized follow-up above changes only formal particle authoring and the S12A lifecycle protection assertion; it does not enter Patch or AI. The isolated S12 Spike remains present for review.
