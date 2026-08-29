# W24 Standalone Desktop Phase 2 foundation report

> **CURRENT CLOSEOUT — U6 FINAL GO / A0–A3 CLOSED / A4 SOLE ACTIVE (2026-08-29).** ADR-005's ordinary-user Phase-2 route is complete at `100/100`; ADR-006 defines the separate post-U6 AI chain with user-owned opaque endpoints. A2 and A3 are final GO, and only A4 Desktop wiring is active. The pre-U0 report below remains historical evidence only; its privileged route and blockers are not current delivery dependencies.
>
> Normative tokens: `USER_MODE_LOCAL_CREATIVE_TOOL_V1` and `AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`.

## U6 final audit closeout, A2/A3 final acceptance, and A4 publication report

The independent receipt `u6-independent-final-audit-20260828T232640380Z` closes U6 at `FINAL GO — P0/P1/P2=0/0/0`. It reports `passed: true`, a stable `16607`-entry source manifest with SHA-256 `592bfeaab629e8cb9b100cf82fd3ce95c5be23972742501be34e57f1908a2284`, frozen-root replay `0` mismatches, and empty point-in-time runtime-process, VFX Composer named-pipe, and owned LocalE2E temporary-root residue. The USER_MODE main architecture is therefore `CLOSED — 100/100`; this conclusion is limited to ADR-005's local ordinary-user route.

Default Broker behavior is unchanged by this closeout: no-argument launch writes only `W24FS001` to stderr and exits `23`. U6 is closed and no longer schedules repairs or source work. The earlier failed U6 publication checkpoint remains historical provenance only and is superseded by this accepted independent final receipt.

In the same post-U6 chain, A0 is `CLOSED — AI_PROVIDER_TWO_CHANNEL_ROUTING` and A1 is `CLOSED — FINAL ACCEPTED — GO` at `698e770a35062cc4135872147a401dce40adcb51`. A2 `WP-AI-CHAT-CHANNEL` is `CLOSED — FINAL GO — P0/P1/P2=0/0/0` at accepted source `55ee0993f71375ee0245cbee54815e7988fe04fd` plus redirect-boundary correction `2678cb62be9ac9ff5a05c9a5b605a75c60effb5c`; its final tests are `23/23 × 3`. A3 `WP-AI-IMAGE-CHANNEL` is `CLOSED — FINAL GO — P0/P1/P2=0/0/0` at accepted source `c7c4adcfcc80c732bfaf87b0dfea11294b4af741` plus redirect-boundary correction `12b58ac69efe3175cf49a6ee129b3784b5b3da5c`; its final tests are `20/20`. The shared closeout records a Release solution build at `0 warnings / 0 errors`. This closes the redirect-quality finding but is not live-provider, paid-image, UI, project, Broker, Worker, Unity, or cross-channel E2E evidence.

The formal new DAG remains `A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`, but A4 `AI_DESKTOP_WIRING` is now the sole `ACTIVE` package. A5 `AI_MOCK_E2E` and A6 `AI_INDEPENDENT_FINAL_AUDIT` are `NOT STARTED`; A5 is the sole owner of mock-handler cross-channel E2E. A4 may consume A2/A3 outputs but cannot reopen their closed channel implementation except the listed Chat integration overlays.

ADR-006 freezes channel routing: all LLM/conversation work uses one explicit `ChatLlm` binding and all image generation uses one explicit `ImageGeneration` binding. Each is exactly one explicit profile/capability/model; sharing a profile is permitted only through separate capabilities and separately displayed bindings. Origin (`Official`, `Relay`, `Friend`, `Subscription`, `Custom`) is metadata only. Missing, invalid, cross-channel, or failed state is fail-closed, with no fallback. `OpaqueEndpoint` is saved and resolved exactly as user text; local configuration acceptance is never request authorization.

A4's new AI roots are exactly `src/VFXComposer.AI.Contracts/Desktop/**`, `src/VFXComposer.AI.Providers/Desktop/**`, and `src/VFXComposer.AI.Tests/Desktop/**`. Its allowed existing AI overlays are `ProviderConfigurationResolver.cs`, `ProviderSecretStore.cs`, `ChatRouteResolver.cs`, `ChatChannelGateway.cs`, `AI.Tests/Chat/**`, and `ProviderSafetySurfaceTests.cs`. Its Desktop scope is the Desktop csproj/lock, `App.axaml.cs`, MainWindow/Create/Settings/Preview view models, Create/Settings/Preview views, the new `Services/PrivateImagePreviewDecoder.cs`, the Desktop test csproj/lock plus new `AiDesktopIntegrationTests.cs` and `NoProjectAccessSurfaceTests.cs`, and the phase gate runner/baseline. The exact all-and-only paths are normative in ADR-006, the current Phase Plan, and the work-package registry.

