# W24 S5 Phase B descriptor-structure replay scaffold — STOPPED report

Date: 2026-08-26

## Outcome

This phase is intentionally stopped at a read-only descriptor-structure replay scaffold.

Current post-r27 source version is `w24-s5-descriptor-structure-replay-scaffold/2`. It removes the scaffold's duplicate legacy raw validator and calls the Writer-owned `w24-s5-shared-legacy-raw-replay/1` facade. The configured Phase-B registry supplies `W24S5LegacyRawReplayPins`, but those values are caller-supplied structural pins rather than trust, verdict, terminal, transition, or advance authority. The facade additionally requires the Candidate Reader's private-issuer `CandidateReplayAuthority` and returns a Writer-private-issuer opaque replay projection containing scalars and exact record hashes only; no mutable JSON or raw bytes cross the boundary.

The current hashes now have r28 focused Unity coverage. The r27 Unity evidence retained below belongs to the pre-shared-raw source/test binding and remains historical rather than evidence for scaffold `/2`.

- Production returns `EVALUATOR_RUNTIME_PENDING` after validating only the in-memory request shape and observing that no production registry exists. It returns before candidate, descriptor, schema, bundle, raw-tree, process, network, lock, or output-path I/O.
- Tests may install explicit descriptor-schema, Phase-A writer-bundle, and capture-tool bundle trust roots and replay a real Phase-A legacy C0/E1 S0b evidence-revision descriptor twice.
- A successful test-only structural replay returns `TEST_ONLY_DESCRIPTOR_STRUCTURE_REPLAYED` with `EVALUATOR_PROVENANCE_PENDING`.
- `INVALID` means only that the requested structure could not be replayed under this scaffold. It is not a machine-gate outcome.

No evaluator verdict is derived. No machine-gate report, route receipt, terminal directory, replay authority, failure authority, transaction authority, formal transition, candidate advance, or production artifact is created. No PASS/FAIL/INVALID gate receipt schema was added.

The legacy source filename and static class name `W24S5MachineFailureProducer` are retained as compatibility debt. Its callable entry point, request, and result are honestly named `ReplayDescriptorStructure`, `W24S5DescriptorStructureReplayRequest`, and `W24S5DescriptorStructureReplayResult`; there is no publish alias.

## Read-only replay boundary

The test-only path consumes `W24S5CandidateEvidenceReader.ReplayCandidateOnly` and its opaque `CandidateReplayAuthority`. It does not trust the reader's mutable snapshot or error text as evidence authority. It then independently replays the real Phase-A descriptor structure with these checks:

- request pins the candidate receipt path/hash, evidence revision, exact `E<n>/evidence-revision.json` path, and descriptor physical hash;
- descriptor physical SHA-256 and typed `selfHash` are verified separately;
- strict UTF-8 and strict JSON parsing reject duplicates, non-finite numbers, and lone surrogates;
- descriptor, candidate projection, legacy raw projection, capture projection, evaluation-input pins, writer snapshot, capture-tool snapshot, metadata, lock, seal, diagnostic manifest, and record variants use exact field sets and explicit value domains;
- candidate, Contract, build, capture-profile, raw, evaluation-input, schema, writer-bundle, and capture-tool identities are cross-pinned;
- recorder source hashes and absolute scene/prefab/production-manifest/capture-tool provenance paths are bound exactly; path spelling is case-sensitive at the persisted provenance boundary;
- writer and capture-tool snapshots must match the registry-pinned bundle bytes and their typed/canonical hashes;
- descriptor and sealed raw trees have bounded iterative walks, exact file and directory sets, file/directory/depth/aggregate-byte limits, reparse rejection, and reparse checks before each read;
- all expected malformed-input exceptions are converted to the scaffold `INVALID` result;
- the replay is run twice and an immutable structural fingerprint must match.

Phase-A deliberately excludes only the raw root's direct `bound/` child from its sealed file set. This scaffold verifies that direct child itself is a non-reparse directory, but does not descend into, count, hash, fingerprint, or otherwise constrain `bound/**`; downstream transition work may legally mutate that subtree. A nested directory merely named `bound` is not excluded.

The replay accepts only the real Phase-A `w24-s5-evidence-revision-legacy-c0-s0b/1` layout. S3 and E2 semantics remain fail-closed and unimplemented.

## Focused source/test coverage

The focused test fixture uses the real Phase-A writer fixture to publish a structurally valid S0b descriptor, then covers:

