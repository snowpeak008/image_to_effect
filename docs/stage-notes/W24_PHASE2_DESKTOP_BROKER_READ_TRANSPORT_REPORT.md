# W24 Phase 2 Desktop-to-Broker read transport checkpoint

Date: 2026-08-26  
Status: `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_SCOPED_GO / PRODUCTION_PROJECT_READ_NO_GO / PHASE_2_GATE_NO_GO`  
Authority: none. This checkpoint grants no production connection, project registration, content access, command/write capability, machine or visual verdict, user sign-off, L3 or L4.

## 1. Exact scope

This slice closes one test-only routing gap between the Phase 1 Client and the existing Broker-to-Worker read scaffold.

- `IVfxComposerConnection` now accepts a typed `ReadDocumentQuery`. `ReadOnlyProjectQueryClient` builds that query only from a `ProjectLeaseDescriptor`, fixed document kind/id, optional typed content hash and request correlation. It accepts no pipe name, absolute path, project root, filename or caller result path.
- `VfxComposerClient` exposes the same identity-only read. The default production factory remains `CreateDisconnected`; `DisconnectedVfxComposerConnection` returns a closed-catalog `VFXP0001` rejection with no bytes.
- Client replay-checks request id, project identity, document kind/id and any expected accepted-content hash before returning a result. A cross-request or cross-content result throws a fixed, non-input-derived error.
- `NamedPipeBrokerHost` can hold two authenticated `CurrentUserOnly` test connections, one Desktop and one Worker. `AuthenticatedPeerConnection.ExclusiveExchange` gained a receive-and-reply operation that shares the existing per-connection exchange gate and closes the session on framing, codec, callback or cancellation failure.
- `DesktopReadQueryTransport` accepts only an already authenticated Desktop connection whose opaque session is the exact session held by the opaque lease. It decodes one strict query, calls the existing Broker identity router and Worker transport, then reserves the exact Desktop connection, Worker connection, both authenticated sessions and the acknowledged lease through the actual response write and flush. Registration, Desktop-session, Worker-session or either connection revocation wins before reservation and produces no response; reservation wins first and the corresponding revocation waits until publication completes. It never opens, enumerates or parses project content.
- The Desktop transport admits one active serve operation. A concurrent second serve does not queue: it closes the Desktop connection and both calls fail closed.

The only pipe-capable Client implementation in this checkpoint is the private `PipeClientConnection` inside `VFXComposer.Broker.Tests`. The shipped Client assembly still contains no `System.IO`, `System.Net`, named-pipe, listener, project-path or Unity API reference. No Desktop view or view-model was changed or launched.

## 2. Accepted and rejected evidence

The accepted Release r5 receipt is `.codex_tmp/w24_phase2_desktop_broker_read_r5/`:

| Test assembly | Passed | Failed | Skipped | TRX SHA-256 |
|---|---:|---:|---:|---|
| Protocol | 80 | 0 | 0 | `e7558f1676f33c2c6b5977e52f306455bdd49270b5e11883b317256d7b36cb84` |
| Client | 12 | 0 | 0 | `8bdcce7d943f91aa0124e4b597ad8b13cf43808a958121687f06363e51c630c8` |
| Desktop | 9 | 0 | 0 | `b7eafd8057204d19191a0f4507f0b3c4d24b39c1f15a832bb499271bad3cbad8` |
| Broker | 35 | 0 | 0 | `44cab2f8e1086d12574504727634d4897eab4663867d8f2d84e75a9972765bb0` |
| Total | 136 | 0 | 0 | — |

The nine-project Release solution build completed with `0` warnings and `0` errors; its log SHA-256 is `ee2cfbc7d8ef90a49aab5923de348ffbcba51f0a053a7aa7a8d70c179d0b3ae8`. The current Release Broker was also invoked directly and returned stderr `W24FS001`, exit `23` before listener construction. The smoke JSON and stderr hashes are `d697ca91eae42c52aecac91180a321c572986910c32d2a5a078ee157827be9ab` and `4b454001a339ecf5b4a87634c58db53b458adaecafa4347c65088c6001cf4f06`.

The new Broker cases prove:

1. a Client-built Manifest query crosses a real authenticated Desktop pipe, is routed through the existing authenticated Worker pipe, returns exact content and then completes ordered revoke;
2. malformed Desktop JSON closes only the Desktop route before a Worker query;
3. an unknown lease closes the Desktop route before any Worker query;
4. a concurrent second Desktop serve is rejected without an unbounded waiter queue;
5. registration revoke waits for an exact response already in publication and only then enters ordered Worker revoke;
6. Desktop connection disposal, direct Desktop-session revoke, direct Worker-session revoke and Worker connection disposal all wait for the exact in-flight response publication; after it drains, the relevant route is unusable and no stale follow-up is accepted;
7. a deterministic barrier between session reservation and store replay proves that session removal invalidates the route without deadlock or stale publication;
8. existing Worker wrong-correlation and read/revoke exchange-order gates remain green.

