# S14 WYSIWYG Slash rebuild

> Status: **visual acceptance withdrawn on 2026-08-23 after user review.** The WYSIWYG/runtime evidence path remains valid, but the authored Slash visual does not match the approved reference closely enough and must not be presented as an accepted visual MVP.

## Scope and rejected comparisons

The Game capture ([`game-observed-rejected.png`](s14-wysiwyg-evidence/rejected-user-comparisons/game-observed-rejected.png), SHA-256 `E33CC0B2C414996465C3F30D68B64578B49CBC088AA21C48E2AF54BC2C11BC12`; original observation alias `codex-clipboard-d7bdc0ca-f7f1-4b9d-bd2e-bb0a588c2a2f.png`) and free Scene capture ([`scene-free-camera-rejected.png`](s14-wysiwyg-evidence/rejected-user-comparisons/scene-free-camera-rejected.png), SHA-256 `3972EF21937184D61E7E84EF087D9FB925192638632B74C5524F43494A176528`; original observation alias `codex-clipboard-ea6c7e98-aa02-49f2-bdc3-9f4f06a5cd4e.png`) are retained byte-for-byte under `s14-wysiwyg-evidence/rejected-user-comparisons/`. They are user-observed rejected comparisons, never target textures or pass evidence. The visual reference `docs/slash/reference/slash-visual-target-v1.png` remains a specification only; no raster reference is placed in the effect.

S12A Gold (`SetFrame`/manual `Emit`) and S12B/C2 independent-camera sampler imagery are **legacy structural evidence and rejected runtime-visual proof**. The legacy BuildAll/Gold menu is retained only for historic formal-authoring compatibility; it is not a Game-view or runtime-proof command.

## Implemented authority path

- `S12_SlashGeneratedPreview.unity` contains the serialized Generated prefab, preview driver, and exactly one authority `MainCamera`; it has no Gold object, scale witness, or evidence camera.
- The camera is SolidColor `[.075,.082,.095,1]`, HDR/MSAA off, FOV 60, layer-0 culling, pose `(0,1.25,-3.7)`, and is the capture camera as well as the ordinary Game-view camera.
- The primary reveal is driven by the enabled controller's normal `Update` and an MPB `_Reveal` value: orange ragged flame body, narrow yellow-white inner blade, and subordinate red/orange afterlayers are generated meshes, not a target-image card.
- The sparse spark template defaults to nine directional diamond particles. It uses size and alpha lifetime curves; dissipation is smaller/darker. The canonical static budget is derived from the generated formal manifests: `37` peak particle capacity, `3` ParticleSystems, `4` unique materials, `7` transparent renderers (limits `48/4/5/7`). This is static preflight only, not device certification.

## Rejected S14 authority evidence

The former run is [`run-20260823T030633958Z`](s14-wysiwyg-evidence/rejected-runs/run-20260823T030633958Z/metadata.json). It is retained only as rejected playback-chain audit evidence: a 28-frame, 960×540, 60 fps ordered PNG sequence from `0` through `.45` seconds. Its metadata records:

Run-link audit (2026-08-25): S14 has **no active authority run**. The linked `run-20260823T030633958Z/metadata.json` exists only under `rejected-runs`; no deleted former run is cited as active evidence in this report.

- scene `Assets/VFX/Preview/S12_SlashGeneratedPreview.unity` and the serialized camera fields above;
- stable Generated Prefab GUID `0dc223c2ffbe2c14aa24f424440f1cd2`;
- Recipe SHA `1b1cd492d9ccaec6ee36c1c48334ab4280e17eae786172ccb85b10ccff066ba5` and build SHA `42d117d2de8ac05ce2f3ca09f51b260490fc504e4b225dfb5c1c9d641171ebe4`;
- one `PlaySlash`, enabled controller, `Time.captureFramerate=60`, normal player-loop `Update`, and a `LateUpdate` observer that only captures completed frames. It does not invoke `SampleForPreview`, `StepForContinuousCapture`, `Emit`, `SetParticles`, phase toggles, or an evidence camera.

This authority refresh records same-frame particle readback: live spark/dissipation counts, projected per-spark size, total projected spark area, and mean alpha at every frame. This is audit data, not a claimed substitute for visual review.

For convenient review outside Unity, `slash-preview-realtime.gif` and `slash-preview-review-slow.gif` in the same run directory are deterministic encodings of those 28 PNGs. They do not re-render, retouch, interpolate, or replace the hashed source frames.

## Gates and review boundary

The recorded sequence is intended for review at `.016/.033/.10/.166/.233/.333/.383/.416/.45`: a compact anticipation lead-in, continuous lower-left-to-upper-right sweep, a subordinate afterimage, sparse non-cloud sparks, and empty completion. Tests may read metadata, hashes, camera fields, and particle facts without invoking `Camera.Render`.

Gate #3 does **not** claim a machine-proven blade-overlap verdict. The particle readback can bound particle count, projected size, area, and lifetime decay, while correct visible separation from the blade was decided by the main agent's explicit inspection of the same authority frames.

## Withdrawn main-agent acceptance

The sole authority sequence was independently inspected at anticipation, reveal, peak, afterimage, dissipation, and completion frames. Its runtime direction, lifecycle, camera consistency, and removal of the original block explosion are valid engineering results, but they are insufficient for visual acceptance.

User review correctly identified that the result is a narrow, flat, low-poly stripe rather than the approved reference's broad crescent flame body. It lacks the required painted/tapered silhouette, layered orange-yellow energy, broken flame tongues, strong origin-to-tip composition, and convincing residual motion. These are core reference requirements, not optional future polish. Therefore this sequence is retained only as rejected engineering evidence and S14 visual work is reopened.

## Verification

- `tools/compile-check.bat`: passed after formal-authoring, capture-metadata, and read-only-test changes.
- Formal S14 template rebuild and canonical Generated Preview rebuild: passed; the Generated Prefab GUID remains `0dc223c2ffbe2c14aa24f424440f1cd2`.
- Explicit authority recorder: passed as a chain check, then moved to `rejected-runs` after visual-family review. At `.166667`, readback records 9 sparks, maximum projected size `4.692139 px`, total projected area `111.1456 px²`, and mean alpha `.815686`; at `.333333`, mean size/alpha are `1.798187 px`/`.443137`, documenting the intended decay.
- Full EditMode: 141 total, 0 failed. Full PlayMode: 8 total, 0 failed. Ordinary tests read authority metadata only and do not call the recorder or `Camera.Render`.

No v1 fireball Recipe bytes, generated hashes, GUIDs, output paths, or frozen AI evidence were changed by S14.
