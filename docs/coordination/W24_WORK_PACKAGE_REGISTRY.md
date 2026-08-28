# W24 work-package registry

> **CURRENT REGISTRY — U5 LOCAL ORDINARY-USER E2E PUBLICATION (2026-08-28).** Only U0-U6 below are current delivery nodes. Every pre-U0 package entry retained later in this file is historical provenance, not an active contract, dependency, blocker, or audit queue.
>
> Normative U0 architecture token: `USER_MODE_LOCAL_CREATIVE_TOOL_V1`.

## Current seven-node registry and integration status

Exact DAG: `U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`.

| Node | Contract | Acceptance boundary |
|---|---|---|
| U0 `USER_MODE_ARCHITECTURE_SIMPLIFICATION` | `CLOSED`; docs-only architecture rebase in commit `53c1eeb4577a7067d8702fdea9866adf01733191`. | Architecture-only, no runtime GO. |
| U1 `C3_W1_ACTUAL_UNITY_WORKER_CONNECTOR` | `CLOSED / INTEGRATED`; commit `48cd27103f8fe0c510770b1584b326f55fca3485`; declared U1 gates complete. | Strict schema/vector/codec parity and connector lifecycle negatives only; no second wire contract, Desktop integration, arbitrary path, project read, or authority. |
| U2 `USER_MODE_CHILD_PIPE_SESSION` | `CLOSED / INTEGRATED`; commit `4b2f9a81a82911d68b8b64864ae05a03f9690b2e`; the initial audit recorded `P1=3`, and one remediation closed with `42/42` three times plus Broker `171/171`. | The remediation result is not represented as a second independent-audit verdict. No Service/SCM/privilege/SACL/loaded-image/enrollment claim. |
| U3 `USER_PROJECT_SELECTION_READ_CONTAINMENT` | `CLOSED / INTEGRATED`; source commit `0123616e21d656b2374809a13aeb2769f0324e7e`, merged at `027ba07448dd6d4a0741a67937427cd2d37b2649`; exact seven files. | Broker `8/8`, Unity `9/9`, no-tests PASS, unified Broker `179/179`, manifest SHA-256 prefix `b716…`; zero Desktop project I/O and no privileged route. |
| U4 `DESKTOP_USER_MODE_INTEGRATION` | `CLOSED / INTEGRATED`; source `2295b022348dc1514c72846533b86430bc4762ad`, integration `e1a6a9a37d3125717afbe795d283a07ffa242060`. | Accepted targets Protocol `108/108`, Client `14/14`, Broker `183/183`, Desktop `12/12`; r2 receipt manifest `b741fef9ab35a683363993cfeeb74abd2b1cbc26f5e3988574febfe1349a66eb`. |
| U5 `WP-USERMODE-LOCAL-E2E` | `ACTIVE`; sole current source package, with exactly 17 owned files below. | Protocol-only standalone Worker plus public-Desktop-backend E2E, adversarial, crash, cleanup, and default-smoke preservation. |
| U6 `USER_MODE_FINAL_AUDIT` | `NOT STARTED`. | No source edits; all declared gates replay; P0/P1/P2=0 for scoped GO after U5. |
| Post-U6 AI A0 | `NOT STARTED`. | Two-channel AI-provider plan is outside U5 source, runtime, test, and evidence scope. |

U1 through U4 are closed/integrated. `WP-USERMODE-LOCAL-E2E` is the sole active current package. U6 and post-U6 AI A0 are not started; there are no other current implementation nodes.

### U3 exact closed ownership and evidence

1. `services/VFXComposer.Broker/Registration/UserModeProjectSelectionStore.cs`
2. `services/VFXComposer.Broker/Ipc/UserModeProjectReadSession.cs`
3. `services/VFXComposer.Broker.Tests/UserModeProjectSelectionReadTests.cs`
4. `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/W24S6UserModeProjectReadSession.cs`
5. `project/Packages/com.vfxcomposer.unity/Editor/W24/S6/Worker/W24S6UserModeProjectReadSession.cs.meta`
6. `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6UserModeProjectReadSessionTests.cs`
7. `project/Packages/com.vfxcomposer.unity/Tests/EditMode/W24S6UserModeProjectReadSessionTests.cs.meta`

U3 is closed at source commit `0123616e21d656b2374809a13aeb2769f0324e7e` and integrated at `027ba07448dd6d4a0741a67937427cd2d37b2649`. The closeout records Broker `8/8`, Unity `9/9`, no-tests PASS, unified Broker `179/179`, and manifest SHA-256 prefix `b716…`. It remains limited to an ordinary explicit current-user local project path, a restricted session-bound locator, and a bounded Worker-only read.

### U4 integrated closeout and retained exact ownership

U4 is closed at source commit `2295b022348dc1514c72846533b86430bc4762ad` and integrated by `e1a6a9a37d3125717afbe795d283a07ffa242060`. Accepted targets are Protocol `108/108`, Client `14/14`, Broker `183/183`, and Desktop `12/12`. The r2 unified receipt records the Release solution build at `0 warnings / 0 errors`, schema `22 / 13 / 14 / 236`, default smoke `W24FS001`/exit `23`, and manifest `b741fef9ab35a683363993cfeeb74abd2b1cbc26f5e3988574febfe1349a66eb`.

The stopped first U4 writer is rejected historical provenance only: it referenced a nonexistent `VFXComposer.UnityWorker.exe`, launched before selection, accepted before strict C2 ACK, and crossed ownership into `apps/VFXComposer.Desktop.Tests/NoProjectAccessSurfaceTests.cs`. Its isolated bytes are not accepted evidence.

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

U4 owns no csproj, solution, package, or lock file. Its retained component boundary is: no Worker before explicit selection; fixed `Path.Combine(AppContext.BaseDirectory, "VFXComposer.UnityWorker.exe")` path; selected canonical root as `WorkingDirectory`; U2 admission then locator send then strict C2 ACK before `SelectAccepted`; and old Worker/session disposal on reselect/restart. Desktop performs no project `File`/`Directory` access and no direct Worker connection. Its scripted peer was test-only and cannot prove real Worker/E2E. The no-argument Broker behavior remains `W24FS001` on stderr and exit `23`, with no listener. No Service/SCM, privilege, SACL, loaded-image, command, mutation, evidence, verdict, or authority is in scope.

### U5 active exact ownership and acceptance contract

`WP-USERMODE-LOCAL-E2E` is active and owns exactly:

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

This same-milestone scope correction adds exactly items 16–17; it does not authorize an 18th U5 file. The other 12 U5-new bytes (items 1–12) remain `UNCOMMITTED / UNACCEPTED`. `VFXComposer.sln`, `eng/run-phase2-gate.ps1`, and `eng/phase2-baseline-roots.json` (items 13–15) remain unmodified. This documentation-only correction accepts no source, test, runtime, or E2E evidence and does not change U5's sole-`ACTIVE` status.

The first genuine U5 LocalE2E attempt is `12/17` passed, not an acceptance receipt. Its five open failures are recorded without waiver:

1. **Client product gap:** if the Broker is already dead before `ReadAsync` or `SelectAsync` enters `ExchangeAsync`, `SessionIdFor` throws outside the existing recovery `try/catch`; the session remains `Reading` or `Selecting`, `EnterRecoveryAsync` is not called, and `RestartAsync` is rejected.
2. **U5-local:** malformed C2 causes an uncaught `WireDecodeException` in the Worker instead of clean exit `31`.
3. **U5-local:** the reparse test setup cannot create a symbolic link in this environment and needs a safe junction fallback.
4. **U5-local:** the wrong-user static scan finds its own `CreateUser` assertion literal.
5. **U5-local:** temporary-project teardown races a lingering file handle after cancellation and needs bounded residue/deletion retry.

The Client correction is acceptance-critical and must not weaken crash recovery: in both `SelectAsync` and `ReadAsync`, `SessionIdFor`, request construction, and `ExchangeAsync` must all be inside the existing recovery `try/catch`. A dead host must transition through `RecoveryRequired` and disposal, then allow `RestartAsync` to reach `ConnectedNoProject`. The existing `UserModeDesktopSessionTests.cs` must genuinely cover pre-exchange inactive-host failure for both read and selection, rather than only an exchange-time failure.

U5 alone may create a standalone Protocol-only `net8.0-windows` `VFXComposer.UnityWorker.exe`, which references Protocol only and has zero Unity source link, Newtonsoft, or `UNITY_INCLUDE_TESTS`. It is the canonical runtime C2 consumer; the Unity package is parity/reference only. A minimum local copy of U2 private `UMB1`/`UMH1` bootstrap ABI is allowed only for byte-level compatibility and real Broker coverage, preserving `CurrentUserOnly`, nonce, session, generation, PID, and epoch. It creates no second C2 format.

The true E2E uses public `UserModeDesktopSession` over the real Desktop/Client -> Broker -> Worker backend, not an installed Avalonia release package. LocalE2E stages the complete Broker and Worker runtime bundle into test `AppContext.BaseDirectory` to satisfy U4's fixed adjacent Worker path. Standalone Worker normal output is permitted; publish-artifact packaging is not a U5 claim. The selected canonical project becomes the child working directory; strict C2 locator ACK precedes actual U3 bounded `LIBRARY_INDEX`/manifest read.

Required real coverage is happy path; bad nonce/session/generation/locator/path/protocol; marker/traversal/reparse/size/JSON rejection; crash/restart/cancel/partial-frame recovery; and zero orphan process, pipe, and temporary-project residue. HandleProbe, startup hooks, scripted/fake peer, Service/SCM, privilege, SACL, and E2E substitutes are forbidden. Wrong-user is `CurrentUserOnly` static/IL plus existing unit evidence only, never a created account or claimed literal wrong-user E2E. Default Broker remains `W24FS001`/exit `23`.

U5 extends only the existing unified `eng/run-phase2-gate.ps1` and runs it once as final gate; no independent E2E runner is a U5 artifact. Fresh assets, if needed, use an approved local feed and unique ignored temporary locks; no pre-existing tracked-lock drift and no copied `bin`/`obj`. U6 and AI A0 remain not started; the AI two-channel plan is not U5 source.

