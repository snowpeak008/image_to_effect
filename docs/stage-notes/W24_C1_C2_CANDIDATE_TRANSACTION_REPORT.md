# W24 C0 → C1 → C2 immutable candidate transaction infrastructure

Date: 2026-08-26
Status: `C1_C2_TRANSACTION_INFRASTRUCTURE_UNITY_FOCUSED_VERIFIED / FAILURE_ISSUER_PENDING`

## Scope and authority boundary

This change adds the file-transaction primitive for a normal W24 implementation retry. It does not issue a production `MACHINE_FAIL` or `VISUAL_FAIL`, does not create a formal C1/C2 candidate in this repository, and does not grant Visual QA, L3, L4, publication, commercial, or user authority.

The ordinary request has no candidate id/revision, Contract JSON, Trace JSON, failure enum, visual verdict, or maturity field. Candidate `C1`/`C2` and numeric revision `1`/`2` are derived by the gate from the exact hash-pinned predecessor receipt. Advancement additionally requires an opaque `W24S5CandidateFailureAuthority`. Production issuance is deliberately unavailable until a separately versioned producer can replay a sealed write-once machine-failure receipt. Visual-failure issuance remains blocked on the independent Visual-QA authority.

`UNITY_INCLUDE_TESTS` contains a gate-owned test issuer solely to exercise mechanical transaction invariants. Its receipts are conspicuously marked `TEST_ONLY_TRANSACTION_INFRASTRUCTURE`, `FAILURE_ISSUER_PENDING`, `VISUAL_PENDING`, and `L2_MAXIMUM_PENDING`. A future production reader must reject this test-only predecessor mode.

## Immutable layout and bindings

- Existing legacy C0 remains read-only at `docs/vfx-candidates/<effectId>/C0`.
- Same-contract retries are namespaced by the frozen Contract revision:
  - `docs/vfx-candidates/<effectId>/R<contractRevision>/C1`
  - `docs/vfx-candidates/<effectId>/R<contractRevision>/C2`
- Candidate assets use physically disjoint roots outside the recursive C0 generated root:
  - `Assets/VFX/Candidates/R<contractRevision>/C1/<effectId>`
  - `Assets/VFX/Candidates/R<contractRevision>/C2/<effectId>`
- Runtime Entry, Preview Scene, and every Manifest-owned output must be inside the exact revision-owned root and must be disjoint from every earlier candidate path/root.
- Each candidate directory is atomically published with `Directory.Move` after `CreateNew` writes of its derived Contract, evidence-free pending Trace, candidate receipt, exact production-Manifest snapshot, exact capture-tool-bundle snapshot, and every bundle source byte snapshot.
- The authoritative live Manifest path remains fixed, but its bytes are hash-pinned and copied into the candidate-local immutable snapshot. The transaction never writes the live Manifest or candidate asset outputs.
- Each receipt freezes predecessor path/hash, Contract revision/hash, a design-semantic hash, build/runtime/Preview identities, exact owned-output list, versioned bundle input and candidate-local bundle/source snapshots, and the candidate-local `evidence` root at evidence revision `0`.
- Candidate bindings use `FROZEN_PRE_C1` / `FROZEN_PRE_C2`. Required requirements, reference roles, forbidden substitutions, and all other design-semantic fields retain the predecessor semantic hash and Contract revision.

Before commit, the one-use approval replays all persisted inputs. The final replay now runs while holding the repository-scoped `docs/vfx-candidates/.w24-s5-candidate-revision.commit.lock`, acquired with `FileMode.CreateNew` / `FileShare.None` and held through the atomic `Directory.Move`. Contention fails closed before the final replay, and the owning handle closes and deletes the lock from `finally`. This serializes cooperating C1/C2 committers across effects without making the lock an authority receipt.

Receipt, Manifest, Contract, Trace, bundle input/snapshot, bundle source snapshot, Preview/camera binding, owned-output bytes, `.meta` GUIDs, path set, capture-profile hash, Runtime Entry identity, capture-tool version, and candidate-local Manifest reference drift all reject the write. The target parent chain is checked for symlink/junction/reparse points before directory creation, immediately after creation, and again after all pending bytes are written immediately before `Directory.Move`; the pending path is also checked at that final boundary. C2 exhaustion emits no C3; it remains a workflow `NEEDS_USER_DECISION` boundary. The candidate static file-set check treats only exact child `evidence/` and `terminal/` subtrees as separately write-once authority roots; their presence never grants advancement by itself.

## Focused source tests

`W24S5CandidateRevisionTransactionTests` covers source-level fixtures for:

