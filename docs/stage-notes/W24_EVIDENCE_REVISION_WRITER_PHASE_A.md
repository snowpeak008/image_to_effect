# W24 S5 Evidence Revision Writer — Phase A

Date: 2026-08-26

## Result

Phase A adds a fail-closed, read-only-input writer for immutable legacy C0 E1 descriptors. It does not evaluate evidence, emit PASS/FAIL, write a terminal receipt, or issue authority.

The production registry is intentionally absent. A non-test build returns `REGISTRY_PENDING` and publishes nothing. `UNITY_INCLUDE_TESTS` may install an exact in-memory registry and successful publication is labelled `TEST_ONLY_DESCRIPTOR_WRITER`.

The current post-r26 source adds `w24-s5-shared-legacy-raw-replay/1`. The Writer remains the single implementation of the legacy raw seal/metadata/tree validator, and Phase B now calls its internal read-only facade instead of carrying a second raw validator. `W24S5LegacyRawReplayPins` are caller-supplied structural pins, not trust, verdict, transition, or candidate-advance authority. Replay still requires the Candidate Reader's private-issuer `CandidateReplayAuthority`; a successful call returns a Writer-private-issuer opaque projection containing only immutable scalar identities and exact record-hash lookups. It exposes no `JObject`, `JToken`, byte buffer, route, verdict, terminal record, receipt, or transition surface.

The current shared-raw binding is now covered by the r28 focused Unity runs recorded below. The r26 Unity evidence remains historical evidence for the immediately preceding Writer binding and must not be cited for the current hashes.

## Production boundary

- Public input is only `candidateReceiptPath`, `candidateReceiptFileHash`, and `evidenceRevision`.
- Only `w24-candidate/1.0`, legacy `C0`, and `E1` are accepted in Phase A.
- `E2` and revisioned `C1`/`C2` are rejected before mutation.
- The writer consumes the candidate reader's opaque candidate-only replay authority. It never reconstructs authority from `Read().Snapshot` or error strings.
- Legacy S0b is structurally sealed and marked evaluator-unsupported. Legacy S3 replays the already-sealed typed raw registry, metrics input/report, matrix, tool pin, and environment observation; it does not execute the tool or infer a verdict.

## Atomic publication

The writer acquires a repository-scoped `CreateNew` lock whose lifecycle is owned by a `DeleteOnClose` handle, builds `.E1.pending-<guid>`, writes candidate-local writer/schema/capture/evaluation snapshots, writes the typed-self-hashed descriptor last, verifies the pending tree, releases the first prepared byte payload, replays every candidate/raw/registry input a second time, and publishes with `Directory.Move`. Existing `E1` is write-once and rejected. A post-move readback failure first moves the invocation-owned `E1` to a direct rollback-quarantine child before bounded cleanup, so the normal failed-readback path does not leave a formal revision namespace. If that quarantine move itself fails, the writer throws a `PUBLICATION_ROLLBACK_FATAL` aggregate containing both failures; it does not return ordinary `INVALID` or claim atomic rollback. In that fatal path the formal `E1` may remain and must not be treated as usable publication evidence.

Writer/capture sources are capped by a 128 MiB aggregate snapshot budget, a prepared tree is capped at 160 MiB, and the whole replay is capped at the 1 GiB legacy raw allowance plus that prepared allowance. The second replay is never retained alongside the first prepared payload.

Cleanup is deliberately narrow. Only a direct invocation-owned pending or rollback-quarantine name is considered, and a bounded traversal rejects reparse points or more than 8 levels, 64 directories, or 512 files. Any traversal error or bound exceed preserves the quarantined tree for inspection.

## Independent-audit remediation

