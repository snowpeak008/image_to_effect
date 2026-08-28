# S15 Slash visual delta and technical plan

> Status: **user conditionally accepted on 2026-08-24: “内容还可以，但无法做商用”.** This is a signed conclusion, not an unconditional visual pass or commercial-use approval. S14 remains withdrawn.

## Input boundary

The approved specification is `docs/slash/reference/slash-visual-target-v1.png`. It defines a visual family, not a texture to copy or a full-screen card to use. The user Game and Scene screenshots remain rejected observations only. S15 will not modify v1 fireballs, their Recipe bytes/GUIDs/output hashes, or any frozen AI evidence.

## Why the current result fails the reference

The present S14 capture proves a camera/playback path, but it does not resemble the approved target closely enough:

| Reference characteristic | Current S14 result | Required S15 correction |
| --- | --- | --- |
| One broad, forceful crescent with a clear mass | Three narrow, smooth, low-poly-looking ribbons | Build a wide, tapered crescent silhouette with a filled energy mass; the crescent is the image, not an outline. |
| Yellow-white inner cut, orange/yellow core mass, red outer fire/fragment envelope | Parallel hard-edged bands with similar visual weight | Use explicit nested value/area hierarchy: bright inner cut, much larger orange energy layer, discontinuous dark-red exterior. |
| Brush/flame edge: broken, feathered, asymmetric, with tongues and chunks | Regular strip edges and repeated geometric teeth | Drive alpha breakup and edge extrusion from local brush/noise masks plus deliberately irregular mesh contour points. |
| Strong lower-left ignition sweeping into upper-right | The primary reads as a short central arc and starts weakly | Recompose the serialized authority camera and arc centreline so the active crescent occupies roughly 40–55% width and 45–70% height. |
| Layers form a single energy event | Spark/afterimage elements read as separate point pattern or bare red remnant | Keep 4–8 readable highlights, attached to outer arc/end direction; retain short orange brush fragments through the early decay. |

The S14 run must therefore remain useful only as playback/camera-chain evidence. It is not visual acceptance evidence for S15.

## Chosen implementation: local masks + URP shader + layered crescent meshes

S15 will use project-owned transparent masks as **local material inputs**, not as a pasted reference image. The requested assets are:

1. `S15_SlashBrushMask.png` — monochrome/alpha brush stroke with asymmetric wisps and gaps, tile-safe only across the short axis; no scene/background pixels.
2. `S15_SlashBreakupNoise.png` — grayscale, non-directional breakup/noise mask for animated clipping and irregular flame fragmentation.
3. `S15_SparkAtlas.png` — small transparent diamond/ember shapes, preferably two or more variants in an atlas; no reference composition.

After delivery, each texture will be imported as a named formal S15 asset and its source SHA-256, import settings, GUID, dimensions, alpha mode, and licence/provenance will be recorded. No texture is accepted if it contains the whole reference composition, background, ground reflection, or an already-composited crescent scene.

### Mesh layers

One shared curved centreline (48–64 adaptive segments) will drive three authored mesh families with independent contour widths and UVs:

- **Inner blade** — narrow, sharply tapered yellow-white curved cut. It uses a bright centre and a small warm fringe, with no bloom dependency.
- **Energy body** — the largest orange/yellow crescent area. Its outer contour has nonuniform widths, lobes, occasional splits, and a brush-mask alpha. It must remain visibly filled, not resolve into rails.
- **Outer flame/fragment layer** — dark red/orange, wider but lower-alpha trailing masses and 6–12 short procedural tongue/fragment surfaces. They follow the arc but are not parallel strips; each uses offset UV flow and different breakup threshold.

The taper is intentional: a strong lower-left ignition wedge transitions to the broadest mid-arc energy mass and resolves into a sharp upper-right terminal point. Fragments are local meshes with deterministic seeds, not a cloud of large ParticleSystem diamonds.

### Shader behaviour

A custom URP transparent unlit shader will sample the supplied brush alpha and breakup noise in local UV space. It will expose material-property-block controls for:

- sweep/reveal coordinate along the crescent (`u`), with a soft burning reveal edge rather than the present hard strip clip;
- independent inner/body/outer colour ramps and alpha/brightness scales;
- low-amplitude UV flow and per-layer noise offsets, preserving a stable silhouette while avoiding static polygon bands;
- animated breakup/dissolve during `.20–.45`, with threshold movement that produces shrinking pieces instead of frame-to-frame flashing.

