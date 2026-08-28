# W24 S3 graphics capture producer

Date: 2026-08-25  
Status: `w24-s3-capture/3.5` executed successfully in the isolated shadow; machine evidence is
sealed and `FORMAL_EVIDENCE_BOUND`, while Visual QA and user authority remain pending. The
superseded 3.4 Contract/test treated the neutral Lit material's importer-managed `_EMISSION`
keyword as a functional invariant and was rejected before this fresh rebuild.

## Scope

`W24S3GraphicsCaptureEvidenceTests` is the formal, explicit C0 producer for the three S3
vertical baselines:

1. `w24_moving_projectile_trail`
2. `w24_weapon_socket_fragments`
3. `w24_real_light_receivers`

Each case loads its own serialized preview scene, requires one `MainCamera`, and drives the
same serialized `W24S3PreviewDriver` used by normal preview playback. It records all three
frozen seeds and retained frames `1, 18, 48, 72, 96, 120` only after real `LateUpdate`, using
an opaque recorder token; every non-retained natural frame is acknowledged as well.

## Evidence produced

The three revised Contracts freeze one exact `extensions.typedDiagnostics.requiredEvidenceMatrix`.
The producer reads that matrix before capture, refuses an extra or absent raw row in either
direction, and copies it plus its canonical hash unchanged into the recorder-owned metrics input.
The final planned raw count is **42**: B `9` + C `21` + D `12`. The final planned measured check
count is **33**: B corridor `9` + C multiview `15` + C fragment cross-evidence `3` + D receiver
luminance `6`.

- Recorder Beauty and effect-only diagnostic frames from the same serialized authority camera.
- A write-once semantic telemetry JSON for all three seeds.
- Projectile: every seed emits retained-travel `trail-only-mask` R8/`|u1` NPY captures. The
  rendered subject is the authored TrailRenderer, while the frozen corridor input comes only
  from `W24S3RuntimeEntry.ReadEmitterHistory()` world samples.
- Binding: every seed emits frozen front and oblique `object-id` R32_UInt/`<u4` and
  `depth-linear` R32_SFloat/`<f4` NPY captures for IDs `10,101,201,202,203` at frame 72. It
  also emits front-view `fragment-id` R32_UInt/`<u4` at frames 54, 63 and 72. The frame-72
  fragment pass has its own path/ID and pass declaration (it may reuse the front Object-ID bytes)
  and records that front Object-ID artifact as `derivedFrom`; it cannot impersonate the
  multiview `object-id` pass. `fragment_tracks` consumes IDs 201–203 and rejects a single rigid
  body as diagnostic cross-evidence for telemetry-authoritative
  `REQ-C-FRAGMENT-INDEPENDENCE`. The same seed's semantic telemetry contains the exact four
  missing-binding probes.
- Real light: every seed emits receiver off/on fixed-exposure linear-LDR float NPY, receiver-ID
  R32_UInt NPY, and an explicit effect-mask R8 NPY. All Runtime Entry renderers are hidden for
  off/on and only actual `UnityEngine.Light.enabled` changes.
- All raw NPY diagnostics use the recorder's typed observed API, including the same natural
  `LateUpdate` token's seed, logical frame, serial, frame, time, pass ID, encoding, view ID and
  non-empty `derivedFrom` provenance. The registry mirrors every raw artifact exactly. A
  recorder-owned metrics input/report DAG binds Contract revision/hash, capture profile, canonical
  bundle path/hash, expected metrics-tool hash and its frozen Python environment; Trace authority
  binds the sealed `metrics-report` with the exact per-requirement `metricCheckId` set. The
  generic capture summary remains supplemental only.
- Formal metrics require `W24_METRICS_PYTHON` to be an absolute existing executable. The producer
  probes it through `W24MetricsEvidenceDag.ProbeMetricsEnvironmentForInput`, rejects any mismatch
  against the Contract-frozen executable/hash/version/NumPy/Pillow identity, and never falls back
  to PATH resolution. Telemetry-only and pending requirements use a sealed Beauty frame as their
  independent cross-evidence; passed diagnostic trace entries are reserved for exact frozen
  `metrics-report` checks.
