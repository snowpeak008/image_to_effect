# W24 controller index

This directory is the durable coordination memory for the standalone Desktop + Unity Worker + Broker program. Chat history is not an authority for scope, ownership, gates, or evidence.

Read in this order:

1. `W24_PROGRAM_CONTROL.md` — non-negotiable constraints, current phase, quota and scheduling rules.
2. `W24_WORK_PACKAGE_REGISTRY.md` — bounded work packages, file ownership, dependencies, gates and handoff contract.
3. `W24_EVIDENCE_INDEX.md` — accepted receipts, frozen document identities and reproducible source baselines.

Normative architecture remains in:

- `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md`
- `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md`
- `docs/stage-notes/W24_UNITY_UI_TO_DESKTOP_MIGRATION_MATRIX.md`

If these documents disagree, stop. The controller must resolve the inconsistency and obtain a read-only audit before publishing another writer package.