No stale-baseline unified gate is run or accepted for this documentation-only correction: it is a U5 same-milestone scope correction with no source acceptance. That exception does not relax the final-gate requirement above.

## Current trust and reuse rules

Trust is limited to the current logged-in user, actively launched local product processes, and explicit project choice. The route defends cross-user access, freshness, exact process/session/project correlation, protocol strictness, release-layout path expectation, redaction and crash cleanup; it does not defend malicious same-user code, admin/kernel control, offline tampering, or a deliberately malicious chosen project. Hashes/signatures are release integrity only.

C1/C2 are accepted reuse inputs. P1 named-pipe and S1 lifecycle/ownership fragments are candidates only to the extent they work for an ordinary-user topology and are revalidated within a U-node. The stopped M1 candidate earns no acceptance credit and is U1 review input only; M2 `fa8843be` remains historical and is not merged.

D1/D1R, ServiceHost/install, I1, R1, A1, B1, SCM/privileged E2E and their audits are retired product-delivery history. They must receive no further implementation or audit and cannot block U1-U6. No Windows Service, SCM, `LocalSystem`, privileged enrollment, `SeSecurityPrivilege`, `SeRestorePrivilege`, strict-SACL live gate, loaded-image proof or privileged issuer may be silently reintroduced.

## Frozen planning baseline

U0 closeout freezes `45/100`, reported as **42%-48% complete**, under weights `U0=8`, `U1=22`, `U2=22`, `U3=18`, `U4=12`, `U5=12`, `U6=6` plus accepted C1/C2 and reviewable ordinary-user P1/S1 reuse credit. The stopped M1/M2 work contributes zero. The new remaining route is estimated **45%-55% shorter** than the superseded privileged route, with a 50% planning point. The algorithm cannot be replaced by line/test/receipt counting.

---

## Historical pre-U0 package registry (superseded for product delivery)

Status date: `2026-08-28`

## 1. Registry

| Package | State | Writer ownership | Dependencies | Terminal condition |
|---|---|---|---|---|
| `WP-CURRENT-DESKTOP-BROKER-READ` | `CLOSED / SCOPED GO` | Historical shared Client/Broker tests and reports | Phase 2 foundation | r5 136/136 and independent `P0=0/P1=0/P2=0`; no production grant. |
| `WP-CONTROL-FREEZE` | `CLOSED / SCOPED GO` | `docs/coordination/**` only | Closed current node | Independent audit `P0=0 / P1=0 / P2=0`; no source mutation. |
| `WP-PROTOCOL-P3` | `STOPPED — DUPLICATE_WRITER_COLLISION` | Protocol, Protocol tests, command/job schemas, one handoff | Two same-checkout writers (`01a03ede-81c6-7793-8384-50bb66ebb73e`, `01a03ede-dcad-70f0-b505-1d1bf7200bdc`) overlapped after valid baseline replay | No scoped GO. Preserve partial bytes; publish no writer until a recovery package has a fresh baseline, sole-writer proof and fresh quota confirmation. |
| `WP-PROTOCOL-P3-RECOVERY` | `CLOSED / SCOPED GO — PURE_PROTOCOL_COMMAND_JOB_CONTRACT_FOUNDATION_ONLY` | Sole Terra Max writer: Protocol, Protocol tests, command/job schemas, recovery handoff | Fresh mixed-byte baseline; collision writers stopped; quota 24% at package start | 88/88, 14 schemas, independent `P0=0 / P1=0 / P2=0`; no runtime, transport, I/O or authority. |
| `WP-AUDIT-PROTOCOL-P3` | `CLOSED / SCOPED GO` | Read-only | Recovery writer STOPPED | Independent final verdict `P0=0 / P1=0 / P2=0` for pure contract foundation only. |
| `WP-BROKER-P2-PROD` | `CLOSED / SCOPED GO — BROKER_PRODUCTION_TRUST_ACL_DORMANT_FOUNDATION_ONLY` | Sole Terra Max writer: Broker and Broker tests only | Production trust ADR plus frozen Protocol requirements; quota 22% at package start | 40/40 and independent `P0=0 / P1=0 / P2=0`; production issuer/listener remain unavailable. |
| `WP-BROKER-P2-ISSUER-HOST` | `CLOSED / SCOPED GO — BROKER_ISSUER_HOST_BOOTSTRAP_ACL_DORMANT_FOUNDATION_ONLY` | Sole Terra Max writer: two Broker files, one Broker.Tests file and one handoff | `WP-BROKER-P2-PROD` frozen; quota 21% at package start | 48/48 and independent `P0=0 / P1=0 / P2=0`; production remains `W24FS001`/23 with no installed service, live issuer, listener, actual ACL application, project access or authority. |
| `WP-BROKER-P2-LIVE-ATTESTATION` | `CLOSED / SCOPED GO — DORMANT_WINDOWS_PROCESS_TOKEN_PATH_OBSERVATION_FOUNDATION_ONLY` | Sole Terra Max writer: two exact Broker files, one exact Broker.Tests file and one handoff | `WP-BROKER-P2-ISSUER-HOST` frozen; quota 21% at publication | 54/54 and independent `P0=0 / P1=0 / P2=0`; no installed service, successful live service attestation, executable-content identity, policy/listener/project access or authority. |
| `WP-BROKER-P2-SERVICE-HOST` | `CLOSED / SCOPED GO — DORMANT_SCM_SERVICE_RUNTIME_FOUNDATION_ONLY` | Fresh Terra Max writer: two new service-host project roots, exact solution membership and one handoff | `WP-BROKER-P2-LIVE-ATTESTATION` frozen; quota 19% at publication | 16/16 and final independent `P0=0 / P1=0 / P2=0`; no install/registration, production policy/listener, project access or authority. |
| `WP-BROKER-P2-SERVICE-HOST-PROVENANCE-RECOVERY` | `CLOSED / SCOPED GO — EXACT SOLUTION DELTA RECOVERED` | Fresh Terra Max evidence-only writer: solution provenance, handoff wording and isolated recovery receipts | Initial fresh auditor found a solution-baseline reconstruction error | Fresh delta auditor reproduced the exact 52f577… baseline and byte-identical b8804a… round trip; no solution/product bytes changed. |
| `WP-BROKER-P2-INSTALL-POLICY` | `CLOSED / SCOPED GO — DORMANT_SCM_INSTALLATION_POLICY_FOUNDATION_ONLY` | Retired fresh Terra Max writer: two exact Broker policy files, one exact Broker.Tests file and one handoff | Service-host runtime frozen; final independent audit `P0=0/P1=0/P2=0`; quota 18% at publication, now stale | Source 3/3 `45eb51eac83ef1f8144be1a9f6e443048415d15d92fcf686e4547345b761530b`; evidence 22/22 `505db45b99bcffa9dbd9f77c2e91b92b92e7b91dd8a8bd93a53122c8daceaf39`; Broker 34 `a82899f692fd3dcb568eb5bf820f90ecf92f4054b515f8d3621a2e34c361772a`; Broker.Tests 15 `c6864861a756cd0458e40c886855ee775ef15cf3d04a5129550cf7e324d74835`; TRX 61/61; builds/solution 0/0; smoke `W24FS001`/23; no install/registration/start/configure, SCM mutation, binary-path binding, live-attestation success, executable-content identity, issuer/listener/ACL/project/Worker/Desktop/Unity/commands/authority or Phase 2 gate. |
| `WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY` | `CLOSED / SCOPED GO — DORMANT_EXECUTABLE_CONTENT_IDENTITY_POLICY_FOUNDATION_ONLY` | Retired fresh Terra Max writer: two exact Broker files, one exact Broker.Tests file and one handoff; final independent audit complete | Installation-policy package frozen/audited; quota `16%` at publication, now stale — refresh required before any later package | Source 3/3 `ae2033ae6402d7ac40c27844ca17c9fa432cbafd840637227142a0e006c8bddb`; evidence 24/24 `a9417349bfa4c8ff0b9ceb3cafd81aa918a5e267c2c716793e04b5b4e38fe880`; Broker 36 `2a8805352c20259f1c2b06b00102f83e7982f178b4de550072595cb126af98c1`; Broker.Tests 16 `1ff9e8b497598c6a0f0a620657e24bbad04542e5e048974308cb6754393fac75`; TRX 68/68 `7d356d4732f336d12e70d1f6e56aea55cf1928c668a499b65d0435298bd63153`; builds/solution 0/0; smoke `W24FS001`/23; handoff pre-status `7d9e824b82b9e8a901393a688cf7493bc398bb05f0584511a5b4d0508d575ebd`; independent final audit P0=0/P1=0/P2=0. Supplied in-memory identity policy only; no file/path/handle byte observation, loaded-image equality, Authenticode/signature/certificate validation, installed service/SCM mutation, production issuer/listener/ACL/project/Worker/Desktop/Unity/commands/authority or Phase 2 gate. |
| `WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION` | `CLOSED / SCOPED GO — DORMANT_PINNED_EXECUTABLE_HANDLE_HASH_OBSERVATION_ONLY` | Retired fresh Terra Max writer; final independent audit `P0=0/P1=0/P2=0` | Source 3/3 `571974b214be8f9146103a1bc06b7e98fe1a47eab10dae97166290d6731ac979`; evidence 24/24 `cb88bd5b42cf2807a314c820a93c2e133833fe2028129c4e353c93a866545886`; handoff pre-status `2981449f9c0808e24c3a268b6c2faa82267ae226eb3f6ca6aa9a65a789314ac2`; Broker 38 `8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760`; Broker.Tests 17 `66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60`; TRX 78/78 `c5a5cb0336e1b83c0cb003e1af03388fc886f7679b7f4ff4b96b6ade985a19d8`; builds/solution 0/0; smoke `W24FS001`/23; zero residue; quota 14% now stale | Caller-supplied handle observation only; no loaded-image/path proof, signature/certificate validation, service install/SCM action, production wiring, project access, authority or Phase 2 gate. Runtime reparse-point coverage is an explicitly unclaimed residual; next package is prohibited until quota refresh (`<=7%`/`<=5%` rules remain). |
| `WP-P2-PRODUCTION-READ-GATE-FREEZE` | `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_ARCHITECTURE_FREEZE_ONLY` | Docs-only architecture freeze; its writer and auditor are retired | Final independent audit `P0=0 / P1=0 / P2=0`; 12 nodes / 6 edge groups / 15 directed edges / 68 exact owned files / 1 receipt-root exception / 0 duplicates | No source/runtime/production/authority capability. Production remains `W24FS001`/23, Phase 2 remains NO-GO, and every one of the 12 implementation nodes remains blocked pending a separately published package. |
| D1 `WP-P2-DURABLE-PRODUCTION-PROFILE` | `STOPPED / NO-GO — REMEDIATION AND DAG REBASE REQUIRED` | Writer and independent auditor retired; three-source fail-closed checkpoint retained | Independent audit: P0 blockers=2 plus P1 source/governance defects; full acceptance not run/passed | No scoped GO. Strict SACL path and rename are unreachable; D1/I1 live acceptance is cyclic. Production remains `W24FS001`/23 and Phase 2 remains NO-GO. |
| C1 `WP-P2-PROTOCOL-PROJECT-SELECTION` | `CLOSED / SCOPED GO — PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY` | Fresh Terra Max writer and independent auditor retired after final `STOPPED`; exact 12-file Protocol/test/schema/verifier allow-list plus handoff | G0 closed; D1 is not a dependency; final independent audit `P0=0 / P1=0 / P2=0` | Source 12/12 and receipts 10/10 replayed; no runtime, path, project I/O, lease, command or authority. |
| `WP-P2-PRODUCTION-READ-DAG-REBASE-1` | `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY` | Docs-only writer and independent auditor retired after final `STOPPED` | At its historical closeout D1 was STOPPED/NO-GO, C1 was closed GO, and C2/D1R/W1 were not published | Final independent verdict `P0=0 / P1=0 / P2=1`. The documented, nonblocking P2 is that the failed verifier lacks literal command/times/exit/raw and receipt-root aggregates are unrepeatable; semantic gates were independently recomputed. No receipt PASS or `P2=0` is claimed; no source/runtime/production/authority capability is granted. |
| C2 `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR` | `CLOSED / SCOPED GO — PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY` | Fresh Terra Max writer and independent auditor retired after final `STOPPED`; exact 16-file Protocol/test/vector/schema/verifier package plus handoff | C1 remains closed GO; D1 remains STOPPED/NO-GO; DAG rebase remains historical closed GO/P2=1; final independent audit `P0=0 / P1=0 / P2=0` | Source 16/16 and receipts 39/39 replayed; no production/runtime/transport/locator issuer or ACK issuer/project I/O/handle/grant/command/authority capability; Phase 2 remains NO-GO. |
| D1R `WP-P2-DURABLE-PRODUCTION-PROFILE-REMEDIATION` | `CLOSED / SCOPED GO — DORMANT_DURABLE_PRODUCTION_PROFILE_STORE_REMEDIATION_ONLY` | Fresh Terra Max writer and independent auditor retired after final `STOPPED`; exactly three D1_0 overlays and the generated-root `HANDOFF.md` only | D1_0 retained `STOPPED / NO-GO` provenance; C1/C2 closed GO; DAG rebase remains historical GO/P2=1; I1 is the next DAG dependency but is not published or started | Final independent audit closed four findings with `P0=0 / P1=0 / P2=0`; source 3/3, receipts 65/65, roots 9/9, target 35/35, full Broker 113/113, schema 22/13/14/236, builds 0/0 and smoke `W24FS001`/23. Dormant/no-privilege only; no production, runtime wiring, project I/O or authority; Phase 2 remains NO-GO. |
| P1 `WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION` | `ACTIVE — DORMANT_PER_SERVING_PRODUCTION_NAMED_PIPE_ACL_PROVISIONING_AND_EXACT_READBACK_ONLY` | Fresh `gpt-5.6-terra`/`max` sole writer: exactly four new Broker/Broker.Tests leaves plus its one handoff and generated receipts | Sole DAG dependency D1R is closed GO; I1 is only an external privileged-root preflight blocker and is not published or started. C1/C2 remain closed GO; old D1 remains STOPPED/NO-GO; DAG-rebase GO/P2=1 is historical; C3/W1 are not published. | Publication only: no receipt or live-serving claim is accepted. Completion requires exact 16 new tests (8+8), target 16/16, full Broker 129/129, schema 22/13/14/236, builds 0/0, `W24FS001`/23 smoke, manifests, zero residue and a fresh independent audit; no listener/`Running`/authority or existing-entry wiring. |
| `WP-DESKTOP-READ` | `PLANNED / NOT PUBLISHED — PRODUCTION READ ROUTE REQUIRED` | Client/Desktop and their tests only | Frozen Protocol plus admitted production read route | Read-only connection/status scoped GO; zero direct project access. |
| Unity Worker command implementation | `BLOCKED` | Unity Worker only | `WP-PROTOCOL-P3-RECOVERY` frozen and audited; separate runtime package still required | Separate future package; no Unity UI changes and no authority promotion. |