A4 must STOP, rather than widen scope, for any Client, Broker, Worker, Unity, project, solution, `OpenAiCompatibleImageGateway`, or `MainWindow.axaml` change. It must keep save/start/Create-Settings-Preview navigation at zero network: no endpoint probe/DNS/HTTP client/health/credential refresh/image download/paid request. Health begins `Unknown` and does not block an explicit prompt; that prompt is the only first request and records health from its own result. Image never receives an automatic health or paid request. A real Image request requires a distinct explicit user action.

The frozen operational boundary keeps `SecretRef`/DPAPI CurrentUser as the formal key store, with no secret read-back. A changed/new secret-bearing binding requires explicit re-entry; explicit revoke removes its selected secret reference and leaves the route fail-closed until re-entry. There is no cross-profile credential recovery or routing fallback. Raw endpoint/query/user-info, secret/ref payload, auth, prompt, raw request/response, base64/image bytes remain excluded from normal UI, logs, exceptions, receipts, telemetry, cache keys, and default export. Only a deliberate edit interaction may show the endpoint text.

`PrivateImagePreviewDecoder` is the only Desktop stream exception. It consumes only a provider-issued `Stream`, converts it into an in-memory Avalonia `Bitmap`, and closes it immediately on every completion path. It may not use `File`, `Directory`, `Path`, `FileStream`, `Environment`, `System.Net`, a project path, or Unity API. Images remain private/untrusted and are never automatically written to Unity, `Assets`, recipes, patches, or a project. A4's focused tests must cover UI/secret/revoke/redaction/no-fallback/zero-auto-network/stream-close/no-project-access controls; it must stop before mock-handler cross-channel E2E, which belongs to A5.

---

## Historical pre-U0 Phase-2 report (superseded for product delivery)

Date: 2026-08-26; production-read architecture freeze update: 2026-08-28  
Status: `PHASE_2_FOUNDATION_IN_PROGRESS / PRODUCTION_CONNECTION_NO_GO / BROKER_TO_UNITY_TEST_READ_SCOPED_GO / PRODUCTION_READ_DAG_REBASE_STOPPED_REMEDIATION_COMPLETE_FRESH_INDEPENDENT_AUDIT_PENDING`  
Authority: none. This report grants no production registration, project-content read, Worker command, project write, machine/visual verdict, user sign-off, L3, L4, Publication or transport authority.

## 1. Scope delivered

This checkpoint is a bounded security foundation, not the Phase 2 gate.

