# W24 program control

> **CURRENT CONTROL PLANE — U6 FINAL GO / A0–A5 CLOSED / A6 SOLE ACTIVE (2026-08-29).** ADR-005's USER_MODE route is closed at `100/100`; ADR-006 controls the separate post-U6 AI-provider route with user-owned opaque endpoints. A5 local loopback E2E is accepted; A6 is the only active, read-only audit node, and AI functionality final GO is pending its result. Old Service/SCM/privileged nodes are historical only and are neither active, schedulable, auditable, nor blockers.
>
> Normative tokens: `USER_MODE_LOCAL_CREATIVE_TOOL_V1` and `AI_PROVIDER_TWO_CHANNEL_ROUTING_V1`.

## Current user-mode integration state

| Item | Current state | Boundary |
|---|---|---|
| U0 architecture | `CLOSED — USER_MODE_ARCHITECTURE_SIMPLIFICATION / DOCS ONLY` | Commit `53c1eeb4577a7067d8702fdea9866adf01733191`; single-user local trusted authoring architecture only, with no runtime or production GO. |
| U1 Worker connector | `CLOSED / INTEGRATED — C3_W1_ACTUAL_UNITY_WORKER_CONNECTOR` | Commit `48cd27103f8fe0c510770b1584b326f55fca3485`; declared U1 gates are complete. No Desktop integration, project read, arbitrary path, or authority claim. |
| U2 child-pipe session | `CLOSED / INTEGRATED — USER_MODE_CHILD_PIPE_SESSION` | Commit `4b2f9a81a82911d68b8b64864ae05a03f9690b2e`; its audit recorded `P1=3`, then one remediation closed with `42/42` three times and Broker `171/171`. This is not a claim of a second independent audit. |
| U3 selection/read | `CLOSED / INTEGRATED — USER_PROJECT_SELECTION_READ_CONTAINMENT` | Source commit `0123616e21d656b2374809a13aeb2769f0324e7e`, merged at `027ba07448dd6d4a0741a67937427cd2d37b2649`; exact seven files; Broker target `8/8`, Unity EditMode `9/9`, no-tests PASS, unified Broker `179/179`, manifest SHA-256 prefix `b716…`. |
| U4 Desktop integration | `CLOSED / INTEGRATED — DESKTOP_USER_MODE_INTEGRATION` | Source commit `2295b022348dc1514c72846533b86430bc4762ad`, integrated by `e1a6a9a37d3125717afbe795d283a07ffa242060`; accepted targets Protocol `108/108`, Client `14/14`, Broker `183/183`, Desktop `12/12`; r2 gate manifest `b741fef9ab35a683363993cfeeb74abd2b1cbc26f5e3988574febfe1349a66eb`. |
| U5 local E2E | `CLOSED / SCOPED GO — LOCAL_ORDINARY_USER_E2E` | Source commit `365e7612b1be276aa74f4ab36f40482a0858e1ae`, integrated at `b9de2eb47e4e9d9ea29e0490b9dfc745a4dc307d`; exact 17-file closeout and independent acceptance `P0/P1/P2=0/0/0`. |
| U6 final audit | `CLOSED — FINAL GO — P0/P1/P2=0/0/0` | Accepted independent receipt `u6-independent-final-audit-20260828T232640380Z`; no U6 repair or source work remains. |
| USER_MODE main architecture | `CLOSED — 100/100` | ADR-005 route is complete; this is not an AI-provider or external-service activation. |
| Post-U6 AI A0 | `CLOSED — DOCS ONLY`; endpoint contract rebased in the current seven-document decision update. | Existing U6 unified-gate evidence only, no project-gate rerun. |
| A1 `AI_PROVIDER_FOUNDATION` | `CLOSED — FINAL ACCEPTED — GO`. | Merged source commit `698e770a35062cc4135872147a401dce40adcb51`; `OpaqueEndpoint` is exact opaque storage, and A1 contains no real Chat/Image HTTP. |
| A2 `WP-AI-CHAT-CHANNEL` | `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. | `55ee0993f71375ee0245cbee54815e7988fe04fd` plus redirect closure `2678cb62be9ac9ff5a05c9a5b605a75c60effb5c`; Chat `23/23 × 3`. |
| A3 `WP-AI-IMAGE-CHANNEL` | `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. | `c7c4adcfcc80c732bfaf87b0dfea11294b4af741` plus redirect closure `12b58ac69efe3175cf49a6ee129b3784b5b3da5c`; Image `20/20`. |
| A4 Desktop wiring | `CLOSED — FINAL GO — P0/P1/P2=0/0/0` | Commits `fc986d11`/`ffc9f609`/`cc5ff806`; receipt `186/186`; AI `77/77`, Desktop `22/22`, `423/423` total; root/residue `0`. |
| A5 mock E2E | `CLOSED — FINAL ACCEPTED / GO — P0/P1/P2=0/0/0` | `9152c7e6` + `14abb1d3`, merged `c6f9920f`; real-loopback `11/11`, receipt `a5-final-acceptance-14abb1d3-bootstrap`, `209` hashes, locks unchanged, residue `0`. |
| A6 AI final audit | `ACTIVE — SOLE READ-ONLY AUDITOR — WP-AI-PROVIDER-FINAL-AUDIT` | Whole-A0–A5 audit only; no product development or repair. |
| Runtime | `USER_MODE FINAL GO; AI FINAL GO PENDING A6` | Default Broker launch with no arguments remains stderr `W24FS001`, exit `23`; no final AI-provider functionality claim is released. |

The product trusts the current logged-in user, intentionally launched Desktop/Broker/Worker or Unity host, and explicit user-selected project. It does not defend against malicious same-user software, administrator/kernel control, or offline tampering. It must defend cross-user pipe access, stale/cross-generation sessions, wrong project, protocol drift, PID reuse, unexpected release-layout path, leakage, crash/disconnect, and orphan cleanup.