- production pending with a nonexistent candidate and a complete before/after directory-set plus file-hash snapshot of both in-scope trees;
- exact honest API surface and absence of verdict, route, report, receipt, terminal, authority, transition, and advancement members;
- absence of filesystem publication APIs and machine PASS/FAIL tokens in production source;
- successful double, read-only replay of the real Phase-A descriptor while evaluator provenance remains pending;
- self-consistent descriptor evaluation-pin tamper rejected by the metadata DAG;
- capture-tool hash and absolute-path provenance swaps rejected after self-consistent resealing;
- descriptor/raw extra empty directories, snapshot byte tamper, and between-replay snapshot mutation rejected;
- the intentional `bound/**` exclusion remaining stable when its content changes between the two structure replays;
- injected reparse-backed descriptor input rejected;
- descriptor physical hash and typed self hash independently enforced.

## Static verification and focused Unity evidence

Before the isolated Unity run, the final source and tests were compiled with Roslyn/.NET in five source-only harnesses:

1. production source plus minimal candidate-reader/Unity stubs;
2. production source plus focused test syntax and stubs;
3. production source against a previously compiled real Phase-A editor API surface;
4. production source and focused tests together with the current Phase-A writer source/tests against that real editor API surface.
5. production source and smoke entry without `UNITY_INCLUDE_TESTS`, proving the registry-free branch compiles independently and returns `EVALUATOR_RUNTIME_PENDING`.

All five builds completed with exit code `0` and zero compile errors. Harness-only unused-field and duplicate-type warnings are not runtime credentials. The production smoke invocation returned `EVALUATOR_RUNTIME_PENDING`. Python static protocol checks also passed for zero-write tokens, honest surface tokens, production registry ordering, exact descriptor suffix, required focused cases, and whitespace/UTF-8 hygiene.

No trusted hermetic evaluator runtime was available, so no fixture outcome was interpreted and no production PASS/FAIL/INVALID result was fabricated.

The accepted isolated run is `r27-w24-phase-b-descriptor-replay`:

- project: `.codex_tmp/w24-fresh-20260825-0628/project`;
- platform/filter: `EditMode / VFXComposer.Tests.EditMode.W24S5MachineFailureProducerTests` under `-nographics`;
- PID `42216`, process start `2026-08-26T00:52:55.7759458Z`;
- XML interval `2026-08-26 00:53:22Z` to `2026-08-26 00:53:30Z`, duration `8.0906665s`;
- process exit code `0`;
- assertions `12/12` passed, `0` failed, `0` skipped, `0` inconclusive;
- XML SHA-256: `7cae770320463285669fbb1e224f6baa05f3eec3baf05059c03d5a1b0e5a2faa`;
- log SHA-256: `019e5d38c3523f386a452532abbd4d06b80375f3c2bf218ffbd5c0626c37eff4`.

The log records result publication followed by Input System shutdown, both licensing-channel disconnects, `Cleanup mono`, and natural process exit. After Unity exited, canonical and shadow source/test hashes still matched, no `w24_evidence_writer_probe` candidate/raw/input tree or evidence-revision lock remained, and the probe terminal-directory count was zero. This focused run proves only the read-only scaffold behavior described above; `-nographics` provides no visual or graphics evidence.

## r27 immutable source/test binding (historical pre-shared-raw source)

- `project/Packages/com.vfxcomposer.unity/Editor/W24/S5/W24S5MachineFailureProducer.cs`
  - lines: `1133`
  - SHA-256: `72c7d5d17236b64e6412efa5f1d9da529bd19b4a92b3668100d397022b1ad666`
- `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S5MachineFailureProducerTests.cs`
  - lines: `398`
  - SHA-256: `d0fd0968e4cc88c8de5a9f7e616aedc52a068b31898ff19e9a00311e73e9efc2`
- source `.meta` SHA-256: `7bfa7b627bd03227fb2e27783733d98a8b5e44c9d07db5fe5564c5a7192852ec`
- test `.meta` SHA-256: `d5f90d27991b69e88da8508efcba31de86a4f4be2272890dd83f8243e12d0058`
- production static DLL SHA-256: `d5de9acfa8288d12a1e086f3c3e717a0b2dc7c634d34ec671ef16f2760625dd2`
- focused-test static DLL SHA-256: `54122263515508acf21dc33b751ac9eafa482908647e31ec26d61726ae90d7e5`
- real Phase-A API static DLL SHA-256: `313d06415ba0887391c18dd9b3b2153b1c4616dea02ac2034c4d8344ef71b27e`
- current Phase-A writer/test integration static DLL SHA-256: `f39e6b0ab102713f96ed45d393f8181edbc00bb973659763e04c4c90e44e17fd`
- production no-`UNITY_INCLUDE_TESTS` static DLL SHA-256: `e7cffcd599e6df4c99d49d6b9bf11700c1fbaae8efe4b0d53ad034d7a6f42461`

