# W24 Phase 2 Unity Worker handle-relative read-query report

Date: 2026-08-26  
Status: `WORKER_TEST_ISSUED_HANDLE_RELATIVE_READ_QUERY_SLICE_SCOPED_GO / PRODUCTION_PROJECT_READ_NO_GO / PHASE_2_GATE_NO_GO`  
Authority: none. This report grants no production registration, connection, project read or write, Worker command, machine/visual verdict, user sign-off, L3, L4 or publication authority.

## 1. Exact scope

This checkpoint adds the first bounded Unity Worker project-document read slice. It is deliberately narrower than the Phase 2 gate:

- the test issues one opaque three-directory-handle lease inside the Unity process;
- the Worker accepts one strict read-query envelope and maps it to one of four fixed repository/project-relative document families;
- all traversal starts from already-pinned directory handles and opens one segment at a time without following reparse points;
- the Worker returns exact bytes, byte length and a typed content hash only on complete success;
- no Desktop, Client or production Broker route reaches this handler;
- no caller path, root, suffix, native handle or Unity API command enters the request;
- the fixture reads only a new GUID-owned scratch repository and removes only that exact tree.

This is not evidence that an authenticated production Broker delivered the handles, that Desktop can query a project, or that existing project content is safe to expose.

## 2. Closed wire and mapping boundary

The shared Protocol now closes the document identifier grammar, three stable path-free diagnostics and exact query/result schema parity. The Worker independently revalidates the same kind/id domain before selecting a target.

| `documentKind` | exact `documentId` domain | pinned root | fixed relative target |
| --- | --- | --- | --- |
| `LIBRARY_INDEX` | `project` only | project root | `ProjectSettings/VFXComposer/LibraryIndex.json` |
| `MANIFEST` | lowercase `[a-z][a-z0-9_-]{0,95}` | project root | `ProjectSettings/VFXComposer/BuildManifests/<id>.manifest.json` |
| `CONTRACT` | lowercase `[a-z][a-z0-9_-]{0,95}` | repository root | `docs/vfx-contracts/<id>.contract.json` |
| `TRACE` | lowercase `[a-z][a-z0-9_-]{0,95}` | repository root | `docs/vfx-traces/<id>.implementation-trace.json` |

The query requires exact protocol/message/request/lease/project/generation/kind/id/hash fields. Strict UTF-8, BOM rejection, decoded duplicate-key rejection, exact required fields, typed project identity and nullable typed expected-content hash are enforced before content I/O. A generation that is not a positive JSON `Int64`, including `9223372036854775808`, is rejected through the fixed protocol exception boundary before any target open.

The result has exactly the protocol/message/request/accepted/project/kind/id/content-hash/byte-length/content-base64/diagnostic fields. It has no path or handle field. Rejections return no content hash, zero byte length and null content with one of:

- `VFXP0007`: project lease rejected;
- `VFXP0008`: document unavailable;
- `VFXP0009`: expected content identity mismatch.

These diagnostics use a fixed catalog and do not echo input, path or content.

## 3. Handle-relative reader and lifecycle boundary

The borrowed-handle reader never owns or disposes the repository/project root handle. From that root it uses `NtOpenFile` with the already-pinned parent as `RootDirectory`, a single validated segment, `OBJ_DONT_REPARSE` and `FILE_OPEN_REPARSE_POINT`. It retains the opened parent chain through the read and post-read replay.

The reader rejects reparse points, wrong directory/file type, volume drift, hard links, identity drift, size over 512 KiB, extra bytes after the declared length and non-exact parent/leaf replay. The leaf is opened read-only with `FILE_NON_DIRECTORY_FILE`; no absolute target open or drive-letter bootstrap occurs on this borrowed-handle path. The lease replays its three root identities before and after the read while holding the same disposal gate, so session revoke waits for an in-flight read and that overlapping query fails closed rather than returning content.

The 512 KiB limit is inclusive. The focused gate accepts an exact 524,288-byte JSON document and rejects 524,289 bytes, invalid UTF-8, valid UTF-8 with invalid JSON syntax, and a decoded duplicate document key.

## 4. Accepted receipts

### 4.1 Final Unity focused gate

Exact filter: `VFXComposer.Tests.EditMode.W24S6WorkerReadOnlyQueryTests`  
Runtime: Unity `2022.3.62f3c1`, Windows EditMode, `-batchmode -nographics`  
PID: `41864`  
Result: `14/14` passed, `0` failed, `0` skipped, `0` inconclusive  
UTC: `2026-08-26 13:46:16Z` to `2026-08-26 13:46:16Z`; XML duration `0.569584` seconds  
Outer exit: `0`

| artifact | SHA-256 | bytes |
| --- | --- | ---: |
| `.codex_tmp/w24_phase2_worker_read_r4/results.xml` | `60ac2b280bc0ae7419ed854ca138a29491703c0cfa0816980391bf15a598be32` | 12,501 |
| `.codex_tmp/w24_phase2_worker_read_r4/unity.log` | `6fd5dfb6a19fe7ef316d195e7d661d888a3aeded69e4fbbad45e6e9e6af215d7` | 100,833 |
| `.codex_tmp/w24_phase2_worker_read_r4/outer-exit.txt` | `777beacfbc12c95266d90eb6e5ac1e412b018bb547d7fb36ac1e0779e018c77a` | 23 |

The log records the exact filter and results path, Input shutdown, licensing disconnects, `Cleanup mono` and the complete `Application.Shutdown` telemetry. After exit: Unity process count `0`, `project/Temp/UnityLockfile` absent, Worker-read scratch count `0`.