The sole active package is P1 `WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION` with scope `DORMANT_PER_SERVING_PRODUCTION_NAMED_PIPE_ACL_PROVISIONING_AND_EXACT_READBACK_ONLY`; no other implementation package is active or next published. D1R is final `STOPPED` scoped GO; C1/C2 are closed GO; old D1 remains frozen `STOPPED / NO-GO`; the DAG-rebase GO/P2=1 remains historical. I1 is an external privileged-root preflight blocker and is not published or started; C3 and W1 are not active or published. Quota is 100% and is not a scheduling condition.

## 2. `WP-PROTOCOL-P3` contract

Unique objective:

> Freeze the pure-C# Phase 3 command/job wire contract and schema/golden-vector foundation. This package defines data and validation only; it implements no Client route, Broker admission, Unity Worker handler, project I/O, UI, authority or mutation.

Allowed files:

- `src/VFXComposer.Protocol/**`
- `src/VFXComposer.Protocol.Tests/**`
- `docs/schemas/desktop/commands/**`
- `docs/schemas/desktop/jobs/**`
- `docs/coordination/handoffs/WP-PROTOCOL-P3.md`

These are the only author-controlled source/document files the writer may create or edit. Generated-output exceptions are limited to:

- `**/bin/**` and `**/obj/**` produced by the required build/test commands;
- `.codex_tmp/WP-PROTOCOL-P3/**` for durable logs, TRX, schema-verifier output, manifests and binary receipts.

Generated outputs are excluded from the changed-source manifest. Any other generated or author-controlled path is a STOP condition.

Forbidden files:

- `src/VFXComposer.Client/**`
- `apps/**`
- `services/**`
- `project/**`
- all legacy Unity UI files
- root build/package files (`VFXComposer.sln`, `Directory.*`, `NuGet.config`, `global.json`)
- architecture, phase, allwork and evidence reports outside the single handoff

Required command/job scope for this package:

- common command envelope and exact identity/idempotency fields;
- `ValidateRecipe`, `BuildCandidate`, `OpenPreviewJob`, `ClosePreviewJob`, `SetPreviewPlayback`, `ValidatePatch`, `ApplyPatch`, `RunFocusedTests`, `CancelJob` data contracts;
- `JobProgress`, `JobLogEvent`, `JobArtifact`, `JobCompletion` data contracts;
- explicit confirmation-policy references without embedding a user verdict;
- typed hashes, stable diagnostics, bounded string/list sizes and an exact capability/version registry;
- immutable DTOs and a single strict codec path;
- exact schemas plus independently reproduced golden vectors.

Required negative boundaries:

- no absolute/caller path, output path, Unity type, delegate, transport, process, filesystem, environment or authority issuer;
- missing, extra, wrong type, duplicate decoded key, unknown version/kind/capability, non-canonical hash, cross-command identity, stale request/job correlation, invalid enum values and structurally impossible correlation reject;
- this package defines state vocabulary and structural correlation only; it must not invent the Broker/Worker job-transition graph or Unity execution semantics;
- transport success or job completion cannot represent machine/visual/user/L3/L4 authority;
- raw Recipe/Patch JSON is never a formal build/apply ticket.

Acceptance commands:

```powershell
New-Item -ItemType Directory -Force -Path .codex_tmp/WP-PROTOCOL-P3/test-results | Out-Null
dotnet build src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --locked-mode --no-restore
dotnet test src/VFXComposer.Protocol.Tests/VFXComposer.Protocol.Tests.csproj --configuration Release --locked-mode --no-build --no-restore --logger "trx;LogFileName=protocol-p3.trx" --results-directory .codex_tmp/WP-PROTOCOL-P3/test-results
dotnet build VFXComposer.sln --configuration Release --locked-mode --no-restore
```

Additional gate:

- Draft 2020-12 meta-validation and positive/negative validation for every new schema;
- source scan proving zero Unity/Avalonia/System.IO/System.Net/Pipe/Socket/Process/Environment/project-path surface in product Protocol;
- existing Phase 1/2 Protocol schemas and tests do not regress;
- deterministic file manifest and binary/test receipt hashes are written to the handoff.
- stdout/stderr and exit codes for both builds, the test run and schema verifier are retained below `.codex_tmp/WP-PROTOCOL-P3/`; the handoff pins their paths and SHA-256 values.

STOP conditions:

- any required change outside the allow-list;
- any need to choose a Unity execution semantic rather than representing an already frozen protocol fact;
- any attempt to add production transport, issuer, project access or authority;
- baseline drift in an allowed pre-existing file not caused by this writer;
- remaining quota reported at or below 5%.

## 3. `WP-BROKER-P2-ISSUER-HOST` contract

Unique objective:

> Add a bounded, dormant Broker-side model for independently hosted bootstrap material and canonical Windows named-pipe ACL provisioning intent. The package must not install or start a service, load production policy, create a listener, apply an ACL, access a project, activate handle admission, connect Desktop/Worker, or grant authority.

