# S12C4 final readiness supplement

Status: **Main-Agent independent acceptance passed. S12 is complete at source stage; no external release was performed.**

This supplement extends the existing S11 A7 release gate without changing S11's historical conclusions. A7 now serializes four scenes in a real external temporary Windows Player build: the 2D and 3D formal fireball previews, formal generated Slash preview, and retained local AI-validated Slash evidence preview. The external Player output is removed in `finally`.

Release documentation now covers the closed Slash v2 error-code family `E1200`–`E1293` (and `I1230`) using stable path families; the existing source-to-documentation bidirectional audit remains the machine gate. README now describes the isolated Slash v2 and audited AI Recipe/Patch workflow without claiming a release, device performance, or on-device AI.

[Slash static budget](../release/S12_SLASH_STATIC_BUDGET.md) separately reports live enabled-module actuals (37 particles, 3 particle systems, 4 unique materials, 7 transparent renderers, 0.45 s) and the reviewed limits (48, 4, 5, 7). It is explicitly static preflight, not hardware certification.

Verification completed for this development handoff: compile exit 0; targeted four-scene A7 Windows Player Build 1/1; targeted error-code audit 1/1; and targeted S12 static-budget release test 1/1. The A7 temporary Player output was removed, and canonical Generated Slash remains rebuilt from canonical revision 1.

## Main-Agent independent acceptance

The independent final run used Unity `2022.3.62f3c1` and did not rely on the development-agent summary:

- Git-ignore audit: passed.
- Compile check: exit 0.
- Full EditMode: **141 total / 106 passed / 0 failed / 35 historical or one-time tests skipped**. This run includes the real four-scene external Windows Player build.
- Full PlayMode: **8 total / 8 passed / 0 failed**.
- Final state: Generated contains exactly `fireball_2d`, `fireball_3d`, and `slash_3d_stylized`; canonical Slash is Recipe v2 revision 1; Prefab GUID is `0dc223c2ffbe2c14aa24f424440f1cd2`; no S12 temporary Recipe/history, compiler/Patch pending or backup artifact, Player-build temp directory, or Unity process remains.

Two independent full-suite attempts were rejected before this pass. The first exposed a native Unity/URP crash because an ordinary regression test regenerated evidence through `Camera.Render`; evidence generation is now an explicit menu operation, while routine tests read-verify the committed PNG SHA-256 values, source Prefab GUID, canonical Recipe/Build hashes, recorded particle facts, and four visual-state relations. The second completed with three failures caused by pre-S12 fixed-set assumptions; the Runtime namespace string audit, A7 fixed-preview audit, and formal Cohort-K Generated-set gate were updated without making them `Explicit` or reducing their assertions. The final full run re-executed all current gates from a clean process and passed.
