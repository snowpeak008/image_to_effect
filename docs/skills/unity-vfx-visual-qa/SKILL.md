---
name: unity-vfx-visual-qa
description: Independently review captured Unity VFX evidence against a frozen design contract and route each candidate through the project Visual QA protocol; use after machine gates, not to implement or sign off assets.
---

# Unity VFX Visual QA

Use this project-local skill only for independent visual review. It does not modify assets, diagnose internal component facts as authoritative, or grant L4. Do not install it as a personal Codex skill.

Read [AGENT.md](AGENT.md) before every review. Then read [the review protocol](review-protocol.md); use [cheat patterns](cheat-patterns.md) when checking the captured image sequence. Use [calibration instructions](calibration/README.md) only for S0a work.

When changing report aggregation, validate against [the accepted report](schemas/examples/visual-review.valid.json) and [the rejected `VISUAL_PASS`/`fail` report](schemas/examples/visual-review.invalid-pass-with-fail.json).

For a sealed report, run `python scripts/validate_visual_review.py <review.json>`; it checks the schema and canonical `sealedReportHash` before aggregation.

Output the five top-level routes and the per-requirement three-state verdicts exactly as the protocol defines. Do not claim calibration or ordinary L3 gate authority before `S0A_GATE_QUALIFIED`; in advisory mode, a `VISUAL_PASS` route is only a visual finding and must be visibly labelled non-gating.
