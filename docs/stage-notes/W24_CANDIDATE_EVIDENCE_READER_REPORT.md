# W24 Candidate / Evidence Reader Stage Report

Status: `LEGACY_C0_CANDIDATE_REPLAY_UNITY_FOCUSED_VERIFIED / CANDIDATE_ONLY_OPAQUE_REPLAY_AVAILABLE / PHASE_A_DESCRIPTOR_TEST_ONLY / EVALUATOR_PENDING / NO_MACHINE_VERDICT`

This stage adds a bounded, read-only replay boundary for a caller-pinned candidate receipt and an E1/E2 request. It is intentionally not a machine evaluator, failure issuer, evidence writer, Visual QA authority, L3/L4 authority, or user verdict.

## Implemented boundary

- `w24-candidate/1.0` is dispatched only through the exact legacy `C0` path `docs/vfx-candidates/<effectId>/C0/candidate-receipt.json`.
- The legacy candidate receipt uses its exact 27-field schema. The reader separately replays:
  - the pinned bootstrap Contract and preregistration Trace;
  - the candidate-local frozen Contract and `C0_CAPTURE_PENDING` Trace;
  - the exact 19-field `VfxOutputManifest` v1 bootstrap snapshot and its 16-field evidence-free `formalProduction` binding;
  - the raw build hash versus canonical receipt hash distinction;
  - all declared owned-output bytes and `.meta` GUIDs;
  - the exact owned-output root file set, with bounded/reparse-safe traversal;
  - the Preview Scene byte hash and `Preview.unity#camera` binding;
  - the candidate static namespace, excluding the separately routed `evidence` and `terminal` namespaces.
- Project assets are hashed as bytes. JSON, Contract, Trace, receipt, Manifest, source snapshot, and `.meta` inputs use strict UTF-8 parsing where text is required; binary Unity assets are not incorrectly forced through UTF-8 decoding.
- `ReplayCandidateOnly` now returns a private-issuer opaque `CandidateReplayAuthority` only after the same candidate replay succeeds; Phase-A/Phase-B structural code consumes this authority instead of inferring validity from a mutable snapshot or error text.
- The four real legacy C0 candidates still have no production-admitted pre-verdict E1 descriptor. The Phase-A evidence-revision writer can create exact S0b/S3 descriptor schemas only under its test registry; production remains `REGISTRY_PENDING`, so ordinary E1 still returns `INVALID` after a non-null candidate snapshot.
- The repository defines no legacy C0 E2 namespace or predecessor pin. E2 therefore returns `INVALID` after a non-null candidate snapshot.
- `w24-candidate-revision/2.0` remains a separate route. Its current C1/C2 transaction is explicitly test-only and has no committed E1/E2 transition schema, so the reader cannot turn it into production evidence.

The source contains no file/directory/AssetDatabase mutation surface. The public result vocabulary remains only `INVALID` or `VALID_READ_ONLY`; neither value is an adjudication.

## Source identity

- `project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5CandidateEvidenceReader.cs`
  - SHA-256 `ed3908778d38778bf2009db63d6a50b602e11af071fdcb374d8283a4f48408a5`
  - `.meta` GUID `7a5b004cbac84872aa2f496cde919ae0`
  - `.meta` SHA-256 `4183fa3805f5de3d7e66cbe38b5df620b1876e3b85d8b17a2dc9fc5276aae86a`
- `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S5CandidateEvidenceReaderTests.cs`
  - SHA-256 `3dd27aa4a0f9badb7ce1df4a3cf2980e96ca308508a2a4f4f159c0adfe14eead`
  - `.meta` GUID `b7a4c9e18f2d43c6a5097e1b3d8f0c24`
  - `.meta` SHA-256 `dc849715889010959a5613bbc9144064b3f4b3a402c58732aa009e8b2ac4e430`

Canonical and isolated-shadow source/test bytes matched exactly before the accepted run. Unity 2022.3 Bee/Roslyn compiled both `VFXComposer.Editor` and `VFXComposer.Tests.EditMode` with exit code `0` before the focused run.

## Focused Unity evidence

Rejected predecessor `r23-candidate-evidence-reader` is retained as diagnosis only:

- assertions: `2/4` passed, `2/4` failed;
- process gate: timeout/forced stop after XML;
- root cause: a synthetic five-field Manifest schema plus the invalid comparison `bootstrapContractHash == candidate-local contractHash` rejected real C0 candidates before `Snapshot` assignment;
- XML SHA-256 `be1b1296796a8cdc7c46d944861066f6114c5ff1e8c8ebcabfef39df49d295fd`;
- log SHA-256 `fe08ae889711eeb94043a6eeb967f11e07998f8b278030c248001f7492588f18`.

Accepted current-source run `r25-w24-evidence-phase-a / candidate-reader-final` (superseding the earlier accepted r24 result):

