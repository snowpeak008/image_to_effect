# W24 S0a projected review/scorer compatibility report

Status: implementation complete; no S0a terminal claim  
Implementation date: 2026-08-25  
Rich-session v2 repair date: 2026-08-26  
Projected scorer: `w24-s0a-projected-scorer/1.1.0`  
Current scorer source hash: `sha256:ef0fb8f4691c0b8061f2919d3a8ba5d2d3f3017553aede3362a90e139156b930`  
Session schema resource: `https://image-to-smart.local/schemas/s0a-isolated-review-session/v3`  
Corpus schema: `s0a-isolated-review-corpus/v3`  
Metrics schema: `s0a-metrics/v3`

## 1. Boundary implemented

`tools/vfx/s0a_projected_scorer.py` is the strict compatibility boundary between the sealed projection-v1 artifacts and the calibration scorer. It does not reshape projected documents into the weaker legacy blind/context/bundle contracts.

The formal projected-score entry accepts one verified projection artifact root. It calls `verify_projection(root)` and loads the canonical manifest, review context and operator evidence bundle from that root. A caller cannot independently supply replacements for those three documents.

The existing legacy scorer and its fixtures remain unchanged. Legacy use is explicit through `score-legacy`; projected use is explicit through `score-projected`. Automatic protocol coercion and mixed-family inputs are rejected.

## 2. Projected review bindings

For every review report:

- `contractHash` must equal `reviewContext.reviewContractHash`;
- `buildHash` must equal `reviewContext.buildHash`;
- `captureProfileHash` must equal `reviewContext.captureProfilePolicyHash`;
- `evidenceHashes` must be exactly the one projected sample `evidenceHash`;
- `perRequirement` must contain the exact opaque assignment from the blind manifest;
- frame references are restricted to the frozen eight-frame table `1, 21, 60, 120, 180, 240, 300, 360`;
- `s0aTerminalStatus` remains `null` and `qaGateAuthority` remains `advisory-only` before scoring.

The projected operator bundle is cross-checked against the blind manifest for sample identity, evidence identity and per-sample capture-profile instance. Its diagnostic frame inventory must be the exact three-seed by eight-frame matrix.

## 3. Isolated review corpus

`build-corpus` accepts exactly three distinct session directories. Each directory must contain only:

```text
session.json
reports/<sampleId>.report.json
# or, for one whole session, reports/<sampleId>.review.json
```

One session must use exactly one suffix throughout; mixed suffixes or foreign files are rejected. Each session must be `isolated=true`, use unique session/reviewer/hash identities, contain the exact cohort once, and reproduce each external report as the same strict-JSON document in the embedded session list, with its canonical sealed-report hash verified. The simple session protocol additionally binds frozen manifest order and increasing RFC3339 timestamps; the rich protocol instead binds its explicit 3×8 slot inventory, route counts, context headers and validation attestations. New rich writers emit `s0a-visual-qa-session/2.0`; its reduced66 and fresh-full110 check tokens are separate exact sets. Rich 1.0 is accepted only at the historical reduced66 boundary. A cohort-token swap, mixed/duplicate/missing/extra set, or malformed rich document is rejected rather than converted to simple. `sessionProtocols[]` records the exact source shape and hash, so mixed authorized source shapes are explicit rather than silently downgraded. Reusing one sealed sample report across sessions is rejected. The write-once corpus binds:

- projection receipt hash;
- blind manifest hash;
- operator evidence bundle hash;
- review context, review contract and capture-profile policy hashes;
- frozen model-version hash;
- all three session hashes and reports.

## 4. Labels and scoring

Labels remain a separate, human-authority artifact. Corpus construction never reads labels. At score time the validator requires the frozen labels, projected samples and operator ledger to have the same sample set. Each label must bind the projected evidence hash and opaque design-requirement assignment. A non-null invalid-evidence derivation is required if and only if the human ground-truth route is `EVIDENCE_INVALID`.

`s0a-metrics/v3` retains the v2 arithmetic and threshold validation, then adds:

- `scorerVersion` and current scorer-source hash;
- verified projection receipt hash;
- review corpus hash.

The scorer source identity covers the projected scorer, unchanged legacy arithmetic implementation and metrics schema, plus the four new projected report/session/corpus/metrics schemas. Source or schema drift invalidates an old v3 report rather than silently reusing it.

## 5. CLI

Build the corpus after all three isolated sessions exist:

```powershell
python tools/vfx/s0a_projected_scorer.py build-corpus `
  --artifacts <projection-artifacts-root> `
  --session-dir <session-1> `
  --session-dir <session-2> `
  --session-dir <session-3> `
  --model-version-id <exact-frozen-model-version-id> `
  --output <new-review-corpus.json>
```

Score projected evidence only after labels have been separately adjudicated and frozen:

```powershell
python tools/vfx/s0a_projected_scorer.py score-projected `
  --artifacts <projection-artifacts-root> `
  --labels <frozen-human-labels.json> `
  --reviews <review-corpus.json> `
  --operator-ledger <sealed-generation-ledger.json> `
  --output <new-metrics-v3.json>