- The raw seal's `provenance.operatorCommandHash` must equal the hash on the unique sealed supplemental `formal-capture-command` record at `diagnostics/operator-command.json`.
- S0b rejects the exact top-level Contract extension routes `extensions.typedDiagnostics` and `extensions.captureToolBundle`; it does not use a recursive property-name union. S3 requires its Contract `metricPlan.tool` to equal `typedDiagnostics.metricsTool.path`, requires `bridge=W24MetricsEvidenceDag`, and requires the exact ordered kind-specific `inputFields`.
- S3 no longer recursively harvests arbitrary JSON strings as evidence references. Only the real frozen kinds `trail`, `fragment_tracks`, `multiview_3d`, and `receiver_luminance_ldr` are accepted. Each has an exact field/evidence-reference shape, exact Contract thresholds, IDs, seeds, logical frames, frozen views and `checkIdPattern` projection, plus exact evidence-set consumption. Unknown kinds, synthetic kinds, route swaps, and one-field value swaps fail closed.
- Multiview `objectIds`/`depth` and receiver `on`/`off`/`receiverIds`/`effectMask` are additionally bound to their real pass ID, encoding, semantic ID/path slot, and exact provenance role or chain; slot swaps fail closed even when seed/frame/view context is unchanged.
- The gate-owned S3 registry now explicitly pins `LegacyMultiviewMinDepthSpan=0.0` alongside the exact capture-tool bundle hash. The writer validates the sealed metrics input against that capture-tool policy; it does not reinterpret the historical input as an exact projection of Contract `thresholds.minimumLinearDepth`.
- `w24-s3-capture/3.5` and its already-sealed legacy C0 metrics inputs use `minDepthSpan=0.0`, while the frozen Contract declares `thresholds.minimumLinearDepth=0.0001`. Phase A preserves this contract-semantic mismatch structurally and grants no verdict. A future evaluator must route this legacy evidence to `EVIDENCE_INVALID`; satisfying `0.0001` requires a vNext/revisioned capture and raw namespace, never modification or re-signing of the sealed C0 input or 3.5 bundle.
- Every metrics-report `checks` token must be a JSON object after its typed self-seal is verified. `EVIDENCE_INVALID` requires `machineGatesPassed=false` and an array with exactly zero tokens; `MEASURED` still requires the exact frozen check ID/kind set and aggregate bit.
- Writer IDs are bounded ASCII descriptor tokens and writer/capture versions are bounded version tokens, all capped at the same 96 characters as both legacy schemas. Writer-shaped S0b and S3 success descriptors, the 96-character boundary, and 97-character rejection are exercised through `Draft202012Validator`.
- The frozen Python executable is SHA-256 hashed as a bounded stream twice, compared for stable hash/length and its immutable pin, and its byte length is charged to the aggregate replay budget. Its bytes are never materialized in a 64 MiB array.
- Both legacy schemas are C0/E1-only: `evidenceRevision` is `const: 1`, predecessor is exactly `NONE`, raw layout is exactly `LEGACY_C0_FLAT_E1`, and every descriptor/snapshot/evaluation path is in the E1 namespace. Repository and asset paths share the writer's 512-character total, 128-character ASCII-segment policy.
- Publication and readback apply compiled exact descriptor semantics in addition to replaying the pinned schema identity, so a pinned but broadened schema cannot authorize an output.

## Snapshot layout

- `evidence/E1/snapshots/writer/writer.bundle.json`
- `evidence/E1/snapshots/writer/sources/<ordinal>.source`
- `evidence/E1/snapshots/schema/<route-schema>.schema.json`
- `evidence/E1/snapshots/capture-tool/capture-tool.bundle.json`
- `evidence/E1/snapshots/capture-tool/sources/<ordinal>.source`
- S3 only: `evidence/E1/snapshots/evaluation/render_metrics.py`
- S3 only: `evidence/E1/snapshots/evaluation/metrics-environment.json`
- `evidence/E1/evidence-revision.json` (written last)

## Focused Unity coverage

