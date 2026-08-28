# W24 Phase 2 Unity Worker connection checkpoint

Status: `UNITY_WORKER_TEST_PIPE_LIFECYCLE_SCAFFOLD_SCOPED_GO / PRODUCTION_CONNECTION_NO_GO`

Authority: none. This checkpoint grants no production Broker or Worker session, project-content read/write, command, machine/visual verdict, user sign-off, L3, L4 or publication authority.

## 1. Exact scope

This slice connects the real Unity 2022.3 Editor process to the existing non-publishable `.NET 8` Broker HandleProbe over one local `CurrentUserOnly` named pipe. The test-only Unity connector performs exact frame decoding, peer hello/session correlation, grant decode, opaque three-handle admission, grant ACK, revoke decode, handle close and revoke ACK.

The Broker still independently observes the connected Unity process PID, process epoch, image bytes and user SID through the pipe. The HandleProbe additionally compares the accepted session to the exact Unity PID/epoch/image it pinned before publishing any registration or handles. The helper-supplied image digest is only the test hello claim; it is not a production Worker image-identity issuer.

The in-process Desktop session used by the helper exists only to prepare a test lease. It is not a Desktop transport receipt. The scratch project contains no content and is removed by exact, non-recursive cleanup after the handle lifecycle.

The complete Unity connector source is wrapped in `#if UNITY_INCLUDE_TESTS`. Production `VFXComposer.Editor` contains no connector, pipe client, peer handshake model, ACK encoder or test session issuer. The HandleProbe remains `IsPublishable=false`, is referenced by no production Broker project, and gains access to Broker internals only as a separately named test assembly.

## 2. Fail-closed behavior

- Unity can connect only to server `.` with a bounded token pipe name; there is no host, TCP, HTTP, stdio MCP or caller project path surface.
- Peer session receipts require the exact nine-field document, protocol/kind/request/role/process epoch, positive generation and the exact four sorted capabilities. Duplicate-decoded keys, unknown/missing fields, changed correlation, changed epoch and reordered capability sets fail with fixed `W24WKR003` and do not echo input.
- Grant/revoke payloads continue through the already accepted strict codec and typed self-hashes. A grant ACK is sent only after opaque lease admission and native identity replay. A revoke ACK is sent only after lease disposal reports both `IsAttached=false` and `IsUsable=false`.
- The Broker test host verifies that the accepted pipe session is the exact requested Unity PID/process epoch/image before registration. Any registration, lease, grant, ACK, revoke or cleanup failure returns a fixed helper exit code and no `PASS` receipt.
- The helper creates only an invocation-owned empty `repository/project` tree beneath a GUID temp directory. Cleanup rejects reparse points and removes the three known empty directories non-recursively. The accepted run leaves zero matching temp directories and no Unity/HandleProbe process.
- Production Broker behavior is unchanged: `BrokerPolicy.TryLoadProduction` fails before listener creation and the current Release executable returns `W24FS001`, exit `23`.

## 3. Accepted runtime evidence

The accepted exact Unity filter is `VFXComposer.Tests.EditMode.W24S6WorkerBrokerSessionTests`. The final r6 rerun moves the helper's `PASS` emission after connection, registration/session and scratch cleanup; all four cleanup stages are attempted and any collected exception suppresses `PASS` and fails the helper.

| Receipt | PID | Result | UTC interval / duration | SHA-256 |
|---|---:|---:|---|---|
| r6 XML | 38668 | 2/2 passed, 0 failed/skipped/inconclusive | 13:01:15Z–13:01:16Z / 0.4478075s | `5fcccad7507dd27c862dce0ea2fbc6486976f4096508c75850e119179baa8784` |
| r6 log | 38668 | exact filter, result write, Input shutdown, 3 licensing disconnects, `Cleanup mono`, `Application.Shutdown.CleanupMono`; no fatal/compile/crash marker | natural shutdown | `962a431bd17ae2215737d4cc3ac594fbd9f7d951dddcec46cbffd63d6c8e4e1f` |
| r6 outer receipt | 38668 | exit 0 | observed runner result | `8f257de628278c83b16309018243715eab590cb9174f1a2a814531c59272cc65` |

r1 is rejected: its protocol-negative case passed, but Unity Test Runner marked the original `async Task` lifecycle method NotRunnable and the process did not naturally exit. r2 passed 2/2 and naturally exited; r3 added the exact requested-PID/epoch/image assertion; r4 first moved `PASS` behind normal cleanup. r5 is also rejected: its tests wrote a passing XML, but Unity stalled after licensing disconnect, never reached `Cleanup mono`, and was terminated only after a five-minute bound following exact PID/executable/command-line verification; its zero-byte stale `UnityLockfile` was then removed. r6 supersedes all of them after cleanup exceptions were aggregated and the process completed a natural exit.

Current-source regression filters also passed naturally:

| Filter | Result | XML SHA-256 | log SHA-256 | outer SHA-256 |
|---|---:|---|---|---|
| `W24S6WorkerHandleAdmissionTests` | 13/13 | `2d3e8c1dab7ddcd3a1cf80a671a3a60a321c16babd1295c9aa07af9524c7b59a` | `b5294dce6fa1cf06c3a1978aa6037c7962afd8298f3599cc59e437ce03d0afc8` | `393de2e5534bfe8a1a708d64397ddf4fbdd0230f50b3e869bcc4607df9b2008b` |
| `W24S6WorkerProtocolTests` | 6/6 | `079b6f9339dc59937c062128fbf7b4946d1bb9653ef346ef26b60a5b9ca8068a` | `e499262e4868c8c2924a9b2006f92a0c472a9317f9c921b053daa388ba15ac9e` | `b445cd1a6b2152234f5cbcb0e03d6481019787ecbf015ddd6cd87b5a56aad8fa` |

