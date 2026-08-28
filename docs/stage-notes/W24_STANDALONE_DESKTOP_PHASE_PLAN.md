# W24 standalone desktop — file-level implementation plan

> **CURRENT CLOSEOUT — U6 FINAL GO / A0 AI ARCHITECTURE FREEZE (2026-08-28).** ADR-005's ordinary-user route is closed at `100/100`; ADR-006 freezes the separate post-U6 AI-provider route. The retained pre-U0 plan below is historical provenance only: its Service/SCM/privileged nodes are not current dependencies, blockers, implementation work, or audit work.
>
> Normative tokens: `USER_MODE_LOCAL_CREATIVE_TOOL_V1` and `AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`.

## Current user-mode closeout and post-U6 AI plan

The independent U6 receipt `u6-independent-final-audit-20260828T232640380Z` is accepted as `FINAL GO — P0/P1/P2=0/0/0`: `summary.json` is passed, frozen-root replay has `0` mismatches, point-in-time process/pipe/temp-root residue is empty, and the recorded source manifest is `16607` entries at SHA-256 `592bfeaab629e8cb9b100cf82fd3ce95c5be23972742501be34e57f1908a2284`. The ADR-005 USER_MODE main architecture is therefore `CLOSED — 100/100`. This does not turn AI configuration or provider traffic on.

The default Broker contract remains frozen: no-argument launch writes only `W24FS001` to stderr and exits `23`. Broker, Worker, and Unity receive no AI secret or provider-network role; Desktop may use only `IAiGateway` and constrained settings-management contracts, never a direct AI transport.

### Closed user-mode DAG

The completed USER_MODE DAG remains historical evidence with these exact edges:

`U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`.

