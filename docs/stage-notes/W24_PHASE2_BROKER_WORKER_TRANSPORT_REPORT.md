# W24 Phase 2 Broker/Worker lifecycle transport report

Date: 2026-08-26  
Status: `BROKER_WORKER_LIFECYCLE_TRANSPORT_TEST_SCAFFOLD_SCOPED_GO / PRODUCTION_CONNECTION_NO_GO`  
Authority: none. This checkpoint grants no production Broker policy, Worker session/ACK issuer, handle admission, project-content read/write, command, visual verdict, user sign-off, L3 or L4 authority.

## 1. Bounded scope

This delta connects the existing Broker handle-lifecycle state machine to one already authenticated, test-admitted `CurrentUserOnly` named-pipe connection. It remains an internal .NET test scaffold:

- `PeerSessionRegistry` admits at most one live Worker session for an exact observed PID/process epoch. Full revocations are serialized separately from the short-held session-map lock: a session becomes unusable before observers run, its PID/epoch reservation remains until every observer completes, observer failures aggregate without skipping later observers, and registry disposal waits for in-flight revocation. Concurrent same-epoch attempts produce exactly one winner; a replacement is rejected during observer cleanup. A synchronous observer call back into registry `Dispose` is explicitly rejected.
- `NamedPipeBrokerHost` strictly frames and decodes the peer hello, derives SID/PID/process epoch/native image identity from the connected OS pipe, and revokes a session if any post-authentication receipt step fails.
- `AuthenticatedPeerConnection` serializes request/response exchanges. Any partial frame, strict-codec failure, cancellation after exchange admission, stale session or pipe failure closes the pipe and revokes the exact session. Concurrent `DisposeAsync` callers await the same completion task.
- `WorkerHandleLifecycleTransport` sends the already sealed Worker handle grant, accepts only a strict exact grant ACK, begins lease revocation, sends the exact revoke object and accepts only a strict exact revoke ACK. Once revocation begins, failure to construct, send or validate the revoke closes the connection rather than returning a usable session.
- The positive gate uses an actual Windows `CurrentUserOnly` named pipe. The current test process acts as the test Worker, adopts exactly three distinct process-local handle values into one owner, verifies they are live, closes them before emitting the exact revoke ACK, and leaves the Broker lease `Revoked` with no retained Worker handle set.
- The negative gate sends a schema-valid but wrong-grant-hash ACK after closing the test-owned remote values. Broker rejects it, revokes the exact session and leaves the published lease fail-closed in `RevocationPending`.

This is not a Unity Worker connection receipt. The Worker side of these two tests is .NET test code in the Broker test process; it does not invoke Unity, read project content or issue production authority.

## 2. Production state remains closed

`BrokerPolicy.TryLoadProduction(out _)` still returns `false`. The Release Broker observation for this frozen source printed `W24FS001` and exited `23` before constructing `NamedPipeBrokerHost`, parsing a request, opening a project root or starting a listener. Client/Desktop have no reference to the new transport.

The test policy is constructed only by the Broker test assembly. No production session issuer, installed service identity/ACL, global Worker-process ownership coordinator, Unity pipe loop, production ACK owner or supervisor is added by this delta.

## 3. Validation

- Release solution build: nine projects, `0` warnings, `0` errors.
- Release solution tests: Protocol `78/78`, Client `8/8`, Desktop `9/9`, Broker `22/22`; total `117/117`, no failures or skips.
- Focused Broker TRX: `22/22`, `0` failed, `0` skipped; start `2026-08-26T20:02:26.7678155+08:00`, finish `2026-08-26T20:02:27.3573451+08:00`.
- Phase 2 Draft 2020-12 verifier: `19` schemas, `10` Phase 2 schemas, `10` positive and `139` negative cases, status `PASS` under `jsonschema 4.26.0`.
- Release Broker fail-closed observation: `W24FS001`, exit `23`.

