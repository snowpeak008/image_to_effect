# W24 S3 typed diagnostic production-entity preparation

Status: typed producer source integration and identity freeze complete; formal Prefab/Scene
rebuild and graphics-backed recorder execution remain intentionally pending. No Unity process
was started for this change.

## Production-facing APIs

- `W24S3RuntimeEntry.ReadEmitterHistory()` returns an immutable
  `W24EmitterHistoryReadback`. Its `Samples` are copies of the world positions naturally accepted
  by `W24MovingEmitterTrailProtocol`; no TrailRenderer position or vertex API is used.
- A seed restart clears prior history. Normal stop freezes the accepted history through the
  bounded tail. Immediate stop and pool reset clear it and leave a readable clear reason and
  generation. The external capture producer must supply `Samples[].Position` to
  `W24TrailMaskDiagnosticCapture`.
- `W24S3RuntimeEntry.ReadMissingBindingProbeReport()` returns the structured
  `w24-binding-probes/v1` payload. Exact required outcomes are: missing socket ->
  `MissingTarget`, missing renderer -> `MissingRenderer`, missing mesh -> `MissingMesh`, and
  missing bone -> `MissingBone`. Any returned anchor or renderer makes that probe fail.

## Frozen diagnostic identities

The binding authoring uses required registrations with stable IDs:

- `10`: bound model MeshRenderer;
- `101`: weapon socket marker MeshRenderer;
- `201`, `202`, `203`: the three independent fragment MeshRenderers.

The socket marker is on layer 30. The formal MainCamera excludes layer 30 from Beauty, while the
marker remains active and enabled for the explicit Object-ID command-buffer pass. Fragment
registrations become active for the frozen fragment capture state; registration components do
not alter normal materials or Beauty rendering.

## Contract plans

- Projectile contract revision 26 freezes the serialized front MainCamera, R8/`|u1` binary mask,
  external history source, minimum two history samples, minimum one foreground pixel,
  corridor coverage >= 0.8, off-corridor fraction <= 0.2, seed order
  `24101, 24111, 24121`, and a 9-row required raw-evidence matrix.
- Binding contract revision 25 freezes front and oblique views, R32_UInt Object-ID and R32_SFloat
  finite depth, IDs `10,101,201,202,203`, minimum one pixel per required ID, linear depth >=
  0.0001, cross-view centroid parallax >= 1 pixel, seed order `24201, 24211, 24221`, a 21-row
  required raw-evidence matrix, and front fragment-ID trajectory frames 54/63/72 for IDs
  `201,202,203`.
- The real-light contract revision 24 freezes a 12-row required matrix for effect-mask,
  receiver-ID and isolated linear-LDR off/on artifacts. All three contracts remain
  `VISUAL_PENDING`. The finalized bootstrap identities are B
  r26 `93fce950b2bbea9de590bdbc9594f43bce5894ebf4ccbacafbe370af8d279b58`, C
  r25 `88c752a126d0c0922861af647158b884dfb9095b173e573d3279239ca0f3dc4d`, and D
  r24 `0a7b6c6715834f17c5b4864346c015cc3b96bbd32b5be734d3b2b6b38e959859`; no Visual
  QA or user decision was created.
- Every typed block freezes the replayable metrics environment produced by the controlled
  bridge: `C:/Program Files/Python312/python.exe`, its SHA-256, Python `3.12.4`, NumPy
  `2.4.5`, Pillow `12.2.0`, and canonical environment hash
  `c4fe189aa8e53cf7add49138dd96830056e545cdac44c82a69251e5112df5c49`.

## Exact recorder integration points

In `W24S3GraphicsCaptureEvidenceTests.RunNaturalLifecycle`, replace any use of TrailRenderer
positions as emitter history with `entry.ReadEmitterHistory()`. At retained travel frames, pass
the copied world positions plus the same serialized MainCamera to
`W24TrailMaskDiagnosticCapture.Capture` and write its R8 NPY/hash as the authority artifact for
`REQ-B-TRAIL-CORRIDOR`.

For binding, collect active `W24DiagnosticObjectRegistration` components and call
`W24ObjectIdDepthDiagnosticCapture.Capture` at front fragment frames 54/63/72 and at both frozen
views at frame 72. The frame-72 front `fragment-id` raw is a distinct typed artifact with a
`derivedFrom` link to the same frame's `object-id` artifact; it cannot reuse an `object-id` pass
identity. Separately serialize
`entry.ReadMissingBindingProbeReport().ToJson()` into semantic telemetry for `REQ-C-MISSING`.
Absence of either typed diagnostic must keep formal completion failed.

## Verification and pending identity work

- The source-level validation run is recorded in the producer report; Unity was not launched
  and no graphics smoke was performed.
- A real graphics batch smoke remains required after integration.
- Independent audit hardening now rejects null Object-ID registrations, rejects seed zero,
  serializes the three entry seeds (`24101/24201/24301`), verifies the moving-history seed after
  Play, requires Linear project color space for linear-LDR diagnostics, and serializes the
  projectile protocol's world-space-history invariant. Unity 2022.3 `TrailRenderer` has no
  `LineRenderer.useWorldSpace` property; its history is intrinsically world-space, so the
  protocol flag is the explicit, testable fail-closed equivalent.
- Final source writers have stopped. `w24-s3-capture-tool.bundle.json` now freezes 31 sources:
  the recorder, bridge, S5/S1 validators, producer/runtime, diagnostic C#/shaders, metrics
  Python and input schema. Its canonical bundle hash is
  `sha256:f605aacf4d27128347a2a8434a29cebc95aa789af73df55cc26c6fd7a0e42726`, copied into all
  three capture profiles as `w24-s3-capture/3.5`. The bundle includes
  `W24S3BaselineAuthoring.cs` at
  `sha256:27749de98934cadd6d679b7d6d7b64da2521c89f9dd2a6393376ccef055d7d35`
  with `CompilerVersion = w24-s3-baseline/2.7`. D now owns separate neutral receiver and
  persistently non-black emissive core materials. The next permitted step is a single rebuild/freeze of Prefabs, preview
  scenes, manifests, build hashes and C0 receipts in an isolated graphics-capable shadow.
