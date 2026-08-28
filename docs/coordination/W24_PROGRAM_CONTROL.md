# W24 program control

> **CURRENT CONTROL PLANE — U6 USER-MODE FINAL AUDIT PUBLICATION (2026-08-28).** This section and ADR-005 supersede the pre-U0 control ledger retained below. Old Service/SCM/privileged nodes are historical only and are neither active, schedulable, auditable, nor blockers.
>
> Normative U0 architecture token: `USER_MODE_LOCAL_CREATIVE_TOOL_V1`.

## Current user-mode integration state

| Item | Current state | Boundary |
|---|---|---|
| U0 architecture | `CLOSED — USER_MODE_ARCHITECTURE_SIMPLIFICATION / DOCS ONLY` | Commit `53c1eeb4577a7067d8702fdea9866adf01733191`; single-user local trusted authoring architecture only, with no runtime or production GO. |
| U1 Worker connector | `CLOSED / INTEGRATED — C3_W1_ACTUAL_UNITY_WORKER_CONNECTOR` | Commit `48cd27103f8fe0c510770b1584b326f55fca3485`; declared U1 gates are complete. No Desktop integration, project read, arbitrary path, or authority claim. |
| U2 child-pipe session | `CLOSED / INTEGRATED — USER_MODE_CHILD_PIPE_SESSION` | Commit `4b2f9a81a82911d68b8b64864ae05a03f9690b2e`; its audit recorded `P1=3`, then one remediation closed with `42/42` three times and Broker `171/171`. This is not a claim of a second independent audit. |
| U3 selection/read | `CLOSED / INTEGRATED — USER_PROJECT_SELECTION_READ_CONTAINMENT` | Source commit `0123616e21d656b2374809a13aeb2769f0324e7e`, merged at `027ba07448dd6d4a0741a67937427cd2d37b2649`; exact seven files; Broker target `8/8`, Unity EditMode `9/9`, no-tests PASS, unified Broker `179/179`, manifest SHA-256 prefix `b716…`. |
| U4 Desktop integration | `CLOSED / INTEGRATED — DESKTOP_USER_MODE_INTEGRATION` | Source commit `2295b022348dc1514c72846533b86430bc4762ad`, integrated by `e1a6a9a37d3125717afbe795d283a07ffa242060`; accepted targets Protocol `108/108`, Client `14/14`, Broker `183/183`, Desktop `12/12`; r2 gate manifest `b741fef9ab35a683363993cfeeb74abd2b1cbc26f5e3988574febfe1349a66eb`. |
| U5 local E2E | `CLOSED / SCOPED GO — LOCAL_ORDINARY_USER_E2E` | Source commit `365e7612b1be276aa74f4ab36f40482a0858e1ae`, integrated at `b9de2eb47e4e9d9ea29e0490b9dfc745a4dc307d`; exact 17-file closeout and independent acceptance `P0/P1/P2=0/0/0`. |
| U6 final audit | `ACTIVE — WP-USERMODE-FINAL-AUDIT` | Sole active current package; read-only frozen-byte/evidence audit of the integrated U0-U5 route. Source changes are prohibited. |
| Post-U6 AI A0 | `NOT STARTED` | The two-channel AI-provider plan is queued only; it is not user-mode source, runtime, evidence, or main-architecture completion credit. |
| Runtime | `NO-GO` | Default Broker launch with no arguments remains stderr `W24FS001`, exit `23`; production remains NO-GO until U6 reaches its scoped final GO. |
| Planning baseline | `45/100 planning point; 42%-48% band` | Frozen ADR-005 weighted algorithm. Remaining plan is approximately 45%-55% shorter than the superseded privileged route. |

The product trusts the current logged-in user, intentionally launched Desktop/Broker/Worker or Unity host, and explicit user-selected project. It does not defend against malicious same-user software, administrator/kernel control, or offline tampering. It must defend cross-user pipe access, stale/cross-generation sessions, wrong project, protocol drift, PID reuse, unexpected release-layout path, leakage, crash/disconnect, and orphan cleanup.

The exact current seven-node DAG is:

`U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`.