| Node | Final state | Boundary retained after closeout |
|---|---|---|
| U0–U5 | `CLOSED / INTEGRATED` or `CLOSED / SCOPED GO` as recorded in the retained evidence. | No scope expansion beyond ADR-005. |
| U6 | `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. | Frozen bytes and final receipt remain provenance; no reopening or source repair under U6. |

### Formal post-U6 AI DAG

The exact formal AI delivery DAG is `A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`.

| Node | State | Planned boundary |
|---|---|---|
| A0 `AI_PROVIDER_TWO_CHANNEL_ROUTING` | `CLOSED — DOCS ONLY` in this same single documentation commit. | ADR-006 plus this plan/report/control/registry/evidence publication; existing U6 evidence only, no project-gate rerun and no implementation bytes. |
| A1 `AI_PROVIDER_FOUNDATION` | `ACTIVE` — the sole active package. | Contracts, strict configuration/security/store/import/resolver/Gateway foundation; no real Chat or Image HTTP. |
| A2 Chat adapter lane | `NOT STARTED`. | Explicit Chat-only adapter behavior after A1. |
| A3 Image adapter lane | `NOT STARTED`. | Explicit Image-only adapter, downloader/cache behavior after A1. |
| A4 Desktop wiring | `NOT STARTED`. | Desktop uses the Gateway only; no direct provider transport or project write. |
| A5 mock E2E | `NOT STARTED`. | Controlled mock-only end-to-end evidence. |
| A6 independent AI audit | `NOT STARTED`. | Read-only audit of frozen A0–A5 bytes. |

ADR-006 freezes two mandatory channels: all LLM/conversation work uses only the one explicit `ChatLlm` binding, and all image generation uses only the one explicit `ImageGeneration` binding. Each binding resolves exactly one profile/capability/model; a profile may serve both only through separately chosen capabilities. Origin (`Official`, `Relay`, `Friend`, `Subscription`, `Custom`) is metadata, not protocol or routing. Unknown, missing, cross-channel, or failed state has no implicit or fallback route.

### A1 exact ownership and stop line

The A1 owned roots are exactly:

1. `src/VFXComposer.AI.Contracts/**`
2. `src/VFXComposer.AI.Providers/**`
3. `src/VFXComposer.AI.Tests/**`
4. `docs/schemas/desktop/vfxcomposer-ai-provider-config-v1.schema.json`
5. `VFXComposer.sln`
6. `eng/run-phase2-gate.ps1`
7. `eng/phase2-baseline-roots.json`

A1 must STOP for any other path, including a central package-management file or an external `.csproj` dependency. It may deliver core contracts/profile/channel bindings/schema, strict versioned atomic JSON with `.bak`, DPAPI CurrentUser SecretRef store, configuration fingerprint, health/adapter-registry skeleton, safe Tom draft import, resolver, and `IAiGateway`; it must not implement real provider HTTP. Its minimum test matrix is canonical configuration, migration/future rejection, corrupt backup, DPAPI plaintext/unreadable failure, URI policy, capability/channel fail-closed behavior, no fallback, Tom-secret exclusion, internal boundary, and redaction.

All credentials are documented API-token/OAuth only. Cookie scraping, scripts/CLI, custom-header templates, dynamic DLLs, TLS bypasses, non-loopback HTTP, raw diagnostic data, and automatic Unity writes are prohibited. Configuration uses DPAPI CurrentUser plus SecretRef and atomic JSON/`.bak`; Tom import accepts only non-sensitive metadata and never decrypts `ApiKeyProtected`; images remain in a private cache only.

---

## Historical pre-U0 plan (superseded for product delivery)

Date: 2026-08-26; production-read architecture freeze: 2026-08-28  
Architecture authority: `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md`; `docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md` for Windows production-read gating  
Current phase: `PHASE_1_GO / PHASE_2_FOUNDATION_IN_PROGRESS / PRODUCTION_READ_DAG_REBASE_STOPPED_REMEDIATION_COMPLETE_FRESH_INDEPENDENT_AUDIT_PENDING`  
Code creation status: `PHASE_1_COMPLETE / PHASE_2_PROTOCOL_AND_BROKER_FOUNDATION_ONLY / PRODUCTION_CONNECTION_NO_GO / NO_PRODUCTION_READ_IMPLEMENTATION_CLAIM`

## 1. Stop-line state

Unity Editor UI feature work is stopped. Existing `VfxStudioWindow`, Models, focused tests and r31/r32/r35/r36 receipts remain unchanged compatibility/diagnostic baselines. Only compile-, corruption-, or security-critical fixes are allowed there.

The current non-UI atomic slices are closed:

- r36 current S6 runtime: registration `6/6`, filesystem `36/36`, envelope/Inspector `41/41`, Models `9/9`, callback/integration `12/12`; total `104/104`, five natural Unity exit-0 processes.
- production registration remains `REGISTRATION_ISSUER_PENDING / W24FS001` before parse or I/O.
- full110 rich-session protocol now has strict reduced66/full110 versioned token sets; formal projection still waits for the exact immutable QA `model-version-id`.

No r31/r32/r35/r36 receipt may be attached to Desktop, Client, Broker, IPC, installer or new Worker APIs.

## 2. Phase 0 deliverables

Files created or updated before any new code root:

| File | Purpose | Gate |
|---|---|---|
| `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md` | accepted architecture, ownership, threat model, fail-closed rules | independent P0/P1 audit |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md` | exact files, order, tests and phase gates | independent P0/P1 audit |
| `docs/stage-notes/W24_UNITY_UI_TO_DESKTOP_MIGRATION_MATRIX.md` | old-to-new feature/evidence mapping | independent P0/P1 audit |
| `docs/DECISIONS.md` | accepted product/ownership/transport decision register | decision consistency |
| `docs/allwork/24_VFX_DESIGN_TO_IMPLEMENTATION_SYNC.md` | S6 direction and completion definition revision | no old evidence rebinding |
| `docs/allwork/00_INDEX_AND_ACCEPTANCE.md` | current status, stop-line and 65%–70% re-estimate | status consistency |
| `docs/stage-notes/W24_S6_UI_REPORT.md` | freeze old UI baseline; bind only r36 source | evidence hash audit |

Phase 0 does not create `apps/`, `src/`, or `services/` code. Gate: independent reviewer reports P0=0 and P1=0.

### 2.1 Frozen estimate and sequencing assumptions

These are elapsed calendar-day estimates supplied by the user, not authority to skip gates:

| Phase | Estimate | Cumulative milestone |
|---|---:|---:|
| Phase 0 — stop-line and architecture freeze | 1–2 days | 1–2 days |
| Phase 1 — Desktop shell and shared Protocol | 2–4 days | 3–6 days |
| Phase 2 — secure registration and read-only connection | 3–5 days | 6–11 days |
| Phase 3 — Worker commands and Jobs | 5–8 days | 11–19 days |
| Phase 4 — independent Preview and Review/Evidence | 4–6 days | 15–25 days |
| Phase 5 — parity, installation and final gates | 5–8 days | 20–33 days, approximately 3–5 calendar weeks |

The gate order is sequential and may not be compressed by running later authority, write, or transport work early. Bounded documentation, test-authoring, and independent-review work inside one phase may overlap only when it does not cross that phase's gate. The user-authorized 2026-08-27 exception permits only the future Phase 3 pure-C# command/job DTO, exact-schema, strict-codec and golden-vector foundation to be frozen before the Phase 2 production gate; it grants no Client/Broker/Worker transport, handler, project read/write, transaction, UI action or authority, and Worker command implementation waits for the audited Protocol freeze. Estimates assume one continuous primary workstream, exclude user-driven visual rework, and are planning ranges rather than acceptance evidence. The usable Desktop + Worker milestone remains approximately 2–4 weeks; the complete production Broker/security/install/e2e milestone remains approximately 3–5 weeks.

## 3. Phase 1 — Desktop shell and shared protocol

Proposed solution files, created only after Phase 0 GO:

```text
VFXComposer.sln
Directory.Build.props
Directory.Packages.props
global.json
NuGet.config
eng/approved-packages.json
eng/verify-offline-restore.ps1

src/VFXComposer.Protocol/
  VFXComposer.Protocol.csproj
  ProtocolVersions.cs
  MessageKinds.cs
  CapabilityIds.cs
  Diagnostics/StableDiagnostic.cs
  Hashing/TypedHash.cs
  Hashing/SelfHash.cs
  Json/StrictJsonReader.cs
  Json/ExactObjectValidator.cs
  Handshake/HandshakeRequest.cs
  Handshake/HandshakeResponse.cs
  Projects/ProjectConnectionState.cs
  Status/MachineStatus.cs
  Status/VisualStatus.cs
  Status/UserVerdictStatus.cs
  Status/L3Status.cs
  Status/L4Status.cs
  Status/StatusProvenance.cs
  Jobs/JobIdentity.cs
  Jobs/JobStatus.cs

src/VFXComposer.Protocol.Tests/
  VFXComposer.Protocol.Tests.csproj
  StrictJsonTests.cs
  TypedHashTests.cs
  SelfHashTests.cs
  HandshakeSchemaTests.cs
  CapabilityNegotiationTests.cs
  AuthorityDomainSeparationTests.cs
  StatusSchemaParityTests.cs
  WireSchemaRegistryTests.cs
  DtoSchemaParityTests.cs
  DependencyBoundaryTests.cs

src/VFXComposer.Client/
  VFXComposer.Client.csproj
  IVfxComposerConnection.cs
  DisconnectedVfxComposerConnection.cs
  VfxComposerClient.cs
  ConnectionState.cs
  RequestCorrelation.cs

src/VFXComposer.Client.Tests/
  VFXComposer.Client.Tests.csproj
  DisconnectedClientTests.cs
  RequestCorrelationTests.cs

apps/VFXComposer.Desktop/
  VFXComposer.Desktop.csproj
  Program.cs
  App.axaml
  App.axaml.cs
  Services/IUiDispatcher.cs
  Services/IDialogService.cs
  ViewModels/MainWindowViewModel.cs
  ViewModels/DashboardViewModel.cs
  ViewModels/LibraryViewModel.cs
  ViewModels/CreateViewModel.cs
  ViewModels/PreviewViewModel.cs
  ViewModels/PatchViewModel.cs
  ViewModels/ReviewViewModel.cs
  ViewModels/JobsViewModel.cs
  ViewModels/SettingsViewModel.cs
  Views/MainWindow.axaml
  Views/DashboardView.axaml
  Views/LibraryView.axaml
  Views/CreateView.axaml
  Views/PreviewView.axaml
  Views/PatchView.axaml
  Views/ReviewView.axaml
  Views/JobsView.axaml
  Views/SettingsView.axaml

apps/VFXComposer.Desktop.Tests/
  VFXComposer.Desktop.Tests.csproj
  StartupDisconnectedTests.cs
  NavigationTests.cs
  AuthorityPresentationTests.cs
  NoUnityDependencyTests.cs
  NoProjectAccessSurfaceTests.cs

docs/schemas/desktop/
  vfxcomposer-handshake-request-v1.schema.json
  vfxcomposer-handshake-response-v1.schema.json
  vfxcomposer-diagnostic-v1.schema.json
  vfxcomposer-machine-status-v1.schema.json
  vfxcomposer-visual-status-v1.schema.json
  vfxcomposer-user-verdict-status-v1.schema.json
  vfxcomposer-l3-status-v1.schema.json
  vfxcomposer-l4-status-v1.schema.json
  vfxcomposer-status-provenance-v1.schema.json
```

Every NuGet-consuming project also carries a checked-in `packages.lock.json`. `NuGet.config` permits only the separately approved package source/cache used to materialize the locked dependency set; `eng/approved-packages.json` binds package ID, version, SHA-512 and provenance. Acquiring or refreshing that set is a separate explicitly recorded dependency transaction. The Phase 1 build/test gate runs locked and offline and must not contact a package feed.

Implementation order:

1. Pin .NET SDK and package versions; no floating dependencies.
2. Establish dependency tests proving Protocol has no Unity/Avalonia references and Desktop has no Unity references.
3. Port strict JSON and typed hash semantics as a shared implementation with cross-language/current-Unity golden vectors; do not copy private Unity-only validators blindly.
4. Implement disconnected Client and Desktop startup before any transport.
5. Add navigation and explicit machine/visual/user/L3/L4 status types.
6. Add structured in-memory logging and global UI error boundary without filesystem output by default.

Phase 1 gate:

- locked-mode offline restore, `dotnet build`, and tests using the frozen approved package set; the NuGet source allow-list contains only the approved local feed, and the cold restore uses `--no-cache` with no network package source configured.
- Desktop starts when Unity and Broker are absent; state is visibly `Disconnected / No registered project`.
- listener count is zero; no TCP/HTTP/stdio server.
- Desktop and Client have no direct project read or write surface; static dependency/API scans and runtime sentinels cover `project/Assets`, `project/Packages`, `project/ProjectSettings`, `docs/vfx-*` and `artifacts`.
- UI cannot construct authority or turn a checkbox into machine/visual/user/L3/L4 status.

Current Phase 1 snapshot (2026-08-26): the bounded Protocol, disconnected Client and Desktop shell implementation is complete. Final receipts record Debug build `0` warnings / `0` errors, Protocol `68/68`, Client `8/8`, Desktop `9/9`, locked offline Release build/tests, nine-schema positive/negative validation, and a disconnected structural smoke with zero observed listeners, watched-root filesystem events, and watcher errors plus four forbidden-process checkpoints. Exact identities and limitations are recorded in `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE1_REPORT.md`.

The independent final frozen-byte audit reports `P0=0 / P1=0 / P2=5` and closes only the `DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY` Phase 1 gate. At that frozen Phase 1 snapshot, Phase 2 was `NOT_STARTED`. Phase 2 foundation work has since begun under a separate report; this does not retroactively change the Phase 1 receipt. The status DTOs remain unauthenticated presentation contracts and the Phase 1 smoke is not pixel/visual QA.

## 4. Phase 2 — secure registration and read-only connection

Proposed Broker files:

```text
services/VFXComposer.Broker/
  VFXComposer.Broker.csproj
  Program.cs
  Configuration/BrokerPolicy.cs
  Ipc/NamedPipeBrokerHost.cs
  Ipc/NamedPipePeerAuthenticator.cs
  Ipc/WorkerSessionRegistry.cs
  Ipc/WorkerSessionRouter.cs
  Ipc/WorkerHandleLifecycleTransport.cs
  Queries/ReadOnlyQueryRouter.cs
  Registration/ProjectRegistrationStore.cs
  Registration/RegisteredProjectIdentity.cs
  Registration/RegisteredProjectLease.cs
  Native/WindowsVolumeHandle.cs
  Native/WindowsDirectoryHandle.cs
  Native/HandleDuplicator.cs
  Native/FileIdentity128.cs
  Security/BrokerAclPolicy.cs
  Security/ProcessEpoch.cs

services/VFXComposer.Broker.Tests/
  VFXComposer.Broker.Tests.csproj
  NamedPipeAuthenticationTests.cs
  WorkerPeerBindingTests.cs
  WrongPidImageAndEpochTests.cs
  HandleDuplicationTargetTests.cs
  RegistrationReplayTests.cs
  HandleRightsTests.cs
  JunctionAndReparseTests.cs
  DosDeviceRemapTests.cs
  LifecycleAndRevocationTests.cs
  WorkerHandleLifecycleTransportTests.cs
  NoNetworkListenerTests.cs

src/VFXComposer.Protocol/
  Ipc/PeerHello.cs
  Ipc/PeerSessionAccepted.cs
  Registration/ProjectRegistrationAttestation.cs
  Registration/ProjectLeaseDescriptor.cs
  Registration/WorkerProjectHandleGrant.cs
  Registration/WorkerProjectHandleGrantAcknowledgement.cs
  Registration/WorkerProjectHandleRevoke.cs
  Registration/WorkerProjectHandleRevokeAcknowledgement.cs
  Queries/ReadDocumentQuery.cs
  Queries/ReadDocumentResult.cs

src/VFXComposer.Client/
  NamedPipeVfxComposerConnection.cs
  ProjectRegistrationClient.cs
  ReadOnlyProjectQueryClient.cs

project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/
  W24S6WorkerBrokerConnection.cs
  W24S6WorkerPeerSession.cs
  W24S6WorkerProjectLease.cs
  W24S6WorkerReadOnlyHost.cs
  W24S6WorkerReadQueryHandler.cs

project/Packages/com.vfxcomposer.unity/Tests/EditMode/
  W24S6WorkerBrokerSessionTests.cs
  W24S6WorkerReadOnlyQueryTests.cs

docs/schemas/desktop/
  vfxcomposer-peer-hello-v1.schema.json
  vfxcomposer-peer-session-accepted-v1.schema.json
  vfxcomposer-project-registration-attestation-v1.schema.json
  vfxcomposer-project-lease-v1.schema.json
  vfxcomposer-worker-project-handle-grant-v1.schema.json
  vfxcomposer-worker-project-handle-grant-ack-v1.schema.json
  vfxcomposer-worker-project-handle-revoke-v1.schema.json
  vfxcomposer-worker-project-handle-revoke-ack-v1.schema.json
  vfxcomposer-read-document-query-v1.schema.json
  vfxcomposer-read-document-result-v1.schema.json
```

Current bounded checkpoint (2026-08-26): ten Phase 2 DTO/schema pairs, a dormant Broker executable, OS-observed same-user named-pipe peer authentication, native volume-guid/`FILE_ID_128` root pinning, least-privilege cross-process handle duplication, ordered grant/ACK/revoke/ACK state and identity-only read-query routing are implemented as a foundation. Published handle numbers are never raw-number-closed by Broker; exact Worker ACK or exact process termination is required. The Unity Worker has a strict grant/revoke codec plus a test-only opaque three-handle admission/close owner. Accepted r48 EditMode filters pass 13/13 admission and 6/6 protocol cases; per-session duplicate grant adoption, duplicate raw values, admission-state overflow and revoke/admission/dispose races fail closed. ACK models and byte entrypoints remain test-conditional, and an independent no-`UNITY_INCLUDE_TESTS` compile contains no named test issuer/hooks or ACK surface within the Worker namespace. Broker additionally has a real `CurrentUserOnly` test-only .NET pipe loop that serializes grant/ACK/revoke/ACK and rejects wrong ACKs by revoking the exact session. A later r6 checkpoint connects that lifecycle to the actual Unity 2022.3 Editor process and passes 2/2 test-only cases with exact PID/process-epoch/image checks, three-handle opaque adoption, exact close-before-revoke-ACK and aggregated cleanup-before-PASS ordering; its frozen-byte audit is `P0=0 / P1=0 / P2=0` for `UNITY_WORKER_TEST_PIPE_LIFECYCLE_SCAFFOLD_ONLY`. Read-query r4 passes 14/14 on a GUID-owned scratch repository with four fixed mappings and a 512 KiB handle-relative no-follow reader; Broker-to-Unity r11 passes 2/2 for five test-pipe reads. The latest .NET r5 checkpoint adds a no-path Client query composer and a second authenticated Desktop test pipe; Protocol 80, Client 12, Desktop 9 and Broker 35 pass, total 136/136. r2 and r3 were rejected by independent review; r4 closed their routing/lock-order defects but retained a managed path-delete TOCTOU, a production-compiled test barrier and non-aggregating fixture cleanup. r5 keeps exact connection/session/lease publication reservations through pipe flush and two-stage session invalidation/drain, removes the product hook, uses pinned-handle no-follow test cleanup, and attempts every fixture cleanup action before aggregating failures. Its pipe adapter remains private test code, no Desktop UI was changed or launched, and independent frozen-byte audit is `P0=0 / P1=0 / P2=0` for `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY`. Production policy/session/global process-ownership/ACK issuers, authenticated production Unity connector and production Client pipe connector remain absent; policy loading still returns `W24FS001` before listener creation and no real registered-project receipt exists. Unity cross-runtime compatibility is frozen by `ADR-003_UNITY_WORKER_PROTOCOL_COMPATIBILITY.md`. Exact scope and blockers are recorded in the Phase 2 reports, including `W24_PHASE2_DESKTOP_BROKER_READ_TRANSPORT_REPORT.md`.

Rules:

- Desktop selects only identities enumerated by Broker; it never submits an absolute root.
- The Windows production-connected profile always traverses Desktop↔Broker and Worker↔Broker authenticated sessions. Broker absence leaves Desktop disconnected; no Desktop→Worker or drive-letter fallback exists. A future replacement trusted host requires a new ADR and independent audit.
- Broker policy is host-owned and not self-authenticated by project JSON, `EditorPrefs` or environment variables.
- Broker opens volume/repository/project roots from native/global namespace and binds GUID/serial/`FILE_ID_128`, but never enumerates, parses, or reads project content.
- Broker authenticates Worker SID, PID, approved image identity, process epoch and Broker generation, requires explicit handle-lifecycle capability negotiation, then duplicates least-privilege non-inheritable handles only to that exact Worker session. Wrong/stale PID, image, epoch, generation, lease or lifecycle ACK is rejected before use.
- Unity Worker is the sole project-content reader and Unity API owner. The Phase 2 read-only host exposes only allow-listed document queries, uses relative no-follow opens from supplied handles, and has no mutation or general command surface.
- Registration drift invalidates the whole request, not individual operations.
- Every Phase 2 wire DTO has an exact registered schema, DTO↔schema parity tests, self/typed-hash vectors where applicable, and missing/extra/wrong-type/unknown-version negative tests.

Phase 2 gate includes both authenticated named-pipe roles, peer SID/PID/image/epoch/generation binding, exact-handle-target tests, junction/UNC/ADS/device/DOS-remap/TOCTOU/hardlink/handle lifecycle adversarial cases, Broker restart/generation replay, no network listener, and Desktop/Client zero direct project reads or writes. r11 demonstrates four successful allow-listed Unity reads plus a content-mismatch rejection in a GUID scratch project. The later .NET r5 sends a Client-built request through an authenticated Desktop test pipe and linearly orders response publication against registration, Desktop and Worker revocation without the r3 lock inversion; test cleanup is pinned-handle/no-follow rather than path-recursive. Both peers still use same-process test policy and the Client pipe adapter is not product code. The gate still requires the production role path plus stale-lease, mid-read drift, wrong-project and unavailable-Worker negatives that perform zero unauthorized content I/O. This gate does not admit build, Preview, test, write, or arbitrary filesystem commands.

### 4.1 Production-read DAG rebase — implementation still unpublished

`ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md` is the
normative production-read dependency owner. The docs-only
`WP-P2-PRODUCTION-READ-DAG-REBASE-1` corrected the D1→I1 live-gate cycle,
rebased P1/S1 onto D1R, and added C2 before W1. Its docs remediation is
complete and STOPPED; a fresh independent audit is still required. This is not
an implementation or package GO. It changes no
source/runtime behavior, test counts, install state, listener state, project
access, Worker route or authority, and it does not alter the current
`W24FS001`/`23` branch.

The production claim taxonomy is now explicit and non-promotable:

1. native process path observation;
2. protected installer-owned launch-file object plus content identity;
3. exact process executable backing-file object identity; and
4. current executable-memory-page integrity.

The ordinary product profile is deliberately bounded at (2): it requires a
launch-correlated protected install object with held leaf/ancestor pins,
actual ACL readback and exact file ID/hash/length, but records
`LoadedImageVerified=false`. Existing path-reopened `process-image/1` peer
facts remain compatibility facts only; they cannot be the sole production
loaded-image or launch-file admission proof. Claims (3) and (4) need a
separately authorized signed-driver/WDAC/CI design and are not silently
required, implemented or claimed by the ordinary .NET path.

Before a future ServiceHost can report `Running` or accept a production
request, the frozen plan requires: durable authenticated profile identity;
readback of fixed SCM/install-root configuration; pre-start protected
executable and ancestor pins that span hash/start/receipt; exact
PID/epoch/token/service-SID/session correlation; one-use nonce/generation
receipt; actual named-pipe ACL readback; Broker-created suspended→kill-on-close
Job→resume dedicated Unity Worker; host-owned Volume-GUID project enrollment;
and exact Worker/locator ACK supervision. The interactive Unity Editor,
Desktop paths and Desktop filesystem access are never fallbacks.

The current future implementation DAG contains thirteen nodes: closed C1
Protocol project selection; D1R fresh durable-profile remediation; C2 pure
Protocol Worker project locator; W1 production Unity Worker connector; I1 SCM
installer/live-store integration; A1 launch correlation; P1 actual pipe
ACL/session; S1 Worker supervision/global ownership; R1 host-owned project
enrollment; B1 Broker production-read convergence; D2 Client/Desktop read; E1
installed E2E; and A2 final independent audit. D1_0 is retained
STOPPED/NO-GO provenance, not a current GO node.

The rebased edges are `D1R→I1→A1`, `D1R→P1`, `D1R→S1`, `I1→R1`,
`C2→W1`, `C1+C2+W1+A1+P1+S1+R1→B1`, `B1+C1→D2`, and
`D2→E1→A2`: 13 nodes, 8 edge groups and 17 directed edges, acyclic by
construction. B1 must additionally consume I1's privileged live-store receipt;
D1R's core receipt alone cannot satisfy it. P1 and S1 therefore depend on D1R,
never historical `D1_0` or old `D1`.

Current accounting is 75 unique leaves, 12 governed sequential overlays and
one generated/read-only A2 receipt-root exception. The historical G0
`12 / 6 / 15 / 68 / 1` accounting is provenance only. Exact likely ownership
envelopes, pre-hashes, receipts, STOP conditions and the adversarial matrix
are normative in ADR-004. Before B1 every production-entry smoke remains exact
`W24FS001`/`23` before listener/path/project I/O; test peers, HandleProbe,
test issuers, scratch projects and legacy Unity receipts can satisfy none of
these production dependencies.

The rebase resolves the current blockers without enabling a route:

1. D1_0 stays a three-source fail-closed NO-GO checkpoint. A new D1R writer
   overlays precisely those same files to correct strict
   `ACCESS_SYSTEM_SECURITY` opens, pending-rename `DELETE`, secret zeroing,
   distinct root/file ACLs, suffix rollback and ordinal manifests. Its core
   acceptance cannot claim a successful strict store. I1 alone provisions the
   pinned root with enabled `SeSecurityPrivilege`, needed
   `ACCESS_SYSTEM_SECURITY` and rename rights, and makes the first real
   strict commit/reopen/readback receipt.
2. C2 is a pure 16-path Protocol/schema/test/vector contract. Its seven new
   leaves are pre-absent; its nine shared registry/codec/test/verifier paths
   overlay C1 only sequentially under exact pre-hashes. It introduces a
   no-caller-path host-owned locator and a distinct locator ACK bound to typed
   identities, session/process epoch and broker/registration/enrollment
   generations. The ACK is not a handle-grant ACK. Existing test-project
   GoldenVectors globbing makes the planned vector path sufficient without
   csproj or lock mutation; W1 later replays the same bytes in Unity.
3. W1 depends only on C2 plus ADR-003 and retains its original exact nine Unity
   files. It is a narrow composition shim, not a second normative protocol
   implementation: only for those nine paths, ADR-003 §4's ownership sentence
   `Unity 侧只允许新增：` and its following two-path listing are superseded as a
   location exception. All other ADR-003 rules remain intact. W1 may only
   compose the frozen read-only `Worker/Protocol/W24S6WorkerProtocolCodec`
   canonical primitives with C2 schemas and immutable golden bytes; it may not
   declare independent DTOs, hashes, canonicalization, registry/token entries,
   message kinds, schemas, encoders or decoders. Its exact planned test file
   may only replay the existing ADR-003 adapter and C2 vectors. W1 remains
   dormant: no pipe, project read, session issuer, handle grant or authority. A
   Unity main-UI edit, net8 copy, caller path, independent wire surface or a
   tenth file is STOP.
4. A1 owns only launch-correlation primitives. B1 depends on
   `C1+C2+W1+A1+P1+S1+R1`, alone owns the ServiceHost `Program.cs`,
   service-host `.csproj`, `WindowsScmServiceHost.cs`, final `Running`
   guard and convergence/integration tests, and consumes I1's live-store
   receipt in addition to each declared predecessor receipt.
5. P1 must apply and read back exact owner/group/DACL/SACL/ACE/mask/protection
   on **each serving pipe instance** before `ConnectNamedPipe`/accept. The
   first serving receipt is a `Running` prerequisite; bootstrap/non-serving
   receipts cannot substitute, and later instances fail closed independently.
6. S1's Job is lifecycle containment only, not a filesystem/capability
   sandbox. Ordinary profile excludes code injection into an already
   authenticated Desktop/dedicated Worker and malicious Editor assemblies in
   an explicitly enrolled (therefore trusted-code) project. Same-user pipe
   impersonation, namespace/path/reparse and PID-reuse attacks remain in
   scope. Restricted token/AppContainer/WDAC/HVCI/sandbox protection is a
   separate future profile/package.
7. ADR-004 section 9 is the sole exact 13-node envelope source. It records 75
   unique leaves, 12 governed sequential overlays and one receipt exception,
   rather than a false global zero-overlap claim. W1 retains every new Unity
   `.cs` and matching `.meta` plus its new directory `.meta`, but those nine
   paths are composition-only and cannot fork ADR-003's one normative adapter;
   E1 retains `tests/EndToEnd/packages.lock.json`. Future allow-lists may only
   shrink.

## 5. Phase 3 — Unity Worker commands and jobs

Proposed Protocol files:

```text
src/VFXComposer.Protocol/Commands/
  CommandEnvelope.cs
  ValidateRecipeCommand.cs
  BuildCandidateCommand.cs
  OpenPreviewJobCommand.cs
  ClosePreviewJobCommand.cs
  SetPreviewPlaybackCommand.cs
  ValidatePatchCommand.cs
  ApplyPatchCommand.cs
  RunFocusedTestsCommand.cs
  CancelJobCommand.cs
src/VFXComposer.Protocol/Jobs/
  JobProgress.cs
  JobLogEvent.cs
  JobArtifact.cs
  JobCompletion.cs

src/VFXComposer.Client/Commands/
  WorkerCommandClient.cs
  CommandIdempotencyStore.cs
src/VFXComposer.Client/Jobs/
  JobSubscription.cs
  JobCancellation.cs
  JobEventCorrelator.cs

services/VFXComposer.Broker/Commands/
  WorkerCommandRouter.cs
  CommandAdmission.cs
services/VFXComposer.Broker/Jobs/
  BrokerJobRegistry.cs
  BrokerJobBackpressure.cs
  BrokerJobAudit.cs

apps/VFXComposer.Desktop/ViewModels/
  JobsViewModel.cs
apps/VFXComposer.Desktop/Views/
  JobsView.axaml

docs/schemas/desktop/commands/
  vfxcomposer-command-envelope-v1.schema.json
  vfxcomposer-validate-recipe-command-v1.schema.json
  vfxcomposer-build-candidate-command-v1.schema.json
  vfxcomposer-open-preview-job-command-v1.schema.json
  vfxcomposer-close-preview-job-command-v1.schema.json
  vfxcomposer-set-preview-playback-command-v1.schema.json
  vfxcomposer-validate-patch-command-v1.schema.json
  vfxcomposer-apply-patch-command-v1.schema.json
  vfxcomposer-run-focused-tests-command-v1.schema.json
  vfxcomposer-cancel-job-command-v1.schema.json
docs/schemas/desktop/jobs/
  vfxcomposer-job-progress-v1.schema.json
  vfxcomposer-job-log-event-v1.schema.json
  vfxcomposer-job-artifact-v1.schema.json
  vfxcomposer-job-completion-v1.schema.json
```

Proposed Unity Worker files:

```text
project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/
  W24S6WorkerHost.cs
  W24S6WorkerDispatcher.cs
  W24S6WorkerCapabilityRegistry.cs
  W24S6WorkerJobRegistry.cs
  W24S6WorkerTransaction.cs
  W24S6WorkerRecovery.cs
  Commands/W24ValidateRecipeHandler.cs
  Commands/W24BuildCandidateHandler.cs
  Commands/W24PreviewJobHandler.cs
  Commands/W24PreviewPlaybackHandler.cs
  Commands/W24PatchCommandHandler.cs
  Commands/W24FocusedTestsHandler.cs
```

Proposed Worker tests:

```text
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WorkerProtocolTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WorkerTransactionTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WorkerRecoveryTests.cs
project/Packages/com.vfxcomposer.unity/Tests/PlayMode/W24S6WorkerPreviewTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WorkerPatchCommandTests.cs
src/VFXComposer.Client.Tests/CommandAndJobCorrelationTests.cs
src/VFXComposer.Client.Tests/CancellationAndDisconnectTests.cs
services/VFXComposer.Broker.Tests/CommandAdmissionTests.cs
services/VFXComposer.Broker.Tests/JobBackpressureAndAuditTests.cs
apps/VFXComposer.Desktop.Tests/JobsViewModelTests.cs
src/VFXComposer.Protocol.Tests/CommandJobSchemaParityTests.cs
```

Rules: exact command allow-list and schema registry, content identities, user-confirmation policy, idempotency, staging/second replay/commit, invocation-owned rollback, structured progress, bounded backpressure, cancellation and crash recovery. Client correlates only exact request/job identities; Broker admits and audits but cannot forge Worker completion; Desktop renders typed job state only. Worker never accepts caller output paths or raw `AssetDatabase` operations.

Phase 3 gate: every command/job DTO passes schema parity, golden-vector and unknown/missing/extra/wrong-type/version/hash negatives. An isolated scratch project demonstrates immediate admission replay before each action, exact before/after tree accounting, allow-listed Unity API effects, natural Unity process exit and zero residue. Duplicate commands create no duplicate asset; bounded backpressure is enforced; cancel and Desktop/Broker/Worker crash leave no partial promotion; restart resumes or deterministically fails; no component can forge completion from a stale event or transport success.

## 6. Phase 4 — Preview and Review/Evidence

Desktop additions:

```text
apps/VFXComposer.Desktop/ViewModels/PreviewTimelineViewModel.cs
apps/VFXComposer.Desktop/ViewModels/EvidenceBrowserViewModel.cs
apps/VFXComposer.Desktop/Views/PreviewTimelineView.axaml
apps/VFXComposer.Desktop/Views/EvidenceBrowserView.axaml
src/VFXComposer.Protocol/Preview/PreviewArtifactManifest.cs
src/VFXComposer.Protocol/Evidence/EvidenceSummary.cs
src/VFXComposer.Protocol/Commands/RunAutomaticReviewChecksCommand.cs
src/VFXComposer.Protocol/Commands/WriteReviewEvidenceCommand.cs
src/VFXComposer.Client/Preview/PreviewArtifactClient.cs
src/VFXComposer.Client/Evidence/EvidenceQueryClient.cs
src/VFXComposer.Client/Evidence/ReviewCommandClient.cs
services/VFXComposer.Broker/Queries/PreviewEvidenceQueryRouter.cs
services/VFXComposer.Broker/Commands/ReviewCommandRouter.cs
project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Queries/W24PreviewArtifactQueryHandler.cs
project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Queries/W24EvidenceSummaryQueryHandler.cs
project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Commands/W24AutomaticReviewChecksHandler.cs
project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/Commands/W24WriteReviewEvidenceHandler.cs
docs/schemas/desktop/preview/vfxcomposer-preview-artifact-manifest-v1.schema.json
docs/schemas/desktop/evidence/vfxcomposer-evidence-summary-v1.schema.json
docs/schemas/desktop/evidence/vfxcomposer-run-automatic-review-checks-command-v1.schema.json
docs/schemas/desktop/evidence/vfxcomposer-write-review-evidence-command-v1.schema.json

apps/VFXComposer.Desktop.Tests/PreviewIdentityTests.cs
apps/VFXComposer.Desktop.Tests/EvidenceAuthorityPresentationTests.cs
apps/VFXComposer.Desktop.Tests/RenderedSnapshotTests.cs
apps/VFXComposer.Desktop.Tests/KeyboardFocusAccessibilityTests.cs
src/VFXComposer.Protocol.Tests/PreviewEvidenceSchemaParityTests.cs
src/VFXComposer.Client.Tests/PreviewEvidenceCorrelationTests.cs
services/VFXComposer.Broker.Tests/PreviewEvidenceRoutingTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6PreviewArtifactQueryTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6EvidenceSummaryQueryTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6AutomaticReviewChecksCommandTests.cs
project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6WriteReviewEvidenceCommandTests.cs
docs/stage-notes/W24_STANDALONE_DESKTOP_PREVIEW_EVIDENCE_REPORT.md
```

Unity Worker produces stills/video/structured timeline; Desktop never embeds or controls an Editor Scene directly. Every preview binds project/candidate/contract/build/capture/tool identities and physical/typed hashes. Old, cross-project or drifted media fails closed.

Review states remain distinct:

- Machine evidence result
- `VISUAL_PENDING`
- Visual QA report presence/result
- user verdict record
- L3
- L4

No image or report automatically grants user verdict or L4.

Phase 4 gate:

- every Preview/Evidence wire DTO passes exact schema, DTO parity, typed/self-hash and negative-shape tests;
- current project/candidate/contract/build/capture/tool and physical/typed media identities are bound end to end;
- stale, cross-project, wrong-build, missing, tampered and hash-drifted media/evidence fail closed;
- Desktop rendered snapshots cover frozen scale/theme states, plus keyboard, focus and accessibility behavior; a human reviews Desktop presentation separately from Unity effect quality;
- tests prove machine result, `VISUAL_PENDING`, Visual QA, user verdict, L3 and L4 are non-equivalent and that no ordinary UI or transport DTO can issue or promote authority;
- independent Protocol/Client/Desktop/Broker/Worker source identities and receipts are recorded; no r31/r32/r35/r36 receipt is reused as the direct Phase 4 receipt.

## 7. Phase 5 — parity, installation and final gates

Proposed packaging/tests:

```text
build/Desktop/
  build.ps1
  package.ps1
  verify-package.ps1
tests/EndToEnd/
  DesktopBrokerWorkerE2ETests.csproj
  InstallUpgradeRollbackTests.cs
  DisconnectRecoveryTests.cs
  CrossProjectIsolationTests.cs
  WorkerCrashRecoveryTests.cs
docs/stage-notes/W24_STANDALONE_DESKTOP_FINAL_REPORT.md
```

Gate matrix:

- clean Windows install/uninstall/upgrade/rollback;
- Broker service/agent lifecycle and ACL;
- supported Unity 2022.3 connection;
- project registration, Library query, candidate build, preview, review and disconnect recovery;
- full Protocol/Client/Desktop/Broker unit and integration tests;
- Unity Edit/Play/Player/graphics-backed focused regressions;
- IPC security, transaction recovery and no-partial-promotion;
- every migration-matrix `MUST-MIGRATE` row reaches its required parity gate; no `KEEP-DISABLED` legacy dependency is silently hidden;
- user explicitly decides whether the old Unity UI becomes hidden diagnostic fallback.

## 8. Evidence naming and non-rebinding rule

Each new component receives its own source-set hash and run directory. Required evidence roots must distinguish at least:

```text
.codex_tmp/w24-desktop-*/
.codex_tmp/w24-protocol-*/
.codex_tmp/w24-broker-*/
.codex_tmp/w24-worker-*/
```

An old Unity XML/log may be listed only as predecessor regression evidence. It cannot be the direct receipt for a new Desktop, Client, Broker, IPC, installer or Worker source hash.

## 9. Work sequencing

1. `[COMPLETE]` Finish Phase 0 documents.
2. `[COMPLETE]` Independent Phase 0 read-only audit: P0=0, P1=0.
3. `[COMPLETE]` Create only the admitted Phase 1 solution/code roots.
4. `[COMPLETE]` Bounded Phase 1 implementation, receipts and independent frozen-byte audit: `P0=0 / P1=0 / P2=5`, scoped to disconnected Desktop and shared Protocol only.
5. `[IN PROGRESS / FOUNDATION ONLY]` Phase 2 protocol and dormant Broker security foundation; production connection remains `NO_GO`.
6. `[NOT STARTED]` Finish trusted Broker policy/ACL issuer, production Unity Worker adapter/transport and registration gate before any production read; the accepted r11 connector/read transport remains test-only.
7. `[IN PROGRESS / DESKTOP-TO-BROKER TEST READ SCOPED GO]` Finish production Broker routing plus Client/Desktop connection and real registered-project read gate before command transport; r11 proves the separate Broker-to-Unity scratch route, while .NET r5 has `P0=0 / P1=0 / P2=0` only for a Client-built request and deadlock-free revocation-linearized response publication over private same-process Desktop/Worker test pipes.
8. `[AUTHORIZED / CONTRACT FOUNDATION ONLY]` Freeze future Phase 3 pure-C# command/job DTOs, exact schemas, strict codec and golden vectors under the 2026-08-27 user exception. Phase 3 runtime and production command transport remain `NOT STARTED`; Unity Worker implementation waits for the audited Protocol freeze and Phase 2 gate as applicable.
9. `[NOT STARTED]` Finish Worker transaction/recovery gate before write-capable UI.
10. `[NOT STARTED]` Finish Preview/Evidence identity gates before review UI parity.
11. `[NOT STARTED]` Finish install/E2E/parity/user decision before hiding old Unity UI.

## 10. Explicitly deferred or blocked

- full110 formal projection: blocked only on exact immutable QA `model-version-id`; do not guess.
- production project read: blocked on trusted Broker issuer/ACL, authenticated production Unity connection, Broker/Client/Desktop routing and an end-to-end registered-project read gate; the test-only r11 Broker-to-Unity scratch route is insufficient.
- production ordinary-profile launch correlation: blocked on durable profile,
  protected install/SCM readback, pre-start file/ancestor pins, one-use
  receipt, actual service token/SID/session/epoch facts, actual pipe ACL
  readback, dedicated Worker Job supervision and host-owned project
  enrollment. It must continue to report `LoadedImageVerified=false`.
- strict loaded-image/backing-file or current-memory-page verification: blocked
  on a separately authorized signed-driver/WDAC/CI high-assurance design;
  no user-mode path reopen, process-image hash or `ReadProcessMemory` result
  may stand in for it.
- production command transport: blocked until Phase 2 security gate.
- visual pass, user verdict, L3/L4: always preserve existing authority rules.
- third-party MCP/Coplay installation: still requires separate user authorization and is not part of Phase 1.