Allowed author-controlled files:

- `services/VFXComposer.Broker/Configuration/HostBootstrapMaterial.cs`
- `services/VFXComposer.Broker/Security/WindowsNamedPipeAclProvisioningIntent.cs`
- `services/VFXComposer.Broker.Tests/HostBootstrapMaterialTests.cs`
- `docs/coordination/handoffs/WP-BROKER-P2-ISSUER-HOST.md`

Generated-output exceptions are limited to:

- `**/bin/**` and `**/obj/**` produced by the required build/test commands;
- `.codex_tmp/WP-BROKER-P2-ISSUER-HOST/**` for baselines, logs, exit receipts, TRX, scans and manifests.

All Protocol/schema, Client/Desktop, Unity package, legacy Unity UI, root build/package, other Broker source, architecture/phase/allwork and coordination files are frozen to the writer. The controller alone may update coordination status after the writer stops. Any production service/SCM/install/listener/policy-loading/project-I/O/authority requirement is a STOP condition.

Acceptance requires exact baseline replay, Broker.Tests Release locked/no-restore build and tests, full Release solution build, controlled `W24FS001`/23 Broker smoke, forbidden-surface scans, deterministic manifests, a STOPPED handoff, and a separate read-only frozen-byte audit. Those requirements closed with independent `P0=0 / P1=0 / P2=0` for the dormant foundation only.

## 4. `WP-BROKER-P2-LIVE-ATTESTATION` contract

Unique objective:

> Add a bounded, dormant Windows process/token attestation primitive that can observe and freeze an exact process object, canonical PID/epoch, a handle-derived native executable-path observation, and required service-SID token membership for future host-bootstrap validation. The path observation is not executable-content identity. The package must not load production policy, install/start a service, register with SCM, create a pipe/listener, apply an ACL, admit a project, or grant authority.

Allowed author-controlled files:

- `services/VFXComposer.Broker/Security/WindowsServiceProcessAttestation.cs`
- `services/VFXComposer.Broker/Configuration/HostBootstrapAttestationAdmission.cs`
- `services/VFXComposer.Broker.Tests/WindowsServiceProcessAttestationTests.cs`
- `docs/coordination/handoffs/WP-BROKER-P2-LIVE-ATTESTATION.md`

Generated-output exceptions are limited to:

- `**/bin/**` and `**/obj/**` produced by required build/test commands;
- `.codex_tmp/WP-BROKER-P2-LIVE-ATTESTATION/**` for baselines, logs, exit receipts, TRX, scans and manifests.

Every other author-controlled file is frozen, including Program.cs, BrokerPolicy.cs, all existing trust/bootstrap files, Protocol/schema, Client/Desktop, Unity, solution/root build files and coordination documents. The controller alone may update coordination status after the writer stops.

Required boundaries and tests:

- hold a non-inheritable pinned process handle and derive PID/creation-time epoch from that exact object;
- inspect token user/groups and require exact configured service-SID membership without treating the caller, environment, path text or same-process token as a trust root;
- capture and replay a handle-derived native executable-path observation without claiming executable-byte/content identity; exact executable-content identity remains a production blocker;
- reject exited/reused PID, wrong epoch, wrong/missing SID, wrong native path observation, stale/cross-generation/cross-session material and replay before returning any dormant observation result;
- dispose/revoke must be linearizable and close all pinned native resources; tests must cover races and idempotence;
- Program/BrokerPolicy remain byte-frozen and `W24FS001`/23 stays before listener/path/project/environment I/O.

Acceptance requires exact baseline replay, Broker.Tests Release locked/no-restore build/tests, full Release solution build, controlled `W24FS001`/23 smoke, forbidden-surface and native-handle lifecycle scans/tests, deterministic manifests, a STOPPED handoff and a separate read-only audit. Those requirements closed with independent `P0=0 / P1=0 / P2=0` for the dormant path-observation foundation only. Any need for service installation/SCM, production policy activation, a listener, actual ACL application, arbitrary caller paths, project I/O, Desktop/Worker/Unity integration or authority is a STOP condition.

## 5. `WP-BROKER-P2-SERVICE-HOST` contract

Unique objective:

> Add a standalone .NET 8 Windows service-host executable and test project with a bounded SCM control/status state machine. Direct launch and SCM launch must both fail closed while production policy/issuer is unavailable. The package does not install, create, configure, start or delete an SCM service and does not create a listener or access a project.

Allowed author-controlled files:

- `services/VFXComposer.Broker.ServiceHost/**`
- `services/VFXComposer.Broker.ServiceHost.Tests/**`
- `VFXComposer.sln` only for exact membership of those two projects;
- `docs/coordination/handoffs/WP-BROKER-P2-SERVICE-HOST.md`

Generated-output exceptions are limited to:

- `**/bin/**` and `**/obj/**` produced by required restore/build/test commands;
- `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/**` for baselines, local-only restore/build/test/smoke logs, exit receipts, TRX, scans and manifests.

All existing Broker/Protocol/Client/Desktop/Unity source, schemas, root package/config files and coordination documents are frozen to the writer. The new projects may use only framework APIs and already approved centrally pinned packages available from the existing local-only NuGet source; adding or updating a package/version/root build file is a STOP condition.

Required boundaries and tests:

- pure managed lifecycle core with explicit `StartPending`, `Running`, `StopPending`, `Stopped` vocabulary and legal-transition rejection;
- thin Windows P/Invoke adapter for `StartServiceCtrlDispatcherW`, `RegisterServiceCtrlHandlerExW` and `SetServiceStatus`, with ABI/layout tests, stable non-path diagnostics and exception-safe callback lifetime;
- outside SCM, direct launch must return the frozen fail-closed diagnostic/code without listener/path/project/environment I/O;
- inside SCM, absent production policy/issuer must never report `Running`; it must report bounded pending/stopped states and close without listener or project access;
- STOP/shutdown controls are idempotent and linearizable; unsupported controls reject without state/authority promotion;
- no service installation/SCM database mutation APIs (`CreateService`, `OpenSCManager` for mutation, `DeleteService`, start/config change), no network/listener, no project I/O, no Desktop/Worker/Unity integration and no authority.

Acceptance requires local-only locked restore for the two new projects if needed, Release build/tests, full Release solution build, direct-launch fail-closed smoke, deterministic state/ABI/negative tests, forbidden-surface scan, exact manifests and receipts, a STOPPED handoff and a fresh independent read-only auditor. Any need to edit existing source/root package configuration, install/register/start a service, activate policy/listener/project access or grant authority is a STOP condition.

## 6. `WP-BROKER-P2-SERVICE-HOST-PROVENANCE-RECOVERY` contract

Unique objective:

> Resolve the independent audit's solution-baseline provenance finding without changing service-host product/test source. Recover and prove the exact pre-edit solution byte stream plus the declared two-project membership delta, or stop with the service-host package remaining NO-GO.

Allowed author-controlled files:

- `VFXComposer.sln`, only if an exact SHA-matching pre-edit reconstruction proves the required byte-preserving membership application;
- `docs/coordination/handoffs/WP-BROKER-P2-SERVICE-HOST.md`, only to correct verified provenance and the two audit wording findings;
- `.codex_tmp/WP-BROKER-P2-SERVICE-HOST/provenance-recovery/**` for read-only reconstructions, diffs, manifests and receipt hashes.

All service-host product/test files, package locks, other source/docs/configuration and prior final receipts are frozen. No build/test rerun is required unless `VFXComposer.sln` bytes change; if they change, full solution Release build and affected manifests/receipts must be regenerated. A claimed baseline may be corrected only when retained or deterministically reconstructed bytes independently hash to that identity. Failure to reproduce a defensible pre-edit solution is a terminal `NO-GO`, not permission to rebaseline.

The handoff must also replace “self-contained” with “standalone framework-dependent” and state that x86/x64 compilation passed while ABI/layout tests executed under x64. These requirements closed with a fresh read-only delta audit at `P0=0 / P1=0 / P2=0`; the original writer and auditor remain retired.

## 7. `WP-BROKER-P2-INSTALL-POLICY` contract

Unique objective:

> Define and validate an internal, non-wire, dormant least-privilege Windows service installation-policy candidate. The candidate fixes service/account/start/security semantics but deliberately excludes executable path/location, payload admission and every SCM API call.

Allowed author-controlled files:

- `services/VFXComposer.Broker/Configuration/WindowsServiceInstallationPolicy.cs`
- `services/VFXComposer.Broker/Security/WindowsServiceInstallationPolicyValidator.cs`
- `services/VFXComposer.Broker.Tests/WindowsServiceInstallationPolicyTests.cs`
- `docs/coordination/handoffs/WP-BROKER-P2-INSTALL-POLICY.md`

Generated-output exceptions are limited to `**/bin/**`, `**/obj/**`, and `.codex_tmp/WP-BROKER-P2-INSTALL-POLICY/**`. Every other source, project, lock, solution, schema, Unity and coordination file is frozen to the writer.

Candidate policy, unless existing ADR evidence contradicts it:

- fixed service name `VFXComposerBrokerHost` and fixed display name;
- own-process service, `NT AUTHORITY\\LocalService`, no password, no interactive/shared-process mode;
- `SERVICE_DEMAND_START`, no delayed auto-start, no service dependencies and no command-line arguments;
- `SERVICE_ERROR_NORMAL`, restricted service SID, no broad ACL identity and no recovery action that silently restarts an unresolved generation;
- exact Broker generation/profile/service-SID/image typed identities are required, but executable path and loaded-image/content attestation remain future host-owned inputs.

Required tests reject any alternate account, auto/boot/system start, delayed start, password, dependency, argument, interactive/share-process, unrestricted/none service SID, broad principal, restart action, unknown flag, stale generation/profile or cross-service identity. DTO/surface must be internal and immutable with no path, secret, raw handle, transport, project, verdict or authority field.

