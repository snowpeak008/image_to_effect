# W24 1.3 S0a: Skill and protocol delivery

## Delivered

- Project-source Design Director Skill: `docs/skills/unity-vfx-design-director/`.
- Project-source independent Visual QA Skill: `docs/skills/unity-vfx-visual-qa/`.
- S0 design-contract, Visual QA report, frozen-label, and S0a metric JSON Schemas.
- Positive and negative schema fixtures for the full S0 contract and Visual QA aggregation.
- Deterministic contract/report validators for canonical hash and seed invariants that JSON Schema alone cannot compare.

The rules encode per-requirement `pass / fail / uncertain`, five separate top routes, authority routing, immutable candidate/evidence hashing, fresh isolated QA sessions, write-once evidence, C0/C1/C2 handling, the user-upgrade path, and the two legal S0a terminal states.

The Design Contract schema now enforces all §7.9 segments: lower_snake_case `effectId`; lifecycle entry/deadline/completion rules; reference roles; origin/orientation/anchor and design/game-distance dimensions; frozen Capture Profile identity; canonical plus exactly two robustness seeds; semantic states/transitions/exits; complete layer properties; explicit substitutions and cleanup; budget; and per-requirement authority/evidence location. `sha256:<64 lowercase hex>` is the sole hash syntax.

Capture identity is one-way and frozen before C0: `sceneHash` is the formal scene content hash, `prefabManifestHash` is `BuildManifest.buildHash` (not a manifest-file byte hash), and `captureToolHash` covers frozen tool source/configuration. All exclude contract/evidence/report back-references. The final pre-C0 contract revision consumes those identities; later changes create a new revision and C0 rather than backfilling an existing candidate.

`calibration-labels/v2` is aligned with the calibration tool’s canonical header and sample fields, including the `TEMPLATE_NOT_REVIEWED`/`HUMAN_REVIEWED` and `frozen` conditional forms. The metrics schema exactly accepts the scorer’s seven top-level fields and its required nested metric structures. JSON Schema validates structure; the calibration scorer remains authoritative for canonical-content hashes and dynamic `perRequirement[].requirementId == requirementId` checks.

The Visual QA report schema uses `perRequirement` and rejects `VISUAL_PASS` when any item is `fail` or `uncertain`; it additionally binds visual routes to valid evidence and an unambiguous contract. A pass from `S0A_ADVISORY_ONLY` remains explicitly non-gating.

## Validation

- Both project-local Skills pass Skill Creator `quick_validate.py` under `PYTHONUTF8=1`.
- All four JSON Schemas pass Draft 2020-12 meta-validation.
- The Design Contract and sealed Visual QA report validators accept their canonical positive fixtures, including content-hash verification; the Design validator checks canonical seed differs from both robustness seeds.
- Positive/negative validation passes: complete S0 contract accepted; upper/hyphenated EffectId, missing spatial segment, and wrong visual authority rejected; valid QA report accepted and `VISUAL_PASS` containing `fail` rejected.
- Canonical generated template labels and an in-memory human-reviewed full-label/qualified-metric fixture validate against the new schemas; invalid frozen template and qualified report with a false-pass are rejected.

The in-memory full fixture is schema/scorer compatibility coverage only. It is not a blind test, calibration result, or gate qualification.

### Scorer alignment correction

The scorer and its report verifier now apply only the qualification metrics frozen in W24 1.3 §17: per-requirement false-pass, false-fail and non-boundary-uncertain thresholds; 100% `EVIDENCE_INVALID` recall; and the two three-session stability thresholds. The top-route confusion matrix remains reported, but it is not an additional all-routes-must-match gate. In particular, first-round `CONTRACT_AMBIGUOUS` has no accuracy metric. Tests prove that stable `VISUAL_UNCERTAIN → CONTRACT_AMBIGUOUS` route errors can coexist with a qualified full corpus, while every authorized threshold still degrades the result to `S0A_ADVISORY_ONLY` when violated.

This correction changes scorer policy and regression coverage only. It does not create a calibration result, assign either terminal status to real evidence, or grant L3 authority.

### Formal capture and no-label evidence status (2026-08-25)

The reduced 66-sample cohort has completed formal graphics capture and the
answer-free projection/review-corpus stages. Its projected corpus contains
exactly three isolated advisory sessions and 198 sealed reports. It still has
no frozen human adjudication labels, no metrics report, and no S0a terminal
status; the three machine-assisted reviews are not label authority.

A separate fresh full holdout was generated with `--cohort full
--fresh-full-holdout`. Its frozen command set contains exactly 110 unique
samples (60 fail / 20 pass / 20 uncertain / 10 invalid), uses the
`FRESH_INDEPENDENT_FULL` isolation mode, and has zero sample-ID overlap with
the reduced 66 cohort. It was captured in a dedicated graphics-backed shadow
whose formal capture root was empty at launch.

The true PlayMode proxy
`Capture_Full110_OperatorOnlyMutants_WhenTheFutureCohortExists` exited
naturally with code 0 and passed 1/1 in 2,137.5858883 seconds. The resulting
raw authority has 110/110 completed candidates, 7,783 read-only files, no
reparse entry, and an exact candidate-set match to the command set. The
independent projector verifier rechecked all 110 candidates, including the
PlayerLoop serial closure, three seeds by eight retained slots, artifact
hashes, raw/derived-invalid evidence, ledger order/tail, completion identity,
common Capture Profile, and source identity.

The write-once raw package is
`artifacts/vfx-calibration/s0a-full110-raw-20260825T145902Z`. Its receipt
self-hash is
`sha256:abfc2ac5b8ccca75e3143916da536b974eff24f85c39b349c522363ce564ca53`;
the capture tree hash is
`sha256:f3ad0f5fc6a81db3a3aaf52f009b8cf0dbbadb538256ea294daa6a0a205f800e`;
and the capture-tool identity remains
`sha256:7719219522abb7bf7c3e59b2da726cd4586776c7986f17e96743de52a8438245`.
The package preserves two superseded UPM ENOSPC preflight logs separately;
both ended before test discovery and created zero candidate directories.

The full raw cohort is deliberately **not projected yet**. Projection freezes
the exact Visual QA model-version identity, and no such immutable identity has
been supplied for this full cohort. Guessing one would corrupt the later
review-context binding. The raw receipt therefore says
`FORMAL_RAW_CAPTURE_SEALED_NOT_PROJECTED` and explicitly records Visual QA,
S0a Gate, L3, L4, and user signature as false/pending.

## Current truth

The reduced no-label review corpus and the fresh full raw capture are real,
hash-bound machine evidence. Neither is a frozen human-label set or a scored
calibration result. There is still no full-cohort QA model/prompt freeze, no
full projection, no human adjudication, no S0a metrics report, and no legal
S0a terminal status. Therefore this report claims no Visual QA calibration and
grants no ordinary L3 gate authority.

## Required downstream inputs

- Full calibration pipeline: an explicit immutable QA model-version identity,
  write-once projection of the archived full raw cohort, three isolated blind
  rich-session 2.0 reviews with the exact full110 validation-token set,
  completed human adjudication, frozen labels, and a scored full metrics report.
- Reduced pipeline: completed human adjudication and frozen labels before the
  existing three-session no-label corpus may be scored. Its outcome remains
  advisory-only by cohort size.
- User: reference-role and contract decisions where design is under-specified; explicit authorization to enter S0b if the eventual terminal status is `S0A_ADVISORY_ONLY`; final L4 signature.
