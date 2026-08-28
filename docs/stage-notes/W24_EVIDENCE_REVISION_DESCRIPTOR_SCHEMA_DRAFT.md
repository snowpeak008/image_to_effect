# W24 immutable evidence-revision descriptor schema draft

Status: `THREE_EXACT_SCHEMAS_FOCUSED_VERIFIED / WRITER_PENDING / READER_INTEGRATION_PENDING / EVALUATOR_PENDING / NO_MACHINE_VERDICT`

This stage freezes three mutually exclusive Draft 2020-12 JSON shapes for a pre-verdict, immutable evidence revision. It does not implement a descriptor writer, extend the Unity candidate reader, evaluate machine gates, issue a failure authority, advance a candidate, or change any frozen S3 bundle, Contract, formal evidence, or shadow workspace.

## Frozen schema routes

| Schema id | Candidate route | Evaluation input |
|---|---|---|
| `w24-s5-evidence-revision-legacy-c0-s0b/1` | legacy `w24-candidate/1.0`, `C0` | exact `w24-s5-eval-input-s0b-legacy/1` fields |
| `w24-s5-evidence-revision-legacy-c0-s3/1` | legacy `w24-candidate/1.0`, `C0` | exact `w24-s5-eval-input-s3-render-metrics/1` fields |
| `w24-s5-evidence-revision-revisioned-s3/1` | revisioned `w24-candidate-revision/2.0`, `C1` or `C2` | exact `w24-s5-eval-input-s3-render-metrics/1` fields |

Every descriptor has exactly these required top-level fields and rejects all others: `schema`, `descriptorStatus`, `writer`, `effectId`, `candidateId`, `candidateRevision`, `contractRevision`, `evidenceRevision`, `candidate`, `rawCapture`, `captureTool`, `evaluationInput`, `predecessor`, `selfHashEncoding`, and `selfHash`. Every nested object is likewise exact (`additionalProperties: false` with every declared property required). There are no nullable fields, optional object members, `anyOf` branches, or type unions.

The descriptor status is only `RAW_CAPTURE_SEALED`. This is a physical-evidence lifecycle state, not a machine verdict. The self-hash encoding is only `w24-typed-binary-v1`; the schema checks the hash token but cannot recompute it. A production reader must recompute the typed self-hash over the descriptor without `selfHash` and compare exact bytes.

## Evidence revision and namespace rules

- Legacy descriptor namespace: `docs/vfx-candidates/<effectId>/C0/evidence/E<n>/evidence-revision.json`.
- Revisioned descriptor namespace: `docs/vfx-candidates/<effectId>/R<contractRevision>/C1|C2/evidence/E<n>/evidence-revision.json`.
- Revisioned raw namespace: `artifacts/vfx-evidence/<effectId>/R<contractRevision>/C1|C2/E<n>/raw`.
- `E1` has the exact predecessor `{ "kind": "NONE" }`. For the legacy C0 routes only, E1 may describe the already sealed flat raw root `artifacts/vfx-evidence/<effectId>/C0`; its layout is explicitly `LEGACY_C0_FLAT_E1`.
- `E2` is not a general retry. It requires an exact `EVIDENCE_INVALID` predecessor that pins E1's descriptor file, gate report, and evidence-invalid receipt by path and file hash. E2 always uses the new `EVIDENCE_REVISION_RAW` namespace `.../C0/E2/raw` for legacy C0 or `.../E2/raw` for a revisioned candidate.
- Evidence revisions outside `1..2`, E1/E2 predecessor swaps, candidate `C1`/revision `2` swaps, and legacy/revision namespace swaps are schema-invalid.

The predecessor terminal files are reserved below `E1/terminal/` as `machine-gate-report.json` and `evidence-invalid-receipt.json`. A completed implementation Trace is deliberately absent from the descriptor so raw capture sealing cannot depend cyclically on a later formal transition.

## Immutable writer and capture-tool bindings

The `writer` object is exact: `writerId`, `writerVersion`, candidate-local `bundleSnapshotPath` and file hash, `bundleTypedHash`, an ordinal-bearing `sourceSnapshots` array, `sourceSetTypedHash`, and candidate-local descriptor-schema snapshot path/file hash. Its only v1 locations are `E<n>/snapshots/writer/writer.bundle.json`, `E<n>/snapshots/writer/sources/...`, and `E<n>/snapshots/schema/<route-specific-schema-filename>.schema.json`. The route-specific filenames are exactly `w24-s5-evidence-revision-legacy-c0-s0b-v1.schema.json`, `w24-s5-evidence-revision-legacy-c0-s3-v1.schema.json`, and `w24-s5-evidence-revision-revisioned-s3-v1.schema.json` for their respective routes.

The descriptor-schema snapshot is evidence, not a trust root. A compiled registry must hard-bind the accepted schema id and expected schema bytes/hash before parsing; it must never dynamically trust the candidate-provided snapshot.