Acceptance: exact baseline replay; Broker.Tests Release locked/no-restore build/tests; full solution Release build; Broker `W24FS001`/23 smoke; forbidden-source/PE surface checks proving no SCM/registry/process/listener/network/path/project/Unity/authority API; deterministic manifests/receipts; STOPPED handoff; fresh independent audit. Any need to choose an undocumented production account semantic, add a path/SCM call, edit outside allowlist, or activate production is a STOP condition. The resulting scope cannot exceed `DORMANT_SCM_INSTALLATION_POLICY_FOUNDATION_ONLY`.

## 8. `WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY` contract

Unique objective:

> Define and validate an internal, immutable, non-wire executable-content identity policy and exact correlation model for the dormant Broker foundation. The policy binds a fixed executable-content typed-hash domain and exact byte length to the existing production trust-profile reference, Broker generation, service SID and process-image typed identity. It consumes only already-supplied in-memory identities and must not observe a path, file, process, signature or certificate.

Allowed author-controlled files:

- `services/VFXComposer.Broker/Configuration/WindowsServiceExecutableIdentityPolicy.cs`
- `services/VFXComposer.Broker/Security/WindowsServiceExecutableIdentityPolicyValidator.cs`
- `services/VFXComposer.Broker.Tests/WindowsServiceExecutableIdentityPolicyTests.cs`
- `docs/coordination/handoffs/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY.md`

Generated-output exceptions are limited to `**/bin/**`, `**/obj/**`, and `.codex_tmp/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY/**`. Every other source, project, lock, solution, schema, Unity and coordination file is frozen to the writer.

Required boundaries:

- define one fixed executable-content typed-hash domain; both construction and every correlation path must reject any alternate TypeTag even when candidate and expected use the same wrong domain and digest;
- require exact positive byte length plus exact content typed hash, and bind both to the same `ProductionTrustProfile` object, Broker generation, service SID and process-image typed identity already used by the dormant installation policy;
- keep all product types internal, sealed/immutable and non-wire, with no public constructor, serializer, delegate, callback, path, filename, command line, certificate, secret, raw handle, transport, project, verdict or authority field;
- reject zero/negative/overflow-shaped length inputs, unknown/alternate typed domains, stale/cross profile, generation, SID, process-image identity, executable-content identity or byte length;
- make no Authenticode, signer, certificate-chain, path-to-bytes, loaded-image equivalence or successful live-attestation claim. Those remain future independently owned gates.

Forbidden product surfaces include filesystem/path APIs, `SafeHandle`/`IntPtr`, process/token APIs, SCM/registry APIs, signature/certificate APIs, listener/network/pipe APIs, project/Unity/Desktop/Worker APIs, policy loading and authority. `Program.cs`, `BrokerPolicy.cs`, all prior Broker files, Protocol/schema, ServiceHost, solution/root build files and legacy Unity UI are frozen.

Acceptance: exact pre-edit baseline replay; Broker.Tests Release locked/no-restore build and full regression; full solution Release build; Broker direct smoke with stdout empty, stderr exact `W24FS001`, exit `23`; forbidden source and PE surface scans; deterministic source/evidence manifests and aggregates; STOPPED handoff; fresh independent read-only audit. Any need for a caller path, executable/file handle, real byte observation, signature decision, SCM call, production wiring or an undocumented trust semantic is a STOP condition. The resulting scope cannot exceed `DORMANT_EXECUTABLE_CONTENT_IDENTITY_POLICY_FOUNDATION_ONLY`.

## 9. `WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION` contract

Unique objective:

> Add a bounded, dormant Windows-only observation primitive that hashes bytes from a caller-supplied already-open local file handle without accepting, resolving or emitting any path. It may correlate that observation with the already-audited in-memory executable-content identity policy, but it must not claim that the handle is the running service's loaded image or that its bytes are signed or trusted.

Allowed author-controlled files:

- `services/VFXComposer.Broker/Native/WindowsPinnedExecutableContentObserver.cs`
- `services/VFXComposer.Broker/Configuration/HostBootstrapExecutableContentCorrelation.cs`
- `services/VFXComposer.Broker.Tests/WindowsPinnedExecutableContentObserverTests.cs`
- `docs/coordination/handoffs/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION.md`

Generated-output exceptions are limited to `**/bin/**`, `**/obj/**`, and `.codex_tmp/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION/**`. Every other source, project, lock, solution, schema, Unity and coordination file is frozen to the writer.

Required boundaries:

- input is a borrowed `SafeFileHandle`; no raw numeric handle, caller path, filename, environment or process lookup is accepted or emitted;
- acquire an independently owned, non-inheritable, read-only handle to the same file object without closing or moving the borrowed handle's file position; no `DUPLICATE_CLOSE_SOURCE` and no path reopen/fallback;
- require a regular non-reparse file, exactly one hard link, size `1..67108864` bytes, and read sharing only; reject directories, reparse points, zero/oversize files, hard links, invalid/closed handles and share conflicts before returning an observation;
- compute the exact `vfxcomposer.executable-content/1` `TypedHash` streaming encoding, with a golden parity test against `TypedHash.Compute`; perform two exact reads and require identical hashes plus before/between/after volume serial, file ID, attributes, link count, size and last-write identity replay;
- return only an internal immutable observation containing typed content hash, byte length and opaque native file identity. It exposes no handle or path and grants no authority;
- optional pure correlation must require the full existing executable identity policy/profile binding plus exact observed content hash and byte length. Correlation returns only a Boolean/non-authoritative result.

Tests must cover exact success and source-handle position/ownership preservation, strict typed-hash golden parity, invalid/closed/directory/zero/oversize/share-conflict rejection, content/length/policy mismatch, immutable/no-path/no-authority surface, and stable cleanup with no recursive path deletion. A real hard-link or reparse negative is required only if it can be created and removed through exact handle-bound/nonrecursive cleanup without privilege or safety broadening; otherwise source predicate and static surface evidence must be recorded and the missing runtime case classified honestly for audit.

Forbidden: path-based open/reopen or fallback in product code; `GetFinalPathNameByHandle`; process/token/SCM/registry/signature/certificate/listener/network/pipe/project/Unity/Desktop/Worker/authority APIs; production policy loading/wiring; any claim of loaded-image equivalence, Authenticode or installer trust. `Program.cs`, `BrokerPolicy.cs`, prior Broker files, Protocol/schema, ServiceHost, solution/root files and legacy Unity UI are frozen.

Acceptance: exact pre-edit replay; Broker.Tests Release locked/no-restore build/full regression; full solution Release build; Broker smoke stdout empty/stderr exact `W24FS001`/exit `23`; product-source and PE forbidden-surface/ABI checks; deterministic manifests/receipts and zero scratch residue; STOPPED handoff; fresh independent read-only audit. Any path need, unsafe cleanup, borrowed-handle ownership ambiguity, inability to prove stable double-read identity, production wiring or scope expansion is a STOP condition. Scope cannot exceed `DORMANT_PINNED_EXECUTABLE_HANDLE_HASH_OBSERVATION_ONLY`.

## 10. `WP-P2-PRODUCTION-READ-GATE-FREEZE` contract

Unique objective:

> Freeze the Windows production-read threat model, executable-image claim taxonomy, installed-service/bootstrap trust chain, dedicated Unity Worker topology, failure-close order and the exact package DAG required before any production listener or project read can activate.

Allowed author-controlled files:

- `docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md`
- `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md`
- `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md`
- `docs/coordination/handoffs/WP-P2-PRODUCTION-READ-GATE-FREEZE.md`

All source, schemas, other docs, solution/root files and legacy Unity UI are frozen. No generated build/test output is required; `.codex_tmp/WP-P2-PRODUCTION-READ-GATE-FREEZE/**` may contain only read-only hashes/checklists.

Required decisions:

- distinguish four claims: OS native path observation; protected installer-owned file object plus content identity; process executable backing-file object identity; current in-memory executable pages. Never infer a stronger claim from a weaker one;
- freeze current product profile as user-mode launch-correlated protected-file evidence with `LoadedImageVerified=false`. Strict backing-file identity requires a separately authorized signed driver/WDAC/CI design and is not silently required or claimed by the ordinary .NET path;
- define the production threat boundary: unprivileged/same-user tampering, namespace remap, reparse, replacement, PID reuse and cross-session attacks are in scope; administrator, kernel and raw-volume compromise require platform Code Integrity/driver controls and remain an explicit higher-assurance profile;
- require a protected install root, durable authenticated profile identity, fixed SCM configuration, pre-start pinned executable/ancestor handles, no-write/no-delete lifetime, exact file ID/hash/length, process PID/epoch/token/service SID/session, one-use nonce/generation receipt and actual pipe ACL readback before ServiceHost may report Running or create a listener;
- production Unity Worker is a Broker-created dedicated process, never the user's interactive Editor. It must be launched into a kill-on-close Job before resume, use a host-owned Volume-GUID project locator, and have exact process/global handle ownership and ACK supervision. Failure to join the Job or accept the locator is a STOP; no interactive fallback;
- Desktop never submits a path. Project enrollment is installer/host-owned and commits pinned local-NTFS volume/repository/project identities. Broker routes/duplicates handles; Unity Worker alone reads project content;
- freeze the shortest package DAG: Protocol project selection, durable profile, Worker production connector, installer/SCM, launch correlation, actual pipe ACL, Worker supervision, project enrollment, Broker production read convergence, Desktop read connector, installed E2E gate and final independent audit;
- before the convergence package, `Program` and `BrokerPolicy` must remain `W24FS001`/23 before listener/path/project I/O. Test peers, HandleProbe, test issuers and legacy receipts can never satisfy a production dependency.

The ADR must state that `WindowsNamedPipePeerFactsSource` currently hashes a path-reopened file as `process-image/1`; that value may remain a compatibility fact but cannot serve as the sole production loaded-image or launch-file admission proof. The production design must consume the independently attested launch receipt instead.

