# W24 measured-failure / invalid recorder sealing — frozen-chain preparation

Status: **implementation intentionally stopped at the frozen bundle boundary**. No production source, formal Contract, candidate, capture bundle, or evidence byte was changed or re-signed.

## Current behavior and exact blocker

`render_metrics.py` already emits three cryptographically distinct outcomes:

1. `route=MEASURED`, `machineGatesPassed=true`, non-empty complete checks;
2. `route=MEASURED`, `machineGatesPassed=false`, non-empty complete checks containing at least one measured failure;
3. `route=EVIDENCE_INVALID`, `machineGatesPassed=false`, `checks=[]`, non-empty `reason`.

All three carry the same input SHA-256, frozen metrics-tool SHA-256, typed-binary encoding name, and typed self-seal. The new focused Python test proves the measured-failure and invalid shapes remain different typed payloads and that neither test treats them as authority.

The recorder already has the required write-once byte-preservation primitive: `W24ContinuousCaptureRecorder.WriteMetricsReport` writes the validated report bytes and binds report file hash, recorder-written input file/hash, canonical analysis-input hash, and expected tool hash into capture metadata. It does not inspect or mint a machine verdict.

The blocker is immediately before that primitive. `W24MetricsEvidenceDag.ValidateReport` currently requires both `machineGatesPassed == true` and every report check `pass == true`; therefore measured failure and evidence-invalid bytes are thrown away before the recorder can seal them.

This is not safe to patch in place. The current `w24-s3-capture/3.5` bundle exactly pins all sources involved:

| Frozen source | Current/pinned SHA-256 |
|---|---|
| `W24MetricsEvidenceDag.cs` | `sha256:22e561a73f85bc815c0d420ec5e1d0f56da86b684362cbc5eba744b7e581d2de` |
| `render_metrics.py` | `sha256:8e32e6d3df25e002c402f3ec9af98bbfbc10da57b3d545f9bd86fcf4a078b8b6` |
| `W24S5EvidenceTransition.cs` | `sha256:44f1bd9cf5cdbac08d014344002a6f448f25482a2624d55b4e19eb49459b0ce1` |
| `W24S5RecorderCaptureCompletion.cs` | `sha256:44034500d02246cb5322f89aa0f751afd43b38f081442d6752c8478fdb8e3203` |
| `W24ContinuousCaptureRecorder.cs` | `sha256:4c26ab7f52333f7f757a86a343ec048df6cc43d3966cb9caf4dabcda94dec4e9` |

The bundle's canonical hash `sha256:f605aacf4d27128347a2a8434a29cebc95aa789af73df55cc26c6fd7a0e42726` is frozen into all three S3 capture profiles. Modifying any of the production sources now would deliberately make `VerifyToolBundle` reject the old chain.

There is also no standalone `w24-render-metrics-report/v1` JSON Schema today; only the input schema exists. The report route union must be frozen before relaxing the bridge.

## Minimal vNext patch

The smallest safe implementation is a new bundle/Contract revision; it does **not** require changing the recorder, completion gate, transition, or Python metrics implementation.

### 1. Freeze a report schema

Add `tools/vfx/metrics/w24-render-metrics-report.schema.json` with a strict top-level `oneOf`:

- common required fields: `schema`, `route`, `machineGatesPassed`, `checks`, `inputSha256`, `toolSha256`, `sealedReportEncoding`, `sealedReportHash`;
- measured branch: `route` is exactly `MEASURED`; `checks` is non-empty; every check requires protocol `id`, supported frozen `kind`, and Boolean `pass`; `reason` is forbidden;
- invalid branch: `route` is exactly `EVIDENCE_INVALID`; `machineGatesPassed` is exactly false; `checks` is exactly empty; bounded non-empty `reason` is required;
- top-level extra fields are forbidden. Metric-specific measured fields remain allowed only inside the corresponding check shape.

The schema cannot prove a typed seal or all-pass consistency; the bridge must still do both in code.

### 2. Change only the bridge validation logic

In a new `W24MetricsEvidenceDag.cs` bundle revision, make `ValidateReport` return a private disposition:

```text
MeasuredPass | MeasuredFail | EvidenceInvalid
```

Validation order must be:

1. exact report schema/route shape;
2. exact `inputSha256 == Hash(CanonicalJson(recorderWrittenInput))`;
3. exact `toolSha256 == expectedToolSha256`;
4. typed-binary encoding and self-seal verification over the report without `sealedReportHash`;
5. for `MEASURED`, exact set equality of frozen input check IDs/kinds, no duplicates, every `pass` a Boolean, and `machineGatesPassed == checks.All(pass)`;
6. for `EVIDENCE_INVALID`, false top-level gate, empty checks, and bounded reason.

