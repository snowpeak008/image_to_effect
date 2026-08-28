# W24 S0a fixture adapter

## Delivered boundary

`Editor/W24/S0a/W24S0aFixtureAdapter.cs` consumes the generated
`s0a-operator-mutation-command/v1` files for `sustained_flame_3d`. It creates an in-memory Prefab
instance in the caller-provided fixture scene. It does not save, import, or
rewrite the sustained flame Prefab, preview scene, Recipe, design contract, or
any calibration labels.

The adapter's fixed allow-list parses all twelve generated `targetKey` values
and their string tokens into typed booleans, floats, and closed enums. Unknown
keys, illegal values, extra command fields, multiple mutations, path traversal,
non-positive seeds, non-canonical command hashes, and already-applied commands
are rejected.

All on-disk fixture output is under the single project-local root
`Library/VFXComposer/W24S0aCalibration/<sampleId>/`. Candidate directories are
write-once. Their ledger is an append-only sequence of `CreateNew` JSON
records, made read-only immediately after each write. Cleanup is idempotent:
it aborts an unfinished recorder, destroys only the in-memory clone, preserves
partial evidence for recovery, and re-checks the hashes of the formal Prefab,
scene, Recipe, and contract.

The ledger is now a canonical SHA-256 chain: every entry stores its predecessor
hash and its own hash, and the adapter verifies the chain before and after each
append. A cleanup failure is attached to (rather than replacing) the original
fixture exception during construction, capture begin/frame observation, or
completion. The fixture scene must be valid and loaded; Prefab
instantiation targets that scene directly.

## Capture and invalid evidence

The only real capture entry points are `BeginActualCapture`,
`ObserveCompletedPlayerLoopFrame`, and `CompleteActualCapture`; they delegate
to `W24ContinuousCaptureRecorder`. The adapter does not invoke any preview
sampling, particle emission/simulation, or time-jump API. The recorder remains
responsible for graphics-backed batchmode and retained frames. The session
independently binds one loaded authority scene, exactly one serialized
`MainCamera`, the capture profile/source hashes, and a real `LateUpdate` token
consumed in natural PlayerLoop order.

`Capture.frameManifestIntegrity` is deliberately not visualized. It is queued
until recorder completion, then creates a separate, audited, sealed-derived
`invalid-evidence/` copy. `missing_key_frame` removes a copied Beauty frame;
`sha256_mismatch` alters only copied metadata. The original sealed `capture/`
directory is retained unchanged. Injection rejects an unsealed or missing
recorder lock/metadata directory. It verifies that the evidence lock binds the
metadata candidate/profile and that every metadata-declared Beauty and
diagnostic source artifact exists with the declared SHA-256 before deriving the
invalid copy. Completion is accepted only after a final artifact-index seal
binds every raw artifact, metadata, command/tool/source provenance, the ledger
tail, and (when applicable) the sealed derived-invalid manifest; merely
read-only forged metadata is rejected.

Visual mutations stay on the clone but now expose deterministic readback:
loop-reset alternates offset/origin at each natural loop boundary; particle
residual config sets the controlled steady/stop lifetimes and a non-preempting
cleanup deadline; light residual starts at the observed `Stopping` transition
and force-closes at its configured duration even if the controller has returned
to idle. Smoke sorting is relative to the clone's primary renderer, not a fixed
order number. Delayed ignition remains distinct from disabled ignition by
releasing and playing its existing burst after 0.42 seconds of normal updates.

## Verification performed

- The Python calibration suite includes fixed non-answer command-set manifest
  coverage in addition to fixture-generation and schema checks.
- The Python generator now exposes `operator_mutation_vocabulary()` and emits
  future commands with effect id `sustained_flame_3d`; the Unity adapter retains
  a narrow legacy bridge for the already-generated reduced fixtures.
- Added EditMode coverage for all 12 command keys, path escape rejection,
  formal-source hash verification, post-capture invalid-evidence audit,
  strong/boundary configuration readback, ledger tamper detection, forged-seal
  rejection, and idempotent cleanup gating.

## Not verified here

No Unity GUI was opened, closed, or terminated, and no Unity batch process was
started. The current workspace does not contain the built sustained-flame
Prefab/preview scene, so Unity compilation, clone construction, graphics
capture, and the formal-source hash test were not executed. No rendered mutant,
human label, QA result, visual verdict, S0a terminal status, L3 authority, or
L4 sign-off is claimed by this delivery.

## Batch formal-capture harness (source-only addition)

The formal PlayMode entry point is now
`VFXComposer.Tests.PlayMode.W24S0aFormal.W24S0aFormalCalibrationCaptureTests`.
It is `Explicit` and requires a graphics-backed Unity batch process; it is not
run by ordinary regression. It selects only the fixed repository cohorts:

- `docs/vfx-calibration/reduced/operator/mutation-commands/` — exactly 66;
- `docs/vfx-calibration/full/operator/mutation-commands/` — exactly 110 when
  that future corpus has been generated.

There is no caller-supplied directory, answer ledger, label file, or blind
payload input. The loader rejects every file outside those two direct command
directories, nested entries, links, unexpected count, a missing or altered
`command-set.json`, compound commands, and
non-`NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER` commands. Generated zero-mutation
commands are retained only as baseline pass controls; all non-control entries
have exactly one allow-listed mutation.

For each command the Explicit test loads the authority sustained-flame preview
scene, disables its in-memory source runtime instance, instantiates a fresh
Prefab clone into that same loaded scene, and captures the clone through the
serialized `MainCamera`. The clone runs only through normal yielded
PlayerLoop frames with the command's exact positive `UInt32` seed. It writes
write-once Beauty, effect-only diagnostic, separate semantic-telemetry JSON,
Capture Profile/source hashes, and metadata. Source Prefab/scene/Recipe/
Manifest/contract plus recorder/adapter tool hashes are checked before, during,
and after every clone.

The batch policy is all-or-nothing: no candidate directory means `Fresh`; a
fully sealed set means `Complete` only when its raw final seal, full lifecycle
ledger, command binding, and expected derived-invalid seal all verify; it is
then never recaptured. Foreign root entries, partial candidates, and unsealed
sets fail for manual recovery with their bytes preserved. Every candidate runs
all three profile seeds (canonical plus both robustness seeds). An
`evidence-invalid` command remains queued until capture sealing, then mutates
only a read-only derived `invalid-evidence/` copy.

Added static/edit tests cover fixed 66/110 cohort sizing, pass controls,
arbitrary-directory/label/ledger rejection, partial-batch non-resume, UInt32
seed serialization, telemetry write validation, final-seal/post-seal foreign
artifact rejection, existing invalid-evidence copy behavior, and cleanup/ledger
recovery paths. These are source assertions
only in this change: no formal cohort, frame, human label, blind review,
metric, `S0A_ADVISORY_ONLY`, or `S0A_GATE_QUALIFIED` result has been created.