The completed USER_MODE DAG is:

`U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`.

| Node | Control identity | Publication dependency |
|---|---|---|
| U0 | `CLOSED` architecture simplification; seven docs, one docs gate and one commit; no handoff micro-package. | Current baseline. |
| U1 | `CLOSED / INTEGRATED` combined C3+W1 actual Unity Worker connector, commit `48cd27103f8fe0c510770b1584b326f55fca3485`. | U0. |
| U2 | `CLOSED / INTEGRATED` ordinary-user Broker/Worker child process, pipe, nonce, generation, handle/epoch session and cleanup, commit `4b2f9a81a82911d68b8b64864ae05a03f9690b2e`. | U0; parallel U1 ownership is complete. |
| U3 | `CLOSED / INTEGRATED` explicit user project selection and restricted Worker-only read containment, source commit `0123616e21d656b2374809a13aeb2769f0324e7e`. | U1 + U2 closed integration outputs. |
| U4 | `CLOSED / INTEGRATED` Desktop ordinary-user launch, explicit selection, read presentation and fail-closed recovery with zero direct project I/O. | U3 closed integration output. |
| U5 | `CLOSED / SCOPED GO — LOCAL_ORDINARY_USER_E2E`; standalone ordinary-user Worker and real local E2E are accepted at source `365e7612b1be276aa74f4ab36f40482a0858e1ae`, integration `b9de2eb47e4e9d9ea29e0490b9dfc745a4dc307d`. | Accepted U4 integration. |
| U6 | `CLOSED — FINAL GO — P0/P1/P2=0/0/0`; accepted independent frozen-byte/evidence audit. | Frozen U0-U5 integration and accepted final receipt. |

The exact formal post-U6 AI DAG is `A0 -> A1 -> (A2 || A3) -> A4 -> A5 -> A6`. A0-A5 are closed; A2/A3/A4 are final GO and A5 is `CLOSED — FINAL ACCEPTED / GO`, each at `P0/P1/P2=0/0/0`. A6 `WP-AI-PROVIDER-FINAL-AUDIT` is the sole active read-only package. The AI program remains separate from the closed USER_MODE `100/100` accounting, and its functionality final GO remains pending A6.

### U3 closed evidence

U3 authored exactly these seven files and no others:

1. `services/VFXComposer.Broker/Registration/UserModeProjectSelectionStore.cs`
2. `services/VFXComposer.Broker/Ipc/UserModeProjectReadSession.cs`
3. `services/VFXComposer.Broker.Tests/UserModeProjectSelectionReadTests.cs`
4. `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/W24S6UserModeProjectReadSession.cs`
5. `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/W24S6UserModeProjectReadSession.cs.meta`
6. `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6UserModeProjectReadSessionTests.cs`
7. `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6UserModeProjectReadSessionTests.cs.meta`

U3 is closed at source commit `0123616e21d656b2374809a13aeb2769f0324e7e` and integrated by merge commit `027ba07448dd6d4a0741a67937427cd2d37b2649`. Its closeout records Broker target `8/8`, Unity EditMode `9/9`, no-tests PASS, unified Broker `179/179`, and controller-supplied manifest SHA-256 prefix `b716…`. Its route remains only an explicit current-user local project selection converted into a session-bound restricted locator and a bounded Worker-side read; it grants no Desktop project I/O, command, mutation, evidence, verdict, or authority.

### U4 integrated closeout and retained exact ownership

U4 is closed at source commit `2295b022348dc1514c72846533b86430bc4762ad` and integrated by merge commit `e1a6a9a37d3125717afbe795d283a07ffa242060`. Its accepted completion targets are Protocol `108/108`, Client `14/14`, Broker `183/183`, and Desktop `12/12`. The r2 unified-gate receipt records a Release solution build with `0 warnings / 0 errors`, schema `22 total / 13 Phase 2 / 14 positive / 236 negative`, default Broker smoke stderr `W24FS001` with exit `23`, and receipt manifest SHA-256 `b741fef9ab35a683363993cfeeb74abd2b1cbc26f5e3988574febfe1349a66eb`.

The stopped first U4 writer remains rejected historical provenance only: it targeted a nonexistent `VFXComposer.UnityWorker.exe`, launched before explicit selection, acknowledged selection before strict C2 locator acknowledgement, and crossed its declared ownership. The integrated U4 source comprises exactly these 19 files:

1. `src/VFXComposer.Protocol/Ipc/UserModeDesktopSessionCodec.cs`
2. `src/VFXComposer.Protocol.Tests/UserModeDesktopSessionCodecTests.cs`
3. `src/VFXComposer.Client/IUserModeDesktopSession.cs`
4. `src/VFXComposer.Client/UserModeBrokerProcessHost.cs`
5. `src/VFXComposer.Client/UserModeDesktopSession.cs`
6. `src/VFXComposer.Client.Tests/UserModeDesktopSessionTests.cs`
7. `services/VFXComposer.Broker/Program.cs`
8. `services/VFXComposer.Broker/Ipc/UserModeDesktopBrokerHost.cs`
9. `services/VFXComposer.Broker.Tests/UserModeDesktopBrokerHostTests.cs`
10. `services/VFXComposer.Broker.Tests/UserModeBrokerProgramTests.cs`
11. `apps/VFXComposer.Desktop/App.axaml.cs`
12. `apps/VFXComposer.Desktop/Services/IProjectSelectionDialog.cs`
13. `apps/VFXComposer.Desktop/Services/AvaloniaProjectSelectionDialog.cs`
14. `apps/VFXComposer.Desktop/Services/AvaloniaUiDispatcher.cs`
15. `apps/VFXComposer.Desktop/ViewModels/MainWindowViewModel.cs`
16. `apps/VFXComposer.Desktop/Views/MainWindow.axaml`
17. `apps/VFXComposer.Desktop.Tests/UserModeDesktopIntegrationTests.cs`
18. `apps/VFXComposer.Desktop.Tests/UserModeProjectSelectionTests.cs`
19. `apps/VFXComposer.Desktop.Tests/NoProjectAccessSurfaceTests.cs`