The EditMode fixture contains 42 expanded NUnit cases across 32 methods. It covers S0b and MEASURED-shaped S3 atomic success; typed descriptor/file-set seals; input non-mutation; reader-authority constructor rejection; production-registry pending; candidate, capture-source, and operator-command-hash swaps; raw tamper, extra, missing, reparse, oversize, and excluded legacy `bound/` files; lock contention and injected lock-commit failure; existing E1; E2/C1/C2 rejection; aggregate budgets; broadened pinned-schema rejection; successful post-move quarantine and composite-fatal quarantine failure; malformed and oversized integer conversion; hostile pending cleanup bounds; both S0b/S3 route mismatches; Contract-only `typedDiagnostics`/`captureToolBundle` rejection; all four exact metric-projection value swaps; multiview object-ID/depth and both receiver semantic-slot swaps; the accepted legacy `0.0` input versus Contract `0.0001` no-verdict boundary and capture-policy swap rejection; scalar report-check rejection on both routes; nonempty `EVIDENCE_INVALID` rejection; the 96/97 descriptor-token boundary; and synthetic metric-kind rejection.

The pre-shared-raw historical isolated shadow Unity gates were:

- `r25-w24-evidence-phase-a/candidate-reader-final`: `W24S5CandidateEvidenceReaderTests`, 4/4 passed, 0 failed/skipped/inconclusive, Unity exit `0`.
- `r26-w24-evidence-phase-a/evidence-writer-final-r2`: `W24S5EvidenceRevisionWriterTests`, 42/42 passed, 0 failed/skipped/inconclusive, Unity exit `0`.

The first runtime writer attempt in `r25-w24-evidence-phase-a/evidence-writer-final` is rejected evidence (31/42). It exposed an impossible recorder-profile validator that required `toneMapping` to be both a string and an object, plus an under-constructed reverse-route fixture. The corrected source removed `toneMapping` from the scalar-string loop while retaining its exact `{value,validation}` object validation, and the reverse-route case constructed an S3 Contract then stripped and resealed its raw typed records to obtain the intended S0b-shaped capture. The r26 run is the accepted historical pre-shared-raw Writer result; r28 below is the current shared-raw Writer result.

The Python Draft 2020-12 suite reports 13/13 passing, including both writer-shaped legacy success descriptors. After that historical r26 run, the scratch candidate, raw evidence root, test asset root, repository lock, pending directory, rollback directory and formal test E1 were absent. These tests exercise only the `UNITY_INCLUDE_TESTS` registry and scratch effect. They do not activate the production registry, execute the S3 metrics tool, run the S3 PlayMode producer, emit a terminal receipt, or publish a real candidate E1.

The current live `w24-s3-capture/3.5` source set is not replayable: `W24RealLightingModule.cs` and `VfxDesignContract.cs` have advanced beyond their two frozen bundle hashes. `ReplaySourceRegistry` therefore rejects a real S3 publication even before any authority could be issued; production is additionally stopped earlier by `REGISTRY_PENDING`. A real S3 E1 remains NO-GO until the historical source bytes are recovered as an immutable snapshot or a vNext/revisioned raw capture is produced.

## r26 Unity-verified artifact hashes (historical pre-shared-raw binding)

- `W24S5CandidateEvidenceReader.cs`: `sha256:ed3908778d38778bf2009db63d6a50b602e11af071fdcb374d8283a4f48408a5`
- `W24S5CandidateEvidenceReaderTests.cs`: `sha256:3dd27aa4a0f9badb7ce1df4a3cf2980e96ca308508a2a4f4f159c0adfe14eead`
- `W24S5EvidenceRevisionWriter.cs`: `sha256:42b44f47377fb074dbf1143e65c5488fffba5e1662ebfffd514216041d97c39b`
- `W24S5EvidenceRevisionWriterTests.cs`: `sha256:0d554d272507a061e8fd8b320359410740e5a1ea354f6c32a9f4884951bc5332`
- frozen `W24S3GraphicsCaptureEvidenceTests.cs`: `sha256:3c5ba683b9d1ea9eb770e4aa4d33a001bb243523caf49ca51e58b7a2ff234c7f`
- legacy C0/S0b schema: `sha256:a87fa43b3dcd6f8b43d8f0725f95b929070e1ad71821509287e777a4f8ce38b6`
- legacy C0/S3 schema: `sha256:c7f48f81fdab4d62d6f2bf2b29ed2606644700e8e22c3a63eedcdf975104d3de`
- schema tests: `sha256:da29396a0dc70e4ee0b10e31922d3016bffd608cadf5411685aec5576e3fe23a`
- final reader XML: `sha256:df14dc3e0d07cf0de2556ba4153d2ce9766d30d2afac116cd958659ee9d20f6f`
- final reader log: `sha256:d6a3900aaf74c7495f039676ff2a9877627e5e6f553758db30d1625c830f82ff`
- final writer XML: `sha256:d702fbc34f43c8e8681fae366c49256c9d25ad77db19841e93654b7c0a76446b`
- final writer log: `sha256:2b485e3f85e180765a82f5774ffdbaa0e9233ae3c094a737711dd220eef8dafb`

