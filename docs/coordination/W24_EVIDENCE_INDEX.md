# W24 coordination evidence index

> **CURRENT EVIDENCE ROUTING — U0 USER-MODE REBASE (2026-08-28).** ADR-005 and this section control new evidence selection. The pre-U0 entries retained below preserve exact history but cannot be selected as proof of the ordinary-user runtime or treated as current dependencies/blockers.
>
> Normative U0 architecture token: `USER_MODE_LOCAL_CREATIVE_TOOL_V1`.

## U0 architecture evidence boundary

- Baseline branch: `codex/u0-user-mode-architecture`.
- Exact starting commit: `038d1b0ef1675fd6bd12c2b1cd196ff17546917b`.
- U0 owns exactly seven documents:
  1. `docs/rules/ADR-005_USER_MODE_BROKER_WORKER_ARCHITECTURE.md` (new);
  2. `docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md` (historical body retained; top supersession notice only);
  3. `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md`;
  4. `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md`;
  5. `docs/coordination/W24_PROGRAM_CONTROL.md`;
  6. `docs/coordination/W24_WORK_PACKAGE_REGISTRY.md`;
  7. `docs/coordination/W24_EVIDENCE_INDEX.md`.
- U0 accepts no source/binary/runtime receipt, performs no M1/M2 merge, and writes no handoff micro-package.
- Required validation is `eng/run-phase2-gate.ps1 -Milestone u0-user-mode-architecture -MutableRoot docs/rules,docs/stage-notes,docs/coordination`; its durable output is the U0 gate evidence. One final commit binds the seven-document rebase.

The exact current DAG is `U0 -> U1`; `U0 -> U2`; `U1 + U2 -> U3`; `U3 -> U4 -> U5 -> U6`. U1 combines C3+W1 actual Unity Worker connector; U2 is ordinary-user child-process/pipe/nonce/session/cleanup; U3 is project selection/read containment; U4 Desktop integration; U5 local E2E; U6 final audit.

The threat/evidence claim is deliberately narrow: current user and intentionally launched local processes/project choice are trusted; cross-user, stale session, wrong project, protocol drift, PID reuse, unexpected path, leakage and crash are defended; same-user malware, admin/kernel and offline attacks are out of scope. Hashes/signatures prove release integrity only.

## Reuse evidence and non-rebinding

C1/C2 accepted contract evidence may be consumed by U1/U3. P1 named-pipe and S1 lifecycle/ownership evidence may be reviewed only for ordinary-user-compatible fragments and must be rebound through new U-node source/test manifests. The stopped M1 uncommitted 12-file candidate is U1 review input, not accepted evidence; M2 `fa8843be` is historical and never enters the line.

D1/D1R, ServiceHost/install, I1, R1, A1, B1 and their privileged/installed E2E or audit evidence are historical. They receive no further implementation/audit and are not blockers. Nothing in their receipts proves a current-user child topology, explicit user project selection, or U0-U6 acceptance.

## Frozen estimate evidence

U0 freezes a formal completion planning point of `45/100`, reported as **42%-48%**, using U0-U6 weights `8/22/22/18/12/12/6` and explicit accepted reuse credit only. It also freezes a 50% remaining-effort reduction planning point, reported as **45%-55% shorter** than the privileged remaining route. M1/M2 contribute zero. Future status must compare actual accepted work against this baseline rather than recompute history.

---

## Historical pre-U0 evidence index (superseded for product delivery)

Status date: `2026-08-28`

This index binds the controller's starting state. It is not a new authority or a substitute for opening the referenced report and receipt.

## 1. Architecture and migration