The independent `captureTool` object binds its version, candidate-local bundle snapshot/file hash, canonical bundle hash, complete ordinal-bearing source snapshots, and typed source-set hash. Its only v1 locations are `E<n>/snapshots/capture-tool/capture-tool.bundle.json` and `E<n>/snapshots/capture-tool/sources/...`. S3 metrics-tool and environment snapshots likewise live only below `E<n>/snapshots/evaluation/`. The predecessor layouts `E<n>/writer/`, `E<n>/capture-tool/`, and `E<n>/evaluation/` are schema-invalid and have no compatibility alias.

Source records have exactly `ordinal`, `sourcePath`, `sourceSha256`, `snapshotPath`, and `snapshotFileHash`; ordinals are bounded to `0..127`, and both writer and capture-tool source lists are bounded to `1..128` items.

JSON Schema cannot prove that ordinals are contiguous and unique, that source paths form a bijection, that snapshot bytes match both hashes, or that bundle/source typed hashes were computed correctly. Those remain mandatory fail-closed writer/reader replay checks.

## Raw file-set definition

`rawCapture.fileSetTypedHash` names the `w24-typed-binary-v1` hash of this normative object:

```json
{
  "schema": "w24-s5-sealed-file-set/1",
  "files": [
    {"path": "relative/utf8/path", "sha256": "sha256:<64 lowercase hex>", "byteLength": 1}
  ]
}
```

`files` is sorted by UTF-8 ordinal comparison of normalized `/`-separated relative paths. It contains every artifact named by `evidence-seal.json`, including `evidence-lock.json`, plus `evidence-seal.json` itself. A legacy C0 `bound/` subtree is excluded because it is a later formal-transition product, not pre-verdict raw capture. A production implementation must reject duplicates, missing or extra files, non-normalized paths, reparse traversal, file/hash/length mismatches, artifact-count or total-byte mismatches, and typed-hash mismatches. The schemas additionally match the reader's hard bounds: `contractRevision <= 1,000,000`, `artifactCount <= 512`, and `totalBytes <= 1,073,741,824` (1 GiB).

## Candidate and evaluator boundaries

The two legacy schemas bind the exact 16-field legacy candidate projection: receipt/version, candidate-local Contract and pending Trace, bootstrap Manifest snapshot, build and capture-profile hashes, Runtime Entry GUID/path, and Preview Scene path/file hash. The revisioned schema binds the exact 21-field revisioned projection, including the previous candidate receipt, `R<n>` namespace, production Manifest snapshot and original input file hash, and owned-output root.

A revisioned candidate must use the isolated asset root `Assets/VFX/Candidates/R<contractRevision>/C1|C2/<effectId>`. Its Runtime Entry must be a `.prefab` below that candidate-versioned root, and its Preview Scene must be a `.unity` below the root's `Preview/` subtree. `Assets/VFX/Generated/<effectId>` and shared `Assets/VFX/Preview/...` paths are rejected. JSON Schema proves each path has the candidate-versioned shape, but it cannot prove that the embedded effect, revision, and candidate tokens equal one another or the top-level fields. The compiled reader must enforce those semantic equalities and the exact `ownedOutputRoot` prefix before accepting any snapshot.

The revisioned shape currently requires `candidate.receiptVersion = w24-candidate-revision/2.0`. This is only a lexical/shape constraint. The eventual writer and reader must independently open the receipt and reject the repository's current `TEST_ONLY_TRANSACTION_INFRASTRUCTURE`, `FAILURE_ISSUER_PENDING`, and `L2_MAXIMUM_PENDING` markers. If a production receipt receives a new version, this descriptor schema must also receive a new version; the current schema must not be widened by an enum or prefix match.

S0b evaluation input binds the operator command, semantic telemetry, matched receiver-off/on images, receiver summary, and replay-policy version. S3 evaluation input instead binds recorder-written metrics input, captured metrics report, metrics-tool snapshot, metrics-environment snapshot, required-evidence-matrix hash, and typed-raw-set hash. The two shapes cannot be swapped.

Path regexes constrain every route to repository-relative, forward-slash, bounded paths and the appropriate legacy or revisioned namespace. They intentionally do not pretend to substitute for semantic equality checks. A future reader must still require the path effect, candidate id, `R<contractRevision>`, and `E<evidenceRevision>` tokens to equal their top-level fields, require every writer/capture/evaluation snapshot to share the same descriptor root, and verify every referenced byte and hash.

## Focused verification

Command:

```text
python -m unittest tools.vfx.tests.test_w24_s5_evidence_revision_schemas -v
```

Result: `11/11` tests passed. Coverage includes positive E1/E2 examples for all three routes; Draft 2020-12 meta-validation; exact object-field checks; unknown/missing fields; malformed, unprefixed, uppercase, and short hashes; traversal/backslash/wrong-namespace and obsolete snapshot paths; revision/candidate/ordinal/artifact-count/byte bounds; revisioned asset-isolation roots and file types; predecessor swaps; S0b/S3 evaluation swaps; schema-id swaps; and complete cross-schema rejection.

No descriptor bytes have been written to a candidate. No schema has been installed as a runtime trust root. No production writer, reader integration, evaluator, terminal receipt, machine failure authority, Visual-QA verdict, user signature, L3, or L4 authority is claimed by this schema-only stage.