U4 is ordinary-user component integration only. Broker starts no Worker until explicit project selection; after selection the Worker path is fixed as `Path.Combine(AppContext.BaseDirectory, "VFXComposer.UnityWorker.exe")`, the selected canonical root is the child `WorkingDirectory`, U2 admission precedes the U3 locator, and strict C2 acknowledgement precedes `SelectAccepted`. Reselect/restart disposes the old Worker/session. Desktop performs zero project filesystem I/O and never connects directly to Worker. A scripted peer was test-only and is not real Worker/E2E evidence. `Program.cs` retains the exact no-argument Broker behavior: stderr `W24FS001`, exit `23`, with no listener. U4 adds no Service/SCM, privilege, enrollment, strict-SACL, loaded-image, command, mutation, evidence, verdict, or authority capability.

### U5 closed exact ownership and accepted runtime/E2E evidence

`WP-USERMODE-LOCAL-E2E` is closed and owns exactly these 17 accepted files and no others:

1. `services/VFXComposer.UnityWorker/VFXComposer.UnityWorker.csproj`
2. `services/VFXComposer.UnityWorker/packages.lock.json`
3. `services/VFXComposer.UnityWorker/Program.cs`
4. `services/VFXComposer.UnityWorker/UserModeUnityWorkerHost.cs`
5. `services/VFXComposer.UnityWorker/UserModeWorkerBootstrapPeerCodec.cs`
6. `tests/VFXComposer.LocalE2E.Tests/VFXComposer.LocalE2E.Tests.csproj`
7. `tests/VFXComposer.LocalE2E.Tests/packages.lock.json`
8. `tests/VFXComposer.LocalE2E.Tests/LocalUserModeE2EFixture.cs`
9. `tests/VFXComposer.LocalE2E.Tests/LocalUserModeHappyPathTests.cs`
10. `tests/VFXComposer.LocalE2E.Tests/LocalUserModeAdversarialTests.cs`
11. `tests/VFXComposer.LocalE2E.Tests/LocalUserModeCrashRecoveryTests.cs`
12. `tests/VFXComposer.LocalE2E.Tests/LocalUserModeContractTests.cs`
13. `VFXComposer.sln`
14. `eng/run-phase2-gate.ps1`
15. `eng/phase2-baseline-roots.json`
16. `src/VFXComposer.Client/UserModeDesktopSession.cs`
17. `src/VFXComposer.Client.Tests/UserModeDesktopSessionTests.cs`

U5 is closed at source commit `365e7612b1be276aa74f4ab36f40482a0858e1ae`, integrated at `b9de2eb47e4e9d9ea29e0490b9dfc745a4dc307d`. Its independent acceptance records `P0/P1/P2=0/0/0`, exact ownership `17/17`, Protocol `108/108`, Client `16/16`, Broker `183/183`, and LocalE2E `17/17`. The external receipt is `U5-local-e2e-independent-acceptance-20260828T144950082Z`: its manifest has `121` rows, frozen-root replay has `0` mismatches, and the point-in-time residue snapshot has `0` Broker/Worker processes, `0` VFX Composer pipes, and `0` owned LocalE2E temporary roots.

The same receipt records the Release solution build at `0 warnings / 0 errors`, schema verifier PASS at `22 total / 13 Phase 2 / 14 positive / 236 negative`, and the default Broker smoke with empty stdout, exact stderr `W24FS001`, and exit `23`. Assembly binding before and after tests ties the staged LocalE2E Broker and Worker assemblies to the tested product assemblies by SHA-256/MVID; this is real staged runtime-bundle evidence, not a scripted-peer substitute.

U5 establishes only the ADR-005 ordinary-user route: a standalone Protocol-only `net8.0-windows` `VFXComposer.UnityWorker.exe`, public `UserModeDesktopSession` across Desktop/Client -> Broker -> Worker, explicit selected-root working directory, and strict C2 locator acknowledgement before bounded `LIBRARY_INDEX`/manifest reads. The accepted adversarial and lifecycle coverage addresses nonce/session/generation/PID-epoch correlation, malformed or drifting protocol/locator/path input, marker/traversal/reparse/size/JSON rejection, crash/cancel/restart/partial-frame recovery, and leak cleanup.

The other-user denial claim remains deliberately bounded to `CurrentUserOnly` static/IL plus existing unit evidence: no separate Windows account and no literal multi-user E2E result is claimed. It is not a claim against malicious same-user code, administrator/kernel control, offline tampering, or a deliberately malicious selected project. U5 introduces no Service/SCM, `LocalSystem`, privilege, SACL, enrollment, loaded-image, command, mutation, evidence, verdict, or authority capability. U6 now closes the defined ADR-005 route; hashes and signatures remain release-integrity evidence only.

### U6 final frozen-byte audit closeout