| Artifact | SHA-256 | Scope |
|---|---|---|
| `docs/rules/ADR-002_STANDALONE_DESKTOP_UNITY_WORKER_BROKER.md` | `0a3f8f3cd8aa3e6ccd748f70da00c182b017d92b361cba88eccc87219f9fe110` | Process/ownership/threat-model decision plus the user-authorized Protocol-contract-only sequencing exception. |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md` | `7a92d6413f1ce1405161ee3881ab7b373dea97ab7fe2a76fb7f6f36f515b233f` | Phase/file/gate plan with Phase 3 runtime still not started. |
| `docs/stage-notes/W24_UNITY_UI_TO_DESKTOP_MIGRATION_MATRIX.md` | `db1172620c9b27dd60a5381b68dc9d0e7ba894812ebae6288a4e7d512c937c5b` | Legacy UI parity and KEEP/HIDE/NO-DELETE boundaries. |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE1_REPORT.md` | `f1c6e9529160ec9c5a7edf28de5663b195152b63304bd586f1311f782073eb02` | Phase 1 disconnected Desktop/Protocol evidence only. |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md` | `9780d1f9340b6ec98a2e621c323a3124c30ed7df6bcb764bb9dda421396c7dd4` | Phase 2 foundation status; production remains NO-GO. |

## 2. Latest closed atomic node

| Artifact | SHA-256 | Scope |
|---|---|---|
| `docs/stage-notes/W24_PHASE2_DESKTOP_BROKER_READ_TRANSPORT_REPORT.md` | `c0af543862f04de3c8c0ae1c138c4c5790aa94dfed13a476129f27ad5ffbd774` | `DESKTOP_TO_BROKER_TEST_PIPE_READ_QUERY_SCAFFOLD_ONLY`, audit P0/P1/P2=0. |
| `docs/stage-notes/W24_PHASE2_DESKTOP_BROKER_READ_SOURCE_MANIFEST.sha256` | `5f05a77e1530e5af7e35c874118d3a09604bb5d2a404d52a622235d7244afca5` | 17 files, 17/17 replay, 2,143 bytes, LF-only, Ordinal paths. |

Accepted r5 counts: Protocol `80`, Client `12`, Desktop `9`, Broker `35`, total `136/136`. This is a same-process test-pipe scaffold receipt. It is not a production project-read or Desktop-application receipt.

Rejected predecessor r2/r3/r4 evidence remains diagnostic only and must not be selected as the accepted source binding.

## 3. `WP-PROTOCOL-P3` starting baselines

Package outcome: `STOPPED — DUPLICATE_WRITER_COLLISION`; the baselines below were valid before edits, but the resulting partial Protocol/schema bytes have mixed writer provenance and are not a scoped GO.

Collision handoff: `docs/coordination/handoffs/WP-PROTOCOL-P3.md`, SHA-256 `e76a338e9b2df6447a890c54d03e9a3b3df38a0a0821cfc297df178f535794ec`.

Aggregate encoding:

1. recursively enumerate files under the named root;
2. exclude any file below `bin/` or `obj/`;
3. convert repository-relative paths to forward slashes;
4. sort paths with `StringComparer.Ordinal`;
5. encode each line as `<lowercase-sha256><two spaces><forward-path><LF>`;
6. SHA-256 the UTF-8 concatenation.

| Root | File count | Aggregate SHA-256 |
|---|---:|---|
| `src/VFXComposer.Protocol` | 48 | `4a82287e5533794c9b3b7fa915facc25a89a04978f2bc9b32b24ec4a8d939a0f` |
| `src/VFXComposer.Protocol.Tests` | 18 | `f7ce93800ab967945d67d80b0c119da61c6ff4741e4f3f56d9c54be5dcd1cabb` |
| `docs/schemas/desktop` | 19 | `214bb22e95ab9488f39d9af230bbd9850367656956c65e4337f92390b32ef3f3` |

At this baseline, `docs/schemas/desktop/commands/` and `docs/schemas/desktop/jobs/` do not exist.

The Protocol writer must reproduce these three baselines before editing. A mismatch is a STOP condition and must be reported to the controller.

## 4. `WP-PROTOCOL-P3-RECOVERY` accepted boundary

- Scoped status: `PURE_PROTOCOL_COMMAND_JOB_CONTRACT_FOUNDATION_ONLY / GO`.
- Independent audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-PROTOCOL-P3-RECOVERY.md`; external SHA-256 `bb8619359bc0aae2ed771b42d4584721fa7f4190df7ce1de6169812a4f6a276a`.
- Authored manifest: 41 files; SHA-256 `9d5c564958ff4d2eca785b8a9cc5b98b4ccf1aaed4920d5e029088bb7a39af48`.
- Receipt manifest: 18 files; SHA-256 `01a331168fdeb05e04dba891a99eb99b42801490e18b3eeffbc1fe36c9d33216`.
- TRX: 88/88; SHA-256 `2439d1d259f264350f54b5ff17785dff03d79590180cb0596d0bd78d406ad710`.
- Final repository-relative forward-slash aggregates:
  - `src/VFXComposer.Protocol`: 67 / `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`
  - `src/VFXComposer.Protocol.Tests`: 21 / `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`
  - `docs/schemas/desktop`: 33 / `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`

This evidence grants no Client/Broker/Worker runtime, transport, project I/O, command-execution or authority capability.

## 5. `WP-BROKER-P2-PROD` accepted boundary

- Scoped status: `BROKER_PRODUCTION_TRUST_ACL_DORMANT_FOUNDATION_ONLY / GO`.
- Independent audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-PROD.md`; external SHA-256 `6565fc2a7b228aca30e8d50856eed73f036c2e0749acba194ba8cd639e624ea2`.
- Broker aggregate: 28 / `522a0f21738fa902df35f920fb9478943f3be722133e47bd6451cc291bde7284`.
- Broker.Tests aggregate: 12 / `f70050154f26b5594a301b1f2cd0da6fbc8d6ff2e37580c42c63035c117124f1`.
- Authored manifest: 8/8; SHA-256 `3a2af548e159098108a280f554cb2c8f6824886f4c33bd0b65f48d8ea2f2e9b1`.
- Evidence manifest: 21/21; SHA-256 `91640c4246dea170791127af630c5a3183c87c64419633ccc0e3926e712ebb58`.
- TRX: 40/40; SHA-256 `cfac707f97990f5ae14eb82d5e2bce11afdc95ad78e2a0c7a8282b29308ac666`.
- Controlled smoke: stderr `W24FS001`, exit `23`, stdout empty; no production listener was activated.

This slice freezes dormant trust-profile and ACL semantics only. Independently privileged service/bootstrap issuance, live OS attestation, real ACL provisioning, production connection, project access and authority remain absent.

## 6. `WP-BROKER-P2-ISSUER-HOST` accepted boundary

- Scoped status: `BROKER_ISSUER_HOST_BOOTSTRAP_ACL_DORMANT_FOUNDATION_ONLY / GO`.
- Independent audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-ISSUER-HOST.md`; external SHA-256 `fd57c194af5a58119fa3fecd05c99c55128cf0fc0cd8cb40ba7bfd9decd63d93`.
- Authored source: 3 files; manifest SHA-256 `c480c3b3f445f3b113bf8556c408503eb415f70c226cb4c34247c34efec3117b`.
- Evidence manifest: 26/26; SHA-256 `0bcc7a045b03f3f275bd80d6a3e65271a52d5371d78e0993498a5e4cd69812eb`.
- TRX: 48/48; SHA-256 `bdf0c6e9e39c1350121815f93eed12c8cde476b015090b2adae55f665f4068b8`.
- Final aggregates: Broker 30 / `10b285e11f0229d0d4ba6286559d0ab0d6da217a9f73fc2412ac9d82cc52d95e`; Broker.Tests 13 / `212a4aa7c471a5e80d339178fca493a69b3b75f6613579eb2e82020fca4f74ce`.
- Controlled smoke remains stdout empty, stderr `W24FS001`, exit `23`.