`RunAndWriteReport` may then call the existing `recorder.WriteMetricsReport(...)` for all three valid dispositions and continue returning only the physical report hash. The private disposition must not be returned as transaction authority or copied into an implementation-trace pass claim.

The following existing protections remain before commit and must not be weakened:

- recorder-written input physical hash is checked before and after child execution;
- frozen required-evidence matrix hash and matrix row domains are checked in `ValidateInputShape`;
- every typed raw artifact is hash/type/provenance checked before copying;
- private copied input/raw bytes are rechecked after child execution;
- the capture-tool bundle canonical hash, exact source hashes, uniquely named metrics tool, and expected tool hash are verified;
- report input/tool identity and typed self-seal are checked before recorder commit.

An `EVIDENCE_INVALID` report generated from a valid parsed input retains the original input hash and is sealable. A CLI-level “invalid input JSON” report hashes `{}` and must remain rejected by the bridge, because the recorder-written input was already strictly parsed and hash-verified; that shape would indicate child/process corruption rather than a legitimate runtime-evidence defect.

### 3. Preserve the authority boundary unchanged

Do not relax `W24S5EvidenceTransition` or `W24S5RecorderCaptureCompletion`:

- transition replay currently requires `route=MEASURED`, `machineGatesPassed=true`, every frozen check passing, exact input/tool identity, and a valid typed seal;
- trace evidence must resolve to a check in `MetricReport.PassingChecks`;
- recorder completion rejects failed/unmeasured machine requirement evidence before it can write `FORMAL_EVIDENCE_BOUND`.

Thus a failed/invalid report may exist in a write-once recorder seal, but it cannot create a completed Trace, transition receipt, candidate advance, or any opaque authority.

The S3 graphics producer will need a separate negative-outcome control path: after a structurally valid failed/invalid report is recorder-committed, call `recorder.Complete()` to seal the capture, skip `FinalizeC0Evidence`, skip completed-Trace construction, and return a non-authority diagnostic outcome to the test harness. The existing all-pass producer path and report assertions remain unchanged.

## Focused vNext test matrix

Bridge unit tests, added with the new bundle revision, must cover:

- measured pass: committed; exact old report bytes/hash unchanged;
- measured fail: committed and sealed; complete frozen ID/kind set; false aggregate; no transition authority;
- evidence invalid from a valid input: committed and sealed; empty checks/reason; no transition authority;
- aggregate/check inconsistency, missing/extra/duplicate check, kind swap, input-hash swap, tool-hash swap, typed-seal tamper, unknown route, invalid-route non-empty checks, and CLI-level `{}` input hash: rejected before recorder write;
- required-matrix hash/row omission and typed-raw path/hash/provenance swap: still rejected before child/report commit;
- existing all-pass formal transition: unchanged and passing;
- measured fail/invalid passed to formal transition: rejected with no completed Trace or transition receipt.

## Re-sign / migration checklist (not executed)

1. Create a new bundle path/version (recommended `w24-s3-capture/3.6`) rather than overwriting the `3.5` bundle.
2. Pin the changed DAG source, new report schema, unchanged metrics-tool hash, producer test changes, and all existing capture sources.
3. Compute the new bundle canonical hash.
4. Create new revisions of the three S3 Contracts (`w24_moving_projectile_trail`, `w24_weapon_socket_fragments`, `w24_real_light_receivers`) with the new bundle path/version/hash; do not mutate historical candidate Contracts.
5. Rebuild only new candidates under the normal C0/C1/C2 transaction rules and recapture in fresh isolated Unity.
6. Run focused bridge/recorder/transition tests, full S1/S3/S5 regressions, and the unchanged all-pass graphics capture before recognizing the new bundle.

## Safe preparation artifacts added now

- `tools/vfx/tests/test_render_metrics.py`
  - focused measured-fail/invalid typed-seal test passed (`1 test`, `OK`);
  - full render-metrics suite passed (`12 tests`, `OK`);
  - SHA-256 after addition: `b675c076d4410c2c348f9387f5c6ac987b3522b8dd8e7b4f69f4759b8b6fd524`.
- `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24MetricsFailureSealingDesignTests.cs`
  - binds the five frozen source pins and documents the current recorder/bridge/transition boundary;
  - SHA-256: `8a98cd3241e1fe51dff4dabeff643e304a2d229067b85ec353a809dd70953a38`;
  - Roslyn static compile: exit `0`, output SHA-256 `609e26434582e845b842566a5689d67728de40e2612ae747c7ac98d36090637c`.

The equivalent source/pin assertions were also executed read-only and passed. Unity was not started. These preparation artifacts grant no machine verdict, candidate transition, Visual QA, user signature, L3, or L4 authority.