## Current shared-raw binding and r28 focused Unity evidence

- `W24S5EvidenceRevisionWriter.cs`: 2537 lines, `sha256:1550c64bc744562d69c0ce0953d9514869627e65a2a97e5886e09b84ac2df06d`
- `W24S5EvidenceRevisionWriter.cs.meta`: `sha256:e42d06d5cbe8e741997454c47e0dcd39c7e5c703388fc0c29790c62a1275b16a`
- `W24S5EvidenceRevisionWriterTests.cs`: 1345 lines, `sha256:0d554d272507a061e8fd8b320359410740e5a1ea354f6c32a9f4884951bc5332`
- `W24S5EvidenceRevisionWriterTests.cs.meta`: `sha256:2f997ad7272509d1f44486be88c79b9d14686ce51e7df01794b240dd255cdad1`
- Writer-focused source/tests Roslyn harness: exit `0`, zero compile errors.
- Combined Writer + Phase-B current source harness with `UNITY_INCLUDE_TESTS`: exit `0`, zero compile errors.
- Combined production/no-`UNITY_INCLUDE_TESTS` harness: exit `0`, zero compile errors; production registry behavior remains `REGISTRY_PENDING` before Writer raw replay or publication.

The accepted current-source Unity runs are under `.codex_tmp/w24-stage-regression-results/r28-w24-shared-raw-s6-editor` against `.codex_tmp/w24-fresh-20260825-0628/project`. Canonical and shadow Writer/Machine source and focused-test SHA-256 values matched before this report update.

- Writer filter `VFXComposer.Tests.EditMode.W24S5EvidenceRevisionWriterTests`: PID `35496`, outer exit `0`, `42/42` passed with no failed/skipped/inconclusive cases. XML interval `2026-08-26 02:01:42Z → 02:01:47Z`, duration `4.7854052s`; XML `sha256:1fb61e96c8c3dcc6d6dc9d34a68e67f8da5034e74dd87cf5c16bc6de9eee516a`, `LastWriteTimeUtc=2026-08-26T02:01:47.5230791Z`; log `sha256:7427849156576a9cacfadc07a11fc1e322a12c541a362e978afc229206e28eec`, `LastWriteTimeUtc=2026-08-26T02:01:48.0276695Z`.
- Shared-core Phase-B consumer filter `VFXComposer.Tests.EditMode.W24S5MachineFailureProducerTests`: PID `9228`, outer exit `0`, `13/13` passed with no failed/skipped/inconclusive cases. XML interval `2026-08-26 02:02:04Z → 02:02:13Z`, duration `8.6847156s`; XML `sha256:ea4763a6fef47689e616bfb16452848327dc921b3b13ae204c46d3f80f6c6e19`, `LastWriteTimeUtc=2026-08-26T02:02:13.7099747Z`; log `sha256:62a433dc39e57aace61862074bbc4e1159b91028d02d02958bfd55d6d20a315f`, `LastWriteTimeUtc=2026-08-26T02:02:14.2172111Z`.

Both logs record the exact EditMode filters, result publication, Input System shutdown, licensing-channel disconnects, `Cleanup mono`, and natural completion. The Candidate Reader focused suite was not rerun in r28; its r25 result remains the latest direct Candidate Reader evidence. These r28 runs do not close the separately duplicated persisted descriptor/schema/snapshot/evaluation validators or provide an evaluator runtime.

The persisted descriptor/schema/snapshot/evaluation projection validators remain separately implemented in the Writer and Phase-B scaffold. They are not claimed closed by `w24-s5-shared-legacy-raw-replay/1`; production evaluator admission remains pending.