- project: `.codex_tmp/w24-fresh-20260825-0628/project`;
- platform/filter: `EditMode / VFXComposer.Tests.EditMode.W24S5CandidateEvidenceReaderTests`;
- PID `42992`;
- XML interval `2026-08-25 23:19:25Z` to `2026-08-25 23:19:25Z`, duration `0.4208484s`;
- process exit code `0`;
- assertions `4/4` passed, `0` failed, `0` skipped, `0` inconclusive;
- XML SHA-256 `df14dc3e0d07cf0de2556ba4153d2ce9766d30d2afac116cd958659ee9d20f6f`;
- log SHA-256 `d6a3900aaf74c7495f039676ff2a9877627e5e6f553758db30d1625c830f82ff`;
- the log records result write, Input System shutdown, both licensing-channel disconnects, `Cleanup mono`, and normal process exit.

The four real shadow legacy candidates are `sustained_flame_3d`, `w24_moving_projectile_trail`, `w24_weapon_socket_fragments`, and `w24_real_light_receivers`. Each now completes candidate replay before the intentional E1 failure; the E2 test confirms that an undefined evidence revision is not invented.

## r28 downstream shared-raw evidence (Candidate Reader not rerun)

r28 did not run `W24S5CandidateEvidenceReaderTests`; the r25 `4/4` result above remains the latest direct Candidate Reader focused evidence. The current Writer and Phase-B suites nevertheless exercise the reader-issued `CandidateReplayAuthority` while covering the shared raw facade. Their current canonical/shadow source and focused-test hashes matched for the run.

- Writer filter `VFXComposer.Tests.EditMode.W24S5EvidenceRevisionWriterTests`: PID `35496`, outer exit `0`, `42/42` passed with no failed/skipped/inconclusive cases. XML interval `2026-08-26 02:01:42Z → 02:01:47Z`, duration `4.7854052s`; XML `sha256:1fb61e96c8c3dcc6d6dc9d34a68e67f8da5034e74dd87cf5c16bc6de9eee516a`, `LastWriteTimeUtc=2026-08-26T02:01:47.5230791Z`; log `sha256:7427849156576a9cacfadc07a11fc1e322a12c541a362e978afc229206e28eec`, `LastWriteTimeUtc=2026-08-26T02:01:48.0276695Z`.
- Phase-B filter `VFXComposer.Tests.EditMode.W24S5MachineFailureProducerTests`: PID `9228`, outer exit `0`, `13/13` passed with no failed/skipped/inconclusive cases. XML interval `2026-08-26 02:02:04Z → 02:02:13Z`, duration `8.6847156s`; XML `sha256:ea4763a6fef47689e616bfb16452848327dc921b3b13ae204c46d3f80f6c6e19`, `LastWriteTimeUtc=2026-08-26T02:02:13.7099747Z`; log `sha256:62a433dc39e57aace61862074bbc4e1159b91028d02d02958bfd55d6d20a315f`, `LastWriteTimeUtc=2026-08-26T02:02:14.2172111Z`.

These are downstream shared-raw integration results, not a new Candidate Reader acceptance run. They do not close the duplicated persisted descriptor validator, evaluator runtime, terminal protocol, or route authority.

## Remaining production gates

1. The current post-r27 source closes the legacy raw half with one Writer-owned validator and a private-issuer opaque scalar/hash projection consumed by Phase B. `W24S5LegacyRawReplayPins` remain caller-supplied structural pins, not trust or advancement authority. Promotion still requires one shared persisted descriptor/schema/snapshot/evaluation replay validator; that descriptor layer remains independently duplicated between Writer and Phase B and therefore cannot yet serve as a production evaluator trust root.
2. Give legacy C0 vNext candidate-local snapshots of the original bootstrap Contract/Trace. C0 v1 currently re-reads their pinned authoritative paths, matching the existing S5 transition behavior; any later source drift fails closed rather than silently accepting an unverifiable predecessor.
3. Define a real C1/C2 evidence transition. The current revision transaction is `TEST_ONLY_TRANSACTION_INFRASTRUCTURE / FAILURE_ISSUER_PENDING`; the reader does not treat its five-field test fixture Manifest as a production schema.
4. Independently replay C1/C2 predecessor receipts, design-semantic hash, production Manifest input bytes, capture-tool input bytes, and source snapshots before admitting any revisioned evidence.
5. Add adversarial focused fixtures for bootstrap-pin drift, extra owned files, reparse entries, binary owned assets, predecessor swaps, and evidence descriptor/report swaps.
6. Implement a repository-trusted hermetic evaluator runtime, frozen-tool rerun, independent rerun-result comparison, exact terminal/route-receipt schemas, atomic publication, and persisted replay before issuing any opaque route authority. A valid structural replay alone must never issue `MACHINE_FAIL`.

Visual QA, user review, L3, L4, publication, commercial eligibility, actual MCP adapter execution, and final product acceptance all remain pending.