- The .NET protocol registry now contains ten Phase 2 exact message kinds: peer hello/session receipt, project registration attestation, Desktop-visible lease descriptor, Worker-only handle grant/grant-ACK/revoke/revoke-ACK, read query and read result.
- All ten have Draft 2020-12 schemas. Persisted registration/lease and every handle-lifecycle object carry typed self-hashes; unknown/missing/wrong-type/version/kind/caller-path forms fail closed.
- `VFXComposer.Broker` exists as a .NET 8 process, but its production policy loader always returns false. The executable prints `W24FS001` and exits `23` before constructing any listener.
- The test-admitted named-pipe scaffold uses `CurrentUserOnly`; peer facts are read from the connected OS pipe/process, not caller JSON. The Broker pins a process object, creation epoch, raw SID identity and exact native-image typed hash before issuing an opaque local session.
- Host-owned registration definitions accept only a global volume GUID root plus bounded single path segments. Broker pins NTFS volume/repository/project directory identities using no-follow relative `NtOpenFile`; it does not enumerate, parse or read project content.
- Source inspection confirms directory-handle duplication requests only traverse/read-attributes/synchronize access and marks the copies non-inheritable; the runtime probe independently verifies exact target process, directory/non-reparse shape and non-inheritance. The Worker-only handle grant encodes the already-duplicated process-local values as canonical 16-digit lowercase hex and binds all root identities, session/epoch and generations. Handle duplication additionally requires explicit `worker.handle-lifecycle.v1` negotiation.
- Broker now enforces a branching lifecycle. `prepared -> grant published` may continue normally through `grant acknowledged -> revocation pending`, or take the safe-cancellation branch directly to `revocation pending` before grant ACK. While the Worker remains alive, both branches require `revoke published -> revoked` through an exact revoke ACK; a late grant ACK after revocation fails closed. The only no-ACK completion is direct transition from `revocation pending` or `revoke published` to `revoked` after observing that the exact pinned Worker process object has terminated. Before grant publication Broker may reclaim the remote handles; after publication it never calls `DUPLICATE_CLOSE_SOURCE` for those raw numbers. Grant/revoke creation and ACK handling are idempotent for exact replay; successful revoke ACKs retain a bounded, live-session-only tombstone so the same transport retry succeeds, while changed request/self-hash, stale session, wrong generation and other out-of-order messages fail closed. Session loss keeps a live process in revocation-pending rather than risking closure of a reused handle number.
- `ReadOnlyQueryRouter` currently performs identity-only routing. No Broker project content reader exists.
- ADR-003 freezes the Unity cross-runtime contract: schemas, token registries, typed/self-hash encoding and golden vectors are normative; Unity 2022.3 will use a narrow Worker adapter under a new Worker path, not the net8 binary and not the frozen Unity UI.
- The first Unity Worker protocol adapter now validates grant/revoke bytes and shares exact four-message golden vectors with .NET. Every ACK-specific model, parser and encoder/sealer path is test-conditional and absent from an independently compiled no-`UNITY_INCLUDE_TESTS` Editor DLL: production cannot acknowledge a parsed grant until a future opaque handle-admission owner proves adoption, nor acknowledge revoke until exact close is proven. The exact r40 receipt, production-surface compile receipt and source boundary are recorded in `W24_PHASE2_UNITY_WORKER_PROTOCOL_ADAPTER_REPORT.md`.
- A later Worker-only test scaffold now converts an authenticated test grant into one opaque three-handle owner, independently replays NTFS/root identities, prevents duplicate adoption within one session, caps session admission state, and closes attached/in-flight leases on session revoke. Its exact r48 filters pass 13/13 admission and 6/6 protocol cases. The production session issuer, global process/session ownership arbitration, ACK issuer and production Unity transport remain absent; details and receipts are recorded in `W24_PHASE2_WORKER_HANDLE_ADMISSION_REPORT.md`.
- The preceding Broker transport checkpoint serializes grant/ACK/revoke/ACK over one real `CurrentUserOnly` test pipe, enforces one concurrent live Worker session per exact PID/process epoch through full observer cleanup and closes the connection on any strict-ACK or lifecycle failure. Its accepted Release Broker receipt passed 22/22 before the current read-transport cases were added. The peer in those tests is the .NET test process, not Unity or production. Exact identities and boundaries are recorded in `W24_PHASE2_BROKER_WORKER_TRANSPORT_REPORT.md`.
- The preceding bounded connector checkpoint joined those two test-only halves using the actual Unity 2022.3 Editor process and the non-publishable HandleProbe host. Accepted r6 passed 2/2 for strict grant/ACK/revoke/ACK after opaque Unity-side adoption and close. The entire Unity connector remains removed by `UNITY_INCLUDE_TESTS`. Exact predecessor evidence is in `W24_PHASE2_UNITY_WORKER_CONNECTION_REPORT.md`.
- The preceding bounded read-query slice added strict Worker query/result codec parity, four fixed Library/Manifest/Contract/Trace mappings, and a 512 KiB handle-relative no-follow reader on the opaque lease. Final r4 passed 14/14 against a GUID-owned scratch repository. Exact predecessor evidence is in `W24_PHASE2_WORKER_READ_QUERY_REPORT.md`.
- The current transport slice joins those bounded test components: the non-publishable HandleProbe uses an in-process Desktop session to send four fixed reads plus one content-mismatch query over the actual Unity pipe between grant ACK and revoke. Broker never opens content; read and revoke share one exclusive exchange; malformed/cross-correlated responses close the connection; overlapping route drift discards bytes. Final r11 passes 2/2 with natural exit and zero scratch residue. This is still a GUID scratch/test-only connector checkpoint, not production or Desktop project access. Exact evidence is in `W24_PHASE2_BROKER_UNITY_READ_TRANSPORT_REPORT.md`.
- The latest Client-to-Broker checkpoint adds an identity-only Client query composer plus a second authenticated `CurrentUserOnly` test pipe for Desktop. A private test adapter sends the Client-built query to Broker, which routes it over the existing .NET test Worker pipe and returns exact content. Malformed JSON, unknown lease, cross-correlation and a concurrent second serve fail closed; Client independently rechecks result identities. r2 rejected a stale publication window and recursive cleanup; r3 rejected a session-reservation/registry-lock inversion and child-first cleanup; r4 closed the lock inversion but retained a managed path-delete TOCTOU, a production-compiled test barrier and non-aggregating fixture cleanup. The current route removes and invalidates sessions inside the registry gate but waits for publication drain only after releasing that gate; exact connection/session/lease reservations remain held through Desktop pipe flush. Release r5 replaces path deletion with test-only pinned-handle, handle-relative, no-follow cleanup, removes the production hook, and aggregates all fixture cleanup failures after every cleanup action is attempted. It passes 136/136 across Protocol 80, Client 12, Desktop 9 and Broker 35. The production Client factory remains disconnected and contains no pipe or project-I/O implementation; no Desktop UI was changed or launched. Exact evidence is in `W24_PHASE2_DESKTOP_BROKER_READ_TRANSPORT_REPORT.md`.