Acceptance: exact starting hashes, cross-document terminology/owner/dependency consistency, explicit file-level package plan, adversarial gate matrix, no source edits, STOPPED handoff and a fresh independent P0/P1 read-only audit. Any attempt to enable code, choose caller paths, reuse the interactive Unity Editor, call weaker evidence loaded-image verification, or leave the production activation order ambiguous is a STOP condition. Scope cannot exceed `PHASE2_PRODUCTION_READ_ARCHITECTURE_FREEZE_ONLY`.

## 11. D1 `WP-P2-DURABLE-PRODUCTION-PROFILE` contract

Unique objective:

> Implement an internal, dormant Windows-only durable authenticated production-profile and single-owner replay-store foundation: canonical profile bytes and typed digest, protected authenticated append-only persistence, monotonic generation, durable one-use nonce consumption, restart-scoped issuer epoch, and crash-safe write-once publish/reopen/readback. It must not load or activate production policy.

Allowed author-controlled product/test files, exactly:

- `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs`
- `services/VFXComposer.Broker/Security/WindowsDurableProfileStore.cs`
- `services/VFXComposer.Broker.Tests/WindowsDurableProfileStoreTests.cs`

Package metadata exceptions, not product-source expansion:

- `docs/coordination/handoffs/WP-P2-DURABLE-PRODUCTION-PROFILE.md`
- `.codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/**` for logs, TRX, manifests, scans and receipts.
- `**/bin/**` and `**/obj/**` produced by required build/test commands.

Every other source, project/lock/root file, schema, coordination/stage document, ServiceHost file, Protocol/Client/Desktop/Unity file, `Program.cs` and `BrokerPolicy.cs` is frozen. If a fourth product/test source file, package/csproj change, path-based trust root, production wiring or scope expansion is needed, STOP.

The store authority begins only from a caller-supplied already-pinned directory handle. Fixed single-segment, handle-relative, no-follow operations must reject non-local/non-NTFS, remote, reparse, hardlink, ADS, UNC/DOS namespace, inheritable handles, wrong object type, file-ID/volume/length/attribute drift, broad or mismatched owner/group/DACL/SACL/control/ACE order/masks, concurrent owner, invalid chain records and cross-store/profile/generation/issuer replay. No absolute path, drive letter, environment, AppContext, registry, KnownFolder, caller/project JSON or default discovery may become a trust root.

Canonical records must be bounded, versioned, ordinal and strict. A typed hash is not authentication: every record must be domain-separated HMAC-SHA-256 under a protected random store key and bind store ID, contiguous sequence, previous authenticator, record kind, exact profile digest and Broker generation. Secrets must never enter receipts/logs and must be zeroed on disposal. The first generation may be any positive value; later commits require exactly current generation plus one. A 32-byte nonce is one-use against the current profile/generation, persists across reopen, and old volatile issuer-instance receipts fail after restart. Receipts are immutable non-authority facts only.

Acceptance requires exact pre-edit replay using repo-relative forward-slash paths sorted with `StringComparer.Ordinal`, excluding `bin/obj`:

- Broker 38 / `8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760`
- Broker.Tests 17 / `66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60`
- Protocol 67 / `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`
- Protocol.Tests 21 / `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`
- desktop schemas 33 / `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`
- ServiceHost 8 / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`
- ServiceHost.Tests 6 / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`
- solution `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`

The three product/test targets must be absent before first edit. Required validation: Broker.Tests Release locked/no-restore build with zero warnings/errors; full Broker regression with durable TRX; full Release solution build; direct Broker smoke with empty stdout, exact `W24FS001` stderr and exit 23; static source/PE/native/ABI/ownership surface scan; adversarial persistence/replay/concurrency/crash-artifact tests; deterministic manifests and zero residue. Runtime network/global DOS-device mutation is forbidden; predicate/API-shape negatives must be described honestly. Fresh independent read-only audit is mandatory. Scope cannot exceed `DORMANT_DURABLE_PRODUCTION_PROFILE_STORE_FOUNDATION_ONLY`.

## 12. C1 `WP-P2-PROTOCOL-PROJECT-SELECTION` contract

Unique objective:

> Add one pure-C# strict no-path registered-project selection DTO, message kind, peer capability, wire codec branch, Draft 2020-12 schema and deterministic positive/negative/golden evidence. Decoding is correlation data only and grants no lease, I/O, trust or authority.

Allowed author-controlled files, exactly:

- `src/VFXComposer.Protocol/Projects/RegisteredProjectSelection.cs`
- `src/VFXComposer.Protocol/Json/StrictWireCodec.cs`
- `src/VFXComposer.Protocol/MessageKinds.cs`
- `src/VFXComposer.Protocol/WireSchemaRegistry.cs`
- `src/VFXComposer.Protocol/Ipc/PeerCapabilityIds.cs`
- `src/VFXComposer.Protocol.Tests/RegisteredProjectSelectionTests.cs`
- `src/VFXComposer.Protocol.Tests/StrictWireCodecTests.cs`
- `src/VFXComposer.Protocol.Tests/WireSchemaRegistryTests.cs`
- `src/VFXComposer.Protocol.Tests/DtoSchemaParityTests.cs`
- `src/VFXComposer.Protocol.Tests/Phase2WireContractTests.cs`
- `eng/verify-phase2-schemas.py`
- `docs/schemas/desktop/vfxcomposer-registered-project-selection-v1.schema.json`

Metadata/generated exceptions: `docs/coordination/handoffs/WP-P2-PROTOCOL-PROJECT-SELECTION.md`, `.codex_tmp/WP-P2-PROTOCOL-PROJECT-SELECTION/validation/**`, and required `**/bin/**`/`**/obj/**`. No csproj, lock, solution, Broker/Client/Desktop/Unity or other document may change. Need any thirteenth author-controlled file is a STOP.

The DTO is internal to protocol version `vfxcomposer.protocol/1.0`, kind `project.registered.selection`, capability `project.selection.v1`, with exactly seven required properties: `protocolVersion`, `messageKind`, bounded `requestId`, bounded opaque `registeredProjectId`, `projectIdentity` typed as `vfxcomposer.project-identity/1`, positive Int64 `brokerGeneration`, and positive Int64 `registrationGeneration`. No path, URI, label, volume/root/directory identity, handle, endpoint, grant, accepted/authorized flag, status, verdict, permission, command or authority field is allowed. No self-hash or decoded authority claim is introduced.

`StrictWireCodec` remains the sole ingress: exact root/nested required fields, unmapped-member and decoded-duplicate rejection, strict version/kind/domain/token/type/range checks, and stable non-echoing diagnostics. Schema properties and required sets must be identical with `additionalProperties:false`. Golden bytes and independent schema negatives must cover missing/duplicate/unknown/wrong type/version/kind, zero/negative/overflow generations, nested typed-hash drift, path-like values/fields and authority-like fields.

Starting aggregates: Protocol 67 / `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`; Protocol.Tests 21 / `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`; desktop schemas 33 / `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`. The new DTO/test/schema paths must be absent before editing. Frozen non-C1 inputs: Broker 40 / `f443abfce66e6fb4cea366bdc876079217d9f402cc3093ffe28ddcda3a1e8692`; Broker.Tests 18 / `16e5d80eff5e94cefdd1a681abfcc2df05d22faadb1726d8f8797be869630297`; ServiceHost 8 / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`; ServiceHost.Tests 6 / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`; solution `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`; Unity package 769 / `506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f`.

Acceptance: locked/no-restore Release Protocol.Tests build 0/0; full Protocol tests with durable TRX (baseline 88 plus the complete new behavior set); `eng/verify-phase2-schemas.py` exact internally consistent counts; full Release solution build 0/0; Broker smoke remains empty stdout/exact `W24FS001` stderr/23; exact 12-file changed manifest; forbidden path/authority/runtime/API scan; frozen-root/legacy-Unity replay; zero package residue; STOPPED handoff; fresh independent read-only audit. Counts must be reported from actual results, not assumed. Scope cannot exceed `PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY`.

Final closeout: `CLOSED / SCOPED GO — PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY`; independent final audit `P0=0 / P1=0 / P2=0`. External handoff pre-status SHA-256 is `8c35cae3759cd17f89d366184d74d843f8cc5904ca0a894aa4aae7da074759ef`; exact authored source manifest is 12/12 `27854e8504ec8e7da12e510db906efd9f2f780577d7a1b49e55c039d7da23600`; self-excluded receipt manifest is 10/10 `7402e367014a05b1d45d28df497de8f24563ce68d4cebed523745141583e1eef`. Final aggregates: Protocol 68 / `bac2177fab013096c2bb890596face218aba3e46387c4a1e4470ea655dc75605`; Protocol.Tests 22 / `672d85c5487281a3f0e272a08f2e265d839d098324eb1a38f6270280c40dbb03`; desktop schemas 34 / `d2384e52ff0901f915373b3d7976067ab3d1a10897dab8b85925c70f6d2233cc`. Validation closed at TRX 95/95, schema verifier 20/11/12/170, Protocol.Tests and solution builds 0 warnings / 0 errors, and Broker smoke `W24FS001`/23. C1 writer and auditor are retired; at C1 closeout, no next package, including W1, was published. D1 remains `STOPPED / NO-GO`; no production, runtime, project I/O, lease, command or authority is enabled.

## 13. `WP-P2-PRODUCTION-READ-DAG-REBASE-1` contract

Unique objective:

> Amend the frozen production-read architecture after two honest failures: separate D1's dormant-core gate from I1's privileged live-store integration, and insert a pure Protocol C2 host-owned Worker locator/locator-ACK node before W1. No implementation is authorized.

Allowed author-controlled files: `docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md`, `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md`, `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md`, and `docs/coordination/handoffs/WP-P2-PRODUCTION-READ-DAG-REBASE-1.md`. Generated receipts only under `.codex_tmp/WP-P2-PRODUCTION-READ-DAG-REBASE-1/**`. All source/schema/root/control/Unity files are frozen.

Required corrections:

