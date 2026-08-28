# W24 S0a calibration fixtures

This directory contains lightweight contracts and placeholders only. It has no
rendered mutants, captured frames, human-reviewed labels, blind-test result, or
S0a terminal status.

Generate the reduced 36 fail / 12 pass / 12 boundary-uncertain / 6
evidence-invalid fixture set:

```powershell
python tools/vfx/generate_s0a_mutants.py --cohort reduced --output docs/vfx-calibration/reduced
```

The output is deliberately split:

```text
reduced/
  blind/                         # the only directory delivered to blind QA
    blind-submission-manifest.json
    review-freeze-context.json
    evidence/<anonymous-id>.evidence.json
  operator/                      # never disclose to blind QA
    command-set.json              # fixed, non-answer command cohort for capture only
    generation-ledger.json
    mutation-commands/<anonymous-id>.mutation-command.json
    evidence-bundle.json
    calibration-labels.json
```

## Formal capture projection (after Unity capture completes)

The generator output above is an operator fixture and an unrendered blind
placeholder. It is **not** the reviewable S0a evidence set. After the Unity
fixture adapter has completed and sealed every candidate in the exact cohort,
run the independent projector once into a new directory named exactly
`artifacts`:

```powershell
python tools/vfx/project_s0a_capture.py `
  --capture-root path/to/completed-formal-cohort `
  --fixture-root docs/vfx-calibration/reduced `
  --output path/to/new-run/artifacts `
  --model-version-id immutable-visual-model-version
```

The projector validates the frozen operator ledger and direct command files,
the exact 66- or 110-sample candidate set, every lifecycle ledger/completion
identity, every capture seal and artifact hash, and the fixed three seeds by
eight retained Beauty/effect-only frames. It refuses foreign files, links or
reparse points, partial cohorts, ordinary missing frames, stale hashes, and
Capture Profile drift. Publication is staged and atomically renamed; an
existing `artifacts` directory is never overwritten.

The formal output is separated again:

```text
artifacts/
  blind/
    review-contract.json               # no answers; opaque requirement IDs
    blind-submission-manifest.json
    review-freeze-context.json
    evidence/<sample>.evidence.json
    frames/<sample>/seed_{0,1,2}/...   # all 3 x 8 fixed Beauty slots
  operator/
    capture-profile-policy.json        # cohort policy + per-sample instance hashes
    evidence-bundle.json               # sealed diagnostics and provenance
    evidence/<sample>/...              # effect-only + metadata + telemetry
    human-adjudication-worksheet.json  # intentionally blank, never a label authority
  projection-receipt.json              # PROJECTED_NOT_REVIEWED only
```

`captureProfilePolicyHash` binds the common policy after removing only the two
per-sample seed fields. Each sample separately carries its exact
`captureProfileInstanceHash`; a single cohort-wide instance hash is invalid.
Blind documents never contain `labelBlueprint`, mutation commands, error
class/strength, expected route, or the operator's baseline/metadata-integrity
names. The projector does not generate or freeze labels, run visual QA, emit
metrics, score the cohort, or claim either S0a terminal status.

An already projected directory can be checked without mutation:

```powershell
python tools/vfx/s0a_projection.py verify --artifacts path/to/run/artifacts
```

## Three isolated reviews and projected scoring

Formal projection-v1 artifacts are consumed directly; they are never weakened
into the older generated-fixture blind/context/bundle shapes. Each of three
independent reviewers writes one isolated directory containing `session.json`
and exactly one sealed report per sample. After all three sessions exist, build
the write-once, receipt-bound corpus:

New formal rich sessions use `s0a-visual-qa-session/2.0`. A reduced66 session
must carry only its exact `*-66` validation tokens; a fresh-full110 session
must carry only its exact `*-110` tokens. Swapped, mixed, duplicate, missing,
or extra check tokens fail both schema and scorer validation. Rich 1.0 is a
reduced66-only legacy read boundary, while the simple directory/v1 shape stays
explicit compatibility data and is never synthesized from a rejected rich
session.

```powershell
python tools/vfx/s0a_projected_scorer.py build-corpus `
  --artifacts path/to/run/artifacts `
  --session-dir path/to/session-1 `
  --session-dir path/to/session-2 `
  --session-dir path/to/session-3 `
  --model-version-id exact-frozen-model-version-id `
  --output path/to/new-review-corpus.json
```

Corpus construction is answer-free and does not read labels. Only after the
separate human adjudication sheet has been completed and frozen may projected
scoring run:

```powershell
python tools/vfx/s0a_projected_scorer.py score-projected `
  --artifacts path/to/run/artifacts `
  --labels path/to/projected-human-labels.frozen.json `
  --reviews path/to/new-review-corpus.json `
  --operator-ledger path/to/generation-ledger.json `
  --output path/to/new-metrics-v3.json