| Node | Control identity | Publication dependency |
|---|---|---|
| U0 | `CLOSED` architecture simplification; seven docs, one docs gate and one commit; no handoff micro-package. | Current baseline. |
| U1 | `CLOSED / INTEGRATED` combined C3+W1 actual Unity Worker connector, commit `48cd27103f8fe0c510770b1584b326f55fca3485`. | U0. |
| U2 | `CLOSED / INTEGRATED` ordinary-user Broker/Worker child process, pipe, nonce, generation, handle/epoch session and cleanup, commit `4b2f9a81a82911d68b8b64864ae05a03f9690b2e`. | U0; parallel U1 ownership is complete. |
| U3 | `CLOSED / INTEGRATED` explicit user project selection and restricted Worker-only read containment, source commit `0123616e21d656b2374809a13aeb2769f0324e7e`. | U1 + U2 closed integration outputs. |
| U4 | `CLOSED / INTEGRATED` Desktop ordinary-user launch, explicit selection, read presentation and fail-closed recovery with zero direct project I/O. | U3 closed integration output. |
| U5 | `CLOSED / SCOPED GO — LOCAL_ORDINARY_USER_E2E`; standalone ordinary-user Worker and real local E2E are accepted at source `365e7612b1be276aa74f4ab36f40482a0858e1ae`, integration `b9de2eb47e4e9d9ea29e0490b9dfc745a4dc307d`. | Accepted U4 integration. |
| U6 | `ACTIVE — WP-USERMODE-FINAL-AUDIT`; read-only final audit with source changes prohibited and `P0/P1/P2=0/0/0` required for final scoped GO. | Accepted U5 frozen integration. |

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

The other-user denial claim remains deliberately bounded to `CurrentUserOnly` static/IL plus existing unit evidence: no separate Windows account and no literal multi-user E2E result is claimed. It is not a claim against malicious same-user code, administrator/kernel control, offline tampering, or a deliberately malicious selected project. U5 introduces no Service/SCM, `LocalSystem`, privilege, SACL, enrollment, loaded-image, command, mutation, evidence, verdict, or authority capability. Production read remains NO-GO until U6 closes; hashes and signatures remain release-integrity evidence only.

### U6 active final frozen-byte audit contract

`WP-USERMODE-FINAL-AUDIT` is the sole active current package. It is read-only: no source, test, solution, project, package-lock, Service/SCM, runtime, or authority change is permitted. This publication may update only this control document, the work-package registry, and the evidence index; U5 is `CLOSED / SCOPED GO` and stopped as a source package.

U6 must audit the frozen U0-U5 integration rather than reinterpret it. It verifies: (1) the bounded other-user denial claim and the explicit exclusion of same-user-adversary protection; (2) session, one-use nonce, generation, parent/child handle, PID and process-epoch correlation; (3) explicit selected-project scope, locator containment, revocation, and Worker-only bounded reads; (4) strict protocol/version/schema/message/correlation rejection and C2 acknowledgement ordering; (5) crash, cancel, disconnect, restart, partial-frame, child cleanup, diagnostic redaction, and process/pipe/temp-root leak behavior; and (6) the real staged Broker/Worker runtime bundle rather than a fake peer or an installed-package claim.

U6 must replay or independently review the unified `eng/run-phase2-gate.ps1` evidence and the real LocalE2E execution, including solution, schema, smoke, binding, frozen-root, and residue receipts. A final scoped GO is possible only with `P0/P1/P2=0/0/0`; any byte drift, failed gate, unbounded claim, missing evidence, Service/SCM or privileged-path reintroduction, or same-user-threat overclaim blocks it. If fresh gate assets are needed, they may come only from the approved local feed with unique ignored temporary locks; tracked locks must not change and no `bin`/`obj` may be copied.

The U6 publication-time gate receipt `u6-final-audit-complete-20260828T150638621Z` is `FAIL`, so it is not final-GO evidence. The new worktree lacked generated `project.assets.json` for the Worker and LocalE2E projects (`NETSDK1004`), preventing Worker, LocalE2E, and solution build completion and therefore a real LocalE2E result; the initial Protocol build also observed a transient generated-`obj` file lock. The partial run recorded Protocol `108/108`, Client `16/16`, Broker `183/183`, schema PASS `22/13/14/236`, and smoke `W24FS001`/`23`, but those partial outcomes do not waive the complete-gate requirement. Its pre/post source manifest is identical at `16607` rows and SHA-256 `2ded369dc466c083499209aa7d21215d79444d7dc60c5035fe8ab809de60a0f9`; frozen-root mismatch count and residue counts are `0`. U6 remains `ACTIVE` with no final GO. A later final-audit agent must bootstrap the approved feed with unique ignored temporary locks before a complete rerun; tracked locks and product source remain frozen.

AI A0 remains `NOT STARTED`. Its provider two-channel plan is queued after U6 only and does not add USER_MODE main-architecture completion credit.

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
