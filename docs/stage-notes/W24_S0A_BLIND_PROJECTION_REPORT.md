# W24 S0a raw-capture → blind-evidence projection report

Date: 2026-08-25  
Implementation state: **projection infrastructure verified; no visual review or S0a terminal status claimed**

## 1. Delivered boundary

This stage supplies the missing deterministic bridge between the Unity fixture
adapter's completed, sealed formal cohort and the files that may be handed to
blind visual reviewers. It is intentionally downstream of Unity capture and
upstream of human labels, three isolated QA sessions, scoring, and all S0a
status decisions.

The projector:

1. validates the exact reduced (66) or full (110) frozen operator cohort,
   direct command hashes, lifecycle-ledger tails, candidate completions,
   capture seals, raw artifact hashes, and derived invalid-evidence identity;
2. accepts the formal recorder 1.1.3 frame contract exactly: Beauty
   `{file, sha256}` and effect-only diagnostic
   `{passId, file, sha256, foregroundPixels, method}`;
3. requires every referenced PNG to have a valid signature/IHDR CRC, exact
   960×540 dimensions, a verified complete PNG chunk stream, and a successful
   full Pillow pixel decode;
4. binds one common Capture Profile policy hash while retaining a distinct
   `captureProfileInstanceHash` for every sample;
5. projects every fixed seed/frame slot (three seeds × eight retained frames),
   without best-frame selection or cherry-picking;
6. keeps mutation commands, `labelBlueprint`, error class/strength and expected
   routes operator-only, and assigns opaque per-sample visual requirement IDs;
7. atomically publishes a new directory named `artifacts`, never overwriting an
   existing artifact set; and
8. emits only a no-answer review contract, blind evidence/manifest, frozen
   review-input context, operator diagnostic bundle, blank human worksheet, and
   a `PROJECTED_NOT_REVIEWED` receipt.

The projector never creates or freezes labels, creates QA reports, emits
metrics, runs the scorer, or asserts `S0A_ADVISORY_ONLY` /
`S0A_GATE_QUALIFIED`.

## 2. Files and schema versions

Implementation:

- `tools/vfx/s0a_projection.py` — `w24-s0a-blind-projection/1.2.0`
- `tools/vfx/project_s0a_capture.py` — projection-only compatibility CLI
- `tools/vfx/tests/test_s0a_projection.py`

Strict Draft 2020-12 contracts (seven payload contracts at `v1`, receipt at
`s0a-projection-receipt/v3`):

- `s0a-capture-profile-policy`
- `s0a-no-answer-review-contract`
- `s0a-projected-blind-evidence`
- `s0a-projected-blind-manifest`
- `s0a-projected-review-freeze-context`
- `s0a-projected-operator-evidence-bundle`
- `s0a-human-adjudication-worksheet`
- `s0a-projection-receipt`

The receipt's `s0a-projection-tool-sources/v2` identity contains exactly 11
source records: all three Python projection/calibration sources and all eight
projection schemas. It also binds the runtime dependency
`Pillow 12.2.0`; any source, schema, or dependency-version drift makes
verification fail.

Documentation:

- `docs/vfx-calibration/README.md`
- `docs/skills/unity-vfx-visual-qa/calibration/README.md`
- this report

## 3. Verification

The focused Python suite contains 19 tests and uses a complete synthetic
reduced cohort for positive and negative paths. It covers deterministic
projection, strict schema validation, partial cohort rejection, foreign entry
rejection, root and nested link/reparse rejection, missing ordinary frame and
non-target derived-file tamper rejection, exact invalid-command/derivation
mapping, raw hash mismatch, trusted recorder identity, projection source and
schema drift, strict JSON duplicate/non-finite/surrogate rejection, reduced
metrics verification, Capture Profile policy mismatch, blind assignment/route
leak rejection, and truncated/wrong-dimension PNG rejection.

Latest full-suite command:

```powershell
python -m unittest tools.vfx.tests.test_s0a_projection -q
```

Result: **19/19 passed** (`Ran 19 tests in 164.868s`). The existing S0a
generator/scorer regression suite also passed **17/17** (`Ran 17 tests in
2.392s`), for **36 relevant Python tests** passing in this stage.

The projection suite must pass before the formal capture root is projected. No
Unity process was launched and no live/shadow capture directory or existing
capture output was read or changed during this implementation stage.

## 4. Current operational state

The reduced-66 raw capture subsequently completed and was projected into its
write-once no-answer artifact set. Three advisory-only review sessions and the
isolated review corpus exist for that reduced projection, but no frozen human
labels, calibration metrics, score, S0a terminal status, L3, L4, or user
verdict has been created.

The independent fresh-full110 raw capture is now sealed at
`artifacts/vfx-calibration/s0a-full110-raw-20260825T145902Z`. Its receipt is
`w24-s0a-full110-raw-capture-receipt/v1`, reports 110/110 completed candidates,
7,783 read-only files, zero reparse entries, and status
`FORMAL_RAW_CAPTURE_SEALED_NOT_PROJECTED`. A read-only audit replayed all 110
completion/seal/profile/telemetry records and found no raw-capture or source
identity drift.

The full110 projection is intentionally blocked because no exact immutable QA
`model-version-id` has been supplied. The projector requires that value and
freezes its hash into the review context; it must not be guessed or reconstructed
from another cohort. Consequently the full110 projection receipt, QA sessions,
corpus, metrics, and score remain absent. Once the model identity is supplied,
the next legal operation is a new write-once formal projection of this already
sealed cohort; a live or partial capture root remains inadmissible.