It will use additive/premultiplied transparent blending deliberately and `ZWrite Off`; no Bloom, postprocess, scene reflection, or floor is part of the look. The shader is constrained to the local meshes, so it cannot present a full-screen reference image.

### Particles and timing

The primary remains true continuous playback from the serialized controller's ordinary `Update`; no `SampleForPreview`, manual `Emit`, or phase toggles are permitted in authority capture. The main mesh reveal moves lower-left to upper-right during `.04–.20`; the brush mask’s reveal edge exposes successively wider energy mass.

Spark capacity stays sparse: 4–8 visible high-brightness diamond/ember particles around the outer mid/terminal arc, approximately 3–8 pixels each at 960-wide authority capture, varied in size/rotation/launch direction. They must not become the silhouette. At `.20–.33`, retain orange brush fragments plus the red external flame layer; from `.33` onward all layers shrink/dim continuously, with no isolated flicker frames.

## Acceptance frames and review criteria

All review frames are captured from the same serialized `S12_SlashGeneratedPreview` MainCamera via the existing true PlayMode recording path. No free Scene camera or separate evidence camera is admissible.

| Time | Required visual state |
| --- | --- |
| `.016–.033` | Small but clear lower-left ignition wedge/brush flash; no full crescent. |
| `.10` | Sweep visibly in progress; a growing broad orange/yellow mass with adjacent white inner cut, not thin parallel rails. |
| `.166` | Dominant wide crescent: 40–55% screen width and 45–70% screen height; readable yellow-white inner blade, larger orange body, irregular red fire/fragments; 4–8 sparse highlights. |
| `.233` | Shortened red outer afterlayer plus still-readable orange fragmented energy; not a single bare red arc. |
| `.333` | Residue is smaller/darker and continuously declining, with no flash/reappearance. |
| `.416` | Only minimal dim residue, if any. |
| `.45/.451` | Empty effect. |

Human visual review is required for family resemblance, flame/brush silhouette, area hierarchy, and blade/spark separation. Metadata/readback can enforce run identity, actual player-loop timing, live count, projected spark size/area, and fade trend, but cannot by itself certify the image looks like the reference.

## Implementation transaction and rollback

When textures are supplied, implementation will occur in one named S15 formal-authoring transaction:

1. validate/import only the listed local masks and record their identities;
2. author S15 mesh/shader/material/template changes and manifest costs;
3. rebuild canonical Slash Generated output and the fixed Preview scene while preserving the stable Generated Prefab GUID where the compiler permits;
4. compile, run targeted tests, explicitly record a new real-Update authority run, inspect the required frames, then run the required regression suite only after visual inspection is acceptable;
5. retain only the final authority run and user-rejected comparisons; mark prior S14 visual runs rejected/legacy rather than passing evidence.

If an imported texture is unsuitable, compilation fails, a visual gate is missed, or the recorded effect still reads as low-poly rails/fragments, the transaction rolls back the S15 template/material/Generated/Preview changes together and leaves the existing S14 assets intact but still visually rejected. It never rolls back or rewrites v1 fireball or frozen-AI artifacts.

## Implementation checkpoint — user review completed

The supplied generated assets were imported as formal local inputs and sampled only within the new S15 painted-crescent action-plane shader. The full `Main` RGBA is mapped onto a 32×20 curved action-plane mesh with transparent surroundings; it is not a full-screen scene card. Breakup noise is sampled only by the shader's animated local dissolve, and the 2×2 atlas is assigned only to the sparse ParticleSystem mesh sprites. The old narrow primary ribbon mesh is no longer the primary renderer.

The sole retained true-`Update` authority sequence is [`run-20260823T041806959Z`](s15-wysiwyg-evidence/run-20260823T041806959Z/metadata.json), from the same serialized MainCamera and 60 fps player-loop recorder. The historical S15 trials that still physically exist are [`run-20260823T033835847Z`](s15-wysiwyg-evidence/rejected-runs/run-20260823T033835847Z/metadata.json), [`run-20260823T034435960Z`](s15-wysiwyg-evidence/rejected-runs/run-20260823T034435960Z/metadata.json), [`run-20260823T034840926Z`](s15-wysiwyg-evidence/rejected-runs/run-20260823T034840926Z/metadata.json), [`run-20260823T035055262Z`](s15-wysiwyg-evidence/rejected-runs/run-20260823T035055262Z/metadata.json), [`run-20260823T035254966Z`](s15-wysiwyg-evidence/rejected-runs/run-20260823T035254966Z/metadata.json), and [`run-20260823T041613756Z`](s15-wysiwyg-evidence/rejected-runs/run-20260823T041613756Z/metadata.json); all remain rejected and none is an alternate authority. The prior `.166` inspection note records a thick painted crescent, white inner energy, orange mass, red flame silhouette, and detached atlas-spark observations along the outer edge/terminal direction. The `.20–.333` note records a same-shape red/orange local-texture dissolve rather than the prior bare narrow red line. Those observations do not widen the signed user conclusion or certify commercial usability.