Earlier runs are not accepted as the final receipt: r1 was rejected at `12/14`; r2 passed `14/14` before the final source boundary; r3 wrote `14/14` results but stalled after results during shutdown and was force-stopped, so it is explicitly rejected. Only r4 is bound to the final slice.

### 4.2 Shared .NET and schema regression

Release solution build: nine projects, `0` warnings and `0` errors. Release test receipts:

| TRX | SHA-256 | result |
| --- | --- | --- |
| `phase2-worker-read_net8.0_20260826212822.trx` | `fc7299b2ce80c10ddf1bad3c8fe1e0b9598f8515036a6128b6e590e2814c6904` | Protocol `80/80` |
| `phase2-worker-read_net8.0_20260826212823.trx` | `1ed49f59d4d8cf8c3c5f26f26d74b1b112232a939cccff40b2ab60a932dccb6d` | Client `8/8` |
| `phase2-worker-read_net8.0_20260826212824.trx` | `4ea2c5d8ec9d5bcb7792435b0d122afcf2a49a9f985ed21b62501fb52c7ef97a` | Desktop `9/9` |
| `phase2-worker-read_net8.0_20260826212825.trx` | `bcd5a023a57caa9c01674ddfaaf57677c29b509341c38134f562647926240b32` | Broker `22/22` |

Total: `119/119`, zero failed. Draft 2020-12 validation: Phase 1 registry `9` schemas, `42` positive and `68` negative cases passed; Phase 2 registry `19` total/`10` Phase 2 schemas, `11` positive and `144` negative cases passed.

### 4.3 Current Unity static and production-surface compiles

The current Editor and focused EditMode response files compile with the Unity Roslyn toolchain at `0` warnings and `0` errors. The independent no-`UNITY_INCLUDE_TESTS` response file is derived from the current Bee Editor response file by removing only that define and changing output/ref-output; normalized diff count is `0`.

| artifact | SHA-256 | bytes |
| --- | --- | ---: |
| current Bee Editor RSP | `f259bc0cbef5c58c2567d336c202491c9d0feb1f07a197932596a51421a5a483` | 41,210 |
| derived no-tests RSP | `7167bc23a08b4034203394264030647e3437928fd95ad5779241853167d0d087` | 41,742 |
| no-tests Editor DLL | `9e587a94c2561d7689b9023c097c52400316cdaf9a25b6f2b4e5d1f575315da1` | 1,667,072 |
| no-tests Editor PDB | `67cb9d4cd9377e7a7dd3c7f6123b4d4a9a827ecb1c3816f299d11b03a51e6db3` | 402,728 |
| current static Editor DLL | `b92ecc05d9f3714ce6bf0e000ff3afb275f18fcfec01c0c43e02ae759c0d5e4c` | 1,687,040 |
| current static focused-tests DLL | `5568a6a67fdc1f863282b1d0aae2a87801c506224a80e8bed1478ccfef79ffc8` | 1,053,184 |

A metadata scan of the no-tests Editor DLL found zero Worker-namespace matches for `Acknowledgement`, `TestIssuer` or `ForTests`. The read codec/host/handler remain compiled as dormant production code, but there is no production session issuer or transport route that can provide their lease.

## 5. Frozen source set and independent audit

`W24_PHASE2_WORKER_READ_QUERY_SOURCE_MANIFEST.sha256` enumerates exactly 21 changed source/schema/meta files as `<lowercase SHA-256><two spaces><forward-slash repo path><LF>`, sorted with `StringComparer.Ordinal`. Its physical SHA-256 is `451abc8a8cc15fb01b1c96ec2e96df9f59541b851d40e26d7959c47ad5f324b6`; it is 2,904 bytes, 21 LF, zero CR, and independent replay found `0` format/order/missing/hash issues.

The final frozen-byte read-only audit reported `P0=0 / P1=0 / P2=0` and scoped GO only for `WORKER_TEST_ISSUED_HANDLE_RELATIVE_READ_QUERY_SLICE_ONLY`. It independently replayed the strict wire/schema boundary, fixed mapping, handle-relative no-follow traversal, lease/revoke linearization, exact numeric/content boundary repairs, the 21-file manifest, r4 XML/log/outer receipt, four .NET TRXs, both schema suites, no-tests RSP/PE/PDB binding and all six current status documents. It found no evidence rebinding or production/authority scope elevation. The audit did not itself run Unity or a product process; it verified the separately recorded r4 receipt bytes.

STOP-THE-LINE remains intact. No Unity main-window, five-tab or Player UI source was changed. The compatibility baseline hashes remain:

- `VfxStudioWindow.cs`: `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`;
- `VfxStudioModels.cs`: `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`.

## 6. Open blockers

This scoped checkpoint does not close the Phase 2 gate. At minimum, all of the following remain open:

1. trusted production Broker policy/ACL issuer and registered-project ownership;
2. authenticated production Unity connector, session issuer, ACK owner and global process/raw-handle arbitration;
3. Broker-to-Worker read-query framing, bounded backpressure, cancellation and disconnect/restart handling;
4. Client/Desktop project selection and query/result routing without direct project access;
5. an isolated end-to-end gate proving Broker-delivered handles, exact project identity, four successful allow-listed reads, stale/wrong-project/mid-read-drift negatives and zero unauthorized I/O;
6. trusted generation of `LibraryIndex.json`; this slice reads a fixed projection but does not define a production index builder or live `AssetDatabase` scan;
7. production supervisor termination/finalization semantics and installer/service lifecycle;
8. independent frozen-byte audit of the final end-to-end Phase 2 implementation.

Therefore production project read remains fail closed. Worker commands, mutations, Preview/Review, automatic authority, visual acceptance, user verdict, L3 and L4 are not started or granted by this slice.