The final Release solution build completed all nine projects with zero warnings/errors. Final Release tests passed `117/117`: Protocol 78, Client 8, Desktop 9 and Broker 22. The four TRX hashes are respectively `3012394f5596e639a2a12b4830b9a55d53a4a4b76f626d4e18c01da39e351d5c`, `1ec4e38a1363c6d08dc1b413689cca4834c9b9d85b3c11db30dd9799d13c2b34`, `6b6efae757009702e774868ddcb15f7df7c55df1e72f65a9c3d6e4e8cf9deabe` and `c780469b7a0c8e578e2c54ed8ed9dca121e6afea220240411601fa19e0fe98a6` for the 78/8/9/22-test assemblies. Draft 2020-12 Phase 2 verification remains `19 schemas / 10 Phase 2 / 10 positive / 139 negative`, all passing.

## 4. Production-surface compile proof

The current Unity Bee Editor RSP SHA-256 is `18a8a865d9b1bb51ce61245786c42d95c001b5a2025f723adee43d115892fbe5`. The derived RSP removes only `-define:UNITY_INCLUDE_TESTS` and redirects output/refout; exact transformation issues are zero.

The no-tests compile receipt SHA-256 is `43a5b87effde242b8361dffab8aa8174ed55db77332ab45e5ce2b473581d7bc7`. It binds derived RSP `0b751238212ecb5805452dc21d36f9091656950d28f1b8e37ca75a075565ee62`, Editor DLL `bb9527a744a15595dcfdb3182fd906cb24156b6e2350f120a873b2f9ec04495c`, ref DLL `1c83233d4125d5eb1cb2235989cd0c2789cf4b57d46c2496faf82206c1071e89`, PDB `2f62f1f54ad801da43c787554085972ab0be9b5ddbaee11309ea8d2b21d29404`, empty compiler stdout/stderr and metadata audit `9f3d6b5b520fb8edda951e52180ae135ba37c103f0a893b0a01248342ee89d06`.

The metadata audit reports zero `Acknowledgement`, `TestIssuer` or `ForTests` matches in the Worker namespace. Because the connector contains `RunLifecycleForTests`, this also proves that the entire connector type was removed. A separate binary string scan reports zero `W24S6WorkerBrokerConnection`, `NamedPipeClientStream`, `PeerHandshakeCodec`, `CreateGrantAcknowledgementForTests` or `IssueForTests` matches.

## 5. Source and binary identity

The exact nine-file delta is enumerated in `W24_PHASE2_UNITY_WORKER_CONNECTION_SOURCE_MANIFEST.sha256`: 1,271 bytes, nine LF, zero CR, `StringComparer.Ordinal` path order, physical SHA-256 `f474653572b315b0bbbce70fc55bddc6d992bd350fb5f96046791462333a2ddd`.

Key current binaries:

- `VFXComposer.Broker.HandleProbe.exe`: `69358226e2a01352c8889caa148a2e192a295c9c458872e69b38be34541351e5`
- `VFXComposer.Broker.HandleProbe.dll`: `298bc9974ae3b75adf0bfbadb4ab6d663b6eb071c2f5749c7945a109e54192d3`
- `VFXComposer.Broker.dll`: `8a91ee4a4d5cc3bb4f943c3d83cf6967d04037ebea0a622e13119c4aa64540b1`
- `VFXComposer.Broker.Tests.dll`: `f0469949fc311517d43e0b8c553ea3bdf74596130e48e73c9158fcc74c017329`

The production Broker receipt has empty stdout, stderr containing only `W24FS001`, and exit `23`; their SHA-256 values are `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`, `4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06` and `cfd0ed3d0eacfb943f12abd8d9f0025cc81f1b20d8fa3506b2f74137b6473752`.

The old Unity UI remains unchanged: `VfxStudioWindow.cs` is `0faceac1a6987c89b987ec9fcb25adc01d43e0c19feb4c4ab7692dc433c26587`; `VfxStudioModels.cs` is `cb7ae96848e47fdbc635eaee234199e10ccee3af609fa5a317a3ba994c529d68`. No old UI receipt is rebound to this Worker connection.

## 6. Remaining blockers

1. No production Broker policy/ACL/service issuer exists; production still returns `W24FS001` before a listener.
2. Unity still has no production peer-hello image claim issuer, authenticated session issuer or ACK owner. The test helper supplies the claimed image digest after OS observation, and every related Unity type is compiled out of production.
3. No production supervisor owns exact Worker startup, restart, termination, backpressure, cancellation or unresolved published-handle recovery.
4. The test host uses an in-process Desktop session; no Desktop↔Broker production transport is proven.
5. No allow-listed Library/Manifest/Contract/Trace content reader or successful content query exists. This gate opens no content read or write.
6. HandleProbe must remain excluded from installation/publish outputs; its `InternalsVisibleTo` and reflection-created policy are test-scaffold mechanisms, not a trusted host design.
7. Independent frozen-byte audit completed on 2026-08-26: `P0=0 / P1=0 / P2=0` for `UNITY_WORKER_TEST_PIPE_LIFECYCLE_SCAFFOLD_ONLY`. This scoped GO grants no production connector, session or ACK issuer, project read/write, Worker command, Phase 2 gate or authority; blockers 1-6 remain open.

Therefore Phase 2 remains incomplete and production connection, project read/write, Worker commands, visual authority and L3/L4 stay fail closed.