- Retain the current D1 three-source checkpoint as NO-GO. Re-specify a fresh D1 remediation overlay that fixes internal store-file `ACCESS_SYSTEM_SECURITY`, pending rename `DELETE`, native secret zeroing, distinct least-privilege root/file ACLs, suffix rollback and manifest ordering. Its scoped core gate must not claim a successful privileged store open.
- Keep D1→I1 only for code dependency. Move privileged root provisioning, enabled `SeSecurityPrivilege`, `ACCESS_SYSTEM_SECURITY`, target-directory rename rights and the first successful strict commit/reopen/readback into I1's integration gate. B1 consumes I1's live-store receipt, not merely D1's dormant-core receipt.
- Add C2 `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR` before W1. C2 owns pure DTO/schema/codec/registry/golden vectors for a host-owned locator and locator acknowledgement; no path, runtime, transport, I/O, lease or authority.
- Replace impossible global zero-overlap for Protocol/D1 remediation with explicit sequential overlay rules: one active writer, exact overlay paths and pre-hashes, no concurrent overlap, fresh audit; new DTO/schema/vector leaf files remain unique. Do not weaken exact allow-lists.
- Rebase W1 to depend on C2 + ADR-003, retain its original nine Unity files and dormant no-pipe/no-read scope; grant ACK cannot substitute for locator ACK.
- Rebase B1 to consume C1, C2, W1, A1, P1, S1 and R1. Recompute node/edge/path/overlay accounting; historical G0 counts remain provenance.

Acceptance: exact starting hashes, explicit acyclic graph, exact likely files/overlays, no source edits, `W24FS001`/23 and all production/authority gates preserved, STOPPED handoff and fresh independent P0/P1 audit. Any source change, hidden protocol fork, live-success overclaim, ownership ambiguity or W1 publication is STOP. Scope cannot exceed `PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY`.

Final closeout: `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY`; final independent verdict `P0=0 / P1=0 / P2=1`. Current authored hashes are ADR-004 `e97c4a9e5c2bd20b191178732a8dc3804cac368741e410e1bb5dd58d1cd6c141`, Phase Plan `3d254384569658493cb7f0b9f5c414046861db08082d68ab6dd2dbc4c7255199`, and Phase2 Report `ee7a5f935e76faf3e14d87a842eba8ff68138f8711061e60cfdca6161bd3c360`; the external handoff pre-status SHA-256 is `373ea70a286aeb61f594126227616114752a2d9c96238a05aeb1f9f67dd0ee9f`. DAG/accounting remain `13/8/17` and `75/12/1`. The sole P2 is explicitly documented and nonblocking: the failed verifier lacks literal command/times/exit/raw and receipt-root aggregates are unrepeatable, but the independent auditor recomputed the semantic gates. This is not a receipt PASS and does not assert `P2=0`. At that historical package closeout, no package was active or next published, C2/D1R/W1 were unpublished, D1 remained NO-GO, C1 remained GO, and `W24FS001`/23, Phase 2 and authority remained NO-GO.

## 14. Handoff contract

Every writer creates exactly one package handoff. Unless a later package-specific contract explicitly replaces the location, it is `docs/coordination/handoffs/<WP>.md`; the later contract remains authoritative for both the handoff location and its allowed generated root.

1. package name, objective, model and reasoning setting;
2. start/end time and starting baseline identities;
3. exact changed-file manifest with SHA-256;
4. commands, exit codes, counts, warnings and durable receipt paths;
5. constraints proved and constraints explicitly not proved;
6. P0/P1/P2 self-audit findings;
7. remaining blockers and next dependency;
8. `STOPPED` declaration; no continuation into another package.

The controller does not accept source changes without the package-prescribed handoff and an independent read-only audit.

After the package reaches its final `STOPPED` verdict, both its writer and auditor are retired. A subsequent package must create new agents; reusing a completed-package agent is forbidden. Finding remediation may stay with the original agent only until that same package is finally closed.

## 15. C2 `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR` contract

Unique objective: implement the pure-C# no-path host-owned Worker locator and exact locator acknowledgement contract frozen in ADR-004 §9.2, with strict codec/schema/registry and immutable .NET golden vectors only.

Allowed author-controlled files are exactly the 16 C2 paths in ADR-004 row C2: seven new leaves (`WorkerProjectLocator.cs`, `WorkerProjectLocatorAcknowledgement.cs`, two new test files, one JSON golden vector, and two schemas) plus nine sequential overlays (`StrictWireCodec.cs`, `MessageKinds.cs`, `WireSchemaRegistry.cs`, `PeerCapabilityIds.cs`, four existing Protocol test files, and `eng/verify-phase2-schemas.py`). Exact repository-relative paths in ADR-004 are normative. Metadata/generated exceptions: `docs/coordination/handoffs/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR.md`, `.codex_tmp/WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR/validation/**`, and required `bin/obj`. Any 17th source/test/schema/vector/verifier path or csproj/lock/solution edit is STOP.

The kinds are `worker.project.locator` and `worker.project.locator.ack`, capability `worker.project-locator.v1`, and typed self-hash domains exactly as ADR-004. The locator binds opaque registered-project ID, typed project/volume/repository/project-root identities, positive broker/registration/enrollment generations, bounded worker session/process epoch and its self-hash. The ACK binds the exact locator hash plus all correlation fields, sole disposition `LOCATOR_ACCEPTED`, and its own self-hash. No path/URI/drive/Volume-GUID text/root text/raw handle/lease/grant/permission/status/Boolean acceptance/session issuance/command/authority field is allowed. It is distinct from handle-grant/revoke ACK and creates no runtime authority.

Follow the existing single strict ingress, canonical JSON, typed/self-hash, immutable DTO, schema parity and golden-vector patterns. Reject BOM, invalid UTF-8, decoded duplicates, unknown/missing/wrong-type fields, wrong version/kind/domain/hash, zero/negative/overflow generations, stale/cross identity/session/epoch correlations, noncanonical golden bytes, path-like and authority-like additions. The later W1 must replay the exact frozen vector; C2 must not add Unity/Broker/Client/Desktop runtime consumers.

Starting aggregates: Protocol 68 / `bac2177fab013096c2bb890596face218aba3e46387c4a1e4470ea655dc75605`; Protocol.Tests 22 / `672d85c5487281a3f0e272a08f2e265d839d098324eb1a38f6270280c40dbb03`; schemas 34 / `d2384e52ff0901f915373b3d7976067ab3d1a10897dab8b85925c70f6d2233cc`; frozen Broker 40 / `f443abfce66e6fb4cea366bdc876079217d9f402cc3093ffe28ddcda3a1e8692`; Broker.Tests 18 / `16e5d80eff5e94cefdd1a681abfcc2df05d22faadb1726d8f8797be869630297`; solution `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`; Unity 769 / `506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f`. The 7 new leaves must be absent/collision-free and all 9 overlay pre-hashes must match the DAG-rebase receipt before editing.

Acceptance: locked/no-restore Release Protocol.Tests build 0/0; full Protocol TRX; exact Phase2 schema counts; full Release solution 0/0; Broker smoke empty stdout/exact `W24FS001`/23; 16/16 manifest, overlay provenance, forbidden path/authority/runtime/API scan, frozen-root/Unity replay, zero residue, STOPPED handoff and fresh independent audit. Report actual counts. Scope cannot exceed `PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY`.

Final closeout: `CLOSED / SCOPED GO — PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY`; final independent audit `P0=0 / P1=0 / P2=0`; final package state `STOPPED`. External handoff pre-status SHA-256 `0a95a335e982527d5f1b5f9193547c40b0093c5832c01b6f4b88d8ee2b12698c` is an audit input only, not a self-hash. Exact authored source manifest is 16/16 `f6095bb4324b33d7d196f0c0c858d52b879c79a079906b18c778781aceaade26`; self-excluded receipt manifest is 39/39 `7e6b9d2c3a963f5d5dcc1d083418e3a812152cb26e74468ff1b94ca617341cd4`. Final aggregates are Protocol 70 / `0ce0420ff218a43b06599bdac1afdc3ff327ab2df93fc99e697b0248ebbb01b4`; Protocol.Tests 25 / `d5fcf76c8d52387effd9a3d2733d458af816dbabda7f8cd60ff5f85002587622`; desktop schemas 36 / `0e41683a2c93c832e5ea6d86667c726eaad3ce65336ce25bb1d9be6b5cb47538`. Validation closed at TRX 104/104, schema verifier 22/13/14/236, Protocol.Tests and solution builds 0 warnings / 0 errors, and Broker smoke `W24FS001`/23. Frozen Broker, Broker.Tests, ServiceHost, ServiceHost.Tests, solution, and Unity roots replay exactly. C2 writer and auditor are retired; at C2 closeout the active package was `NONE / NEXT NOT PUBLISHED`. C1 remains GO, D1 remains `STOPPED / NO-GO`, and the DAG-rebase closed GO/P2=1 record remains historical. No production, runtime, transport, locator issuer or ACK issuer, project I/O, handle/grant, command or authority is enabled; Phase 2 remains NO-GO; quota remains 100% with no quota-based scheduling.

## 16. D1R `WP-P2-DURABLE-PRODUCTION-PROFILE-REMEDIATION` final closed boundary