The focused TRX SHA-256 is `ef97c84d01c6fe78da3d2661e084181c98d371daf668ecd5919f1cdfaa84a8a5`. It is a local .NET runtime receipt, not an installed-service, cross-process Unity Worker, recovery or project-read receipt.

## 4. Frozen delta identities

The six changed/new source and test files are enumerated in `W24_PHASE2_BROKER_WORKER_TRANSPORT_SOURCE_MANIFEST.sha256`, using `<lowercase SHA-256><two spaces><forward-slash repo path><LF>` and ordinal path order. The manifest is `773` bytes, has six LF-terminated records and SHA-256 `6fd9b12476cffcfabf4a4fa51180a3252a409b9d24f7ba9fe8842e3f76ad031d`.

| File | SHA-256 |
|---|---|
| `services/VFXComposer.Broker/Ipc/NamedPipeBrokerHost.cs` | `14e9882f818706fa81478fcabee18a3a2050175e011c5518c806cda9f9356505` |
| `services/VFXComposer.Broker/Ipc/PeerSessionRegistry.cs` | `b1947ecfe7e0c99a9e035a9d64fee37c17b6c1912593b0170435fa0d45cd94ee` |
| `services/VFXComposer.Broker/Ipc/WorkerHandleLifecycleTransport.cs` | `2b603f633c635d602c112129d36c58a567104aa0985d41e2ca991b5afb8870e8` |
| `services/VFXComposer.Broker.Tests/BrokerAdmissionAndRoutingTests.cs` | `0498b2820d0430d5a16b0fc23371a2aedd4e07bb3db5ee9a07f4d3d0dfbf1af0` |
| `services/VFXComposer.Broker.Tests/NamedPipeScaffoldTests.cs` | `b7682059339e33246f4cf3f3a782deafe8e0726a834d5d31df85859981fc400d` |
| `services/VFXComposer.Broker.Tests/WorkerHandleLifecycleTransportTests.cs` | `7bb97fa93088126fea5bc3ac70ff9197ed5a21416c10ddea272814e6d2c4ed23` |
| Release `VFXComposer.Broker.dll` | `ef7958f749b1d6e59ba4ae71dd008fb85e3adc8c33f34c19c6372f42ca09fe44` |
| Release `VFXComposer.Broker.Tests.dll` | `875a6d18b45fd71c4a16bb2f2e2988a02efb6412ac34c8106c1fa5b0952a77dd` |

The original 66-file Phase 2 foundation manifest remains historical predecessor evidence; this delta manifest does not rewrite it. The Unity Worker protocol and handle-admission manifests remain separate and unchanged.

## 5. Open blockers

1. The real Windows production profile still lacks the host-owned policy issuer, service SID/ACL, install/lifecycle policy and hardened long-lived listener.
2. The Unity Worker has no authenticated production pipe connector or production ACK owner. The test-only .NET Worker loop cannot substitute for it.
3. Only concurrent live-session uniqueness is enforced. A disconnected PID/process epoch is not globally retired while published leases remain unresolved; production replacement requires supervisor-driven exact-process termination and global raw-handle ownership arbitration.
4. Backpressure beyond one serialized exchange, bounded queueing, restart/generation replay, cancellation recovery and service/Worker crash supervision are not complete.
5. No allow-listed Worker content reader or successful Library/Manifest/Contract/Trace query exists. Desktop/Client remain disconnected.
6. DOS-device remap, installer/service-principal and production ACL threat gates remain open.

Independent frozen-byte audit completed on 2026-08-26 with `P0=0 / P1=0 / P2=0` for `BROKER_WORKER_LIFECYCLE_TRANSPORT_TEST_SCAFFOLD_ONLY`. This scoped GO applies only to the six-file manifest, local .NET test-peer transport and receipts recorded here. It grants no Unity/production connection, project real-read/write, Phase 2 gate or authority; blockers 1–6 remain open.

Therefore Phase 2, production connection, project real-read/write and authority remain `NO_GO`.