The test helper `VFXComposer.Broker.HandleProbe` is a non-publishable separate .NET test process. It verifies the exact target process, directory/non-reparse shape, non-inheritance, session-loss retention and exact-process-exit fallback; the current bounded variant also creates four fixed documents in a new GUID scratch tree and pins their cleanup ancestry. Requested access rights remain a source-level assertion. It is not a production Unity Worker or evidence of real-project access.

## 2. Fail-closed production state

The Release Broker executable was invoked directly from the current build:

```text
exitCode: 23
stderr: W24FS001
```

The only production branch before that return is the in-memory `BrokerPolicy.TryLoadProduction(out _)`, which returns `false`. No production Broker policy issuer, service SID/ACL profile, registered project, named-pipe listener, Worker connection or Client/Desktop connection is available.

Tests construct the private `BrokerPolicy` constructor from the test assembly only. The production assembly has no `CreateForTests`, `IssueForTests`, caller-path registration method or environment/`EditorPrefs` loader.

## 3. Validation performed

Current Release source built as the nine-project solution with `0` warnings and `0` errors.

Current Release tests:

| Fixture | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Protocol | 80 | 0 | 0 |
| Client | 12 | 0 | 0 |
| Desktop | 9 | 0 | 0 |
| Broker | 35 | 0 | 0 |
| Total | 136 | 0 | 0 |

Broker tests include a real Windows named-pipe peer-facts round trip, one-winner concurrent Worker authentication, a real test-only grant/ACK/revoke/ACK transport loop, wrong-ACK session revocation, global-volume relative traversal, a local NTFS junction rejection with no network target, exact session revocation, grant/revoke ACK ordering and replay negatives, a 256-iteration publish/cleanup race, and a separate process that validates three duplicated directory handles before exercising revoked-session retention and exact-process-exit finalization. Runtime access-mask introspection is not claimed; least-privilege access is currently a source-level assertion plus successful bounded use.

`eng/verify-phase2-schemas.py` reports:

```json
{"schema":"w24-phase2-schema-verification/1","status":"PASS","jsonschemaVersion":"4.26.0","totalSchemaCount":19,"phase2SchemaCount":10,"positiveCount":11,"negativeCount":144}
```

These are local build/test observations, not a durable installed-service, Unity, IPC recovery or production security receipt.

The current accepted Unity Worker r48 filters separately pass 13/13 handle-admission and 6/6 protocol cases with observed outer exit 0; their XML/log and exact 15-file delta manifest are bound by the Worker handle-admission report. This does not turn the Broker foundation receipt into an end-to-end IPC or project-read receipt.

The later Worker read-query r4 filter passes 14/14 with natural outer exit 0, full Unity shutdown telemetry and zero scratch residue. Its lease is test-issued in-process and its targets are a new GUID-owned scratch repository; it is not a production Broker-to-Worker or Desktop query receipt.

The current Broker-to-Unity read transport r11 filter passes 2/2 with natural outer exit 0, complete shutdown telemetry and zero scratch residue. It sends five queries over the test pipe using an in-process Desktop session; no Client/Desktop production route or real registered-project access is proven.

The current Desktop-to-Broker .NET r5 receipt passes 136/136 and exercises a Client-built query over a real same-user Desktop test pipe before the existing Worker test pipe. It proves that registration revoke and direct Desktop/Worker session or connection revoke cannot overtake an exact response already reserved through pipe flush, and that a revoke landing between session reservation and store replay rejects without deadlock or publication. Its test cleanup is pinned-handle and handle-relative rather than path-recursive. This remains a same-process test policy with a private test pipe adapter: no production Client connector, Desktop application run, trusted service issuer or real registered-project read is proven.

