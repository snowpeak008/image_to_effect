# Review protocol

## Per-requirement state: exactly three values

Each required visual requirement receives exactly one of these states:

| State | Meaning |
|---|---|
| `pass` | Valid captured evidence at the contract location shows the stated condition. |
| `fail` | Valid captured evidence at the contract location shows the stated condition is not met. |
| `uncertain` | Valid evidence and an unambiguous contract remain, but the visible result cannot be reliably distinguished. |

`uncertain` is not a synonym for missing frames, weak resolution, mismatched hashes, or multiple reasonable contract readings. Those are top-level routing failures below. Record affected items as `uncertain` for completeness with `countable: false`; do not fold them into visual confusion-matrix counts.

Per-requirement metrics count only `pass`, `fail`, and true visual `uncertain`; they never count a top-level route. For S0a, invisible injected faults are excluded from the visual confusion matrix and become behavioral/structural samples instead.

## Top-level route: exactly five values

Apply this ordered decision:

1. `EVIDENCE_INVALID` — image input is missing/insufficient, a key region is obscured or cropped, frames are missing or out of order, or metadata/hash/Capture Profile does not match. It takes precedence because no visual judgment is reliable.
2. `CONTRACT_AMBIGUOUS` — evidence is valid, but a visual statement has multiple reasonable readings, references conflict, or the specified evidence location cannot observe the requirement.
3. `VISUAL_FAIL` — no earlier route applies and one or more required visual requirements fail.
4. `VISUAL_UNCERTAIN` — no earlier route applies and one or more required visual requirements are truly visually uncertain.
5. `VISUAL_PASS` — no earlier route applies and every required visual requirement passes.

Top-level routes are counted separately from per-requirement states. `EVIDENCE_INVALID` has its own S0a recall measure; `CONTRACT_AMBIGUOUS` has no first-round S0a metric unless dedicated samples are made and declared.

## Candidate loop and user escalation

- Machine failure bypasses Visual QA and advances to the next candidate.
- `VISUAL_FAIL` advances from C0 to C1 or C1 to C2. Each candidate rechecks all required items and carries a per-requirement regression diff.
- `EVIDENCE_INVALID` permits one re-capture without consuming a candidate. A second failure becomes `CAPTURE_BLOCKED`, then workflow-level `NEEDS_USER_DECISION`.
- `CONTRACT_AMBIGUOUS` returns to Design Director with a new `contractRevision`. A second consecutive reopening for the same effect needs user confirmation.
- `VISUAL_UNCERTAIN` goes to the marked user-upgrade path.
- C2 failure or machine failure becomes workflow-level `NEEDS_USER_DECISION` or redesign.

The marked user-upgrade path is also mandatory for advisory-mode candidates and calibration-label disputes. It visibly states the source, terminal status (or its absence), and gate authority. A `VISUAL_PASS` route from advisory review remains a non-gating visual finding, never an ordinary QA gate pass. The user alone grants L4, bound to `contractRevision + buildHash + captureProfile`.

## Required report fields

Use [the review schema](schemas/vfx-visual-review.schema.json). A report includes candidate identity/hashes, image-input confirmation, evidence and contract validity, top-level route, every visual requirement’s `perRequirement` state and evidence location, conflict notes, S0a terminal status (or `null` while incomplete), gate authority, and a sealed report hash. The schema forbids `VISUAL_PASS` when any required item is `fail` or `uncertain`; a failure names the frame and image region and may not rely on an unanchored aesthetic label.