### Origin-anchor checkpoint

`SlashOriginAnchor.MainTextureUv = (0.166, 0.068)` is the white lower-left ignition-core UV measured from the supplied Main RGBA, not an alpha-bounds extremum. `CurvedActionPlane` subtracts that UV in X/Y **and** subtracts its curve/wave displacement, so interpolating the anchor UV maps to local `(0,0,0)`. Primary, afterimage, and residue have no positional phase offset; the residue scale therefore contracts around the same origin. The ignition brush/star are also authored at local zero. The authority metadata records every key-frame projection for all three anchors: 28/28 records in the retained run, with a measured maximum separation of `0 px` (threshold `<= 3 px`). `S15SlashOriginAnchorTests` additionally interpolates the generated mesh UV to assert the three world anchors agree within `0.01 m`, and checks the metadata alignment. The targeted EditMode run passed `22/22` tests. This machine checkpoint does not replace the user's signed conclusion below.

### Read-only detached-warm-component geometry audit v2

Independent review rejected the v1 component-to-particle interpretation. V1 remains byte-for-byte at `s15-wysiwyg-derived/run-20260823T041806959Z-projection-audit-v1/` only as a superseded historical record, with status `SUPERSEDED_AFTER_INDEPENDENT_AUDIT_NO_GO`; it is not active evidence. Its hash-bound verifier `tools/vfx/s15_wysiwyg_projection_audit.py` is likewise frozen solely for historical reproducibility and must not be invoked as the active audit. The corrected verifier is `tools/vfx/s15_wysiwyg_projection_audit_v2.py`; its write-once outputs are [`projection-metrics.json`](s15-wysiwyg-derived/run-20260823T041806959Z-projection-audit-v2/projection-metrics.json) and [`PROJECTION_AUDIT.md`](s15-wysiwyg-derived/run-20260823T041806959Z-projection-audit-v2/PROJECTION_AUDIT.md).

V2 hash-verifies the retained `metadata.json` and all 28 PNGs without rewriting an authority byte. RGB is allowed to report only detached warm candidate components and their image geometry. It cannot identify a candidate as a spark: a candidate may instead be dissipation, a mesh fragment, an anti-aliased island, or another source. Recorder spark and dissipation counts are copied only as context and summed into `unattributedRecorderLiveCount`; no candidate-to-instance correspondence or ratio is inferred. For example, frame `0020` records one spark plus three dissipation instances (`unattributedRecorderLiveCount = 4`) and one detached warm candidate, but those facts are not matched.

Detected warm candidate pixels do not touch the canvas border; their minimum measured clearance is `148 px`. That statement cannot reveal an off-canvas projection. True spark canvas containment, blade overlap, visibility, and occlusion all remain `PENDING_INSTANCE_ID_OR_DEPTH_DIAGNOSTIC`; off-canvas, merged, occluded, below-threshold, dissipation/fragment, and foreign-source cases are indistinguishable in the sealed beauty RGB. Thresholds remain `PENDING_HUMAN_OR_DIAGNOSTIC_CALIBRATION`. These are geometry measurements, not a visual pass, and they do not widen the user's commercial-use restriction.

## User visual conclusion

On 2026-08-24, the user signed `Assets/VFX/Preview/S12_SlashGeneratedPreview.unity` as **conditional pass**, with the exact conclusion: **“内容还可以，但无法做商用”.**

The user did not specify a particular failing frame or a condition for lifting the restriction. This record does not infer whether the commercial-use restriction arises from quality, licensing, provenance, or any other cause. It is not an unconditional visual pass, is not recorded as commercial-use approval, and does not authorize rework. The finalized acceptance record and authority-frame index are in `S15_REPORT.md`.
