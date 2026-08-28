# W24 Implementation Trace authorities

These files are independent from Design Contracts. A Design Contract may reference a Trace path through `extensions.implementationTrace`, but an Implementation Trace must never be embedded as a contract top-level field.

The sustained-flame S0b registration and the three S3 registrations currently have `traceStatus: PENDING_FIRST_FORMAL_BUILD_BINDING`. They are preregistrations: requirement IDs, authorities, intended Unity hierarchy/component mappings, state IDs, and layer IDs are reviewable before authoring. Their `buildHash`, `captureProfileHash`, Runtime Entry GUID, immutable object identities, seeds, and authority/cross-evidence records intentionally remain unbound. Therefore `VfxImplementationTraceJson` can parse them, while `VfxImplementationTraceValidator` must reject them as incomplete. They cannot authorize capture, L3, or L4.

The first formal build does **not** rewrite these preregistrations. S5 writes an immutable `PRE_C0_FIRST_FORMAL_BUILD` Manifest receipt, then creates a separate write-once `docs/vfx-candidates/<effectId>/C0/` Contract/Trace pair. That candidate freezes real scene/build/Runtime Entry GUID identities and remains `C0_CAPTURE_PENDING`; it contains no invented evidence and cannot enter the ordinary production gate.

After real capture, the evidence binder must write another immutable Trace with status `FORMAL_EVIDENCE_BOUND`, real object/component identities, all three seeds, and authority plus independent cross-evidence. Only that terminal evidence-bound Trace may enter the ordinary S5 exact-plan gate. Visual QA and L4 remain separate; only the user can sign L4.