The new Client cases prove disconnected no-content rejection, cancellation-before-result, cross-request rejection and unexpected accepted-content-hash rejection. The Desktop assembly-level IL gate still reports no direct project I/O, listener, pipe, network or Unity surface.

The first attempted full receipt, r1, is rejected: the pre-existing exact-process-exit test observed the duplicated kernel process object one scheduling interval before it became signaled, producing Broker 28/29. The test now polls the same pinned kernel object for at most one second after `WaitForExitAsync`; it does not accept PID reuse or a different process. The interrupted parallel run left one empty GUID test tree containing only `repository/project`; after verifying exact Temp containment, empty directories and no reparse points, it was removed bottom-up non-recursively.

The formerly accepted r2 receipt is also rejected by the independent review that followed it. That review found a stale-response window between the handler's last route replay and the actual Desktop pipe write, plus an unsafe recursive cleanup in the changed cross-process test.

r3 is rejected by the next independent delta review. Although it added response reservations, the acquisition order could hold a session reservation while waiting for the peer-registry gate, while session revoke held that registry gate and waited for the same reservation. Its cleanup also inspected and deleted child-first, so a pre-existing ancestor junction could be discovered only after an external empty child had been touched. r4 closed the lock inversion, but its managed path verification still left a concurrent ancestor-swap window before `Directory.Delete`; it also compiled the deterministic barrier hook into Broker and could skip later cleanup after an earlier dispose exception. r4 is therefore rejected.

r5 keeps the two-stage session invalidation/drain and removes the production hook: the test now acquires the real connection/session reservations directly before store replay. Cleanup no longer calls `Directory.Delete`. A test-only native helper pins the physical scratch root, opens repository/project one segment at a time through the pinned parent with `OBJ_DONT_REPARSE` and `FILE_OPEN_REPARSE_POINT`, verifies exact final DOS paths and child sets, then applies delete disposition to the pinned handles bottom-up. Fixture cleanup attempts every connection/client/store/session/scratch action and aggregates failures only after all actions run. r5 ended with zero HandleProbe process, zero Unity Editor process and zero matching scratch residue.

No Unity process or application Desktop process was started for this slice. r11 remains predecessor evidence for the separate Broker-to-actual-Unity test route and is not rebound to these Client/Broker bytes.

## 3. Frozen identities

The seventeen changed source/test/project files are enumerated by `W24_PHASE2_DESKTOP_BROKER_READ_SOURCE_MANIFEST.sha256` as lowercase SHA-256 plus two spaces plus forward-slash repository path plus LF, sorted with `StringComparer.Ordinal`. Pre-audit replay found 17/17 matches, 2,143 bytes, 17 LF, 0 CR and manifest SHA-256 `5f05a77e1530e5af7e35c874118d3a09604bb5d2a404d52a622235d7244afca5`.

Current Release binaries:

| Binary | SHA-256 |
|---|---|
| `VFXComposer.Protocol.dll` | `e14355a16b1f5a58503caea8f7bd01d53458fd28066b084d827f89f69374b8a2` |
| `VFXComposer.Client.dll` | `8fa006ef0e87d3ce71097f0845ba9a5fde4d4ed8b3de030ce23d2d8615787687` |
| `VFXComposer.Desktop.dll` | `1970acdd3af5743e19315f9544d7455c3c0c507789b825180014693f92155c58` |
| `VFXComposer.Broker.dll` | `5eebd88880c4aab40ccb87f3a00e87fe21e696fa38147c21b88935ab2cc37a49` |
| `VFXComposer.Broker.Tests.dll` | `d1ca608f046fceedaefd198330010928988a1838301dba95e096f23bd212df5f` |

## 4. Remaining blockers

1. No production Broker policy/service/ACL issuer exists; `W24FS001` remains the first production outcome.
2. The production Client has no named-pipe connector or authenticated session owner. The test-side pipe adapter is not product code and cannot be promoted by reflection or copying its test policy.
3. No Broker project enumeration/selection response or production opaque lease acquisition path reaches Client/Desktop.
4. The production Unity connector, session/ACK issuer, global process/raw-handle ownership arbitration and Worker supervisor remain absent.
5. The current one-request serve primitive is fail-closed and bounded, but a production multi-request loop, bounded backpressure policy, cancellation recovery, disconnect/reconnect and Broker-generation replay are not implemented.
6. No real registered Unity project or production Library/Manifest/Contract/Trace read was performed. Scratch/test-issued receipts cannot fill that gap.
7. Desktop UI remains deliberately disconnected and unchanged. It has not displayed these results, and this checkpoint is not pixel, visual QA, Preview, Review or accessibility evidence.
8. Commands, mutations, jobs, evidence authority and L3/L4 remain out of scope.
9. Independent frozen-byte audit completed on 2026-08-26: `P0=0 / P1=0 / P2=0` for `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY`. This scoped GO grants no production project read, Phase 2 gate, Desktop integration, commands or authority; blockers 1–8 remain open.

Therefore this checkpoint does not complete Phase 2 and does not open production project read.
