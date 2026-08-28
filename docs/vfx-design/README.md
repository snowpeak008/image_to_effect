# W24 S1 design-to-implementation authority

This directory is the human-readable companion to the frozen Design Director Schema. The editor-side authority is `VfxDesignContractValidator` and `VfxImplementationTraceValidator`; the portable input authority remains `docs/skills/unity-vfx-design-director/schemas/vfx-design-contract.schema.json`.

Every Runtime Entry is classified as follows:

| Level | Permission |
|---|---|
| L0 | Invalid or missing; cannot be offered. |
| L1 | Functional protocol only. |
| L2 | Visual placeholder; the default while visual review is pending. |
| L3 | Only a `S0A_GATE_QUALIFIED` QA gate plus machine pass and `VISUAL_PASS` may create this candidate state. |
| L4 | Only the user can sign. The signature binds `contractRevision`, `buildHash`, and `captureProfileHash`; any visual rebuild invalidates it. |

Agents, validators, and QA reports may never produce L4. During the user's deferred-review instruction, new work remains `VISUAL_PENDING` / L2 until the final review batch.

Use the following in this order:

1. Select Small, Standard, or Hero from [templates](templates/).
2. Select real carriers from [carrier matrix](catalogs/visual-carrier-matrix.md) and a semantic motion pattern from [semantic pattern library](catalogs/semantic-pattern-library.md).
3. State every prohibited shortcut in [error substitution catalog](catalogs/error-substitution-catalog.md).
4. Produce both the Design Contract and the reciprocal Implementation Trace.
5. Use [machine and human report templates](reports/) without changing status language.
