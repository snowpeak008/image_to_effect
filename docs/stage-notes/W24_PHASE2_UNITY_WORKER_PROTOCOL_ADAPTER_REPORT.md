# W24 Phase 2 Unity Worker protocol adapter report

Date: 2026-08-26  
Status: `UNITY_WORKER_GRANT_REVOKE_CODEC_COMPATIBILITY_SCOPED_GO / HANDLE_ADMISSION_PENDING / PRODUCTION_CONNECTION_NO_GO`  
Authority: none. This report grants no production registration, native-handle admission, project read, transport, Worker command, write, machine/visual verdict, user sign-off, L3 or L4.

## 1. Delivered boundary

This slice adds the first Unity Worker code under the directory frozen by ADR-003. It does not extend `VfxStudioWindow`, the legacy five-tab UI or any Player UI.

- `W24S6WorkerProtocolCodec` admits only Worker handle grants and revocations in its production surface. It enforces a 64 KiB byte cap, strict UTF-8 without BOM, bounded strict JSON parsing, duplicate decoded-key rejection, exact top-level and nested fields, exact constants/types, positive integral generations, canonical opaque handle text, typed-hash tags and physical self-hashes.
- Canonical self-hash bytes match the .NET 8 `VFXComposer.Protocol` encoding: UTF-8 property-name ordering, exact integral numbers and the `vfxcomposer.typed-sha256.length-prefixed/1` domain/length framing.
- Four shared lifecycle golden vectors are stored once as Base64 of exact UTF-8 JSON bytes. The .NET codec decodes all four and pins byte length, physical SHA-256, self-hash and grant/revoke linkage. Unity decodes the same four bytes and reproduces both ACK byte sequences exactly.
- All ACK-specific models, constants, field tables, private parsers, sealing helpers and declared byte entrypoints are compiled only under `UNITY_INCLUDE_TESTS`; the declared entrypoints are named `ForTests`. The production codec cannot parse or emit an ACK, or emit `GRANT_ACCEPTED` merely because a grant parsed. A future handle-admission owner must first convert and pin the exact handles; revoke ACK must likewise require proof that the exact handles were closed.
- The adapter has no `IntPtr`, `SafeHandle`, file/directory API, named pipe, socket, HTTP/TCP, Unity API, `AssetDatabase`, `EditorPrefs`, environment-variable, authority or project-path surface. It does not open, close, duplicate or use a native handle.

This is protocol compatibility evidence only. It does not close the authenticated Worker pipe, native-handle ownership or read-only query blockers.

## 2. Validation

The current .NET Release solution builds all nine projects with zero warnings and zero errors. Current Release tests are:

| Fixture | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Protocol | 78 | 0 | 0 |
| Client | 8 | 0 | 0 |
| Desktop | 9 | 0 | 0 |
| Broker | 14 | 0 | 0 |
| Total | 109 | 0 | 0 |

The Phase 2 Draft 2020-12 schema verifier remains `19 total / 10 Phase 2 / 10 positive / 139 negative`, PASS.

Unity 2022.3.62f3c1 was run with exact filter
`VFXComposer.Tests.EditMode.W24S6WorkerProtocolTests` in batchmode and nographics. The final r40 process was PID `11360`, observed outer exit `0`. XML reports `6/6` passed, zero failed/skipped/inconclusive, UTC `09:47:19Z`, duration `0.1055785` seconds. The log records the exact filter, result persistence, Input shutdown, both licensing disconnects, `Cleanup mono` and `Application.Shutdown.CleanupMono`; no compilation error, fatal or crash marker was found.

A second compile used the current Unity Editor Bee response file but removed only the exact `-define:UNITY_INCLUDE_TESTS` line and redirected its output. It exited `0` with empty stdout/stderr. A metadata-reader pass over the resulting DLL found zero ACK-specific types, fields or methods. The production `W24S6WorkerProtocolCodec` method set contains `DecodeGrant`, `DecodeRevoke`, their two private projectors and only shared validation/canonical-hash helpers; it contains no ACK model/parser, `Seal`, `WriteTypedHash` or `MatchesGrant`.

