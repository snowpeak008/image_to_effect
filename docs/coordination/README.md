# W24 controller index

> **Status (2026-08-29) — U6 FINAL GO / A0–A6 CLOSED.** The W24 program is complete: U0–U6 and A0–A6 are closed and no W24 work package is active. This directory is retained as the W24 closeout and provenance record. Sequencing for all new work is owned solely by `docs/plans/OPTIMIZATION_MASTER_PLAN.md` (the post-closeout P0/R/O/F milestone series); day-to-day task acceptance follows `docs/plans/CODING_STANDARDS.md`, and per-task receipts are no longer required. The single-file architecture entry point is `docs/PROJECT_ARCHITECTURE_AND_DEVELOPMENT.md`.

This directory is the durable coordination memory for the standalone Desktop + Unity Worker + Broker program. Chat history is not an authority for scope, ownership, gates, or evidence.

Read in this order (each file's current top section governs; their pre-U0 ledgers are historical — superseded by U6/A6 closeout):

1. `W24_PROGRAM_CONTROL.md` — non-negotiable constraints, current phase, quota and scheduling rules.
2. `W24_WORK_PACKAGE_REGISTRY.md` — bounded work packages, file ownership, dependencies, gates and handoff contract.
3. `W24_EVIDENCE_INDEX.md` — accepted receipts, frozen document identities and reproducible source baselines.

Normative architecture remains in:

- `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md`
- `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md`
- `docs/stage-notes/W24_UNITY_UI_TO_DESKTOP_MIGRATION_MATRIX.md`

The final accepted routes are normatively defined in `docs/rules/ADR-005_USER_MODE_BROKER_WORKER_ARCHITECTURE.md` and `docs/rules/ADR-006_AI_PROVIDER_TWO_CHANNEL_ROUTING.md`, and summarized in `docs/PROJECT_ARCHITECTURE_AND_DEVELOPMENT.md`.

If these documents disagree, stop. The controller must resolve the inconsistency and obtain a read-only audit before publishing another writer package. *(Historical W24 rule; for current work, conflicts are resolved by `docs/plans/OPTIMIZATION_MASTER_PLAN.md` and its acceptance baseline.)*