## 4. Frozen source identities for this checkpoint

The exact original 66-file Broker/protocol source/schema/test set is enumerated, rather than inferred, in `W24_PHASE2_FOUNDATION_SOURCE_MANIFEST.sha256`. Each line is `<lowercase SHA-256><two spaces><forward-slash repo path><LF>`, sorted by repo path with `StringComparer.Ordinal`. The manifest physical SHA-256, which is also the aggregate of those exact lines, is `782ab3ecc17058a78c27d04478d2feffd4a058ef3c1290d80b14756c769c9182`; independent replay reports zero mismatches. Later Worker codec, handle-admission, read-query and Broker-to-Unity read-transport deltas are independently enumerated by `W24_PHASE2_WORKER_PROTOCOL_SOURCE_MANIFEST.sha256`, `W24_PHASE2_WORKER_HANDLE_ADMISSION_SOURCE_MANIFEST.sha256`, `W24_PHASE2_WORKER_READ_QUERY_SOURCE_MANIFEST.sha256` and `W24_PHASE2_BROKER_UNITY_READ_TRANSPORT_SOURCE_MANIFEST.sha256`; none rewrites this accepted foundation receipt.

Key files:

| File | SHA-256 |
|---|---|
| `services/VFXComposer.Broker/Program.cs` | `e2cbc1feb5143a8067d630e2eb39c28e5170747eecbccdbef59b5c9b5ddbbb0a` |
| `services/VFXComposer.Broker/Configuration/BrokerPolicy.cs` | `3d4002565f9d4ba03c417cfa328176ed3eed0c0b6f98a033f2de4f8bd3845a09` |
| `services/VFXComposer.Broker/Ipc/WindowsNamedPipePeerFactsSource.cs` | `8854ea927307f4022375a8384c6553257ce9f8a6c227b8aca4d42394bec6d532` |
| `services/VFXComposer.Broker/Security/ProcessEpoch.cs` | `a365b6b25a2ecbf4a448119a2fa147079841db42e8ee23deb8caa92e7f446035` |
| `services/VFXComposer.Broker/Native/WindowsVolumeHandle.cs` | `0edd7ab6ed493c28e560ec45a4a3cfa4df18dc1c2224d8595702a5cb0b71cff8` |
| `services/VFXComposer.Broker/Native/HandleDuplicator.cs` | `809066f1ecffbc8e46c28eb22c104ecaca0f0c1489ff2ba1e7424b80808c977f` |
| `services/VFXComposer.Broker/Registration/RegisteredProjectLease.cs` | `0a068957824a3f7130c8d9e65fa60b7f1dbe5ca5760332874eedd2142e37eff9` |
| `services/VFXComposer.Broker/Registration/ProjectRegistrationStore.cs` | `07fb9224759729fb9aeec3d5dc970d1dc4967cb61eb1e1e2ffaf5c9aec83903c` |
| `services/VFXComposer.Broker/Ipc/AuthenticatedPeerSession.cs` | `7173f31d53dab4b86f0d061de60a7ba4d2d5af6bc55c59971fd968bb930996b7` |
| `services/VFXComposer.Broker/Ipc/PeerSessionRegistry.cs` | `77727e03606c539f50500c8ebe3e1eca599f8e3ffc9ba11d7cb7a10d315fb564` |
| `src/VFXComposer.Protocol/Registration/WorkerProjectHandleGrant.cs` | `06d8b1041f9703dd93199ade519d661fbaabce2e0f651e7ec0a7d51aaa1b0915` |
| `src/VFXComposer.Protocol/Registration/WorkerProjectHandleGrantAcknowledgement.cs` | `4e4e486ae041db8590c7830346e04c99d4b9ac94388136ec10b55a45bcb282d6` |
| `src/VFXComposer.Protocol/Registration/WorkerProjectHandleRevoke.cs` | `c9aee25541092ed9ecc5d6ae6eb574301b19c3b8bcc501fe7027770f9ea665e9` |
| `src/VFXComposer.Protocol/Registration/WorkerProjectHandleRevokeAcknowledgement.cs` | `e35ad012fd33b3310192951eaf3404b2125e701bd06f00b91c84795de62896e2` |
| `docs/schemas/desktop/vfxcomposer-worker-project-handle-grant-v1.schema.json` | `8178f6b8fff6169bcd1107da33a1ab5a2b087b58bc9b1a773e8c9d3fd59ce1f4` |
| `docs/rules/ADR-003_UNITY_WORKER_PROTOCOL_COMPATIBILITY.md` | `5746a57df5f08c1a42bf28b48e7e6cc6be2541fba16783c446deff7d4afebbc2` |