- Final state: `CLOSED / SCOPED GO — DORMANT_DURABLE_PRODUCTION_PROFILE_STORE_REMEDIATION_ONLY`; package state `STOPPED`. The fresh `gpt-5.6-terra`/`max` writer and the independent read-only auditor are retired. The final independent audit closed all four findings with `P0=0 / P1=0 / P2=0`.
- Exact final source hashes are `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs` / `636ef191393395981e991d891d4f7d43924bf1dd6230e12fef031e183d961ed1`; `services/VFXComposer.Broker/Security/WindowsDurableProfileStore.cs` / `800d7c0ab03ddb46aab8a7dce2bb8741662013ea277f27c01ca12dcec81aadb8`; and `services/VFXComposer.Broker.Tests/WindowsDurableProfileStoreTests.cs` / `faecf035e24ee7bed5398d4f2f9facffcf5c1bf6ae0e0ddd28fa5c6fab992b0d`. The strict-Ordinal source manifest is 3/3 `337198737cfd647422a7f1a82c1073d936b6ea5d4f462b18e082b5cc929dd90c`.
- The self-excluded receipt manifest is 65/65 `ba2341f446b6b6bff98098a0c5067b23c1dfdca0aa4f26743039b43c183b1726`; final root aggregate manifest is 9/9 `1b9b145a17a38f20609df505828dbc877f9df6c8fbb8e5f295fe0865351afa21`. Final owned roots are Broker 40 / `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c` and Broker.Tests 18 / `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66`; all foreign/C2 roots replay exactly.
- Validation closed at targeted D1R tests 35/35, full Broker regression 113/113, schema verifier 22/13/14/236, locked/no-restore Broker.Tests and Release solution builds 0 warnings / 0 errors, and Broker smoke zero stdout with exact `W24FS001` stderr and exit 23. The final audit used the generated handoff pre-status SHA-256 `b196070c07dc98b722cf3ca3741f1981f009c45117caa0c2d3d9f57604b74e56` as an input only, never as a self-hash.
- Scope remains dormant source/static/synthetic remediation and no-privilege runtime-negative behavior only. D1R did not enable `SeSecurityPrivilege`, provision or pin a privileged root, or claim a privileged strict-store open, live commit, reopen, readback or receipt. `Program`, `BrokerPolicy`, ServiceHost, listener, SCM, path/environment/registry trust, project I/O, Client/Desktop/Worker/Unity, Protocol/schema, production activation and authority remain outside this package and NO-GO.
- The inherited bare `dotnet restore VFXComposer.sln --locked-mode` NU1004 limitation remains nonblocking: pre-existing HandleProbe/Broker.Tests project-reference lock inconsistencies were not changed, and the passing builds were locked/no-restore. No lock file changed.
- C1 and C2 remain closed GO; old D1 remains retained `STOPPED / NO-GO`; the DAG rebase remains historical scoped GO/P2=1. At D1R closeout, the active package was `NONE / NEXT NOT PUBLISHED`; I1 was NO-GO and not published or started, W1 was not active or published, and production, Phase 2 and authority remained NO-GO.

## 17. P1 `WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION` active contract

Unique objective:

> Implement one dormant Windows-native per-serving-instance pipe provisioning and exact same-handle readback primitive. It applies the already-frozen canonical ACL to a new unconnected pipe instance, proves the exact returned handle's owner/group/DACL/SACL/control/native ACE facts before any connect or accept, and otherwise fails closed. It is not a listener, ServiceHost `Running` gate, peer admission, project route, or authority issuer.

The sole fresh writer is `gpt-5.6-terra` with `max` reasoning. Its only author-controlled product/test leaves are exactly:

- `services/VFXComposer.Broker/Ipc/WindowsProductionNamedPipeHost.cs`
- `services/VFXComposer.Broker/Security/WindowsNamedPipeAclReadback.cs`
- `services/VFXComposer.Broker.Tests/WindowsProductionNamedPipeHostTests.cs`
- `services/VFXComposer.Broker.Tests/WindowsNamedPipeAclReadbackTests.cs`

All four paths were pre-absent at publication and their case-insensitive collision count was zero; the writer must repeat that check immediately before its first write. Package metadata is limited to `docs/coordination/handoffs/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION.md`, `.codex_tmp/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION/**`, and required build/test `**/bin/**` and `**/obj/**`. Metadata is not product-source ownership. A fifth author-controlled source/test leaf, any csproj/lock/solution/control-document extra, or any other handoff is STOP.

Preflight must replay the following strict-Ordinal roots (repository-relative forward-slash paths, lowercase file hashes, two ASCII spaces, LF, UTF-8 without BOM, `StringComparer.Ordinal`, excluding `bin/obj`):

| Root | Count | SHA-256 |
|---|---:|---|
| `services/VFXComposer.Broker` | 40 | `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c` |
| `services/VFXComposer.Broker.Tests` | 18 | `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66` |
| `src/VFXComposer.Protocol` | 70 | `0ce0420ff218a43b06599bdac1afdc3ff327ab2df93fc99e697b0248ebbb01b4` |
| `src/VFXComposer.Protocol.Tests` | 25 | `d5fcf76c8d52387effd9a3d2733d458af816dbabda7f8cd60ff5f85002587622` |
| `docs/schemas/desktop` | 36 | `0e41683a2c93c832e5ea6d86667c726eaad3ce65336ce25bb1d9be6b5cb47538` |
| `services/VFXComposer.Broker.ServiceHost` | 8 | `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103` |
| `services/VFXComposer.Broker.ServiceHost.Tests` | 6 | `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c` |
| `VFXComposer.sln` | 1 | `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689` |
| `project/Packages/com.vfxcomposer.unity` | 769 | `506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f` |

The frozen direct ACL/profile inputs are also preflight hashes, never editable P1 inputs:

| Input | SHA-256 |
|---|---|
| `services/VFXComposer.Broker/Security/CanonicalNamedPipeAcl.cs` | `ccaccb2be4d15f45f3ba248ee291bad598c9acf3a58625a4bb4015ffd40937d7` |
| `services/VFXComposer.Broker/Security/WindowsNamedPipeAclProvisioningIntent.cs` | `51a19b3a601b1134761537bf4d102d431ef04b56abd884a3180c9f79e3f777fc` |
| `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs` | `636ef191393395981e991d891d4f7d43924bf1dd6230e12fef031e183d961ed1` |
| `services/VFXComposer.Broker/Security/WindowsSid.cs` | `438ff2fe3f1d74fc33ebe910d0b8525a312f9ec71af62186e60f67f02bbf7f09` |

P1's only graph dependency is the closed D1R source boundary. It must bind the exact D1R `vfxcomposer.durable-production-profile/1` profile digest and exact service SID to the existing `WindowsNamedPipeAclProvisioningIntent`; the durable profile's service SID must fixed-match the intent's service SID before a handle is created. This creates no independent profile, caller trust root, bootstrap substitute, privileged root, token privilege, installation, service, or live-store receipt. I1 remains an external privileged-root preflight blocker, is not a P1 dependency substitute, and is neither published nor started.

Per-instance contract:

1. Create each candidate through native `CreateNamedPipeW` with a real `SECURITY_ATTRIBUTES` that contains only the validated security descriptor created from the existing intent. The handle is non-inheritable. The bootstrap instance uses `FILE_FLAG_FIRST_PIPE_INSTANCE`; every instance uses `PIPE_REJECT_REMOTE_CLIENTS`. A later instance must make a fresh native creation and may not reuse a first-instance, name-reopened, or cached handle.
2. Immediately call native `GetSecurityInfo` on the exact returned `SafePipeHandle` with owner, group, DACL and SACL requested. Name-based reopen, a different handle, a cached descriptor, `GetNamedSecurityInfo`, or an inferred ACL is forbidden. Any call error, null required owner/group/DACL, unreadable SACL, allocation/parsing error, or mismatch fails closed and disposes the just-created handle before it can be returned.
3. The readback must require exact service-SID owner and group; an absent readable SACL; exact control flags `SELF_RELATIVE | DACL_PRESENT | DACL_PROTECTED` (`0x9004`) and no other control/SACL bit; and exactly two DACL ACEs in this order: non-inherited, non-opaque `AccessAllowed` user SID mask `0x0012019B`, then the exact service SID mask `0x001F019F`. Type, flags, inheritance, opaque length, mask, SID and native ACE count/order are all exact; broad, deny, callback, inherited, duplicate or extra ACEs fail.
4. Readback is complete before any `ConnectNamedPipe`, accept, peer fact, session issue, receipt usable by `Running`, or handoff to another host. The first future serving receipt is a B1-only prerequisite to `Running`; P1 cannot report or consume it as a `Running` fact. Each later serving candidate independently repeats creation and same-handle readback; prior success never blesses a later handle. State is bounded to the currently owned safe handle and immutable exact facts, and all native descriptor/local allocations and failure paths clean up deterministically.

P1 may expose only an unconnected, readback-verified dormant candidate to a later owned integration node. It may not wire `Program`, `BrokerPolicy`, `NamedPipeBrokerHost`, an existing host, ServiceHost or any `Running` path; invoke listener loops or peer acceptance; use `CurrentUserOnly`, a test issuer or bootstrap material as success evidence; add path/project/Client/Desktop/Worker/Unity/authority surface; or claim that an ordinary token has produced a live serving instance. Ordinary-token execution may fail closed only. Any name reopen/different handle, unreadable SACL acceptance, readback-after-accept, cached ACL state, actual listener activation, `Running`, or authority claim is STOP.

The exact new-test budget is 16 and must remain split 8+8:

| Test leaf | Exact count | Required cases |
|---|---:|---|
| `WindowsProductionNamedPipeHostTests.cs` | 8 | first-instance non-inheritable `CreateNamedPipeW`/`PIPE_REJECT_REMOTE_CLIENTS`; same-handle readback before any connect; successful result remains unconnected; create failure cleanup; readback failure cleanup/no accept; fresh later-instance create/readback; cache/name/different-handle rejection; ordinary-token fail-closed/no positive serving state. |
| `WindowsNamedPipeAclReadbackTests.cs` | 8 | exact canonical pass; owner mismatch; group mismatch; SACL present or unreadable; control mismatch; ACE type/flags/inheritance/opaque/order mismatch; user SID/mask mismatch; service SID/mask/extra-ACE mismatch. |

Acceptance is not met until durable receipts show target 16/16 and full Broker 129/129; unchanged schema verification 22 total / 13 Phase 2 / 14 positive / 236 negative; Broker.Tests and full Release solution builds with 0 warnings / 0 errors; and Broker smoke with zero stdout, exact `W24FS001` stderr and exit 23. The handoff must carry the exact 4/4 source manifest, self-excluded receipt manifest, frozen-root replay, command/exit/TRX receipts, source/PE/native ABI (`SECURITY_ATTRIBUTES` and safe-handle) scan, no-wiring scan, collision/absence proof and zero-residue proof. It must end `STOPPED`, after which a fresh independent read-only audit must report actual findings; no scoped GO or production capability is presumed by this publication.