The report's own SHA-256 is deliberately reported externally after this binding block is finalized.

## Remaining blockers

Phase B terminal/evaluator/authority protocol is not implemented. A future phase must first provide:

1. a shared private persisted descriptor/schema/snapshot/evaluation replay validator. Legacy raw seal/metadata/tree provenance is now single-implementation, but persisted descriptor semantics remain independently duplicated;
2. a repository-trusted evaluator registry and hermetic runtime lease;
3. a frozen evaluator/tool rerun from the exact sealed typed inputs, with complete independent result comparison;
4. separate exact terminal report and route-specific receipt schemas, atomic publication, persisted replay, and privately constructible route authorities only after the first three items close.

Until then, production remains pending and zero-write. This report grants no machine verdict, recapture decision, blocked decision, transaction authority, candidate advancement, Visual QA, user sign-off, migration completion, L3, or L4 authority.

## Current shared-raw binding and r28 focused Unity evidence

- `W24S5MachineFailureProducer.cs`: 782 lines, `sha256:aeb79cf54307047a6d5b42bee69a98a177530098484047d56e6914c21eb0f9cb`
- `W24S5MachineFailureProducer.cs.meta`: `sha256:7bfa7b627bd03227fb2e27783733d98a8b5e44c9d07db5fe5564c5a7192852ec`
- `W24S5MachineFailureProducerTests.cs`: 457 lines, `sha256:2fe05bc7d2f56d8991b62202976f48e1aa9f0ec5ad0741ab0147554e5b136129`
- `W24S5MachineFailureProducerTests.cs.meta`: `sha256:d5f90d27991b69e88da8508efcba31de86a4f4be2272890dd83f8243e12d0058`
- Combined current Writer + Phase-B source harness with `UNITY_INCLUDE_TESTS`: exit `0`, zero compile errors.
- Current Writer + Phase-B focused test sources: exit `0`, zero compile errors. The new static contract case rejects forged opaque-authority construction, forbids `JObject`/`JToken`/`byte[]` and verdict/route/terminal members, proves the Machine calls the shared facade, proves the removed duplicate helper names are absent, and proves exactly one private `ReplayLegacyRaw` implementation remains.
- Combined production/no-`UNITY_INCLUDE_TESTS` source harness: exit `0`, zero compile errors. Production still returns `EVALUATOR_RUNTIME_PENDING` before candidate/raw/descriptor I/O and retains no filesystem write API.

The accepted current-source runs are under `.codex_tmp/w24-stage-regression-results/r28-w24-shared-raw-s6-editor` against `.codex_tmp/w24-fresh-20260825-0628/project`. Canonical and shadow Writer/Machine source and focused-test SHA-256 values matched.

- Writer prerequisite filter `VFXComposer.Tests.EditMode.W24S5EvidenceRevisionWriterTests`: PID `35496`, outer exit `0`, `42/42` passed with no failed/skipped/inconclusive cases. XML interval `2026-08-26 02:01:42Z → 02:01:47Z`, duration `4.7854052s`; XML `sha256:1fb61e96c8c3dcc6d6dc9d34a68e67f8da5034e74dd87cf5c16bc6de9eee516a`, `LastWriteTimeUtc=2026-08-26T02:01:47.5230791Z`; log `sha256:7427849156576a9cacfadc07a11fc1e322a12c541a362e978afc229206e28eec`, `LastWriteTimeUtc=2026-08-26T02:01:48.0276695Z`.
- Phase-B filter `VFXComposer.Tests.EditMode.W24S5MachineFailureProducerTests`: PID `9228`, outer exit `0`, `13/13` passed with no failed/skipped/inconclusive cases. XML interval `2026-08-26 02:02:04Z → 02:02:13Z`, duration `8.6847156s`; XML `sha256:ea4763a6fef47689e616bfb16452848327dc921b3b13ae204c46d3f80f6c6e19`, `LastWriteTimeUtc=2026-08-26T02:02:13.7099747Z`; log `sha256:62a433dc39e57aace61862074bbc4e1159b91028d02d02958bfd55d6d20a315f`, `LastWriteTimeUtc=2026-08-26T02:02:14.2172111Z`.

Both logs record result publication, Input System shutdown, licensing-channel disconnects, `Cleanup mono`, and natural completion. The Candidate Reader suite was not rerun in r28; r25 remains its latest direct focused result. r26 Writer and r27 Phase-B evidence remain historical bindings. Persisted descriptor/schema/snapshot/evaluation replay, evaluator provenance/runtime, terminal publication, and route authority remain pending.