Current Release binaries:

| Binary | SHA-256 |
|---|---|
| `VFXComposer.Protocol.dll` | `e14355a16b1f5a58503caea8f7bd01d53458fd28066b084d827f89f69374b8a2` |
| `VFXComposer.Broker.dll` | `5eebd88880c4aab40ccb87f3a00e87fe21e696fa38147c21b88935ab2cc37a49` |
| `VFXComposer.Broker.Tests.dll` | `d1ca608f046fceedaefd198330010928988a1838301dba95e096f23bd212df5f` |
| `VFXComposer.Broker.HandleProbe.dll` | `65eef234c1927278307d01c65944c99b2daa6ead550304d10b4c395333b50df1` |

## 5. Explicit blockers before the Phase 2 gate

1. Production host/service ownership, Broker policy loading, service SID, pipe ACL and install/lifecycle policy are absent. `CurrentUserOnly` is test-scaffold evidence, not the final production ACL.
2. The Unity 2022.3 Worker grant/revoke codec, cross-runtime golden vectors and a real test-only Unity↔Broker pipe lifecycle are implemented and run. The connector, session issuer and ACK owner are all compiled out without `UNITY_INCLUDE_TESTS`; a production image-claim issuer, authenticated production Unity session and production ACK owner are still absent. No old Unity UI, test helper or .NET test-peer receipt may fill this gap.
3. Unity now has a test-only opaque three-handle owner with duplicate-adoption and revoke/close gates, and r6 connects it to the test Broker host. Production policy/session/global ownership and ACK issuance remain absent and fail closed.
4. A test-issued handle-relative Worker reader covers four allow-listed document families, the non-publishable HandleProbe routes those reads over the actual Unity test pipe, and the latest .NET checkpoint routes a Client-built query through a real Desktop test pipe to a .NET test Worker. No production Broker-delivered lease, registered real project or trusted Library-index producer has run, so production project-content access remains absent.
5. Client can now compose and correlation-check an identity-only read through an injected connection, but its production factory remains deliberately disconnected. No production named-pipe connector, Broker project-selection client or Desktop result presentation exists.
6. The new Desktop serve primitive rejects a concurrent second request instead of queueing, but a production bounded multi-request loop, backpressure policy, cancellation recovery, Broker restart/generation replay and continuous lifecycle recovery gates are not complete.
7. The Broker-side state machine now has both a .NET test-peer loop and a test-only connection to the actual Unity Editor process, but no authenticated production Unity transport or production ACK owner exists. Production service/session loss must be connected to a supervisor that terminates the exact Worker process and drives `FinalizeExitedWorkerRevocations`; Broker shutdown alone deliberately abandons published numbers instead of risking an unsafe close. A disconnected PID/process epoch also cannot be replaced in production until unresolved published ownership is globally arbitrated.
8. DOS-device remap/installer/service-principal threat gates remain for the production issuer profile.
9. Independent frozen-byte delta audit completed on 2026-08-26: `P0=0 / P1=0 / P2=0` for this bounded, non-production Phase 2 Broker-side foundation only. This is a scoped foundation GO, not the Phase 2 gate or production authority; blockers 1-8 remain open.

The later Worker handle-admission delta independently audited at `P0=0 / P1=0 / P2=0` for `TEST_ISSUED_SINGLE_SESSION_OPAQUE_HANDLE_ADMISSION_LIFECYCLE_SCAFFOLD_ONLY`. That separate scoped GO does not close blockers 1-8 or grant production connection, global process/raw-handle ownership, ACK, content read or authority.

The Broker/Worker transport delta independently audited at `P0=0 / P1=0 / P2=0` for `BROKER_WORKER_LIFECYCLE_TRANSPORT_TEST_SCAFFOLD_ONLY`. Its real pipe is a .NET test peer, not Unity or production; the scoped GO does not close blockers 1-8 or grant a Unity/production connection, content read/write or authority.

The later Unity Worker connection checkpoint independently audited at `P0=0 / P1=0 / P2=0` for `UNITY_WORKER_TEST_PIPE_LIFECYCLE_SCAFFOLD_ONLY`. Its scoped GO closes only that test-only lifecycle checkpoint; it does not close blockers 1-8 or grant production connection, project content access, Worker commands or authority.