```

The tool refuses to overwrite any output or place it inside a sealed projection/session input root, and rejects JSON duplicate keys, non-finite numbers, lone surrogates, parent traversal, symlinks, junctions and reparse points.

## 6. Migration and deliberate non-actions

- Legacy blind/context/bundle artifacts are not losslessly convertible because they lack projected opaque assignments, per-sample profile instances and derivation provenance. They must be re-projected from verified capture.
- Legacy reports may not be mixed into a projected corpus.
- Semantic requirement IDs may not be automatically rewritten to opaque IDs; projected labels must be materialized through human adjudication.
- Metrics v2 are not renamed or copied into v3; projected metrics must be recalculated.
- No labels, frozen verdicts, metrics, QA gate result or S0a terminal status were created by this implementation.
- No Unity or capture process was launched, and no live capture or existing projection artifact was modified.

## 7. Deferred full-cohort seam

The currently sealed projection tool source identity includes `s0a_calibration.py` and the projection code. Modifying that source set now would invalidate the active reduced projection receipt. Therefore the current reduced run is completed with the new scorer while the existing projector remains untouched.

Before an expanded-full projection is authorized, the projector must be explicitly version-bumped so its full-cohort precondition accepts and verifies bound `s0a-metrics/v3` provenance (`projectionReceiptHash` and `reviewCorpusHash`). Legacy v2 metrics must not authorize a projected full expansion. This is a versioned follow-up, not an implicit compatibility shortcut.

## 8. Verification

Current rich-session v2 source-only repair evidence (2026-08-26):

- Exact rich-session boundary tests: 5/5 passed. These cover reduced66 rich v2 plus legacy-v1 parsing, fresh-full110 rich v2, both cohort-token swaps, and mixed/duplicate/missing/extra token rejection. The full110 positive case also builds a three-rich-session corpus, verifies that all three `sessionProtocols[]` entries remain v2, and validates the complete emitted corpus against Draft 2020-12.
- Projected scorer unit/negative module: 26 discovered, 25 passed and one existing Windows symlink-creation test skipped because the process lacks symlink privilege; zero failures.
- Existing legacy calibration module: 17/17 passed.
- Draft 2020-12 meta-validation: session v3 resource, corpus v3 resource and projected metrics v3 resource passed.
- Python AST parsing: projected scorer and its test module passed.
- The repair did not execute formal projection, Unity, or any network operation, and did not open formal labels, the operator ledger, or mutation-answer data.

Earlier broad regression evidence retained from the preceding compatibility slice, not rerun by this repair:

- Existing projection suite: 19/19 passed in 166.278 seconds.
- Full `tools/vfx/tests` discovery: 71 discovered, 70 passed and the same Windows symlink-privilege test skipped; zero failures in 174.969 seconds.

## 9. Isolated-review execution note

- Session 1 was sealed with 66 reports and canonical session hash `sha256:c01737eda6e5e034f95572e562c814ec30ae154da8e760cb5660f9d23e7ddd07`; reports remained advisory-only with null terminal status.
- The first attempt at session 2 was rejected because PowerShell serialization collapsed one-item arrays, invalidating all report seals and the session hash. Its complete 67-file output was quarantined and never counted.
- Session 2 was rerun from the blind package using Python canonical JSON, validated in a temporary directory, and atomically published only after all 66 report seals, embedded-report equality and session hash passed. Its valid session hash is `sha256:677eba5bdfecbbb2b44456e07aa0aae46541b816b4b92f88ec141d67c2336459`.
- The first session-3 publication was subsequently rejected by the corpus validator: all 66 reports placed their per-sample `captureProfileInstanceHash` into the report-level `captureProfileHash` field instead of the frozen cohort `captureProfilePolicyHash`. It was quarantined unchanged under `rejected-qa-sessions/session-3-instance-hash-in-policy-field-20260825T091038Z`; no corpus was created from it. A fresh isolated retry was required rather than weakening field semantics or rewriting sealed reports.
- The fresh session-3 retry was independently resealed with all 66 reports bound to the frozen policy hash. Its valid session hash is `sha256:b1623e99c5a2c49c771600f80586f9754f968c8fdb6d74144a9e9cb126696e73`.
- After all three sessions passed the current scorer independently, the write-once corpus was atomically published at `artifacts/vfx-calibration/s0a-reduced66-projection-20260825T0815Z/qa-sessions/isolated-review-corpus.v3.json`. It contains three unique sessions and 198 sealed reports, has corpus hash `sha256:1c2f17e546ea3213015406a459d664a9cb81b83a17a9a899a8b450fd4a6098ed`, binds projection receipt `sha256:ae7f85172fb4fa988851a9f7414f5a6a3dc2b2e7c50bc2444240ccc59818a1f8`, and retains `advisory-only` authority with null terminal status throughout.
- Corpus construction and verification did not read or create human labels, metrics, a gate result or any S0a terminal status.
