# W24 1.3 S0b: sustained-flame vertical slice — machine-evidence status

## Scope

S0b is the first W24 production baseline: `sustained_flame_3d`. Its Runtime Entry and preview are authored by `SustainedFlameAuthoring`; this report records only the evidence/test closure added around that baseline. It is not a visual QA report, a user verdict, or an L4 sign-off.

## Formal evidence protocol

- Explicit PlayMode job: `VFXComposer.Tests.PlayMode.W24SustainedFlameFormalEvidenceTests.Capture_C0_ActualLifecycleAndLightDiagnostics_FromOneSerializedCamera`.
- Required invocation: `tools/Invoke-Unity.ps1 -Mode PlayMode -UseGraphics -TestFilter VFXComposer.Tests.PlayMode.W24SustainedFlameFormalEvidenceTests`.
- Formal execution requires Unity `-batchmode` with a real graphics device. `-nographics` and GUI/editor captures are rejected for the formal candidate.
- Source is the serialized `MainCamera` in `Assets/VFX/Preview/VFXPREVIEW_SustainedFlame.unity`; no replacement evidence camera is created.
- The preview driver is disabled. Each sequence uses `PlayWithSeed`, normal `Update`/`LateUpdate` progression at 60 fps, and normal `Stop(AllowTail)` or `Interrupt`; it does not call `Simulate`, `Emit`, `SetParticles`, sampling hooks, or visibility-phase shortcuts.
- The frozen seeds are canonical `24001`, robustness `24011`, and robustness `24021`. Canonical and first robustness perform the stop branch; second robustness performs the interrupt branch.
- Each branch remains in steady state for 291 frames (4.85 seconds after start, including a 4.5-second steady window), which exceeds three 1.37-second Shuriken cycles. Retained frames cover start, all steady checkpoints, requested exit, and post-cleanup.

## Candidate package contents

Before graphics capture, the S5 first-build transaction preserves the source preregistration and writes an exact `PRE_C0_FIRST_FORMAL_BUILD` Manifest receipt. A distinct write-once identity candidate is created at `docs/vfx-candidates/sustained_flame_3d/C0/`; its Contract freezes the real preview-scene/build identities and its Trace is explicitly `C0_CAPTURE_PENDING`. The formal recorder reads this candidate, never the pending bootstrap Contract.

The first successful run writes exactly once to:

`artifacts/vfx-evidence/sustained_flame_3d/C0/`

The package contains:

- Beauty plus effect-only diagnostic PNGs for every retained seed/frame pair.
- `capture-metadata.json`, hashes, frozen Capture Profile, source identities, graphics policy, and diagnostic pass manifest.
- The frozen `captureToolHash` is the canonical hash of a registered bundle over the profile/store/recorder, formal PlayMode producer, S5 completion/transition chain, strict Contract/Trace validators, and canonicalizer. The driver version is `w24-s0b-formal-capture/1.1.0`; capture first verifies the exact bidirectional source set and every source hash plus candidate Scene/Manifest/GUID identities. S0b has no typed-metrics/Python authority.
- `diagnostics/semantic-telemetry.json` with controller state, seed, particle/emitter/renderer/light counts, transition serial, and cleanup status for both stop and interrupt branches.
- `diagnostics/receiver-light-off.png`, `receiver-light-on.png`, and `receiver-light-ab.json`. The matched A/B probe hides particle renderers in both samples and toggles only the actual `UnityEngine.Light`; it measures a 15×15 linear-luminance receiver-marker probe. Positive delta is a machine gate for `REQ-LIGHT-RECEIVER`.

The recorder seals every output after metadata write. Existing C0 evidence is never deleted, overwritten, or “updated”; a source/contract change must create the next permitted candidate under the W24 workflow.

## Machine assertions

- Exactly one serialized camera and one Runtime Entry controller exist in the formal scene.
- Start reaches steady, steady survives the minimum window, and both stop and interrupt branches are observed using their distinct controller states.
- Every branch returns to `Idle`, reports bounded cleanup complete, and leaves no enabled controlled Light.
- During steady state exactly one real controlled Light is enabled.
- The real-light receiver A/B diagnostic requires a positive measured linear-luminance delta outside the effect layer.
- Existing production tests additionally verify the entry’s distinct carriers, deterministic particle seeds, zero runtime textures, shared dependencies, manifest/contract binding, and preview/runtime separation. Before the compiler emits these assets those tests report an explicit precondition skip rather than a false production failure.

## Unverified at this writing

- The formal C0 run has **not** been executed in this report revision while the user’s graphical Unity Editor holds the project lock. No evidence directory, performance result, machine-pass assertion, Visual QA result, L3, L4, or user visual acceptance is claimed here.
- The actual graphics-backed batch run must be performed only after the authored Prefab, renderer asset, preview scene, and BuildManifest are present and the editor lock is released.
- `REQ-VISUAL-STEADY` and `REQ-VISUAL-EXITS` remain `VISUAL_PENDING`; only an independent Visual QA review of the produced Beauty sequence and the user’s final dynamic sign-off can resolve them. This report cannot replace either step.