This accepted dormant source/test slice introduces no installed service, SCM registration, live OS attestation, production issuer/listener, actual ACL application, project access or authority. It is not the Phase 2 gate or a production activation receipt.

## 7. `WP-BROKER-P2-LIVE-ATTESTATION` accepted boundary

- Scoped status: `DORMANT_WINDOWS_PROCESS_TOKEN_PATH_OBSERVATION_FOUNDATION_ONLY / GO`.
- Independent audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-LIVE-ATTESTATION.md`; external SHA-256 `39765c806cbf0fd51a328e2eda2dccd104df5c27585c584591c67f88b9174110`.
- Authored source: 3 files; manifest SHA-256 `f4dee3f195026f85901d63e48eeee4a0e770f930d394a3f227497d338b8c1954`.
- Evidence manifest: 18/18; SHA-256 `2fca4efa60a55f01e6fabfa61bd71ee4ab5210d178e90ebd6e7d82755882cb4c`.
- TRX: 54/54; SHA-256 `21ba84b4110b75e1cf80fe33af15dae17335f0145e4eaae71b877db865bb60d5`.
- Final aggregates: Broker 32 / `e5b7fad903c584422c8c5afa2e810a9fdfeebf34b3d765a527aea935a5f9f3ac`; Broker.Tests 14 / `f345f0ba8b8b32619a8bf6fde1cd9702a3bd390f89b488e6c49a9066b23456e2`.
- Controlled smoke remains stdout empty, stderr `W24FS001`, exit `23`.

This slice records a pinned process/token/native-path observation primitive only. It does not prove an installed service, successful live service attestation, executable-content identity, production issuer/listener, ACL application, project access or authority, and it is not the Phase 2 gate.

## 8. `WP-BROKER-P2-SERVICE-HOST` accepted boundary

- Scoped status: `DORMANT_SCM_SERVICE_RUNTIME_FOUNDATION_ONLY / GO`.
- Final independent audit after fresh-agent provenance recovery: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-SERVICE-HOST.md`; external SHA-256 `8b4d74d6593bc151f57cc444f04ea9ff3cffc25c1589fd2b11d4f2e139c5d2d9`.
- Authored source/solution manifest: 15/15; SHA-256 `4d1154af3a300201f2b941ef8017524a4e473ac773c02e17ed291f1c4542f44c`.
- Evidence manifest: 39/39; SHA-256 `033ff72b30f7c18619a83a4c1be8d2c12b40488ae3ef282835a668eddce456e2`.
- TRX: 16/16; SHA-256 `1e023fa57d3cac3c706bae2cb698a2804f5a5a49e47345781dd6330cd2396e79`.
- ServiceHost aggregate: 8 / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`; ServiceHost.Tests: 6 / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`.
- Solution: current `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`; exact reconstructed pre-edit baseline `52f5773cf00f230d451cb73ba5228735987ea8bfa7c6f894dc441a746df2604e`.
- Provenance recovery independently removed and reinserted exactly two complete project blocks, eight configuration lines and two nesting lines with byte-identical round trip; solution/product bytes were not changed during recovery.
- Direct smoke remains stdout empty, exact `W24FS001`, exit `23`.

This framework-dependent service-host runtime does not install, register, start, configure or delete an SCM service. Production issuer/live attestation/content identity/policy/listener/ACL/project access/authority and the Phase 2 gate remain absent.

## 9. `WP-BROKER-P2-INSTALL-POLICY` accepted boundary