| Artifact | SHA-256 |
|---|---|
| r40 XML | `1ffba970741cd17e90ee38ec10eb96c102b40af373165461f377986593216e5e` |
| r40 log | `eed8625a2018249c8109ac9109801e3af3a273f68544cc8933045f82360d520c` |
| r40 outer-exit receipt | `e0c93a86b97505e9e6490412d6d6b1d52054e738c997c7b21ed265f0a07f9ace` |
| no-`UNITY_INCLUDE_TESTS` compile receipt | `6d9de895db316b8b54525c95bfc7ff18c21bbdd9efc25c53dcd75c61e3737cfd` |
| no-`UNITY_INCLUDE_TESTS` Editor DLL | `cd2ca3c7b75dfa0642532a5920ec79f81396e113e752e86c617f8a247b3e3060` |
| no-`UNITY_INCLUDE_TESTS` response file | `9c25550c2d5fd4ece55ad66824cd5b1b6579307bfa2586920ecb9a8f443e8e6b` |
| Release `VFXComposer.Protocol.dll` | `6c3992663030a103b66a949b0bc83910222590f6cb6290931aac82cac63d3422` |
| Release `VFXComposer.Protocol.Tests.dll` | `bcd54d081cf944806217b09dfa9bf94aeae010beca5ea05a7867ce1236b68561` |

The earlier r37 and r39 runs also passed 6/6 and naturally shut down, but r40 supersedes them because r40 binds the final byte-level production/test ACK boundary and a captured parent-process exit code.

## 3. Frozen source set

`W24_PHASE2_WORKER_PROTOCOL_SOURCE_MANIFEST.sha256` enumerates the exact 11 source/meta/vector files as lowercase SHA-256 plus forward-slash repo path, Ordinal sorted with LF endings. It independently replays with zero mismatch. Its physical/aggregate SHA-256 is:

`cfa7b25757a6566fa637a6a6515e7e6f6866f4d672b4e644db6a878c234e3a77`

Key source hashes:

| File | SHA-256 |
|---|---|
| `W24S6WorkerProtocolCodec.cs` | `83f82d4beae3fe31c318ab66177010052039e9a3152186749a081ec5720aee44` |
| `W24S6WorkerProtocolModels.cs` | `ff8b74c1e07d549b2c1411f9cc6e6f6fb0bdbfc0275faf647589551c676f7ff7` |
| `W24S6WorkerProtocolTests.cs` | `5141872a6e61e14c7c811bc01bb338e6453114ef028f3a070a4b15427bf7b40f` |
| shared golden vectors | `4effaa542628a0d8f53e62b90b1d743a5d810fb714a4b753622ffb8b9c8793bf` |
| .NET golden-vector tests | `0b0145ca64c9b6d238f0c7d8177b5b7c29329ad00586fa4e690828bbbfe6dd2e` |

Frozen Unity UI baselines remain unchanged: `VfxStudioWindow.cs` is `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`; `VfxStudioModels.cs` is `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`. No old UI receipt is rebound to this Worker evidence.

## 4. Remaining blockers

1. Production ACK issuance is intentionally absent. A private, opaque Worker handle-admission/ownership object must prove all three exact handles were safely adopted before grant ACK, and must prove exact close before revoke ACK.
2. The authenticated Worker named-pipe loop, peer-session admission and ordered transport delivery are absent.
3. The Unity Worker has no handle-relative, no-follow project document reader and no allow-listed Library/Manifest/Contract/Trace query handler.
4. Client/Desktop remain disconnected and cannot select a registered Broker project or issue a read query.
5. Production Broker policy/service issuer, SID/ACL/installer profile, Worker supervision, restart, backpressure and recovery gates remain open.
6. Independent frozen-byte audit completed on 2026-08-26: `P0=0 / P1=0 / P2=0` for `UNITY_WORKER_GRANT_REVOKE_CODEC_COMPATIBILITY_ONLY`. This scoped adapter GO grants no production ACK, handle admission, transport, project read/write, Phase 2 gate or authority; blockers 1-5 remain open.

Therefore Phase 2 and all production connection/read/write/authority surfaces remain fail closed.
