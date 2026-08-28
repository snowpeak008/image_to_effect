# S0a calibration protocol

This directory defines the calibration artifacts; it contains no approved labels, holdout results, or calibrated status. Do not report any S0a terminal state until the frozen blind-test artifacts and metrics exist.

## Separation and freeze

Use parameterized Patch scripts to generate controlled mutants; each sample injects one explicit error, uses a fixed seed, and receives an anonymous random ID whose filename does not disclose the answer. The training calibration set may improve the QA prompt. Generate the blind holdout separately and never use it to tune the prompt.

Before blind review, freeze and hash: QA prompt/version, model version, image-input strategy, contract version, filmstrip layout/resolution/frame table, three-state rules, top-level aggregation rules, and `calibration-labels.json`. The labels are a human-reviewed, frozen, hash-addressed authority. Patch parameters are only initial traceable labels.

Use [the label schema](calibration-labels.schema.json) for the frozen manifest and [the S0a metrics schema](s0a-metrics.schema.json) for terminal reporting. Evidence and labels are append-only/write-once; a changed label set requires a new manifest version and hash, never an in-place edit. All hashes are `sha256:` plus 64 lowercase hexadecimal characters; canonical JSON is UTF-8, sorted keys, and compact separators, with the document's own hash field omitted.

The raw-capture projector uses these eight projection schemas in this directory:

- `s0a-capture-profile-policy`: one cohort policy hash plus one exact Capture
  Profile instance hash per sample;
- `s0a-blind-review-contract`, `s0a-projected-blind-evidence`, and
  `s0a-projected-blind-manifest`: a no-answer, opaque-assignment blind surface;
- `s0a-projected-review-freeze-context` and
  `s0a-projected-operator-evidence-bundle`: review-input and operator-only
  provenance identities;
- `s0a-human-adjudication-worksheet`: a strictly blank handoff worksheet;
- `s0a-projection-receipt`: a self-sealed `PROJECTED_NOT_REVIEWED` receipt whose
  five authority assertions must all remain false.

The current receipt is `s0a-projection-receipt/v3` and the projector is
`w24-s0a-blind-projection/1.2.0`. Its canonical projection-tool identity binds
all eight schemas above, the three Python projection/calibration sources, and
the frozen runtime dependency `Pillow 12.2.0`. Projection and later
verification both recompute that identity. Every retained Beauty and
effect-only PNG is independently required to have a valid PNG signature and
IHDR CRC, dimensions exactly 960×540, a complete verified chunk stream, and a
successful full Pillow pixel decode before it can appear in either the blind
or operator evidence tree.

Every cohort-bearing schema fixes its array length to 66 for the reduced
cohort or 110 for the full cohort. Every blind sample declares the complete
three-by-eight frame table. These projected artifacts precede human
adjudication: they are not compatible substitutes for frozen
`calibration-labels/v2`, three sealed QA sessions, or `s0a-metrics`.

New rich Visual QA writers must emit `s0a-visual-qa-session/2.0`. Its six
frozen `validationChecks` are cohort-exact: reduced66 uses only the four
counted `*-66` tokens plus `session-hash-valid` and the fixed 3x8 review token;
fresh-full110 uses the corresponding four `*-110` tokens plus those same two
common tokens. The schema and corpus builder both reject a 66/110 token swap,
a mixed set, duplicate, missing, extra, or prose replacement. Legacy
`s0a-visual-qa-session/1.0` remains readable only for the already-defined
reduced66 boundary. The explicit simple directory/v1 protocol remains a
separately identified compatibility shape; it is never an automatic downgrade
for a malformed rich session and is not the writer target for new formal rich
reviews.

`calibration-labels/v2` has exactly these top-level fields: `schemaVersion`, `holdoutCohort`, `reviewStatus`, `frozen`, `reviewer`, `frozenAt`, `samples`, and `manifestHash`. A template is `TEMPLATE_NOT_REVIEWED` plus `frozen: false`; its human adjudication fields are null and its source is `PENDING_HUMAN_ADJUDICATION`. A final manifest is `HUMAN_REVIEWED` plus `frozen: true`, a non-empty reviewer and ISO timestamp, and per-sample human values. Every sample has `sampleId`, `requirementId`, `groundTruthRoute`, non-empty `perRequirement[]`, `labelSource`, `reviewer`, `visuallyObservable`, `eligibleForVisualMetrics`, `isBoundary`, `evidenceHash`, and `verdictVersion`; optional `adjudicationNotes` is retained. The scorer, not JSON Schema, checks that every `perRequirement[].requirementId` equals its sample `requirementId` and that the manifest hash matches its content.

The metrics report has exactly `holdoutCohort`, `labelManifestHash`, `perRequirementMetrics`, `topRouteMetrics`, `stability`, `terminalStatus`, and `reportHash`. Its nested required values preserve the scorer output: false-pass, false-fail, and uncertain metrics plus per-requirement matrices and qualification preconditions; evidence-invalid recall/counts and five-route confusion matrix; three isolated-session agreement values. `S0A_GATE_QUALIFIED` additionally requires the exact full 60/20/20/10 cohort and all stated thresholds; all other completed blind outcomes are `S0A_ADVISORY_ONLY`.

## Cohorts, measurements, and the only terminal states

Reduced blind holdout: 36 visual-fail, 12 pass, 12 visual-boundary, and 6 evidence-invalid samples. Any false-pass in its known visual-fail set stops expansion; repair the QA protocol before a new blind sequence. Zero reduced false-passes permits only `S0A_ADVISORY_ONLY`, never L3 gate authority.

Full blind holdout: 60 visual-fail, 20 pass, 20 visual-boundary, and 10 evidence-invalid samples. `S0A_GATE_QUALIFIED` is allowed only when all full-holdout thresholds pass: known visual fail false-pass = 0; known pass false-fail <= 10%; non-boundary visual uncertain <= 15%; evidence-invalid recall = 100%; and three fresh isolated reviews of the same evidence have both per-requirement and top-route agreement >= 90%.

`S0A_ADVISORY_ONLY` is the only other S0a terminal state: blind testing completed but the cohort is reduced or a full threshold is unmet. QA remains mandatory but non-blocking, cannot create ordinary L3, and S0b requires explicit user authorization through the marked user-upgrade path. These are the only two S0a terminal states; do not invent an intermediate “qualified enough” label.

Exclude injected defects that are not visible in the final frame set from visual fail counts; preserve them as behavioral/structural cases. User review must cover at least one strong fail and one boundary sample per error class, plus every disputed label.