- Package status: `CLOSED / SCOPED GO — DORMANT_SCM_INSTALLATION_POLICY_FOUNDATION_ONLY`.
- Independent final audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-INSTALL-POLICY.md`; pre-controller-status-delta SHA-256 `79aa061021d7c803b69fbd44fdf5a5643b3d06bcfc9d0b5f75a2d8cfeee9b21b`.
- Authored source manifest: 3/3; SHA-256 `45eb51eac83ef1f8144be1a9f6e443048415d15d92fcf686e4547345b761530b`.
- Evidence manifest: 22/22; SHA-256 `505db45b99bcffa9dbd9f77c2e91b92b92e7b91dd8a8bd93a53122c8daceaf39`.
- Final aggregates: Broker 34 / `a82899f692fd3dcb568eb5bf820f90ecf92f4054b515f8d3621a2e34c361772a`; Broker.Tests 15 / `c6864861a756cd0458e40c886855ee775ef15cf3d04a5129550cf7e324d74835`.
- Final TRX: 61/61 passed; SHA-256 `a315c1962c428043d66f7bc96bf6a4ccb782f90f25067c65cfccb1a11a111b5c`.
- Broker.Tests and full solution Release builds: `0 warnings / 0 errors`; controlled smoke: stdout empty, stderr exact `W24FS001`, exit `23`.
- User-reported quota was `18%` at publication and is stale after closeout. Refresh it before publishing any later package; the `<=7%` and `<=5%` rules remain in force.

This evidence proves only an internal, immutable, non-wire dormant least-privilege SCM installation-policy candidate. It does not prove or enable a production installation, SCM mutation, service registration/configuration/start, successful live service attestation, executable-content identity, production issuer/listener/ACL application, project access, Worker/Desktop/Unity production routing, commands, authority or the Phase 2 gate.

## 10. `WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY` accepted boundary

- Package status: `CLOSED / SCOPED GO — DORMANT_EXECUTABLE_CONTENT_IDENTITY_POLICY_FOUNDATION_ONLY`.
- Independent final audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-EXECUTABLE-IDENTITY-POLICY.md`; pre-controller-status-delta SHA-256 `7d9e824b82b9e8a901393a688cf7493bc398bb05f0584511a5b4d0508d575ebd`.
- Authored source manifest: 3/3; SHA-256 `ae2033ae6402d7ac40c27844ca17c9fa432cbafd840637227142a0e006c8bddb`.
- Evidence manifest: 24/24; SHA-256 `a9417349bfa4c8ff0b9ceb3cafd81aa918a5e267c2c716793e04b5b4e38fe880`.
- Final aggregates: Broker 36 / `2a8805352c20259f1c2b06b00102f83e7982f178b4de550072595cb126af98c1`; Broker.Tests 16 / `1ff9e8b497598c6a0f0a620657e24bbad04542e5e048974308cb6754393fac75`.
- Final TRX: 68/68 passed; SHA-256 `7d356d4732f336d12e70d1f6e56aea55cf1928c668a499b65d0435298bd63153`.
- Broker.Tests and full solution Release builds: `0 warnings / 0 errors`; controlled smoke: stdout empty, stderr exact `W24FS001`, exit `23`.
- User-reported quota was `16%` at publication and is stale after closeout. Do not publish any later package until it is refreshed; the `<=7%` and `<=5%` rules remain in force.

This evidence proves only a supplied-in-memory internal, immutable, non-wire executable-content identity policy/correlation model. It does not prove or enable file/path/handle byte observation, loaded-image equality, Authenticode/signature/certificate validation, an installed service or SCM mutation, production issuer/listener/ACL application/project/Worker/Desktop/Unity routes/commands/authority, or the Phase 2 gate.

## 11. `WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION` accepted boundary

- Package status: `CLOSED / SCOPED GO — DORMANT_PINNED_EXECUTABLE_HANDLE_HASH_OBSERVATION_ONLY`.
- Independent final audit: `P0=0 / P1=0 / P2=0`.
- Handoff: `docs/coordination/handoffs/WP-BROKER-P2-EXECUTABLE-BYTE-OBSERVATION.md`; pre-controller-status-delta SHA-256 `2981449f9c0808e24c3a268b6c2faa82267ae226eb3f6ca6aa9a65a789314ac2`.
- Authored source manifest: 3/3; SHA-256 `571974b214be8f9146103a1bc06b7e98fe1a47eab10dae97166290d6731ac979`.
- Evidence manifest: 24/24; SHA-256 `cb88bd5b42cf2807a314c820a93c2e133833fe2028129c4e353c93a866545886`.
- Final aggregates: Broker 38 / `8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760`; Broker.Tests 17 / `66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60`.
- Final TRX: 78/78 passed; SHA-256 `c5a5cb0336e1b83c0cb003e1af03388fc886f7679b7f4ff4b96b6ade985a19d8`.
- Broker.Tests and full solution Release builds: `0 warnings / 0 errors`; controlled smoke: stdout empty, stderr exact `W24FS001`, exit `23`; zero scratch residue.
- User-reported quota was `14%` at publication and is stale after final `STOPPED`. No later package may be published until it is refreshed; the `<=7%` and `<=5%` rules remain in force.
- Runtime reparse-point negative: not claimed. The source predicate and static surface evidence exist, but a safe privilege-independent reparse setup/cleanup was not assumed; this remains an honest coverage residual, not a success claim.

This evidence proves only a bounded observation of a caller-supplied already-open local file handle. It does not prove or enable loaded-image equality or image-path identity, signature/Authenticode/signer/certificate validation, service installation or SCM state/action, production wiring/admission, project access, authority, or the Phase 2 gate.

## 12. `WP-P2-PRODUCTION-READ-GATE-FREEZE` final closed boundary