The later Worker read-query delta independently audited at `P0=0 / P1=0 / P2=0` for `WORKER_TEST_ISSUED_HANDLE_RELATIVE_READ_QUERY_SLICE_ONLY`. Its scoped GO closes only strict query/mapping and test-issued handle-relative scratch reads; it does not close blockers 1-8 or grant a production session, Broker/Desktop route, real-project access, commands or authority.

The current Broker-to-Unity read-transport delta independently audited at `P0=0 / P1=0 / P2=0` for `BROKER_TO_UNITY_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY`. Its scoped GO closes only the non-publishable GUID-scratch test route; production connection/read, Client/Desktop routing, commands and authority stay fail closed and blockers 1–8 remain open.

The later Desktop-to-Broker r5 read-transport delta passed independent frozen-byte audit with `P0=0 / P1=0 / P2=0`, strictly scoped to `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY`. Its 136/136 Release receipt covers only a private test Client adapter and same-process authenticated Desktop/Worker pipes; it does not close blockers 1–8 or grant production connection/read, Desktop UI integration, commands or authority.

Therefore Phase 2 is not complete. Production project read, Worker actions, writes, Preview/Review, machine/visual/user authority and L3/L4 remain fail closed.

## 6. Production-read DAG rebase — current blockers, not runtime delivery

`WP-P2-PRODUCTION-READ-DAG-REBASE-1` is docs-only. Its completed remediation
corrects the D1→I1 live-store cycle, rebases P1/S1 to D1R, and adds C2 before
W1; it is STOPPED and a fresh independent audit is pending. This report
declares no package GO. The current
Broker binary remains required to return `W24FS001`/`23` before listener,
caller-path or project I/O; this report does not claim a service installation,
durable profile success, protected install object, production Worker, listener,
project enrollment or real read.

ADR-004 freezes four non-interchangeable executable claims: native process
path observation; protected installer-owned launch-file object plus content
identity; process executable backing-file-object identity; and current
executable-memory-page integrity. The ordinary Windows product profile is
limited to launch-correlated protected-file evidence and explicitly records
`LoadedImageVerified=false`. `WindowsNamedPipePeerFactsSource` currently
reopens a native process path to hash `process-image/1`; that remains a
compatibility peer fact only and cannot serve as the sole production
loaded-image or launch-file admission proof. Strict backing-file or memory
claims require a separately authorized signed-driver/WDAC/CI high-assurance
design; ordinary post-start user-mode path APIs and `ReadProcessMemory` do not
prove them.

The still-open production prerequisites are rebased rather than inferred:

1. closed C1 no-path Protocol selection; D1R fresh dormant remediation over
   the retained D1_0 NO-GO bytes; C2 pure Protocol host-owned locator/ACK; and
   W1 dedicated production Unity Worker connector after C2 plus ADR-003;
2. D1R→I1 only as code dependency. I1 owns privileged root provisioning,
   enabled `SeSecurityPrivilege`, `ACCESS_SYSTEM_SECURITY`, target-directory
   rename rights and the first real strict store commit/reopen/readback; then
   I1→A1 and I1→R1. D1R→P1 and D1R→S1 remain separate;
3. C1+C2+W1+A1+P1+S1+R1→B1 production Broker convergence; B1+C1→D2→E1→A2,
   where D2 is Client/Desktop read, E1 is installed E2E, and A2 is the final
   independent audit.

The rebased edges are exactly `D1R→I1→A1`, `D1R→P1`, `D1R→S1`,
`I1→R1`, `C2→W1`, `C1+C2+W1+A1+P1+S1+R1→B1`,
`B1+C1→D2`, and `D2→E1→A2`. This is an acyclic 13-node, 8-edge-group,
17-directed-edge DAG. Historical G0 `12/6/15/68/1` is provenance only;
current accounting is 75 unique leaves, 12 sequential overlays and one
receipt-root exception. P1 and S1 each depend on D1R, never retained `D1_0`
or old `D1`; the owner table and edge list use that same identity.

The ordinary-profile production activation prerequisite set includes protected
local-NTFS install root/ancestor and executable pins that span hash/start/
receipt, actual ACL and fixed SCM configuration readback, exact file
ID/hash/length, process PID/epoch/token/service-SID/session replay, and a
one-use nonce/generation receipt. A ServiceHost cannot report `Running` or
accept a request until those facts, actual pipe ACL readback, Worker
supervision and project enrollment are live. This is a launch correlation, not
a loaded-image equivalence claim.