```

The formal entry verifies the projection receipt and loads manifest, context
and evidence bundle only from that verified root. The corpus binds the receipt,
contract, profile policy, per-sample opaque requirement/evidence assignments,
model identity and all three isolated sessions; manifest order, distinct
reviewer/session identities and cross-session sealed-report anti-replay are
enforced. Corpus/metrics outputs may not be placed inside a sealed projection
or session input root. `s0a-metrics/v3` additionally
binds the scorer source and corpus. Use `score-legacy` only for explicit legacy
fixture regression; projected and legacy protocol families cannot be mixed.

The blind manifest has only anonymous `sampleId`, evidence identity, and
contract/capture/context identities. It contains no Patch reference, mutation
command, error class, property, strength, kind, or expected route. Its order
is deterministically shuffled. The operator ledger retains the private random
ID salt, seed, and answer blueprint; it is not a label authority.

Each operator command is an `s0a-operator-mutation-command/v1` with a
`targetKey` and `value`. It is not a production Recipe Patch and its status is
`NOT_APPLIED_BY_UNITY_FIXTURE_ADAPTER`. A later Unity fixture adapter must
apply it and capture evidence before calling a sample a rendered mutant or
submitting it for blind review.

`operator/command-set.json` is a separate `s0a-operator-command-set/v1`
manifest. It contains only `(sampleId, commandHash)` pairs, the exact cohort
count, and a self-hash; its status is `FROZEN_FOR_CAPTURE`. The Unity capture
harness accepts only this manifest plus the matching direct command files. It
does not read `calibration-labels.json`, `blind/`, or `generation-ledger.json`.

`operator/calibration-labels.json` is a
`TEMPLATE_NOT_REVIEWED` document conforming to
`docs/skills/unity-vfx-visual-qa/calibration/calibration-labels.schema.json`.
It is deliberately not human authority. A human adjudicator must enter a
route, non-empty three-state per-requirement result, reviewer, observability,
metric eligibility, distinct captured evidence hash, label source, and verdict
version for every sample. `freeze-labels` fills only its top-level reviewer and
time; it never invents a verdict.

```powershell
python tools/vfx/s0a_calibration.py freeze-labels `
  --labels path/to/completed-labels.json `
  --reviewer reviewer-id `
  --frozen-at 2026-08-25T12:00:00Z `
  --output path/to/calibration-labels.frozen.json
```

All hashes use canonical compact UTF-8 JSON with sorted keys and the format
`sha256:<64 lowercase hex>`. Frozen labels must match a frozen one-to-one
evidence bundle; replaying one evidence hash across samples is rejected.

For the legacy generated-fixture workflow, before review the capture/operator workflow must freeze the context hashes
for the QA prompt, model, image-input strategy, three-state rules, aggregation
rules, VFX review schema, contract/build/capture/frame identities, and the
pre-C0 `sceneHash`, `prefabManifestHash`, and `captureToolHash`. The three
independent review sessions use sealed reports conforming to
`schemas/vfx-visual-review.schema.json`, are bound to that context plus the
blind manifest/evidence bundle, and must each include every sample exactly
once. The scorer rejects duplicate `(sampleId, sessionId)`, non-distinct
reviewer-session identities, missing/extra samples or requirements, illegal
labels, unsealed hashes, and anything other than exactly three sessions.

```powershell
python tools/vfx/calculate_s0a_calibration.py `
  --labels path/to/calibration-labels.frozen.json `
  --reviews path/to/three-sealed-isolated-sessions.json `
  --blind-manifest path/to/blind-submission-manifest.frozen.json `
  --evidence-bundle path/to/evidence-bundle.frozen.json `
  --review-context path/to/review-freeze-context.frozen.json `
  --operator-ledger path/to/generation-ledger.json `
  --output path/to/metrics.json
```

The scorer emits the project `s0a-metrics` schema: three-state
per-requirement matrices, five-route matrix, invalid-evidence recall, and
exactly-three-session agreement. Reduced results are always
`S0A_ADVISORY_ONLY`. `S0A_GATE_QUALIFIED` is possible only for the complete
110-sample 60/20/20/10 frozen corpus with no uncounted sample, no visual
false-pass, all thresholds, and exact route consistency. A full *expanded*
corpus additionally requires a frozen reduced metrics report with
`falsePassCount=0`, hash-bound in the review context; a prompt/model change
after reduced testing requires `--fresh-full-holdout` instead of reusing the
stimuli.

Frozen hashes detect drift, not a malicious local writer. Pin or sign the
operator-side ledger/context/bundle hashes outside this directory if stronger
authority than accidental-change detection is required. Generator regeneration
only replaces untouched templates and refuses applied commands, captured
evidence, frozen contexts/bundles, final labels, or foreign files. Frozen score
and label outputs are write-once.