- A machine-only completed implementation trace, then the existing S5
  `W24S5RecorderCaptureCompletion` command for the matching effect.

The producer never fabricates Visual QA or user authority. Those records are explicit pending
evidence and remain `VISUAL_PENDING`; no L3 or L4 outcome is created here.

## Diagnostic isolation bridge

S3 authoring now puts only Runtime Entry hierarchies on `TransparentFX` (layer 1). Preview model
and receiver objects stay on Default. The recorder therefore emits an actual effect-only mask
without mixing in light receivers or the attachment model.

`W24S3PreviewDriver.RestartForFormalCapture(uint)` is a bounded capture bridge: it resets the
entry, applies the frozen seed before `Initialize`/`Play`, then uses the same driver
`Begin`/`Update` event sequence as normal preview playback.

The common recorder seals formal evidence only when every observed `LateUpdate` token has been
consumed. Its capture metadata records observed and consumed serials. Beauty/effect-only capture
surfaces are `ARGB32` with `RenderTextureReadWrite.Linear` and a linear readback texture, matching
the fixed-exposure linear-LDR contract wording.

## Intended execution

Run only in an isolated shadow Unity project after S3 first-formal authoring has created the
three C0 candidate directories, with a graphics device:

```powershell
$env:W24_METRICS_PYTHON = 'C:\Program Files\Python312\python.exe'
& .\tools\Invoke-Unity.ps1 -Mode PlayMode -UseGraphics -TestFilter "VFXComposer.Tests.PlayMode.W24S3GraphicsCaptureEvidenceTests"
```

Do not add `-nographics`. The operation is write-once under
`artifacts/vfx-evidence/<effectId>/C0`; a second invocation intentionally ignores an existing
evidence directory rather than overwriting it.

## Executed machine gate and remaining authority

The producer was executed after a fresh compiler-2.7 build in the isolated shadow. Contract tests
passed `19/19`, runtime tests `6/6`, the formal precondition gate `1/1`, and the three graphics
methods passed separately at `1/1` each. The sealed outputs contain B `9`, C `21`, and D `12`
typed raw diagnostics, one frozen metrics input and one measured report per effect, with no missed
PlayerLoop token. S5 accepted all three transitions as `FORMAL_EVIDENCE_BOUND`; a missing matrix
row/view/pass, failed metric, wrong tool environment, or missing trace binding would have rejected
the transition rather than falling back to a summary.

These are machine results, not visual approval. Visual QA and the user's final L4 decision remain
outstanding for every baseline, and the isolated artifacts have not been promoted over the
canonical project.

## Frozen source identity

The finalized 31-source `w24-s3-capture/3.5` bundle hash is
`sha256:f605aacf4d27128347a2a8434a29cebc95aa789af73df55cc26c6fd7a0e42726`. It includes the
current `W24S3BaselineAuthoring.cs` bytes at
`sha256:27749de98934cadd6d679b7d6d7b64da2521c89f9dd2a6393376ccef055d7d35`
(`CompilerVersion = w24-s3-baseline/2.7`) and the current real-light dependency-injection
source. The re-signed bootstrap Contract revisions/hashes are B r26
`sha256:93fce950b2bbea9de590bdbc9594f43bce5894ebf4ccbacafbe370af8d279b58`, C r25
`sha256:88c752a126d0c0922861af647158b884dfb9095b173e573d3279239ca0f3dc4d`, and D r24
`sha256:0a7b6c6715834f17c5b4864346c015cc3b96bbd32b5be734d3b2b6b38e959859`.
The three bootstrap traces bind those exact revisions/hashes bidirectionally. The immutable
bootstrap records remain source identities; separate C0/evidence records now prove the repaired
formal build and 3.5 machine capture. They still do not prove visual acceptance.
