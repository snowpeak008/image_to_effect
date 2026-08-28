---
name: unity-vfx-design-director
description: Turn a Unity VFX request and its references into a versioned, auditable design contract before implementation; use for new or materially revised VFX design intent, not for modifying Unity assets.
---

# Unity VFX Design Director

Create or revise a `VfxDesignContract`; do not create, edit, or sign off Unity assets. This project-local skill is the source of truth and must not be installed as a personal Codex skill.

## Required outcome

Return exactly one outcome: `DESIGN_READY`, `NEEDS_USER_DECISION`, `UNSUPPORTED_CARRIER`, or `REFERENCE_CONFLICT`.

- Use `DESIGN_READY` only when all ten W24 S1 segments validate, every required requirement has a unique `designRequirementId`, type, `evidenceAuthority`, and evidence location, and `effectId` is lower_snake_case (for example `sustained_flame_3d`).
- Stop with `NEEDS_USER_DECISION` when a choice changes visual semantics, a reference role, a prohibited substitution, or a required carrier. Do not invent a default.
- A reference states what it informs (composition, colour, material, motion, or atmosphere), its weight, and what it explicitly does not require copying. Conflicting required roles are `REFERENCE_CONFLICT`.

## Contract rules

Use [the W24 S1 contract schema](schemas/vfx-design-contract.schema.json) as the single machine-checkable authority for sustained, one-shot, and event-driven effects. Read [contract authoring](references/contract-authoring.md) before writing or revising a contract; read [carrier and substitution rules](references/carrier-and-substitution-rules.md) when selecting a carrier or stating a prohibition.

When changing the schema, use [the accepted S0 example](schemas/examples/s0-contract.valid.json) and [negative cases](schemas/examples/negative-cases.json) as regression fixtures; they demonstrate a compliant lower_snake_case EffectId and the minimum rejected omissions/misroutes.

For every concrete W24 contract, run `python scripts/validate_s0_contract.py <contract.json>`. The historical filename is retained for compatibility, but the script is the full S1 validator: it enforces the common schema, canonical content hash, seed comparison, and pre-C0 capture-identity state. A `PENDING_FIRST_FORMAL_BUILD` contract must use `pending:formal-build` for both scene and manifest identities and cannot enter capture; `FROZEN_PRE_C0` requires real content hashes.

- A requirement has one authority only: `telemetry`, `diagnostic`, `visualQa`, or `user`. Split mixed internal and image facts into separate IDs instead of sharing an authority.
- A visual verdict description must identify the subject/layer, the expected relationship or event, the state and frame interval, and an ROI or layer mask. Words such as “beautiful”, “polished”, “good”, or “matches the vibe” are not verdict criteria without those anchors.
- Freeze `contractRevision` on C0. Before C0 only, implementation may provide the formal scene, `BuildManifest.buildHash`, and capture-tool source identity so Design Director can create the final pre-C0 revision without changing design semantics. After C0, any changed identity or required item requires a new revision and a new C0; it is not a C1/C2 repair.
- The contract records all lifecycle entry/deadline/completion conditions; spatial origin/orientation/formal anchor and design/game-distance dimensions; the full Capture Profile identity; semantic-state exits/transitions; layer geometry/material/colour/blend/motion/timing/attachment/continuity/budget; cleanup; and canonical plus exactly two distinct robustness seeds. The canonical seed must differ from both robustness seeds; validate that cross-field rule before handoff. Do not use an unspecified general-purpose carrier as a substitute.

## Handoff

For `DESIGN_READY`, hand the immutable contract hash to the implementer and identify each requirement’s authority. The implementer maps IDs to objects; the independent QA reviewer assesses images only and never replaces telemetry, diagnostic, or user authority.