- Package state: `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_ARCHITECTURE_FREEZE_ONLY`.
- Final independent audit: `P0=0 / P1=0 / P2=0`; no implementation, production, project-read, command or authority capability is granted.
- Final ownership/DAG accounting: `DAG_NODES=12`, `DAG_EDGE_GROUPS=6`, `DIRECTED_EDGES=15`, `EXACT_OWNED_FILES=68`, `RECEIPT_ROOT_EXCEPTIONS=1`, `OWNERSHIP_DUPLICATES=0`.

| Final artifact | SHA-256 |
|---|---|
| `docs/rules/ADR-004_WINDOWS_BROKER_INSTALLATION_AND_PRODUCTION_READ.md` | `7849105a1d8038592b744816662fdf60b18d5760e462d3b71fabde51882afd75` |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE_PLAN.md` | `737d968f05a6f8b9467ba16d78f62d4e4b442ff65d623bad4ab1f98447a16ef0` |
| `docs/stage-notes/W24_STANDALONE_DESKTOP_PHASE2_REPORT.md` | `7aca5fc37a1b48f3b207230fe3504b8eee161e0794a6a3db73789dd5e29da0ef` |
| Handoff pre-status, `docs/coordination/handoffs/WP-P2-PRODUCTION-READ-GATE-FREEZE.md` | `4bbe445e31551b18aca2b303d546e1e692e88cf1efacd0d6f1f4bbfda8155537` |
| Authored manifest | `54026fa11679d09dc6a2aa6a9f9ee6aeec510f8107b14d62060ac41be4c5859c` |
| Final independent audit verification | `4ce24729bab5fef581c174092e28b6d8b19729d6bbea68ced3baf0f3f5b8c0e0` |

The pre-status handoff hash is an audit input, not a self-hash of its final status-updated form. Existing source/runtime bytes remain frozen. Production stays `W24FS001`/23 and Phase 2 stays NO-GO. D1 is separately published below; C1 is final `STOPPED` and W1 remains unpublished.

## 13. D1 `WP-P2-DURABLE-PRODUCTION-PROFILE` stopped boundary

- State: `STOPPED / NO-GO — REMEDIATION AND DAG REBASE REQUIRED`; no scoped GO.
- Exact targets are pre-absent: `DurableProductionProfile.cs`, `WindowsDurableProfileStore.cs`, and `WindowsDurableProfileStoreTests.cs`.
- Starting aggregates: Broker 38 / `8f58569ce4d92c83eb1b1910c4156e6760240da6cca5e3385d31301f4d6d5760`; Broker.Tests 17 / `66948ddcd0ebf5ebd546a0c8c25b481471313c8af904671ca383ff7789261a60`; Protocol 67 / `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`; Protocol.Tests 21 / `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`; desktop schemas 33 / `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`; ServiceHost 8 / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`; ServiceHost.Tests 6 / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`; solution `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`.
- Product/test authorship is exactly three files. One handoff and `.codex_tmp/WP-P2-DURABLE-PRODUCTION-PROFILE/validation/**` are metadata/receipt exceptions. All other source and docs remain frozen.
- D1 is dormant only. It cannot load production policy, activate `Program`/`BrokerPolicy`/ServiceHost/listener/SCM, access a project, connect Worker/Desktop/Unity, issue commands or grant authority. Production remains `W24FS001`/23 and Phase 2 remains NO-GO.
- Frozen source handoff: `docs/coordination/handoffs/WP-P2-DURABLE-PRODUCTION-PROFILE.md`; external handoff SHA begins `e8819e0c` and ends `6f5ad4a1`. Source manifest begins `3dde2adc` and ends `108412e9`; receipt list begins `b578f305` and ends `892d1757`. Current TEMP package residue is zero.
- Independent audit found two acceptance/architecture blockers: all strict store-file SACL reads need `ACCESS_SYSTEM_SECURITY`/privilege but internal opens omit it; and D1 live success requires the I1-owned privileged root while the frozen edge is D1→I1. P1 defects include a rename handle without `DELETE`, native plaintext key buffers not zeroed before free, overbroad/non-distinct root/file ACL semantics, suffix-truncation rollback, non-Ordinal manifests, and stale controller state now corrected here. Required full regression/solution/smoke/static/ABI evidence was not accepted.
- Retain the three source files as a fail-closed checkpoint; do not roll them back or treat them as an admission dependency. A fresh docs-only DAG rebase and a fresh bounded remediation writer are required before D1 can be reconsidered.

## 14. C1 `WP-P2-PROTOCOL-PROJECT-SELECTION` final closed boundary

- State: `CLOSED / SCOPED GO — PURE_PROTOCOL_REGISTERED_PROJECT_SELECTION_CONTRACT_ONLY`; final independent audit `P0=0 / P1=0 / P2=0`.
- Pre-edit absent targets: `RegisteredProjectSelection.cs`, `RegisteredProjectSelectionTests.cs`, and `vfxcomposer-registered-project-selection-v1.schema.json`; case-insensitive collision count zero.
- Pre-edit starting aggregates: Protocol 67 / `0ea82b9756618459b2a031f8064835f2a1f083a9b8d817df1f35307affd39a56`; Protocol.Tests 21 / `aba9d92d9cb7d73414289e01f33513a50b6b8b5fabf832ce45820134eb294fa5`; desktop schemas 33 / `f0250fac5847b424fa5cfdd34acf45bf02bc121e9829d8ccd10434a3e74a57e9`.
- Frozen retained D1 inputs are Broker 40 / `f443abfce66e6fb4cea366bdc876079217d9f402cc3093ffe28ddcda3a1e8692` and Broker.Tests 18 / `16e5d80eff5e94cefdd1a681abfcc2df05d22faadb1726d8f8797be869630297`; those bytes remain NO-GO and are not a C1 dependency.
- C1 is an exact 12-file pure Protocol/test/schema/verifier package plus one handoff and generated receipt root. It grants no path, project I/O, lease, command, runtime connection or authority. Production remains `W24FS001`/23 and Phase 2 remains NO-GO.

| Final C1 artifact / result | SHA-256 or result |
|---|---|
| External handoff pre-status, `docs/coordination/handoffs/WP-P2-PROTOCOL-PROJECT-SELECTION.md` | `8c35cae3759cd17f89d366184d74d843f8cc5904ca0a894aa4aae7da074759ef` |
| Authored source manifest | 12/12 `27854e8504ec8e7da12e510db906efd9f2f780577d7a1b49e55c039d7da23600` |
| Self-excluded receipt manifest | 10/10 `7402e367014a05b1d45d28df497de8f24563ce68d4cebed523745141583e1eef` |
| Final Protocol aggregate | 68 / `bac2177fab013096c2bb890596face218aba3e46387c4a1e4470ea655dc75605` |
| Final Protocol.Tests aggregate | 22 / `672d85c5487281a3f0e272a08f2e265d839d098324eb1a38f6270280c40dbb03` |
| Final desktop-schema aggregate | 34 / `d2384e52ff0901f915373b3d7976067ab3d1a10897dab8b85925c70f6d2233cc` |
| Protocol TRX | 95/95 passed |
| Schema verifier | 20 total / 11 Phase 2 / 12 positives / 170 negatives |
| Protocol.Tests and solution builds | 0 warnings / 0 errors |
| Broker smoke | empty stdout; exact `W24FS001` stderr; exit `23` |

The external pre-status handoff SHA is an independent-audit input, not a self-hash of this status-updated handoff. C1 is `STOPPED`; its writer and auditor are retired. At C1 closeout, no package was active or next published, W1 was unpublished, and D1 remained `STOPPED / NO-GO`. No production, Phase 2 gate, runtime, project I/O, lease, command or authority is enabled.

## 15. `WP-P2-PRODUCTION-READ-DAG-REBASE-1` final closed boundary

- Final independent verdict: `CLOSED / SCOPED GO — PHASE2_PRODUCTION_READ_DAG_REBASE_ONLY`; `P0=0 / P1=0 / P2=1`; final package state `STOPPED`.
- Current authored hashes: ADR-004 `e97c4a9e5c2bd20b191178732a8dc3804cac368741e410e1bb5dd58d1cd6c141`; Phase Plan `3d254384569658493cb7f0b9f5c414046861db08082d68ab6dd2dbc4c7255199`; Phase2 Report `ee7a5f935e76faf3e14d87a842eba8ff68138f8711061e60cfdca6161bd3c360`.
- External handoff pre-status SHA-256: `373ea70a286aeb61f594126227616114752a2d9c96238a05aeb1f9f67dd0ee9f`. It is an audit input, not a self-hash of the final status-updated handoff.
- Recorded DAG/accounting remain `13/8/17` and `75/12/1`; C2 `7/7` and W1 `9/9` planned leaves remain absent.
- Sole P2, explicitly documented and nonblocking: the failed verifier lacks literal command/times/exit/raw, and receipt-root aggregates are unrepeatable. The independent auditor recomputed the semantic gates, so this residual does not block the documentation-only scoped GO. No receipt PASS is claimed and this status update neither repairs nor regenerates receipts.
- Historical starting architecture identities were ADR-004 `7849105a1d8038592b744816662fdf60b18d5760e462d3b71fabde51882afd75`, Phase Plan `737d968f05a6f8b9467ba16d78f62d4e4b442ff65d623bad4ab1f98447a16ef0`, and Phase2 Report `7aca5fc37a1b48f3b207230fe3504b8eee161e0794a6a3db73789dd5e29da0ef`.
- Inputs remain D1 frozen STOPPED/NO-GO, C1 closed pure Protocol GO, and W1's read-only preflight STOPPED/NO-GO before the rebase; no W1 file was created.
- Scope validation at that historical closeout: only `docs/coordination/W24_PROGRAM_CONTROL.md`, `docs/coordination/W24_WORK_PACKAGE_REGISTRY.md`, this evidence index, and the package handoff were updated for final status. No source or implementation package was published; active package was `NONE / NEXT NOT PUBLISHED`; C2/D1R/W1 were not published. Production remained `W24FS001`/23 and Phase 2 and authority remained NO-GO; quota was `100%` with no quota-based scheduling.

## 16. C2 `WP-P2-PROTOCOL-WORKER-PROJECT-LOCATOR` final closed boundary

- State: `CLOSED / SCOPED GO — PURE_PROTOCOL_WORKER_PROJECT_LOCATOR_CONTRACT_ONLY`; final independent audit `P0=0 / P1=0 / P2=0`; final package state `STOPPED`.
- Final-audit external handoff pre-status SHA-256: `0a95a335e982527d5f1b5f9193547c40b0093c5832c01b6f4b88d8ee2b12698c`. It is an audit input, not a self-hash of the final status-updated handoff.
- Exact authored source manifest: 16/16 `f6095bb4324b33d7d196f0c0c858d52b879c79a079906b18c778781aceaade26`; self-excluded receipt manifest: 39/39 `7e6b9d2c3a963f5d5dcc1d083418e3a812152cb26e74468ff1b94ca617341cd4`.
- Final aggregates: Protocol 70 / `0ce0420ff218a43b06599bdac1afdc3ff327ab2df93fc99e697b0248ebbb01b4`; Protocol.Tests 25 / `d5fcf76c8d52387effd9a3d2733d458af816dbabda7f8cd60ff5f85002587622`; schemas 36 / `0e41683a2c93c832e5ea6d86667c726eaad3ce65336ce25bb1d9be6b5cb47538`. Frozen Broker 40 / `f443abfce66e6fb4cea366bdc876079217d9f402cc3093ffe28ddcda3a1e8692`, Broker.Tests 18 / `16e5d80eff5e94cefdd1a681abfcc2df05d22faadb1726d8f8797be869630297`, ServiceHost 8 / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`, ServiceHost.Tests 6 / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`, solution `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`, and Unity 769 / `506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f` replay exactly.
- Validation: TRX 104/104; schema verifier 22 total / 13 Phase 2 / 14 positives / 236 negatives; Protocol.Tests and solution builds 0 warnings / 0 errors; Broker smoke empty stdout, exact `W24FS001` stderr, exit 23.
- At C2 closeout, the active package was `NONE / NEXT NOT PUBLISHED`; W1 and D1R were not started or published. C1 remains GO, D1 remains `STOPPED / NO-GO`, and the DAG-rebase closed GO/P2=1 is historical and unchanged. C2 is pure contract only: no production/runtime/transport/locator issuer or ACK issuer/project I/O/handle/grant/command/authority capability exists; Phase 2 remains NO-GO. Quota remains 100% with no quota-based scheduling.

## 17. D1R `WP-P2-DURABLE-PRODUCTION-PROFILE-REMEDIATION` final closed boundary

- Final state: `CLOSED / SCOPED GO — DORMANT_DURABLE_PRODUCTION_PROFILE_STORE_REMEDIATION_ONLY`; final package state `STOPPED`. The independent final read-only audit closed all four findings with `P0=0 / P1=0 / P2=0`.
- Exact final source hashes: `services/VFXComposer.Broker/Configuration/DurableProductionProfile.cs` / `636ef191393395981e991d891d4f7d43924bf1dd6230e12fef031e183d961ed1`; `services/VFXComposer.Broker/Security/WindowsDurableProfileStore.cs` / `800d7c0ab03ddb46aab8a7dce2bb8741662013ea277f27c01ca12dcec81aadb8`; `services/VFXComposer.Broker.Tests/WindowsDurableProfileStoreTests.cs` / `faecf035e24ee7bed5398d4f2f9facffcf5c1bf6ae0e0ddd28fa5c6fab992b0d`. Strict-Ordinal source manifest: 3/3 `337198737cfd647422a7f1a82c1073d936b6ea5d4f462b18e082b5cc929dd90c`.
- Self-excluded receipt manifest: 65/65 `ba2341f446b6b6bff98098a0c5067b23c1dfdca0aa4f26743039b43c183b1726`. Final-root aggregate manifest: 9/9 `1b9b145a17a38f20609df505828dbc877f9df6c8fbb8e5f295fe0865351afa21`; final owned roots are Broker 40 / `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c` and Broker.Tests 18 / `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66`; C2 and all foreign frozen roots replay exactly.
- Validation: target D1R tests 35/35; full Broker tests 113/113; schema verifier 22/13/14/236; Broker.Tests and solution builds 0 warnings / 0 errors; Broker smoke zero stdout, exact `W24FS001` stderr, exit 23; zero residue. The independent audit consumed generated `HANDOFF.md` pre-status SHA-256 `b196070c07dc98b722cf3ca3741f1981f009c45117caa0c2d3d9f57604b74e56` as an audit input, not as a self-hash.
- D1R proves dormant source/static/synthetic remediation and no-privilege runtime-negative behavior only. It did not enable `SeSecurityPrivilege`, provision a privileged root, or produce a privileged strict-store open, live commit, reopen, readback or live receipt. I1 alone owns those actions and remains NO-GO as the next DAG dependency; it is not published or started. Production, Phase 2 and authority remain NO-GO.
- The bare `dotnet restore VFXComposer.sln --locked-mode` NU1004 limitation is inherited, nonblocking, and unmodified: pre-existing HandleProbe/Broker.Tests project-reference lock inconsistencies remain outside D1R authority; locked/no-restore builds passed and no lock changed.
- C1 and C2 remain closed GO; old D1 remains retained `STOPPED / NO-GO`; the DAG rebase remains historical scoped GO/P2=1. At D1R closeout, the active package was `NONE / NEXT NOT PUBLISHED`; W1 was not active or published. The final status-updated handoff records no current self-hash.

## 18. P1 `WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION` active publication boundary

- State: `ACTIVE — DORMANT_PER_SERVING_PRODUCTION_NAMED_PIPE_ACL_PROVISIONING_AND_EXACT_READBACK_ONLY`. This is a controller publication boundary only; it records no P1 source result, test pass, production listener, serving receipt, `Running` state or authority.
- The sole fresh Terra Max writer owns exactly four new leaves: `services/VFXComposer.Broker/Ipc/WindowsProductionNamedPipeHost.cs`, `services/VFXComposer.Broker/Security/WindowsNamedPipeAclReadback.cs`, `services/VFXComposer.Broker.Tests/WindowsProductionNamedPipeHostTests.cs`, and `services/VFXComposer.Broker.Tests/WindowsNamedPipeAclReadbackTests.cs`. All four were pre-absent with case-insensitive collision count zero. The only metadata exceptions are `docs/coordination/handoffs/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION.md`, `.codex_tmp/WP-P2-ACTUAL-NAMED-PIPE-ACL-SESSION/**`, and required `bin/obj`; metadata is not a fifth product leaf.
- Frozen preflight roots are Broker 40 / `c769cdc2fc0e169d2d69f43fb0e45a2099b5239dbbd9c67a0d99ab1c50cbe05c`; Broker.Tests 18 / `e8ae08d444b9f1cd34550c7d401bcf6961594c124cbb7b90f5daf73974a74b66`; Protocol 70 / `0ce0420ff218a43b06599bdac1afdc3ff327ab2df93fc99e697b0248ebbb01b4`; Protocol.Tests 25 / `d5fcf76c8d52387effd9a3d2733d458af816dbabda7f8cd60ff5f85002587622`; schemas 36 / `0e41683a2c93c832e5ea6d86667c726eaad3ce65336ce25bb1d9be6b5cb47538`; ServiceHost 8 / `0073c96df90b67863845e328c810469b862e228ccc5f5e3721ac50bba0eb9103`; ServiceHost.Tests 6 / `69ba819bce918379e8141b2a6900b34f8d738531c612899ce5fc360a16eca83c`; solution / `b8804af2170986371d2c804b4b832f8c2c1d2ef904a63cf0a1824d60d7adf689`; Unity 769 / `506068f8338ee96d432987968b95da9e17f8effa723a2b3251be81680680f19f`.
- Frozen direct inputs are canonical ACL `CanonicalNamedPipeAcl.cs` / `ccaccb2be4d15f45f3ba248ee291bad598c9acf3a58625a4bb4015ffd40937d7`; provisioning intent `WindowsNamedPipeAclProvisioningIntent.cs` / `51a19b3a601b1134761537bf4d102d431ef04b56abd884a3180c9f79e3f777fc`; D1R profile `DurableProductionProfile.cs` / `636ef191393395981e991d891d4f7d43924bf1dd6230e12fef031e183d961ed1`; and SID parser `WindowsSid.cs` / `438ff2fe3f1d74fc33ebe910d0b8525a312f9ec71af62186e60f67f02bbf7f09`. P1 must bind the D1R durable-profile digest and exact service SID to the existing provisioning intent, never create a substitute trust root.
- Contract evidence required at closeout: every candidate serving instance is created through `CreateNamedPipeW` with non-inheritable `SECURITY_ATTRIBUTES`, `PIPE_REJECT_REMOTE_CLIENTS`, and first-instance bootstrap; the exact returned `SafePipeHandle` receives `GetSecurityInfo` owner/group/DACL/SACL/control/native-ACE readback before `ConnectNamedPipe` or accept. Readback must require exact service-SID owner/group, readable absent SACL, control `0x9004`, and exactly ordered allow ACEs user `0x0012019B`, then service `0x001F019F`. Later instances repeat independently; name reopen, different handle, cached ACL blessing, unreadable-SACL success, accept-before-readback, or failure cleanup omission is NO-GO.
- Required eventual acceptance is exactly 16 new tests (8 host + 8 readback), target 16/16, full Broker 129/129, schema 22/13/14/236, two Release builds 0/0, zero-stdout exact-`W24FS001`-stderr/23 smoke, exact manifests, source/PE/ABI/no-wiring/frozen-root/residue receipts, final STOPPED handoff, and a fresh independent audit. There is no advance claim before those receipts exist.
- D1R remains closed GO and old D1 remains retained `STOPPED / NO-GO`; C1/C2 remain closed GO and the DAG-rebase scoped GO/P2=1 remains historical. I1 is an external privileged-root preflight blocker but is not published or started; C3 and W1 are not published. `Program`, `BrokerPolicy`, existing host and ServiceHost wiring, `CurrentUserOnly` positive evidence, listener activation, `Running`, path/project access and authority remain outside P1 and NO-GO.

## 19. Authority and evidence boundary

- This index is coordination metadata only and grants no authority.
- Scheduling update: on `2026-08-28` the user reported `100%` and removed quota-based scheduling. Historical package quota entries remain provenance only; scope and safety gates are unchanged.
- Status DTOs are presentation identities, not issuers.
- Machine pass, visual evidence, user verdict, L3 and L4 remain separate.
- No legacy Unity receipt proves Desktop/Broker/Worker behavior.
- No test-issued handle, test pipe, scratch project or decoded message may enter production admission.