- absence of an opaque authority despite caller-created files under `terminal/`, plus a test-only authorized replay that proves the exact `terminal/` subtree does not invalidate the predecessor's static file set;
- gate-derived C0 → namespaced C1 → namespaced C2 ordering;
- C0 byte preservation and C1 byte preservation after C2;
- write-once candidate replay rejection and one-use approval consumption;
- C2 exhaustion with no C3;
- receipt and owned-output drift between evaluate/commit, with no partial candidate directory;
- C1 candidate-local bundle and source-snapshot tamper rejection before C2;
- revision-owned path, Manifest hash, exact versioned bundle path, and unowned Trace mapping rejection;
- candidate Preview remapping across both the primary capture profile and nested frozen-view diagnostic bindings without changing the frozen design-semantic hash;
- persistent `VISUAL_PENDING`, null Visual-QA/user records, test-only route/issuer/failure-receipt fields, `L2_MAXIMUM_PENDING`, candidate-local evidence root, and `FROZEN_PRE_C1/C2` round-trip fields;
- repository-scoped `CreateNew` lock contention rejecting before the final replay with no candidate target;
- an instrumented final-write seam confirming the exclusive lock remains held while the complete pending tree exists and the immutable target is still absent, then is removed after publish;
- final parent-chain reparse detection rejecting before `Directory.Move`, deleting the pending tree, leaving the target absent, and releasing the repository lock.

The final focused test source has now also been executed in an isolated Unity Editor process; the exact machine credential is recorded below.

## Static and focused Unity verification performed

Before the isolated Unity run, the existing Unity Bee response-file dependency graph plus current missing Runtime/Editor source additions was used to compile:

- current `VFXComposer.Runtime` source: exit `0` (two pre-existing unused-field warnings only);
- current `VFXComposer.Editor` source plus `W24S5CandidateRevisionTransaction.cs`: exit `0`;
- current `VFXComposer.Tests.EditMode` source plus `W24S5CandidateRevisionTransactionTests.cs`: exit `0`.

Generated static compiler outputs are confined to `.codex_tmp/w24-candidate-revision-static` and are not evidence. Both new Unity `.meta` GUIDs occur exactly once; the source-tree `.meta` audit across `project/Assets` and `project/Packages/com.vfxcomposer.unity` (excluding generated `Library`) reported zero duplicate GUID groups.

The post-hardening physical SHA-256 identities are:

- `W24S5CandidateRevisionTransaction.cs`: `974479d8399cb0a9fd99284605ce44f9f0bbf0d8b2a5c873283d2a430d72b915`;
- `W24S5CandidateRevisionTransactionTests.cs`: `15f2f7096ad9da9281262a404f016a5dd89aa2ca71350028913a8823aeadfd48`;
- source `.meta`: `e0eab2ea430dc81fbea16d2c8a78ffeda8ceed3980f71184ff6a1326f9e12ba7`;
- test `.meta`: `efa09f827365798cad82654d23e28ba95f7b5abe35787fe41efb6f18bc949253`.

The final isolated run used Unity `2022.3.62f3c1`, EditMode filter
`VFXComposer.Tests.EditMode.W24S5CandidateRevisionTransactionTests`, and the shadow project at
`.codex_tmp/w24-fresh-20260825-0628/project`. Runner PID `14572` started at
`2026-08-25T19:12:50.5639832Z` and exited naturally with code `0`. The NUnit root is
`10 total / 10 passed / 0 failed / 0 skipped / 0 inconclusive`, `2026-08-25 19:13:00Z` to
`19:13:01Z`, duration `1.4629562` seconds. The log reaches normal licensing disconnect and
`Cleanup mono`.

- XML: `.codex_tmp/w24-stage-regression-results/r21-candidate-revision-transaction/candidate-revision-transaction.xml`, SHA-256 `b0c29aad70a59654264eb83dd82e6202ddab805124136c8866b9f1b41881e81e`, `LastWriteTimeUtc=2026-08-25T19:13:01.8208900Z`;
- log: `.codex_tmp/w24-stage-regression-results/r21-candidate-revision-transaction/candidate-revision-transaction.log`, SHA-256 `adeba14e3825f22139f9531aeb3f11f5b485f723e6c5f577a40edaf95a8be5cc`, `LastWriteTimeUtc=2026-08-25T19:13:02.2197836Z`.

An earlier r20 run (`9/10`) was rejected and superseded: production code correctly failed closed,
while one test expected an obsolete diagnostic substring. Only that assertion was narrowed before
the r21 run; the production transaction source hash above did not change.

## Remaining gate

This source is not a production-capable retry issuer. A later gate-owned producer must write and replay an immutable terminal machine-failure receipt (including receipt/file/self hashes, producer bundle identity, evidence revision, and exact predecessor candidate binding) before `testOnly=false` advancement can exist. The present test-only transaction suite has passed, but after the real producer/replay path lands the affected transaction and S1/S5 regressions must run again in a fresh isolated Unity project with a naturally exiting Editor process. Until those production-authority gates pass, no production C1/C2 has been issued or validated.