D1R can prove only dormant source/static remediation and a fail-closed
privilege-negative; it may not report successful strict store open/commit/
reopen/readback. The I1 live-store receipt is the sole proof of those actions
and must be consumed by B1. C2's strict no-path locator and separate locator
ACK bind typed host-owned identities, Worker session/process epoch and
broker/registration/enrollment generations; neither is a lease or a
handle-grant acknowledgement.

The production Worker must be Broker-created suspended, assigned to a
kill-on-close Job, then resumed; it receives only a host-owned Volume-GUID
project locator and exact duplicated capabilities. Interactive Editor reuse,
Desktop paths, direct Desktop project I/O, path fallback, test peers,
HandleProbe, test issuers, scratch artifacts and legacy Unity receipts are
all prohibited as production substitutes. Worker crash, Job/ACK failure,
unresolved handles, nonce replay, pipe squatting/ACL drift, PID reuse,
hardlink/reparse/DOS remap, file replacement or enrollment drift must revoke
the whole relevant receipt/session/lease before any content I/O.

W1 retains exactly its original nine Unity paths and depends on C2 plus
ADR-003. It is only a composition shim: ADR-003 §4's exact ownership sentence
`Unity 侧只允许新增：` and its immediately following two-path listing are
superseded only as the location exception for those nine paths. ADR-003's
single normative Unity adapter remains unchanged. Existing ADR-003
`Worker/Protocol/**` files, including `W24S6WorkerProtocolCodec`, and its
existing protocol tests are frozen/read-only dependencies. W1 may only reuse
that codec's canonical primitives and C2 schemas/immutable golden bytes; it
may not declare independent DTOs, hashes, canonicalization, registry/token
entries, message kinds, schemas, encoders or decoders. Its one planned test
path may only replay the existing ADR-003 adapter plus C2 vectors. It has no
production pipe, project read, session issuer, handle grant or authority; a
handle-grant ACK never substitutes for locator ACK.

The complete file-level ownership envelopes, adversarial gate matrix, receipts
and STOP conditions are normative in
`docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md`.
No Phase 2 gate, authority, user verdict, L3 or L4 conclusion follows from
this architecture freeze.

The rebase makes the formerly ambiguous production boundaries explicit:

- D1_0 remains NO-GO. D1R has exactly the same three source/test overlays and
  must fix strict security/rename/zeroing/ACL/rollback/manifest defects without
  claiming live success. I1 exclusively owns privileged root provisioning and
  the first strict store commit/reopen/readback.
- C2 has an exact 16-path pure Protocol envelope: seven pre-absent unique
  leaves and nine C1→C2 sequential overlays with exact prior hashes. Existing
  GoldenVectors globbing means neither a csproj nor lock change is needed.
  Its schemas, strict codec, .NET test vectors and future W1 Unity replay via
  the frozen ADR-003 adapter reject all path, handle, grant-ACK and authority
  substitution; W1 cannot create a second codec or protocol fork.
- A1 owns only `LaunchCorrelationReceipt`/protected-launch-pin/correlation
  primitives. B1 depends on `C1+C2+W1+A1+P1+S1+R1`, alone owns ServiceHost
  `Program.cs`, service-host `.csproj`, `WindowsScmServiceHost.cs`, the final
  `Running` guard and convergence/integration tests, and requires I1's
  privileged live-store receipt.
- P1 must apply and read back exact owner/group/DACL/SACL/ACE/mask/protection
  for **every serving named-pipe instance** before `ConnectNamedPipe`/accept.
  The first serving receipt—not a bootstrap/non-serving receipt—is a
  ServiceHost `Running` prerequisite; every later instance receives its own
  receipt and independently fails closed.
- S1's kill-on-close Job is lifecycle containment only. It is not a
  filesystem/capability sandbox. The ordinary profile continues to defend
  same-user pipe impersonation, namespace/path/reparse and PID-reuse attacks,
  but explicitly excludes malicious code injection into an already
  authenticated Desktop/dedicated Worker and malicious Editor assemblies in
  an explicitly enrolled trusted-code project. Restricted token/AppContainer,
  WDAC/HVCI/code-integrity and sandbox designs are separate future profiles.
- ADR-004 section 9 is the sole 13-node path-owner/overlay ledger: 75 unique
  leaves, 12 governed sequential overlays and one receipt exception, not a
  global zero-overlap statement. W1 retains every new Unity `.cs` and matching
  `.meta` plus the new directory `.meta`, but only as the narrow composition
  shim described above; those paths cannot fork ADR-003's one normative
  adapter. E1 retains `tests/EndToEnd/packages.lock.json`. Its allow-lists may
  only shrink.