`WP-USERMODE-FINAL-AUDIT` is `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. The accepted independent receipt is `u6-independent-final-audit-20260828T232640380Z`: it reports passed summary, `16607` source-manifest rows at SHA-256 `592bfeaab629e8cb9b100cf82fd3ce95c5be23972742501be34e57f1908a2284`, frozen-root mismatch count `0`, and zero point-in-time process/pipe/owned-temp-root residue. It supersedes the prior failed publication checkpoint as the current U6 evidence.

The result closes the entire ADR-005 USER_MODE main architecture at `100/100`. It neither adds nor permits an AI provider, credential, external network request, Desktop project I/O, Unity mutation, or authority claim. The default Broker remains exact `W24FS001` stderr / exit `23` on no-argument launch.

The AI A0 documentation freeze is closed, with no U6 gate replay. ADR-006 freezes `ChatLlm` and `ImageGeneration` as isolated, exact one-profile/capability/model bindings, with Origin as metadata and explicit independent protocol. It prohibits fallback, undocumented auth, cookie/script/custom-header/DLL/TLS-bypass behavior, secret/raw diagnostic leakage, and automatic Unity writes. Provider endpoint is `OpaqueEndpoint`: a user-defined string which local configuration saves and resolves unchanged; local acceptance is never network authorization.

`A1 — AI_PROVIDER_FOUNDATION` is `CLOSED — FINAL ACCEPTED — GO` at merged source commit `698e770a35062cc4135872147a401dce40adcb51`. Its closed ownership was exactly `src/VFXComposer.AI.Contracts/**`, `src/VFXComposer.AI.Providers/**`, `src/VFXComposer.AI.Tests/**`, `docs/schemas/desktop/vfxcomposer-ai-provider-config-v1.schema.json`, `VFXComposer.sln`, `eng/verify-phase2-schemas.py`, `eng/run-phase2-gate.ps1`, and `eng/phase2-baseline-roots.json`; the verifier was the only additional file. `OpaqueEndpoint` accepts and resolves arbitrary bounded text unchanged, including user-info/query/fragment and non-URI-like text; structural/type/size failures still fail, configuration acceptance invokes no network, and local acceptance never authorizes a request. A1 also retains redaction, DPAPI/`SecretRef`, and Tom `ApiKeyProtected` exclusion, while providing no real Chat/Image HTTP.

The accepted A1 record is AI tests `23/23 × 3`, schema opaque-endpoint vectors `9`, and independent gate receipt `D:\wt\i2s-a1\.codex_tmp\a1-phase2-gate-092b7d6b3aeb4246928688323771e8b8`: self-excluded receipt manifest `167/167`, Release solution `0 warnings / 0 errors`, frozen-root replay `0` mismatches, and point-in-time runtime/pipe/owned-temp-root residue `0`. This is the `A1 CLOSED GO` evidence; no later channel package may reopen A1 ownership.

`A2 — WP-AI-CHAT-CHANNEL` is `CLOSED — FINAL GO — P0/P1/P2=0/0/0` at source `55ee0993f71375ee0245cbee54815e7988fe04fd` and redirect correction `2678cb62be9ac9ff5a05c9a5b605a75c60effb5c`; `A3 — WP-AI-IMAGE-CHANNEL` is equivalently closed at `c7c4adcfcc80c732bfaf87b0dfea11294b4af741` and `12b58ac69efe3175cf49a6ee129b3784b5b3da5c`. Their accepted tests are Chat `23/23 × 3`, Image `20/20`, and the solution build is `0 warnings / 0 errors`. The prior channel roots remain immutable output except A4's expressly authorized Chat overlays. Their request-time endpoint/no-path-append/per-request-auth/no-fallback/redacted-failure and image redirect/MIME/byte/dimension/hash/no-auth-forwarding rules remain binding.

`A4 — AI_DESKTOP_WIRING` is `CLOSED — FINAL GO — P0/P1/P2=0/0/0`. Its accepted implementation sequence is `fc986d11` (Desktop provider wiring), `ffc9f609` (health/secret remediation), and `cc5ff806` (final baseline). Final evidence is receipt `186/186`, AI `77/77`, Desktop `22/22`, `423/423` total tests, frozen-root replay `0`, and owned runtime/pipe/private-artifact residue `0`. It closes its exact former Desktop/AI scope and its zero-auto-network, secret/revoke, redaction, decoder-stream, and no-project-write controls; it is not a mock-handler cross-channel E2E receipt.

`A5 — AI_MOCK_E2E` is closed at `FINAL ACCEPTED / GO — P0/P1/P2=0/0/0`. Its all-and-only implementation scope was `tests/VFXComposer.AiLocalE2E.Tests/**`, `src/VFXComposer.AI.Providers/Desktop/ProviderDesktopRuntime.cs`, `VFXComposer.sln`, `eng/run-phase2-gate.ps1`, and `eng/phase2-baseline-roots.json`. `ProviderDesktopRuntime.cs` was the only production seam: its existing constructor remains, the only addition is optional `privateImageTempRoot` passed transparently to `ImageGateway`, and production `null` behavior is unchanged. The accepted commits are `9152c7e6` and `14abb1d3`, merged at `c6f9920f`.

A5 proved a real loopback `TcpListener` flow through the production runtime and production `HttpClient` handlers, with no handler injection, external-network access, or paid-provider call. The flow is Settings CRUD/DPAPI/explicit bindings -> Create Chat -> Preview Image through the existing decoder for base64 and URL image responses, covering restart persistence, channel isolation, opaque endpoint handling, failures, revoke/fail-closed containment, redaction, private-artifact cleanup, and no project write. Receipt `a5-final-acceptance-14abb1d3-bootstrap` records `11/11`, `209` SHA-256 receipt hashes, root replay `0`, locked Release build, product-assembly binding, unchanged tracked locks, and `vfxcomposer-a5` residue `0`; scope drift, nonzero gate/residue result, changed production `null` behavior, handler injection, external/paid traffic, fallback, leak, or project write remains STOP.

`A6 — WP-AI-PROVIDER-FINAL-AUDIT` is the sole active package. It audits all frozen A0–A5 source and evidence read-only; no product development, repair, implementation, or product-gate rerun is allowed. Its audit must verify the two explicit channel routes and no fallback, opaque configuration separated from request-time parsing, secret/sensitive-data redaction, no Broker/Worker/Unity provider-network role, real loopback with no paid external calls, frozen gates and tracked locks, and cleanup/residue. Any finding is STOP and must be reported without repair. Do not assert AI functionality final GO before A6 completes.

---

## Historical pre-U0 control ledger (superseded for product delivery)

Status date: `2026-08-28`  
Controller role: `planning / package publication / dependency coordination / merge and integration gate only`  
Current product direction: `Standalone Avalonia Desktop + pure C# Protocol/Client + Unity Package Worker + Windows Broker`  

## 1. Non-negotiable constraints

1. The legacy Unity Editor main window is under STOP-THE-LINE. Preserve its source and r31/r32/r35/r36 compatibility evidence. Only a compile failure, data-corruption defect, or security regression may justify a narrowly reviewed fix. Do not add tabs, polish, Player UI, or MCP entrypoints there.
2. Desktop never directly reads or writes `Assets`, `Packages`, `ProjectSettings`, or arbitrary caller paths. Broker owns trusted process/root admission only; Unity Worker is the only project-content and Unity-API owner.
3. Production registration, real project read, Worker command transport, mutation, evidence authority, user verdict, L3 and L4 remain fail closed until their own gates pass. `W24FS001` must remain before production listener/path/project I/O while the issuer is pending.
4. Shared Protocol is Unity-free and transport-free. Every wire type requires an exact versioned schema, strict decoded-key handling, exact required/unknown-field rejection, typed/self-hash parity where applicable, and positive/negative/golden-vector tests.
5. Transport authentication, job completion, machine evidence, visual evidence, user verdict, L3 and L4 are distinct domains. UI state or a decoded DTO never creates authority. User visual sign-off remains explicit and write-once.
6. Old Unity, S5 or calibration receipts cannot be rebound to Desktop, Broker, Worker transport or a later source revision.
7. Do not use public HTTP, arbitrary TCP, production stdio MCP, caller paths, `EditorPrefs`, environment variables, project JSON or Desktop status as a trust root. Windows production transport is an authenticated local named pipe behind an independently trusted Broker issuer.
8. One writer owns Shared Protocol/schema at a time. Writer and auditor are separate tasks. A work package stops after one scoped GO and cannot continue into the next slice.
9. Controller model is explicitly `gpt-5.6-sol` with `high` reasoning. Every independent development task is explicitly `gpt-5.6-terra` with `max` reasoning. Read-only audit model policy is unchanged unless the user specifies otherwise.
10. The earlier user-reported quota thresholds are historical. On `2026-08-28` the user reported `100%` and explicitly removed quota-based scheduling and stop conditions. Scope, dependency, ownership and safety gates still apply without relaxation.
11. After an atomic subtask/package reaches `STOPPED`, its writer and auditor are retired and must not be reused for a later package. Every later package receives a fresh Terra Max writer and a fresh read-only auditor so completed-task context cannot accumulate across packages. The same agent may close findings only while its original package is still incomplete; it must retire immediately after that package's final scoped verdict.

## 2. Current phase state

| Area | Current state | What it proves | Still NO-GO |
|---|---|---|---|
| Phase 0 architecture | `GO` | Desktop/Protocol/Client/Broker/Worker ownership and legacy-UI migration boundaries are documented and independently audited. | No runtime capability. |
| Phase 1 | `GO — DISCONNECTED_DESKTOP_AND_SHARED_PROTOCOL_ONLY` | Pure Protocol, disconnected Client, Avalonia shell, strict codec/schema and offline build receipts. | Connection, project access, commands and authority. |
| Phase 2 foundation | `IN PROGRESS` | Dormant Broker production trust/ACL profile, test-issued handle lifecycle, Worker read query, test-pipe routes, revocation-linearized Desktop-to-Broker r5 scaffold, independently audited issuer-host/bootstrap and process-token observations, a standalone framework-dependent dormant SCM service-host runtime, a dormant least-privilege SCM installation-policy candidate, an independently audited supplied-in-memory executable-content identity policy, and an independently audited caller-supplied pinned-handle content-observation package. | No installed/registered service or SCM mutation; no successful live service attestation, loaded-image equivalence or signature verification; no production issuer/listener/ACL application/policy/connectors; no project access, Worker/Desktop/Unity production route, commands or authority; Phase 2 gate remains open. |
| Latest runtime test node | `GO — DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY` | r5: Protocol 80, Client 12, Desktop 9, Broker 35; total 136/136; independent audit P0=0/P1=0/P2=0. | Desktop product connector, production read, commands and authority. |
| Latest package closeout | `CLOSED / SCOPED GO — DORMANT_DURABLE_PRODUCTION_PROFILE_STORE_REMEDIATION_ONLY` | D1R `WP-P2-DURABLE-PRODUCTION-PROFILE-REMEDIATION`: final independent audit closed all four findings with `P0=0 / P1=0 / P2=0`; source manifest 3/3 `337198737cfd647422a7f1a82c1073d936b6ea5d4f462b18e082b5cc929dd90c`; self-excluded receipt manifest 65/65 `ba2341f446b6b6bff98098a0c5067b23c1dfdca0aa4f26743039b43c183b1726`; final roots 9/9 `1b9b145a17a38f20609df505828dbc877f9df6c8fbb8e5f295fe0865351afa21`; Broker 40 / `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c`; Broker.Tests 18 / `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66`; target 35/35, full 113/113, schema 22/13/14/236, builds 0/0, smoke `W24FS001`/23. | Dormant source/static/synthetic plus no-privilege runtime-negative remediation only. No privileged success, `SeSecurityPrivilege` enablement, root provisioning, live commit/reopen/readback/receipt, production wiring or authority. I1 is the next DAG dependency but is not published or started; production, Phase 2 and authority remain NO-GO. |
| Active package | `ACTIVE — WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION / DORMANT_PER_SERVING_PRODUCTION_NAMED_PIPE_ACL_PROVISIONING_AND_EXACT_READBACK_ONLY` | Sole fresh Terra Max P1 writer is authorized for exactly four new Broker/Broker.Tests leaves and package metadata only. This publication is a bounded implementation authorization, not an acceptance receipt or a production-serving claim. D1R remains final `STOPPED` scoped GO; C1/C2 remain closed GO; old D1 remains retained `STOPPED / NO-GO`; the DAG-rebase scoped GO/P2=1 record remains historical. | I1 is an external privileged-root preflight blocker and is not published or started; C3 and W1 are not published. No `Program`, `BrokerPolicy`, existing host, ServiceHost, listener activation, `Running`, project I/O, Client/Desktop/Worker/Unity route or authority change is authorized. Production and Phase 2 remain `W24FS001`/23 NO-GO. |
| Phase 3 | `PURE PROTOCOL CONTRACT FOUNDATION SCOPED GO / RUNTIME NOT STARTED` | Recovered pure-C# command/job DTO, schema, strict-codec and independent golden-vector foundation; 88/88 and independent P0/P1/P2=0. | Client/Broker/Worker transport, handlers, project I/O, mutation and authority. |
| Phase 4–5 | `NOT STARTED` | Nothing. | Preview/evidence migration, installation, parity retirement and final acceptance. |

## 3. Quota and scheduling ledger

- Current user instruction (`2026-08-28`): quota is `100%`; do not use quota percentage as a scheduling or stop condition.
- The earlier `<=10%`, `<=7%` and `<=5%` rules remain historical provenance only and are superseded by the current instruction.
- Continue to bound every package by unique objective, exact file ownership, frozen dependencies, independent audit and STOPPED handoff.
- Parallelism is a ceiling, not a target: maximum three writers plus one read-only auditor, but current budget policy defaults to one writer plus one auditor.

## 4. Repository topology exception

The repository currently has an unborn `master` branch and no commits; the existing workspace is entirely untracked from Git's perspective. A safe independent Git worktree cannot be based on this state without creating an initial commit that captures user-owned files.

Until the user separately authorizes and audits an initial baseline commit:

- development tasks run in the saved project checkout (`local`), not a fabricated worktree;
- every task has an exact, non-overlapping author-controlled source allow-list and one handoff file;
- every newly published package uses newly created writer/auditor agents; completed-package agents are never recycled into later packages;
- touching author-controlled source outside the allow-list is a STOP condition; generated build/test outputs require a separately enumerated exception in the work package;
- the controller checks concurrent drift before integration;
- no task may create commits, branches, tags or perform destructive Git operations.

This is an explicit temporary exception to the default worktree policy, not permission to broaden scope.

## 5. Controller sequence

1. Freeze these coordination documents and obtain a read-only P0/P1 audit.
2. Preserve `WP-PROTOCOL-P3` as the stopped duplicate-writer incident; do not rebind it as accepted evidence.
3. `WP-PROTOCOL-P3-RECOVERY` reached pure-contract scoped GO with independent `P0=0 / P1=0 / P2=0`.
4. `WP-BROKER-P2-PROD` reached dormant trust/ACL scoped GO with independent `P0=0 / P1=0 / P2=0`; it did not activate production admission.
5. The user authorized `WP-BROKER-P2-ISSUER-HOST` at `21%`; it reached `BROKER_ISSUER_HOST_BOOTSTRAP_ACL_DORMANT_FOUNDATION_ONLY` scoped GO with independent `P0=0 / P1=0 / P2=0`. It did not activate a service, issuer, listener, ACL, project access or authority.
6. `WP-BROKER-P2-LIVE-ATTESTATION` reached `DORMANT_WINDOWS_PROCESS_TOKEN_PATH_OBSERVATION_FOUNDATION_ONLY` scoped GO with independent `P0=0 / P1=0 / P2=0`. It proves no installed service, successful live service attestation or executable-content identity.
7. `WP-BROKER-P2-SERVICE-HOST` and its fresh-agent provenance recovery reached `DORMANT_SCM_SERVICE_RUNTIME_FOUNDATION_ONLY` scoped GO with final independent `P0=0 / P1=0 / P2=0`; neither task installed, registered or started an SCM service.
8. `WP-BROKER-P2-INSTALL-POLICY` is `CLOSED / SCOPED GO — DORMANT_SCM_INSTALLATION_POLICY_FOUNDATION_ONLY` with independent final audit `P0=0 / P1=0 / P2=0`: source manifest 3/3 `45eb51eac83ef1f8144be1a9f6e443048415d15d92fcf686e4547345b761530b`; evidence 22/22 `505db45b99bcffa9dbd9f77c2e91b92b92e7b91dd8a8bd93a53122c8daceaf39`; Broker 34 `a82899f692fd3dcb568eb5bf820f90ecf92f4054b515f8d3621a2e34c361772a`; Broker.Tests 15 `c6864861a756cd0458e40c886855ee775ef15cf3d04a5129550cf7e324d74835`; TRX 61/61; Broker.Tests and solution builds 0 warnings/0 errors; smoke `W24FS001`/23; pre-delta handoff `79aa061021d7c803b69fbd44fdf5a5643b3d06bcfc9d0b5f75a2d8cfeee9b21b`. It does not install/register/start/configure a service, call SCM, bind a binary path, prove live attestation/content identity, or activate issuer/listener/ACL/project/Worker/Desktop/Unity/commands/authority.
9. `WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY` is `CLOSED / SCOPED GO — DORMANT_EXECUTABLE_CONTENT_IDENTITY_POLICY_FOUNDATION_ONLY` with independent final audit `P0=0 / P1=0 / P2=0`: source 3/3 `ae2033ae6402d7ac40c27844ca17c9fa432cbafd840637227142a0e006c8bddb`; evidence 24/24 `a9417349bfa4c8ff0b9ceb3cafd81aa918a5e267c2c716793e04b5b4e38fe880`; Broker 36 `2a8805352c20259f1c2b06b00102f83e7982f178b4de550072595cb126af98c1`; Broker.Tests 16 `1ff9e8b497598c6a0f0a620657e24bbad04542e5e048974308cb6754393fac75`; TRX 68/68 `7d356d4732f336d12e70d1f6e56aea55cf1928c668a499b65d0435298bd63153`; Broker.Tests and solution builds 0 warnings/0 errors; smoke `W24FS001`/23; handoff pre-status `7d9e824b82b9e8a901393a688cf7493bc398bb05f0584511a5b4d0508d575ebd`. It proves only a supplied-in-memory identity policy/correlation model; it does not prove or enable file/path/handle byte observation, loaded-image equality, Authenticode/signature/certificate validation, installed service/SCM mutation, production issuer/listener/ACL/project/Worker/Desktop/Unity/commands/authority or the Phase 2 gate. The user-reported `16%` is now stale; do not publish a later package until it is refreshed.
10. `WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION` is `CLOSED / SCOPED GO — DORMANT_PINNED_EXECUTABLE_HANDLE_HASH_OBSERVATION_ONLY` with independent final audit `P0=0 / P1=0 / P2=0`: source 3/3 `571974b214be8f9146103a1bc06b7e98fe1a47eab10dae97166290d6731ac979`; evidence 24/24 `cb88bd5b42cf2807a314c820a93c2e133833fe2028129c4e353c93a866545886`; handoff pre-status `2981449f9c0808e24c3a268b6c2faa82267ae226eb3f6ca6aa9a65a789314ac2`; Broker 38 `8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760`; Broker.Tests 17 `66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60`; TRX 78/78 `c5a5cb0336e1b83c0cb003e1af03388fc886f7679b7f4ff4b96b6ade985a19d8`; Broker.Tests and solution builds 0 warnings/0 errors; smoke `W24FS001`/23; zero residue. It proves only bounded observation of a caller-supplied already-open handle, not a loaded image or image path, signature/certificate trust, service installation/SCM state, production wiring, project access, authority or the Phase 2 gate. The runtime reparse-point negative remains an explicitly unclaimed coverage residual. The user-reported `14%` is stale after final `STOPPED`; no later package may be published until refreshed, and the `<=7%`/`<=5%` rules remain in force.
11. The user reported `100%` on `2026-08-28` and explicitly removed quota-based scheduling. Continue Phase 2 through fresh bounded packages; do not relax any production, security, Unity UI, evidence or authority gate.
12. `WP-P2-PRODUCTION-READ-GATE-FREEZE` is `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_ARCHITECTURE_FREEZE_ONLY` after an independent final `P0=0 / P1=0 / P2=0` verdict. Its 12-node DAG remains an implementation blocker set; D1 remains `STOPPED / NO-GO`, and W1 is not published.
13. D1 `WP-P2-DURABLE-PRODUCTION-PROFILE` was published as the sole active implementation package. Its product allow-list was exactly two Broker files plus one Broker.Tests file; one coordination handoff and one generated receipt root were package metadata, not product-source expansion. It remained dormant and did not load or activate production policy.
14. D1 stopped `NO-GO` after independent audit. The frozen three-source checkpoint fails strict SACL and rename admission, has secret-zeroing/DACL/chain-integrity defects, and its live acceptance depends on I1 even though the frozen DAG places D1 before I1. No D1 scoped GO is granted; the bytes are retained fail-closed and production remains disabled.
15. C1 `WP-P2-PROTOCOL-PROJECT-SELECTION` is `CLOSED / SCOPED GO — PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY` after a final independent `P0=0 / P1=0 / P2=0` verdict. The external pre-status handoff SHA-256 is `8c35cae3759cd17f89d366184d74d843f8cc5904ca0a894aa4aae7da074759ef`; its exact source manifest is 12/12 `27854e8504ec8e7da12e510db906efd9f2f780577d7a1b49e55c039d7da23600` and its self-excluded receipt manifest is 10/10 `7402e367014a05b1d45d28df497de8f24563ce68d4cebed523745141583e1eef`. Final aggregates are Protocol 68 / `bac2177fab013096c2bb890596face218aba3e46387c4a1e4470ea655dc75605`, Protocol.Tests 22 / `672d85c5487281a3f0e272a08f2e265d839d098324eb1a38f6270280c40dbb03`, and desktop schemas 34 / `d2384e52ff0901f915373b3d7976067ab3d1a10897dab8b85925c70f6d2233cc`; TRX is 95/95, schema verification is 20/11/12/170, both builds are 0 warnings / 0 errors, and the smoke remains `W24FS001`/23. At C1 closeout, C1 was `STOPPED`, its writer and auditor were retired, no package was active or next published, and W1 was unpublished. D1 is not a dependency and its retained `STOPPED / NO-GO` bytes remain frozen. C1 grants no runtime, production, project I/O, lease, command or authority; production remains `W24FS001`/23 and Phase 2 remains NO-GO.
16. W1 read-only preflight stopped NO-GO because its nine Unity files had no normative host-owned locator/locator-ACK Protocol contract. The subsequent `WP-P2-PRODUCTION-READ-DAG-REBASE-1` docs-only package rebased that future dependency without publishing C2, D1R or W1.
17. `WP-P2-PRODUCTION-READ-DAG-REBASE-1` is `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY` and `STOPPED` after its final independent verdict `P0=0 / P1=0 / P2=1`. Its recorded current authored hashes are ADR-004 `e97c4a9e5c2bd20b191178732a8dc3804cac368741e410e1bb5dd58d1cd6c141`, Phase Plan `3d254384569658493cb7f0b9f5c414046861db08082d68ab6dd2dbc4c7255199`, Phase2 Report `ee7a5f935e76faf3e14d87a842eba8ff68138f8711061e60cfdca6161bd3c360`, and pre-status handoff `373ea70a286aeb61f594126227616114752a2d9c96238a05aeb1f9f67dd0ee9f`; its accounting remains `13/8/17` and `75/12/1`. P2 is an explicitly documented, nonblocking receipt-verifiability residual: the failed verifier lacks literal command/times/exit/raw and receipt-root aggregates are unrepeatable, but the independent auditor recomputed semantic gates. No receipt PASS or `P2=0` is claimed. At that historical closeout, no package was active or next published and C2/D1R/W1 were unpublished; D1 remained NO-GO, C1 remained GO, and production `W24FS001`/23, Phase 2 and authority remained NO-GO.
18. C2 `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR` is `CLOSED / SCOPED GO — PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY` and final `STOPPED` after its independent final audit `P0=0 / P1=0 / P2=0`. The audit used the external pre-status handoff SHA-256 `0a95a335e982527d5f1b5f9193547c40b0093c5832c01b6f4b88d8ee2b12698c` as input, not as a self-hash. C2 source/receipt manifests are 16/16 `f6095bb4324b33d7d196f0c0c858d52b879c79a079906b18c778781aceaade26` and 39/39 `7e6b9d2c3a963f5d5dcc1d083418e3a812152cb26e74468ff1b94ca617341cd4`; final roots are Protocol 70 / `0ce0420ff218a43b06599bdac1afdc3ff327ab2df93fc99e697b0248ebbb01b4`, Protocol.Tests 25 / `d5fcf76c8d52387effd9a3d2733d458af816dbabda7f8cd60ff5f85002587622`, and schemas 36 / `0e41683a2c93c832e5ea6d86667c726eaad3ce65336ce25bb1d9be6b5cb47538`; TRX is 104/104, schema verification 22/13/14/236, both builds 0/0, and smoke remains `W24FS001`/23. At C2 closeout the active package was `NONE / NEXT NOT PUBLISHED`; that historical state is unchanged. C2 grants no production/runtime/transport/locator issuer or ACK issuer/project I/O/handle/grant/command/authority capability; Phase 2 remains NO-GO. C1 remains GO, D1 remains NO-GO, and the DAG-rebase historical scoped GO/P2=1 record is unchanged. The quota remains 100% with no quota-based scheduling.
19. D1R `WP-P2-DURABLE-PRODUCTION-PROFILE-REMEDIATION` is `CLOSED / SCOPED GO — DORMANT_DURABLE_PRODUCTION_PROFILE_STORE_REMEDIATION_ONLY` and final `STOPPED` after an independent read-only audit closed all four findings with `P0=0 / P1=0 / P2=0`. Its exact final source hashes are Profile `636ef191393395981e991d891d4f7d43924bf1dd6230e12fef031e183d961ed1`, Store `800d7c0ab03ddb46aab8a7dce2bb8741662013ea277f27c01ca12dcec81aadb8`, and Tests `faecf035e24ee7bed5398d4f2f9facffcf5c1bf6ae0e0ddd28fa5c6fab992b0d`; the strict-Ordinal source manifest is 3/3 `337198737cfd647422a7f1a82c1073d936b6ea5d4f462b18e082b5cc929dd90c`, the self-excluded receipt manifest is 65/65 `ba2341f446b6b6bff98098a0c5067b23c1dfdca0aa4f26743039b43c183b1726`, and final roots are 9/9 `1b9b145a17a38f20609df505828dbc877f9df6c8fbb8e5f295fe0865351afa21`. The independent audit consumed handoff pre-status SHA-256 `b196070c07dc98b722cf3ca3741f1981f009c45117caa0c2d3d9f57604b74e56` as input, not as a self-hash. Final owned roots are Broker 40 / `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c` and Broker.Tests 18 / `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66`; validation is target 35/35, full Broker 113/113, schema 22/13/14/236, Broker.Tests and solution builds 0/0, and smoke `W24FS001`/23. D1R proves dormant source/static/synthetic remediation and no-privilege runtime negatives only: it did not enable `SeSecurityPrivilege`, provision a privileged root, or produce a strict live commit, reopen, readback or receipt. The inherited bare `dotnet restore VFXComposer.sln --locked-mode` NU1004 limitation remains nonblocking and unmodified; locked no-restore builds passed and no lock changed. D1 remains retained `STOPPED / NO-GO` provenance; C1 and C2 remain closed GO; the DAG-rebase record remains historical scoped GO/P2=1. At D1R closeout, the active package was `NONE / NEXT NOT PUBLISHED`; I1 was NO-GO and not published or started, W1 was not active, and production, Phase 2 and authority remained NO-GO.
20. The sole active P1 package is `WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION`, bounded to `DORMANT_PER_SERVING_PRODUCTION_NAMED_PIPE_ACL_PROVISIONING_AND_EXACT_READBACK_ONLY`. Its only product/test leaves are `services/VFXComposer.Broker/Ipc/WindowsProductionNamedPipeHost.cs`, `services/VFXComposer.Broker/Security/WindowsNamedPipeAclReadback.cs`, `services/VFXComposer.Broker.Tests/WindowsProductionNamedPipeHostTests.cs`, and `services/VFXComposer.Broker.Tests/WindowsNamedPipeAclReadbackTests.cs`; all four were pre-absent with case-insensitive collision count zero. It may create a dormant, unconnected per-instance native pipe only after binding the D1R durable profile digest/service SID to the frozen `WindowsNamedPipeAclProvisioningIntent`; each returned `SafePipeHandle` must receive native same-handle `GetSecurityInfo` exact ACL readback before any `ConnectNamedPipe` or accept. Every later instance must recreate and reread rather than inherit a cached blessing. The package cannot activate a listener, report `Running`, use `CurrentUserOnly` or a test issuer/bootstrap substitute, reopen by name, use a different readback handle, accept unreadable SACL, wire an existing host, or make a positive ordinary-token live-serving/authority claim. Required acceptance is exactly 16 new tests (8+8), target 16/16, full Broker 129/129, schema 22/13/14/236, Broker.Tests and solution builds 0/0, and empty-stdout/exact-`W24FS001`-stderr exit 23 smoke, followed by source/PE/ABI/no-wiring/frozen-root/residue/manifests evidence, a STOPPED handoff and independent audit. Any fifth source/test leaf, csproj/lock/solution/control extra or the stated security/order failure is STOP.
